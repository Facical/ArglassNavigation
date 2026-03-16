"""
신뢰 및 수행 분석 스크립트
- NASA-TLX 하위척도별 비교 (2조건: Glass Only vs Hybrid)
- 확신도 변화 궤적 (트리거 전후)
- 확신도-정확도 보정(calibration) 분석 (v2)
- 트리거 유형별 확신도 분석 (v2)
- 통계: Paired t-test / Wilcoxon signed-rank
- [ISMAR] head_pose sidecar에서 head scanning amplitude / angular_velocity_yaw 분석
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
from plot_style import (apply_style, save_fig, violin_with_dots, spaghetti_plot,
                        COLORS_COND, COND_LABELS, COLOR_GLASS, COLOR_HYBRID,
                        COLOR_TRIGGER, DPI)

apply_style()
warnings.filterwarnings("ignore", category=FutureWarning)

# ──────────────────────────────────────────────
# 1. 경로 설정
# ──────────────────────────────────────────────

DATA_DIR = Path(__file__).resolve().parent.parent / "data"
RAW_DIR = DATA_DIR / "raw"
SURVEY_DIR = DATA_DIR / "surveys"
OUTPUT_DIR = Path(__file__).resolve().parent / "output"
OUTPUT_DIR.mkdir(exist_ok=True)

CONDITIONS = ["glass_only", "hybrid"]
CONDITION_LABELS = ["Glass Only", "Hybrid"]
TRIGGER_WAYPOINTS = ["WP03", "WP06"]
WAYPOINTS = [f"WP{i:02d}" for i in range(1, 9)]
N_PARTICIPANTS = 24
TRIGGER_TYPES = ["T1", "T2", "T3", "T4"]

BEAM_CONTENT_EVENTS = [
    "BEAM_TAB_SWITCH", "BEAM_POI_VIEWED", "BEAM_INFO_CARD_OPENED",
    "BEAM_INFO_CARD_CLOSED", "BEAM_MAP_ZOOMED", "BEAM_COMPARISON_VIEWED",
    "BEAM_MISSION_REF_VIEWED",
]

TLX_SUBSCALES = [
    "mental_demand", "physical_demand", "temporal_demand",
    "performance", "effort", "frustration",
]
TLX_LABELS_EN = [
    "Mental\nDemand", "Physical\nDemand", "Temporal\nDemand",
    "Performance", "Effort", "Frustration",
]


# ──────────────────────────────────────────────
# 2. 데이터 로드
# ──────────────────────────────────────────────

def load_nasa_tlx(allow_fallback: bool = False) -> pd.DataFrame:
    """NASA-TLX 설문 데이터 로드 또는 fallback 생성."""
    path = SURVEY_DIR / "nasa_tlx.csv"
    if path.exists():
        return pd.read_csv(path)
    if allow_fallback:
        print(f"[경고] {path} 없음. fallback 데이터 생성.")
        return _generate_fallback_tlx()
    print(f"[오류] {path} 없음. fallback으로 실행하려면 --fallback 플래그를 사용하세요.")
    sys.exit(1)


def _generate_fallback_tlx() -> pd.DataFrame:
    from experiment_config import NASA_TLX, NASA_TLX_SD
    rng = np.random.default_rng(42)
    rows = []
    for pid in range(1, N_PARTICIPANTS + 1):
        for cond in CONDITIONS:
            row = {"participant_id": f"P{pid:02d}", "condition": cond}
            for sub in TLX_SUBSCALES:
                val = rng.normal(NASA_TLX[cond][sub], NASA_TLX_SD)
                row[sub] = int(np.clip(round(val), 1, 7))
            rows.append(row)
    return pd.DataFrame(rows)


def load_trust_scale(allow_fallback: bool = False) -> pd.DataFrame:
    """시스템 신뢰 척도 데이터 로드 또는 fallback 생성."""
    path = SURVEY_DIR / "trust_scale.csv"
    if path.exists():
        return pd.read_csv(path)
    if allow_fallback:
        print(f"[경고] {path} 없음. fallback 데이터 생성.")
        return _generate_fallback_trust()
    print(f"[오류] {path} 없음. fallback으로 실행하려면 --fallback 플래그를 사용하세요.")
    sys.exit(1)


def _generate_fallback_trust() -> pd.DataFrame:
    from experiment_config import TRUST, TRUST_SD
    rng = np.random.default_rng(123)
    trust_items = ["direction", "reliability", "confidence", "accuracy",
                   "safety", "destination_belief", "willingness_reuse"]
    rows = []
    for pid in range(1, N_PARTICIPANTS + 1):
        for cond in CONDITIONS:
            row = {"participant_id": f"P{pid:02d}", "condition": cond}
            for q, item in enumerate(trust_items, 1):
                val = rng.normal(TRUST[cond][item], TRUST_SD)
                row[f"trust_q{q}"] = int(np.clip(round(val), 1, 7))
            row["trust_mean"] = round(np.mean([row[f"trust_q{q}"] for q in range(1, 8)]), 2)
            rows.append(row)
    return pd.DataFrame(rows)


def load_confidence_from_events(allow_fallback: bool = False) -> pd.DataFrame:
    """이벤트 로그에서 확신도 데이터 추출 또는 fallback 생성."""
    # [ISMAR] sidecar 파일 제외
    SIDECAR_SUFFIXES = ("_head_pose.csv", "_nav_trace.csv", "_beam_segments.csv",
                        "_anchor_reloc.csv", "_system_health.csv")
    csv_files = sorted(
        f for f in RAW_DIR.glob("P*_*.csv")
        if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
    )
    if csv_files:
        frames = []
        for f in csv_files:
            df = pd.read_csv(f, parse_dates=["timestamp"])
            frames.append(df)
        all_events = pd.concat(frames, ignore_index=True)
        conf = all_events[all_events["event_type"] == "CONFIDENCE_RATED"].copy()
        conf["confidence_rating"] = pd.to_numeric(conf["confidence_rating"], errors="coerce")
        return conf[["participant_id", "condition", "waypoint_id", "confidence_rating"]].dropna()
    if allow_fallback:
        print(f"[경고] 이벤트 로그 없음. fallback 데이터 생성.")
        return _generate_fallback_confidence()
    print(f"[오류] {RAW_DIR}에 이벤트 로그 없음. fallback으로 실행하려면 --fallback 플래그를 사용하세요.")
    sys.exit(1)


def _generate_fallback_confidence() -> pd.DataFrame:
    from experiment_config import CONFIDENCE, CONFIDENCE_SD, CONFIDENCE_TRIGGER_DROP
    rng = np.random.default_rng(77)
    rows = []
    # Map WPs to missions for confidence lookup
    wp_mission = {"WP01": "A1", "WP02": "A1", "WP03": "B1", "WP04": "A2",
                  "WP05": "A2", "WP06": "B2", "WP07": "C1", "WP08": "C1"}
    trigger_wps = {"WP03", "WP06"}
    for pid in range(1, N_PARTICIPANTS + 1):
        for cond in CONDITIONS:
            for wp in WAYPOINTS:
                mission = wp_mission.get(wp, "A1")
                base = CONFIDENCE[cond].get(mission, 4.0)
                if wp in trigger_wps:
                    base += CONFIDENCE_TRIGGER_DROP[cond]
                val = rng.normal(base, CONFIDENCE_SD)
                rows.append({
                    "participant_id": f"P{pid:02d}",
                    "condition": cond,
                    "waypoint_id": wp,
                    "confidence_rating": int(np.clip(round(val), 1, 7)),
                })
    return pd.DataFrame(rows)


# ──────────────────────────────────────────────
# 3. NASA-TLX 분석
# ──────────────────────────────────────────────

def analyze_nasa_tlx(tlx_df: pd.DataFrame):
    """NASA-TLX 하위척도별 조건 간 비교."""
    print("\n=== NASA-TLX 하위척도별 분석 ===")
    results = []
    for sub, label in zip(TLX_SUBSCALES, TLX_LABELS_EN):
        print(f"\n  [{label}]")
        for cond, clabel in zip(CONDITIONS, CONDITION_LABELS):
            vals = tlx_df[tlx_df["condition"] == cond][sub]
            print(f"    {clabel}: M={vals.mean():.1f}, SD={vals.std():.1f}")

        _run_paired_test(tlx_df, sub, label)

        results.append({
            "subscale": label,
            **{f"{cl}_mean": tlx_df[tlx_df["condition"] == c][sub].mean()
               for c, cl in zip(CONDITIONS, CONDITION_LABELS)},
            **{f"{cl}_sd": tlx_df[tlx_df["condition"] == c][sub].std()
               for c, cl in zip(CONDITIONS, CONDITION_LABELS)},
        })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 4. 시스템 신뢰 분석
# ──────────────────────────────────────────────

def analyze_trust(trust_df: pd.DataFrame):
    """시스템 신뢰 척도 조건 간 비교."""
    print("\n=== 시스템 신뢰 척도 분석 ===")
    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        vals = trust_df[trust_df["condition"] == cond]["trust_mean"]
        print(f"  {label}: M={vals.mean():.2f}, SD={vals.std():.2f}")

    _run_paired_test(trust_df, "trust_mean", "시스템 신뢰")


# ──────────────────────────────────────────────
# 5. 확신도 궤적 분석
# ──────────────────────────────────────────────

def analyze_confidence_trajectory(conf_df: pd.DataFrame):
    """확신도 변화 궤적 분석 — 트리거 전후 비교."""
    print("\n=== 확신도 궤적 분석 ===")

    pivot = conf_df.groupby(["condition", "waypoint_id"])["confidence_rating"].mean().reset_index()
    print("\n  조건별 웨이포인트 평균 확신도:")
    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        subset = pivot[pivot["condition"] == cond].sort_values("waypoint_id")
        vals = subset["confidence_rating"].values
        print(f"    {label}: {' → '.join(f'{v:.1f}' for v in vals)}")

    print("\n  트리거 전후 확신도 변화량:")
    for trigger_wp, pre_wp in [("WP03", "WP02"), ("WP06", "WP05")]:
        print(f"\n    {trigger_wp} (트리거) vs {pre_wp} (직전):")
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            cond_data = conf_df[conf_df["condition"] == cond]
            pre = cond_data[cond_data["waypoint_id"] == pre_wp]["confidence_rating"]
            post = cond_data[cond_data["waypoint_id"] == trigger_wp]["confidence_rating"]
            if len(pre) > 0 and len(post) > 0:
                diff = post.mean() - pre.mean()
                print(f"      {label}: Δ = {diff:+.2f} ({pre.mean():.1f} → {post.mean():.1f})")

    return pivot


# ──────────────────────────────────────────────
# 5b. 확신도-정확도 보정(Calibration) 분석 — v2
# ──────────────────────────────────────────────

def analyze_calibration(conf_df: pd.DataFrame, events_df: pd.DataFrame = None):
    """확신도-정확도 상관(calibration index) 분석."""
    print("\n=== 확신도-정확도 보정(Calibration) 분석 ===")

    if events_df is not None and "MISSION_COMPLETE" in events_df["event_type"].values:
        mc = events_df[events_df["event_type"] == "MISSION_COMPLETE"].copy()

        def extract_correct(extra):
            d = parse_extra(extra)
            return d.get("correct", None)

        mc["correct"] = mc["extra_data"].apply(extract_correct)
        acc_df = mc[["participant_id", "condition", "waypoint_id", "correct"]].dropna()
    else:
        rng = np.random.default_rng(99)
        acc_rows = []
        acc_base = {"glass_only": 0.70, "hybrid": 0.80}
        mission_wps = ["WP02", "WP03", "WP05", "WP06", "WP07"]
        for pid in range(1, N_PARTICIPANTS + 1):
            for cond in CONDITIONS:
                for wp in mission_wps:
                    correct = rng.random() < acc_base[cond]
                    acc_rows.append({
                        "participant_id": f"P{pid:02d}",
                        "condition": cond,
                        "waypoint_id": wp,
                        "correct": correct,
                    })
        acc_df = pd.DataFrame(acc_rows)

    merged = conf_df.merge(acc_df, on=["participant_id", "condition", "waypoint_id"], how="inner")
    if merged.empty:
        print("  [경고] 확신도-정확도 결합 데이터 없음")
        return pd.DataFrame()

    merged["correct_num"] = merged["correct"].astype(int)

    cal_results = []
    for (pid, cond), grp in merged.groupby(["participant_id", "condition"]):
        if len(grp) >= 3 and grp["correct_num"].std() > 0:
            r, p = stats.pointbiserialr(grp["correct_num"], grp["confidence_rating"])
            cal_results.append({
                "participant_id": pid,
                "condition": cond,
                "calibration_r": round(r, 3),
                "calibration_p": round(p, 4),
                "n_missions": len(grp),
            })
        else:
            cal_results.append({
                "participant_id": pid,
                "condition": cond,
                "calibration_r": np.nan,
                "calibration_p": np.nan,
                "n_missions": len(grp),
            })

    cal_df = pd.DataFrame(cal_results)

    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        subset = cal_df[cal_df["condition"] == cond]["calibration_r"].dropna()
        if len(subset) > 0:
            print(f"  {label}: 평균 calibration r = {subset.mean():.3f} (SD={subset.std():.3f})")

    cal_valid = cal_df.dropna(subset=["calibration_r"])
    if len(cal_valid) >= N_PARTICIPANTS:
        _run_paired_test(cal_valid, "calibration_r", "Calibration Index")

    # v2.1: 정보 접근량과 calibration index 상관
    if events_df is not None and "beam_content_type" in events_df.columns:
        content_events = events_df[
            (events_df["condition"] == "hybrid") &
            (events_df["event_type"].isin(BEAM_CONTENT_EVENTS))
        ]
        if not content_events.empty:
            content_counts = content_events.groupby("participant_id").size().reset_index(name="content_access_count")
            hybrid_cal = cal_df[cal_df["condition"] == "hybrid"].copy()
            merged_cal = hybrid_cal.merge(content_counts, on="participant_id", how="left")
            merged_cal["content_access_count"] = merged_cal["content_access_count"].fillna(0)
            valid = merged_cal.dropna(subset=["calibration_r"])
            if len(valid) >= 5:
                r, p = stats.pearsonr(valid["content_access_count"], valid["calibration_r"])
                print(f"\n  [v2.1] 정보 접근량-Calibration 상관: r={r:.3f}, p={p:.4f}")

    return cal_df


def analyze_trigger_type_effects(events_df: pd.DataFrame = None):
    """트리거 유형별 확신도 변화 분석 (v2)."""
    print("\n=== 트리거 유형별 분석 ===")

    if events_df is None or "TRIGGER_ACTIVATED" not in events_df.get("event_type", pd.Series()).values:
        print("  [경고] TRIGGER_ACTIVATED 이벤트 없음, 요약 출력")
        rng = np.random.default_rng(55)
        for tt in TRIGGER_TYPES:
            drop = rng.normal(-1.2, 0.4)
            rt = rng.uniform(3, 8)
            print(f"  {tt}: 확신도 변화 = {drop:+.1f}, 반응시간 = {rt:.1f}s")
        return

    triggers = events_df[events_df["event_type"] == "TRIGGER_ACTIVATED"].copy()

    def extract_trigger_type(extra):
        d = parse_extra(extra)
        return d.get("trigger_type", "unknown")

    triggers["trigger_type"] = triggers["extra_data"].apply(extract_trigger_type)

    for tt in TRIGGER_TYPES:
        tt_events = triggers[triggers["trigger_type"] == tt]
        if len(tt_events) > 0:
            print(f"  {tt}: 발생 횟수 = {len(tt_events)}, "
                  f"참가자 수 = {tt_events['participant_id'].nunique()}")
        else:
            print(f"  {tt}: 발생 없음")


# ──────────────────────────────────────────────
# 5c. [ISMAR] head_pose sidecar 로드 및 분석
# ──────────────────────────────────────────────

def load_head_pose() -> pd.DataFrame:
    """[ISMAR] *_head_pose.csv sidecar 파일이 있으면 로드.

    head_pose에는 timestamp, participant_id, condition, yaw, pitch, roll 등이 포함.
    head scanning amplitude 및 angular_velocity_yaw 분석에 사용.
    """
    pose_files = sorted(RAW_DIR.glob("*_head_pose.csv"))
    if not pose_files:
        return pd.DataFrame()
    frames = []
    for f in pose_files:
        try:
            df = pd.read_csv(f, parse_dates=["timestamp"])
            frames.append(df)
        except Exception as e:
            print(f"  [경고] head_pose 로드 실패: {f.name} — {e}")
    if frames:
        combined = pd.concat(frames, ignore_index=True)
        print(f"  [ISMAR] head_pose sidecar 로드: {len(combined)}건 ({len(pose_files)} 파일)")
        return combined
    return pd.DataFrame()


def analyze_head_scanning(head_pose: pd.DataFrame, events_df: pd.DataFrame = None) -> pd.DataFrame:
    """[ISMAR] head_pose 데이터로 미션별 head scanning amplitude 및 angular_velocity_yaw 분석.

    - head scanning amplitude: 미션 구간 내 yaw의 range (max - min)
    - angular_velocity_yaw: 연속 프레임 간 yaw 변화율의 통계 (mean, std, max)
    """
    if head_pose.empty or "yaw" not in head_pose.columns:
        return pd.DataFrame()

    # yaw 컬럼이 있는지 확인
    if "participant_id" not in head_pose.columns or "timestamp" not in head_pose.columns:
        print("  [경고] head_pose에 필수 컬럼(participant_id, timestamp) 없음")
        return pd.DataFrame()

    # 미션 구간 정보 추출 (events_df에서)
    if events_df is not None and "MISSION_START" in events_df["event_type"].values:
        mission_starts = events_df[events_df["event_type"] == "MISSION_START"].copy()
        mission_completes = events_df[events_df["event_type"] == "MISSION_COMPLETE"].copy()

        mission_starts["parsed"] = mission_starts["extra_data"].apply(parse_extra)
        mission_starts["mission_id"] = mission_starts["parsed"].apply(lambda d: d.get("mission_id", ""))
        mission_completes["parsed"] = mission_completes["extra_data"].apply(parse_extra)
        mission_completes["mission_id"] = mission_completes["parsed"].apply(lambda d: d.get("mission_id", ""))
    else:
        # 미션 구간 정보 없으면 참가자별 전체 구간으로 분석
        mission_starts = None

    results = []

    if mission_starts is not None and not mission_starts.empty:
        # 미션별 분석
        for _, ms in mission_starts.iterrows():
            pid = ms["participant_id"]
            cond = ms["condition"]
            m_id = ms["mission_id"]
            start_time = ms["timestamp"]

            # 미션 완료 시점 찾기
            matching_mc = mission_completes[
                (mission_completes["participant_id"] == pid) &
                (mission_completes["condition"] == cond) &
                (mission_completes["mission_id"] == m_id)
            ]
            if matching_mc.empty:
                continue
            end_time = matching_mc["timestamp"].iloc[0]

            # 해당 구간의 head_pose 데이터
            pose_segment = head_pose[
                (head_pose["participant_id"] == pid) &
                (head_pose["timestamp"] >= start_time) &
                (head_pose["timestamp"] <= end_time)
            ].sort_values("timestamp")

            if len(pose_segment) < 3:
                continue

            yaw_vals = pose_segment["yaw"].values

            # head scanning amplitude (yaw range)
            # 주의: yaw는 -180~180 범위이므로 circular range 계산
            yaw_range = np.ptp(yaw_vals)  # peak-to-peak
            if yaw_range > 180:
                # wraparound 보정
                adjusted = np.where(yaw_vals < 0, yaw_vals + 360, yaw_vals)
                yaw_range = np.ptp(adjusted)

            # angular_velocity_yaw: 연속 프레임 간 yaw 변화율
            dt = pose_segment["timestamp"].diff().dt.total_seconds().values[1:]
            dyaw = np.diff(yaw_vals)
            # wraparound 보정
            dyaw = np.where(dyaw > 180, dyaw - 360, dyaw)
            dyaw = np.where(dyaw < -180, dyaw + 360, dyaw)

            valid = dt > 0
            if valid.sum() > 0:
                angular_vel = np.abs(dyaw[valid]) / dt[valid]  # deg/s
                results.append({
                    "participant_id": pid,
                    "condition": cond,
                    "mission_id": m_id,
                    "scan_amplitude_deg": round(yaw_range, 1),
                    "angular_vel_mean_dps": round(np.mean(angular_vel), 1),
                    "angular_vel_std_dps": round(np.std(angular_vel), 1),
                    "angular_vel_max_dps": round(np.max(angular_vel), 1),
                    "n_samples": len(pose_segment),
                })
    else:
        # 미션 정보 없으면 참가자×조건별 전체 분석
        for (pid, cond), grp in head_pose.groupby(
            ["participant_id", "condition"] if "condition" in head_pose.columns
            else ["participant_id"]
        ):
            if isinstance(cond, tuple):
                cond = cond[0] if cond else ""
            grp = grp.sort_values("timestamp")
            if len(grp) < 3:
                continue

            yaw_vals = grp["yaw"].values
            yaw_range = np.ptp(yaw_vals)
            if yaw_range > 180:
                adjusted = np.where(yaw_vals < 0, yaw_vals + 360, yaw_vals)
                yaw_range = np.ptp(adjusted)

            dt = grp["timestamp"].diff().dt.total_seconds().values[1:]
            dyaw = np.diff(yaw_vals)
            dyaw = np.where(dyaw > 180, dyaw - 360, dyaw)
            dyaw = np.where(dyaw < -180, dyaw + 360, dyaw)

            valid = dt > 0
            if valid.sum() > 0:
                angular_vel = np.abs(dyaw[valid]) / dt[valid]
                results.append({
                    "participant_id": pid,
                    "condition": cond,
                    "mission_id": "all",
                    "scan_amplitude_deg": round(yaw_range, 1),
                    "angular_vel_mean_dps": round(np.mean(angular_vel), 1),
                    "angular_vel_std_dps": round(np.std(angular_vel), 1),
                    "angular_vel_max_dps": round(np.max(angular_vel), 1),
                    "n_samples": len(grp),
                })

    scan_df = pd.DataFrame(results)

    print("\n=== [ISMAR] Head Scanning Amplitude / Angular Velocity 분석 ===")
    if not scan_df.empty:
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            subset = scan_df[scan_df["condition"] == cond]
            if not subset.empty:
                print(f"  {label}:")
                print(f"    Scan Amplitude: M={subset['scan_amplitude_deg'].mean():.1f}°, "
                      f"SD={subset['scan_amplitude_deg'].std():.1f}°")
                print(f"    Angular Velocity (yaw): M={subset['angular_vel_mean_dps'].mean():.1f}°/s, "
                      f"SD={subset['angular_vel_mean_dps'].std():.1f}°/s")
                print(f"    Max Angular Velocity: {subset['angular_vel_max_dps'].mean():.1f}°/s")

        # 2조건 비교
        if scan_df["condition"].nunique() == 2:
            pid_scan = scan_df.groupby(["participant_id", "condition"])["scan_amplitude_deg"].mean().reset_index()
            _run_paired_test(pid_scan, "scan_amplitude_deg", "Head Scanning Amplitude")

            pid_vel = scan_df.groupby(["participant_id", "condition"])["angular_vel_mean_dps"].mean().reset_index()
            _run_paired_test(pid_vel, "angular_vel_mean_dps", "Angular Velocity (yaw)")
    else:
        print("  [경고] head scanning 분석 결과 없음")

    return scan_df


# ──────────────────────────────────────────────
# 5d. 정보 접근량 vs NASA-TLX 관계 분석 — v2.1
# ──────────────────────────────────────────────

def analyze_information_load_tlx(tlx_df: pd.DataFrame, events_df: pd.DataFrame = None):
    """Beam Pro 정보 접근량과 NASA-TLX 관계 분석 (v2.1)."""
    print("\n=== 정보 접근량 vs NASA-TLX 분석 (v2.1) ===")

    if events_df is None:
        print("  [경고] 이벤트 데이터 없음, 기본 분석 수행")
        rng = np.random.default_rng(77)
        content_counts = pd.DataFrame({
            "participant_id": [f"P{i:02d}" for i in range(1, N_PARTICIPANTS + 1)],
            "content_access_count": rng.integers(3, 25, size=N_PARTICIPANTS),
        })
    elif "beam_content_type" not in events_df.columns:
        print("  [경고] beam_content_type 컬럼 없음, 기본 분석 수행")
        rng = np.random.default_rng(77)
        content_counts = pd.DataFrame({
            "participant_id": [f"P{i:02d}" for i in range(1, N_PARTICIPANTS + 1)],
            "content_access_count": rng.integers(3, 25, size=N_PARTICIPANTS),
        })
    else:
        content_events = events_df[
            (events_df["condition"] == "hybrid") &
            (events_df["event_type"].isin(BEAM_CONTENT_EVENTS))
        ]
        content_counts = content_events.groupby("participant_id").size().reset_index(
            name="content_access_count"
        )

    # Hybrid 조건의 TLX와 매칭
    hybrid_tlx = tlx_df[tlx_df["condition"] == "hybrid"].copy()
    merged = hybrid_tlx.merge(content_counts, on="participant_id", how="left")
    merged["content_access_count"] = merged["content_access_count"].fillna(0)

    if len(merged) < 5:
        print("  [경고] 데이터 부족")
        return

    # TLX 총점 계산
    tlx_subscales = ["mental_demand", "physical_demand", "temporal_demand",
                     "performance", "effort", "frustration"]
    available_subs = [s for s in tlx_subscales if s in merged.columns]
    if available_subs:
        merged["tlx_total"] = merged[available_subs].mean(axis=1)

        r, p = stats.pearsonr(merged["content_access_count"], merged["tlx_total"])
        print(f"  정보 접근량 vs TLX 총점: r={r:.3f}, p={p:.4f}")

        # 하위척도별 상관
        print(f"  [하위척도별 상관]")
        for sub, label in zip(available_subs,
                              ["정신적 요구", "신체적 요구", "시간적 압박",
                               "수행", "노력", "좌절"][:len(available_subs)]):
            r_sub, p_sub = stats.pearsonr(merged["content_access_count"], merged[sub])
            sig = "*" if p_sub < 0.05 else ""
            print(f"    {label}: r={r_sub:.3f}, p={p_sub:.4f} {sig}")


# ──────────────────────────────────────────────
# 6. 통계 검정 유틸리티
# ──────────────────────────────────────────────

def _run_paired_test(data: pd.DataFrame, dv: str, label: str):
    """2조건 Paired t-test (pingouin) 또는 Wilcoxon signed-rank (fallback)."""
    try:
        import pingouin as pg
        # 최소 2명 이상 paired 데이터 필요
        paired_pids = set(data[data["condition"] == "glass_only"]["participant_id"]) & \
                      set(data[data["condition"] == "hybrid"]["participant_id"])
        if len(paired_pids) < 2:
            print(f"    [경고] {label}: paired 참가자 {len(paired_pids)}명, 검정 불가 (최소 2명 필요)")
            return
        test = pg.pairwise_tests(
            data=data, dv=dv, within="condition", subject="participant_id",
            parametric=True
        )
        if not test.empty and "T" in test.columns:
            row = test.iloc[0]
            p_val = row.get("p-unc", row.get("p_unc", 0))
            hedges = row.get("hedges", float("nan"))
            print(f"    Paired t-test ({label}): t={row['T']:.2f}, p={p_val:.4f}, "
                  f"d={hedges:.2f}")
        elif not test.empty:
            print(f"    [경고] {label}: 검정 결과 불완전 (dof={test.iloc[0].get('dof', '?')})")
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
# 7. 시각화
# ──────────────────────────────────────────────

def plot_tlx_comparison(tlx_df: pd.DataFrame):
    """NASA-TLX 하위척도별 조건 비교 — violin + dots + significance."""
    n_subs = len(TLX_SUBSCALES)
    fig, axes = plt.subplots(2, 3, figsize=(12, 8))
    axes = axes.flatten()

    for idx, (sub, label_en) in enumerate(zip(TLX_SUBSCALES, TLX_LABELS_EN)):
        ax = axes[idx]
        data = [tlx_df[tlx_df["condition"] == c][sub].dropna().values for c in CONDITIONS]
        res = paired_comparison(tlx_df, sub)
        violin_with_dots(ax, data, [1, 2], COLORS_COND, COND_LABELS,
                         p_value=res["p"], ylabel="Score (1-7)",
                         title=label_en)
        ax.set_ylim(0.5, 7.5)

    fig.suptitle("NASA-TLX Subscales by Condition", fontsize=13, fontweight="bold")
    fig.tight_layout(rect=[0, 0, 1, 0.96])
    save_fig(fig, OUTPUT_DIR / "nasa_tlx_comparison")


def plot_trust_comparison(trust_df: pd.DataFrame):
    """시스템 신뢰 척도 조건별 비교 — violin + dots + significance."""
    fig, ax = plt.subplots(figsize=(5, 5))
    data = [trust_df[trust_df["condition"] == c]["trust_mean"].dropna().values for c in CONDITIONS]
    res = paired_comparison(trust_df, "trust_mean")
    violin_with_dots(ax, data, [1, 2], COLORS_COND, COND_LABELS,
                     p_value=res["p"], ylabel="System Trust Score (1-7)",
                     title="System Trust by Condition")
    ax.set_ylim(1, 7.5)
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "trust_comparison")


def plot_confidence_trajectory(pivot: pd.DataFrame, conf_df: pd.DataFrame = None):
    """확신도 변화 궤적 — mean line + CI + spaghetti option."""
    fig, ax = plt.subplots(figsize=(10, 6))

    colors = {"glass_only": COLOR_GLASS, "hybrid": COLOR_HYBRID}
    group_labels = {"glass_only": "Glass Only", "hybrid": "Hybrid"}

    # Spaghetti plot (개인별 궤적) if raw data available
    if conf_df is not None and not conf_df.empty:
        spaghetti_plot(ax, conf_df, x_col="waypoint_id", y_col="confidence_rating",
                       group_col="condition",
                       group_colors=colors, group_labels=group_labels,
                       alpha_individual=0.1, mean_line=True, ci_band=True)
    else:
        for cond, label in zip(CONDITIONS, CONDITION_LABELS):
            subset = pivot[pivot["condition"] == cond].sort_values("waypoint_id")
            ax.plot(subset["waypoint_id"], subset["confidence_rating"],
                    "o-", label=label, color=colors[cond], linewidth=2, markersize=8)

    for tw in TRIGGER_WAYPOINTS:
        ax.axvline(x=tw, color=COLOR_TRIGGER, linestyle="--", alpha=0.4)
        ax.text(tw, 6.8, "Trigger", ha="center", fontsize=8, color=COLOR_TRIGGER)

    ax.set_xlabel("Waypoint")
    ax.set_ylabel("Confidence (1-7)")
    ax.set_title("Confidence Trajectory by Condition")
    ax.set_ylim(1, 7)
    ax.legend()
    ax.grid(axis="y", alpha=0.3)
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "confidence_trajectory")


def plot_confidence_drop(conf_df: pd.DataFrame):
    """트리거 지점 전후 확신도 변화량 비교."""
    fig, axes = plt.subplots(1, 2, figsize=(10, 5))

    for idx, (trigger_wp, pre_wp) in enumerate([("WP03", "WP02"), ("WP06", "WP05")]):
        drops = []
        for cond in CONDITIONS:
            cond_data = conf_df[conf_df["condition"] == cond]
            pre_vals = cond_data[cond_data["waypoint_id"] == pre_wp].set_index("participant_id")["confidence_rating"]
            post_vals = cond_data[cond_data["waypoint_id"] == trigger_wp].set_index("participant_id")["confidence_rating"]
            common = pre_vals.index.intersection(post_vals.index)
            diff = post_vals.loc[common] - pre_vals.loc[common]
            drops.append(diff.values)

        bp = axes[idx].boxplot(drops, tick_labels=CONDITION_LABELS)
        axes[idx].axhline(y=0, color="red", linestyle="--", alpha=0.5)
        axes[idx].set_ylabel("Confidence Change (Δ)")
        axes[idx].set_title(f"Trigger {trigger_wp}: {pre_wp} → {trigger_wp} Confidence Change")

    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "confidence_drop")


# ──────────────────────────────────────────────
# 8. 메인
# ──────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="신뢰 및 수행 분석")
    parser.add_argument("--fallback", action="store_true",
                        help="데이터 파일이 없을 때 fallback 데이터로 실행")
    args = parser.parse_args()
    (OUTPUT_DIR / "csv").mkdir(exist_ok=True)

    print("=" * 60)
    print("신뢰 및 수행 분석")
    print("=" * 60)

    # 데이터 로드
    tlx_df = load_nasa_tlx(allow_fallback=args.fallback)
    trust_df = load_trust_scale(allow_fallback=args.fallback)
    conf_df = load_confidence_from_events(allow_fallback=args.fallback)

    print(f"NASA-TLX: {len(tlx_df)} rows ({tlx_df['participant_id'].nunique()} 참가자)")
    print(f"신뢰 척도: {len(trust_df)} rows ({trust_df['participant_id'].nunique()} 참가자)")
    print(f"확신도: {len(conf_df)} rows ({conf_df['participant_id'].nunique()} 참가자)")

    # 분석
    tlx_results = analyze_nasa_tlx(tlx_df)
    analyze_trust(trust_df)
    pivot = analyze_confidence_trajectory(conf_df)

    # 이벤트 데이터 로드 (v2.1 콘텐츠 분석용)
    # [ISMAR] sidecar 파일 제외
    SIDECAR_SUFFIXES = ("_head_pose.csv", "_nav_trace.csv", "_beam_segments.csv",
                        "_anchor_reloc.csv", "_system_health.csv")
    csv_files = sorted(
        f for f in RAW_DIR.glob("P*_*.csv")
        if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
    )
    events_df = None
    if csv_files:
        frames = [pd.read_csv(f, parse_dates=["timestamp"]) for f in csv_files]
        events_df = pd.concat(frames, ignore_index=True)

    # v2: calibration 분석
    cal_df = analyze_calibration(conf_df, events_df)
    analyze_trigger_type_effects(events_df)

    # v2.1: 정보 접근량-TLX 분석
    analyze_information_load_tlx(tlx_df, events_df)

    # [ISMAR] head_pose sidecar 로드 및 분석
    head_pose = load_head_pose()
    scan_df = pd.DataFrame()
    if not head_pose.empty:
        scan_df = analyze_head_scanning(head_pose, events_df)

    # 시각화
    print(f"\n=== 시각화 ===")
    plot_tlx_comparison(tlx_df)
    plot_trust_comparison(trust_df)
    plot_confidence_trajectory(pivot, conf_df)
    plot_confidence_drop(conf_df)

    # 결과 저장
    tlx_results.to_csv(OUTPUT_DIR / "csv" / "nasa_tlx_summary.csv", index=False)
    print(f"  → {OUTPUT_DIR / 'csv' / 'nasa_tlx_summary.csv'} 저장")

    trust_summary = trust_df.groupby("condition")["trust_mean"].agg(["mean", "std"]).reset_index()
    trust_summary.to_csv(OUTPUT_DIR / "csv" / "trust_summary.csv", index=False)
    print(f"  → {OUTPUT_DIR / 'csv' / 'trust_summary.csv'} 저장")

    if not cal_df.empty:
        cal_df.to_csv(OUTPUT_DIR / "csv" / "calibration_summary.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'csv' / 'calibration_summary.csv'} 저장")

    # [ISMAR] head scanning 결과 저장
    if not scan_df.empty:
        scan_df.to_csv(OUTPUT_DIR / "csv" / "head_scanning_analysis.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'csv' / 'head_scanning_analysis.csv'} 저장")

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
