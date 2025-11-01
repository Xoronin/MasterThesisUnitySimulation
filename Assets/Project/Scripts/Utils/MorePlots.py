
# -*- coding: utf-8 -*-
"""
Unity RF Analysis - All-in-One
--------------------------------
Generates thesis-ready plots/tables from Unity simulation CSV exports.
Assumes **one CSV per scenario run**; auto-detects scenario from the 'scenario'
column (if unique) or falls back to the filename.

Usage examples:
  python analyze_unity_allinone.py runs/*.csv
  python analyze_unity_allinone.py my_s2_corner.csv my_s3_canyon.csv -o results_plots

CSV columns (robust to gaps):
  Required for most:  distance_m, rsrp_dbm (or rx_signal_strength_dbm), tx_power_dbm(optional)
  Helpful/optional:   model, scenario, frequency_mhz, buildings_on(0/1), material, incidence_deg,
                      interference_dbm, bandwidth_hz, noise_figure_db
"""
from __future__ import annotations
import os, sys, glob, argparse, math
from typing import Optional, Sequence, Tuple, Dict

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

# ------------------------------
# Utilities
# ------------------------------

def load_runs_csv(path: str) -> pd.DataFrame:
    return pd.read_csv(path)

def _ensure_rsrp_dbm(df: pd.DataFrame) -> pd.DataFrame:
    """Create/normalize an 'rsrp_dbm' column if only 'rx_signal_strength_dbm' or loss exists."""
    out = df.copy()
    if "rsrp_dbm" in out.columns:
        return out
    if "rx_signal_strength_dbm" in out.columns:
        out["rsrp_dbm"] = out["rx_signal_strength_dbm"].astype(float)
        return out
    if "pl_db" in out.columns and "tx_power_dbm" in out.columns:
        out["rsrp_dbm"] = out["tx_power_dbm"].astype(float) - out["pl_db"].astype(float)
        return out
    return out

def _fspl_db(distance_m: np.ndarray, freq_mhz: float) -> np.ndarray:
    # FSPL(dB) = 32.44 + 20 log10(d_km) + 20 log10(f_MHz)
    d_km = np.maximum(distance_m, 1e-6) * 0.001
    f = max(freq_mhz, 1e-6)
    return 32.44 + 20.0*np.log10(d_km) + 20.0*np.log10(f)

def _format_ax(ax, title: str, xlabel: str, ylabel: str):
    ax.set_title(title)
    ax.set_xlabel(xlabel)
    ax.set_ylabel(ylabel)
    ax.grid(True, which="both", linestyle=":")

def _has_cols(df: pd.DataFrame, cols):
    return all(c in df.columns for c in cols)

# ------------------------------
# Plots
# ------------------------------

def plot_corner_nlos(df: pd.DataFrame, scenario: Optional[str]=None, freq_mhz: Optional[float]=None) -> plt.Figure:
    """NLOS 'around the corner' RSRP vs distance; overlays models and FSPL baseline if available."""
    data = _ensure_rsrp_dbm(df)
    if scenario is not None and "scenario" in data.columns:
        data = data[data["scenario"]==scenario]
    if "distance_m" not in data.columns or "rsrp_dbm" not in data.columns:
        raise ValueError("Need distance_m and RSRP")

    # Pick frequency
    if freq_mhz is None:
        if "frequency_mhz" in data.columns and not data["frequency_mhz"].dropna().empty:
            freq_mhz = float(data["frequency_mhz"].iloc[0])
        else:
            freq_mhz = 3500.0

    d = data["distance_m"].to_numpy()
    fspl = _fspl_db(d, freq_mhz)
    tx = data["tx_power_dbm"].iloc[0] if "tx_power_dbm" in data.columns and not data["tx_power_dbm"].dropna().empty else 30.0
    fspl_rsrp = tx - fspl

    fig = plt.figure(figsize=(8,5))
    ax = fig.add_subplot(111)

    if "model" in data.columns:
        for m in sorted(data["model"].unique()):
            dm = data[data["model"]==m]
            ax.plot(dm["distance_m"], dm["rsrp_dbm"], linestyle="-", label=str(m))
    else:
        ax.plot(data["distance_m"], data["rsrp_dbm"], linestyle="-", label="Simulation")

    ax.plot(d, fspl_rsrp, linestyle="--", label="FSPL (baseline)")
    _format_ax(ax, "Corner / NLOS: RSRP vs Distance", "Distance [m]", "RSRP [dBm]")
    ax.legend()
    return fig

