using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.Models
{
    public class LogDModel : IPathLossModel
    {
        public string ModelName => "Log-D";

        public float CalculatePathLoss(PropagationContext context)
        {
            float distance = context.Distance;
            float referenceDistance = RFConstants.REFERENCE_DISTANCE;
            float pathLossExponent = context.IsLOS ? RFConstants.PATH_LOSS_EXPONENT_URBAN : RFConstants.PATH_LOSS_EXPONENT_URBAN_SHADOWED;

            // Skip calculation if not LOS
            if (!context.IsLOS)
                return float.NegativeInfinity;

            // Ensure minimum distance
            distance = Mathf.Max(distance, RFConstants.MIN_DISTANCE);

            // Calculate reference path loss (free space at reference distance)
            var referenceContext = context.Clone();
            referenceContext.ReceiverPosition = context.TransmitterPosition +
                                               Vector3.forward * referenceDistance;

            var freeSpaceModel = new FreeSpaceModel();
            float referenceLoss = freeSpaceModel.CalculatePathLoss(referenceContext);

            // Log-distance path loss
            float distanceRatio = distance / referenceDistance;
            float pathLoss = referenceLoss + 10f * pathLossExponent * Mathf.Log10(distanceRatio);

            // Validate result
            if (float.IsInfinity(pathLoss) || float.IsNaN(pathLoss))
            {
                return float.NegativeInfinity;
            }

            return pathLoss;
        }

    }
}