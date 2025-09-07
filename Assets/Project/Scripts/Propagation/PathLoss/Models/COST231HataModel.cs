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

    public float Calculate(PropagationContext context)
    {
        // Validate parameters
        if (!IsValidForCOST231(context))
        {
            // Fallback to free space for frequencies outside range
            var freeSpace = new FreeSpaceModel();
            return freeSpace.Calculate(context) - 10f; // Add 10dB urban penalty
        }

        // Extract parameters
        float tansmitterHeight = context.TransmitterPosition.y;
        float receiverHeight = context.ReceiverPosition.y;
        float frequency = context.FrequencyMHz;              // fc (MHz)
        float distance = context.Distance / 1000f;          // d (km) - Hata uses kilometers

        if (receiverHeight <= 0 || tansmitterHeight <= 0 || distance <= 0)
        {
            Debug.LogError("COST231: Invalid input parameters");
            return float.NegativeInfinity;
        }

        // Ensure minimum distance
        distance = Mathf.Max(distance, 1f);

        // Calculate mobile antenna correction factor (same as original Hata)
        float mobileCorrection = CalculateMobileAntennaCorrection(frequency, receiverHeight);

        // COST-231 Hata Formula (extension of original Hata):
        // L50 = 46.3 + 33.9*log10(fc) - 13.82*log10(hte) - a(hre) + (44.9 - 6.55*log10(hte))*log10(d) + CM
        // Where CM = 0 dB for medium cities and suburban areas with moderate tree density
        //       CM = 3 dB for metropolitan centers

        float metropolitanCorrection = 3f; // Assume metropolitan center (dense urban)

        float pathLoss = 46.3f +                                     // Modified base constant
                       33.9f * Mathf.Log10(frequency) -              // Modified frequency term  
                       13.82f * Mathf.Log10(tansmitterHeight) -            // Base antenna height term
                       mobileCorrection +                            // Mobile antenna correction
                       (44.9f - 6.55f * Mathf.Log10(tansmitterHeight)) * Mathf.Log10(distance) + // Distance term
                       metropolitanCorrection;                       // Metropolitan correction

        // Calculate received power
        float receivedPower = context.TransmitterPowerDbm +
                            context.AntennaGainDbi +
                            0f - // Mobile antenna gain
                            pathLoss;

        if (!float.IsFinite(pathLoss))
        {
            Debug.LogError($"COST231: Invalid path loss calculation: {pathLoss}");
            return float.NegativeInfinity;
        }

        return receivedPower;
    }

    private float CalculateMobileAntennaCorrection(float frequencyMHz, float receiverHeight)
    {
        // Same as original Hata model
        float logFreq = Mathf.Log10(frequencyMHz);
        return (1.1f * logFreq - 0.7f) * receiverHeight - (1.56f * logFreq - 0.8f);
    }

    private bool IsValidForCOST231(PropagationContext context)
    {
        float distanceKm = context.Distance / 1000f;
        return context.FrequencyMHz >= 1500f && context.FrequencyMHz <= 2000f &&
               distanceKm >= 1f && distanceKm <= 20f;
    }
}