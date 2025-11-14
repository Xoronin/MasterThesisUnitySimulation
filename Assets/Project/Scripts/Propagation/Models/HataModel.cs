using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.Models
{
    public class HataModel : IPathLossModel
    {
        public string ModelName => "Hata";

        public float CalculatePathLoss(PropagationContext context)
        {

            float hte = context.TransmitterHeight;      
            float hre = context.ReceiverHeight;         
            float fc = context.FrequencyMHz;            
            float d = context.Distance / 1000f;         

            // Validate parameters according to Hata model limitations
            var validation = ValidateHataParameters(context);
            if (!validation.isValid)
            {
                Debug.LogWarning($"[HataModel] {validation.warning}");
            }

            // Calculate mobile antenna correction factor a(hre) for small/medium cities
            float logFc = Mathf.Log10(fc);
            float aHre = (1.1f * logFc - 0.7f) * hre - (1.56f * logFc - 0.8f);

            float pathLoss = 69.55f +
                           26.16f * logFc -                    
                           13.82f * Mathf.Log10(hte) -         
                           aHre +                              
                           (44.9f - 6.55f * Mathf.Log10(hte)) * Mathf.Log10(d);

            // Validate result
            if (float.IsInfinity(pathLoss) || float.IsNaN(pathLoss))
            {
                return float.NegativeInfinity;
            }

            return pathLoss;
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
    }
}