"""
앵커 재인식 견고성 분석 스크립트 (ISMAR 로깅 시스템)
- 웨이포인트별 재인식 성공/타임아웃/실패율 (누적 바)
- 재인식 소요 시간 분포 (웨이포인트별 박스플롯)
- 실패 시 SLAM 상태 (파이 차트)
- 앵커 복구 타임라인
- 시스템 상태 상관 (FPS vs 트래킹 상태)
- 출력: analysis/output/anchor_*.png
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

from plot_style import apply_style, save_fig, DPI

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

WAYPOINTS = [f"WP{i:02d}" for i in range(9)]

# AnchorRelocState enum (SpatialAnchorManager.cs)
RELOC_STATES = {
    "Tracking": "Success",
    "TimedOut": "Timed Out",
    "LoadFailed": "Load Failed",
    "InProgress": "In Progress",
}
SUCCESS_STATE = "Tracking"
FAILURE_STATES = ["TimedOut", "LoadFailed"]

STATE_COLORS = {
    "Tracking": "#2ecc71",
    "TimedOut": "#f39c12",
    "LoadFailed": "#e74c3c",
}


# ──────────────────────────────────────────────
# 데이터 로드
# ──────────────────────────────────────────────

def load_all_data(data_dir: Path, allow_fallback: bool = False):
    """anchor_reloc + system_health CSV를 모두 로드.

    Returns:
        reloc_dfs: list of DataFrames (anchor_reloc)
        health_dfs: list of DataFrames (system_health)
    """
    from trajectory_utils import (
        load_anchor_reloc, load_system_health,
        find_session_files, generate_fallback_data
    )

    sessions = find_session_files(str(data_dir))

    has_reloc = any("anchor_reloc" in v for v in sessions.values())
    if not sessions or not has_reloc:
        if allow_fallback:
            print("[경고] anchor_reloc 파일 없음. Fallback 데이터를 생성합니다.")
            generate_fallback_data(str(data_dir))
            sessions = find_session_files(str(data_dir))
        else:
            print(f"[오류] {data_dir}에 anchor_reloc 파일 없음.")
            print("  fallback으로 실행하려면 --fallback 플래그를 사용하세요.")
            sys.exit(1)

    reloc_dfs = []
    health_dfs = []

    for prefix, files in sessions.items():
        if "anchor_reloc" in files:
            df = load_anchor_reloc(files["anchor_reloc"])
            if not df.empty:
                df["session"] = prefix
                reloc_dfs.append(df)

        if "system_health" in files:
            hdf = load_system_health(files["system_health"])
            if not hdf.empty:
                hdf["session"] = prefix
                health_dfs.append(hdf)

    return reloc_dfs, health_dfs


# ──────────────────────────────────────────────
# 분석 함수
# ──────────────────────────────────────────────

def analyze_reloc_rates(reloc_dfs: list) -> pd.DataFrame:
    """웨이포인트별 재인식 성공/타임아웃/실패율 산출."""
    if not reloc_dfs:
        return pd.DataFrame()

    all_reloc = pd.concat(reloc_dfs, ignore_index=True)
    # InProgress 행 제거 (최종 결과만)
    final = all_reloc[all_reloc["state"] != "InProgress"].copy()

    results = []
    for wp in WAYPOINTS:
        wp_data = final[final["waypoint_id"] == wp]
        total = len(wp_data)
        if total == 0:
            results.append({
                "waypoint_id": wp, "total": 0,
                "success": 0, "timeout": 0, "load_failed": 0,
                "success_rate": 0, "timeout_rate": 0, "fail_rate": 0,
            })
            continue

        success = len(wp_data[wp_data["state"] == "Tracking"])
        timeout = len(wp_data[wp_data["state"] == "TimedOut"])
        load_failed = len(wp_data[wp_data["state"] == "LoadFailed"])

        results.append({
            "waypoint_id": wp,
            "total": total,
            "success": success,
            "timeout": timeout,
            "load_failed": load_failed,
            "success_rate": round(success / total, 3),
            "timeout_rate": round(timeout / total, 3),
            "fail_rate": round(load_failed / total, 3),
        })

    return pd.DataFrame(results)


def analyze_reloc_times(reloc_dfs: list) -> pd.DataFrame:
    """웨이포인트별 재인식 소요 시간 분포."""
    if not reloc_dfs:
        return pd.DataFrame()

    all_reloc = pd.concat(reloc_dfs, ignore_index=True)
    # 최종 결과만 (InProgress 제외)
    final = all_reloc[all_reloc["state"] != "InProgress"].copy()

    results = []
    for wp in WAYPOINTS:
        wp_data = final[final["waypoint_id"] == wp]
        if wp_data.empty:
            continue
        for _, row in wp_data.iterrows():
            results.append({
                "waypoint_id": wp,
                "state": row["state"],
                "elapsed_s": row["elapsed_s"],
                "is_critical": row.get("is_critical", False),
                "session": row.get("session", ""),
            })

    return pd.DataFrame(results)


def analyze_slam_at_failure(reloc_dfs: list) -> pd.DataFrame:
    """실패(TimedOut/LoadFailed) 시 SLAM 상태 분포."""
    if not reloc_dfs:
        return pd.DataFrame()

    all_reloc = pd.concat(reloc_dfs, ignore_index=True)
    failures = all_reloc[
        all_reloc["state"].isin(FAILURE_STATES) &
        (all_reloc["slam_reason"] != "")
    ].copy()

    if failures.empty:
        return pd.DataFrame()

    slam_counts = failures["slam_reason"].value_counts().reset_index()
    slam_counts.columns = ["slam_reason", "count"]
    slam_counts["percentage"] = round(
        slam_counts["count"] / slam_counts["count"].sum() * 100, 1)

    return slam_counts


def analyze_recovery_timeline(reloc_dfs: list) -> pd.DataFrame:
    """앵커 복구 타임라인 (InProgress -> Tracking 전환)."""
    if not reloc_dfs:
        return pd.DataFrame()

    all_reloc = pd.concat(reloc_dfs, ignore_index=True)
    all_reloc = all_reloc.sort_values(["session", "waypoint_id", "timestamp"])

    timelines = []
    for (sess, wp), grp in all_reloc.groupby(["session", "waypoint_id"]):
        progress_rows = grp[grp["state"] == "InProgress"]
        final_row = grp[grp["state"] != "InProgress"]

        if final_row.empty:
            continue

        final_state = final_row.iloc[-1]["state"]
        final_time = final_row.iloc[-1]["elapsed_s"]

        # 중간 체크포인트
        checkpoints = []
        for _, row in progress_rows.iterrows():
            checkpoints.append({
                "elapsed_s": row["elapsed_s"],
                "slam_reason": row.get("slam_reason", ""),
            })

        timelines.append({
            "session": sess,
            "waypoint_id": wp,
            "final_state": final_state,
            "total_elapsed_s": final_time,
            "n_checkpoints": len(checkpoints),
            "checkpoints_slam": ";".join(
                str(c.get("slam_reason", "") or "") for c in checkpoints),
        })

    return pd.DataFrame(timelines)


def analyze_health_correlation(reloc_dfs: list, health_dfs: list) -> pd.DataFrame:
    """시스템 상태(FPS)와 트래킹 상태의 상관 분석."""
    if not health_dfs:
        return pd.DataFrame()

    all_health = pd.concat(health_dfs, ignore_index=True)

    # 트래킹 상태별 FPS 비교
    ready = all_health[all_health["tracking_state"] == "ready"]["fps"]
    not_ready = all_health[all_health["tracking_state"] == "not_ready"]["fps"]

    results = []
    if len(ready) > 0:
        results.append({
            "tracking_state": "ready",
            "mean_fps": round(ready.mean(), 1),
            "std_fps": round(ready.std(), 1),
            "n": len(ready),
        })
    if len(not_ready) > 0:
        results.append({
            "tracking_state": "not_ready",
            "mean_fps": round(not_ready.mean(), 1),
            "std_fps": round(not_ready.std(), 1),
            "n": len(not_ready),
        })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 시각화
# ──────────────────────────────────────────────

def plot_reloc_rates(rates_df: pd.DataFrame):
    """웨이포인트별 재인식 결과 누적 바 차트."""
    if rates_df.empty:
        return

    fig, ax = plt.subplots(figsize=(12, 6))
    x = np.arange(len(rates_df))
    width = 0.6

    # 누적 바
    bottom_timeout = rates_df["success_rate"].values
    bottom_fail = bottom_timeout + rates_df["timeout_rate"].values

    ax.bar(x, rates_df["success_rate"], width,
          label="Success (Tracking)", color=STATE_COLORS["Tracking"])
    ax.bar(x, rates_df["timeout_rate"], width,
          bottom=bottom_timeout,
          label="Timed Out (TimedOut)", color=STATE_COLORS["TimedOut"])
    ax.bar(x, rates_df["fail_rate"], width,
          bottom=bottom_fail,
          label="Load Failed (LoadFailed)", color=STATE_COLORS["LoadFailed"])

    # 성공률 텍스트
    for i, row in rates_df.iterrows():
        if row["total"] > 0:
            ax.text(i, row["success_rate"] / 2, f"{row['success_rate']:.0%}",
                   ha="center", va="center", fontsize=9, fontweight="bold",
                   color="white")

    ax.set_xlabel("Waypoint")
    ax.set_ylabel("Ratio")
    ax.set_title("Anchor Relocalization Result Distribution by Waypoint")
    ax.set_xticks(x)
    ax.set_xticklabels(rates_df["waypoint_id"], rotation=45)
    ax.set_ylim(0, 1.05)
    ax.legend(loc="upper right")
    ax.grid(axis="y", alpha=0.3)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "anchor_reloc_rates")


def plot_reloc_time_distribution(times_df: pd.DataFrame):
    """웨이포인트별 재인식 소요 시간 박스플롯."""
    if times_df.empty:
        return

    fig, ax = plt.subplots(figsize=(12, 6))

    # 성공한 경우만 시간 분포 표시
    success_times = times_df[times_df["state"] == "Tracking"]

    wp_data = []
    wp_labels = []
    for wp in WAYPOINTS:
        vals = success_times[success_times["waypoint_id"] == wp]["elapsed_s"]
        if not vals.empty:
            wp_data.append(vals.values)
            wp_labels.append(wp)

    if wp_data:
        bp = ax.boxplot(wp_data, tick_labels=wp_labels, patch_artist=True)
        for patch in bp["boxes"]:
            patch.set_facecolor(STATE_COLORS["Tracking"])
            patch.set_alpha(0.7)

        # 타임아웃/실패 마커
        for state, marker, color in [("TimedOut", "x", STATE_COLORS["TimedOut"]),
                                      ("LoadFailed", "D", STATE_COLORS["LoadFailed"])]:
            state_data = times_df[times_df["state"] == state]
            for i, wp in enumerate(wp_labels):
                vals = state_data[state_data["waypoint_id"] == wp]["elapsed_s"]
                if not vals.empty:
                    ax.scatter([i + 1] * len(vals), vals, marker=marker,
                             color=color, s=50, zorder=3, label=state if i == 0 else "")

    ax.set_xlabel("Waypoint")
    ax.set_ylabel("Relocalization Time (s)")
    ax.set_title("Relocalization Time Distribution by Waypoint")
    ax.grid(axis="y", alpha=0.3)

    # 범례 중복 제거
    handles, labels = ax.get_legend_handles_labels()
    by_label = dict(zip(labels, handles))
    if by_label:
        ax.legend(by_label.values(), by_label.keys())

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "anchor_reloc_time_dist")


def plot_slam_failure_reasons(slam_df: pd.DataFrame):
    """실패 시 SLAM 상태 파이 차트."""
    if slam_df.empty:
        return

    fig, ax = plt.subplots(figsize=(8, 6))

    slam_labels = {
        "feature_insufficient": "Insufficient Features",
        "relocalizing": "Relocalizing",
        "limited_tracking": "Limited Tracking",
        "file_not_found": "File Not Found",
    }

    labels = [slam_labels.get(r, r) for r in slam_df["slam_reason"]]
    colors = plt.cm.Set2(np.linspace(0, 1, len(slam_df)))

    wedges, texts, autotexts = ax.pie(
        slam_df["count"], labels=labels, autopct="%1.0f%%",
        colors=colors, startangle=90, pctdistance=0.75,
        textprops={"fontsize": 10})

    for autotext in autotexts:
        autotext.set_fontsize(10)
        autotext.set_fontweight("bold")

    ax.set_title("SLAM State Distribution at Anchor Relocalization Failure")

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "anchor_slam_failure")


def plot_recovery_timeline(timeline_df: pd.DataFrame):
    """앵커 복구 타임라인 (세션별 수평 바)."""
    if timeline_df.empty:
        return

    fig, ax = plt.subplots(figsize=(14, 6))

    # 세션별 웨이포인트 타임라인
    sessions = timeline_df["session"].unique()[:5]  # 최대 5세션 표시
    y_offset = 0

    for sess in sessions:
        sess_data = timeline_df[timeline_df["session"] == sess].sort_values("waypoint_id")
        for _, row in sess_data.iterrows():
            color = STATE_COLORS.get(row["final_state"], "#999999")
            wp_x = WAYPOINTS.index(row["waypoint_id"]) if row["waypoint_id"] in WAYPOINTS else 0

            bar = ax.barh(y_offset, row["total_elapsed_s"],
                         left=wp_x * 35, height=0.6,
                         color=color, alpha=0.8, edgecolor="black",
                         linewidth=0.5)

            # 시간 텍스트
            if row["total_elapsed_s"] > 3:
                ax.text(wp_x * 35 + row["total_elapsed_s"] / 2, y_offset,
                       f"{row['total_elapsed_s']:.0f}s",
                       ha="center", va="center", fontsize=7)

        # 세션 라벨
        short_sess = sess.split("_")[0]  # P01 등
        ax.text(-10, y_offset, short_sess, ha="right", va="center", fontsize=8)
        y_offset += 1

    ax.set_xlabel("Waypoint Segment + Elapsed Time (s)")
    ax.set_ylabel("")
    ax.set_title("Anchor Relocalization Timeline by Session")

    # 범례
    from matplotlib.patches import Patch
    legend_elements = [
        Patch(facecolor=STATE_COLORS["Tracking"], label="Success"),
        Patch(facecolor=STATE_COLORS["TimedOut"], label="Timed Out"),
        Patch(facecolor=STATE_COLORS["LoadFailed"], label="Load Failed"),
    ]
    ax.legend(handles=legend_elements, loc="upper right")
    ax.grid(axis="x", alpha=0.3)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "anchor_recovery_timeline")


def plot_health_correlation(health_dfs: list):
    """FPS vs 트래킹 상태 시각화."""
    if not health_dfs:
        return

    all_health = pd.concat(health_dfs, ignore_index=True)

    fig, axes = plt.subplots(1, 2, figsize=(14, 5))

    # 1. 트래킹 상태별 FPS 분포
    ax = axes[0]
    ready_fps = all_health[all_health["tracking_state"] == "ready"]["fps"]
    not_ready_fps = all_health[all_health["tracking_state"] == "not_ready"]["fps"]

    data = []
    labels = []
    if not ready_fps.empty:
        data.append(ready_fps.values)
        labels.append(f"Ready\n(n={len(ready_fps)})")
    if not not_ready_fps.empty:
        data.append(not_ready_fps.values)
        labels.append(f"Not Ready\n(n={len(not_ready_fps)})")

    if data:
        bp = ax.boxplot(data, tick_labels=labels, patch_artist=True)
        colors = [STATE_COLORS["Tracking"], STATE_COLORS["TimedOut"]]
        for patch, color in zip(bp["boxes"], colors[:len(data)]):
            patch.set_facecolor(color)
            patch.set_alpha(0.7)

    ax.set_ylabel("FPS")
    ax.set_title("FPS Distribution by Tracking State")
    ax.grid(axis="y", alpha=0.3)

    # 통계 검정
    if len(ready_fps) > 5 and len(not_ready_fps) > 5:
        t_stat, p_val = stats.ttest_ind(ready_fps, not_ready_fps)
        ax.text(0.5, 0.95, f"t={t_stat:.2f}, p={p_val:.3f}",
               transform=ax.transAxes, ha="center", va="top", fontsize=9,
               bbox=dict(boxstyle="round", facecolor="wheat", alpha=0.8))

    # 2. FPS 시계열 + 트래킹 상태
    ax = axes[1]
    # 첫 번째 세션만 표시
    if health_dfs:
        sample = health_dfs[0].copy()
        sample = sample.sort_values("timestamp")
        t0 = sample["timestamp"].iloc[0]
        elapsed = (sample["timestamp"] - t0).dt.total_seconds()

        ax.plot(elapsed, sample["fps"], "-", color="#3498db",
               alpha=0.6, linewidth=0.8, label="FPS")

        # not_ready 구간 강조
        not_ready_mask = sample["tracking_state"] == "not_ready"
        if not_ready_mask.any():
            not_ready_elapsed = elapsed[not_ready_mask]
            for t in not_ready_elapsed:
                ax.axvline(x=t, color=STATE_COLORS["TimedOut"],
                          alpha=0.3, linewidth=1)
            ax.axvline(x=not_ready_elapsed.iloc[0],
                      color=STATE_COLORS["TimedOut"], alpha=0.5,
                      linewidth=1, label="Tracking Unstable")

        ax.set_xlabel("Elapsed Time (s)")
        ax.set_ylabel("FPS")
        ax.set_title("FPS Time Series + Tracking Instability")
        ax.legend()
        ax.grid(alpha=0.3)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "anchor_health_correlation")


# ──────────────────────────────────────────────
# 메인
# ──────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="앵커 재인식 견고성 분석")
    parser.add_argument("--data-dir", type=str, default=None,
                        help="데이터 디렉토리 (기본: data/raw/)")
    parser.add_argument("--fallback", action="store_true",
                        help="데이터 파일이 없을 때 fallback 데이터로 실행")
    args = parser.parse_args()

    data_dir = Path(args.data_dir) if args.data_dir else RAW_DIR
    (OUTPUT_DIR / "csv").mkdir(exist_ok=True)

    print("=" * 60)
    print("앵커 재인식 견고성 분석 (ISMAR 로깅)")
    print("=" * 60)

    # 데이터 로드
    reloc_dfs, health_dfs = load_all_data(data_dir, allow_fallback=args.fallback)
    print(f"anchor_reloc 세션 수: {len(reloc_dfs)}")
    print(f"system_health 세션 수: {len(health_dfs)}")

    if reloc_dfs:
        all_reloc = pd.concat(reloc_dfs, ignore_index=True)
        total_anchors = len(all_reloc[all_reloc["state"] != "InProgress"])
        print(f"총 앵커 재인식 시도: {total_anchors}")

    # ── 분석 ──

    # 1. 재인식 성공률
    print("\n--- 웨이포인트별 재인식 결과 ---")
    rates_df = analyze_reloc_rates(reloc_dfs)
    if not rates_df.empty:
        overall_success = rates_df["success"].sum() / max(rates_df["total"].sum(), 1)
        print(f"  전체 성공률: {overall_success:.1%}")
        for _, row in rates_df.iterrows():
            if row["total"] > 0:
                print(f"  {row['waypoint_id']}: 성공 {row['success_rate']:.0%}, "
                      f"타임아웃 {row['timeout_rate']:.0%}, "
                      f"실패 {row['fail_rate']:.0%} (n={row['total']})")

    # 2. 재인식 소요 시간
    print("\n--- 재인식 소요 시간 ---")
    times_df = analyze_reloc_times(reloc_dfs)
    if not times_df.empty:
        success_times = times_df[times_df["state"] == "Tracking"]
        if not success_times.empty:
            print(f"  성공 시 평균: {success_times['elapsed_s'].mean():.1f}s "
                  f"(SD={success_times['elapsed_s'].std():.1f})")
            print(f"  성공 시 중앙값: {success_times['elapsed_s'].median():.1f}s")
            print(f"  성공 시 최대: {success_times['elapsed_s'].max():.1f}s")

        timeout_times = times_df[times_df["state"] == "TimedOut"]
        if not timeout_times.empty:
            print(f"  타임아웃 시 평균: {timeout_times['elapsed_s'].mean():.1f}s")

    # 3. SLAM 상태
    print("\n--- 실패 시 SLAM 상태 ---")
    slam_df = analyze_slam_at_failure(reloc_dfs)
    if not slam_df.empty:
        for _, row in slam_df.iterrows():
            print(f"  {row['slam_reason']}: {row['count']}건 ({row['percentage']:.0f}%)")
    else:
        print("  실패 사례 없음 (또는 SLAM 사유 미기록)")

    # 4. 복구 타임라인
    print("\n--- 앵커 복구 타임라인 ---")
    timeline_df = analyze_recovery_timeline(reloc_dfs)
    if not timeline_df.empty:
        n_slow = len(timeline_df[timeline_df["total_elapsed_s"] > 10])
        n_total = len(timeline_df)
        print(f"  총 재인식 기록: {n_total}")
        print(f"  10초 초과 소요: {n_slow} ({n_slow / max(n_total, 1):.0%})")
        print(f"  중간 체크포인트 평균: "
              f"{timeline_df['n_checkpoints'].mean():.1f}개/앵커")

    # 5. 시스템 상태 상관
    print("\n--- 시스템 상태 상관 (FPS vs 트래킹) ---")
    health_corr = analyze_health_correlation(reloc_dfs, health_dfs)
    if not health_corr.empty:
        for _, row in health_corr.iterrows():
            print(f"  {row['tracking_state']}: FPS M={row['mean_fps']:.1f} "
                  f"(SD={row['std_fps']:.1f}, n={row['n']})")

    # ── 시각화 ──
    print(f"\n=== 시각화 ===")
    plot_reloc_rates(rates_df)
    plot_reloc_time_distribution(times_df)
    plot_slam_failure_reasons(slam_df)
    plot_recovery_timeline(timeline_df)
    plot_health_correlation(health_dfs)

    # ── 결과 CSV 저장 ──
    if not rates_df.empty:
        rates_df.to_csv(OUTPUT_DIR / "csv" / "anchor_reloc_rates.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'anchor_reloc_rates.csv'} 저장")

    if not times_df.empty:
        times_df.to_csv(OUTPUT_DIR / "csv" / "anchor_reloc_times.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'anchor_reloc_times.csv'} 저장")

    if not slam_df.empty:
        slam_df.to_csv(OUTPUT_DIR / "csv" / "anchor_slam_failure.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'anchor_slam_failure.csv'} 저장")

    if not health_corr.empty:
        health_corr.to_csv(OUTPUT_DIR / "csv" / "anchor_health_correlation.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'anchor_health_correlation.csv'} 저장")

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
