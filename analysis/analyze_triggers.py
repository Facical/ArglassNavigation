"""
트리거 반응 분석 스크립트 (v2.1)
- 트리거 유형별(T1-T4) 반응시간 분석 (2조건: Glass Only vs Hybrid)
- 트리거 유형별 확신도 변화 분석
- 트리거 유형별 오방향 선택률
- 트리거-기기 전환 연관 분석 (Hybrid 조건)
"""

import warnings
from pathlib import Path

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib
from scipy import stats

matplotlib.rcParams["font.family"] = "AppleGothic"
matplotlib.rcParams["axes.unicode_minus"] = False
warnings.filterwarnings("ignore", category=FutureWarning)

# ──────────────────────────────────────────────
# 1. 설정
# ──────────────────────────────────────────────

DATA_DIR = Path(__file__).resolve().parent.parent / "data"
RAW_DIR = DATA_DIR / "raw"
OUTPUT_DIR = Path(__file__).resolve().parent / "output"
OUTPUT_DIR.mkdir(exist_ok=True)

CONDITIONS = ["glass_only", "hybrid"]
CONDITION_LABELS = ["Glass Only", "Hybrid"]
TRIGGER_TYPES = ["T1", "T2", "T3", "T4"]
TRIGGER_LABELS = {
    "T1": "안내 열화",
    "T2": "정보 불일치",
    "T3": "해상도 부족",
    "T4": "안내 부재",
}
N_PARTICIPANTS = 24
BEAM_CONTENT_EVENTS = [
    "BEAM_TAB_SWITCH", "BEAM_POI_VIEWED", "BEAM_INFO_CARD_OPENED",
    "BEAM_INFO_CARD_CLOSED", "BEAM_MAP_ZOOMED", "BEAM_COMPARISON_VIEWED",
    "BEAM_MISSION_REF_VIEWED",
]
BEAM_CONTENT_TYPES = ["poi_detail", "info_card", "comparison", "map", "mission_ref"]

# 트리거별 기대 콘텐츠 접근 유형
TRIGGER_EXPECTED_CONTENT = {
    "T1": ["map"],                  # 안내 열화 → 지도 줌 접근
    "T2": ["poi_detail", "info_card"],  # 정보 불일치 → POI + 카드
    "T3": ["map"],                  # 해상도 부족 → 지도 줌
    "T4": ["poi_detail"],           # 안내 부재 → POI 상세
}


# ──────────────────────────────────────────────
# 2. 데이터 로드 / 데모 생성
# ──────────────────────────────────────────────

def load_events() -> pd.DataFrame:
    """이벤트 로그 로드 또는 데모 생성."""
    csv_files = sorted(RAW_DIR.glob("P*_*.csv"))
    if csv_files:
        frames = [pd.read_csv(f, parse_dates=["timestamp"]) for f in csv_files]
        return pd.concat(frames, ignore_index=True)
    print("[경고] 이벤트 로그 없음. 데모 데이터 생성.")
    return generate_demo_data()


