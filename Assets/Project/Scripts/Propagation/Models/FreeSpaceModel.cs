using UnityEngine;
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
            // Skip calculation if not LOS
            if (!context.IsLOS)
                return float.NegativeInfinity;

            // Ensure minimum distance
            var distance = Mathf.Max(context.DistanceM, RFConstants.MIN_DISTANCE);

            float pathLoss = RFMathHelper.CalculateFSPL(distance, context.FrequencyMHz);

            // Validate result
            return float.IsInfinity(pathLoss) || float.IsNaN(pathLoss)
                ? float.NegativeInfinity 
                : pathLoss;
        }
    }
}