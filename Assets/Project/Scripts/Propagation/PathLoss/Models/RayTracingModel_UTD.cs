//// RFSimulation/Propagation/Models/RayTracingModel.cs
//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using RFSimulation.Propagation.Core;
//using RFSimulation.Visualization;
//using RFSimulation.Core;
//using RFSimulation.Interfaces;

//namespace RFSimulation.Propagation.PathLoss.Models
//{
//    /// <summary>
//    /// Urban ray tracing with:
//    ///  - LOS (direct)
//    ///  - Single-bounce specular reflection (wall plane/image method)
//    ///  - Single-edge diffraction (UTD-lite wedge; rooftops + vertical corners)
//    ///  - Optional diffuse scattering (rough façades)
//    ///
//    /// Visualization is delegated to RayVisualization (set via .Visualizer).
//    /// </summary>
//    [Serializable]
//    public class RayTracingModel : IPathLossModel
//    {
//        public string ModelName => "Ray Tracing";

//        public float Calculate(PropagationContext context)
//        {
//            var buildingsMask = context.BuildingLayers.HasValue
//                ? context.BuildingLayers.Value
//                : mapboxBuildingLayer;

//            var paths = new List<PathContribution>(16);
//            float bestLossDb = float.PositiveInfinity;

//            if (enableRayVisualization && Visualizer != null) Visualizer.BeginFrame();
//            try
//            {
//                TryDirect(context, buildingsMask, ref bestLossDb, paths);
//                if (maxReflections > 0) TrySingleBounceReflections(context, buildingsMask, ref bestLossDb, paths);
//                if (maxDiffractions > 0) TrySingleEdgeDiffractions(context, buildingsMask, ref bestLossDb, paths);
//                if (enableDiffuseScattering) TrySingleBounceScattering(context, buildingsMask, ref bestLossDb, paths);

//                if (paths.Count > 0)
//                {
//                    float prxDbm = CombinePathsPhasor(paths, context.TransmitterPowerDbm, context.WavelengthMeters);
//                    return context.TransmitterPowerDbm - prxDbm;
//                }
//                return float.IsPositiveInfinity(bestLossDb) ? blockedLossDb : bestLossDb;
//            }
//            finally
//            {
//                if (enableRayVisualization && Visualizer != null) Visualizer.EndFrame();
//            }
//        }

//        // -----------------------------
//        // Public configuration
//        // -----------------------------
//        [Header("General")]
//        public bool preferAsPrimary = false;
//        public float maxDistance = 1000f;
//        [Tooltip("Returned if no path is found.")]
//        public float blockedLossDb = 200f;

//        [Tooltip("Maximum number of reflections (currently 1 supported).")]
//        public int maxReflections = 1;

//        [Tooltip("Maximum number of diffractions (currently 1 supported).")]
//        public int maxDiffractions = 1;

//        [Tooltip("Layer used if context does not supply one (bitmask).")]
//        public LayerMask mapboxBuildingLayer = 1 << 8;

//        [Header("Diffraction")]
//        [Tooltip("Use UTD-style wedge diffraction (on) or fallback knife-edge (off).")]
//        public bool useUtdDiffraction = true;

//        [Header("Scattering")]
//        public bool enableDiffuseScattering = false;
//        public bool showScatterRays = true;
//        public float defaultScatterAlbedo = 0.25f;         // 0..1
//        public int scatterLobeExponent = 1;                // 1=Lambertian
//        public float scatterBaseLossDb = 16f;              // base diffuse loss

//        [Header("Visualization")]
//        public bool enableRayVisualization = true;
//        public bool showDirectRays = true;
//        public bool showReflectionRays = true;
//        public bool showDiffractionRays = true;
//        public bool showBlockedPaths = true;

//        public Color directRayColor = Color.green;
//        public Color reflectionRayColor = Color.blue;
//        public Color diffractionRayColor = new Color(1f, 0.7f, 0f); // orange
//        public Color blockedRayColor = Color.red;
//        public Color scatterRayColor = Color.cyan;

//        /// <summary>Set this from your manager/transmitter.</summary>
//        public RayVisualization Visualizer { get; set; }

