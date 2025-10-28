# -*- coding: utf-8 -*-
"""
Analyze Unity RF simulation runs (one CSV per experiment).
- Auto-detects common column names
- Derives FSPL & Excess Path Loss (EPL)
- Saves per-run plots (PDF) & a numeric summary CSV
- Optionally makes combined plots across all runs
"""

from pathlib import Path
import math
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

# ------------------------
# Config
# ------------------------
DATA_DIR = Path("Data/Exports")
PLOT_DIR = Path("Data/Plots")
PLOT_DIR.mkdir(parents=True, exist_ok=True)

# Coverage thresholds (dBm)
THRESHOLDS = [-95, -100, -110]

# ------------------------
# Helpers
# ------------------------
def pick_col(df: pd.DataFrame, candidates, casefold=True):
    """Return first matching column name from candidates or None."""
    cols = list(df.columns)
    if casefold:
        lowmap = {c.lower(): c for c in cols}
        for cand in candidates:
            if cand.lower() in lowmap:
                return lowmap[cand.lower()]
        return None
    else:
        for cand in candidates:
            if cand in cols:
                return cand
        return None

def ensure_distance(df, rx_xyz_cols, tx_xyz_cols):
    """
    Ensure/compute distance in meters.
    Priority: explicit distance column; else derive from positions.
    """
    dist_col = pick_col(df, ["distance_m", "distance", "rx_tx_distance_m"])
    if dist_col is not None:
        return df[dist_col].astype(float)

    rx_x, rx_y, rx_z = rx_xyz_cols
    tx_x, tx_y, tx_z = tx_xyz_cols
    if all(c is not None for c in [rx_x, rx_y, rx_z, tx_x, tx_y, tx_z]):
        rx = df[[rx_x, rx_y, rx_z]].astype(float).to_numpy()
        tx = df[[tx_x, tx_y, tx_z]].astype(float).to_numpy()
        return np.linalg.norm(rx - tx, axis=1)
    # last resort: index as pseudo-distance (not ideal, but avoids crash)
    return np.arange(len(df), dtype=float)

def ensure_frequency_mhz(df):
    """
    Return frequency in MHz from any of:
    - frequency_hz / freq_hz
    - frequency_mhz / freq_mhz
    - frequency (assume Hz if > 10^5)
    """
    f_hz = pick_col(df, ["frequency_hz", "freq_hz"])
    f_mhz = pick_col(df, ["frequency_mhz", "freq_mhz"])
    f = pick_col(df, ["frequency"])  # ambiguous; detect unit by magnitude

    if f_mhz is not None:
        return df[f_mhz].astype(float)
    if f_hz is not None:
        return df[f_hz].astype(float) / 1e6
    if f is not None:
        vals = df[f].astype(float)
        # if median looks like Hz, convert
        return np.where(vals.median() > 1e5, vals / 1e6, vals)
    # fallback default (so plots work); you can change this
    return pd.Series(np.full(len(df), 2100.0), index=df.index)

def fspl_db(distance_m, frequency_mhz):
    """
    Free-Space Path Loss (dB)
    FSPL(dB) = 32.44 + 20 log10(f_MHz) + 20 log10(d_km)
    """
    d_km = np.maximum(distance_m, 1e-3) / 1000.0  # avoid log(0)
    f_mhz = np.maximum(frequency_mhz, 1e-6)
    return 32.44 + 20.0 * np.log10(f_mhz) + 20.0 * np.log10(d_km)

def ensure_rx_power_dbm(df):
    power_col = pick_col(df, [
        "rx_power_dbm", "rsrp_dbm", "received_power_dbm",
        "currentSignalStrength", "rx_power", "RSRP"
    ])
    if power_col is None:
        raise ValueError("Could not find a received power column (e.g., rx_power_dbm / RSRP).")
    return df[power_col].astype(float)

def ensure_model_label(df):
    mcol = pick_col(df, ["model", "propagation_model", "modelName"])
    if mcol is None:
        return pd.Series(["(model?)"] * len(df), index=df.index)
    return df[mcol].astype(str)