def plot_canyon_reflection(df: pd.DataFrame, scenario: Optional[str]=None) -> plt.Figure:
    """Street-canyon reflections: overlay Buildings ON vs OFF."""
    data = _ensure_rsrp_dbm(df)
    if scenario is not None and "scenario" in data.columns:
        data = data[data["scenario"]==scenario]
    if "distance_m" not in data.columns or "rsrp_dbm" not in data.columns:
        raise ValueError("Need distance_m and RSRP")

    fig = plt.figure(figsize=(8,5))
    ax = fig.add_subplot(111)
    if "buildings_on" in data.columns:
        for on in sorted(data["buildings_on"].unique()):
            dm = data[data["buildings_on"]==on]
            label = "Buildings ON" if int(on)==1 else "Buildings OFF"
            ax.plot(dm["distance_m"], dm["rsrp_dbm"], linestyle="-", label=label)
    else:
        ax.plot(data["distance_m"], data["rsrp_dbm"], linestyle="-", label="Simulation")

    _format_ax(ax, "Street Canyon: RSRP vs Distance", "Distance [m]", "RSRP [dBm]")
    ax.legend()
    return fig

def plot_material_sweep(df: pd.DataFrame, scenario: Optional[str]=None, group_by_incidence: bool=True) -> plt.Figure:
    """Bar plot: ΔRSRP vs baseline across materials; grouped by incidence if available."""
    data = _ensure_rsrp_dbm(df)
    if scenario is not None and "scenario" in data.columns:
        data = data[data["scenario"]==scenario]
    for c in ["material","rsrp_dbm"]:
        if c not in data.columns:
            raise ValueError("Need 'material' and RSRP")

    if group_by_incidence and "incidence_deg" in data.columns:
        gcols = ["material","incidence_deg"]
    else:
        gcols = ["material"]

    med = data.groupby(gcols)["rsrp_dbm"].median().reset_index()
    baseline_candidates = med.copy()
    if "material" in med.columns:
        baseline_candidates = med[med["material"].str.lower().isin(["air","none","free","baseline"])]
    baseline_val = med["rsrp_dbm"].max() if baseline_candidates.empty else baseline_candidates["rsrp_dbm"].max()
    med["delta_db"] = med["rsrp_dbm"] - float(baseline_val)

    fig = plt.figure(figsize=(9,5))
    ax = fig.add_subplot(111)
    if "incidence_deg" in med.columns and group_by_incidence:
        incidences = sorted(med["incidence_deg"].unique())
        materials = list(med["material"].unique())
        width = 0.8 / max(len(incidences),1)
        for i, inc in enumerate(incidences):
            sub = med[med["incidence_deg"]==inc]
            vals = [float(sub[sub["material"]==m]["delta_db"].iloc[0]) if not sub[sub["material"]==m].empty else np.nan for m in materials]
            x = np.arange(len(materials)) + i*width
            ax.bar(x, vals, width=width, label=f"{inc}°")
        ax.set_xticks(np.arange(len(materials)) + (len(incidences)-1)*width/2.0)
        ax.set_xticklabels(materials, rotation=20, ha="right")
        _format_ax(ax, "Material Sweep: ΔRSRP vs Baseline", "Material", "ΔRSRP [dB]")
        ax.legend()
    else:
        materials = med["material"].tolist()
        vals = med["delta_db"].tolist()
        ax.bar(materials, vals)
        ax.set_xticklabels(materials, rotation=20, ha="right")
        _format_ax(ax, "Material Sweep: ΔRSRP vs Baseline", "Material", "ΔRSRP [dB]")
    return fig

