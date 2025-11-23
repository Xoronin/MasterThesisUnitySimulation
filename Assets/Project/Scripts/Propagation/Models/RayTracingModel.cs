using RFSimulation.Environment;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;
using RFSimulation.Utils;
using RFSimulation.Visualization;
using System;
using System.Collections.Generic;
using UnityEngine;
using Complex = System.Numerics.Complex;


namespace RFSimulation.Propagation.Models
{
    public class RayTracingModel : IPathLossModel
    {
        public string ModelName => "RayTracing";

        [Header("Visualization")]
        public bool enableRayVisualization = true; 
        public Color directRayColor = Color.green;
        public Color reflectionRayColor = Color.blue;
        public Color diffractionRayColor = Color.yellow;
        public Color scatteringRayColor = Color.cyan;
        public Color blockedRayColor = Color.red;

        public RayVisualization Visualizer { get; set; }

        private PropagationContext context;
        private List<PathContribution> paths;
        private readonly List<RayPath> visualPaths = new List<RayPath>();

        private class RayPath
        {
            public List<Vector3> points = new List<Vector3>();
            public Color color;
            public string label;
        }

        private enum PathType
        {
            LOS,
            Reflection,
            Diffraction,
            Scattering
        }

        private struct PathContribution
        {
            public float LossDb;
            public float DistanceMeters;
            public float ExtraPhaseRad;

            public PathType Type;
            public float FsplDb;
            public float MechanismExtraDb;
        }

        const float EPS = 1e-6f;

        public float CalculatePathLoss(PropagationContext ctx)
        {
            context = ctx;
            paths = new List<PathContribution>();
            visualPaths.Clear();

            if (enableRayVisualization && Visualizer != null)
                Visualizer.BeginFrame();

            try
            {
                // trace paths
                TraceLOS();
                if (context.MaxReflections > 0) TraceReflections();
                if (context.MaxDiffractions > 0) TraceDiffractions();
                if (context.MaxScattering > 0) TraceScattering();

                float result = CombinePaths(paths);

                if (enableRayVisualization && Visualizer != null)
                    VisualizePaths();

                return result;
            }
            finally
            {
                if (enableRayVisualization && Visualizer != null)
                    Visualizer.EndFrame();
            }
        }

        // ====================================================================
        // LOS Path
        // ====================================================================

        private void TraceLOS()
        {
            var tx = context.TransmitterPosition;
            var rx = context.ReceiverPosition;
            var dir = rx - tx;
            var dist = dir.magnitude;

            if (dist < 0.01f || dist > context.MaxDistanceMeters) return;

            // check for blockage
            if (!RaycastHelper.IsLineOfSight(tx, rx, context.BuildingLayer, out var losHit))
            {
                visualPaths.Add(new RayPath
                {
                    points = new List<Vector3> { tx, rx },
                    color = blockedRayColor,
                    label = $"LOS blocked by {losHit.collider.name}"
                });
                return;
            }

            // clear LOS path
            float fspl = RFMathHelper.CalculateFSPL(dist, context.FrequencyMHz);

            paths.Add(new PathContribution
            {
                Type = PathType.LOS,
                LossDb = fspl,
                FsplDb = fspl,
                MechanismExtraDb = 0f,
                DistanceMeters = dist,
                ExtraPhaseRad = 0f
            });

            visualPaths.Add(new RayPath
            {
                points = new List<Vector3> { tx, rx },
                color = directRayColor,
                label = $"LOS ({fspl:F1} dB)"
            });
        }

        // ====================================================================
        // Reflection (Image Method)
        // ====================================================================

