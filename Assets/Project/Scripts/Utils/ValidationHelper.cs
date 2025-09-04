using UnityEngine;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Utils
{
    public static class ValidationHelper
    {
        /// <summary>
        /// Validate PropagationContext parameters
        /// </summary>
        public static bool ValidateContext(PropagationContext context, out string errorMessage)
        {
            if (context == null)
            {
                errorMessage = "PropagationContext is null";
                return false;
            }

            return context.IsValid(out errorMessage);
        }

        /// <summary>
        /// Validate frequency range
        /// </summary>
        public static bool ValidateFrequency(float frequencyMHz, out string errorMessage)
        {
            const float MIN_FREQ = 1f;      // 1 MHz
            const float MAX_FREQ = 100000f; // 100 GHz

            if (frequencyMHz < MIN_FREQ || frequencyMHz > MAX_FREQ)
            {
                errorMessage = $"Frequency {frequencyMHz} MHz out of range [{MIN_FREQ}-{MAX_FREQ}] MHz";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Validate power range
        /// </summary>
        public static bool ValidatePower(float powerDbm, out string errorMessage)
        {
            const float MIN_POWER = -50f;  // -50 dBm
            const float MAX_POWER = 100f;  // 100 dBm (100W)

            if (powerDbm < MIN_POWER || powerDbm > MAX_POWER)
            {
                errorMessage = $"Power {powerDbm} dBm out of range [{MIN_POWER}-{MAX_POWER}] dBm";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Validate antenna gain
        /// </summary>
        public static bool ValidateGain(float gainDbi, out string errorMessage)
        {
            const float MIN_GAIN = -20f;  // -20 dBi
            const float MAX_GAIN = 50f;   // 50 dBi

            if (gainDbi < MIN_GAIN || gainDbi > MAX_GAIN)
            {
                errorMessage = $"Gain {gainDbi} dBi out of range [{MIN_GAIN}-{MAX_GAIN}] dBi";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Validate distance
        /// </summary>
        public static bool ValidateDistance(float distance, out string errorMessage)
        {
            const float MIN_DISTANCE = 0.01f;  // 1 cm
            const float MAX_DISTANCE = 100000f; // 100 km

            if (distance < MIN_DISTANCE || distance > MAX_DISTANCE)
            {
                errorMessage = $"Distance {distance} m out of range [{MIN_DISTANCE}-{MAX_DISTANCE}] m";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Sanitize input values
        /// </summary>
        public static PropagationContext SanitizeContext(PropagationContext context)
        {
            var sanitized = context.Clone();

            // Ensure minimum distance
            float distance = sanitized.Distance;
            if (distance < RFConstants.MIN_DISTANCE)
            {
                Vector3 direction = (sanitized.ReceiverPosition - sanitized.TransmitterPosition).normalized;
                sanitized.ReceiverPosition = sanitized.TransmitterPosition +
                                           direction * RFConstants.MIN_DISTANCE;
            }

            // Clamp frequency
            sanitized.FrequencyMHz = Mathf.Clamp(sanitized.FrequencyMHz, 1f, 100000f);

            // Clamp power
            sanitized.TransmitterPowerDbm = Mathf.Clamp(sanitized.TransmitterPowerDbm, -50f, 100f);

            // Clamp antenna gain
            sanitized.AntennaGainDbi = Mathf.Clamp(sanitized.AntennaGainDbi, -20f, 50f);

            return sanitized;
        }
    }
}

