"""
인앱 설문 분석 스크립트
- 이벤트 CSV 로그에 내장된 인앱 설문 데이터를 추출하여 분석
- NASA-TLX Raw (6항목, 7점): mental_demand, physical_demand, temporal_demand,
  performance, effort, frustration
- Trust (7항목, 7점): direction, reliability, confidence, accuracy,
  safety, destination_belief, willingness_reuse
- 비교 설문 (Comparison Survey): 5페이지 (선호 조건, 신뢰 비교, 선호 이유,
  전환 행동, 제안)
- 시각화: Fig 5 (NASA-TLX grouped violin), Fig 9 (Diverging Likert bar),
  Trust violin, 비교 설문 분포
- 통계: Paired t-test / Wilcoxon signed-rank, Cohen's d, 95% CI
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
from stat_utils import (
    paired_comparison, batch_paired_comparison, significance_marker,
    add_significance_bracket, format_stat_line,
)
from plot_style import (
    apply_style, save_fig, violin_with_dots, diverging_likert_bar,
    COLORS_COND, COND_LABELS, COLOR_GLASS, COLOR_HYBRID,
    FIG_DOUBLE_COL, FIG_WIDE, DPI,
)

apply_style()

# ──────────────────────────────────────────────
# 1. 설정
# ──────────────────────────────────────────────

DATA_DIR = Path(__file__).resolve().parent.parent / "data"
RAW_DIR = DATA_DIR / "raw"
OUTPUT_DIR = Path(__file__).resolve().parent / "output"
OUTPUT_DIR.mkdir(exist_ok=True)

CONDITIONS = ["glass_only", "hybrid"]
CONDITION_LABELS = COND_LABELS  # ["Glass Only", "Hybrid"]
N_PARTICIPANTS = 24

SIDECAR_SUFFIXES = ("_head_pose.csv", "_nav_trace.csv", "_beam_segments.csv",
                    "_anchor_reloc.csv", "_system_health.csv")

# NASA-TLX items (7-point, in-app)
NASA_TLX_ITEMS = [
    "mental_demand", "physical_demand", "temporal_demand",
    "performance", "effort", "frustration",
]
NASA_TLX_LABELS = [
    "Mental Demand", "Physical Demand", "Temporal Demand",
    "Performance", "Effort", "Frustration",
]
NASA_TLX_LABELS_KR = [
    "정신적 요구", "신체적 요구", "시간적 압박",
    "수행", "노력", "좌절",
]

# Trust items (7-point)
TRUST_ITEMS = [
    "direction", "reliability", "confidence", "accuracy",
    "safety", "destination_belief", "willingness_reuse",
]
TRUST_LABELS = [
    "Direction", "Reliability", "Confidence", "Accuracy",
    "Safety", "Destination Belief", "Willingness to Reuse",
]
TRUST_LABELS_KR = [
    "방향 안내", "신뢰성", "확신", "정확도",
    "안전", "목적지 도달", "재사용 의향",
]

# 비교 설문 페이지 구성
COMPARISON_PAGES = {
    1: "preferred_condition",    # 선호 조건
    2: "trust_comparison",       # 신뢰 비교
    3: "preference_reason",      # 선호 이유
    4: "switching_behavior",     # 전환 행동
    5: "suggestion",             # 제안
}


# ──────────────────────────────────────────────
# 2. 데이터 로드
# ──────────────────────────────────────────────

def load_all_events(allow_fallback: bool = False) -> pd.DataFrame:
    """data/raw/ 내 모든 이벤트 로그 CSV를 통합하여 반환.

    allow_fallback=True인 경우, CSV가 없거나 설문 이벤트가 없으면 fallback 데이터를 생성.
    """
    csv_files = sorted(
        f for f in RAW_DIR.glob("P*_*.csv")
        if not any(f.name.endswith(s) for s in SIDECAR_SUFFIXES)
    )
    if not csv_files:
        if allow_fallback:
            print(f"[경고] {RAW_DIR}에 CSV 파일이 없습니다. fallback 데이터를 생성합니다.")
            return generate_fallback_data()
        else:
            print(f"[오류] {RAW_DIR}에 CSV 파일이 없습니다.")
            print("  fallback 데이터로 실행하려면 --fallback 플래그를 사용하세요.")
            sys.exit(1)

    frames = []
    for f in csv_files:
        df = pd.read_csv(f, parse_dates=["timestamp"])
        frames.append(df)
    combined = pd.concat(frames, ignore_index=True)

    # 설문 응답 이벤트가 없으면 fallback 데이터로 대체 (완료 이벤트만 있는 경우 포함)
    survey_answer_events = combined[combined["event_type"].isin([
        "SURVEY_ITEM_ANSWERED", "COMPARISON_SURVEY_ANSWERED",
    ])]
    if survey_answer_events.empty:
        if allow_fallback:
            print(f"[경고] 이벤트 로그에 설문 이벤트가 없습니다. fallback 데이터를 생성합니다.")
            return generate_fallback_data()
        else:
            print(f"[경고] 이벤트 로그에 설문 이벤트(SURVEY_ITEM_ANSWERED 등)가 없습니다.")
            print("  인앱 설문 데이터가 포함된 로그가 필요합니다.")
            print("  fallback 데이터로 실행하려면 --fallback 플래그를 사용하세요.")

    return combined


def generate_fallback_data() -> pd.DataFrame:
    """인앱 설문 fallback 데이터 생성 (24명 x 2조건)."""
    from experiment_config import (NASA_TLX, NASA_TLX_SD, TRUST, TRUST_SD,
                              PREF_PROBS, TRUST_COMP_PROBS)
    rng = np.random.default_rng(42)
    rows = []
    base_time = pd.Timestamp("2026-03-15T10:00:00")

    # --- 비교 설문 기댓값 ---
    pref_probs = PREF_PROBS
    pref_choices = ["glass_only", "hybrid", "no_preference"]

    trust_comp_probs = TRUST_COMP_PROBS
    trust_comp_choices = ["glass_only", "hybrid", "same"]

    reason_categories = [
        "Info accessibility", "Intuitiveness", "Minimal view obstruction",
        "Screen size", "Hands free", "Map usage", "Other",
    ]

    switch_behaviors = [
        "Frequent switching", "Switch only when needed",
        "Rarely switched", "Other",
    ]

    for pid in range(1, N_PARTICIPANTS + 1):
        participant_id = f"P{pid:02d}"

        for cond in CONDITIONS:
            t = base_time + pd.Timedelta(minutes=pid * 30)
            if cond == "hybrid":
                t += pd.Timedelta(hours=1)

            # NASA-TLX 설문 응답
            for item in NASA_TLX_ITEMS:
                val = int(np.clip(round(rng.normal(NASA_TLX[cond][item], NASA_TLX_SD)), 1, 7))
                t += pd.Timedelta(seconds=rng.uniform(3, 8))
                rows.append(_event(t, participant_id, cond, "SURVEY_ITEM_ANSWERED",
                                   item_id=f"nasa_{item}", value=val,
                                   survey_type="nasa_tlx"))

            # NASA-TLX 완료
            t += pd.Timedelta(seconds=rng.uniform(1, 3))
            rows.append(_event(t, participant_id, cond, "SURVEY_COMPLETED",
                               survey_type="nasa_tlx"))

            # Trust 설문 응답
            for item in TRUST_ITEMS:
                val = int(np.clip(round(rng.normal(TRUST[cond][item], TRUST_SD)), 1, 7))
                t += pd.Timedelta(seconds=rng.uniform(3, 8))
                rows.append(_event(t, participant_id, cond, "SURVEY_ITEM_ANSWERED",
                                   item_id=f"trust_{item}", value=val,
                                   survey_type="trust"))

            # Trust 완료
            t += pd.Timedelta(seconds=rng.uniform(1, 3))
            rows.append(_event(t, participant_id, cond, "SURVEY_COMPLETED",
                               survey_type="trust"))

        # 비교 설문 (양 조건 완료 후 1회)
        t = base_time + pd.Timedelta(minutes=pid * 30, hours=2)

        # Page 1: 선호 조건
        pref = rng.choice(pref_choices, p=pref_probs)
        rows.append(_event(t, participant_id, "", "COMPARISON_SURVEY_ANSWERED",
                           page=1, question="preferred_condition", answer=pref))

        # Page 2: 신뢰 비교
        t += pd.Timedelta(seconds=rng.uniform(5, 15))
        trust_comp = rng.choice(trust_comp_choices, p=trust_comp_probs)
        rows.append(_event(t, participant_id, "", "COMPARISON_SURVEY_ANSWERED",
                           page=2, question="trust_comparison", answer=trust_comp))

        # Page 3: 선호 이유
        t += pd.Timedelta(seconds=rng.uniform(10, 30))
        reason = rng.choice(reason_categories)
        rows.append(_event(t, participant_id, "", "COMPARISON_SURVEY_ANSWERED",
                           page=3, question="preference_reason", answer=reason))

        # Page 4: 전환 행동
        t += pd.Timedelta(seconds=rng.uniform(5, 15))
        switch = rng.choice(switch_behaviors)
        rows.append(_event(t, participant_id, "", "COMPARISON_SURVEY_ANSWERED",
                           page=4, question="switching_behavior", answer=switch))

        # Page 5: 제안
        t += pd.Timedelta(seconds=rng.uniform(10, 30))
        suggestion = rng.choice(["Larger screen", "Faster response", "Better design",
                                  "Not sure", "Satisfied"])
        rows.append(_event(t, participant_id, "", "COMPARISON_SURVEY_ANSWERED",
                           page=5, question="suggestion", answer=suggestion))

        # 비교 설문 완료
        t += pd.Timedelta(seconds=rng.uniform(1, 3))
        rows.append(_event(t, participant_id, "", "COMPARISON_SURVEY_COMPLETED"))

    df = pd.DataFrame(rows)
    df["timestamp"] = pd.to_datetime(df["timestamp"], format="ISO8601")
    return df


def _event(t, pid, cond, etype, **extra) -> dict:
    """이벤트 행 생성 헬퍼."""
    row = {
        "timestamp": t.isoformat(),
        "participant_id": pid,
        "condition": cond,
        "event_type": etype,
        "waypoint_id": "",
        "head_rotation_x": 0,
        "head_rotation_y": 0,
        "head_rotation_z": 0,
        "device_active": "",
        "confidence_rating": "",
        "beam_content_type": "",
        "extra_data": json.dumps(extra) if extra else "{}",
    }
    return row


# ──────────────────────────────────────────────
# 3. 설문 데이터 추출
# ──────────────────────────────────────────────

def extract_nasa_tlx(df: pd.DataFrame) -> pd.DataFrame:
    """이벤트 로그에서 NASA-TLX 인앱 설문 데이터를 추출하여 wide 형식으로 반환.

    Returns:
        DataFrame with columns: participant_id, condition, mental_demand, ...
                                frustration, tlx_total
    """
    survey_items = df[df["event_type"] == "SURVEY_ITEM_ANSWERED"].copy()
    if survey_items.empty:
        return pd.DataFrame()

    survey_items["parsed"] = survey_items["extra_data"].apply(parse_extra)
    survey_items["survey_type"] = survey_items["parsed"].apply(
        lambda d: d.get("survey_type", ""))
    survey_items["item_id"] = survey_items["parsed"].apply(
        lambda d: d.get("item_id", ""))
    survey_items["value"] = survey_items["parsed"].apply(
        lambda d: d.get("value", np.nan))

    tlx = survey_items[survey_items["survey_type"] == "nasa_tlx"].copy()
    if tlx.empty:
        return pd.DataFrame()

    # item_id에서 "nasa_" 접두사 제거
    tlx["item_key"] = tlx["item_id"].str.replace("nasa_", "", n=1)

    # Wide 형식으로 변환
    pivot = tlx.pivot_table(
        index=["participant_id", "condition"],
        columns="item_key",
        values="value",
        aggfunc="first",
    ).reset_index()

    # TLX 총점 (평균)
    available_items = [i for i in NASA_TLX_ITEMS if i in pivot.columns]
    if available_items:
        pivot["tlx_total"] = pivot[available_items].mean(axis=1)

    print(f"  NASA-TLX 추출: {len(pivot)}건 ({pivot['participant_id'].nunique()} 참가자)")
    return pivot


def extract_trust(df: pd.DataFrame) -> pd.DataFrame:
    """이벤트 로그에서 Trust 인앱 설문 데이터를 추출하여 wide 형식으로 반환.

    Returns:
        DataFrame with columns: participant_id, condition, direction, ...
                                willingness_reuse, trust_mean
    """
    survey_items = df[df["event_type"] == "SURVEY_ITEM_ANSWERED"].copy()
    if survey_items.empty:
        return pd.DataFrame()

    survey_items["parsed"] = survey_items["extra_data"].apply(parse_extra)
    survey_items["survey_type"] = survey_items["parsed"].apply(
        lambda d: d.get("survey_type", ""))
    survey_items["item_id"] = survey_items["parsed"].apply(
        lambda d: d.get("item_id", ""))
    survey_items["value"] = survey_items["parsed"].apply(
        lambda d: d.get("value", np.nan))

    trust = survey_items[survey_items["survey_type"] == "trust"].copy()
    if trust.empty:
        return pd.DataFrame()

    # item_id에서 "trust_" 접두사 제거
    trust["item_key"] = trust["item_id"].str.replace("trust_", "", n=1)

    # Wide 형식으로 변환
    pivot = trust.pivot_table(
        index=["participant_id", "condition"],
        columns="item_key",
        values="value",
        aggfunc="first",
    ).reset_index()

    # Trust 평균점수
    available_items = [i for i in TRUST_ITEMS if i in pivot.columns]
    if available_items:
        pivot["trust_mean"] = pivot[available_items].mean(axis=1)

    print(f"  Trust 추출: {len(pivot)}건 ({pivot['participant_id'].nunique()} 참가자)")
    return pivot


def extract_comparison_survey(df: pd.DataFrame) -> pd.DataFrame:
    """이벤트 로그에서 비교 설문 데이터를 추출.

    Returns:
        DataFrame with columns: participant_id, page, question, answer
    """
    comp = df[df["event_type"] == "COMPARISON_SURVEY_ANSWERED"].copy()
    if comp.empty:
        return pd.DataFrame()

    comp["parsed"] = comp["extra_data"].apply(parse_extra)
    comp["page"] = comp["parsed"].apply(lambda d: d.get("page", 0))
    comp["question"] = comp["parsed"].apply(lambda d: d.get("question", ""))
    comp["answer"] = comp["parsed"].apply(lambda d: d.get("answer", ""))

    result = comp[["participant_id", "page", "question", "answer"]].copy()
    print(f"  비교 설문 추출: {len(result)}건 ({result['participant_id'].nunique()} 참가자)")
    return result


# ──────────────────────────────────────────────
# 4. NASA-TLX 분석
# ──────────────────────────────────────────────

def analyze_nasa_tlx(tlx_df: pd.DataFrame) -> pd.DataFrame:
    """NASA-TLX 하위척도별 조건 간 비교 (7점 척도)."""
    print("\n=== NASA-TLX 인앱 설문 분석 (7점 척도) ===")

    if tlx_df.empty:
        print("  [경고] NASA-TLX 데이터 없음")
        return pd.DataFrame()

    results = []
    for item, label_en, label_kr in zip(NASA_TLX_ITEMS, NASA_TLX_LABELS,
                                         NASA_TLX_LABELS_KR):
        if item not in tlx_df.columns:
            continue
        print(f"\n  [{label_kr} ({label_en})]")
        for cond, clabel in zip(CONDITIONS, CONDITION_LABELS):
            vals = tlx_df[tlx_df["condition"] == cond][item].dropna()
            print(f"    {clabel}: M={vals.mean():.2f}, SD={vals.std():.2f} (n={len(vals)})")

        # Paired comparison
        r = paired_comparison(tlx_df, item)
        print(f"    {format_stat_line(r)}")
        results.append({
            "subscale": label_en,
            "subscale_kr": label_kr,
            "glass_mean": r["mean_a"],
            "glass_sd": r["sd_a"],
            "hybrid_mean": r["mean_b"],
            "hybrid_sd": r["sd_b"],
            "test": r["test"],
            "statistic": r["statistic"],
            "p": r["p"],
            "d": r["d"],
            "significant": r["significant"],
        })

    # TLX 총점 비교
    if "tlx_total" in tlx_df.columns:
        print(f"\n  [TLX 총점 (평균)]")
        for cond, clabel in zip(CONDITIONS, CONDITION_LABELS):
            vals = tlx_df[tlx_df["condition"] == cond]["tlx_total"].dropna()
            print(f"    {clabel}: M={vals.mean():.2f}, SD={vals.std():.2f}")
        r = paired_comparison(tlx_df, "tlx_total")
        print(f"    {format_stat_line(r)}")
        results.append({
            "subscale": "TLX Total",
            "subscale_kr": "TLX 총점",
            "glass_mean": r["mean_a"],
            "glass_sd": r["sd_a"],
            "hybrid_mean": r["mean_b"],
            "hybrid_sd": r["sd_b"],
            "test": r["test"],
            "statistic": r["statistic"],
            "p": r["p"],
            "d": r["d"],
            "significant": r["significant"],
        })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 5. Trust 분석
# ──────────────────────────────────────────────

def analyze_trust(trust_df: pd.DataFrame) -> pd.DataFrame:
    """Trust 척도 조건 간 비교 (7점 척도)."""
    print("\n=== Trust 인앱 설문 분석 (7점 척도) ===")

    if trust_df.empty:
        print("  [경고] Trust 데이터 없음")
        return pd.DataFrame()

    results = []
    for item, label_en, label_kr in zip(TRUST_ITEMS, TRUST_LABELS, TRUST_LABELS_KR):
        if item not in trust_df.columns:
            continue
        print(f"\n  [{label_kr} ({label_en})]")
        for cond, clabel in zip(CONDITIONS, CONDITION_LABELS):
            vals = trust_df[trust_df["condition"] == cond][item].dropna()
            print(f"    {clabel}: M={vals.mean():.2f}, SD={vals.std():.2f} (n={len(vals)})")

        r = paired_comparison(trust_df, item)
        print(f"    {format_stat_line(r)}")
        results.append({
            "item": label_en,
            "item_kr": label_kr,
            "glass_mean": r["mean_a"],
            "glass_sd": r["sd_a"],
            "hybrid_mean": r["mean_b"],
            "hybrid_sd": r["sd_b"],
            "test": r["test"],
            "statistic": r["statistic"],
            "p": r["p"],
            "d": r["d"],
            "significant": r["significant"],
        })

    # Trust 평균점수 비교
    if "trust_mean" in trust_df.columns:
        print(f"\n  [Trust 평균점수]")
        for cond, clabel in zip(CONDITIONS, CONDITION_LABELS):
            vals = trust_df[trust_df["condition"] == cond]["trust_mean"].dropna()
            print(f"    {clabel}: M={vals.mean():.2f}, SD={vals.std():.2f}")
        r = paired_comparison(trust_df, "trust_mean")
        print(f"    {format_stat_line(r)}")
        results.append({
            "item": "Trust Mean",
            "item_kr": "Trust 평균",
            "glass_mean": r["mean_a"],
            "glass_sd": r["sd_a"],
            "hybrid_mean": r["mean_b"],
            "hybrid_sd": r["sd_b"],
            "test": r["test"],
            "statistic": r["statistic"],
            "p": r["p"],
            "d": r["d"],
            "significant": r["significant"],
        })

    return pd.DataFrame(results)


# ──────────────────────────────────────────────
# 6. 비교 설문 분석
# ──────────────────────────────────────────────

def analyze_comparison_survey(comp_df: pd.DataFrame) -> dict:
    """비교 설문 분석: 선호 조건, 신뢰 비교, 선호 이유, 전환 행동."""
    print("\n=== 비교 설문 분석 ===")

    if comp_df.empty:
        print("  [경고] 비교 설문 데이터 없음")
        return {}

    results = {}

    # Page 1: 선호 조건 분포
    pref = comp_df[comp_df["question"] == "preferred_condition"]
    if not pref.empty:
        pref_counts = pref["answer"].value_counts()
        total = len(pref)
        print(f"\n  [선호 조건] (n={total})")
        for ans, cnt in pref_counts.items():
            label = {"glass_only": "Glass Only", "hybrid": "Hybrid",
                     "no_preference": "No Preference"}.get(ans, ans)
            print(f"    {label}: {cnt}명 ({cnt/total:.0%})")
        results["preferred_condition"] = pref_counts.to_dict()

    # Page 2: 신뢰 비교
    trust_comp = comp_df[comp_df["question"] == "trust_comparison"]
    if not trust_comp.empty:
        tc_counts = trust_comp["answer"].value_counts()
        total = len(trust_comp)
        print(f"\n  [신뢰 비교] (n={total})")
        for ans, cnt in tc_counts.items():
            label = {"glass_only": "Glass Only", "hybrid": "Hybrid",
                     "same": "동일"}.get(ans, ans)
            print(f"    {label}: {cnt}명 ({cnt/total:.0%})")
        results["trust_comparison"] = tc_counts.to_dict()

    # Page 3: 선호 이유
    reasons = comp_df[comp_df["question"] == "preference_reason"]
    if not reasons.empty:
        reason_counts = reasons["answer"].value_counts()
        total = len(reasons)
        print(f"\n  [선호 이유] (n={total})")
        for ans, cnt in reason_counts.items():
            print(f"    {ans}: {cnt}명 ({cnt/total:.0%})")
        results["preference_reason"] = reason_counts.to_dict()

    # Page 4: 전환 행동
    switching = comp_df[comp_df["question"] == "switching_behavior"]
    if not switching.empty:
        switch_counts = switching["answer"].value_counts()
        total = len(switching)
        print(f"\n  [전환 행동 자기 보고] (n={total})")
        for ans, cnt in switch_counts.items():
            print(f"    {ans}: {cnt}명 ({cnt/total:.0%})")
        results["switching_behavior"] = switch_counts.to_dict()

    # Page 5: 제안
    suggestions = comp_df[comp_df["question"] == "suggestion"]
    if not suggestions.empty:
        sug_counts = suggestions["answer"].value_counts()
        total = len(suggestions)
        print(f"\n  [제안] (n={total})")
        for ans, cnt in sug_counts.head(5).items():
            print(f"    {ans}: {cnt}명 ({cnt/total:.0%})")
        results["suggestion"] = sug_counts.to_dict()

    return results


# ──────────────────────────────────────────────
# 7. 시각화 — Fig 5: NASA-TLX Grouped Violin
# ──────────────────────────────────────────────

def plot_fig5_nasa_tlx_violin(tlx_df: pd.DataFrame, tlx_results: pd.DataFrame):
    """Fig 5: NASA-TLX 6개 하위척도 grouped violin plot."""
    if tlx_df.empty:
        return

    available_items = [i for i in NASA_TLX_ITEMS if i in tlx_df.columns]
    if not available_items:
        return

    n_items = len(available_items)
    fig, axes = plt.subplots(1, n_items, figsize=(n_items * 1.8, 4.5), sharey=True)
    if n_items == 1:
        axes = [axes]

    for idx, item in enumerate(available_items):
        ax = axes[idx]
        glass_vals = tlx_df[tlx_df["condition"] == "glass_only"][item].dropna().values
        hybrid_vals = tlx_df[tlx_df["condition"] == "hybrid"][item].dropna().values

        # p값 추출
        p_val = None
        if not tlx_results.empty:
            match = tlx_results[tlx_results["subscale"] == NASA_TLX_LABELS[
                NASA_TLX_ITEMS.index(item)]]
            if not match.empty:
                p_val = match.iloc[0]["p"]

        violin_with_dots(
            ax,
            [glass_vals, hybrid_vals],
            positions=[0, 1],
            colors=COLORS_COND,
            labels=CONDITION_LABELS,
            p_value=p_val,
            ylabel="Score (1-7)" if idx == 0 else "",
            title=NASA_TLX_LABELS[NASA_TLX_ITEMS.index(item)],
        )
        ax.set_ylim(0.5, 7.5)

    fig.suptitle("Fig 5. NASA-TLX Subscales by Condition (In-app, 7-point)", fontsize=12,
                 fontweight="bold", y=1.02)
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "fig5_nasa_tlx_violin")


# ──────────────────────────────────────────────
# 8. 시각화 — Trust Violin
# ──────────────────────────────────────────────

def plot_trust_violin(trust_df: pd.DataFrame, trust_results: pd.DataFrame):
    """Trust 평균점수 violin plot + 개별 항목 비교."""
    if trust_df.empty:
        return

    # Trust 평균점수 violin
    if "trust_mean" in trust_df.columns:
        fig, ax = plt.subplots(figsize=(4, 4.5))
        glass_vals = trust_df[trust_df["condition"] == "glass_only"]["trust_mean"].dropna().values
        hybrid_vals = trust_df[trust_df["condition"] == "hybrid"]["trust_mean"].dropna().values

        p_val = None
        if not trust_results.empty:
            match = trust_results[trust_results["item"] == "Trust Mean"]
            if not match.empty:
                p_val = match.iloc[0]["p"]

        violin_with_dots(
            ax,
            [glass_vals, hybrid_vals],
            positions=[0, 1],
            colors=COLORS_COND,
            labels=CONDITION_LABELS,
            p_value=p_val,
            ylabel="Trust Mean (1-7)",
            title="Trust Mean Score",
        )
        ax.set_ylim(0.5, 7.5)
        fig.tight_layout()
        save_fig(fig, OUTPUT_DIR / "trust_mean_violin")

    # 개별 Trust 항목 비교 (그룹 바 차트)
    available_items = [i for i in TRUST_ITEMS if i in trust_df.columns]
    if not available_items:
        return

    fig, ax = plt.subplots(figsize=FIG_DOUBLE_COL)
    x = np.arange(len(available_items))
    width = 0.35

    for i, (cond, clabel, color) in enumerate(
        zip(CONDITIONS, CONDITION_LABELS, COLORS_COND)
    ):
        means = []
        sds = []
        for item in available_items:
            vals = trust_df[trust_df["condition"] == cond][item].dropna()
            means.append(vals.mean())
            sds.append(vals.std())
        ax.bar(x + i * width, means, width, yerr=sds, label=clabel,
               color=color, capsize=3, alpha=0.8)

    # 유의성 마커
    if not trust_results.empty:
        for j, item in enumerate(available_items):
            label_en = TRUST_LABELS[TRUST_ITEMS.index(item)]
            match = trust_results[trust_results["item"] == label_en]
            if not match.empty:
                p = match.iloc[0]["p"]
                marker = significance_marker(p)
                if marker and marker != "n.s.":
                    y_max = max(
                        trust_df[trust_df["condition"] == "glass_only"][item].mean(),
                        trust_df[trust_df["condition"] == "hybrid"][item].mean(),
                    )
                    ax.text(j + width / 2, y_max + 0.5, marker,
                            ha="center", va="bottom", fontsize=9, fontweight="bold")

    ax.set_xlabel("Trust Item")
    ax.set_ylabel("Score (1-7)")
    ax.set_title("Trust Items by Condition (In-app, 7-point)")
    ax.set_xticks(x + width / 2)
    ax.set_xticklabels(
        [TRUST_LABELS[TRUST_ITEMS.index(i)] for i in available_items],
        rotation=20, ha="right",
    )
    ax.legend()
    ax.set_ylim(0, 7.5)
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "trust_items_comparison")


# ──────────────────────────────────────────────
# 9. 시각화 — Fig 9: Diverging Likert Bar
# ──────────────────────────────────────────────

def plot_fig9_diverging_likert(tlx_df: pd.DataFrame, trust_df: pd.DataFrame):
    """Fig 9: NASA-TLX + Trust 전 항목 Diverging Stacked Bar (7점 Likert).

    좌: Glass Only, 우: Hybrid — side by side.
    Negative (1-3) 왼쪽, Positive (5-7) 오른쪽, Neutral (4) 중앙.
    """
    if tlx_df.empty and trust_df.empty:
        return

    # 모든 항목 통합
    all_items = []
    all_labels = []

    tlx_available = [i for i in NASA_TLX_ITEMS if i in tlx_df.columns] if not tlx_df.empty else []
    trust_available = [i for i in TRUST_ITEMS if i in trust_df.columns] if not trust_df.empty else []

    all_items = tlx_available + trust_available
    all_labels = (
        [NASA_TLX_LABELS[NASA_TLX_ITEMS.index(i)] for i in tlx_available] +
        [TRUST_LABELS[TRUST_ITEMS.index(i)] for i in trust_available]
    )

    if not all_items:
        return

    fig, axes = plt.subplots(1, 2, figsize=FIG_WIDE, sharey=True)

    for ax_idx, (cond, clabel, color) in enumerate(
        zip(CONDITIONS, CONDITION_LABELS, COLORS_COND)
    ):
        ax = axes[ax_idx]

        # 해당 조건의 데이터 결합
        cond_data = pd.DataFrame()
        if not tlx_df.empty:
            tlx_cond = tlx_df[tlx_df["condition"] == cond][tlx_available].copy()
            cond_data = pd.concat([cond_data, tlx_cond], axis=1)
        if not trust_df.empty:
            trust_cond = trust_df[trust_df["condition"] == cond][trust_available].copy()
            # index 맞추기 위해 reset
            trust_cond = trust_cond.reset_index(drop=True)
            if not cond_data.empty:
                cond_data = cond_data.reset_index(drop=True)
            cond_data = pd.concat([cond_data, trust_cond], axis=1)

        diverging_likert_bar(
            ax,
            cond_data,
            items=all_items,
            item_labels=all_labels,
            scale_range=(1, 7),
            condition=cond,
            title=clabel,
        )

    # 구분선: TLX와 Trust 사이
    if tlx_available and trust_available:
        for ax in axes:
            y_sep = len(tlx_available) - 0.5
            ax.axhline(y=y_sep, color="gray", linewidth=0.8, linestyle="--", alpha=0.5)

    # 범례
    from matplotlib.patches import Patch
    neg_colors = ["#d73027", "#fc8d59", "#fee08b"]
    mid_color = "#ffffbf"
    pos_colors = ["#d9ef8b", "#91cf60", "#1a9850"]
    legend_colors = neg_colors + [mid_color] + pos_colors
    legend_labels = ["1 (Very Low)", "2", "3", "4 (Neutral)", "5", "6", "7 (Very High)"]
    handles = [Patch(facecolor=c, label=l) for c, l in zip(legend_colors, legend_labels)]
    fig.legend(handles=handles, loc="lower center", ncol=7, fontsize=7,
               bbox_to_anchor=(0.5, -0.02))

    fig.suptitle("Fig 9. NASA-TLX + Trust Response Distribution (Diverging Likert, 7-point)",
                 fontsize=12, fontweight="bold")
    fig.tight_layout(rect=[0, 0.05, 1, 0.95])
    save_fig(fig, OUTPUT_DIR / "fig9_diverging_likert")


# ──────────────────────────────────────────────
# 10. 시각화 — 비교 설문
# ──────────────────────────────────────────────

def plot_comparison_survey(comp_results: dict):
    """비교 설문 시각화: 선호 조건 파이, 신뢰 비교 바, 선호 이유/전환 행동 바."""
    if not comp_results:
        return

    n_plots = sum(1 for k in ["preferred_condition", "trust_comparison",
                               "preference_reason", "switching_behavior"]
                  if k in comp_results)
    if n_plots == 0:
        return

    fig, axes = plt.subplots(2, 2, figsize=FIG_WIDE)
    axes = axes.flatten()

    # Plot 1: 선호 조건 (파이)
    ax_idx = 0
    if "preferred_condition" in comp_results:
        ax = axes[ax_idx]
        pref = comp_results["preferred_condition"]
        label_map = {"glass_only": "Glass Only", "hybrid": "Hybrid",
                     "no_preference": "No Preference"}
        color_map = {"glass_only": COLOR_GLASS, "hybrid": COLOR_HYBRID,
                     "no_preference": "#999999"}
        labels = [label_map.get(k, k) for k in pref.keys()]
        values = list(pref.values())
        colors = [color_map.get(k, "#cccccc") for k in pref.keys()]
        wedges, texts, autotexts = ax.pie(
            values, labels=labels, colors=colors, autopct="%1.0f%%",
            startangle=90, textprops={"fontsize": 9})
        for autotext in autotexts:
            autotext.set_fontweight("bold")
        ax.set_title("Preferred Condition")
        ax_idx += 1

    # Plot 2: 신뢰 비교 (바)
    if "trust_comparison" in comp_results:
        ax = axes[ax_idx]
        tc = comp_results["trust_comparison"]
        label_map = {"glass_only": "Glass Only", "hybrid": "Hybrid", "same": "Same"}
        color_map = {"glass_only": COLOR_GLASS, "hybrid": COLOR_HYBRID,
                     "same": "#999999"}
        labels = [label_map.get(k, k) for k in tc.keys()]
        values = list(tc.values())
        colors = [color_map.get(k, "#cccccc") for k in tc.keys()]
        bars = ax.bar(labels, values, color=colors, edgecolor="white")
        for bar in bars:
            h = bar.get_height()
            ax.text(bar.get_x() + bar.get_width() / 2., h + 0.3,
                    f"{int(h)}명", ha="center", va="bottom", fontsize=9)
        ax.set_ylabel("Response Count")
        ax.set_title("More Trusted Condition")
        ax_idx += 1

    # Plot 3: 선호 이유 (수평 바)
    if "preference_reason" in comp_results:
        ax = axes[ax_idx]
        reasons = comp_results["preference_reason"]
        sorted_reasons = sorted(reasons.items(), key=lambda x: x[1], reverse=True)
        labels = [r[0] for r in sorted_reasons]
        values = [r[1] for r in sorted_reasons]
        y_pos = np.arange(len(labels))
        ax.barh(y_pos, values, color=COLOR_HYBRID, alpha=0.8)
        ax.set_yticks(y_pos)
        ax.set_yticklabels(labels, fontsize=8)
        ax.set_xlabel("Response Count")
        ax.set_title("Preference Reason")
        ax.invert_yaxis()
        ax_idx += 1

    # Plot 4: 전환 행동 (수평 바)
    if "switching_behavior" in comp_results:
        ax = axes[ax_idx]
        switch = comp_results["switching_behavior"]
        sorted_switch = sorted(switch.items(), key=lambda x: x[1], reverse=True)
        labels = [s[0] for s in sorted_switch]
        values = [s[1] for s in sorted_switch]
        y_pos = np.arange(len(labels))
        ax.barh(y_pos, values, color=COLOR_GLASS, alpha=0.8)
        ax.set_yticks(y_pos)
        ax.set_yticklabels(labels, fontsize=8)
        ax.set_xlabel("Response Count")
        ax.set_title("Switching Behavior Self-report")
        ax.invert_yaxis()
        ax_idx += 1

    # 사용하지 않는 axes 숨기기
    for i in range(ax_idx, len(axes)):
        axes[i].set_visible(False)

    fig.suptitle("Comparison Survey Results", fontsize=12, fontweight="bold")
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "comparison_survey")


# ──────────────────────────────────────────────
# 11. 일괄 통계 (batch)
# ──────────────────────────────────────────────

def run_batch_analysis(tlx_df: pd.DataFrame, trust_df: pd.DataFrame):
    """NASA-TLX + Trust 전 항목에 대해 batch paired comparison 수행."""
    print("\n=== 일괄 Paired Comparison (Table 1 형식) ===")

    all_dvs = []
    all_labels = []
    combined_df = pd.DataFrame()

    # TLX
    if not tlx_df.empty:
        tlx_available = [i for i in NASA_TLX_ITEMS if i in tlx_df.columns]
        if "tlx_total" in tlx_df.columns:
            tlx_available.append("tlx_total")
        all_dvs.extend(tlx_available)
        all_labels.extend(
            [NASA_TLX_LABELS[NASA_TLX_ITEMS.index(i)] if i in NASA_TLX_ITEMS
             else "TLX Total" for i in tlx_available]
        )
        combined_df = tlx_df[["participant_id", "condition"] + tlx_available].copy()

    # Trust
    if not trust_df.empty:
        trust_available = [i for i in TRUST_ITEMS if i in trust_df.columns]
        if "trust_mean" in trust_df.columns:
            trust_available.append("trust_mean")
        all_dvs.extend(trust_available)
        all_labels.extend(
            [TRUST_LABELS[TRUST_ITEMS.index(i)] if i in TRUST_ITEMS
             else "Trust Mean" for i in trust_available]
        )
        trust_subset = trust_df[["participant_id", "condition"] + trust_available].copy()
        if not combined_df.empty:
            combined_df = combined_df.merge(trust_subset,
                                            on=["participant_id", "condition"],
                                            how="outer")
        else:
            combined_df = trust_subset

    if combined_df.empty or not all_dvs:
        print("  [경고] 분석 가능한 데이터 없음")
        return pd.DataFrame()

    batch_df = batch_paired_comparison(combined_df, all_dvs, bonferroni=True)

    # 레이블 매핑
    dv_to_label = dict(zip(all_dvs, all_labels))
    batch_df["label"] = batch_df["dv"].map(dv_to_label)

    # 콘솔 출력
    print(f"\n  {'DV':<20s} {'Glass M(SD)':<16s} {'Hybrid M(SD)':<16s} "
          f"{'test':<15s} {'p':<8s} {'d':<8s} {'sig':<5s}")
    print("  " + "-" * 88)
    for _, row in batch_df.iterrows():
        label = row.get("label", row["dv"])
        glass_str = f"{row['mean_a']:.2f}({row['sd_a']:.2f})" if not np.isnan(row["mean_a"]) else "—"
        hybrid_str = f"{row['mean_b']:.2f}({row['sd_b']:.2f})" if not np.isnan(row["mean_b"]) else "—"
        test_str = f"{row['test']}" if row["test"] != "N/A" else "—"
        p_str = f"{row['p']:.4f}" if not np.isnan(row["p"]) else "—"
        d_str = f"{row['d']:.2f}" if not np.isnan(row["d"]) else "—"
        sig_str = significance_marker(row["p"])
        print(f"  {label:<20s} {glass_str:<16s} {hybrid_str:<16s} "
              f"{test_str:<15s} {p_str:<8s} {d_str:<8s} {sig_str:<5s}")

    return batch_df


# ──────────────────────────────────────────────
# 12. Forest plot
# ──────────────────────────────────────────────

def plot_forest(batch_df: pd.DataFrame):
    """효과크기 Forest plot — NASA-TLX + Trust 전 항목."""
    if batch_df.empty:
        return

    from plot_style import forest_plot

    plot_df = batch_df.dropna(subset=["d"]).copy()
    if plot_df.empty:
        return

    # 레이블이 있으면 사용
    if "label" in plot_df.columns:
        plot_df = plot_df.rename(columns={"label": "dv_label"})
        label_col = "dv_label"
    else:
        label_col = "dv"

    fig, ax = plt.subplots(figsize=(7, max(3, len(plot_df) * 0.45)))
    forest_plot(
        ax, plot_df, label_col=label_col,
        title="Effect Size Forest Plot (Cohen's d, 95% CI)\nGlass Only vs Hybrid"
    )
    fig.tight_layout()
    save_fig(fig, OUTPUT_DIR / "survey_forest_plot")


# ──────────────────────────────────────────────
# 13. 메인
# ──────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="인앱 설문 분석")
    parser.add_argument("--fallback", action="store_true",
                        help="CSV 파일이 없을 때 fallback 데이터로 실행")
    args = parser.parse_args()

    print("=" * 60)
    print("인앱 설문 분석 (NASA-TLX + Trust + 비교 설문)")
    print("=" * 60)

    (OUTPUT_DIR / "csv").mkdir(exist_ok=True)

    # 데이터 로드
    df = load_all_events(allow_fallback=args.fallback)
    print(f"총 이벤트 수: {len(df)}")
    print(f"참가자 수: {df['participant_id'].nunique()}")

    # 설문 데이터 추출
    print("\n--- 설문 데이터 추출 ---")
    tlx_df = extract_nasa_tlx(df)
    trust_df = extract_trust(df)
    comp_df = extract_comparison_survey(df)

    # 분석
    tlx_results = analyze_nasa_tlx(tlx_df)
    trust_results = analyze_trust(trust_df)
    comp_results = analyze_comparison_survey(comp_df)

    # 일괄 통계
    batch_df = run_batch_analysis(tlx_df, trust_df)

    # 시각화
    print(f"\n=== 시각화 ===")
    plot_fig5_nasa_tlx_violin(tlx_df, tlx_results)
    plot_trust_violin(trust_df, trust_results)
    plot_fig9_diverging_likert(tlx_df, trust_df)
    plot_comparison_survey(comp_results)
    plot_forest(batch_df)

    # 결과 저장
    print(f"\n=== 결과 저장 ===")
    if not tlx_results.empty:
        tlx_results.to_csv(OUTPUT_DIR / "csv" / "inapp_nasa_tlx_summary.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'inapp_nasa_tlx_summary.csv'} 저장")

    if not trust_results.empty:
        trust_results.to_csv(OUTPUT_DIR / "csv" / "inapp_trust_summary.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'inapp_trust_summary.csv'} 저장")

    if comp_df is not None and not comp_df.empty:
        comp_df.to_csv(OUTPUT_DIR / "csv" / "inapp_comparison_survey.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'inapp_comparison_survey.csv'} 저장")

    if not batch_df.empty:
        batch_df.to_csv(OUTPUT_DIR / "csv" / "inapp_survey_batch_stats.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'inapp_survey_batch_stats.csv'} 저장")

    # TLX wide 데이터 저장 (재사용용)
    if not tlx_df.empty:
        tlx_df.to_csv(OUTPUT_DIR / "csv" / "inapp_nasa_tlx_wide.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'inapp_nasa_tlx_wide.csv'} 저장")

    if not trust_df.empty:
        trust_df.to_csv(OUTPUT_DIR / "csv" / "inapp_trust_wide.csv", index=False)
        print(f"  -> {OUTPUT_DIR / 'csv' / 'inapp_trust_wide.csv'} 저장")

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
