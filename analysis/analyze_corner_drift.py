"""
코너/U턴 구간 드리프트 진단 분석
- 런타임 distance_m (SlamToFloorPlan 적용 후) 직접 분석
- SLAM raw 궤적 시각화 (세션별)
- WP05→WP06→WP07 U턴 구간 거리 곡선 비교
- 보정 앵커 배치 최적화 제안
"""

import glob
import os
import sys

import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

# ── 한국어 폰트 ────────────────────────────────────────
plt.rcParams["font.family"] = "AppleGothic"
plt.rcParams["axes.unicode_minus"] = False

OUT = os.path.join(os.path.dirname(__file__), "output")
os.makedirs(OUT, exist_ok=True)

DATA = os.path.join(os.path.dirname(__file__), "..", "data", "raw_device")

# ── 웨이포인트 도면 좌표 & 반경 ──────────────────────
WP_FLOORPLAN = {
    "WP00": (36, 24), "WP01": (36, 18), "WP02": (36, 33),
    "WP03": (36, 45), "WP04": (36, 57), "WP05": (36, 66),
    "WP06": (39, 72), "WP07": (36, 48), "WP08": (36, -7),
}
WP_RADIUS = {
    "WP00": 3.0, "WP01": 3.0, "WP02": 3.5, "WP03": 3.5,
    "WP04": 3.5, "WP05": 3.5, "WP06": 4.0, "WP07": 3.5, "WP08": 3.5,
}
# calibration boost 매핑
CALIB_BOOST = {
    "none": 2.5, "auto_zero_anchor": 2.5, "anchor_1": 2.0,
    "anchor_2": 1.8, "anchor_3": 1.4, "anchor_4": 1.0,
    "manual": 1.0, "injected": 1.0,
}


def load_sessions():
    """이동이 있는 nav_trace 세션 로드"""
    files = sorted(glob.glob(os.path.join(DATA, "*_nav_trace.csv")))
    sessions = []
    for f in files:
        try:
            df = pd.read_csv(f)
            if len(df) < 50:
                continue
            # 실제 이동이 있는 세션만 (player 좌표 범위 > 5m)
            x_range = df.player_x.max() - df.player_x.min()
            z_range = df.player_z.max() - df.player_z.min()
            if max(x_range, z_range) < 5:
                continue
            name = os.path.basename(f).replace("_nav_trace.csv", "")
            df["session"] = name
            # 경과 시간 계산
            df["ts"] = pd.to_datetime(df.timestamp)
            df["elapsed_s"] = (df.ts - df.ts.iloc[0]).dt.total_seconds()
            sessions.append(df)
        except Exception:
            continue
    return sessions


def get_effective_radius(wp_id, calib_source):
    """보정 품질 기반 실효 도착 반경"""
    base = WP_RADIUS.get(wp_id, 3.5)
    boost = CALIB_BOOST.get(calib_source, 1.0)
    # anchor_4+ → 1.0
    if calib_source.startswith("anchor_"):
        try:
            n = int(calib_source.split("_")[1])
            if n >= 4:
                boost = 1.0
            elif n == 3:
                boost = 1.4
            elif n == 2:
                boost = 1.8
            else:
                boost = 2.0
        except ValueError:
            boost = 1.0
    return base * boost


def fig1_slam_trajectories(sessions):
    """Fig 1: SLAM raw 궤적 (XZ 평면), WP 전환 컬러코딩"""
    fig, ax = plt.subplots(1, 1, figsize=(10, 10))

    wp_colors = {
        "WP00": "#999999", "WP01": "#999999", "WP02": "#1f77b4",
        "WP03": "#ff7f0e", "WP04": "#2ca02c", "WP05": "#d62728",
        "WP06": "#9467bd", "WP07": "#8c564b", "WP08": "#e377c2",
    }

    for i, df in enumerate(sessions):
        name = df.session.iloc[0].split("_20260")[0]
        calib = df.calib_source.iloc[-1]

        # 연한 전체 경로
        ax.plot(df.player_x, df.player_z, "-", alpha=0.15, linewidth=0.5,
                color="gray")

        # WP별 색상 구간
        for wp_id in df.current_wp_id.unique():
            sub = df[df.current_wp_id == wp_id]
            color = wp_colors.get(wp_id, "#333333")
            ax.plot(sub.player_x, sub.player_z, ".", color=color,
                    markersize=2, alpha=0.5)

        # 시작점
        ax.plot(df.player_x.iloc[0], df.player_z.iloc[0], "^k",
                markersize=8, zorder=5)
        ax.annotate(f"{name}\n({calib})",
                    (df.player_x.iloc[0], df.player_z.iloc[0]),
                    fontsize=6, xytext=(5, 5), textcoords="offset points")

    # 범례
    for wp_id, color in wp_colors.items():
        ax.plot([], [], "o", color=color, label=wp_id, markersize=5)
    ax.legend(fontsize=8, ncol=3, loc="upper right")

    ax.set_xlabel("SLAM X (m)")
    ax.set_ylabel("SLAM Z (m)")
    ax.set_title("SLAM Raw 궤적 (WP별 컬러)")
    ax.set_aspect("equal")
    ax.grid(True, alpha=0.3)
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, "corner_drift_slam_raw.png"), dpi=150)
    print(f"[저장] {OUT}/corner_drift_slam_raw.png")
    plt.close(fig)


