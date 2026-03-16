"""
참가자 전략 클러스터링 분석 스크립트
- Hybrid 조건에서의 Beam Pro 사용 패턴 기반 K-means 클러스터링
- 정보 과부하 역U자 관계 (Beam Pro 접근량 3분위 × 정확도)
- 클러스터별 성과 레이더 차트
- 사용 패턴 히트맵 (참가자 × 특성, z-scored)
- 통계: 실루엣 점수 기반 최적 K 자동 선택, 이차 회귀 R²/p
"""

import sys
import json
import argparse
import warnings
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib
from scipy import stats

from parse_utils import parse_extra
from stat_utils import paired_comparison, significance_marker
from plot_style import (apply_style, save_fig, COLORS_COND, PALETTE,
                        FIG_DOUBLE_COL, FIG_WIDE, DPI)
from trajectory_utils import (find_session_files, load_event_csv,
                              load_beam_segments, CONDITIONS)

warnings.filterwarnings("ignore", category=FutureWarning)

# sklearn 가용성 확인
try:
    from sklearn.preprocessing import StandardScaler
    from sklearn.cluster import KMeans
    from sklearn.decomposition import PCA
    from sklearn.metrics import silhouette_score
    HAS_SKLEARN = True
except ImportError:
    HAS_SKLEARN = False

# ──────────────────────────────────────────────
# 경로 설정
# ──────────────────────────────────────────────

DATA_DIR = Path(__file__).resolve().parent.parent / "data"
RAW_DIR = DATA_DIR / "raw"
OUTPUT_DIR = Path(__file__).resolve().parent / "output"
OUTPUT_DIR.mkdir(exist_ok=True)

SIDECAR_SUFFIXES = ("_head_pose.csv", "_nav_trace.csv", "_beam_segments.csv",
                    "_anchor_reloc.csv", "_system_health.csv")

N_PARTICIPANTS = 24
N_WAYPOINTS = 8
MISSIONS = ["A1", "B1", "A2", "B2", "C1"]
MISSION_TARGET_WPS = {"A1": "WP02", "B1": "WP03", "A2": "WP05",
                      "B2": "WP06", "C1": "WP07"}
TRIGGER_WAYPOINTS = ["WP03", "WP06"]

BEAM_CONTENT_EVENTS = [
    "BEAM_TAB_SWITCH", "BEAM_POI_VIEWED", "BEAM_INFO_CARD_OPENED",
    "BEAM_INFO_CARD_CLOSED", "BEAM_MAP_ZOOMED", "BEAM_COMPARISON_VIEWED",
    "BEAM_MISSION_REF_VIEWED",
]

# 클러스터링 특성 목록
FEATURE_COLS = [
    "switch_count", "total_beam_time_s", "avg_beam_duration_s",
    "map_ratio", "info_ratio", "poi_ratio",
    "accuracy", "completion_time_s",
]

FEATURE_LABELS_KR = {
    "switch_count": "Switch Count",
    "total_beam_time_s": "Total Beam Time(s)",
    "avg_beam_duration_s": "Avg Switch Duration(s)",
    "map_ratio": "Map Tab Ratio",
    "info_ratio": "Info Tab Ratio",
    "poi_ratio": "POI Tab Ratio",
    "accuracy": "Accuracy",
    "completion_time_s": "Completion Time(s)",
}

CLUSTER_NAMES = {
    0: "Cautious",
    1: "Efficient",
    2: "Glass-focused",
}

# 레이더 차트 지표
RADAR_METRICS = ["accuracy", "completion_time_s", "switch_count",
                 "total_beam_time_s", "avg_beam_duration_s"]
RADAR_LABELS = ["Accuracy", "Speed\n(inverse)", "Switch\nCount", "Beam\nTime", "Avg\nSwitch"]


# ──────────────────────────────────────────────
# 1. 데이터 로드
# ──────────────────────────────────────────────

def load_all_events(allow_fallback: bool = False) -> pd.DataFrame:
    """data/raw/ 내 이벤트 CSV 통합 로드."""
    csv_files = sorted(
        f for f in RAW_DIR.glob("P*_*.csv")
        if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
    )
    if not csv_files:
        if allow_fallback:
            print(f"[경고] {RAW_DIR}에 CSV 파일이 없습니다. fallback 데이터를 생성합니다.")
            return generate_fallback_data()
        else:
            print(f"[오류] {RAW_DIR}에 CSV 파일이 없습니다.")
            print("  fallback 데이터로 실행하려면 --fallback 플래그를 사용하세요.")
            sys.exit(1)

    frames = []
    for f in csv_files:
        df = pd.read_csv(f, parse_dates=["timestamp"])
        frames.append(df)
    return pd.concat(frames, ignore_index=True)


