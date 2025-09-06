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

            // Test different environments
            EnvironmentType[] environments = {
                EnvironmentType.FreeSpace,
                EnvironmentType.Urban
            };

            foreach (var env in environments)
            {
                Debug.Log($"\n--- Environment: {env} ---");
                Debug.Log("Distance(m) | Path Loss(dB) | RX Power(dBm) | Path Loss Exp | Reference Dist");
                Debug.Log("-----------|---------------|---------------|--------------|---------------");

                foreach (float distance in testDistances)
                {
                    var context = CreateTestContext(distance);
                    context.Environment = env;

                    float rxPower = logDistanceModel.Calculate(context);
                    float pathLoss = testTransmitterPower + testAntennaGain - rxPower;

                    // Get model parameters
                    float pathLossExp = GetPathLossExponent(env);
                    float refDistance = GetReferenceDistance(env);

                    Debug.Log($"{distance,10:F1} | {pathLoss,13:F2} | {rxPower,13:F2} | {pathLossExp,12:F1} | {refDistance,13:F1}");
                }
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
                urbanContext.Environment = EnvironmentType.Urban;

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
            context.Environment = EnvironmentType.FreeSpace;

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

        [ContextMenu("Test Frequency Dependency")]
        public void TestFrequencyDependency()
        {
            Debug.Log("=== FREQUENCY DEPENDENCY TEST ===");
            Debug.Log("Testing FSPL frequency dependence (should increase with frequency)");

            float[] frequencies = { 900f, 1800f, 2400f, 5800f, 28000f }; // MHz
            float testDist = 1000f; // 1km

            Debug.Log("Frequency(MHz) | FSPL(dB) | RX Power(dBm) | Change from 900MHz");
            Debug.Log("-------------|----------|---------------|------------------");

            float baselineFSPL = 0f;

            for (int i = 0; i < frequencies.Length; i++)
            {
                var context = CreateTestContext(testDist);
                context.FrequencyMHz = frequencies[i];

                float rxPower = freeSpaceModel.Calculate(context);
                float fspl = testTransmitterPower + testAntennaGain - rxPower;

                if (i == 0) baselineFSPL = fspl;
                float change = fspl - baselineFSPL;

                Debug.Log($"{frequencies[i],12:F0} | {fspl,8:F1} | {rxPower,13:F1} | {change,17:F1}");
            }
        }

        [ContextMenu("Manual Calculation Check")]
        public void ManualCalculationCheck()
        {
            Debug.Log("=== MANUAL CALCULATION CHECK ===");

            // Use simple round numbers for easy verification
            float power = 30f; // dBm (1W)
            float gain = 0f;   // dBi (isotropic)
            float freq = 2400f; // MHz
            float dist = 1000f; // m = 1km

            Debug.Log($"Manual calculation for: {power}dBm, {gain}dBi, {freq}MHz, {dist}m");

            // Step-by-step manual calculation
            Debug.Log("\nStep-by-step calculation:");
            Debug.Log($"1. Distance in km: {dist / 1000f}");
            Debug.Log($"2. 20*log10(distance_km): {20f * Mathf.Log10(dist / 1000f):F2}");
            Debug.Log($"3. 20*log10(frequency_MHz): {20f * Mathf.Log10(freq):F2}");
            Debug.Log($"4. Constant (32.45): 32.45");

            float fspl = 20f * Mathf.Log10(dist / 1000f) + 20f * Mathf.Log10(freq) + 32.45f;
            float rxPower = power + gain - fspl;

            Debug.Log($"5. Total FSPL: {fspl:F2}dB");
            Debug.Log($"6. RX Power: {power} + {gain} - {fspl:F2} = {rxPower:F2}dBm");

            // Compare with our model
            var context = PropagationContext.Create(Vector3.zero, Vector3.forward * dist, power, freq);
            context.AntennaGainDbi = gain;
            context.Environment = EnvironmentType.FreeSpace;

            float modelResult = freeSpaceModel.Calculate(context);
            float error = modelResult - rxPower;

            Debug.Log($"\nModel result: {modelResult:F2}dBm");
            Debug.Log($"Error: {error:F3}dB");

            if (Mathf.Abs(error) < 0.01f)
            {
                Debug.Log("✅ Model calculation is CORRECT");
            }
            else
            {
                Debug.LogError("❌ Model calculation has ERROR!");
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
            context.Environment = EnvironmentType.FreeSpace;
            return context;
        }

        private float CalculateExpectedFSPL(float distanceMeters, float frequencyMHz)
        {
            // Standard FSPL formula: FSPL(dB) = 20*log10(d_km) + 20*log10(f_MHz) + 32.45
            float distanceKm = distanceMeters / 1000f;
            return 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequencyMHz) + 32.45f;
        }

        private float GetPathLossExponent(EnvironmentType env)
        {
            // These should match your RFConstants.PATH_LOSS_EXPONENTS
            return env switch
            {
                EnvironmentType.FreeSpace => 2.0f,
                EnvironmentType.Urban => 3.5f,
                _ => 2.0f
            };
        }

        private float GetReferenceDistance(EnvironmentType env)
        {
            // These should match your RFConstants.REFERENCE_DISTANCES
            return env switch
            {
                EnvironmentType.FreeSpace => 1f,
                EnvironmentType.Urban => 100f,
                _ => 1f
            };
        }
    }

    // Additional validation class for integration testing
    public class TransmitterReceiverValidator : MonoBehaviour
    {
        [ContextMenu("Test Live Transmitter-Receiver")]
        public void TestLiveTransmitterReceiver()
        {
            Debug.Log("=== LIVE TRANSMITTER-RECEIVER TEST ===");

            var transmitters = FindObjectsByType<Transmitter>(FindObjectsSortMode.InstanceID);
            var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);

            if (transmitters.Length == 0 || receivers.Length == 0)
            {
                Debug.LogWarning("Need at least 1 transmitter and 1 receiver in scene");
                return;
            }

            var tx = transmitters[0];
            var rx = receivers[0];

            float distance = Vector3.Distance(tx.transform.position, rx.transform.position);
            float calculatedSignal = tx.CalculateSignalStrength(rx.transform.position);

            Debug.Log($"TX: {tx.uniqueID} at {tx.transform.position}");
            Debug.Log($"RX: {rx.uniqueID} at {rx.transform.position}");
            Debug.Log($"Distance: {distance:F1}m");
            Debug.Log($"TX Power: {tx.transmitterPower:F1}dBm");
            Debug.Log($"TX Gain: {tx.antennaGain:F1}dBi");
            Debug.Log($"Frequency: {tx.frequency:F0}MHz");
            Debug.Log($"Propagation Model: {tx.propagationModel}");
            Debug.Log($"Environment: {tx.environmentType}");
            Debug.Log($"Calculated Signal: {calculatedSignal:F1}dBm");
            Debug.Log($"RX Sensitivity: {rx.sensitivity:F1}dBm");
            Debug.Log($"RX Current Signal: {rx.currentSignalStrength:F1}dBm");

            bool shouldConnect = calculatedSignal >= (rx.sensitivity + rx.connectionMargin);
            bool isConnected = rx.IsConnected();

            Debug.Log($"Should Connect: {shouldConnect} (Signal: {calculatedSignal:F1} >= Required: {rx.sensitivity + rx.connectionMargin:F1})");
            Debug.Log($"Is Connected: {isConnected}");

            if (shouldConnect != isConnected)
            {
                Debug.LogWarning("❌ Connection state mismatch!");
            }
            else
            {
                Debug.Log("✅ Connection state is correct");
            }
        }
    }
}