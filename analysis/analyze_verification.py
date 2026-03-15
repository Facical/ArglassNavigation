"""
미션 정확도 및 검증 행동 분석 스크립트 (v2.1)
- 미션 타입별 정확도 비교 (2조건: Glass Only vs Hybrid)
- 검증 행동 분류 (proactive vs reactive)
- 미션별 소요시간 분석
- 난이도 평정 분석
- 통계: Paired t-test / Wilcoxon signed-rank
- [ISMAR] view_duration_s (BEAM_INFO_CARD_CLOSED), duration_s (MISSION_COMPLETE) 통합
"""

import sys
import json
import argparse
import warnings
from pathlib import Path

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib
from scipy import stats

from parse_utils import parse_extra
from stat_utils import paired_comparison, significance_marker, add_significance_bracket
from plot_style import (apply_style, save_fig, violin_with_dots,
                        COLORS_COND, COND_LABELS, COLOR_GLASS, COLOR_HYBRID, DPI)

apply_style()
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
MISSION_TYPES = {"A1": "A", "A2": "A", "B1": "B", "B2": "B", "C1": "C"}
MISSION_TYPE_LABELS = {"A": "방향+검증", "B": "모호한 의사결정", "C": "정보 통합"}
BEAM_CONTENT_EVENTS = [
    "BEAM_TAB_SWITCH", "BEAM_POI_VIEWED", "BEAM_INFO_CARD_OPENED",
    "BEAM_INFO_CARD_CLOSED", "BEAM_MAP_ZOOMED", "BEAM_COMPARISON_VIEWED",
    "BEAM_MISSION_REF_VIEWED",
]
N_PARTICIPANTS = 24


# ──────────────────────────────────────────────
# 2. 데이터 로드 / 데모 생성
# ──────────────────────────────────────────────

def load_events(allow_demo: bool = False) -> pd.DataFrame:
    """이벤트 로그 로드 또는 데모 생성."""
    # [ISMAR] sidecar 파일 제외
    SIDECAR_SUFFIXES = ("_head_pose.csv", "_nav_trace.csv", "_beam_segments.csv",
                        "_anchor_reloc.csv", "_system_health.csv")
    csv_files = sorted(
        f for f in RAW_DIR.glob("P*_*.csv")
        if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
    )
    if csv_files:
        frames = [pd.read_csv(f, parse_dates=["timestamp"]) for f in csv_files]
        return pd.concat(frames, ignore_index=True)
    if allow_demo:
        print("[경고] 이벤트 로그 없음. 데모 데이터 생성.")
        return generate_demo_data()
    print(f"[오류] {RAW_DIR}에 이벤트 로그 없음. 데모로 실행하려면 --demo 플래그를 사용하세요.")
    sys.exit(1)