def load_all_beam_segments() -> pd.DataFrame:
    """beam_segments sidecar CSV 통합 로드."""
    import re
    seg_files = sorted(RAW_DIR.glob("*_beam_segments.csv"))
    if not seg_files:
        return pd.DataFrame()
    frames = []
    for f in seg_files:
        try:
            df = pd.read_csv(f)
            for ts_col in ["on_ts", "off_ts"]:
                if ts_col in df.columns:
                    df[ts_col] = pd.to_datetime(df[ts_col])
            match = re.match(r"(P\d+)_", f.name)
            if match:
                df["participant_id"] = match.group(1)
            frames.append(df)
        except Exception as e:
            print(f"  [경고] beam_segments 로드 실패: {f.name} — {e}")
    if frames:
        return pd.concat(frames, ignore_index=True)
    return pd.DataFrame()


# ──────────────────────────────────────────────
# 2. 특성 추출
# ──────────────────────────────────────────────

def extract_features(df: pd.DataFrame,
                     beam_seg: pd.DataFrame) -> pd.DataFrame:
    """Hybrid 조건에서 참가자별 클러스터링 특성 추출."""
    hybrid = df[df["condition"] == "hybrid"].copy()
    participants = sorted(hybrid["participant_id"].unique())

    features = []
    for pid in participants:
        pid_df = hybrid[hybrid["participant_id"] == pid]

        # ── 전환 횟수 ──
        beam_on = pid_df[pid_df["event_type"] == "BEAM_SCREEN_ON"]
        beam_off = pid_df[pid_df["event_type"] == "BEAM_SCREEN_OFF"]
        switch_count = len(beam_on)

        # ── 총 Beam 시간 / 평균 전환 시간 ──
        total_beam_time = 0.0
        durations = []

        # beam_segments sidecar 우선 사용
        if not beam_seg.empty and "participant_id" in beam_seg.columns:
            pid_segs = beam_seg[beam_seg["participant_id"] == pid]
            if not pid_segs.empty and "duration_s" in pid_segs.columns:
                durations = pid_segs["duration_s"].dropna().tolist()
                total_beam_time = sum(durations)
        # fallback: BEAM_SCREEN_OFF extra_data
        if not durations:
            for _, row in beam_off.iterrows():
                d = parse_extra(row.get("extra_data", "{}"))
                dur = d.get("duration_s", None)
                if dur is not None:
                    try:
                        durations.append(float(dur))
                    except (ValueError, TypeError):
                        pass
                elif "duration_s" in pid_df.columns:
                    dur_val = row.get("duration_s", np.nan)
                    if pd.notna(dur_val):
                        durations.append(float(dur_val))
            total_beam_time = sum(durations)

        avg_beam_duration = np.mean(durations) if durations else 0.0

        # ── 탭 비율 (map / info / poi) ──
        content_events = pid_df[pid_df["event_type"].isin(BEAM_CONTENT_EVENTS)]
        n_content = max(len(content_events), 1)

        # beam_content_type 컬럼 또는 event_type으로 판별
        if "beam_content_type" in content_events.columns:
            map_count = len(content_events[
                content_events["beam_content_type"] == "map"])
            info_count = len(content_events[
                content_events["beam_content_type"].isin(
                    ["info_card", "comparison", "mission_ref"])])
            poi_count = len(content_events[
                content_events["beam_content_type"] == "poi_detail"])
        else:
            map_count = len(content_events[
                content_events["event_type"] == "BEAM_MAP_ZOOMED"])
            info_count = len(content_events[
                content_events["event_type"].isin([
                    "BEAM_INFO_CARD_OPENED", "BEAM_COMPARISON_VIEWED",
                    "BEAM_MISSION_REF_VIEWED"])])
            poi_count = len(content_events[
                content_events["event_type"] == "BEAM_POI_VIEWED"])

        map_ratio = map_count / n_content
        info_ratio = info_count / n_content
        poi_ratio = poi_count / n_content

        # ── 정확도 ──
        verifications = pid_df[pid_df["event_type"] == "VERIFICATION_ANSWERED"]
        if not verifications.empty:
            if "verification_correct" in verifications.columns:
                correct_vals = verifications["verification_correct"].dropna()
                # boolean 또는 True/False 문자열 처리
                correct_bools = correct_vals.astype(str).str.lower().map(
                    {"true": True, "false": False, "1": True, "0": False,
                     "1.0": True, "0.0": False})
                correct_bools = correct_bools.dropna()
                accuracy = correct_bools.mean() if len(correct_bools) > 0 else 0.5
            else:
                # extra_data에서 추출
                corrects = []
                for _, row in verifications.iterrows():
                    d = parse_extra(row.get("extra_data", "{}"))
                    c = d.get("correct", None)
                    if c is not None:
                        corrects.append(bool(c))
                accuracy = np.mean(corrects) if corrects else 0.5
        else:
            accuracy = 0.5

        # ── 완료 시간 ──
        route_start = pid_df[pid_df["event_type"] == "ROUTE_START"]
        route_end = pid_df[pid_df["event_type"] == "ROUTE_END"]
        if not route_start.empty and not route_end.empty:
            start_t = pd.to_datetime(route_start["timestamp"].iloc[0])
            end_t = pd.to_datetime(route_end["timestamp"].iloc[0])
            completion_time = (end_t - start_t).total_seconds()
        else:
            # fallback: 첫 이벤트~마지막 이벤트
            timestamps = pd.to_datetime(pid_df["timestamp"])
            completion_time = (timestamps.max() - timestamps.min()).total_seconds()

        features.append({
            "participant_id": pid,
            "switch_count": switch_count,
            "total_beam_time_s": round(total_beam_time, 1),
            "avg_beam_duration_s": round(avg_beam_duration, 1),
            "map_ratio": round(map_ratio, 3),
            "info_ratio": round(info_ratio, 3),
            "poi_ratio": round(poi_ratio, 3),
            "accuracy": round(accuracy, 3),
            "completion_time_s": round(completion_time, 1),
        })

    return pd.DataFrame(features)


