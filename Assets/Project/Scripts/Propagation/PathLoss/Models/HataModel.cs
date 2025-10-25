using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.PathLoss.Models
{
    public class HataModel : IPathLossModel
    {
        public string ModelName => "Hata Urban Path Loss Model";

        public float Calculate(PropagationContext context)
        {

            float hte = context.TransmitterHeight;  // Base station height
            float hre = context.ReceiverHeight;     // Mobile height  
            float fc = context.FrequencyMHz;            // Carrier frequency (MHz)
            float d = context.Distance / 1000f;         // Distance in km

            // Validate parameters according to Hata model limitations
            var validation = ValidateHataParameters(context);
            if (!validation.isValid)
            {
                Debug.LogWarning($"[HataModel] {validation.warning}");
                // Continue with calculation but note limitations
            }

            // Ensure minimum values for stability
            d = Mathf.Max(d, 1f);     // Minimum 1km for Hata model
            hte = Mathf.Max(hte, 30f); // Minimum 30m base station height
            hre = Mathf.Clamp(hre, 1f, 10f); // Mobile height 1-10m

            // Calculate mobile antenna correction factor a(hre) for small/medium cities
            // a(hre) = (1.1*log10(fc) - 0.7)*hre - (1.56*log10(fc) - 0.8)
            float logFc = Mathf.Log10(fc);
            float aHre = (1.1f * logFc - 0.7f) * hre - (1.56f * logFc - 0.8f);

            // Implement exact equation 2.5 from thesis:
            // L50(urban) = 69.55 + 26.16*log10(fc) - 13.82*log10(hte) - a(hre) + (44.9 - 6.55*log10(hte))*log10(d)
            float pathLoss = 69.55f +
                           26.16f * logFc -                    // Frequency term
                           13.82f * Mathf.Log10(hte) -         // Base station height
                           aHre +                              // Mobile correction
                           (44.9f - 6.55f * Mathf.Log10(hte)) * Mathf.Log10(d); // Distance term

            // Calculate received power: Pr = Pt + Gt + Gr - PL
            // Assume Gr = 0 dBi (mobile antenna gain)
            float receivedPower = context.TransmitterPowerDbm +
                                context.AntennaGainDbi +
                                0f -  // Mobile antenna gain (0 dBi)
                                pathLoss;

            return receivedPower;
        }


        /// <summary>
        /// Validate parameters against Hata model specifications from thesis Section 2.1.1
        /// </summary>
        private (bool isValid, string warning) ValidateHataParameters(PropagationContext context)
        {
            string warnings = "";
            bool isValid = true;

            // Frequency range: 150 - 1500 MHz (from thesis)
            if (context.FrequencyMHz < 150f || context.FrequencyMHz > 1500f)
            {
                warnings += $"Frequency {context.FrequencyMHz}MHz outside valid range (150-1500MHz). ";
                isValid = false;
            }

            // Distance range: 1 - 20 km (from thesis)
            float distanceKm = context.Distance / 1000f;
            if (distanceKm < 1f || distanceKm > 20f)
            {
                warnings += $"Distance {distanceKm:F1}km outside valid range (1-20km). ";
                isValid = false;
            }

            // Base station height: 30 - 200 m (typical values)
            if (context.TransmitterHeight < 30f || context.TransmitterHeight > 200f)
            {
                warnings += $"Base station height {context.TransmitterHeight}m outside typical range (30-200m). ";
                // Don't set isValid = false for this - just a warning
            }

            // Mobile station height: 1 - 10 m (from Hata specification)
            if (context.ReceiverHeight < 1f || context.ReceiverHeight > 10f)
            {
                warnings += $"Mobile height {context.ReceiverHeight}m outside valid range (1-10m). ";
                // Don't set isValid = false for this - just a warning
            }

            return (isValid, warnings);
        }

        /// <summary>
        /// Alternative mobile antenna correction for large cities (if needed)
        /// </summary>
        private float CalculateLargeCityCorrection(float frequencyMHz, float mobileHeight)
        {
            if (frequencyMHz <= 200f)
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