"""
종합 분석 스크립트 — CHI/UIST 논문용 Table 1, Table 2, Fig 1, Fig 4 생성.

24명 x 2조건 (Glass Only / Hybrid) within-subjects 설계.
4 카운터밸런싱 그룹 (G1-G4): 조건 x 미션세트 순서.

출력:
  Table 1: 전체 종속변수 요약 (M, SD, 통계량, p, Cohen's d, 95% CI) → LaTeX + CSV
  Table 2: 순서 효과 검증 (Condition x Order Mixed ANOVA) → LaTeX + CSV
  Fig 1:   4개 주요 DV violin plot (2x2 subplot)
  Fig 4:   Forest plot (전체 DV Cohen's d + 95% CI)

사용법:
  python3 analysis/analyze_comprehensive.py --fallback    # fallback 데이터로 실행
  python3 analysis/analyze_comprehensive.py           # 실제 데이터로 실행
"""

import argparse
import json
import sys
import warnings
from pathlib import Path

import numpy as np
import pandas as pd
import matplotlib
import matplotlib.pyplot as plt
from scipy import stats

from parse_utils import parse_extra
from stat_utils import (
    paired_comparison,
    batch_paired_comparison,
    test_order_effect,
    significance_marker,
    format_stat_line,
    format_p,
    format_ci,
    cohens_d_paired,
    cohens_d_ci,
)
from plot_style import (
    apply_style,
    save_fig,
    violin_with_dots,
    forest_plot,
    COLORS_COND,
    COND_LABELS,
    FIG_DOUBLE_COL,
    COLOR_GLASS,
    COLOR_HYBRID,
    DPI,
)

warnings.filterwarnings("ignore", category=FutureWarning)

# ──────────────────────────────────────────────
# 경로 설정
# ──────────────────────────────────────────────

SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR.parent / "data"
RAW_DIR = DATA_DIR / "raw"
SURVEY_DIR = DATA_DIR / "surveys"
OUTPUT_DIR = SCRIPT_DIR / "output"
OUTPUT_DIR.mkdir(exist_ok=True)

# ──────────────────────────────────────────────
# 상수
# ──────────────────────────────────────────────

CONDITIONS = ["glass_only", "hybrid"]
N_PARTICIPANTS = 24
N_MISSIONS = 5
MISSIONS = ["A1", "B1", "A2", "B2", "C1"]
MISSION_TARGET_WPS = {"A1": "WP02", "B1": "WP03", "A2": "WP05",
                      "B2": "WP06", "C1": "WP07"}
TRIGGER_WAYPOINTS = ["WP03", "WP06"]
WAYPOINTS = [f"WP{i:02d}" for i in range(1, 9)]

SIDECAR_SUFFIXES = (
    "_head_pose.csv", "_nav_trace.csv", "_beam_segments.csv",
    "_anchor_reloc.csv", "_system_health.csv",
)

# 종속변수 정의 (Table 1 / Forest plot 순서)
DV_LIST = [
    "completion_time",
    "pause_count",
    "accuracy",
    "confidence_mean",
    "trust_mean",
    "tlx_total",
    "tlx_mental",
    "tlx_physical",
    "tlx_temporal",
    "tlx_performance",
    "tlx_effort",
    "tlx_frustration",
    "switching_count",
    "beam_total_time",
]

DV_LABELS = {
    "completion_time": "Completion Time (s)",
    "pause_count": "Pause Count",
    "accuracy": "Verification Accuracy",
    "confidence_mean": "Confidence (1-7)",
    "trust_mean": "Trust (1-7)",
    "tlx_total": "TLX Total (1-7)",
    "tlx_mental": "TLX Mental Demand",
    "tlx_physical": "TLX Physical Demand",
    "tlx_temporal": "TLX Temporal Demand",
    "tlx_performance": "TLX Performance",
    "tlx_effort": "TLX Effort",
    "tlx_frustration": "TLX Frustration",
    "switching_count": "Device Switching Count",
    "beam_total_time": "Beam Pro Total Time (s)",
}

TLX_SUBSCALES = [
    "mental_demand", "physical_demand", "temporal_demand",
    "performance", "effort", "frustration",
]

# 카운터밸런싱 그룹 정의
CB_GROUPS = {
    "G1": {"first_cond": "glass_only", "second_cond": "hybrid",
            "first_set": "Set1", "second_set": "Set2"},
    "G2": {"first_cond": "glass_only", "second_cond": "hybrid",
            "first_set": "Set2", "second_set": "Set1"},
    "G3": {"first_cond": "hybrid", "second_cond": "glass_only",
            "first_set": "Set1", "second_set": "Set2"},
    "G4": {"first_cond": "hybrid", "second_cond": "glass_only",
            "first_set": "Set2", "second_set": "Set1"},
}


# ══════════════════════════════════════════════
#  Fallback 데이터 생성 (24명 x 2조건, 4 카운터밸런싱 그룹)
# ══════════════════════════════════════════════

