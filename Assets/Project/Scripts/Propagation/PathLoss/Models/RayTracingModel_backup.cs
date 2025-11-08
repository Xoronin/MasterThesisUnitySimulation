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
//    ///  - Single-edge diffraction (knife-edge; rooftops + vertical corners)
//    ///
//    /// Visualization is fully delegated to RayVisualization (set via .Visualizer).
//    /// </summary>
//    [Serializable]
//    public class RayTracingModel : IPathLossModel
//    {
//        public string ModelName => "Ray Tracing";

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

//        [Header("Scattering")]
//        public bool enableDiffuseScattering = true;
//        [Range(0f, 1f)] public float defaultScatterAlbedo = 0.2f; // S
//        [Range(0f, 8f)] public int scatterLobeExponent = 2;       // m
//        public float scatterBaseLossDb = 10f;                    // L_diff,0

//        public LayerMask mapboxBuildingLayer = 8;
//        public LayerMask terrainLayer = 6;

//        [Header("Visualization")]
//        public bool enableRayVisualization = true;
//        public bool showDirectRays = true;
//        public bool showReflectionRays = true;
//        public bool showDiffractionRays = true;
//        public bool showScatterRays = true;
//        public bool showBlockedPaths = true;

//        public Color directRayColor = Color.green;
//        public Color reflectionRayColor = Color.blue;
//        public Color diffractionRayColor = Color.yellow;
//        public Color scatterRayColor = Color.cyan;
//        public Color blockedRayColor = Color.red;

//        /// <summary>Set this from your manager/transmitter.</summary>
//        public RayVisualization Visualizer { get; set; }

//        // -----------------------------
//        // Internal constants
//        // -----------------------------
//        const float MIN_STEP = 0.01f;
//        const float NORMAL_EPS = 1e-6f;
//        const float EPS = 1e-6f;

//        public float CalculatePathLoss(PropagationContext context)
//        {
//            var combinedMask = context.BuildingLayers.HasValue
//                ? context.BuildingLayers.Value | terrainLayer
//                : (mapboxBuildingLayer | terrainLayer);

//            var paths = new List<PathContribution>(8);
//            float bestLossDb = float.PositiveInfinity;

//            if (enableRayVisualization && Visualizer != null) Visualizer.BeginFrame();

//            try
//            {
//                TryDirect(context, combinedMask, ref bestLossDb, paths);
//                if (maxReflections > 0) TrySingleBounceReflections(context, combinedMask, ref bestLossDb, paths);
//                if (maxDiffractions > 0) TrySingleEdgeDiffractions(context, combinedMask, ref bestLossDb, paths);
//                if (enableDiffuseScattering) TrySingleBounceScattering(context, combinedMask, ref bestLossDb, paths);

//                if (paths.Count > 0)
//                {
//                    float prxDbm = CombinePathsPhasor(paths, context.TransmitterPowerDbm, context.WavelengthMeters);
//                    bestLossDb = context.TransmitterPowerDbm - prxDbm;
//                    return bestLossDb;
//                }

//                return float.IsPositiveInfinity(bestLossDb) ? blockedLossDb : bestLossDb;
//            }
//            finally
//            {
//                if (enableRayVisualization && Visualizer != null) Visualizer.EndFrame();
//            }
//        }

//        // -----------------------------
//        // 1) Direct
//        // -----------------------------
//        private void TryDirect(PropagationContext ctx, LayerMask combinedMask, ref float bestLossDb, List<PathContribution> paths)
//        {
//            var tx = ctx.TransmitterPosition;
//            var rx = ctx.ReceiverPosition;

//            var dir = rx - tx;
//            var dist = dir.magnitude;
//            if (dist < MIN_STEP || dist > maxDistance) return;

//            dir /= Mathf.Max(dist, EPS);

//            if (Physics.Raycast(tx, dir, out var hit, dist, combinedMask))
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
//        private void TrySingleBounceReflections(PropagationContext ctx, LayerMask combinedMask, ref float bestLossDb, List<PathContribution> paths)
//        {
//            var tx = ctx.TransmitterPosition;
//            var rx = ctx.ReceiverPosition;

//            // AABB around segment TX-RX to find candidates
//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.6f + 3f,
//                Mathf.Max(5f, Mathf.Abs(tx.y - rx.y)) * 0.6f + 3f,
//                Mathf.Abs(tx.z - rx.z) * 0.6f + 3f
//            );

//            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, combinedMask);
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