        private void TraceReflections()
        {
            var reflectionsCount = 0;

            var tx = context.TransmitterPosition;
            var rx = context.ReceiverPosition;

            var mid = 0.5f * (tx + rx);
            var size = new Vector3(
                Mathf.Abs(tx.x - rx.x) + 10f,
                Mathf.Max(10f, Mathf.Abs(tx.y - rx.y)) + 10f,
                Mathf.Abs(tx.z - rx.z) + 10f
            );

            var buildings = Physics.OverlapBox(mid, size * 0.5f, Quaternion.identity, context.BuildingLayer);

            foreach (var building in buildings)
            {
                var bounds = building.bounds;

                foreach (var wall in GeometryHelper.GetVerticalWalls(bounds))
                {
                    if (!GeometryHelper.GetReflectionPoint(tx, rx, wall, out var theoreticalPt))
                        continue;

                    var toTheoretical = theoreticalPt - tx;
                    var distToTheoretical = toTheoretical.magnitude;
                    if (distToTheoretical < 0.01f) continue;

                    var dirToTheoretical = toTheoretical / distToTheoretical;

                    if (!Physics.Raycast(tx, dirToTheoretical,
                                         out var hit,
                                         distToTheoretical + 0.1f,
                                         context.BuildingLayer))
                        continue;

                    if (hit.collider != building)
                        continue;

                    var reflPt = hit.point;

                    var offsetPt = reflPt + hit.normal * 0.01f;

                    float d1 = Vector3.Distance(tx, reflPt);
                    float d2 = Vector3.Distance(reflPt, rx);
                    if (d1 < 0.01f || d2 < 0.01f) continue;

                    RaycastHit blockHit;

                    if (RaycastHelper.IsSegmentBlocked(tx, reflPt, context.BuildingLayer, out blockHit))
                    {
                        if (enableRayVisualization)
                        {
                            visualPaths.Add(new RayPath
                            {
                                points = new List<Vector3> { tx, blockHit.point, rx },
                                color = blockedRayColor,
                                label = $"Reflection TX->wall blocked by {blockHit.collider.name}"
                            });
                        }
                        continue;
                    }

                    var reflStart = reflPt + hit.normal * 0.05f;
                    if (RaycastHelper.IsSegmentBlocked(reflStart, rx, context.BuildingLayer, out blockHit))
                    {
                        if (enableRayVisualization)
                        {
                            visualPaths.Add(new RayPath
                            {
                                points = new List<Vector3> { tx, reflStart, rx },
                                color = blockedRayColor,
                                label = $"Reflection wall->RX blocked by {blockHit.collider.name}"
                            });
                        }
                        continue;
                    }

                    Vector3 dirIn = (reflPt - tx).normalized;
                    Vector3 n = hit.normal;
                    float cosTheta = Mathf.Abs(Vector3.Dot(-dirIn, n));

                    float gammaMag = 0f;
                    var bldg = building.GetComponent<Building>();
                    if (bldg?.material != null)
                    {
                        gammaMag = RFMathHelper.CalculateFresnelReflectionCoefficients(cosTheta, context.FrequencyMHz, bldg.material);
                        float rho_s = RFMathHelper.CalculateRoughnessCorrection(bldg.material, cosTheta, context.FrequencyMHz, out float g);

                        // check if surface is smooth
                        if (g >= 1f)
                        {
                            continue;
                        }

                        // |Γ_rough| = |Γ| · ρ_s
                        gammaMag *= rho_s;
                    }

                    float totalDist = d1 + d2;

                    float fspl = RFMathHelper.CalculateFSPL(totalDist, context.FrequencyMHz);
                    float reflLoss = -20f * Mathf.Log10(Mathf.Max(0.1f, gammaMag));
                    float totalLoss = fspl + reflLoss;

                    float reflPhase = Mathf.PI;

                    paths.Add(new PathContribution
                    {
                        Type = PathType.Reflection,
                        LossDb = totalLoss,
                        FsplDb = fspl,
                        MechanismExtraDb = reflLoss,
                        DistanceMeters = totalDist,
                        ExtraPhaseRad = reflPhase
                    });

                    visualPaths.Add(new RayPath
                    {
                        points = new List<Vector3> { tx, reflPt, rx },
                        color = reflectionRayColor,
                        label = $"Reflection ({totalLoss:F1} dB, |Γ|={gammaMag:F2})"
                    });

                    reflectionsCount++;

                    if (context.MaxReflections <= reflectionsCount) break;
                }
            }
        }

        // ====================================================================
        // Diffraction (Knife-Edge)
        // ====================================================================

