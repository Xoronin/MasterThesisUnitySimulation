//// RFSimulation/Propagation/Models/RayTracingModel.cs
//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using RFSimulation.Propagation.Core;
//using RFSimulation.Visualization;
//using RFSimulation.Core;
//using RFSimulation.Interfaces;
//using RFSimulation.Environment;
//using RFSimulation.Utils;


//namespace RFSimulation.Propagation.PathLoss.Models
//{
//    public class RayTracingModel : IPathLossModel
//    {
//        public string ModelName => "Ray Tracing";

//        [Header("General")]
//        public float maxDistance = 5000f;
//        public int maxReflections = 2;
//        public int maxDiffractions = 2;

//        [Header("Visualization")]
//        public bool enableRayVisualization = true;
//        public Color directRayColor = Color.green;
//        public Color reflectionRayColor = Color.blue;
//        public Color diffractionRayColor = Color.yellow;
//        public Color blockedRayColor = Color.red;

//        public RayVisualization Visualizer { get; set; }

//        private PropagationContext context;
//        private List<PathContribution> paths;
//        private readonly List<RayPath> visualPaths = new List<RayPath>();

//        private class RayPath
//        {
//            public List<Vector3> points = new List<Vector3>();
//            public Color color;
//            public string label;
//        }

//        private enum PathType
//        {
//            LOS,
//            Reflection,
//            Diffraction
//        }

//        private struct PathContribution
//        {
//            public float LossDb;
//            public float DistanceMeters;
//            public float ExtraPhaseRad;

//            public PathType Type;
//            public float FsplDb;
//            public float MechanismExtraDb;
//        }

//        const float EPS = 1e-6f;

//        public float CalculatePathLoss(PropagationContext ctx)
//        {
//            context = ctx;
//            paths = new List<PathContribution>();
//            visualPaths.Clear();

//            if (enableRayVisualization && Visualizer != null)
//                Visualizer.BeginFrame();

//            try
//            {
//                // trace paths
//                TraceLOS();
//                if (maxReflections > 0) TraceReflections();
//                if (maxDiffractions > 0) TraceDiffractions();

//                DebugDumpPaths();
//                float result = CombinePaths(paths);

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

//        // ====================================================================
//        // LOS Path
//        // ====================================================================

//        private void TraceLOS()
//        {
//            var tx = context.TransmitterPosition;
//            var rx = context.ReceiverPosition;
//            var dir = rx - tx;
//            var dist = dir.magnitude;

//            if (dist < 0.01f || dist > maxDistance) return;

//            // check for blockage
//            if (Physics.Raycast(tx, dir.normalized, dist, context.BuildingLayer))
//            {
//                visualPaths.Add(new RayPath
//                {
//                    points = new List<Vector3> { tx, rx },
//                    color = blockedRayColor,
//                    label = "LOS blocked"
//                });
//                return;
//            }

//            // clear LOS path
//            float fspl = FSPL(context.FrequencyMHz, dist);

//            paths.Add(new PathContribution
//            {
//                Type = PathType.LOS,
//                LossDb = fspl,
//                FsplDb = fspl,
//                MechanismExtraDb = 0f,
//                DistanceMeters = dist,
//                ExtraPhaseRad = 0f
//            });

//            visualPaths.Add(new RayPath
//            {
//                points = new List<Vector3> { tx, rx },
//                color = directRayColor,
//                label = $"LOS ({fspl:F1} dB)"
//            });
//        }

//        // ====================================================================
//        // Reflection (Image Method)
//        // ====================================================================

//        private void TraceReflections()
//        {
//            var tx = context.TransmitterPosition;
//            var rx = context.ReceiverPosition;

//            // find buildings near TX-RX line
//            var mid = 0.5f * (tx + rx);
//            var size = new Vector3(
//                Mathf.Abs(tx.x - rx.x) + 10f,
//                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) + 10f,
//                Mathf.Abs(tx.z - rx.z) + 10f
//            );

//            var buildings = Physics.OverlapBox(mid, size * 0.5f, Quaternion.identity, context.BuildingLayer);

//            foreach (var building in buildings)
//            {
//                var bounds = building.bounds;

//                foreach (var wall in GetWalls(bounds))
//                {
//                    if (!GetReflectionPoint(tx, rx, wall, out var theoreticalPt))
//                        continue;

