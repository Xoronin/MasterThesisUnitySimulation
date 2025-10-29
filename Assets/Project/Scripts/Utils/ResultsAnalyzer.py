# analyze_unity_runs_1pdf_fixed.py
# One PDF per CSV with 4 vertically stacked plots:
# (A) RSRP vs Distance
# (C1) Path Loss vs Distance (Sim PL vs FSPL)
# (C2) Deviation from FSPL (error only)
# (D) Buildings ΔRSRP (OFF − ON), binned
#
# Expected columns (your schema):
# scenario,timestamp,propagation_model,rx_id,rx_x,rx_y,rx_z,rx_height_m,rx_sensitivity_dbm,rx_signal_strength_dbm
# tx_id,tx_x,tx_y,tx_z,tx_height_m,tx_power_dbm,tx_frequency_mhz,distance_m,buildings_on

from pathlib import Path
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

DATA_DIR = Path("Data/Exports")
OUT_DIR  = Path("Data/Plots")
OUT_DIR.mkdir(parents=True, exist_ok=True)

THRESHOLDS = [-95, -100, -110]
BIN_M = 20.0  # distance bin for buildings impact

def fspl_db(distance_m, frequency_mhz):
    # FSPL(dB) = 32.44 + 20 log10(f_MHz) + 20 log10(d_km)
    d_km = np.maximum(distance_m, 1e-3) / 1000.0
    f_mhz = np.maximum(frequency_mhz, 1e-6)
    return 32.44 + 20.0*np.log10(f_mhz) + 20.0*np.log10(d_km)

def cov_title_bits(df):
    s = []
    rsrp = df["rx_signal_strength_dbm"].to_numpy()
    for thr in THRESHOLDS:
        s.append(f"≥{thr} dBm: {(rsrp >= thr).mean()*100:.1f}%")
    s.append(f"≥Sens: {(rsrp >= df['rx_sensitivity_dbm']).mean()*100:.1f}%")
    return " | ".join(s)

def load_and_prepare(csv_path: Path) -> pd.DataFrame:
    df = pd.read_csv(csv_path)

    # Minimal casts/renames
    df["model"]     = df["propagation_model"].astype(str)
    df["buildings"]       = df["buildings_on"].map(lambda v: "ON" if str(v).lower() in ("true","1","on","yes") else "OFF")
    df["dist_m"]    = df["distance_m"].astype(float)
    df["freq_mhz"]  = df["tx_frequency_mhz"].astype(float)
    df["tx_p_dbm"]  = df["tx_power_dbm"].astype(float)
    df["rx_sensitivity_dbm"] = df["rx_sensitivity_dbm"].astype(float)
    df["rsrp_dbm"] = df["rx_signal_strength_dbm"].astype(float)

    # FSPL, theoretical received power, simulated path loss, error
    df["fspl_db"]      = fspl_db(df["dist_m"].to_numpy(), df["freq_mhz"].to_numpy())
    df["fspl_rx_dbm"]  = df["tx_p_dbm"] - df["fspl_db"]              # theoretical received power
    df["pl_sim_db"]    = df["tx_p_dbm"] - df["rsrp_dbm"]             # simulated path loss
    df["err_db"]       = df["rsrp_dbm"] - df["fspl_rx_dbm"]          # deviation from FSPL

    return df

