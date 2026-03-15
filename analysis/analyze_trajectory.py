"""
궤적 분석 스크립트 (ISMAR 로깅 시스템)
- 속도 프로필 (조건별 비교 라인 차트)
- 경로 맵/히트맵 (2D scatter, 속도 색상)
- 정체 구간 분석 (speed < 0.15 m/s, > 2초)
- 웨이포인트 체류 시간 (조건별 바 차트)
- 경로 길이 비교 (Glass vs Hybrid, 에러바 포함)
- 경로 효율 (실제/이상 경로 길이 비율)
- 출력: analysis/output/trajectory_*.png
"""

import sys
import argparse
import warnings
from pathlib import Path

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib
from scipy import stats

from plot_style import apply_style, save_fig, violin_with_dots, COLORS_COND, COND_LABELS, DPI
from stat_utils import paired_comparison

apply_style()
warnings.filterwarnings("ignore", category=FutureWarning)

# ──────────────────────────────────────────────
# 경로 설정
# ──────────────────────────────────────────────

SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR.parent / "data"
RAW_DIR = DATA_DIR / "raw"
OUTPUT_DIR = SCRIPT_DIR / "output"
OUTPUT_DIR.mkdir(exist_ok=True)

CONDITIONS = ["glass_only", "hybrid"]
CONDITION_LABELS = ["Glass Only", "Hybrid"]
CONDITION_COLORS = {"glass_only": "#3498db", "hybrid": "#2ecc71"}

WAYPOINTS = [f"WP{i:02d}" for i in range(9)]
TRIGGER_WAYPOINTS = ["WP03", "WP06"]

# nav_trace 용
HESITATION_SPEED_THRESHOLD = 0.15  # m/s
HESITATION_MIN_DURATION = 2.0  # seconds


# ──────────────────────────────────────────────
# 데이터 로드
# ──────────────────────────────────────────────

def load_all_nav_traces(data_dir: Path, allow_demo: bool = False):
    """모든 nav_trace CSV를 로드하여 조건별로 그룹핑.

    Returns:
        dict: {condition: list of DataFrames}
        sessions_meta: list of (participant_id, condition, filepath)
    """
    from trajectory_utils import (
        load_nav_trace, find_session_files, generate_demo_data
    )

    sessions = find_session_files(str(data_dir))

    # nav_trace가 없으면 데모 생성
    has_nav = any("nav_trace" in v for v in sessions.values())
    if not sessions or not has_nav:
        if allow_demo:
            print("[경고] nav_trace 파일 없음. 데모 데이터를 생성합니다.")
            generate_demo_data(str(data_dir))
            sessions = find_session_files(str(data_dir))
        else:
            print(f"[오류] {data_dir}에 nav_trace 파일 없음.")
            print("  데모로 실행하려면 --demo 플래그를 사용하세요.")
            sys.exit(1)

    result = {"glass_only": [], "hybrid": []}
    sessions_meta = []

    for prefix, files in sessions.items():
        if "nav_trace" not in files:
            continue
        nav_df = load_nav_trace(files["nav_trace"])
        if nav_df.empty:
            continue

        # prefix에서 condition 추출: P01_glass_only_Set1_...
        parts = prefix.split("_")
        cond = None
        for c in CONDITIONS:
            if c in prefix:
                cond = c
                break
        if cond is None:
            # glass_only는 두 단어이므로 조합 확인
            if "glass" in parts and "only" in parts:
                cond = "glass_only"
            elif "hybrid" in parts:
                cond = "hybrid"
            else:
                continue

        pid = parts[0] if parts else "unknown"
        nav_df["participant_id"] = pid
        nav_df["condition"] = cond
        result[cond].append(nav_df)
        sessions_meta.append((pid, cond, files["nav_trace"]))

    return result, sessions_meta


# ──────────────────────────────────────────────
# 분석 함수
# ──────────────────────────────────────────────