def generate_demo_data() -> pd.DataFrame:
    """미션 관련 데모 데이터 생성."""
    rng = np.random.default_rng(42)
    rows = []
    missions = ["A1", "B1", "A2", "B2", "C1"]
    mission_end_wps = {"A1": "WP02", "B1": "WP03", "A2": "WP05", "B2": "WP06", "C1": "WP08"}
    base_time = pd.Timestamp("2026-03-15T10:00:00")

    acc_base = {
        "glass_only":    {"A": 0.70, "B": 0.50, "C": 0.55},
        "hybrid":        {"A": 0.90, "B": 0.82, "C": 0.85},
    }
    dur_base = {
        "glass_only":    {"A": 75, "B": 65, "C": 95},
        "hybrid":        {"A": 80, "B": 75, "C": 105},
    }
    diff_base = {
        "glass_only":    {"A": 3.5, "B": 5.2, "C": 5.0},
        "hybrid":        {"A": 2.5, "B": 3.5, "C": 3.2},
    }

    for pid in range(1, N_PARTICIPANTS + 1):
        participant_id = f"P{pid:02d}"
        for cond in CONDITIONS:
            t = base_time
            for m_id in missions:
                m_type = MISSION_TYPES[m_id]
                wp = mission_end_wps[m_id]

                # MISSION_START
                rows.append({
                    "timestamp": t.isoformat(),
                    "participant_id": participant_id,
                    "condition": cond,
                    "event_type": "MISSION_START",
                    "waypoint_id": wp,
                    "extra_data": json.dumps({"mission_id": m_id, "type": m_type}),
                })

                dur = max(30, rng.normal(dur_base[cond][m_type], 20))
                t += pd.Timedelta(seconds=dur)

                # Hybrid: Beam Pro 참조 여부
                beam_referenced = False
                if cond == "hybrid":
                    ref_prob = 0.5 if m_type == "A" else (0.7 if m_type == "B" else 0.6)
                    beam_referenced = rng.random() < ref_prob
                    if beam_referenced:
                        ref_time = t - pd.Timedelta(seconds=rng.uniform(5, 30))
                        rows.append({
                            "timestamp": ref_time.isoformat(),
                            "participant_id": participant_id,
                            "condition": cond,
                            "event_type": "BEAM_SCREEN_ON",
                            "waypoint_id": wp,
                            "extra_data": "{}",
                        })

                # VERIFICATION_ANSWERED
                correct = bool(rng.random() < acc_base[cond][m_type])
                rt = max(1, rng.normal(5, 2))
                rows.append({
                    "timestamp": t.isoformat(),
                    "participant_id": participant_id,
                    "condition": cond,
                    "event_type": "VERIFICATION_ANSWERED",
                    "waypoint_id": wp,
                    "extra_data": json.dumps({
                        "mission_id": m_id, "correct": correct,
                        "rt_s": round(rt, 1),
                    }),
                })

                # MISSION_COMPLETE
                rows.append({
                    "timestamp": t.isoformat(),
                    "participant_id": participant_id,
                    "condition": cond,
                    "event_type": "MISSION_COMPLETE",
                    "waypoint_id": wp,
                    "extra_data": json.dumps({
                        "mission_id": m_id, "correct": correct,
                        "duration_s": round(dur, 1),
                    }),
                })

                # DIFFICULTY_RATED
                diff = int(np.clip(round(rng.normal(diff_base[cond][m_type], 1)), 1, 7))
                rows.append({
                    "timestamp": t.isoformat(),
                    "participant_id": participant_id,
                    "condition": cond,
                    "event_type": "DIFFICULTY_RATED",
                    "waypoint_id": wp,
                    "extra_data": json.dumps({"mission_id": m_id, "rating": diff}),
                })

                t += pd.Timedelta(seconds=rng.integers(5, 15))

    df = pd.DataFrame(rows)
    for col in ["head_rotation_x", "head_rotation_y", "head_rotation_z",
                 "device_active", "confidence_rating", "mission_id",
                 "difficulty_rating", "verification_correct"]:
        if col not in df.columns:
            df[col] = ""
    df["timestamp"] = pd.to_datetime(df["timestamp"], format="ISO8601")
    return df


def _parse_extra(extra_str) -> dict:
    """extra_data 문자열을 딕셔너리로 파싱."""
    return parse_extra(extra_str)


# ──────────────────────────────────────────────
# 3. 미션 정확도 분석
# ──────────────────────────────────────────────