def generate_demo_data() -> pd.DataFrame:
    """트리거 관련 데모 데이터 생성."""
    rng = np.random.default_rng(88)
    rows = []
    base_time = pd.Timestamp("2026-03-15T10:00:00")

    # 경로별 트리거 배치
    route_triggers = {
        "A": [("WP03", "T1"), ("WP06", "T4")],
        "B": [("WP03", "T2"), ("WP06", "T3")],
    }

    # 반응 시간 기댓값 (조건 × 트리거 유형)
    rt_base = {
        "glass_only":    {"T1": 5.5, "T2": 7.0, "T3": 5.0, "T4": 8.0},
        "hybrid":        {"T1": 3.0, "T2": 4.0, "T3": 3.0, "T4": 4.5},
    }

    # 확신도 변화 기댓값 (트리거 전후)
    conf_drop = {
        "glass_only":    {"T1": -1.5, "T2": -2.0, "T3": -1.0, "T4": -2.5},
        "hybrid":        {"T1": -0.5, "T2": -0.8, "T3": -0.4, "T4": -1.0},
    }

    # 오방향 선택률 기댓값
    wrong_dir_base = {
        "glass_only":    {"T1": 0.18, "T2": 0.35, "T3": 0.22, "T4": 0.40},
        "hybrid":        {"T1": 0.05, "T2": 0.12, "T3": 0.08, "T4": 0.15},
    }

    for pid in range(1, N_PARTICIPANTS + 1):
        participant_id = f"P{pid:02d}"
        # 참가자마다 하나의 경로 배정 (간략화: 순환)
        route = ["A", "B"][pid % 2]

        for cond in CONDITIONS:
            t = base_time

            for wp, ttype in route_triggers[route]:
                # 트리거 전 확신도
                pre_conf = int(np.clip(round(rng.normal(5.5, 0.8)), 1, 7))
                t += pd.Timedelta(seconds=rng.integers(60, 120))
                rows.append(_event(t, participant_id, cond, "CONFIDENCE_RATED",
                                   _prev_wp(wp), confidence=pre_conf))

                # 트리거 활성화
                t += pd.Timedelta(seconds=rng.integers(10, 30))
                rows.append(_event(t, participant_id, cond, "TRIGGER_ACTIVATED", wp,
                                   trigger_type=ttype))

                # 반응 시간
                rt = max(1, rng.normal(rt_base[cond][ttype], 1.5))

                # Hybrid: 기기 전환 여부
                if cond == "hybrid":
                    switch_prob = {"T1": 0.70, "T2": 0.80, "T3": 0.60, "T4": 0.85}
                    if rng.random() < switch_prob[ttype]:
                        t += pd.Timedelta(seconds=rt * 0.3)
                        rows.append(_event(t, participant_id, cond, "BEAM_SCREEN_ON", wp))
                        # v2.1: 트리거별 콘텐츠 접근 이벤트 생성
                        sub_t = t + pd.Timedelta(seconds=rng.uniform(0.5, 1.5))
                        expected_types = TRIGGER_EXPECTED_CONTENT.get(ttype, ["map"])
                        for exp_ct in expected_types:
                            if exp_ct == "map":
                                rows.append(_event(sub_t, participant_id, cond,
                                                   "BEAM_MAP_ZOOMED", wp,
                                                   beam_content_type="map",
                                                   zoom_level=round(rng.uniform(1.5, 3.0), 1)))
                            elif exp_ct == "poi_detail":
                                rows.append(_event(sub_t, participant_id, cond,
                                                   "BEAM_POI_VIEWED", wp,
                                                   beam_content_type="poi_detail",
                                                   poi_id=f"poi_{rng.integers(1, 10):02d}",
                                                   poi_type="meeting_room",
                                                   view_duration_s=round(rng.uniform(2, 6), 1)))
                            elif exp_ct == "info_card":
                                card_id = f"card_{rng.integers(1, 5):02d}"
                                rows.append(_event(sub_t, participant_id, cond,
                                                   "BEAM_INFO_CARD_OPENED", wp,
                                                   beam_content_type="info_card",
                                                   card_id=card_id,
                                                   card_type="sign_card",
                                                   auto_shown=True))
                                sub_t += pd.Timedelta(seconds=rng.uniform(1, 4))
                                rows.append(_event(sub_t, participant_id, cond,
                                                   "BEAM_INFO_CARD_CLOSED", wp,
                                                   beam_content_type="info_card",
                                                   card_id=card_id,
                                                   view_duration_s=round(rng.uniform(1, 4), 1)))
                            sub_t += pd.Timedelta(seconds=rng.uniform(0.5, 2))
                        beam_dur = (sub_t - t).total_seconds() + rng.uniform(0.5, 2)
                        t = sub_t + pd.Timedelta(seconds=rng.uniform(0.5, 2))
                        rows.append(_event(t, participant_id, cond, "BEAM_SCREEN_OFF", wp,
                                           duration_s=round(beam_dur, 1)))

                # 트리거 비활성화
                t += pd.Timedelta(seconds=rt)
                rows.append(_event(t, participant_id, cond, "TRIGGER_DEACTIVATED", wp,
                                   trigger_type=ttype, duration_s=round(rt + rng.uniform(2, 5), 1)))

                # 오방향 선택 여부
                wrong_dir = rng.random() < wrong_dir_base[cond][ttype]
                if wrong_dir:
                    rows.append(_event(t, participant_id, cond, "WAYPOINT_SKIPPED", wp,
                                       reason="wrong_turn"))

                # 트리거 후 확신도
                post_conf_val = pre_conf + conf_drop[cond][ttype] + rng.normal(0, 0.5)
                post_conf = int(np.clip(round(post_conf_val), 1, 7))
                t += pd.Timedelta(seconds=rng.integers(20, 50))
                rows.append(_event(t, participant_id, cond, "CONFIDENCE_RATED", wp,
                                   confidence=post_conf))

                # 반응 시간 기록 이벤트
                rows.append(_event(t, participant_id, cond, "TRIGGER_RESPONSE", wp,
                                   trigger_type=ttype, reaction_time_s=round(rt, 1),
                                   wrong_direction=wrong_dir))

    df = pd.DataFrame(rows)
    df["timestamp"] = pd.to_datetime(df["timestamp"], format="ISO8601")
    return df


