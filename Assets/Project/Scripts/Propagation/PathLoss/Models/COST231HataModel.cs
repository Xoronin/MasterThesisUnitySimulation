using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss.Models;

/// <summary>
/// COST-231 Hata Model - Extension for higher frequencies (1500-2000 MHz)
/// </summary>
public class COST231HataModel : IPathLossModel
{
    public string ModelName => "COST-231 Hata Model";

    public float CalculatePathLoss(PropagationContext context)
    {
        // Extract parameters
        float tansmitterHeight = context.TransmitterHeight;     // Base station height
        float receiverHeight = context.ReceiverHeight;          // Mobile height  
        float frequency = context.FrequencyMHz;                 // Carrier frequency (MHz)
        float distance = context.Distance / 1000f;              // Distance in km
        float CM = RFConstants.METROPOLITAN_CORRECTION;

        var validation = ValidateCOST231Parameters(context);
        if (!validation.isValid)
        {
            Debug.LogWarning($"[COST-321Model] {validation.warning}");
            // Continue with calculation but note limitations
        }

        //// Ensure minimum distance
        //distance = Mathf.Max(distance, 1f);

        // Calculate mobile antenna correction factor (same as original Hata)
        float mobileCorrection = CalculateLargeCityCorrection(frequency, receiverHeight);

        // COST-231 Hata Formula (extension of original Hata):
        // L50 = 46.3 + 33.9*log10(fc) - 13.82*log10(hte) - a(hre) + (44.9 - 6.55*log10(hte))*log10(d) + CM
        // Where CM = 0 dB for medium cities and suburban areas with moderate tree density
        //       CM = 3 dB for metropolitan centers
        float pathLoss = 46.3f +                                     // Modified base constant
                       33.9f * Mathf.Log10(frequency) -              // Modified frequency term  
                       13.82f * Mathf.Log10(tansmitterHeight) -     // Base antenna height term
                       mobileCorrection +                            // Mobile antenna correction
                       (44.9f - 6.55f * Mathf.Log10(tansmitterHeight)) * Mathf.Log10(distance) + // Distance term
                       CM;                                          // Metropolitan correction

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