def generate_fallback_data() -> dict:
    """24명 참가자, 4 카운터밸런싱 그룹에 대한 fallback 데이터 생성.

    Returns:
        dict with keys: events_df, tlx_df, trust_df, cb_map
    """
    print("[Fallback] 24명 참가자 데이터 생성 (4 카운터밸런싱 그룹)...")
    rng = np.random.default_rng(2026)

    # 참가자→그룹 할당 (6명씩)
    group_names = list(CB_GROUPS.keys())
    cb_map = {}  # participant_id -> group_name
    for i in range(N_PARTICIPANTS):
        pid = f"P{i + 1:02d}"
        cb_map[pid] = group_names[i % 4]

    # ──────────────────────────────────────
    # 1. 이벤트 CSV 데이터 생성
    # ──────────────────────────────────────
    event_rows = []
    base_time = pd.Timestamp("2026-03-20T09:00:00")

    # 조건별 기본 파라미터
    params = {
        "glass_only": {
            "acc_base": 0.70, "completion_base": 480, "completion_sd": 80,
            "conf_base": 3.6, "conf_sd": 0.9,
            "pause_base": 5.0, "pause_sd": 1.5,
            "diff_base": 3.4, "diff_sd": 1.0,
        },
        "hybrid": {
            "acc_base": 0.80, "completion_base": 440, "completion_sd": 70,
            "conf_base": 4.9, "conf_sd": 0.9,
            "pause_base": 3.5, "pause_sd": 1.2,
            "diff_base": 2.9, "diff_sd": 1.0,
        },
    }

    for pid_idx in range(N_PARTICIPANTS):
        pid = f"P{pid_idx + 1:02d}"
        group = cb_map[pid]
        group_cfg = CB_GROUPS[group]

        for run_num, cond in enumerate([group_cfg["first_cond"],
                                         group_cfg["second_cond"]], 1):
            mission_set = group_cfg["first_set"] if run_num == 1 else group_cfg["second_set"]
            p = params[cond]

            # 개인차 (참가자 고유 오프셋)
            pid_offset_time = rng.normal(0, 40)
            pid_offset_conf = rng.normal(0, 0.3)
            pid_offset_acc = rng.normal(0, 0.05)

            ts_offset = pid_idx * 7200 + (run_num - 1) * 3600
            session_start = base_time + pd.Timedelta(seconds=ts_offset)
            session_id = f"{pid}_{session_start.strftime('%Y%m%d_%H%M%S')}"

            t = session_start

            def _evt(ts, etype, wp="", **kw):
                """이벤트 행 생성 헬퍼."""
                extra = {k: v for k, v in kw.items()
                         if k not in ("confidence_rating", "difficulty_rating",
                                      "verification_correct", "beam_content_type",
                                      "trigger_id", "trigger_type", "cause",
                                      "duration_s", "distance_m", "anchor_bound",
                                      "arrow_visible", "mission_id")}
                return {
                    "timestamp": ts.isoformat(),
                    "participant_id": pid,
                    "condition": cond,
                    "event_type": etype,
                    "waypoint_id": wp,
                    "head_rotation_x": round(rng.normal(0, 5), 1),
                    "head_rotation_y": round(rng.uniform(-180, 180), 1),
                    "head_rotation_z": round(rng.normal(0, 2), 1),
                    "device_active": "glass" if cond == "glass_only" else "both",
                    "confidence_rating": kw.get("confidence_rating", ""),
                    "mission_id": kw.get("mission_id", ""),
                    "difficulty_rating": kw.get("difficulty_rating", ""),
                    "verification_correct": (
                        str(kw["verification_correct"]).lower()
                        if "verification_correct" in kw else ""
                    ),
                    "beam_content_type": kw.get("beam_content_type", ""),
                    "session_id": session_id,
                    "mission_set": mission_set,
                    "trigger_id": kw.get("trigger_id", ""),
                    "trigger_type": kw.get("trigger_type", ""),
                    "cause": kw.get("cause", ""),
                    "duration_s": (
                        str(kw["duration_s"]) if "duration_s" in kw else ""
                    ),
                    "distance_m": (
                        str(kw["distance_m"]) if "distance_m" in kw else ""
                    ),
                    "anchor_bound": (
                        str(kw["anchor_bound"]).lower()
                        if "anchor_bound" in kw else ""
                    ),
                    "arrow_visible": (
                        str(kw["arrow_visible"]).lower()
                        if "arrow_visible" in kw else ""
                    ),
                    "extra_data": json.dumps(extra) if extra else "{}",
                }

            # SESSION_INITIALIZED
            event_rows.append(_evt(t, "SESSION_INITIALIZED"))
            t += pd.Timedelta(seconds=5)

            # RELOCALIZATION
            event_rows.append(_evt(t, "RELOCALIZATION_STARTED"))
            reloc_dur = rng.uniform(15, 35)
            t += pd.Timedelta(seconds=reloc_dur)
            event_rows.append(_evt(t, "RELOCALIZATION_COMPLETED",
                                   duration_s=round(reloc_dur, 1)))
            t += pd.Timedelta(seconds=3)

            # ROUTE_START
            event_rows.append(_evt(t, "ROUTE_START"))
            t += pd.Timedelta(seconds=2)

            mission_idx = 0
            current_mission = MISSIONS[mission_idx]
            event_rows.append(_evt(t, "MISSION_START", WAYPOINTS[0],
                                   mission_id=current_mission))

            for wp in WAYPOINTS:
                # 정지 이벤트
                if rng.random() < 0.35:
                    pause_dur = rng.exponential(p["pause_sd"]) + 1.5
                    event_rows.append(_evt(t, "PAUSE_START", wp))
                    t += pd.Timedelta(seconds=pause_dur)
                    event_rows.append(_evt(t, "PAUSE_END", wp,
                                           duration_s=round(pause_dur, 1)))

                # Beam Pro 전환 (hybrid만)
                if cond == "hybrid":
                    switch_prob = 0.60 if wp in TRIGGER_WAYPOINTS else 0.25
                    if rng.random() < switch_prob:
                        event_rows.append(_evt(t, "BEAM_SCREEN_ON", wp))
                        beam_dur = rng.uniform(3, 18)
                        t += pd.Timedelta(seconds=beam_dur)
                        event_rows.append(_evt(t, "BEAM_SCREEN_OFF", wp,
                                               duration_s=round(beam_dur, 1)))

                # 트리거 이벤트
                trigger_at = {"WP03": "T2", "WP06": "T3"} if mission_set == "Set1" else {
                    "WP03": "T1", "WP06": "T4"}
                if wp in trigger_at:
                    ttype = trigger_at[wp]
                    event_rows.append(_evt(t, "TRIGGER_ACTIVATED", wp,
                                           trigger_id=ttype, trigger_type=ttype,
                                           mission_id=current_mission))
                    trigger_dur = rng.uniform(6, 14)
                    t += pd.Timedelta(seconds=trigger_dur)
                    event_rows.append(_evt(t, "TRIGGER_DEACTIVATED", wp,
                                           trigger_id=ttype, trigger_type=ttype,
                                           duration_s=round(trigger_dur, 1),
                                           mission_id=current_mission))

                # 이동 시간
                move_dur = rng.uniform(35, 90) + pid_offset_time * 0.1
                t += pd.Timedelta(seconds=max(move_dur, 20))

                # WAYPOINT_REACHED
                dist = round(rng.uniform(0.5, 2.5), 2)
                event_rows.append(_evt(t, "WAYPOINT_REACHED", wp,
                                       distance_m=dist,
                                       anchor_bound=rng.random() < 0.55,
                                       cause="proximity",
                                       mission_id=current_mission))

                # 확신도
                conf_val = rng.normal(p["conf_base"] + pid_offset_conf, p["conf_sd"])
                if wp in TRIGGER_WAYPOINTS:
                    conf_val -= rng.uniform(0.3, 0.8)
                conf_val = int(np.clip(round(conf_val), 1, 7))
                event_rows.append(_evt(t, "CONFIDENCE_RATED", wp,
                                       confidence_rating=conf_val,
                                       mission_id=current_mission))

                # 미션 완료 검증
                if mission_idx < len(MISSIONS):
                    m = MISSIONS[mission_idx]
                    if wp == MISSION_TARGET_WPS.get(m, ""):
                        correct = rng.random() < (p["acc_base"] + pid_offset_acc)
                        rt = round(rng.uniform(2, 8), 1)
                        t += pd.Timedelta(seconds=rt)
                        event_rows.append(_evt(t, "VERIFICATION_ANSWERED", wp,
                                               verification_correct=correct,
                                               duration_s=rt,
                                               mission_id=m))
                        event_rows.append(_evt(t, "MISSION_COMPLETE", wp,
                                               mission_id=m,
                                               verification_correct=correct))

                        diff_val = int(np.clip(
                            round(rng.normal(p["diff_base"], p["diff_sd"])), 1, 7))
                        event_rows.append(_evt(t, "DIFFICULTY_RATED", wp,
                                               difficulty_rating=diff_val,
                                               mission_id=m))

                        mission_idx += 1
                        if mission_idx < len(MISSIONS):
                            current_mission = MISSIONS[mission_idx]
                            t += pd.Timedelta(seconds=rng.uniform(3, 8))
                            event_rows.append(_evt(t, "MISSION_START", wp,
                                                   mission_id=current_mission))

            # ROUTE_END
            t += pd.Timedelta(seconds=rng.uniform(3, 8))
            event_rows.append(_evt(t, "ROUTE_END"))

    events_df = pd.DataFrame(event_rows)
    events_df["timestamp"] = pd.to_datetime(events_df["timestamp"],
                                            format="ISO8601")

    # ──────────────────────────────────────
    # 2. NASA-TLX 데이터 생성
    # ──────────────────────────────────────
    tlx_means = {
        "glass_only": {"mental_demand": 4.3, "physical_demand": 3.0, "temporal_demand": 3.7,
                       "performance": 3.5, "effort": 4.0, "frustration": 3.7},
        "hybrid":     {"mental_demand": 3.7, "physical_demand": 2.8, "temporal_demand": 3.2,
                       "performance": 3.0, "effort": 3.4, "frustration": 3.0},
    }
    tlx_rows = []
    for pid_idx in range(N_PARTICIPANTS):
        pid = f"P{pid_idx + 1:02d}"
        for cond in CONDITIONS:
            row = {"participant_id": pid, "condition": cond}
            for sub in TLX_SUBSCALES:
                val = rng.normal(tlx_means[cond][sub], 1.2)
                row[sub] = int(np.clip(round(val), 1, 7))
            tlx_rows.append(row)
    tlx_df = pd.DataFrame(tlx_rows)

    # ──────────────────────────────────────
    # 3. 신뢰 척도 데이터 생성
    # ──────────────────────────────────────
    trust_means = {"glass_only": 4.4, "hybrid": 5.0}
    trust_rows = []
    for pid_idx in range(N_PARTICIPANTS):
        pid = f"P{pid_idx + 1:02d}"
        for cond in CONDITIONS:
            row = {"participant_id": pid, "condition": cond}
            for q in range(1, 8):
                val = rng.normal(trust_means[cond], 1.0)
                row[f"trust_q{q}"] = int(np.clip(round(val), 1, 7))
            row["trust_mean"] = round(
                np.mean([row[f"trust_q{q}"] for q in range(1, 8)]), 2)
            trust_rows.append(row)
    trust_df = pd.DataFrame(trust_rows)

    print(f"  이벤트: {len(events_df)}행, NASA-TLX: {len(tlx_df)}행, "
          f"신뢰: {len(trust_df)}행")

    return {
        "events_df": events_df,
        "tlx_df": tlx_df,
        "trust_df": trust_df,
        "cb_map": cb_map,
    }


