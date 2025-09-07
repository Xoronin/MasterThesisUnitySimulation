using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Core;
using RFSimulation.Propagation.Core;
using RFSimulation.Interfaces;

namespace RFSimulation.Propagation.PathLoss.Models
{
    /// <summary>
    /// PHASE 1: Basic Ray Tracing Implementation
    /// Simple ray shooting with first-order reflections
    /// </summary>
    public class BasicRayTracingModel : IPathLossModel
    {
        public string ModelName => "Basic Ray Tracing";

        [Header("Ray Tracing Settings")]
        public int maxReflections = 3;
        public int maxDiffractions = 2;
        public float maxRayDistance = 1000f;
        public LayerMask obstacleLayerMask = -1;

        [Header("Performance")]
        public bool useApproximation = true; // For real-time performance
        public int maxRaysPerCalculation = 50;

        private RayTracingCache cache = new RayTracingCache();

        public float Calculate(PropagationContext context)
        {
            // Check cache first
            string cacheKey = GenerateCacheKey(context);
            if (cache.TryGetValue(cacheKey, out float cachedResult))
            {
                return cachedResult;
            }

            float totalReceivedPower = 0f;

            // 1. Direct ray (Line of Sight)
            Ray directRay = CalculateDirectRay(context);
            float directPower = TraceRay(directRay, context, 0);
            if (directPower > float.NegativeInfinity)
            {
                totalReceivedPower += Mathf.Pow(10f, directPower / 10f); // Convert to linear
            }

            if (useApproximation && totalReceivedPower > 0)
            {
                // If we have LOS, use fast approximation for reflections
                float reflectedPower = CalculateApproximateReflections(context);
                totalReceivedPower += Mathf.Pow(10f, reflectedPower / 10f);
            }
            else
            {
                // Full ray tracing for NLOS scenarios
                List<Ray> reflectedRays = CalculateReflectedRays(context);
                foreach (var ray in reflectedRays)
                {
                    float power = TraceRay(ray, context, 1);
                    if (power > float.NegativeInfinity)
                    {
                        totalReceivedPower += Mathf.Pow(10f, power / 10f);
                    }
                }
            }

            // Convert back to dBm
            float result = totalReceivedPower > 0f
                ? 10f * Mathf.Log10(totalReceivedPower)
                : -200f;

            // Cache result
            cache.Store(cacheKey, result);
            return result;
        }

        private int GetEffectiveLayerMask(PropagationContext context)
        {
            // If context.BuildingLayers is nullable, use ??; if you make it non-nullable, just return context.BuildingLayers.value
            return (context.BuildingLayers ?? obstacleLayerMask).value;
        }


        private Ray CalculateDirectRay(PropagationContext context)
        {
            Vector3 direction = (context.ReceiverPosition - context.TransmitterPosition).normalized;
            return new Ray(context.TransmitterPosition, direction);
        }

        private float TraceRay(Ray ray, PropagationContext context, int reflectionCount)
        {
            if (reflectionCount > maxReflections) return float.NegativeInfinity;

            // Always use the straight TX->RX segment length as the "max check" bound
            float maxSeg = Vector3.Distance(context.TransmitterPosition, context.ReceiverPosition);

            // Is there anything between ray origin and the receiver?
            Vector3 toRx = (context.ReceiverPosition - ray.origin);
            float toRxDist = toRx.magnitude;
            Vector3 dirToRx = toRx / toRxDist;

            int layerMask = GetEffectiveLayerMask(context);

            if (!Physics.Raycast(ray.origin, dirToRx, out RaycastHit hit, toRxDist, layerMask))
            {
                // Clear to receiver along this ray: use correct segment distance for FSPL
                float lfs = CalculateFreeSpaceLoss(toRxDist, context.FrequencyMHz);
                return context.TransmitterPowerDbm + context.AntennaGainDbi - lfs;
            }

            // Blocked: try a single-bounce reflection from this hit
            if (reflectionCount < maxReflections)
            {
                return CalculateReflection(hit, context, reflectionCount);
            }

            return float.NegativeInfinity;
        }

        private float CalculateReflection(RaycastHit hit, PropagationContext context, int reflectionCount)
        {
            // Incident direction from TX to hit
            Vector3 incDir = (hit.point - context.TransmitterPosition).normalized;
            // Reflect it on the surface
            Vector3 reflDir = Vector3.Reflect(incDir, hit.normal);

            // Check if reflected ray reaches the receiver without further blocks
            Vector3 toRx = context.ReceiverPosition - hit.point;
            float toRxDist = toRx.magnitude;
            Vector3 dirToRx = toRx / toRxDist;

            int layerMask = GetEffectiveLayerMask(context);

            // Ensure outgoing direction actually points roughly towards receiver
            if (Vector3.Dot(reflDir, dirToRx) <= 0.0f) return float.NegativeInfinity;

            if (Physics.Raycast(hit.point, dirToRx, out RaycastHit block, toRxDist, layerMask))
                return float.NegativeInfinity;

            // Total geometric path length: TX->hit + hit->RX
            float txToHit = Vector3.Distance(context.TransmitterPosition, hit.point);
            float totalDist = txToHit + toRxDist;

            // Path loss for the full broken path
            float lfs = CalculateFreeSpaceLoss(totalDist, context.FrequencyMHz);

            // Fresnel reflection coefficient (amplitude 0..1) -> power dB = 20*log10(|Γ|)
            SurfaceMaterial mat = GetSurfaceMaterial(hit.collider);
            float gammaAmp = CalculateReflectionCoefficient(hit.normal, -incDir, context.FrequencyMHz, mat);
            float reflectionDb = 20f * Mathf.Log10(Mathf.Clamp(gammaAmp, 1e-4f, 1f)); // negative dB

            return context.TransmitterPowerDbm + context.AntennaGainDbi - lfs + reflectionDb;
        }

