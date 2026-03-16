"""
Deep RQ 분석 스크립트 — CHI/ISMAR 논문용 고급 시각화 5종.

Fig 3: Calibration Curve (확신도 vs 실제 정확도)
Fig 2: Trust Recovery (트리거 전후 확신도 이벤트 잠금 평균)
Fig 8: Switching Cost (Beam Pro ON/OFF 전후 속도 프로파일)
Fig 9: Scatter + Regression (참가자별 확신도-정확도 산점도)
Fig 10: Interaction Plot (조건 x 트리거 유형 확신도 드롭)

주요 기능:
  - --fallback 플래그: data/raw/에 데이터 없을 때 fallback 데이터 자동 생성
  - data/raw/ 이벤트/nav_trace/beam_segments CSV 로드
  - 통계: paired_comparison, cohens_d_paired, significance_marker
  - 출판용 플롯: apply_style, save_fig, calibration_curve 등
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
from scipy import stats

from stat_utils import paired_comparison, cohens_d_paired, significance_marker
from plot_style import (
    apply_style, save_fig, calibration_curve, spaghetti_plot,
    interaction_plot, COLORS_COND, COND_LABELS, COLOR_GLASS, COLOR_HYBRID,
    COLOR_TRIGGER, FIG_DOUBLE_COL, DPI,
)
from trajectory_utils import (
    load_event_csv, load_nav_trace, load_beam_segments,
    find_session_files, generate_fallback_data, CONDITIONS,
)
from parse_utils import parse_extra

warnings.filterwarnings("ignore", category=FutureWarning)

# ──────────────────────────────────────────────
# 상수
# ──────────────────────────────────────────────

DATA_DIR = Path(__file__).resolve().parent.parent / "data"
RAW_DIR = DATA_DIR / "raw"
OUTPUT_DIR = Path(__file__).resolve().parent / "output"

SIDECAR_SUFFIXES = (
    "_head_pose.csv", "_nav_trace.csv", "_beam_segments.csv",
    "_anchor_reloc.csv", "_system_health.csv",
)

WAYPOINTS = [f"WP{i:02d}" for i in range(1, 9)]  # WP01-WP08
TRIGGER_WAYPOINTS = ["WP03", "WP06"]
TRIGGER_TYPES = ["T1", "T2", "T3", "T4"]

N_PARTICIPANTS = 24
MISSIONS = ["A1", "B1", "A2", "B2", "C1"]
MISSION_TARGET_WPS = {"A1": "WP02", "B1": "WP03", "A2": "WP05",
                      "B2": "WP06", "C1": "WP07"}


# ──────────────────────────────────────────────
# Fallback 데이터 생성 (24명, 자체 완결형)
# ──────────────────────────────────────────────

def _generate_fallback_data() -> Tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    """24명 x 2조건 fallback 데이터 생성.

    Returns:
        (events_df, nav_trace_df, beam_segments_df)
    """
    rng = np.random.default_rng(2026)
    base_time = pd.Timestamp("2026-03-15T10:00:00")

    wp_order = ["WP00"] + WAYPOINTS  # WP00 보정용 + WP01~WP08
    trigger_at_wp = {"WP03": "T2", "WP06": "T3"}

    all_events = []
    all_nav = []
    all_beam = []

    for pid_num in range(1, N_PARTICIPANTS + 1):
        participant_id = f"P{pid_num:02d}"
        mission_set = "Set1" if pid_num % 2 == 1 else "Set2"

        # Set1: T2+T3 at WP03/WP06, Set2: T1+T4 at WP03/WP06
        if mission_set == "Set1":
            trigger_map = {"WP03": "T2", "WP06": "T3"}
        else:
            trigger_map = {"WP03": "T1", "WP06": "T4"}

        for cond in CONDITIONS:
            ts_offset = (pid_num - 1) * 3600 + (0 if cond == "glass_only" else 1800)
            session_start = base_time + pd.Timedelta(seconds=ts_offset)
            session_id = f"{participant_id}_{session_start.strftime('%Y%m%d_%H%M%S')}"

            # ── 이벤트 CSV 생성 ──
            t = session_start
            mission_idx = 0
            current_mission = MISSIONS[0]

            # 확신도 기본값: Glass ~4.5, Hybrid ~5.5, 트리거 지점 ~1.5 드롭
            conf_base = 3.6 if cond == "glass_only" else 4.9
            acc_base = 0.70 if cond == "glass_only" else 0.80

            # 참가자별 개인차
            pid_offset = rng.normal(0, 0.5)

            rows_evt = []

            def _evt(ts, etype, wp="", **kw):
                return {
                    "timestamp": ts,
                    "participant_id": participant_id,
                    "condition": cond,
                    "event_type": etype,
                    "waypoint_id": wp,
                    "head_rotation_x": round(rng.normal(0, 5), 1),
                    "head_rotation_y": round(rng.uniform(-180, 180), 1),
                    "head_rotation_z": round(rng.normal(0, 2), 1),
                    "device_active": "glass" if cond == "glass_only" else "both",
                    "confidence_rating": kw.get("confidence_rating", np.nan),
                    "mission_id": kw.get("mission_id", ""),
                    "difficulty_rating": kw.get("difficulty_rating", np.nan),
                    "verification_correct": kw.get("verification_correct", None),
                    "beam_content_type": kw.get("beam_content_type", ""),
                    "session_id": session_id,
                    "mission_set": mission_set,
                    "trigger_id": kw.get("trigger_id", ""),
                    "trigger_type": kw.get("trigger_type", ""),
                    "cause": kw.get("cause", ""),
                    "duration_s": kw.get("duration_s", np.nan),
                    "distance_m": kw.get("distance_m", np.nan),
                    "anchor_bound": kw.get("anchor_bound", None),
                    "arrow_visible": kw.get("arrow_visible", None),
                    "extra_data": kw.get("extra_data", "{}"),
                }

            rows_evt.append(_evt(t, "SESSION_INITIALIZED"))
            t += pd.Timedelta(seconds=5)
            rows_evt.append(_evt(t, "ROUTE_START"))
            t += pd.Timedelta(seconds=2)
            rows_evt.append(_evt(t, "MISSION_START", "WP01",
                                 mission_id=current_mission))

            for wp_i, wp in enumerate(WAYPOINTS):
                move_dur = rng.uniform(30, 70)

                # 트리거 이벤트
                ttype = trigger_map.get(wp, "")
                if ttype:
                    trigger_t = t + pd.Timedelta(seconds=move_dur * 0.6)
                    rows_evt.append(_evt(trigger_t, "TRIGGER_ACTIVATED", wp,
                                         trigger_id=ttype, trigger_type=ttype,
                                         mission_id=current_mission))
                    trigger_dur = rng.uniform(5, 15)
                    rows_evt.append(_evt(
                        trigger_t + pd.Timedelta(seconds=trigger_dur),
                        "TRIGGER_DEACTIVATED", wp,
                        trigger_id=ttype, trigger_type=ttype,
                        duration_s=round(trigger_dur, 1),
                        mission_id=current_mission))

                # Beam Pro 이벤트 (hybrid)
                if cond == "hybrid" and rng.random() < 0.40:
                    beam_on_t = t + pd.Timedelta(seconds=move_dur * 0.5)
                    rows_evt.append(_evt(beam_on_t, "BEAM_SCREEN_ON", wp,
                                         mission_id=current_mission))
                    beam_dur = rng.uniform(3, 15)
                    rows_evt.append(_evt(
                        beam_on_t + pd.Timedelta(seconds=beam_dur),
                        "BEAM_SCREEN_OFF", wp,
                        duration_s=round(beam_dur, 1),
                        mission_id=current_mission))

                t += pd.Timedelta(seconds=move_dur)

                # WAYPOINT_REACHED
                dist = round(rng.uniform(0.5, 2.5), 2)
                rows_evt.append(_evt(t, "WAYPOINT_REACHED", wp,
                                     distance_m=dist,
                                     anchor_bound=rng.random() < 0.55,
                                     cause="proximity",
                                     mission_id=current_mission))

                # 확신도 결정
                is_trigger = wp in trigger_map
                if is_trigger:
                    conf_val = conf_base - 0.8 + pid_offset + rng.normal(0, 0.6)
                else:
                    conf_val = conf_base + pid_offset + rng.normal(0, 0.5)
                conf = int(np.clip(round(conf_val), 1, 7))
                rows_evt.append(_evt(t, "CONFIDENCE_RATED", wp,
                                     confidence_rating=conf,
                                     mission_id=current_mission))

                # 미션 완료 체크
                if mission_idx < len(MISSIONS):
                    m = MISSIONS[mission_idx]
                    if wp == MISSION_TARGET_WPS.get(m, ""):
                        correct = rng.random() < acc_base
                        rt = round(rng.uniform(2, 8), 1)
                        t += pd.Timedelta(seconds=rt)
                        rows_evt.append(_evt(t, "VERIFICATION_ANSWERED", wp,
                                             verification_correct=correct,
                                             duration_s=rt,
                                             mission_id=m))
                        rows_evt.append(_evt(t, "MISSION_COMPLETE", wp,
                                             mission_id=m))
                        diff = int(np.clip(round(rng.normal(
                            2.9 if cond == "hybrid" else 3.4, 1)), 1, 7))
                        rows_evt.append(_evt(t, "DIFFICULTY_RATED", wp,
                                             difficulty_rating=diff,
                                             mission_id=m))
                        mission_idx += 1
                        if mission_idx < len(MISSIONS):
                            current_mission = MISSIONS[mission_idx]
                            t += pd.Timedelta(seconds=rng.uniform(3, 8))
                            rows_evt.append(_evt(t, "MISSION_START", wp,
                                                 mission_id=current_mission))

            t += pd.Timedelta(seconds=5)
            rows_evt.append(_evt(t, "ROUTE_END"))
            all_events.extend(rows_evt)

            # ── nav_trace 생성 ──
            # 2Hz 속도 프로파일, Beam ON 시 속도 dip 포함
            nav_t = session_start + pd.Timedelta(seconds=10)
            total_nav_dur = (t - session_start).total_seconds()
            n_nav_samples = int(total_nav_dur * 2)  # 2Hz
            speed_base_val = 1.1 if cond == "glass_only" else 1.2

            # Beam ON 구간 수집 (speed dip 삽입용)
            beam_on_times_local = []
            beam_off_times_local = []
            for evt in rows_evt:
                if evt["event_type"] == "BEAM_SCREEN_ON":
                    beam_on_times_local.append(evt["timestamp"])
                elif evt["event_type"] == "BEAM_SCREEN_OFF":
                    beam_off_times_local.append(evt["timestamp"])

            for si in range(min(n_nav_samples, 3000)):  # 상한
                sample_t = session_start + pd.Timedelta(seconds=si * 0.5)
                if sample_t > t:
                    break

                speed = speed_base_val + rng.normal(0, 0.15)

                # Beam ON 구간에서 속도 dip
                for bi, bon in enumerate(beam_on_times_local):
                    boff = beam_off_times_local[bi] if bi < len(beam_off_times_local) else bon + pd.Timedelta(seconds=10)
                    dt_from_on = (sample_t - bon).total_seconds()
                    dt_to_off = (boff - sample_t).total_seconds()

                    # -5s ~ +20s 범위에서 속도 변화
                    if -5 <= dt_from_on <= 20:
                        if 0 <= dt_from_on <= 3:
                            # 전환 직후: 급격한 감속
                            speed *= 0.3 + 0.1 * dt_from_on
                        elif 3 < dt_from_on <= 10:
                            # 사용 중: 느린 이동
                            speed *= 0.5
                        elif 10 < dt_from_on <= 20:
                            # 복귀: 점진적 가속
                            recovery = (dt_from_on - 10) / 10.0
                            speed *= 0.5 + 0.5 * recovery

                speed = max(0.0, speed)
                beam_on_flag = False
                for bi, bon in enumerate(beam_on_times_local):
                    boff = beam_off_times_local[bi] if bi < len(beam_off_times_local) else bon + pd.Timedelta(seconds=10)
                    if bon <= sample_t <= boff:
                        beam_on_flag = True
                        break

                # 트리거 근처 감속
                progress = si / max(n_nav_samples - 1, 1)
                approx_wp_idx = int(progress * (len(WAYPOINTS) - 1))
                approx_wp = WAYPOINTS[min(approx_wp_idx, len(WAYPOINTS) - 1)]
                trigger_id_nav = trigger_map.get(approx_wp, "")
                if trigger_id_nav and rng.random() < 0.3:
                    speed *= 0.5

                all_nav.append({
                    "timestamp": sample_t,
                    "session_id": session_id,
                    "mission_id": current_mission,
                    "current_wp_index": approx_wp_idx,
                    "current_wp_id": approx_wp,
                    "target_wp_id": WAYPOINTS[min(approx_wp_idx + 1, len(WAYPOINTS) - 1)],
                    "player_x": 36.0 + rng.normal(0, 0.3),
                    "player_y": 0.0,
                    "player_z": 18.0 + progress * 80 + rng.normal(0, 0.3),
                    "target_x": 36.0,
                    "target_y": 0.0,
                    "target_z": 18.0 + (progress + 0.05) * 80,
                    "distance_m": round(rng.uniform(0.5, 10.0), 2),
                    "speed_ms": round(speed, 3),
                    "anchor_bound": True,
                    "is_fallback": False,
                    "has_map_calib": True,
                    "calib_source": "anchor_N",
                    "heading_offset_deg": round(rng.normal(2, 1.5), 1),
                    "arrow_visible": True,
                    "beam_on": beam_on_flag,
                    "trigger_id": trigger_id_nav,
                    "participant_id": participant_id,
                    "condition": cond,
                })

            # ── beam_segments 생성 ──
            if cond == "hybrid":
                for seg_idx, bon in enumerate(beam_on_times_local):
                    boff = beam_off_times_local[seg_idx] if seg_idx < len(beam_off_times_local) else bon + pd.Timedelta(seconds=10)
                    dur = (boff - bon).total_seconds()
                    approx_wp = WAYPOINTS[min(seg_idx, len(WAYPOINTS) - 1)]
                    all_beam.append({
                        "segment_id": seg_idx + 1,
                        "on_ts": bon,
                        "off_ts": boff,
                        "duration_s": round(dur, 1),
                        "mission_id": current_mission,
                        "start_wp": approx_wp,
                        "end_wp": approx_wp,
                        "trigger_active": trigger_map.get(approx_wp, ""),
                        "primary_tab": rng.choice(["map", "info", "poi"]),
                        "poi_view_count": int(rng.integers(0, 4)),
                        "info_card_open_count": int(rng.integers(0, 3)),
                        "comparison_count": int(rng.integers(0, 2)),
                        "mission_ref_count": int(rng.integers(0, 2)),
                        "zoom_count": int(rng.integers(0, 3)),
                        "map_view_duration_s": round(rng.uniform(0, dur * 0.6), 1),
                        "participant_id": participant_id,
                        "condition": cond,
                    })

    events_df = pd.DataFrame(all_events)
    events_df["timestamp"] = pd.to_datetime(events_df["timestamp"])

    nav_df = pd.DataFrame(all_nav)
    nav_df["timestamp"] = pd.to_datetime(nav_df["timestamp"])
    for col in ["speed_ms", "distance_m", "heading_offset_deg",
                "player_x", "player_y", "player_z",
                "target_x", "target_y", "target_z"]:
        if col in nav_df.columns:
            nav_df[col] = pd.to_numeric(nav_df[col], errors="coerce")

    beam_df = pd.DataFrame(all_beam)
    if not beam_df.empty:
        for col in ["on_ts", "off_ts"]:
            if col in beam_df.columns:
                beam_df[col] = pd.to_datetime(beam_df[col])
        for col in ["duration_s"]:
            if col in beam_df.columns:
                beam_df[col] = pd.to_numeric(beam_df[col], errors="coerce")

    print(f"이벤트 {len(events_df)}건, nav_trace {len(nav_df)}건, "
          f"beam_segments {len(beam_df)}건 생성 완료")
    return events_df, nav_df, beam_df


# ──────────────────────────────────────────────
# 실제 데이터 로드
# ──────────────────────────────────────────────

def _load_real_data() -> Tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    """data/raw/ 에서 이벤트, nav_trace, beam_segments CSV 로드."""
    # 이벤트 CSV (sidecar 제외)
    csv_files = sorted(
        f for f in RAW_DIR.glob("P*_*.csv")
        if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
    )
    if not csv_files:
        return pd.DataFrame(), pd.DataFrame(), pd.DataFrame()

    event_frames = []
    for f in csv_files:
        try:
            df = load_event_csv(str(f))
            # 파일명에서 participant_id, condition 보강
            import re
            match = re.match(r"(P\d+)_([a-z_]+)_", f.name)
            if match and "participant_id" not in df.columns:
                df["participant_id"] = match.group(1)
            event_frames.append(df)
        except Exception as e:
            print(f"  [경고] 이벤트 CSV 로드 실패: {f.name} -- {e}")
    events_df = pd.concat(event_frames, ignore_index=True) if event_frames else pd.DataFrame()

    # nav_trace
    nav_files = sorted(RAW_DIR.glob("*_nav_trace.csv"))
    nav_frames = []
    for f in nav_files:
        try:
            df = load_nav_trace(str(f))
            import re
            match = re.match(r"(P\d+)_([a-z_]+)_", f.name)
            if match:
                df["participant_id"] = match.group(1)
                df["condition"] = match.group(2)
            nav_frames.append(df)
        except Exception as e:
            print(f"  [경고] nav_trace 로드 실패: {f.name} -- {e}")
    nav_df = pd.concat(nav_frames, ignore_index=True) if nav_frames else pd.DataFrame()

    # beam_segments
    beam_files = sorted(RAW_DIR.glob("*_beam_segments.csv"))
    beam_frames = []
    for f in beam_files:
        try:
            df = load_beam_segments(str(f))
            import re
            match = re.match(r"(P\d+)_", f.name)
            if match:
                df["participant_id"] = match.group(1)
            beam_frames.append(df)
        except Exception as e:
            print(f"  [경고] beam_segments 로드 실패: {f.name} -- {e}")
    beam_df = pd.concat(beam_frames, ignore_index=True) if beam_frames else pd.DataFrame()

    return events_df, nav_df, beam_df


def load_or_fallback(use_fallback: bool) -> Tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    """데이터 로드 또는 fallback 생성."""
    if not use_fallback:
        events_df, nav_df, beam_df = _load_real_data()
        if not events_df.empty:
            n_pids = events_df["participant_id"].nunique() if "participant_id" in events_df.columns else 0
            print(f"[실제 데이터] 이벤트 {len(events_df)}건, "
                  f"nav_trace {len(nav_df)}건, beam_segments {len(beam_df)}건 "
                  f"({n_pids}명)")
            return events_df, nav_df, beam_df
        print("[경고] data/raw/에 이벤트 CSV 없음. --fallback 플래그 사용 권장.")
        sys.exit(1)

    print("[Fallback 모드] 24명 x 2조건 fallback 데이터를 생성합니다...")
    return _generate_fallback_data()


# ──────────────────────────────────────────────
# 분석 1: 확신도-정확도 데이터 추출
# ──────────────────────────────────────────────

def extract_confidence_accuracy(events_df: pd.DataFrame) -> pd.DataFrame:
    """이벤트 로그에서 확신도 + 정확도 데이터 추출.

    Returns:
        DataFrame: participant_id, condition, waypoint_id, confidence,
                   accuracy, trigger_type
    """
    # 확신도
    conf_events = events_df[events_df["event_type"] == "CONFIDENCE_RATED"].copy()
    conf_events["confidence"] = pd.to_numeric(
        conf_events["confidence_rating"], errors="coerce")
    conf_events = conf_events.dropna(subset=["confidence"])

    # 정확도 (VERIFICATION_ANSWERED)
    ver_events = events_df[events_df["event_type"] == "VERIFICATION_ANSWERED"].copy()
    if "verification_correct" in ver_events.columns:
        ver_events["accuracy"] = ver_events["verification_correct"].map(
            {True: 1, False: 0, "true": 1, "false": 0, "True": 1, "False": 0})
    else:
        ver_events["accuracy"] = np.nan

    # 미션 웨이포인트에서의 정확도를 해당 WP의 확신도와 매칭
    mission_wps = set(MISSION_TARGET_WPS.values())

    rows = []
    for _, conf_row in conf_events.iterrows():
        pid = conf_row["participant_id"]
        cond = conf_row["condition"]
        wp = conf_row["waypoint_id"]
        conf_val = conf_row["confidence"]

        # 해당 WP에서의 정확도 찾기
        matching_ver = ver_events[
            (ver_events["participant_id"] == pid) &
            (ver_events["condition"] == cond) &
            (ver_events["waypoint_id"] == wp)
        ]
        acc_val = matching_ver["accuracy"].iloc[0] if not matching_ver.empty else np.nan

        # 트리거 유형 결정
        trigger_type = ""
        if wp in TRIGGER_WAYPOINTS:
            trig_events = events_df[
                (events_df["participant_id"] == pid) &
                (events_df["condition"] == cond) &
                (events_df["event_type"] == "TRIGGER_ACTIVATED") &
                (events_df["waypoint_id"] == wp)
            ]
            if not trig_events.empty:
                tt = trig_events.iloc[0].get("trigger_type", "")
                if tt and str(tt) not in ("", "nan"):
                    trigger_type = tt

        rows.append({
            "participant_id": pid,
            "condition": cond,
            "waypoint_id": wp,
            "confidence": conf_val,
            "accuracy": acc_val,
            "trigger_type": trigger_type,
        })

    return pd.DataFrame(rows)


# ──────────────────────────────────────────────
# 분석 2: Trust Recovery 데이터
# ──────────────────────────────────────────────

def compute_trust_recovery(events_df: pd.DataFrame) -> pd.DataFrame:
    """트리거 WP 전후 확신도 이벤트 잠금 평균 계산.

    트리거 WP 기준 -2, -1, 0(trigger), +1, +2 상대 위치별
    확신도를 추출.

    Returns:
        DataFrame: participant_id, condition, trigger_wp, relative_pos,
                   confidence, wp_label
    """
    conf_events = events_df[events_df["event_type"] == "CONFIDENCE_RATED"].copy()
    conf_events["confidence"] = pd.to_numeric(
        conf_events["confidence_rating"], errors="coerce")
    conf_events = conf_events.dropna(subset=["confidence"])

    wp_to_idx = {wp: i for i, wp in enumerate(WAYPOINTS)}

    rows = []
    for trigger_wp in TRIGGER_WAYPOINTS:
        tw_idx = wp_to_idx.get(trigger_wp)
        if tw_idx is None:
            continue

        # 상대 위치: -2, -1, 0, +1, +2
        for rel_pos in range(-2, 3):
            abs_idx = tw_idx + rel_pos
            if abs_idx < 0 or abs_idx >= len(WAYPOINTS):
                continue
            target_wp = WAYPOINTS[abs_idx]

            for (pid, cond), grp in conf_events.groupby(
                    ["participant_id", "condition"]):
                wp_data = grp[grp["waypoint_id"] == target_wp]
                if not wp_data.empty:
                    conf_val = wp_data["confidence"].mean()
                    rows.append({
                        "participant_id": pid,
                        "condition": cond,
                        "trigger_wp": trigger_wp,
                        "relative_pos": rel_pos,
                        "confidence": conf_val,
                        "wp_label": target_wp,
                    })

    return pd.DataFrame(rows)


# ──────────────────────────────────────────────
# 분석 3: Switching Cost 데이터
# ──────────────────────────────────────────────

def compute_switching_cost(nav_df: pd.DataFrame,
                           events_df: pd.DataFrame,
                           beam_df: pd.DataFrame) -> pd.DataFrame:
    """Beam Pro ON 이벤트 전후 속도 프로파일 추출.

    -10s ~ +20s 범위에서 1초 간격으로 평균 속도를 시간 잠금 프로파일로
    생성.

    Returns:
        DataFrame: relative_time_s, speed_ms, participant_id, segment_idx
    """
    if nav_df.empty:
        return pd.DataFrame()

    # Beam ON 시점 수집
    if not beam_df.empty and "on_ts" in beam_df.columns:
        beam_on_events = beam_df[["on_ts", "participant_id"]].copy()
        beam_on_events = beam_on_events.rename(columns={"on_ts": "timestamp"})
    else:
        # 이벤트에서 BEAM_SCREEN_ON 추출
        beam_on_events = events_df[
            events_df["event_type"] == "BEAM_SCREEN_ON"
        ][["timestamp", "participant_id"]].copy()

    if beam_on_events.empty:
        return pd.DataFrame()

    beam_on_events["timestamp"] = pd.to_datetime(beam_on_events["timestamp"])

    # speed 컬럼 확인
    speed_col = None
    for col_name in ["speed_ms", "speed_mps"]:
        if col_name in nav_df.columns:
            speed_col = col_name
            break
    if speed_col is None:
        return pd.DataFrame()

    time_window_pre = 10   # seconds before
    time_window_post = 20  # seconds after
    bin_size = 1.0         # 1-second bins

    rows = []
    for seg_idx, (_, beam_evt) in enumerate(beam_on_events.iterrows()):
        pid = beam_evt["participant_id"]
        beam_t = beam_evt["timestamp"]

        # 해당 참가자의 nav_trace 필터링
        pid_nav = nav_df[nav_df["participant_id"] == pid] if "participant_id" in nav_df.columns else nav_df

        # 시간 윈도우
        window_start = beam_t - pd.Timedelta(seconds=time_window_pre)
        window_end = beam_t + pd.Timedelta(seconds=time_window_post)

        window_nav = pid_nav[
            (pid_nav["timestamp"] >= window_start) &
            (pid_nav["timestamp"] <= window_end)
        ].copy()

        if window_nav.empty:
            continue

        window_nav["relative_time_s"] = (
            window_nav["timestamp"] - beam_t
        ).dt.total_seconds()

        # 1초 빈으로 평균
        for bin_start in np.arange(-time_window_pre, time_window_post, bin_size):
            bin_end = bin_start + bin_size
            bin_data = window_nav[
                (window_nav["relative_time_s"] >= bin_start) &
                (window_nav["relative_time_s"] < bin_end)
            ]
            if not bin_data.empty:
                rows.append({
                    "relative_time_s": bin_start + bin_size / 2,
                    "speed_ms": bin_data[speed_col].mean(),
                    "participant_id": pid,
                    "segment_idx": seg_idx,
                })

    return pd.DataFrame(rows)


# ──────────────────────────────────────────────
# 플롯 1: Calibration Curve (Fig 3)
# ──────────────────────────────────────────────

def plot_calibration_curve(ca_df: pd.DataFrame):
    """확신도 vs 실제 정확도 보정 곡선 — 조건별 2곡선."""
    fig, ax = plt.subplots(figsize=FIG_DOUBLE_COL)

    # 미션 WP에서 정확도가 있는 데이터만
    valid = ca_df.dropna(subset=["accuracy"]).copy()
    if valid.empty:
        print("  [경고] Calibration curve: 정확도 데이터 없음")
        plt.close(fig)
        return

    for cond, color, label in zip(CONDITIONS, COLORS_COND, COND_LABELS):
        cond_data = valid[valid["condition"] == cond]
        if cond_data.empty:
            continue
        calibration_curve(
            ax,
            confidence=cond_data["confidence"].values,
            accuracy=cond_data["accuracy"].values,
            color=color,
            label=label,
            n_bins=5,
            show_perfect=(cond == CONDITIONS[0]),  # 대각선은 한 번만
        )

    ax.set_title("Calibration Curve: Confidence vs Accuracy")
    ax.legend(loc="lower right")
    save_fig(fig, OUTPUT_DIR / "fig3_calibration_curve")


# ──────────────────────────────────────────────
# 플롯 2: Trust Recovery (Fig 2)
# ──────────────────────────────────────────────

def plot_trust_recovery(tr_df: pd.DataFrame):
    """트리거 전후 확신도 이벤트 잠금 프로파일 — 트리거 WP별."""
    if tr_df.empty:
        print("  [경고] Trust recovery: 데이터 없음")
        return

    trigger_wps = tr_df["trigger_wp"].unique()
    n_panels = len(trigger_wps)
    fig, axes = plt.subplots(1, n_panels, figsize=(FIG_DOUBLE_COL[0],
                                                    FIG_DOUBLE_COL[1]),
                              sharey=True)
    if n_panels == 1:
        axes = [axes]

    rel_labels = {-2: "-2", -1: "-1", 0: "Trigger", 1: "+1", 2: "+2"}

    for ax, tw in zip(axes, sorted(trigger_wps)):
        tw_data = tr_df[tr_df["trigger_wp"] == tw]

        for cond, color, label in zip(CONDITIONS, COLORS_COND, COND_LABELS):
            cond_data = tw_data[tw_data["condition"] == cond]
            if cond_data.empty:
                continue

            means = cond_data.groupby("relative_pos")["confidence"].mean()
            sds = cond_data.groupby("relative_pos")["confidence"].std()
            ns = cond_data.groupby("relative_pos")["confidence"].count()
            ses = sds / np.sqrt(ns)
            ci = 1.96 * ses

            positions = sorted(means.index)
            mean_vals = [means.get(p, np.nan) for p in positions]
            ci_vals = [ci.get(p, 0) for p in positions]

            ax.plot(positions, mean_vals, "o-", color=color, linewidth=2,
                    markersize=7, label=label, zorder=5)
            ax.fill_between(
                positions,
                [m - c for m, c in zip(mean_vals, ci_vals)],
                [m + c for m, c in zip(mean_vals, ci_vals)],
                color=color, alpha=0.15, zorder=2,
            )

        # 트리거 마커
        ax.axvline(x=0, color=COLOR_TRIGGER, linestyle="--", alpha=0.6, linewidth=1)
        ax.text(0, ax.get_ylim()[1] if ax.get_ylim()[1] > 0 else 7,
                "Trigger", ha="center", va="bottom", fontsize=8,
                color=COLOR_TRIGGER)

        ax.set_xticks(sorted(tr_df["relative_pos"].unique()))
        ax.set_xticklabels([rel_labels.get(p, str(p))
                            for p in sorted(tr_df["relative_pos"].unique())])
        ax.set_xlabel("Relative Waypoint Position")
        ax.set_title(f"Trigger WP: {tw}")
        ax.set_ylim(1, 7)
        ax.legend(fontsize=7)

    axes[0].set_ylabel("Confidence (1-7)")
    fig.suptitle("Trust Recovery: Event-locked Confidence around Triggers",
                 fontsize=11, fontweight="bold", y=1.02)
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "fig2_trust_recovery")


# ──────────────────────────────────────────────
# 플롯 3: Switching Cost (Fig 8)
# ──────────────────────────────────────────────

def plot_switching_cost(sc_df: pd.DataFrame):
    """Beam Pro ON 전후 속도 변화 프로파일."""
    if sc_df.empty:
        print("  [경고] Switching cost: 데이터 없음")
        return

    fig, ax = plt.subplots(figsize=FIG_DOUBLE_COL)

    # 전체 참가자 평균 + CI
    grouped = sc_df.groupby("relative_time_s")["speed_ms"]
    means = grouped.mean()
    sds = grouped.std()
    ns = grouped.count()
    ses = sds / np.sqrt(ns)
    ci = 1.96 * ses

    ax.plot(means.index, means.values, color=COLOR_HYBRID, linewidth=2.5,
            label="Mean Speed", zorder=5)
    ax.fill_between(
        means.index, means - ci, means + ci,
        color=COLOR_HYBRID, alpha=0.2, label="95% CI", zorder=2,
    )

    # 개별 세그먼트 (얇은 선)
    n_segs = sc_df["segment_idx"].nunique()
    max_individual = min(n_segs, 50)  # 너무 많으면 제한
    for seg_idx in sc_df["segment_idx"].unique()[:max_individual]:
        seg_data = sc_df[sc_df["segment_idx"] == seg_idx].sort_values("relative_time_s")
        ax.plot(seg_data["relative_time_s"], seg_data["speed_ms"],
                color=COLOR_HYBRID, alpha=0.06, linewidth=0.5)

    # Beam ON 마커
    ax.axvline(x=0, color=COLOR_TRIGGER, linestyle="--", linewidth=1.5,
               label="Beam ON", zorder=4)
    ax.axvspan(0, 10, color=COLOR_TRIGGER, alpha=0.05, zorder=1)

    # 기준 속도 (사전 평균)
    pre_speed = means[means.index < 0].mean()
    if not np.isnan(pre_speed):
        ax.axhline(y=pre_speed, color="gray", linestyle=":", alpha=0.5,
                   label=f"Pre-switch mean ({pre_speed:.2f} m/s)")

    ax.set_xlabel("Time relative to Beam ON (s)")
    ax.set_ylabel("Walking Speed (m/s)")
    ax.set_title("Switching Cost: Speed Profile around Beam Pro ON")
    ax.set_xlim(-10, 20)
    ax.set_ylim(bottom=0)
    ax.legend(loc="upper right", fontsize=8)
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "fig8_switching_cost")


# ──────────────────────────────────────────────
# 플롯 4: Scatter + Regression (Fig 9)
# ──────────────────────────────────────────────

def plot_confidence_accuracy_scatter(ca_df: pd.DataFrame):
    """참가자별 평균 확신도 vs 평균 정확도 산점도 + 회귀선."""
    valid = ca_df.dropna(subset=["accuracy"]).copy()
    if valid.empty:
        print("  [경고] Scatter plot: 정확도 데이터 없음")
        return

    # 참가자별 평균
    pid_means = valid.groupby(["participant_id", "condition"]).agg(
        mean_confidence=("confidence", "mean"),
        mean_accuracy=("accuracy", "mean"),
    ).reset_index()

    fig, ax = plt.subplots(figsize=FIG_DOUBLE_COL)

    for cond, color, label in zip(CONDITIONS, COLORS_COND, COND_LABELS):
        cond_data = pid_means[pid_means["condition"] == cond]
        if len(cond_data) < 3:
            continue

        x = cond_data["mean_confidence"].values
        y = cond_data["mean_accuracy"].values

        # 산점도
        ax.scatter(x, y, color=color, s=50, alpha=0.7, edgecolors="white",
                   linewidth=0.5, label=label, zorder=4)

        # 회귀선
        slope, intercept, r_val, p_val, std_err = stats.linregress(x, y)
        x_line = np.linspace(x.min() - 0.2, x.max() + 0.2, 100)
        y_line = slope * x_line + intercept
        ax.plot(x_line, y_line, color=color, linewidth=1.5, linestyle="--",
                alpha=0.8, zorder=3)

        # r, p 주석
        sig = significance_marker(p_val)
        ax.annotate(
            f"r = {r_val:.2f}, p = {p_val:.3f} {sig}",
            xy=(0.98 if cond == "hybrid" else 0.02,
                0.98 if cond == "hybrid" else 0.92),
            xycoords="axes fraction",
            ha="right" if cond == "hybrid" else "left",
            va="top",
            fontsize=8,
            color=color,
            fontweight="bold",
            bbox=dict(boxstyle="round,pad=0.3", facecolor="white",
                      edgecolor=color, alpha=0.8),
        )

    ax.set_xlabel("Mean Confidence (1-7)")
    ax.set_ylabel("Mean Accuracy (0-1)")
    ax.set_title("Confidence vs Accuracy: Per-Participant Scatter + Regression")
    ax.set_xlim(1, 7)
    ax.set_ylim(-0.05, 1.05)
    ax.legend(loc="lower right")
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "fig9_confidence_accuracy_scatter")


# ──────────────────────────────────────────────
# 플롯 5: Interaction Plot (Fig 10)
# ──────────────────────────────────────────────

def plot_interaction_condition_trigger(ca_df: pd.DataFrame):
    """조건 x 트리거 유형 교호작용 — 확신도 드롭."""
    # 트리거 WP에서의 확신도만
    trigger_data = ca_df[ca_df["trigger_type"].isin(TRIGGER_TYPES)].copy()
    if trigger_data.empty:
        print("  [경고] Interaction plot: 트리거 데이터 없음")
        return

    fig, ax = plt.subplots(figsize=FIG_DOUBLE_COL)

    cond_colors = {"glass_only": COLOR_GLASS, "hybrid": COLOR_HYBRID}
    cond_labels = {"glass_only": "Glass Only", "hybrid": "Hybrid"}

    interaction_plot(
        ax,
        data=trigger_data,
        x_col="trigger_type",
        y_col="confidence",
        trace_col="condition",
        colors=cond_colors,
        labels=cond_labels,
        ylabel="Confidence (1-7)",
        title="Condition x Trigger Type Interaction: Confidence",
        show_ci=True,
    )

    ax.set_xlabel("Trigger Type")
    ax.set_ylim(1, 7)
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "fig10_interaction_condition_trigger")


# ──────────────────────────────────────────────
# 통계 분석 콘솔 출력
# ──────────────────────────────────────────────

def print_statistics(ca_df: pd.DataFrame, tr_df: pd.DataFrame,
                     sc_df: pd.DataFrame):
    """주요 통계 요약 출력."""
    print("\n" + "=" * 60)
    print("통계 분석 결과 요약")
    print("=" * 60)

    # 1. 확신도 조건 간 비교
    if not ca_df.empty and "confidence" in ca_df.columns:
        conf_pid = ca_df.groupby(["participant_id", "condition"])[
            "confidence"].mean().reset_index()
        result = paired_comparison(conf_pid, "confidence")
        print(f"\n[1] 확신도 조건 간 비교:")
        print(f"    Glass Only: M={result['mean_a']:.2f}, SD={result['sd_a']:.2f}")
        print(f"    Hybrid:     M={result['mean_b']:.2f}, SD={result['sd_b']:.2f}")
        print(f"    {result['test']}: stat={result['statistic']:.3f}, "
              f"p={result['p']:.4f}, d={result['d']:.3f} "
              f"{significance_marker(result['p'])}")

    # 2. 정확도 조건 간 비교
    valid = ca_df.dropna(subset=["accuracy"])
    if not valid.empty:
        acc_pid = valid.groupby(["participant_id", "condition"])[
            "accuracy"].mean().reset_index()
        result = paired_comparison(acc_pid, "accuracy")
        print(f"\n[2] 정확도 조건 간 비교:")
        print(f"    Glass Only: M={result['mean_a']:.2f}, SD={result['sd_a']:.2f}")
        print(f"    Hybrid:     M={result['mean_b']:.2f}, SD={result['sd_b']:.2f}")
        print(f"    {result['test']}: stat={result['statistic']:.3f}, "
              f"p={result['p']:.4f}, d={result['d']:.3f} "
              f"{significance_marker(result['p'])}")

    # 3. Trust Recovery: 트리거 전후 확신도 변화
    if not tr_df.empty:
        print(f"\n[3] Trust Recovery (트리거 전후 확신도 변화):")
        for tw in sorted(tr_df["trigger_wp"].unique()):
            tw_data = tr_df[tr_df["trigger_wp"] == tw]
            for cond, label in zip(CONDITIONS, COND_LABELS):
                cond_data = tw_data[tw_data["condition"] == cond]
                pre = cond_data[cond_data["relative_pos"] == -1]["confidence"].mean()
                at_trigger = cond_data[cond_data["relative_pos"] == 0]["confidence"].mean()
                post = cond_data[cond_data["relative_pos"] == 1]["confidence"].mean()
                if not np.isnan(pre) and not np.isnan(at_trigger):
                    drop = at_trigger - pre
                    recovery = post - at_trigger if not np.isnan(post) else np.nan
                    print(f"    {tw} {label}: 직전 {pre:.2f} -> 트리거 {at_trigger:.2f} "
                          f"(drop={drop:+.2f}) -> 직후 {post:.2f} "
                          f"(recovery={recovery:+.2f})")

    # 4. Switching Cost
    if not sc_df.empty:
        pre_speed = sc_df[sc_df["relative_time_s"] < 0]["speed_ms"].mean()
        during_speed = sc_df[
            (sc_df["relative_time_s"] >= 0) &
            (sc_df["relative_time_s"] < 5)
        ]["speed_ms"].mean()
        post_speed = sc_df[
            (sc_df["relative_time_s"] >= 10) &
            (sc_df["relative_time_s"] <= 20)
        ]["speed_ms"].mean()
        print(f"\n[4] Switching Cost:")
        print(f"    사전 평균 속도:      {pre_speed:.3f} m/s")
        print(f"    전환 직후 (0-5s):   {during_speed:.3f} m/s "
              f"(변화율: {(during_speed - pre_speed) / max(pre_speed, 0.01) * 100:+.1f}%)")
        print(f"    복귀 후 (10-20s):   {post_speed:.3f} m/s "
              f"(변화율: {(post_speed - pre_speed) / max(pre_speed, 0.01) * 100:+.1f}%)")

    # 5. 트리거 유형별 확신도
    trigger_data = ca_df[ca_df["trigger_type"].isin(TRIGGER_TYPES)]
    if not trigger_data.empty:
        print(f"\n[5] 트리거 유형별 확신도:")
        for tt in TRIGGER_TYPES:
            tt_data = trigger_data[trigger_data["trigger_type"] == tt]
            if tt_data.empty:
                continue
            for cond, label in zip(CONDITIONS, COND_LABELS):
                vals = tt_data[tt_data["condition"] == cond]["confidence"]
                if not vals.empty:
                    print(f"    {tt} {label}: M={vals.mean():.2f}, SD={vals.std():.2f}, n={len(vals)}")


# ──────────────────────────────────────────────
# CSV 결과 저장
# ──────────────────────────────────────────────

def save_results(ca_df: pd.DataFrame, tr_df: pd.DataFrame,
                 sc_df: pd.DataFrame):
    """분석 결과를 CSV로 저장."""
    csv_dir = OUTPUT_DIR / "csv"
    if not ca_df.empty:
        ca_df.to_csv(csv_dir / "rq_deep_confidence_accuracy.csv", index=False)
        print(f"  -> {csv_dir / 'rq_deep_confidence_accuracy.csv'} 저장")

    if not tr_df.empty:
        tr_df.to_csv(csv_dir / "rq_deep_trust_recovery.csv", index=False)
        print(f"  -> {csv_dir / 'rq_deep_trust_recovery.csv'} 저장")

    if not sc_df.empty:
        sc_df.to_csv(csv_dir / "rq_deep_switching_cost.csv", index=False)
        print(f"  -> {csv_dir / 'rq_deep_switching_cost.csv'} 저장")

    # 참가자별 요약 통계
    if not ca_df.empty:
        valid = ca_df.dropna(subset=["accuracy"])
        if not valid.empty:
            summary = valid.groupby(["participant_id", "condition"]).agg(
                mean_confidence=("confidence", "mean"),
                mean_accuracy=("accuracy", "mean"),
                n_missions=("accuracy", "count"),
            ).reset_index()
            summary.to_csv(csv_dir / "rq_deep_participant_summary.csv",
                           index=False)
            print(f"  -> {csv_dir / 'rq_deep_participant_summary.csv'} 저장")


# ──────────────────────────────────────────────
# 메인
# ──────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description="Deep RQ 분석 -- CHI/ISMAR 논문용 고급 시각화")
    parser.add_argument("--fallback", action="store_true",
                        help="데이터 파일이 없을 때 24명 fallback 데이터로 실행")
    args = parser.parse_args()

    apply_style()
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUTPUT_DIR / "csv").mkdir(exist_ok=True)

    print("=" * 60)
    print("Deep RQ 분석 (5종 시각화)")
    print("=" * 60)

    # 데이터 로드/생성
    events_df, nav_df, beam_df = load_or_fallback(use_fallback=args.fallback)

    n_pids = events_df["participant_id"].nunique() if "participant_id" in events_df.columns else 0
    n_conds = events_df["condition"].nunique() if "condition" in events_df.columns else 0
    print(f"\n참가자 수: {n_pids}, 조건 수: {n_conds}")
    print(f"이벤트: {len(events_df)}건, nav_trace: {len(nav_df)}건, "
          f"beam_segments: {len(beam_df)}건")

    # 데이터 추출
    print("\n--- 데이터 추출 ---")
    ca_df = extract_confidence_accuracy(events_df)
    print(f"확신도-정확도 데이터: {len(ca_df)}건")

    tr_df = compute_trust_recovery(events_df)
    print(f"Trust recovery 데이터: {len(tr_df)}건")

    sc_df = compute_switching_cost(nav_df, events_df, beam_df)
    print(f"Switching cost 데이터: {len(sc_df)}건")

    # 통계 분석
    print_statistics(ca_df, tr_df, sc_df)

    # 시각화
    print("\n--- 시각화 ---")
    plot_calibration_curve(ca_df)
    plot_trust_recovery(tr_df)
    plot_switching_cost(sc_df)
    plot_confidence_accuracy_scatter(ca_df)
    plot_interaction_condition_trigger(ca_df)

    # 결과 저장
    print("\n--- 결과 저장 ---")
    save_results(ca_df, tr_df, sc_df)

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
