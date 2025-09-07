using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.PathLoss.Models;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;

namespace RFSimulation.Propagation.PathLoss
{
    /// <summary>
    /// Enhanced PathLossCalculator with urban ray tracing support for Mapbox environments
    /// Uses composition instead of inheritance to avoid virtual method issues
    /// </summary>
    public class UrbanPathLossCalculator
    {
        [Header("Urban Environment Detection")]
        public bool autoDetectUrbanEnvironment = true;
        public float urbanDetectionRadius = 500f;
        public int minimumBuildingsForUrban = 5;
        public LayerMask mapboxBuildingLayer = 1 << 8;

        [Header("Urban Model Selection")]
        public bool preferUrbanRayTracing = true;
        public bool fallbackToBasicModels = true;
        public float maxUrbanDistance = 2000f; // Beyond this, use empirical models

        // Base calculator for fallback functionality
        private readonly PathLossCalculator baseCalculator;

        // Enhanced model dictionary with urban models
        private readonly Dictionary<PropagationModel, IPathLossModel> urbanModels;
        private readonly PathLossCache urbanCache;

        public UrbanPathLossCalculator(IObstacleCalculator obstacleCalculator = null)
        {
            // Initialize base calculator
            baseCalculator = new PathLossCalculator(obstacleCalculator);

            // Initialize urban cache
            urbanCache = new PathLossCache();

            // Initialize urban-specific models
            urbanModels = new Dictionary<PropagationModel, IPathLossModel>
            {
                // Keep original models
                { PropagationModel.FreeSpace, new FreeSpaceModel() },
                { PropagationModel.LogDistance, new LogDistanceModel() },
                { PropagationModel.TwoRaySimple, new TwoRaySimpleModel() },
                { PropagationModel.TwoRayGroundReflection, new TwoRayGroundReflectionModel() },
                { PropagationModel.Hata, new HataModel() },
                { PropagationModel.COST231Hata, new COST231HataModel() },
                
                // Add urban ray tracing models
                { PropagationModel.BasicRayTracing, new UrbanRayTracingModel() },
                { PropagationModel.AdvancedRayTracing, new UrbanRayTracingModel() }
            };
        }

        public float CalculateReceivedPower(PropagationContext context)
        {
            // Validate input
            if (!context.IsValid(out string error))
            {
                Debug.LogWarning($"[UrbanPathLoss] Invalid context: {error}");
                return float.NegativeInfinity;
            }

            // Check cache first
            string cacheKey = GenerateUrbanCacheKey(context);
            if (urbanCache.TryGetValue(context, out float cachedResult))
            {
                return cachedResult;
            }

            // Enhanced model selection for urban environments
            if (context.Model == PropagationModel.Auto)
            {
                context.Model = SelectUrbanOptimizedModel(context);
            }

            float result;

            // Use urban model if available, fallback to base implementation
            if (urbanModels.TryGetValue(context.Model, out IPathLossModel model))
            {
                result = CalculateWithUrbanModel(model, context);
            }
            else
            {
                // Fallback to base calculator
                result = baseCalculator.CalculateReceivedPower(context);
            }

            // Cache the result
            urbanCache.Store(context, result);
            return result;
        }

        private PropagationModel SelectUrbanOptimizedModel(PropagationContext context)
        {
            // Detect if we're in an urban environment
            bool isUrbanEnvironment = DetectUrbanEnvironment(context);

            if (isUrbanEnvironment && preferUrbanRayTracing)
            {
                return SelectUrbanRayTracingModel(context);
            }
            else
            {
                // Use original model selection logic for non-urban environments
                return SelectStandardModel(context);
            }
        }

        private bool DetectUrbanEnvironment(PropagationContext context)
        {
            if (!autoDetectUrbanEnvironment) return false;

            // Count nearby buildings using Mapbox layer
            Vector3 searchCenter = (context.TransmitterPosition + context.ReceiverPosition) * 0.5f;
            Collider[] nearbyBuildings = Physics.OverlapSphere(searchCenter, urbanDetectionRadius, mapboxBuildingLayer);

            int validBuildings = 0;
            foreach (var building in nearbyBuildings)
            {
                // Validate it's actually a building (has reasonable height)
                Renderer renderer = building.GetComponent<Renderer>();
                if (renderer != null && renderer.bounds.size.y > 2f)
                {
                    validBuildings++;
                }
            }

            bool isUrban = validBuildings >= minimumBuildingsForUrban;

            if (isUrban)
            {
                Debug.Log($"[UrbanPathLoss] Urban environment detected: {validBuildings} buildings in {urbanDetectionRadius}m radius");
            }

            return isUrban;
        }

