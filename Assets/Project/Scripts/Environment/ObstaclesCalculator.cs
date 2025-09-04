using UnityEngine;
using RFSimulation.Environment;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Environment
{
	public class ObstacleCalculator : IObstacleCalculator
	{
		private const int MAX_BUILDINGS_PENETRATED = 2;
		private const float MAX_BUILDING_LOSS = 20f; // dB per building
		private const float MAX_TOTAL_LOSS = 50f;    // dB total

		public bool HasLineOfSight(PropagationContext context)
		{
			if (!context.BuildingLayers.HasValue)
				return true; // No obstacles defined

			Vector3 direction = context.ReceiverPosition - context.TransmitterPosition;
			float distance = direction.magnitude;

			return !Physics.Raycast(
				context.TransmitterPosition,
				direction.normalized,
				distance,
				context.BuildingLayers.Value
			);
		}

		public float CalculatePenetrationLoss(PropagationContext context)
		{
			if (!context.BuildingLayers.HasValue)
				return 0f; // No obstacles

			Vector3 direction = context.ReceiverPosition - context.TransmitterPosition;
			float maxDistance = direction.magnitude;

			Debug.Log($"🔍 Calculating penetration loss from {context.TransmitterPosition} to {context.ReceiverPosition}");

			// Get all building intersections
			RaycastHit[] hits = Physics.RaycastAll(
				context.TransmitterPosition,
				direction.normalized,
				maxDistance,
				context.BuildingLayers.Value
			);

			if (hits.Length == 0)
				return 0f; // No buildings in path

			// Sort hits by distance
			System.Array.Sort(hits, (hit1, hit2) => hit1.distance.CompareTo(hit2.distance));

			float totalLoss = 0f;
			int buildingCount = 0;

			foreach (RaycastHit hit in hits)
			{
				Building building = hit.collider.GetComponent<Building>();
				if (building == null) continue;

				buildingCount++;

				// After max buildings, signal finds alternative paths
				if (buildingCount > MAX_BUILDINGS_PENETRATED)
				{
					Debug.Log($"⚡ Signal finds alternative path after {buildingCount - 1} buildings");
					break;
				}

				// Calculate wall thickness instead of full building penetration
				float wallThickness = CalculateWallThickness(hit.collider, building);

				// Apply material loss with realistic values
				float materialLoss = GetMaterialPenetrationLoss(building.material);
				float buildingAttenuation = materialLoss * wallThickness;

				// Limit per building loss
				buildingAttenuation = Mathf.Clamp(buildingAttenuation, 0f, MAX_BUILDING_LOSS);
				totalLoss += buildingAttenuation;

				Debug.Log($"🏠 Building {buildingCount}: {building.material?.materialName ?? "Unknown"}");
				Debug.Log($"   📏 Wall thickness: {wallThickness:F2}m");
				Debug.Log($"   📉 Attenuation: {buildingAttenuation:F1}dB");
			}

			// Apply total loss limit
			totalLoss = Mathf.Clamp(totalLoss, 0f, MAX_TOTAL_LOSS);

			Debug.Log($"🎯 Final building penetration loss: {totalLoss:F1}dB");
			return totalLoss;
		}

		public float CalculateHandoverProbability(float currentSINR, float targetSINR, float margin = 3f)
		{
			float hysteresis = 1f; // Default hysteresis
			float sinrDifference = targetSINR - currentSINR;

			if (sinrDifference < margin)
				return 0f; // Target not good enough

			if (sinrDifference > margin + hysteresis)
				return 1f; // Clear handover candidate

			// Gradual probability in hysteresis zone
			float probability = (sinrDifference - margin) / hysteresis;
			return Mathf.Clamp01(probability);
		}

		private float CalculateWallThickness(Collider buildingCollider, Building building)
		{
			// Use building material thickness if available
			if (building.material != null && building.material.thickness > 0)
			{
				return building.material.thickness;
			}

			// Estimate from building bounds
			Bounds bounds = buildingCollider.bounds;
			float minDimension = Mathf.Min(bounds.size.x, bounds.size.z);

			// Typical wall thickness: 10% of smallest dimension, realistic limits
			float wallThickness = Mathf.Clamp(minDimension * 0.1f, 0.2f, 1.0f);

			Debug.Log($"   🧱 Estimated wall thickness: {wallThickness:F2}m");
			return wallThickness;
		}

		private float GetMaterialPenetrationLoss(BuildingMaterial material)
		{
			if (material == null)
				return 10f; // Default loss per meter

			// Reduce material losses for more realistic values (50% of original)
			return material.penetrationLoss * 0.5f;
		}
	}
}