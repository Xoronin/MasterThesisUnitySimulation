using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Core;
using RFSimulation.Propagation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Environment;

namespace RFSimulation.Propagation.PathLoss.Models
{
    /// <summary>
    /// Urban Ray Tracing Model optimized for Mapbox Unity SDK environments
    /// Extends BasicRayTracingModel with urban-specific features
    /// </summary>
    public class UrbanRayTracingModel : IPathLossModel
    {
        public string ModelName => "Urban Ray Tracing";

        [Header("Urban Ray Tracing Settings")]
        public int maxReflections = 3;
        public int maxDiffractions = 2;
        public float maxRayDistance = 2000f; // Increased for urban environments
        public LayerMask mapboxBuildingLayer = 1 << 8; // Layer for Mapbox buildings

        [Header("Urban-Specific Settings")]
        public bool enableDiffraction = true;
        public bool enableMultipleReflections = true;
        public bool useUrbanAcceleration = true;
        public float diffractionThreshold = 0.6f; // Fresnel zone clearance

        [Header("Performance")]
        public bool use2DSimplification = true; // Recommended by paper for urban
        public int maxRaysPerCalculation = 100;
        public bool enableSpatialPartitioning = true;

        private UrbanRayCache rayCache = new UrbanRayCache();
        private UrbanSpatialGrid spatialGrid;
        private List<BuildingEdge> urbanEdges = new List<BuildingEdge>();
        private bool spatialGridInitialized = false;

        public float Calculate(PropagationContext context)
        {
            // Initialize spatial partitioning if not done
            if (!spatialGridInitialized && enableSpatialPartitioning)
            {
                InitializeUrbanSpatialGrid(context);
            }

            // Check cache first
            string cacheKey = GenerateUrbanCacheKey(context);
            if (rayCache.TryGetValue(cacheKey, out float cachedResult))
            {
                return cachedResult;
            }

            float totalReceivedPower = 0f;

            // 1. Direct ray (Line of Sight)
            float directPower = CalculateDirectRay(context);
            if (directPower > float.NegativeInfinity)
            {
                totalReceivedPower += Mathf.Pow(10f, directPower / 10f);
            }

            // 2. Urban-specific propagation mechanisms
            if (use2DSimplification)
            {
                // Use 2D simplification as recommended by paper for urban environments
                totalReceivedPower += Calculate2DUrbanPropagation(context);
            }
            else
            {
                // Full 3D urban ray tracing
                totalReceivedPower += Calculate3DUrbanPropagation(context);
            }

            // Convert back to dBm
            float result = totalReceivedPower > 0f ? 10f * Mathf.Log10(totalReceivedPower) : -200f;

            // Cache result
            rayCache.Store(cacheKey, result);
            return result;
        }

        private void InitializeUrbanSpatialGrid(PropagationContext context)
        {
            // Create spatial grid for fast building lookup
            Vector3 center = (context.TransmitterPosition + context.ReceiverPosition) * 0.5f;
            float gridSize = Mathf.Max(context.Distance * 2f, 1000f);

            spatialGrid = new UrbanSpatialGrid(center, gridSize, 50); // 50x50 grid

            // Populate grid with Mapbox buildings
            PopulateGridWithMapboxBuildings();

            // Extract building edges for diffraction
            ExtractUrbanDiffractionEdges();

            spatialGridInitialized = true;
            Debug.Log($"[UrbanRayTracing] Initialized spatial grid with {urbanEdges.Count} diffraction edges");
        }

        private void PopulateGridWithMapboxBuildings()
        {
            // Find all Mapbox buildings in the scene
            GameObject[] buildings = GameObject.FindGameObjectsWithTag("Building");
            if (buildings.Length == 0)
            {
                // Fallback: search by layer
                Collider[] buildingColliders = Physics.OverlapSphere(spatialGrid.center, spatialGrid.size, mapboxBuildingLayer);
                foreach (var collider in buildingColliders)
                {
                    spatialGrid.AddBuilding(collider.gameObject);
                }
            }
            else
            {
                foreach (var building in buildings)
                {
                    spatialGrid.AddBuilding(building);
                }
            }
        }