//        // -----------------------------
//        // Internal constants
//        // -----------------------------
//        const float MIN_STEP = 0.01f;
//        const float NORMAL_EPS = 1e-6f;
//        const float EPS = 1e-6f;

//        // -----------------------------
//        // 1) Direct
//        // -----------------------------
//        private void TryDirect(PropagationContext ctx, LayerMask buildingsMask, ref float bestLossDb, List<PathContribution> paths)
//        {
//            var tx = ctx.TransmitterPosition;
//            var rx = ctx.ReceiverPosition;

//            var dir = rx - tx;
//            var dist = dir.magnitude;
//            if (dist < MIN_STEP || dist > maxDistance) return;

//            dir /= Mathf.Max(dist, EPS);

//            if (Physics.Raycast(tx, dir, out var hit, dist, buildingsMask))
//            {
//                if (enableRayVisualization && showDirectRays && showBlockedPaths && Visualizer != null)
//                    Visualizer.DrawSegment(tx, hit.point, blockedRayColor, $"Direct blocked by {hit.collider.name}");
//                return;
//            }

//            var fsplDb = FSPL(ctx.FrequencyMHz, dist);
//            bestLossDb = Mathf.Min(bestLossDb, fsplDb);
//            paths.Add(new PathContribution
//            {
//                LossDb = fsplDb,
//                DistanceMeters = dist,
//                ExtraPhaseRad = 0f
//            });

//            if (enableRayVisualization && showDirectRays && Visualizer != null)
//                Visualizer.DrawSegment(tx, rx, directRayColor, $"LOS (loss {fsplDb:F1} dB)");
//        }

//        // -----------------------------
//        // 2) Single-bounce reflection (image method)
//        // -----------------------------
//        private void TrySingleBounceReflections(PropagationContext ctx, LayerMask buildingsMask, ref float bestLossDb, List<PathContribution> paths)
//        {
//            var tx = ctx.TransmitterPosition;
//            var rx = ctx.ReceiverPosition;

//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.6f + 3f,
//                Mathf.Max(5f, Mathf.Abs(tx.y - rx.y)) * 0.6f + 3f,
//                Mathf.Abs(tx.z - rx.z) * 0.6f + 3f
//            );

//            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, buildingsMask);
//            foreach (var col in colliders)
//            {
//                var rend = col.GetComponent<Renderer>();
//                if (!rend) continue;

//                var b = rend.bounds;
//                foreach (var wall in GetWallPlanes(b))
//                {
//                    if (!TrySpecularPoint(tx, rx, wall, out var p)) continue;
//                    if (!PointInsideWallRect(p, wall.Bounds, wall.Normal)) continue;

//                    var d1 = Vector3.Distance(tx, p);
//                    var d2 = Vector3.Distance(p, rx);
//                    if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance) continue;

//                    if (Physics.Raycast(tx, (p - tx).normalized, out var hit1, d1, buildingsMask))
//                    {
//                        if (enableRayVisualization && showReflectionRays && showBlockedPaths && Visualizer != null)
//                            Visualizer.DrawPolyline(new[] { tx, hit1.point, rx }, blockedRayColor, "Reflection (TX->P blocked)");
//                        continue;
//                    }
//                    if (Physics.Raycast(p, (rx - p).normalized, out var hit2, d2, buildingsMask))
//                    {
//                        if (enableRayVisualization && showReflectionRays && showBlockedPaths && Visualizer != null)
//                            Visualizer.DrawPolyline(new[] { tx, p, hit2.point }, blockedRayColor, "Reflection (P->RX blocked)");
//                        continue;
//                    }

//                    // Material: reflection coefficient magnitude (0..1)
//                    var bld = col.GetComponent<RFSimulation.Environment.Building>();
//                    float gamma = 0.5f;
//                    if (bld && bld.material)
//                        gamma = Mathf.Clamp01(bld.material.reflectionCoefficient);

//                    var inDir = (p - tx).normalized;
//                    float cosInc = IncidenceCos(inDir, wall.Normal);

//                    float reflLossDb = ReflectionLossDb(gamma, cosInc);
//                    float reflPhase = ReflectionExtraPhase(gamma);

//                    var loss = FSPL(ctx.FrequencyMHz, d1) + FSPL(ctx.FrequencyMHz, d2) + reflLossDb;
//                    bestLossDb = Mathf.Min(bestLossDb, loss);