        private float CalculateFreeSpaceLoss(float distance, float frequency)
        {
            // Standard free space path loss formula
            float distanceKm = distance / 1000f;
            return 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequency) + 32.45f;
        }

        private SurfaceMaterial GetSurfaceMaterial(Collider surface)
        {
            // Try to get material from building component
            var building = surface.GetComponent<RFSimulation.Environment.Building>();
            if (building?.material != null)
            {
                return ConvertBuildingMaterial(building.material);
            }

            // Default materials based on object name/tag
            string objName = surface.gameObject.name.ToLower();
            if (objName.Contains("building") || objName.Contains("wall"))
                return SurfaceMaterial.Concrete;
            else if (objName.Contains("ground") || objName.Contains("terrain"))
                return SurfaceMaterial.Ground;
            else if (objName.Contains("metal"))
                return SurfaceMaterial.Metal;

            return SurfaceMaterial.Generic;
        }

        private float CalculateReflectionCoefficient(Vector3 normal, Vector3 incidentDir, float frequency, SurfaceMaterial material)
        {
            // Simplified Fresnel reflection coefficient
            float incidentAngle = Vector3.Angle(normal, -incidentDir) * Mathf.Deg2Rad;

            // Material-dependent dielectric properties
            float epsilon = material.DielectricConstant();
            float conductivity = material.Conductivity();

            // Simplified reflection coefficient (vertical polarization)
            float cosTheta = Mathf.Cos(incidentAngle);
            float sinTheta = Mathf.Sin(incidentAngle);

            // Complex dielectric constant
            float wavelength = 299.792458f / frequency; // c/f in meters
            float complexEpsilon = epsilon - (conductivity * wavelength * 60f);

            // Simplified Fresnel coefficient
            float denominator = cosTheta + Mathf.Sqrt(complexEpsilon - sinTheta * sinTheta);
            float reflectionCoeff = Mathf.Abs((cosTheta - Mathf.Sqrt(complexEpsilon - sinTheta * sinTheta)) / denominator);

            return Mathf.Clamp01(reflectionCoeff);
        }

        // Fast approximation methods for performance
        private float CalculateApproximateReflections(PropagationContext context)
        {
            // Simplified reflection calculation for performance
            // Assumes average urban reflection coefficients
            float distance = context.Distance;
            float reflectedDistance = distance * 1.2f; // Approximate longer path
            float reflectionLoss = 6f; // Typical reflection loss in urban environment

            float freeSpaceLoss = CalculateFreeSpaceLoss(reflectedDistance, context.FrequencyMHz);
            return context.TransmitterPowerDbm + context.AntennaGainDbi - freeSpaceLoss - reflectionLoss;
        }

        private List<Ray> CalculateReflectedRays(PropagationContext context)
        {
            // Simplified: find major reflecting surfaces in the area
            var rays = new List<Ray>();

            // This would be expanded to find actual reflecting surfaces
            // For now, just approximate with a few common reflection points

            return rays;
        }

        private SurfaceMaterial ConvertBuildingMaterial(RFSimulation.Environment.BuildingMaterial buildingMat)
        {
            return buildingMat.materialType switch
            {
                RFSimulation.Environment.MaterialType.Concrete => SurfaceMaterial.Concrete,
                RFSimulation.Environment.MaterialType.Metal => SurfaceMaterial.Metal,
                RFSimulation.Environment.MaterialType.Glass => SurfaceMaterial.Glass,
                RFSimulation.Environment.MaterialType.Brick => SurfaceMaterial.Brick,
                _ => SurfaceMaterial.Generic
            };
        }

        private string GenerateCacheKey(PropagationContext context)
        {
            return $"{context.TransmitterPosition}_{context.ReceiverPosition}_{context.FrequencyMHz:F0}";
        }
    }

    // Supporting data structures
    public enum SurfaceMaterial
    {
        Generic,
        Concrete,
        Metal,
        Glass,
        Brick,
        Ground
    }

    public static class MaterialProperties
    {
        public static readonly Dictionary<SurfaceMaterial, (float DielectricConstant, float Conductivity)> Properties =
            new Dictionary<SurfaceMaterial, (float, float)>
        {
            { SurfaceMaterial.Concrete, (6.0f, 0.01f) },
            { SurfaceMaterial.Metal, (1.0f, 1e7f) },
            { SurfaceMaterial.Glass, (6.0f, 1e-12f) },
            { SurfaceMaterial.Brick, (4.5f, 0.02f) },
            { SurfaceMaterial.Ground, (15.0f, 0.005f) },
            { SurfaceMaterial.Generic, (4.0f, 0.01f) }
        };
    }

    public static class SurfaceMaterialExtensions
    {
        public static float DielectricConstant(this SurfaceMaterial material)
        {
            return MaterialProperties.Properties.TryGetValue(material, out var props) ? props.DielectricConstant : 4.0f;
        }

        public static float Conductivity(this SurfaceMaterial material)
        {
            return MaterialProperties.Properties.TryGetValue(material, out var props) ? props.Conductivity : 0.01f;
        }
    }

    // Simple caching system
    public class RayTracingCache
    {
        private Dictionary<string, (float value, float timestamp)> cache = new Dictionary<string, (float, float)>();
        private const float CACHE_DURATION = 5f; // seconds

        public bool TryGetValue(string key, out float value)
        {
            if (cache.TryGetValue(key, out var entry) &&
                Time.time - entry.timestamp < CACHE_DURATION)
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