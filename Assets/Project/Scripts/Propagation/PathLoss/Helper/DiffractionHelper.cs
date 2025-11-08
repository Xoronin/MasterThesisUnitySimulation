// RFSimulation/Propagation/KnifeEdgePathLoss.cs
//
// Single-edge (knife-edge) diffraction path loss in dB for a TX → edge → RX path.
// This matches the widely used ITU-R P.526 "v-parameter" formulation.
//
// References & formulas (documented where used):
//   • Wavelength: λ = c / f , with c = 299,792,458 m/s
//   • Fresnel-parameter v (single edge):
//       v = h * sqrt( (2/λ) * (d1 + d2) / (d1 * d2) )
//     where:
//       d1 = |TX - edge|,  d2 = |edge - RX|  (in meters)
//       h  = edge height ABOVE the straight LOS line between TX and RX (meters)
//           (negative h means the edge is below the LOS line)
//   • Diffraction loss L_d [dB] (ITU-R P.526 approximation):
//       if v <= -0.78  →  L_d = 0 dB      (no additional loss)
//       else           →  L_d = 6.9 + 20 log10( sqrt((v - 0.1)^2 + 1) + v - 0.1 )
//   • Free-space path loss (FSPL) over total geometric distance d = d1 + d2:
//       L_fs[dB] = 20 log10(4π d / λ)
//                = 32.44 + 20 log10(d_km) + 20 log10(f_MHz)
//       (32.44 is the unit-change constant from meters/Hz to km/MHz:
//        32.44 = 20·log10(4π·10^3 / 3·10^8))
//
// Total diffraction path loss:
//   L_path[dB] = FSPL(d1 + d2, f) + L_d(v)
//
// NOTE: This function assumes you've already chosen the edge point `pEdge`.
//       It computes `h` from geometry only (no normals required).

using System;
using UnityEngine;

namespace RFSimulation.Propagation.PathLoss.Helper
{
    public static class DiffractionHelper
    {

        private const double C0 = 299792458.0; // speed of light [m/s]

        /// <summary>
        /// Returns total path loss [dB] for a single knife-edge diffraction path (TX → pEdge → RX).
        /// </summary>
        /// <param name="tx">Transmitter world position</param>
        /// <param name="pEdge">Chosen diffraction point on the edge (roofline/corner)</param>
        /// <param name="rx">Receiver world position</param>
        /// <param name="freqHz">Carrier frequency in Hz</param>
        public static float ComputeKnifeEdgePathLoss(Vector3 tx, Vector3 pEdge, Vector3 rx, double freqHz)
        {
            // -------------------------------------------------------------
            // 1) Distances: d1 = |TX - edge|, d2 = |edge - RX|
            // -------------------------------------------------------------
            double d1 = Vector3.Distance(tx, pEdge);
            double d2 = Vector3.Distance(pEdge, rx);
            if (d1 < 1e-6 || d2 < 1e-6)
                return float.PositiveInfinity; // degenerate geometry

            // -------------------------------------------------------------
            // 2) Edge height above LOS line (h)
            //    Compute linear height of the TX→RX straight line at the
            //    longitudinal position of the edge, then h = y_edge - y_LOS
            // -------------------------------------------------------------
            double h = EdgeClearanceHeight(tx, rx, pEdge);

            // -------------------------------------------------------------
            // 3) Wavelength λ = c / f   and v-parameter:
            //       v = h * sqrt( (2/λ) * (d1 + d2) / (d1 * d2) )
            // -------------------------------------------------------------
            double lambda = C0 / Math.Max(freqHz, 1.0);
            double root = Math.Sqrt(Math.Max(0.0, (2.0 / lambda) * ((d1 + d2) / (d1 * d2))));
            double v = h * root;

            // -------------------------------------------------------------
            // 4) Diffraction loss L_d(v) per ITU-R P.526 approximation
            // -------------------------------------------------------------
            double Ld;
            if (v <= -0.78)
            {
                Ld = 0.0; // no additional loss when edge well below LOS
            }
            else
            {
                // L_d = 6.9 + 20 log10( sqrt((v - 0.1)^2 + 1) + v - 0.1 )
                double term = Math.Sqrt((v - 0.1) * (v - 0.1) + 1.0) + v - 0.1;
                Ld = 6.9 + 20.0 * Math.Log10(Math.Max(term, 1e-12));
            }

            // -------------------------------------------------------------
            // 5) FSPL over the total geometric length d = d1 + d2
            //     L_fs[dB] = 32.44 + 20·log10(d_km) + 20·log10(f_MHz)
            // -------------------------------------------------------------
            double d = d1 + d2;
            double d_km = d * 0.001;
            double f_MHz = freqHz * 1e-6;
            double Lfs = 32.44 + 20.0 * Math.Log10(Math.Max(d_km, 1e-9)) + 20.0 * Math.Log10(Math.Max(f_MHz, 1e-9));

            // -------------------------------------------------------------
            // 6) Total diffraction path loss
            // -------------------------------------------------------------
            return (float)(Lfs + Ld);
        }

