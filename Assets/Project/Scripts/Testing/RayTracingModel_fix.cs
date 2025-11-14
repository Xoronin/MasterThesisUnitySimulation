//// RFSimulation/Propagation/Models/RayTracingModel.cs
//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using RFSimulation.Propagation.Core;
//using RFSimulation.Visualization;
//using RFSimulation.Core;
//using RFSimulation.Interfaces;
//using RFSimulation.Environment;

//namespace RFSimulation.Propagation.PathLoss.Models
//{

//    public class RayTracingModel : IPathLossModel
//    {
//        public string ModelName => "Ray Tracing";

//        [Header("General")]
//        public bool preferAsPrimary = false;
//        public float maxDistance = 1000f;
//        public float blockedLossDb = 200f;
//        public int maxReflections = 2;
//        public int maxDiffractions = 2;

//        [Header("Scattering")]
//        public bool enableDiffuseScattering = true;

//        [Range(0f, 1f)]
//        public float defaultScatterAlbedo = 0.2f;

//        [Range(0f, 8f)]
//        public int scatterLobeExponent = 2;

//        public float scatterBaseLossDb = 20f;

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

//        public RayVisualization Visualizer { get; set; }

//        private PropagationContext context;
//        private float bestLossDb;
//        private List<PathContribution> paths;

//        private readonly List<RayPath> allPaths = new List<RayPath>();
//        private readonly List<RayPath> blockedPaths = new List<RayPath>();

//        private class RayPath
//        {
//            public readonly List<Vector3> points = new List<Vector3>();
//            public float pathLossDb;
//            public float phaseRadians;
//            public float extraPhaseRad;
//            public bool isLOS;
//            public Color color;
//            public string label;
//        }

//        private struct PathContribution
//        {
//            public float LossDb;         // total path loss for this ray (dB)
//            public float DistanceMeters; // total geometric length of the path (m)
//            public float ExtraPhaseRad;  // mechanism-specific phase shift (radians)
//        }

//        private const float MIN_STEP = 0.01f;
//        private const float NORMAL_EPS = 1e-6f;
//        private const float EPS = 1e-6f;

//        public float CalculatePathLoss(PropagationContext ctx)
//        {
//            context = ctx;
//            paths = new List<PathContribution>(8);
//            bestLossDb = float.PositiveInfinity;
//            allPaths.Clear();
//            blockedPaths.Clear();

//            if (enableRayVisualization && Visualizer != null)
//                Visualizer.BeginFrame();

//            try
//            {
//                TraceLOSPath();

//                if (maxReflections > 0)
//                    TraceReflections();

//                if (maxDiffractions > 0)
//                    TraceDiffractions();

//                if (enableDiffuseScattering)
//                    TraceScattering();

//                float result;
//                if (paths.Count > 0)
//                {
//                    result = CombinePaths(paths);
//                }
//                else
//                {
//                    result = float.IsPositiveInfinity(bestLossDb) ? blockedLossDb : bestLossDb;
//                }

//                if (enableRayVisualization && Visualizer != null)
//                    VisualizePaths();

//                return result;
//            }
//            finally
//            {
//                if (enableRayVisualization && Visualizer != null)
//                    Visualizer.EndFrame();
//            }
//        }

//        // --------------------------------------------------------------------
//        // 1) Direct LOS
//        // --------------------------------------------------------------------

//        private void TraceLOSPath()
//        {
//            var tx = context.TransmitterPosition;
//            var rx = context.ReceiverPosition;

//            var dir = rx - tx;
//            var dist = dir.magnitude;
//            if (dist < MIN_STEP || dist > maxDistance)
//                return;

//            dir /= Mathf.Max(dist, EPS);

//            if (Physics.Raycast(tx, dir, out var hit, dist, context.BuildingLayer))
//            {
//                // Blocked direct path
//                var blockedPath = new RayPath
//                {
//                    isLOS = false,
//                    color = blockedRayColor,
//                    label = $"Direct blocked by {hit.collider.name}",
//                    pathLossDb = blockedLossDb,
//                    phaseRadians = 0f,
//                    extraPhaseRad = 0f
//                };
//                blockedPath.points.Add(tx);
//                blockedPath.points.Add(hit.point);
//                blockedPaths.Add(blockedPath);
//                return;
//            }

//            var fsplDb = FSPL(context.FrequencyMHz, dist);
//            bestLossDb = Mathf.Min(bestLossDb, fsplDb);

//            paths.Add(new PathContribution
//            {
//                LossDb = fsplDb,
//                DistanceMeters = dist,
//                ExtraPhaseRad = 0f // LOS has zero extra phase shift
//            });

//            var losPath = new RayPath
//            {
//                isLOS = true,
//                color = directRayColor,
//                label = $"LOS (loss {fsplDb:F1} dB, d={dist:F1} m)",
//                pathLossDb = fsplDb,
//                phaseRadians = 0f,
//                extraPhaseRad = 0f
//            };
//            losPath.points.Add(tx);
//            losPath.points.Add(rx);
//            allPaths.Add(losPath);
//        }

//        // --------------------------------------------------------------------
//        // 2) Single-bounce reflection (image method)
//        // --------------------------------------------------------------------

//        private void TraceReflections()
//        {
//            var tx = context.TransmitterPosition;
//            var rx = context.ReceiverPosition;