def analyze_mission_accuracy(df: pd.DataFrame) -> pd.DataFrame:
    """조건별, 미션 타입별 정확도 분석."""
    mc = df[df["event_type"] == "MISSION_COMPLETE"].copy()
    mc["parsed"] = mc["extra_data"].apply(_parse_extra)
    mc["mission_id"] = mc["parsed"].apply(lambda d: d.get("mission_id", ""))
    mc["correct"] = mc["parsed"].apply(lambda d: d.get("correct", None))
    mc["mission_type"] = mc["mission_id"].map(MISSION_TYPES)
    mc = mc.dropna(subset=["correct"])
    mc["correct_num"] = mc["correct"].astype(int)

    print("\n=== 미션 정확도 분석 ===")

    # 전체 정확도 (조건별)
    print("\n  [전체 정확도]")
    overall_results = []
    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        subset = mc[mc["condition"] == cond]
        acc = subset["correct_num"].mean()
        print(f"    {label}: {acc:.1%} ({subset['correct_num'].sum()}/{len(subset)})")
        overall_results.append({"condition": label, "accuracy": round(acc, 3), "n": len(subset)})

    # 미션 타입별 정확도
    print("\n  [미션 타입별 정확도]")
    type_results = []
    for mt, mt_label in MISSION_TYPE_LABELS.items():
        print(f"\n    미션 타입 {mt} ({mt_label}):")
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            subset = mc[(mc["condition"] == cond) & (mc["mission_type"] == mt)]
            if len(subset) > 0:
                acc = subset["correct_num"].mean()
                print(f"      {label}: {acc:.1%} (n={len(subset)})")
                type_results.append({
                    "mission_type": mt, "condition": label,
                    "accuracy": round(acc, 3), "n": len(subset),
                })

    # 참가자별 정확도 (조건 간 비교용)
    pid_acc = mc.groupby(["participant_id", "condition"])["correct_num"].mean().reset_index(
        name="accuracy"
    )

    _run_paired_test(pid_acc, "accuracy", "미션 정확도 (전체)")

    return pd.DataFrame(type_results)


# ──────────────────────────────────────────────
# 4. 검증 행동 분류
# ──────────────────────────────────────────────

def analyze_verification_behavior(df: pd.DataFrame) -> pd.DataFrame:
    """Hybrid 조건에서 검증 행동을 proactive/reactive로 분류."""
    hybrid = df[df["condition"] == "hybrid"]
    missions = hybrid[hybrid["event_type"] == "MISSION_START"].copy()
    verifications = hybrid[hybrid["event_type"] == "VERIFICATION_ANSWERED"].copy()
    beam_ons = hybrid[hybrid["event_type"] == "BEAM_SCREEN_ON"].copy()

    if missions.empty or verifications.empty:
        print("\n=== 검증 행동 분류 (Hybrid) ===")
        print("  [경고] 데이터 부족")
        return pd.DataFrame()

    missions["parsed"] = missions["extra_data"].apply(_parse_extra)
    missions["mission_id"] = missions["parsed"].apply(lambda d: d.get("mission_id", ""))
    verifications["parsed"] = verifications["extra_data"].apply(_parse_extra)
    verifications["mission_id"] = verifications["parsed"].apply(lambda d: d.get("mission_id", ""))
    verifications["correct"] = verifications["parsed"].apply(lambda d: d.get("correct", None))

    behaviors = []
    for pid in hybrid["participant_id"].unique():
        pid_missions = missions[missions["participant_id"] == pid]
        pid_verif = verifications[verifications["participant_id"] == pid]
        pid_beams = beam_ons[beam_ons["participant_id"] == pid]

        for _, m_row in pid_missions.iterrows():
            m_id = m_row["mission_id"]
            m_start = m_row["timestamp"]

            v_match = pid_verif[pid_verif["mission_id"] == m_id]
            if v_match.empty:
                continue
            v_row = v_match.iloc[0]
            m_end = v_row["timestamp"]

            refs = pid_beams[(pid_beams["timestamp"] >= m_start) & (pid_beams["timestamp"] <= m_end)]

            # v2.1: 콘텐츠 기반 검증 행동 분류
            pid_content = hybrid[
                (hybrid["participant_id"] == pid) &
                (hybrid["event_type"].isin(BEAM_CONTENT_EVENTS)) &
                (hybrid["timestamp"] >= m_start) &
                (hybrid["timestamp"] <= m_end)
            ]
            content_types = pid_content["beam_content_type"].unique().tolist() if (
                "beam_content_type" in pid_content.columns and not pid_content.empty
            ) else []

            if len(refs) == 0 and len(pid_content) == 0:
                behavior = "none"
            else:
                first_ref_time = refs["timestamp"].min() if len(refs) > 0 else (
                    pid_content["timestamp"].min() if not pid_content.empty else m_end
                )
                mission_duration = (m_end - m_start).total_seconds()
                ref_timing = (first_ref_time - m_start).total_seconds()
                is_early = (ref_timing / mission_duration < 0.4) if mission_duration > 0 else False

                if is_early:
                    if "poi_detail" in content_types or "info_card" in content_types:
                        behavior = "proactive_poi"
                    else:
                        behavior = "proactive_map"
                else:
                    if "comparison" in content_types:
                        behavior = "reactive_comparison"
                    elif "info_card" in content_types or "poi_detail" in content_types:
                        behavior = "reactive_info_card"
                    else:
                        behavior = "reactive_info_card"  # default reactive with beam reference

            behaviors.append({
                "participant_id": pid,
                "mission_id": m_id,
                "mission_type": MISSION_TYPES.get(m_id, ""),
                "behavior": behavior,
                "correct": v_row["correct"],
                "beam_ref_count": len(refs),
            })

    beh_df = pd.DataFrame(behaviors)

    print("\n=== 검증 행동 분류 (Hybrid 조건) ===")
    behavior_types = ["proactive_poi", "proactive_map", "reactive_info_card",
                      "reactive_comparison", "none"]
    if not beh_df.empty:
        counts = beh_df["behavior"].value_counts()
        total = len(beh_df)
        for beh in behavior_types:
            n = counts.get(beh, 0)
            print(f"  {beh}: {n}회 ({n/total:.1%})")

        print("\n  행동 유형별 정확도:")
        for beh in behavior_types:
            subset = beh_df[beh_df["behavior"] == beh]
            if len(subset) > 0 and subset["correct"].notna().any():
                acc = subset["correct"].astype(float).mean()
                print(f"    {beh}: {acc:.1%} (n={len(subset)})")

    return beh_df


