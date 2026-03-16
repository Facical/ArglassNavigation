"""
ISMAR 로깅 시스템 데이터 유틸리티 모듈.

새 ISMAR CSV 파일 로더:
  - nav_trace (2Hz 위치/내비 상태)
  - head_pose (10Hz 머리 회전)
  - event (24컬럼 이벤트 로그)
  - anchor_reloc (앵커 재인식)
  - beam_segments (Beam Pro 사용 세그먼트)
  - system_health (1Hz 시스템 상태)

경로 분석 유틸리티:
  - compute_path_length, compute_path_deviation, compute_head_scanning_amplitude

Fallback 데이터 생성:
  - generate_fallback_data (data/raw/ 비어 있을 때 자동 생성)
"""

import json
import os
import re
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import numpy as np
import pandas as pd

# ──────────────────────────────────────────────
# 상수
# ──────────────────────────────────────────────

CONDITIONS = ["glass_only", "hybrid"]
N_PARTICIPANTS = 24
N_WAYPOINTS = 9  # WP00~WP08

# 웨이포인트 fallback 좌표 (WaypointDataGenerator 기준)
WAYPOINT_POSITIONS = {
    "WP00": (36.0, 0.0, 24.0),
    "WP01": (36.0, 0.0, 18.0),
    "WP02": (36.0, 0.0, 33.0),
    "WP03": (36.0, 0.0, 45.0),
    "WP04": (36.0, 0.0, 57.0),
    "WP05": (36.0, 0.0, 66.0),
    "WP06": (39.0, 0.0, 72.0),
    "WP07": (36.0, 0.0, 48.0),
    "WP08": (36.0, 0.0, -7.0),
}

# nav_trace CSV 헤더 (NavigationTraceLogger.cs 기준)
NAV_TRACE_HEADERS = [
    "timestamp", "session_id", "mission_id",
    "current_wp_index", "current_wp_id", "target_wp_id",
    "player_x", "player_y", "player_z",
    "target_x", "target_y", "target_z",
    "distance_m", "speed_ms",
    "anchor_bound", "is_fallback", "has_map_calib", "calib_source",
    "heading_offset_deg", "arrow_visible", "beam_on", "trigger_id",
]

# head_pose CSV 헤더 (HeadPoseTraceLogger.cs 기준)
HEAD_POSE_HEADERS = [
    "timestamp", "session_id", "participant_id", "condition",
    "mission_id", "waypoint_id",
    "yaw", "pitch", "roll", "angular_velocity_yaw",
    "beam_on", "arrow_visible", "trigger_id",
]

# event CSV 헤더 (EventLogger.cs 기준, 24컬럼)
EVENT_HEADERS = [
    "timestamp", "participant_id", "condition", "event_type",
    "waypoint_id", "head_rotation_x", "head_rotation_y", "head_rotation_z",
    "device_active", "confidence_rating", "mission_id",
    "difficulty_rating", "verification_correct", "beam_content_type",
    "session_id", "mission_set", "trigger_id", "trigger_type",
    "cause", "duration_s", "distance_m", "anchor_bound", "arrow_visible",
    "extra_data",
]

# anchor_reloc CSV 헤더 (AnchorRelocLogger.cs 기준)
ANCHOR_RELOC_HEADERS = [
    "timestamp", "session_id", "waypoint_id", "anchor_guid",
    "is_critical", "state", "elapsed_s", "slam_reason",
    "remap_attempt", "file_exists", "in_sdk",
]

# beam_segments CSV 헤더 (BeamSegmentLogger.cs 기준)
BEAM_SEGMENT_HEADERS = [
    "segment_id", "on_ts", "off_ts", "duration_s",
    "mission_id", "start_wp", "end_wp", "trigger_active",
    "primary_tab", "poi_view_count", "info_card_open_count",
    "comparison_count", "mission_ref_count", "zoom_count",
    "map_view_duration_s",
]

# system_health CSV 헤더 (SystemHealthLogger.cs 기준)
SYSTEM_HEALTH_HEADERS = [
    "timestamp", "fps", "frame_time_ms", "battery_level",
    "battery_charging", "thermal_status", "memory_mb",
    "tracking_state", "slam_reason",
]

# 파일 접미사 → 로더 매핑
FILE_SUFFIXES = {
    "_nav_trace.csv": "nav_trace",
    "_head_pose.csv": "head_pose",
    "_anchor_reloc.csv": "anchor_reloc",
    "_beam_segments.csv": "beam_segments",
    "_system_health.csv": "system_health",
    "_meta.json": "meta",
    "_summary.json": "summary",
}


# ──────────────────────────────────────────────
# 데이터 로더
# ──────────────────────────────────────────────