//            // AABB around TX-RX to find candidate buildings
//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.6f + 3f,
//                Mathf.Max(5f, Mathf.Abs(tx.y - rx.y)) * 0.6f + 3f,
//                Mathf.Abs(tx.z - rx.z) * 0.6f + 3f
//            );

//            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, context.BuildingLayer);
//            foreach (var col in colliders)
//            {
//                var rend = col.GetComponent<Renderer>();
//                if (!rend) continue;

//                var bounds = rend.bounds;

//                foreach (var wall in GetWallPlanes(bounds))
//                {
//                    if (!TrySpecularPoint(tx, rx, wall, out var p))
//                        continue;

//                    if (!PointInsideWallRect(p, wall.Bounds, wall.Normal))
//                        continue;

//                    var dirTx = (p - tx).normalized;
//                    var d1Test = Vector3.Distance(tx, p);

//                    // TX → wall must hit this collider first
//                    if (!Physics.Raycast(tx, dirTx, out var hitTx,
//                            Mathf.Min(d1Test + 0.5f, maxDistance), context.BuildingLayer))
//                        continue;

//                    if (hitTx.collider != col)
//                        continue;

//                    p = hitTx.point;

//                    // From a tiny offset on the wall towards RX
//                    var pOffset = p + wall.Normal * 0.01f;
//                    var dirRx = (rx - pOffset).normalized;
//                    var d2Test = Vector3.Distance(pOffset, rx);

//                    // Wall → RX must not be blocked
//                    if (Physics.Raycast(pOffset, dirRx, out var hitRx, d2Test, context.BuildingLayer))
//                    {
//                        var blockedPath = new RayPath
//                        {
//                            isLOS = false,
//                            color = blockedRayColor,
//                            label = "Reflection (P->RX blocked)",
//                            pathLossDb = blockedLossDb
//                        };
//                        blockedPath.points.Add(tx);
//                        blockedPath.points.Add(p);
//                        blockedPath.points.Add(hitRx.point);
//                        blockedPaths.Add(blockedPath);
//                        continue;
//                    }

//                    var d1 = Vector3.Distance(tx, p);
//                    var d2 = Vector3.Distance(pOffset, rx);
//                    if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance)
//                        continue;

//                    // Material / reflection coefficient
//                    var bld = col.GetComponent<Building>();

//                    float epsilonR = 5f;      // Default concrete
//                    float sigma = 0.01f;       // Default concrete
//                    float gammaMag = 0.5f;     // Default |Γ|

//                    if (bld != null && bld.material != null)
//                    {
//                        var material = bld.material;
//                        epsilonR = material.relativePermittivity;
//                        sigma = material.conductivity;

//                        float reflCoefficient =
//                            BuildingMaterial.GetReflectionCoefficient(context.FrequencyMHz / 1000f, material);
//                        gammaMag = Mathf.Clamp01(reflCoefficient);
//                    }

//                    var inDir = (p - tx).normalized;
//                    float cosInc = IncidenceCos(inDir, wall.Normal);
//                    float incidenceAngleRad = Mathf.Acos(Mathf.Clamp(cosInc, 0f, 1f));

//                    // CORRECT: Use Fresnel equations for phase
//                    float reflPhase = CalculateReflectionPhase(
//                        epsilonR,
//                        sigma,
//                        context.FrequencyMHz,
//                        incidenceAngleRad
//                    );

//                    float reflLossDb = ReflectionLossDb(gammaMag);

//                    var totalDist = d1 + d2;
//                    var loss = FSPL(context.FrequencyMHz, totalDist) + reflLossDb;

//                    bestLossDb = Mathf.Min(bestLossDb, loss);

//                    paths.Add(new PathContribution
//                    {
//                        LossDb = loss,
//                        DistanceMeters = totalDist,
//                        ExtraPhaseRad = reflPhase // Fresnel-based phase
//                    });

//                    var angleDeg = incidenceAngleRad * Mathf.Rad2Deg;
//                    var reflPath = new RayPath
//                    {
//                        isLOS = false,
//                        color = reflectionRayColor,
//                        pathLossDb = loss,
//                        phaseRadians = 0f,
//                        extraPhaseRad = reflPhase,
//                        label = $"Reflection via {col.name} |Γ|={gammaMag:F2}, θi={angleDeg:F0}°, φ={reflPhase:F2} rad (loss {loss:F1} dB)"
//                    };
//                    reflPath.points.Add(tx);
//                    reflPath.points.Add(p);
//                    reflPath.points.Add(rx);
//                    allPaths.Add(reflPath);

//                    if (maxReflections <= 1)
//                        break;
//                }
//            }
//        }

//        // --------------------------------------------------------------------
//        // 3) Single-edge diffraction (knife-edge)
//        // --------------------------------------------------------------------

//        private void TraceDiffractions()
//        {
//            var tx = context.TransmitterPosition;
//            var rx = context.ReceiverPosition;

//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.7f + 5f,
//                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) * 0.7f + 5f,
//                Mathf.Abs(tx.z - rx.z) * 0.7f + 5f
//            );

//            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, context.BuildingLayer);
//            var edges = ExtractBuildingEdges(colliders, tx, rx);

//            foreach (var e in edges)
//            {
//                // Closest point on edge to TX-RX line
//                var p = ClosestPointOnEdgeToLine(tx, rx, e.start, e.end);

