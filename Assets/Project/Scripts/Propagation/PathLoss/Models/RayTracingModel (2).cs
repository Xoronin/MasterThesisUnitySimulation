// RFSimulation/Propagation/Models/RayTracingModel.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Propagation.Core;
using RFSimulation.Visualization;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Environment;

namespace RFSimulation.Propagation.PathLoss.Models
{
    /// <summary>
    /// Urban ray tracing with:
    ///  - LOS (direct)
    ///  - Single-bounce specular reflection (wall plane/image method)
    ///  - Single-edge diffraction (knife-edge; rooftops + vertical corners)
    ///
    /// Visualization is fully delegated to RayVisualization (set via .Visualizer).
    /// </summary>
    [Serializable]
    public class RayTracingModel : IPathLossModel
    {
        public string ModelName => "Ray Tracing";

        // -----------------------------
        // Public configuration
        // -----------------------------
        [Header("General")]
        public bool preferAsPrimary = false;
        public float maxDistance = 1000f;
        [Tooltip("Returned if no path is found.")]
        public float blockedLossDb = 200f;

        [Tooltip("Maximum number of reflections (currently 1 supported).")]
        public int maxReflections = 2;

        [Tooltip("Maximum number of diffractions (currently 1 supported).")]
        public int maxDiffractions = 2;

        [Header("Scattering")]
        public bool enableDiffuseScattering = true;
        [Range(0f, 1f)] public float defaultScatterAlbedo = 0.2f; // S
        [Range(0f, 8f)] public int scatterLobeExponent = 2;       // m
        public float scatterBaseLossDb = 20f;                    // L_diff,0

        public LayerMask mapboxBuildingLayer = 8;
        public LayerMask terrainLayer = 6;

        [Header("Visualization")]
        public bool enableRayVisualization = true;
        public bool showDirectRays = true;
        public bool showReflectionRays = true;
        public bool showDiffractionRays = true;
        public bool showScatterRays = true;
        public bool showBlockedPaths = true;

        public Color directRayColor = Color.green;
        public Color reflectionRayColor = Color.blue;
        public Color diffractionRayColor = Color.yellow;
        public Color scatterRayColor = Color.cyan;
        public Color blockedRayColor = Color.red;

        /// <summary>Set this from your manager/transmitter.</summary>
        public RayVisualization Visualizer { get; set; }

        // -----------------------------
        // Internal constants
        // -----------------------------
        const float MIN_STEP = 0.01f;
        const float NORMAL_EPS = 1e-6f;
        const float EPS = 1e-6f;

        public float CalculatePathLoss(PropagationContext context)
        {
            var combinedMask = context.BuildingLayers.HasValue
                ? context.BuildingLayers.Value | terrainLayer
                : (mapboxBuildingLayer | terrainLayer);

            var paths = new List<PathContribution>(8);
            float bestLossDb = float.PositiveInfinity;

            if (enableRayVisualization && Visualizer != null) Visualizer.BeginFrame();

            try
            {
                TryDirect(context, combinedMask, ref bestLossDb, paths);
                if (maxReflections > 0) TrySingleBounceReflections(context, combinedMask, ref bestLossDb, paths);
                if (maxDiffractions > 0) TrySingleEdgeDiffractions(context, combinedMask, ref bestLossDb, paths);
                if (enableDiffuseScattering) TrySingleBounceScattering(context, combinedMask, ref bestLossDb, paths);

                if (paths.Count > 0)
                {
                    return CombinePathsToLoss(paths);
                }

                return float.IsPositiveInfinity(bestLossDb) ? blockedLossDb : bestLossDb;
            }
            finally
            {
                if (enableRayVisualization && Visualizer != null) Visualizer.EndFrame();
            }
        }

        // -----------------------------
        // 1) Direct
        // -----------------------------
        private void TryDirect(PropagationContext ctx, LayerMask combinedMask, ref float bestLossDb, List<PathContribution> paths)
        {
            var tx = ctx.TransmitterPosition;
            var rx = ctx.ReceiverPosition;

            var dir = rx - tx;
            var dist = dir.magnitude;
            if (dist < MIN_STEP || dist > maxDistance) return;

            dir /= Mathf.Max(dist, EPS);

            if (Physics.Raycast(tx, dir, out var hit, dist, combinedMask))
            {
                if (enableRayVisualization && showDirectRays && showBlockedPaths && Visualizer != null)
                    Visualizer.DrawSegment(tx, hit.point, blockedRayColor, $"Direct blocked by {hit.collider.name}");
                return;
            }

            var fsplDb = FSPL(ctx.FrequencyMHz, dist);
            bestLossDb = Mathf.Min(bestLossDb, fsplDb);
            paths.Add(new PathContribution
            {
                LossDb = fsplDb,
                DistanceMeters = dist,
                ExtraPhaseRad = 0f
            });

            if (enableRayVisualization && showDirectRays && Visualizer != null)
                Visualizer.DrawSegment(tx, rx, directRayColor, $"LOS (loss {fsplDb:F1} dB)");
        }

