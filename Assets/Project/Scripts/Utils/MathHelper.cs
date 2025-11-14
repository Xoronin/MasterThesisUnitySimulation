using UnityEngine;

namespace RFSimulation.Utils
{
    public static class MathHelper
    {
        private const float SPEED_OF_LIGHT = 299792458f; // m/s

        public static float MHzToGHz(float mhz)
        {
            return mhz / 1000f;
        }

        public static float GHzToMHz(float ghz)
        {
            return ghz * 1000f;
        }

        public static float MHzToHz(float mhz)
        {
            return mhz * 1e6f;
        }

        public static float mToKm(float meters)
        {
            return meters / 1000f;
        }

        /// <summary>
        /// Calculate wavelength from frequency
        /// </summary>
        public static float CalculateWavelength(float frequencyMHz)
        {
            float frequencyHz = frequencyMHz * 1e6f;
            return SPEED_OF_LIGHT / frequencyHz;
        }

        /// <summary>
        /// Convert power from dBm to Watts
        /// </summary>
        public static float DbmToWatts(float powerDbm)
        {
            return Mathf.Pow(10f, (powerDbm - 30f) / 10f);
        }

        /// <summary>
        /// Convert power from Watts to dBm
        /// </summary>
        public static float WattsToDbm(float powerWatts)
        {
            return 10f * Mathf.Log10(powerWatts * 1000f);
        }

        /// <summary>
        /// Convert linear value to dB
        /// </summary>
        public static float LinearToDb(float linear)
        {
            return 10f * Mathf.Log10(Mathf.Max(linear, 1e-10f));
        }

        /// <summary>
        /// Convert dB value to linear
        /// </summary>
        public static float DbToLinear(float db)
        {
            return Mathf.Pow(10f, db / 10f);
        }

        /// <summary>
        /// Sample from Gaussian distribution using Box-Muller transform
        /// </summary>
        public static float SampleGaussian(float mean = 0f, float stdDev = 1f)
        {
            float u1 = 1.0f - Random.Range(0f, 1f);
            float u2 = 1.0f - Random.Range(0f, 1f);

            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
                                 Mathf.Sin(2.0f * Mathf.PI * u2);

            return mean + stdDev * randStdNormal;
        }

        /// <summary>
        /// Smooth step function
        /// </summary>
        public static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

    }
}