def analyze_speed_profile(nav_by_cond: dict) -> dict:
    """조건별 시간에 따른 속도 프로필 계산."""
    profiles = {}
    for cond in CONDITIONS:
        dfs = nav_by_cond.get(cond, [])
        if not dfs:
            continue
        # 모든 세션을 0초 기준으로 정규화
        all_speeds = []
        for df in dfs:
            if df.empty:
                continue
            df = df.sort_values("timestamp")
            t0 = df["timestamp"].iloc[0]
            elapsed = (df["timestamp"] - t0).dt.total_seconds()
            speeds = df["speed_ms"].values
            all_speeds.append(pd.DataFrame({
                "elapsed_s": elapsed.values, "speed_ms": speeds}))

        if all_speeds:
            combined = pd.concat(all_speeds, ignore_index=True)
            # 10초 빈으로 리샘플링
            combined["time_bin"] = (combined["elapsed_s"] // 10) * 10
            profile = combined.groupby("time_bin")["speed_ms"].agg(
                ["mean", "std"]).reset_index()
            profiles[cond] = profile

    return profiles


def identify_hesitation_zones(nav_by_cond: dict) -> pd.DataFrame:
    """정체 구간 식별: speed < threshold, duration > min_duration."""
    zones = []
    for cond in CONDITIONS:
        for df in nav_by_cond.get(cond, []):
            if df.empty:
                continue
            df = df.sort_values("timestamp").copy()
            pid = df["participant_id"].iloc[0] if "participant_id" in df.columns else "unknown"

            # 저속 상태 플래그
            df["is_slow"] = df["speed_ms"] < HESITATION_SPEED_THRESHOLD

            # 연속 저속 구간 식별
            df["group"] = (df["is_slow"] != df["is_slow"].shift()).cumsum()
            slow_groups = df[df["is_slow"]].groupby("group")

            for _, grp in slow_groups:
                if len(grp) < 2:
                    continue
                t_start = grp["timestamp"].iloc[0]
                t_end = grp["timestamp"].iloc[-1]
                duration = (t_end - t_start).total_seconds()
                if duration >= HESITATION_MIN_DURATION:
                    mean_x = grp["player_x"].mean()
                    mean_z = grp["player_z"].mean()
                    near_wp = grp["current_wp_id"].mode()
                    zones.append({
                        "participant_id": pid,
                        "condition": cond,
                        "start_time": t_start,
                        "duration_s": round(duration, 1),
                        "center_x": round(mean_x, 1),
                        "center_z": round(mean_z, 1),
                        "near_waypoint": near_wp.iloc[0] if not near_wp.empty else "",
                    })

    return pd.DataFrame(zones)


def compute_waypoint_dwell_time(nav_by_cond: dict) -> pd.DataFrame:
    """각 웨이포인트 근처(5m) 체류 시간 (조건별)."""
    from trajectory_utils import WAYPOINT_POSITIONS

    results = []
    for cond in CONDITIONS:
        for df in nav_by_cond.get(cond, []):
            if df.empty:
                continue
            df = df.sort_values("timestamp")
            pid = df["participant_id"].iloc[0] if "participant_id" in df.columns else "unknown"
            dt = 0.5  # 2Hz 샘플링 간격

            for wp, (wx, wy, wz) in WAYPOINT_POSITIONS.items():
                near_mask = np.sqrt(
                    (df["player_x"] - wx) ** 2 +
                    (df["player_z"] - wz) ** 2
                ) < 5.0
                dwell_s = near_mask.sum() * dt
                results.append({
                    "participant_id": pid,
                    "condition": cond,
                    "waypoint_id": wp,
                    "dwell_time_s": round(dwell_s, 1),
                })

    return pd.DataFrame(results)


def compute_path_stats(nav_by_cond: dict) -> pd.DataFrame:
    """참가자별 경로 길이 및 효율 산출."""
    from trajectory_utils import compute_path_length, WAYPOINT_POSITIONS

    # 이상 경로 길이 (웨이포인트 직선 연결)
    wp_order = [f"WP{i:02d}" for i in range(9)]
    ideal_length = 0.0
    for i in range(len(wp_order) - 1):
        a = WAYPOINT_POSITIONS[wp_order[i]]
        b = WAYPOINT_POSITIONS[wp_order[i + 1]]
        ideal_length += np.sqrt((a[0] - b[0]) ** 2 + (a[2] - b[2]) ** 2)

    results = []
    for cond in CONDITIONS:
        for df in nav_by_cond.get(cond, []):
            if df.empty:
                continue
            pid = df["participant_id"].iloc[0] if "participant_id" in df.columns else "unknown"
            actual = compute_path_length(df)
            efficiency = ideal_length / actual if actual > 0 else 0
            results.append({
                "participant_id": pid,
                "condition": cond,
                "actual_path_m": round(actual, 1),
                "ideal_path_m": round(ideal_length, 1),
                "efficiency": round(efficiency, 3),
            })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 시각화
# ──────────────────────────────────────────────

def plot_speed_profile(profiles: dict):
    """조건별 속도 프로필 비교 라인 차트."""
    fig, ax = plt.subplots(figsize=(12, 5))

    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        if cond not in profiles:
            continue
        p = profiles[cond]
        ax.plot(p["time_bin"], p["mean"], "-", label=label,
                color=CONDITION_COLORS[cond], linewidth=2)
        ax.fill_between(p["time_bin"],
                        p["mean"] - p["std"],
                        p["mean"] + p["std"],
                        alpha=0.2, color=CONDITION_COLORS[cond])

    ax.set_xlabel("경과 시간 (s)")
    ax.set_ylabel("이동 속도 (m/s)")
    ax.set_title("조건별 속도 프로필")
    ax.legend()
    ax.grid(axis="y", alpha=0.3)
    ax.set_ylim(bottom=0)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "trajectory_speed_profile")