# ──────────────────────────────────────────────
# 3. 클러스터링
# ──────────────────────────────────────────────

def cluster_participants(feat_df: pd.DataFrame
                         ) -> Tuple[pd.DataFrame, int, float, np.ndarray]:
    """K-means 클러스터링 + 실루엣 점수 기반 최적 K 선택.

    Returns:
        (feat_df with 'cluster' column, best_k, best_silhouette, pca_components)
    """
    if not HAS_SKLEARN:
        print("[오류] scikit-learn이 설치되지 않아 클러스터링을 수행할 수 없습니다.")
        print("  pip install scikit-learn 으로 설치하세요.")
        feat_df["cluster"] = 0
        return feat_df, 1, 0.0, np.zeros((len(feat_df), 2))

    X = feat_df[FEATURE_COLS].values
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)

    # 최적 K 선택 (K=2, 3 중 실루엣 점수 최대)
    best_k = 2
    best_sil = -1
    best_labels = None

    for k in [2, 3]:
        if len(X_scaled) < k:
            continue
        km = KMeans(n_clusters=k, random_state=42, n_init=10)
        labels = km.fit_predict(X_scaled)
        sil = silhouette_score(X_scaled, labels)
        print(f"  K={k}: 실루엣 점수 = {sil:.3f}")
        if sil > best_sil:
            best_sil = sil
            best_k = k
            best_labels = labels

    feat_df = feat_df.copy()
    feat_df["cluster"] = best_labels
    print(f"  → 최적 K = {best_k} (실루엣 = {best_sil:.3f})")

    # PCA (2D)
    pca = PCA(n_components=2, random_state=42)
    pca_coords = pca.fit_transform(X_scaled)
    print(f"  PCA 설명 분산: PC1={pca.explained_variance_ratio_[0]:.1%}, "
          f"PC2={pca.explained_variance_ratio_[1]:.1%}, "
          f"총={sum(pca.explained_variance_ratio_):.1%}")

    feat_df["pca_x"] = pca_coords[:, 0]
    feat_df["pca_y"] = pca_coords[:, 1]

    return feat_df, best_k, best_sil, pca.explained_variance_ratio_


def describe_clusters(feat_df: pd.DataFrame, n_clusters: int):
    """클러스터별 프로파일 출력."""
    print(f"\n=== 클러스터 프로파일 (K={n_clusters}) ===")
    for c in range(n_clusters):
        cluster_data = feat_df[feat_df["cluster"] == c]
        n = len(cluster_data)
        name = CLUSTER_NAMES.get(c, f"Cluster {c}")
        print(f"\n  [{name}] (n={n})")
        for col in FEATURE_COLS:
            m = cluster_data[col].mean()
            s = cluster_data[col].std()
            label = FEATURE_LABELS_KR.get(col, col)
            print(f"    {label}: M={m:.2f}, SD={s:.2f}")


# ──────────────────────────────────────────────
# 4. 정보 과부하 역U자 분석
# ──────────────────────────────────────────────