def fig2_distance_curves(sessions):
    """Fig 2: 전체 경로 distance_m 시계열 + 도착 반경 표시"""
    n_sess = len(sessions)
    fig, axes = plt.subplots(n_sess, 1, figsize=(14, 3 * n_sess),
                             squeeze=False)

    for i, df in enumerate(sessions):
        ax = axes[i, 0]
        name = df.session.iloc[0].split("_20260")[0]
        calib = df.calib_source.iloc[-1]

        # 거리 곡선
        ax.plot(df.elapsed_s, df.distance_m, "-", linewidth=1, color="#1f77b4",
                alpha=0.8)

        # WP 전환 마커
        wp_changes = df.current_wp_id.ne(df.current_wp_id.shift())
        for idx in df.index[wp_changes]:
            wp_id = df.loc[idx, "current_wp_id"]
            t = df.loc[idx, "elapsed_s"]
            ax.axvline(t, color="gray", linestyle=":", alpha=0.5)
            ax.text(t, ax.get_ylim()[1] if ax.get_ylim()[1] > 0 else 20,
                    wp_id, fontsize=7, rotation=45, va="top", ha="left")

            # 실효 반경 표시
            eff_r = get_effective_radius(wp_id, calib)
            ax.axhline(eff_r, color="red", linestyle="--", alpha=0.2)

        ax.set_ylabel("distance_m")
        ax.set_title(f"{name} (보정: {calib})", fontsize=10)
        ax.grid(True, alpha=0.3)
        ax.set_ylim(bottom=0)

    axes[-1, 0].set_xlabel("경과 시간 (초)")
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, "corner_drift_distance_curves.png"), dpi=150)
    print(f"[저장] {OUT}/corner_drift_distance_curves.png")
    plt.close(fig)


