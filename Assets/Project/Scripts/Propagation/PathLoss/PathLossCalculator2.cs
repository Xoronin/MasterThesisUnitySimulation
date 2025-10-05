//using UnityEngine;
//using System.Collections.Generic;
//using RFSimulation.Interfaces;
//using RFSimulation.Propagation.PathLoss.Models;
//using RFSimulation.Propagation.Core;
//using RFSimulation.Propagation.SignalQuality;

//namespace RFSimulation.Propagation.PathLoss
//{
//    public class PathLossCalculator
//    {
//        private readonly Dictionary<PropagationModel, IPathLossModel> _models;
//        private readonly PathLossCache _cache;
//        private readonly IObstacleCalculator _obstacleCalculator;

//        [Header("Model Selection Settings")]
//        public bool enableAutomaticModelSelection = true;
//        public bool logModelSelectionReasons = false;

//        public PathLossCalculator(IObstacleCalculator obstacleCalculator = null)
//        {
//            _models = new Dictionary<PropagationModel, IPathLossModel>
//            {
//                { PropagationModel.FreeSpace, new FreeSpaceModel() },
//                { PropagationModel.LogDistance, new LogDistanceModel() },
//                { PropagationModel.TwoRaySimple, new TwoRaySimpleModel() },
//                { PropagationModel.TwoRayGroundReflection, new TwoRayGroundReflectionModel() },
//                { PropagationModel.Hata, new HataModel() },                    
//                { PropagationModel.COST231Hata, new COST231HataModel() }       
//            };

//            _cache = new PathLossCache();
//            _obstacleCalculator = obstacleCalculator;
//        }

//        public float CalculateReceivedPower(PropagationContext context)
//        {
//            // Validate input
//            if (!context.IsValid(out string error))
//            {
//                return float.NegativeInfinity;
//            }

//            // AUTOMATIC MODEL SELECTION - Choose best model for scenario
//            if (enableAutomaticModelSelection && context.Model == PropagationModel.Auto)
//            {
//                context.Model = SelectPropagationModel(context);
//            }

//            // Check cache first
//            if (_cache.TryGetValue(context, out float cachedResult))
//                return cachedResult;

//            // Get appropriate model
//            if (!_models.TryGetValue(context.Model, out IPathLossModel model))
//            {
//                context.Model = SelectPropagationModel(context);
//                model = _models[context.Model];
//            }

//            // Calculate base received power
//            float receivedPower = model.Calculate(context);

//            // Add obstacle losses if available
//            if (context.HasObstacles && _obstacleCalculator != null)
//            {
//                float obstacleLoss = _obstacleCalculator.CalculatePenetrationLoss(context);
//                receivedPower -= obstacleLoss; // Subtract loss from received power
//            }

//            // Cache result
//            _cache.Store(context, receivedPower);

//            return receivedPower;
//        }

//        /// <summary>
//        /// Evidence-based model selection using established criteria
//        /// </summary>
//        private PropagationModel SelectPropagationModel(PropagationContext context)
//        {
//            var selectedModel = ModelSelectionCriteria.SelectOptimalModel(context);

//            if (logModelSelectionReasons)
//            {
//                var applicabilityInfo = ModelSelectionCriteria.GetApplicabilityInfo(context);

//                // Detailed analysis if needed
//                if (Application.isEditor)
//                {
//                    applicabilityInfo.LogDebugInfo();
//                }
//            }

//            return selectedModel;
//        }

//        public SignalQualityCategory GetSignalQuality(PropagationContext context)
//        {
//            float receivedPower = CalculateReceivedPower(context);
//            float margin = receivedPower - context.ReceiverSensitivityDbm;

//            // Use the same logic but return SignalQualityCategory
//            if (margin < 0f) return SignalQualityCategory.NoService;
//            if (margin < 5f) return SignalQualityCategory.Poor;
//            if (margin < 10f) return SignalQualityCategory.Fair;
//            if (margin < 15f) return SignalQualityCategory.Good;
//            return SignalQualityCategory.Excellent;
//        }

//        public SignalQualityMetrics GetSignalQualityMetrics(PropagationContext context)
//        {
//            float receivedPower = CalculateReceivedPower(context);