        private void TraceDiffractions()
        {
            var diffractionsCount = 0;

            var tx = context.TransmitterPosition;
            var rx = context.ReceiverPosition;

            // find buildings that might obstruct
            var mid = 0.5f * (tx + rx);
            var size = new Vector3(
                Mathf.Abs(tx.x - rx.x) + 20f,
                Mathf.Max(20f, Mathf.Abs(tx.y - rx.y)) + 20f,
                Mathf.Abs(tx.z - rx.z) + 20f
            );

            var buildings = Physics.OverlapBox(mid, size * 0.5f, Quaternion.identity, context.BuildingLayer);

            var edges = new List<GeometryHelper.Edge>();
            edges.AddRange(GeometryHelper.ExtractRooftopEdges(buildings));
            edges.AddRange(GeometryHelper.ExtractCornerEdges(buildings));

            foreach (var edge in edges)
            {
                // find diffraction point on edge
                var theoreticalPt = GeometryHelper.ClosestPointOnEdgeToLine(tx, rx, edge.Point1, edge.Point2);

                Vector3 diffPt;
                Vector3 normal;

                if (edge.IsCorner)
                {
                    // vertical corner
                    diffPt = theoreticalPt;

                    var b = edge.Building.bounds;
                    normal = (diffPt - b.center);
                    normal.y = 0f;
                    if (normal.sqrMagnitude < 1e-4f)
                        normal = Vector3.up;
                    normal.Normalize();
                }
                else
                {
                    // rooftop
                    var abovePoint = theoreticalPt + Vector3.up * 1f;
                    if (!Physics.Raycast(abovePoint, Vector3.down, out var hit, 5f, context.BuildingLayer))
                        continue;

                    diffPt = hit.point;
                    normal = hit.normal;
                }

                // check if edge actually obstructs the path
                if (!GeometryHelper.EdgeObstructs(tx, rx, diffPt))
                    continue;

                float d1 = Vector3.Distance(tx, diffPt);
                float d2 = Vector3.Distance(diffPt, rx);
                if (d1 < 0.01f || d2 < 0.01f)
                    continue;

                // segment occlusion checks
                RaycastHit blockHit;

                // TX -> edge || edge -> RX 
                if (RaycastHelper.IsSegmentBlocked(tx, diffPt, context.BuildingLayer, out blockHit))
                {
                    if (enableRayVisualization)
                    {
                        visualPaths.Add(new RayPath
                        {
                            points = new List<Vector3> { tx, blockHit.point, rx },
                            color = blockedRayColor,
                            label = $"Diffraction TX->edge blocked by {blockHit.collider.name}"
                        });
                    }
                    continue;
                }

                // edge -> RX 
                var diffStart = diffPt + normal * 0.05f;
                if (RaycastHelper.IsSegmentBlocked(diffStart, rx, context.BuildingLayer, out blockHit))
                {
                    if (enableRayVisualization)
                    {
                        visualPaths.Add(new RayPath
                        {
                            points = new List<Vector3> { tx, diffStart, rx },
                            color = blockedRayColor,
                            label = $"Diffraction edge->RX blocked by {blockHit.collider.name}"
                        });
                    }
                    continue;
                }

                // calculate Fresnel parameter
                float v = RFMathHelper.FresnelV(tx, rx, diffPt, context.FrequencyMHz);

                // knife-edge diffraction loss 
                float diffLoss = RFMathHelper.KnifeEdgeLoss(v);

                float totalDist = d1 + d2;
                float fspl = RFMathHelper.CalculateFSPL(totalDist, context.FrequencyMHz);
                float totalLoss = fspl + diffLoss;

                paths.Add(new PathContribution
                {
                    Type = PathType.Diffraction,
                    LossDb = totalLoss,
                    FsplDb = fspl,
                    MechanismExtraDb = diffLoss,
                    DistanceMeters = totalDist,
                    ExtraPhaseRad = -Mathf.PI * 0.25f
                });

                visualPaths.Add(new RayPath
                {
                    points = new List<Vector3> { tx, diffPt, rx },
                    color = diffractionRayColor,
                    label = $"Diffraction ({totalLoss:F1} dB, v={v:F2})"
                });

                diffractionsCount++;

                if (context.MaxDiffractions <= diffractionsCount) break;
            }
        }

        // ====================================================================
        // Diffuse Scattering
        // ====================================================================