//                    var toTheoretical = theoreticalPt - tx;
//                    var distToTheoretical = toTheoretical.magnitude;
//                    if (distToTheoretical < 0.01f) continue;

//                    var dirToTheoretical = toTheoretical / distToTheoretical;

//                    if (!Physics.Raycast(tx, dirToTheoretical,
//                                         out var hit,
//                                         distToTheoretical + 0.1f,
//                                         context.BuildingLayer))
//                        continue;

//                    // Make sure we actually hit THIS building
//                    if (hit.collider != building)
//                        continue;

//                    var reflPt = hit.point;

//                    var offsetPt = reflPt + hit.normal * 0.01f;

//                    float d1 = Vector3.Distance(tx, reflPt);
//                    float d2 = Vector3.Distance(reflPt, rx);
//                    if (d1 < 0.01f || d2 < 0.01f) continue;

//                    // Visibility checks: TX -> reflPt, and reflPt -> RX must both be clear
//                    RaycastHit blockHit;

//                    // TX -> reflPt
//                    if (IsSegmentBlocked(tx, reflPt, out blockHit))
//                    {
//                        // Optional red viz
//                        if (enableRayVisualization)
//                        {
//                            visualPaths.Add(new RayPath
//                            {
//                                points = new List<Vector3> { tx, blockHit.point },
//                                color = blockedRayColor,
//                                label = $"Reflection TX->wall blocked by {blockHit.collider.name}"
//                            });
//                        }
//                        continue;
//                    }

//                    // reflPt -> RX (start a bit off the wall along normal to avoid self-hit)
//                    var reflStart = reflPt + hit.normal * 0.05f;
//                    if (IsSegmentBlocked(reflStart, rx, out blockHit))
//                    {
//                        if (enableRayVisualization)
//                        {
//                            visualPaths.Add(new RayPath
//                            {
//                                points = new List<Vector3> { reflStart, blockHit.point },
//                                color = blockedRayColor,
//                                label = $"Reflection wall->RX blocked by {blockHit.collider.name}"
//                            });
//                        }
//                        continue;
//                    }

//                    // get material properties
//                    float gammaMag = 0.6f;
//                    var bldg = building.GetComponent<Building>();
//                    if (bldg?.material != null)
//                    {
//                        gammaMag = Mathf.Clamp01(
//                            BuildingMaterial.GetReflectionCoefficient(
//                                context.FrequencyMHz / 1000f, bldg.material));
//                    }

//                    // calculate path loss
//                    float totalDist = d1 + d2;
//                    float fspl = FSPL(context.FrequencyMHz, totalDist);
//                    float reflLoss = -20f * Mathf.Log10(Mathf.Max(0.1f, gammaMag));
//                    float totalLoss = fspl + reflLoss;

//                    // reflection phase from Fresnel coefficient
//                    // for most materials, phase shift is ~π (180 degrees)
//                    float reflPhase = Mathf.PI;

//                    paths.Add(new PathContribution
//                    {
//                        Type = PathType.Reflection,
//                        LossDb = totalLoss,
//                        FsplDb = fspl,
//                        MechanismExtraDb = reflLoss,
//                        DistanceMeters = totalDist,
//                        ExtraPhaseRad = Mathf.PI
//                    });

//                    visualPaths.Add(new RayPath
//                    {
//                        points = new List<Vector3> { tx, reflPt, rx },
//                        color = reflectionRayColor,
//                        label = $"Reflection ({totalLoss:F1} dB, |Γ|={gammaMag:F2})"
//                    });

//                    if (maxReflections <= 1) break;
//                }
//            }
//        }

//        // ====================================================================
//        // Diffraction (Knife-Edge)
//        // ====================================================================

//        private void TraceDiffractions()
//        {
//            var tx = context.TransmitterPosition;
//            var rx = context.ReceiverPosition;

//            // find buildings that might obstruct
//            var mid = 0.5f * (tx + rx);
//            var size = new Vector3(
//                Mathf.Abs(tx.x - rx.x) + 20f,
//                Mathf.Max(20f, Mathf.Abs(tx.y - rx.y)) + 20f,
//                Mathf.Abs(tx.z - rx.z) + 20f
//            );

//            var buildings = Physics.OverlapBox(mid, size * 0.5f, Quaternion.identity, context.BuildingLayer);
//            var edges = ExtractRooftopEdges(buildings, tx, rx);