# ══════════════════════════════════════════════
#  실제 데이터 로드
# ══════════════════════════════════════════════

def load_real_data() -> dict:
    """실제 CSV 데이터 로드."""
    # 이벤트 CSV 로드
    csv_files = sorted(
        f for f in RAW_DIR.glob("P*_*.csv")
        if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
    )
    if not csv_files:
        print(f"[오류] {RAW_DIR}에 CSV 파일이 없습니다.")
        print("  fallback 데이터로 실행하려면 --fallback 플래그를 사용하세요.")
        sys.exit(1)

    frames = []
    for f in csv_files:
        df = pd.read_csv(f, parse_dates=["timestamp"])
        frames.append(df)
    events_df = pd.concat(frames, ignore_index=True)

    # NASA-TLX 로드
    tlx_path = SURVEY_DIR / "nasa_tlx.csv"
    if tlx_path.exists():
        tlx_df = pd.read_csv(tlx_path)
    else:
        print(f"[경고] {tlx_path} 없음. 이벤트 로그의 인앱 설문 데이터에서 추출 시도...")
        tlx_df = _extract_tlx_from_events(events_df)

    # 신뢰 척도 로드
    trust_path = SURVEY_DIR / "trust_scale.csv"
    if trust_path.exists():
        trust_df = pd.read_csv(trust_path)
    else:
        print(f"[경고] {trust_path} 없음. 이벤트 로그의 인앱 설문 데이터에서 추출 시도...")
        trust_df = _extract_trust_from_events(events_df)

    # 카운터밸런싱 그룹 추정
    cb_map = _infer_cb_map(events_df)

    print(f"  이벤트: {len(events_df)}행 ({events_df['participant_id'].nunique()}명), "
          f"NASA-TLX: {len(tlx_df)}행, 신뢰: {len(trust_df)}행")

    return {
        "events_df": events_df,
        "tlx_df": tlx_df,
        "trust_df": trust_df,
        "cb_map": cb_map,
    }


