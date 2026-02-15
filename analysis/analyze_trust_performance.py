"""
신뢰 및 수행 분석 스크립트
- NASA-TLX 하위척도별 비교 (2조건: Glass Only vs Hybrid)
- 확신도 변화 궤적 (트리거 전후)
- 확신도-정확도 보정(calibration) 분석 (v2)
- 트리거 유형별 확신도 분석 (v2)
- 통계: Paired t-test / Wilcoxon signed-rank
"""

import os
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
TLX_LABELS_KR = [
    "정신적 요구", "신체적 요구", "시간적 압박",
    "수행", "노력", "좌절",
]


# ──────────────────────────────────────────────
# 2. 데이터 로드 / 데모 생성
# ──────────────────────────────────────────────

def load_nasa_tlx() -> pd.DataFrame:
    """NASA-TLX 설문 데이터 로드 또는 데모 생성."""
    path = SURVEY_DIR / "nasa_tlx.csv"
    if path.exists():
        return pd.read_csv(path)
    print(f"[경고] {path} 없음. 데모 데이터 생성.")
    return _generate_demo_tlx()


def _generate_demo_tlx() -> pd.DataFrame:
    rng = np.random.default_rng(42)
    rows = []
    means = {
        "glass_only":  {"mental_demand": 12, "physical_demand": 5, "temporal_demand": 9,
                        "performance": 8, "effort": 11, "frustration": 9},
        "hybrid":      {"mental_demand": 10, "physical_demand": 6, "temporal_demand": 8,
                        "performance": 5, "effort": 9, "frustration": 6},
    }
    for pid in range(1, N_PARTICIPANTS + 1):
        for cond in CONDITIONS:
            row = {"participant_id": f"P{pid:02d}", "condition": cond}
            for sub in TLX_SUBSCALES:
                val = rng.normal(means[cond][sub], 3)
                row[sub] = int(np.clip(round(val), 0, 21))
            rows.append(row)
    return pd.DataFrame(rows)


def load_trust_scale() -> pd.DataFrame:
    """시스템 신뢰 척도 데이터 로드 또는 데모 생성."""
    path = SURVEY_DIR / "trust_scale.csv"
    if path.exists():
        return pd.read_csv(path)
    print(f"[경고] {path} 없음. 데모 데이터 생성.")
    return _generate_demo_trust()


def _generate_demo_trust() -> pd.DataFrame:
    rng = np.random.default_rng(123)
    rows = []
    trust_means = {"glass_only": 4.2, "hybrid": 5.5}
    for pid in range(1, N_PARTICIPANTS + 1):
        for cond in CONDITIONS:
            row = {"participant_id": f"P{pid:02d}", "condition": cond}
            for q in range(1, 8):
                val = rng.normal(trust_means[cond], 0.9)
                row[f"trust_q{q}"] = int(np.clip(round(val), 1, 7))
            row["trust_mean"] = round(np.mean([row[f"trust_q{q}"] for q in range(1, 8)]), 2)
            rows.append(row)
    return pd.DataFrame(rows)


def load_confidence_from_events() -> pd.DataFrame:
    """이벤트 로그에서 확신도 데이터 추출 또는 데모 생성."""
    csv_files = sorted(RAW_DIR.glob("P*_*.csv"))
    if csv_files:
        frames = []
        for f in csv_files:
            df = pd.read_csv(f, parse_dates=["timestamp"])
            frames.append(df)
        all_events = pd.concat(frames, ignore_index=True)
        conf = all_events[all_events["event_type"] == "CONFIDENCE_RATED"].copy()
        conf["confidence_rating"] = pd.to_numeric(conf["confidence_rating"], errors="coerce")
        return conf[["participant_id", "condition", "waypoint_id", "confidence_rating"]].dropna()
    print(f"[경고] 이벤트 로그 없음. 데모 확신도 데이터 생성.")
    return _generate_demo_confidence()


