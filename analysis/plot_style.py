"""
CHI/UIST 출판용 플롯 스타일 — 색맹 친화, 300DPI, 컬럼 폭 맞춤.

주요 기능:
  - apply_style(): 전역 rcParams 설정
  - 색맹 친화 팔레트 (Wong 2011 기반)
  - violin_with_dots(): violin + 개별 점 + 유의성 마커
  - forest_plot(): 효과크기 Forest plot
  - diverging_likert_bar(): Likert 응답 diverging stacked bar
  - spaghetti_plot(): 개인별 궤적 spaghetti plot
  - interaction_plot(): 2x2 교호작용 그래프
  - save_fig(): PNG + PDF/SVG 벡터 저장
"""

import warnings
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple

import numpy as np
import pandas as pd
import matplotlib
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch

warnings.filterwarnings("ignore", category=FutureWarning)

# ──────────────────────────────────────────────
# 색상 팔레트 (색맹 친화 — Wong 2011 + 보조)
# ──────────────────────────────────────────────

# 조건 색상
COLOR_GLASS = "#3498db"    # 파랑
COLOR_HYBRID = "#2ecc71"   # 초록
COLORS_COND = [COLOR_GLASS, COLOR_HYBRID]
COND_LABELS = ["Glass Only", "Hybrid"]

# 일반 팔레트 (색맹 친화)
PALETTE = [
    "#0072B2",  # 파랑
    "#D55E00",  # 주황
    "#009E73",  # 청록
    "#CC79A7",  # 핑크
    "#F0E442",  # 노랑
    "#56B4E9",  # 하늘
    "#E69F00",  # 금색
    "#000000",  # 검정
]

# 트리거 색상
COLOR_TRIGGER = "#e74c3c"
COLOR_TRIGGER_ZONE = "#e74c3c33"

# 유의성 마커 색상
COLOR_SIG = "#333333"

# Figure 크기 (CHI 컬럼 폭 기준)
FIG_SINGLE_COL = (3.33, 2.5)   # 단일 컬럼 (85mm)
FIG_1_5_COL = (5.0, 3.5)       # 1.5 컬럼
FIG_DOUBLE_COL = (7.0, 4.0)    # 더블 컬럼 (178mm)
FIG_WIDE = (10.0, 5.0)         # 와이드 (프레젠테이션용)

DPI = 300


# ──────────────────────────────────────────────
# 스타일 적용
# ──────────────────────────────────────────────

def apply_style():
    """CHI 출판용 전역 matplotlib 스타일 적용."""
    matplotlib.rcParams.update({
        "font.family": "DejaVu Sans",
        "font.size": 9,
        "axes.unicode_minus": False,
        "axes.labelsize": 10,
        "axes.titlesize": 11,
        "axes.titleweight": "bold",
        "axes.linewidth": 0.8,
        "axes.grid": True,
        "grid.alpha": 0.2,
        "grid.linewidth": 0.5,
        "xtick.labelsize": 8,
        "ytick.labelsize": 8,
        "legend.fontsize": 8,
        "legend.framealpha": 0.8,
        "figure.dpi": DPI,
        "savefig.dpi": DPI,
        "savefig.bbox": "tight",
        "savefig.pad_inches": 0.05,
    })


# Paper-specific colors (ISMAR 2026)
COLOR_GLASS_PAPER = "#4A90D9"
COLOR_HYBRID_PAPER = "#50B86C"


def apply_paper_style():
    """ISMAR 논문용 축소 폰트 + 클린 스타일."""
    matplotlib.rcParams.update({
        "font.family": "sans-serif",
        "font.size": 8,
        "axes.titlesize": 9,
        "axes.titleweight": "bold",
        "axes.labelsize": 8,
        "axes.linewidth": 0.6,
        "axes.grid": False,
        "axes.spines.top": False,
        "axes.spines.right": False,
        "axes.unicode_minus": False,
        "xtick.labelsize": 7,
        "ytick.labelsize": 7,
        "xtick.major.width": 0.6,
        "ytick.major.width": 0.6,
        "legend.fontsize": 7,
        "legend.framealpha": 0.8,
        "figure.dpi": DPI,
        "savefig.dpi": DPI,
        "savefig.bbox": "tight",
        "savefig.pad_inches": 0.05,
    })