def plot_frequency_compare(df: pd.DataFrame, scenario: Optional[str]=None, thresholds_dbm: Sequence[float]=(-95,-100,-110)) -> Tuple[plt.Figure, pd.DataFrame]:
    """RSRP vs distance per frequency; also returns a coverage % table for thresholds."""
    data = _ensure_rsrp_dbm(df)
    if scenario is not None and "scenario" in data.columns:
        data = data[data["scenario"]==scenario]
    if "frequency_mhz" not in data.columns:
        raise ValueError("Need frequency_mhz column")
    if "distance_m" not in data.columns or "rsrp_dbm" not in data.columns:
        raise ValueError("Need distance_m and RSRP")

    fig = plt.figure(figsize=(8,5))
    ax = fig.add_subplot(111)
    for f in sorted(data["frequency_mhz"].unique()):
        dfreq = data[data["frequency_mhz"]==f]
        ax.plot(dfreq["distance_m"], dfreq["rsrp_dbm"], linestyle="-", label=f"{f:.0f} MHz")
    _format_ax(ax, "Frequency Compare: RSRP vs Distance", "Distance [m]", "RSRP [dBm]")
    ax.legend()

    rows = []
    for f in sorted(data["frequency_mhz"].unique()):
        dfreq = data[data["frequency_mhz"]==f]
        cov = {}
        for th in thresholds_dbm:
            cov[f"pct_ge_{int(th)}"] = 100.0 * float((dfreq["rsrp_dbm"]>=th).mean())
        cov["frequency_mhz"] = float(f)
        rows.append(cov)
    table = pd.DataFrame(rows).set_index("frequency_mhz").sort_index()
    return fig, table

# ------------------------------
# SINR & Throughput
# ------------------------------

def _thermal_noise_dbm(bandwidth_hz: float, noise_figure_db: float=5.0) -> float:
    # kTB (dBm) ≈ -174 dBm/Hz + 10*log10(BW) + NF
    return -174.0 + 10.0*math.log10(max(bandwidth_hz,1.0)) + float(noise_figure_db)

def _to_linear_dbm(dbm: np.ndarray) -> np.ndarray:
    return np.power(10.0, dbm/10.0)

def _to_dbm(linear: np.ndarray) -> np.ndarray:
    linear = np.maximum(linear, 1e-30)
    return 10.0*np.log10(linear)

def compute_sinr_and_throughput(df: pd.DataFrame, bandwidth_hz: float=20e6, noise_figure_db: float=5.0) -> pd.DataFrame:
    """Adds 'sinr_db' and 'throughput_mbps' (Shannon w/ cap) to DataFrame."""
    data = _ensure_rsrp_dbm(df).copy()
    if "rsrp_dbm" not in data.columns:
        raise ValueError("Need RSRP to compute SINR.")
    if "interference_dbm" not in data.columns:
        data["interference_dbm"] = -np.inf  # effectively 0 linear

    noise_dbm = _thermal_noise_dbm(bandwidth_hz, noise_figure_db)
    s = _to_linear_dbm(data["rsrp_dbm"].to_numpy())
    i = _to_linear_dbm(data["interference_dbm"].to_numpy())
    n = _to_linear_dbm(np.full_like(s, noise_dbm))

    sinr_linear = s / (i + n)
    data["sinr_db"] = _to_dbm(sinr_linear)

    # Throughput upper bound: BW * log2(1+SINR), cap SE to ~7.6 bps/Hz
    se = np.log2(1.0 + sinr_linear)
    se_cap = np.minimum(se, 7.6)
    data["throughput_mbps"] = (bandwidth_hz * se_cap) / 1e6
    return data

def plot_throughput_histogram(df_with_sinr: pd.DataFrame) -> plt.Figure:
    if "throughput_mbps" not in df_with_sinr.columns:
        raise ValueError("Run compute_sinr_and_throughput first.")
    fig = plt.figure(figsize=(7,5))
    ax = fig.add_subplot(111)
    ax.hist(df_with_sinr["throughput_mbps"].dropna().to_numpy(), bins=30)
    _format_ax(ax, "Throughput Distribution", "Throughput [Mbps]", "Count")
    return fig

# ------------------------------
# Model ablation & buildings impact
# ------------------------------

def summarize_models_vs_fspl(df: pd.DataFrame, scenario: Optional[str]=None, freq_mhz: Optional[float]=None) -> pd.DataFrame:
    """Table: RMSE vs FSPL (dB), median error (dB), count per model."""
    data = _ensure_rsrp_dbm(df)
    if scenario is not None and "scenario" in data.columns:
        data = data[data["scenario"]==scenario]
    for r in ["distance_m","rsrp_dbm"]:
        if r not in data.columns:
            raise ValueError("Need distance_m and RSRP.")
    if freq_mhz is None:
        if "frequency_mhz" in data.columns and not data["frequency_mhz"].dropna().empty:
            freq_mhz = float(data["frequency_mhz"].iloc[0])
        else:
            freq_mhz = 3500.0

    d = data["distance_m"].to_numpy()
    fspl_db = _fspl_db(d, freq_mhz)
    tx = data["tx_power_dbm"].iloc[0] if "tx_power_dbm" in data.columns and not data["tx_power_dbm"].dropna().empty else 30.0
    fspl_rsrp = tx - fspl_db

    rows = []
    if "model" not in data.columns:
        err = data["rsrp_dbm"].to_numpy() - fspl_rsrp
        rmse = float(np.sqrt(np.mean(err**2)))
        rows.append({"model":"Simulation","rmse_vs_fspl_db":rmse,"median_error_db":float(np.median(err)),"count":len(err)})
    else:
        for m in sorted(data["model"].unique()):
            dm = data[data["model"]==m]
            d2 = dm["distance_m"].to_numpy()
            fspl_db2 = _fspl_db(d2, freq_mhz)
            err = dm["rsrp_dbm"].to_numpy() - (tx - fspl_db2)
            rmse = float(np.sqrt(np.mean(err**2)))
            rows.append({"model":str(m),"rmse_vs_fspl_db":rmse,"median_error_db":float(np.median(err)),"count":len(err)})
    return pd.DataFrame(rows)

