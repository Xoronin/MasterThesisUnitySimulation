using UnityEngine;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.SignalQuality
{
    /// <summary>
    /// Analyzes signal quality metrics such as SINR, throughput, reliability, and quality category.
    /// </summary>
    public static class SignalQualityAnalyzer
    {
        /// <summary>
        /// Calculates SINR (dB) from signal, interference, and noise powers in dBm.
        /// </summary>
        public static float CalculateSINR(float signalPowerDbm, float interferencePowerDbm, float noisePowerDbm = -110f)
        {
            float signalLinear = Mathf.Pow(10f, signalPowerDbm / 10f);
            float interferenceLinear = interferencePowerDbm > float.NegativeInfinity ? Mathf.Pow(10f, interferencePowerDbm / 10f) : 0f;
            float noiseLinear = Mathf.Pow(10f, noisePowerDbm / 10f);

            float denom = interferenceLinear + noiseLinear;
            if (denom <= 0f) return signalPowerDbm - noisePowerDbm;

            float sinrLinear = signalLinear / denom;
            return 10f * Mathf.Log10(sinrLinear);
        }

        /// <summary>
        /// Estimates data throughput (Mbps) from SINR for a given technology.
        /// </summary>
        public static float EstimateThroughput(float sinrDb, TechnologyType technology = TechnologyType.LTE)
        {
            switch (technology)
            {
                case TechnologyType.LTE:
                    return EstimateLTEThroughput(sinrDb);
                case TechnologyType.FiveGSub6:
                    return Estimate5GSub6Throughput(sinrDb);
                case TechnologyType.FiveGmmWave:
                    return Estimate5GmmWaveThroughput(sinrDb);
                default:
                    return EstimateLTEThroughput(sinrDb);
            }
        }

        /// <summary>
        /// Calculates connection reliability (0–1) based on SINR and technology.
        /// </summary>
        public static float CalculateReliability(float sinrDb, TechnologyType technology = TechnologyType.LTE)
        {
            float minSINR = GetMinimumSINR(technology);
            float optimalSINR = GetOptimalSINR(technology);

            if (sinrDb < minSINR) return 0f;
            if (sinrDb >= optimalSINR) return 1f;

            float reliability = (sinrDb - minSINR) / (optimalSINR - minSINR);
            return Mathf.Clamp01(reliability);
        }

        /// <summary>
        /// Estimates packet error rate (0–1) as the inverse of reliability.
        /// </summary>
        public static float EstimatePacketErrorRate(float sinrDb, TechnologyType technology = TechnologyType.LTE)
        {
            float reliability = CalculateReliability(sinrDb, technology);
            return 1f - reliability;
        }

        /// <summary>
        /// Maps SINR to a quality category for the given technology.
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
        /// Calculates handover probability based on current and target SINR with margin and hysteresis.
        /// </summary>
        public static float CalculateHandoverProbability(float currentSINR, float targetSINR, float handoverMargin = 3f, float hysteresis = 1f)
        {
            float diff = targetSINR - currentSINR;
            if (diff < handoverMargin) return 0f;
            if (diff > handoverMargin + hysteresis) return 1f;

            float probability = (diff - handoverMargin) / hysteresis;
            return Mathf.Clamp01(probability);
        }

        private static float EstimateLTEThroughput(float sinrDb)
        {
            if (sinrDb >= 20f) return 300f;
            if (sinrDb >= 15f) return 150f;
            if (sinrDb >= 10f) return 75f;
            if (sinrDb >= 5f) return 25f;
            if (sinrDb >= 0f) return 5f;
            return 0f;
        }

        private static float Estimate5GSub6Throughput(float sinrDb)
        {
            if (sinrDb >= 25f) return 1000f;
            if (sinrDb >= 20f) return 600f;
            if (sinrDb >= 15f) return 300f;
            if (sinrDb >= 10f) return 120f;
            if (sinrDb >= 5f) return 30f;
            return 0f;
        }

        private static float Estimate5GmmWaveThroughput(float sinrDb)
        {
            if (sinrDb >= 30f) return 5000f;
            if (sinrDb >= 25f) return 2000f;
            if (sinrDb >= 20f) return 1000f;
            if (sinrDb >= 15f) return 500f;
            if (sinrDb >= 10f) return 100f;
            return 0f;
        }

        private static float GetMinimumSINR(TechnologyType technology)
        {
            switch (technology)
            {
                case TechnologyType.LTE: return -6f;
                case TechnologyType.FiveGSub6: return -3f;
                case TechnologyType.FiveGmmWave: return 0f;
                default: return -6f;
            }
        }

        private static float GetGoodSINR(TechnologyType technology)
        {
            switch (technology)
            {
                case TechnologyType.LTE: return 10f;
                case TechnologyType.FiveGSub6: return 15f;
                case TechnologyType.FiveGmmWave: return 20f;
                default: return 10f;
            }
        }

        private static float GetExcellentSINR(TechnologyType technology)
        {
            switch (technology)
            {
                case TechnologyType.LTE: return 20f;
                case TechnologyType.FiveGSub6: return 25f;
                case TechnologyType.FiveGmmWave: return 30f;
            }
            return 20f;
        }

        private static float GetOptimalSINR(TechnologyType technology)
        {
            return GetExcellentSINR(technology) + 5f;
        }
    }

    /// <summary>
    /// Signal quality categories.
    /// </summary>
    public enum SignalQualityCategory
    {
        NoService,
        Poor,
        Fair,
        Good,
        Excellent
    }

    /// <summary>
    /// Aggregated signal quality metrics for a receiver.
    /// </summary>
    [System.Serializable]
    public class SignalQualityMetrics
    {
        public float sinrDb;
        public float throughputMbps;
        public float reliability;
        public float packetErrorRate;
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
    }
}
