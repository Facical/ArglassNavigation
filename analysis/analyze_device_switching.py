"""
기기 전환 패턴 분석 스크립트
- 하이브리드 조건에서의 기기 전환 빈도, 평균 전환 시간, 트리거 전후 전환율
- 검증 에피소드 추출 및 교차검증 지수(CVI) 계산 (v2)
- 2조건 간 Paired t-test / Wilcoxon signed-rank 비교
- 시각화: 조건별 boxplot, 트리거 전후 timeline
- [ISMAR] PAUSE_START/PAUSE_END 이벤트, ROUTE_END 기반 완료시간, beam_segments sidecar
"""

import os
import sys
import glob
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
# 1. 데이터 로드
# ──────────────────────────────────────────────

DATA_DIR = Path(__file__).resolve().parent.parent / "data"
RAW_DIR = DATA_DIR / "raw"
OUTPUT_DIR = Path(__file__).resolve().parent / "output"
OUTPUT_DIR.mkdir(exist_ok=True)

CONDITIONS = ["glass_only", "hybrid"]
CONDITION_LABELS = ["Glass Only", "Hybrid"]
TRIGGER_WAYPOINTS = ["WP03", "WP06"]
TRIGGER_TYPES = ["T1", "T2", "T3", "T4"]
BEAM_CONTENT_EVENTS = [
    "BEAM_TAB_SWITCH", "BEAM_POI_VIEWED", "BEAM_INFO_CARD_OPENED",
    "BEAM_INFO_CARD_CLOSED", "BEAM_MAP_ZOOMED", "BEAM_COMPARISON_VIEWED",
    "BEAM_MISSION_REF_VIEWED",
]
BEAM_CONTENT_TYPES = ["poi_detail", "info_card", "comparison", "map", "mission_ref"]
N_PARTICIPANTS = 24
N_WAYPOINTS = 8


def _detect_csv_format(path: Path) -> int:
    """[ISMAR] CSV 컬럼 수를 감지하여 포맷 버전 반환 (15=legacy, 24+=ISMAR)."""
    try:
        sample = pd.read_csv(path, nrows=1)
        return len(sample.columns)
    except Exception:
        return 15  # fallback to legacy


def load_all_events(allow_fallback: bool = False) -> pd.DataFrame:
    """data/raw/ 내 모든 이벤트 로그 CSV를 통합하여 반환."""
    # [ISMAR] sidecar 파일 제외 (head_pose, nav_trace, beam_segments 등)
    SIDECAR_SUFFIXES = ("_head_pose.csv", "_nav_trace.csv", "_beam_segments.csv",
                        "_anchor_reloc.csv", "_system_health.csv")
    csv_files = sorted(
        f for f in RAW_DIR.glob("P*_*.csv")
        if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
    )
    if not csv_files:
        if allow_fallback:
            print(f"[경고] {RAW_DIR}에 CSV 파일이 없습니다. Fallback 데이터를 생성합니다.")
            return generate_fallback_data()
        else:
            print(f"[오류] {RAW_DIR}에 CSV 파일이 없습니다.")
            print("  fallback 데이터로 실행하려면 --fallback 플래그를 사용하세요.")
            sys.exit(1)

    frames = []
    for f in csv_files:
        # [ISMAR] 포맷 감지: 컬럼 수에 따라 호환 처리
        ncols = _detect_csv_format(f)
        df = pd.read_csv(f, parse_dates=["timestamp"])
        if ncols <= 15:
            # Legacy 15-column format — 누락 컬럼 채우기
            for col in ["trigger_id", "trigger_type", "duration_s"]:
                if col not in df.columns:
                    df[col] = ""
        frames.append(df)
    return pd.concat(frames, ignore_index=True)


def load_beam_segments() -> pd.DataFrame:
    """[ISMAR] *_beam_segments.csv sidecar 파일이 있으면 로드하여 반환.

    실제 파일 컬럼: segment_id, on_ts, off_ts, duration_s, mission_id,
    start_wp, end_wp, trigger_active, primary_tab, poi_view_count, ...
    participant_id는 파일명에서 추출.
    """
    seg_files = sorted(RAW_DIR.glob("*_beam_segments.csv"))
    if not seg_files:
        return pd.DataFrame()
    frames = []
    for f in seg_files:
        try:
            # [ISMAR] 타임스탬프 컬럼명 호환: on_ts/off_ts 또는 start_time/end_time
            df = pd.read_csv(f)
            for ts_col in ["on_ts", "off_ts", "start_time", "end_time"]:
                if ts_col in df.columns:
                    df[ts_col] = pd.to_datetime(df[ts_col])
            # 파일명에서 participant_id 추출 (예: P01_hybrid_Set1_..._beam_segments.csv)
            import re
            match = re.match(r"(P\d+)_", f.name)
            if match:
                df["participant_id"] = match.group(1)
            frames.append(df)
        except Exception as e:
            print(f"  [경고] beam_segments 로드 실패: {f.name} — {e}")
    if frames:
        combined = pd.concat(frames, ignore_index=True)
        print(f"  [ISMAR] beam_segments sidecar 로드: {len(combined)}건 ({len(seg_files)} 파일)")
        return combined
    return pd.DataFrame()


