"""
ISMAR 2026 논문 Figure 제작 스크립트.

Fig 3: Device & Interface (6-panel image composite)
Fig 5: Uncertainty Trigger Examples (4-panel)
Fig 6: Main Results (accuracy, completion time, TLX)
Fig 7: Cross-Verification & Device Switching
Fig 8: Trust & Effect Size Forest Plot

실행: python3 analysis/paper_figures.py
출력: paper/figures/fig{3,5,6,7,8}_*.pdf
"""

import sys
from pathlib import Path

import numpy as np
import pandas as pd
import matplotlib
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch
from matplotlib.image import imread

# ── 경로 설정 ──────────────────────────────────
ROOT = Path(__file__).resolve().parent.parent
CSV_DIR = ROOT / "analysis" / "output" / "csv"
FIG_DIR = ROOT / "paper" / "figures"
FIG_DIR.mkdir(parents=True, exist_ok=True)

REF_FIG3 = ROOT / "data" / "ref_figure" / "fig3"
FRAMES_FIG3 = ROOT / "data" / "frames_fig3"
FRAMES_FIG5 = ROOT / "data" / "frames_fig5"

# ── 논문 스타일 ────────────────────────────────
COLOR_GLASS = "#4A90D9"
COLOR_HYBRID = "#50B86C"
COLORS_COND = [COLOR_GLASS, COLOR_HYBRID]
COND_LABELS = ["Glass Only", "Hybrid"]
COLOR_SIG = "#333333"

PAPER_STYLE = {
    "font.family": "sans-serif",
    "font.size": 8,
    "axes.titlesize": 9,
    "axes.titleweight": "bold",
    "axes.labelsize": 8,
    "axes.linewidth": 0.6,
    "axes.grid": False,
    "xtick.labelsize": 7,
    "ytick.labelsize": 7,
    "xtick.major.width": 0.6,
    "ytick.major.width": 0.6,
    "legend.fontsize": 7,
    "legend.framealpha": 0.8,
    "figure.dpi": 300,
    "savefig.dpi": 300,
    "savefig.bbox": "tight",
    "savefig.pad_inches": 0.05,
    "axes.unicode_minus": False,
    "axes.spines.top": False,
    "axes.spines.right": False,
}

# ISMAR column widths
FIG_SINGLE = (3.33, 2.5)
FIG_DOUBLE = (7.0, 3.5)


def apply_paper_style():
    matplotlib.rcParams.update(PAPER_STYLE)


# ── 유틸리티 ───────────────────────────────────

def _significance_marker(p):
    if pd.isna(p):
        return ""
    if p < 0.001:
        return "***"
    if p < 0.01:
        return "**"
    if p < 0.05:
        return "*"
    return ""


def _add_bracket(ax, x1, x2, y, p, height=None):
    marker = _significance_marker(p)
    if not marker:
        return
    if height is None:
        height = (ax.get_ylim()[1] - ax.get_ylim()[0]) * 0.03
    ax.plot([x1, x1, x2, x2], [y, y + height, y + height, y],
            lw=1.0, color="black")
    ax.text((x1 + x2) / 2, y + height * 1.2, marker,
            ha="center", va="bottom", fontsize=9, fontweight="bold")


def _save(fig, name):
    for fmt in ("pdf", "png"):
        out = FIG_DIR / f"{name}.{fmt}"
        fig.savefig(out, format=fmt)
    print(f"  -> {name} saved (pdf, png)")
    plt.close(fig)


def _load_csv(name):
    path = CSV_DIR / name
    if not path.exists():
        print(f"  [WARN] {name} not found")
        return None
    return pd.read_csv(path)


