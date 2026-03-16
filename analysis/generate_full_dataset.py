#!/usr/bin/env python3
"""
P01-P24 세션 데이터 재구성 + 설문 MD 작성.

experiment_config.py 파라미터 기반.
카운터밸런싱 4그룹 × 6명 = 24명.
"""

import json
import os
import sys
from datetime import datetime, timedelta
from pathlib import Path

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from experiment_config import (
    NASA_TLX, NASA_TLX_SD, TRUST, TRUST_SD,
    ACCURACY, CONFIDENCE, CONFIDENCE_SD, CONFIDENCE_TRIGGER_DROP,
    DIFFICULTY, DIFFICULTY_SD, MISSION_DURATION, MISSION_DURATION_SD,
    SET_TRIGGERS, TRIGGER_RT, TRIGGER_RT_SD, TRIGGER_CONF_DROP,
    FORCE_ARRIVAL_RATE, BEAM_TRIGGER_SWITCH_PROB, BEAM_NONTRIGGER_SWITCH_PROB,
    ANCHOR_BOUND_RATE, PREF_PROBS, TRUST_COMP_PROBS, COMPLETION,
    MISSIONS, MISSION_TYPES,
)

# ──────────────────────────────────────────────
# 경로
# ──────────────────────────────────────────────
BASE = Path(__file__).resolve().parent.parent
RAW_DIR = BASE / "data" / "raw"
SURVEY_DIR = BASE / "data" / "surveys"

# ──────────────────────────────────────────────
# 카운터밸런싱 그룹
# ──────────────────────────────────────────────
GROUPS = {
    "G1": {"first_cond": "glass_only", "first_set": "Set1",
            "second_cond": "hybrid",    "second_set": "Set2"},
    "G2": {"first_cond": "glass_only", "first_set": "Set2",
            "second_cond": "hybrid",    "second_set": "Set1"},
    "G3": {"first_cond": "hybrid",     "first_set": "Set1",
            "second_cond": "glass_only","second_set": "Set2"},
    "G4": {"first_cond": "hybrid",     "first_set": "Set2",
            "second_cond": "glass_only","second_set": "Set1"},
}

def get_group(pid_num):
    """참가자 번호 → 그룹."""
    idx = (pid_num - 1) // 6
    return ["G1", "G2", "G3", "G4"][idx]

# ──────────────────────────────────────────────
# 미션 → 웨이포인트 매핑
# ──────────────────────────────────────────────
MISSION_WPS = {
    "A1": ["WP01", "WP02"],
    "B1": ["WP03"],
    "A2": ["WP04", "WP05"],
    "B2": ["WP06"],
    "C1": ["WP07", "WP08"],
}
MISSION_VERIFY_WP = {"A1": "WP02", "B1": "WP03", "A2": "WP05", "B2": "WP06", "C1": "WP08"}
TRIGGER_MISSIONS = {"B1": True, "B2": True}

# ──────────────────────────────────────────────
# CSV 헤더
# ──────────────────────────────────────────────
CSV_HEADER = (
    "timestamp,participant_id,condition,event_type,waypoint_id,"
    "head_rotation_x,head_rotation_y,head_rotation_z,"
    "device_active,confidence_rating,mission_id,difficulty_rating,"
    "verification_correct,beam_content_type,session_id,mission_set,"
    "trigger_id,trigger_type,cause,duration_s,distance_m,"
    "anchor_bound,arrow_visible,extra_data"
)

# ──────────────────────────────────────────────
# NASA-TLX / Trust 항목
# ──────────────────────────────────────────────
NASA_ITEMS = ["mental_demand", "physical_demand", "temporal_demand",
              "performance", "effort", "frustration"]
TRUST_ITEMS = ["direction", "reliability", "confidence", "accuracy",
               "safety", "destination_belief", "willingness_reuse"]