//            foreach (var edge in edges)
//            {
//                // find diffraction point on edge
//                var theoreticalPt = ClosestPointOnEdge(tx, rx, edge.p1, edge.p2);

//                var abovePoint = theoreticalPt + Vector3.up * 1f;
//                if (!Physics.Raycast(abovePoint, Vector3.down, out var hit, 5f, context.BuildingLayer))
//                    continue;

//                var diffPt = hit.point;
//                //var offsetPt = diffPt + hit.normal * 0.01f;

//                // check if edge actually obstructs the path
//                if (!EdgeObstructs(tx, rx, diffPt))
//                    continue;

//                float d1 = Vector3.Distance(tx, diffPt);
//                float d2 = Vector3.Distance(diffPt, rx);
//                if (d1 < 0.01f || d2 < 0.01f)
//                    continue;

//                // segment occlusion checks
//                RaycastHit blockHit;

//                // TX -> edge
//                if (IsSegmentBlocked(tx, diffPt, out blockHit))
//                {
//                    if (enableRayVisualization)
//                    {
//                        visualPaths.Add(new RayPath
//                        {
//                            points = new List<Vector3> { tx, blockHit.point },
//                            color = blockedRayColor,
//                            label = $"Diffraction TX->edge blocked by {blockHit.collider.name}"
//                        });
//                    }
//                    continue;
//                }

//                // edge -> RX (start slightly off surface)
//                var diffStart = diffPt + hit.normal * 0.05f;
//                if (IsSegmentBlocked(diffStart, rx, out blockHit))
//                {
//                    if (enableRayVisualization)
//                    {
//                        visualPaths.Add(new RayPath
//                        {
//                            points = new List<Vector3> { diffStart, blockHit.point },
//                            color = blockedRayColor,
//                            label = $"Diffraction edge->RX blocked by {blockHit.collider.name}"
//                        });
//                    }
//                    continue;
//                }

//                // calculate Fresnel parameter
//                float v = FresnelV(tx, rx, diffPt, context.WavelengthMeters);

//                // knife-edge diffraction loss (ITU-R P.526)
//                float diffLoss = KnifeEdgeLoss(v);

//                float totalDist = d1 + d2;
//                float fspl = FSPL(context.FrequencyMHz, totalDist);
//                float totalLoss = fspl + diffLoss;

//                paths.Add(new PathContribution
//                {
//                    Type = PathType.Diffraction,
//                    LossDb = totalLoss,
//                    FsplDb = fspl,
//                    MechanismExtraDb = diffLoss,
//                    DistanceMeters = totalDist,
//                    ExtraPhaseRad = -Mathf.PI * 0.25f
//                });

//                visualPaths.Add(new RayPath
//                {
//                    points = new List<Vector3> { tx, diffPt, rx },
//                    color = diffractionRayColor,
//                    label = $"Diffraction ({totalLoss:F1} dB, v={v:F2})"
//                });

//                if (maxDiffractions <= 1) break;
//            }
//        }

//        // ====================================================================
//        // Path Combination (Coherent Phasor Addition)
//        // ====================================================================

//        private float CombinePaths(List<PathContribution> paths)
//        {
//            if (paths.Count == 0) return float.PositiveInfinity;
//            if (paths.Count == 1) return paths[0].LossDb;

//            double wavelength = 3e8 / (context.FrequencyMHz * 1e6);
//            double realSum = 0.0;
//            double imagSum = 0.0;

//            foreach (var p in paths)
//            {
//                // convert loss to linear power
//                double powerLin = Math.Pow(10.0, -p.LossDb / 10.0);
//                double amplitude = Math.Sqrt(powerLin);

//                // total phase = distance phase + mechanism phase
//                double phase = (2.0 * Math.PI * p.DistanceMeters / wavelength) + p.ExtraPhaseRad;

//                // phasor addition
//                realSum += amplitude * Math.Cos(phase);
//                imagSum += amplitude * Math.Sin(phase);
//            }

//            // convert back to loss
//            double totalPower = realSum * realSum + imagSum * imagSum;
//            if (totalPower < 1e-20) return float.PositiveInfinity;

//            return (float)(-10.0 * Math.Log10(totalPower));
//        }

//        // ====================================================================
//        // Helper Functions
//        // ====================================================================

