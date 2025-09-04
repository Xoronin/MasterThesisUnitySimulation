using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.PathLoss
{
    /// <summary>
    /// Evidence-based model selection based on ITU-R recommendations and academic literature
    /// References:
    /// - ITU-R P.1546: Method for point-to-area predictions
    /// - ITU-R P.526: Propagation by diffraction
    /// - Rappaport "Wireless Communications: Principles and Practice"
    /// - Molisch "Wireless Communications"
    /// </summary>
    public static class ModelSelectionCriteria
    {
        /// <summary>
        /// Select optimal propagation model based on ITU-R recommendations and literature
        /// </summary>
        public static PropagationModel SelectOptimalModel(PropagationContext context)
        {
            float distance = context.Distance;
            float frequency = context.FrequencyMHz;
            EnvironmentType environment = context.Environment;

            // Apply selection criteria based on established guidelines
            return ApplySelectionCriteria(distance, frequency, environment);
        }

        private static PropagationModel ApplySelectionCriteria(float distance, float frequency, EnvironmentType environment)
        {
            // CRITERIA 1: ITU-R Distance-based recommendations
            if (distance < 100f)
            {
                // Very short range: Free space or Two-ray appropriate
                // Reference: Rappaport, Chapter 4.3
                return frequency > 1000f ? PropagationModel.FreeSpace : PropagationModel.TwoRaySimple;
            }

            // CRITERIA 2: Hata Model applicability ranges (ITU-R P.370)
            if (IsWithinHataRange(distance, frequency))
            {
                return SelectHataVariant(frequency);
            }

            // CRITERIA 3: Two-Ray model for specific scenarios
            if (IsTwoRayApplicable(distance, frequency, environment))
            {
                return PropagationModel.TwoRayGroundReflection;
            }

            // CRITERIA 4: Log-distance for general cases
            // Reference: Rappaport, Chapter 4.4 - "When empirical models don't apply"
            return PropagationModel.LogDistance;
        }

        /// <summary>
        /// Check if parameters fall within Hata model's validated range
        /// Reference: ITU-R P.370-7, Hata original paper (1980)
        /// </summary>
        private static bool IsWithinHataRange(float distance, float frequency)
        {
            // Original Hata model validated ranges:
            // Frequency: 150-1500 MHz
            // Distance: 1-20 km
            // Reference: Hata, "Empirical formula for propagation loss in land mobile radio services"
            bool frequencyInRange = frequency >= 150f && frequency <= 1500f;
            bool distanceInRange = distance >= 1000f && distance <= 20000f;

            return frequencyInRange && distanceInRange;
        }

        /// <summary>
        /// Select appropriate Hata model variant
        /// Reference: COST 231 Final Report, Chapter 4
        /// </summary>
        private static PropagationModel SelectHataVariant(float frequency)
        {
            if (frequency <= 1500f)
            {
                // Original Hata model range
                // Reference: ITU-R P.370-7
                return PropagationModel.Hata;
            }
            else if (frequency <= 2000f)
            {
                // COST-231 Hata extension
                // Reference: COST 231 Final Report, valid 1500-2000 MHz
                return PropagationModel.COST231Hata;
            }
            else
            {
                // Above validated Hata range - use log-distance
                return PropagationModel.LogDistance;
            }
        }

        /// <summary>
        /// Determine if Two-Ray model is most appropriate
        /// Reference: Rappaport Chapter 4.5, ITU-R P.526
        /// </summary>
        private static bool IsTwoRayApplicable(float distance, float frequency, EnvironmentType environment)
        {
            // Two-ray is optimal when:
            // 1. Long distance where ground reflection dominates
            // 2. Relatively flat terrain (approximated by free space environment)
            // 3. Beyond Hata's validated range
            // Reference: Rappaport, Section 4.5.1

            bool isLongDistance = distance > 20000f; // Beyond Hata range
            bool isFlatTerrain = environment == EnvironmentType.FreeSpace;
            bool isReasonableFrequency = frequency >= 30f && frequency <= 3000f; // VHF to UHF range

            return isLongDistance && (isFlatTerrain || isReasonableFrequency);
        }

        /// <summary>
        /// Get model applicability information for debugging/validation
        /// </summary>
        public static ModelApplicabilityInfo GetApplicabilityInfo(PropagationContext context)
        {
            var info = new ModelApplicabilityInfo();

            // Check each model's applicability
            info.FreeSpace = CheckFreeSpaceApplicability(context);
            info.TwoRay = CheckTwoRayApplicability(context);
            info.Hata = CheckHataApplicability(context);
            info.COST231 = CheckCOST231Applicability(context);
            info.LogDistance = CheckLogDistanceApplicability(context);

            info.RecommendedModel = SelectOptimalModel(context);
            info.SelectionReason = GetSelectionReason(context, info.RecommendedModel);

            return info;
        }

        private static ModelValidityStatus CheckFreeSpaceApplicability(PropagationContext context)
        {
            // Free space: Line-of-sight, no obstacles
            // Reference: ITU-R P.525
            if (context.Distance < 50f)
                return new ModelValidityStatus(true, "Very short range, LOS assumption valid");
            else if (context.Distance > 1000f)
                return new ModelValidityStatus(false, "Distance too large for free space assumption");
            else
                return new ModelValidityStatus(true, "Suitable for LOS scenarios");
        }

        private static ModelValidityStatus CheckTwoRayApplicability(PropagationContext context)
        {
            // Two-ray: Ground reflection dominant
            // Reference: Rappaport Section 4.5
            bool distanceOk = context.Distance > 100f && context.Distance < 50000f;
            bool frequencyOk = context.FrequencyMHz >= 30f && context.FrequencyMHz <= 3000f;

            if (!distanceOk)
                return new ModelValidityStatus(false, $"Distance {context.Distance}m outside optimal range (100m-50km)");
            if (!frequencyOk)
                return new ModelValidityStatus(false, $"Frequency {context.FrequencyMHz}MHz outside validated range (30-3000MHz)");

            return new ModelValidityStatus(true, "Good for scenarios with dominant ground reflection");
        }

        private static ModelValidityStatus CheckHataApplicability(PropagationContext context)
        {
            // Original Hata model validation ranges
            // Reference: ITU-R P.370-7
            bool distanceOk = context.Distance >= 1000f && context.Distance <= 20000f;
            bool frequencyOk = context.FrequencyMHz >= 150f && context.FrequencyMHz <= 1500f;
            bool environmentOk = context.Environment == EnvironmentType.Urban;

            if (!distanceOk)
                return new ModelValidityStatus(false, $"Distance {context.Distance}m outside Hata range (1-20km)");
            if (!frequencyOk)
                return new ModelValidityStatus(false, $"Frequency {context.FrequencyMHz}MHz outside Hata range (150-1500MHz)");
            if (!environmentOk)
                return new ModelValidityStatus(false, "Hata model designed for urban environments");

            return new ModelValidityStatus(true, "Within Hata model's validated parameter space");
        }

        private static ModelValidityStatus CheckCOST231Applicability(PropagationContext context)
        {
            // COST-231 Hata extension
            // Reference: COST 231 Final Report
            bool distanceOk = context.Distance >= 1000f && context.Distance <= 20000f;
            bool frequencyOk = context.FrequencyMHz >= 1500f && context.FrequencyMHz <= 2000f;
            bool environmentOk = context.Environment == EnvironmentType.Urban;

            if (!distanceOk)
                return new ModelValidityStatus(false, $"Distance {context.Distance}m outside COST-231 range (1-20km)");
            if (!frequencyOk)
                return new ModelValidityStatus(false, $"Frequency {context.FrequencyMHz}MHz outside COST-231 range (1500-2000MHz)");
            if (!environmentOk)
                return new ModelValidityStatus(false, "COST-231 designed for urban environments");

            return new ModelValidityStatus(true, "Within COST-231 validated range for urban cellular");
        }

        private static ModelValidityStatus CheckLogDistanceApplicability(PropagationContext context)
        {
            // Log-distance: General purpose, always applicable
            // Reference: Rappaport Chapter 4.4
            if (context.Distance < 10f)
                return new ModelValidityStatus(false, "Too close - near-field effects");
            else if (context.Distance > 100000f)
                return new ModelValidityStatus(false, "Very long range - curvature effects");
            else
                return new ModelValidityStatus(true, "General-purpose model, widely applicable");
        }

        private static string GetSelectionReason(PropagationContext context, PropagationModel selectedModel)
        {
            return selectedModel switch
            {
                PropagationModel.FreeSpace => "Short range with line-of-sight assumption",
                PropagationModel.TwoRaySimple => "Short range with simple ground reflection",
                PropagationModel.TwoRayGroundReflection => "Long range beyond empirical model validity",
                PropagationModel.Hata => "Within original Hata validated range (150-1500MHz, 1-20km, urban)",
                PropagationModel.COST231Hata => "COST-231 extension for higher frequencies (1500-2000MHz)",
                PropagationModel.LogDistance => "General case - outside empirical model ranges",
                _ => "Default selection"
            };
        }
    }

    /// <summary>
    /// Information about model applicability for debugging and validation
    /// </summary>
    public class ModelApplicabilityInfo
    {
        public ModelValidityStatus FreeSpace;
        public ModelValidityStatus TwoRay;
        public ModelValidityStatus Hata;
        public ModelValidityStatus COST231;
        public ModelValidityStatus LogDistance;

        public PropagationModel RecommendedModel;
        public string SelectionReason;

        public void LogDebugInfo()
        {
            Debug.Log("=== MODEL APPLICABILITY ANALYSIS ===");
            Debug.Log($"Free Space: {FreeSpace.IsValid} - {FreeSpace.Reason}");
            Debug.Log($"Two-Ray: {TwoRay.IsValid} - {TwoRay.Reason}");
            Debug.Log($"Hata: {Hata.IsValid} - {Hata.Reason}");
            Debug.Log($"COST-231: {COST231.IsValid} - {COST231.Reason}");
            Debug.Log($"Log-Distance: {LogDistance.IsValid} - {LogDistance.Reason}");
            Debug.Log($"RECOMMENDED: {RecommendedModel} - {SelectionReason}");
        }
    }

    public struct ModelValidityStatus
    {
        public bool IsValid;
        public string Reason;

        public ModelValidityStatus(bool isValid, string reason)
        {
            IsValid = isValid;
            Reason = reason;
        }
    }
}