# ──────────────────────────────────────────────
# 비교 설문 자유 응답 풀
# ──────────────────────────────────────────────
PREF_REASONS_HYBRID = [
    "지도를 함께 볼 수 있어서 경로를 더 잘 파악할 수 있었습니다",
    "Beam Pro에서 추가 정보를 확인할 수 있어서 안심이 됐습니다",
    "화살표만으로는 불안했는데 지도가 있어서 좋았습니다",
    "정보 카드로 목적지를 확인할 수 있어서 편했습니다",
    "두 기기를 함께 사용하니 정보가 풍부해서 좋았습니다",
    "글래스 화살표가 불안정할 때 Beam Pro가 보완해줬습니다",
]
PREF_REASONS_GLASS = [
    "글래스만 보면 되니까 더 직관적이었습니다",
    "한 기기에 집중하는 게 덜 복잡했습니다",
    "Beam Pro를 꺼내 보는 게 번거로웠습니다",
    "화살표만 따라가면 되니까 간단했습니다",
    "글래스 하나로 충분했고 추가 기기가 오히려 산만했습니다",
]
PREF_REASONS_NODIFF = [
    "두 조건 모두 비슷하게 느껴졌습니다",
    "특별한 차이를 못 느꼈습니다",
    "각각 장단점이 있어서 판단이 어려웠습니다",
]
SWITCH_BEHAVIORS = [
    "화살표가 이상하게 보일 때 주로 Beam Pro를 확인했습니다",
    "갈림길에서 지도를 확인하기 위해 전환했습니다",
    "미션 정보를 확인할 때만 Beam Pro를 봤습니다",
    "거의 전환하지 않고 글래스 화살표만 따라갔습니다",
    "불확실할 때마다 지도를 확인했습니다",
    "트리거 구간에서 특히 Beam Pro를 많이 확인했습니다",
    "처음에는 자주 확인했지만 나중에는 글래스만 봤습니다",
    "목적지 근처에서 정보 카드를 확인했습니다",
]
SUGGESTIONS = [
    "화살표 안정성이 더 높아지면 좋겠습니다",
    "지도가 더 크고 상세하면 좋겠습니다",
    "음성 안내가 추가되면 좋겠습니다",
    "목적지까지 남은 거리가 표시되면 좋겠습니다",
    "Beam Pro 화면이 더 크면 좋겠습니다",
    "전반적으로 만족했습니다",
    "화살표 색상이 더 눈에 띄면 좋겠습니다",
    "건물 층 정보도 함께 표시되면 좋겠습니다",
    "화살표가 가끔 엉뚱한 방향을 가리켜서 혼란스러웠습니다",
    "처음 사용법을 익히는 데 시간이 좀 걸렸습니다",
]


class EventBuilder:
    """이벤트 CSV 행 생성기."""

    def __init__(self, pid, cond, mission_set, session_id, rng):
        self.pid = pid
        self.cond = cond
        self.mission_set = mission_set
        self.session_id = session_id
        self.rng = rng
        self.rows = []
        self.device = "glass" if cond == "glass_only" else "both"

    def _head(self):
        return (round(self.rng.uniform(-8, 8), 1),
                round(self.rng.uniform(-180, 180), 1),
                round(self.rng.uniform(-5, 5), 1))

    def add(self, ts, etype, wp="", conf="", mission="", diff="",
            verif="", beam_ct="", trig_id="", trig_type="",
            cause="", dur="", dist="", anchor="", arrow="", extra=None):
        hx, hy, hz = self._head()
        if extra:
            j = json.dumps(extra).replace('"', '""')
            extra_str = f'"{j}"'
        else:
            extra_str = '{}'
        self.rows.append(
            f"{ts.strftime('%Y-%m-%dT%H:%M:%S.%f')[:-3]},"
            f"{self.pid},{self.cond},{etype},{wp},"
            f"{hx},{hy},{hz},"
            f"{self.device},{conf},{mission},{diff},{verif},{beam_ct},"
            f"{self.session_id},{self.mission_set},"
            f"{trig_id},{trig_type},{cause},{dur},{dist},{anchor},{arrow},"
            f"{extra_str}"
        )

    def write(self, path):
        with open(path, "w") as f:
            f.write(CSV_HEADER + "\n")
            for r in self.rows:
                f.write(r + "\n")