//                    paths.Add(new PathContribution
//                    {
//                        LossDb = loss,
//                        DistanceMeters = d1 + d2,
//                        ExtraPhaseRad = reflPhase
//                    });

//                    if (enableRayVisualization && showReflectionRays && Visualizer != null)
//                        Visualizer.DrawPolyline(new[] { tx, p, rx }, reflectionRayColor,
//                            $"Reflection via {col.name} |Γ|={gamma:F2}, θi≈{Mathf.Acos(cosInc) * Mathf.Rad2Deg:F0}° (loss {loss:F1} dB)");

//                    if (maxReflections <= 1) break;
//                }
//            }
//        }

//        // -----------------------------
//        // 3) Single-edge diffraction (UTD-lite wedge)
//        // -----------------------------
//        private void TrySingleEdgeDiffractions(PropagationContext ctx, LayerMask buildingsMask, ref float bestLossDb, List<PathContribution> paths)
//        {
//            var tx = ctx.TransmitterPosition;
//            var rx = ctx.ReceiverPosition;

//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.7f + 5f,
//                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) * 0.7f + 5f,
//                Mathf.Abs(tx.z - rx.z) * 0.7f + 5f
//            );

//            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, buildingsMask);
//            var edges = ExtractBuildingEdges(colliders); // roof rims + vertical corners, with dir + wedgeAlpha

//            foreach (var e in edges)
//            {
//                var p = ClosestPointBetweenSegments(tx, rx, e.start, e.end);

//                var d1 = Vector3.Distance(tx, p);
//                var d2 = Vector3.Distance(p, rx);
//                if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance) continue;

//                if (Physics.Raycast(tx, (p - tx).normalized, out var hit1, d1, buildingsMask))
//                {
//                    if (enableRayVisualization && showDiffractionRays && showBlockedPaths && Visualizer != null)
//                        Visualizer.DrawPolyline(new[] { tx, hit1.point, rx }, blockedRayColor, "Diffraction (TX->edge blocked)");
//                    continue;
//                }
//                if (Physics.Raycast(p, (rx - p).normalized, out var hit2, d2, buildingsMask))
//                {
//                    if (enableRayVisualization && showDiffractionRays && showBlockedPaths && Visualizer != null)
//                        Visualizer.DrawPolyline(new[] { tx, p, hit2.point }, blockedRayColor, "Diffraction (edge->RX blocked)");
//                    continue;
//                }

//                float loss;
//                float extraPhase;
//                var lambda = ctx.WavelengthMeters;

//                if (useUtdDiffraction)
//                {
//                    var utd = ComputeUtdWedgeCoeff(tx, rx, p, e.dir, e.wedgeAlphaRad, lambda);

//                    // Field ~ |D|/sqrt(d1 d2) → extra dB term from |D|
//                    float diffDb = -20f * Mathf.Log10(Mathf.Max(utd.mag, 1e-6f));

//                    loss = FSPL(ctx.FrequencyMHz, d1) + FSPL(ctx.FrequencyMHz, d2) + diffDb;
//                    extraPhase = utd.phase;
//                }
//                else
//                {
//                    // Fallback: knife-edge
//                    var v = FresnelParameterV(tx, rx, p, lambda);
//                    var diffLossDb = KnifeEdgeDiffractionLossDb(v);
//                    loss = FSPL(ctx.FrequencyMHz, d1) + FSPL(ctx.FrequencyMHz, d2) + diffLossDb;
//                    extraPhase = -Mathf.PI * 0.25f;
//                }

//                bestLossDb = Mathf.Min(bestLossDb, loss);
//                paths.Add(new PathContribution
//                {
//                    LossDb = loss,
//                    DistanceMeters = d1 + d2,
//                    ExtraPhaseRad = extraPhase
//                });

//                if (enableRayVisualization && showDiffractionRays && Visualizer != null)
//                {
//                    var txt = useUtdDiffraction
//                        ? $"UTD wedge (α={e.wedgeAlphaRad * Mathf.Rad2Deg:F0}°) loss {loss:F1} dB"
//                        : $"Knife-edge loss {loss:F1} dB";
//                    Visualizer.DrawPolyline(new[] { tx, p, rx }, diffractionRayColor, txt);
//                }

