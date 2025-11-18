import os
import glob
import pandas as pd
import matplotlib.pyplot as plt


EXPORT_FOLDER = r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S1"
SCENARIO_FILTER = "S1_Baseline_LOS"
SOURCE_FREQ = 3500            # MHz
FREQ_GHZ = 3.5                # GHz
BUILDINGS_OFF_ONLY = False    
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

    # Debug info
    print("[DEBUG] Columns:", df.columns.tolist())
    print("[DEBUG] Unique ScenarioName:", df["ScenarioName"].unique())
    print("[DEBUG] Unique PropagationModel:", df["PropagationModel"].unique())
    print("[DEBUG] Unique TxFrequency:", df["TxFrequency"].unique())

    # Basic cleaning
    df["ScenarioName"] = df["ScenarioName"].astype(str).str.strip()
    df["PropagationModel"] = df["PropagationModel"].astype(str).str.strip()
    df["TxFrequency"] = pd.to_numeric(df["TxFrequency"], errors="coerce")
    df["Distance"] = pd.to_numeric(df["Distance"], errors="coerce")
    df["RxSignalStrength"] = pd.to_numeric(df["RxSignalStrength"], errors="coerce")

    data = df.copy()

    # 1) Scenario filter
    data = data[data["ScenarioName"] == SCENARIO_FILTER]
    if data.empty:
        raise ValueError(f"No rows for ScenarioName == '{SCENARIO_FILTER}'")

    # 2) Optional Buildings filter
    if BUILDINGS_OFF_ONLY and "BuildingsOn" in data.columns:
        data = data[data["BuildingsOn"] == False]
        if data.empty:
            raise ValueError("After BuildingsOn filter, no rows remain.")

    # 3) Frequency filter
    data = data[data["TxFrequency"] == SOURCE_FREQ]
    if data.empty:
        raise ValueError(
            f"No rows with TxFrequency == {SOURCE_FREQ} MHz for scenario '{SCENARIO_FILTER}'"
        )

    # 4) Sort for nice plots
    data = data.sort_values("Distance")

    # 5) Plot one curve per model
    models = sorted(data["PropagationModel"].unique())
    print("[DEBUG] Plotting models:", models)

    # Optional: styling per model (you can tweak names if needed)
    line_models = {"FreeSpace", "LogD"}   
    marker_only_models = {"LogNShadow"}   

    fig, ax = plt.subplots(figsize=(6, 4))

    for model in models:
        msub = data[data["PropagationModel"] == model]

        if model in line_models:
            ax.plot(
                msub["Distance"],
                msub["RxSignalStrength"],
                linestyle="-",
                linewidth=1.2,
                #marker="o",
                #markersize=2,
                label=model,
            )
        elif model in marker_only_models:
            ax.plot(
                msub["Distance"],
                msub["RxSignalStrength"],
                linestyle="None",
                marker="o",
                markersize=2,
                label=model,
            )
        else:
            # fallback style for any other model
            ax.plot(
                msub["Distance"],
                msub["RxSignalStrength"],
                linestyle="--",
                linewidth=1.2,
                marker="x",
                markersize=2,
                label=model,
            )

    ax.set_xlabel("Distance d [m]")
    ax.set_ylabel("Received power $P_r$ [dBm]")
    ax.set_title(f"S1 – baseline, f = {FREQ_GHZ} GHz")
    ax.grid(True, linestyle="--", linewidth=0.5)
    ax.legend(title="Propagation Model", frameon=False)

    fig.tight_layout()

    output_path = os.path.join(EXPORT_FOLDER, f"S1_baseline_{SOURCE_FREQ}MHz.png")
    plt.savefig(output_path, dpi=300, bbox_inches="tight", transparent=False)
    print(f"[INFO] Saved figure to: {output_path}")
    plt.show()


if __name__ == "__main__":
    main()
