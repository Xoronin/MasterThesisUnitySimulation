import os
import glob
import math
import pandas as pd
import matplotlib.pyplot as plt
import numpy as np

SCENARIOS = [
    {
        "id": "LTE",
        "label": "LTE, f = 700 MHz",
        "folder": r"..\Project\Data\Exports\S2\LTE",
        "technology": "LTE",
        "freq_mhz": 700,
        "los_max_distance": 100.0,
        "ref_los": "FreeSpace",
    },
    {
        "id": "5GSub6",
        "label": "5G Sub-6 GHz, f = 3500 MHz",
        "folder": r"..\Project\Data\Exports\S2\5GSub6",
        "technology": "FiveGSub6",
        "freq_mhz": 3500,
        "los_max_distance": 100.0,
        "ref_los": "FreeSpace",
    },
]

PLOT_MODEL_ORDER = ["FreeSpace", "LogD", "Hata", "COST231", "RayTracing"]

C = 3e8  # speed of light [m/s]

def fspl_db(distance_m, frequency_mhz):
    """FSPL in dB for d [m], f [MHz] using 4πd/λ formulation."""
    wavelength = C / (frequency_mhz * 1e6)
    d = np.maximum(np.asarray(distance_m, float), 1e-3)
    path_loss_linear = (4 * math.pi * d / wavelength) ** 2
    return 10 * np.log10(path_loss_linear)


def coverage_ratio(pr_dbm: np.ndarray, threshold_dbm: float) -> float:
    """Percentage of receivers above a given sensitivity threshold."""
    return float(np.mean(pr_dbm >= threshold_dbm) * 100.0)


def load_scenario(folder: str) -> pd.DataFrame:
    pattern = os.path.join(folder, "*.csv")
    files = glob.glob(pattern)
    if not files:
        raise FileNotFoundError(f"No CSV files found in {folder}")

    dfs = []
    for f in files:
        df = pd.read_csv(f, header=0)

        df.replace(r"^\s*$", np.nan, regex=True, inplace=True)

        for col in ["ScenarioName", "PropagationModel", "Technology"]:
            if col in df.columns:
                df[col] = df[col].astype(str).str.strip()

        for col in ["TxFrequency", "Distance", "RxSignalStrength", "RxSensitivity", "TxPower"]:
            if col in df.columns:
                df[col] = pd.to_numeric(df[col], errors="coerce")

        if "BuildingsOn" in df.columns and df["BuildingsOn"].dtype == object:
            df["BuildingsOn"] = df["BuildingsOn"].astype(str).str.strip().map(
                {"True": True, "False": False}
            )

        dfs.append(df)

    data = pd.concat(dfs, ignore_index=True)

    if "PropagationModel" in data.columns:
        data["PropagationModel"] = data["PropagationModel"].astype(str).str.strip()

    return data

def compute_metrics(df: pd.DataFrame,
                    reference_model: str = "FreeSpace",
                    thresholds=(-90, -100, -110)) -> pd.DataFrame:
    """
    Compute:
      - Mean Received Power
      - Path Loss
      - Excess Path Loss vs. FSPL
      - RMSE and Mean Bias vs. reference model
      - Coverage ratios
    """
    models = sorted(df["PropagationModel"].unique())
    print("[DEBUG] Models:", models)

    results = []

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

        freq_mhz = sub["TxFrequency"].iloc[0]
        tx_power_dbm = sub["TxPower"].iloc[0]

        dist = sub["Distance"].values
        pr_sim = sub["RxSignalStrength"].values

        valid = np.isfinite(pr_sim)
        if not np.any(valid):
            print(f"[WARN] No valid RxSignalStrength values for model '{model}'")
            mean_pr = float("nan")
            mean_pl = float("nan")
            mean_excess_pl = float("nan")
        else:
            dist_valid = dist[valid]
            pr_valid = pr_sim[valid]

            pl_model = tx_power_dbm - pr_valid
            pl_fspl = fspl_db(dist_valid, freq_mhz)
            excess_pl = pl_model - pl_fspl

            mean_pr = float(np.mean(pr_valid))
            mean_pl = float(np.mean(pl_model))
            mean_excess_pl = float(np.mean(excess_pl))

        if not np.any(valid):
            rmse = float("nan")
            bias = float("nan")
        else:
            dist_valid = dist[valid]
            pr_valid = pr_sim[valid]

            pl_fspl_full = fspl_db(dist_valid, freq_mhz)      
            pr_ref = tx_power_dbm - pl_fspl_full              

            err = pr_valid - pr_ref
            rmse = float(np.sqrt(np.mean(err ** 2)))
            bias = float(np.mean(err))

        cov = {
            f"Coverage ≥ {th} dBm [%]": coverage_ratio(pr_sim, th)
            for th in thresholds
        }

        results.append({
            "Model": model,
            "Freq_MHz": freq_mhz,
            "TxPower_dBm": tx_power_dbm,
            "N_Samples": len(sub),
            "Mean_Pr_dBm": mean_pr,
            "Mean_PL_dB": mean_pl,
            "Mean_ExcessPL_dB": mean_excess_pl,
            "RMSE_vs_" + reference_model + "_dB": rmse,
            "Bias_vs_" + reference_model + "_dB": bias,
            **cov
        })

    return pd.DataFrame(results)


