"""
Fig 2: Experiment Environment & Route
도면 위에 Route B 경로를 오버레이하여 논문 Figure 생성.

실행: python3 analysis/generate_fig2_route.py
출력: paper/figures/fig2_environment_route.pdf / .png
"""

import numpy as np
import matplotlib
import matplotlib.pyplot as plt
import matplotlib.patheffects as pe
from matplotlib.image import imread
from matplotlib.patches import FancyArrowPatch, FancyBboxPatch
from pathlib import Path

# ── 경로 설정 ──────────────────────────────────
ROOT = Path(__file__).resolve().parent.parent
FLOORPLAN = ROOT / "ARNavExperiment" / "Assets" / "Data" / "FloorPlan" / "KIT_B1F_FloorPlan.png"
FIG_DIR = ROOT / "paper" / "figures"
FIG_DIR.mkdir(parents=True, exist_ok=True)

# ── 좌표계 ─────────────────────────────────────
# FloorPlanMapBase: worldMin=(-39,-23), worldMax=(49,80)
WORLD_MIN = np.array([-39, -23])
WORLD_MAX = np.array([49, 80])


def world_to_normalized(x, z):
    """월드 좌표(x,z)를 0~1 정규화 좌표로 변환."""
    tx = (x - WORLD_MIN[0]) / (WORLD_MAX[0] - WORLD_MIN[0])
    ty = (z - WORLD_MIN[1]) / (WORLD_MAX[1] - WORLD_MIN[1])
    return tx, ty


def world_to_pixel(x, z, img_w, img_h):
    """월드 좌표를 이미지 픽셀 좌표로 변환 (이미지 좌상단 원점)."""
    tx, ty = world_to_normalized(x, z)
    px = tx * img_w
    py = (1 - ty) * img_h  # 이미지 Y축 반전
    return px, py


# ── 웨이포인트 데이터 (WaypointDataGenerator.cs 기준) ──
WAYPOINTS = [
    ("WP00", 37.5, 24, 3.0, "B110\n(Calibration)"),
    ("WP01", 37.5, 18, 3.0, "B111\n(Start)"),
    ("WP02", 37.5, 33, 3.5, "B107"),
    ("WP03", 37.5, 45, 3.5, "B105"),
    ("WP04", 37.5, 57, 3.5, "B103"),
    ("WP05", 37.5, 66, 3.5, "B101"),
    ("WP06", 39, 72, 4.0, "NE Corner\n(U-turn)"),
    ("WP07", 37.5, 48, 3.5, "B104/B105\n(Return)"),
    ("WP08", 37.5, -7, 3.5, "B121\n(End)"),
]

# 경로 순서 (인덱스)
ROUTE_ORDER = [0, 1, 2, 3, 4, 5, 6, 7, 8]

# 트리거 존 (미션 세트별)
# Set1: T2(WP03 근처, B105) + T3(WP06 근처, NE코너)
# Set2: T1(WP03 근처) + T4(WP06 근처)
TRIGGERS = {
    "T2/T1": (37.5, 45, "Information Conflict /\nTracking Degradation"),
    "T3/T4": (39, 72, "Low Resolution /\nGuidance Absence"),
}

# ── 스타일 ──────────────────────────────────────
PAPER_STYLE = {
    "font.family": "sans-serif",
    "font.size": 12,
    "axes.titlesize": 14,
    "axes.titleweight": "bold",
    "axes.labelsize": 12,
    "axes.linewidth": 0.8,
    "axes.unicode_minus": False,
    "figure.dpi": 600,
    "savefig.dpi": 600,
    "savefig.bbox": "tight",
    "savefig.pad_inches": 0.05,
}

COLOR_OUTBOUND = "#2E86C1"   # 북상 (파란색)
COLOR_RETURN = "#E74C3C"     # 남하 (빨간색)
COLOR_WP = "#2C3E50"         # 웨이포인트 마커
COLOR_TRIGGER = "#F39C12"    # 트리거 존
COLOR_START = "#27AE60"      # 시작점
COLOR_END = "#8E44AD"        # 종료점


