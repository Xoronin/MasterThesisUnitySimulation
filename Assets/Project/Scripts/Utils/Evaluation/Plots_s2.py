import os
import glob
import pandas as pd
import matplotlib.pyplot as plt
import math

# ---------------------------------------------------
# CONFIG – adjust these for your setup
# ---------------------------------------------------

EXPORT_FOLDER = r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S2"
SCENARIO_FILTER = "S2_LTE_new"   # value in 'ScenarioName' column
TECH_FILTER = "LTE"               # value in 'Technology' column, or None to ignore
TARGET_FREQ_MHZ = 700             # LTE urban (S2)
BUILDINGS_ON_ONLY = True          # S2 is urban with buildings enabled

# Models to show in the plot (in this order if present)
PLOT_MODELS = ["FreeSpace", "LogD", "Hata", "COST231", "RayTracing"]

# Reference model for error metrics
REFERENCE_MODEL = "FreeSpace"
# ---------------------------------------------------


def load_all_exports(folder: str) -> pd.DataFrame:
    pattern = os.path.join(folder, "*.csv")
    files = glob.glob(pattern)
    if not files:
        raise FileNotFoundError(f"No CSV files found in {folder}")

    dfs = [pd.read_csv(f, header=0) for f in files]
    return pd.concat(dfs, ignore_index=True)


def compute_metrics_vs_reference(data: pd.DataFrame, reference_model: str):
    """
    Compute RMSE and mean bias for each model vs the reference_model,
    by aligning rows on 'Distance' for the same scenario.
    """
    metrics = {}

    ref = data[data["PropagationModel"] == reference_model].copy()
    if ref.empty:
        print(f"[WARN] No rows for reference model '{reference_model}'. Skipping metrics.")
        return metrics

    ref = ref[["Distance", "RxSignalStrength"]].rename(
        columns={"RxSignalStrength": "RefPr"}
    )

    for model in sorted(data["PropagationModel"].unique()):
        if model == reference_model:
            continue

        mdf = data[data["PropagationModel"] == model][["Distance", "RxSignalStrength"]].copy()
        merged = pd.merge(mdf, ref, on="Distance", how="inner")
        if merged.empty:
            print(f"[WARN] No overlapping distances for model '{model}' vs reference.")
            continue

        err = merged["RxSignalStrength"] - merged["RefPr"]
        rmse = math.sqrt((err ** 2).mean())
        bias = err.mean()

        metrics[model] = (rmse, bias)

    return metrics


def main():
    df = load_all_exports(EXPORT_FOLDER)

    # --- Debug info ---
    print("[DEBUG] Columns:", df.columns.tolist())
    print("[DEBUG] Unique ScenarioName:", df["ScenarioName"].unique())
    print("[DEBUG] Unique PropagationModel:", df["PropagationModel"].unique())
    print("[DEBUG] Unique Technology:", df["Technology"].unique())
    print("[DEBUG] Unique TxFrequency:", df["TxFrequency"].unique())
    if "BuildingsOn" in df.columns:
        print("[DEBUG] Unique BuildingsOn:", df["BuildingsOn"].unique())

    # --- Basic cleaning ---
    df["ScenarioName"] = df["ScenarioName"].astype(str).str.strip()
    df["PropagationModel"] = df["PropagationModel"].astype(str).str.strip()
    df["Technology"] = df["Technology"].astype(str).str.strip()

    df["TxFrequency"] = pd.to_numeric(df["TxFrequency"], errors="coerce")
    df["Distance"] = pd.to_numeric(df["Distance"], errors="coerce")
    df["RxSignalStrength"] = pd.to_numeric(df["RxSignalStrength"], errors="coerce")

    data = df.copy()

    # --- Scenario filter ---
    data = data[data["ScenarioName"] == SCENARIO_FILTER]
    if data.empty:
        raise ValueError(f"No rows for ScenarioName == '{SCENARIO_FILTER}'")

    # --- Technology filter (optional) ---
    if TECH_FILTER is not None:
        data = data[data["Technology"] == TECH_FILTER]
        if data.empty:
            raise ValueError(f"No rows for Technology == '{TECH_FILTER}' after scenario filter")

    # --- Buildings filter ---
    if BUILDINGS_ON_ONLY and "BuildingsOn" in data.columns:
        data = data[data["BuildingsOn"] == True]
        if data.empty:
            raise ValueError("After BuildingsOn == True filter, no rows remain.")

    # --- Frequency filter ---
    data = data[data["TxFrequency"] == TARGET_FREQ_MHZ]
    if data.empty:
        raise ValueError(
            f"No rows with TxFrequency == {TARGET_FREQ_MHZ} MHz for scenario '{SCENARIO_FILTER}'"
        )

    # --- Sort for nicer plotting ---
    data = data.sort_values("Distance")

    # --- Figure: Pr vs distance for all selected models ---
    models_present = sorted(data["PropagationModel"].unique())
    print("[INFO] Models present in filtered data:", models_present)

    # Filter PLOT_MODELS to those that actually exist
    models_to_plot = [m for m in PLOT_MODELS if m in models_present]
    if not models_to_plot:
        raise ValueError("None of the PLOT_MODELS are present in the filtered data.")

    fig, ax = plt.subplots(figsize=(7, 4.5))

    for model in models_to_plot:
        msub = data[data["PropagationModel"] == model]
        if msub.empty:
            continue

        if model == "RayTracing":
            ax.plot(
                msub["Distance"],
                msub["RxSignalStrength"],
                linestyle="None",               
                marker="o", markersize=3,
                label=model
            )
        else:
            ax.plot(
                msub["Distance"],
                msub["RxSignalStrength"],
                linestyle="-",
                linewidth=1.2,
                marker="None",
                label=model
            )

    ax.set_xlabel("Distance $d$ [m]")
    ax.set_ylabel("Received power $P_r$ [dBm]")
    ax.set_title(f"S2 – Urban LOS {TECH_FILTER} at f = {TARGET_FREQ_MHZ} MHz")
    ax.grid(True, linestyle="--", linewidth=0.5)
    ax.legend(title="Propagation Model", frameon=False)

    fig.tight_layout()

    out_png = os.path.join(EXPORT_FOLDER, f"S2_{TECH_FILTER}_{TARGET_FREQ_MHZ}MHz_Pr_vs_d.png")
    plt.savefig(out_png, dpi=300, bbox_inches="tight", transparent=False)
    print(f"[INFO] Saved figure to: {out_png}")
    plt.show()

    # --- Metrics vs reference model (e.g. Hata) ---
    print(f"\n[INFO] Error metrics vs reference model: {REFERENCE_MODEL}")
    metrics = compute_metrics_vs_reference(data, REFERENCE_MODEL)
    if not metrics:
        print("[WARN] No metrics computed.")
    else:
        for model, (rmse, bias) in metrics.items():
            print(f"  {model}: RMSE = {rmse:.2f} dB, Mean Bias = {bias:+.2f} dB")


if __name__ == "__main__":
    main()
