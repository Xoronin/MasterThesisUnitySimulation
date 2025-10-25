using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.PathLoss.Models;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;

namespace RFSimulation.Propagation.PathLoss
{
    /// <summary>
    ///  PathLossCalculator with integrated urban ray tracing support
    /// Assumes urban environment by default and includes all propagation models
    /// </summary>
    public class PathLossCalculator
    {
        [Header("Model Selection Settings")]
        public bool enableAutomaticModelSelection = true;
        public bool logModelSelectionReasons = false;

        [Header("Urban Settings")]
        public bool preferRayTracing = true;
        public bool fallbackToBasicModels = true;
        public float maxDistance = 2000f; // Beyond this, use empirical models
        public LayerMask mapboxBuildingLayer = 1 << 8;

        // Model dictionary with all available models
        private readonly Dictionary<PropagationModel, IPathLossModel> _models;
        private readonly PathLossCache _cache;
        private readonly IObstacleCalculator _obstacleCalculator;

        public PathLossCalculator(IObstacleCalculator obstacleCalculator = null)
        {
            _obstacleCalculator = obstacleCalculator;
            _cache = new PathLossCache();

            // Initialize all available models including urban ray tracing
            _models = new Dictionary<PropagationModel, IPathLossModel>
            {
                // Standard propagation models
                { PropagationModel.FreeSpace, new FreeSpaceModel() },
                { PropagationModel.LogDistance, new LogDistanceModel() },
                { PropagationModel.Hata, new HataModel() },
                { PropagationModel.COST231, new COST231HataModel() },
                
                // Ray tracing model
                { PropagationModel.RayTracing, new UrbanRayTracingModel() },
            };
        }

        public float CalculateReceivedPower(PropagationContext context)
        {
            // Validate input
            if (!context.IsValid(out string error))
            {
                Debug.LogWarning($"[PathLoss] Invalid context: {error}");
                return float.NegativeInfinity;
            }

            // Check cache first
            if (_cache.TryGetValue(context, out float cachedResult))
                return cachedResult;

            // Automatic model selection
            if (enableAutomaticModelSelection && context.Model == PropagationModel.Auto)
            {
                context.Model = SelectOptimalModel(context);
            }

            // Get appropriate model
            if (!_models.TryGetValue(context.Model, out IPathLossModel model))
            {
                context.Model = SelectOptimalModel(context);
                model = _models[context.Model];
            }

            float receivedPower;

            try
            {
                // Calculate base received power
                receivedPower = model.Calculate(context);

                // Apply urban-specific corrections for urban environment
                receivedPower = ApplyUrbanCorrections(receivedPower, context);

                // Add obstacle losses if available
                if (context.HasObstacles && _obstacleCalculator != null)
                {
                    float obstacleLoss = _obstacleCalculator.CalculatePenetrationLoss(context);
                    receivedPower -= obstacleLoss;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PathLoss] Error with {model.ModelName}: {e.Message}");

                if (fallbackToBasicModels)
                {
                    // Fallback to free space model
                    var fallbackModel = new FreeSpaceModel();
                    receivedPower = fallbackModel.Calculate(context);
                }
                else
                {
                    receivedPower = float.NegativeInfinity;
                }
            }

            // Cache result
            _cache.Store(context, receivedPower);
            return receivedPower;
        }

        private PropagationModel SelectOptimalModel(PropagationContext context)
        {
            if (preferRayTracing)
            {
                return SelectUrbanOptimizedModel(context);
            }
            else
            {
                return SelectStandardModel(context);
            }
        }

        private PropagationModel SelectUrbanOptimizedModel(PropagationContext context)
        {
            float distance = context.Distance;
            float frequency = context.FrequencyMHz;

            // For very short distances, use free space or two-ray
            if (distance < 100f)
            {
                return PropagationModel.FreeSpace;
            }

            // For medium distances in urban environment, use ray tracing
            if (distance <= maxDistance)
            {
                return PropagationModel.RayTracing;
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
            return PathLossModelSelection.SelectOptimalModel(context);
        }

        private float ApplyUrbanCorrections(float receivedPower, PropagationContext context)
        {
            // Apply urban-specific corrections since we're always in urban environment

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
            // Calculate building density loss for urban environment
            Vector3 midpoint = (context.TransmitterPosition + context.ReceiverPosition) * 0.5f;
            float sampleRadius = Mathf.Min(context.Distance * 0.3f, 200f);

            Collider[] buildings = Physics.OverlapSphere(midpoint, sampleRadius, mapboxBuildingLayer);

            if (buildings.Length == 0) return 2f; // Minimum urban loss even if no buildings detected

            // Calculate building density
            float sampleArea = Mathf.PI * sampleRadius * sampleRadius;
            float totalBuildingArea = 0f;

            foreach (var building in buildings)
            {
                Bounds bounds = building.bounds;
                totalBuildingArea += bounds.size.x * bounds.size.z;
            }

            float buildingDensity = Mathf.Clamp01(totalBuildingArea / sampleArea);

            // Convert to additional loss (2-7 dB based on density, always some urban loss)
            return 2f + (buildingDensity * 5f);
        }

        private float CalculateUrbanFrequencyFactor(float frequencyMHz)
        {
            // Urban environments cause more attenuation at higher frequencies
            if (frequencyMHz > 2400f) return 2f;    // 5G+ frequencies
            if (frequencyMHz > 1800f) return 1.5f;  // Higher LTE bands
            if (frequencyMHz > 900f) return 1f;     // Lower LTE bands
            return 0.5f; // Lower frequencies
        }

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
                return PropagationModel.COST231;
            }
            else
            {
                return PropagationModel.LogDistance;
            }
        }