def generate_fallback_data() -> pd.DataFrame:
    """분석 파이프라인 테스트용 fallback 데이터 생성 (2조건, 미션 + 트리거 이벤트 포함)."""
    rng = np.random.default_rng(42)
    rows = []
    waypoints = [f"WP{i:02d}" for i in range(1, N_WAYPOINTS + 1)]
    missions = ["A1", "B1", "A2", "B2", "C1"]
    mission_wps = {"A1": ("WP01", "WP02"), "B1": ("WP03",), "A2": ("WP04", "WP05"),
                   "B2": ("WP06",), "C1": ("WP07",)}
    trigger_at_wp = {"WP03": "T1", "WP06": "T4"}
    base_time = pd.Timestamp("2026-03-15T10:00:00")

    for pid in range(1, N_PARTICIPANTS + 1):
        participant_id = f"P{pid:02d}"
        for condition in CONDITIONS:
            t = base_time
            mission_idx = 0
            current_mission = missions[mission_idx]
            # ROUTE_START
            rows.append(_event(t, participant_id, condition, "ROUTE_START", "", route="A"))
            t += pd.Timedelta(seconds=rng.integers(5, 15))

            # MISSION_START for first mission
            rows.append(_event(t, participant_id, condition, "MISSION_START", "WP01",
                               mission_id=current_mission))

            for wp in waypoints:
                # 이동 중 정지
                if rng.random() < 0.3:
                    pause_dur = rng.uniform(2, 8)
                    rows.append(_event(t, participant_id, condition, "PAUSE_START", wp))
                    t += pd.Timedelta(seconds=pause_dur)
                    rows.append(_event(t, participant_id, condition, "PAUSE_END", wp,
                                       pause_duration_s=round(pause_dur, 1)))

                # 하이브리드: 기기 전환
                if condition == "hybrid":
                    switch_prob = 0.60 if wp in TRIGGER_WAYPOINTS else 0.25
                    if rng.random() < switch_prob:
                        rows.append(_event(t, participant_id, condition, "BEAM_SCREEN_ON", wp))
                        # v2.1: 정보 허브 하위 이벤트 생성
                        sub_t = t + pd.Timedelta(seconds=rng.uniform(0.5, 1.5))
                        n_sub_events = rng.integers(1, 4)
                        for _ in range(n_sub_events):
                            content_type = rng.choice(BEAM_CONTENT_TYPES,
                                                      p=[0.30, 0.25, 0.10, 0.25, 0.10])
                            if content_type == "poi_detail":
                                poi_id = f"poi_{rng.integers(1, 10):02d}"
                                rows.append(_event(sub_t, participant_id, condition,
                                                   "BEAM_POI_VIEWED", wp,
                                                   beam_content_type="poi_detail",
                                                   poi_id=poi_id,
                                                   poi_type=rng.choice(["meeting_room", "vending_machine",
                                                                        "restroom", "emergency_exit"]),
                                                   view_duration_s=round(rng.uniform(1, 5), 1)))
                            elif content_type == "info_card":
                                card_id = f"card_{rng.integers(1, 8):02d}"
                                rows.append(_event(sub_t, participant_id, condition,
                                                   "BEAM_INFO_CARD_OPENED", wp,
                                                   beam_content_type="info_card",
                                                   card_id=card_id,
                                                   card_type=rng.choice(["poi_detail", "sign_card",
                                                                         "landmark"]),
                                                   auto_shown=bool(rng.random() < 0.4)))
                                sub_t += pd.Timedelta(seconds=rng.uniform(1, 4))
                                rows.append(_event(sub_t, participant_id, condition,
                                                   "BEAM_INFO_CARD_CLOSED", wp,
                                                   beam_content_type="info_card",
                                                   card_id=card_id,
                                                   view_duration_s=round(rng.uniform(1, 4), 1)))
                            elif content_type == "comparison":
                                rows.append(_event(sub_t, participant_id, condition,
                                                   "BEAM_COMPARISON_VIEWED", wp,
                                                   beam_content_type="comparison",
                                                   comparison_id=f"comp_{rng.integers(1, 3):02d}",
                                                   items_compared=["item_A", "item_B"]))
                            elif content_type == "map":
                                rows.append(_event(sub_t, participant_id, condition,
                                                   "BEAM_MAP_ZOOMED", wp,
                                                   beam_content_type="map",
                                                   zoom_level=round(rng.uniform(1.0, 3.0), 1)))
                            elif content_type == "mission_ref":
                                rows.append(_event(sub_t, participant_id, condition,
                                                   "BEAM_MISSION_REF_VIEWED", wp,
                                                   beam_content_type="mission_ref",
                                                   mission_id=current_mission,
                                                   ref_type="briefing_review"))
                            sub_t += pd.Timedelta(seconds=rng.uniform(0.5, 2))
                        switch_dur = (sub_t - t).total_seconds() + rng.uniform(0.5, 2)
                        t = sub_t + pd.Timedelta(seconds=rng.uniform(0.5, 2))
                        rows.append(_event(t, participant_id, condition, "BEAM_SCREEN_OFF", wp,
                                           duration_s=round(switch_dur, 1)))

                # 트리거 이벤트
                if wp in trigger_at_wp:
                    ttype = trigger_at_wp[wp]
                    rows.append(_event(t, participant_id, condition, "TRIGGER_ACTIVATED", wp,
                                       trigger_type=ttype))
                    t += pd.Timedelta(seconds=rng.uniform(8, 15))
                    rows.append(_event(t, participant_id, condition, "TRIGGER_DEACTIVATED", wp,
                                       trigger_type=ttype, duration_s=round(rng.uniform(8, 15), 1)))

                # 웨이포인트 도달
                t += pd.Timedelta(seconds=rng.integers(30, 90))
                conf = _generate_confidence(condition, wp, rng)
                rows.append(_event(t, participant_id, condition, "WAYPOINT_REACHED", wp))
                rows.append(_event(t, participant_id, condition, "CONFIDENCE_RATED", wp,
                                   confidence=conf))

                # 미션 검증
                if mission_idx < len(missions):
                    cm = missions[mission_idx]
                    end_wps = mission_wps[cm]
                    if wp == end_wps[-1]:
                        acc_base = {"glass_only": 0.70, "hybrid": 0.80}
                        correct = rng.random() < acc_base[condition]
                        t += pd.Timedelta(seconds=rng.uniform(2, 6))
                        rows.append(_event(t, participant_id, condition, "VERIFICATION_ANSWERED", wp,
                                           mission_id=cm, correct=correct,
                                           rt_s=round(rng.uniform(2, 8), 1)))
                        rows.append(_event(t, participant_id, condition, "MISSION_COMPLETE", wp,
                                           mission_id=cm, correct=correct))

                        # 난이도 평정
                        diff_base = {"glass_only": 3.4, "hybrid": 2.9}
                        diff_rating = int(np.clip(round(rng.normal(diff_base[condition], 1)), 1, 7))
                        rows.append(_event(t, participant_id, condition, "DIFFICULTY_RATED", wp,
                                           mission_id=cm, rating=diff_rating))

                        mission_idx += 1
                        if mission_idx < len(missions):
                            t += pd.Timedelta(seconds=rng.integers(3, 8))
                            rows.append(_event(t, participant_id, condition, "MISSION_START",
                                               wp, mission_id=missions[mission_idx]))

            # ROUTE_END
            t += pd.Timedelta(seconds=rng.integers(5, 15))
            rows.append(_event(t, participant_id, condition, "ROUTE_END", ""))

    df = pd.DataFrame(rows)
    df["timestamp"] = pd.to_datetime(df["timestamp"], format="ISO8601")
    return df


