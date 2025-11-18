using UnityEngine;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;
using RFSimulation.Utils;

namespace RFSimulation.Propagation.Models
{
    public class LogNShadowingModel : IPathLossModel
    {
        public string ModelName => "Log-N-Shadow";

        public float CalculatePathLoss(PropagationContext context)
        {
            float distance = Mathf.Max(context.DistanceM, RFConstants.MIN_DISTANCE);
            float pathLossExponent = context.IsLOS
                ? RFConstants.PATH_LOSS_EXPONENT_URBAN
                : RFConstants.PATH_LOSS_EXPONENT_URBAN_SHADOWED;

            distance = Mathf.Max(distance, RFConstants.MIN_DISTANCE);

            // Calculate reference path loss PL(d0) using free space model
            var freeSpaceModel = new FreeSpaceModel();
            var referenceContext = context.Clone();
            referenceContext.ReceiverPosition = context.TransmitterPosition + Vector3.forward * RFConstants.REFERENCE_DISTANCE;

            float referencePathLoss = freeSpaceModel.CalculatePathLoss(referenceContext);

            // Log-distance path loss: PL(d) = PL(d0) + 10 n log10(d/d0)
            float pathLoss = RFMathHelper.CalculateLogDistancePathLoss(
                referencePathLoss,
                distance,
                RFConstants.REFERENCE_DISTANCE,
                pathLossExponent
            );

            // Add log-normal shadowing term
            pathLoss += RFMathHelper.SampleLogNormalShadowing(4f);


            return float.IsInfinity(pathLoss) || float.IsNaN(pathLoss)
                ? float.NegativeInfinity
                : pathLoss;

        }
    }
}