//                    // LoS checks
//                    if (Physics.Raycast(tx, (p - tx).normalized, out var hit1, d1, combinedMask))
//                    {
//                        if (enableRayVisualization && showReflectionRays && showBlockedPaths && Visualizer != null)
//                            Visualizer.DrawPolyline(new[] { tx, hit1.point, rx }, blockedRayColor, "Reflection (TX->P blocked)");
//                        continue;
//                    }
//                    if (Physics.Raycast(rx, (p - rx).normalized, out var hit2, d2, combinedMask))
//                    {
//                        if (enableRayVisualization && showReflectionRays && showBlockedPaths && Visualizer != null)
//                            Visualizer.DrawPolyline(new[] { tx, p, hit2.point }, blockedRayColor, "Reflection (P->RX blocked)");
//                        continue;
//                    }

//                    // Get building material from the collider (the one whose bounds produced this wall)
//                    var bld = col.GetComponent<RFSimulation.Environment.Building>();

//                    float gamma = 0f;
//                    if (bld && bld.material)
//                        gamma = Mathf.Clamp01(bld.material.reflectionCoefficient);  // 0..1  (from your ScriptableObject)  :contentReference[oaicite:4]{index=4}

//                    // Incidence cosine using TX->P direction vs wall normal
//                    var inDir = (p - tx).normalized;
//                    float cosInc = IncidenceCos(inDir, wall.Normal);

//                    // Compute dB loss + a material-based phase term
//                    float reflLossDb = ReflectionLossDb(gamma, cosInc);
//                    float reflPhase = ReflectionExtraPhase(gamma);

//                    // Total path loss for the reflection
//                    var loss = FSPL(ctx.FrequencyMHz, d1) + FSPL(ctx.FrequencyMHz, d2) + reflLossDb;
//                    bestLossDb = Mathf.Min(bestLossDb, loss);

//                    // Record contribution for phasor combine
//                    paths.Add(new PathContribution
//                    {
//                        LossDb = loss,
//                        DistanceMeters = d1 + d2,
//                        ExtraPhaseRad = reflPhase
//                    });

//                    // (optional) update viz text
//                    if (enableRayVisualization && showReflectionRays && Visualizer != null)
//                        Visualizer.DrawPolyline(new[] { tx, p, rx }, reflectionRayColor,
//                            $"Reflection via {col.name} |Γ|={gamma:F2}, θi≈{Mathf.Acos(cosInc) * Mathf.Rad2Deg:F0}° (loss {loss:F1} dB)");

//                    if (maxReflections <= 1) break;
//                }
//            }
//        }

//        // -----------------------------
//        // 3) Single-edge diffraction (knife-edge)
//        // -----------------------------
//        private void TrySingleEdgeDiffractions(PropagationContext ctx, LayerMask combinedMask, ref float bestLossDb, List<PathContribution> paths)
//        {
//            var tx = ctx.TransmitterPosition;
//            var rx = ctx.ReceiverPosition;

//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.7f + 5f,
//                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) * 0.7f + 5f,
//                Mathf.Abs(tx.z - rx.z) * 0.7f + 5f
//            );

//            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, combinedMask);
//            var edges = ExtractBuildingEdges(colliders); // rooftop + vertical corners

//            foreach (var e in edges)
//            {
//                var p = ClosestPointBetweenSegments(tx, rx, e.start, e.end);

//                var d1 = Vector3.Distance(tx, p);
//                var d2 = Vector3.Distance(p, rx);
//                if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance) continue;

//                // LoS checks
//                if (Physics.Raycast(tx, (p - tx).normalized, out var hit1, d1, combinedMask))
//                {
//                    if (enableRayVisualization && showDiffractionRays && showBlockedPaths && Visualizer != null)
//                        Visualizer.DrawPolyline(new[] { tx, hit1.point, rx }, blockedRayColor, "Diffraction (TX->edge blocked)");
//                    continue;
//                }
//                if (Physics.Raycast(rx, (p - rx).normalized, out var hit2, d2, combinedMask))
//                {
//                    if (enableRayVisualization && showDiffractionRays && showBlockedPaths && Visualizer != null)
//                        Visualizer.DrawPolyline(new[] { tx, p, hit2.point }, blockedRayColor, "Diffraction (edge->RX blocked)");
//                    continue;
//                }

//                // Knife-edge loss
//                var lambda = ctx.WavelengthMeters; // c/f in meters
//                var v = FresnelParameterV(tx, rx, p, lambda);
//                var diffLossDb = KnifeEdgeDiffractionLossDb(v);

//                var loss = FSPL(ctx.FrequencyMHz, d1) + FSPL(ctx.FrequencyMHz, d2) + diffLossDb;
//                bestLossDb = Mathf.Min(bestLossDb, loss);
//                paths.Add(new PathContribution
//                {
//                    LossDb = loss,
//                    DistanceMeters = d1 + d2,
//                    ExtraPhaseRad = -Mathf.PI * 0.25f // knife-edge approx
//                });