def fig3_corner_analysis(sessions):
    """Fig 3: WP05→WP06→WP07 코너 구간 상세 분석"""
    corner_sessions = [df for df in sessions
                       if "WP06" in df.current_wp_id.values]

    if not corner_sessions:
        print("[경고] WP06까지 도달한 세션이 없습니다")
        return

    fig, axes = plt.subplots(2, 2, figsize=(14, 10))

    colors = plt.cm.Set1(np.linspace(0, 1, max(len(corner_sessions), 1)))

    # (0,0): 코너 구간 distance_m 시계열
    ax = axes[0, 0]
    for i, df in enumerate(corner_sessions):
        name = df.session.iloc[0].split("_20260")[0]
        calib = df.calib_source.iloc[-1]
        mask = df.current_wp_id.isin(["WP05", "WP06", "WP07"])
        sub = df[mask].copy()
        if len(sub) == 0:
            continue
        sub["rel_t"] = (sub.ts - sub.ts.iloc[0]).dt.total_seconds()

        # WP별 색상 분리
        for wp_id, ls in [("WP05", "-"), ("WP06", "--"), ("WP07", ":")]:
            wp_sub = sub[sub.current_wp_id == wp_id]
            if len(wp_sub) == 0:
                continue
            ax.plot(wp_sub.rel_t.values, wp_sub.distance_m.values,
                    ls, color=colors[i], linewidth=1.5, alpha=0.8)

        ax.plot([], [], "-", color=colors[i], label=f"{name} ({calib})")

    # 반경 표시
    for wp_id, c in [("WP05", "#d62728"), ("WP06", "#9467bd"),
                     ("WP07", "#8c564b")]:
        r = WP_RADIUS[wp_id]
        ax.axhline(r, color=c, linestyle=":", alpha=0.4, label=f"{wp_id} 반경 {r}m")

    ax.set_title("코너 구간 distance_m (─WP05 ──WP06 ···WP07)")
    ax.set_xlabel("구간 내 경과 시간 (초)")
    ax.set_ylabel("distance_m (m)")
    ax.legend(fontsize=6, ncol=2)
    ax.grid(True, alpha=0.3)
    ax.set_ylim(bottom=0)

    # (0,1): WP06 체류 시간 vs 최소 거리
    ax = axes[0, 1]
    wp06_stats = []
    for df in corner_sessions:
        name = df.session.iloc[0].split("_20260")[0]
        calib = df.calib_source.iloc[-1]
        sub = df[df.current_wp_id == "WP06"]
        if len(sub) == 0:
            continue
        dwell = (sub.ts.iloc[-1] - sub.ts.iloc[0]).total_seconds()
        min_dist = sub.distance_m.min()
        eff_r = get_effective_radius("WP06", calib)
        wp06_stats.append({
            "session": name, "dwell_s": dwell, "min_dist": min_dist,
            "eff_radius": eff_r, "calib": calib
        })

    if wp06_stats:
        sdf = pd.DataFrame(wp06_stats)
        for j, row in sdf.iterrows():
            ax.scatter(row.dwell_s, row.min_dist, s=80,
                       color=colors[j], zorder=5, edgecolors="black")
            ax.annotate(f"{row.session}\n({row.calib})",
                        (row.dwell_s, row.min_dist),
                        fontsize=7, xytext=(5, 5),
                        textcoords="offset points")
            # 실효 반경 원
            ax.axhline(row.eff_radius, color=colors[j], linestyle=":",
                       alpha=0.3)

        ax.axhline(WP_RADIUS["WP06"], color="red", linestyle="--",
                   alpha=0.5, label=f"WP06 기본 반경 {WP_RADIUS['WP06']}m")

    ax.set_title("WP06 체류 시간 vs 최소 거리")
    ax.set_xlabel("체류 시간 (초)")
    ax.set_ylabel("최소 distance_m (m)")
    ax.legend(fontsize=7)
    ax.grid(True, alpha=0.3)

    # (1,0): 속도 프로필 (코너 구간)
    ax = axes[1, 0]
    for i, df in enumerate(corner_sessions):
        name = df.session.iloc[0].split("_20260")[0]
        mask = df.current_wp_id.isin(["WP05", "WP06", "WP07"])
        sub = df[mask].copy()
        if len(sub) == 0:
            continue
        sub["rel_t"] = (sub.ts - sub.ts.iloc[0]).dt.total_seconds()
        ax.plot(sub.rel_t.values, sub.speed_ms.values, "-",
                color=colors[i], alpha=0.6, linewidth=1, label=name)

    ax.axhline(0.15, color="red", linestyle=":", alpha=0.5,
               label="정체 임계 (0.15 m/s)")
    ax.set_title("코너 구간 이동 속도")
    ax.set_xlabel("구간 내 경과 시간 (초)")
    ax.set_ylabel("속도 (m/s)")
    ax.legend(fontsize=7)
    ax.grid(True, alpha=0.3)

    # (1,1): WP별 distance_m 분포 (box plot)
    ax = axes[1, 1]
    wp_ids = ["WP02", "WP03", "WP04", "WP05", "WP06", "WP07", "WP08"]
    data_by_wp = {wp: [] for wp in wp_ids}

    for df in sessions:
        for wp_id in wp_ids:
            sub = df[df.current_wp_id == wp_id]
            if len(sub) > 0:
                data_by_wp[wp_id].append(sub.distance_m.min())

    box_data = [data_by_wp[wp] for wp in wp_ids]
    bp = ax.boxplot(box_data, labels=wp_ids, patch_artist=True)
    corner_idx = [wp_ids.index("WP05"), wp_ids.index("WP06"),
                  wp_ids.index("WP07")]
    for j, box in enumerate(bp["boxes"]):
        if j in corner_idx:
            box.set_facecolor("#ffcdd2")
        else:
            box.set_facecolor("#bbdefb")

    # 반경선
    for j, wp_id in enumerate(wp_ids):
        r = WP_RADIUS[wp_id]
        ax.plot([j + 0.6, j + 1.4], [r, r], "r--", alpha=0.4, linewidth=0.8)

    ax.set_title("WP별 최소 distance_m (빨강=코너)")
    ax.set_ylabel("최소 distance_m (m)")
    ax.grid(True, alpha=0.3, axis="y")

    fig.tight_layout()
    fig.savefig(os.path.join(OUT, "corner_drift_analysis.png"), dpi=150)
    print(f"[저장] {OUT}/corner_drift_analysis.png")
    plt.close(fig)