//                if (maxDiffractions <= 1) break;
//            }
//        }

//        // -----------------------------
//        // 4) Diffuse scattering (optional)
//        // -----------------------------
//        private void TrySingleBounceScattering(PropagationContext ctx, LayerMask buildingsMask, ref float bestLossDb, List<PathContribution> paths)
//        {
//            var tx = ctx.TransmitterPosition;
//            var rx = ctx.ReceiverPosition;

//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.7f + 5f,
//                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) * 0.7f + 5f,
//                Mathf.Abs(tx.z - rx.z) * 0.7f + 5f
//            );

//            var cols = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, buildingsMask);
//            foreach (var col in cols)
//            {
//                var rend = col.GetComponent<Renderer>(); if (!rend) continue;
//                var b = rend.bounds;

//                foreach (var wall in GetWallPlanes(b))
//                {
//                    // Center sample near specular (or wall center)
//                    Vector3 center = wall.Point;
//                    if (TrySpecularPoint(tx, rx, wall, out var pSpec)) center = ClampToWallRect(pSpec, wall.Bounds, wall.Normal);

//                    foreach (var p0 in SampleAroundCenter(center, wall.Bounds, wall.Normal, 3, 0.2f))
//                    {
//                        var p = p0 + wall.Normal * 0.01f;

//                        var d1 = Vector3.Distance(tx, p);
//                        var d2 = Vector3.Distance(p, rx);
//                        if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance) continue;

//                        if (Occluded(tx, p, buildingsMask, col)) continue;
//                        if (Occluded(p, rx, buildingsMask, col)) continue;

//                        var bld = col.GetComponent<RFSimulation.Environment.Building>();
//                        float S = bld && bld.material ? Mathf.Clamp01(bld.material.scatterAlbedo) : defaultScatterAlbedo;
//                        float rhoSmooth = bld && bld.material ? Mathf.Clamp01(bld.material.reflectionCoefficient) : 0.3f;
//                        float sigma_h = bld && bld.material ? Mathf.Max(0f, bld.material.roughnessSigmaMeters) : 0f;

//                        float k0 = 2f * Mathf.PI / Mathf.Max(ctx.WavelengthMeters, EPS);
//                        var dir1 = (p - tx).normalized;
//                        float sinPsi = Mathf.Sqrt(1f - Mathf.Pow(IncidenceCos(dir1, wall.Normal), 2f));
//                        float rhoRough = rhoSmooth * Mathf.Exp(-2f * Mathf.Pow(k0 * sigma_h * sinPsi, 2f));

//                        var dir2 = (rx - p).normalized;
//                        float cos_i = Mathf.Clamp01(Vector3.Dot(-dir1, wall.Normal));
//                        float cos_s = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(wall.Normal, dir2)));
//                        float lobe = cos_i * Mathf.Pow(cos_s, Mathf.Max(1, scatterLobeExponent));
//                        if (lobe <= 0f || S <= 0f) continue;

//                        float diffuseMag = S * (1f - rhoRough * rhoRough) * lobe;

//                        float loss = FSPL(ctx.FrequencyMHz, d1) + FSPL(ctx.FrequencyMHz, d2)
//                                   + scatterBaseLossDb - 20f * Mathf.Log10(Mathf.Max(diffuseMag, 1e-6f));

//                        bestLossDb = Mathf.Min(bestLossDb, loss);
//                        paths.Add(new PathContribution
//                        {
//                            LossDb = loss,
//                            DistanceMeters = d1 + d2,
//                            ExtraPhaseRad = 0f
//                        });

//                        if (enableRayVisualization && showScatterRays && Visualizer != null)
//                            Visualizer.DrawPolyline(new[] { tx, p, rx }, scatterRayColor,
//                                $"Diffuse scatter via {col.name} (S≈{S:F2}, ρ_rough≈{rhoRough:F2}, loss {loss:F1} dB)");
//                    }
//                }
//            }
//        }

//        // -----------------------------
//        // Helpers: reflection geometry
//        // -----------------------------
//        private struct WallPlane
//        {
//            public Vector3 Point;   // point on plane
//            public Vector3 Normal;  // outward, normalized
//            public Bounds Bounds;   // AABB of the building
//        }

