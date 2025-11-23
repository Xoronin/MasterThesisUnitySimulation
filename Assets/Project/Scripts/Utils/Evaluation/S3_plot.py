import os
import re
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


BASE_DIR = r"..\Project\Data\Exports\S3"

FILES = {
    "S3_3m.csv": 3,
    "S3_5m.csv": 5,
    "S3_10m.csv": 10,
}

SENSITIVITY_DBM = -90.0
CORNER_DISTANCE_M = 40.0 

MODEL_COLUMN = "PropagationModel"
RAYTRACING_NAME = "RayTracing"

DIST_COL = "Distance"
PR_COL = "RxSignalStrength"


all_runs = []  

for fname, h_tx in FILES.items():
    path = os.path.join(BASE_DIR, fname)
    df = pd.read_csv(path)

    if MODEL_COLUMN in df.columns:
        df = df[df[MODEL_COLUMN] == RAYTRACING_NAME].copy()

    if DIST_COL not in df.columns or PR_COL not in df.columns:
        raise ValueError(f"{fname} does not contain {DIST_COL} or {PR_COL} columns")

    df = df[[DIST_COL, PR_COL]].copy()
    df.sort_values(by=DIST_COL, inplace=True)
    df["TxHeight_m"] = h_tx

    all_runs.append((h_tx, df))

all_runs.sort(key=lambda x: x[0])

print("Loaded heights:", [h for h, _ in all_runs])

plt.figure(figsize=(8, 4.5))

for h_tx, df in all_runs:
    plt.plot(
        df[DIST_COL].values,
        df[PR_COL].values,
        marker="o",
        markersize=3,
        linewidth=1.0,
        label=f"h_t = {h_tx} m"
    )


plt.axvline(CORNER_DISTANCE_M, linestyle="--", linewidth=1.0)
plt.text(CORNER_DISTANCE_M + 2, plt.ylim()[0] + 25, "Corner / NLOS start", fontsize=8)

plt.axhline(SENSITIVITY_DBM, linestyle=":", linewidth=1.0)
plt.text(plt.xlim()[0] + 5, SENSITIVITY_DBM + 1, f"Sensitivity {SENSITIVITY_DBM:.0f} dBm", fontsize=8)

plt.xlabel("Distance [m]")
plt.ylabel("Received power $P_r$ [dBm]")
plt.title("Scenario S3 - Urban mmWave LOS/NLOS, f = 28 GHz")
plt.grid(True, linestyle=":", linewidth=0.5)
plt.legend()
plt.tight_layout()

out_path = os.path.join(BASE_DIR, "S3_mmWave_Pr_vs_d_heights.png")
plt.savefig(out_path, dpi=300)
plt.show()

print(f"\nPlot saved to: {out_path}")
