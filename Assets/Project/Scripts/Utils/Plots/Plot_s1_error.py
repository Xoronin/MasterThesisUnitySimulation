import os
import glob
import pandas as pd
import matplotlib.pyplot as plt
import itertools

# ----------------------------
# CONFIG – adjust this
# ----------------------------
EXPORT_FOLDER = r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S1a"
SCENARIO_FILTER = "S1a"
FREQUENCIES_OF_INTEREST = [700, 3500, 26000]   # MHz
BASELINE_MODEL_NAMES = {
    "FreeSpace", "FreeSpaceModel", "Free Space Path Loss"
}
BUILDINGS_OFF_ONLY = False
# ----------------------------


def load_all_exports(folder: str) -> pd.DataFrame:
    pattern = os.path.join(folder, "*.csv")
    files = glob.glob(pattern)
    if not files:
        raise FileNotFoundError(f"No CSV files found in {folder}")
    dfs = [pd.read_csv(f) for f in files]
    return pd.concat(dfs, ignore_index=True)


def main():
    df = load_all_exports(EXPORT_FOLDER)

    s1a = df[df["scenario"] == SCENARIO_FILTER].copy()
    if BUILDINGS_OFF_ONLY and "buildings_on" in s1a.columns:
        s1a = s1a[(s1a["buildings_on"] == 0) | (s1a["buildings_on"] == False)]

    freqs_in_data = sorted(s1a["tx_frequency_mhz"].unique())
    freqs = [f for f in FREQUENCIES_OF_INTEREST if f in freqs_in_data]
    if not freqs:
        raise ValueError(
            f"No matching frequencies from {FREQUENCIES_OF_INTEREST} in data. "
            f"Available: {freqs_in_data}"
        )

    fig, axes = plt.subplots(1, len(freqs), figsize=(5 * len(freqs), 4), sharey=True)
    if len(freqs) == 1:
        axes = [axes]

    color_cycle = itertools.cycle(
        ["tab:orange", "tab:green", "tab:red", "tab:purple", "tab:brown"]
    )
    model_colors = {}

    for ax, freq in zip(axes, freqs):
        sub = s1a[s1a["tx_frequency_mhz"] == freq].copy()

        # find baseline (FreeSpace) rows
        baseline = sub[sub["propagation_model"].isin(BASELINE_MODEL_NAMES)].copy()
        if baseline.empty:
            raise RuntimeError(f"No FreeSpace baseline found for f={freq} MHz")

        baseline = baseline.sort_values("distance_m")
        baseline_series = baseline.set_index("distance_m")["rx_signal_strength_dbm"]

        models = sorted(sub["propagation_model"].unique())
        # exclude baseline from error curves
        models = [m for m in models if m not in BASELINE_MODEL_NAMES]

        for model in models:
            msub = sub[sub["propagation_model"] == model].copy()
            if msub.empty:
                continue

            if model not in model_colors:
                model_colors[model] = next(color_cycle)

            msub = msub.sort_values("distance_m")
            d = msub["distance_m"]
            # align to baseline distances
            baseline_vals = baseline_series.loc[d].values
            err = msub["rx_signal_strength_dbm"].values - baseline_vals

            ax.plot(
                d,
                err,
                linestyle="None",
                marker="o",
                markersize=1,
                markerfacecolor=model_colors[model],
                markeredgecolor=model_colors[model],
                label=model,
            )

        ax.axhline(0.0, color="black", linestyle="--", linewidth=0.8)
        ax.set_xscale("log")
        ax.set_xlabel("Distance d [m]")
        ax.set_title(f"f = {freq} MHz")
        ax.grid(True, which="both", linestyle="--", linewidth=0.5)

    axes[0].set_ylabel(r"$\Delta P_r = P_{model} - P_{\mathrm{FreeSpace}}$ [dB]")

    # legend
    handles = []
    labels = []
    for model, color in model_colors.items():
        h = plt.Line2D(
            [], [], linestyle="None", marker="o",
            markersize=4, color=color, label=model
        )
        handles.append(h)
        labels.append(model)

    fig.legend(
        handles,
        labels,
        loc="upper center",
        ncol=min(len(labels), 4),
        bbox_to_anchor=(0.5, 1.08),
        title="Model vs. FreeSpace",
        frameon=False,
    )

    fig.suptitle("Scenario S1a – Error vs FreeSpace baseline", y=1.15)
    fig.tight_layout()

    out_path = os.path.join(EXPORT_FOLDER, "S1a_error_vs_FreeSpace.png")
    plt.savefig(out_path, dpi=300, bbox_inches="tight")
    print(f"[INFO] Saved figure to: {out_path}")
    plt.show()


if __name__ == "__main__":
    main()
