using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Interfaces;
using RFSimulation.Utils;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.PathLoss.Models
{
	public class TwoRayGroundReflectionModel : IPathLossModel
	{
		public string ModelName => "Two-Ray Ground Reflection";

		public float Calculate(PropagationContext context)
		{
			Vector3 txPos = context.TransmitterPosition;
			Vector3 rxPos = context.ReceiverPosition;

			// Calculate direct path distance
			float directDistance = Vector3.Distance(txPos, rxPos);
			directDistance = Mathf.Max(directDistance, RFConstants.MIN_DISTANCE);

			// Calculate ground reflection point
			Vector3 reflectionPoint = GeometryHelper.CalculateGroundReflectionPoint(txPos, rxPos);

			// Calculate reflected path distance
			float reflectedDistance = Vector3.Distance(txPos, reflectionPoint) +
									Vector3.Distance(reflectionPoint, rxPos);

			// Calculate path difference
			float pathDifference = reflectedDistance - directDistance;

			// Calculate phase difference
			float wavelength = context.WavelengthMeters;
			float phaseDifference = (2 * Mathf.PI * pathDifference) / wavelength;

			// Ground reflection coefficient (depends on ground material and polarization)
			float reflectionCoefficient = -0.9f;

			// Calculate field components
			float directFieldMagnitude = 1.0f / directDistance;
			float reflectedFieldMagnitude = Mathf.Abs(reflectionCoefficient) / reflectedDistance;

			// Vector addition considering phase difference (180° phase shift for ground reflection)
			float totalFieldMagnitude = Mathf.Sqrt(
				Mathf.Pow(directFieldMagnitude - reflectedFieldMagnitude * Mathf.Cos(phaseDifference), 2) +
				Mathf.Pow(reflectedFieldMagnitude * Mathf.Sin(phaseDifference), 2)
			);

			// Calculate path loss
			float freeSpacePathLoss = 20 * Mathf.Log10(4 * Mathf.PI * directDistance / wavelength);
			float multiPathGain = 20 * Mathf.Log10(totalFieldMagnitude * directDistance);

			float totalPathLoss = freeSpacePathLoss - multiPathGain;

			// Convert to received power
			float receivedPower = context.TransmitterPowerDbm + context.AntennaGainDbi - totalPathLoss;

			return receivedPower;
		}

	}
}