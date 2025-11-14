import os
import glob
import pandas as pd
import matplotlib.pyplot as plt
import itertools

# ---------------------------------------------------
# CONFIGURATION – adjust these for your setup
# ---------------------------------------------------

EXPORT_FOLDER = r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S1a"
SCENARIO_FILTER = "S1a"        # value in 'scenario' column for S1a
FREQUENCIES_OF_INTEREST = [700, 3500, 26000]  # MHz
BUILDINGS_OFF_ONLY = False     # set True if you want to filter buildings_off
# ---------------------------------------------------


def load_all_exports(folder: str) -> pd.DataFrame:
    pattern = os.path.join(folder, "*.csv")
    files = glob.glob(pattern)
    if not files:
        raise FileNotFoundError(f"No CSV files found in {folder}")

    dfs = [pd.read_csv(f) for f in files]
    return pd.concat(dfs, ignore_index=True)


def main():
    df = load_all_exports(EXPORT_FOLDER)

    # Filter to Scenario S1a
    s1a = df[df["scenario"] == SCENARIO_FILTER].copy()

    # Optional: ensure buildings are OFF (free-space)
    if BUILDINGS_OFF_ONLY and "buildings_on" in s1a.columns:
        s1a = s1a[(s1a["buildings_on"] == 0) | (s1a["buildings_on"] == False)]

    # Determine which frequencies to use
    freqs_in_data = sorted(s1a["tx_frequency_mhz"].unique())
    freqs = [f for f in FREQUENCIES_OF_INTEREST if f in freqs_in_data]
    if not freqs:
        raise ValueError(
            f"No matching frequencies from {FREQUENCIES_OF_INTEREST} found in data. "
            f"Available frequencies: {freqs_in_data}"
        )

    # Create 1xN subplots: one per frequency
    fig, axes = plt.subplots(1, len(freqs), figsize=(5 * len(freqs), 4), sharey=True)

    if len(freqs) == 1:
        axes = [axes]

    # consistent colors per model across all subplots
    color_cycle = itertools.cycle(
        ["tab:blue", "tab:orange", "tab:green", "tab:red",
         "tab:purple", "tab:brown", "tab:pink", "tab:gray"]
    )
    model_colors = {}

    # models that should be shown as continuous lines (baseline)
    line_models = {"FreeSpace", "LogD"}

    for ax, freq in zip(axes, freqs):
        sub = s1a[s1a["tx_frequency_mhz"] == freq].copy()

        models = sorted(sub["propagation_model"].unique())

        for model in models:
            msub = sub[sub["propagation_model"] == model].copy()
            if msub.empty:
                continue

            # assign color to model if not already assigned
            if model not in model_colors:
                model_colors[model] = next(color_cycle)

            msub = msub.sort_values("distance_m")

            # choose style depending on model
            if model in line_models:
                # draw smooth baseline line
                ax.plot(
                    msub["distance_m"],
                    msub["rx_signal_strength_dbm"],
                    linestyle="-",
                    linewidth=1,
                    color=model_colors[model],
                    label=model,
                )
            else:
                # discrete scatter points (no connecting lines)
                ax.plot(
                    msub["distance_m"],
                    msub["rx_signal_strength_dbm"],
                    linestyle="None",
                    marker="o",
                    markersize=1,
                    markerfacecolor=model_colors[model],
                    markeredgecolor=model_colors[model],
                    label=model,
                )

        ax.set_xscale("log")
        ax.set_xlabel("Distance d [m]")
        ax.set_title(f"f = {freq} MHz")
        ax.grid(True, which="both", linestyle="--", linewidth=0.5)

    axes[0].set_ylabel("Received power $P_r$ [dBm]")

    # Build one common legend (unique models)
    legend_handles = []
    legend_labels = []
    for model, color in model_colors.items():
        # show both line+marker in legend (even if model is line-only or marker-only)
        h = plt.Line2D(
            [], [], color=color, marker="o", linestyle="-", linewidth=1.4,
            markersize=4, label=model
        )
        legend_handles.append(h)
        legend_labels.append(model)

    fig.legend(
        legend_handles,
        legend_labels,
        loc="upper center",
        ncol=min(len(legend_labels), 4),
        bbox_to_anchor=(0.5, 1.08),
        title="Propagation Model",
        frameon=False,
    )

    fig.suptitle("Scenario S1a – LOS, all models per frequency", y=1.15)
    fig.tight_layout()

    output_path = os.path.join(EXPORT_FOLDER, "S1a_all_models.png")
    plt.savefig(output_path, dpi=300, bbox_inches="tight", transparent=False)
    print(f"[INFO] Saved figure to: {output_path}")
    plt.show()


if __name__ == "__main__":
    main()