def _extract_tlx_from_events(events_df: pd.DataFrame) -> pd.DataFrame:
    """인앱 설문 이벤트에서 NASA-TLX 데이터 추출 (SURVEY_ITEM_ANSWERED)."""
    survey_items = events_df[events_df["event_type"] == "SURVEY_ITEM_ANSWERED"].copy()
    if survey_items.empty:
        print("  [경고] SURVEY_ITEM_ANSWERED 이벤트 없음, 빈 TLX DataFrame 반환")
        return pd.DataFrame(columns=["participant_id", "condition"] + TLX_SUBSCALES)

    rows = []
    for (pid, cond), grp in survey_items.groupby(["participant_id", "condition"]):
        row = {"participant_id": pid, "condition": cond}
        for _, item in grp.iterrows():
            extra = parse_extra(item.get("extra_data", "{}"))
            key = extra.get("item_key", "")
            value = extra.get("value", None)
            if key in TLX_SUBSCALES and value is not None:
                row[key] = int(value)
        if any(sub in row for sub in TLX_SUBSCALES):
            rows.append(row)
    return pd.DataFrame(rows)


def _extract_trust_from_events(events_df: pd.DataFrame) -> pd.DataFrame:
    """인앱 설문 이벤트에서 Trust 데이터 추출."""
    survey_items = events_df[events_df["event_type"] == "SURVEY_ITEM_ANSWERED"].copy()
    if survey_items.empty:
        return pd.DataFrame(columns=["participant_id", "condition", "trust_mean"])

    rows = []
    for (pid, cond), grp in survey_items.groupby(["participant_id", "condition"]):
        row = {"participant_id": pid, "condition": cond}
        trust_vals = []
        for _, item in grp.iterrows():
            extra = parse_extra(item.get("extra_data", "{}"))
            key = extra.get("item_key", "")
            value = extra.get("value", None)
            if key.startswith("trust_") and value is not None:
                q_num = key.replace("trust_", "")
                row[f"trust_q{q_num}"] = int(value)
                trust_vals.append(int(value))
        if trust_vals:
            row["trust_mean"] = round(np.mean(trust_vals), 2)
            rows.append(row)
    return pd.DataFrame(rows)


def _infer_cb_map(events_df: pd.DataFrame) -> dict:
    """이벤트 데이터에서 참가자별 카운터밸런싱 그룹 추정.

    첫 번째 세션의 condition과 mission_set으로 그룹 결정.
    """
    cb_map = {}
    for pid, grp in events_df.groupby("participant_id"):
        sessions = grp.sort_values("timestamp")
        first_session = sessions.iloc[0]
        first_cond = first_session.get("condition", "")
        first_set = first_session.get("mission_set", "")

        for gname, gcfg in CB_GROUPS.items():
            if gcfg["first_cond"] == first_cond and gcfg["first_set"] == first_set:
                cb_map[pid] = gname
                break
        else:
            cb_map[pid] = "unknown"

    return cb_map


# ══════════════════════════════════════════════
#  DV 집계 (participant x condition 수준)
# ══════════════════════════════════════════════