def _prev_wp(wp: str) -> str:
    """이전 웨이포인트 ID 반환."""
    num = int(wp.replace("WP", ""))
    return f"WP{num - 1:02d}" if num > 1 else "WP01"


def _event(t, pid, cond, etype, wp, **extra) -> dict:
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
        "extra_data": str({k: v for k, v in extra.items()
                          if k not in ("confidence", "beam_content_type")}) if extra else "{}",
    }
    return row


def _parse_extra(extra_str) -> dict:
    if isinstance(extra_str, str) and extra_str:
        try:
            return eval(extra_str)
        except Exception:
            return {}
    return {}


# ──────────────────────────────────────────────
# 3. 트리거 반응시간 분석
# ──────────────────────────────────────────────

def analyze_trigger_reaction_time(df: pd.DataFrame) -> pd.DataFrame:
    """트리거 유형별, 조건별 반응시간 분석."""
    tr = df[df["event_type"] == "TRIGGER_RESPONSE"].copy()
    tr["parsed"] = tr["extra_data"].apply(_parse_extra)
    tr["trigger_type"] = tr["parsed"].apply(lambda d: d.get("trigger_type", ""))
    tr["reaction_time_s"] = tr["parsed"].apply(lambda d: d.get("reaction_time_s", np.nan))
    tr = tr.dropna(subset=["reaction_time_s"])

    print("\n=== 트리거 반응시간 분석 ===")
    results = []
    for ttype in TRIGGER_TYPES:
        t_label = TRIGGER_LABELS.get(ttype, ttype)
        print(f"\n  {ttype} ({t_label}):")
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            subset = tr[(tr["trigger_type"] == ttype) & (tr["condition"] == cond)]
            if len(subset) > 0:
                m = subset["reaction_time_s"].mean()
                sd = subset["reaction_time_s"].std()
                print(f"    {label}: M={m:.1f}s, SD={sd:.1f}s (n={len(subset)})")
                results.append({
                    "trigger_type": ttype, "trigger_label": t_label,
                    "condition": label, "mean_rt_s": round(m, 1),
                    "sd_rt_s": round(sd, 1), "n": len(subset),
                })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 4. 트리거 유형별 확신도 변화 분석
# ──────────────────────────────────────────────

