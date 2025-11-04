
from pathlib import Path
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import argparse

DEFAULT_DATA_DIR = "Data/Exports"
DEFAULT_OUT_DIR  = "Data/Plots/Overlays"
VALID_BUILDINGS = {"ON","OFF","BOTH"}

def load_csv(p: Path) -> pd.DataFrame:
    df = pd.read_csv(p)
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
    return pd.concat(frames, ignore_index=True)

def plot_scatter(df: pd.DataFrame, out_dir: Path, buildings_filter: str, min_points: int, markersize: int):
    out_dir.mkdir(parents=True, exist_ok=True)

    df["freq_mhz_round"] = df["freq_mhz"].round(0)

    if buildings_filter in {"ON","OFF"}:
        df = df[df["buildings"] == buildings_filter]

    for (scenario, f_mhz), g in df.groupby(["scenario","freq_mhz_round"]):
        if len(g) < min_points:
            continue
        g = g.sort_values("dist_m")

        fig, ax = plt.subplots(figsize=(8.5, 5.5), constrained_layout=True)

        if buildings_filter == "BOTH":
            for (model, bflag), gg in g.groupby(["model","buildings"]):
                ax.scatter(gg["dist_m"].to_numpy(), gg["rsrp_dbm"].to_numpy(),
                           s=markersize, alpha=0.9, label=f"{model} (Buildings {bflag})")
        else:
            for model, gg in g.groupby("model"):
                ax.scatter(gg["dist_m"].to_numpy(), gg["rsrp_dbm"].to_numpy(),
                           s=markersize, alpha=0.9, label=f"{model}")

        ax.set_title(f"{scenario} — Per-Receiver Scatter (f≈{int(f_mhz)} MHz)")
        ax.set_xlabel("Distance [m]")
        ax.set_ylabel("Received Power (RSRP) [dBm]")
        ax.grid(True, alpha=0.3)
        ax.legend(ncol=2, fontsize=8)

        safe_s = "".join(c if c.isalnum() or c in "-_." else "_" for c in scenario)[:60]
        out_pdf = out_dir / f"scatter_{safe_s}_{int(f_mhz)}MHz.pdf"
        out_png = out_dir / f"scatter_{safe_s}_{int(f_mhz)}MHz.png"
        fig.savefig(out_pdf, bbox_inches="tight")
        fig.savefig(out_png, bbox_inches="tight", dpi=200)
        plt.close(fig)
        print(f"✓ {out_pdf}")

def main():
    ap = argparse.ArgumentParser(description="Overlay scatter of receiver points (RSRP vs distance) for all models, one figure per (scenario, freq).")
    ap.add_argument("--data_dir", type=str, default=DEFAULT_DATA_DIR)
    ap.add_argument("--out_dir",  type=str, default=DEFAULT_OUT_DIR)
    ap.add_argument("--buildings", type=str, default="BOTH", help="ON, OFF, or BOTH")
    ap.add_argument("--min_points", type=int, default=10)
    ap.add_argument("--markersize", type=int, default=14)
    args = ap.parse_args()

    buildings = args.buildings.upper()
    if buildings not in VALID_BUILDINGS:
        raise SystemExit(f"--buildings must be one of {VALID_BUILDINGS}")

    df = prepare_all(Path(args.data_dir))
    plot_scatter(df, Path(args.out_dir), buildings_filter=buildings, min_points=args.min_points, markersize=args.markersize)

if __name__ == "__main__":
    main()