# ──────────────────────────────────────────────
# 5. 미션별 소요시간 분석
# ──────────────────────────────────────────────

def analyze_mission_duration(df: pd.DataFrame) -> pd.DataFrame:
    """조건별, 미션 타입별 소요시간 분석.

    [ISMAR] duration_s가 top-level 컬럼으로 존재하면 우선 사용 (24-column 포맷).
    없으면 extra_data JSON에서 추출 (하위 호환).
    """
    mc = df[df["event_type"] == "MISSION_COMPLETE"].copy()
    mc["parsed"] = mc["extra_data"].apply(_parse_extra)
    mc["mission_id"] = mc["parsed"].apply(lambda d: d.get("mission_id", ""))

    # [ISMAR] duration_s: top-level 컬럼 우선, 없으면 extra_data에서 추출
    if "duration_s" in mc.columns and mc["duration_s"].notna().any():
        # top-level 컬럼이 있고 값이 존재하면 사용
        mc["duration_s"] = pd.to_numeric(mc["duration_s"], errors="coerce")
        # NaN인 행만 extra_data에서 보충
        mask = mc["duration_s"].isna()
        mc.loc[mask, "duration_s"] = mc.loc[mask, "parsed"].apply(
            lambda d: d.get("duration_s", np.nan)
        )
        print("  [ISMAR] MISSION_COMPLETE duration_s 컬럼 사용")
    else:
        mc["duration_s"] = mc["parsed"].apply(lambda d: d.get("duration_s", np.nan))

    mc["mission_type"] = mc["mission_id"].map(MISSION_TYPES)
    mc = mc.dropna(subset=["duration_s"])

    print("\n=== 미션 소요시간 분석 ===")
    results = []
    for mt, mt_label in MISSION_TYPE_LABELS.items():
        print(f"\n  미션 타입 {mt} ({mt_label}):")
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            subset = mc[(mc["condition"] == cond) & (mc["mission_type"] == mt)]
            if len(subset) > 0:
                m = subset["duration_s"].mean()
                sd = subset["duration_s"].std()
                print(f"    {label}: M={m:.1f}s, SD={sd:.1f}s")
                results.append({
                    "mission_type": mt, "condition": label,
                    "mean_duration_s": round(m, 1), "sd_duration_s": round(sd, 1),
                })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 6. 난이도 평정 분석
# ──────────────────────────────────────────────