def plot_buildings_delta_by_distance(df: pd.DataFrame, scenario: Optional[str]=None, bins: int=12) -> plt.Figure:
    """Bars: OFF-ON ΔRSRP in distance bins; >0 helps (reflection), <0 hurts (shadow)."""
    data = _ensure_rsrp_dbm(df)
    if scenario is not None and "scenario" in data.columns:
        data = data[data["scenario"]==scenario]
    if "buildings_on" not in data.columns or "distance_m" not in data.columns:
        raise ValueError("Need 'buildings_on' and 'distance_m' columns")
    data = data.copy()
    data["dist_bin"] = pd.qcut(data["distance_m"], q=bins, duplicates="drop")
    med = data.groupby(["dist_bin","buildings_on"])["rsrp_dbm"].median().reset_index()
    piv = med.pivot(index="dist_bin", columns="buildings_on", values="rsrp_dbm").rename(columns={0:"OFF",1:"ON"})
    piv["delta_off_on_db"] = piv["OFF"] - piv["ON"]

    fig = plt.figure(figsize=(8,4))
    ax = fig.add_subplot(111)
    labels = [f"{float(iv.left):.0f}-{float(iv.right):.0f}" for iv in piv.index]
    ax.bar(np.arange(len(labels)), piv["delta_off_on_db"].to_numpy())
    ax.set_xticks(np.arange(len(labels)))
    ax.set_xticklabels(labels, rotation=20, ha="right")
    _format_ax(ax, "Buildings Impact by Distance Bin (OFF-ON)", "Distance bin [m]", "ΔRSRP [dB]")
    return fig

# ------------------------------
# Scenario handling & CLI main
# ------------------------------

def _scenario_name_from(df: pd.DataFrame, csv_path: str) -> str:
    if "scenario" in df.columns:
        vals = [str(x) for x in df["scenario"].dropna().unique()]
        if len(vals) == 1:
            return vals[0]
    base = os.path.basename(csv_path)
    return os.path.splitext(base)[0]