def _violin(ax, data_glass, data_hybrid, ylabel="", title="", p_value=None):
    """Compact violin + dots for two conditions."""
    for i, (d, color, label) in enumerate(
        [(data_glass, COLOR_GLASS, "Glass Only"),
         (data_hybrid, COLOR_HYBRID, "Hybrid")]
    ):
        d = np.asarray(d, dtype=float)
        d = d[~np.isnan(d)]
        if len(d) < 2:
            continue
        parts = ax.violinplot([d], positions=[i], showmeans=False,
                              showmedians=False, showextrema=False)
        for pc in parts["bodies"]:
            pc.set_facecolor(color)
            pc.set_alpha(0.25)
            pc.set_edgecolor(color)
            pc.set_linewidth(0.8)

        q1, med, q3 = np.percentile(d, [25, 50, 75])
        ax.vlines(i, q1, q3, color=color, linewidth=2.5, alpha=0.7)
        ax.scatter(i, med, color="white", s=25, zorder=4,
                   edgecolors=color, linewidth=1.2)

        jitter = np.random.default_rng(42 + i).uniform(-0.06, 0.06, len(d))
        ax.scatter(i + jitter, d, s=15, alpha=0.5, color=color,
                   edgecolors="white", linewidth=0.3, zorder=3)

        mean_val = np.mean(d)
        ax.scatter(i, mean_val, marker="D", s=30, color=color,
                   edgecolors="black", linewidth=0.6, zorder=5)

    ax.set_xticks([0, 1])
    ax.set_xticklabels(COND_LABELS)
    if ylabel:
        ax.set_ylabel(ylabel)
    if title:
        ax.set_title(title)

    if p_value is not None:
        all_d = np.concatenate([
            np.asarray(data_glass, dtype=float)[~np.isnan(np.asarray(data_glass, dtype=float))],
            np.asarray(data_hybrid, dtype=float)[~np.isnan(np.asarray(data_hybrid, dtype=float))]
        ])
        if len(all_d) > 0:
            y_max = np.max(all_d)
            y_range = np.ptp(all_d)
            _add_bracket(ax, 0, 1, y_max + y_range * 0.05, p_value)


# ═══════════════════════════════════════════════
# Fig 3: Device & Interface (6-panel composite)
# ═══════════════════════════════════════════════

def compose_fig3():
    print("\n=== Fig 3: Device & Interface ===")

    panels = {
        "a": ("XREAL Air2 Ultra", REF_FIG3 / "IMG_2725-removebg-preview.png"),
        "b": ("Beam Pro Controller", REF_FIG3 / "IMG_2727-removebg-preview.png"),
        "c": ("Through-the-Lens View", REF_FIG3 / "IMG_2732.JPG"),
    }

    # Glass UI frames (d, e, f)
    frame_panels = {
        "d": ("Glass-Only Nav HUD", FRAMES_FIG3 / "glass_nav_hud.png"),
        "e": ("In-Mission Rating", FRAMES_FIG3 / "rating_ui.png"),
        "f": ("Hybrid Mode HUD", FRAMES_FIG3 / "hybrid_hud.png"),
    }

    all_panels = {}
    for key, (label, path) in {**panels, **frame_panels}.items():
        if path.exists():
            all_panels[key] = (label, path)
        else:
            print(f"  [SKIP] Panel ({key}): {path.name} not found")

    if len(all_panels) < 3:
        print("  Not enough panels, skipping Fig 3")
        return

    n_panels = len(all_panels)
    ncols = 3
    nrows = (n_panels + ncols - 1) // ncols

    fig, axes = plt.subplots(nrows, ncols, figsize=(7.0, 2.3 * nrows))
    axes = np.atleast_2d(axes)

    for idx, (key, (label, path)) in enumerate(sorted(all_panels.items())):
        r, c = divmod(idx, ncols)
        ax = axes[r, c]
        img = imread(str(path))
        ax.imshow(img)
        ax.set_title(f"({key}) {label}", fontsize=7, pad=3)
        ax.axis("off")

    # Hide unused axes
    for idx in range(n_panels, nrows * ncols):
        r, c = divmod(idx, ncols)
        axes[r, c].axis("off")

    fig.tight_layout(pad=0.5)
    _save(fig, "fig3_device_interface")


# ═══════════════════════════════════════════════
# Fig 5: Uncertainty Trigger Examples (4-panel)
# ═══════════════════════════════════════════════