def plot_one(csv_path: Path):
    df = load_and_prepare(csv_path)

    fig, axes = plt.subplots(4, 1, figsize=(8.2, 12), constrained_layout=True)
    axA, axC1, axC2, axD = axes

    # (A) RSRP vs Distance
    for (model, buildings), g in df.sort_values("dist_m").groupby(["model","buildings"]):
        axA.plot(g["dist_m"], g["rsrp_dbm"], label=f"{model} (Buildings {buildings})")
    axA.set_xlabel("Distance [m]")
    axA.set_ylabel("RSRP [dBm]")
    axA.set_title("(A) Received Power (RSRP) vs Distance")
    axA.grid(True, alpha=0.3)
    axA.legend(ncol=2, fontsize=8)

    # (C1) Path Loss vs Distance — Simulated vs FSPL (same units)
    # We plot simulated path loss and theoretical FSPL together.
    # If multiple models exist, show per-model PL curves + one FSPL reference.
    # FSPL is unique per (distance, frequency), but freq may vary slightly; we plot the per-row mean FSPL via binning for clarity.
    # Simpler: draw each (model,buildings) PL, and also a single FSPL curve computed from the median TX power & frequency.
    df_sorted = df.sort_values("dist_m")
    # FSPL reference: use per-row fspl_db (it varies only with distance & freq)
    axC1.plot(df_sorted["dist_m"], df_sorted["fspl_db"], linewidth=2, label="FSPL (theory)")
    for (model, buildings), g in df_sorted.groupby(["model","buildings"]):
        axC1.plot(g["dist_m"], g["pl_sim_db"], label=f"PL Sim — {model} (Buildings {buildings})")
    axC1.set_xlabel("Distance [m]")
    axC1.set_ylabel("Path Loss [dB]")
    axC1.set_title("(C1) Path Loss vs Distance — Simulated vs FSPL")
    axC1.grid(True, alpha=0.3)
    axC1.legend(ncol=2, fontsize=8)

    # (C2) Deviation from FSPL (error only)
    for (model, buildings), g in df_sorted.groupby(["model","buildings"]):
        axC2.plot(g["dist_m"], g["err_db"], label=f"{model} (Buildings {buildings})")
    axC2.axhline(0.0, linewidth=1)
    # Symmetric y-limits around 0 with a cap
    err_min, err_max = float(df["err_db"].min()), float(df["err_db"].max())
    yabs = min(20.0, max(abs(err_min), abs(err_max)) + 1.0)
    axC2.set_ylim(-yabs, yabs)
    axC2.set_xlabel("Distance [m]")
    axC2.set_ylabel("Error vs FSPL [dB]")
    axC2.set_title("(C2) Deviation from FSPL (RSRP − (TxPower − FSPL))")
    axC2.grid(True, alpha=0.3)
    axC2.legend(ncol=2, fontsize=8)

    # (D) Buildings impact: ΔRSRP = OFF − ON (binned by distance)
    dmin, dmax = float(df["dist_m"].min()), float(df["dist_m"].max())
    edges = np.arange(np.floor(dmin / BIN_M) * BIN_M, np.ceil(dmax / BIN_M) * BIN_M + BIN_M, BIN_M)
    centers = (edges[:-1] + edges[1:]) / 2.0
    for model, g_model in df.groupby("model"):
        g_off = g_model[g_model["buildings"] == "OFF"]
        g_on  = g_model[g_model["buildings"] == "ON"]
        if len(g_off) == 0 or len(g_on) == 0:
            continue
        off_bin = pd.cut(g_off["dist_m"], edges, labels=False)
        on_bin  = pd.cut(g_on["dist_m"], edges, labels=False)
        m_off = pd.Series(g_off["rsrp_dbm"].values).groupby(off_bin).mean()
        m_on  = pd.Series(g_on["rsrp_dbm"].values).groupby(on_bin).mean()
        idx = sorted(set(m_off.dropna().index).intersection(set(m_on.dropna().index)))
        if not idx:
            continue
        delta = (m_off.loc[idx].to_numpy() - m_on.loc[idx].to_numpy())
        axD.plot(centers[idx], delta, marker='o', label=f"{model}")
    axD.set_xlabel("Distance [m] (bin center)")
    axD.set_ylabel("ΔRSRP (OFF − ON) [dB]")
    axD.set_title(f"(D) Buildings impact (bin = {int(BIN_M)} m)")
    axD.grid(True, alpha=0.3)
    axD.legend(ncol=3, fontsize=8)

    # Title
    run_title = f"{df['scenario'].iloc[0]} — models={df['model'].unique().tolist()} — f≈{df['freq_mhz'].median():.0f} MHz"
    fig.suptitle(f"{run_title}\n{cov_title_bits(df)}", fontsize=11)

    # Save
    out_dir = OUT_DIR / csv_path.stem
    out_dir.mkdir(parents=True, exist_ok=True)
    pdf_path = out_dir / f"run_{csv_path.stem}.pdf"
    fig.savefig(pdf_path, bbox_inches="tight")
    plt.close(fig)
    print(f"✓ {pdf_path}")

    # Minimal numeric summary (handy for tables)
    summary = (
        df.groupby(["model","buildings"])
          .agg(n=("rsrp_dbm","size"),
               mean_rsrp=("rsrp_dbm","mean"),
               std_rsrp=("rsrp_dbm","std"),
               mean_err=("err_db","mean"))
          .reset_index()
    )
    summary.to_csv(out_dir / f"summary_{csv_path.stem}.csv", index=False)

def main():
    csvs = sorted(DATA_DIR.glob("*.csv"))
    if not csvs:
        print(f"⚠️ No CSVs found in {DATA_DIR.resolve()}")
        return
    for csv in csvs:
        plot_one(csv)

if __name__ == "__main__":
    main()