def process_csv(csv_path: str, out_root: str):
    df = load_runs_csv(csv_path)
    scenario = _scenario_name_from(df, csv_path)
    out_dir = os.path.join(out_root, scenario)
    os.makedirs(out_dir, exist_ok=True)
    print(f"=== {scenario} ===")
    print(f"Input:  {csv_path}")
    print(f"Output: {out_dir}")

    # 1) Corner/NLOS plot
    try:
        if _has_cols(df, ["distance_m"]) and ("rsrp_dbm" in df.columns or "rx_signal_strength_dbm" in df.columns):
            fig = plot_corner_nlos(df, scenario=None)
            fig.savefig(os.path.join(out_dir, f"{scenario}_corner_nlos.png"), dpi=200)
            plt.close(fig)
            print(" [OK] Corner/NLOS RSRP vs distance")
        else:
            print(" [SKIP] Corner/NLOS (missing distance/RSRP)")
    except Exception as e:
        print(f" [WARN] Corner/NLOS failed: {e}")

    # 2) Canyon reflections (needs buildings_on)
    try:
        if "buildings_on" in df.columns and _has_cols(df, ["distance_m"]):
            fig = plot_canyon_reflection(df, scenario=None)
            fig.savefig(os.path.join(out_dir, f"{scenario}_canyon_reflection.png"), dpi=200)
            plt.close(fig)
            print(" [OK] Canyon reflection (ON/OFF)")
        else:
            print(" [SKIP] Canyon reflection (needs buildings_on + distance)")
    except Exception as e:
        print(f" [WARN] Canyon reflection failed: {e}")

    # 3) Material sweep
    try:
        if "material" in df.columns and ("rsrp_dbm" in df.columns or "rx_signal_strength_dbm" in df.columns):
            fig = plot_material_sweep(df, scenario=None, group_by_incidence=True)
            fig.savefig(os.path.join(out_dir, f"{scenario}_material_sweep.png"), dpi=200)
            plt.close(fig)
            print(" [OK] Material sweep")
        else:
            print(" [SKIP] Material sweep (needs material + RSRP)")
    except Exception as e:
        print(f" [WARN] Material sweep failed: {e}")

    # 4) Frequency compare
    try:
        if "frequency_mhz" in df.columns and _has_cols(df, ["distance_m"]):
            fig, cov = plot_frequency_compare(df, scenario=None, thresholds_dbm=(-95, -100, -110))
            fig.savefig(os.path.join(out_dir, f"{scenario}_frequency_compare.png"), dpi=200)
            plt.close(fig)
            cov.to_csv(os.path.join(out_dir, f"{scenario}_frequency_coverage_table.csv"))
            print(" [OK] Frequency compare + coverage table")
        else:
            print(" [SKIP] Frequency compare (needs frequency_mhz + distance)")
    except Exception as e:
        print(f" [WARN] Frequency compare failed: {e}")

    # 5) SINR + throughput
    try:
        if ("rsrp_dbm" in df.columns or "rx_signal_strength_dbm" in df.columns):
            df2 = compute_sinr_and_throughput(df, bandwidth_hz=20e6, noise_figure_db=5.0)
            fig = plot_throughput_histogram(df2)
            fig.savefig(os.path.join(out_dir, f"{scenario}_throughput_hist.png"), dpi=200)
            plt.close(fig)
            df2[["sinr_db", "throughput_mbps"]].to_csv(os.path.join(out_dir, f"{scenario}_sinr_throughput.csv"), index=False)
            print(" [OK] SINR/throughput")
        else:
            print(" [SKIP] SINR/throughput (needs RSRP)")
    except Exception as e:
        print(f" [WARN] SINR/throughput failed: {e}")

    # 6) Model ablation vs FSPL
    try:
        if "model" in df.columns and ("rsrp_dbm" in df.columns or "rx_signal_strength_dbm" in df.columns) and "distance_m" in df.columns:
            tbl = summarize_models_vs_fspl(df, scenario=None)
            tbl.to_csv(os.path.join(out_dir, f"{scenario}_model_ablation_rmse.csv"), index=False)
            print(" [OK] Model ablation (RMSE vs FSPL)")
        else:
            print(" [SKIP] Ablation (needs model + distance + RSRP)")
    except Exception as e:
        print(f" [WARN] Ablation failed: {e}")

    # 7) Buildings OFF-ON Δ by distance bins
    try:
        if "buildings_on" in df.columns and "distance_m" in df.columns and ("rsrp_dbm" in df.columns or "rx_signal_strength_dbm" in df.columns):
            fig = plot_buildings_delta_by_distance(df, scenario=None, bins=12)
            fig.savefig(os.path.join(out_dir, f"{scenario}_buildings_delta_by_distance.png"), dpi=200)
            plt.close(fig)
            print(" [OK] Buildings impact by distance")
        else:
            print(" [SKIP] Buildings Δ (needs buildings_on + distance + RSRP)")
    except Exception as e:
        print(f" [WARN] Buildings Δ failed: {e}")

    print(f" Done: {scenario}")

def main(argv=None):
    ap = argparse.ArgumentParser(description="Unity RF analysis (one CSV per scenario).")
    ap.add_argument("inputs", nargs="+", help="CSV file(s) or globs. Example: runs/*.csv")
    ap.add_argument("-o", "--out", default="Data/Plots", help="Output root folder")
    args = ap.parse_args(argv)

    # Expand globs and de-dup
    files = []
    for pat in args.inputs:
        matched = glob.glob(pat)
        if not matched and os.path.isfile(pat):
            matched = [pat]
        files.extend(matched)
    files = sorted(set(files))
    if not files:
        print("No CSV files found. Pass file paths or globs.", file=sys.stderr)
        return 2

    os.makedirs(args.out, exist_ok=True)

    for f in files:
        try:
            process_csv(f, args.out)
        except Exception as e:
            print(f"[ERROR] Failed {f}: {e}")

    print(f"All outputs under: {os.path.abspath(args.out)}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