def los_ratio(df: pd.DataFrame, los_boundary_m: float = 100.0) -> float:
    """Approximate LOS ratio via distance boundary if no IsLOS flag is present."""
    if "IsLOS" in df.columns:
        return float(df["IsLOS"].mean())
    else:
        return float((df["Distance"] <= los_boundary_m).mean())


def compute_metrics_vs_reference(data: pd.DataFrame, reference_model: str):
    """
    Simple RMSE/Bias vs reference for a subset (e.g. LOS only),
    used for the LOS summary printout.
    """
    metrics = {}
    ref = data[data["PropagationModel"] == reference_model].copy()
    if ref.empty:
        print(f"[WARN] No rows for reference model '{reference_model}'.")
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
            print(f"[WARN] No overlapping distances for '{model}' vs '{reference_model}'.")
            continue

        err = merged["RxSignalStrength"] - merged["RefPr"]
        rmse = math.sqrt((err ** 2).mean())
        bias = err.mean()
        metrics[model] = (rmse, bias)

    return metrics


def plot_combined_los_nlos(df: pd.DataFrame, cfg, out_path: str):
    models_present = sorted(df["PropagationModel"].unique())
    models_to_plot = [m for m in PLOT_MODEL_ORDER if m in models_present and m != "FreeSpace"]
    if not models_to_plot:
        print(f"[WARN] No known models to plot in {cfg['id']}. Found: {models_present}")
        return

    fig, ax = plt.subplots(figsize=(7, 4.5))

    colors = {
        "LogD": "green",
        "Hata": "orange",
        "COST231": "yellow",
        "RayTracing": "blue"
    }

    # 1) Plot all non-FSPL models
    for model in models_to_plot:
        msub = df[df["PropagationModel"] == model].sort_values("Distance")
        if model == "RayTracing":
            ax.plot(
                msub["Distance"],
                msub["RxSignalStrength"],
                linestyle="None",
                marker="o",
                markersize=3,
                label=model,
                color=colors.get(model, "grey")
            )
        else:
            ax.plot(
                msub["Distance"],
                msub["RxSignalStrength"],
                linestyle="-",
                linewidth=1.2,
                marker="None",
                label=model,
                color=colors.get(model, "grey")
            )

    # 2) Analytic FSPL over full distance range
    d_split = cfg["los_max_distance"]
    d_min = df["Distance"].min()
    d_max = df["Distance"].max()
    d_all = np.linspace(d_min, d_max, 300)

    # Compute path loss and received power
    pl_db = fspl_db(d_all, cfg["freq_mhz"])
    tx_power_dbm = df["TxPower"].iloc[0]
    fspl_all = tx_power_dbm - pl_db

    mask_los = d_all <= d_split
    mask_nlos = d_all > d_split

    # solid in LOS
    ax.plot(
        d_all[mask_los],
        fspl_all[mask_los],
        color="tab:red",
        linewidth=1.4,
        linestyle="-",
        label="FSPL (LOS ref)"
    )
    # dashed in NLOS (virtual LOS baseline)
    ax.plot(
        d_all[mask_nlos],
        fspl_all[mask_nlos],
        color="tab:red",
        linewidth=1.2,
        linestyle="--"
    )

    # 3) Corner marker
    ax.axvline(d_split, linestyle="--", linewidth=1, color="gray")
    y_top = ax.get_ylim()[1]
    ax.text(d_split, y_top,
            "Corner", rotation=90, va="top", ha="left", fontsize=8)

    ax.set_xlabel("Distance $d$ [m]")
    ax.set_ylabel("Received power $P_r$ [dBm]")
    ax.set_title(f"S2 - Urban LOS/NLOS, {cfg['label']}")
    ax.grid(True, linestyle="--", linewidth=0.5)
    ax.legend(title="Propagation Model", frameon=False)

    fig.tight_layout()
    plt.savefig(out_path, dpi=300, bbox_inches="tight", transparent=False)
    plt.close(fig)
    print(f"[INFO] Saved combined LOS/NLOS plot to: {out_path}")


