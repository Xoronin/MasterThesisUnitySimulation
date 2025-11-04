
from pathlib import Path
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import argparse

# ---------- Config ----------
DEFAULT_DATA_DIR = "Data/Exports"
DEFAULT_OUT_DIR  = "Data/Plots/Overlays"
VALID_BUILDINGS = {"ON","OFF","BOTH"}

def fspl_db(distance_m, frequency_mhz):
    # FSPL(dB) = 32.44 + 20 log10(f_MHz) + 20 log10(d_km)
    d_km = np.maximum(np.asarray(distance_m, float), 1e-3) / 1000.0
    f_mhz = np.maximum(np.asarray(frequency_mhz, float), 1e-6)
    return 32.44 + 20.0*np.log10(f_mhz) + 20.0*np.log10(d_km)

def load_csv(csv_path: Path) -> pd.DataFrame:
    df = pd.read_csv(csv_path)
    # expected columns:
    # scenario,timestamp,propagation_model,rx_id,rx_x,rx_y,rx_z,rx_height_m,rx_sensitivity_dbm,rx_signal_strength_dbm
    # tx_id,tx_x,tx_y,tx_z,tx_height_m,tx_power_dbm,tx_frequency_mhz,distance_m,buildings_on
    df["scenario"]  = df["scenario"].astype(str)
    df["model"]     = df["propagation_model"].astype(str)
    df["dist_m"]    = df["distance_m"].astype(float)
    df["freq_mhz"]  = df["tx_frequency_mhz"].astype(float)
    df["tx_p_dbm"]  = df["tx_power_dbm"].astype(float)
    df["rsrp_dbm"]  = df["rx_signal_strength_dbm"].astype(float)
    df["buildings"] = df["buildings_on"].map(lambda v: "ON" if str(v).lower() in ("true","1","on","yes") else "OFF")
    return df

def prepare_all(data_dir: Path) -> pd.DataFrame:
    frames = []
    for p in sorted(data_dir.glob("*.csv")):
        try:
            frames.append(load_csv(p))
        except Exception as e:
            print(f"⚠️ Skipping {p.name}: {e}")
    if not frames:
        raise SystemExit(f"No CSVs found in {data_dir.resolve()}")
    df = pd.concat(frames, ignore_index=True)
    return df

def plot_overlays(df: pd.DataFrame, out_dir: Path, buildings_filter: str, fspl_ref: bool, min_points: int):
    out_dir.mkdir(parents=True, exist_ok=True)

    # We produce ONE figure per (scenario, rounded frequency in MHz).
    # Rounding groups small freq drifts (e.g., 3500.1 -> 3500).
    df["freq_mhz_round"] = df["freq_mhz"].round(0)
    # Filter buildings if needed
    if buildings_filter in {"ON","OFF"}:
        df = df[df["buildings"] == buildings_filter]

    # Iterate groups
    for (scenario, f_mhz), g in df.groupby(["scenario","freq_mhz_round"]):
        # optional guard
        if len(g) < min_points:
            continue

        # Sort by distance for nicer lines
        g = g.sort_values("dist_m")

        # Create figure (single-plot per tool constraints)
        fig, ax = plt.subplots(figsize=(8.5, 5.5), constrained_layout=True)

        # Plot each model (+ buildings when BOTH)
        if buildings_filter == "BOTH":
            # legend distinguishes model + buildings state
            for (model, bflag), gg in g.groupby(["model","buildings"]):
                ax.plot(gg["dist_m"].to_numpy(), gg["rsrp_dbm"].to_numpy(), label=f"{model} (Buildings {bflag})")
        else:
            for model, gg in g.groupby("model"):
                ax.plot(gg["dist_m"].to_numpy(), gg["rsrp_dbm"].to_numpy(), label=f"{model}")

        # Optional FSPL reference curve for context (uses median Tx power & exact per-row distance)
        if fspl_ref:
            tx_p = float(g["tx_p_dbm"].median())
            fmhz = float(g["freq_mhz"].median())
            # Compute FSPL received power
            fspl = fspl_db(g["dist_m"].to_numpy(), fmhz)
            fspl_rx = tx_p - fspl
            ax.plot(g["dist_m"].to_numpy(), fspl_rx, linewidth=2, label="FSPL (reference)")

        ax.set_title(f"{scenario} — All Models Overlaid (f≈{int(f_mhz)} MHz)")
        ax.set_xlabel("Distance [m]")
        ax.set_ylabel("Received Power (RSRP) [dBm]")
        ax.grid(True, alpha=0.3)
        ax.legend(ncol=2, fontsize=8)

        # Save
        safe_s = "".join(c if c.isalnum() or c in "-_." else "_" for c in scenario)[:60]
        out_path_pdf = out_dir / f"overlay_{safe_s}_{int(f_mhz)}MHz.pdf"
        out_path_png = out_dir / f"overlay_{safe_s}_{int(f_mhz)}MHz.png"
        fig.savefig(out_path_pdf, bbox_inches="tight")
        fig.savefig(out_path_png, bbox_inches="tight", dpi=200)
        plt.close(fig)
        print(f"✓ {out_path_pdf}")

def main():
    ap = argparse.ArgumentParser(description="Overlay RSRP vs Distance for ALL models in the folder, one figure per (scenario, frequency).")
    ap.add_argument("--data_dir", type=str, default=DEFAULT_DATA_DIR, help="Folder with CSVs (one per model/run).")
    ap.add_argument("--out_dir",  type=str, default=DEFAULT_OUT_DIR,  help="Where to write plots.")
    ap.add_argument("--buildings", type=str, default="BOTH", help="ON, OFF, or BOTH (default).")
    ap.add_argument("--no_fspl", action="store_true", help="Disable FSPL reference curve.")
    ap.add_argument("--min_points", type=int, default=10, help="Minimum samples required to emit a plot for a group.")
    args = ap.parse_args()

    buildings = args.buildings.upper()
    if buildings not in VALID_BUILDINGS:
        raise SystemExit(f"--buildings must be one of {VALID_BUILDINGS}")

    data_dir = Path(args.data_dir)
    out_dir  = Path(args.out_dir)

    df = prepare_all(data_dir)
    plot_overlays(df, out_dir, buildings_filter=buildings, fspl_ref=(not args.no_fspl), min_points=args.min_points)

if __name__ == "__main__":
    main()
