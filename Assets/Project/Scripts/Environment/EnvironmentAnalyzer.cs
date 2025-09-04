using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Propagation.Core;


namespace RFSimulation.Environment
{
    public static class EnvironmentAnalyzer
    {
        public static EnvironmentType DetermineEnvironment(PropagationContext context)
        {
            // Auto-detect environment based on context
            if (context.BuildingLayers.HasValue)
            {
                // Check building density
                float buildingDensity = CalculateBuildingDensity(context);

                if (buildingDensity > 0.3f) return EnvironmentType.Urban;
            }

            return EnvironmentType.FreeSpace;
        }

        public static float GetEnvironmentFactor(EnvironmentType environment, float frequency)
        {
            // Frequency-dependent environment factors
            return environment switch
            {
                EnvironmentType.Urban => GetUrbanFactor(frequency),
                EnvironmentType.FreeSpace => 1.0f,
                _ => 1.0f
            };
        }

        private static float CalculateBuildingDensity(PropagationContext context)
        {
            if (!context.BuildingLayers.HasValue)
                return 0f;

            // Sample area around the link
            Vector3 midpoint = (context.TransmitterPosition + context.ReceiverPosition) * 0.5f;
            float sampleRadius = Mathf.Min(context.Distance * 0.5f, 500f);

            // Count buildings in sample area (simplified)
            Collider[] buildings = Physics.OverlapSphere(midpoint, sampleRadius, context.BuildingLayers.Value);
            float sampleArea = Mathf.PI * sampleRadius * sampleRadius;

            float totalBuildingArea = 0f;
            foreach (var building in buildings)
            {
                Bounds bounds = building.bounds;
                totalBuildingArea += bounds.size.x * bounds.size.z;
            }

            return Mathf.Clamp01(totalBuildingArea / sampleArea);
        }

        private static float GetUrbanFactor(float frequencyMHz)
        {
            // Higher frequencies suffer more in urban environments
            if (frequencyMHz > 2000f) return 1.3f;  // 5G frequencies
            if (frequencyMHz > 1000f) return 1.2f;  // Higher LTE bands
            return 1.1f; // Lower frequencies
        }
    }
}