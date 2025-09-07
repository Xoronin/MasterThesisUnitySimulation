using UnityEngine;
using RFSimulation.Propagation.PathLoss.Models;
using RFSimulation.Propagation.Core;
using RFSimulation.Core;
using System.Collections.Generic;

namespace RFSimulation.Testing
{
    public class PathLossValidator : MonoBehaviour
    {
        [Header("Test Parameters")]
        public float testTransmitterPower = 20f; // dBm
        public float testAntennaGain = 0f; // dBi (isotropic)
        public float testFrequency = 2400f; // MHz

        [Header("Test Distances (meters)")]
        public float[] testDistances = { 1f, 10f, 100f, 1000f, 10000f };

        [Header("Known Good Values")]
        public bool compareWithKnownValues = true;

        private FreeSpaceModel freeSpaceModel;
        private LogDistanceModel logDistanceModel;

        void Start()
        {
            freeSpaceModel = new FreeSpaceModel();
            logDistanceModel = new LogDistanceModel();
        }

        [ContextMenu("Test Free Space Model")]
        public void TestFreeSpaceModel()
        {
            Debug.Log("=== FREE SPACE PATH LOSS TEST ===");
            Debug.Log($"TX Power: {testTransmitterPower}dBm, Gain: {testAntennaGain}dBi, Freq: {testFrequency}MHz");
            Debug.Log("Distance(m) | FSPL(dB) | RX Power(dBm) | Expected FSPL | Expected RX | Error(dB)");
            Debug.Log("-----------|----------|---------------|--------------|-------------|----------");

            foreach (float distance in testDistances)
            {
                // Create test context
                var context = CreateTestContext(distance);

                // Calculate with our model
                float rxPower = freeSpaceModel.Calculate(context);
                float fspl = testTransmitterPower + testAntennaGain - rxPower;

                // Calculate expected values using standard formula
                float expectedFSPL = CalculateExpectedFSPL(distance, testFrequency);
                float expectedRxPower = testTransmitterPower + testAntennaGain - expectedFSPL;

                // Calculate error
                float error = rxPower - expectedRxPower;

                Debug.Log($"{distance,10:F1} | {fspl,8:F2} | {rxPower,13:F2} | {expectedFSPL,12:F2} | {expectedRxPower,11:F2} | {error,9:F3}");

                // Validate results
                if (Mathf.Abs(error) > 0.1f)
                {
                    Debug.LogWarning($"❌ Large error at {distance}m: {error:F3}dB");
                }
                else
                {
                    Debug.Log($"✅ Good match at {distance}m");
                }
            }
        }

        [ContextMenu("Test Log Distance Model")]
        public void TestLogDistanceModel()
        {
            Debug.Log("=== LOG DISTANCE PATH LOSS TEST ===");
            Debug.Log($"TX Power: {testTransmitterPower}dBm, Gain: {testAntennaGain}dBi, Freq: {testFrequency}MHz");

            Debug.Log("Distance(m) | Path Loss(dB) | RX Power(dBm) | Path Loss Exp | Reference Dist");
            Debug.Log("-----------|---------------|---------------|--------------|---------------");

            foreach (float distance in testDistances)
            {
                var context = CreateTestContext(distance);

                float rxPower = logDistanceModel.Calculate(context);
                float pathLoss = testTransmitterPower + testAntennaGain - rxPower;

                // Get model parameters
                float pathLossExp = RFConstants.PATH_LOSS_EXPONENT;
				float refDistance = RFConstants.REFERENCE_DISTANCE;

                Debug.Log($"{distance,10:F1} | {pathLoss,13:F2} | {rxPower,13:F2} | {pathLossExp,12:F1} | {refDistance,13:F1}");
            }
            
        }