def _generate_confidence(condition: str, wp: str, rng) -> int:
    """조건과 웨이포인트에 따라 확신도 생성."""
    if wp in TRIGGER_WAYPOINTS:
        base = {"glass_only": 3.0, "hybrid": 4.3}
    else:
        base = {"glass_only": 3.8, "hybrid": 5.0}
    val = rng.normal(base[condition], 0.9)
    return int(np.clip(round(val), 1, 7))


def _event(t, pid, cond, etype, wp, **extra) -> dict:
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
# 2. 기기 전환 분석
# ──────────────────────────────────────────────

def analyze_switching(df: pd.DataFrame, beam_segments: pd.DataFrame = None) -> pd.DataFrame:
    """하이브리드 조건의 기기 전환 통계 산출."""
    hybrid = df[df["condition"] == "hybrid"]
    beam_on = hybrid[hybrid["event_type"] == "BEAM_SCREEN_ON"]
    beam_off = hybrid[hybrid["event_type"] == "BEAM_SCREEN_OFF"]

    # 참가자별 전환 횟수
    switch_counts = beam_on.groupby("participant_id").size().reset_index(name="switch_count")

    # [ISMAR] beam_segments sidecar가 있으면 우선 사용, 없으면 기존 방식
    if beam_segments is not None and not beam_segments.empty and "duration_s" in beam_segments.columns:
        print("  [ISMAR] beam_segments sidecar 데이터를 지속시간 분석에 사용합니다.")
        avg_dur = beam_segments.groupby("participant_id")["duration_s"].mean().reset_index(
            name="avg_switch_duration_s"
        )
        switch_counts = switch_counts.merge(avg_dur, on="participant_id", how="left")
        # sidecar에 content_type 등 추가 컬럼이 있으면 요약 출력
        if "content_types_accessed" in beam_segments.columns:
            print(f"    sidecar 콘텐츠 유형 필드 감지 — 세그먼트별 콘텐츠 분석 가능")
    else:
        # 기존 방식: BEAM_SCREEN_OFF extra_data에서 duration_s 추출
        durations = []
        for _, row in beam_off.iterrows():
            d = parse_extra(row.get("extra_data", "{}"))
            dur = d.get("duration_s", np.nan)
            durations.append({"participant_id": row["participant_id"], "duration_s": dur})

        dur_df = pd.DataFrame(durations)
        if not dur_df.empty:
            avg_dur = dur_df.groupby("participant_id")["duration_s"].mean().reset_index(
                name="avg_switch_duration_s"
            )
            switch_counts = switch_counts.merge(avg_dur, on="participant_id", how="left")
        else:
            switch_counts["avg_switch_duration_s"] = np.nan

    # 트리거 전후 전환율
    trigger_switches = beam_on[beam_on["waypoint_id"].isin(TRIGGER_WAYPOINTS)]

    trigger_rate = len(trigger_switches) / max(len(beam_on), 1)
    print(f"\n=== 기기 전환 분석 (Hybrid 조건) ===")
    print(f"총 전환 횟수: {len(beam_on)}")
    print(f"참가자 평균 전환 횟수: {switch_counts['switch_count'].mean():.1f} "
          f"(SD={switch_counts['switch_count'].std():.1f})")
    if "avg_switch_duration_s" in switch_counts.columns:
        print(f"평균 전환 지속시간: {switch_counts['avg_switch_duration_s'].mean():.1f}s")
    print(f"트리거 지점 전환 비율: {trigger_rate:.1%}")

    # v2.1: 콘텐츠 유형별 접근 분석
    content_events = hybrid[hybrid["event_type"].isin(BEAM_CONTENT_EVENTS)]
    if not content_events.empty:
        print(f"\n  [콘텐츠 유형별 접근 빈도]")
        for ct in BEAM_CONTENT_TYPES:
            ct_events = content_events[content_events.get("beam_content_type", pd.Series()) == ct]
            if hasattr(content_events, "beam_content_type"):
                ct_events = content_events[content_events["beam_content_type"] == ct]
            else:
                ct_events = pd.DataFrame()
            count = len(ct_events)
            print(f"    {ct}: {count}회")

    return switch_counts