//        private static IEnumerable<WallPlane> GetWallPlanes(Bounds b)
//        {
//            var c = b.center; var ex = b.extents;
//            yield return new WallPlane { Point = new Vector3(c.x + ex.x, c.y, c.z), Normal = Vector3.right, Bounds = b };
//            yield return new WallPlane { Point = new Vector3(c.x - ex.x, c.y, c.z), Normal = Vector3.left, Bounds = b };
//            yield return new WallPlane { Point = new Vector3(c.x, c.y, c.z + ex.z), Normal = Vector3.forward, Bounds = b };
//            yield return new WallPlane { Point = new Vector3(c.x, c.y, c.z - ex.z), Normal = Vector3.back, Bounds = b };
//        }

//        private static bool TrySpecularPoint(Vector3 tx, Vector3 rx, WallPlane wall, out Vector3 p)
//        {
//            var n = wall.Normal; var q = wall.Point;
//            if (n.sqrMagnitude < NORMAL_EPS) { p = default; return false; }

//            var d = Vector3.Dot(rx - q, n);
//            var rxImage = rx - 2f * d * n;

//            var v = rxImage - tx;
//            var denom = Vector3.Dot(n, v);
//            if (Mathf.Abs(denom) < EPS) { p = default; return false; }

//            var t = Vector3.Dot(n, q - tx) / denom;
//            if (t <= 0f || t >= 1f) { p = default; return false; }

//            p = tx + t * v;
//            return true;
//        }

//        private static bool PointInsideWallRect(Vector3 p, Bounds b, Vector3 wallNormal)
//        {
//            var min = b.min; var max = b.max;

//            bool approx(Vector3 a, Vector3 bb) => (a - bb).sqrMagnitude < 1e-6f;

//            if (approx(wallNormal, Vector3.right) || approx(wallNormal, Vector3.left))
//                return p.y >= min.y - EPS && p.y <= max.y + EPS &&
//                       p.z >= min.z - EPS && p.z <= max.z + EPS;

//            if (approx(wallNormal, Vector3.forward) || approx(wallNormal, Vector3.back))
//                return p.y >= min.y - EPS && p.y <= max.y + EPS &&
//                       p.x >= min.x - EPS && p.x <= max.x + EPS;

//            return false;
//        }

//        // -----------------------------
//        // Edge extraction (roof + vertical corners)
//        // -----------------------------
//        private struct BuildingEdge
//        {
//            public Vector3 start;
//            public Vector3 end;
//            public float height;
//            public Collider building;

//            public Vector3 dir;         // along edge
//            public float wedgeAlphaRad; // exterior wedge angle (rad), default convex ~ 270°
//        }

//        private static List<BuildingEdge> ExtractBuildingEdges(Collider[] colliders)
//        {
//            var edges = new List<BuildingEdge>(64);
//            for (int i = 0; i < colliders.Length; i++)
//            {
//                var r = colliders[i].GetComponent<Renderer>();
//                if (!r) continue;

//                var b = r.bounds; var min = b.min; var max = b.max;

//                // Roof perimeter (y = max.y)
//                var p1 = new Vector3(min.x, max.y, min.z);
//                var p2 = new Vector3(max.x, max.y, min.z);
//                var p3 = new Vector3(max.x, max.y, max.z);
//                var p4 = new Vector3(min.x, max.y, max.z);

//                edges.Add(new BuildingEdge { start = p1, end = p2, height = max.y, building = colliders[i], dir = (p2 - p1).normalized, wedgeAlphaRad = Mathf.Deg2Rad * 270f });
//                edges.Add(new BuildingEdge { start = p2, end = p3, height = max.y, building = colliders[i], dir = (p3 - p2).normalized, wedgeAlphaRad = Mathf.Deg2Rad * 270f });
//                edges.Add(new BuildingEdge { start = p3, end = p4, height = max.y, building = colliders[i], dir = (p4 - p3).normalized, wedgeAlphaRad = Mathf.Deg2Rad * 270f });
//                edges.Add(new BuildingEdge { start = p4, end = p1, height = max.y, building = colliders[i], dir = (p1 - p4).normalized, wedgeAlphaRad = Mathf.Deg2Rad * 270f });

