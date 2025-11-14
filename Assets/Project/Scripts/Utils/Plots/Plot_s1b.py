import os
import glob
import pandas as pd
import matplotlib.pyplot as plt
import itertools

# ----------------------------
# CONFIG – adjust this
# ----------------------------
EXPORT_FOLDER = r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S1b"
SCENARIO_FILTER = "S1b"          # must match 'scenario' column
FREQUENCIES_OF_INTEREST = [700, 1800]   # MHz
BUILDINGS_OFF_ONLY = False       # set True if you logged buildings_on=0 for S1b
# ----------------------------


def load_all_exports(folder: str) -> pd.DataFrame:
    full_path = os.path.abspath(folder)
    print(f"[INFO] Loading CSVs from: {full_path}")
    pattern = os.path.join(full_path, "*.csv")
    files = glob.glob(pattern)
    if not files:
        raise FileNotFoundError(f"No CSV files found in {full_path}")
    dfs = [pd.read_csv(f) for f in files]
    return pd.concat(dfs, ignore_index=True)


def main():
    df = load_all_exports(EXPORT_FOLDER)

    print("Unique scenarios in data:", df["scenario"].unique())
    print("Unique frequencies in data:", df["tx_frequency_mhz"].unique())

    # --- scenario filter ---
    global SCENARIO_FILTER
    if SCENARIO_FILTER not in df["scenario"].unique():
        print(f"[WARN] SCENARIO_FILTER='{SCENARIO_FILTER}' not found, "
              "using first available scenario instead.")
        SCENARIO_FILTER = df["scenario"].unique()[0]

    s1b = df[df["scenario"] == SCENARIO_FILTER].copy()
    print(f"[INFO] Rows in scenario '{SCENARIO_FILTER}': {len(s1b)}")

    if BUILDINGS_OFF_ONLY and "buildings_on" in s1b.columns:
        before = len(s1b)
        s1b = s1b[(s1b["buildings_on"] == 0) | (s1b["buildings_on"] == False)]
        print(f"[INFO] Filtered buildings_on -> {before} -> {len(s1b)} rows")

    freqs_in_data = sorted(s1b["tx_frequency_mhz"].unique())
    print("Frequencies in selected scenario:", freqs_in_data)

    freqs = [f for f in FREQUENCIES_OF_INTEREST if f in freqs_in_data]
    if not freqs:
        raise ValueError(
            f"No matching frequencies from {FREQUENCIES_OF_INTEREST} found in "
            f"scenario '{SCENARIO_FILTER}'. Available: {freqs_in_data}"
        )

    # --- create subplots (one per frequency) ---
    fig, axes = plt.subplots(1, len(freqs), figsize=(5 * len(freqs), 4), sharey=True)
    if len(freqs) == 1:
        axes = [axes]

    # consistent colors per model
    color_cycle = itertools.cycle(
        ["tab:blue", "tab:orange", "tab:green", "tab:red", "tab:purple", "tab:brown"]
    )
    model_colors = {}

    # models that should be shown as continuous lines (baseline)
    line_models = {"FreeSpace", "LogD"}

    for ax, freq in zip(axes, freqs):
        sub = s1b[s1b["tx_frequency_mhz"] == freq].copy()
        models = sorted(sub["propagation_model"].unique())
        print(f"[INFO] Frequency {freq} MHz has models:", models)

        for model in models:
            msub = sub[sub["propagation_model"] == model].copy()
            if msub.empty:
                continue

            if model not in model_colors:
                model_colors[model] = next(color_cycle)

            msub = msub.sort_values("distance_m")
            # convert to km for macrocell view
            d_km = msub["distance_m"] / 1000.0

            # choose style depending on model
            if model in line_models:
                ax.plot(
                    d_km,
                    msub["rx_signal_strength_dbm"],
                    linestyle="-",
                    linewidth=1,
                    color=model_colors[model],
                    label=model,
                )
            else:
                ax.plot(
                    d_km,
                    msub["rx_signal_strength_dbm"],
                    linestyle="None",
                    marker="o",
                    markersize=1,
                    markerfacecolor=model_colors[model],
                    markeredgecolor=model_colors[model],
                    label=model,
                )

        ax.set_xlabel("Distance d [km]")
        ax.set_title(f"f = {freq} MHz")
        ax.grid(True, linestyle="--", linewidth=0.5)

    axes[0].set_ylabel("Received power $P_r$ [dBm]")

    # build legend (unique models)
    handles = []
    labels = []
    for model, color in model_colors.items():
        h = plt.Line2D(
            [], [], color=color, marker="o", linestyle="-",
            linewidth=1.4, markersize=4, label=model
        )
        handles.append(h)
        labels.append(model)

    fig.legend(
        handles,
        labels,
        loc="upper center",
        title="Propagation Model",
        ncol=min(len(labels), 4),
        bbox_to_anchor=(0.5, 1.08),
        frameon=False,
    )

    fig.suptitle(f"Scenario {SCENARIO_FILTER} – Macrocell LOS", y=1.12)
    fig.tight_layout()

    # save + show
    output_path = os.path.join(EXPORT_FOLDER, "S1b_macro_all_models_colored.png")
    plt.savefig(output_path, dpi=300, bbox_inches="tight")
    print(f"[INFO] Saved figure to: {output_path}")
    plt.show()


if __name__ == "__main__":
    main()