//                // Edge must actually obstruct TX-RX
//                if (!EdgeObstructsPath(tx, rx, p, e.start, e.end))
//                    continue;

//                // Snap p to the roof (cast downward to find the top face)
//                if (!SnapToColliderSurface(
//                        p + Vector3.up * 1.0f,
//                        Vector3.up,
//                        5f,
//                        context.BuildingLayer,
//                        e.building,
//                        out var pRoof,
//                        out var nRoof))
//                    continue;

//                p = pRoof;

//                // Offset slightly to avoid self-hit
//                var pOut = p + nRoof * 0.01f;

//                // TX -> p must not be blocked by other colliders (this building is allowed)
//                var d1 = Vector3.Distance(context.TransmitterPosition, pOut);
//                if (Physics.Raycast(
//                        context.TransmitterPosition,
//                        (pOut - context.TransmitterPosition).normalized,
//                        out var h1,
//                        d1,
//                        context.BuildingLayer) &&
//                    h1.collider != e.building)
//                    continue;

//                // p -> RX must also be clear
//                var d2 = Vector3.Distance(pOut, context.ReceiverPosition);
//                if (Physics.Raycast(
//                        pOut,
//                        (context.ReceiverPosition - pOut).normalized,
//                        out var h2,
//                        d2,
//                        context.BuildingLayer) &&
//                    h2.collider != e.building)
//                    continue;

//                // Extra LoS checks for visualization & debugging
//                if (Physics.Raycast(tx, (p - tx).normalized, out var hit1, d1, context.BuildingLayer))
//                {
//                    var blockedPath = new RayPath
//                    {
//                        isLOS = false,
//                        color = blockedRayColor,
//                        label = "Diffraction (TX->edge blocked)",
//                        pathLossDb = blockedLossDb
//                    };
//                    blockedPath.points.Add(tx);
//                    blockedPath.points.Add(hit1.point);
//                    blockedPath.points.Add(rx);
//                    blockedPaths.Add(blockedPath);
//                    continue;
//                }

//                if (Physics.Raycast(rx, (p - rx).normalized, out var hit2, d2, context.BuildingLayer))
//                {
//                    var blockedPath = new RayPath
//                    {
//                        isLOS = false,
//                        color = blockedRayColor,
//                        label = "Diffraction (edge->RX blocked)",
//                        pathLossDb = blockedLossDb
//                    };
//                    blockedPath.points.Add(tx);
//                    blockedPath.points.Add(p);
//                    blockedPath.points.Add(hit2.point);
//                    blockedPaths.Add(blockedPath);
//                    continue;
//                }

//                // Knife-edge loss with corrected Fresnel parameter
//                var lambda = context.WavelengthMeters;
//                var v = FresnelParameterV(tx, rx, p, lambda);
//                var diffLossDb = KnifeEdgeDiffractionLossDb(v);

//                var totalDist = d1 + d2;
//                var loss = FSPL(context.FrequencyMHz, totalDist) + diffLossDb;

//                bestLossDb = Mathf.Min(bestLossDb, loss);

//                paths.Add(new PathContribution
//                {
//                    LossDb = loss,
//                    DistanceMeters = totalDist,
//                    ExtraPhaseRad = -Mathf.PI * 0.25f // -π/4 for knife-edge near v≈0
//                });

//                var diffPath = new RayPath
//                {
//                    isLOS = false,
//                    color = diffractionRayColor,
//                    pathLossDb = loss,
//                    phaseRadians = 0f,
//                    extraPhaseRad = -Mathf.PI * 0.25f,
//                    label = $"Diffraction (v={v:F2}, loss {loss:F1} dB)"
//                };
//                diffPath.points.Add(tx);
//                diffPath.points.Add(pOut);
//                diffPath.points.Add(rx);
//                allPaths.Add(diffPath);

//                if (maxDiffractions <= 1)
//                    break;
//            }
//        }

//        // --------------------------------------------------------------------
//        // 4) Single-bounce diffuse scattering
//        // --------------------------------------------------------------------

//        private void TraceScattering()
//        {
//            if (!enableDiffuseScattering)
//                return;

//            var tx = context.TransmitterPosition;
//            var rx = context.ReceiverPosition;

//            var mid = 0.5f * (tx + rx);
//            var halfExtents = new Vector3(
//                Mathf.Abs(tx.x - rx.x) * 0.7f + 5f,
//                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) * 0.7f + 5f,
//                Mathf.Abs(tx.z - rx.z) * 0.7f + 5f
//            );

//            var colliders = Physics.OverlapBox(mid, halfExtents, Quaternion.identity, context.BuildingLayer);

//            foreach (var col in colliders)
//            {
//                var rend = col.GetComponent<Renderer>();
//                if (!rend) continue;

//                var bounds = rend.bounds;

//                foreach (var wall in GetWallPlanes(bounds))
//                {
//                    // Pick a center on the wall near specular region, if any
//                    Vector3 center;
//                    if (TrySpecularPoint(tx, rx, wall, out var pSpec))
//                        center = ClampToWallRect(pSpec, wall.Bounds, wall.Normal);
//                    else
//                        center = ClampToWallRect(wall.Point, wall.Bounds, wall.Normal);

//                    // A few samples around that center
//                    foreach (var p0 in SampleAroundCenter(center, wall.Bounds, wall.Normal, 3, 0.2f))
//                    {
//                        if (!SnapToColliderSurface(
//                                p0 + wall.Normal * 0.5f,
//                                wall.Normal,
//                                2.0f,
//                                context.BuildingLayer,
//                                col,
//                                out var pHit,
//                                out var nHit))
//                            continue;