def analyze_inverted_u(feat_df: pd.DataFrame) -> Dict:
    """Beam Pro 접근량 3분위별 정확도 분석 + 이차 회귀.

    Returns:
        dict with tertile_means, coeffs, r_squared, p_quadratic
    """
    df = feat_df.copy()

    # 접근량 지표: total_beam_time_s
    access = df["total_beam_time_s"].values
    accuracy = df["accuracy"].values

    # 3분위 분할
    tertile_labels = pd.qcut(access, q=3, labels=["Low", "Mid", "High"],
                             duplicates="drop")
    df["tertile"] = tertile_labels

    tertile_means = {}
    tertile_sds = {}
    for t in ["Low", "Mid", "High"]:
        subset = df[df["tertile"] == t]["accuracy"]
        if len(subset) > 0:
            tertile_means[t] = float(subset.mean())
            tertile_sds[t] = float(subset.std())
        else:
            tertile_means[t] = np.nan
            tertile_sds[t] = np.nan

    print(f"\n=== 정보 과부하 역U자 분석 ===")
    for t in ["Low", "Mid", "High"]:
        n = len(df[df["tertile"] == t])
        print(f"  {t} ({n}명): 정확도 M={tertile_means[t]:.3f}, "
              f"SD={tertile_sds.get(t, 0):.3f}")

    # 이차 회귀: accuracy = a * access² + b * access + c
    valid = ~(np.isnan(access) | np.isnan(accuracy))
    x = access[valid]
    y = accuracy[valid]

    if len(x) < 4:
        print("  [경고] 데이터 부족으로 이차 회귀 불가")
        return {"tertile_means": tertile_means, "coeffs": [0, 0, 0],
                "r_squared": 0, "p_quadratic": 1.0}

    coeffs = np.polyfit(x, y, 2)
    y_pred = np.polyval(coeffs, x)
    ss_res = np.sum((y - y_pred) ** 2)
    ss_tot = np.sum((y - np.mean(y)) ** 2)
    r_squared = 1 - ss_res / ss_tot if ss_tot > 0 else 0

    # 이차항(a)의 유의성 검정
    # 선형 모형과 비교하는 F-test로 이차항 기여도 검정
    coeffs_lin = np.polyfit(x, y, 1)
    y_pred_lin = np.polyval(coeffs_lin, x)
    ss_res_lin = np.sum((y - y_pred_lin) ** 2)
    n = len(x)
    # F = ((SS_lin - SS_quad) / 1) / (SS_quad / (n - 3))
    df_num = 1
    df_den = n - 3
    if df_den > 0 and ss_res > 0:
        f_stat = ((ss_res_lin - ss_res) / df_num) / (ss_res / df_den)
        p_quadratic = 1 - stats.f.cdf(f_stat, df_num, df_den)
    else:
        f_stat = 0
        p_quadratic = 1.0

    marker = significance_marker(p_quadratic)
    print(f"  이차 회귀: a={coeffs[0]:.6f}, b={coeffs[1]:.4f}, c={coeffs[2]:.3f}")
    print(f"  R² = {r_squared:.3f}, F({df_num},{df_den}) = {f_stat:.2f}, "
          f"p = {p_quadratic:.4f} {marker}")

    return {
        "tertile_means": tertile_means,
        "tertile_sds": tertile_sds,
        "tertile_df": df,
        "coeffs": coeffs,
        "r_squared": r_squared,
        "p_quadratic": p_quadratic,
        "x": x,
        "y": y,
    }


# ──────────────────────────────────────────────
# 5. 시각화
# ──────────────────────────────────────────────

def plot_pca_scatter(feat_df: pd.DataFrame, n_clusters: int,
                     explained_var: np.ndarray):
    """PCA 2D 산점도 — 클러스터별 색상."""
    fig, ax = plt.subplots(figsize=FIG_DOUBLE_COL)

    cluster_colors = PALETTE[:n_clusters]
    for c in range(n_clusters):
        cluster_data = feat_df[feat_df["cluster"] == c]
        name = CLUSTER_NAMES.get(c, f"Cluster {c}")
        ax.scatter(
            cluster_data["pca_x"], cluster_data["pca_y"],
            c=cluster_colors[c], label=f"{name} (n={len(cluster_data)})",
            s=60, alpha=0.8, edgecolors="white", linewidth=0.5, zorder=3
        )
        # 참가자 ID 라벨
        for _, row in cluster_data.iterrows():
            ax.annotate(
                row["participant_id"],
                (row["pca_x"], row["pca_y"]),
                textcoords="offset points", xytext=(5, 5),
                fontsize=6, alpha=0.7
            )

    ax.set_xlabel(f"PC1 ({explained_var[0]:.1%} explained var.)")
    ax.set_ylabel(f"PC2 ({explained_var[1]:.1%} explained var.)")
    ax.set_title("Participant Strategy Clustering (PCA 2D)")
    ax.legend(loc="best", framealpha=0.9)
    ax.axhline(y=0, color="gray", linewidth=0.5, linestyle="--", alpha=0.3)
    ax.axvline(x=0, color="gray", linewidth=0.5, linestyle="--", alpha=0.3)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "strategy_pca_scatter")