def plot_path_heatmap(nav_by_cond: dict):
    """2D 경로 scatter (속도 색상 코딩)."""
    fig, axes = plt.subplots(1, 2, figsize=(14, 7), sharey=True)

    for idx, (cond, label) in enumerate(zip(CONDITIONS, CONDITION_LABELS)):
        ax = axes[idx]
        dfs = nav_by_cond.get(cond, [])
        if not dfs:
            ax.set_title(f"{label} (데이터 없음)")
            continue

        all_x, all_z, all_speed = [], [], []
        for df in dfs:
            all_x.extend(df["player_x"].values)
            all_z.extend(df["player_z"].values)
            all_speed.extend(df["speed_ms"].values)

        sc = ax.scatter(all_x, all_z, c=all_speed, cmap="RdYlGn",
                       s=2, alpha=0.5, vmin=0, vmax=2.0)
        fig.colorbar(sc, ax=ax, label="속도 (m/s)", shrink=0.8)

        # 웨이포인트 표시
        from trajectory_utils import WAYPOINT_POSITIONS
        for wp, (wx, wy, wz) in WAYPOINT_POSITIONS.items():
            marker_color = "#e74c3c" if wp in TRIGGER_WAYPOINTS else "#333333"
            ax.plot(wx, wz, "^", color=marker_color, markersize=8)
            ax.annotate(wp, (wx, wz), textcoords="offset points",
                       xytext=(5, 5), fontsize=7)

        ax.set_xlabel("X (m)")
        ax.set_ylabel("Z (m)")
        ax.set_title(f"{label} - 경로 히트맵")
        ax.set_aspect("equal")
        ax.grid(alpha=0.2)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "trajectory_path_heatmap")


def plot_hesitation_zones(nav_by_cond: dict, zones_df: pd.DataFrame):
    """정체 구간 맵 시각화."""
    fig, ax = plt.subplots(figsize=(10, 8))

    # 배경: 전체 경로 (연한 회색)
    for cond in CONDITIONS:
        for df in nav_by_cond.get(cond, []):
            ax.plot(df["player_x"], df["player_z"], "-",
                   color="#cccccc", alpha=0.2, linewidth=0.5)

    # 웨이포인트
    from trajectory_utils import WAYPOINT_POSITIONS
    for wp, (wx, wy, wz) in WAYPOINT_POSITIONS.items():
        marker_color = "#e74c3c" if wp in TRIGGER_WAYPOINTS else "#333333"
        ax.plot(wx, wz, "^", color=marker_color, markersize=10)
        ax.annotate(wp, (wx, wz), textcoords="offset points",
                   xytext=(8, 5), fontsize=8)

    # 정체 구간
    if not zones_df.empty:
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            cond_zones = zones_df[zones_df["condition"] == cond]
            if cond_zones.empty:
                continue
            sizes = cond_zones["duration_s"] * 15
            ax.scatter(cond_zones["center_x"], cond_zones["center_z"],
                      s=sizes, alpha=0.5, label=f"{label} 정체 구간",
                      color=CONDITION_COLORS[cond], edgecolors="black",
                      linewidth=0.5)

    ax.set_xlabel("X (m)")
    ax.set_ylabel("Z (m)")
    ax.set_title("정체 구간 분포 (원 크기 = 체류 시간)")
    handles, labels = ax.get_legend_handles_labels()
    if handles:
        ax.legend()
    ax.set_aspect("equal")
    ax.grid(alpha=0.2)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "trajectory_hesitation_zones")