def analyze_difficulty_ratings(df: pd.DataFrame) -> pd.DataFrame:
    """조건별, 미션 타입별 주관적 난이도 분석."""
    dr = df[df["event_type"] == "DIFFICULTY_RATED"].copy()
    dr["parsed"] = dr["extra_data"].apply(_parse_extra)
    dr["mission_id"] = dr["parsed"].apply(lambda d: d.get("mission_id", ""))
    dr["rating"] = pd.to_numeric(
        dr["parsed"].apply(lambda d: d.get("rating", np.nan)), errors="coerce"
    )
    dr["mission_type"] = dr["mission_id"].map(MISSION_TYPES)
    dr = dr.dropna(subset=["rating"])

    print("\n=== 난이도 평정 분석 ===")
    results = []
    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        subset = dr[dr["condition"] == cond]
        if len(subset) > 0:
            m = subset["rating"].mean()
            sd = subset["rating"].std()
            print(f"  {label}: M={m:.1f}, SD={sd:.1f}")
            results.append({"condition": label, "mean_difficulty": round(m, 1), "sd": round(sd, 1)})

    pid_diff = dr.groupby(["participant_id", "condition"])["rating"].mean().reset_index()
    _run_paired_test(pid_diff, "rating", "주관적 난이도")

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 6b. 콘텐츠 유형별 열람-정확도 상관 분석 (v2.1)
# ──────────────────────────────────────────────

def analyze_content_accuracy_correlation(df: pd.DataFrame) -> pd.DataFrame:
    """콘텐츠 유형별 열람과 미션 정확도 간 상관 분석 (v2.1)."""
    hybrid = df[df["condition"] == "hybrid"]
    mc = hybrid[hybrid["event_type"] == "MISSION_COMPLETE"].copy()
    content_events = hybrid[hybrid["event_type"].isin(BEAM_CONTENT_EVENTS)]

    if mc.empty or content_events.empty:
        print("\n=== 콘텐츠-정확도 상관 분석 (v2.1) ===")
        print("  [경고] 데이터 부족")
        return pd.DataFrame()

    mc["parsed"] = mc["extra_data"].apply(_parse_extra)
    mc["mission_id"] = mc["parsed"].apply(lambda d: d.get("mission_id", ""))
    mc["correct"] = mc["parsed"].apply(lambda d: d.get("correct", None))
    mc = mc.dropna(subset=["correct"])
    mc["correct_num"] = mc["correct"].astype(int)

    content_types = ["poi_detail", "info_card", "comparison", "map", "mission_ref"]
    results = []

    print("\n=== 콘텐츠-정확도 상관 분석 (v2.1) ===")
    for ct in content_types:
        ct_events_filtered = content_events[
            content_events.get("beam_content_type", pd.Series()) == ct
        ] if "beam_content_type" in content_events.columns else pd.DataFrame()

        # 각 미션에서 해당 콘텐츠를 열람했는지 여부
        mission_ct_access = []
        for _, m_row in mc.iterrows():
            pid = m_row["participant_id"]
            mc_time = m_row["timestamp"]
            accessed = len(ct_events_filtered[
                (ct_events_filtered["participant_id"] == pid) &
                (ct_events_filtered["timestamp"] < mc_time) &
                (ct_events_filtered["timestamp"] > mc_time - pd.Timedelta(seconds=120))
            ]) > 0
            mission_ct_access.append({
                "accessed": accessed,
                "correct": m_row["correct_num"],
            })

        access_df = pd.DataFrame(mission_ct_access)
        if not access_df.empty and access_df["accessed"].sum() > 0:
            acc_with = access_df[access_df["accessed"]]["correct"].mean()
            acc_without = access_df[~access_df["accessed"]]["correct"].mean()
            n_with = access_df["accessed"].sum()
            n_without = (~access_df["accessed"]).sum()
            print(f"  {ct}: 열람 시 {acc_with:.1%} (n={n_with}) vs 미열람 {acc_without:.1%} (n={n_without})")
            results.append({
                "content_type": ct,
                "acc_with_access": round(acc_with, 3),
                "acc_without_access": round(acc_without, 3),
                "n_accessed": int(n_with),
                "n_not_accessed": int(n_without),
            })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 6c. [ISMAR] 열람 시간 → 정확도 상관 분석