        private PropagationModel SelectUrbanRayTracingModel(PropagationContext context)
        {
            float distance = context.Distance;
            float frequency = context.FrequencyMHz;

            // Decision logic based on paper recommendations and computational constraints

            // For very short urban distances, use free space or two-ray
            if (distance < 100f)
            {
                return frequency > 1000f ? PropagationModel.FreeSpace : PropagationModel.TwoRaySimple;
            }

            // For medium urban distances, use ray tracing if computationally feasible
            if (distance <= maxUrbanDistance)
            {
                // Use basic ray tracing for most urban scenarios
                return PropagationModel.BasicRayTracing;
            }

            // For long distances, fallback to empirical models for performance
            if (IsWithinHataRange(distance, frequency))
            {
                return SelectHataVariant(frequency);
            }

            // Ultimate fallback
            return PropagationModel.LogDistance;
        }

        private PropagationModel SelectStandardModel(PropagationContext context)
        {
            // Use the model selection logic from ModelSelectionCriteria
            return ModelSelectionCriteria.SelectOptimalModel(context);
        }

        private float CalculateWithUrbanModel(IPathLossModel model, PropagationContext context)
        {
            try
            {
                float receivedPower = model.Calculate(context);

                // Apply urban-specific corrections if needed
                receivedPower = ApplyUrbanCorrections(receivedPower, context);

                return receivedPower;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UrbanPathLoss] Error with {model.ModelName}: {e.Message}");

                if (fallbackToBasicModels)
                {
                    // Fallback to free space model
                    var fallbackModel = new FreeSpaceModel();
                    return fallbackModel.Calculate(context);
                }

                return float.NegativeInfinity;
            }
        }

        private float ApplyUrbanCorrections(float receivedPower, PropagationContext context)
        {
            // Apply urban-specific corrections based on paper recommendations

            // 1. Building density correction
            float buildingDensityLoss = CalculateBuildingDensityLoss(context);
            receivedPower -= buildingDensityLoss;

            // 2. Frequency-dependent urban effects
            float frequencyUrbanFactor = CalculateUrbanFrequencyFactor(context.FrequencyMHz);
            receivedPower -= frequencyUrbanFactor;

            return receivedPower;
        }

        private float CalculateBuildingDensityLoss(PropagationContext context)
        {
            // Simplified building density loss calculation
            Vector3 midpoint = (context.TransmitterPosition + context.ReceiverPosition) * 0.5f;
            float sampleRadius = Mathf.Min(context.Distance * 0.3f, 200f);

            Collider[] buildings = Physics.OverlapSphere(midpoint, sampleRadius, mapboxBuildingLayer);

            if (buildings.Length == 0) return 0f;

            // Calculate building density
            float sampleArea = Mathf.PI * sampleRadius * sampleRadius;
            float totalBuildingArea = 0f;

            foreach (var building in buildings)
            {
                Bounds bounds = building.bounds;
                totalBuildingArea += bounds.size.x * bounds.size.z;
            }

            float buildingDensity = Mathf.Clamp01(totalBuildingArea / sampleArea);

            // Convert to additional loss (0-5 dB based on density)
            return buildingDensity * 5f;
        }

        private float CalculateUrbanFrequencyFactor(float frequencyMHz)
        {
            // Urban environments cause more attenuation at higher frequencies
            if (frequencyMHz > 2400f) return 2f;    // 5G+ frequencies
            if (frequencyMHz > 1800f) return 1.5f;  // Higher LTE bands
            if (frequencyMHz > 900f) return 1f;     // Lower LTE bands
            return 0.5f; // Lower frequencies
        }

        // Helper methods from original ModelSelectionCriteria
        private bool IsWithinHataRange(float distance, float frequency)
        {
            bool frequencyInRange = frequency >= 150f && frequency <= 1500f;
            bool distanceInRange = distance >= 1000f && distance <= 20000f;
            return frequencyInRange && distanceInRange;
        }

        private PropagationModel SelectHataVariant(float frequency)
        {
            if (frequency <= 1500f)
            {
                return PropagationModel.Hata;
            }
            else if (frequency <= 2000f)
            {
                return PropagationModel.COST231Hata;
            }
            else
            {
                return PropagationModel.LogDistance;
            }
        }

