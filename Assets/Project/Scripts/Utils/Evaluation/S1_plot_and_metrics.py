import os
import glob
import math
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


EXPORT_FOLDER = r"..\Project\Data\Exports\S1"
SCENARIO_FILTER = "S1_Baseline_LOS"
SOURCE_FREQ = 3500            # MHz
FREQ_GHZ = 3.5                # GHz
BUILDINGS_OFF_ONLY = False

C = 3e8  # speed of light in m/s


def load_all_exports(folder: str) -> pd.DataFrame:
    pattern = os.path.join(folder, "*.csv")
    files = glob.glob(pattern)
    if not files:
        raise FileNotFoundError(f"No CSV files found in {folder}")

    dfs = [pd.read_csv(f) for f in files]
    return pd.concat(dfs, ignore_index=True)


def fspl_db(distance_m: float, frequency_mhz: float) -> float:
    """
    Free-space path loss in dB for a given distance (m) and frequency (MHz).
    """
    wavelength = C / (frequency_mhz * 1e6)  # MHz → Hz
    path_loss_linear = (4 * math.pi * distance_m / wavelength) ** 2
    return 10 * math.log10(path_loss_linear)


def pr_dbm(distance_m: float, frequency_mhz: float, tx_power_dbm: float) -> float:
    """
    Theoretical received power (dBm) based on FSPL.
    """
    return tx_power_dbm - fspl_db(distance_m, frequency_mhz)


def plot_models(data: pd.DataFrame) -> None:
    """
    Plot received power vs. distance for all propagation models in 'data'.
    """
    models = sorted(data["PropagationModel"].unique())

    line_models = {"FreeSpace", "LogD"}
    marker_only_models = {"LogNShadow", "RayTracing"}

    fig, ax = plt.subplots(figsize=(6, 4))

    for model in models:
        msub = data[data["PropagationModel"] == model]

        if model in line_models:
            ax.plot(
                msub["Distance"],
                msub["RxSignalStrength"],
                linestyle="-",
                linewidth=1.2,
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


def evaluate_vs_fspl(data: pd.DataFrame) -> None:
    """
    Compute RMSE and mean bias between simulated received power and analytical FSPL
    """
    metrics = []

    models = sorted(data["PropagationModel"].unique())
    for model in models:
        sub = data[data["PropagationModel"] == model].copy()
        if sub.empty:
            continue

        dist = sub["Distance"].values

        freq = float(sub["TxFrequency"].iloc[0])

        tx_power = float(sub["TxPower"].iloc[0])

        pr_theory = np.array([pr_dbm(d, freq, tx_power) for d in dist])

        pr_sim = sub["RxSignalStrength"].values

        rmse = float(np.sqrt(np.mean((pr_sim - pr_theory) ** 2)))
        mean_bias = float(np.mean(pr_sim - pr_theory))

        metrics.append((model, rmse, mean_bias))

    print("\n[INFO] FSPL comparison (simulated vs analytical):")
    for model, rmse, bias in metrics:
        print(f"  {model:15s} RMSE = {rmse:6.2f} dB | Mean Bias = {bias:+6.2f} dB")


def main():
    df = load_all_exports(EXPORT_FOLDER)

    df["ScenarioName"] = df["ScenarioName"].astype(str).str.strip()
    df["PropagationModel"] = df["PropagationModel"].astype(str).str.strip()
    df["TxFrequency"] = pd.to_numeric(df["TxFrequency"], errors="coerce")
    df["Distance"] = pd.to_numeric(df["Distance"], errors="coerce")
    df["RxSignalStrength"] = pd.to_numeric(df["RxSignalStrength"], errors="coerce")

    data = df.copy()

    data = data[data["ScenarioName"] == SCENARIO_FILTER]

    if BUILDINGS_OFF_ONLY and "BuildingsOn" in data.columns:
        data = data[data["BuildingsOn"] == False]

    data = data[data["TxFrequency"] == SOURCE_FREQ]


    data = data.sort_values("Distance")
    plot_models(data)
    evaluate_vs_fspl(data)


if __name__ == "__main__":
    main()
