using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;
using RFSimulation.Utils;

namespace RFSimulation.Propagation.Models
{
    public class FreeSpaceModel : IPathLossModel
    {
        public string ModelName => "FreeSpace";

        public float CalculatePathLoss(PropagationContext context)
        {
            float distance = context.Distance;
            float frequency = context.FrequencyMHz;

            // Skip calculation if not LOS
            if (!context.IsLOS)
                return float.NegativeInfinity;

            // Ensure minimum distance
            distance = Mathf.Max(distance, RFConstants.MIN_DISTANCE);

            // Calculate wavelength (λ = c/f)
            float wavelength = RFMathHelper.CalculateWavelength(frequency);

            // Calculate path loss in linear scale
            float fourPiSquared = Mathf.Pow(4f * Mathf.PI, 2f);
            float pathLossLinear = fourPiSquared * distance * distance / (wavelength * wavelength);

            // Ensure pathLossLinear is valid before taking log
            if (pathLossLinear <= 0f || float.IsInfinity(pathLossLinear) || float.IsNaN(pathLossLinear))
            {
                Debug.LogError($"Invalid pathLossLinear: {pathLossLinear}");
                return float.NegativeInfinity;
            }

            // Convert to dB
            float pathLossDb = 10f * Mathf.Log10(pathLossLinear);

            // Validate result
            if (float.IsInfinity(pathLossDb) || float.IsNaN(pathLossDb))
            {
                return float.NegativeInfinity;
            }

            return pathLossDb;
        }
    }
}