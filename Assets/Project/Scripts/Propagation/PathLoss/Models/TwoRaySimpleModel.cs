using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.PathLoss.Models
{
    public class TwoRaySimpleModel : IPathLossModel
    {
        public string ModelName => "Two-Ray Simple";

        public float Calculate(PropagationContext context)
        {
            // Get horizontal distance between transmitter and receiver
            Vector2 txPos = new Vector2(context.TransmitterPosition.x, context.TransmitterPosition.z);
            Vector2 rxPos = new Vector2(context.ReceiverPosition.x, context.ReceiverPosition.z);
            float horizontalDistance = Vector2.Distance(txPos, rxPos);

            // Ensure minimum distance
            horizontalDistance = Mathf.Max(horizontalDistance, RFConstants.MIN_DISTANCE);

            float txHeight = context.TransmitterPosition.y;
            float rxHeight = context.ReceiverPosition.y;

            // Calculate wavelength
            float wavelength = context.WavelengthMeters;

            // Calculate path difference between direct and reflected paths
            float pathDifference = (2 * txHeight * rxHeight) / horizontalDistance;

            // Calculate phase difference
            float phaseDifference = (2 * Mathf.PI * pathDifference) / wavelength;

            // Calculate field strength ratio (simplified)
            float fieldRatio = 2 * Mathf.Abs(Mathf.Sin(phaseDifference / 2));

            // Avoid division by zero
            if (fieldRatio < 0.001f) fieldRatio = 0.001f;

            // Calculate path loss in dB
            float pathLossLinear = Mathf.Pow(horizontalDistance, 2) / Mathf.Pow(fieldRatio, 2);
            float pathLossDb = 10 * Mathf.Log10(pathLossLinear);

            // Convert to received power
            float receivedPower = context.TransmitterPowerDbm + context.AntennaGainDbi - pathLossDb;

            return receivedPower;
        }
    }
}