# ──────────────────────────────────────────────
# 저장 유틸
# ──────────────────────────────────────────────

def save_fig(fig, path: Path, formats: Sequence[str] = ("png", "pdf")):
    """PNG + 벡터(PDF/SVG) 저장 — 포맷별 하위 폴더에 저장."""
    path = Path(path)
    parent = path.parent  # e.g. analysis/output
    for fmt in formats:
        fmt_dir = parent / fmt
        fmt_dir.mkdir(parents=True, exist_ok=True)
        out = fmt_dir / path.with_suffix(f".{fmt}").name
        fig.savefig(out, dpi=DPI, format=fmt)
    print(f"  → {path.stem} saved ({', '.join(formats)})")
    plt.close(fig)


# ──────────────────────────────────────────────
# Violin + individual dots
# ──────────────────────────────────────────────

def violin_with_dots(
    ax,
    data_list: List[np.ndarray],
    positions: List[float],
    colors: List[str],
    labels: List[str],
    p_value: Optional[float] = None,
    ylabel: str = "",
    title: str = "",
    dot_alpha: float = 0.5,
    dot_size: float = 20,
    show_means: bool = True,
):
    """Violin plot + 개별 점 + 유의성 마커 (CHI 표준).

    Args:
        data_list: 각 조건의 데이터 배열 리스트
        positions: 각 violin의 x 위치
        colors: 각 violin의 색상
        labels: x축 레이블
        p_value: 유의성 마커를 위한 p값 (None이면 표시 안 함)
    """
    # Skip empty arrays to avoid matplotlib errors
    valid_data = [(d, p) for d, p in zip(data_list, positions) if len(d) >= 2]
    if not valid_data:
        return
    valid_arrays, valid_positions = zip(*valid_data)

    parts = ax.violinplot(
        list(valid_arrays), positions=list(valid_positions), showmeans=False,
        showmedians=False, showextrema=False
    )

    # Map valid indices back to original color indices
    valid_idx_map = [i for i, d in enumerate(data_list) if len(d) >= 2]
    for vi, pc in enumerate(parts["bodies"]):
        ci = valid_idx_map[vi] if vi < len(valid_idx_map) else vi
        pc.set_facecolor(colors[ci % len(colors)])
        pc.set_alpha(0.3)
        pc.set_edgecolor(colors[ci % len(colors)])
        pc.set_linewidth(1.0)

    # Box (Q1-Q3) + median
    for i, d in enumerate(data_list):
        if len(d) == 0:
            continue
        q1, med, q3 = np.percentile(d, [25, 50, 75])
        ax.vlines(positions[i], q1, q3, color=colors[i % len(colors)],
                  linewidth=3, alpha=0.7)
        ax.scatter(positions[i], med, color="white", s=30, zorder=4,
                   edgecolors=colors[i % len(colors)], linewidth=1.5)

        # 개별 점 (jitter)
        jitter = np.random.default_rng(42 + i).uniform(-0.08, 0.08, len(d))
        ax.scatter(
            positions[i] + jitter, d,
            s=dot_size, alpha=dot_alpha, color=colors[i % len(colors)],
            edgecolors="white", linewidth=0.3, zorder=3
        )

        # 평균 마커
        if show_means:
            mean_val = np.mean(d)
            ax.scatter(positions[i], mean_val, marker="D", s=40,
                       color=colors[i % len(colors)], edgecolors="black",
                       linewidth=0.8, zorder=5)

    ax.set_xticks(positions)
    ax.set_xticklabels(labels)
    if ylabel:
        ax.set_ylabel(ylabel)
    if title:
        ax.set_title(title)

    # 유의성 브래킷
    if p_value is not None and len(positions) == 2:
        from stat_utils import significance_marker, add_significance_bracket
        all_data = np.concatenate(data_list)
        y_max = np.max(all_data) if len(all_data) > 0 else 1
        y_range = np.ptp(all_data) if len(all_data) > 0 else 1
        bracket_y = y_max + y_range * 0.05
        bracket_h = y_range * 0.03
        add_significance_bracket(
            ax, positions[0], positions[1], bracket_y, p_value,
            height=bracket_h, text_offset=bracket_h * 0.5
        )