def main():
    summary_rows = []

    for cfg in SCENARIOS:
        print("\n" + "=" * 70)
        print(f"[INFO] Scenario: {cfg['id']} – {cfg['label']}")
        print("=" * 70)

        df = load_scenario(cfg["folder"])

        # Filter by tech + freq
        df = df[df["Technology"] == cfg["technology"]]
        df = df[df["TxFrequency"] == cfg["freq_mhz"]]
        if df.empty:
            print(f"[WARN] No rows after filtering Tech={cfg['technology']}, f={cfg['freq_mhz']} MHz.")
            continue

        d_split = cfg["los_max_distance"]
        los_data = df[df["Distance"] <= d_split].copy()

        print(f"[INFO] LOS rows:  {len(los_data)} (d <= {d_split} m)")
        print(f"[INFO] NLOS rows: {len(df) - len(los_data)} (d > {d_split} m)")

        # Combined LOS+NLOS plot
        out_png = os.path.join(cfg["folder"], f"{cfg['id']}_LOS_NLOS_Pr_vs_d.png")
        plot_combined_los_nlos(df, cfg, out_png)

        # LOS metrics vs reference 
        if not los_data.empty:
            ref_los = cfg["ref_los"]
            print(f"[INFO] LOS metrics vs {ref_los}:")
            los_metrics = compute_metrics_vs_reference(los_data, ref_los)
            for model, (rmse, bias) in los_metrics.items():
                print(f"  {model:12s} (LOS):  RMSE = {rmse:6.2f} dB, Bias = {bias:+6.2f} dB")
            rt_rmse_los, rt_bias_los = los_metrics.get("RayTracing", (None, None))
        else:
            rt_rmse_los, rt_bias_los = (None, None)

        summary_rows.append({
            "Scenario": cfg["id"],
            "Label": cfg["label"],
            "FreqMHz": cfg["freq_mhz"],
            "RT_RMSE_LOS_dB": rt_rmse_los,
            "RT_Bias_LOS_dB": rt_bias_los,
        })

        # scenario metrics 
        print("\n[INFO] Scenario metrics (LOS+NLOS):")
        metrics_df = compute_metrics(df, reference_model=cfg["ref_los"], thresholds=(-90, -100, -110))
        print(metrics_df.to_string(index=False))

        los_val = los_ratio(df, los_boundary_m=d_split)
        print(f"\nApprox. LOS ratio (d ≤ {d_split} m): {los_val * 100:.1f} %")

        out_csv = os.path.join(cfg["folder"], f"{cfg['id']}_metrics_output.csv")
        metrics_df.to_csv(out_csv, index=False)
        print(f"[INFO] Metrics saved to: {out_csv}")

    if summary_rows:
        print("\n" + "=" * 70)
        print("[SUMMARY] RayTracing LOS performance")
        print("=" * 70)
        summary_df = pd.DataFrame(summary_rows)
        print(summary_df.to_string(index=False))


if __name__ == "__main__":
    main()
