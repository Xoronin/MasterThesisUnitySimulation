using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.PathLoss.Models
{
    public class LogDistanceModel : IPathLossModel
    {
        public string ModelName => "Log-Distance Path Loss";

        public float Calculate(PropagationContext context)
        {
            float distance = context.Distance;
            float pathLossExponent = RFConstants.PATH_LOSS_EXPONENT;
            float referenceDistance = RFConstants.REFERENCE_DISTANCE;

            // Ensure minimum distance
            distance = Mathf.Max(distance, RFConstants.MIN_DISTANCE);

            // Calculate reference path loss (free space at reference distance)
            var referenceContext = context.Clone();
            referenceContext.ReceiverPosition = context.TransmitterPosition +
                                               Vector3.forward * referenceDistance;

            var freeSpaceModel = new FreeSpaceModel();
            float referenceLoss = context.TransmitterPowerDbm + context.AntennaGainDbi -
                                freeSpaceModel.Calculate(referenceContext);

            // Log-distance path loss
            float distanceRatio = distance / referenceDistance;
            float pathLoss = referenceLoss + 10f * pathLossExponent * Mathf.Log10(distanceRatio);

            // Add log-normal shadowing (optional) - THIS IS THE LIKELY CULPRIT
            float shadowing = SampleGaussianSafe(0f, 4f);
            pathLoss += shadowing;

            float receivedPower = context.TransmitterPowerDbm + context.AntennaGainDbi - pathLoss;

            // Validate result
            if (float.IsInfinity(receivedPower) || float.IsNaN(receivedPower))
            {
                return float.NegativeInfinity;
            }

            return receivedPower;
        }

        // FIXED: Safe Gaussian sampling that avoids log(0) = -infinity
        private float SampleGaussianSafe(float mean, float stdDev)
        {
            // Clamp random values to avoid log(0)
            const float MIN_RANDOM = 0.0001f; // Avoid exactly 0
            const float MAX_RANDOM = 0.9999f; // Avoid exactly 1

            float u1 = Mathf.Clamp(Random.Range(0f, 1f), MIN_RANDOM, MAX_RANDOM);
            float u2 = Mathf.Clamp(Random.Range(0f, 1f), MIN_RANDOM, MAX_RANDOM);

            // Box-Muller transform
            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
                                 Mathf.Sin(2.0f * Mathf.PI * u2);

            float result = mean + stdDev * randStdNormal;

            // Clamp the final result to reasonable bounds
            return Mathf.Clamp(result, -15f, 15f); // Limit shadowing to ±30dB
        }
    }
}