//                        var p = pHit;
//                        var n = nHit;

//                        var d1 = Vector3.Distance(tx, p);
//                        var d2 = Vector3.Distance(p, rx);
//                        if (d1 < MIN_STEP || d2 < MIN_STEP || d1 + d2 > maxDistance)
//                            continue;

//                        // LoS checks – allow this collider at p, but nothing else
//                        if (Occluded(tx, p, context.BuildingLayer, col)) continue;
//                        if (Occluded(p, rx, context.BuildingLayer, col)) continue;

//                        // Extra blocked-path visualization
//                        if (Physics.Raycast(tx, (p - tx).normalized, out var hit1, d1, context.BuildingLayer))
//                        {
//                            var blockedPath = new RayPath
//                            {
//                                isLOS = false,
//                                color = blockedRayColor,
//                                label = "Scattering (TX->edge blocked)",
//                                pathLossDb = blockedLossDb
//                            };
//                            blockedPath.points.Add(tx);
//                            blockedPath.points.Add(hit1.point);
//                            blockedPath.points.Add(rx);
//                            blockedPaths.Add(blockedPath);
//                            continue;
//                        }

//                        if (Physics.Raycast(rx, (p - rx).normalized, out var hit2, d2, context.BuildingLayer))
//                        {
//                            var blockedPath = new RayPath
//                            {
//                                isLOS = false,
//                                color = blockedRayColor,
//                                label = "Scattering (edge->RX blocked)",
//                                pathLossDb = blockedLossDb
//                            };
//                            blockedPath.points.Add(tx);
//                            blockedPath.points.Add(p);
//                            blockedPath.points.Add(hit2.point);
//                            blockedPaths.Add(blockedPath);
//                            continue;
//                        }

//                        // Material parameters
//                        var bld = col.GetComponent<Building>();

//                        float S = (bld != null && bld.material != null)
//                            ? Mathf.Clamp01(bld.material.scatterAlbedo)
//                            : defaultScatterAlbedo;

//                        var material = (bld != null) ? bld.material : null;

//                        float reflCoefficient = (material != null)
//                            ? BuildingMaterial.GetReflectionCoefficient(context.FrequencyMHz / 1000f, material)
//                            : 0.3f;

//                        float rhoSmooth = (bld != null && bld.material != null)
//                            ? Mathf.Clamp01(reflCoefficient)
//                            : 0.3f;

//                        float sigma_h = (bld != null && bld.material != null)
//                            ? Mathf.Max(0f, bld.material.roughnessSigmaMeters)
//                            : 0f;

//                        var dir1 = (p - tx).normalized;
//                        var dir2 = (rx - p).normalized;

//                        float cos_i = Mathf.Clamp01(Vector3.Dot(-dir1, n));
//                        float cos_s = Mathf.Clamp01(Vector3.Dot(dir2, n));

//                        float rho_spec = ComputeRhoSpec(sigma_h, context.WavelengthMeters, cos_i);
//                        float rho_nonSpec = 1f - rho_spec;

//                        float illuminatedArea =
//                            Mathf.Max(1f, wall.Bounds.size.x * wall.Bounds.size.y);

//                        float scatterLossDb = ScatteringLossDb(
//                            d1: d1,
//                            d2: d2,
//                            wavelength: context.WavelengthMeters,
//                            rhoNonSpec: rho_nonSpec,
//                            illuminatedArea: illuminatedArea,
//                            cosInc: cos_i,
//                            cosScat: cos_s,
//                            lobeExponent: scatterLobeExponent 
//                        );

//                        bestLossDb = Mathf.Min(bestLossDb, scatterLossDb);

//                        var scatterPath = new RayPath
//                        {
//                            isLOS = false,
//                            color = scatterRayColor,
//                            pathLossDb = scatterLossDb,
//                            phaseRadians = 0f,
//                            extraPhaseRad = 0f,
//                            label = $"Diffuse via {col.name} (loss {scatterLossDb:F1} dB, incoherent)"
//                        };
//                        scatterPath.points.Add(tx);
//                        scatterPath.points.Add(p + n * 0.01f);
//                        scatterPath.points.Add(rx);
//                        allPaths.Add(scatterPath);

//                    }
//                }
//            }
//        }

//        // --------------------------------------------------------------------
//        // Visibility helpers
//        // --------------------------------------------------------------------

//        private bool Occluded(Vector3 a, Vector3 b, LayerMask mask, Collider ignore = null)
//        {
//            var v = b - a;
//            var dist = v.magnitude;
//            if (dist < 1e-6f)
//                return false;

//            var dir = v / dist;

//            const float startEps = 1e-3f;
//            const float endEps = 1e-3f;

//            var a2 = a + dir * startEps;
//            var maxDist = Mathf.Max(0f, dist - (startEps + endEps));

//            if (Physics.Raycast(a2, dir, out var hit, maxDist, mask))
//            {
//                if (ignore != null && hit.collider == ignore)
//                    return false;
//                return true;
//            }

//            return false;
//        }

//        private bool IsSegmentBlocked(Vector3 a, Vector3 b)
//        {
//            var v = b - a;
//            var dist = v.magnitude;
//            if (dist < EPS)
//                return false;

