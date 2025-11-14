using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.Models
{
    public class LogDShadowingModel : IPathLossModel
    {
        public string ModelName => "Log-D-Shadow";

        public float CalculatePathLoss(PropagationContext context)
        {
            float distance = context.Distance;
            float referenceDistance = RFConstants.REFERENCE_DISTANCE;
            float pathLossExponent = context.IsLOS
                ? RFConstants.PATH_LOSS_EXPONENT_URBAN
                : RFConstants.PATH_LOSS_EXPONENT_URBAN_SHADOWED;

            distance = Mathf.Max(distance, RFConstants.MIN_DISTANCE);

            // Calculate reference path loss PL(d0) using free space model
            var referenceContext = context.Clone();
            referenceContext.ReceiverPosition =
                context.TransmitterPosition + Vector3.forward * referenceDistance;

            var freeSpaceModel = new FreeSpaceModel();
            float referencePathLoss = freeSpaceModel.CalculatePathLoss(referenceContext); 

            // Log-distance path loss: PL(d) = PL(d0) + 10 n log10(d/d0)
            float distanceRatio = distance / referenceDistance;
            float pathLoss = referencePathLoss + 10f * pathLossExponent * Mathf.Log10(distanceRatio);

            // Add log-normal shadowing term
            float shadowing = SampleGaussian(0f, 4f); 
            pathLoss += shadowing;

            if (float.IsInfinity(pathLoss) || float.IsNaN(pathLoss))
                return float.NegativeInfinity;

            return pathLoss;
        }


        private float SampleGaussian(float mean, float stdDev)
        {
            const float MIN_RANDOM = 0.0001f;
            const float MAX_RANDOM = 0.9999f;

            float u1 = Mathf.Clamp(Random.Range(0f, 1f), MIN_RANDOM, MAX_RANDOM);
            float u2 = Mathf.Clamp(Random.Range(0f, 1f), MIN_RANDOM, MAX_RANDOM);

            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
                                  Mathf.Sin(2.0f * Mathf.PI * u2);

            float result = mean + stdDev * randStdNormal;

            return Mathf.Clamp(result, -15f, 15f);
        }
    }
}