# ──────────────────────────────────────────────
# 2b. 교차검증 지수 (CVI) 분석 — v2
# ──────────────────────────────────────────────

def analyze_cross_verification(df: pd.DataFrame) -> pd.DataFrame:
    """교차검증 지수(CVI) 계산: 트리거 구간 Beam Pro 참조율 ÷ 전체 참조율."""
    hybrid = df[df["condition"] == "hybrid"]
    beam_on = hybrid[hybrid["event_type"] == "BEAM_SCREEN_ON"]

    results = []
    for pid in beam_on["participant_id"].unique():
        pid_events = beam_on[beam_on["participant_id"] == pid]
        total_switches = len(pid_events)
        trigger_switches = len(pid_events[pid_events["waypoint_id"].isin(TRIGGER_WAYPOINTS)])

        n_trigger_wp = len(TRIGGER_WAYPOINTS)
        non_trigger_switches = total_switches - trigger_switches

        trigger_rate = trigger_switches / n_trigger_wp if n_trigger_wp > 0 else 0
        overall_rate = total_switches / N_WAYPOINTS if N_WAYPOINTS > 0 else 0
        cvi = trigger_rate / overall_rate if overall_rate > 0 else np.nan

        results.append({
            "participant_id": pid,
            "total_switches": total_switches,
            "trigger_switches": trigger_switches,
            "non_trigger_switches": non_trigger_switches,
            "cvi": round(cvi, 2) if not np.isnan(cvi) else np.nan,
        })

    cvi_df = pd.DataFrame(results)

    print(f"\n=== 교차검증 지수 (CVI) 분석 ===")
    if not cvi_df.empty:
        print(f"참가자 수: {len(cvi_df)}")
        print(f"평균 CVI: {cvi_df['cvi'].mean():.2f} (SD={cvi_df['cvi'].std():.2f})")
        print(f"  (CVI > 1: 트리거 구간에서 더 많이 Beam Pro 참조)")
        cvi_above_1 = (cvi_df["cvi"] > 1).sum()
        print(f"  CVI > 1인 참가자: {cvi_above_1}/{len(cvi_df)} ({cvi_above_1/len(cvi_df):.0%})")
    else:
        print("  [경고] CVI 계산 불가 (Hybrid 조건 전환 데이터 없음)")

    # v2.1: 콘텐츠별 CVI
    if not cvi_df.empty and "beam_content_type" in hybrid.columns:
        content_events = hybrid[hybrid["event_type"].isin(BEAM_CONTENT_EVENTS)]
        if not content_events.empty:
            print(f"\n  [콘텐츠별 CVI]")
            for ct in BEAM_CONTENT_TYPES:
                ct_events = content_events[content_events["beam_content_type"] == ct]
                trigger_ct = ct_events[ct_events["waypoint_id"].isin(TRIGGER_WAYPOINTS)]
                n_trigger = len(TRIGGER_WAYPOINTS)
                trigger_rate = len(trigger_ct) / max(n_trigger, 1)
                overall_rate = len(ct_events) / max(N_WAYPOINTS, 1)
                ct_cvi = trigger_rate / overall_rate if overall_rate > 0 else 0
                print(f"    {ct}: CVI = {ct_cvi:.2f}")

    return cvi_df