def analyze_trigger_confidence_drop(df: pd.DataFrame) -> pd.DataFrame:
    """트리거 전후 확신도 변화량을 트리거 유형별로 분석."""
    triggers = df[df["event_type"] == "TRIGGER_ACTIVATED"].copy()
    triggers["parsed"] = triggers["extra_data"].apply(_parse_extra)
    triggers["trigger_type"] = triggers["parsed"].apply(lambda d: d.get("trigger_type", ""))

    conf_events = df[df["event_type"] == "CONFIDENCE_RATED"].copy()
    conf_events["confidence_rating"] = pd.to_numeric(conf_events["confidence_rating"], errors="coerce")

    print("\n=== 트리거 유형별 확신도 변화 ===")
    results = []
    for ttype in TRIGGER_TYPES:
        t_label = TRIGGER_LABELS.get(ttype, ttype)
        tt_triggers = triggers[triggers["trigger_type"] == ttype]

        print(f"\n  {ttype} ({t_label}):")
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            cond_triggers = tt_triggers[tt_triggers["condition"] == cond]
            drops = []

            for _, trig in cond_triggers.iterrows():
                pid = trig["participant_id"]
                trig_time = trig["timestamp"]

                pid_conf = conf_events[
                    (conf_events["participant_id"] == pid) &
                    (conf_events["condition"] == cond)
                ].sort_values("timestamp")

                pre = pid_conf[pid_conf["timestamp"] < trig_time]
                post = pid_conf[pid_conf["timestamp"] > trig_time]

                if not pre.empty and not post.empty:
                    pre_val = pre.iloc[-1]["confidence_rating"]
                    post_val = post.iloc[0]["confidence_rating"]
                    if not np.isnan(pre_val) and not np.isnan(post_val):
                        drops.append(post_val - pre_val)

            if drops:
                m = np.mean(drops)
                sd = np.std(drops)
                print(f"    {label}: Δ확신도 = {m:+.2f} (SD={sd:.2f}, n={len(drops)})")
                results.append({
                    "trigger_type": ttype, "trigger_label": t_label,
                    "condition": label, "mean_drop": round(m, 2),
                    "sd_drop": round(sd, 2), "n": len(drops),
                })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 5. 오방향 선택률 분석
# ──────────────────────────────────────────────

def analyze_wrong_direction(df: pd.DataFrame) -> pd.DataFrame:
    """트리거 유형별 오방향 선택률 분석."""
    tr = df[df["event_type"] == "TRIGGER_RESPONSE"].copy()
    tr["parsed"] = tr["extra_data"].apply(_parse_extra)
    tr["trigger_type"] = tr["parsed"].apply(lambda d: d.get("trigger_type", ""))
    tr["wrong_direction"] = tr["parsed"].apply(lambda d: d.get("wrong_direction", False))

    print("\n=== 오방향 선택률 분석 ===")
    results = []
    for ttype in TRIGGER_TYPES:
        t_label = TRIGGER_LABELS.get(ttype, ttype)
        print(f"\n  {ttype} ({t_label}):")
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            subset = tr[(tr["trigger_type"] == ttype) & (tr["condition"] == cond)]
            if len(subset) > 0:
                wrong_rate = subset["wrong_direction"].astype(float).mean()
                print(f"    {label}: {wrong_rate:.1%} ({subset['wrong_direction'].sum()}/{len(subset)})")
                results.append({
                    "trigger_type": ttype, "trigger_label": t_label,
                    "condition": label, "wrong_rate": round(wrong_rate, 3),
                    "n": len(subset),
                })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 6. Hybrid 조건 트리거-기기전환 연관 분석
# ──────────────────────────────────────────────