        private void ExtractUrbanDiffractionEdges()
        {
            urbanEdges.Clear();

            foreach (var building in spatialGrid.GetAllBuildings())
            {
                var edges = ExtractBuildingEdges(building);
                urbanEdges.AddRange(edges);
            }
        }

        private List<BuildingEdge> ExtractBuildingEdges(GameObject building)
        {
            List<BuildingEdge> edges = new List<BuildingEdge>();

            // Get building bounds
            Renderer renderer = building.GetComponent<Renderer>();
            if (renderer == null) return edges;

            Bounds bounds = renderer.bounds;

            // For Mapbox buildings, extract key diffraction edges
            // Rooftop corners (most important for urban diffraction)
            Vector3[] rooftopCorners = {
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z), // Bottom-left
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z), // Bottom-right
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z), // Top-right
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z)  // Top-left
            };

            // Create diffraction edges between corners
            for (int i = 0; i < rooftopCorners.Length; i++)
            {
                int next = (i + 1) % rooftopCorners.Length;
                edges.Add(new BuildingEdge
                {
                    start = rooftopCorners[i],
                    end = rooftopCorners[next],
                    height = bounds.max.y,
                    building = building
                });
            }

            return edges;
        }

        private float CalculateDirectRay(PropagationContext context)
        {
            Vector3 direction = (context.ReceiverPosition - context.TransmitterPosition).normalized;
            float distance = context.Distance;

            // Check for obstacles using Mapbox buildings
            if (Physics.Raycast(context.TransmitterPosition, direction, out RaycastHit hit, distance, mapboxBuildingLayer))
            {
                return float.NegativeInfinity; // Blocked by building
            }

            // Clear line of sight - calculate free space loss
            float fspl = CalculateFreeSpacePathLoss(distance, context.FrequencyMHz);
            return context.TransmitterPowerDbm + context.AntennaGainDbi - fspl;
        }

        private float Calculate2DUrbanPropagation(PropagationContext context)
        {
            float totalLinearPower = 0f;

            // Project to 2D ground plane as recommended by paper
            Vector2 tx2D = new Vector2(context.TransmitterPosition.x, context.TransmitterPosition.z);
            Vector2 rx2D = new Vector2(context.ReceiverPosition.x, context.ReceiverPosition.z);

            // Calculate reflections using 2D simplification
            if (enableMultipleReflections)
            {
                var reflectionPower = Calculate2DReflections(context, tx2D, rx2D);
                totalLinearPower += reflectionPower;
            }

            // Calculate diffraction using rooftop edges
            if (enableDiffraction)
            {
                var diffractionPower = Calculate2DDiffraction(context, tx2D, rx2D);
                totalLinearPower += diffractionPower;
            }

            return totalLinearPower;
        }

        private float Calculate2DReflections(PropagationContext context, Vector2 tx2D, Vector2 rx2D)
        {
            float totalLinearPower = 0f;
            int reflectionsCalculated = 0;

            // Get nearby buildings for reflection calculation
            var nearbyBuildings = spatialGrid.GetBuildingsInRadius(context.TransmitterPosition, maxRayDistance);

            foreach (var building in nearbyBuildings)
            {
                if (reflectionsCalculated >= maxReflections) break;

                // Calculate reflection from building walls
                var reflectionPower = CalculateBuildingReflection(context, building);
                if (reflectionPower > float.NegativeInfinity)
                {
                    totalLinearPower += Mathf.Pow(10f, reflectionPower / 10f);
                    reflectionsCalculated++;
                }
            }

            return totalLinearPower;
        }

        private float Calculate2DDiffraction(PropagationContext context, Vector2 tx2D, Vector2 rx2D)
        {
            float totalLinearPower = 0f;
            int diffractionsCalculated = 0;

            // Calculate diffraction from urban edges
            foreach (var edge in urbanEdges)
            {
                if (diffractionsCalculated >= maxDiffractions) break;

                // Check if edge is relevant for this TX-RX pair
                if (!IsEdgeRelevantForPath(edge, context)) continue;

                // Calculate diffraction using UTD (simplified)
                float diffractionPower = CalculateEdgeDiffraction(context, edge);
                if (diffractionPower > float.NegativeInfinity)
                {
                    totalLinearPower += Mathf.Pow(10f, diffractionPower / 10f);
                    diffractionsCalculated++;
                }
            }

            return totalLinearPower;
        }

        private float CalculateBuildingReflection(PropagationContext context, GameObject building)
        {
            // Simplified building reflection calculation
            Bounds bounds = building.GetComponent<Renderer>().bounds;

            // Find reflection point on building surface (simplified)
            Vector3 reflectionPoint = bounds.center;
            reflectionPoint.y = bounds.min.y + bounds.size.y * 0.5f; // Mid-height

            // Check if reflection path is clear
            Vector3 txToReflection = reflectionPoint - context.TransmitterPosition;
            Vector3 reflectionToRx = context.ReceiverPosition - reflectionPoint;

            if (Physics.Raycast(context.TransmitterPosition, txToReflection.normalized, txToReflection.magnitude, mapboxBuildingLayer) ||
                Physics.Raycast(reflectionPoint, reflectionToRx.normalized, reflectionToRx.magnitude, mapboxBuildingLayer))
            {
                return float.NegativeInfinity; // Path blocked
            }

            // Calculate total path length
            float totalDistance = txToReflection.magnitude + reflectionToRx.magnitude;

            // Path loss for reflected path
            float pathLoss = CalculateFreeSpacePathLoss(totalDistance, context.FrequencyMHz);

            // Add reflection loss (simplified urban value from paper)
            float reflectionLoss = 6f; // Typical urban reflection loss

            return context.TransmitterPowerDbm + context.AntennaGainDbi - pathLoss - reflectionLoss;
        }

        private float CalculateEdgeDiffraction(PropagationContext context, BuildingEdge edge)
        {
            // Simplified UTD diffraction calculation
            Vector3 edgePoint = (edge.start + edge.end) * 0.5f; // Use edge midpoint

            // Check if diffraction path is clear
            Vector3 txToEdge = edgePoint - context.TransmitterPosition;
            Vector3 edgeToRx = context.ReceiverPosition - edgePoint;

            // Calculate Fresnel parameter (simplified)
            float fresnelParameter = CalculateFresnelParameter(
                context.TransmitterPosition,
                context.ReceiverPosition,
                edgePoint,
                context.FrequencyMHz
            );

            // UTD diffraction coefficient (simplified)
            float diffractionLoss = CalculateUTDLoss(fresnelParameter);

            // Total path length
            float totalDistance = txToEdge.magnitude + edgeToRx.magnitude;
            float pathLoss = CalculateFreeSpacePathLoss(totalDistance, context.FrequencyMHz);

            return context.TransmitterPowerDbm + context.AntennaGainDbi - pathLoss - diffractionLoss;
        }

        private float CalculateFresnelParameter(Vector3 tx, Vector3 rx, Vector3 edge, float frequency)
        {
            float d1 = Vector3.Distance(tx, edge);
            float d2 = Vector3.Distance(edge, rx);
            float totalDistance = d1 + d2;

            // Height of obstacle above line-of-sight
            Vector3 losDirection = (rx - tx).normalized;
            Vector3 pointOnLOS = tx + losDirection * d1;
            float h = Vector3.Distance(edge, pointOnLOS);

            // Fresnel parameter calculation
            float wavelength = 299.792458f / (frequency * 1e6f);
            return h * Mathf.Sqrt(2 * totalDistance / (wavelength * d1 * d2));
        }

        private float CalculateUTDLoss(float fresnelParameter)
        {
            // Simplified UTD knife-edge diffraction loss
            float v = fresnelParameter;

            if (v <= -0.7f) return 0f;
            if (v <= 0f) return 6.9f + 20f * Mathf.Log10(Mathf.Sqrt(Mathf.Pow(v - 0.1f, 2) + 1f) + v - 0.1f);
            if (v <= 1.6f) return 6.9f + 20f * Mathf.Log10(Mathf.Sqrt(Mathf.Pow(v - 0.1f, 2) + 1f) + v - 0.1f);
            return 12.953f + 20f * Mathf.Log10(v);
        }

        private bool IsEdgeRelevantForPath(BuildingEdge edge, PropagationContext context)
        {
            // Check if edge is in the general direction of the TX-RX path
            Vector3 txToRx = context.ReceiverPosition - context.TransmitterPosition;
            Vector3 txToEdge = edge.start - context.TransmitterPosition;

            float dot = Vector3.Dot(txToRx.normalized, txToEdge.normalized);
            return dot > 0.1f; // Edge is roughly in the forward direction
        }

        private float Calculate3DUrbanPropagation(PropagationContext context)
        {
            // Full 3D implementation (more computationally expensive)
            // This would include full 3D reflection and diffraction calculations
            return 0f; // Placeholder - implement if 2D is insufficient
        }

        private float CalculateFreeSpacePathLoss(float distance, float frequency)
        {
            float distanceKm = distance / 1000f;
            return 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequency) + 32.45f;
        }

        private string GenerateUrbanCacheKey(PropagationContext context)
        {
            return $"urban_{context.TransmitterPosition}_{context.ReceiverPosition}_{context.FrequencyMHz:F0}";
        }
    }

    // Supporting classes for urban ray tracing
    public class BuildingEdge
    {
        public Vector3 start;
        public Vector3 end;
        public float height;
        public GameObject building;
    }

    public class UrbanSpatialGrid
    {
        public Vector3 center;
        public float size;
        private int gridResolution;
        private List<GameObject>[,] grid;
        private List<GameObject> allBuildings = new List<GameObject>();

        public UrbanSpatialGrid(Vector3 center, float size, int resolution)
        {
            this.center = center;
            this.size = size;
            this.gridResolution = resolution;
            this.grid = new List<GameObject>[resolution, resolution];

            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    grid[x, z] = new List<GameObject>();
                }
            }
        }

        public void AddBuilding(GameObject building)
        {
            allBuildings.Add(building);

            // Add to spatial grid cells
            Bounds bounds = building.GetComponent<Renderer>().bounds;
            Vector2Int minCell = WorldToGrid(new Vector2(bounds.min.x, bounds.min.z));
            Vector2Int maxCell = WorldToGrid(new Vector2(bounds.max.x, bounds.max.z));

            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int z = minCell.y; z <= maxCell.y; z++)
                {
                    if (x >= 0 && x < gridResolution && z >= 0 && z < gridResolution)
                    {
                        grid[x, z].Add(building);
                    }
                }
            }
        }

        public List<GameObject> GetBuildingsInRadius(Vector3 position, float radius)
        {
            List<GameObject> result = new List<GameObject>();

            foreach (var building in allBuildings)
            {
                if (Vector3.Distance(position, building.transform.position) <= radius)
                {
                    result.Add(building);
                }
            }

            return result;
        }

        public List<GameObject> GetAllBuildings()
        {
            return new List<GameObject>(allBuildings);
        }

        private Vector2Int WorldToGrid(Vector2 worldPos)
        {
            Vector2 relative = new Vector2(worldPos.x - center.x, worldPos.y - center.z);
            Vector2 normalized = (relative + Vector2.one * size * 0.5f) / size;

            int x = Mathf.Clamp(Mathf.FloorToInt(normalized.x * gridResolution), 0, gridResolution - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(normalized.y * gridResolution), 0, gridResolution - 1);

            return new Vector2Int(x, z);
        }
    }

    public class UrbanRayCache
    {
        private Dictionary<string, (float value, float timestamp)> cache = new Dictionary<string, (float, float)>();
        private const float CACHE_DURATION = 10f; // Longer cache for urban scenarios

        public bool TryGetValue(string key, out float value)
        {
            if (cache.TryGetValue(key, out var entry) && Time.time - entry.timestamp < CACHE_DURATION)
            {
                value = entry.value;
                return true;
            }
            value = 0f;
            return false;
        }

        public void Store(string key, float value)
        {
            cache[key] = (value, Time.time);
        }
    }
}