//                // Vertical corner edges
//                var v1b = new Vector3(min.x, min.y, min.z);
//                var v2b = new Vector3(max.x, min.y, min.z);
//                var v3b = new Vector3(max.x, min.y, max.z);
//                var v4b = new Vector3(min.x, min.y, max.z);

//                var v1t = new Vector3(min.x, max.y, min.z);
//                var v2t = new Vector3(max.x, max.y, min.z);
//                var v3t = new Vector3(max.x, max.y, max.z);
//                var v4t = new Vector3(min.x, max.y, max.z);

//                edges.Add(new BuildingEdge { start = v1b, end = v1t, height = max.y, building = colliders[i], dir = (v1t - v1b).normalized, wedgeAlphaRad = Mathf.Deg2Rad * 270f });
//                edges.Add(new BuildingEdge { start = v2b, end = v2t, height = max.y, building = colliders[i], dir = (v2t - v2b).normalized, wedgeAlphaRad = Mathf.Deg2Rad * 270f });
//                edges.Add(new BuildingEdge { start = v3b, end = v3t, height = max.y, building = colliders[i], dir = (v3t - v3b).normalized, wedgeAlphaRad = Mathf.Deg2Rad * 270f });
//                edges.Add(new BuildingEdge { start = v4b, end = v4t, height = max.y, building = colliders[i], dir = (v4t - v4b).normalized, wedgeAlphaRad = Mathf.Deg2Rad * 270f });
//            }
//            return edges;
//        }

//        // -----------------------------
//        // Geometry helper: closest point between two segments (for diffraction point seed)
//        // -----------------------------
//        private static Vector3 ClosestPointBetweenSegments(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
//        {
//            var u = a1 - a0;
//            var v = b1 - b0;
//            var w0 = a0 - b0;

//            float A = Vector3.Dot(u, u);
//            float B = Vector3.Dot(u, v);
//            float C = Vector3.Dot(v, v);
//            float D = Vector3.Dot(u, w0);
//            float E = Vector3.Dot(v, w0);

//            float denom = A * C - B * B;
//            float sc, tc;

//            if (denom < EPS) { sc = 0f; tc = (B > C ? D / B : E / C); }
//            else
//            {
//                sc = (B * E - C * D) / denom;
//                tc = (A * E - B * D) / denom;
//            }

//            sc = Mathf.Clamp01(sc);
//            tc = Mathf.Clamp01(tc);

//            return b0 + tc * v; // point on edge segment
//        }

//        // -----------------------------
//        // UTD wedge helpers
//        // -----------------------------
//        private struct UtdCoeff { public float mag; public float phase; }

//        private static void BuildEdgeFrame(Vector3 edgeDir, out Vector3 s, out Vector3 u, out Vector3 v)
//        {
//            s = edgeDir.normalized;
//            u = Vector3.Cross(Mathf.Abs(s.y) < 0.9f ? Vector3.up : Vector3.right, s);
//            if (u.sqrMagnitude < 1e-8f) u = Vector3.right;
//            u = u.normalized;
//            v = Vector3.Cross(s, u).normalized;
//        }

//        private UtdCoeff ComputeUtdWedgeCoeff(Vector3 tx, Vector3 rx, Vector3 p, Vector3 edgeDir, float wedgeAlphaRad, float wavelength)
//        {
//            float k = 2f * Mathf.PI / Mathf.Max(wavelength, EPS);

//            BuildEdgeFrame(edgeDir, out var s, out var u, out var v);

//            // Incident/observation directions from p
//            var it = (tx - p).normalized;
//            var ir = (rx - p).normalized;

//            // Azimuths around edge (in the transverse plane ⟂ s)
//            Vector2 It = new Vector2(Vector3.Dot(it, u), Vector3.Dot(it, v));
//            Vector2 Ir = new Vector2(Vector3.Dot(ir, u), Vector3.Dot(ir, v));
//            float phi_i = Mathf.Atan2(It.y, It.x);
//            float phi_o = Mathf.Atan2(Ir.y, Ir.x);