def plot_inverted_u(result: Dict):
    """역U자 관계 시각화: 3분위 막대 + 이차 회귀 곡선."""
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=FIG_WIDE)

    # ── 좌측: 3분위 막대 그래프 ──
    tertiles = ["Low", "Mid", "High"]
    tertile_labels = ["Low\n(bottom 1/3)", "Mid\n(middle 1/3)", "High\n(top 1/3)"]
    means = [result["tertile_means"].get(t, 0) for t in tertiles]
    sds = [result.get("tertile_sds", {}).get(t, 0) for t in tertiles]
    colors = [PALETTE[0], PALETTE[2], PALETTE[1]]

    bars = ax1.bar(tertile_labels, means, yerr=sds, capsize=5,
                   color=colors, alpha=0.8, edgecolor="white", linewidth=0.5)
    ax1.set_ylabel("Accuracy")
    ax1.set_xlabel("Beam Pro Access (Tertile)")
    ax1.set_title("Accuracy by Access Level (Inverted-U Hypothesis)")
    ax1.set_ylim(0, 1.05)

    # 유의성 정보 표시
    p = result["p_quadratic"]
    r2 = result["r_squared"]
    marker = significance_marker(p)
    ax1.text(0.95, 0.95,
             f"R² = {r2:.3f}\np(quad) = {p:.4f} {marker}",
             transform=ax1.transAxes, ha="right", va="top",
             fontsize=8, bbox=dict(boxstyle="round,pad=0.3",
                                   facecolor="white", alpha=0.8))

    # ── 우측: 산점도 + 이차 회귀 곡선 ──
    x = result["x"]
    y = result["y"]
    coeffs = result["coeffs"]

    ax2.scatter(x, y, c=PALETTE[0], s=40, alpha=0.6,
                edgecolors="white", linewidth=0.5, label="Participant")

    # 회귀 곡선
    x_smooth = np.linspace(x.min(), x.max(), 100)
    y_smooth = np.polyval(coeffs, x_smooth)
    ax2.plot(x_smooth, y_smooth, color=PALETTE[1], linewidth=2,
             label=f"Quadratic Regression (R²={r2:.3f})")

    ax2.set_xlabel("Total Beam Pro Usage Time (s)")
    ax2.set_ylabel("Accuracy")
    ax2.set_title("Beam Pro Usage vs Accuracy")
    ax2.set_ylim(0, 1.05)
    ax2.legend(loc="best", fontsize=8)

    # R²/p 어노테이션
    ax2.annotate(
        f"y = {coeffs[0]:.5f}x² + {coeffs[1]:.4f}x + {coeffs[2]:.3f}\n"
        f"R² = {r2:.3f}, p = {p:.4f} {marker}",
        xy=(0.05, 0.05), xycoords="axes fraction",
        fontsize=7, ha="left", va="bottom",
        bbox=dict(boxstyle="round,pad=0.3", facecolor="lightyellow", alpha=0.9))

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "strategy_inverted_u")


def plot_radar_chart(feat_df: pd.DataFrame, n_clusters: int):
    """클러스터별 성과 레이더 차트."""
    fig, ax = plt.subplots(figsize=(7, 7), subplot_kw=dict(polar=True))

    n_metrics = len(RADAR_METRICS)
    angles = np.linspace(0, 2 * np.pi, n_metrics, endpoint=False).tolist()
    angles += angles[:1]  # 닫기

    cluster_colors = PALETTE[:n_clusters]

    # 전체 데이터 기준 정규화 범위 계산
    global_min = {}
    global_max = {}
    for col in RADAR_METRICS:
        vals = feat_df[col].values
        global_min[col] = float(np.min(vals))
        global_max[col] = float(np.max(vals))

    for c in range(n_clusters):
        cluster_data = feat_df[feat_df["cluster"] == c]
        name = CLUSTER_NAMES.get(c, f"Cluster {c}")

        values = []
        for col in RADAR_METRICS:
            m = cluster_data[col].mean()
            # 0-1 정규화
            range_val = global_max[col] - global_min[col]
            if range_val > 0:
                norm = (m - global_min[col]) / range_val
            else:
                norm = 0.5

            # completion_time_s는 역수 (낮을수록 좋음 → 높은 값)
            if col == "completion_time_s":
                norm = 1 - norm

            values.append(norm)

        values += values[:1]  # 닫기

        ax.fill(angles, values, color=cluster_colors[c], alpha=0.15)
        ax.plot(angles, values, color=cluster_colors[c], linewidth=2,
                label=f"{name} (n={len(cluster_data)})")

    # 축 라벨
    ax.set_xticks(angles[:-1])
    ax.set_xticklabels(RADAR_LABELS, fontsize=9)
    ax.set_ylim(0, 1)
    ax.set_yticks([0.2, 0.4, 0.6, 0.8, 1.0])
    ax.set_yticklabels(["0.2", "0.4", "0.6", "0.8", "1.0"], fontsize=7)
    ax.set_title("Cluster Performance Profile (Normalized)", pad=20)
    ax.legend(loc="upper right", bbox_to_anchor=(1.3, 1.1), fontsize=8)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "strategy_radar")