//            var dir = v / dist;
//            const float margin = 1e-3f;
//            float maxDist = Mathf.Max(0f, dist - margin);

//            return Physics.Raycast(a, dir, maxDist, context.BuildingLayer);
//        }

//        // --------------------------------------------------------------------
//        // Reflection geometry helpers
//        // --------------------------------------------------------------------

//        private struct WallPlane
//        {
//            public Vector3 Point;
//            public Vector3 Normal;
//            public Bounds Bounds;
//        }

//        private static IEnumerable<WallPlane> GetWallPlanes(Bounds b)
//        {
//            var c = b.center;
//            var ex = b.extents;

//            yield return new WallPlane { Point = new Vector3(c.x + ex.x, c.y, c.z), Normal = Vector3.right, Bounds = b };
//            yield return new WallPlane { Point = new Vector3(c.x - ex.x, c.y, c.z), Normal = Vector3.left, Bounds = b };
//            yield return new WallPlane { Point = new Vector3(c.x, c.y, c.z + ex.z), Normal = Vector3.forward, Bounds = b };
//            yield return new WallPlane { Point = new Vector3(c.x, c.y, c.z - ex.z), Normal = Vector3.back, Bounds = b };
//        }

//        private static bool TrySpecularPoint(Vector3 tx, Vector3 rx, WallPlane wall, out Vector3 p)
//        {
//            var n = wall.Normal;
//            var q = wall.Point;

//            if (n.sqrMagnitude < NORMAL_EPS)
//            {
//                p = default;
//                return false;
//            }

//            var d = Vector3.Dot(rx - q, n);
//            var rxImage = rx - 2f * d * n;

//            var v = rxImage - tx;
//            var denom = Vector3.Dot(n, v);
//            if (Mathf.Abs(denom) < EPS)
//            {
//                p = default;
//                return false;
//            }

//            var t = Vector3.Dot(n, q - tx) / denom;
//            if (t <= 0f || t >= 1f)
//            {
//                p = default;
//                return false;
//            }

//            p = tx + t * v;
//            return true;
//        }

//        private static bool PointInsideWallRect(Vector3 p, Bounds b, Vector3 wallNormal)
//        {
//            var min = b.min;
//            var max = b.max;

//            bool approx(Vector3 a, Vector3 bb) => (a - bb).sqrMagnitude < 1e-6f;

//            if (approx(wallNormal, Vector3.right) || approx(wallNormal, Vector3.left))
//                return p.y >= min.y - EPS && p.y <= max.y + EPS &&
//                       p.z >= min.z - EPS && p.z <= max.z + EPS;

//            if (approx(wallNormal, Vector3.forward) || approx(wallNormal, Vector3.back))
//                return p.y >= min.y - EPS && p.y <= max.y + EPS &&
//                       p.x >= min.x - EPS && p.x <= max.x + EPS;

//            return false;
//        }

//        private static bool Approximately(Vector3 a, Vector3 b) =>
//            (a - b).sqrMagnitude < 1e-6f;

//        private static Vector3 ClampToWallRect(Vector3 p, Bounds b, Vector3 n)
//        {
//            var min = b.min;
//            var max = b.max;
//            var q = p;

//            if (Approximately(n, Vector3.right))
//            {
//                q.x = max.x;
//                q.y = Mathf.Clamp(q.y, min.y, max.y);
//                q.z = Mathf.Clamp(q.z, min.z, max.z);
//            }
//            else if (Approximately(n, Vector3.left))
//            {
//                q.x = min.x;
//                q.y = Mathf.Clamp(q.y, min.y, max.y);
//                q.z = Mathf.Clamp(q.z, min.z, max.z);
//            }
//            else if (Approximately(n, Vector3.forward))
//            {
//                q.z = max.z;
//                q.y = Mathf.Clamp(q.y, min.y, max.y);
//                q.x = Mathf.Clamp(q.x, min.x, max.x);
//            }
//            else if (Approximately(n, Vector3.back))
//            {
//                q.z = min.z;
//                q.y = Mathf.Clamp(q.y, min.y, max.y);
//                q.x = Mathf.Clamp(q.x, min.x, max.x);
//            }

//            return q;
//        }

//        private static void BuildWallFrame(Vector3 n, out Vector3 t1, out Vector3 t2)
//        {
//            Vector3 refAxis = (Mathf.Abs(n.y) < 0.9f) ? Vector3.up : Vector3.right;
//            t1 = Vector3.Normalize(Vector3.Cross(refAxis, n));
//            t2 = Vector3.Normalize(Vector3.Cross(n, t1));
//        }

//        private static IEnumerable<Vector3> SampleAroundCenter(
//            Vector3 center,
//            Bounds b,
//            Vector3 n,
//            int count,
//            float radiusFrac)
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

//        // --------------------------------------------------------------------
//        // Edge extraction (roof + vertical corners)
//        // --------------------------------------------------------------------

//        private struct BuildingEdge
//        {
//            public Vector3 start;
//            public Vector3 end;
//            public float height;
//            public Collider building;
//        }

//        private static List<BuildingEdge> ExtractBuildingEdges(Collider[] colliders, Vector3 tx, Vector3 rx)
//        {
//            var edges = new List<BuildingEdge>(64);
//            var txRxDir = (rx - tx).normalized;
//            var txRxDist = Vector3.Distance(tx, rx);
//            var txRxMid = 0.5f * (tx + rx);