def load_nav_trace(filepath: str) -> pd.DataFrame:
    """nav_trace CSV 로드 → DataFrame.

    Returns:
        DataFrame with columns: timestamp, session_id, mission_id,
        current_wp_index, current_wp_id, target_wp_id,
        player_x/y/z, target_x/y/z, distance_m, speed_ms,
        anchor_bound, is_fallback, has_map_calib, calib_source,
        heading_offset_deg, arrow_visible, beam_on, trigger_id
    """
    df = pd.read_csv(filepath)
    df["timestamp"] = pd.to_datetime(df["timestamp"])
    for col in ["player_x", "player_y", "player_z",
                "target_x", "target_y", "target_z",
                "distance_m", "speed_ms", "heading_offset_deg"]:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")
    for col in ["current_wp_index"]:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce").astype("Int64")
    for col in ["anchor_bound", "is_fallback", "has_map_calib", "arrow_visible", "beam_on"]:
        if col in df.columns:
            df[col] = df[col].astype(str).str.lower().map({"true": True, "false": False})
    return df


def load_head_pose(filepath: str) -> pd.DataFrame:
    """head_pose CSV 로드 → DataFrame.

    Returns:
        DataFrame with columns: timestamp, session_id, participant_id,
        condition, mission_id, waypoint_id, yaw, pitch, roll,
        angular_velocity_yaw, beam_on, arrow_visible, trigger_id
    """
    df = pd.read_csv(filepath)
    df["timestamp"] = pd.to_datetime(df["timestamp"])
    for col in ["yaw", "pitch", "roll", "angular_velocity_yaw"]:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")
    for col in ["beam_on", "arrow_visible"]:
        if col in df.columns:
            df[col] = df[col].astype(str).str.lower().map({"true": True, "false": False})
    return df


def load_event_csv(filepath: str) -> pd.DataFrame:
    """24컬럼 이벤트 CSV 로드 → DataFrame."""
    df = pd.read_csv(filepath)
    if "timestamp" in df.columns:
        df["timestamp"] = pd.to_datetime(df["timestamp"])
    for col in ["confidence_rating", "difficulty_rating",
                "duration_s", "distance_m"]:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")
    for col in ["verification_correct", "anchor_bound", "arrow_visible"]:
        if col in df.columns:
            df[col] = df[col].astype(str).str.lower().map(
                {"true": True, "false": False, "nan": None, "": None})
    return df


def load_anchor_reloc(filepath: str) -> pd.DataFrame:
    """anchor_reloc CSV 로드 → DataFrame."""
    df = pd.read_csv(filepath)
    df["timestamp"] = pd.to_datetime(df["timestamp"])
    for col in ["elapsed_s", "remap_attempt"]:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")
    for col in ["is_critical", "file_exists", "in_sdk"]:
        if col in df.columns:
            df[col] = df[col].astype(str).str.lower().map(
                {"true": True, "false": False, "": None})
    return df


def load_beam_segments(filepath: str) -> pd.DataFrame:
    """beam_segments CSV 로드 → DataFrame."""
    df = pd.read_csv(filepath)
    for col in ["on_ts", "off_ts"]:
        if col in df.columns:
            df[col] = pd.to_datetime(df[col])
    for col in ["duration_s", "poi_view_count", "info_card_open_count",
                "comparison_count", "mission_ref_count", "zoom_count",
                "map_view_duration_s"]:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")
    return df


def load_system_health(filepath: str) -> pd.DataFrame:
    """system_health CSV 로드 → DataFrame."""
    df = pd.read_csv(filepath)
    df["timestamp"] = pd.to_datetime(df["timestamp"])
    for col in ["fps", "frame_time_ms", "battery_level", "memory_mb"]:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")
    return df


# ──────────────────────────────────────────────
# 파일 탐색
# ──────────────────────────────────────────────

