using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.SignalQuality
{
    /// <summary>
    /// Analyzes signal quality metrics like SINR, throughput, error rates
    /// </summary>
    public static class SignalQualityAnalyzer
    {
        /// <summary>
        /// Calculate Signal-to-Interference-plus-Noise Ratio
        /// </summary>
        public static float CalculateSINR(float signalPowerDbm, float interferencePowerDbm, float noisePowerDbm = -110f)
        {
            // Convert to linear power
            float signalLinear = Mathf.Pow(10f, signalPowerDbm / 10f);
            float interferenceLinear = interferencePowerDbm > float.NegativeInfinity ?
                Mathf.Pow(10f, interferencePowerDbm / 10f) : 0f;
            float noiseLinear = Mathf.Pow(10f, noisePowerDbm / 10f);

            float interferenceAndNoise = interferenceLinear + noiseLinear;

            if (interferenceAndNoise <= 0f)
                return signalPowerDbm - noisePowerDbm; // SNR

            float sinrLinear = signalLinear / interferenceAndNoise;
            return 10f * Mathf.Log10(sinrLinear);
        }

        /// <summary>
        /// Estimate data throughput based on SINR and modulation scheme
        /// </summary>
        public static float EstimateThroughput(float sinrDb, TechnologyType technology = TechnologyType.LTE)
        {
            // Simplified throughput estimation based on SINR
            switch (technology)
            {
                case TechnologyType.LTE:
                    return EstimateLTEThroughput(sinrDb);
                case TechnologyType.FiveG:
                    return Estimate5GThroughput(sinrDb);
                case TechnologyType.IoT:
                    return EstimateIoTThroughput(sinrDb);
                default:
                    return EstimateLTEThroughput(sinrDb);
            }
        }

        /// <summary>
        /// Calculate connection reliability (success rate)
        /// </summary>
        public static float CalculateReliability(float sinrDb, TechnologyType technology = TechnologyType.LTE)
        {
            float minSINR = GetMinimumSINR(technology);
            float optimalSINR = GetOptimalSINR(technology);

            if (sinrDb < minSINR)
                return 0f; // No connection possible

            if (sinrDb >= optimalSINR)
                return 1f; // Perfect reliability

            // Linear interpolation between minimum and optimal
            float reliability = (sinrDb - minSINR) / (optimalSINR - minSINR);
            return Mathf.Clamp01(reliability);
        }

        /// <summary>
        /// Estimate packet error rate
        /// </summary>
        public static float EstimatePacketErrorRate(float sinrDb, TechnologyType technology = TechnologyType.LTE)
        {
            float reliability = CalculateReliability(sinrDb, technology);
            return 1f - reliability; // Simple inverse relationship
        }

        /// <summary>
        /// Get signal quality category
        /// </summary>
        public static SignalQualityCategory GetQualityCategory(float sinrDb, TechnologyType technology = TechnologyType.LTE)
        {
            float minSINR = GetMinimumSINR(technology);
            float goodSINR = GetGoodSINR(technology);
            float excellentSINR = GetExcellentSINR(technology);

            if (sinrDb < minSINR) return SignalQualityCategory.NoService;
            if (sinrDb < minSINR + 3f) return SignalQualityCategory.Poor;
            if (sinrDb < goodSINR) return SignalQualityCategory.Fair;
            if (sinrDb < excellentSINR) return SignalQualityCategory.Good;
            return SignalQualityCategory.Excellent;
        }

        /// <summary>
        /// Calculate handover probability
        /// </summary>
        public static float CalculateHandoverProbability(
            float currentSINR,
            float targetSINR,
            float handoverMargin = 3f,
            float hysteresis = 1f)
        {
            float sinrDifference = targetSINR - currentSINR;

            if (sinrDifference < handoverMargin)
                return 0f; // Target not good enough

            if (sinrDifference > handoverMargin + hysteresis)
                return 1f; // Clear handover candidate

            // Gradual probability in hysteresis zone
            float probability = (sinrDifference - handoverMargin) / hysteresis;
            return Mathf.Clamp01(probability);
        }

        #region Technology-Specific Implementations

        private static float EstimateWiFiThroughput(float sinrDb)
        {
            // WiFi 6 theoretical max ~9.6 Gbps, practical ~1 Gbps
            if (sinrDb >= 25f) return 1000f; // Mbps - Excellent (1024-QAM)
            if (sinrDb >= 20f) return 600f;  // Good (256-QAM)
            if (sinrDb >= 15f) return 300f;  // Fair (64-QAM)
            if (sinrDb >= 10f) return 150f;  // Poor (16-QAM)
            if (sinrDb >= 5f) return 50f;   // Very poor (QPSK)
            return 0f; // No connection
        }

        private static float EstimateLTEThroughput(float sinrDb)
        {
            // LTE Cat 20 theoretical max ~2 Gbps, practical ~300 Mbps
            if (sinrDb >= 20f) return 300f; // Excellent (256-QAM)
            if (sinrDb >= 15f) return 150f; // Good (64-QAM)
            if (sinrDb >= 10f) return 75f;  // Fair (16-QAM)
            if (sinrDb >= 5f) return 25f;   // Poor (QPSK)
            if (sinrDb >= 0f) return 5f;    // Very poor
            return 0f; // No connection
        }

        private static float Estimate5GThroughput(float sinrDb)
        {
            // 5G theoretical max ~20 Gbps, practical ~1-5 Gbps
            if (sinrDb >= 30f) return 5000f; // Excellent (1024-QAM)
            if (sinrDb >= 25f) return 2000f; // Very good (256-QAM)
            if (sinrDb >= 20f) return 1000f; // Good (64-QAM)
            if (sinrDb >= 15f) return 500f;  // Fair (16-QAM)
            if (sinrDb >= 10f) return 100f;  // Poor (QPSK)
            if (sinrDb >= 5f) return 20f;    // Very poor
            return 0f; // No connection
        }

        private static float EstimateIoTThroughput(float sinrDb)
        {
            // IoT typically low throughput, high reliability
            if (sinrDb >= 10f) return 1f;    // Kbps - Good
            if (sinrDb >= 5f) return 0.5f;   // Fair
            if (sinrDb >= 0f) return 0.1f;   // Poor but usable
            if (sinrDb >= -5f) return 0.01f; // Very poor
            return 0f; // No connection
        }

        #endregion

        #region SINR Thresholds

        private static float GetMinimumSINR(TechnologyType technology)
        {
            switch (technology)
            {
                case TechnologyType.LTE: return -6f;
                case TechnologyType.FiveG: return -3f;
                case TechnologyType.IoT: return -10f;
                default: return -6f;
            }
        }

        private static float GetGoodSINR(TechnologyType technology)
        {
            switch (technology)
            {
                case TechnologyType.LTE: return 10f;
                case TechnologyType.FiveG: return 15f;
                case TechnologyType.IoT: return 5f;
                default: return 10f;
            }
        }

        private static float GetExcellentSINR(TechnologyType technology)
        {
            switch (technology)
            {
                case TechnologyType.LTE: return 20f;
                case TechnologyType.FiveG: return 25f;
                case TechnologyType.IoT: return 15f;
                default: return 20f;
            }
        }

        private static float GetOptimalSINR(TechnologyType technology)
        {
            return GetExcellentSINR(technology) + 5f;
        }

        #endregion
    }

    /// <summary>
    /// Signal quality categories
    /// </summary>
    public enum SignalQualityCategory
    {
        NoService,   // Cannot connect
        Poor,        // Connected but very limited
        Fair,        // Basic functionality
        Good,        // Good performance
        Excellent    // Optimal performance
    }

    /// <summary>
    /// Comprehensive signal quality metrics
    /// </summary>
    [System.Serializable]
    public class SignalQualityMetrics
    {
        public float sinrDb;
        public float throughputMbps;
        public float reliability; // 0-1
        public float packetErrorRate; // 0-1
        public SignalQualityCategory category;
        public TechnologyType technology;

        public SignalQualityMetrics(float sinr, TechnologyType tech = TechnologyType.LTE)
        {
            sinrDb = sinr;
            technology = tech;
            throughputMbps = SignalQualityAnalyzer.EstimateThroughput(sinr, tech);
            reliability = SignalQualityAnalyzer.CalculateReliability(sinr, tech);
            packetErrorRate = SignalQualityAnalyzer.EstimatePacketErrorRate(sinr, tech);
            category = SignalQualityAnalyzer.GetQualityCategory(sinr, tech);
        }

        public override string ToString()
        {
            return $"SINR: {sinrDb:F1}dB, Throughput: {throughputMbps:F1}Mbps, " +
                   $"Reliability: {reliability:P1}, Category: {category}";
        }
    }
}