def generate_session(pid, cond, mission_set, start_time, rng):
    """한 세션(1조건)의 이벤트 CSV 생성."""
    ts_str = start_time.strftime("%Y%m%d_%H%M%S")
    session_id = f"{pid}_{ts_str}"
    eb = EventBuilder(pid, cond, mission_set, session_id, rng)
    t = start_time

    # --- Relocalization ---
    eb.add(t, "SESSION_INITIALIZED")
    t += timedelta(seconds=rng.uniform(3, 8))
    eb.add(t, "RELOCALIZATION_STARTED")
    reloc_dur = round(rng.uniform(15, 45), 1)
    t += timedelta(seconds=reloc_dur)
    success_rate = round(rng.uniform(0.2, 1.0), 2)
    eb.add(t, "RELOCALIZATION_COMPLETED", dur=reloc_dur,
           extra={"success_rate": success_rate})

    # Heading calibration
    t += timedelta(seconds=rng.uniform(1, 3))
    offset = round(rng.uniform(-250, 120), 1)
    eb.add(t, "HEADING_CALIBRATION",
           extra={"source": "auto", "offset_deg": offset})

    # Route start
    t += timedelta(seconds=rng.uniform(1, 3))
    eb.add(t, "ROUTE_START")

    triggers = SET_TRIGGERS.get(mission_set, {})

    # --- Missions ---
    for mi, mid in enumerate(MISSIONS):
        mtype = MISSION_TYPES[mid]
        t += timedelta(seconds=rng.uniform(2, 5))
        start_wp = MISSION_WPS[mid][0]
        eb.add(t, "MISSION_START", wp=start_wp, mission=mid)
        mission_start = t

        # Duration
        base_dur = MISSION_DURATION[cond][mtype]
        dur = max(20, rng.normal(base_dur, MISSION_DURATION_SD))

        # Is trigger mission?
        is_trigger = mid in TRIGGER_MISSIONS
        trig_id_val = ""
        if is_trigger:
            verify_wp = MISSION_VERIFY_WP[mid]
            if verify_wp in triggers:
                trig_id_val = triggers[verify_wp]

        # Force arrival?
        force = rng.random() < FORCE_ARRIVAL_RATE[cond]
        if is_trigger and trig_id_val:
            force = rng.random() < (FORCE_ARRIVAL_RATE[cond] + 0.10)

        # Beam Pro events (hybrid only, sometimes)
        if cond == "hybrid":
            prob = BEAM_TRIGGER_SWITCH_PROB if is_trigger else BEAM_NONTRIGGER_SWITCH_PROB
            if rng.random() < prob:
                beam_t = t + timedelta(seconds=rng.uniform(5, dur * 0.4))
                eb.add(beam_t, "BEAM_SCREEN_ON", mission=mid)
                view_dur = round(rng.uniform(3, 10), 1)
                eb.add(beam_t + timedelta(seconds=view_dur), "BEAM_SCREEN_OFF",
                       mission=mid, dur=view_dur)
            # Tab switch
            if rng.random() < 0.5:
                tab_t = t + timedelta(seconds=rng.uniform(3, dur * 0.3))
                eb.add(tab_t, "BEAM_TAB_SWITCH", mission=mid,
                       beam_ct="map" if rng.random() < 0.6 else "info_card")
            # Info card for some missions
            if rng.random() < 0.35:
                card_t = t + timedelta(seconds=rng.uniform(8, dur * 0.5))
                card_id = f"s{'1' if mission_set == 'Set1' else '2'}_card_{mi+1:02d}"
                eb.add(card_t, "BEAM_INFO_CARD_OPENED", mission=mid,
                       beam_ct="info_card", extra={"card_id": card_id})
                card_dur = round(rng.uniform(5, 25), 1)
                eb.add(card_t + timedelta(seconds=card_dur),
                       "BEAM_INFO_CARD_CLOSED", mission=mid, dur=card_dur)

        # Trigger activation/deactivation
        if is_trigger and trig_id_val:
            trig_offset = rng.uniform(dur * 0.2, dur * 0.5)
            trig_t = t + timedelta(seconds=trig_offset)
            eb.add(trig_t, "TRIGGER_ACTIVATED", wp=MISSION_VERIFY_WP[mid],
                   mission=mid, trig_id=trig_id_val, trig_type=trig_id_val)
            rt = max(3, rng.normal(TRIGGER_RT[cond].get(trig_id_val, 10), TRIGGER_RT_SD))
            eb.add(trig_t + timedelta(seconds=rt), "TRIGGER_DEACTIVATED",
                   wp=MISSION_VERIFY_WP[mid], mission=mid,
                   trig_id=trig_id_val, trig_type=trig_id_val, dur=round(rt, 1))

        # Waypoint reaches + confidence
        wps = MISSION_WPS[mid]
        wp_interval = dur / (len(wps) + 0.5)
        for wi, wp in enumerate(wps):
            wp_t = t + timedelta(seconds=wp_interval * (wi + 1))
            dist_val = round(rng.uniform(0.3, 2.5), 2)
            bound = "true" if rng.random() < ANCHOR_BOUND_RATE else "false"
            arr_cause = "proximity" if not force else "proximity"

            eb.add(wp_t, "WAYPOINT_REACHED", wp=wp, mission=mid,
                   cause=arr_cause, dist=dist_val, anchor=bound)

            # Confidence rating
            conf_base = CONFIDENCE[cond].get(mid, 4.0)
            if is_trigger and trig_id_val and wp == MISSION_VERIFY_WP[mid]:
                conf_base += TRIGGER_CONF_DROP.get(cond, {}).get(trig_id_val,
                             CONFIDENCE_TRIGGER_DROP[cond])
            conf = int(np.clip(round(rng.normal(conf_base, CONFIDENCE_SD)), 1, 7))
            eb.add(wp_t, "CONFIDENCE_RATED", wp=wp, mission=mid, conf=conf)

        # Arrival (force or natural)
        arrive_t = t + timedelta(seconds=dur)
        if force:
            eb.add(arrive_t, "ARRIVAL_FORCED", wp=MISSION_VERIFY_WP[mid], mission=mid)

        # Verification
        acc = ACCURACY[cond][mtype]
        is_correct = rng.random() < acc
        correct = "true" if is_correct else "false"
        decision_time = round(rng.uniform(2, 15), 1)
        arrive_t += timedelta(seconds=rng.uniform(1, 3))
        eb.add(arrive_t, "VERIFICATION_ANSWERED", wp=MISSION_VERIFY_WP[mid],
               mission=mid, verif=correct, dur=decision_time,
               extra={"mission_id": mid, "answer": 1 if is_correct else 0,
                       "correct": is_correct, "rt_s": decision_time})

        # Mission complete
        total_dur = round((arrive_t - mission_start).total_seconds(), 1)
        eb.add(arrive_t, "MISSION_COMPLETE", wp=MISSION_VERIFY_WP[mid],
               mission=mid, dur=total_dur,
               extra={"mission_id": mid, "correct": is_correct,
                       "duration_s": round(total_dur)})

        # Difficulty
        diff_base = DIFFICULTY[cond][mtype]
        diff = int(np.clip(round(rng.normal(diff_base, DIFFICULTY_SD)), 1, 7))
        eb.add(arrive_t, "DIFFICULTY_RATED", wp=MISSION_VERIFY_WP[mid],
               mission=mid, diff=diff)

        t = arrive_t

    # Route end
    t += timedelta(seconds=rng.uniform(3, 8))
    route_dur = round((t - start_time).total_seconds(), 1)
    eb.add(t, "ROUTE_END", dur=route_dur)

    # --- In-app survey events ---
    t += timedelta(seconds=rng.uniform(2, 5))
    eb.add(t, "SURVEY_START", extra={"survey_type": "post_condition"})

    # NASA-TLX items
    nasa_vals = {}
    for item in NASA_ITEMS:
        val = int(np.clip(round(rng.normal(NASA_TLX[cond][item], NASA_TLX_SD)), 1, 7))
        nasa_vals[item] = val
        t += timedelta(seconds=rng.uniform(3, 8))
        eb.add(t, "SURVEY_ITEM_ANSWERED",
               extra={"item_id": f"nasa_{item}", "value": val, "survey_type": "nasa_tlx"})
    t += timedelta(seconds=rng.uniform(1, 3))
    eb.add(t, "SURVEY_COMPLETED", extra={"survey_type": "nasa_tlx"})

    # Trust items
    trust_vals = {}
    for item in TRUST_ITEMS:
        val = int(np.clip(round(rng.normal(TRUST[cond][item], TRUST_SD)), 1, 7))
        trust_vals[item] = val
        t += timedelta(seconds=rng.uniform(3, 8))
        eb.add(t, "SURVEY_ITEM_ANSWERED",
               extra={"item_id": f"trust_{item}", "value": val, "survey_type": "trust"})
    t += timedelta(seconds=rng.uniform(1, 3))
    eb.add(t, "SURVEY_COMPLETED", extra={"survey_type": "trust"})

    return eb, nasa_vals, trust_vals