def analyze_trigger_switching(df: pd.DataFrame) -> pd.DataFrame:
    """트리거 유형별 Beam Pro 전환 확률 (Hybrid 조건)."""
    hybrid = df[df["condition"] == "hybrid"]
    triggers = hybrid[hybrid["event_type"] == "TRIGGER_ACTIVATED"].copy()
    triggers["parsed"] = triggers["extra_data"].apply(_parse_extra)
    triggers["trigger_type"] = triggers["parsed"].apply(lambda d: d.get("trigger_type", ""))
    beam_ons = hybrid[hybrid["event_type"] == "BEAM_SCREEN_ON"]

    print("\n=== 트리거-기기전환 연관 (Hybrid) ===")
    results = []
    for ttype in TRIGGER_TYPES:
        tt_triggers = triggers[triggers["trigger_type"] == ttype]
        switch_count = 0
        total = 0

        for _, trig in tt_triggers.iterrows():
            pid = trig["participant_id"]
            trig_time = trig["timestamp"]
            total += 1

            pid_beams = beam_ons[beam_ons["participant_id"] == pid]
            post_beams = pid_beams[
                (pid_beams["timestamp"] > trig_time) &
                (pid_beams["timestamp"] < trig_time + pd.Timedelta(seconds=30))
            ]
            if len(post_beams) > 0:
                switch_count += 1

        rate = switch_count / total if total > 0 else 0
        t_label = TRIGGER_LABELS.get(ttype, ttype)
        print(f"  {ttype} ({t_label}): 전환율 = {rate:.1%} ({switch_count}/{total})")
        results.append({
            "trigger_type": ttype, "trigger_label": t_label,
            "switch_rate": round(rate, 3), "n": total,
        })

    # v2.1: 트리거별 콘텐츠 접근 유형 분석
    content_events = hybrid[hybrid["event_type"].isin(BEAM_CONTENT_EVENTS)]
    if not content_events.empty and "beam_content_type" in content_events.columns:
        print(f"\n  [트리거별 콘텐츠 접근 유형 (v2.1)]")
        for ttype in TRIGGER_TYPES:
            tt_triggers = triggers[triggers["trigger_type"] == ttype]
            ct_counts = {ct: 0 for ct in BEAM_CONTENT_TYPES}

            for _, trig in tt_triggers.iterrows():
                pid = trig["participant_id"]
                trig_time = trig["timestamp"]
                post_content = content_events[
                    (content_events["participant_id"] == pid) &
                    (content_events["timestamp"] > trig_time) &
                    (content_events["timestamp"] < trig_time + pd.Timedelta(seconds=30))
                ]
                for ct in post_content["beam_content_type"].dropna():
                    if ct in ct_counts:
                        ct_counts[ct] += 1

            t_label = TRIGGER_LABELS.get(ttype, ttype)
            top_ct = max(ct_counts, key=ct_counts.get) if any(ct_counts.values()) else "none"
            print(f"    {ttype} ({t_label}): 주요 콘텐츠={top_ct}, "
                  f"분포={{{', '.join(f'{k}:{v}' for k, v in ct_counts.items() if v > 0)}}}")

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 7. 시각화
# ──────────────────────────────────────────────

def plot_reaction_time_by_trigger(rt_df: pd.DataFrame):
    """트리거 유형별 반응시간 그룹 바 차트."""
    if rt_df.empty:
        return

    fig, ax = plt.subplots(figsize=(10, 6))
    x = np.arange(len(TRIGGER_TYPES))
    width = 0.35

    for i, label in enumerate(CONDITION_LABELS):
        rts = []
        for tt in TRIGGER_TYPES:
            row = rt_df[(rt_df["trigger_type"] == tt) & (rt_df["condition"] == label)]
            rts.append(row["mean_rt_s"].values[0] if len(row) > 0 else 0)
        ax.bar(x + i * width, rts, width, label=label)

    ax.set_xlabel("트리거 유형")
    ax.set_ylabel("반응시간 (s)")
    ax.set_title("트리거 유형별 조건 간 반응시간")
    ax.set_xticks(x + width / 2)
    ax.set_xticklabels([f"{tt}\n({TRIGGER_LABELS[tt]})" for tt in TRIGGER_TYPES])
    ax.legend()
    fig.tight_layout()
    fig.savefig(OUTPUT_DIR / "trigger_reaction_time.png", dpi=150)
    print(f"  → {OUTPUT_DIR / 'trigger_reaction_time.png'} 저장")
    plt.close(fig)