def ensure_building_flag(df):
    bcol = pick_col(df, ["buildings_on", "buildings", "buildingsEnabled", "bld_on"])
    if bcol is None:
        return pd.Series(["unknown"] * len(df), index=df.index)
    # normalize to ON/OFF strings
    vals = df[bcol]
    def norm(v):
        if isinstance(v, str):
            vlow = v.strip().lower()
            if vlow in ("1","true","on","yes"): return "ON"
            if vlow in ("0","false","off","no"): return "OFF"
            return v
        if isinstance(v, (int, float)):
            return "ON" if v != 0 else "OFF"
        if isinstance(v, bool):
            return "ON" if v else "OFF"
        return str(v)
    return vals.map(norm)

def ensure_path_loss(df, rx_power_dbm):
    """
    If 'path_loss_db' exists, use it. Else try tx_eirp_dbm - rx_power_dbm.
    Else compute FSPL-based placeholder (not ideal, but avoids crash).
    """
    pl_col = pick_col(df, ["path_loss_db", "pathloss_db"])
    if pl_col is not None:
        return df[pl_col].astype(float)

    eirp_col = pick_col(df, ["tx_eirp_dbm", "eirp_dbm", "tx_power_dbm", "transmitterPower"])
    if eirp_col is not None:
        return df[eirp_col].astype(float) - rx_power_dbm

    # last resort: use FSPL as placeholder (EPL becomes ~0 in LOS)
    freq_mhz = ensure_frequency_mhz(df)
    # we'll set distance soon and re-run EPL anyway in the caller
    return pd.Series(np.nan, index=df.index)

def load_and_normalize(path: Path):
    df = pd.read_csv(path)
    # positional columns (optional)
    rx_x = pick_col(df, ["rx_x","rx_world_x","rxX"])
    rx_y = pick_col(df, ["rx_y","rx_world_y","rxY"])
    rx_z = pick_col(df, ["rx_z","rx_world_z","rxZ"])

    tx_x = pick_col(df, ["tx_x","tx_world_x","txX"])
    tx_y = pick_col(df, ["tx_y","tx_world_y","txY"])
    tx_z = pick_col(df, ["tx_z","tx_world_z","txZ"])

    rx_xyz_cols = (rx_x, rx_y, rx_z)
    tx_xyz_cols = (tx_x, tx_y, tx_z)

    # normalize fields
    freq_mhz = ensure_frequency_mhz(df)
    rx_power = ensure_rx_power_dbm(df)
    distance_m = ensure_distance(df, rx_xyz_cols, tx_xyz_cols)
    model = ensure_model_label(df)
    buildings = ensure_building_flag(df)

    # path loss and EPL
    path_loss = ensure_path_loss(df, rx_power)
    fspl = fspl_db(distance_m, freq_mhz)
    # if path_loss had NaNs, try to fill from FSPL + (mean offset of rx_power)
    if path_loss.isna().any():
        # if tx EIRP unknown, we can use PL = FSPL + EPL_est; but we don't have EPL
        # set PL = FSPL so EPL = 0 as neutral fallback
        path_loss = path_loss.fillna(fspl)

    epl = path_loss - fspl

    # index proxy
    rx_idx = pick_col(df, ["rx_anchor_idx","rx_index","receiver_index","rx_id"])
    if rx_idx is None:
        rx_idx = pd.Series(np.arange(len(df)), index=df.index)
    else:
        rx_idx = df[rx_idx]

    out = pd.DataFrame({
        "file": path.name,
        "model": model,
        "buildings": buildings,
        "frequency_mhz": freq_mhz,
        "distance_m": distance_m.astype(float),
        "rx_power_dbm": rx_power.astype(float),
        "path_loss_db": path_loss.astype(float),
        "fspl_db": fspl.astype(float),
        "excess_path_loss_db": epl.astype(float),
        "rx_index": rx_idx
    })
    return out

def coverage_percent(series_dbm, thr_dbm):
    return float((series_dbm >= thr_dbm).mean() * 100.0)

def plot_power_distance(df, title, outfile):
    # sort by distance for nicer lines
    fig = plt.figure(figsize=(8.0, 5.0))
    for (model, bld), g in df.sort_values("distance_m").groupby(["model","buildings"]):
        plt.plot(g["distance_m"], g["rx_power_dbm"], label=f"{model} (Bld {bld})")
    plt.xlabel("Distance [m]")
    plt.ylabel("Received Power [dBm]")
    plt.title(title)
    plt.grid(True, which="both", alpha=0.3)
    plt.legend()
    plt.tight_layout()
    fig.savefig(outfile, bbox_inches="tight")
    plt.close(fig)

