using RFSimulation.Environment;
using RFSimulation.Propagation.Core;
using System;
using System.Numerics;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

namespace RFSimulation.Utils
{
    public static class RFMathHelper
    {
        // Wavelength: λ = c/f
        public static float CalculateWavelength(float frequencyMHz)
        {
            float frequencyHz = UnitConversionHelper.MHzToHz(frequencyMHz);
            return RFConstants.SPEED_OF_LIGHT / frequencyHz;
        }

        // FSLP(dB) = 20log10(4πd/λ)
        public static float CalculateFSPL(float distanceM, float frequencyMHz)
        {
            if (distanceM < RFConstants.MIN_DISTANCE)
                distanceM = RFConstants.MIN_DISTANCE;

            float wavelength = RFMathHelper.CalculateWavelength(frequencyMHz);
            float fourPiSquared = Mathf.Pow(4f * Mathf.PI, 2f);
            float pathLossLinear = fourPiSquared * distanceM * distanceM /
                                  (wavelength * wavelength);

            if (pathLossLinear <= 0f || float.IsInfinity(pathLossLinear) || float.IsNaN(pathLossLinear))
                return float.NegativeInfinity;

            float fspl = 10f * Mathf.Log10(pathLossLinear);

            return fspl;
        }

        // Calculate fresnel refelection coefficients using complex permittivity
        public static float CalculateFresnelReflectionCoefficients(
            float cosTheta,
            float frequencyMHz,
            BuildingMaterial material)
        {
            float conductivity = CalculateConductivity(material.conductivityCoefficientSM, material.conductivityExponent, frequencyMHz);

            // Clamp cosθ to valid range
            cosTheta = Mathf.Clamp(cosTheta, -1f, 1f);
            float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
            float cos2Theta = cosTheta * cosTheta;

            // Angular frequency
            double freqHz = UnitConversionHelper.MHzToHz(frequencyMHz);
            double omega = 2.0 * RFConstants.PI * freqHz;

            // Complex relative permittivity: ε_r = -j * conductivity(σ) / angular_frequency(ω) * ε_0
            double imagPart = -conductivity / (omega * RFConstants.EPS0);
            Complex epsComplex = new Complex(material.relativePermittivity, imagPart);

            // Term under sqrt: ~ε_r - cos^2(theta_i)
            Complex underRoot = epsComplex - new Complex(cos2Theta, 0.0);
            Complex root = Complex.Sqrt(underRoot);

            // Vertical polarization: (sinTheta_i - root_term) / (sinTheta_i + root_term)
            Complex numTM = sinTheta - root;
            Complex denTM = sinTheta + root;
            Complex gammaTM = numTM / denTM;

            // Coefficients
            double magTM = gammaTM.Magnitude;

            return Mathf.Clamp01((float)magTM);
        }

        // Roughness correction factor p_s -> returrn 1 = smooth
        public static float CalculateRoughnessCorrection(BuildingMaterial material, float cosThetaI, float frequencyMHz, out float g)
        {
            g = 0f;
            float sigmaMeters = material.roughnessMM * 0.001f;
            if (sigmaMeters <= 0f)
                return 1f;

            cosThetaI = Mathf.Clamp01(Mathf.Abs(cosThetaI));
            float sinThetaI = Mathf.Sqrt(Mathf.Clamp01(1f - cosThetaI * cosThetaI));

            float lambda = CalculateWavelength(frequencyMHz);

            // g = π σ_h sinθ / λ
            g = Mathf.PI * sigmaMeters * sinThetaI / lambda;

            // ρ_s = exp(-8 g^2)
            float rho_s = Mathf.Exp(-8f * g * g);

            return Mathf.Clamp01(rho_s);
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

        // Sample from Gaussian distribution using Box-Muller transform
        public static float SampleGaussian(
            float mean = 0f,
            float stdDev = 1f,
            float clampMin = -15f,
            float clampMax = 15f)
        {
            const float MIN_RANDOM = 0.0001f;
            const float MAX_RANDOM = 0.9999f;

            // Generate two uniform random numbers
            float u1 = Mathf.Clamp(Random.Range(0f, 1f), MIN_RANDOM, MAX_RANDOM);
            float u2 = Mathf.Clamp(Random.Range(0f, 1f), MIN_RANDOM, MAX_RANDOM);

            // Box-Muller transform
            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);

            float result = mean + stdDev * randStdNormal;

            return Mathf.Clamp(result, clampMin, clampMax);
        }

        public static float SampleLogNormalShadowing(float shadowingStdDevDb = 4f)
        {
            return SampleGaussian(0f, shadowingStdDevDb, -15f, 15f);
        }

        // Fresnel parameter for knife-edge diffraction
        public static float FresnelV(Vector3 tx, Vector3 rx, Vector3 edge, float frequencyMHz)
        {
            float d1 = Vector3.Distance(tx, edge);
            float d2 = Vector3.Distance(edge, rx);
            if (d1 < RFConstants.EPS || d2 < RFConstants.EPS) return 0f;

            float wavelength = CalculateWavelength(frequencyMHz);

            Vector3 dir = rx - tx;
            float dTot = dir.magnitude;
            if (dTot < RFConstants.EPS) return 0f;

            Vector3 dirN = dir / dTot;

            float along = Vector3.Dot(edge - tx, dirN);
            float t = Mathf.Clamp01(along / dTot);

            float lineHeight = Mathf.Lerp(tx.y, rx.y, t);

            float h = edge.y - lineHeight;
            if (h <= 0f) return 0f;

            // calculate v: v = h * sqrt(2 * (d1 + d2) / (lambda * d1 * d2))
            float factor = Mathf.Sqrt(2f * (d1 + d2) / (wavelength * d1 * d2));
            float v = h * factor;
            return v;
        }

        public static float KnifeEdgeLoss(float v)
        {
            if (v == -0.78f)
                return 0f;

            // L(v) = 6.9 + 20 log10{ sqrt[(v - 0.1)^2 + 1] + v - 0.1 }
            float term = Mathf.Sqrt((v - 0.1f) * (v - 0.1f) + 1f) + v - 0.1f;
            float L = 6.9f + 20f * Mathf.Log10(term);

            return Mathf.Max(0f, L);
        }

        public static float ScatteringLoss(float cosThetaI, float cosThetaS, float S)
        {
            float angularFactor = Mathf.Sqrt(cosThetaI * cosThetaS);
            float gainTerm = S * angularFactor;
            float scatteringLossDb = -20f * Mathf.Log10(gainTerm);

            return scatteringLossDb;
        }
    }
}