//            for (int i = 0; i < colliders.Length; i++)
//            {
//                var r = colliders[i].GetComponent<Renderer>();
//                if (!r) continue;

//                var b = r.bounds;
//                var min = b.min;
//                var max = b.max;

//                var toBuilding = b.center - tx;
//                var projection = Vector3.Dot(toBuilding, txRxDir);
//                if (projection < -b.extents.magnitude || projection > txRxDist + b.extents.magnitude)
//                    continue;

//                var p1 = new Vector3(min.x, max.y, min.z);
//                var p2 = new Vector3(max.x, max.y, min.z);
//                var p3 = new Vector3(max.x, max.y, max.z);
//                var p4 = new Vector3(min.x, max.y, max.z);

//                AddEdgeIfRelevant(edges, p1, p2, max.y, colliders[i], tx, rx, txRxMid);
//                AddEdgeIfRelevant(edges, p2, p3, max.y, colliders[i], tx, rx, txRxMid);
//                AddEdgeIfRelevant(edges, p3, p4, max.y, colliders[i], tx, rx, txRxMid);
//                AddEdgeIfRelevant(edges, p4, p1, max.y, colliders[i], tx, rx, txRxMid);

//                var distToPath = DistancePointToLineSegment(b.center, tx, rx);
//                if (distToPath < 10f)
//                {
//                    var v1b = new Vector3(min.x, min.y, min.z);
//                    var v2b = new Vector3(max.x, min.y, min.z);
//                    var v3b = new Vector3(max.x, min.y, max.z);
//                    var v4b = new Vector3(min.x, min.y, max.z);

//                    var v1t = new Vector3(min.x, max.y, min.z);
//                    var v2t = new Vector3(max.x, max.y, min.z);
//                    var v3t = new Vector3(max.x, max.y, max.z);
//                    var v4t = new Vector3(min.x, max.y, max.z);

//                    AddEdgeIfRelevant(edges, v1b, v1t, max.y, colliders[i], tx, rx, txRxMid);
//                    AddEdgeIfRelevant(edges, v2b, v2t, max.y, colliders[i], tx, rx, txRxMid);
//                    AddEdgeIfRelevant(edges, v3b, v3t, max.y, colliders[i], tx, rx, txRxMid);
//                    AddEdgeIfRelevant(edges, v4b, v4t, max.y, colliders[i], tx, rx, txRxMid);
//                }
//            }

//            return edges;
//        }

//        private static void AddEdgeIfRelevant(
//            List<BuildingEdge> edges,
//            Vector3 start,
//            Vector3 end,
//            float height,
//            Collider building,
//            Vector3 tx,
//            Vector3 rx,
//            Vector3 txRxMid)
//        {
//            var edgeMid = 0.5f * (start + end);

//            var distToMid = Vector3.Distance(edgeMid, txRxMid);
//            var txRxDist = Vector3.Distance(tx, rx);
//            if (distToMid > txRxDist * 0.75f)
//                return;

//            var txRxDir = (rx - tx).normalized;
//            var toEdge = edgeMid - tx;
//            var alongPath = Vector3.Dot(toEdge, txRxDir);
//            if (alongPath < -5f || alongPath > txRxDist + 5f)
//                return;

//            edges.Add(new BuildingEdge
//            {
//                start = start,
//                end = end,
//                height = height,
//                building = building
//            });
//        }

//        private static float DistancePointToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
//        {
//            var lineDir = lineEnd - lineStart;
//            var lineLength = lineDir.magnitude;
//            if (lineLength < 1e-6f)
//                return Vector3.Distance(point, lineStart);

//            lineDir /= lineLength;
//            var t = Mathf.Clamp(Vector3.Dot(point - lineStart, lineDir), 0f, lineLength);
//            var closestPoint = lineStart + t * lineDir;
//            return Vector3.Distance(point, closestPoint);
//        }

//        private static Vector3 ClosestPointOnEdgeToLine(
//            Vector3 tx,
//            Vector3 rx,
//            Vector3 edgeStart,
//            Vector3 edgeEnd)
//        {
//            var lineDir = (rx - tx).normalized;
//            var edgeDir = (edgeEnd - edgeStart).normalized;

//            var toEdgeStart = edgeStart - tx;
//            var projDist = Vector3.Dot(toEdgeStart, lineDir);
//            var closestOnLine = tx + projDist * lineDir;

//            var toClosest = closestOnLine - edgeStart;
//            var edgeLength = Vector3.Distance(edgeStart, edgeEnd);
//            float t = edgeLength > EPS
//                ? Mathf.Clamp(Vector3.Dot(toClosest, edgeDir) / edgeLength, 0f, 1f)
//                : 0f;

//            return Vector3.Lerp(edgeStart, edgeEnd, t);
//        }

//        private static bool EdgeObstructsPath(
//            Vector3 tx,
//            Vector3 rx,
//            Vector3 edgePoint,
//            Vector3 edgeStart,
//            Vector3 edgeEnd)
//        {
//            var lineDir = (rx - tx).normalized;
//            var toEdge = edgePoint - tx;
//            var alongLine = Vector3.Dot(toEdge, lineDir);

//            var lineDist = Vector3.Distance(tx, rx);
//            if (alongLine < 0f || alongLine > lineDist)
//                return false;