def find_session_files(data_dir: str, prefix: str = None
                       ) -> Dict[str, Dict[str, str]]:
    """세션 파일 그룹핑.

    Args:
        data_dir: data/raw/ 디렉토리 경로
        prefix: 특정 세션 prefix 필터 (None이면 전체)

    Returns:
        {session_prefix: {type: filepath}} 딕셔너리.
        type은 "event", "nav_trace", "head_pose", "anchor_reloc",
              "beam_segments", "system_health", "meta", "summary"
    """
    data_path = Path(data_dir)
    if not data_path.exists():
        return {}

    sessions: Dict[str, Dict[str, str]] = {}

    for f in sorted(data_path.iterdir()):
        if not f.is_file():
            continue
        name = f.name

        # sidecar 파일 매칭
        matched = False
        for suffix, ftype in FILE_SUFFIXES.items():
            if name.endswith(suffix):
                sess_prefix = name[: -len(suffix)]
                if prefix and not sess_prefix.startswith(prefix):
                    continue
                sessions.setdefault(sess_prefix, {})[ftype] = str(f)
                matched = True
                break

        # 메인 이벤트 CSV (접미사 없는 .csv)
        if not matched and name.endswith(".csv"):
            # P01_glass_only_Set1_20260315_100000.csv 패턴
            sess_prefix = name[:-4]  # .csv 제거
            if prefix and not sess_prefix.startswith(prefix):
                continue
            # sidecar가 아닌 CSV만 event로 분류
            is_sidecar = any(name.endswith(sfx) for sfx in FILE_SUFFIXES)
            if not is_sidecar:
                sessions.setdefault(sess_prefix, {})["event"] = str(f)

    return sessions


# ──────────────────────────────────────────────
# 경로 분석 유틸리티
# ──────────────────────────────────────────────

def compute_path_length(nav_df: pd.DataFrame) -> float:
    """nav_trace DataFrame에서 총 경로 길이(m) 산출.

    (player_x, player_z)의 연속 포인트 간 유클리드 거리 합산.
    """
    if nav_df.empty or len(nav_df) < 2:
        return 0.0
    dx = nav_df["player_x"].diff()
    dz = nav_df["player_z"].diff()
    distances = np.sqrt(dx ** 2 + dz ** 2)
    # 텔레포트/리셋 등의 비정상적 큰 점프 제거 (50m 이상)
    distances = distances[distances < 50.0]
    return float(distances.sum())


def compute_path_deviation(nav_df: pd.DataFrame,
                           waypoints_df: pd.DataFrame = None) -> float:
    """이상 경로 대비 평균 횡 편차(m) 산출.

    이상 경로: waypoint 간 직선 연결.
    nav_df의 각 포인트에서 현재→다음 waypoint 직선까지의 수직 거리 평균.
    waypoints_df가 None이면 WAYPOINT_POSITIONS 사용.
    """
    if nav_df.empty:
        return 0.0

    # 이상 경로 세그먼트 구성
    wp_order = ["WP00", "WP01", "WP02", "WP03", "WP04",
                "WP05", "WP06", "WP07", "WP08"]

    if waypoints_df is not None and not waypoints_df.empty:
        wp_coords = {}
        for _, row in waypoints_df.iterrows():
            wp_coords[row["waypoint_id"]] = (row["x"], row["z"])
    else:
        wp_coords = {k: (v[0], v[2]) for k, v in WAYPOINT_POSITIONS.items()}

    deviations = []
    for _, row in nav_df.iterrows():
        px, pz = row["player_x"], row["player_z"]
        wp_id = row.get("current_wp_id", "")
        target_id = row.get("target_wp_id", "")

        if wp_id in wp_coords and target_id in wp_coords:
            ax, az = wp_coords[wp_id]
            bx, bz = wp_coords[target_id]
        elif target_id in wp_coords:
            # 현재 WP가 없으면 target까지의 직선 거리만
            bx, bz = wp_coords[target_id]
            deviations.append(0.0)
            continue
        else:
            continue

        # 점 P에서 직선 AB까지의 수직 거리
        ab_len = np.sqrt((bx - ax) ** 2 + (bz - az) ** 2)
        if ab_len < 0.01:
            dev = np.sqrt((px - ax) ** 2 + (pz - az) ** 2)
        else:
            dev = abs((bz - az) * px - (bx - ax) * pz + bx * az - bz * ax) / ab_len
        deviations.append(dev)

    return float(np.mean(deviations)) if deviations else 0.0


def compute_head_scanning_amplitude(head_df: pd.DataFrame,
                                    window_s: float = 2.0) -> pd.Series:
    """롤링 윈도우 내 yaw 범위를 스캐닝 진폭으로 산출.

    Args:
        head_df: head_pose DataFrame (timestamp, yaw 필수)
        window_s: 롤링 윈도우 크기 (초)

    Returns:
        yaw_range Series (같은 인덱스)
    """
    if head_df.empty:
        return pd.Series(dtype=float)

    df = head_df.copy()
    df = df.sort_values("timestamp")
    df = df.set_index("timestamp")

    window = f"{window_s}s"
    yaw_max = df["yaw"].rolling(window, min_periods=1).max()
    yaw_min = df["yaw"].rolling(window, min_periods=1).min()
    amplitude = yaw_max - yaw_min

    # 360도 경계 처리 (yaw가 -180~180 범위)
    amplitude = amplitude.clip(upper=180.0)

    return amplitude.reset_index(drop=True)