def _generate_demo_confidence() -> pd.DataFrame:
    rng = np.random.default_rng(77)
    rows = []
    base_conf = {
        "glass_only":    [5.2, 5.5, 3.2, 4.5, 4.8, 3.0, 4.8, 5.0],
        "hybrid":        [5.8, 6.0, 4.8, 5.5, 5.7, 4.5, 5.7, 5.9],
    }
    for pid in range(1, N_PARTICIPANTS + 1):
        for cond in CONDITIONS:
            for i, wp in enumerate(WAYPOINTS):
                val = rng.normal(base_conf[cond][i], 0.7)
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
    for sub, label in zip(TLX_SUBSCALES, TLX_LABELS_KR):
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
            if isinstance(extra, str):
                try:
                    d = eval(extra) if extra else {}
                except Exception:
                    d = {}
            else:
                d = {}
            return d.get("correct", None)

        mc["correct"] = mc["extra_data"].apply(extract_correct)
        acc_df = mc[["participant_id", "condition", "waypoint_id", "correct"]].dropna()
    else:
        rng = np.random.default_rng(99)
        acc_rows = []
        acc_base = {"glass_only": 0.60, "hybrid": 0.88}
        mission_wps = ["WP02", "WP03", "WP05", "WP06", "WP08"]
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
        print("  [경고] TRIGGER_ACTIVATED 이벤트 없음, 데모 요약 출력")
        rng = np.random.default_rng(55)
        for tt in TRIGGER_TYPES:
            drop = rng.normal(-1.2, 0.4)
            rt = rng.uniform(3, 8)
            print(f"  {tt}: 확신도 변화 = {drop:+.1f}, 반응시간 = {rt:.1f}s")
        return

    triggers = events_df[events_df["event_type"] == "TRIGGER_ACTIVATED"].copy()

    def extract_trigger_type(extra):
        if isinstance(extra, str):
            try:
                d = eval(extra) if extra else {}
            except Exception:
                d = {}
        else:
            d = {}
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
# 5c. 정보 접근량 vs NASA-TLX 관계 분석 — v2.1
# ──────────────────────────────────────────────

def analyze_information_load_tlx(tlx_df: pd.DataFrame, events_df: pd.DataFrame = None):
    """Beam Pro 정보 접근량과 NASA-TLX 관계 분석 (v2.1)."""
    print("\n=== 정보 접근량 vs NASA-TLX 분석 (v2.1) ===")

    if events_df is None:
        print("  [경고] 이벤트 데이터 없음, 데모 분석 수행")
        rng = np.random.default_rng(77)
        content_counts = pd.DataFrame({
            "participant_id": [f"P{i:02d}" for i in range(1, N_PARTICIPANTS + 1)],
            "content_access_count": rng.integers(3, 25, size=N_PARTICIPANTS),
        })
    elif "beam_content_type" not in events_df.columns:
        print("  [경고] beam_content_type 컬럼 없음, 데모 분석 수행")
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
        test = pg.pairwise_tests(
            data=data, dv=dv, within="condition", subject="participant_id",
            parametric=True
        )
        if not test.empty:
            row = test.iloc[0]
            print(f"    Paired t-test ({label}): t={row['T']:.2f}, p={row['p-unc']:.4f}, "
                  f"d={row['hedges']:.2f}")
    except ImportError:
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
    """NASA-TLX 하위척도별 조건 비교 그룹 바 차트."""
    fig, ax = plt.subplots(figsize=(12, 6))

    x = np.arange(len(TLX_SUBSCALES))
    width = 0.35

    for i, (cond, label) in enumerate(zip(CONDITIONS, CONDITION_LABELS)):
        means = [tlx_df[tlx_df["condition"] == cond][sub].mean() for sub in TLX_SUBSCALES]
        sds = [tlx_df[tlx_df["condition"] == cond][sub].std() for sub in TLX_SUBSCALES]
        ax.bar(x + i * width, means, width, yerr=sds, label=label, capsize=3)

    ax.set_xlabel("하위척도")
    ax.set_ylabel("점수 (0-21)")
    ax.set_title("NASA-TLX 하위척도별 조건 비교")
    ax.set_xticks(x + width / 2)
    ax.set_xticklabels(TLX_LABELS_KR, rotation=15)
    ax.legend()
    ax.set_ylim(0, 21)
    fig.tight_layout()
    fig.savefig(OUTPUT_DIR / "nasa_tlx_comparison.png", dpi=150)
    print(f"  → {OUTPUT_DIR / 'nasa_tlx_comparison.png'} 저장")
    plt.close(fig)


def plot_trust_comparison(trust_df: pd.DataFrame):
    """시스템 신뢰 척도 조건별 비교 boxplot."""
    fig, ax = plt.subplots(figsize=(7, 5))
    data = [trust_df[trust_df["condition"] == c]["trust_mean"].values for c in CONDITIONS]
    ax.boxplot(data, tick_labels=CONDITION_LABELS)
    ax.set_ylabel("시스템 신뢰 점수 (1-7)")
    ax.set_title("조건별 시스템 신뢰 비교")
    ax.set_ylim(1, 7)
    fig.tight_layout()
    fig.savefig(OUTPUT_DIR / "trust_comparison.png", dpi=150)
    print(f"  → {OUTPUT_DIR / 'trust_comparison.png'} 저장")
    plt.close(fig)