        /// <summary>
        /// Computes the edge height above (or below, if negative) the straight TX→RX line at the
        /// edge's longitudinal position. This is the 'h' used in the v-parameter.
        ///
        /// Steps:
        ///   • Project edge onto TX→RX direction to get fractional position t ∈ [0,1]
        ///   • Interpolate LOS height: y_LOS = y_tx + t (y_rx - y_tx)
        ///   • h = y_edge - y_LOS
        /// </summary>
        private static double EdgeClearanceHeight(in Vector3 tx, in Vector3 rx, in Vector3 pEdge)
        {
            Vector3 dir = rx - tx;
            double L = dir.magnitude;
            if (L < 1e-6) return 0.0;

            Vector3 u = dir / (float)L;                      // unit direction TX→RX
            double t = Vector3.Dot(pEdge - tx, u) / L;      // fractional distance along the line
            t = Math.Max(0.0, Math.Min(1.0, t));             // clamp to segment

            double yLOS = tx.y + t * (rx.y - tx.y);          // linear LOS height at edge
            return pEdge.y - yLOS;                           // positive = above LOS
        }

        // RFSimulation/Propagation/UTDWedgePathLoss.cs
        //
        // Uniform Theory of Diffraction (UTD) — Perfectly Conducting Wedge (PC-UTD)
        // Returns TOTAL path loss [dB] for a single wedge diffraction path TX → pEdge → RX.
        //
        // Geometry (standard UTD wedge):
        //   • Wedge edge is a line; cross-section in the plane ⟂ to the edge is a 2D wedge.
        //   • Wedge exterior (open) angle:  α  (radians),  with  n = π / α    (wedge index)
        //   • φ'  = incident angle   (radians) measured from a chosen wedge face
        //   • φ   = observation angle (radians) measured from the same face
        //   • k = 2π/λ  with  λ = c / f
        //
        // PC-UTD diffraction coefficient (deep-shadow, transition functions F≈1):
        //   D_pc(φ,φ') = - e^{-jπ/4} / [ 2 n √(2π k) ] · S
        //   where S = cot( (π + (φ - φ'))/(2n) ) + cot( (π - (φ - φ'))/(2n) )
        //           + cot( (π + (φ + φ'))/(2n) ) + cot( (π - (φ + φ'))/(2n) )
        //
        // Field relation (UTD amplitude law):
        //   |E_diffracted| ≈ |E_incident| · |D_pc| / √(d1 d2)
        //   with geometric distances: d1 = |TX - pEdge|, d2 = |pEdge - RX|
        //
        // Convert to PATH LOSS relative to FSPL over total length d = d1 + d2:
        //   FSPL(d) = 20 log10(4π d / λ)
        //   Extra UTD loss (relative to FSPL(d)):
        //     L_extra = -20 log10(|D_pc|) - 10 log10( d^2 / (d1 d2) )
        //
        // Therefore TOTAL path loss:
        //   L_path = FSPL(d) + L_extra
        //
        // Notes:
        //   • This "deep-shadow" PC-UTD is reliable away from shadow boundaries.
        //   • Near the shadow boundary, full UTD uses transition functions F(x); here we set F≈1.
        //   • For lossy wedges (building corners), apply Luebbers-style corrections (future extension).
        //
        // Inputs you must provide:
        //   - tx, rx: world positions
        //   - pEdge  : chosen edge point (where diffraction is evaluated)
        //   - edgeDir: unit vector along wedge edge (world space)
        //   - faceN  : unit normal of the reference wedge face (world space), used to define φ, φ'
        //   - alpha  : wedge exterior angle α in radians (0<α<π for a convex edge; e.g., α≈90°→π/2)
        //   - freqHz : carrier frequency
        //
        // How φ, φ' are computed here:
        //   • Build a 2D local frame in the cross-section plane (plane ⟂ to edgeDir).
        //   • Project incident (pEdge→TX) and observation (pEdge→RX) directions to this plane.
        //   • Measure angles to the "reference face direction" (the inward tangent of the face):
        //        t_face = normalize( edgeDir × faceN )   (lies in the cross-section plane)
        //     φ' = signed_angle( t_face, v_i_proj ),   φ = signed_angle( t_face, v_o_proj )
        //   • Both angles are wrapped into [0, 2π) as UTD expects.
        //
        // Limitations/Next steps:
        //   - No polarization-specific UTD splitting here; good as scalar loss for RSS/RSRP.
        //   - Add transition functions + Luebbers corrections later if needed.