def analyze_verification_episodes(df: pd.DataFrame) -> pd.DataFrame:
    """검증 에피소드 추출: Beam Pro 참조 전후 미션 정확도 비교 (Hybrid 조건)."""
    hybrid = df[df["condition"] == "hybrid"]

    mission_completes = hybrid[hybrid["event_type"] == "MISSION_COMPLETE"].copy()
    if mission_completes.empty:
        print("\n=== 검증 에피소드 분석 ===")
        print("  [경고] MISSION_COMPLETE 이벤트 없음")
        return pd.DataFrame()

    def extract_correct(extra):
        d = parse_extra(extra)
        return d.get("correct", None)

    mission_completes["correct"] = mission_completes["extra_data"].apply(extract_correct)

    beam_on_times = hybrid[hybrid["event_type"] == "BEAM_SCREEN_ON"][
        ["participant_id", "timestamp", "waypoint_id"]
    ]

    episodes = []
    for _, mc in mission_completes.iterrows():
        pid = mc["participant_id"]
        mc_time = mc["timestamp"]
        pid_beams = beam_on_times[beam_on_times["participant_id"] == pid]
        recent_beams = pid_beams[
            (pid_beams["timestamp"] < mc_time) &
            (pid_beams["timestamp"] > mc_time - pd.Timedelta(seconds=60))
        ]
        episodes.append({
            "participant_id": pid,
            "waypoint_id": mc["waypoint_id"],
            "correct": mc["correct"],
            "beam_referenced": len(recent_beams) > 0,
        })

    ep_df = pd.DataFrame(episodes)

    print(f"\n=== 검증 에피소드 분석 (Hybrid 조건) ===")
    if not ep_df.empty and ep_df["correct"].notna().any():
        ref = ep_df[ep_df["beam_referenced"]]
        noref = ep_df[~ep_df["beam_referenced"]]
        ref_acc = ref["correct"].mean() if len(ref) > 0 else np.nan
        noref_acc = noref["correct"].mean() if len(noref) > 0 else np.nan
        print(f"  Beam Pro 참조 후 정확도: {ref_acc:.1%} (n={len(ref)})")
        print(f"  미참조 시 정확도: {noref_acc:.1%} (n={len(noref)})")
        if len(ref) > 5 and len(noref) > 5:
            from scipy.stats import chi2_contingency
            table = pd.crosstab(ep_df["beam_referenced"], ep_df["correct"])
            if table.shape == (2, 2):
                chi2, p, _, _ = chi2_contingency(table)
                print(f"  χ² = {chi2:.2f}, p = {p:.4f}")
    else:
        print("  [경고] 검증 에피소드 데이터 부족")

    return ep_df


def analyze_content_access_patterns(df: pd.DataFrame) -> pd.DataFrame:
    """미션 타입 × 콘텐츠 유형 접근 패턴 분석 (v2.1)."""
    hybrid = df[df["condition"] == "hybrid"]
    content_events = hybrid[hybrid["event_type"].isin(BEAM_CONTENT_EVENTS)].copy()

    if content_events.empty or "beam_content_type" not in content_events.columns:
        print("\n=== 콘텐츠 접근 패턴 분석 ===")
        print("  [경고] 콘텐츠 이벤트 없음")
        return pd.DataFrame()

    # 미션 구간에 콘텐츠 이벤트 매핑
    missions = hybrid[hybrid["event_type"] == "MISSION_START"].copy()
    missions["parsed"] = missions["extra_data"].apply(parse_extra)
    missions["mission_id"] = missions["parsed"].apply(lambda d: d.get("mission_id", ""))

    mission_types_map = {"A1": "A", "A2": "A", "B1": "B", "B2": "B", "C1": "C"}
    results = []

    for mt in ["A", "B", "C"]:
        for ct in BEAM_CONTENT_TYPES:
            ct_events = content_events[content_events["beam_content_type"] == ct]
            # 미션 타입에 해당하는 웨이포인트에서의 콘텐츠 접근
            mission_wps = {"A": ["WP01", "WP02", "WP04", "WP05"],
                           "B": ["WP03", "WP06"],
                           "C": ["WP07"]}
            mt_events = ct_events[ct_events["waypoint_id"].isin(mission_wps.get(mt, []))]
            results.append({
                "mission_type": mt,
                "content_type": ct,
                "access_count": len(mt_events),
            })

    result_df = pd.DataFrame(results)

    print("\n=== 콘텐츠 접근 패턴 분석 (미션 타입 × 콘텐츠 유형) ===")
    if not result_df.empty:
        pivot = result_df.pivot(index="mission_type", columns="content_type", values="access_count")
        print(pivot.to_string())

    return result_df


def analyze_information_utilization(df: pd.DataFrame) -> pd.DataFrame:
    """콘텐츠 열람 후 미션 정확도 분석 (v2.1)."""
    hybrid = df[df["condition"] == "hybrid"]

    mc = hybrid[hybrid["event_type"] == "MISSION_COMPLETE"].copy()
    if mc.empty:
        print("\n=== 정보 활용도 분석 ===")
        print("  [경고] MISSION_COMPLETE 이벤트 없음")
        return pd.DataFrame()

    mc["parsed"] = mc["extra_data"].apply(parse_extra)
    mc["mission_id"] = mc["parsed"].apply(lambda d: d.get("mission_id", ""))
    mc["correct"] = mc["parsed"].apply(lambda d: d.get("correct", None))

    content_events = hybrid[hybrid["event_type"].isin(BEAM_CONTENT_EVENTS)]

    results = []
    for _, row in mc.iterrows():
        pid = row["participant_id"]
        mc_time = row["timestamp"]
        pid_content = content_events[
            (content_events["participant_id"] == pid) &
            (content_events["timestamp"] < mc_time) &
            (content_events["timestamp"] > mc_time - pd.Timedelta(seconds=120))
        ]
        content_accessed = [
            str(v) for v in pid_content["beam_content_type"].dropna().unique()
        ] if ("beam_content_type" in pid_content.columns and not pid_content.empty) else []
        results.append({
            "participant_id": pid,
            "mission_id": row["mission_id"],
            "correct": row["correct"],
            "content_count": len(pid_content),
            "content_types": ",".join(content_accessed) if content_accessed else "none",
        })

    util_df = pd.DataFrame(results)

    print("\n=== 정보 활용도 분석 (Hybrid 조건) ===")
    if not util_df.empty and util_df["correct"].notna().any():
        with_content = util_df[util_df["content_count"] > 0]
        without_content = util_df[util_df["content_count"] == 0]
        if len(with_content) > 0:
            print(f"  콘텐츠 열람 후 정확도: {with_content['correct'].astype(float).mean():.1%} "
                  f"(n={len(with_content)})")
        if len(without_content) > 0:
            print(f"  콘텐츠 미열람 정확도: {without_content['correct'].astype(float).mean():.1%} "
                  f"(n={len(without_content)})")

    return util_df