def generate_fig2():
    matplotlib.rcParams.update(PAPER_STYLE)

    img = imread(str(FLOORPLAN))
    img_h, img_w = img.shape[:2]

    # --- 픽셀 좌표 계산 ---
    wp_pixels = []
    for wp_id, x, z, radius, label in WAYPOINTS:
        px, py = world_to_pixel(x, z, img_w, img_h)
        wp_pixels.append((wp_id, px, py, radius, label))

    # --- Figure 생성 ---
    # 동쪽 복도 중심으로 크롭 (Route B 영역만)
    # 월드 좌표 기준: x=[25, 48], z=[-15, 78]
    crop_left, _ = world_to_pixel(22, 78, img_w, img_h)
    crop_right, _ = world_to_pixel(48, 78, img_w, img_h)
    _, crop_top = world_to_pixel(22, 78, img_w, img_h)
    _, crop_bottom = world_to_pixel(22, -15, img_w, img_h)

    crop_left = max(0, int(crop_left))
    crop_right = min(img_w, int(crop_right))
    crop_top = max(0, int(crop_top))
    crop_bottom = min(img_h, int(crop_bottom))

    cropped = img[crop_top:crop_bottom, crop_left:crop_right]
    crop_h, crop_w = cropped.shape[:2]

    # 크롭 오프셋 적용
    wp_cropped = []
    for wp_id, px, py, radius, label in wp_pixels:
        wp_cropped.append((wp_id, px - crop_left, py - crop_top, radius, label))

    # Figure (single column width: 3.33in, but wider for floor plan)
    aspect = crop_h / crop_w
    fig_w = 7.0
    fig_h = fig_w * aspect
    fig, ax = plt.subplots(1, 1, figsize=(fig_w, fig_h))

    # --- 도면 배경 ---
    ax.imshow(cropped, alpha=0.6)

    # --- 경로 그리기 ---
    # Outbound: WP00 → WP05 → WP06 (북상)
    outbound_indices = [0, 1, 2, 3, 4, 5, 6]
    out_x = [wp_cropped[i][1] for i in outbound_indices]
    out_y = [wp_cropped[i][2] for i in outbound_indices]

    # Return: WP06 → WP07 → WP08 (남하)
    return_indices = [6, 7, 8]
    ret_x = [wp_cropped[i][1] for i in return_indices]
    ret_y = [wp_cropped[i][2] for i in return_indices]

    # 경로 라인 (두꺼운 선 + 얇은 테두리)
    line_kw = dict(linewidth=5.5, solid_capstyle="round", solid_joinstyle="round",
                   zorder=3, path_effects=[pe.withStroke(linewidth=8.5, foreground="white")])

    ax.plot(out_x, out_y, color=COLOR_OUTBOUND, label="Outbound (northward)", **line_kw)
    ax.plot(ret_x, ret_y, color=COLOR_RETURN, label="Return (southward)", linestyle="--", **line_kw)

    # --- 방향 화살표 ---
    arrow_kw = dict(arrowstyle="-|>", mutation_scale=22, linewidth=3, zorder=5)

    # Outbound 화살표 (WP02→WP03 구간 중간)
    mid_out = 2  # WP02 index in outbound
    ax.annotate("", xy=(out_x[mid_out + 1], out_y[mid_out + 1]),
                xytext=(out_x[mid_out], out_y[mid_out]),
                arrowprops=dict(**arrow_kw, color=COLOR_OUTBOUND,
                                path_effects=[pe.withStroke(linewidth=5.5, foreground="white")]))

    # Return 화살표 (WP07→WP08 구간 중간)
    # WP07→WP08 중간 지점에 화살표
    mid_rx = (ret_x[1] + ret_x[2]) / 2
    mid_ry = (ret_y[1] + ret_y[2]) / 2
    ax.annotate("", xy=(ret_x[2], ret_y[2]),
                xytext=(ret_x[1], ret_y[1]),
                arrowprops=dict(**arrow_kw, color=COLOR_RETURN,
                                path_effects=[pe.withStroke(linewidth=5.5, foreground="white")]))

    # --- 웨이포인트 마커 ---
    for i, (wp_id, px, py, radius, label) in enumerate(wp_cropped):
        # 특별 마커: 시작, 종료, U-turn
        if i == 1:  # WP01 Start
            color = COLOR_START
            marker_size = 15
        elif i == 8:  # WP08 End
            color = COLOR_END
            marker_size = 15
        elif i == 6:  # WP06 U-turn
            color = COLOR_TRIGGER
            marker_size = 15
        elif i == 0:  # WP00 Calibration
            color = "#95A5A6"
            marker_size = 11
        else:
            color = COLOR_WP
            marker_size = 11

        ax.plot(px, py, "o", color=color, markersize=marker_size,
                markeredgecolor="white", markeredgewidth=2.5, zorder=6)

        # 웨이포인트 ID 라벨
        # 라벨 위치 조정 (경로 오른쪽에 배치, 특수 WP는 별도)
        offset_x, offset_y = 28, 0
        ha = "left"
        va = "center"

        if i == 6:  # NE corner - 오른쪽 위
            offset_x, offset_y = 24, -24
            va = "top"
        elif i == 0:  # Calibration - 왼쪽
            offset_x = -28
            ha = "right"

        txt = ax.annotate(
            wp_id,
            xy=(px, py),
            xytext=(px + offset_x, py + offset_y),
            fontsize=9.5, fontweight="bold", color=color,
            ha=ha, va=va, zorder=7,
            bbox=dict(boxstyle="round,pad=0.2", facecolor="white",
                      edgecolor=color, alpha=0.85, linewidth=0.8),
        )

    # --- 트리거 존 표시 ---
    for trig_label, (tx, tz, desc) in TRIGGERS.items():
        tpx, tpy = world_to_pixel(tx, tz, img_w, img_h)
        tpx -= crop_left
        tpy -= crop_top

        # 반투명 원으로 트리거 존 표시
        trigger_circle = plt.Circle(
            (tpx, tpy), 55, facecolor=COLOR_TRIGGER,
            alpha=0.15, linewidth=2.5, linestyle="--",
            edgecolor=COLOR_TRIGGER, fill=True, zorder=2
        )
        ax.add_patch(trigger_circle)

        # 트리거 라벨 (왼쪽에)
        ax.annotate(
            f"⚡ {trig_label}",
            xy=(tpx, tpy),
            xytext=(tpx - 125, tpy),
            fontsize=8.5, fontweight="bold", color=COLOR_TRIGGER,
            ha="right", va="center", zorder=7,
            arrowprops=dict(arrowstyle="-", color=COLOR_TRIGGER,
                            linewidth=1.2, linestyle="--"),
            bbox=dict(boxstyle="round,pad=0.3", facecolor="#FFF9E6",
                      edgecolor=COLOR_TRIGGER, alpha=0.9, linewidth=0.8),
        )

    # --- 범례 & 제목 ---
    # Start/End 표시
    ax.plot([], [], "o", color=COLOR_START, markersize=11,
            markeredgecolor="white", label="Start (B111)")
    ax.plot([], [], "o", color=COLOR_END, markersize=11,
            markeredgecolor="white", label="End (B121)")
    ax.plot([], [], "o", color=COLOR_TRIGGER, markersize=11,
            markeredgecolor="white", label="U-turn / Trigger zone")

    legend = ax.legend(loc="lower left", fontsize=9.5, framealpha=0.9,
                       edgecolor="#cccccc", fancybox=True,
                       handletextpad=0.5, borderpad=0.4)

    # 스케일바 (10m)
    scale_10m_px = (10 / (WORLD_MAX[0] - WORLD_MIN[0])) * img_w
    bar_y = crop_h - 50
    bar_x_start = 30
    ax.plot([bar_x_start, bar_x_start + scale_10m_px], [bar_y, bar_y],
            color="black", linewidth=3, solid_capstyle="butt", zorder=8)
    ax.plot([bar_x_start, bar_x_start], [bar_y - 8, bar_y + 8],
            color="black", linewidth=2.5, zorder=8)
    ax.plot([bar_x_start + scale_10m_px, bar_x_start + scale_10m_px],
            [bar_y - 8, bar_y + 8], color="black", linewidth=2.5, zorder=8)
    ax.text(bar_x_start + scale_10m_px / 2, bar_y - 15, "10 m",
            ha="center", va="bottom", fontsize=9.5, fontweight="bold",
            bbox=dict(boxstyle="round,pad=0.2", facecolor="white",
                      alpha=0.8, edgecolor="none"))

    # 방위 표시 (N↑)
    ax.annotate("N", xy=(crop_w - 55, 60),
                fontsize=14, fontweight="bold", ha="center", va="bottom",
                bbox=dict(boxstyle="round,pad=0.25", facecolor="white",
                          edgecolor="#333", alpha=0.9, linewidth=0.8))
    ax.annotate("", xy=(crop_w - 55, 38), xytext=(crop_w - 55, 82),
                arrowprops=dict(arrowstyle="-|>", color="#333",
                                linewidth=2.5))

    ax.set_xlim(0, crop_w)
    ax.set_ylim(crop_h, 0)
    ax.axis("off")

    fig.tight_layout(pad=0.3)

    # --- 저장 ---
    for fmt in ("pdf", "png"):
        out = FIG_DIR / f"fig2_environment_route.{fmt}"
        fig.savefig(out, format=fmt, facecolor="white")
    print(f"  -> fig2_environment_route saved to {FIG_DIR}")

    plt.close(fig)
    return FIG_DIR / "fig2_environment_route.png"


if __name__ == "__main__":
    out = generate_fig2()
    print(f"\nOutput: {out}")