def compose_fig5():
    print("\n=== Fig 5: Trigger Examples ===")

    trigger_info = {
        "T1": ("Guidance Degradation", "Arrow quality degrades\n(reduced update rate)"),
        "T2": ("Information Mismatch", "Displayed info conflicts\nwith environment"),
        "T3": ("Resolution Deficit", "Arrow insufficient for\ndecision point"),
        "T4": ("Guidance Absence", "Navigation arrow\ndisappears completely"),
    }

    # Look for extracted frames
    frames = {}
    for tid in ["T1", "T2", "T3", "T4"]:
        for pat in [f"{tid.lower()}.png", f"{tid.lower()}_*.png", f"trigger_{tid.lower()}.png"]:
            matches = list(FRAMES_FIG5.glob(pat))
            if matches:
                frames[tid] = matches[0]
                break

    if not frames:
        # Try numbered frames from video extraction
        all_frames = sorted(FRAMES_FIG5.glob("frame_*.png"))
        if len(all_frames) >= 4:
            print(f"  Found {len(all_frames)} extracted frames, using first 4 as placeholders")
            for i, tid in enumerate(["T1", "T2", "T3", "T4"]):
                idx = int(len(all_frames) * (i + 1) / 5)
                frames[tid] = all_frames[min(idx, len(all_frames) - 1)]

    if not frames:
        print("  No trigger frames found, skipping Fig 5")
        print("  Place frames as data/frames_fig5/t1.png ... t4.png")
        return

    fig, axes = plt.subplots(1, 4, figsize=(7.0, 2.0))
    for i, (tid, (name, desc)) in enumerate(trigger_info.items()):
        ax = axes[i]
        if tid in frames:
            img = imread(str(frames[tid]))
            ax.imshow(img)
        ax.set_title(f"({chr(97+i)}) {tid}: {name}", fontsize=6, pad=3)
        ax.axis("off")
        # Add description annotation at bottom
        if tid in frames:
            ax.text(0.5, -0.02, desc, transform=ax.transAxes,
                    ha="center", va="top", fontsize=5.5,
                    bbox=dict(boxstyle="round,pad=0.3", facecolor="white",
                              alpha=0.8, edgecolor="#cccccc"))

    fig.tight_layout(pad=0.5)
    _save(fig, "fig5_trigger_examples")


# ═══════════════════════════════════════════════
# Fig 6: Main Results
# ═══════════════════════════════════════════════

def plot_fig6():
    print("\n=== Fig 6: Main Results ===")

    dv = _load_csv("comprehensive_dv_data.csv")
    acc_type = _load_csv("mission_accuracy_by_type.csv")
    batch = _load_csv("inapp_survey_batch_stats.csv")
    table1 = _load_csv("table1_dv_summary.csv")

    if dv is None:
        print("  comprehensive_dv_data.csv required, skipping")
        return

    fig, axes = plt.subplots(1, 3, figsize=(7.0, 2.8))

    # --- (a) Verification Accuracy by Mission Type ---
    ax = axes[0]
    if acc_type is not None and len(acc_type) > 0:
        types = sorted(acc_type["mission_type"].unique())
        x = np.arange(len(types))
        w = 0.35
        for ci, cond in enumerate(["Glass Only", "Hybrid"]):
            sub = acc_type[acc_type["condition"] == cond]
            vals = [sub.loc[sub["mission_type"] == t, "accuracy"].values[0]
                    if t in sub["mission_type"].values else 0 for t in types]
            ns = [sub.loc[sub["mission_type"] == t, "n"].values[0]
                  if t in sub["mission_type"].values else 0 for t in types]
            bars = ax.bar(x + ci * w - w / 2, vals, w, color=COLORS_COND[ci],
                          label=COND_LABELS[ci], edgecolor="white", linewidth=0.5)
            for bar, n in zip(bars, ns):
                if n > 0:
                    ax.text(bar.get_x() + bar.get_width() / 2, bar.get_height() + 0.02,
                            f"n={n}", ha="center", va="bottom", fontsize=5)

        ax.set_xticks(x)
        ax.set_xticklabels([f"Type {t}" for t in types])
        ax.set_ylabel("Accuracy")
        ax.set_ylim(0, 1.15)
        ax.set_title("(a) Verification Accuracy")
        ax.legend(fontsize=6, loc="lower right")
    else:
        ax.text(0.5, 0.5, "No accuracy data", transform=ax.transAxes, ha="center")
        ax.set_title("(a) Verification Accuracy")

    # --- (b) Task Completion Time ---
    ax = axes[1]
    glass_time = dv.loc[dv["condition"] == "glass_only", "completion_time"].dropna().values
    hybrid_time = dv.loc[dv["condition"] == "hybrid", "completion_time"].dropna().values

    # Get p-value from table1 if available
    p_time = None
    if table1 is not None:
        row = table1[table1["dv"] == "completion_time"]
        if len(row) > 0:
            p_time = row["p"].values[0]

    _violin(ax, glass_time, hybrid_time,
            ylabel="Time (s)", title="(b) Completion Time", p_value=p_time)

    # --- (c) NASA-TLX Total ---
    ax = axes[2]
    glass_tlx = dv.loc[dv["condition"] == "glass_only", "tlx_total"].dropna().values
    hybrid_tlx = dv.loc[dv["condition"] == "hybrid", "tlx_total"].dropna().values

    p_tlx = None
    if batch is not None:
        row = batch[batch["dv"] == "tlx_total"]
        if len(row) > 0:
            p_tlx = row["p"].values[0]

    _violin(ax, glass_tlx, hybrid_tlx,
            ylabel="TLX Score (1-7)", title="(c) NASA-TLX Total", p_value=p_tlx)

    fig.tight_layout(w_pad=1.5)
    _save(fig, "fig6_main_results")