def plot_epl_box(df, title, outfile):
    fig = plt.figure(figsize=(7.2, 4.8))
    labels = []
    data = []
    for (model, bld), g in df.groupby(["model","buildings"]):
        labels.append(f"{model}\n(Bld {bld})")
        data.append(g["excess_path_loss_db"].dropna().values)
    plt.boxplot(data, labels=labels, showfliers=False)
    plt.ylabel("Excess Path Loss [dB]")
    plt.title(title)
    plt.grid(axis="y", alpha=0.3)
    plt.tight_layout()
    fig.savefig(outfile, bbox_inches="tight")
    plt.close(fig)

def plot_coverage_bars(df, title, outfile):
    # build a small table of coverage per model/buildings/threshold
    rows = []
    for (model, bld), g in df.groupby(["model","buildings"]):
        for thr in THRESHOLDS:
            rows.append({
                "label": f"{model}\n(Bld {bld})",
                "threshold": thr,
                "coverage": coverage_percent(g["rx_power_dbm"], thr)
            })
    cdf = pd.DataFrame(rows)
    # pivot to draw grouped bars
    pivot = cdf.pivot(index="label", columns="threshold", values="coverage").fillna(0.0)
    x = np.arange(len(pivot))
    width = 0.22

    fig = plt.figure(figsize=(8.5, 5.0))
    for i, thr in enumerate(sorted(pivot.columns)):
        plt.bar(x + i*width - width, pivot[thr].values, width=width, label=f"≥ {thr} dBm")
    plt.xticks(x, pivot.index, rotation=0)
    plt.ylabel("Coverage [% of receivers]")
    plt.title(title)
    plt.grid(axis="y", alpha=0.3)
    plt.legend()
    plt.tight_layout()
    fig.savefig(outfile, bbox_inches="tight")
    plt.close(fig)

def summarize_run(df):
    rows = []
    for (model, bld), g in df.groupby(["model","buildings"]):
        row = {
            "model": model,
            "buildings": bld,
            "n_samples": len(g),
            "mean_rsrp_dbm": float(np.nanmean(g["rx_power_dbm"])),
            "mean_epl_db": float(np.nanmean(g["excess_path_loss_db"])),
            "std_epl_db": float(np.nanstd(g["excess_path_loss_db"])),
        }
        for thr in THRESHOLDS:
            row[f"coverage_ge_{thr}_pct"] = coverage_percent(g["rx_power_dbm"], thr)
        rows.append(row)
    return pd.DataFrame(rows)

# ------------------------
# Main
# ------------------------
def main():
    csvs = sorted(DATA_DIR.glob("*.csv"))
    if not csvs:
        print(f"⚠️  No CSVs found in {DATA_DIR}")
        return

    all_norm = []

    for csv in csvs:
        try:
            df = load_and_normalize(csv)
        except Exception as e:
            print(f"❌ Failed to parse {csv.name}: {e}")
            continue

        all_norm.append(df)

        # per-run plots
        stem = csv.stem
        plot_power_distance(
            df, f"Power vs Distance — {stem}",
            PLOT_DIR / f"power_distance__{stem}.pdf"
        )
        plot_epl_box(
            df, f"Excess Path Loss — {stem}",
            PLOT_DIR / f"epl_box__{stem}.pdf"
        )
        plot_coverage_bars(
            df, f"Coverage — {stem}",
            PLOT_DIR / f"coverage__{stem}.pdf"
        )

        # per-run summary CSV
        summary = summarize_run(df)
        summary.to_csv(PLOT_DIR / f"summary__{stem}.csv", index=False)

        print(f"✅ Processed {csv.name}")

    # combined plots (if multiple files)
    if len(all_norm) >= 2:
        big = pd.concat(all_norm, ignore_index=True)
        plot_power_distance(big, "Power vs Distance — Combined Runs", PLOT_DIR / "combined_power_distance.pdf")
        plot_epl_box(big, "Excess Path Loss — Combined Runs", PLOT_DIR / "combined_epl_box.pdf")
        plot_coverage_bars(big, "Coverage — Combined Runs", PLOT_DIR / "combined_coverage.pdf")
        # also write one combined summary
        summarize_run(big).to_csv(PLOT_DIR / "summary__combined.csv", index=False)
        print("📊 Combined plots saved.")

    print("Done. Plots in:", PLOT_DIR.resolve())

if __name__ == "__main__":
    main()
