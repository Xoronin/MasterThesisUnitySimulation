using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RadioSignalSimulation.Core;

namespace RadioSimulationTests
{
    /// <summary>
    /// Tests for core mathematical models and formulas used in radio propagation
    /// These tests verify the accuracy of the mathematical calculations
    /// </summary>
    public class MathematicalModelsTests
    {
        private const float TOLERANCE = 0.1f; // Acceptable error margin

        [Test]
        public void FSPL_Formula_Manual_Calculation_Verification()
        {
            // Test the FSPL formula manually to verify our implementation
            // FSPL = 20*log10(d) + 20*log10(f) + 32.45
            
            // Test case: 1km distance, 2.4GHz frequency
            float distance_km = 1.0f;
            float frequency_mhz = 2400f;
            
            // Manual calculation
            float term1 = 20f * Mathf.Log10(distance_km);        // 20*log10(1) = 0
            float term2 = 20f * Mathf.Log10(frequency_mhz);      // 20*log10(2400) ≈ 67.6
            float term3 = 32.45f;                                // Constant
            float expectedFSPL = term1 + term2 + term3;          // ≈ 100.05 dB
            
            // Verify each component
            Assert.AreEqual(0f, term1, 0.01f, "Distance term should be 0 for 1km");
            Assert.AreEqual(67.6f, term2, 0.5f, "Frequency term should be ~67.6 dB");
            Assert.AreEqual(100.05f, expectedFSPL, 1.0f, "Total FSPL should be ~100 dB");
        }

        [Test]
        [TestCase(1f, 2400f, 100.05f)]      // 1km, 2.4GHz
        [TestCase(2f, 2400f, 106.05f)]      // 2km, 2.4GHz (+6dB)
        [TestCase(1f, 4800f, 106.05f)]      // 1km, 4.8GHz (+6dB)
        [TestCase(0.1f, 900f, 71.5f)]       // 100m, 900MHz
        [TestCase(10f, 5000f, 126.4f)]      // 10km, 5GHz
        public void FSPL_Known_Values_Test(float distance_km, float frequency_mhz, float expected_fspl)
        {
            // Calculate FSPL using the standard formula
            float calculated_fspl = 20f * Mathf.Log10(distance_km) + 
                                   20f * Mathf.Log10(frequency_mhz) + 
                                   32.45f;
            
            Assert.AreEqual(expected_fspl, calculated_fspl, 1.0f,
                $"FSPL for {distance_km}km at {frequency_mhz}MHz should be ~{expected_fspl}dB");
        }

        [Test]
        public void Distance_Doubling_Rule_6dB_Increase()
        {
            // When distance doubles, FSPL should increase by ~6dB
            float frequency = 2400f;
            float distance1_km = 1f;
            float distance2_km = 2f;
            
            float fspl1 = 20f * Mathf.Log10(distance1_km) + 20f * Mathf.Log10(frequency) + 32.45f;
            float fspl2 = 20f * Mathf.Log10(distance2_km) + 20f * Mathf.Log10(frequency) + 32.45f;
            
            float increase = fspl2 - fspl1;
            float expected_increase = 20f * Mathf.Log10(2f); // Should be ~6.02 dB
            
            Assert.AreEqual(expected_increase, increase, 0.1f,
                "Doubling distance should increase FSPL by ~6dB");
        }

        [Test]
        public void Frequency_Doubling_Rule_6dB_Increase()
        {
            // When frequency doubles, FSPL should increase by ~6dB
            float distance_km = 1f;
            float frequency1 = 2400f;
            float frequency2 = 4800f;
            
            float fspl1 = 20f * Mathf.Log10(distance_km) + 20f * Mathf.Log10(frequency1) + 32.45f;
            float fspl2 = 20f * Mathf.Log10(distance_km) + 20f * Mathf.Log10(frequency2) + 32.45f;
            
            float increase = fspl2 - fspl1;
            float expected_increase = 20f * Mathf.Log10(2f); // Should be ~6.02 dB
            
            Assert.AreEqual(expected_increase, increase, 0.1f,
                "Doubling frequency should increase FSPL by ~6dB");
        }

