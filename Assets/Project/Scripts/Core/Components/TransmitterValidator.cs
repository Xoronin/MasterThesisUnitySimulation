using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Propagation;
using RFSimulation.Visualization;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;

// Validation component
namespace RFSimulation.Core
{
    public class TransmitterValidator : MonoBehaviour
    {
        public bool ValidatePower(float powerDbm)
        {
            if (powerDbm >= 0f && powerDbm <= 80f)
                return true;

            Debug.LogWarning($"Invalid power value: {powerDbm}dBm. Must be 0-80 dBm.");
            return false;
        }

        public bool ValidateFrequency(float frequencyMHz)
        {
            if (frequencyMHz > 0f && frequencyMHz <= 100000f)
                return true;

            Debug.LogWarning($"Invalid frequency: {frequencyMHz}MHz. Must be > 0 MHz.");
            return false;
        }

        public bool ValidateAntennaGain(float gainDbi)
        {
            if (gainDbi >= 0f && gainDbi <= 50f)
                return true;

            Debug.LogWarning($"Invalid gain: {gainDbi}dBi. Must be 0-50 dBi.");
            return false;
        }
    }
}