def plot_usage_heatmap(feat_df: pd.DataFrame):
    """참가자 × 특성 히트맵 (z-scored, 클러스터 순 정렬)."""
    df = feat_df.sort_values(["cluster", "participant_id"]).copy()

    X = df[FEATURE_COLS].values
    # z-score 정규화
    means = X.mean(axis=0)
    stds = X.std(axis=0)
    stds[stds == 0] = 1
    Z = (X - means) / stds

    fig, ax = plt.subplots(figsize=FIG_WIDE)

    im = ax.imshow(Z, aspect="auto", cmap="RdBu_r", vmin=-2.5, vmax=2.5)

    # x축: 특성 라벨
    feature_labels = [FEATURE_LABELS_KR.get(c, c) for c in FEATURE_COLS]
    ax.set_xticks(range(len(FEATURE_COLS)))
    ax.set_xticklabels(feature_labels, rotation=35, ha="right", fontsize=8)

    # y축: 참가자 ID + 클러스터 라벨
    y_labels = []
    for _, row in df.iterrows():
        cname = CLUSTER_NAMES.get(row["cluster"], f"C{row['cluster']}")
        y_labels.append(f"{row['participant_id']} ({cname})")
    ax.set_yticks(range(len(y_labels)))
    ax.set_yticklabels(y_labels, fontsize=7)

    # 클러스터 경계선
    cluster_ids = df["cluster"].values
    for i in range(1, len(cluster_ids)):
        if cluster_ids[i] != cluster_ids[i - 1]:
            ax.axhline(y=i - 0.5, color="black", linewidth=1.5)

    # 셀 값 표시
    for i in range(Z.shape[0]):
        for j in range(Z.shape[1]):
            val = Z[i, j]
            color = "white" if abs(val) > 1.5 else "black"
            ax.text(j, i, f"{val:.1f}", ha="center", va="center",
                    fontsize=6, color=color)

    ax.set_title("Usage Pattern per Participant (z-scored)")
    cbar = fig.colorbar(im, ax=ax, label="z-score", shrink=0.8)
    cbar.ax.tick_params(labelsize=7)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "strategy_heatmap")


# ──────────────────────────────────────────────
# 6. Fallback 데이터 생성
# ──────────────────────────────────────────────

