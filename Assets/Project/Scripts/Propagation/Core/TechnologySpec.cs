using UnityEngine;
using System;

namespace RFSimulation.Propagation.Core
{
    /// <summary>
    /// Defines standard specifications for each wireless technology.
    /// </summary>
    [Serializable]
    public class TechnologySpec
    {
        public string Name;
        public float TypicalFrequencyMHz;
        public float MinFrequencyMHz;
        public float MaxFrequencyMHz;

        // Receiver specifications
        public float SensitivityDbm;
        public float ConnectionMarginDb;
        public float MinimumSINRDb;

        // Transmitter specifications
        public float TypicalTxPowerDbm;
        public float MaxTxPowerDbm;
        public float TypicalAntennaGainDbi;

        // Coverage characteristics
        public float TypicalRangeMeters;
        public float MaxRangeMeters;

        // Quality thresholds (margin above sensitivity)
        public float PoorThresholdDb;
        public float FairThresholdDb;
        public float GoodThresholdDb;
        // Excellent is anything above Good
    }

    /// <summary>
    /// Static repository of technology specifications.
    /// </summary>
    public static class TechnologySpecifications
    {
        private static TechnologySpec _lte;
        private static TechnologySpec _fiveGmmWave;
        private static TechnologySpec _fiveGSub6;

        static TechnologySpecifications()
        {
            InitializeSpecs();
        }

        private static void InitializeSpecs()
        {
            // LTE (Long Term Evolution)
            _lte = new TechnologySpec
            {
                Name = "LTE",
                TypicalFrequencyMHz = 700f,      // Band 12/13/14/17 (US)
                MinFrequencyMHz = 450f,           // Band 31
                MaxFrequencyMHz = 2600f,          // Band 7

                // Receiver specs
                SensitivityDbm = -105f,           // QPSK 1/3, 20 MHz
                ConnectionMarginDb = 5f,          // Connects at -100 dBm
                MinimumSINRDb = -5f,              // LTE handles interference well

                // Transmitter specs
                TypicalTxPowerDbm = 40f,          // 10W macro cell
                MaxTxPowerDbm = 46f,              // 40W max
                TypicalAntennaGainDbi = 15f,      // Sector antenna

                // Coverage
                TypicalRangeMeters = 1000f,       // Urban
                MaxRangeMeters = 3000f,           // Rural/suburban

                // Quality thresholds
                PoorThresholdDb = 5f,             // -105 to -100 dBm
                FairThresholdDb = 12f,            // -100 to -93 dBm
                GoodThresholdDb = 20f,            // -93 to -85 dBm
                // Excellent: > -85 dBm
            };

            // 5G mmWave (millimeter Wave)
            _fiveGmmWave = new TechnologySpec
            {
                Name = "5G mmWave",
                TypicalFrequencyMHz = 28000f,     // n257 band
                MinFrequencyMHz = 24250f,         // n258 lower
                MaxFrequencyMHz = 40000f,         // n260 upper

                // Receiver specs
                SensitivityDbm = -90f,            // QPSK, 100 MHz BW
                ConnectionMarginDb = 8f,          // Connects at -82 dBm
                MinimumSINRDb = 0f,               // Higher due to interference

                // Transmitter specs
                TypicalTxPowerDbm = 23f,          // 200mW small cell
                MaxTxPowerDbm = 30f,              // 1W max typical
                TypicalAntennaGainDbi = 20f,      // Beamforming array

                // Coverage
                TypicalRangeMeters = 150f,        // Urban LOS
                MaxRangeMeters = 300f,            // Ideal LOS

                // Quality thresholds
                PoorThresholdDb = 10f,            // -90 to -80 dBm
                FairThresholdDb = 20f,            // -80 to -70 dBm
                GoodThresholdDb = 30f,            // -70 to -60 dBm
                // Excellent: > -60 dBm
            };

            // 5G Sub-6 GHz
            _fiveGSub6 = new TechnologySpec
            {
                Name = "5G Sub-6",
                TypicalFrequencyMHz = 3500f,      // n78 band (3.3-3.8 GHz)
                MinFrequencyMHz = 600f,           // n71 (600 MHz)
                MaxFrequencyMHz = 4200f,          // n79 upper

                // Receiver specs
                SensitivityDbm = -100f,           // QPSK, 100 MHz BW
                ConnectionMarginDb = 6f,          // Connects at -94 dBm
                MinimumSINRDb = -3f,              // Moderate interference tolerance

                // Transmitter specs
                TypicalTxPowerDbm = 35f,          // ~3W small cell / macro
                MaxTxPowerDbm = 43f,              // 20W macro
                TypicalAntennaGainDbi = 18f,      // Massive MIMO

                // Coverage
                TypicalRangeMeters = 500f,        // Urban
                MaxRangeMeters = 1500f,           // Suburban

                // Quality thresholds
                PoorThresholdDb = 8f,             // -100 to -92 dBm
                FairThresholdDb = 15f,            // -92 to -85 dBm
                GoodThresholdDb = 25f,            // -85 to -75 dBm
                // Excellent: > -75 dBm
            };
        }

        /// <summary>
        /// Get specifications for a technology type.
        /// </summary>
        public static TechnologySpec GetSpec(TechnologyType technology)
        {
            return technology switch
            {
                TechnologyType.LTE => _lte,
                TechnologyType.FiveGmmWave => _fiveGmmWave,
                TechnologyType.FiveGSub6 => _fiveGSub6,
                _ => _fiveGSub6 // Default to Sub-6
            };
        }

        /// <summary>
        /// Get specifications by parsing technology string.
        /// </summary>
        public static TechnologySpec GetSpec(string technologyString)
        {
            var tech = ParseTechnologyString(technologyString);
            return GetSpec(tech);
        }

        /// <summary>
        /// Parse technology string to enum.
        /// </summary>
        public static TechnologyType ParseTechnologyString(string tech)
        {
            if (string.IsNullOrEmpty(tech)) return TechnologyType.FiveGSub6;

            var t = tech.ToUpperInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");

            if (t.Contains("5GMMWAVE") || t.Contains("MMWAVE") || t.Contains("5GMM"))
                return TechnologyType.FiveGmmWave;

            if (t.Contains("5GSUB6") || t.Contains("SUB6") || t.Contains("5GSUB"))
                return TechnologyType.FiveGSub6;

            if (t.Contains("LTE"))
                return TechnologyType.LTE;

            return TechnologyType.FiveGSub6; // Default
        }

        /// <summary>
        /// Get all available technology specs.
        /// </summary>
        public static TechnologySpec[] GetAllSpecs()
        {
            return new[] { _lte, _fiveGmmWave, _fiveGSub6 };
        }
    }
}