//            // Estimate SINR from received power (simplified)
//            float estimatedSINR = receivedPower - (-110f); // Assume -110dBm noise floor

//            return new SignalQualityMetrics(estimatedSINR, context.Technology);
//        }

//        public float EstimateCoverageRadius(PropagationContext baseContext)
//        {
//            // Use deterministic calculation based on link budget
//            // This ensures the same transmitter always has the same coverage

//            float txPower = baseContext.TransmitterPowerDbm;
//            float txGain = baseContext.AntennaGainDbi;
//            float frequency = baseContext.FrequencyMHz;
//            float sensitivity = baseContext.ReceiverSensitivityDbm;
//            float connectionMargin = 10f; // Standard margin

//            // Calculate available link budget
//            float linkBudget = txPower + txGain - (sensitivity + connectionMargin);

//            // Use path loss model parameters directly (no randomness)
//            float pathLossExponent = RFConstants.PATH_LOSS_EXPONENT;
//            float referenceDistance = RFConstants.REFERENCE_DISTANCE;

//            // Calculate reference path loss at reference distance (typically 100m)
//            float referenceLoss = CalculateReferencePathLoss(frequency, referenceDistance);

//            // Available path loss budget beyond reference distance
//            float additionalLoss = linkBudget - referenceLoss;

//            if (additionalLoss <= 0)
//            {
//                // Coverage doesn't even reach reference distance
//                return referenceDistance * 0.5f;
//            }

//            // Calculate coverage using log-distance model
//            // PathLoss = ReferenceLoss + 10*n*log10(d/d0)
//            // Solving for d: d = d0 * 10^((PathLoss - ReferenceLoss)/(10*n))

//            float distanceRatio = Mathf.Pow(10f, additionalLoss / (10f * pathLossExponent));
//            float coverageRadius = referenceDistance * distanceRatio;

//            // Apply realistic limits
//            coverageRadius = Mathf.Clamp(coverageRadius, 50f, 5000f);

//            // Apply environment-specific reduction factors (deterministic)
//            float environmentFactor = 0.75f;
//            coverageRadius *= environmentFactor;

//            return coverageRadius;
//        }

//        private float CalculateReferencePathLoss(float frequencyMHz, float referenceDistance)
//        {
//            // Use free space path loss at reference distance
//            // FSPL = 20*log10(d) + 20*log10(f) + 32.45 (d in km, f in MHz)
//            float distanceKm = referenceDistance / 1000f;
//            return 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequencyMHz) + 32.45f;
//        }

//        /// Validate model selection against established criteria
//        /// </summary>
//        [ContextMenu("Validate Model Selection")]
//        public void ValidateModelSelection(PropagationContext context)
//        {
//            var applicabilityInfo = ModelSelectionCriteria.GetApplicabilityInfo(context);
//            applicabilityInfo.LogDebugInfo();
//        }

//        [ContextMenu("Debug Coverage Calculation")]
//        public void DebugCoverageCalculation(PropagationContext context)
//        {
//            Debug.Log("=== COVERAGE CALCULATION DEBUG ===");
//            Debug.Log($"TX Power: {context.TransmitterPowerDbm:F1} dBm");
//            Debug.Log($"TX Gain: {context.AntennaGainDbi:F1} dBi");
//            Debug.Log($"Frequency: {context.FrequencyMHz:F0} MHz");
//            Debug.Log($"Sensitivity: {context.ReceiverSensitivityDbm:F1} dBm");

//            float linkBudget = context.TransmitterPowerDbm + context.AntennaGainDbi - (context.ReceiverSensitivityDbm + 10f);
//            Debug.Log($"Link Budget: {linkBudget:F1} dB");

//            float pathLossExponent = RFConstants.PATH_LOSS_EXPONENT;
//            Debug.Log($"Path Loss Exponent: {pathLossExponent:F1}");

//            float coverage = EstimateCoverageRadius(context);
//            Debug.Log($"COVERAGE RADIUS: {coverage:F0} meters");
//        }


//        public void ClearCache() => _cache.Clear();
//        public (int entries, float hitRate) GetCacheStats() => _cache.GetStats();
//    }
//}