# ──────────────────────────────────────────────
# 3. 정지 횟수/시간 비교 (2조건)
# ──────────────────────────────────────────────

def analyze_pauses(df: pd.DataFrame) -> pd.DataFrame:
    """조건별 정지 횟수 및 총 정지 시간 비교.

    [ISMAR] PAUSE_START/PAUSE_END 이벤트를 사용하여 이동 정지 구간을 분석.
    PAUSE_END의 extra_data에 pause_duration_s가 있으면 해당 값을 사용하고,
    없으면 PAUSE_START/PAUSE_END 타임스탬프 차이로 계산 (하위 호환).
    """
    pause_starts = df[df["event_type"] == "PAUSE_START"]
    pause_ends = df[df["event_type"] == "PAUSE_END"]

    pause_counts = (
        pause_starts.groupby(["participant_id", "condition"])
        .size()
        .reset_index(name="pause_count")
    )

    pause_durations = []
    for _, row in pause_ends.iterrows():
        d = parse_extra(row.get("extra_data", "{}"))
        dur = d.get("pause_duration_s", None)

        # [ISMAR] pause_duration_s가 extra_data에 없으면 타임스탬프 매칭으로 계산
        if dur is None:
            pid = row["participant_id"]
            cond = row["condition"]
            end_time = row["timestamp"]
            matching_starts = pause_starts[
                (pause_starts["participant_id"] == pid) &
                (pause_starts["condition"] == cond) &
                (pause_starts["timestamp"] < end_time)
            ]
            if not matching_starts.empty:
                start_time = matching_starts["timestamp"].iloc[-1]
                dur = (end_time - start_time).total_seconds()
            else:
                dur = 0

        pause_durations.append({
            "participant_id": row["participant_id"],
            "condition": row["condition"],
            "pause_duration_s": dur,
        })

    dur_df = pd.DataFrame(pause_durations)
    if not dur_df.empty:
        total_dur = dur_df.groupby(["participant_id", "condition"])["pause_duration_s"].sum().reset_index(
            name="total_pause_s"
        )
        pause_counts = pause_counts.merge(total_dur, on=["participant_id", "condition"], how="left")
    else:
        pause_counts["total_pause_s"] = 0

    print(f"\n=== 정지 분석 (2조건) [ISMAR: PAUSE_START/PAUSE_END] ===")
    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        subset = pause_counts[pause_counts["condition"] == cond]
        if not subset.empty:
            print(f"  {label}: 평균 {subset['pause_count'].mean():.1f}회, "
                  f"총 {subset['total_pause_s'].mean():.1f}s")
        else:
            print(f"  {label}: PAUSE 이벤트 없음")

    return pause_counts


# ──────────────────────────────────────────────
# 4. 과제 완료 시간 비교
# ──────────────────────────────────────────────

def analyze_completion_time(df: pd.DataFrame) -> pd.DataFrame:
    """조건별 과제 완료 시간 산출.

    [ISMAR] ROUTE_END 이벤트를 우선 사용하여 실제 경로 완료 시간을 정확히 측정.
    ROUTE_END가 없으면 마지막 WAYPOINT_REACHED 또는 MISSION_COMPLETE 타임스탬프로
    fallback하여 하위 호환성 유지.
    """
    starts = df[df["event_type"] == "ROUTE_START"][["participant_id", "condition", "timestamp"]]
    starts = starts.rename(columns={"timestamp": "start_time"})

    # [ISMAR] ROUTE_END 이벤트 우선 사용
    route_ends = df[df["event_type"] == "ROUTE_END"]
    if not route_ends.empty:
        ends = route_ends[["participant_id", "condition", "timestamp"]].copy()
        ends = ends.rename(columns={"timestamp": "end_time"})
        print("  [ISMAR] ROUTE_END 이벤트를 사용하여 완료 시간 산출")
    else:
        # Fallback: 마지막 WAYPOINT_REACHED 또는 MISSION_COMPLETE 이벤트
        fallback_events = df[df["event_type"].isin(["WAYPOINT_REACHED", "MISSION_COMPLETE"])]
        if not fallback_events.empty:
            ends = (
                fallback_events.groupby(["participant_id", "condition"])["timestamp"]
                .max()
                .reset_index()
                .rename(columns={"timestamp": "end_time"})
            )
            print("  [호환] ROUTE_END 없음 — 마지막 WP/미션 이벤트로 대체")
        else:
            ends = pd.DataFrame(columns=["participant_id", "condition", "end_time"])

    merged = starts.merge(ends, on=["participant_id", "condition"])
    if merged.empty:
        print(f"\n=== 과제 완료 시간 (2조건) ===")
        print("  [경고] ROUTE_START/종료 이벤트 매칭 불가")
        return pd.DataFrame(columns=["participant_id", "condition", "completion_time_s"])
    # 타입 안전: datetime으로 변환
    merged["start_time"] = pd.to_datetime(merged["start_time"])
    merged["end_time"] = pd.to_datetime(merged["end_time"])
    merged["completion_time_s"] = (merged["end_time"] - merged["start_time"]).dt.total_seconds()

    print(f"\n=== 과제 완료 시간 (2조건) ===")
    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        subset = merged[merged["condition"] == cond]
        if not subset.empty:
            print(f"  {label}: M={subset['completion_time_s'].mean():.1f}s, "
                  f"SD={subset['completion_time_s'].std():.1f}s")
        else:
            print(f"  {label}: 완료 시간 데이터 없음")

    return merged[["participant_id", "condition", "completion_time_s"]]


