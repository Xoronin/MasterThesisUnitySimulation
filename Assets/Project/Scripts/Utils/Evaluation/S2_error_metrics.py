import os
import math
from glob import glob

import numpy as np
import pandas as pd

C = 3e8  # speed of light [m/s]


# ----------------------------
# Helpers
# ----------------------------

def fspl_db(distance_m, frequency_mhz):
    """FSPL in dB for d [m], f [MHz]."""
    wavelength = C / (frequency_mhz * 1e6)
    path_loss_linear = (4 * math.pi * distance_m / wavelength) ** 2
    return 10 * math.log10(path_loss_linear)


def coverage_ratio(pr_dbm: np.ndarray, threshold_dbm: float) -> float:
    """Percentage of receivers above a given sensitivity threshold."""
    return float(np.mean(pr_dbm >= threshold_dbm) * 100.0)


def load_all_csvs(folder: str) -> pd.DataFrame:
    pattern = os.path.join(folder, "*.csv")
    files = glob(pattern)
    if not files:
        raise FileNotFoundError(f"No CSV files found in {folder}")

    dfs = [pd.read_csv(f) for f in files]
    df = pd.concat(dfs, ignore_index=True)

    # clean up model names just in case
    if "PropagationModel" in df.columns:
        df["PropagationModel"] = df["PropagationModel"].astype(str).str.strip()

    return df


# ----------------------------
# Metric computation
# ----------------------------

def compute_metrics(df: pd.DataFrame,
                    reference_model: str = "FreeSpace",
                    thresholds=(-95, -100, -110)) -> pd.DataFrame:
    """
    Compute:
      - Received Power (implicit, from CSV)
      - Path Loss
      - Excess Path Loss vs. FSPL
      - RMSE and Mean Bias vs. reference model
      - Coverage ratios for given thresholds
    """
    models = sorted(df["PropagationModel"].unique())
    print("[DEBUG] Models:", models)

    # we'll need FSPL per-model, not global
    results = []

    # Pre-extract reference model (for RMSE/Bias)
    ref_df = df[df["PropagationModel"] == reference_model][
        ["Distance", "RxSignalStrength", "TxFrequency", "TxPower"]
    ].copy()

    if ref_df.empty:
        print(f"[WARN] No rows for reference model '{reference_model}'. "
              f"RMSE/Bias will be NaN.")
    
    for model in models:
        sub = df[df["PropagationModel"] == model].copy()
        if sub.empty:
            continue

        # assume constant f / Pt per scenario
        freq_mhz = sub["TxFrequency"].iloc[0]
        tx_power_dbm = sub["TxPower"].iloc[0]

        dist = sub["Distance"].values
        pr_sim = sub["RxSignalStrength"].values

        # --- Path Loss & Excess Path Loss vs FSPL ---
        pl_model = tx_power_dbm - pr_sim
        pl_fspl = np.array([fspl_db(d, freq_mhz) for d in dist])
        excess_pl = pl_model - pl_fspl

        # --- RMSE & Bias vs Reference ---
        if model == reference_model or ref_df.empty:
            rmse = float("nan") if ref_df.empty else 0.0
            bias = float("nan") if ref_df.empty else 0.0
        else:
            # align with reference by Distance
            merged = pd.merge(
                sub[["Distance", "RxSignalStrength"]],
                ref_df[["Distance", "RxSignalStrength"]],
                on="Distance",
                how="inner",
                suffixes=("_model", "_ref"),
            )

            if merged.empty:
                print(f"[WARN] No overlapping distances for '{model}' vs '{reference_model}'.")
                rmse = float("nan")
                bias = float("nan")
            else:
                err = merged["RxSignalStrength_model"].values - merged["RxSignalStrength_ref"].values
                rmse = float(np.sqrt(np.mean(err ** 2)))
                bias = float(np.mean(err))

        # --- Coverage ratios ---
        cov = {
            f"Coverage ≥ {th} dBm [%]": coverage_ratio(pr_sim, th)
            for th in thresholds
        }

        results.append({
            "Model": model,
            "Freq_MHz": freq_mhz,
            "TxPower_dBm": tx_power_dbm,
            "N_Samples": len(sub),
            "Mean_Pr_dBm": float(np.mean(pr_sim)),
            "Mean_PL_dB": float(np.mean(pl_model)),
            "Mean_ExcessPL_dB": float(np.mean(excess_pl)),
            "RMSE_vs_" + reference_model + "_dB": rmse,
            "Bias_vs_" + reference_model + "_dB": bias,
            **cov
        })

    return pd.DataFrame(results)


def los_ratio(df: pd.DataFrame, los_boundary_m: float = 100.0) -> float:
    """If you don't have an IsLOS flag, approximate via distance boundary."""
    if "IsLOS" in df.columns:
        return float(df["IsLOS"].mean())
    else:
        return float((df["Distance"] <= los_boundary_m).mean())


# ----------------------------
# MAIN
# ----------------------------

if __name__ == "__main__":
    FOLDER_PATH = r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S2\LTE_10m"
    REFERENCE_MODEL = "FreeSpace"
    THRESHOLDS = (-95, -100, -110)

    df = load_all_csvs(FOLDER_PATH)
    print("\nLoaded files with models:", df["PropagationModel"].unique())
    print("Distance range:", df["Distance"].min(), "–", df["Distance"].max(), "m")

    metrics_df = compute_metrics(df, reference_model=REFERENCE_MODEL, thresholds=THRESHOLDS)
    los_val = los_ratio(df, los_boundary_m=100.0)

    print("\n=== Scenario S2 Metrics ===")
    print(metrics_df.to_string(index=False))

    print(f"\nApprox. LOS ratio (d ≤ 100 m): {los_val * 100:.1f} %")

    out_csv = os.path.join(FOLDER_PATH, "S2_metrics_output.csv")
    metrics_df.to_csv(out_csv, index=False)
    print(f"\n[INFO] Metrics saved to: {out_csv}")