//                if (loss < bestLossDb)
//                {
//                    bestLossDb = loss;
//                    if (enableRayVisualization && showDiffractionRays && Visualizer != null)
//                        Visualizer.DrawPolyline(new[] { tx, p, rx }, diffractionRayColor, $"Diffraction (v={v:F2}, loss {loss:F1} dB)");
//                }

//                if (maxDiffractions <= 1) break;
//            }
//        }

//        // -----------------------------
//        // 4. Scattering
//        // -----------------------------

//        private void TrySingleBounceScattering(PropagationContext ctx, LayerMask combinedMask, ref float bestLossDb, List<PathContribution> paths)
//        {
//            if (!enableDiffuseScattering) return;

//            var tx = ctx.TransmitterPosition;
//            var rx = ctx.ReceiverPosition;

//            // candidate buildings around TX–RX
//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.7f + 5f,
//                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) * 0.7f + 5f,
//                Mathf.Abs(tx.z - rx.z) * 0.7f + 5f
//            );

//            var cols = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, combinedMask);

//            foreach (var col in cols)
//            {
//                var rend = col.GetComponent<Renderer>(); if (!rend) continue;
//                var b = rend.bounds;

//                foreach (var wall in GetWallPlanes(b))
//                {
//                    // center samples near the specular point if possible
//                    Vector3 center;
//                    if (TrySpecularPoint(tx, rx, wall, out var pSpec))
//                        center = ClampToWallRect(pSpec, wall.Bounds, wall.Normal);
//                    else
//                        center = ClampToWallRect(wall.Point, wall.Bounds, wall.Normal);

//                    foreach (var p0 in SampleAroundCenter(center, wall.Bounds, wall.Normal, 3, 0.2f))
//                    {
//                        var p = p0 + wall.Normal * 0.01f; // tiny outward offset from face

//                        var d1 = Vector3.Distance(tx, p);
//                        var d2 = Vector3.Distance(p, rx);
//                        if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance) continue;

//                        if (Occluded(tx, p, combinedMask, col)) continue;
//                        if (Occluded(p, rx, combinedMask, col)) continue;

//                        // material params
//                        var bld = col.GetComponent<RFSimulation.Environment.Building>();
//                        float S = bld && bld.material ? Mathf.Clamp01(bld.material.scatterAlbedo) : defaultScatterAlbedo;
//                        float rhoSmooth = bld && bld.material ? Mathf.Clamp01(bld.material.reflectionCoefficient) : 0.3f;
//                        float sigma_h = bld && bld.material ? Mathf.Max(0f, bld.material.roughnessSigmaMeters) : 0f;

//                        // roughness factor (Rayleigh)
//                        float k0 = 2f * Mathf.PI / Mathf.Max(ctx.WavelengthMeters, EPS);
//                        var dir1 = (p - tx).normalized;
//                        float sinPsi = Mathf.Sqrt(1f - Mathf.Pow(IncidenceCos(dir1, wall.Normal), 2f));
//                        float rhoRough = rhoSmooth * Mathf.Exp(-2f * Mathf.Pow(k0 * sigma_h * sinPsi, 2f));

//                        // cosine lobe (two-sided on RX)
//                        var dir2 = (rx - p).normalized;
//                        float cos_i = Mathf.Clamp01(Vector3.Dot(-dir1, wall.Normal));
//                        float cos_s = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(wall.Normal, dir2)));
//                        float lobe = cos_i * Mathf.Pow(cos_s, Mathf.Max(1, scatterLobeExponent));
//                        if (lobe <= 0f || S <= 0f) continue;

//                        // allocate small fraction to diffuse
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


//        // Simple sampler (center + jitter)
//        private IEnumerable<Vector3> SampleWallPoints(Bounds b, Vector3 n, int count)
//        {
//            var c = b.center; var ex = b.extents;
//            // pick the two in-plane axes
//            Vector3 u, v;
//            if (Vector3.Dot(n, Vector3.right) > 0.9f || Vector3.Dot(n, Vector3.left) > 0.9f) { u = Vector3.up; v = Vector3.forward; }
//            else { u = Vector3.up; v = Vector3.right; }

//            yield return c;
//            for (int i = 0; i < count - 1; i++)
//            {
//                float ru = UnityEngine.Random.Range(-0.6f, 0.6f);
//                float rv = UnityEngine.Random.Range(-0.6f, 0.6f);
//                var p = c + ru * ex.y * u + rv * ((Vector3.Dot(n, Vector3.forward) > 0.5f || Vector3.Dot(n, Vector3.back) > 0.5f) ? ex.x : ex.z) * v;
//                yield return p;
//            }
//        }

//        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-6f;

//        private static Vector3 ClampToWallRect(Vector3 p, Bounds b, Vector3 n)
//        {
//            var min = b.min; var max = b.max;
//            var q = p;