        [ContextMenu("Compare Models Side by Side")]
        public void CompareModels()
        {
            Debug.Log("=== MODEL COMPARISON ===");
            Debug.Log($"TX Power: {testTransmitterPower}dBm, Gain: {testAntennaGain}dBi, Freq: {testFrequency}MHz");
            Debug.Log("Distance(m) | Free Space(dBm) | Log Urban(dBm) | Difference Urban");
            Debug.Log("-----------|-----------------|----------------|-----------------|------------------");

            foreach (float distance in testDistances)
            {
                var freeSpaceContext = CreateTestContext(distance);
                var urbanContext = CreateTestContext(distance);

                float freeSpaceRx = freeSpaceModel.Calculate(freeSpaceContext);
                float urbanRx = logDistanceModel.Calculate(urbanContext);

                float urbanDiff = freeSpaceRx - urbanRx;

                Debug.Log($"{distance,10:F1} | {freeSpaceRx,15:F2} | {urbanRx,14:F2} | {urbanDiff,15:F2}");
            }
        }

        [ContextMenu("Test Known Scenarios")]
        public void TestKnownScenarios()
        {
            Debug.Log("=== KNOWN SCENARIO TESTS ===");

            // Test Case 1: WiFi router at 2.4GHz, 100mW (20dBm)
            TestScenario("WiFi Router", 20f, 2400f, 0f, 10f, -42.2f);

            // Test Case 2: Cell tower at 1800MHz, 10W (40dBm), 15dBi gain
            TestScenario("Cell Tower", 40f, 1800f, 15f, 1000f, -32.9f);

            // Test Case 3: GPS satellite at 1575MHz, very distant
            TestScenario("GPS Satellite", 13f, 1575f, 0f, 20200000f, -162.5f);
        }

        private void TestScenario(string name, float power, float freq, float gain, float distance, float expectedRx)
        {
            Debug.Log($"\n--- {name} Test ---");
            Debug.Log($"Power: {power}dBm, Freq: {freq}MHz, Gain: {gain}dBi, Distance: {distance}m");

            var context = PropagationContext.Create(
                Vector3.zero,
                Vector3.forward * distance,
                power,
                freq
            );
            context.AntennaGainDbi = gain;

            float calculatedRx = freeSpaceModel.Calculate(context);
            float error = calculatedRx - expectedRx;

            Debug.Log($"Expected: {expectedRx:F1}dBm, Calculated: {calculatedRx:F1}dBm, Error: {error:F1}dB");

            if (Mathf.Abs(error) < 1f)
            {
                Debug.Log("✅ Test PASSED");
            }
            else
            {
                Debug.LogWarning("❌ Test FAILED - Large error!");
            }
        }

        [ContextMenu("Test Near Field Issues")]
        public void TestNearFieldIssues()
        {
            Debug.Log("=== NEAR FIELD TEST ===");
            Debug.Log("Testing very short distances that might cause issues...");

            float[] nearDistances = { 0.01f, 0.1f, 1f, 5f };

            foreach (float distance in nearDistances)
            {
                var context = CreateTestContext(distance);
                float wavelength = 299.792458f / testFrequency; // c/f in meters (freq in MHz)
                float farFieldDistance = wavelength / (4f * Mathf.PI);

                float rxPower = freeSpaceModel.Calculate(context);
                float fspl = testTransmitterPower + testAntennaGain - rxPower;

                bool isNearField = distance < farFieldDistance;
                string fieldType = isNearField ? "NEAR FIELD" : "FAR FIELD";
                string warning = (rxPower > testTransmitterPower + testAntennaGain) ? "⚠️ GAIN!" : "";

                Debug.Log($"Distance: {distance,6:F3}m | Wavelength: {wavelength,6:F3}m | Far Field: {farFieldDistance,6:F3}m | " +
                         $"Field: {fieldType,10} | RX: {rxPower,6:F1}dBm | FSPL: {fspl,6:F1}dB {warning}");
            }
        }

        // Helper methods
        private PropagationContext CreateTestContext(float distance)
        {
            var context = PropagationContext.Create(
                Vector3.zero,
                Vector3.forward * distance,
                testTransmitterPower,
                testFrequency
            );
            context.AntennaGainDbi = testAntennaGain;
            return context;
        }

        private float CalculateExpectedFSPL(float distanceMeters, float frequencyMHz)
        {
            // Standard FSPL formula: FSPL(dB) = 20*log10(d_km) + 20*log10(f_MHz) + 32.45
            float distanceKm = distanceMeters / 1000f;
            return 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequencyMHz) + 32.45f;
        }

    }
}