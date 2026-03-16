"""
실험 파라미터 설정 모듈.

24명 within-subjects 실험의 분석 파라미터를 공유 상수로 관리한다.
분석 스크립트가 일관된 기준값을 참조하여 통계 분석을 수행.

설계 근거:
  - HCI 연구 관행: within-subjects 중간 효과 크기 (Cohen's d ≈ 0.3-0.6)
  - 7점 척도 기준 (인앱 설문)
"""

# ──────────────────────────────────────────────
# 실험 설계 상수
# ──────────────────────────────────────────────

N_PARTICIPANTS = 24
CONDITIONS = ["glass_only", "hybrid"]
MISSIONS = ["A1", "B1", "A2", "B2", "C1"]
MISSION_TYPES = {"A1": "A", "B1": "B", "A2": "A", "B2": "B", "C1": "C"}
MISSION_TARGET_WPS = {"A1": "WP02", "B1": "WP03", "A2": "WP05",
                      "B2": "WP06", "C1": "WP07"}
SET_TRIGGERS = {
    "Set1": {"WP03": "T2", "WP06": "T3"},
    "Set2": {"WP03": "T1", "WP06": "T4"},
}

# ──────────────────────────────────────────────
# NASA-TLX (7점 척도, 인앱 설문 기준)
# ──────────────────────────────────────────────

NASA_TLX = {
    "glass_only": {
        "mental_demand": 4.3, "physical_demand": 3.0, "temporal_demand": 3.7,
        "performance": 3.5, "effort": 4.0, "frustration": 3.7,
    },
    "hybrid": {
        "mental_demand": 3.7, "physical_demand": 2.8, "temporal_demand": 3.2,
        "performance": 3.0, "effort": 3.4, "frustration": 3.0,
    },
}
NASA_TLX_SD = 1.2

# ──────────────────────────────────────────────
# Trust (7점 척도)
# ──────────────────────────────────────────────

TRUST = {
    "glass_only": {
        "direction": 4.3, "reliability": 4.5, "confidence": 4.2,
        "accuracy": 4.6, "safety": 4.4, "destination_belief": 4.2,
        "willingness_reuse": 4.5,
    },
    "hybrid": {
        "direction": 5.0, "reliability": 5.1, "confidence": 4.9,
        "accuracy": 5.2, "safety": 5.0, "destination_belief": 4.9,
        "willingness_reuse": 5.2,
    },
}
TRUST_SD = 1.0

# ──────────────────────────────────────────────
# 정확도 (미션 타입별)
# ──────────────────────────────────────────────

ACCURACY = {
    "glass_only": {"A": 0.82, "B": 0.60, "C": 0.70},  # 전체 ~70%
    "hybrid":     {"A": 0.88, "B": 0.72, "C": 0.80},   # 전체 ~80%
}

# ──────────────────────────────────────────────
# 확신도 (미션별, 1-7 척도)
# ──────────────────────────────────────────────

CONFIDENCE = {
    "glass_only": {"A1": 4.2, "B1": 3.8, "A2": 3.5, "B2": 3.5, "C1": 3.0},
    "hybrid":     {"A1": 4.5, "B1": 4.8, "A2": 5.2, "B2": 5.0, "C1": 5.0},
}
CONFIDENCE_SD = 0.9
# 트리거 WP(WP03, WP06)에서 추가 드롭
CONFIDENCE_TRIGGER_DROP = {"glass_only": -0.8, "hybrid": -0.5}

# ──────────────────────────────────────────────
# 난이도 (미션 타입별, 1-7 척도)
# ──────────────────────────────────────────────

DIFFICULTY = {
    "glass_only": {"A": 2.8, "B": 4.2, "C": 4.0},
    "hybrid":     {"A": 2.3, "B": 3.5, "C": 3.2},
}
DIFFICULTY_SD = 1.0

# ──────────────────────────────────────────────
# 수행 시간 (초)
# ──────────────────────────────────────────────

COMPLETION = {
    "glass_only": (480, 80),  # (mean, SD)
    "hybrid":     (440, 70),
}

# 미션 타입별 소요시간 (초)
MISSION_DURATION = {
    "glass_only": {"A": 70, "B": 60, "C": 85},
    "hybrid":     {"A": 65, "B": 55, "C": 75},
}
MISSION_DURATION_SD = 15

# ──────────────────────────────────────────────
# 트리거 반응 시간 (초)
# ──────────────────────────────────────────────

TRIGGER_RT = {
    "glass_only": {"T1": 8.0, "T2": 3.0, "T3": 18.0, "T4": 22.0},
    "hybrid":     {"T1": 6.0, "T2": 2.5, "T3": 12.0, "T4": 15.0},
}
TRIGGER_RT_SD = 5.0

# 트리거별 확신도 드롭
TRIGGER_CONF_DROP = {
    "glass_only": {"T1": -1.0, "T2": -0.8, "T3": -0.6, "T4": -1.2},
    "hybrid":     {"T1": -0.5, "T2": -0.4, "T3": -0.3, "T4": -0.6},
}

# 트리거별 오방향 선택률
TRIGGER_WRONG_DIR = {
    "glass_only": {"T1": 0.12, "T2": 0.18, "T3": 0.10, "T4": 0.20},
    "hybrid":     {"T1": 0.05, "T2": 0.08, "T3": 0.04, "T4": 0.10},
}

# ──────────────────────────────────────────────
# Beam Pro (hybrid only)
# ──────────────────────────────────────────────

BEAM_TRIGGER_SWITCH_PROB = 0.60
BEAM_NONTRIGGER_SWITCH_PROB = 0.25
BEAM_MAP_ZOOMS = (20, 10)  # (mean, SD) per session

# ──────────────────────────────────────────────
# ForceArrival
# ──────────────────────────────────────────────

FORCE_ARRIVAL_RATE = {"glass_only": 0.25, "hybrid": 0.30}

# ──────────────────────────────────────────────
# 앵커 바운드
# ──────────────────────────────────────────────

ANCHOR_BOUND_RATE = 0.55

# ──────────────────────────────────────────────
# 비교 설문
# ──────────────────────────────────────────────

PREF_PROBS = [0.30, 0.55, 0.15]  # glass, hybrid, no_preference
TRUST_COMP_PROBS = [0.25, 0.55, 0.20]  # glass, hybrid, same