def fig4_heading_and_anchor(sessions):
    """Fig 4: heading 변화 + 앵커 바인딩 시점"""
    fig, axes = plt.subplots(2, 1, figsize=(14, 8))

    # 상: heading offset 시계열
    ax = axes[0]
    for df in sessions:
        name = df.session.iloc[0].split("_20260")[0]
        ax.plot(df.elapsed_s, df.heading_offset_deg, "-", alpha=0.7,
                linewidth=1, label=name)
    ax.set_title("Heading Offset 시계열")
    ax.set_xlabel("경과 시간 (초)")
    ax.set_ylabel("Heading (°)")
    ax.legend(fontsize=6, ncol=2)
    ax.grid(True, alpha=0.3)

    # 하: 앵커 바인딩 여부 타임라인
    ax = axes[1]
    for i, df in enumerate(sessions):
        name = df.session.iloc[0].split("_20260")[0]
        anchored = df[df.anchor_bound == True]
        not_anchored = df[df.anchor_bound == False]

        y = i
        if len(not_anchored) > 0:
            ax.scatter(not_anchored.elapsed_s, [y] * len(not_anchored),
                       c="lightcoral", s=1, alpha=0.3)
        if len(anchored) > 0:
            ax.scatter(anchored.elapsed_s, [y] * len(anchored),
                       c="green", s=3, alpha=0.8)

    ax.set_yticks(range(len(sessions)))
    ax.set_yticklabels([df.session.iloc[0].split("_20260")[0]
                        for df in sessions], fontsize=7)
    ax.set_title("앵커 바인딩 타임라인 (녹색=바인딩, 빨강=fallback)")
    ax.set_xlabel("경과 시간 (초)")
    ax.grid(True, alpha=0.3)

    fig.tight_layout()
    fig.savefig(os.path.join(OUT, "corner_drift_heading.png"), dpi=150)
    print(f"[저장] {OUT}/corner_drift_heading.png")
    plt.close(fig)


def fig5_anchor_suggestion():
    """Fig 5: 보정 앵커 배치 제안도"""
    fig, ax = plt.subplots(1, 1, figsize=(6, 14))

    # 경로 (WP 순서)
    wp_order = ["WP00", "WP01", "WP02", "WP03", "WP04", "WP05",
                "WP06", "WP07", "WP08"]
    xs = [WP_FLOORPLAN[w][0] for w in wp_order]
    zs = [WP_FLOORPLAN[w][1] for w in wp_order]
    ax.plot(xs, zs, "k-", alpha=0.4, linewidth=2, zorder=1)

    # WP + 반경 원
    for wp_id, (wx, wz) in WP_FLOORPLAN.items():
        r = WP_RADIUS[wp_id]
        circle = plt.Circle((wx, wz), r, fill=False, color="blue",
                             linestyle="--", alpha=0.3)
        ax.add_patch(circle)
        ax.plot(wx, wz, "ko", markersize=8, zorder=5)
        ax.annotate(f"{wp_id}\n(r={r}m)", (wx, wz), fontsize=7,
                    ha="center", va="bottom",
                    xytext=(0, 8), textcoords="offset points")

    # Reference Anchor 레지스트리 (ReferencePointRegistry.cs)
    ref_points = {
        "B101": (36, 66), "B102": (36, 63), "B103": (36, 60),
        "B104": (36, 54), "B105": (36, 48), "B106": (36, 42),
        "B107": (36, 36), "B108": (36, 30), "B109": (36, 27),
        "B110": (36, 24), "B111": (36, 18), "B114": (36, 6),
        "B116": (39, 36), "B117": (39, 42), "B118": (39, 48),
        "B119": (39, 60), "B121": (36, -7),
    }

    for room, (rx, rz) in ref_points.items():
        ax.plot(rx, rz, "s", color="lightblue", markersize=5, alpha=0.5)
        ax.annotate(room, (rx, rz), fontsize=5, color="steelblue",
                    alpha=0.6, xytext=(3, -5), textcoords="offset points")

    # 우선 배치 (드리프트가 가장 심한 코너 근처)
    priority = {
        "B101": "WP05 근처 — 복도 끝 보정",
        "B119": "WP06 근처 — U턴 동쪽 벽 보정",
        "B105": "WP07 근처 — U턴 복귀 보정",
        "B111": "WP01 근처 — 시작점 보정",
    }
    for room, desc in priority.items():
        rx, rz = ref_points[room]
        ax.plot(rx, rz, "r*", markersize=18, zorder=6)
        ax.annotate(f"★ {room}\n{desc}", (rx, rz), fontsize=7,
                    color="red", fontweight="bold",
                    xytext=(12, 0), textcoords="offset points",
                    bbox=dict(boxstyle="round,pad=0.3", facecolor="lightyellow",
                              alpha=0.8))

    # 드리프트 위험 구간 강조
    ax.fill_between([34, 42], 64, 75, alpha=0.1, color="red",
                    label="U턴 드리프트 위험 구간")

    ax.set_xlabel("X (m) — 동서")
    ax.set_ylabel("Z (m) — 남북")
    ax.set_title("보정 앵커 최적 배치 제안")
    ax.set_aspect("equal")
    ax.legend(fontsize=8, loc="lower left")
    ax.grid(True, alpha=0.2)
    ax.set_xlim(30, 45)
    ax.set_ylim(-12, 78)
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, "corner_drift_anchor_plan.png"), dpi=150)
    print(f"[저장] {OUT}/corner_drift_anchor_plan.png")
    plt.close(fig)