# ──────────────────────────────────────────────

def analyze_viewing_time_accuracy(df: pd.DataFrame) -> pd.DataFrame:
    """[ISMAR] BEAM_INFO_CARD_CLOSED의 view_duration_s를 이용한 열람 시간-정확도 상관 분석.

    extra_data JSON에서 view_duration_s를 추출하여,
    미션 완료 직전 정보 카드 열람 시간이 길수록 정확도가 높은지 검증.
    """
    hybrid = df[df["condition"] == "hybrid"]

    # BEAM_INFO_CARD_CLOSED에서 view_duration_s 추출
    card_closed = hybrid[hybrid["event_type"] == "BEAM_INFO_CARD_CLOSED"].copy()
    if card_closed.empty:
        print("\n=== [ISMAR] 열람 시간-정확도 상관 분석 ===")
        print("  [경고] BEAM_INFO_CARD_CLOSED 이벤트 없음")
        return pd.DataFrame()

    card_closed["parsed"] = card_closed["extra_data"].apply(_parse_extra)
    card_closed["view_duration_s"] = card_closed["parsed"].apply(
        lambda d: d.get("view_duration_s", np.nan)
    )
    card_closed = card_closed.dropna(subset=["view_duration_s"])

    if card_closed.empty:
        print("\n=== [ISMAR] 열람 시간-정확도 상관 분석 ===")
        print("  [경고] view_duration_s 데이터 없음")
        return pd.DataFrame()

    # MISSION_COMPLETE에서 정확도 추출
    mc = hybrid[hybrid["event_type"] == "MISSION_COMPLETE"].copy()
    mc["parsed_mc"] = mc["extra_data"].apply(_parse_extra)
    mc["mission_id"] = mc["parsed_mc"].apply(lambda d: d.get("mission_id", ""))
    mc["correct"] = mc["parsed_mc"].apply(lambda d: d.get("correct", None))
    mc = mc.dropna(subset=["correct"])

    if mc.empty:
        print("\n=== [ISMAR] 열람 시간-정확도 상관 분석 ===")
        print("  [경고] MISSION_COMPLETE 정확도 데이터 없음")
        return pd.DataFrame()

    # 각 미션 완료 이전 120초 이내의 카드 열람 시간 집계
    results = []
    for _, m_row in mc.iterrows():
        pid = m_row["participant_id"]
        mc_time = m_row["timestamp"]
        m_id = m_row["mission_id"]

        recent_cards = card_closed[
            (card_closed["participant_id"] == pid) &
            (card_closed["timestamp"] < mc_time) &
            (card_closed["timestamp"] > mc_time - pd.Timedelta(seconds=120))
        ]

        if not recent_cards.empty:
            total_view_time = recent_cards["view_duration_s"].sum()
            mean_view_time = recent_cards["view_duration_s"].mean()
            n_cards = len(recent_cards)
        else:
            total_view_time = 0
            mean_view_time = 0
            n_cards = 0

        results.append({
            "participant_id": pid,
            "mission_id": m_id,
            "correct": int(m_row["correct"]),
            "total_view_time_s": round(total_view_time, 1),
            "mean_view_time_s": round(mean_view_time, 1),
            "n_cards_viewed": n_cards,
        })

    view_df = pd.DataFrame(results)

    print("\n=== [ISMAR] 열람 시간-정확도 상관 분석 ===")
    if not view_df.empty and view_df["n_cards_viewed"].sum() > 0:
        # 카드를 열람한 경우만 상관 분석
        with_cards = view_df[view_df["n_cards_viewed"] > 0]
        if len(with_cards) >= 5:
            from scipy.stats import pointbiserialr
            r, p = pointbiserialr(with_cards["correct"], with_cards["total_view_time_s"])
            print(f"  총 열람시간 vs 정확도: r_pb={r:.3f}, p={p:.4f} (n={len(with_cards)})")

            r2, p2 = pointbiserialr(with_cards["correct"], with_cards["mean_view_time_s"])
            print(f"  평균 열람시간 vs 정확도: r_pb={r2:.3f}, p={p2:.4f}")

        # 열람 vs 미열람 정확도 비교
        viewed = view_df[view_df["n_cards_viewed"] > 0]
        not_viewed = view_df[view_df["n_cards_viewed"] == 0]
        if len(viewed) > 0 and len(not_viewed) > 0:
            print(f"  카드 열람 시 정확도: {viewed['correct'].mean():.1%} (n={len(viewed)})")
            print(f"  카드 미열람 정확도: {not_viewed['correct'].mean():.1%} (n={len(not_viewed)})")

    return view_df


