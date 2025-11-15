using UnityEngine;
using RFSimulation.Propagation.Core;
using System.Numerics;

namespace RFSimulation.Utils
{
    public static class RFMathHelper
    {
        // Wavelength: λ = c/f
        public static float CalculateWavelength(float frequencyMHz)
        {
            float frequencyHz = frequencyMHz * 1e6f;
            return RFConstants.SPEED_OF_LIGHT / frequencyHz;
        }

        // FSPL(dB) = 20log10(d) + 20log10(f) + 32.45
        public static float CalculateFSPL(float frequencyMHz, float distanceMeters)
        {
            if (distanceMeters < RFConstants.MIN_DISTANCE)
                distanceMeters = RFConstants.MIN_DISTANCE;

            float distanceKm = UnitConversionHelper.mToKm(distanceMeters);
            float freqMHz = frequencyMHz;

            float fspl = 20f * Mathf.Log10(distanceKm) +
                        20f * Mathf.Log10(freqMHz) +
                        32.45f;

            return fspl;
        }

        // FSLP(dB) = 20log10(4πd/λ)
        public static float CalculateFSPLFromWavelength(float distanceMeters, float wavelengthMeters)
        {
            float fourPiSquared = Mathf.Pow(4f * Mathf.PI, 2f);
            float pathLossLinear = fourPiSquared * distanceMeters * distanceMeters /
                                  (wavelengthMeters * wavelengthMeters);

            if (pathLossLinear <= 0f || float.IsInfinity(pathLossLinear) || float.IsNaN(pathLossLinear))
                return float.NegativeInfinity;

            return 10f * Mathf.Log10(pathLossLinear);
        }

        // Calculate fresnel refelection magnitude using complex permittivity: ε_r - jσ/(ωε_0)
        public static float CalculateFresnelReflectionMagnitude(
            float cosTheta,
            float frequencyMHz,
            double relativePermittivity,
            double conductivity)
        {
            // Clamp cosθ to valid range
            cosTheta = Mathf.Clamp(cosTheta, -1f, 1f);
            float absCos = Mathf.Abs(cosTheta);
            float sin2 = Mathf.Clamp01(1f - absCos * absCos);

            // Physical constants
            const double eps0 = 8.854e-12; 
            const double pi = System.Math.PI;

            // Angular frequency
            double freqHz = UnitConversionHelper.MHzToHz(frequencyMHz);
            double omega = 2.0 * pi * freqHz;

            // Complex relative permittivity
            double imagPart = -conductivity / (omega * eps0);
            Complex epsTilde = new Complex(relativePermittivity, imagPart);

            // Term under sqrt: ε̃_r - sin²θ
            Complex underRoot = epsTilde - new Complex(sin2, 0.0);
            Complex root = Complex.Sqrt(underRoot);

            // TE (perpendicular) polarization
            Complex numTE = new Complex(absCos, 0.0) - root;
            Complex denTE = new Complex(absCos, 0.0) + root;
            Complex gammaTE = numTE / denTE;

            // TM (parallel) polarization
            Complex numTM = epsTilde * absCos - root;
            Complex denTM = epsTilde * absCos + root;
            Complex gammaTM = numTM / denTM;

            // Magnitudes
            double magTE = gammaTE.Magnitude;
            double magTM = gammaTM.Magnitude;

            // Average for unpolarized wave
            double mag = System.Math.Sqrt(0.5 * (magTE * magTE + magTM * magTM));

            return Mathf.Clamp01((float)mag);
        }

        // Conductivity: σ(f) = σ_0 f^n
        public static float CalculateConductivity(
            float conductivityCoefficient,
            float conductivityExponent,
            float frequencyMHz)
        {
            float freqGHz = UnitConversionHelper.MHzToGHz(frequencyMHz);
            return conductivityCoefficient * Mathf.Pow(freqGHz, conductivityExponent);
        }

        // Scattering Coefficient: S = 1 - exp(-2(k_0 σ_h|cosθ|)²)
        public static float CalculateScatteringCoefficient(
            float roughnessMeters,
            float wavelengthMeters,
            float cosIncidence,
            float roughnessFactor = 1f)
        {
            if (roughnessMeters <= 0f || wavelengthMeters <= 0f)
                return 0f;

            // Wave number
            float k0 = 2f * Mathf.PI / wavelengthMeters;

            // Rayleigh parameter
            float x = k0 * roughnessMeters * Mathf.Abs(cosIncidence);

            // Reduction factor: exp(-2x²)
            float roughReduction = Mathf.Exp(-2f * x * x);

            // Scattering coefficient (Kirchhoff approximation)
            float S_rayleigh = 1f - roughReduction;

            // Add material roughness factor
            float S = S_rayleigh * roughnessFactor;

            return Mathf.Clamp01(S);
        }

        // Log-Distance Path Loss Model: PL(d) = PL(d0) + 10n log10(d/d0)
        public static float CalculateLogDistancePathLoss(
            float referencePathLossDb,
            float distance,
            float referenceDistance,
            float pathLossExponent)
        {
            distance = Mathf.Max(distance, RFConstants.MIN_DISTANCE);
            float distanceRatio = distance / referenceDistance;

            float pathLoss = referencePathLossDb + 10f * pathLossExponent * Mathf.Log10(distanceRatio);

            return pathLoss;
        }

    }
}