# ──────────────────────────────────────────────
# Fallback 데이터 생성
# ──────────────────────────────────────────────

def generate_fallback_data(output_dir: str) -> Dict[str, Dict[str, str]]:
    """분석 파이프라인 테스트용 CSV/JSON 파일 생성.

    5명 참가자 x 2조건 = 10세션.
    각 세션: event CSV, nav_trace CSV, head_pose CSV,
            anchor_reloc CSV, beam_segments CSV, system_health CSV,
            meta.json, summary.json

    Args:
        output_dir: 출력 디렉토리 (data/raw/)

    Returns:
        find_session_files와 동일한 형식의 세션 딕셔너리
    """
    out_path = Path(output_dir)
    out_path.mkdir(parents=True, exist_ok=True)

    rng = np.random.default_rng(2026)
    sessions = {}
    base_time = pd.Timestamp("2026-03-15T10:00:00")

    missions = ["A1", "B1", "A2", "B2", "C1"]
    mission_target_wps = {"A1": "WP02", "B1": "WP03", "A2": "WP05",
                          "B2": "WP06", "C1": "WP07"}
    trigger_at_wp = {"WP03": "T2", "WP06": "T3"}

    wp_order = ["WP00", "WP01", "WP02", "WP03", "WP04",
                "WP05", "WP06", "WP07", "WP08"]

    for pid in range(1, N_PARTICIPANTS + 1):
        participant_id = f"P{pid:02d}"

        for cond in CONDITIONS:
            mission_set = "Set1" if pid % 2 == 1 else "Set2"
            ts_offset = (pid - 1) * 3600 + (0 if cond == "glass_only" else 1800)
            session_start = base_time + pd.Timedelta(seconds=ts_offset)
            ts_str = session_start.strftime("%Y%m%d_%H%M%S")
            prefix = f"{participant_id}_{cond}_{mission_set}_{ts_str}"
            session_id = f"{participant_id}_{ts_str}"

            file_paths = {}

            # ── 1. nav_trace ──
            nav_rows = _generate_nav_trace(
                rng, session_id, session_start, cond, missions,
                mission_target_wps, trigger_at_wp, wp_order)
            nav_df = pd.DataFrame(nav_rows, columns=NAV_TRACE_HEADERS)
            nav_path = out_path / f"{prefix}_nav_trace.csv"
            nav_df.to_csv(nav_path, index=False)
            file_paths["nav_trace"] = str(nav_path)

            # ── 2. head_pose ──
            head_rows = _generate_head_pose(
                rng, session_id, participant_id, cond,
                session_start, missions, wp_order, trigger_at_wp)
            head_df = pd.DataFrame(head_rows, columns=HEAD_POSE_HEADERS)
            head_path = out_path / f"{prefix}_head_pose.csv"
            head_df.to_csv(head_path, index=False)
            file_paths["head_pose"] = str(head_path)

            # ── 3. event CSV (24컬럼) ──
            event_rows = _generate_event_csv(
                rng, participant_id, cond, session_id, mission_set,
                session_start, missions, mission_target_wps,
                trigger_at_wp, wp_order)
            event_df = pd.DataFrame(event_rows, columns=EVENT_HEADERS)
            event_path = out_path / f"{prefix}.csv"
            event_df.to_csv(event_path, index=False)
            file_paths["event"] = str(event_path)

            # ── 4. anchor_reloc ──
            reloc_rows = _generate_anchor_reloc(
                rng, session_id, session_start, wp_order)
            reloc_df = pd.DataFrame(reloc_rows, columns=ANCHOR_RELOC_HEADERS)
            reloc_path = out_path / f"{prefix}_anchor_reloc.csv"
            reloc_df.to_csv(reloc_path, index=False)
            file_paths["anchor_reloc"] = str(reloc_path)

            # ── 5. beam_segments ──
            beam_rows = _generate_beam_segments(
                rng, session_start, cond, missions, wp_order, trigger_at_wp)
            beam_df = pd.DataFrame(beam_rows, columns=BEAM_SEGMENT_HEADERS)
            beam_path = out_path / f"{prefix}_beam_segments.csv"
            beam_df.to_csv(beam_path, index=False)
            file_paths["beam_segments"] = str(beam_path)

            # ── 6. system_health ──
            health_rows = _generate_system_health(rng, session_start)
            health_df = pd.DataFrame(health_rows, columns=SYSTEM_HEALTH_HEADERS)
            health_path = out_path / f"{prefix}_system_health.csv"
            health_df.to_csv(health_path, index=False)
            file_paths["system_health"] = str(health_path)

            # ── 7. meta.json ──
            meta = {
                "participant_id": participant_id,
                "condition": cond,
                "mission_set": mission_set,
                "device_model": "XREAL Beam Pro",
                "android_version": "14",
                "app_version": "1.0.0",
                "xreal_sdk_version": "3.1.0",
                "build_time": "1.0.0",
                "route_version": "RouteB",
                "mapping_version": "2026-03-14",
                "start_time": session_start.isoformat(),
            }
            meta_path = out_path / f"{prefix}_meta.json"
            with open(meta_path, "w") as f:
                json.dump(meta, f, indent=2)
            file_paths["meta"] = str(meta_path)

            # ── 8. summary.json ──
            total_dur = rng.uniform(600, 900)
            summary = {
                "end_time": (session_start + pd.Timedelta(seconds=total_dur)).isoformat(),
                "total_duration_s": round(total_dur, 1),
                "total_waypoints": 9,
                "fallback_count": int(rng.integers(0, 4)),
                "beam_total_time_s": round(rng.uniform(30, 180), 1) if cond == "hybrid" else 0.0,
                "beam_segment_count": int(rng.integers(3, 12)) if cond == "hybrid" else 0,
                "relocalization_success_rate": round(rng.uniform(0.6, 1.0), 2),
                "forced_events_count": int(rng.integers(0, 3)),
                "pause_count": int(rng.integers(2, 8)),
                "total_pause_duration_s": round(rng.uniform(5, 40), 1),
                "mission_count": 5,
            }
            summary_path = out_path / f"{prefix}_summary.json"
            with open(summary_path, "w") as f:
                json.dump(summary, f, indent=2)
            file_paths["summary"] = str(summary_path)

            sessions[prefix] = file_paths

    print(f"[trajectory_utils] Fallback 데이터 생성 완료: {len(sessions)}개 세션 → {output_dir}")
    return sessions


