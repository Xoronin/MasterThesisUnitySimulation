using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.PathLoss.Models
{
    public class FreeSpaceModel : IPathLossModel
    {
        public string ModelName => "Free Space Path Loss";

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
            float wavelength = RFConstants.SPEED_OF_LIGHT / (frequency * 1e6f);

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

            // Calculate received power
            //float receivedPower = context.TransmitterPowerDbm + context.AntennaGainDbi - pathLossDb;

            // Validate result
            if (float.IsInfinity(pathLossDb) || float.IsNaN(pathLossDb))
            {
                return float.NegativeInfinity;
            }

            return pathLossDb;
        }
    }
}