def compute_dvs(events_df: pd.DataFrame,
                tlx_df: pd.DataFrame,
                trust_df: pd.DataFrame,
                cb_map: dict) -> pd.DataFrame:
    """이벤트/설문 데이터에서 모든 DV를 참가자 x 조건 수준으로 집계.

    Returns:
        DataFrame with columns: participant_id, condition, order_group,
                                + all DV columns
    """
    participants = sorted(events_df["participant_id"].unique())
    records = []

    for pid in participants:
        for cond in CONDITIONS:
            pid_cond = events_df[
                (events_df["participant_id"] == pid) &
                (events_df["condition"] == cond)
            ]
            if pid_cond.empty:
                continue

            rec = {
                "participant_id": pid,
                "condition": cond,
                "order_group": cb_map.get(pid, "unknown"),
            }

            # ── completion_time ──
            route_start = pid_cond[pid_cond["event_type"] == "ROUTE_START"]
            route_end = pid_cond[pid_cond["event_type"] == "ROUTE_END"]
            if not route_start.empty and not route_end.empty:
                t_start = pd.to_datetime(route_start["timestamp"].iloc[0])
                t_end = pd.to_datetime(route_end["timestamp"].iloc[-1])
                rec["completion_time"] = (t_end - t_start).total_seconds()
            else:
                rec["completion_time"] = np.nan

            # ── pause_count ──
            pauses = pid_cond[pid_cond["event_type"] == "PAUSE_START"]
            rec["pause_count"] = len(pauses)

            # ── accuracy ──
            verifications = pid_cond[
                pid_cond["event_type"] == "VERIFICATION_ANSWERED"
            ].copy()
            if not verifications.empty:
                if "verification_correct" in verifications.columns:
                    vc = verifications["verification_correct"].copy()
                    vc = vc.map({"true": True, "false": False, True: True, False: False})
                    vc = vc.dropna()
                    if len(vc) > 0:
                        rec["accuracy"] = float(vc.astype(float).mean())
                    else:
                        # extra_data fallback
                        corrects = []
                        for _, row in verifications.iterrows():
                            extra = parse_extra(row.get("extra_data", "{}"))
                            c = extra.get("correct", extra.get("verification_correct", None))
                            if c is not None:
                                corrects.append(bool(c))
                        rec["accuracy"] = (
                            np.mean(corrects) if corrects else np.nan
                        )
                else:
                    rec["accuracy"] = np.nan
            else:
                rec["accuracy"] = np.nan

            # ── confidence_mean ──
            conf_events = pid_cond[
                pid_cond["event_type"] == "CONFIDENCE_RATED"
            ].copy()
            if not conf_events.empty:
                conf_vals = pd.to_numeric(
                    conf_events["confidence_rating"], errors="coerce"
                ).dropna()
                rec["confidence_mean"] = (
                    float(conf_vals.mean()) if len(conf_vals) > 0 else np.nan
                )
            else:
                rec["confidence_mean"] = np.nan

            # ── switching_count ──
            beam_on = pid_cond[pid_cond["event_type"] == "BEAM_SCREEN_ON"]
            rec["switching_count"] = len(beam_on)

            # ── beam_total_time ──
            beam_off = pid_cond[pid_cond["event_type"] == "BEAM_SCREEN_OFF"]
            if not beam_off.empty:
                durations = []
                for _, row in beam_off.iterrows():
                    dur_val = pd.to_numeric(
                        row.get("duration_s", np.nan), errors="coerce"
                    )
                    if not np.isnan(dur_val):
                        durations.append(dur_val)
                    else:
                        extra = parse_extra(row.get("extra_data", "{}"))
                        dur_extra = extra.get("duration_s", None)
                        if dur_extra is not None:
                            durations.append(float(dur_extra))
                rec["beam_total_time"] = sum(durations) if durations else 0.0
            else:
                rec["beam_total_time"] = 0.0

            records.append(rec)

    dv_df = pd.DataFrame(records)

    # ── TLX 병합 ──
    if not tlx_df.empty:
        tlx_merge = tlx_df.copy()
        # TLX total = 6 하위척도 평균
        available_subs = [s for s in TLX_SUBSCALES if s in tlx_merge.columns]
        if available_subs:
            tlx_merge["tlx_total"] = tlx_merge[available_subs].mean(axis=1)
            # 개별 하위척도를 tlx_ 접두사로 이름 변경
            rename_map = {sub: f"tlx_{sub.replace('_demand', '').replace('demand', '')}"
                          for sub in available_subs}
            # 더 명확한 이름 매핑
            rename_map = {}
            for sub in available_subs:
                short = sub.replace("_demand", "")
                rename_map[sub] = f"tlx_{short}"
            tlx_for_merge = tlx_merge[
                ["participant_id", "condition", "tlx_total"] +
                available_subs
            ].rename(columns=rename_map)
            dv_df = dv_df.merge(
                tlx_for_merge, on=["participant_id", "condition"], how="left"
            )

    # ── Trust 병합 ──
    if not trust_df.empty and "trust_mean" in trust_df.columns:
        trust_for_merge = trust_df[["participant_id", "condition", "trust_mean"]]
        dv_df = dv_df.merge(
            trust_for_merge, on=["participant_id", "condition"], how="left"
        )

    print(f"\n[DV 집계] {len(dv_df)}행 ({dv_df['participant_id'].nunique()}명 x "
          f"{dv_df['condition'].nunique()}조건)")

    return dv_df


# ══════════════════════════════════════════════
#  Table 1: 전체 DV 요약 테이블
# ══════════════════════════════════════════════