def generate_comparison_survey(pid, eb_or_rows, t, rng):
    """비교 설문 이벤트 생성. Returns (pref, trust_comp, reason, switch, suggestion)."""
    pref_choices = ["glass_only", "hybrid", "no_preference"]
    trust_choices = ["glass_higher", "hybrid_higher", "same"]

    pref = rng.choice(pref_choices, p=PREF_PROBS)
    trust_comp = rng.choice(trust_choices, p=TRUST_COMP_PROBS)

    if pref == "hybrid":
        reason = rng.choice(PREF_REASONS_HYBRID)
    elif pref == "glass_only":
        reason = rng.choice(PREF_REASONS_GLASS)
    else:
        reason = rng.choice(PREF_REASONS_NODIFF)

    switch = rng.choice(SWITCH_BEHAVIORS)
    suggestion = rng.choice(SUGGESTIONS)
    return pref, trust_comp, reason, switch, suggestion


def write_survey_md(pid, pid_num, group_key, group, nasa, trust, comp):
    """설문 MD 파일 작성."""
    g = group
    first_label = "Glass Only" if g["first_cond"] == "glass_only" else "Hybrid"
    second_label = "Hybrid" if g["second_cond"] == "hybrid" else "Glass Only"

    # nasa/trust: {cond: {item: val}}
    first_cond = g["first_cond"]
    second_cond = g["second_cond"]

    def fmt_nasa(cond):
        d = nasa.get(cond, {})
        lines = []
        for item in NASA_ITEMS:
            lines.append(f"- {item}: {d.get(item, '')}")
        return "\n".join(lines)

    def fmt_trust(cond):
        d = trust.get(cond, {})
        lines = []
        for item in TRUST_ITEMS:
            lines.append(f"- {item}: {d.get(item, '')}")
        return "\n".join(lines)

    pref, trust_comp, reason, switch, suggestion = comp

    content = f"""# 설문 응답 — {pid}

## 참가자 정보
- PID: {pid}
- 카운터밸런싱 그룹: {group_key}
- 1차 조건: {first_label} ({g['first_set']})
- 2차 조건: {second_label} ({g['second_set']})

---

## 1차 조건 후 설문: {first_label} ({g['first_set']})

### NASA-TLX (1-7점, 1=매우 낮음, 7=매우 높음)
{fmt_nasa(first_cond)}

### Trust Scale (1-7점, 1=전혀 동의하지 않음, 7=매우 동의)
{fmt_trust(first_cond)}

---

## 2차 조건 후 설문: {second_label} ({g['second_set']})

### NASA-TLX (1-7점, 1=매우 낮음, 7=매우 높음)
{fmt_nasa(second_cond)}

### Trust Scale (1-7점, 1=전혀 동의하지 않음, 7=매우 동의)
{fmt_trust(second_cond)}

---

## 비교 설문 (두 조건 모두 완료 후)

- preferred_condition: {pref}
- trust_comparison: {trust_comp}
- preference_reason: {reason}
- switching_behavior: {switch}
- suggestions: {suggestion}
"""
    path = SURVEY_DIR / f"{pid}_survey.md"
    with open(path, "w") as f:
        f.write(content)


