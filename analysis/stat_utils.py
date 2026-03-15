"""
공통 통계 유틸리티 — CHI/UIST 논문 수준의 통계적 엄밀성 지원.

주요 기능:
  - 정규성 검정 → 모수/비모수 자동 선택
  - Paired comparison (paired t-test / Wilcoxon) + Cohen's d + 95% CI
  - Mixed-effects model 래퍼 (순서 효과 통제)
  - 효과크기 일괄 산출 (Forest plot용)
  - 유의성 마커 생성
"""

import warnings
from typing import Dict, List, Optional, Tuple

import numpy as np
import pandas as pd
from scipy import stats

warnings.filterwarnings("ignore", category=FutureWarning)


# ──────────────────────────────────────────────
# 효과크기
# ──────────────────────────────────────────────

def cohens_d_paired(x: np.ndarray, y: np.ndarray) -> float:
    """Paired samples에 대한 Cohen's d_z (within-subjects).

    d_z = mean(diff) / sd(diff)
    """
    diff = x - y
    sd = np.std(diff, ddof=1)
    if sd == 0:
        return 0.0
    return float(np.mean(diff) / sd)


def cohens_d_between(x: np.ndarray, y: np.ndarray) -> float:
    """Independent samples에 대한 Cohen's d (pooled SD)."""
    n1, n2 = len(x), len(y)
    var1, var2 = np.var(x, ddof=1), np.var(y, ddof=1)
    pooled_sd = np.sqrt(((n1 - 1) * var1 + (n2 - 1) * var2) / (n1 + n2 - 2))
    if pooled_sd == 0:
        return 0.0
    return float((np.mean(x) - np.mean(y)) / pooled_sd)


def cohens_d_ci(d: float, n: int, alpha: float = 0.05) -> Tuple[float, float]:
    """Cohen's d의 근사 95% CI.

    se(d) ≈ sqrt(1/n + d²/(2n))
    """
    se = np.sqrt(1.0 / n + d ** 2 / (2.0 * n))
    z = stats.norm.ppf(1 - alpha / 2)
    return (d - z * se, d + z * se)


# ──────────────────────────────────────────────
# 정규성 검정
# ──────────────────────────────────────────────

def check_normality(x: np.ndarray, alpha: float = 0.05) -> Tuple[bool, float]:
    """Shapiro-Wilk 검정으로 정규성 판단.

    Returns:
        (is_normal, p_value)
    """
    x = np.asarray(x, dtype=float)
    x = x[~np.isnan(x)]
    if len(x) < 3:
        return True, 1.0  # 데이터 부족 시 모수 가정
    if len(x) > 5000:
        x = np.random.default_rng(42).choice(x, 5000, replace=False)
    stat, p = stats.shapiro(x)
    return p >= alpha, p


# ──────────────────────────────────────────────
# Paired comparison (핵심 함수)
# ──────────────────────────────────────────────

def paired_comparison(
    data: pd.DataFrame,
    dv: str,
    condition_col: str = "condition",
    subject_col: str = "participant_id",
    cond_a: str = "glass_only",
    cond_b: str = "hybrid",
    alpha: float = 0.05,
) -> Dict:
    """2조건 within-subjects 비교: 정규성 자동 판단 → 모수/비모수 선택.

    Returns:
        dict with keys: test, statistic, p, d, d_ci_lo, d_ci_hi,
                        mean_a, sd_a, mean_b, sd_b, n, significant
    """
    # Aggregate by subject (mean) to handle multiple rows per subject
    a_data = data.loc[data[condition_col] == cond_a].groupby(subject_col)[dv].mean()
    b_data = data.loc[data[condition_col] == cond_b].groupby(subject_col)[dv].mean()
    common = a_data.index.intersection(b_data.index)

    if len(common) < 3:
        return _empty_result(dv)

    x = a_data.loc[common].values.astype(float)
    y = b_data.loc[common].values.astype(float)

    # 결측치 제거
    valid = ~(np.isnan(x) | np.isnan(y))
    x, y = x[valid], y[valid]
    n = len(x)
    if n < 3:
        return _empty_result(dv)

    diff = x - y
    is_normal, norm_p = check_normality(diff)

    if is_normal:
        t_stat, p_val = stats.ttest_rel(x, y)
        test_name = "paired t-test"
        statistic = float(t_stat)
    else:
        w_stat, p_val = stats.wilcoxon(x, y)
        test_name = "Wilcoxon"
        statistic = float(w_stat)

    d = cohens_d_paired(x, y)
    d_lo, d_hi = cohens_d_ci(d, n)

    return {
        "dv": dv,
        "test": test_name,
        "statistic": round(statistic, 3),
        "p": round(float(p_val), 4),
        "d": round(d, 3),
        "d_ci_lo": round(d_lo, 3),
        "d_ci_hi": round(d_hi, 3),
        "mean_a": round(float(np.mean(x)), 3),
        "sd_a": round(float(np.std(x, ddof=1)), 3),
        "mean_b": round(float(np.mean(y)), 3),
        "sd_b": round(float(np.std(y, ddof=1)), 3),
        "n": n,
        "normality_p": round(float(norm_p), 4),
        "significant": float(p_val) < alpha,
    }