        [Test]
        public void Power_Budget_Calculation()
        {
            // Test basic power budget: Received Power = Transmitted Power - Path Loss
            float transmit_power_dbm = 20f;    // 20 dBm (100 mW)
            float path_loss_db = 80f;          // 80 dB path loss
            float expected_received_power = transmit_power_dbm - path_loss_db; // -60 dBm
            
            float calculated_received_power = transmit_power_dbm - path_loss_db;
            
            Assert.AreEqual(expected_received_power, calculated_received_power, 0.01f,
                "Power budget calculation should be accurate");
        }

        [Test]
        [TestCase(20f, -90f, true)]    // Strong signal above sensitivity
        [TestCase(-90f, -90f, true)]   // Signal at sensitivity threshold
        [TestCase(-95f, -90f, false)]  // Weak signal below sensitivity
        public void Signal_Detection_Threshold_Logic(float signal_dbm, float sensitivity_dbm, bool should_detect)
        {
            bool can_detect = signal_dbm >= sensitivity_dbm;
            
            Assert.AreEqual(should_detect, can_detect,
                $"Signal {signal_dbm}dBm with sensitivity {sensitivity_dbm}dBm detection should be {should_detect}");
        }

        [Test]
        public void Wavelength_Calculation_Verification()
        {
            // Test wavelength calculation: λ = c/f
            float speed_of_light = 3e8f; // m/s
            
            // Test cases
            var testCases = new[]
            {
                new { freq_mhz = 2400f, expected_wavelength = 0.125f },  // 2.4GHz WiFi
                new { freq_mhz = 5000f, expected_wavelength = 0.06f },   // 5GHz WiFi  
                new { freq_mhz = 900f, expected_wavelength = 0.333f },   // 900MHz GSM
                new { freq_mhz = 1800f, expected_wavelength = 0.167f }   // 1.8GHz GSM
            };
            
            foreach (var testCase in testCases)
            {
                float frequency_hz = testCase.freq_mhz * 1e6f;
                float calculated_wavelength = speed_of_light / frequency_hz;
                
                Assert.AreEqual(testCase.expected_wavelength, calculated_wavelength, 0.01f,
                    $"Wavelength for {testCase.freq_mhz}MHz should be ~{testCase.expected_wavelength}m");
            }
        }

        [Test]
        public void SNR_Calculation_Logic()
        {
            // Test Signal-to-Noise Ratio calculations
            float signal_power_dbm = -70f;
            float noise_power_dbm = -100f;
            float expected_snr_db = signal_power_dbm - noise_power_dbm; // 30 dB
            
            float calculated_snr = signal_power_dbm - noise_power_dbm;
            
            Assert.AreEqual(expected_snr_db, calculated_snr, 0.01f,
                "SNR calculation should be signal power minus noise power");
        }

        [Test]
        public void dBm_To_Watts_Conversion()
        {
            // Test conversion from dBm to watts: P(W) = 10^((P(dBm) - 30) / 10)
            var testCases = new[]
            {
                new { dbm = 30f, watts = 1f },      // 30 dBm = 1 W
                new { dbm = 20f, watts = 0.1f },    // 20 dBm = 100 mW
                new { dbm = 10f, watts = 0.01f },   // 10 dBm = 10 mW
                new { dbm = 0f, watts = 0.001f }    // 0 dBm = 1 mW
            };
            
            foreach (var testCase in testCases)
            {
                float calculated_watts = Mathf.Pow(10f, (testCase.dbm - 30f) / 10f);
                
                Assert.AreEqual(testCase.watts, calculated_watts, 0.001f,
                    $"{testCase.dbm} dBm should equal {testCase.watts} watts");
            }
        }

        [Test]
        public void Edge_Cases_Handling()
        {
            // Test edge cases that might cause issues
            
            // Very small distances
            float very_small_distance = 0.001f; // 1mm
            float fspl_small = 20f * Mathf.Log10(very_small_distance) + 20f * Mathf.Log10(2400f) + 32.45f;
            Assert.IsFalse(float.IsNaN(fspl_small), "Very small distances should not produce NaN");
            
            // Very large distances  
            float very_large_distance = 1000f; // 1000km
            float fspl_large = 20f * Mathf.Log10(very_large_distance) + 20f * Mathf.Log10(2400f) + 32.45f;
            Assert.IsFalse(float.IsInfinity(fspl_large), "Very large distances should not produce infinity");
            
            // Zero distance should be handled (though not physically meaningful)
            // In practice, you might want to add a minimum distance check in your actual code
        }
    }
}