def generate_fallback_data() -> pd.DataFrame:
    """전략 클러스터링 분석용 fallback 데이터 생성 (24명 × 2조건).

    3가지 전략 패턴:
    - Cautious (8명): 높은 전환 횟수, 긴 Beam 시간, 높은 정확도, 느림
    - Efficient (8명): 적절한 전환, 짧은 탐색, 중간 정확도, 빠름
    - Glass-focused (8명): 최소 전환, 낮은 B-타입 정확도, 보통 속도
    """
    rng = np.random.default_rng(42)
    rows = []
    waypoints = [f"WP{i:02d}" for i in range(1, N_WAYPOINTS + 1)]

    # 전략 그룹 배정 (8명씩)
    strategies = (["cautious"] * 8 + ["efficient"] * 8 +
                  ["glass_focused"] * 8)
    rng.shuffle(strategies)

    base_time = pd.Timestamp("2026-03-15T10:00:00")
    beam_content_types = ["poi_detail", "info_card", "comparison",
                          "map", "mission_ref"]

    for pid in range(1, N_PARTICIPANTS + 1):
        participant_id = f"P{pid:02d}"
        strategy = strategies[pid - 1]

        for condition in CONDITIONS:
            t = base_time + pd.Timedelta(hours=pid, minutes=30 if condition == "hybrid" else 0)

            # SESSION_INITIALIZED
            rows.append(_event(t, participant_id, condition,
                               "SESSION_INITIALIZED", ""))
            t += pd.Timedelta(seconds=5)

            # ROUTE_START
            rows.append(_event(t, participant_id, condition,
                               "ROUTE_START", ""))
            t += pd.Timedelta(seconds=rng.integers(3, 8))

            mission_idx = 0
            current_mission = MISSIONS[mission_idx]
            rows.append(_event(t, participant_id, condition,
                               "MISSION_START", "WP01",
                               mission_id=current_mission))

            for wp in waypoints:
                # ── 전략별 Beam Pro 전환 확률/지속시간 결정 ──
                if condition == "hybrid":
                    if strategy == "cautious":
                        switch_prob = 0.85 if wp in TRIGGER_WAYPOINTS else 0.60
                        beam_dur_range = (8, 25)
                        tab_probs = [0.20, 0.30, 0.10, 0.30, 0.10]
                    elif strategy == "efficient":
                        switch_prob = 0.50 if wp in TRIGGER_WAYPOINTS else 0.25
                        beam_dur_range = (2, 8)
                        tab_probs = [0.35, 0.20, 0.05, 0.30, 0.10]
                    else:  # glass_focused
                        switch_prob = 0.15 if wp in TRIGGER_WAYPOINTS else 0.05
                        beam_dur_range = (1, 5)
                        tab_probs = [0.40, 0.20, 0.10, 0.20, 0.10]

                    if rng.random() < switch_prob:
                        rows.append(_event(t, participant_id, condition,
                                           "BEAM_SCREEN_ON", wp))
                        sub_t = t + pd.Timedelta(
                            seconds=rng.uniform(0.5, 1.5))

                        # 콘텐츠 이벤트 생성
                        n_sub = rng.integers(1, 4)
                        for _ in range(n_sub):
                            ct = rng.choice(beam_content_types, p=tab_probs)
                            evt_type = {
                                "poi_detail": "BEAM_POI_VIEWED",
                                "info_card": "BEAM_INFO_CARD_OPENED",
                                "comparison": "BEAM_COMPARISON_VIEWED",
                                "map": "BEAM_MAP_ZOOMED",
                                "mission_ref": "BEAM_MISSION_REF_VIEWED",
                            }[ct]
                            rows.append(_event(sub_t, participant_id,
                                               condition, evt_type, wp,
                                               beam_content_type=ct))
                            sub_t += pd.Timedelta(
                                seconds=rng.uniform(0.5, 2))

                        beam_dur = rng.uniform(*beam_dur_range)
                        t_end = t + pd.Timedelta(seconds=beam_dur)
                        rows.append(_event(t_end, participant_id, condition,
                                           "BEAM_SCREEN_OFF", wp,
                                           duration_s=round(beam_dur, 1)))
                        t = t_end + pd.Timedelta(
                            seconds=rng.uniform(0.5, 2))

                # ── 트리거 ──
                if wp in TRIGGER_WAYPOINTS:
                    ttype = {"WP03": "T2", "WP06": "T3"}[wp]
                    rows.append(_event(t, participant_id, condition,
                                       "TRIGGER_ACTIVATED", wp,
                                       trigger_type=ttype))
                    trig_dur = rng.uniform(8, 15)
                    t += pd.Timedelta(seconds=trig_dur)
                    rows.append(_event(t, participant_id, condition,
                                       "TRIGGER_DEACTIVATED", wp,
                                       trigger_type=ttype,
                                       duration_s=round(trig_dur, 1)))

                # ── 이동 시간 (전략별) ──
                if strategy == "cautious":
                    move_time = rng.integers(40, 90)
                elif strategy == "efficient":
                    move_time = rng.integers(25, 55)
                else:
                    move_time = rng.integers(30, 70)

                t += pd.Timedelta(seconds=move_time)

                # ── 웨이포인트 도달 ──
                rows.append(_event(t, participant_id, condition,
                                   "WAYPOINT_REACHED", wp))

                # ── 확신도 ──
                if strategy == "cautious":
                    conf_base = 5.5 if condition == "hybrid" else 4.0
                elif strategy == "efficient":
                    conf_base = 5.0 if condition == "hybrid" else 4.2
                else:
                    conf_base = 4.0 if condition == "hybrid" else 3.5
                if wp in TRIGGER_WAYPOINTS:
                    conf_base -= 1.0
                conf = int(np.clip(round(rng.normal(conf_base, 0.8)), 1, 7))
                rows.append(_event(t, participant_id, condition,
                                   "CONFIDENCE_RATED", wp,
                                   confidence=conf))

                # ── 미션 검증 ──
                if mission_idx < len(MISSIONS):
                    m = MISSIONS[mission_idx]
                    if wp == MISSION_TARGET_WPS.get(m, ""):
                        # 전략별 정확도
                        if strategy == "cautious":
                            acc_base = 0.90 if condition == "hybrid" else 0.65
                        elif strategy == "efficient":
                            acc_base = 0.75 if condition == "hybrid" else 0.60
                        else:  # glass_focused
                            # B-타입 미션에서 특히 낮은 정확도
                            if m.startswith("B"):
                                acc_base = 0.45 if condition == "hybrid" else 0.40
                            else:
                                acc_base = 0.65 if condition == "hybrid" else 0.55

                        correct = rng.random() < acc_base
                        rt = round(rng.uniform(2, 8), 1)
                        t += pd.Timedelta(seconds=rt)
                        rows.append(_event(
                            t, participant_id, condition,
                            "VERIFICATION_ANSWERED", wp,
                            mission_id=m, correct=correct, rt_s=rt))
                        rows.append(_event(
                            t, participant_id, condition,
                            "MISSION_COMPLETE", wp,
                            mission_id=m, correct=correct))

                        diff_base = {"cautious": 3.0, "efficient": 3.5,
                                     "glass_focused": 4.5}[strategy]
                        diff = int(np.clip(round(
                            rng.normal(diff_base, 1)), 1, 7))
                        rows.append(_event(
                            t, participant_id, condition,
                            "DIFFICULTY_RATED", wp,
                            mission_id=m, rating=diff))

                        mission_idx += 1
                        if mission_idx < len(MISSIONS):
                            current_mission = MISSIONS[mission_idx]
                            t += pd.Timedelta(
                                seconds=rng.integers(3, 8))
                            rows.append(_event(
                                t, participant_id, condition,
                                "MISSION_START", wp,
                                mission_id=current_mission))

            # ROUTE_END
            t += pd.Timedelta(seconds=rng.integers(5, 15))
            rows.append(_event(t, participant_id, condition,
                               "ROUTE_END", ""))

    df = pd.DataFrame(rows)
    df["timestamp"] = pd.to_datetime(df["timestamp"], format="ISO8601")
    return df