        private void TraceScattering()
        {
            var scatteringCount = 0f;

            var tx = context.TransmitterPosition;
            var rx = context.ReceiverPosition;

            var mid = 0.5f * (tx + rx);
            var size = new Vector3(
                Mathf.Abs(tx.x - rx.x) + 20f,
                Mathf.Max(20f, Mathf.Abs(tx.y - rx.y)) + 20f,
                Mathf.Abs(tx.z - rx.z) + 20f
            );

            var buildings = Physics.OverlapBox(mid, size * 0.5f, Quaternion.identity, context.BuildingLayer);

            foreach (var building in buildings)
            {
                var bounds = building.bounds;
                foreach (var wall in GeometryHelper.GetVerticalWalls(bounds))
                {
                    if (!GeometryHelper.GetReflectionPoint(tx, rx, wall, out var theoreticalPt))
                        continue;

                    Vector3 toTheo = theoreticalPt - tx;
                    float distToTheo = toTheo.magnitude;
                    if (distToTheo < 0.05f) continue;

                    Vector3 dirToTheo = toTheo / distToTheo;
                    if (!Physics.Raycast(tx, dirToTheo, out var hitOnWall, distToTheo + 0.1f, context.BuildingLayer))
                        continue;

                    if (hitOnWall.collider != building)
                        continue;

                    Vector3 scatterPt = hitOnWall.point;
                    Vector3 wallNormal = hitOnWall.normal;

                    bool txInFront = Vector3.Dot(tx - scatterPt, wallNormal) > 0f;
                    bool rxInFront = Vector3.Dot(rx - scatterPt, wallNormal) > 0f;
                    if (!txInFront || !rxInFront)
                    {
                        continue;
                    }

                    if (RaycastHelper.IsSegmentBlocked(tx, scatterPt, context.BuildingLayer, out var blockHit) && blockHit.collider != building)
                    {
                        if (enableRayVisualization)
                        {
                            visualPaths.Add(new RayPath
                            {
                                points = new List<Vector3> { tx, blockHit.point },
                                color = blockedRayColor,
                                label = $"Scatt TX->wall blocked by {blockHit.collider.name}"
                            });
                        }
                        continue;
                    }

                    var startFromWall = scatterPt + wallNormal * 0.06f;
                    if (RaycastHelper.IsSegmentBlocked(startFromWall, rx, context.BuildingLayer, out blockHit))
                    {
                        bool immediateSelf =
                            blockHit.collider == building &&
                            Vector3.Distance(startFromWall, blockHit.point) < 0.08f;

                        if (!immediateSelf)
                        {
                            if (enableRayVisualization)
                            {
                                visualPaths.Add(new RayPath
                                {
                                    points = new List<Vector3> { startFromWall, blockHit.point },
                                    color = blockedRayColor,
                                    label = $"Scatt wall->RX blocked by {blockHit.collider.name}"
                                });
                            }
                            continue;
                        }
                    }

                    float d1 = Vector3.Distance(tx, scatterPt);
                    float d2 = Vector3.Distance(scatterPt, rx);
                    if (d1 < 0.5f || d2 < 0.5f) continue;

                    Vector3 dirIn = (scatterPt - tx).normalized;
                    Vector3 dirOut = (rx - scatterPt).normalized;

                    float cosThetaI = Mathf.Max(0.2f, Mathf.Abs(Vector3.Dot(dirIn, wallNormal)));
                    float cosThetaS = Mathf.Max(0.2f, Mathf.Abs(Vector3.Dot(dirOut, wallNormal)));

                    float S = 0;
                    var bldg = building.GetComponent<Building>();
                    if (bldg?.material != null)
                    {
                        float rho_s = RFMathHelper.CalculateRoughnessCorrection(bldg.material, cosThetaI, context.FrequencyMHz, out float g);

                        S = Mathf.Clamp01(1f - rho_s);

                        // skip for nearly smooth surfaces
                        if (S < 0.05f)
                            continue;
                    }

                    float fspl1 = RFMathHelper.CalculateFSPL(d1, context.FrequencyMHz);
                    float fspl2 = RFMathHelper.CalculateFSPL(d2, context.FrequencyMHz);
                    float fsplTotal = fspl1 + fspl2;

                    float scatteringLoss = RFMathHelper.ScatteringLoss(cosThetaI, cosThetaS, S);
                    float totalLoss = fsplTotal + scatteringLoss;

                    paths.Add(new PathContribution
                    {
                        Type = PathType.Scattering,
                        LossDb = totalLoss,
                        FsplDb = fsplTotal,
                        MechanismExtraDb = scatteringLoss,
                        DistanceMeters = d1 + d2,
                        ExtraPhaseRad = 0f
                    });

                    if (enableRayVisualization)
                    {
                        visualPaths.Add(new RayPath
                        {
                            points = new List<Vector3> { tx, scatterPt, rx },
                            color = scatteringRayColor,
                            label = $"Scatt (S={S:F2}, L={totalLoss:F1} dB)"
                        });
                    }

                    scatteringCount++;

                    if (context.MaxScattering <= scatteringCount) break;

                }
            }
        }


        // ====================================================================
        // Path Combination (Coherent Phasor Addition)
        // ====================================================================

        private float CombinePaths(List<PathContribution> paths)
        {
            if (paths.Count == 0) return float.PositiveInfinity;
            if (paths.Count == 1) return paths[0].LossDb;

            double wavelength = RFMathHelper.CalculateWavelength(context.FrequencyMHz);
            double realSum = 0.0;
            double imagSum = 0.0;

            foreach (var p in paths)
            {
                // convert loss to linear power
                double powerLin = Math.Pow(10.0, -p.LossDb / 10.0);
                double amplitude = Math.Sqrt(powerLin);

                // total phase = distance phase + mechanism phase
                double phase = (2.0 * Math.PI * p.DistanceMeters / wavelength) + p.ExtraPhaseRad;

                // phasor addition
                realSum += amplitude * Math.Cos(phase);
                imagSum += amplitude * Math.Sin(phase);
            }

            // convert back to loss
            double totalPower = realSum * realSum + imagSum * imagSum;
            if (totalPower < 1e-20) return float.PositiveInfinity;

            return (float)(-10.0 * Math.Log10(totalPower));
        }


        // Visualization
        private void VisualizePaths()
        {
            if (Visualizer == null) return;

            foreach (var path in visualPaths)
            {
                if (path.points.Count < 2) continue;

                for (int i = 0; i < path.points.Count - 1; i++)
                {
                    Visualizer.DrawPolyline(
                        new[] { path.points[i], path.points[i + 1] },
                        path.color,
                        path.label
                    );
                }
            }
        }
    }
}