# ──────────────────────────────────────────────
# Forest plot
# ──────────────────────────────────────────────

def forest_plot(
    ax,
    results_df: pd.DataFrame,
    label_col: str = "dv",
    d_col: str = "d",
    ci_lo_col: str = "d_ci_lo",
    ci_hi_col: str = "d_ci_hi",
    p_col: str = "p",
    title: str = "Effect Sizes (Cohen's d)",
):
    """효과크기 Forest plot — 10+ DV를 한눈에 비교.

    results_df: batch_paired_comparison() 결과
    """
    df = results_df.dropna(subset=[d_col]).copy()
    if df.empty:
        return

    df = df.sort_values(d_col, ascending=True).reset_index(drop=True)
    n = len(df)
    y_positions = np.arange(n)

    for i, row in df.iterrows():
        d = row[d_col]
        lo, hi = row[ci_lo_col], row[ci_hi_col]
        p = row[p_col]

        color = COLOR_GLASS if d < 0 else COLOR_HYBRID
        if p >= 0.05:
            color = "#999999"

        ax.plot([lo, hi], [i, i], color=color, linewidth=2, solid_capstyle="round")
        ax.scatter(d, i, color=color, s=60, zorder=5, edgecolors="black", linewidth=0.5)

    # 영효과 기준선
    ax.axvline(x=0, color="black", linewidth=0.8, linestyle="--", alpha=0.5)

    # 효과크기 해석 구간
    for threshold, label_text in [(0.2, "Small"), (0.5, "Medium"), (0.8, "Large")]:
        for sign in [1, -1]:
            ax.axvline(x=threshold * sign, color="#cccccc", linewidth=0.5,
                       linestyle=":", alpha=0.5)

    ax.set_yticks(y_positions)
    ax.set_yticklabels(df[label_col].values)
    ax.set_xlabel("Cohen's d (95% CI)")
    ax.set_title(title)

    # p값 표시
    from stat_utils import significance_marker
    x_right = ax.get_xlim()[1]
    for i, row in df.iterrows():
        marker = significance_marker(row[p_col])
        if marker:
            ax.text(x_right * 0.95, i, marker, ha="right", va="center",
                    fontsize=9, fontweight="bold", color=COLOR_SIG)


# ──────────────────────────────────────────────
# Diverging stacked bar (Likert)
# ──────────────────────────────────────────────