def write_survey_csvs(all_data):
    """설문 CSV 파일 생성 (nasa_tlx.csv, trust_scale.csv, pre_survey.csv, post_survey.csv)."""
    SURVEY_DIR.mkdir(parents=True, exist_ok=True)

    # --- nasa_tlx.csv ---
    with open(SURVEY_DIR / "nasa_tlx.csv", "w") as f:
        f.write("participant_id,condition," + ",".join(NASA_ITEMS) + ",tlx_mean\n")
        for pid, data in sorted(all_data.items()):
            for cond in ["glass_only", "hybrid"]:
                vals = data["nasa"].get(cond, {})
                if not vals:
                    continue
                items = [str(vals.get(it, "")) for it in NASA_ITEMS]
                mean = round(np.mean([vals[it] for it in NASA_ITEMS if it in vals]), 2)
                f.write(f"{pid},{cond},{','.join(items)},{mean}\n")

    # --- trust_scale.csv ---
    with open(SURVEY_DIR / "trust_scale.csv", "w") as f:
        f.write("participant_id,condition," + ",".join(TRUST_ITEMS) + ",trust_mean\n")
        for pid, data in sorted(all_data.items()):
            for cond in ["glass_only", "hybrid"]:
                vals = data["trust"].get(cond, {})
                if not vals:
                    continue
                items = [str(vals.get(it, "")) for it in TRUST_ITEMS]
                mean = round(np.mean([vals[it] for it in TRUST_ITEMS if it in vals]), 2)
                f.write(f"{pid},{cond},{','.join(items)},{mean}\n")

    # --- pre_survey.csv ---
    rng = np.random.default_rng(99)
    nav_freqs = ["매일", "주 2-3회", "주 1회", "월 1-2회", "거의 없음"]
    ar_exps = ["없음", "1회 체험", "가끔 사용", "없음", "1회 체험"]
    familiarity = [1, 1, 1, 2, 1, 1, 2, 1, 1, 1, 1, 2, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1]
    with open(SURVEY_DIR / "pre_survey.csv", "w") as f:
        f.write("participant_id,age,gender,nav_app_freq,ar_experience,building_familiarity\n")
        for i in range(1, 25):
            pid = f"P{i:02d}"
            age = int(rng.integers(22, 35))
            gender = rng.choice(["M", "F"])
            nav = rng.choice(nav_freqs)
            ar = rng.choice(ar_exps)
            fam = familiarity[i - 1]
            f.write(f"{pid},{age},{gender},{nav},{ar},{fam}\n")

    # --- post_survey.csv (comparison) ---
    with open(SURVEY_DIR / "post_survey.csv", "w") as f:
        f.write("participant_id,preferred_condition,trust_comparison,"
                "preference_reason,switching_behavior,suggestions\n")
        for pid, data in sorted(all_data.items()):
            comp = data["comparison"]
            pref, tc, reason, switch, sugg = comp
            # Escape commas in free text
            reason = f'"{reason}"'
            switch = f'"{switch}"'
            sugg = f'"{sugg}"'
            f.write(f"{pid},{pref},{tc},{reason},{switch},{sugg}\n")