def plot_waypoint_dwell_time(dwell_df: pd.DataFrame):
    """웨이포인트별 체류 시간 조건 비교 바 차트."""
    if dwell_df.empty:
        return

    fig, ax = plt.subplots(figsize=(12, 6))
    x = np.arange(len(WAYPOINTS))
    width = 0.35

    for i, (cond, label) in enumerate(zip(CONDITIONS, CONDITION_LABELS)):
        means = []
        errs = []
        for wp in WAYPOINTS:
            vals = dwell_df[(dwell_df["condition"] == cond) &
                           (dwell_df["waypoint_id"] == wp)]["dwell_time_s"]
            means.append(vals.mean() if len(vals) > 0 else 0)
            errs.append(vals.std() if len(vals) > 1 else 0)
        ax.bar(x + i * width, means, width, yerr=errs,
              label=label, color=CONDITION_COLORS[cond], capsize=3)

    # 트리거 WP 강조
    for wp in TRIGGER_WAYPOINTS:
        wp_idx = WAYPOINTS.index(wp)
        ax.axvspan(wp_idx - 0.4, wp_idx + width + 0.4,
                  alpha=0.1, color="red")

    ax.set_xlabel("웨이포인트")
    ax.set_ylabel("체류 시간 (s)")
    ax.set_title("웨이포인트별 체류 시간 (조건 비교)")
    ax.set_xticks(x + width / 2)
    ax.set_xticklabels(WAYPOINTS, rotation=45)
    ax.legend()
    ax.grid(axis="y", alpha=0.3)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "trajectory_dwell_time")


def plot_path_length_comparison(stats_df: pd.DataFrame):
    """조건별 경로 길이 비교 바 차트 (에러바 포함)."""
    if stats_df.empty:
        return

    fig, axes = plt.subplots(1, 2, figsize=(12, 5))

    # 1. 경로 길이 비교
    ax = axes[0]
    means, sds = [], []
    for cond in CONDITIONS:
        vals = stats_df[stats_df["condition"] == cond]["actual_path_m"]
        means.append(vals.mean())
        sds.append(vals.std())
    bars = ax.bar(CONDITION_LABELS, means, yerr=sds, capsize=5,
                  color=[CONDITION_COLORS[c] for c in CONDITIONS])

    # 이상 경로 수평선
    ideal = stats_df["ideal_path_m"].iloc[0] if not stats_df.empty else 0
    ax.axhline(y=ideal, color="red", linestyle="--", alpha=0.7,
              label=f"이상 경로 ({ideal:.0f}m)")

    for bar, m in zip(bars, means):
        ax.text(bar.get_x() + bar.get_width() / 2., bar.get_height() + 2,
               f"{m:.0f}m", ha="center", va="bottom", fontsize=10)

    ax.set_ylabel("경로 길이 (m)")
    ax.set_title("조건별 경로 길이 비교")
    ax.legend()

    # 통계 검정
    glass_vals = stats_df[stats_df["condition"] == "glass_only"]["actual_path_m"].values
    hybrid_vals = stats_df[stats_df["condition"] == "hybrid"]["actual_path_m"].values
    if len(glass_vals) > 1 and len(hybrid_vals) > 1:
        min_len = min(len(glass_vals), len(hybrid_vals))
        t_stat, p_val = stats.ttest_ind(glass_vals[:min_len], hybrid_vals[:min_len])
        ax.text(0.5, 0.95, f"t={t_stat:.2f}, p={p_val:.3f}",
               transform=ax.transAxes, ha="center", va="top", fontsize=9,
               bbox=dict(boxstyle="round", facecolor="wheat", alpha=0.8))

    # 2. 경로 효율 비교
    ax = axes[1]
    means_eff, sds_eff = [], []
    for cond in CONDITIONS:
        vals = stats_df[stats_df["condition"] == cond]["efficiency"]
        means_eff.append(vals.mean())
        sds_eff.append(vals.std())
    bars = ax.bar(CONDITION_LABELS, means_eff, yerr=sds_eff, capsize=5,
                  color=[CONDITION_COLORS[c] for c in CONDITIONS])

    ax.axhline(y=1.0, color="red", linestyle="--", alpha=0.7,
              label="이상 효율 (1.0)")

    for bar, m in zip(bars, means_eff):
        ax.text(bar.get_x() + bar.get_width() / 2., bar.get_height() + 0.01,
               f"{m:.2f}", ha="center", va="bottom", fontsize=10)

    ax.set_ylabel("경로 효율 (이상/실제)")
    ax.set_title("조건별 경로 효율")
    ax.legend()
    ax.set_ylim(0, 1.2)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "trajectory_path_comparison")


