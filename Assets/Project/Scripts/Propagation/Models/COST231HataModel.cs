using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.Models
{

    public class COST231HataModel : IPathLossModel
    {
        public string ModelName => "COST-231";

        public float CalculatePathLoss(PropagationContext context)
        {
            // Extract parameters
            float tansmitterHeight = context.TransmitterHeight;
            float receiverHeight = context.ReceiverHeight;
            float frequency = context.FrequencyMHz;
            float distance = context.Distance / 1000f;
            float CM = RFConstants.METROPOLITAN_CORRECTION;

            var validation = ValidateCOST231Parameters(context);
            if (!validation.isValid)
            {
                Debug.LogWarning($"[COST-321Model] {validation.warning}");
            }

            // Calculate mobile antenna correction factor (same as original Hata)
            float mobileCorrection = CalculateLargeCityCorrection(frequency, receiverHeight);

            float pathLoss = 46.3f +
                           33.9f * Mathf.Log10(frequency) -
                           13.82f * Mathf.Log10(tansmitterHeight) -
                           mobileCorrection +
                           (44.9f - 6.55f * Mathf.Log10(tansmitterHeight)) * Mathf.Log10(distance) +
                           CM;

            // Validate result
            if (float.IsInfinity(pathLoss) || float.IsNaN(pathLoss))
            {
                return float.NegativeInfinity;
            }

            return pathLoss;
        }

        private (bool isValid, string warning) ValidateCOST231Parameters(PropagationContext context)
        {
            string warnings = "";
            bool isValid = true;

            // Frequency range: 1500 - 2000 MHz 
            if (context.FrequencyMHz < 1500f || context.FrequencyMHz > 2000f)
            {
                warnings += $"Frequency {context.FrequencyMHz}MHz outside valid range (1500-2000MHz). ";
                isValid = false;
            }

            // Distance range: 1 - 20 km
            float distanceKm = context.Distance / 1000f;
            if (distanceKm < 1f || distanceKm > 20f)
            {
                warnings += $"Distance {distanceKm:F1}km outside valid range (1-20km). ";
                isValid = false;
            }

            // Base station height: 30 - 200 m 
            if (context.TransmitterHeight < 30f || context.TransmitterHeight > 200f)
            {
                warnings += $"Base station height {context.TransmitterHeight}m outside typical range (30-200m). ";
            }

            // Mobile station height: 1 - 10 m
            if (context.ReceiverHeight < 1f || context.ReceiverHeight > 10f)
            {
                warnings += $"Mobile height {context.ReceiverHeight}m outside valid range (1-10m). ";
            }

            return (isValid, warnings);
        }

        /// <summary>
        /// Alternative mobile antenna correction for large cities 
        /// </summary>
        private float CalculateLargeCityCorrection(float frequencyMHz, float mobileHeight)
        {
            if (frequencyMHz <= 300f)
            {
                return 8.29f * Mathf.Pow(Mathf.Log10(1.54f * mobileHeight), 2f) - 1.1f;
            }
            else
            {
                return 3.2f * Mathf.Pow(Mathf.Log10(11.75f * mobileHeight), 2f) - 4.97f;
            }
        }

        private bool IsValidForCOST231(PropagationContext context)
        {
            float distanceKm = context.Distance / 1000f;
            return context.FrequencyMHz >= 1500f && context.FrequencyMHz <= 2000f &&
                   distanceKm >= 1f && distanceKm <= 20f;
        }
    }
}