# ──────────────────────────────────────────────
# 내부 fallback 데이터 생성 헬퍼
# ──────────────────────────────────────────────

def _lerp_position(wp_a: str, wp_b: str, t: float) -> Tuple[float, float, float]:
    """두 웨이포인트 사이 선형 보간 위치."""
    ax, ay, az = WAYPOINT_POSITIONS.get(wp_a, (0, 0, 0))
    bx, by, bz = WAYPOINT_POSITIONS.get(wp_b, (0, 0, 0))
    return (
        ax + (bx - ax) * t,
        ay + (by - ay) * t,
        az + (bz - az) * t,
    )


def _generate_nav_trace(rng, session_id, start_time, condition, missions,
                         mission_target_wps, trigger_at_wp, wp_order):
    """nav_trace 행 생성."""
    rows = []
    t = start_time
    sample_dt = pd.Timedelta(milliseconds=500)  # 2Hz
    mission_idx = 0

    for wp_idx in range(len(wp_order) - 1):
        wp_id = wp_order[wp_idx]
        next_wp = wp_order[wp_idx + 1]
        segment_duration = rng.uniform(30, 80)  # 세그먼트 소요 시간
        n_samples = int(segment_duration / 0.5)

        # 조건별 속도 특성
        speed_base = rng.uniform(0.8, 1.4) if condition == "glass_only" else rng.uniform(0.9, 1.5)

        for i in range(n_samples):
            frac = i / max(n_samples - 1, 1)
            px, py, pz = _lerp_position(wp_id, next_wp, frac)
            # 노이즈 추가
            px += rng.normal(0, 0.3)
            pz += rng.normal(0, 0.3)

            tx, ty, tz = WAYPOINT_POSITIONS.get(next_wp, (0, 0, 0))
            dist = np.sqrt((px - tx) ** 2 + (pz - tz) ** 2)

            # 속도: 트리거 근처에서 감속
            speed = speed_base
            if next_wp in trigger_at_wp and frac > 0.7:
                speed *= rng.uniform(0.2, 0.6)  # 정체
            elif frac > 0.9:
                speed *= rng.uniform(0.3, 0.7)  # 도착 감속

            speed += rng.normal(0, 0.1)
            speed = max(0.0, speed)

            mission_id = missions[min(mission_idx, len(missions) - 1)]
            target_wp_id = mission_target_wps.get(mission_id, next_wp)
            anchor_bound = rng.random() < 0.55
            is_fallback = not anchor_bound
            has_calib = True
            calib_source = "anchor_N" if anchor_bound else "auto_zero_anchor"
            heading_offset = round(rng.normal(2.0, 1.5), 1)
            arrow_visible = frac < 0.95
            beam_on = condition == "hybrid" and rng.random() < 0.15
            trigger_id = trigger_at_wp.get(next_wp, "") if frac > 0.7 else ""

            rows.append([
                (t + pd.Timedelta(seconds=i * 0.5)).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3],
                session_id, mission_id,
                str(wp_idx), wp_id, target_wp_id,
                f"{px:.2f}", f"{py:.2f}", f"{pz:.2f}",
                f"{tx:.2f}", f"{ty:.2f}", f"{tz:.2f}",
                f"{dist:.2f}", f"{speed:.2f}",
                str(anchor_bound).lower(), str(is_fallback).lower(),
                str(has_calib).lower(), calib_source,
                str(heading_offset), str(arrow_visible).lower(),
                str(beam_on).lower(), trigger_id,
            ])

        t += pd.Timedelta(seconds=segment_duration)

        # 미션 전진
        if mission_idx < len(missions):
            m = missions[mission_idx]
            if next_wp == mission_target_wps.get(m, ""):
                mission_idx += 1

    return rows