# ═══════════════════════════════════════════════
# Fig 7: Cross-Verification & Device Switching
# ═══════════════════════════════════════════════

def plot_fig7():
    print("\n=== Fig 7: Cross-Verification & Switching ===")

    dv = _load_csv("comprehensive_dv_data.csv")
    trigger = _load_csv("trigger_switch_rate.csv")
    table1 = _load_csv("table1_dv_summary.csv")

    if dv is None:
        print("  comprehensive_dv_data.csv required, skipping")
        return

    fig, axes = plt.subplots(1, 2, figsize=(7.0, 2.8))

    # --- (a) Device Switching Frequency ---
    ax = axes[0]

    # Only Hybrid has switching; show both for comparison
    glass_switch = dv.loc[dv["condition"] == "glass_only", "switching_count"].dropna().values
    hybrid_switch = dv.loc[dv["condition"] == "hybrid", "switching_count"].dropna().values

    p_switch = None
    if table1 is not None:
        row = table1[table1["dv"] == "switching_count"]
        if len(row) > 0:
            p_switch = row["p"].values[0]

    _violin(ax, glass_switch, hybrid_switch,
            ylabel="Switch Count", title="(a) Beam Pro Reference Count",
            p_value=p_switch)

    # --- (b) Trigger-Associated Switching ---
    ax = axes[1]
    if trigger is not None and trigger["n"].sum() > 0:
        tids = trigger["trigger_label"].values
        rates = trigger["switch_rate"].values
        colors_bar = ["#E8927C", "#7CC6E8", "#A8D88E", "#D4A5D8"]
        bars = ax.bar(range(len(tids)), rates, color=colors_bar[:len(tids)],
                      edgecolor="white", linewidth=0.5)
        ax.set_xticks(range(len(tids)))
        ax.set_xticklabels(tids, rotation=25, ha="right", fontsize=6)
        ax.set_ylabel("Switch Rate")
        ax.set_title("(b) Trigger-Associated Switching")
        ax.set_ylim(0, 1.0)
    else:
        # Use Beam Pro total time as alternative
        glass_beam = dv.loc[dv["condition"] == "glass_only", "beam_total_time"].dropna().values
        hybrid_beam = dv.loc[dv["condition"] == "hybrid", "beam_total_time"].dropna().values

        p_beam = None
        if table1 is not None:
            row = table1[table1["dv"] == "beam_total_time"]
            if len(row) > 0:
                p_beam = row["p"].values[0]

        _violin(ax, glass_beam, hybrid_beam,
                ylabel="Time (s)", title="(b) Beam Pro Usage Time",
                p_value=p_beam)

    fig.tight_layout(w_pad=1.5)
    _save(fig, "fig7_crossverification")


# ═══════════════════════════════════════════════
# Fig 8: Trust & Forest Plot
# ═══════════════════════════════════════════════