def diverging_likert_bar(
    ax,
    data: pd.DataFrame,
    items: List[str],
    item_labels: List[str],
    scale_range: Tuple[int, int] = (1, 7),
    condition: Optional[str] = None,
    title: str = "",
    midpoint: Optional[float] = None,
):
    """Likert 응답 분포 diverging stacked bar chart.

    data: item별 응답 (컬럼: items의 각 요소)
    scale_range: (min, max) 예: (1, 7)
    midpoint: 중앙점 (None이면 자동 계산)
    """
    lo, hi = scale_range
    n_levels = hi - lo + 1
    if midpoint is None:
        midpoint = (lo + hi) / 2

    # 색상: 부정(빨강 계열) → 중립(회색) → 긍정(파랑 계열)
    neg_colors = ["#d73027", "#fc8d59", "#fee08b"]
    pos_colors = ["#d9ef8b", "#91cf60", "#1a9850"]
    mid_color = "#ffffbf"

    if n_levels == 7:
        colors = neg_colors + [mid_color] + pos_colors
    elif n_levels == 5:
        colors = [neg_colors[0], neg_colors[2], mid_color, pos_colors[0], pos_colors[2]]
    else:
        colors = plt.cm.RdYlGn(np.linspace(0.1, 0.9, n_levels))

    y_positions = np.arange(len(items))

    for idx, item in enumerate(items):
        if item not in data.columns:
            continue
        values = data[item].dropna().values
        counts = np.zeros(n_levels)
        for v in values:
            vi = int(round(v)) - lo
            if 0 <= vi < n_levels:
                counts[vi] += 1
        pcts = counts / max(counts.sum(), 1) * 100

        # 중앙점 기준 좌우 분할
        mid_idx = int(midpoint - lo)
        left = 0
        for j in range(n_levels):
            w = pcts[j]
            if j < mid_idx:
                ax.barh(y_positions[idx], -w, left=-left - w,
                        color=colors[j], edgecolor="white", linewidth=0.3)
                left += w
            elif j == mid_idx:
                ax.barh(y_positions[idx], w / 2, left=0,
                        color=colors[j], edgecolor="white", linewidth=0.3)
                ax.barh(y_positions[idx], -w / 2, left=0,
                        color=colors[j], edgecolor="white", linewidth=0.3)
            else:
                offset = sum(pcts[mid_idx + 1:j]) + pcts[mid_idx] / 2
                ax.barh(y_positions[idx], w, left=offset,
                        color=colors[j], edgecolor="white", linewidth=0.3)

    ax.set_yticks(y_positions)
    ax.set_yticklabels(item_labels)
    ax.set_xlabel("Response Distribution (%)")
    ax.axvline(x=0, color="black", linewidth=0.8)
    if title:
        ax.set_title(title)


# ──────────────────────────────────────────────
# Spaghetti plot (개인별 궤적)
# ──────────────────────────────────────────────

def spaghetti_plot(
    ax,
    data: pd.DataFrame,
    x_col: str,
    y_col: str,
    subject_col: str = "participant_id",
    group_col: Optional[str] = None,
    group_colors: Optional[Dict[str, str]] = None,
    group_labels: Optional[Dict[str, str]] = None,
    mean_line: bool = True,
    ci_band: bool = True,
    alpha_individual: float = 0.15,
    title: str = "",
    ylabel: str = "",
):
    """개인별 궤적 + 평균 + CI band (spaghetti plot)."""
    groups = [None] if group_col is None else data[group_col].unique()

    for g in groups:
        subset = data if g is None else data[data[group_col] == g]
        color = (group_colors or {}).get(g, PALETTE[0])
        label = (group_labels or {}).get(g, str(g))

        # 개인별 라인
        for pid, pdata in subset.groupby(subject_col):
            pdata = pdata.sort_values(x_col)
            ax.plot(pdata[x_col], pdata[y_col],
                    color=color, alpha=alpha_individual, linewidth=0.8)

        # 평균 + CI
        if mean_line:
            means = subset.groupby(x_col)[y_col].mean()
            sds = subset.groupby(x_col)[y_col].std()
            ns = subset.groupby(x_col)[y_col].count()
            ses = sds / np.sqrt(ns)

            ax.plot(means.index, means.values, color=color, linewidth=2.5,
                    label=label, zorder=5)

            if ci_band:
                ci = 1.96 * ses
                ax.fill_between(
                    means.index, means - ci, means + ci,
                    color=color, alpha=0.15, zorder=2
                )

    if ylabel:
        ax.set_ylabel(ylabel)
    if title:
        ax.set_title(title)
    if group_col:
        ax.legend()


# ──────────────────────────────────────────────
# Interaction plot (2x2)
# ──────────────────────────────────────────────