def _generate_head_pose(rng, session_id, participant_id, condition,
                         start_time, missions, wp_order, trigger_at_wp):
    """head_pose 행 생성."""
    rows = []
    t = start_time
    sample_dt = 0.1  # 10Hz
    total_duration = rng.uniform(600, 850)
    n_samples = int(total_duration / sample_dt)

    mission_idx = 0
    wp_idx = 0
    yaw = rng.uniform(-30, 30)

    for i in range(n_samples):
        ts = t + pd.Timedelta(seconds=i * sample_dt)

        # yaw 변화: 트리거 근처에서 더 활발한 스캐닝
        progress = i / n_samples
        wp_idx_approx = int(progress * (len(wp_order) - 1))
        wp_idx_approx = min(wp_idx_approx, len(wp_order) - 1)
        current_wp = wp_order[wp_idx_approx]

        if current_wp in trigger_at_wp:
            yaw_change = rng.normal(0, 8)  # 활발한 스캐닝
        else:
            yaw_change = rng.normal(0, 3)

        yaw += yaw_change
        yaw = np.clip(yaw, -180, 180)
        pitch = rng.normal(-5, 3)
        roll = rng.normal(0, 1)
        ang_vel = abs(yaw_change / sample_dt)

        mission_id = missions[min(mission_idx, len(missions) - 1)]
        beam_on = condition == "hybrid" and rng.random() < 0.12
        arrow_visible = rng.random() < 0.8
        trigger_id = trigger_at_wp.get(current_wp, "") if rng.random() < 0.3 else ""

        rows.append([
            ts.strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3],
            session_id, participant_id, condition,
            mission_id, current_wp,
            f"{yaw:.1f}", f"{pitch:.1f}", f"{roll:.1f}",
            f"{ang_vel:.1f}",
            str(beam_on).lower(), str(arrow_visible).lower(), trigger_id,
        ])

        # 매 ~1000 샘플마다 미션 전진
        if i > 0 and i % (n_samples // 5) == 0 and mission_idx < len(missions) - 1:
            mission_idx += 1

    return rows


def _generate_event_csv(rng, participant_id, condition, session_id,
                         mission_set, start_time, missions,
                         mission_target_wps, trigger_at_wp, wp_order):
    """24컬럼 이벤트 CSV 행 생성."""
    rows = []
    t = start_time

    def _evt(ts, etype, wp="", **kw):
        rot_x = round(rng.normal(0, 5), 1)
        rot_y = round(rng.uniform(-180, 180), 1)
        rot_z = round(rng.normal(0, 2), 1)
        device = "glass" if condition == "glass_only" else "both"
        extra = {k: v for k, v in kw.items()
                 if k not in ("confidence_rating", "difficulty_rating",
                              "verification_correct", "beam_content_type",
                              "trigger_id", "trigger_type", "cause",
                              "duration_s", "distance_m", "anchor_bound",
                              "arrow_visible", "mission_id_override")}
        mid = kw.get("mission_id_override", "")
        return [
            ts.strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3],
            participant_id, condition, etype, wp,
            str(rot_x), str(rot_y), str(rot_z),
            device,
            str(kw.get("confidence_rating", "")),
            mid,
            str(kw.get("difficulty_rating", "")),
            str(kw.get("verification_correct", "")).lower() if kw.get("verification_correct") is not None else "",
            kw.get("beam_content_type", ""),
            session_id, mission_set,
            kw.get("trigger_id", ""), kw.get("trigger_type", ""),
            kw.get("cause", ""),
            str(kw.get("duration_s", "")) if kw.get("duration_s") is not None else "",
            str(kw.get("distance_m", "")) if kw.get("distance_m") is not None else "",
            str(kw.get("anchor_bound", "")).lower() if kw.get("anchor_bound") is not None else "",
            str(kw.get("arrow_visible", "")).lower() if kw.get("arrow_visible") is not None else "",
            json.dumps(extra) if extra else "{}",
        ]

    # SESSION_INITIALIZED
    rows.append(_evt(t, "SESSION_INITIALIZED"))
    t += pd.Timedelta(seconds=5)

    # RELOCALIZATION
    rows.append(_evt(t, "RELOCALIZATION_STARTED"))
    t += pd.Timedelta(seconds=rng.uniform(15, 40))
    rows.append(_evt(t, "RELOCALIZATION_COMPLETED",
                     duration_s=round(rng.uniform(15, 40), 1)))
    t += pd.Timedelta(seconds=3)

    # ROUTE_START
    rows.append(_evt(t, "ROUTE_START"))
    t += pd.Timedelta(seconds=2)

    mission_idx = 0
    current_mission = missions[mission_idx]
    rows.append(_evt(t, "MISSION_START", wp_order[1],
                     mission_id_override=current_mission))

    for wp_i, wp in enumerate(wp_order[1:], 1):  # WP01~WP08
        # 이동
        move_dur = rng.uniform(25, 70)

        # 트리거 이벤트
        if wp in trigger_at_wp:
            ttype = trigger_at_wp[wp]
            trigger_t = t + pd.Timedelta(seconds=move_dur * 0.6)
            rows.append(_evt(trigger_t, "TRIGGER_ACTIVATED", wp,
                             trigger_id=ttype, trigger_type=ttype,
                             mission_id_override=current_mission))
            trigger_dur = rng.uniform(5, 15)
            rows.append(_evt(
                trigger_t + pd.Timedelta(seconds=trigger_dur),
                "TRIGGER_DEACTIVATED", wp,
                trigger_id=ttype, trigger_type=ttype,
                duration_s=round(trigger_dur, 1),
                mission_id_override=current_mission))

        # Beam Pro 이벤트 (hybrid)
        if condition == "hybrid" and rng.random() < 0.4:
            beam_t = t + pd.Timedelta(seconds=move_dur * 0.5)
            rows.append(_evt(beam_t, "BEAM_SCREEN_ON", wp,
                             mission_id_override=current_mission))
            beam_dur = rng.uniform(3, 15)
            rows.append(_evt(
                beam_t + pd.Timedelta(seconds=beam_dur),
                "BEAM_SCREEN_OFF", wp,
                duration_s=round(beam_dur, 1),
                mission_id_override=current_mission))

        t += pd.Timedelta(seconds=move_dur)

        # WAYPOINT_REACHED
        dist = round(rng.uniform(0.5, 2.5), 2)
        rows.append(_evt(t, "WAYPOINT_REACHED", wp,
                         distance_m=dist,
                         anchor_bound=rng.random() < 0.55,
                         cause="proximity",
                         mission_id_override=current_mission))

        # 확신도
        conf_base = 4.9 if condition == "hybrid" else 3.6
        if wp in trigger_at_wp:
            conf_base -= 0.8
        conf = int(np.clip(round(rng.normal(conf_base, 0.8)), 1, 7))
        rows.append(_evt(t, "CONFIDENCE_RATED", wp,
                         confidence_rating=conf,
                         mission_id_override=current_mission))

        # 미션 완료 체크
        if mission_idx < len(missions):
            m = missions[mission_idx]
            if wp == mission_target_wps.get(m, ""):
                correct = rng.random() < (0.80 if condition == "hybrid" else 0.70)
                rt = round(rng.uniform(2, 8), 1)
                t += pd.Timedelta(seconds=rt)
                rows.append(_evt(t, "VERIFICATION_ANSWERED", wp,
                                 verification_correct=correct,
                                 duration_s=rt,
                                 mission_id_override=m))
                rows.append(_evt(t, "MISSION_COMPLETE", wp,
                                 mission_id_override=m))

                diff = int(np.clip(round(rng.normal(
                    2.9 if condition == "hybrid" else 3.4, 1)), 1, 7))
                rows.append(_evt(t, "DIFFICULTY_RATED", wp,
                                 difficulty_rating=diff,
                                 mission_id_override=m))

                mission_idx += 1
                if mission_idx < len(missions):
                    current_mission = missions[mission_idx]
                    t += pd.Timedelta(seconds=rng.uniform(3, 8))
                    rows.append(_evt(t, "MISSION_START", wp,
                                     mission_id_override=current_mission))

    # ROUTE_END
    t += pd.Timedelta(seconds=5)
    rows.append(_evt(t, "ROUTE_END"))

    return rows