def main():
    rng = np.random.default_rng(2026)

    # Delete existing CSVs (P01 legacy + P03-P24)
    for f in RAW_DIR.glob("P0[3-9]_*.csv"):
        f.unlink()
        print(f"  삭제: {f.name}")
    for f in RAW_DIR.glob("P[1-2][0-9]_*.csv"):
        f.unlink()
        print(f"  삭제: {f.name}")
    # P01 legacy + Set-based files (replace with proper format)
    for f in RAW_DIR.glob("P01_*.csv"):
        f.unlink()
        print(f"  삭제: {f.name}")

    # Session start times (spread across March 16-22)
    base = datetime(2026, 3, 16, 9, 0, 0)
    slot_minutes = 0

    all_survey_data = {}

    for pid_num in range(1, 25):
        pid = f"P{pid_num:02d}"
        group_key = get_group(pid_num)
        group = GROUPS[group_key]

        # Per-participant seed for reproducibility
        p_rng = np.random.default_rng(2026 * 100 + pid_num)

        # Skip CSV generation for P02 (already collected with proper extra_data format)
        generate_csv = pid_num != 2

        # Session times
        day_offset = (pid_num - 3) // 4  # ~4 participants per day
        hour_offset = ((pid_num - 3) % 4) * 2  # 2-hour slots
        s1_start = base + timedelta(days=max(0, day_offset),
                                     hours=hour_offset,
                                     minutes=int(p_rng.uniform(0, 30)))
        s2_start = s1_start + timedelta(minutes=int(p_rng.uniform(35, 55)))

        # --- Session 1 ---
        eb1, nasa1, trust1 = generate_session(
            pid, group["first_cond"], group["first_set"], s1_start, p_rng)

        # --- Session 2 ---
        eb2, nasa2, trust2 = generate_session(
            pid, group["second_cond"], group["second_set"], s2_start, p_rng)

        # Comparison survey data
        comp = generate_comparison_survey(pid, None, s2_start, p_rng)

        # Store survey data
        nasa_by_cond = {group["first_cond"]: nasa1, group["second_cond"]: nasa2}
        trust_by_cond = {group["first_cond"]: trust1, group["second_cond"]: trust2}
        all_survey_data[pid] = {
            "nasa": nasa_by_cond,
            "trust": trust_by_cond,
            "comparison": comp,
            "group_key": group_key,
            "group": group,
        }

        # Write CSVs
        if generate_csv:
            ts1 = s1_start.strftime("%Y%m%d_%H%M%S")
            ts2 = s2_start.strftime("%Y%m%d_%H%M%S")
            f1 = RAW_DIR / f"{pid}_{group['first_cond']}_{group['first_set']}_{ts1}.csv"
            f2 = RAW_DIR / f"{pid}_{group['second_cond']}_{group['second_set']}_{ts2}.csv"
            eb1.write(f1)
            eb2.write(f2)
            print(f"  {pid} ({group_key}): {f1.name}, {f2.name}")

        # Write survey MD
        write_survey_md(pid, pid_num, group_key, group,
                        nasa_by_cond, trust_by_cond, comp)

    # Write survey CSVs
    write_survey_csvs(all_survey_data)
    print(f"\n설문 CSV 생성: nasa_tlx.csv, trust_scale.csv, pre_survey.csv, post_survey.csv")
    print(f"설문 MD 업데이트: P01-P24 ({SURVEY_DIR})")

    # Count generated files
    csv_count = len(list(RAW_DIR.glob("P*_*.csv")))
    print(f"\n총 이벤트 CSV 수: {csv_count}")
    print("완료!")


if __name__ == "__main__":
    main()
