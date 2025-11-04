using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.PathLoss.Models;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Visualization;

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
        public float maxDistance = 2000f; 
        public LayerMask mapboxBuildingLayer = 8;

        // Model dictionary with all available models
        private readonly Dictionary<PropagationModel, IPathLossModel> _models;
        private readonly PathLossCache _cache;

        public RayVisualization RayViz { get; set; }

        public PathLossCalculator()
        {
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
                { PropagationModel.RayTracing, new RayTracingModel() },
            };

            ConfigureRayTracingModel();

        }

        private void ConfigureRayTracingModel()
        {
            var rt = GetRayTracingModel();
            if (rt == null) return;

            // Propagate common settings
            rt.maxDistance = maxDistance;
            rt.mapboxBuildingLayer = mapboxBuildingLayer;

            // Enable viz and hook the shared visualizer
            rt.enableRayVisualization = true;
            rt.showDirectRays = true;
            rt.showReflectionRays = true;
            rt.showDiffractionRays = true;

            // Prefer an explicitly assigned visualizer; otherwise find one in the scene
            rt.Visualizer = RayViz ?? UnityEngine.Object.FindAnyObjectByType<RayVisualization>();
        }

        public float CalculateReceivedPower(PropagationContext context)
        {
            // Validate
            if (!context.IsValid(out string error))
            {
                Debug.LogWarning($"[PathLoss] Invalid context: {error}");
                return float.NegativeInfinity;
            }

            // hit the cache with the model baked into the key
            if (_cache.TryGetValue(context, out float cachedResult))
                return cachedResult;

            float receivedPowerDbm;
            IPathLossModel model = null;
            try
            {
                if (!_models.TryGetValue(context.Model, out model))
                {
                    Debug.LogWarning($"[PathLoss] No path-loss model registered for {context.Model}. Falling back.");
                    if (fallbackToBasicModels)
                    {
                        float fallbackOut = new FreeSpaceModel().CalculatePathLoss(context);
                        receivedPowerDbm = PathLossToReceivedPower(fallbackOut, context);
                    }
                    else
                    {
                        receivedPowerDbm = float.NegativeInfinity;
                    }
                    _cache.Store(context, receivedPowerDbm);
                    return receivedPowerDbm;
                }

                // Call the model
                float pathLossDb = model.CalculatePathLoss(context);
                if (float.IsNaN(pathLossDb) || float.IsNegativeInfinity(pathLossDb)) pathLossDb = float.PositiveInfinity;

                receivedPowerDbm = PathLossToReceivedPower(pathLossDb, context);
                if (float.IsPositiveInfinity(pathLossDb)) receivedPowerDbm = float.NegativeInfinity;

            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PathLoss] Error with {model.ModelName}: {e.Message}");
                if (fallbackToBasicModels)
                {
                    float fallbackOut = new FreeSpaceModel().CalculatePathLoss(context);
                    receivedPowerDbm = PathLossToReceivedPower(fallbackOut, context);
                }
                else
                {
                    receivedPowerDbm = float.NegativeInfinity;
                }
            }

            _cache.Store(context, receivedPowerDbm);
            return receivedPowerDbm;
        }

        public float PathLossToReceivedPower(float pathLossDb, PropagationContext context)
        {
            float txDbm = context.TransmitterPowerDbm;
            float txGainDbi = context.AntennaGainDbi;
            float rxGainDbi = context.ReceiverGainDbi;
            return txDbm + txGainDbi + rxGainDbi - pathLossDb;
        }


        // not used
        //public SignalQualityCategory GetSignalQuality(PropagationContext context)
        //{
        //    float receivedPower = CalculateReceivedPower(context);
        //    float margin = receivedPower - context.ReceiverSensitivityDbm;

        //    if (margin < 0f) return SignalQualityCategory.NoService;
        //    if (margin < 5f) return SignalQualityCategory.Poor;
        //    if (margin < 10f) return SignalQualityCategory.Fair;
        //    if (margin < 15f) return SignalQualityCategory.Good;
        //    return SignalQualityCategory.Excellent;
        //}

        //// not used

        //public SignalQualityMetrics GetSignalQualityMetrics(PropagationContext context)
        //{
        //    float receivedPower = CalculateReceivedPower(context);

        //    // Estimate SINR from received power (simplified)
        //    float estimatedSINR = receivedPower - (-110f); // Assume -110dBm noise floor

        //    return new SignalQualityMetrics(estimatedSINR, context.Technology);
        //}

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
            float pathLossExponent = RFConstants.PATH_LOSS_EXPONENT_URBAN;
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

            // Apply realistic limits
            return Mathf.Clamp(coverageRadius, 50f, 2000f); // Urban coverage typically limited
        }

        private float CalculateReferencePathLoss(float frequencyMHz, float referenceDistance)
        {
            // Use free space path loss at reference distance
            float distanceKm = referenceDistance / 1000f;
            return 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequencyMHz) + 32.45f;
        }

        public RayTracingModel GetRayTracingModel()
        {
            if (_models.TryGetValue(PropagationModel.RayTracing, out IPathLossModel model))
            {
                return model as RayTracingModel;
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
    }
}