def _generate_anchor_reloc(rng, session_id, start_time, wp_order):
    """anchor_reloc 행 생성."""
    rows = []
    t = start_time + pd.Timedelta(seconds=5)

    states = ["Tracking", "Tracking", "Tracking", "TimedOut",
              "Tracking", "Tracking", "Tracking", "Tracking", "LoadFailed"]
    slam_reasons = ["", "", "", "feature_insufficient",
                    "", "", "", "", "file_not_found"]

    for i, wp in enumerate(wp_order):
        guid = f"anchor-guid-{wp}-{rng.integers(1000, 9999)}"
        is_critical = wp in ("WP00", "WP01")
        state = rng.choice(states[:3]) if rng.random() < 0.55 else rng.choice(states[3:])
        elapsed = round(rng.uniform(0.5, 30.0), 1)
        slam = "" if state == "Tracking" else rng.choice(
            ["feature_insufficient", "relocalizing", "limited_tracking"])
        remap = 0 if state == "Tracking" else int(rng.integers(0, 3))
        file_exists = state != "LoadFailed"
        in_sdk = state != "LoadFailed"

        # InProgress 행 (중간 상태)
        if elapsed > 5:
            for prog_t in range(5, int(elapsed), 5):
                rows.append([
                    (t + pd.Timedelta(seconds=prog_t)).strftime(
                        "%Y-%m-%dT%H:%M:%S.%f")[:-3],
                    session_id, wp, guid,
                    str(is_critical).lower(), "InProgress",
                    str(float(prog_t)), rng.choice(["relocalizing", ""]),
                    "0", "", "",
                ])

        # 최종 결과
        rows.append([
            (t + pd.Timedelta(seconds=elapsed)).strftime(
                "%Y-%m-%dT%H:%M:%S.%f")[:-3],
            session_id, wp, guid,
            str(is_critical).lower(), state,
            str(elapsed), slam,
            str(remap), str(file_exists).lower(), str(in_sdk).lower(),
        ])

        t += pd.Timedelta(seconds=elapsed + rng.uniform(1, 3))

    return rows