# ──────────────────────────────────────────────
# 메인
# ──────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="궤적 분석 (ISMAR 로깅)")
    parser.add_argument("--data-dir", type=str, default=None,
                        help="데이터 디렉토리 (기본: data/raw/)")
    parser.add_argument("--demo", action="store_true",
                        help="데이터 파일이 없을 때 데모 데이터로 실행")
    args = parser.parse_args()

    data_dir = Path(args.data_dir) if args.data_dir else RAW_DIR

    print("=" * 60)
    print("궤적 분석 (ISMAR 로깅 시스템)")
    print("=" * 60)

    # 데이터 로드
    nav_by_cond, sessions_meta = load_all_nav_traces(data_dir, allow_demo=args.demo)
    total_sessions = sum(len(v) for v in nav_by_cond.values())
    total_samples = sum(len(df) for dfs in nav_by_cond.values() for df in dfs)
    print(f"세션 수: {total_sessions}")
    print(f"총 nav_trace 샘플 수: {total_samples}")
    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        print(f"  {label}: {len(nav_by_cond.get(cond, []))}개 세션")

    # ── 분석 ──

    # 1. 속도 프로필
    print("\n--- 속도 프로필 분석 ---")
    profiles = analyze_speed_profile(nav_by_cond)
    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        if cond in profiles:
            p = profiles[cond]
            print(f"  {label}: 평균 속도 {p['mean'].mean():.2f} m/s, "
                  f"SD {p['std'].mean():.2f}")

    # 2. 정체 구간
    print("\n--- 정체 구간 분석 ---")
    zones_df = identify_hesitation_zones(nav_by_cond)
    if not zones_df.empty:
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            cond_zones = zones_df[zones_df["condition"] == cond]
            print(f"  {label}: {len(cond_zones)}개 정체 구간, "
                  f"평균 {cond_zones['duration_s'].mean():.1f}s")
            if not cond_zones.empty:
                top_wps = cond_zones["near_waypoint"].value_counts().head(3)
                print(f"    빈번 정체 WP: {', '.join(f'{wp}({cnt})' for wp, cnt in top_wps.items())}")
    else:
        print("  정체 구간 없음")

    # 3. 웨이포인트 체류 시간
    print("\n--- 웨이포인트 체류 시간 ---")
    dwell_df = compute_waypoint_dwell_time(nav_by_cond)
    if not dwell_df.empty:
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            cond_dwell = dwell_df[dwell_df["condition"] == cond]
            for wp in TRIGGER_WAYPOINTS:
                wp_vals = cond_dwell[cond_dwell["waypoint_id"] == wp]["dwell_time_s"]
                if not wp_vals.empty:
                    print(f"  {label} @ {wp} (트리거): "
                          f"M={wp_vals.mean():.1f}s, SD={wp_vals.std():.1f}s")

    # 4. 경로 길이 및 효율
    print("\n--- 경로 길이/효율 ---")
    stats_df = compute_path_stats(nav_by_cond)
    if not stats_df.empty:
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            cond_stats = stats_df[stats_df["condition"] == cond]
            print(f"  {label}: 경로 길이 M={cond_stats['actual_path_m'].mean():.0f}m "
                  f"(SD={cond_stats['actual_path_m'].std():.0f}), "
                  f"효율 {cond_stats['efficiency'].mean():.3f}")
        print(f"  이상 경로: {stats_df['ideal_path_m'].iloc[0]:.0f}m")

    # ── 시각화 ──
    print(f"\n=== 시각화 ===")
    plot_speed_profile(profiles)
    plot_path_heatmap(nav_by_cond)
    plot_hesitation_zones(nav_by_cond, zones_df)
    plot_waypoint_dwell_time(dwell_df)
    plot_path_length_comparison(stats_df)

    # ── 결과 CSV 저장 ──
    if not zones_df.empty:
        zones_df.to_csv(OUTPUT_DIR / "trajectory_hesitation_zones.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'trajectory_hesitation_zones.csv'} 저장")

    if not dwell_df.empty:
        dwell_df.to_csv(OUTPUT_DIR / "trajectory_dwell_time.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'trajectory_dwell_time.csv'} 저장")

    if not stats_df.empty:
        stats_df.to_csv(OUTPUT_DIR / "trajectory_path_stats.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'trajectory_path_stats.csv'} 저장")

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
