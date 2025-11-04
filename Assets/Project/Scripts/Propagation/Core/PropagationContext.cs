using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Propagation.Core
{
    public class PropagationContext
    {
        // Required parameters
        public Vector3 TransmitterPosition { get; set; }
        public Vector3 ReceiverPosition { get; set; }
        public float TransmitterPowerDbm { get; set; }
        public float FrequencyMHz { get; set; }
        public float TransmitterHeight { get; set; }
        public float ReceiverHeight { get; set; }

        // Optional parameters with defaults
        public float AntennaGainDbi { get; set; }
        public float ReceiverGainDbi { get; set; }
        public float ReceiverSensitivityDbm { get; set; }
        public PropagationModel Model { get; set; }
        public LayerMask? BuildingLayers { get; set; }
        public TechnologyType Technology { get; set; }
        public bool IsLOS { get; set; }

        // Computed properties
        public float Distance => Vector3.Distance(TransmitterPosition, ReceiverPosition);
        public bool HasObstacles => BuildingLayers.HasValue;
        public float WavelengthMeters => RFConstants.SPEED_OF_LIGHT / (FrequencyMHz * 1e6f);

        // Factory methods
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

        // Validation
        public bool IsValid(out string errorMessage)
        {
            if (Distance < RFConstants.MIN_DISTANCE)
            {
                errorMessage = $"Distance too small: {Distance:F2}m (min: {RFConstants.MIN_DISTANCE}m)";
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

        // Copy constructor for modifications
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
                BuildingLayers = BuildingLayers,
                Technology = Technology,
                IsLOS = IsLOS
            };
        }
    }

    // Enums
    public enum PropagationModel
    {
        FreeSpace,
        LogDistance,
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