# ──────────────────────────────────────────────
# 5. Paired t-test / Wilcoxon signed-rank (2조건)
# ──────────────────────────────────────────────

def run_paired_test(data: pd.DataFrame, dv: str, label: str):
    """2조건 Paired t-test (pingouin) 또는 Wilcoxon signed-rank (scipy fallback)."""
    try:
        import pingouin as pg
        n_paired = data.groupby("participant_id")["condition"].nunique()
        n_paired = (n_paired == 2).sum()
        if n_paired < 2:
            print(f"\n=== {label}: paired 참가자 {n_paired}명, 검정 불가 (최소 2명 필요) ===")
            return
        test = pg.pairwise_tests(
            data=data, dv=dv, within="condition", subject="participant_id",
            parametric=True
        )
        print(f"\n=== Paired t-test: {label} ===")
        if not test.empty and "T" in test.columns:
            row = test.iloc[0]
            print(f"  {row['A']} vs {row['B']}: t={row['T']:.2f}, p={row.get('p-unc', row.get('p_unc', 0)):.4f}, "
                  f"d={row['hedges']:.2f}")
    except (ImportError, ValueError, KeyError) as e:
        if isinstance(e, ImportError):
            pass
        else:
            print(f"\n=== {label}: pingouin 검정 실패 ({e}), scipy fallback ===")
        # fallback
        print(f"\n=== Wilcoxon signed-rank: {label} ===")
        print("  (pingouin 미설치 → scipy.stats.wilcoxon 사용)")
        glass_vals = data[data["condition"] == "glass_only"][dv].values
        hybrid_vals = data[data["condition"] == "hybrid"][dv].values
        min_len = min(len(glass_vals), len(hybrid_vals))
        if min_len > 0:
            # Paired t-test first
            t_stat, t_p = stats.ttest_rel(glass_vals[:min_len], hybrid_vals[:min_len])
            print(f"  Paired t-test: t={t_stat:.2f}, p={t_p:.4f}")
            # Wilcoxon as robustness check
            w_stat, w_p = stats.wilcoxon(glass_vals[:min_len], hybrid_vals[:min_len])
            print(f"  Wilcoxon: W={w_stat:.1f}, p={w_p:.4f}")
            # Effect size (Cohen's d)
            diff = glass_vals[:min_len] - hybrid_vals[:min_len]
            d = diff.mean() / diff.std() if diff.std() > 0 else 0
            print(f"  Cohen's d = {d:.2f}")
        else:
            print("  [경고] 데이터 부족, 검정 불가")


# ──────────────────────────────────────────────
# 6. 시각화
# ──────────────────────────────────────────────

def plot_switching_boxplot(switch_df: pd.DataFrame):
    """하이브리드 조건 기기 전환 횟수 violin + dots."""
    fig, ax = plt.subplots(1, 1, figsize=(4, 5))
    data = switch_df["switch_count"].dropna().values
    violin_with_dots(ax, [data], [1], [COLOR_HYBRID], ["Hybrid"],
                     ylabel="Device Switching Count",
                     title="Hybrid — Device Switching Distribution")
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "switching_boxplot")


def plot_pause_comparison(pause_df: pd.DataFrame):
    """2조건 정지 횟수 비교 violin + dots + significance."""
    fig, axes = plt.subplots(1, 2, figsize=(10, 5))

    data_count = [pause_df[pause_df["condition"] == c]["pause_count"].dropna().values for c in CONDITIONS]
    res_count = paired_comparison(pause_df, "pause_count")
    violin_with_dots(axes[0], data_count, [1, 2], COLORS_COND, COND_LABELS,
                     p_value=res_count["p"], ylabel="Pause Count",
                     title="Pause Count by Condition")

    data_time = [pause_df[pause_df["condition"] == c]["total_pause_s"].dropna().values for c in CONDITIONS]
    res_time = paired_comparison(pause_df, "total_pause_s")
    violin_with_dots(axes[1], data_time, [1, 2], COLORS_COND, COND_LABELS,
                     p_value=res_time["p"], ylabel="Total Pause Time (s)",
                     title="Pause Duration by Condition")

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "pause_comparison")


def plot_completion_time(ct_df: pd.DataFrame):
    """2조건 과제 완료 시간 비교 violin + dots + significance."""
    fig, ax = plt.subplots(1, 1, figsize=(5, 5))
    data = [ct_df[ct_df["condition"] == c]["completion_time_s"].dropna().values for c in CONDITIONS]
    res = paired_comparison(ct_df, "completion_time_s")
    violin_with_dots(ax, data, [1, 2], COLORS_COND, COND_LABELS,
                     p_value=res["p"], ylabel="Completion Time (s)",
                     title="Task Completion Time")
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "completion_time")