//        // Free Space Path Loss
//        private float FSPL(float freqMHz, float distMeters)
//        {
//            float d_km = Mathf.Max(distMeters, 0.001f) / 1000f;
//            float f = Mathf.Max(freqMHz, 1f);
//            return 32.44f + 20f * Mathf.Log10(d_km) + 20f * Mathf.Log10(f);
//        }

//        // Fresnel parameter for knife-edge diffraction
//        private float FresnelV(Vector3 tx, Vector3 rx, Vector3 edge, float wavelength)
//        {
//            float d1 = Vector3.Distance(tx, edge);
//            float d2 = Vector3.Distance(edge, rx);
//            if (d1 < EPS || d2 < EPS) return 0f;

//            Vector3 dir = rx - tx;
//            float dTot = dir.magnitude;
//            if (dTot < EPS) return 0f;

//            Vector3 dirN = dir / dTot;

//            float along = Vector3.Dot(edge - tx, dirN);
//            float t = Mathf.Clamp01(along / dTot);

//            float lineHeight = Mathf.Lerp(tx.y, rx.y, t);

//            float h = edge.y - lineHeight;
//            if (h <= 0f) return 0f; 

//            float factor = Mathf.Sqrt(2f * (d1 + d2) / (wavelength * d1 * d2));
//            return h * factor;
//        }

//        // Knife-edge diffraction loss (ITU-R P.526)
//        private float KnifeEdgeLoss(float v)
//        {
//            if (v <= -0.78f) return 0f;
//            float term = Mathf.Sqrt((v - 0.1f) * (v - 0.1f) + 1f) + v - 0.1f;
//            return 6.9f + 20f * Mathf.Log10(term);
//        }

//        // Check if edge obstructs TX-RX line
//        private bool EdgeObstructs(Vector3 tx, Vector3 rx, Vector3 edge)
//        {
//            var dir = (rx - tx).normalized;
//            var toEdge = edge - tx;
//            float alongPath = Vector3.Dot(toEdge, dir);
//            float dist = Vector3.Distance(tx, rx);

//            if (alongPath < 0 || alongPath > dist) return false;

//            var closestOnLine = tx + alongPath * dir;
//            float perpDist = Vector3.Distance(edge, closestOnLine);

//            float t = alongPath / dist;
//            float lineHeight = Mathf.Lerp(tx.y, rx.y, t);

//            return edge.y > lineHeight && perpDist > 0.1f;
//        }

//        // Get reflection point using image method
//        private bool GetReflectionPoint(Vector3 tx, Vector3 rx, Wall wall, out Vector3 point)
//        {
//            float d = Vector3.Dot(rx - wall.center, wall.normal);
//            var rxImage = rx - 2f * d * wall.normal;

//            var v = rxImage - tx;
//            float denom = Vector3.Dot(wall.normal, v);
//            if (Mathf.Abs(denom) < EPS)
//            {
//                point = Vector3.zero;
//                return false;
//            }

//            float t = Vector3.Dot(wall.normal, wall.center - tx) / denom;
//            if (t <= 0f || t >= 1f)
//            {
//                point = Vector3.zero;
//                return false;
//            }

//            point = tx + t * v;

//            return IsPointOnWall(point, wall);
//        }

//        // Check if point is within wall rectangle
//        private bool IsPointOnWall(Vector3 p, Wall wall)
//        {
//            var b = wall.bounds;
//            float margin = 0.1f;

//            if (Mathf.Abs(wall.normal.x) > 0.9f)
//                return p.y >= b.min.y - margin && p.y <= b.max.y + margin &&
//                       p.z >= b.min.z - margin && p.z <= b.max.z + margin;

//            if (Mathf.Abs(wall.normal.z) > 0.9f)
//                return p.y >= b.min.y - margin && p.y <= b.max.y + margin &&
//                       p.x >= b.min.x - margin && p.x <= b.max.x + margin;

//            return false;
//        }

//        // Extract rooftop edges from buildings
//        private struct Edge { public Vector3 p1, p2; public Collider building; }
//        private struct Wall { public Vector3 center, normal; public Bounds bounds; }

//        private List<Edge> ExtractRooftopEdges(Collider[] buildings, Vector3 tx, Vector3 rx)
//        {
//            var edges = new List<Edge>();