        // -----------------------------
        // 2) Single-bounce reflection (image method)
        // -----------------------------
        private void TrySingleBounceReflections(PropagationContext ctx, LayerMask combinedMask, ref float bestLossDb, List<PathContribution> paths)
        {
            var tx = ctx.TransmitterPosition;
            var rx = ctx.ReceiverPosition;

            // AABB around segment TX-RX to find candidates
            var mid = 0.5f * (tx + rx);
            var halfExtents = new Vector3(
                Mathf.Abs(tx.x - rx.x) * 0.6f + 3f,
                Mathf.Max(5f, Mathf.Abs(tx.y - rx.y)) * 0.6f + 3f,
                Mathf.Abs(tx.z - rx.z) * 0.6f + 3f
            );

            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, combinedMask);
            foreach (var col in colliders)
            {
                var rend = col.GetComponent<Renderer>();
                if (!rend) continue;

                var b = rend.bounds;
                foreach (var wall in GetWallPlanes(b))
                {
                    if (!TrySpecularPoint(tx, rx, wall, out var p)) continue;
                    if (!PointInsideWallRect(p, wall.Bounds, wall.Normal)) continue;

                    var dirTx = (p - tx).normalized;
                    if (!Physics.Raycast(tx, dirTx, out var hitTx, Mathf.Min(Vector3.Distance(tx, p) + 0.5f, maxDistance), combinedMask))
                        continue;
                    if (hitTx.collider != col) 
                        continue;

                    p = hitTx.point;

                    var pOffset = p + wall.Normal * 0.01f; 
                    var dirRx = (rx - pOffset).normalized;
                    var d2Test = Vector3.Distance(pOffset, rx);

                    if (Physics.Raycast(pOffset, dirRx, out var hitRx, d2Test, combinedMask))
                    {
                        if (enableRayVisualization && showReflectionRays && showBlockedPaths && Visualizer != null)
                            Visualizer.DrawPolyline(new[] { tx, p, hitRx.point }, blockedRayColor, "Reflection (P->RX blocked)");
                        continue;
                    }


                    var d1 = Vector3.Distance(tx, p);
                    var d2 = Vector3.Distance(pOffset, rx);
                    if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance) continue;

                    // Get building material from the collider (the one whose bounds produced this wall)
                    var bld = col.GetComponent<RFSimulation.Environment.Building>();

                    float gamma = 0f;
                    if (bld && bld.material)
                    {
                        var material = bld.material;
                        float reflCoefficient = BuildingMaterial.GetReflectionCoefficient(ctx.FrequencyMHz / 1000f, material);
                        gamma = Mathf.Clamp01(reflCoefficient);
                    }

                    // Incidence cosine using TX->P direction vs wall normal
                    var inDir = (p - tx).normalized;
                    float cosInc = IncidenceCos(inDir, wall.Normal);

                    // Compute dB loss + a material-based phase term
                    float reflLossDb = ReflectionLossDb(gamma);
                    float reflPhase = ReflectionExtraPhase(gamma);

                    // Total path loss for the reflection
                    var loss = FSPL(ctx.FrequencyMHz, d1 + d2) + reflLossDb;
                    bestLossDb = Mathf.Min(bestLossDb, loss);

                    // Record contribution for phasor combine
                    paths.Add(new PathContribution
                    {
                        LossDb = loss,
                        DistanceMeters = d1 + d2,
                        ExtraPhaseRad = reflPhase
                    });

                    // (optional) update viz text
                    if (enableRayVisualization && showReflectionRays && Visualizer != null)
                        Visualizer.DrawPolyline(new[] { tx, p, rx }, reflectionRayColor,
                            $"Reflection via {col.name} |Γ|={gamma:F2}, θi≈{Mathf.Acos(cosInc) * Mathf.Rad2Deg:F0}° (loss {loss:F1} dB)");

                    if (maxReflections <= 1) break;
                }
            }
        }

        // -----------------------------
        // 3) Single-edge diffraction (knife-edge)
        // -----------------------------
        private void TrySingleEdgeDiffractions(PropagationContext ctx, LayerMask combinedMask, ref float bestLossDb, List<PathContribution> paths)
        {
            var tx = ctx.TransmitterPosition;
            var rx = ctx.ReceiverPosition;

            var mid = 0.5f * (tx + rx);
            var halfExtents = new Vector3(
                Mathf.Abs(tx.x - rx.x) * 0.7f + 5f,
                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) * 0.7f + 5f,
                Mathf.Abs(tx.z - rx.z) * 0.7f + 5f
            );

            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, combinedMask);
            var edges = ExtractBuildingEdges(colliders, tx, rx);

            foreach (var e in edges)
            {
                // Find point on edge closest to TX-RX line (not between segments!)
                var p = ClosestPointOnEdgeToLine(tx, rx, e.start, e.end);

                // Check if edge actually obstructs the direct path
                if (!EdgeObstructsPath(tx, rx, p, e.start, e.end))
                    continue;

                //var d1 = Vector3.Distance(tx, p);
                //var d2 = Vector3.Distance(p, rx);
                //if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance)
                //    continue;

                // Snap p to the roof (cast downward to find the top face)
                if (!SnapToColliderSurface(p + Vector3.up * 1.0f, Vector3.up, 5f, combinedMask, e.building, out var pRoof, out var nRoof))
                    continue;
                p = pRoof;

                // Allow the rays to start/leave just off the edge to avoid self-hit
                var pOut = p + nRoof * 0.01f;

                // TX->p must not be blocked by other colliders (hitting this same building is OK)
                var d1 = Vector3.Distance(ctx.TransmitterPosition, pOut);
                if (Physics.Raycast(ctx.TransmitterPosition, (pOut - ctx.TransmitterPosition).normalized, out var h1, d1, combinedMask)
                    && h1.collider != e.building) continue;

                // p->RX must also be clear (besides the edge itself)
                var d2 = Vector3.Distance(pOut, ctx.ReceiverPosition);
                if (Physics.Raycast(pOut, (ctx.ReceiverPosition - pOut).normalized, out var h2, d2, combinedMask)
                    && h2.collider != e.building) continue;

                // LoS checks (same as before)
                if (Physics.Raycast(tx, (p - tx).normalized, out var hit1, d1, combinedMask))
                {
                    if (enableRayVisualization && showDiffractionRays && showBlockedPaths && Visualizer != null)
                        Visualizer.DrawPolyline(new[] { tx, hit1.point, rx }, blockedRayColor, "Diffraction (TX->edge blocked)");
                    continue;
                }
                if (Physics.Raycast(rx, (p - rx).normalized, out var hit2, d2, combinedMask))
                {
                    if (enableRayVisualization && showDiffractionRays && showBlockedPaths && Visualizer != null)
                        Visualizer.DrawPolyline(new[] { tx, p, hit2.point }, blockedRayColor, "Diffraction (edge->RX blocked)");
                    continue;
                }

                // Knife-edge loss with CORRECTED Fresnel parameter
                var lambda = ctx.WavelengthMeters;
                var v = FresnelParameterV(tx, rx, p, lambda);
                var diffLossDb = KnifeEdgeDiffractionLossDb(v);

                // FIXED: Use total distance, not double FSPL
                var totalDist = d1 + d2;
                var loss = FSPL(ctx.FrequencyMHz, totalDist) + diffLossDb;

                bestLossDb = Mathf.Min(bestLossDb, loss);
                paths.Add(new PathContribution
                {
                    LossDb = loss,
                    DistanceMeters = totalDist,
                    ExtraPhaseRad = -Mathf.PI * 0.25f
                });

                if (enableRayVisualization && showDiffractionRays && Visualizer != null)
                    Visualizer.DrawPolyline(new[] { tx, pOut, rx }, diffractionRayColor,
                        $"Diffraction (v={v:F2}, loss {loss:F1} dB)");

                if (maxDiffractions <= 1) break;
            }
        }

        // -----------------------------
        // 4. Scattering
        // -----------------------------

        private void TrySingleBounceScattering(PropagationContext ctx, LayerMask combinedMask, ref float bestLossDb, List<PathContribution> paths)
        {
            if (!enableDiffuseScattering) return;

            var tx = ctx.TransmitterPosition;
            var rx = ctx.ReceiverPosition;

            var mid = 0.5f * (tx + rx);
            var halfExtents = new Vector3(
                Mathf.Abs(tx.x - rx.x) * 0.7f + 5f,
                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) * 0.7f + 5f,
                Mathf.Abs(tx.z - rx.z) * 0.7f + 5f
            );

            var cols = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, combinedMask);

            foreach (var col in cols)
            {
                var rend = col.GetComponent<Renderer>();
                if (!rend) continue;
                var b = rend.bounds;

                foreach (var wall in GetWallPlanes(b))
                {
                    Vector3 center;
                    if (TrySpecularPoint(tx, rx, wall, out var pSpec))
                        center = ClampToWallRect(pSpec, wall.Bounds, wall.Normal);
                    else
                        center = ClampToWallRect(wall.Point, wall.Bounds, wall.Normal);

                    foreach (var p0 in SampleAroundCenter(center, wall.Bounds, wall.Normal, 3, 0.2f))
                    {
                        if (!SnapToColliderSurface(p0 + wall.Normal * 0.5f, wall.Normal, 2.0f, combinedMask, col, out var pHit, out var nHit))
                            continue;

                        var p = pHit;
                        var n = nHit;

                        var d1 = Vector3.Distance(tx, p);
                        var d2 = Vector3.Distance(p, rx);
                        if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance) continue;

                        // LoS checks — allow this same collider at p, but nothing else
                        if (Occluded(tx, p, combinedMask, col)) continue;
                        if (Occluded(p, rx, combinedMask, col)) continue;


                        // LoS checks (same as before)
                        if (Physics.Raycast(tx, (p - tx).normalized, out var hit1, d1, combinedMask))
                        {
                            if (enableRayVisualization && showBlockedPaths && Visualizer != null)
                                Visualizer.DrawPolyline(new[] { tx, hit1.point, rx }, blockedRayColor, "Scattering (TX->edge blocked)");
                            continue;
                        }
                        if (Physics.Raycast(rx, (p - rx).normalized, out var hit2, d2, combinedMask))
                        {
                            if (enableRayVisualization && showBlockedPaths && Visualizer != null)
                                Visualizer.DrawPolyline(new[] { tx, p, hit2.point }, blockedRayColor, "Scattering (edge->RX blocked)");
                            continue;
                        }

                        // Material params
                        var bld = col.GetComponent<RFSimulation.Environment.Building>();
                        float S = bld && bld.material ? Mathf.Clamp01(bld.material.scatterAlbedo) : defaultScatterAlbedo;
                        var material = bld.material;
                        float reflCoefficient = BuildingMaterial.GetReflectionCoefficient(ctx.FrequencyMHz / 1000f, material);
                        float rhoSmooth = bld && bld.material ? Mathf.Clamp01(reflCoefficient) : 0.3f;
                        float sigma_h = bld && bld.material ? Mathf.Max(0f, bld.material.roughnessSigmaMeters) : 0f;

                        // Use the *measured* surface normal for all angular terms
                        var dir1 = (p - tx).normalized;
                        var dir2 = (rx - p).normalized;

                        float cos_i = Mathf.Clamp01(Vector3.Dot(-dir1, n));
                        float cos_s = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(n, dir2)));

                        // Roughness with the actual incidence
                        float k0 = 2f * Mathf.PI / Mathf.Max(ctx.WavelengthMeters, EPS);
                        float sinPsi = Mathf.Sqrt(1f - Mathf.Pow(cos_i, 2f));

                        // Roughness factor (Rayleigh criterion)
                        //float k0 = 2f * Mathf.PI / Mathf.Max(ctx.WavelengthMeters, EPS);
                        //var dir1 = (p - tx).normalized;
                        //float sinPsi = Mathf.Sqrt(1f - Mathf.Pow(IncidenceCos(dir1, wall.Normal), 2f));
                        float rhoRough = rhoSmooth * Mathf.Exp(-2f * Mathf.Pow(k0 * sigma_h * sinPsi, 2f));

                        // Cosine lobe
                        //var dir2 = (rx - p).normalized;
                        //float cos_i = Mathf.Clamp01(Vector3.Dot(-dir1, wall.Normal));
                        //float cos_s = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(wall.Normal, dir2)));
                        float lobe = cos_i * Mathf.Pow(cos_s, Mathf.Max(1, scatterLobeExponent));
                        if (lobe <= 0f || S <= 0f) continue;

                        float diffuseMag = S * (1f - rhoRough * rhoRough) * lobe;

                        var totalDist = d1 + d2;
                        float fspl = FSPL(ctx.FrequencyMHz, totalDist);

                        // Scattering loss = FSPL + spreading loss + material loss + directional loss
                        float materialLoss = -10f * Mathf.Log10(Mathf.Max(S, 0.01f));           // ~7-14 dB
                        float directionalLoss = -10f * Mathf.Log10(Mathf.Max(diffuseMag, 1e-4f)); // ~20-40 dB

                        float loss = fspl + scatterBaseLossDb + materialLoss + directionalLoss;

                        bestLossDb = Mathf.Min(bestLossDb, loss);
                        paths.Add(new PathContribution
                        {
                            LossDb = loss,
                            DistanceMeters = totalDist, // Store total distance
                            ExtraPhaseRad = 0f // Random phase for diffuse scatter
                        });

                        if (enableRayVisualization && showScatterRays && Visualizer != null)
                            Visualizer.DrawPolyline(new[] { tx, p + n * 0.01f, rx }, scatterRayColor,
                                    $"Diffuse via {col.name} (loss {loss:F1} dB)");
                    }
                }
            }
        }


        // Simple sampler (center + jitter)
        private IEnumerable<Vector3> SampleWallPoints(Bounds b, Vector3 n, int count)
        {
            var c = b.center; var ex = b.extents;
            // pick the two in-plane axes
            Vector3 u, v;
            if (Vector3.Dot(n, Vector3.right) > 0.9f || Vector3.Dot(n, Vector3.left) > 0.9f) { u = Vector3.up; v = Vector3.forward; }
            else { u = Vector3.up; v = Vector3.right; }

            yield return c;
            for (int i = 0; i < count - 1; i++)
            {
                float ru = UnityEngine.Random.Range(-0.6f, 0.6f);
                float rv = UnityEngine.Random.Range(-0.6f, 0.6f);
                var p = c + ru * ex.y * u + rv * ((Vector3.Dot(n, Vector3.forward) > 0.5f || Vector3.Dot(n, Vector3.back) > 0.5f) ? ex.x : ex.z) * v;
                yield return p;
            }
        }

        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-6f;

        private static Vector3 ClampToWallRect(Vector3 p, Bounds b, Vector3 n)
        {
            var min = b.min; var max = b.max;
            var q = p;

            // Axis-aligned faces from your GetWallPlanes()
            if (Approximately(n, Vector3.right)) { q.x = max.x; q.y = Mathf.Clamp(q.y, min.y, max.y); q.z = Mathf.Clamp(q.z, min.z, max.z); }
            else if (Approximately(n, Vector3.left)) { q.x = min.x; q.y = Mathf.Clamp(q.y, min.y, max.y); q.z = Mathf.Clamp(q.z, min.z, max.z); }
            else if (Approximately(n, Vector3.forward)) { q.z = max.z; q.y = Mathf.Clamp(q.y, min.y, max.y); q.x = Mathf.Clamp(q.x, min.x, max.x); }
            else if (Approximately(n, Vector3.back)) { q.z = min.z; q.y = Mathf.Clamp(q.y, min.y, max.y); q.x = Mathf.Clamp(q.x, min.x, max.x); }

            return q;
        }

        private static void BuildWallFrame(Vector3 n, out Vector3 t1, out Vector3 t2)
        {
            Vector3 refAxis = (Mathf.Abs(n.y) < 0.9f) ? Vector3.up : Vector3.right;
            t1 = Vector3.Normalize(Vector3.Cross(refAxis, n));
            t2 = Vector3.Normalize(Vector3.Cross(n, t1));
        }

        private static IEnumerable<Vector3> SampleAroundCenter(Vector3 center, Bounds b, Vector3 n, int count, float radiusFrac)
        {
            BuildWallFrame(n, out var t1, out var t2);

            float Rx = Mathf.Max(0.5f, 0.5f * (b.extents.x + b.extents.z));
            float Ry = Mathf.Max(0.5f, b.extents.y);
            float r = Mathf.Clamp(radiusFrac * Mathf.Min(Rx, Ry), 0.1f, 5f);

            // exact center first
            yield return ClampToWallRect(center, b, n);

            // a couple of jittered samples near center
            for (int i = 0; i < count - 1; i++)
            {
                float u = UnityEngine.Random.Range(-r, r);
                float v = UnityEngine.Random.Range(-r, r);
                var p = center + u * t1 + v * t2;
                yield return ClampToWallRect(p, b, n);
            }
        }

        private bool Occluded(Vector3 a, Vector3 b, LayerMask mask, Collider ignore = null)
        {
            var v = b - a; var dist = v.magnitude;
            if (dist < 1e-6f) return false;
            var dir = v / dist;

            const float startEps = 1e-3f, endEps = 1e-3f;
            var a2 = a + dir * startEps;
            var maxDist = Mathf.Max(0f, dist - (startEps + endEps));

            if (Physics.Raycast(a2, dir, out var hit, maxDist, mask))
            {
                if (ignore != null && hit.collider == ignore) return false; // allow the target wall
                return true;
            }
            return false;
        }


        // -----------------------------
        // Reflection geometry helpers
        // -----------------------------
        private struct WallPlane
        {
            public Vector3 Point;   // a point on the plane (on face)
            public Vector3 Normal;  // outward, normalized
            public Bounds Bounds;   // AABB of the building
        }

        private static IEnumerable<WallPlane> GetWallPlanes(Bounds b)
        {
            var c = b.center; var ex = b.extents;
            yield return new WallPlane { Point = new Vector3(c.x + ex.x, c.y, c.z), Normal = Vector3.right, Bounds = b };
            yield return new WallPlane { Point = new Vector3(c.x - ex.x, c.y, c.z), Normal = Vector3.left, Bounds = b };
            yield return new WallPlane { Point = new Vector3(c.x, c.y, c.z + ex.z), Normal = Vector3.forward, Bounds = b };
            yield return new WallPlane { Point = new Vector3(c.x, c.y, c.z - ex.z), Normal = Vector3.back, Bounds = b };
        }

        private static bool TrySpecularPoint(Vector3 tx, Vector3 rx, WallPlane wall, out Vector3 p)
        {
            var n = wall.Normal; var q = wall.Point;
            if (n.sqrMagnitude < NORMAL_EPS) { p = default; return false; }

            // Mirror RX across plane
            var d = Vector3.Dot(rx - q, n);
            var rxImage = rx - 2f * d * n;

            // Intersect TX -> RX' with plane
            var v = rxImage - tx;
            var denom = Vector3.Dot(n, v);
            if (Mathf.Abs(denom) < EPS) { p = default; return false; }

            var t = Vector3.Dot(n, q - tx) / denom;
            if (t <= 0f || t >= 1f) { p = default; return false; }

            p = tx + t * v;
            return true;
        }

        private static bool PointInsideWallRect(Vector3 p, Bounds b, Vector3 wallNormal)
        {
            var min = b.min; var max = b.max;

            bool approx(Vector3 a, Vector3 bb) => (a - bb).sqrMagnitude < 1e-6f;

            if (approx(wallNormal, Vector3.right) || approx(wallNormal, Vector3.left))
                return p.y >= min.y - EPS && p.y <= max.y + EPS &&
                       p.z >= min.z - EPS && p.z <= max.z + EPS;

            if (approx(wallNormal, Vector3.forward) || approx(wallNormal, Vector3.back))
                return p.y >= min.y - EPS && p.y <= max.y + EPS &&
                       p.x >= min.x - EPS && p.x <= max.x + EPS;

            return false;
        }

        // -----------------------------
        // Edge extraction (roof + vertical corners)
        // -----------------------------
        private struct BuildingEdge
        {
            public Vector3 start;
            public Vector3 end;
            public float height;
            public Collider building;
        }

        private static List<BuildingEdge> ExtractBuildingEdges(Collider[] colliders, Vector3 tx, Vector3 rx)
        {
            var edges = new List<BuildingEdge>(64);
            var txRxDir = (rx - tx).normalized;
            var txRxDist = Vector3.Distance(tx, rx);
            var txRxMid = 0.5f * (tx + rx);

            for (int i = 0; i < colliders.Length; i++)
            {
                var r = colliders[i].GetComponent<Renderer>();
                if (!r) continue;

                var b = r.bounds;
                var min = b.min;
                var max = b.max;

                // Quick rejection: building not between TX and RX
                var toBuilding = b.center - tx;
                var projection = Vector3.Dot(toBuilding, txRxDir);
                if (projection < -b.extents.magnitude || projection > txRxDist + b.extents.magnitude)
                    continue;

                // Only extract rooftop edges (most important for diffraction)
                var p1 = new Vector3(min.x, max.y, min.z);
                var p2 = new Vector3(max.x, max.y, min.z);
                var p3 = new Vector3(max.x, max.y, max.z);
                var p4 = new Vector3(min.x, max.y, max.z);

                // Add rooftop perimeter edges
                AddEdgeIfRelevant(edges, p1, p2, max.y, colliders[i], tx, rx, txRxMid);
                AddEdgeIfRelevant(edges, p2, p3, max.y, colliders[i], tx, rx, txRxMid);
                AddEdgeIfRelevant(edges, p3, p4, max.y, colliders[i], tx, rx, txRxMid);
                AddEdgeIfRelevant(edges, p4, p1, max.y, colliders[i], tx, rx, txRxMid);

                // Optionally add vertical edges only if building is very close to path
                var distToPath = DistancePointToLineSegment(b.center, tx, rx);
                if (distToPath < 10f) // Only add vertical edges for nearby buildings
                {
                    var v1b = new Vector3(min.x, min.y, min.z);
                    var v2b = new Vector3(max.x, min.y, min.z);
                    var v3b = new Vector3(max.x, min.y, max.z);
                    var v4b = new Vector3(min.x, min.y, max.z);

                    var v1t = new Vector3(min.x, max.y, min.z);
                    var v2t = new Vector3(max.x, max.y, min.z);
                    var v3t = new Vector3(max.x, max.y, max.z);
                    var v4t = new Vector3(min.x, max.y, max.z);

                    AddEdgeIfRelevant(edges, v1b, v1t, max.y, colliders[i], tx, rx, txRxMid);
                    AddEdgeIfRelevant(edges, v2b, v2t, max.y, colliders[i], tx, rx, txRxMid);
                    AddEdgeIfRelevant(edges, v3b, v3t, max.y, colliders[i], tx, rx, txRxMid);
                    AddEdgeIfRelevant(edges, v4b, v4t, max.y, colliders[i], tx, rx, txRxMid);
                }
            }
            return edges;
        }

        private static void AddEdgeIfRelevant(List<BuildingEdge> edges, Vector3 start, Vector3 end,
            float height, Collider building, Vector3 tx, Vector3 rx, Vector3 txRxMid)
        {
            var edgeMid = 0.5f * (start + end);

            // Filter 1: Edge should be reasonably close to TX-RX midpoint
            var distToMid = Vector3.Distance(edgeMid, txRxMid);
            var txRxDist = Vector3.Distance(tx, rx);
            if (distToMid > txRxDist * 0.75f)
                return;

            // Filter 2: Edge should be roughly between TX and RX (not way off to side)
            var txRxDir = (rx - tx).normalized;
            var toEdge = edgeMid - tx;
            var alongPath = Vector3.Dot(toEdge, txRxDir);
            if (alongPath < -5f || alongPath > txRxDist + 5f)
                return;

            edges.Add(new BuildingEdge
            {
                start = start,
                end = end,
                height = height,
                building = building
            });
        }

        // Helper: Distance from point to line segment
        private static float DistancePointToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            var lineDir = lineEnd - lineStart;
            var lineLength = lineDir.magnitude;
            if (lineLength < 1e-6f) return Vector3.Distance(point, lineStart);

            lineDir /= lineLength;
            var t = Mathf.Clamp(Vector3.Dot(point - lineStart, lineDir), 0f, lineLength);
            var closestPoint = lineStart + t * lineDir;
            return Vector3.Distance(point, closestPoint);
        }

        // NEW: Find closest point on edge to the TX-RX line
        private static Vector3 ClosestPointOnEdgeToLine(Vector3 tx, Vector3 rx, Vector3 edgeStart, Vector3 edgeEnd)
        {
            var lineDir = (rx - tx).normalized;
            var edgeDir = (edgeEnd - edgeStart).normalized;

            // Project edge start onto TX-RX line
            var toEdgeStart = edgeStart - tx;
            var projDist = Vector3.Dot(toEdgeStart, lineDir);
            var closestOnLine = tx + projDist * lineDir;

            // Now find closest point on edge segment to this point
            var toClosest = closestOnLine - edgeStart;
            var t = Vector3.Dot(toClosest, edgeDir);
            t = Mathf.Clamp(t / Vector3.Distance(edgeStart, edgeEnd), 0f, 1f);

            return Vector3.Lerp(edgeStart, edgeEnd, t);
        }

        // NEW: Check if edge actually obstructs the direct path
        private static bool EdgeObstructsPath(Vector3 tx, Vector3 rx, Vector3 edgePoint, Vector3 edgeStart, Vector3 edgeEnd)
        {
            // Calculate perpendicular distance from edge point to TX-RX line
            var lineDir = (rx - tx).normalized;
            var toEdge = edgePoint - tx;
            var alongLine = Vector3.Dot(toEdge, lineDir);

            // Edge must be between TX and RX (not behind or beyond)
            var lineDist = Vector3.Distance(tx, rx);
            if (alongLine < 0f || alongLine > lineDist)
                return false;

            // Point on line closest to edge
            var closestOnLine = tx + alongLine * lineDir;
            var perpDist = Vector3.Distance(edgePoint, closestOnLine);

            // Edge must be "above" the line (obstructing, not below)
            // Simple heuristic: edge point should be higher than interpolated height
            var t = alongLine / lineDist;
            var lineHeightAtEdge = Mathf.Lerp(tx.y, rx.y, t);

            return edgePoint.y > lineHeightAtEdge && perpDist > 0.1f;
        }

        // -----------------------------
        // Geometry helper: closest point between two segments
        // -----------------------------
        private static Vector3 ClosestPointBetweenSegments(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            var u = a1 - a0;
            var v = b1 - b0;
            var w0 = a0 - b0;

            float A = Vector3.Dot(u, u);
            float B = Vector3.Dot(u, v);
            float C = Vector3.Dot(v, v);
            float D = Vector3.Dot(u, w0);
            float E = Vector3.Dot(v, w0);

            float denom = A * C - B * B;
            float sc, tc;

            if (denom < EPS) { sc = 0f; tc = (B > C ? D / B : E / C); }
            else
            {
                sc = (B * E - C * D) / denom;
                tc = (A * E - B * D) / denom;
            }

            sc = Mathf.Clamp01(sc);
            tc = Mathf.Clamp01(tc);

            // point on edge segment
            return b0 + tc * v;
        }

        private struct PathContribution
        {
            public float LossDb;            // total path loss for this ray (dB)
            public float DistanceMeters;    // total geometric length of the path (m)
            public float ExtraPhaseRad;     // 0=LOS, π=reflection, ~-π/4=diffraction
        }

        /// <summary>
        /// Combines multiple path contributions into a single effective path loss.
        /// Uses power-domain addition with random phase assumption (incoherent combining).
        /// </summary>
        private float CombinePathsToLoss(IList<PathContribution> paths)
        {
            if (paths.Count == 0) return float.PositiveInfinity;

            // For a single path, just return its loss
            if (paths.Count == 1) return paths[0].LossDb;

            // For multiple paths: use incoherent power combining
            // This assumes random phases (realistic for multipath in urban environments)
            // Result is typically 3-6 dB better than worst path, but not as optimistic as coherent sum

            double totalPowerFraction = 0.0;

            for (int i = 0; i < paths.Count; i++)
            {
                var p = paths[i];
                // Convert path loss to power fraction (0 dB loss = 1.0, higher loss = smaller fraction)
                double powerFraction = Math.Pow(10.0, -p.LossDb / 10.0);
                totalPowerFraction += powerFraction;
            }

            // Convert back to dB
            if (totalPowerFraction <= 0) return float.PositiveInfinity;
            return (float)(-10.0 * Math.Log10(totalPowerFraction));
        }


        // -----------------------------
        // Diffraction + FSPL
        // -----------------------------
        private static float FresnelParameterV(Vector3 tx, Vector3 rx, Vector3 edgePoint, float wavelengthMeters)
        {
            float d1 = Vector3.Distance(tx, edgePoint);
            float d2 = Vector3.Distance(edgePoint, rx);
            if (d1 < EPS || d2 < EPS) return 0f;

            // Create a plane containing TX and RX, with normal pointing "up" relative to scene
            var txToRx = rx - tx;
            var horizontalDir = new Vector3(txToRx.x, 0f, txToRx.z).normalized;
            var planeNormal = Vector3.Cross(txToRx.normalized, Vector3.up).normalized;

            // If TX-RX is vertical, use a different reference
            if (planeNormal.sqrMagnitude < 0.01f)
                planeNormal = Vector3.Cross(txToRx.normalized, Vector3.right).normalized;

            // Distance from edge point to the plane containing TX-RX line
            var toEdge = edgePoint - tx;
            float h = Vector3.Dot(toEdge, planeNormal);

            // Fresnel parameter
            float dTot = d1 + d2;
            float root = Mathf.Sqrt(Mathf.Max(0f, (2f / Mathf.Max(wavelengthMeters, EPS)) * (dTot / (d1 * d2))));
            return h * root;
        }

        private static float KnifeEdgeDiffractionLossDb(float v)
        {
            if (v <= -0.78f) return 0f;
            float term = Mathf.Sqrt((v - 0.1f) * (v - 0.1f) + 1f) + v - 0.1f;
            return 6.9f + 20f * Mathf.Log10(Mathf.Max(term, EPS));
        }

        private static float FSPL(float frequencyMHz, float distanceMeters)
        {
            float d_km = Mathf.Max(distanceMeters, EPS) * 0.001f;
            float f = Mathf.Max(frequencyMHz, EPS);
            return 32.44f + 20f * Mathf.Log10(d_km) + 20f * Mathf.Log10(f);
        }

        // -----------------------------
        // Building helpers
        // -----------------------------

        // Returns unit incidence direction (TX->wall) and cos(theta_i) where theta_i is angle to normal
        private static float IncidenceCos(Vector3 inDir, Vector3 wallNormal)
        {
            // inDir must point *toward* the wall point (TX -> P)
            return Mathf.Clamp01(Mathf.Abs(Vector3.Dot(-inDir.normalized, wallNormal.normalized)));
        }

        // Simple, robust dB loss from |Γ| and incidence; |Γ| comes from BuildingMaterial.reflectionCoefficient
        private static float ReflectionLossDb(float gammaMag)
        {
            gammaMag = Mathf.Clamp(gammaMag, 0.01f, 0.99f);
            return -20f * Mathf.Log10(gammaMag);
        }

        // Optional crude phase tweak: π for metal-ish, ~π/2 for high-loss glass/wood
        private static float ReflectionExtraPhase(float gammaMag)
        {
            // Metal → near π; lossy dielectrics → between π/2..π
            return Mathf.Lerp(0.5f * Mathf.PI, Mathf.PI, Mathf.Clamp01(gammaMag));
        }

        private static bool SnapToColliderSurface(Vector3 approxPoint, Vector3 castDir, float maxDist,
                                          LayerMask mask, Collider mustBe, out Vector3 snapped, out Vector3 hitNormal)
        {
            // Start a little off the surface to avoid starting inside
            var start = approxPoint + castDir.normalized * 0.25f;
            if (Physics.Raycast(start, -castDir.normalized, out var hit, maxDist + 0.5f, mask))
            {
                if (mustBe != null && hit.collider != mustBe)
                {
                    snapped = default; hitNormal = default;
                    return false;
                }
                snapped = hit.point;
                hitNormal = hit.normal;
                return true;
            }
            snapped = default; hitNormal = default;
            return false;
        }

    }
}