def generate_table1(dv_df: pd.DataFrame) -> pd.DataFrame:
    """Table 1: 전체 DV 요약 (M, SD, 통계량, p, Cohen's d, 95% CI).

    Returns:
        결과 DataFrame
    """
    print("\n" + "=" * 70)
    print("Table 1: 종속변수 요약 (Glass Only vs Hybrid)")
    print("=" * 70)

    # 실제 존재하는 DV만 사용
    available_dvs = [dv for dv in DV_LIST if dv in dv_df.columns]

    results_df = batch_paired_comparison(
        dv_df, available_dvs,
        condition_col="condition",
        subject_col="participant_id",
        cond_a="glass_only",
        cond_b="hybrid",
        bonferroni=False,
    )

    # 콘솔 출력
    print(f"\n{'DV':<25} {'Glass Only':>15} {'Hybrid':>15} "
          f"{'Test':>13} {'p':>8} {'d':>7} {'95% CI':>16} {'Sig':>5}")
    print("-" * 110)

    for _, row in results_df.iterrows():
        dv_name = row["dv"]
        label = DV_LABELS.get(dv_name, dv_name)
        glass_str = f"{row['mean_a']:.2f} ({row['sd_a']:.2f})"
        hybrid_str = f"{row['mean_b']:.2f} ({row['sd_b']:.2f})"
        test_str = f"{row['test']}"
        p_str = format_p(row["p"])
        d_str = f"{row['d']:.2f}" if not np.isnan(row["d"]) else "-"
        ci_str = format_ci(row["d_ci_lo"], row["d_ci_hi"])
        sig = significance_marker(row["p"])
        print(f"{label:<25} {glass_str:>15} {hybrid_str:>15} "
              f"{test_str:>13} {p_str:>8} {d_str:>7} {ci_str:>16} {sig:>5}")

    # CSV 저장
    csv_dir = OUTPUT_DIR / "csv"
    csv_path = csv_dir / "table1_dv_summary.csv"
    out_df = results_df.copy()
    out_df["label"] = out_df["dv"].map(DV_LABELS)
    out_df.to_csv(csv_path, index=False)
    print(f"\n  -> {csv_path} 저장")

    # LaTeX 저장
    latex_path = csv_dir / "table1_dv_summary.tex"
    _write_table1_latex(results_df, latex_path)
    print(f"  -> {latex_path} 저장")

    # 통계 한줄 요약 출력
    print("\n[통계 한줄 요약]")
    for _, row in results_df.iterrows():
        label = DV_LABELS.get(row["dv"], row["dv"])
        line = format_stat_line(row.to_dict())
        print(f"  {label}: {line}")

    return results_df


def _write_table1_latex(results_df: pd.DataFrame, path: Path):
    """Table 1을 LaTeX tabular 형식으로 저장."""
    lines = []
    lines.append("\\begin{table}[t]")
    lines.append("\\centering")
    lines.append("\\caption{Summary of dependent variables across conditions "
                 "(Glass Only vs.\\ Hybrid). "
                 "$M$ = Mean, $SD$ = Standard Deviation.}")
    lines.append("\\label{tab:dv-summary}")
    lines.append("\\small")
    lines.append("\\begin{tabular}{lcccccc}")
    lines.append("\\toprule")
    lines.append("Dependent Variable & \\multicolumn{2}{c}{Glass Only} & "
                 "\\multicolumn{2}{c}{Hybrid} & Cohen's $d$ & $p$ \\\\")
    lines.append(" & $M$ & $SD$ & $M$ & $SD$ & [95\\% CI] & \\\\")
    lines.append("\\midrule")

    for _, row in results_df.iterrows():
        label = DV_LABELS.get(row["dv"], row["dv"])
        # LaTeX-safe label
        label_tex = label.replace("_", "\\_").replace("%", "\\%")

        mean_a = f"{row['mean_a']:.2f}"
        sd_a = f"{row['sd_a']:.2f}"
        mean_b = f"{row['mean_b']:.2f}"
        sd_b = f"{row['sd_b']:.2f}"

        if np.isnan(row["d"]):
            d_ci = "--"
        else:
            d_ci = f"{row['d']:.2f} [{row['d_ci_lo']:.2f}, {row['d_ci_hi']:.2f}]"

        p_val = row["p"]
        if np.isnan(p_val):
            p_str = "--"
        elif p_val < 0.001:
            p_str = "$< .001$"
        else:
            p_str = f"$.{str(round(p_val, 3))[2:]}$"

        sig = significance_marker(p_val)
        if sig and sig != "n.s.":
            p_str += f"\\textsuperscript{{{sig}}}"

        lines.append(
            f"{label_tex} & {mean_a} & {sd_a} & {mean_b} & {sd_b} & "
            f"{d_ci} & {p_str} \\\\"
        )

    lines.append("\\bottomrule")
    lines.append("\\end{tabular}")
    lines.append("\\vspace{2pt}")
    lines.append("\\raggedright\\footnotesize")
    lines.append("\\textit{Note.} $^{*}p < .05$, $^{**}p < .01$, "
                 "$^{***}p < .001$. "
                 "Tests selected automatically: "
                 "paired $t$-test if Shapiro-Wilk $p \\geq .05$, "
                 "Wilcoxon signed-rank otherwise.")
    lines.append("\\end{table}")

    path.write_text("\n".join(lines), encoding="utf-8")


# ══════════════════════════════════════════════
#  Table 2: 순서 효과 검증 (Condition x Order Mixed ANOVA)
# ══════════════════════════════════════════════

