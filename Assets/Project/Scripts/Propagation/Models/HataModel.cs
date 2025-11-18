using UnityEngine;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;
using RFSimulation.Utils;

namespace RFSimulation.Propagation.Models
{
    public class HataModel : IPathLossModel
    {
        public string ModelName => "Hata";

        public float CalculatePathLoss(PropagationContext context)
        { 
            // Validate parameters according to Hata model limitations
            var validation = ValidateHataParameters(context);
            if (!validation.isValid)
            {
                Debug.LogWarning($"[HataModel] {validation.warning}");
            }

            float hte = context.TransmitterHeight;
            float hre = context.ReceiverHeight;
            float fc = context.FrequencyMHz;
            float d = UnitConversionHelper.mToKm(context.DistanceM);

            float mobileCorrection = CalculateLargeCityCorrection(fc, hre);

            float pathLoss = 69.55f +
                           26.16f * Mathf.Log10(fc) -                    
                           13.82f * Mathf.Log10(hte) -
                           mobileCorrection +                              
                           (44.9f - 6.55f * Mathf.Log10(hte)) * Mathf.Log10(d);

            // Validate result
            return float.IsInfinity(pathLoss) || float.IsNaN(pathLoss)
                ? float.NegativeInfinity
                : pathLoss;
        }

        private (bool isValid, string warning) ValidateHataParameters(PropagationContext context)
        {
            string warnings = "";
            bool isValid = true;

            // Frequency range: 150 - 1500 MHz 
            if (context.FrequencyMHz < 150f || context.FrequencyMHz > 1500f)
            {
                warnings += $"Frequency {context.FrequencyMHz}MHz outside valid range (150-1500MHz). ";
                isValid = false;
            }

            // Distance range: 1 - 20 km
            float distanceKm = UnitConversionHelper.mToKm(context.DistanceM);
            if (distanceKm < 1f || distanceKm > 20f)
            {
                warnings += $"Distance {distanceKm:F1}km outside valid range (1-20km). ";
                isValid = false;
            }

            // Base station height: 30 - 200 m 
            if (context.TransmitterHeight < 30f || context.TransmitterHeight > 200f)
            {
                warnings += $"Transmitter height {context.TransmitterHeight}m outside typical range (30-200m). ";
            }

            // Mobile station height: 1 - 10 m
            if (context.ReceiverHeight < 1f || context.ReceiverHeight > 10f)
            {
                warnings += $"Receiver height {context.ReceiverHeight}m outside valid range (1-10m). ";
            }

            return (isValid, warnings);
        }

        // mobile antenna correction for large cities 
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
    }
}