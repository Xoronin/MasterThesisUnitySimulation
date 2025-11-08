// RFSimulation/Propagation/FresnelPathLoss.cs
//
// Single-call function that returns total specular reflection path loss in dB.
//
// Includes physically correct Fresnel reflection (complex permittivity) + FSPL.
//
// References:
//   FSPL formula: L_fs[dB] = 20·log10(4πd / λ)
//                = 32.44 + 20·log10(d_km) + 20·log10(f_MHz)
//     → 32.44 = 20·log10(4π·10³ / 3×10⁸) converts meters→km and Hz→MHz
//
//   Reflection loss: L_refl = −20·log10(|Γ|)
//     → Γ = complex Fresnel reflection coefficient magnitude
//
//   Full path loss: L_path = FSPL(d1+d2) + L_refl
//
// All constants and formulas are derived explicitly below.

using System;
using Complex = System.Numerics.Complex;
using UnityEngine;

namespace RFSimulation.Propagation.PathLoss.Helper
{
    [Serializable]
    public class MaterialParams
    {
        public double epsilon_r_prime = 5.0;       // relative permittivity ε′_r
        public double conductivity_S_per_m = 0.02; // conductivity σ [S/m]
        public double roughness_sigma_m = 0.0;     // RMS roughness σ_h [m]
        public double weightTE = 0.5;              // polarization weights
        public double weightTM = 0.5;
    }

    public static class ReflectionHelper
    {
        const double EPS0 = 8.854187817e-12; // Vacuum permittivity [F/m]
        const double C0 = 299792458.0;     // Speed of light [m/s]

        /// <summary>
        /// Returns total path loss [dB] for a single reflection (TX→P→RX).
        /// </summary>
        public static float Compute(Vector3 tx, Vector3 p, Vector3 rx, Vector3 normal,
                                    double freqHz, MaterialParams mat)
        {
            // -------------------------------------------------------------
            // 1. Geometry: incidence angle
            // -------------------------------------------------------------
            var v_i = (p - tx).normalized;                     // incident direction
            double cosThetaI = Math.Clamp(Vector3.Dot(-v_i, normal), 0.0001f, 1.0f);
            double sinTheta2 = 1.0 - cosThetaI * cosThetaI;

            // -------------------------------------------------------------
            // 2. Material: complex permittivity
            // ε̃_r = ε′_r - j σ / (ω ε0)
            // -------------------------------------------------------------
            double omega = 2.0 * Math.PI * freqHz;
            Complex eps_r = new(mat.epsilon_r_prime,
                                -mat.conductivity_S_per_m / (omega * EPS0));

            // -------------------------------------------------------------
            // 3. Fresnel reflection coefficients (air → material)
            // Γ_s = (cosθ_i - √(ε̃_r - sin²θ_i)) / (cosθ_i + √(ε̃_r - sin²θ_i))
            // Γ_p = (ε̃_r cosθ_i - √(ε̃_r - sin²θ_i)) / (ε̃_r cosθ_i + √(ε̃_r - sin²θ_i))
            // -------------------------------------------------------------
            Complex s = Complex.Sqrt(eps_r - sinTheta2);
            Complex Gamma_s = (cosThetaI - s) / (cosThetaI + s);                  // TE
            Complex Gamma_p = (eps_r * cosThetaI - s) / (eps_r * cosThetaI + s);  // TM

            // -------------------------------------------------------------
            // 4. Surface roughness correction (Rayleigh factor)
            // ρ_rough = exp[ -2 (k σ_h sinψ)² ],  k = 2π/λ,  sinψ = √(1 - cos²θ_i)
            // -------------------------------------------------------------
            if (mat.roughness_sigma_m > 0)
            {
                double lambda = C0 / freqHz;
                double k0 = 2.0 * Math.PI / lambda;
                double sinPsi = Math.Sqrt(Math.Max(0.0, 1.0 - cosThetaI * cosThetaI));
                double rho = Math.Exp(-2.0 * Math.Pow(k0 * mat.roughness_sigma_m * sinPsi, 2.0));
                Gamma_s *= rho;
                Gamma_p *= rho;
            }

            // -------------------------------------------------------------
            // 5. Polarization weighting (default equal if unknown)
            // |Γ| = | wTE·Γ_s + wTM·Γ_p |
            // -------------------------------------------------------------
            double wTE = Math.Clamp(mat.weightTE, 0.0, 1.0);
            double wTM = Math.Clamp(mat.weightTM, 0.0, 1.0);
            double sum = Math.Max(1e-6, wTE + wTM);
            wTE /= sum; wTM /= sum;

            Complex Ge = Gamma_s * wTE + Gamma_p * wTM;
            double GammaMag = Math.Clamp(Complex.Abs(Ge), 0.01, 0.999);

            // -------------------------------------------------------------
            // 6. Path geometry: total reflection distance
            // -------------------------------------------------------------
            double d = Vector3.Distance(tx, p) + Vector3.Distance(p, rx);

            // -------------------------------------------------------------
            // 7. Free-space path loss (FSPL)
            // FSPL = 20·log10(4πd / λ)
            // If distance in km and frequency in MHz:
            // FSPL[dB] = 32.44 + 20·log10(d_km) + 20·log10(f_MHz)
            //
            // Derivation:
            //   λ = c / f
            //   20·log10(4π / c) + 20·log10(d) + 20·log10(f)
            //   = 32.44 + 20·log10(d[km]) + 20·log10(f[MHz])
            //
            //   32.44 = 20·log10(4π×10³ / 3×10⁸)
            // -------------------------------------------------------------
            double d_km = d * 0.001;
            double f_MHz = freqHz * 1e-6;
            double fspl = 32.44 + 20.0 * Math.Log10(d_km) + 20.0 * Math.Log10(f_MHz);

            // -------------------------------------------------------------
            // 8. Reflection loss
            // L_refl = −20·log10(|Γ|)
            // The amplitude reflection coefficient Γ gives power ratio |Γ|²,
            // so power loss is −10·log10(|Γ|²) = −20·log10(|Γ|)
            // -------------------------------------------------------------
            double reflLoss = -20.0 * Math.Log10(GammaMag);

            // -------------------------------------------------------------
            // 9. Total path loss
            // L_path = FSPL + L_refl
            // -------------------------------------------------------------
            return (float)(fspl + reflLoss);
        }
    }
}