# ──────────────────────────────────────────────
# 7. 통계 검정 유틸리티
# ──────────────────────────────────────────────

def _run_paired_test(data: pd.DataFrame, dv: str, label: str):
    """2조건 Paired t-test (pingouin) 또는 Wilcoxon signed-rank (fallback)."""
    try:
        import pingouin as pg
        test = pg.pairwise_tests(
            data=data, dv=dv, within="condition", subject="participant_id",
            parametric=True
        )
        if not test.empty:
            row = test.iloc[0]
            print(f"    Paired t-test ({label}): t={row['T']:.2f}, p={row.get('p-unc', row.get('p_unc', 0)):.4f}, "
                  f"d={row['hedges']:.2f}")
    except (ImportError, ValueError):
        glass_vals = data[data["condition"] == "glass_only"][dv].dropna().values
        hybrid_vals = data[data["condition"] == "hybrid"][dv].dropna().values
        min_len = min(len(glass_vals), len(hybrid_vals))
        if min_len > 0:
            t_stat, t_p = stats.ttest_rel(glass_vals[:min_len], hybrid_vals[:min_len])
            print(f"    Paired t-test ({label}): t={t_stat:.2f}, p={t_p:.4f}")
            w_stat, w_p = stats.wilcoxon(glass_vals[:min_len], hybrid_vals[:min_len])
            print(f"    Wilcoxon ({label}): W={w_stat:.1f}, p={w_p:.4f}")
        else:
            print(f"    [경고] {label}: 데이터 부족, 검정 불가")


# ──────────────────────────────────────────────
# 8. 시각화
# ──────────────────────────────────────────────

def plot_accuracy_by_type(df: pd.DataFrame, type_results: pd.DataFrame):
    """미션 타입별 정확도 그룹 바 차트."""
    if type_results.empty:
        return

    fig, ax = plt.subplots(figsize=(10, 6))
    mission_types = list(MISSION_TYPE_LABELS.keys())
    x = np.arange(len(mission_types))
    width = 0.35

    for i, label in enumerate(CONDITION_LABELS):
        accs = []
        for mt in mission_types:
            row = type_results[(type_results["mission_type"] == mt) &
                               (type_results["condition"] == label)]
            accs.append(row["accuracy"].values[0] * 100 if len(row) > 0 else 0)
        ax.bar(x + i * width, accs, width, label=label)

    ax.set_xlabel("미션 타입")
    ax.set_ylabel("정확도 (%)")
    ax.set_title("미션 타입별 조건 간 정확도 비교")
    ax.set_xticks(x + width / 2)
    ax.set_xticklabels([f"{k}\n({v})" for k, v in MISSION_TYPE_LABELS.items()])
    ax.legend()
    ax.set_ylim(0, 100)
    # Add significance markers for each mission type
    for j, mt in enumerate(mission_types):
        mc_data = df[df["event_type"] == "MISSION_COMPLETE"].copy()
        mc_data["parsed_sig"] = mc_data["extra_data"].apply(_parse_extra)
        mc_data["mission_id_sig"] = mc_data["parsed_sig"].apply(lambda d: d.get("mission_id", ""))
        mc_data["correct_sig"] = mc_data["parsed_sig"].apply(lambda d: d.get("correct", None))
        mc_data["mission_type_sig"] = mc_data["mission_id_sig"].map(MISSION_TYPES)
        mc_mt = mc_data[(mc_data["mission_type_sig"] == mt)].dropna(subset=["correct_sig"])
        if not mc_mt.empty:
            mc_mt["correct_num_sig"] = mc_mt["correct_sig"].astype(int)
            pid_acc = mc_mt.groupby(["participant_id", "condition"])["correct_num_sig"].mean().reset_index(name="accuracy_sig")
            if pid_acc["condition"].nunique() == 2:
                res = paired_comparison(pid_acc, "accuracy_sig", condition_col="condition")
                marker = significance_marker(res["p"])
                if marker and marker != "n.s.":
                    y_max = max(accs[j] for accs in [[type_results[(type_results["mission_type"] == mt) &
                                (type_results["condition"] == label)]["accuracy"].values[0] * 100
                                if len(type_results[(type_results["mission_type"] == mt) &
                                (type_results["condition"] == label)]) > 0 else 0
                                for label in CONDITION_LABELS] for _ in [0]][0])
                    ax.text(j + width / 2, y_max + 5, marker,
                            ha="center", va="bottom", fontsize=11, fontweight="bold")

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "mission_accuracy_by_type")