//            var closestOnLine = tx + alongLine * lineDir;
//            var perpDist = Vector3.Distance(edgePoint, closestOnLine);

//            var t = alongLine / lineDist;
//            var lineHeightAtEdge = Mathf.Lerp(tx.y, rx.y, t);

//            return edgePoint.y > lineHeightAtEdge && perpDist > 0.1f;
//        }

//        // --------------------------------------------------------------------
//        // Scattering helpers 
//        // --------------------------------------------------------------------

//        private static float ScatteringLossDb(
//            float d1,
//            float d2,
//            float wavelength,
//            float rhoNonSpec,
//            float illuminatedArea,
//            float cosInc,        
//            float cosScat,       
//            int lobeExponent)    
//        {
//            if (d1 <= EPS || d2 <= EPS || wavelength <= EPS)
//                return float.PositiveInfinity;

//            rhoNonSpec = Mathf.Clamp01(rhoNonSpec);
//            if (rhoNonSpec <= 0f)
//                return float.PositiveInfinity;

//            float A = Mathf.Max(illuminatedArea, 0f);
//            if (A <= 0f)
//                return float.PositiveInfinity;

//            cosInc = Mathf.Clamp01(cosInc);
//            cosScat = Mathf.Clamp01(cosScat);

//            double lobe;
//            if (lobeExponent <= 0)
//            {
//                lobe = cosInc;
//            }
//            else
//            {
//                lobe = cosInc * Math.Pow(cosScat, Math.Max(1, lobeExponent));
//            }

//            if (lobe <= 0.0)
//                return float.PositiveInfinity;

//            double lambda = wavelength;
//            double term1 = lambda / (4.0 * Math.PI * d1);
//            double term2 = lambda / (4.0 * Math.PI * d2);

//            double gain = rhoNonSpec * A * lobe;

//            double powerRatio = gain * term1 * term1 * term2 * term2;

//            if (powerRatio <= 0.0)
//                return float.PositiveInfinity;

//            float lossDb = (float)(-10.0 * Math.Log10(powerRatio));
//            return lossDb;
//        }

//        private static float ComputeRhoSpec(float sigmaMeters, float wavelength, float cosInc)
//        {
//            sigmaMeters = Mathf.Max(sigmaMeters, 0f);
//            wavelength = Mathf.Max(wavelength, EPS);
//            cosInc = Mathf.Clamp01(cosInc);

//            double g = 4.0 * Math.PI * sigmaMeters * cosInc / wavelength;
//            double rho_s = Math.Exp(-g * g);

//            rho_s = Math.Max(rho_s, 0.15);

//            double rho_spec = rho_s * rho_s; 
//            return (float)Mathf.Clamp01((float)rho_spec);
//        }

//        // --------------------------------------------------------------------
//        // Path combination 
//        // --------------------------------------------------------------------

//        private float CombinePaths(IList<PathContribution> paths)
//        {
//            if (paths.Count == 0)
//                return float.PositiveInfinity;

//            if (paths.Count == 1)
//                return paths[0].LossDb;

//            double wavelengthMeters = 3e8 / (context.FrequencyMHz * 1e6);

//            double realSum = 0.0;
//            double imagSum = 0.0;

//            foreach (var p in paths)
//            {

//                double pathLossLinear = Math.Pow(10.0, p.LossDb / 10.0);
//                double powerFraction = 1.0 / pathLossLinear;

//                double amplitude = Math.Sqrt(powerFraction);

//                double propagationPhase = 2.0 * Math.PI * p.DistanceMeters / wavelengthMeters;

//                double totalPhase = propagationPhase + p.ExtraPhaseRad;

//                totalPhase = totalPhase % (2.0 * Math.PI);
//                if (totalPhase < 0)
//                    totalPhase += 2.0 * Math.PI;

//                realSum += amplitude * Math.Cos(totalPhase);
//                imagSum += amplitude * Math.Sin(totalPhase);
//            }

//            double totalAmplitude = Math.Sqrt(realSum * realSum + imagSum * imagSum);

//            double totalPowerFraction = totalAmplitude * totalAmplitude;

//            if (totalPowerFraction <= 1e-20) 
//                return float.PositiveInfinity;

//            double combinedLossDb = -10.0 * Math.Log10(totalPowerFraction);

//            return (float)combinedLossDb;
//        }

//        // --------------------------------------------------------------------
//        // Diffraction & FSPL helpers
//        // --------------------------------------------------------------------

//        private static float FresnelParameterV(Vector3 tx, Vector3 rx, Vector3 edgePoint, float wavelengthMeters)
//        {
//            float d1 = Vector3.Distance(tx, edgePoint);
//            float d2 = Vector3.Distance(edgePoint, rx);
//            if (d1 < EPS || d2 < EPS)
//                return 0f;

//            var txToRx = rx - tx;
//            var planeNormal = Vector3.Cross(txToRx.normalized, Vector3.up).normalized;

//            if (planeNormal.sqrMagnitude < 0.01f)
//                planeNormal = Vector3.Cross(txToRx.normalized, Vector3.right).normalized;

//            var toEdge = edgePoint - tx;
//            float h = Vector3.Dot(toEdge, planeNormal);

//            float dTot = d1 + d2;
//            float root = Mathf.Sqrt(Mathf.Max(
//                0f,
//                (2f / Mathf.Max(wavelengthMeters, EPS)) * (dTot / (d1 * d2))
//            ));

