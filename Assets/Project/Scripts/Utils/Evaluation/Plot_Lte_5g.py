import os
import glob
import math
import pandas as pd
import matplotlib.pyplot as plt
import numpy as np


# ------------------------------------------
# CONFIG – ADAPT THIS
# ------------------------------------------

SCENARIOS = [
    {
        "id": "LTE",
        "label": "LTE, f = 700 MHz",
        "folder": r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S2\LTE_10m",
        "technology": "LTE",          # must match Technology column
        "freq_mhz": 700,
        "los_max_distance": 100.0,    # LOS up to this distance, > = NLOS (adjust!)
        "ref_los": "FreeSpace",       # reference model in LOS
        "ref_nlos": "LogD",           # reference model in NLOS (or "LogD")
    },
    {
        "id": "5G Sub-6 GHz",
        "label": "5G Sub-6 GHz, f = 3500 MHz",
        "folder": r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S2\5GSub6_10m",
        "technology": "FiveGSub6",    # must match Technology column
        "freq_mhz": 3500,
        "los_max_distance": 100.0,
        "ref_los": "FreeSpace",
        "ref_nlos": "LogD",        # or "LogD"
    },
]

PLOT_MODEL_ORDER = ["FreeSpace", "LogD", "LogDShadow", "Hata", "COST231", "RayTracing"]

# ------------------------------------------


def load_scenario(folder: str) -> pd.DataFrame:
    pattern = os.path.join(folder, "*.csv")
    files = glob.glob(pattern)
    if not files:
        raise FileNotFoundError(f"No CSV files found in {folder}")

    dfs = [pd.read_csv(f, header=0) for f in files]
    data = pd.concat(dfs, ignore_index=True)

    # Clean text fields
    for col in ["ScenarioName", "PropagationModel", "Technology"]:
        if col in data.columns:
            data[col] = data[col].astype(str).str.strip()

    # Numeric conversions
    for col in ["TxFrequency", "Distance", "RxSignalStrength", "RxSensitivity"]:
        if col in data.columns:
            data[col] = pd.to_numeric(data[col], errors="coerce")

    if "BuildingsOn" in data.columns and data["BuildingsOn"].dtype == object:
        data["BuildingsOn"] = data["BuildingsOn"].astype(str).str.strip().map(
            {"True": True, "False": False}
        )

    return data


def fspl_db(distance_m, frequency_mhz):
    # FSPL(dB) = 32.44 + 20 log10(f_MHz) + 20 log10(d_km)
    d_km = np.maximum(np.asarray(distance_m, float), 1e-3) / 1000.0
    f_mhz = np.maximum(np.asarray(frequency_mhz, float), 1e-6)
    return 32.44 + 20.0*np.log10(f_mhz) + 20.0*np.log10(d_km)

def compute_metrics_vs_reference(data: pd.DataFrame, reference_model: str):
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
    # skip CSV FreeSpace, we draw analytic FSPL instead
    models_to_plot = [m for m in PLOT_MODEL_ORDER if m in models_present and m != "FreeSpace"]
    if not models_to_plot:
        print(f"[WARN] No known models to plot in {cfg['id']}. Found: {models_present}")
        return

    fig, ax = plt.subplots(figsize=(7, 4.5))

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

    # 2) Analytic FSPL over full distance range
    d_split = cfg["los_max_distance"]
    d_min = df["Distance"].min()
    d_max = df["Distance"].max()
    d_all = np.linspace(d_min, d_max, 300)

    # Compute path loss
    pl_db = fspl_db(d_all, cfg["freq_mhz"])  # FSPL [dB]

    # Assume constant transmit power from CSV reference
    tx_power_dbm = df["TxPower"].iloc[0]  # or set manually if consistent
    fspl_all = tx_power_dbm - pl_db  # Received power [dBm]
    mask_los = d_all <= d_split
    mask_nlos = d_all > d_split#
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

        print("[DEBUG] Models present:", sorted(df["PropagationModel"].unique()))
        print("[DEBUG] Distance range:", df["Distance"].min(), "to", df["Distance"].max())

        d_split = cfg["los_max_distance"]
        los_data = df[df["Distance"] <= d_split].copy()
        nlos_data = df[df["Distance"] > d_split].copy()

        print(f"[INFO] LOS rows:  {len(los_data)} (d <= {d_split} m)")
        print(f"[INFO] NLOS rows: {len(nlos_data)} (d > {d_split} m)")

        # One combined plot LOS+NLOS
        out_png = os.path.join(cfg["folder"], f"{cfg['id']}_LOS_NLOS_Pr_vs_d.png")
        plot_combined_los_nlos(df, cfg, out_png)

        # LOS metrics
        if not los_data.empty:
            ref_los = cfg["ref_los"]
            print(f"[INFO] LOS metrics vs {ref_los}:")
            los_metrics = compute_metrics_vs_reference(los_data, ref_los)
            for model, (rmse, bias) in los_metrics.items():
                print(f"  {model:12s} (LOS):  RMSE = {rmse:6.2f} dB, Bias = {bias:+6.2f} dB")
            rt_rmse_los, rt_bias_los = los_metrics.get("RayTracing", (None, None))
        else:
            rt_rmse_los, rt_bias_los = (None, None)

        # NLOS metrics
        if not nlos_data.empty:
            ref_nlos = cfg["ref_nlos"]
            print(f"[INFO] NLOS metrics vs {ref_nlos}:")
            nlos_metrics = compute_metrics_vs_reference(nlos_data, ref_nlos)
            for model, (rmse, bias) in nlos_metrics.items():
                print(f"  {model:12s} (NLOS): RMSE = {rmse:6.2f} dB, Bias = {bias:+6.2f} dB")
            rt_rmse_nlos, rt_bias_nlos = nlos_metrics.get("RayTracing", (None, None))
        else:
            rt_rmse_nlos, rt_bias_nlos = (None, None)

        summary_rows.append({
            "Scenario": cfg["id"],
            "Label": cfg["label"],
            "FreqMHz": cfg["freq_mhz"],
            "RT_RMSE_LOS_dB": rt_rmse_los,
            "RT_Bias_LOS_dB": rt_bias_los,
            "RT_RMSE_NLOS_dB": rt_rmse_nlos,
            "RT_Bias_NLOS_dB": rt_bias_nlos,
        })

    if summary_rows:
        print("\n" + "=" * 70)
        print("[SUMMARY] RayTracing LOS/NLOS performance")
        print("=" * 70)
        summary_df = pd.DataFrame(summary_rows)
        print(summary_df.to_string(index=False))


if __name__ == "__main__":
    main()