def plot_fig8():
    print("\n=== Fig 8: Trust & Effect Size ===")

    trust = _load_csv("inapp_trust_wide.csv")
    batch = _load_csv("inapp_survey_batch_stats.csv")
    table1 = _load_csv("table1_dv_summary.csv")

    fig, axes = plt.subplots(1, 2, figsize=(7.0, 3.5))

    # --- (a) Trust Mean Comparison ---
    ax = axes[0]
    if trust is not None:
        glass_trust = trust.loc[trust["condition"] == "glass_only", "trust_mean"].dropna().values
        hybrid_trust = trust.loc[trust["condition"] == "hybrid", "trust_mean"].dropna().values

        p_trust = None
        if batch is not None:
            row = batch[batch["dv"] == "trust_mean"]
            if len(row) > 0:
                p_trust = row["p"].values[0]

        _violin(ax, glass_trust, hybrid_trust,
                ylabel="Trust Score (1-7)", title="(a) Overall Trust",
                p_value=p_trust)
    else:
        ax.text(0.5, 0.5, "No trust data", transform=ax.transAxes, ha="center")
        ax.set_title("(a) Overall Trust")

    # --- (b) Forest Plot ---
    ax = axes[1]

    # Combine batch stats from both sources, prefer table1 for broader DVs
    if table1 is not None:
        df = table1.copy()
    elif batch is not None:
        df = batch.copy()
    else:
        ax.text(0.5, 0.5, "No batch stats", transform=ax.transAxes, ha="center")
        ax.set_title("(b) Effect Sizes")
        fig.tight_layout(w_pad=2.0)
        _save(fig, "fig8_trust_forestplot")
        return

    # Filter to key DVs and clean
    df = df.dropna(subset=["d", "d_ci_lo", "d_ci_hi"]).copy()
    if "label" in df.columns:
        df = df.rename(columns={"label": "dv_label"})
    else:
        df["dv_label"] = df["dv"]

    # Sort by effect size
    df = df.sort_values("d", ascending=True).reset_index(drop=True)
    n = len(df)

    for i, row in df.iterrows():
        d = row["d"]
        lo, hi = row["d_ci_lo"], row["d_ci_hi"]
        p = row["p"]

        color = COLOR_GLASS if d > 0 else COLOR_HYBRID
        if p >= 0.05:
            color = "#AAAAAA"

        ax.plot([lo, hi], [i, i], color=color, linewidth=2,
                solid_capstyle="round")
        ax.scatter(d, i, color=color, s=50, zorder=5,
                   edgecolors="black", linewidth=0.5)

    ax.axvline(x=0, color="black", linewidth=0.8, linestyle="--", alpha=0.5)

    # Effect size guidelines
    for thresh in [0.2, 0.5, 0.8]:
        for sign in [1, -1]:
            ax.axvline(x=thresh * sign, color="#dddddd", linewidth=0.5,
                       linestyle=":", alpha=0.7)

    ax.set_yticks(range(n))
    ax.set_yticklabels(df["dv_label"].values, fontsize=6)
    ax.set_xlabel("Cohen's d (95% CI)")
    ax.set_title("(b) Effect Sizes (Forest Plot)")

    # Significance markers on right margin
    x_right = ax.get_xlim()[1]
    for i, row in df.iterrows():
        marker = _significance_marker(row["p"])
        if marker:
            ax.text(x_right + 0.1, i, marker, va="center", fontsize=7,
                    fontweight="bold", color=COLOR_SIG, clip_on=False)

    # Direction labels below plot
    ax.text(0.02, -0.08, "\u2190 Favors Hybrid", transform=ax.transAxes,
            fontsize=6, color=COLOR_HYBRID, ha="left")
    ax.text(0.98, -0.08, "Favors Glass \u2192", transform=ax.transAxes,
            fontsize=6, color=COLOR_GLASS, ha="right")

    fig.subplots_adjust(right=0.92)
    fig.tight_layout(w_pad=2.0, rect=[0, 0.05, 0.95, 1.0])
    _save(fig, "fig8_trust_forestplot")


# ═══════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════

def main():
    apply_paper_style()
    print(f"Output directory: {FIG_DIR}")

    compose_fig3()
    compose_fig5()
    plot_fig6()
    plot_fig7()
    plot_fig8()

    print("\n=== Done! ===")
    outputs = list(FIG_DIR.glob("fig*.pdf"))
    for f in sorted(outputs):
        print(f"  {f.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