def plot_confidence_trajectory(pivot: pd.DataFrame):
    """확신도 변화 궤적 라인 차트."""
    fig, ax = plt.subplots(figsize=(10, 6))

    colors = {"glass_only": "#3498db", "hybrid": "#2ecc71"}

    for cond, label in zip(CONDITIONS, CONDITION_LABELS):
        subset = pivot[pivot["condition"] == cond].sort_values("waypoint_id")
        ax.plot(subset["waypoint_id"], subset["confidence_rating"],
                "o-", label=label, color=colors[cond], linewidth=2, markersize=8)

    for tw in TRIGGER_WAYPOINTS:
        ax.axvline(x=tw, color="gray", linestyle="--", alpha=0.5)
        ax.text(tw, 6.8, "트리거", ha="center", fontsize=9, color="gray")

    ax.set_xlabel("웨이포인트")
    ax.set_ylabel("평균 확신도 (1-7)")
    ax.set_title("조건별 확신도 변화 궤적")
    ax.set_ylim(1, 7)
    ax.legend()
    ax.grid(axis="y", alpha=0.3)
    fig.tight_layout()
    fig.savefig(OUTPUT_DIR / "confidence_trajectory.png", dpi=150)
    print(f"  → {OUTPUT_DIR / 'confidence_trajectory.png'} 저장")
    plt.close(fig)


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
        axes[idx].set_ylabel("확신도 변화 (Δ)")
        axes[idx].set_title(f"트리거 {trigger_wp}: {pre_wp} → {trigger_wp} 확신도 변화")

    fig.tight_layout()
    fig.savefig(OUTPUT_DIR / "confidence_drop.png", dpi=150)
    print(f"  → {OUTPUT_DIR / 'confidence_drop.png'} 저장")
    plt.close(fig)


# ──────────────────────────────────────────────
# 8. 메인
# ──────────────────────────────────────────────

def main():
    print("=" * 60)
    print("신뢰 및 수행 분석")
    print("=" * 60)

    # 데이터 로드
    tlx_df = load_nasa_tlx()
    trust_df = load_trust_scale()
    conf_df = load_confidence_from_events()

    print(f"NASA-TLX: {len(tlx_df)} rows ({tlx_df['participant_id'].nunique()} 참가자)")
    print(f"신뢰 척도: {len(trust_df)} rows ({trust_df['participant_id'].nunique()} 참가자)")
    print(f"확신도: {len(conf_df)} rows ({conf_df['participant_id'].nunique()} 참가자)")

    # 분석
    tlx_results = analyze_nasa_tlx(tlx_df)
    analyze_trust(trust_df)
    pivot = analyze_confidence_trajectory(conf_df)

    # 이벤트 데이터 로드 (v2.1 콘텐츠 분석용)
    csv_files = sorted(RAW_DIR.glob("P*_*.csv"))
    events_df = None
    if csv_files:
        frames = [pd.read_csv(f, parse_dates=["timestamp"]) for f in csv_files]
        events_df = pd.concat(frames, ignore_index=True)

    # v2: calibration 분석
    cal_df = analyze_calibration(conf_df, events_df)
    analyze_trigger_type_effects(events_df)

    # v2.1: 정보 접근량-TLX 분석
    analyze_information_load_tlx(tlx_df, events_df)

    # 시각화
    print(f"\n=== 시각화 ===")
    plot_tlx_comparison(tlx_df)
    plot_trust_comparison(trust_df)
    plot_confidence_trajectory(pivot)
    plot_confidence_drop(conf_df)

    # 결과 저장
    tlx_results.to_csv(OUTPUT_DIR / "nasa_tlx_summary.csv", index=False)
    print(f"  → {OUTPUT_DIR / 'nasa_tlx_summary.csv'} 저장")

    trust_summary = trust_df.groupby("condition")["trust_mean"].agg(["mean", "std"]).reset_index()
    trust_summary.to_csv(OUTPUT_DIR / "trust_summary.csv", index=False)
    print(f"  → {OUTPUT_DIR / 'trust_summary.csv'} 저장")

    if not cal_df.empty:
        cal_df.to_csv(OUTPUT_DIR / "calibration_summary.csv", index=False)
        print(f"  → {OUTPUT_DIR / 'calibration_summary.csv'} 저장")

    print("\n분석 완료.")


if __name__ == "__main__":
    main()