def interaction_plot(
    ax,
    data: pd.DataFrame,
    x_col: str,
    y_col: str,
    trace_col: str,
    colors: Optional[Dict] = None,
    labels: Optional[Dict] = None,
    ylabel: str = "",
    title: str = "",
    show_ci: bool = True,
):
    """2x2 교호작용 그래프 — 조건 x 트리거 등."""
    traces = sorted(data[trace_col].unique())
    x_vals = sorted(data[x_col].unique())
    x_pos = np.arange(len(x_vals))

    for i, trace in enumerate(traces):
        subset = data[data[trace_col] == trace]
        means = [subset[subset[x_col] == x][y_col].mean() for x in x_vals]
        sds = [subset[subset[x_col] == x][y_col].std() for x in x_vals]
        ns = [len(subset[subset[x_col] == x]) for x in x_vals]
        ses = [s / np.sqrt(n) if n > 0 else 0 for s, n in zip(sds, ns)]

        color = (colors or {}).get(trace, PALETTE[i % len(PALETTE)])
        label = (labels or {}).get(trace, str(trace))

        if show_ci:
            ci = [1.96 * se for se in ses]
            ax.errorbar(x_pos, means, yerr=ci, fmt="o-", color=color,
                        label=label, linewidth=2, markersize=8, capsize=4)
        else:
            ax.plot(x_pos, means, "o-", color=color, label=label,
                    linewidth=2, markersize=8)

    ax.set_xticks(x_pos)
    ax.set_xticklabels([str(x) for x in x_vals])
    if ylabel:
        ax.set_ylabel(ylabel)
    if title:
        ax.set_title(title)
    ax.legend()


# ──────────────────────────────────────────────
# Calibration curve
# ──────────────────────────────────────────────

def calibration_curve(
    ax,
    confidence: np.ndarray,
    accuracy: np.ndarray,
    color: str = COLOR_GLASS,
    label: str = "",
    n_bins: int = 5,
    show_perfect: bool = True,
):
    """확신도 vs 실제 정확도 calibration curve.

    confidence: 1-7 스케일 확신도
    accuracy: 0/1 이진 정확도
    n_bins: 확신도 구간 수
    """
    conf = np.asarray(confidence, dtype=float)
    acc = np.asarray(accuracy, dtype=float)
    valid = ~(np.isnan(conf) | np.isnan(acc))
    conf, acc = conf[valid], acc[valid]

    if len(conf) < 5:
        return

    # 확신도를 n_bins 구간으로 분할
    bin_edges = np.linspace(conf.min() - 0.01, conf.max() + 0.01, n_bins + 1)
    bin_centers = []
    bin_accs = []
    bin_counts = []

    for i in range(n_bins):
        mask = (conf >= bin_edges[i]) & (conf < bin_edges[i + 1])
        if mask.sum() > 0:
            bin_centers.append(np.mean(conf[mask]))
            bin_accs.append(np.mean(acc[mask]))
            bin_counts.append(mask.sum())

    if not bin_centers:
        return

    # 완벽한 보정 대각선
    if show_perfect:
        # 확신도를 0-1 범위로 정규화하여 대각선과 비교
        conf_norm_range = (min(bin_centers), max(bin_centers))
        ax.plot([0, 1], [0, 1], "--", color="gray", alpha=0.5, label="Perfect calibration")

    # 확신도를 0-1로 정규화
    conf_min, conf_max = conf.min(), conf.max()
    conf_range = conf_max - conf_min if conf_max > conf_min else 1
    norm_centers = [(c - conf_min) / conf_range for c in bin_centers]

    ax.plot(norm_centers, bin_accs, "o-", color=color, linewidth=2,
            markersize=8, label=label)

    # 각 점에 N 표시
    for nc, ba, bc in zip(norm_centers, bin_accs, bin_counts):
        ax.annotate(f"n={bc}", (nc, ba), textcoords="offset points",
                    xytext=(0, 8), ha="center", fontsize=7, color=color)

    ax.set_xlabel("Confidence (normalized)")
    ax.set_ylabel("Actual Accuracy")
    ax.set_xlim(-0.05, 1.05)
    ax.set_ylim(-0.05, 1.05)