//            // Grazing angles (to the edge direction)
//            float cos_ti = Mathf.Abs(Vector3.Dot(it, s));
//            float cos_to = Mathf.Abs(Vector3.Dot(ir, s));
//            float theta_i = Mathf.Acos(Mathf.Clamp01(cos_ti));
//            float theta_o = Mathf.Acos(Mathf.Clamp01(cos_to));
//            float beta = 0.5f * (theta_i + theta_o);

//            float N = (2f * Mathf.PI) / Mathf.Max(wedgeAlphaRad, 1e-3f);
//            float Phi = Mathf.DeltaAngle(phi_i * Mathf.Rad2Deg, phi_o * Mathf.Rad2Deg) * Mathf.Deg2Rad;
//            float denom = Mathf.Max(Mathf.Sin(Mathf.Max(beta, 1e-3f)), 1e-3f);

//            // Core UTD amplitude skeleton (transition functions omitted for speed; guarded near boundaries)
//            float c = 1f / (2f * N * Mathf.Sqrt(2f * Mathf.PI * k) * denom);

//            // Stable proxies for cot terms (away from exact shadow boundaries)
//            float s1 = Mathf.Max(Mathf.Abs(Mathf.Sin((Mathf.PI + Phi) / (2f * N))), 0.15f);
//            float s2 = Mathf.Max(Mathf.Abs(Mathf.Sin((Mathf.PI - Phi) / (2f * N))), 0.15f);
//            float mag = c * 0.5f * (1f / s1 + 1f / s2);

//            // Base UTD phase (perfect conductor wedge); add material phase here if needed
//            float phase = -0.25f * Mathf.PI;

//            // Guard rails
//            mag = Mathf.Clamp(mag, 1e-6f, 1e3f);

//            return new UtdCoeff { mag = mag, phase = phase };
//        }

//        // -----------------------------
//        // Phasor combine
//        // -----------------------------
//        private struct PathContribution
//        {
//            public float LossDb;            // total path loss for this ray (dB)
//            public float DistanceMeters;    // geometric path length
//            public float ExtraPhaseRad;     // extra phase (reflection/UTD); LOS=0
//        }

//        private float CombinePathsPhasor(IList<PathContribution> paths, float txDbm, float wavelength)
//        {
//            double re = 0, im = 0;
//            for (int i = 0; i < paths.Count; i++)
//            {
//                var p = paths[i];
//                double p_lin = Math.Pow(10.0, (txDbm - p.LossDb) / 10.0);
//                double a = Math.Sqrt(p_lin); // field amplitude ~ sqrt(power)
//                double phi = 2.0 * Math.PI * (p.DistanceMeters / (double)wavelength) + p.ExtraPhaseRad;
//                re += a * Math.Cos(phi);
//                im += a * Math.Sin(phi);
//            }
//            double p_total_lin = re * re + im * im;
//            return (float)(10.0 * Math.Log10(Math.Max(p_total_lin, 1e-30)));
//        }

//        // -----------------------------
//        // Math kernels
//        // -----------------------------
//        private static float FresnelParameterV(Vector3 tx, Vector3 rx, Vector3 p, float wavelengthMeters)
//        {
//            float d1 = Vector3.Distance(tx, p);
//            float d2 = Vector3.Distance(p, rx);
//            if (d1 < EPS || d2 < EPS) return 0f;

//            float dTot = d1 + d2;
//            float t = Mathf.Clamp01(d1 / Mathf.Max(dTot, EPS));
//            float yLoS = Mathf.Lerp(tx.y, rx.y, t);
//            float h = p.y - yLoS;

//            float root = Mathf.Sqrt(Mathf.Max(0f, (2f / Mathf.Max(wavelengthMeters, EPS)) * (dTot / (d1 * d2))));
//            return h * root;
//        }

//        private static float KnifeEdgeDiffractionLossDb(float v)
//        {
//            if (v <= -0.78f) return 0f;
//            float term = Mathf.Sqrt((v - 0.1f) * (v - 0.1f) + 1f) + v - 0.1f;
//            return 6.9f + 20f * Mathf.Log10(Mathf.Max(term, EPS));
//        }

//        private static float FSPL(float frequencyMHz, float distanceMeters)
//        {
//            float d_km = Mathf.Max(distanceMeters, EPS) * 0.001f;
//            float f = Mathf.Max(frequencyMHz, EPS);
//            return 32.44f + 20f * Mathf.Log10(d_km) + 20f * Mathf.Log10(f);
//        }