def _event(t, pid, cond, etype, wp, **extra) -> dict:
    """이벤트 행 생성 헬퍼."""
    extra_filtered = {k: v for k, v in extra.items()
                      if k not in ("confidence", "beam_content_type")}
    row = {
        "timestamp": t.isoformat(),
        "participant_id": pid,
        "condition": cond,
        "event_type": etype,
        "waypoint_id": wp,
        "head_rotation_x": round(np.random.uniform(-10, 10), 1),
        "head_rotation_y": round(np.random.uniform(-180, 180), 1),
        "head_rotation_z": round(np.random.uniform(-5, 5), 1),
        "device_active": "glass" if cond == "glass_only" else "both",
        "confidence_rating": extra.get("confidence", ""),
        "beam_content_type": extra.get("beam_content_type", ""),
        "extra_data": json.dumps(extra_filtered) if extra_filtered else "{}",
    }
    return row


# ──────────────────────────────────────────────
# 7. 메인
# ──────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="참가자 전략 클러스터링 분석")
    parser.add_argument("--fallback", action="store_true",
                        help="CSV 파일이 없을 때 fallback 데이터로 실행")
    args = parser.parse_args()

    apply_style()
    (OUTPUT_DIR / "csv").mkdir(exist_ok=True)

    print("=" * 60)
    print("참가자 전략 클러스터링 분석")
    print("=" * 60)

    if not HAS_SKLEARN:
        print("[경고] scikit-learn이 설치되지 않았습니다.")
        print("  pip install scikit-learn 으로 설치하면 클러스터링이 가능합니다.")
        print("  sklearn 없이도 특성 추출 및 기본 분석은 수행됩니다.\n")

    # 데이터 로드
    df = load_all_events(allow_fallback=args.fallback)
    print(f"총 이벤트 수: {len(df)}")
    print(f"참가자 수: {df['participant_id'].nunique()}")
    print(f"조건: {df['condition'].unique().tolist()}")

    # beam_segments sidecar 로드
    beam_seg = load_all_beam_segments()
    if not beam_seg.empty:
        print(f"beam_segments sidecar: {len(beam_seg)}건 로드")

    # ── 특성 추출 (Hybrid 조건) ──
    print(f"\n=== 특성 추출 (Hybrid 조건) ===")
    feat_df = extract_features(df, beam_seg)
    print(f"  참가자 수: {len(feat_df)}")
    for col in FEATURE_COLS:
        label = FEATURE_LABELS_KR.get(col, col)
        print(f"  {label}: M={feat_df[col].mean():.2f}, "
              f"SD={feat_df[col].std():.2f}")

    if len(feat_df) < 4:
        print("\n[오류] 클러스터링에 필요한 최소 참가자 수(4)에 미달합니다.")
        feat_df.to_csv(OUTPUT_DIR / "csv" / "strategy_features.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'csv' / 'strategy_features.csv'} 저장")
        return

    # ── 클러스터링 ──
    print(f"\n=== K-means 클러스터링 ===")
    feat_df, best_k, best_sil, explained_var = cluster_participants(feat_df)
    describe_clusters(feat_df, best_k)

    # ── 정보 과부하 역U자 분석 ──
    inverted_u_result = analyze_inverted_u(feat_df)

    # ── 시각화 ──
    print(f"\n=== 시각화 ===")

    if HAS_SKLEARN:
        plot_pca_scatter(feat_df, best_k, explained_var)
    else:
        print("  [경고] sklearn 미설치 — PCA 산점도 생략")

    plot_inverted_u(inverted_u_result)
    plot_radar_chart(feat_df, best_k)
    plot_usage_heatmap(feat_df)

    # ── 결과 CSV 저장 ──
    feat_df.to_csv(OUTPUT_DIR / "csv" / "strategy_features.csv", index=False)
    print(f"  → {OUTPUT_DIR / 'csv' / 'strategy_features.csv'} 저장")

    # 클러스터 요약 CSV
    cluster_summary = []
    for c in range(best_k):
        cluster_data = feat_df[feat_df["cluster"] == c]
        row = {"cluster": c, "name": CLUSTER_NAMES.get(c, f"Cluster {c}"),
               "n": len(cluster_data)}
        for col in FEATURE_COLS:
            row[f"{col}_mean"] = round(cluster_data[col].mean(), 3)
            row[f"{col}_sd"] = round(cluster_data[col].std(), 3)
        cluster_summary.append(row)
    cluster_summary_df = pd.DataFrame(cluster_summary)
    cluster_summary_df.to_csv(OUTPUT_DIR / "csv" / "strategy_cluster_summary.csv",
                              index=False)
    print(f"  → {OUTPUT_DIR / 'csv' / 'strategy_cluster_summary.csv'} 저장")

    print(f"\n분석 완료. 최적 K={best_k}, 실루엣={best_sil:.3f}")


if __name__ == "__main__":
    main()
