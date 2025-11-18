import numpy as np
import pandas as pd
import math

C = 3e8  # speed of light

def fspl_db(distance_m, frequency_mhz):
    wavelength = C / (frequency_mhz * 1e6)           # MHz → Hz
    path_loss_linear = (4 * math.pi * distance_m / wavelength) ** 2
    return 10 * math.log10(path_loss_linear)

def pr_dbm(distance_m, frequency_mhz, tx_power_dbm):
    return tx_power_dbm - fspl_db(distance_m, frequency_mhz)

# Load your CSV
df = pd.read_csv(r"C:\Users\mer-l\MasterThesis\MasterThesis\Assets\Project\Data\Exports\S1\S1_logn.csv")

# Use only entries for FreeSpace/LogD/LogDShadow
models = df['PropagationModel'].unique()

results = []
for model in models:
    sub = df[df['PropagationModel'] == model]
    
    dist = df["Distance"].values
    freq = df["TxFrequency"].iloc[0]
    tx_power = df["TxPower"].iloc[0]

    # Analytical FSPL for comparison
    pr_theory = np.array([pr_dbm(d, freq, tx_power) for d in dist])
    
    # Actual simulated received power
    pr_sim = sub['RxSignalStrength'].values
    
    # Compute errors
    rmse = np.sqrt(np.mean((pr_sim - pr_theory) ** 2))
    mean_bias = np.mean(pr_sim - pr_theory)
    
    results.append((model, rmse, mean_bias))

# Output
for model, rmse, bias in results:
    print(f"{model}: RMSE = {rmse:.2f} dB, Mean Bias = {bias:+.2f} dB")