def generate_table2(dv_df: pd.DataFrame) -> pd.DataFrame:
    """Table 2: 순서 효과 검증 (Mixed ANOVA).

    Returns:
        ANOVA 결과 DataFrame
    """
    print("\n" + "=" * 70)
    print("Table 2: 순서 효과 검증 (Condition x Order Mixed ANOVA)")
    print("=" * 70)

    # pingouin 필수
    try:
        import pingouin as pg  # noqa: F401
    except ImportError:
        print("  [오류] pingouin 패키지가 필요합니다: pip install pingouin")
        return pd.DataFrame()

    # order_group이 있는지 확인
    if "order_group" not in dv_df.columns or dv_df["order_group"].nunique() < 2:
        print("  [경고] order_group 데이터 부족 (2그룹 미만). 순서 효과 검증 불가.")
        return pd.DataFrame()

    main_dvs = ["completion_time", "accuracy", "confidence_mean",
                "trust_mean", "tlx_total"]
    available_dvs = [dv for dv in main_dvs if dv in dv_df.columns]

    all_results = []

    print(f"\n{'DV':<25} {'Source':>20} {'F':>8} {'p':>8} {'np2':>8} {'Sig':>5}")
    print("-" * 80)

    for dv in available_dvs:
        result = test_order_effect(
            dv_df, dv,
            condition_col="condition",
            order_col="order_group",
            subject_col="participant_id",
        )
        if result is None:
            print(f"{DV_LABELS.get(dv, dv):<25} {'(검정 실패)':>20}")
            continue

        for source, vals in result.items():
            sig = significance_marker(vals["p"])
            print(f"{DV_LABELS.get(dv, dv):<25} {source:>20} "
                  f"{vals['F']:>8.3f} {vals['p']:>8.4f} "
                  f"{vals['np2']:>8.3f} {sig:>5}")
            all_results.append({
                "dv": dv,
                "label": DV_LABELS.get(dv, dv),
                "source": source,
                "F": vals["F"],
                "p": vals["p"],
                "np2": vals["np2"],
                "significant": vals["p"] < 0.05,
            })

    results_df = pd.DataFrame(all_results)

    if not results_df.empty:
        # CSV 저장
        csv_dir = OUTPUT_DIR / "csv"
        csv_path = csv_dir / "table2_order_effects.csv"
        results_df.to_csv(csv_path, index=False)
        print(f"\n  -> {csv_path} 저장")

        # LaTeX 저장
        latex_path = csv_dir / "table2_order_effects.tex"
        _write_table2_latex(results_df, latex_path)
        print(f"  -> {latex_path} 저장")

        # 순서 효과 유무 요약
        interaction = results_df[results_df["source"] == "Interaction"]
        if not interaction.empty:
            any_sig = interaction["significant"].any()
            if any_sig:
                sig_dvs = interaction[interaction["significant"]]["label"].tolist()
                print(f"\n  [주의] 조건 x 순서 교호작용 유의: {', '.join(sig_dvs)}")
            else:
                print(f"\n  [확인] 조건 x 순서 교호작용 전부 비유의 "
                      f"-> 카운터밸런싱 유효")

    return results_df


def _write_table2_latex(results_df: pd.DataFrame, path: Path):
    """Table 2를 LaTeX tabular 형식으로 저장."""
    lines = []
    lines.append("\\begin{table}[t]")
    lines.append("\\centering")
    lines.append("\\caption{Mixed ANOVA results for Condition "
                 "(within-subjects) $\\times$ Order Group (between-subjects). "
                 "Non-significant interaction effects validate the "
                 "counterbalancing design.}")
    lines.append("\\label{tab:order-effects}")
    lines.append("\\small")
    lines.append("\\begin{tabular}{llccc}")
    lines.append("\\toprule")
    lines.append("Dependent Variable & Source & $F$ & $p$ & $\\eta_p^2$ \\\\")
    lines.append("\\midrule")

    current_dv = ""
    for _, row in results_df.iterrows():
        label = row["label"].replace("_", "\\_")
        if row["dv"] != current_dv:
            if current_dv:
                lines.append("\\addlinespace")
            current_dv = row["dv"]
            dv_label = label
        else:
            dv_label = ""

        p_val = row["p"]
        if p_val < 0.001:
            p_str = "$< .001$"
        else:
            p_str = f"$.{str(round(p_val, 3))[2:]}$"

        sig = significance_marker(p_val)
        if sig and sig != "n.s.":
            p_str += f"\\textsuperscript{{{sig}}}"

        source = row["source"]
        lines.append(
            f"{dv_label} & {source} & {row['F']:.3f} & "
            f"{p_str} & {row['np2']:.3f} \\\\"
        )

    lines.append("\\bottomrule")
    lines.append("\\end{tabular}")
    lines.append("\\vspace{2pt}")
    lines.append("\\raggedright\\footnotesize")
    lines.append("\\textit{Note.} $^{*}p < .05$, $^{**}p < .01$, "
                 "$^{***}p < .001$. "
                 "$\\eta_p^2$ = partial eta squared.")
    lines.append("\\end{table}")

    path.write_text("\n".join(lines), encoding="utf-8")


# ══════════════════════════════════════════════
#  Fig 1: 4-panel Violin Plot (주요 DV)
# ══════════════════════════════════════════════

def generate_fig1(dv_df: pd.DataFrame, results_df: pd.DataFrame):
    """Fig 1: 4개 주요 DV의 2x2 violin plot."""
    print("\n" + "=" * 70)
    print("Fig 1: 주요 DV Violin Plot (2x2)")
    print("=" * 70)

    main_dvs = [
        ("accuracy", "Verification Accuracy", ""),
        ("completion_time", "Completion Time (s)", ""),
        ("tlx_total", "NASA-TLX (0-21)", ""),
        ("trust_mean", "Trust (1-7)", ""),
    ]

    # 실제 존재하는 DV만 필터
    available = [(dv, label, unit) for dv, label, unit in main_dvs
                 if dv in dv_df.columns]

    if len(available) < 2:
        print("  [경고] 사용 가능한 주요 DV가 2개 미만, Fig 1 생성 건너뜀")
        return

    n_plots = len(available)
    nrows = 2 if n_plots > 2 else 1
    ncols = 2

    fig, axes = plt.subplots(nrows, ncols, figsize=FIG_DOUBLE_COL)
    if nrows == 1:
        axes = axes.reshape(1, -1)
    axes_flat = axes.flatten()

    for idx, (dv, label, unit) in enumerate(available):
        ax = axes_flat[idx]

        glass_vals = dv_df.loc[
            dv_df["condition"] == "glass_only", dv
        ].dropna().values
        hybrid_vals = dv_df.loc[
            dv_df["condition"] == "hybrid", dv
        ].dropna().values

        # p값 가져오기
        p_val = None
        if results_df is not None and not results_df.empty:
            match = results_df[results_df["dv"] == dv]
            if not match.empty:
                p_val = match["p"].iloc[0]

        violin_with_dots(
            ax,
            data_list=[glass_vals, hybrid_vals],
            positions=[0, 1],
            colors=COLORS_COND,
            labels=COND_LABELS,
            p_value=p_val,
            ylabel=label,
            title="",
        )

        # 서브플롯 라벨 (a, b, c, d)
        subplot_label = chr(ord("a") + idx)
        ax.text(-0.12, 1.05, f"({subplot_label})", transform=ax.transAxes,
                fontsize=11, fontweight="bold", va="top")

    # 남는 서브플롯 숨기기
    for idx in range(len(available), len(axes_flat)):
        axes_flat[idx].set_visible(False)

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "fig1_violin_main_dvs")


