using UnityEngine;

namespace RFSimulation.Testing
{
    /// <summary>
    /// Reference implementation for path loss calculations
    /// Use this to verify your models are working correctly
    /// </summary>
    public static class ReferenceCalculator
    {
        /// <summary>
        /// Calculate Free Space Path Loss using the standard formula
        /// FSPL(dB) = 20*log10(d_km) + 20*log10(f_MHz) + 32.45
        /// </summary>
        public static float CalculateFreeSpacePathLoss(float distanceMeters, float frequencyMHz)
        {
            if (distanceMeters <= 0 || frequencyMHz <= 0)
            {
                Debug.LogError("Invalid input: distance and frequency must be positive");
                return float.MaxValue;
            }

            float distanceKm = distanceMeters / 1000f;
            float fspl = 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequencyMHz) + 32.45f;

            return fspl;
        }

        /// <summary>
        /// Calculate received power using Friis transmission equation
        /// Pr(dBm) = Pt(dBm) + Gt(dBi) + Gr(dBi) - FSPL(dB)
        /// </summary>
        public static float CalculateReceivedPower(float txPowerDbm, float txGainDbi, float rxGainDbi,
                                                   float distanceMeters, float frequencyMHz)
        {
            float fspl = CalculateFreeSpacePathLoss(distanceMeters, frequencyMHz);
            float rxPower = txPowerDbm + txGainDbi + rxGainDbi - fspl;

            return rxPower;
        }

        /// <summary>
        /// Alternative FSPL calculation using wavelength
        /// FSPL = (4πd/λ)²
        /// </summary>
        public static float CalculateFreeSpacePathLossWavelength(float distanceMeters, float frequencyMHz)
        {
            float wavelengthMeters = 299.792458f / frequencyMHz; // c/f (speed of light / frequency)
            float fsplLinear = Mathf.Pow((4f * Mathf.PI * distanceMeters) / wavelengthMeters, 2f);
            float fsplDb = 10f * Mathf.Log10(fsplLinear);

            return fsplDb;
        }

        /// <summary>
        /// Check if distance is in far-field region
        /// Far-field starts at distance > λ/4π
        /// </summary>
        public static bool IsInFarField(float distanceMeters, float frequencyMHz)
        {
            float wavelengthMeters = 299.792458f / frequencyMHz;
            float farFieldDistance = wavelengthMeters / (4f * Mathf.PI);

            return distanceMeters > farFieldDistance;
        }

        /// <summary>
        /// Calculate Log-Distance path loss
        /// PL(d) = PL(d0) + 10*n*log10(d/d0) + Xσ
        /// </summary>
        public static float CalculateLogDistancePathLoss(float distanceMeters, float referenceDistance,
                                                       float pathLossExponent, float referenceLossDb,
                                                       float shadowingDb = 0f)
        {
            if (distanceMeters < referenceDistance)
            {
                distanceMeters = referenceDistance; // Clamp to reference distance
            }

            float pathLoss = referenceLossDb +
                            10f * pathLossExponent * Mathf.Log10(distanceMeters / referenceDistance) +
                            shadowingDb;

            return pathLoss;
        }

        /// <summary>
        /// Get typical path loss exponents for different environments
        /// </summary>
        public static float GetTypicalPathLossExponent(string environment)
        {
            return environment.ToLower() switch
            {
                "freespace" => 2.0f,
                "urban" => 3.5f,
                _ => 2.0f
            };
        }

        /// <summary>
        /// Validate path loss calculation against known values
        /// </summary>
        public static void ValidateCalculation(string testName, float calculated, float expected, float toleranceDb = 0.1f)
        {
            float error = Mathf.Abs(calculated - expected);

            if (error <= toleranceDb)
            {
                Debug.Log($"✅ {testName}: PASS (Calc: {calculated:F2}dB, Expected: {expected:F2}dB, Error: {error:F3}dB)");
            }
            else
            {
                Debug.LogWarning($"❌ {testName}: FAIL (Calc: {calculated:F2}dB, Expected: {expected:F2}dB, Error: {error:F3}dB)");
            }
        }

        /// <summary>
        /// Run comprehensive validation tests
        /// </summary>
        public static void RunValidationTests()
        {
            Debug.Log("=== REFERENCE CALCULATOR VALIDATION ===");

            // Test 1: Free space at 1km, 2.4GHz
            float fspl1 = CalculateFreeSpacePathLoss(1000f, 2400f);
            ValidateCalculation("FSPL 1km@2.4GHz", fspl1, 100.04f, 0.1f);

            // Test 2: Free space at 100m, 900MHz  
            float fspl2 = CalculateFreeSpacePathLoss(100f, 900f);
            ValidateCalculation("FSPL 100m@900MHz", fspl2, 71.45f, 0.1f);

            // Test 3: Received power calculation
            float rxPower = CalculateReceivedPower(20f, 0f, 0f, 1000f, 2400f);
            ValidateCalculation("RX Power 20dBm,0dBi,1km", rxPower, -80.04f, 0.1f);

            // Test 4: Compare wavelength vs frequency formula
            float fspl_freq = CalculateFreeSpacePathLoss(1000f, 2400f);
            float fspl_wave = CalculateFreeSpacePathLossWavelength(1000f, 2400f);
            ValidateCalculation("Wavelength vs Frequency", fspl_wave, fspl_freq, 0.01f);

            // Test 5: Far-field check
            bool farField1 = IsInFarField(1000f, 2400f); // Should be true
            bool farField2 = IsInFarField(0.01f, 2400f);  // Should be false

            Debug.Log($"Far-field 1000m@2.4GHz: {farField1} (expected: true)");
            Debug.Log($"Far-field 0.01m@2.4GHz: {farField2} (expected: false)");
        }
    }

    /// <summary>
    /// Quick test component to validate your models
    /// </summary>
    public class QuickPathLossTest : MonoBehaviour
    {
        [ContextMenu("Run Quick Validation")]
        public void RunQuickValidation()
        {
            ReferenceCalculator.RunValidationTests();
        }

        [ContextMenu("Compare Your Models")]
        public void CompareYourModels()
        {
            Debug.Log("=== COMPARING YOUR MODELS TO REFERENCE ===");

            // Test parameters
            float[] distances = { 100f, 1000f, 5000f };
            float[] frequencies = { 900f, 1800f, 2400f };
            float power = 30f;
            float gain = 0f;

            var freeSpaceModel = new RFSimulation.Propagation.PathLoss.Models.FreeSpaceModel();

            foreach (float freq in frequencies)
            {
                foreach (float dist in distances)
                {
                    // Reference calculation
                    float refFSPL = ReferenceCalculator.CalculateFreeSpacePathLoss(dist, freq);
                    float refRxPower = power + gain - refFSPL;

                    // Your model calculation
                    var context = RFSimulation.Propagation.Core.PropagationContext.Create(
                        Vector3.zero, Vector3.forward * dist, power, freq);
                    context.AntennaGainDbi = gain;
                    context.Environment = RFSimulation.Propagation.Core.EnvironmentType.FreeSpace;

                    float yourRxPower = freeSpaceModel.Calculate(context);
                    float error = yourRxPower - refRxPower;

                    string status = Mathf.Abs(error) < 0.1f ? "✅" : "❌";
                    Debug.Log($"{status} {freq}MHz, {dist}m: Ref={refRxPower:F2}dBm, Yours={yourRxPower:F2}dBm, Error={error:F3}dB");
                }
            }
        }
    }
}