        /// <summary>
        /// Compute TOTAL path loss [dB] for a PC-UTD wedge diffraction path.
        /// </summary>
        /// <param name="tx">Transmitter world position</param>
        /// <param name="pEdge">Diffraction point on the wedge edge</param>
        /// <param name="rx">Receiver world position</param>
        /// <param name="edgeDir">Unit vector along wedge edge</param>
        /// <param name="faceNormal">Unit normal of the chosen wedge face (defines φ=0 axis)</param>
        /// <param name="alphaRad">Wedge exterior angle α [rad], e.g. 90°→π/2</param>
        /// <param name="freqHz">Carrier frequency [Hz]</param>
        public static float ComputeUTDWedgePathLoss(
            in Vector3 tx, in Vector3 pEdge, in Vector3 rx,
            in Vector3 edgeDir, in Vector3 faceNormal,
            double alphaRad, double freqHz)
        {
            // Distances
            double d1 = Vector3.Distance(tx, pEdge);
            double d2 = Vector3.Distance(pEdge, rx);
            if (d1 < 1e-6 || d2 < 1e-6) return float.PositiveInfinity;
            double d = d1 + d2;

            // Wedge index n = π / α
            double n = Math.PI / Math.Max(1e-6, alphaRad);

            // Wave number
            double lambda = C0 / Math.Max(freqHz, 1.0);
            double k = 2.0 * Math.PI / lambda;

            // Build cross-section plane basis (⊥ edge)
            Vector3 ez = edgeDir.normalized;
            // Reference face tangent direction in cross-section plane:
            // t_face = normalize( edgeDir × faceNormal )
            Vector3 t_face = Vector3.Cross(ez, faceNormal).normalized;
            if (t_face.sqrMagnitude < 1e-8f)
            {
                // Degenerate: faceNormal // edgeDir — pick any orthogonal
                Vector3 any = Math.Abs(ez.x) < 0.9f ? Vector3.right : Vector3.up;
                t_face = Vector3.Cross(ez, any).normalized;
            }
            // The other in-plane axis:
            Vector3 q_face = Vector3.Cross(ez, t_face).normalized; // completes RHS basis in the cross-section

            // Incident & observation unit directions projected to cross-section plane
            Vector3 vi = (tx - pEdge).normalized;
            Vector3 vo = (rx - pEdge).normalized;

            Vector2 vi2 = ProjectToPlane2D(vi, t_face, q_face);
            Vector2 vo2 = ProjectToPlane2D(vo, t_face, q_face);

            double phiPrime = Angle0To2Pi(vi2); // φ'
            double phi = Angle0To2Pi(vo2); // φ

            // UTD PC diffraction coefficient (deep-shadow, F≈1)
            double Dmag = UTD_PC_DeepShadow_Magnitude(phi, phiPrime, n, k);

            // Extra UTD loss relative to FSPL(d):
            // L_extra = -20 log10(|D|) - 10 log10( d^2 / (d1 d2) )
            double Lextra = -20.0 * Math.Log10(Math.Max(Dmag, 1e-12))
                            - 10.0 * Math.Log10(Math.Max((d * d) / (d1 * d2), 1e-12));

            // FSPL(d) = 32.44 + 20 log10(d[km]) + 20 log10(f[MHz])
            double d_km = d * 0.001;
            double f_MHz = freqHz * 1e-6;
            double Lfs = 32.44 + 20.0 * Math.Log10(Math.Max(d_km, 1e-12))
                                    + 20.0 * Math.Log10(Math.Max(f_MHz, 1e-12));

            return (float)(Lfs + Lextra);
        }

        // --- Helpers ---

        // PC-UTD deep-shadow coefficient magnitude (no transition function)
        // |D_pc| = 1 / [ 2 n √(2π k) ] · | S |
        private static double UTD_PC_DeepShadow_Magnitude(double phi, double phiPrime, double n, double k)
        {
            double a = (phi - phiPrime);
            double b = (phi + phiPrime);

            double term1 = Cot((Math.PI + a) / (2.0 * n));
            double term2 = Cot((Math.PI - a) / (2.0 * n));
            double term3 = Cot((Math.PI + b) / (2.0 * n));
            double term4 = Cot((Math.PI - b) / (2.0 * n));

            double S = term1 + term2 + term3 + term4;

            double denom = 2.0 * n * Math.Sqrt(2.0 * Math.PI * k);
            return Math.Abs(S) / Math.Max(denom, 1e-12);
        }

        private static double Cot(double x)
        {
            double s = Math.Sin(x);
            double c = Math.Cos(x);
            return c / Math.Max(s, 1e-12);
        }

        // Project v onto the cross-section plane basis (t_face, q_face)
        private static Vector2 ProjectToPlane2D(in Vector3 v, in Vector3 t_face, in Vector3 q_face)
        {
            return new Vector2(Vector3.Dot(v, t_face), Vector3.Dot(v, q_face)).normalized;
        }

        // Return angle in [0, 2π) for a 2D vector
        private static double Angle0To2Pi(in Vector2 xy)
        {
            double ang = Math.Atan2(xy.y, xy.x);
            return (ang >= 0.0) ? ang : (ang + 2.0 * Math.PI);
        }
    }

}