        public SignalQualityCategory GetSignalQuality(PropagationContext context)
        {
            float receivedPower = CalculateReceivedPower(context);
            float margin = receivedPower - context.ReceiverSensitivityDbm;

            if (margin < 0f) return SignalQualityCategory.NoService;
            if (margin < 5f) return SignalQualityCategory.Poor;
            if (margin < 10f) return SignalQualityCategory.Fair;
            if (margin < 15f) return SignalQualityCategory.Good;
            return SignalQualityCategory.Excellent;
        }

        public SignalQualityMetrics GetSignalQualityMetrics(PropagationContext context)
        {
            float receivedPower = CalculateReceivedPower(context);

            // Estimate SINR from received power (simplified)
            float estimatedSINR = receivedPower - (-110f); // Assume -110dBm noise floor

            return new SignalQualityMetrics(estimatedSINR, context.Technology);
        }

        public float EstimateCoverageRadius(PropagationContext baseContext)
        {
            // Calculate coverage radius with urban considerations
            float txPower = baseContext.TransmitterPowerDbm;
            float txGain = baseContext.AntennaGainDbi;
            float frequency = baseContext.FrequencyMHz;
            float sensitivity = baseContext.ReceiverSensitivityDbm;
            float connectionMargin = 10f; // Standard margin

            // Calculate available link budget
            float linkBudget = txPower + txGain - (sensitivity + connectionMargin);

            // Use path loss model parameters
            float pathLossExponent = RFConstants.PATH_LOSS_EXPONENT;
            float referenceDistance = RFConstants.REFERENCE_DISTANCE;

            // Calculate reference path loss at reference distance
            float referenceLoss = CalculateReferencePathLoss(frequency, referenceDistance);

            // Available path loss budget beyond reference distance
            float additionalLoss = linkBudget - referenceLoss;

            if (additionalLoss <= 0)
            {
                return referenceDistance * 0.5f;
            }

            // Calculate coverage using log-distance model
            float distanceRatio = Mathf.Pow(10f, additionalLoss / (10f * pathLossExponent));
            float coverageRadius = referenceDistance * distanceRatio;

            // Apply urban-specific reductions (always urban environment)
            coverageRadius = ApplyUrbanCoverageReductions(coverageRadius, baseContext);

            // Apply realistic limits
            return Mathf.Clamp(coverageRadius, 50f, 2000f); // Urban coverage typically limited
        }

        private float ApplyUrbanCoverageReductions(float baseCoverage, PropagationContext context)
        {
            // Urban environments typically reduce coverage by ~40%
            float urbanReductionFactor = 0.6f;

            // Additional reduction based on building density
            float densityReduction = CalculateBuildingDensityLoss(context) / 10f;
            urbanReductionFactor -= densityReduction * 0.1f;

            float urbanCoverage = baseCoverage * urbanReductionFactor;

            // Apply frequency-specific urban limitations
            float frequencyFactor = GetUrbanFrequencyFactor(context.FrequencyMHz);
            urbanCoverage *= frequencyFactor;

            return urbanCoverage;
        }

        private float GetUrbanFrequencyFactor(float frequencyMHz)
        {
            // Higher frequencies have more limited urban coverage
            if (frequencyMHz > 3500f) return 0.7f;  // 5G frequencies
            if (frequencyMHz > 2400f) return 0.8f;  // Upper cellular bands
            if (frequencyMHz > 1800f) return 0.9f;  // Mid cellular bands
            return 1.0f; // Lower frequencies penetrate urban environments better
        }

        private float CalculateReferencePathLoss(float frequencyMHz, float referenceDistance)
        {
            // Use free space path loss at reference distance
            float distanceKm = referenceDistance / 1000f;
            return 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequencyMHz) + 32.45f;
        }

        public UrbanRayTracingModel GetRayTracingModel()
        {
            if (_models.TryGetValue(PropagationModel.RayTracing, out IPathLossModel model))
            {
                return model as UrbanRayTracingModel;
            }
            return null;
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

        public (int entries, float hitRate) GetCacheStats()
        {
            return _cache.GetStats();
        }

        // Validation and debugging methods

        public void DebugCoverageCalculation(PropagationContext context)
        {
            Debug.Log("=== URBAN COVERAGE CALCULATION DEBUG ===");
            Debug.Log($"TX Power: {context.TransmitterPowerDbm:F1} dBm");
            Debug.Log($"TX Gain: {context.AntennaGainDbi:F1} dBi");
            Debug.Log($"Frequency: {context.FrequencyMHz:F0} MHz");
            Debug.Log($"Sensitivity: {context.ReceiverSensitivityDbm:F1} dBm");

            float linkBudget = context.TransmitterPowerDbm + context.AntennaGainDbi - (context.ReceiverSensitivityDbm + 10f);
            Debug.Log($"Link Budget: {linkBudget:F1} dB");

            float buildingLoss = CalculateBuildingDensityLoss(context);
            Debug.Log($"Building Density Loss: {buildingLoss:F1} dB");

            float frequencyLoss = CalculateUrbanFrequencyFactor(context.FrequencyMHz);
            Debug.Log($"Urban Frequency Factor: {frequencyLoss:F1} dB");

            float coverage = EstimateCoverageRadius(context);
            Debug.Log($"URBAN COVERAGE RADIUS: {coverage:F0} meters");
        }
    }
}