def plot_behavior_distribution(beh_df: pd.DataFrame):
    """검증 행동 유형 분포 파이 차트."""
    if beh_df.empty:
        return

    fig, axes = plt.subplots(1, 3, figsize=(15, 5))

    for idx, mt in enumerate(["A", "B", "C"]):
        subset = beh_df[beh_df["mission_type"] == mt]
        if subset.empty:
            axes[idx].text(0.5, 0.5, "데이터 없음", ha="center", va="center")
            axes[idx].set_title(f"미션 {mt}")
            continue
        counts = subset["behavior"].value_counts()
        colors = {"proactive": "#2ecc71", "reactive": "#e74c3c", "none": "#95a5a6"}
        labels = counts.index.tolist()
        axes[idx].pie(counts.values, labels=labels, autopct="%1.0f%%",
                      colors=[colors.get(l, "#bdc3c7") for l in labels])
        axes[idx].set_title(f"미션 {mt} ({MISSION_TYPE_LABELS.get(mt, '')})")

    fig.suptitle("Hybrid 조건 — 검증 행동 유형 분포", fontsize=14)
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "verification_behavior")


# ──────────────────────────────────────────────
# 9. 메인
# ──────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="미션 정확도 및 검증 행동 분석")
    parser.add_argument("--demo", action="store_true",
                        help="데이터 파일이 없을 때 데모 데이터로 실행")
    args = parser.parse_args()

    print("=" * 60)
    print("미션 정확도 및 검증 행동 분석 (v2.1)")
    print("=" * 60)

    df = load_events(allow_demo=args.demo)
    print(f"총 이벤트 수: {len(df)}")

    # 분석
    type_results = analyze_mission_accuracy(df)
    beh_df = analyze_verification_behavior(df)
    dur_results = analyze_mission_duration(df)
    diff_results = analyze_difficulty_ratings(df)
    content_corr = analyze_content_accuracy_correlation(df)

    # [ISMAR] 열람 시간-정확도 상관 분석
    view_time_df = analyze_viewing_time_accuracy(df)

    # 시각화
    print(f"\n=== 시각화 ===")
    plot_accuracy_by_type(df, type_results)
    plot_behavior_distribution(beh_df)

    # 결과 저장
    if not type_results.empty:
        type_results.to_csv(OUTPUT_DIR / "mission_accuracy_by_type.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'mission_accuracy_by_type.csv'} 저장")

    if not dur_results.empty:
        dur_results.to_csv(OUTPUT_DIR / "mission_duration_summary.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'mission_duration_summary.csv'} 저장")

    if not beh_df.empty:
        beh_df.to_csv(OUTPUT_DIR / "verification_behavior.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'verification_behavior.csv'} 저장")

    if not content_corr.empty:
        content_corr.to_csv(OUTPUT_DIR / "content_accuracy_correlation.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'content_accuracy_correlation.csv'} 저장")

    # [ISMAR] 열람 시간-정확도 결과 저장
    if not view_time_df.empty:
        view_time_df.to_csv(OUTPUT_DIR / "viewing_time_accuracy.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'viewing_time_accuracy.csv'} 저장")

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