def _generate_beam_segments(rng, start_time, condition, missions,
                             wp_order, trigger_at_wp):
    """beam_segments 행 생성."""
    rows = []
    if condition != "hybrid":
        return rows

    seg_id = 0
    t = start_time + pd.Timedelta(seconds=60)
    tabs = ["map", "info", "poi"]

    for _ in range(rng.integers(4, 10)):
        seg_id += 1
        dur = round(rng.uniform(3, 20), 1)
        on_ts = t.strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3]
        off_ts = (t + pd.Timedelta(seconds=dur)).strftime(
            "%Y-%m-%dT%H:%M:%S.%f")[:-3]

        wp = rng.choice(wp_order[1:])
        mission = rng.choice(missions)
        trigger = trigger_at_wp.get(wp, "")
        tab = rng.choice(tabs)

        rows.append([
            str(seg_id), on_ts, off_ts, str(dur),
            mission, wp, wp,
            trigger, tab,
            str(int(rng.integers(0, 4))),  # poi_view_count
            str(int(rng.integers(0, 3))),  # info_card_open_count
            str(int(rng.integers(0, 2))),  # comparison_count
            str(int(rng.integers(0, 2))),  # mission_ref_count
            str(int(rng.integers(0, 3))),  # zoom_count
            str(round(rng.uniform(0, dur * 0.6), 1)),  # map_view_duration_s
        ])
        t += pd.Timedelta(seconds=dur + rng.uniform(30, 90))

    return rows


def _generate_system_health(rng, start_time):
    """system_health 행 생성."""
    rows = []
    total_dur = int(rng.uniform(600, 850))

    for sec in range(0, total_dur, 1):  # 1Hz
        ts = (start_time + pd.Timedelta(seconds=sec)).strftime(
            "%Y-%m-%dT%H:%M:%S.%f")[:-3]
        fps = round(rng.normal(55, 8), 1)
        fps = max(10, fps)
        frame_ms = round(1000.0 / fps, 1)
        battery = round(max(0, min(1, 0.85 - sec * 0.0001 + rng.normal(0, 0.01))), 2)
        charging = "NotCharging"
        thermal = rng.choice(["none", "none", "none", "light"],
                              p=[0.7, 0.15, 0.1, 0.05])
        memory = round(rng.normal(350, 30), 1)

        # 트래킹 상태: 대부분 ready, 가끔 not_ready
        if rng.random() < 0.05:
            tracking = "not_ready"
            slam = rng.choice(["relocalizing", "feature_insufficient"])
        else:
            tracking = "ready"
            slam = ""

        rows.append([
            ts, str(fps), str(frame_ms), str(battery),
            charging, thermal, str(memory),
            tracking, slam,
        ])

    return rows
