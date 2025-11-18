using UnityEngine;
using System;

namespace RFSimulation.Propagation.Core
{
    public class TechnologySpec
    {
        public string Name;
        public float TypicalFrequencyMHz;

        // Receiver specifications
        public float SensitivityDbm;
        public float ConnectionMarginDb;
        public float MinimumSINRDb;
        public float TypicalRxHeight;

        // Transmitter specifications
        public float TypicalTxPowerDbm;
        public float TypicalTxHeight;
    }

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
            // LTE 
            _lte = new TechnologySpec
            {
                Name = "LTE",
                TypicalFrequencyMHz = 700f,      

                // Receiver specs
                SensitivityDbm = -100f,           
                ConnectionMarginDb = 5f,          
                TypicalRxHeight = 1.5f,

                // Transmitter specs
                TypicalTxPowerDbm = 43f,
                TypicalTxHeight = 25f         
            };

            // 5G mmWave 
            _fiveGmmWave = new TechnologySpec
            {
                Name = "5GmmWave",
                TypicalFrequencyMHz = 28000f,     

                // Receiver specs
                SensitivityDbm = -90f,            
                ConnectionMarginDb = 8f,          
                TypicalRxHeight = 1.5f,

                // Transmitter specs
                TypicalTxPowerDbm = 23f,
                TypicalTxHeight = 10f
            };

            // 5G Sub-6 GHz
            _fiveGSub6 = new TechnologySpec
            {
                Name = "5GSub6",
                TypicalFrequencyMHz = 3500f,    

                // Receiver specs
                SensitivityDbm = -98f,           
                ConnectionMarginDb = 6f,          
                TypicalRxHeight = 1.5f,

                // Transmitter specs
                TypicalTxPowerDbm = 43f,
                TypicalTxHeight = 25f

            };
        }

        public static TechnologySpec GetSpec(TechnologyType technology)
        {
            return technology switch
            {
                TechnologyType.LTE => _lte,
                TechnologyType.FiveGmmWave => _fiveGmmWave,
                TechnologyType.FiveGSub6 => _fiveGSub6,
                _ => _fiveGSub6 
            };
        }

        public static TechnologySpec GetSpec(string technologyString)
        {
            var tech = ParseTechnologyString(technologyString);
            return GetSpec(tech);
        }

        public static TechnologyType ParseTechnologyString(string tech)
        {
            if (string.IsNullOrEmpty(tech)) return TechnologyType.FiveGSub6;

            var t = tech.ToUpperInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");

            if (t.Contains("5GMMWAVE"))
                return TechnologyType.FiveGmmWave;

            if (t.Contains("5GSUB6"))
                return TechnologyType.FiveGSub6;

            if (t.Contains("LTE"))
                return TechnologyType.LTE;

            return TechnologyType.FiveGSub6;
        }

        public static TechnologySpec[] GetAllSpecs()
        {
            return new[] { _lte, _fiveGmmWave, _fiveGSub6 };
        }
    }
}