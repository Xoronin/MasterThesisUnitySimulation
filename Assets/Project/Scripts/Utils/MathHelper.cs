using UnityEngine;

namespace RFSimulation.Utils
{
    public static class MathHelper
    {
        private const float SPEED_OF_LIGHT = 299792458f; // m/s

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
            return 10f * Mathf.Log10(Mathf.Max(linear, 1e-10f)); // Avoid log(0)
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
        /// Clamp value with optional soft limiting
        /// </summary>
        public static float SoftClamp(float value, float min, float max, float softness = 0.1f)
        {
            if (value <= min) return min;
            if (value >= max) return max;

            // Soft limiting near boundaries
            float range = max - min;
            float softZone = range * softness;

            if (value < min + softZone)
            {
                float t = (value - min) / softZone;
                return min + softZone * SmoothStep(t);
            }

            if (value > max - softZone)
            {
                float t = (max - value) / softZone;
                return max - softZone * (1f - SmoothStep(t));
            }

            return value;
        }

        /// <summary>
        /// Smooth step function
        /// </summary>
        public static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Calculate moving average
        /// </summary>
        public static float MovingAverage(float[] values, int count)
        {
            if (values == null || values.Length == 0) return 0f;

            int actualCount = Mathf.Min(count, values.Length);
            float sum = 0f;

            for (int i = 0; i < actualCount; i++)
            {
                sum += values[values.Length - 1 - i];
            }

            return sum / actualCount;
        }

        /// <summary>
        /// Interpolate between values with different interpolation modes
        /// </summary>
        public static float Interpolate(float a, float b, float t, InterpolationMode mode = InterpolationMode.Linear)
        {
            t = Mathf.Clamp01(t);

            return mode switch
            {
                InterpolationMode.Linear => Mathf.Lerp(a, b, t),
                InterpolationMode.Smooth => Mathf.Lerp(a, b, SmoothStep(t)),
                InterpolationMode.Exponential => Mathf.Lerp(a, b, t * t),
                InterpolationMode.Logarithmic => Mathf.Lerp(a, b, Mathf.Sqrt(t)),
                _ => Mathf.Lerp(a, b, t)
            };
        }
    }

    public enum InterpolationMode
    {
        Linear,
        Smooth,
        Exponential,
        Logarithmic
    }
}