//            // Axis-aligned faces from your GetWallPlanes()
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

//            // exact center first
//            yield return ClampToWallRect(center, b, n);

//            // a couple of jittered samples near center
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
//                if (ignore != null && hit.collider == ignore) return false; // allow the target wall
//                return true;
//            }
//            return false;
//        }


//        // -----------------------------
//        // Reflection geometry helpers
//        // -----------------------------
//        private struct WallPlane
//        {
//            public Vector3 Point;   // a point on the plane (on face)
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

//            // Mirror RX across plane
//            var d = Vector3.Dot(rx - q, n);
//            var rxImage = rx - 2f * d * n;

//            // Intersect TX -> RX' with plane
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

//                edges.Add(new BuildingEdge { start = p1, end = p2, height = max.y, building = colliders[i] });
//                edges.Add(new BuildingEdge { start = p2, end = p3, height = max.y, building = colliders[i] });
//                edges.Add(new BuildingEdge { start = p3, end = p4, height = max.y, building = colliders[i] });
//                edges.Add(new BuildingEdge { start = p4, end = p1, height = max.y, building = colliders[i] });

//                // Vertical corner edges
//                var v1b = new Vector3(min.x, min.y, min.z);
//                var v2b = new Vector3(max.x, min.y, min.z);
//                var v3b = new Vector3(max.x, min.y, max.z);
//                var v4b = new Vector3(min.x, min.y, max.z);

//                var v1t = new Vector3(min.x, max.y, min.z);
//                var v2t = new Vector3(max.x, max.y, min.z);
//                var v3t = new Vector3(max.x, max.y, max.z);
//                var v4t = new Vector3(min.x, max.y, max.z);

//                edges.Add(new BuildingEdge { start = v1b, end = v1t, height = max.y, building = colliders[i] });
//                edges.Add(new BuildingEdge { start = v2b, end = v2t, height = max.y, building = colliders[i] });
//                edges.Add(new BuildingEdge { start = v3b, end = v3t, height = max.y, building = colliders[i] });
//                edges.Add(new BuildingEdge { start = v4b, end = v4t, height = max.y, building = colliders[i] });
//            }
//            return edges;
//        }

//        // -----------------------------
//        // Geometry helper: closest point between two segments
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

//            // point on edge segment
//            return b0 + tc * v;
//        }

//        private struct PathContribution
//        {
//            public float LossDb;            // total path loss for this ray (dB)
//            public float DistanceMeters;    // total geometric length of the path (m)
//            public float ExtraPhaseRad;     // 0=LOS, π=reflection, ~-π/4=diffraction
//        }

//        private float CombinePathsPhasor(IList<PathContribution> paths, float txDbm, float wavelength)
//        {
//            double re = 0, im = 0;
//            for (int i = 0; i < paths.Count; i++)
//            {
//                var p = paths[i];

//                // power of this path in linear scale
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
//        // Diffraction + FSPL
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

//        // -----------------------------
//        // Building helpers
//        // -----------------------------

//        // Returns unit incidence direction (TX->wall) and cos(theta_i) where theta_i is angle to normal
//        private static float IncidenceCos(Vector3 inDir, Vector3 wallNormal)
//        {
//            // inDir must point *toward* the wall point (TX -> P)
//            return Mathf.Clamp01(Mathf.Abs(Vector3.Dot(-inDir.normalized, wallNormal.normalized)));
//        }

//        // Simple, robust dB loss from |Γ| and incidence; |Γ| comes from BuildingMaterial.reflectionCoefficient
//        private static float ReflectionLossDb(float gammaMag, float cosInc)
//        {
//            // Give more loss near normal incidence (less specular energy returned along path),
//            // and less loss at grazing (cos small ⇒ more like mirror in canyon).
//            // Tune exponent/k to taste; this is a pragmatic, stable model.
//            gammaMag = Mathf.Clamp(gammaMag, 0.0f, 0.999f);
//            float angularBoost = Mathf.Pow(1.0f - 0.6f * cosInc, 1.0f); // 0..1 (more loss at normal)
//            float eff = Mathf.Clamp(gammaMag * (1.0f - 0.15f * cosInc), 1e-3f, 0.999f);
//            return -20f * Mathf.Log10(eff) + (angularBoost * 2.0f); // base from |Γ| plus small angular term
//        }

//        // Optional crude phase tweak: π for metal-ish, ~π/2 for high-loss glass/wood
//        private static float ReflectionExtraPhase(float gammaMag)
//        {
//            // Metal → near π; lossy dielectrics → between π/2..π
//            return Mathf.Lerp(0.5f * Mathf.PI, Mathf.PI, Mathf.Clamp01(gammaMag));
//        }

//    }
//}