        // Enhanced coverage calculation for urban environments
        public float EstimateCoverageRadius(PropagationContext baseContext)
        {
            bool isUrban = DetectUrbanEnvironment(baseContext);

            if (isUrban)
            {
                return EstimateUrbanCoverageRadius(baseContext);
            }
            else
            {
                return baseCalculator.EstimateCoverageRadius(baseContext);
            }
        }

        private float EstimateUrbanCoverageRadius(PropagationContext baseContext)
        {
            // Start with base calculation
            float baseCoverage = baseCalculator.EstimateCoverageRadius(baseContext);

            // Apply urban-specific reductions
            float urbanReductionFactor = 0.6f; // Urban environments typically reduce coverage by ~40%

            // Additional reduction based on building density
            float densityReduction = CalculateBuildingDensityLoss(baseContext) / 10f; // Convert dB loss to ratio
            urbanReductionFactor -= densityReduction * 0.1f; // Further reduce coverage based on density

            float urbanCoverage = baseCoverage * urbanReductionFactor;

            // Apply frequency-specific urban limitations
            float frequencyFactor = GetUrbanFrequencyFactor(baseContext.FrequencyMHz);
            urbanCoverage *= frequencyFactor;

            // Ensure reasonable bounds for urban coverage
            return Mathf.Clamp(urbanCoverage, 50f, 1000f);
        }

        private float GetUrbanFrequencyFactor(float frequencyMHz)
        {
            // Higher frequencies have more limited urban coverage
            if (frequencyMHz > 3500f) return 0.7f;  // 5G frequencies
            if (frequencyMHz > 2400f) return 0.8f;  // Upper cellular bands
            if (frequencyMHz > 1800f) return 0.9f;  // Mid cellular bands
            return 1.0f; // Lower frequencies penetrate urban environments better
        }

        // Delegate methods to base calculator
        public SignalQualityCategory GetSignalQuality(PropagationContext context)
        {
            return baseCalculator.GetSignalQuality(context);
        }

        public SignalQualityMetrics GetSignalQualityMetrics(PropagationContext context)
        {
            return baseCalculator.GetSignalQualityMetrics(context);
        }

        public void ClearCache()
        {
            urbanCache.Clear();
            baseCalculator.ClearCache();
        }

        public (int entries, float hitRate) GetCacheStats()
        {
            return urbanCache.GetStats();
        }

        private string GenerateUrbanCacheKey(PropagationContext context)
        {
            return $"urban_{context.TransmitterPosition}_{context.ReceiverPosition}_{context.FrequencyMHz:F0}";
        }

        // Debug and validation methods
        [ContextMenu("Test Urban Detection")]
        public void TestUrbanDetection()
        {
            // Find a test point in the scene
            var transmitters = Object.FindObjectsByType<RFSimulation.Core.Components.Transmitter>(FindObjectsSortMode.None);
            if (transmitters.Length > 0)
            {
                var tx = transmitters[0];
                var context = PropagationContext.Create(
                    tx.transform.position,
                    tx.transform.position + Vector3.forward * 500f,
                    tx.transmitterPower,
                    tx.frequency
                );

                bool isUrban = DetectUrbanEnvironment(context);
                Debug.Log($"[UrbanPathLoss] Test location urban status: {isUrban}");

                var selectedModel = SelectUrbanOptimizedModel(context);
                Debug.Log($"[UrbanPathLoss] Selected model: {selectedModel}");
            }
        }

        [ContextMenu("Debug Urban Models")]
        public void DebugUrbanModels()
        {
            Debug.Log("=== URBAN PATHLOSS CALCULATOR DEBUG ===");
            Debug.Log($"Available urban models: {urbanModels.Count}");

            foreach (var model in urbanModels)
            {
                Debug.Log($"  {model.Key}: {model.Value.ModelName}");
            }

            Debug.Log($"Auto-detect urban: {autoDetectUrbanEnvironment}");
            Debug.Log($"Urban detection radius: {urbanDetectionRadius}m");
            Debug.Log($"Minimum buildings for urban: {minimumBuildingsForUrban}");
            Debug.Log($"Prefer urban ray tracing: {preferUrbanRayTracing}");
            Debug.Log($"Max urban distance: {maxUrbanDistance}m");
        }
    }
}