using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Propagation.Core
{
    public class PropagationContext
    {
        public Vector3 TransmitterPosition { get; set; }
        public Vector3 ReceiverPosition { get; set; }
        public float TransmitterPowerDbm { get; set; }
        public float FrequencyMHz { get; set; }
        public float TransmitterHeight { get; set; }
        public float ReceiverHeight { get; set; }

        public float AntennaGainDbi { get; set; }
        public float ReceiverGainDbi { get; set; }
        public float ReceiverSensitivityDbm { get; set; }
        public PropagationModel Model { get; set; }
        public LayerMask BuildingLayer { get; set; }
        public TechnologyType Technology { get; set; }
        public bool IsLOS { get; set; }

        public float MaxReflections { get; set; }
        public float MaxDiffractions { get; set; }
        public float MaxScattering { get; set; }
        public float MaxDistanceMeters { get; set; }

        public float DistanceM => Vector3.Distance(TransmitterPosition, ReceiverPosition);

        public static PropagationContext Create(
            Vector3 txPosition,
            Vector3 rxPosition,
            float txPowerDbm,
            float frequencyMHz,
            float transmitterHeight,
            float receiverHeight)
        {
            return new PropagationContext
            {
                TransmitterPosition = txPosition,
                ReceiverPosition = rxPosition,
                TransmitterPowerDbm = txPowerDbm,
                FrequencyMHz = frequencyMHz,
                TransmitterHeight = transmitterHeight,
                ReceiverHeight = receiverHeight,
            };
        }

        public bool IsValid(out string errorMessage)
        {
            if (DistanceM < RFConstants.MIN_DISTANCE)
            {
                errorMessage = $"Distance too small: {DistanceM:F2}m (min: {RFConstants.MIN_DISTANCE}m)";
                return false;
            }

            if (FrequencyMHz <= 0)
            {
                errorMessage = "Frequency must be positive";
                return false;
            }

            if (TransmitterPowerDbm < -50f || TransmitterPowerDbm > 100f)
            {
                errorMessage = $"Transmitter power out of range: {TransmitterPowerDbm}dBm";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public PropagationContext Clone()
        {
            return new PropagationContext
            {
                TransmitterPosition = TransmitterPosition,
                ReceiverPosition = ReceiverPosition,
                TransmitterPowerDbm = TransmitterPowerDbm,
                FrequencyMHz = FrequencyMHz,
                AntennaGainDbi = AntennaGainDbi,
                ReceiverGainDbi = ReceiverGainDbi,
                ReceiverSensitivityDbm = ReceiverSensitivityDbm,
                Model = Model,
                BuildingLayer = BuildingLayer,
                Technology = Technology,
                IsLOS = IsLOS
            };
        }
    }

    public enum PropagationModel
    {
        FreeSpace,
        LogD,
        LogNShadow,
        Hata,                    
        COST231,
        RayTracing
    }

    public enum TechnologyType
    {
        LTE,
        FiveGmmWave,
        FiveGSub6
    }
}