//            foreach (var bldg in buildings)
//            {
//                var b = bldg.bounds;
//                float top = b.max.y;

//                edges.Add(new Edge
//                {
//                    p1 = new Vector3(b.min.x, top, b.min.z),
//                    p2 = new Vector3(b.max.x, top, b.min.z),
//                    building = bldg  // ← ADD THIS!
//                });
//                edges.Add(new Edge
//                {
//                    p1 = new Vector3(b.max.x, top, b.min.z),
//                    p2 = new Vector3(b.max.x, top, b.max.z),
//                    building = bldg  // ← ADD THIS!
//                });
//                edges.Add(new Edge
//                {
//                    p1 = new Vector3(b.max.x, top, b.max.z),
//                    p2 = new Vector3(b.min.x, top, b.max.z),
//                    building = bldg  // ← ADD THIS!
//                });
//                edges.Add(new Edge
//                {
//                    p1 = new Vector3(b.min.x, top, b.max.z),
//                    p2 = new Vector3(b.min.x, top, b.min.z),
//                    building = bldg  // ← ADD THIS!
//                });
//            }

//            return edges;
//        }

//        // Get 4 vertical walls of a building
//        private IEnumerable<Wall> GetWalls(Bounds b)
//        {
//            var c = b.center;
//            var e = b.extents;

//            yield return new Wall { center = new Vector3(c.x + e.x, c.y, c.z), normal = Vector3.right, bounds = b };
//            yield return new Wall { center = new Vector3(c.x - e.x, c.y, c.z), normal = Vector3.left, bounds = b };
//            yield return new Wall { center = new Vector3(c.x, c.y, c.z + e.z), normal = Vector3.forward, bounds = b };
//            yield return new Wall { center = new Vector3(c.x, c.y, c.z - e.z), normal = Vector3.back, bounds = b };
//        }

//        // Closest point on edge to TX-RX line
//        private Vector3 ClosestPointOnEdge(Vector3 tx, Vector3 rx, Vector3 edgeStart, Vector3 edgeEnd)
//        {
//            var lineDir = (rx - tx).normalized;
//            var edgeDir = (edgeEnd - edgeStart).normalized;
//            var toEdge = edgeStart - tx;

//            float proj = Vector3.Dot(toEdge, lineDir);
//            var closestOnLine = tx + proj * lineDir;

//            var toClosest = closestOnLine - edgeStart;
//            float edgeLen = Vector3.Distance(edgeStart, edgeEnd);
//            float t = Mathf.Clamp01(Vector3.Dot(toClosest, edgeDir) / edgeLen);

//            return Vector3.Lerp(edgeStart, edgeEnd, t);
//        }

//        private bool IsSegmentBlocked(Vector3 a, Vector3 b, out RaycastHit hit)
//        {
//            var dir = b - a;
//            float dist = dir.magnitude;
//            hit = default;

//            if (dist < 0.01f)
//                return false;

//            dir /= dist;

//            const float eps = 0.05f;
//            var origin = a + dir * eps;
//            var maxDist = Mathf.Max(0f, dist - 2f * eps);

//            if (Physics.Raycast(origin, dir, out hit, maxDist, context.BuildingLayer))
//                return true;

//            return false;
//        }

//        // ====================================================================
//        // Visualization
//        // ====================================================================

//        private void VisualizePaths()
//        {
//            if (Visualizer == null) return;

//            foreach (var path in visualPaths)
//            {
//                if (path.points.Count < 2) continue;

//                for (int i = 0; i < path.points.Count - 1; i++)
//                {
//                    Visualizer.DrawPolyline(
//                        new[] { path.points[i], path.points[i + 1] },
//                        path.color,
//                        path.label
//                    );
//                }
//            }
//        }

//        private void DebugDumpPaths()
//        {
//            Debug.Log("=== RayTracingModel Path Dump ===");

//            foreach (var p in paths)
//            {
//                float rxPower = context.TransmitterPowerDbm - p.LossDb;

//                Debug.Log(
//                    $"{p.Type} | d = {p.DistanceMeters:F2} m | " +
//                    $"FSPL = {p.FsplDb:F1} dB | Extra = {p.MechanismExtraDb:F1} dB | " +
//                    $"TotalLoss = {p.LossDb:F1} dB | Pr = {rxPower:F1} dBm"
//                );
//            }
//        }
//    }
//}