//        // Incidence cosine (TX->wall) vs wall normal
//        private static float IncidenceCos(Vector3 inDir, Vector3 wallNormal)
//        {
//            return Mathf.Clamp01(Mathf.Abs(Vector3.Dot(-inDir.normalized, wallNormal.normalized)));
//        }

//        // Pragmatic reflection loss from |Γ| and angle
//        private static float ReflectionLossDb(float gammaMag, float cosInc)
//        {
//            gammaMag = Mathf.Clamp(gammaMag, 0.0f, 0.999f);
//            float angularBoost = Mathf.Pow(1.0f - 0.6f * cosInc, 1.0f);
//            float eff = Mathf.Clamp(gammaMag * (1.0f - 0.15f * cosInc), 1e-3f, 0.999f);
//            return -20f * Mathf.Log10(eff) + (angularBoost * 2.0f);
//        }

//        private static float ReflectionExtraPhase(float gammaMag)
//        {
//            return Mathf.Lerp(0.5f * Mathf.PI, Mathf.PI, Mathf.Clamp01(gammaMag));
//        }

//        // -----------------------------
//        // Small helpers for scattering samples / occlusion
//        // -----------------------------
//        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-6f;

//        private static Vector3 ClampToWallRect(Vector3 p, Bounds b, Vector3 n)
//        {
//            var min = b.min; var max = b.max; var q = p;

//            if (Approximately(n, Vector3.right)) { q.x = max.x; q.y = Mathf.Clamp(q.y, min.y, max.y); q.z = Mathf.Clamp(q.z, min.z, max.z); }
//            else if (Approximately(n, Vector3.left)) { q.x = min.x; q.y = Mathf.Clamp(q.y, min.y, max.y); q.z = Mathf.Clamp(q.z, min.z, max.z); }
//            else if (Approximately(n, Vector3.forward)) { q.z = max.z; q.y = Mathf.Clamp(q.y, min.y, max.y); q.x = Mathf.Clamp(q.x, min.x, max.x); }
//            else if (Approximately(n, Vector3.back)) { q.z = min.z; q.y = Mathf.Clamp(q.y, min.y, max.y); q.x = Mathf.Clamp(q.x, min.x, max.x); }

//            return q;
//        }

//        private static void BuildWallFrame(Vector3 n, out Vector3 t1, out Vector3 t2)
//        {
//            Vector3 refAxis = (Mathf.Abs(n.y) < 0.9f) ? Vector3.up : Vector3.right;
//            t1 = Vector3.Normalize(Vector3.Cross(refAxis, n));
//            t2 = Vector3.Normalize(Vector3.Cross(n, t1));
//        }

//        private static IEnumerable<Vector3> SampleAroundCenter(Vector3 center, Bounds b, Vector3 n, int count, float radiusFrac)
//        {
//            BuildWallFrame(n, out var t1, out var t2);

//            float Rx = Mathf.Max(0.5f, 0.5f * (b.extents.x + b.extents.z));
//            float Ry = Mathf.Max(0.5f, b.extents.y);
//            float r = Mathf.Clamp(radiusFrac * Mathf.Min(Rx, Ry), 0.1f, 5f);

//            yield return ClampToWallRect(center, b, n);
//            for (int i = 0; i < count - 1; i++)
//            {
//                float u = UnityEngine.Random.Range(-r, r);
//                float v = UnityEngine.Random.Range(-r, r);
//                var p = center + u * t1 + v * t2;
//                yield return ClampToWallRect(p, b, n);
//            }
//        }

//        private bool Occluded(Vector3 a, Vector3 b, LayerMask mask, Collider ignore = null)
//        {
//            var v = b - a; var dist = v.magnitude;
//            if (dist < 1e-6f) return false;
//            var dir = v / dist;

//            const float startEps = 1e-3f, endEps = 1e-3f;
//            var a2 = a + dir * startEps;
//            var maxDist = Mathf.Max(0f, dist - (startEps + endEps));

//            if (Physics.Raycast(a2, dir, out var hit, maxDist, mask))
//            {
//                if (ignore != null && hit.collider == ignore) return false;
//                return true;
//            }
//            return false;
//        }
//    }
//}