def plot_trigger_timeline(df: pd.DataFrame):
    """트리거 지점 전후 기기 전환 timeline 시각화."""
    hybrid = df[df["condition"] == "hybrid"]
    waypoints = [f"WP{i:02d}" for i in range(1, N_WAYPOINTS + 1)]

    switch_rates = []
    for wp in waypoints:
        wp_events = hybrid[hybrid["waypoint_id"] == wp]
        total = wp_events["participant_id"].nunique()
        switches = wp_events[wp_events["event_type"] == "BEAM_SCREEN_ON"]["participant_id"].nunique()
        rate = switches / max(total, 1)
        switch_rates.append(rate)

    fig, ax = plt.subplots(1, 1, figsize=(8, 5))
    colors = ["#e74c3c" if wp in TRIGGER_WAYPOINTS else "#3498db" for wp in waypoints]
    bars = ax.bar(waypoints, switch_rates, color=colors)
    ax.set_ylabel("Beam Pro Reference Rate")
    ax.set_xlabel("Waypoint")
    ax.set_title("Beam Pro Reference Rate by Waypoint (Hybrid)")
    ax.set_ylim(0, 1)

    from matplotlib.patches import Patch
    legend_elements = [
        Patch(facecolor="#e74c3c", label="Uncertainty Trigger"),
        Patch(facecolor="#3498db", label="Normal Waypoint"),
    ]
    ax.legend(handles=legend_elements)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "trigger_timeline")


def plot_content_heatmap(pattern_df: pd.DataFrame):
    """미션 타입 × 콘텐츠 유형 접근 히트맵 (v2.1)."""
    if pattern_df.empty:
        return

    pivot = pattern_df.pivot(index="mission_type", columns="content_type", values="access_count")
    if pivot.empty:
        return

    fig, ax = plt.subplots(figsize=(10, 5))
    im = ax.imshow(pivot.values, cmap="YlOrRd", aspect="auto")

    ax.set_xticks(range(len(pivot.columns)))
    ax.set_xticklabels(pivot.columns, rotation=30, ha="right")
    ax.set_yticks(range(len(pivot.index)))
    ax.set_yticklabels([f"Mission {mt}" for mt in pivot.index])

    for i in range(len(pivot.index)):
        for j in range(len(pivot.columns)):
            val = pivot.values[i, j]
            ax.text(j, i, f"{val:.0f}", ha="center", va="center",
                    color="white" if val > pivot.values.max() * 0.6 else "black")

    ax.set_title("Content Access by Mission Type x Content Type (Hybrid)")
    fig.colorbar(im, ax=ax, label="Access Count")
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "content_access_heatmap")


# ──────────────────────────────────────────────
# 7. 메인
# ──────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="기기 전환 패턴 분석")
    parser.add_argument("--fallback", action="store_true",
                        help="CSV 파일이 없을 때 fallback 데이터로 실행")
    args = parser.parse_args()

    (OUTPUT_DIR / "csv").mkdir(exist_ok=True)

    print("=" * 60)
    print("기기 전환 패턴 분석")
    print("=" * 60)

    df = load_all_events(allow_fallback=args.fallback)
    print(f"총 이벤트 수: {len(df)}")
    print(f"참가자 수: {df['participant_id'].nunique()}")
    print(f"조건: {df['condition'].unique().tolist()}")

    # [ISMAR] beam_segments sidecar 로드 시도
    beam_segments = load_beam_segments()

    # 분석
    switch_df = analyze_switching(df, beam_segments=beam_segments)
    cvi_df = analyze_cross_verification(df)
    ep_df = analyze_verification_episodes(df)
    content_df = analyze_content_access_patterns(df)
    util_df_content = analyze_information_utilization(df)
    pause_df = analyze_pauses(df)
    ct_df = analyze_completion_time(df)

    # 2조건 대응 비교: 정지 횟수
    run_paired_test(pause_df, "pause_count", "정지 횟수")

    # 2조건 대응 비교: 과제 완료 시간
    run_paired_test(ct_df, "completion_time_s", "과제 완료 시간")

    # 시각화
    print(f"\n=== 시각화 ===")
    plot_switching_boxplot(switch_df)
    plot_pause_comparison(pause_df)
    plot_completion_time(ct_df)
    plot_trigger_timeline(df)
    plot_content_heatmap(content_df)

    # 요약 CSV 저장
    summary = ct_df.merge(pause_df, on=["participant_id", "condition"], how="outer")
    summary.to_csv(OUTPUT_DIR / "csv" / "device_switching_summary.csv", index=False)
    print(f"  → {OUTPUT_DIR / 'csv' / 'device_switching_summary.csv'} 저장")

    if not cvi_df.empty:
        cvi_df.to_csv(OUTPUT_DIR / "csv" / "cvi_summary.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'csv' / 'cvi_summary.csv'} 저장")

    if not ep_df.empty:
        ep_df.to_csv(OUTPUT_DIR / "csv" / "verification_episodes.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'csv' / 'verification_episodes.csv'} 저장")

    if not content_df.empty:
        content_df.to_csv(OUTPUT_DIR / "csv" / "content_access_patterns.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'csv' / 'content_access_patterns.csv'} 저장")

    if not util_df_content.empty:
        util_df_content.to_csv(OUTPUT_DIR / "csv" / "information_utilization.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'csv' / 'information_utilization.csv'} 저장")

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