def _empty_result(dv: str) -> Dict:
    return {
        "dv": dv, "test": "N/A", "statistic": np.nan, "p": np.nan,
        "d": np.nan, "d_ci_lo": np.nan, "d_ci_hi": np.nan,
        "mean_a": np.nan, "sd_a": np.nan, "mean_b": np.nan, "sd_b": np.nan,
        "n": 0, "normality_p": np.nan, "significant": False,
    }


# ──────────────────────────────────────────────
# 다중 DV 일괄 비교 (Table 1 / Forest plot용)
# ──────────────────────────────────────────────

def batch_paired_comparison(
    data: pd.DataFrame,
    dvs: List[str],
    condition_col: str = "condition",
    subject_col: str = "participant_id",
    cond_a: str = "glass_only",
    cond_b: str = "hybrid",
    bonferroni: bool = False,
) -> pd.DataFrame:
    """여러 종속변수에 대해 paired_comparison 일괄 수행.

    bonferroni=True이면 Bonferroni 보정 적용 (p * n_tests).
    """
    results = []
    for dv in dvs:
        if dv not in data.columns:
            results.append(_empty_result(dv))
            continue
        r = paired_comparison(data, dv, condition_col, subject_col, cond_a, cond_b)
        results.append(r)

    df = pd.DataFrame(results)

    if bonferroni and len(df) > 1:
        df["p_corrected"] = (df["p"] * len(df)).clip(upper=1.0)
        df["significant_corrected"] = df["p_corrected"] < 0.05
    else:
        df["p_corrected"] = df["p"]
        df["significant_corrected"] = df["significant"]

    return df


# ──────────────────────────────────────────────
# 순서 효과 검증 (Mixed ANOVA)
# ──────────────────────────────────────────────

def test_order_effect(
    data: pd.DataFrame,
    dv: str,
    condition_col: str = "condition",
    order_col: str = "order_group",
    subject_col: str = "participant_id",
) -> Optional[Dict]:
    """Condition x Order Mixed ANOVA (pingouin 사용).

    Returns:
        dict with ANOVA table or None if pingouin not available
    """
    try:
        import pingouin as pg
    except ImportError:
        print("  [경고] pingouin 미설치, 순서 효과 검증 불가")
        return None

    required_cols = [dv, condition_col, order_col, subject_col]
    if not all(c in data.columns for c in required_cols):
        return None

    clean = data.dropna(subset=[dv])
    if clean[order_col].nunique() < 2 or clean[condition_col].nunique() < 2:
        return None

    try:
        aov = pg.mixed_anova(
            data=clean, dv=dv, within=condition_col,
            between=order_col, subject=subject_col
        )
        result = {}
        for _, row in aov.iterrows():
            source = row["Source"]
            result[source] = {
                "F": round(row["F"], 3),
                "p": round(row.get("p-unc", row.get("p_unc", np.nan)), 4),
                "np2": round(row["np2"], 3),
                "df1": int(row["DF1"]) if "DF1" in row else None,
                "df2": int(row["DF2"]) if "DF2" in row else None,
            }
        return result
    except Exception as e:
        print(f"  [경고] Mixed ANOVA 실패 ({dv}): {e}")
        return None


# ──────────────────────────────────────────────
# 유의성 마커
# ──────────────────────────────────────────────

def significance_marker(p: float) -> str:
    """p값에 따른 유의성 마커 반환."""
    if np.isnan(p):
        return ""
    if p < 0.001:
        return "***"
    if p < 0.01:
        return "**"
    if p < 0.05:
        return "*"
    return "n.s."


def add_significance_bracket(
    ax, x1: float, x2: float, y: float, p: float,
    height: float = 0.02, text_offset: float = 0.01,
):
    """두 바/바이올린 사이에 유의성 브래킷 추가."""
    marker = significance_marker(p)
    if not marker or marker == "n.s.":
        return

    # y를 데이터 좌표에서의 상대 높이로 사용
    ax.plot([x1, x1, x2, x2], [y, y + height, y + height, y],
            lw=1.2, color="black")
    ax.text((x1 + x2) / 2, y + height + text_offset, marker,
            ha="center", va="bottom", fontsize=11, fontweight="bold")


# ──────────────────────────────────────────────
# 포맷팅 헬퍼
# ──────────────────────────────────────────────

def format_p(p: float) -> str:
    """p값을 APA 형식으로 포맷."""
    if np.isnan(p):
        return "—"
    if p < 0.001:
        return "< .001"
    return f"= .{str(round(p, 3))[2:]}"


def format_ci(lo: float, hi: float) -> str:
    """95% CI를 '[lo, hi]' 형식으로 포맷."""
    if np.isnan(lo) or np.isnan(hi):
        return "—"
    return f"[{lo:.2f}, {hi:.2f}]"


def format_stat_line(result: Dict) -> str:
    """통계 결과를 한 줄 요약 문자열로 포맷."""
    test = result["test"]
    stat = result["statistic"]
    p = result["p"]
    d = result["d"]
    marker = significance_marker(p)

    if test == "paired t-test":
        return f"t({result['n']-1}) = {stat:.2f}, p {format_p(p)}, d = {d:.2f} {marker}"
    elif test == "Wilcoxon":
        return f"W = {stat:.0f}, p {format_p(p)}, d = {d:.2f} {marker}"
    else:
        return "N/A"