def plot_confidence_drop_by_trigger(drop_df: pd.DataFrame):
    """트리거 유형별 확신도 변화량 히트맵."""
    if drop_df.empty:
        return

    fig, ax = plt.subplots(figsize=(10, 4))

    pivot = drop_df.pivot_table(
        values="mean_drop", index="condition", columns="trigger_type"
    )
    cond_order = CONDITION_LABELS
    trigger_order = TRIGGER_TYPES
    pivot = pivot.reindex(index=cond_order, columns=trigger_order)

    im = ax.imshow(pivot.values, cmap="RdYlGn", aspect="auto", vmin=-3, vmax=0)
    ax.set_xticks(range(len(trigger_order)))
    ax.set_xticklabels([f"{tt}\n({TRIGGER_LABELS[tt]})" for tt in trigger_order])
    ax.set_yticks(range(len(cond_order)))
    ax.set_yticklabels(cond_order)

    for i in range(len(cond_order)):
        for j in range(len(trigger_order)):
            val = pivot.values[i, j]
            if not np.isnan(val):
                ax.text(j, i, f"{val:+.1f}", ha="center", va="center",
                        color="white" if val < -1.5 else "black", fontsize=11)

    ax.set_title("트리거 유형별 확신도 변화량 (Δ)")
    fig.colorbar(im, ax=ax, label="확신도 변화 (Δ)")
    fig.tight_layout()
    fig.savefig(OUTPUT_DIR / "trigger_confidence_heatmap.png", dpi=150)
    print(f"  → {OUTPUT_DIR / 'trigger_confidence_heatmap.png'} 저장")
    plt.close(fig)


def plot_trigger_switch_rate(switch_df: pd.DataFrame):
    """트리거 유형별 Hybrid 기기 전환율 바 차트."""
    if switch_df.empty:
        return

    fig, ax = plt.subplots(figsize=(8, 5))
    colors = ["#e74c3c", "#3498db", "#2ecc71", "#f39c12"]
    bars = ax.bar(
        [f"{tt}\n({TRIGGER_LABELS[tt]})" for tt in TRIGGER_TYPES],
        [switch_df[switch_df["trigger_type"] == tt]["switch_rate"].values[0] * 100
         if len(switch_df[switch_df["trigger_type"] == tt]) > 0 else 0
         for tt in TRIGGER_TYPES],
        color=colors,
    )
    ax.set_ylabel("Beam Pro 전환율 (%)")
    ax.set_title("트리거 유형별 Beam Pro 전환율 (Hybrid 조건)")
    ax.set_ylim(0, 100)

    for bar in bars:
        height = bar.get_height()
        ax.text(bar.get_x() + bar.get_width() / 2., height + 1,
                f"{height:.0f}%", ha="center", va="bottom")

    fig.tight_layout()
    fig.savefig(OUTPUT_DIR / "trigger_switch_rate.png", dpi=150)
    print(f"  → {OUTPUT_DIR / 'trigger_switch_rate.png'} 저장")
    plt.close(fig)


# ──────────────────────────────────────────────
# 8. 메인
# ──────────────────────────────────────────────

def main():
    print("=" * 60)
    print("트리거 반응 분석 (v2.1)")
    print("=" * 60)

    df = load_events()
    print(f"총 이벤트 수: {len(df)}")

    # 분석
    rt_df = analyze_trigger_reaction_time(df)
    drop_df = analyze_trigger_confidence_drop(df)
    wrong_df = analyze_wrong_direction(df)
    switch_df = analyze_trigger_switching(df)

    # 시각화
    print(f"\n=== 시각화 ===")
    plot_reaction_time_by_trigger(rt_df)
    plot_confidence_drop_by_trigger(drop_df)
    plot_trigger_switch_rate(switch_df)

    # 결과 저장
    for name, result_df in [("trigger_reaction_time", rt_df),
                             ("trigger_confidence_drop", drop_df),
                             ("trigger_wrong_direction", wrong_df),
                             ("trigger_switch_rate", switch_df)]:
        if not result_df.empty:
            result_df.to_csv(OUTPUT_DIR / f"{name}.csv", index=False)
            print(f"  → {OUTPUT_DIR / f'{name}.csv'} 저장")

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