//            return h * root;
//        }

//        private static float KnifeEdgeDiffractionLossDb(float v)
//        {
//            if (v <= -0.78f)
//                return 0f;

//            float term = Mathf.Sqrt((v - 0.1f) * (v - 0.1f) + 1f) + v - 0.1f;
//            return 6.9f + 20f * Mathf.Log10(Mathf.Max(term, EPS));
//        }

//        private static float FSPL(float frequencyMHz, float distanceMeters)
//        {
//            float d_km = Mathf.Max(distanceMeters, EPS) * 0.001f;
//            float f = Mathf.Max(frequencyMHz, EPS);
//            return 32.44f + 20f * Mathf.Log10(d_km) + 20f * Mathf.Log10(f);
//        }

//        // --------------------------------------------------------------------
//        // Building / material helpers
//        // --------------------------------------------------------------------

//        private static float IncidenceCos(Vector3 inDir, Vector3 wallNormal)
//        {
//            return Mathf.Clamp01(Mathf.Abs(Vector3.Dot(-inDir.normalized, wallNormal.normalized)));
//        }

//        private static float ReflectionLossDb(float gammaMag)
//        {
//            gammaMag = Mathf.Clamp(gammaMag, 0.01f, 0.99f);
//            return -20f * Mathf.Log10(gammaMag);
//        }

//        private static float CalculateReflectionPhase(
//            float relativePermittivity,
//            float conductivity,
//            float frequencyMHz,
//            float incidenceAngleRad)
//        {
//            float omega = 2f * Mathf.PI * frequencyMHz * 1e6f;
//            float epsilon0 = 8.854e-12f;

//            float epsilonReal = relativePermittivity;
//            float epsilonImag = conductivity / (omega * epsilon0);

//            float cosTheta = Mathf.Cos(incidenceAngleRad);
//            float sinTheta = Mathf.Sin(incidenceAngleRad);

//            float underSqrtReal = epsilonReal - sinTheta * sinTheta;
//            float underSqrtImag = -epsilonImag;

//            float magnitude = Mathf.Sqrt(underSqrtReal * underSqrtReal + underSqrtImag * underSqrtImag);
//            float sqrtMag = Mathf.Pow(magnitude, 0.25f);
//            float argPhase = Mathf.Atan2(underSqrtImag, underSqrtReal);
//            float sqrtPhase = 0.5f * argPhase;

//            float sqrtReal = sqrtMag * Mathf.Cos(sqrtPhase);
//            float sqrtImag = sqrtMag * Mathf.Sin(sqrtPhase);

//            float numReal = cosTheta - sqrtReal;
//            float numImag = -sqrtImag;
//            float denReal = cosTheta + sqrtReal;
//            float denImag = sqrtImag;

//            float denomMagSq = denReal * denReal + denImag * denImag;

//            if (denomMagSq < 1e-10f)
//            {
//                return Mathf.PI;
//            }

//            float gammaReal = (numReal * denReal + numImag * denImag) / denomMagSq;
//            float gammaImag = (numImag * denReal - numReal * denImag) / denomMagSq;

//            float phase = Mathf.Atan2(gammaImag, gammaReal);

//            return phase;
//        }

//        private static bool SnapToColliderSurface(
//            Vector3 approxPoint,
//            Vector3 castDir,
//            float maxDist,
//            LayerMask mask,
//            Collider mustBe,
//            out Vector3 snapped,
//            out Vector3 hitNormal)
//        {
//            var start = approxPoint + castDir.normalized * 0.25f;

//            if (Physics.Raycast(start, -castDir.normalized, out var hit, maxDist + 0.5f, mask))
//            {
//                if (mustBe != null && hit.collider != mustBe)
//                {
//                    snapped = default;
//                    hitNormal = default;
//                    return false;
//                }

//                snapped = hit.point;
//                hitNormal = hit.normal;
//                return true;
//            }

//            snapped = default;
//            hitNormal = default;
//            return false;
//        }

//        // --------------------------------------------------------------------
//        // Visualization
//        // --------------------------------------------------------------------

//        private void VisualizePaths()
//        {
//            if (Visualizer == null)
//                return;

//            foreach (var path in allPaths)
//            {
//                if (path.points.Count < 2)
//                    continue;

//                bool blocked = false;

//                for (int i = 0; i < path.points.Count - 1; i++)
//                {
//                    if (IsSegmentBlocked(path.points[i], path.points[i + 1]))
//                    {
//                        blocked = true;
//                        break;
//                    }
//                }

//                var drawColor = blocked ? blockedRayColor : path.color;
//                var drawLabel = blocked ? $"{path.label} (blocked)" : path.label;

//                for (int i = 0; i < path.points.Count - 1; i++)
//                {
//                    Visualizer.DrawPolyline(
//                        new[] { path.points[i], path.points[i + 1] },
//                        drawColor,
//                        drawLabel
//                    );
//                }
//            }

//            if (showBlockedPaths)
//            {
//                foreach (var path in blockedPaths)
//                {
//                    if (path.points.Count < 2)
//                        continue;

//                    for (int i = 0; i < path.points.Count - 1; i++)
//                    {
//                        Visualizer.DrawPolyline(
//                            new[] { path.points[i], path.points[i + 1] },
//                            blockedRayColor,
//                            path.label
//                        );
//                    }
//                }
//            }
//        }
//    }
//}