# ══════════════════════════════════════════════
#  Fig 4: Forest Plot (전체 DV 효과크기)
# ══════════════════════════════════════════════

def generate_fig4(results_df: pd.DataFrame):
    """Fig 4: 전체 DV의 Cohen's d + 95% CI Forest plot."""
    print("\n" + "=" * 70)
    print("Fig 4: Forest Plot (전체 DV 효과크기)")
    print("=" * 70)

    if results_df.empty:
        print("  [경고] 결과 데이터 없음, Fig 4 생성 건너뜀")
        return

    # DV 라벨을 사람이 읽기 쉬운 형태로 변환
    plot_df = results_df.copy()
    plot_df["label"] = plot_df["dv"].map(DV_LABELS)
    plot_df = plot_df.dropna(subset=["d"])

    if plot_df.empty:
        print("  [경고] 유효한 효과크기 데이터 없음")
        return

    # Figure 크기: DV 개수에 따라 높이 조정
    n_dvs = len(plot_df)
    fig_height = max(3.5, n_dvs * 0.45 + 1.0)
    fig, ax = plt.subplots(figsize=(FIG_DOUBLE_COL[0], fig_height))

    forest_plot(
        ax, plot_df,
        label_col="label",
        d_col="d",
        ci_lo_col="d_ci_lo",
        ci_hi_col="d_ci_hi",
        p_col="p",
        title="Effect Sizes: Glass Only vs. Hybrid (Cohen's d)",
    )

    # 방향 라벨 추가
    xlim = ax.get_xlim()
    ax.text(xlim[0] * 0.5, -1.2, "Favors Glass Only",
            ha="center", va="top", fontsize=7, style="italic", color="#666666")
    ax.text(xlim[1] * 0.5, -1.2, "Favors Hybrid",
            ha="center", va="top", fontsize=7, style="italic", color="#666666")

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "fig4_forest_plot")


# ══════════════════════════════════════════════
#  메인
# ══════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(
        description="종합 분석: CHI/UIST 논문용 Table 1, Table 2, Fig 1, Fig 4 생성"
    )
    parser.add_argument(
        "--fallback", action="store_true",
        help="데이터 파일이 없을 때 24명 fallback 데이터로 실행"
    )
    args = parser.parse_args()

    print("=" * 70)
    print("종합 분석 (CHI/UIST 논문용)")
    print("  Glass Only vs Hybrid — Within-Subjects Design")
    print("=" * 70)

    # 스타일 적용 + csv 서브폴더 준비
    apply_style()
    (OUTPUT_DIR / "csv").mkdir(exist_ok=True)

    # ── 데이터 로드 ──
    if args.fallback:
        data = generate_fallback_data()
    else:
        # 실제 데이터 존재 여부 확인
        csv_files = sorted(
            f for f in RAW_DIR.glob("P*_*.csv")
            if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
        )
        if csv_files:
            data = load_real_data()
        else:
            print(f"[경고] {RAW_DIR}에 CSV 파일 없음. --fallback 플래그로 실행하세요.")
            sys.exit(1)

    events_df = data["events_df"]
    tlx_df = data["tlx_df"]
    trust_df = data["trust_df"]
    cb_map = data["cb_map"]

    # ── DV 집계 ──
    dv_df = compute_dvs(events_df, tlx_df, trust_df, cb_map)

    # 기초 통계 출력
    print(f"\n[기초 통계]")
    print(f"  참가자 수: {dv_df['participant_id'].nunique()}")
    print(f"  조건: {dv_df['condition'].unique().tolist()}")
    print(f"  카운터밸런싱 그룹: {dv_df['order_group'].value_counts().to_dict()}")

    # ── Table 1: DV 요약 ──
    results_df = generate_table1(dv_df)

    # ── Table 2: 순서 효과 검증 ──
    order_df = generate_table2(dv_df)

    # ── Fig 1: 주요 DV Violin ──
    generate_fig1(dv_df, results_df)

    # ── Fig 4: Forest Plot ──
    generate_fig4(results_df)

    # ── 전체 DV 데이터 저장 ──
    csv_dir = OUTPUT_DIR / "csv"
    dv_path = csv_dir / "comprehensive_dv_data.csv"
    dv_df.to_csv(dv_path, index=False)
    print(f"\n  -> {dv_path} 저장")

    print("\n" + "=" * 70)
    print("종합 분석 완료.")
    print(f"  출력 디렉토리: {OUTPUT_DIR}")
    print("  생성 파일:")
    print("    - csv/table1_dv_summary.csv / .tex (Table 1)")
    print("    - csv/table2_order_effects.csv / .tex (Table 2)")
    print("    - fig1_violin_main_dvs.png / .pdf (Fig 1)")
    print("    - fig4_forest_plot.png / .pdf (Fig 4)")
    print("    - csv/comprehensive_dv_data.csv (전체 DV 데이터)")
    print("=" * 70)


if __name__ == "__main__":
    main()