def print_summary(sessions):
    """세션별 코너 구간 통계 요약 (distance_m 기반)"""
    print("\n" + "=" * 80)
    print("코너/U턴 드리프트 진단 요약 (런타임 distance_m 기반)")
    print("=" * 80)

    for df in sessions:
        name = df.session.iloc[0]
        calib = df.calib_source.iloc[-1]
        heading = df.heading_offset_deg.iloc[-1]
        wps = df.current_wp_id.unique()

        print(f"\n── {name.split('_20260')[0]} ({calib}, heading={heading:.1f}°) ──")
        print(f"   도달 WP: {', '.join(wps)}")

        for wp_id in ["WP02", "WP03", "WP04", "WP05", "WP06", "WP07", "WP08"]:
            if wp_id not in wps:
                continue
            sub = df[df.current_wp_id == wp_id]
            eff_r = get_effective_radius(wp_id, calib)
            dwell = (sub.ts.iloc[-1] - sub.ts.iloc[0]).total_seconds()
            marker = " ← 미도달!" if sub.distance_m.min() > eff_r else ""

            print(f"   {wp_id}: dist=[{sub.distance_m.min():.1f}, "
                  f"{sub.distance_m.max():.1f}]m, "
                  f"실효반경={eff_r:.1f}m, "
                  f"체류={dwell:.0f}초, "
                  f"anchor={sub.anchor_bound.any()}"
                  f"{marker}")

    # 세션 간 비교
    print("\n" + "-" * 80)
    print("WP별 최소 distance_m 요약 (모든 세션)")
    print("-" * 80)
    wp_ids = ["WP02", "WP03", "WP04", "WP05", "WP06", "WP07", "WP08"]
    for wp_id in wp_ids:
        vals = []
        for df in sessions:
            sub = df[df.current_wp_id == wp_id]
            if len(sub) > 0:
                vals.append(sub.distance_m.min())
        if vals:
            print(f"   {wp_id}: min={min(vals):.1f}m, max={max(vals):.1f}m, "
                  f"mean={np.mean(vals):.1f}m (n={len(vals)})")


def main():
    sessions = load_sessions()
    if not sessions:
        print("[에러] 유효한 nav_trace 세션이 없습니다")
        sys.exit(1)

    print(f"로드된 세션: {len(sessions)}개")
    for df in sessions:
        print(f"  {df.session.iloc[0]}: {len(df)}행, "
              f"calib={df.calib_source.iloc[-1]}, "
              f"WP: {df.current_wp_id.nunique()}개")

    fig1_slam_trajectories(sessions)
    fig2_distance_curves(sessions)
    fig3_corner_analysis(sessions)
    fig4_heading_and_anchor(sessions)
    fig5_anchor_suggestion()
    print_summary(sessions)


if __name__ == "__main__":
    main()
