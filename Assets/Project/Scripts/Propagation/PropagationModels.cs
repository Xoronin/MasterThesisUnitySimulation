
using UnityEngine;
using System.Collections.Generic;
using RadioSignalSimulation.Environment;

namespace RadioSignalSimulation.Propagation
{
    public static class PropagationModels
    {
        // Propagation model parameter
        public enum PropagationModel
        {
            FreeSpace,
            LogDistance,
            TwoRaySimple,
            TwoRayGroundReflection
        }

        // Environment parameter
        public enum EnvironmentType
        {
            FreeSpace,
            Urban,
            Indoor
        }

        public static float CalculatePathLoss(
            Vector3 transmitterPosition,
            Vector3 receiverPosition,
            float transmitterPower,
            float antennaGain,
            float frequency,
            PropagationModel model,
            EnvironmentType environment)
        {
            float distance = Vector3.Distance(transmitterPosition, receiverPosition);
            float pathLoss = 0f;

            switch (model)
            {
                case PropagationModel.FreeSpace:
                    pathLoss = CalculateFreeSpacePathLoss(distance, transmitterPower, antennaGain, frequency);
                    break;
                case PropagationModel.LogDistance:
                    pathLoss = CalculateLogDistancePathLoss(distance, environment, transmitterPower, antennaGain, frequency);
                    break;
                case PropagationModel.TwoRaySimple:
                    pathLoss = CalculateTwoRayPathLoss(transmitterPosition, receiverPosition, transmitterPower, frequency);
                    break;
                case PropagationModel.TwoRayGroundReflection:
                    // Hier ggf. weitere Parameter ergänzen, falls benötigt
                    pathLoss = CalculateTwoRayGroundReflection(transmitterPosition, receiverPosition, transmitterPower, frequency);
                    break;
                default:
                    pathLoss = CalculateFreeSpacePathLoss(distance, transmitterPower, antennaGain, frequency);
                    break;
            }
            return pathLoss;
        }

        // Calculate the Log-Distance Path Loss model
        // Formula: PL(d) = PL(d0) + 10 * n * log10(d/d0)
        // PL = Path Loss in dB
        // d = distance between transmitter and receiver in meters
        // d0 = reference distance in meters (usually 1 meter)
        // n = path loss exponent (depends on the environment)
        public static float CalculateLogDistancePathLoss(
            float distance,
            EnvironmentType environment,
            float transmitterPower,
            float antennaGain,
            float frequency)
        {
            float n = GetPathLossExponent(environment);
            float d0 = GetReferenceDistance(environment);
            float freeSpaceLoss = CalculateFreeSpacePathLoss(distance, transmitterPower, antennaGain, frequency);
            float PL = freeSpaceLoss + 10f * n * Mathf.Log10(distance / d0);
            return PL;
        }

        // TODO: check values source
        static float GetPathLossExponent(EnvironmentType environment)
        {
            switch (environment)
            {
                case EnvironmentType.FreeSpace:
                    return 2.0f; // Free space path loss exponent
                case EnvironmentType.Urban:
                    return 3.0f; // Urban environment path loss exponent
                case EnvironmentType.Indoor:
                    return 2.7f; // Indoor environment path loss exponent
                default:
                    return 2.0f; // Default to free space
            }
        }

        // TODO: check values source
        static float GetReferenceDistance(EnvironmentType environment)
        {
            switch (environment)
            {
                case EnvironmentType.FreeSpace:
                    return 1.0f; // Reference distance for free space
                case EnvironmentType.Urban:
                    return 100.0f; // Reference distance for urban environment
                case EnvironmentType.Indoor:
                    return 1.0f; // Reference distance for indoor environment
                default:
                    return 1.0f; // Default to 1 meter
            }
        }

        //TODO
        // Check if there is a line of sight between the transmitter and receiver
        public static bool HasLineOfSight(Vector3 transmitterPosition, Vector3 receiverPosition, LayerMask obstacleLayers)
        {
            Vector3 direction = receiverPosition - transmitterPosition;
            float distance = direction.magnitude; 

            return !Physics.Raycast(transmitterPosition, direction.normalized, distance, obstacleLayers);
        }

        // Add Log-Normal Shadowing
        // TODO check values
        static float AddLogNormalShadowing(float meanPathLoss, float standardDeviation = 8f)
        {
            float randomVariation = SampleGaussian(0f, standardDeviation);
            return meanPathLoss + randomVariation;
        }

        // Calculate the free space path loss using the Friis transmission equation
        // Friis equation: Pr = (Pt * Gt * Gr * λ²) / ((4π)² * d² * L)
        // Pr = received power (dBm)
        // Pt = transmitted power (dBm)
        // Gt = gain of the transmitting antenna (linear scale)
        // Gr = gain of the receiving antenna (linear scale)
        // λ = wavelength (meters)
        // d = distance between transmitter and receiver (meters)
        // L = system losses (linear scale, typically 1 for free space)
        public static float CalculateFreeSpacePathLoss(
            float distance,
            float powerOutput,
            float antennaGain,
            float frequency)
        {
            // Pt: Convert transmitter power from dBm to watts
            // Pt(watts) = 10^((Pt(dBm) - 30) / 10)
            float Pt = Mathf.Pow(10f, (powerOutput - 30f) / 10f); // Transmitted power in watts

            // Gt: Gain of the transmitting antenna (isotropic antenna, linear scale)
            float Gt = antennaGain;

            // Gr: Gain of the receiving antenna (isotropic antenna, linear scale)
            float Gr = 1f; 

            // L: System losses (linear scale, typically 1 for free space)
            float L = 1f;

            // d: Distance between transmitter and receiver in meters
            float d = distance;

            // λ: Calculate wavelength in meters
            float λ = CalculateWaveLength(frequency);

            // Gt and Gr: Assuming isotropic antennas, gain is 1 (linear scale)
            // G = 4π * Ae / λ²
            // Ae = the effective aperture of the antenna
            // Calculate the free space path loss

            // Pr: Apply Friis equation
            // Pr = (Pt * Gt * Gr * λ²) / ((4π)² * d² * L)
            float numerator = Pt * Gt * Gr * Mathf.Pow(λ, 2f);
            float denominator = Mathf.Pow(4f * Mathf.PI, 2f) * d * d * L;
            float Pr = numerator / denominator; // Received power in watts

            // Convert received power from watts to dBm
            // Pr(dBm) = 10 * log10(Pr(watts)) * 1000
            float Pr_dBm = 10f * Mathf.Log10(Pr * 1000f); // Received power in dBm

            return Pr_dBm; // Return the transmitter power in dBm
        }

        // Calculate the wavelength based on the frequency
        // Formula: λ = c / f = 2π * c / Wc
        // where:
        // λ = wavelength in meters 
        // c = speed of light in meters per second (approximately 299,792,458 m/s)
        // f = carrier frequency in Hz
        // Wc = carrier frequency in radians per second
        static float CalculateWaveLength(float frequency)
        {
            float c = 3e8f;                             // Speed of light in m/s
            float f = frequency * 1e6f;                 // Convert MHz to Hz
            float λ = c / f;                            // Wavelength in m

            //Debug.Log($"Wavelength: {λ} meters");

            return λ;
        }

        // Calculate the free space path loss (FSPL) in dB
        // FSPL = 20 * log10(d) + 20 * log10(f) + 20 * log10(4π/c)
        // where:
        // d = distance in meters
        // f = frequency in Hz
        // c = speed of light in m/s (approximately 3e8 m/s)
        static float CalculateSimpleFSPL(float distance, float frequency)
        {

            float d = distance / 1000f;     // Convert km
            float f = frequency;            // Frequency in MHz
            //float c = 3e8f;                 // Speed of light
            float fspl = 20f * Mathf.Log10(d) + 20f * Mathf.Log10(f) + 32.44f;

            Debug.Log($"Distance: {distance}m, FSPL: {fspl:F1}dB");

            // Return path loss in dB
            return fspl;
        }

        static float CalculateReceivedPowerFSPL(float distance, float frequency, float powerOutput)
        {
            float pathLoss = CalculateSimpleFSPL(distance, frequency);     // Free space path loss in dB
            float receivedPower = powerOutput - pathLoss;       // Received power in dBm

            Debug.Log($"Distance: {distance}m, FSPL: {pathLoss}dB, Received: {receivedPower}dBm");

            return receivedPower;
        }

        // Calculate the Two-Ray Ground Reflection model
        // Formula: PL(d) = 20 * log10(d) + 20 * log10(f) + 20 * log10(4π/c) + 2 * hT * hR / d
        // PL = Path Loss in dB
        // d = distance between transmitter and receiver in meters
        // f = frequency in Hz
        // hT = height of the transmitter in meters
        // hR = height of the receiver in meters
        // c = speed of light in meters per second (approximately 299,792,458 m/s)
        // Note: This model assumes that the ground is flat and the transmitter and receiver are above the ground level.
        static float CalculateTwoRayPathLoss(Vector3 transmitterPosition, Vector3 receiverPosition, float transmitterPower, float frequency)
        {
            // Get horizontal distance between transmitter and receiver
            Vector2 txPos = new Vector2(transmitterPosition.x, transmitterPosition.z);
            Vector2 rxPos = new Vector2(receiverPosition.x, receiverPosition.z);
            float horizontalDistance = Vector2.Distance(txPos, rxPos);

            // Calculate wavelength
            float wavelength = CalculateWaveLength(frequency);

            // Calculate path difference
            float pathDifference = (2 * transmitterPosition.y * receiverPosition.y) / horizontalDistance;
            
            // Calculate phase difference
            float phaseDifference = (2 * Mathf.PI * pathDifference) / wavelength;

            // Calculate field strength ratio
            float fieldRatio = 2 * Mathf.Sin(phaseDifference / 2);

            // Convert to path loss in dB
            float pathLossLinear = Mathf.Pow(horizontalDistance, 2) / Mathf.Pow(fieldRatio, 2);
            float pathLossDB = 10 * Mathf.Log10(pathLossLinear);

            return pathLossDB;
        }

        // Calculate Two-Ray Ground Reflection model with ground reflection
        // Formula: PL(d) = 20 * log10(d) + 20 * log10(f) + 20 * log10(4π/c) + 2 * hT * hR / d + 10 * log10(GT * GR)
        public static float CalculateTwoRayGroundReflection(
            Vector3 transmitterPosition,
            Vector3 receiverPosition,
            float transmitterPower,
            float frequency)
        {
            // Get horizontal distance between transmitter and receiver
            Vector2 txPos = new Vector2(transmitterPosition.x, transmitterPosition.z);
            Vector2 rxPos = new Vector2(receiverPosition.x, receiverPosition.z);
            float horizontalDistance = Vector2.Distance(txPos, rxPos);

            // Calculate wavelength
            float wavelength = CalculateWaveLength(frequency);

            // Direct path distance
            float directPathDistance = Vector3.Distance(transmitterPosition, receiverPosition);

            // Reflected path distance using ground reflection point
            // TODO:
            Vector3 reflectionPoint = CalculateGroundReflectionPoint(transmitterPosition, receiverPosition);
            float reflectedDistance = Vector3.Distance(transmitterPosition, reflectionPoint) * Vector3.Distance(reflectionPoint, receiverPosition);

            // Path difference
            float pathDifference = reflectedDistance - directPathDistance;

            // Phase difference
            float phaseDifference = (2 * Mathf.PI * pathDifference) / wavelength;

            // Ground reflection coefficient
            // TODO: check value
            float reflectionCoefficient =- 0.9f; // Concrete/asphalt

            // Calculate total field (direct + reflected)
            //Complex directField = new Complex(1.0f, directPathDistance, 0);
            //Complex reflectedField = new Complex(
            //    (reflectionCoefficient, reflectedDistance) * Mathf.Cos(phaseDifference),
            //    (reflectionCoefficient, reflectedDistance) * Mathf.Sin(phaseDifference)
            //);

            //Complex totalField = directField + reflectedField;
            //float totalFieldMagnitude = totalField.Magnitude;

            // Simplified calculation (avoiding Complex numbers for now)
            float directFieldMagnitude = 1.0f / directPathDistance;
            float reflectedFieldMagnitude = Mathf.Abs(reflectionCoefficient) / reflectedDistance;

            // Vector addition considering phase difference
            float totalFieldMagnitude = Mathf.Sqrt(
                Mathf.Pow(directFieldMagnitude + reflectedFieldMagnitude * Mathf.Cos(phaseDifference), 2) +
                Mathf.Pow(reflectedFieldMagnitude * Mathf.Sin(phaseDifference), 2)
            );

            // Convert to path loss in dB
            float pathLoss = 20 * Mathf.Log10(4 * Mathf.PI * directPathDistance / wavelength) - 20 * Mathf.Log10(totalFieldMagnitude);

            return pathLoss;
        }

        // Calculate the ground reflection point based on transmitter and receiver positions
        static Vector3 CalculateGroundReflectionPoint(Vector3 tx, Vector3 rx)
        {
            // Find reflection point on the ground
            float t = tx.y / (tx.y - rx.y); // Calculate the ratio of heights
            Vector3 reflectionPoint = new Vector3(
                tx.x + t * (rx.x - tx.x),
                0f, // Ground level
                tx.z + t * (rx.z - tx.z)
            );

            return reflectionPoint;
        }

        static float SampleGaussian(float mean, float stdDev)
        {
            // Box-Muller transform for Gaussian distribution
            float u1 = 1.0f - Random.Range(0f, 1f);
            float u2 = 1.0f - Random.Range(0f, 1f);
            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
            return mean + stdDev * randStdNormal;
        }

        public static float CalculatePathLossWithObstacles(
            Vector3 transmitterPosition,
            Vector3 receiverPosition,
            float transmitterPower,
            float antennaGain,
            float frequency,
            PropagationModel model,
            EnvironmentType environment,
            LayerMask buildingLayers)
        {
            // Calculate base path loss
            float basePathLoss = CalculatePathLoss(
                transmitterPosition, receiverPosition, transmitterPower,
                antennaGain, frequency, model, environment);

            // Calculate additional losses from buildings
            float buildingLoss = CalculateBuildingPenetrationLoss(
                transmitterPosition, receiverPosition, frequency, buildingLayers);

            return basePathLoss - buildingLoss; // Subtract because we want received power
        }

        private static float CalculateBuildingPenetrationLoss(
    Vector3 transmitterPosition,
    Vector3 receiverPosition,
    float frequency,
    LayerMask buildingLayers)
        {
            float totalLoss = 0f;
            Vector3 direction = receiverPosition - transmitterPosition;
            float maxDistance = direction.magnitude;

            Debug.Log($"🔍 Raycast from {transmitterPosition} to {receiverPosition}, distance: {maxDistance:F1}m");

            RaycastHit[] hits = Physics.RaycastAll(transmitterPosition, direction.normalized, maxDistance, buildingLayers);
            Debug.Log($"🏢 Found {hits.Length} building intersections");

            // Sort hits by distance
            System.Array.Sort(hits, (hit1, hit2) => hit1.distance.CompareTo(hit2.distance));

            int buildingCount = 0;

            foreach (RaycastHit hit in hits)
            {
                Building building = hit.collider.GetComponent<Building>();
                if (building != null)
                {
                    buildingCount++;

                    // REALISTIC CHANGE 1: After 2 buildings, signals find alternative paths
                    if (buildingCount > 2)
                    {
                        Debug.Log($"⚡ Signal finds alternative path after {buildingCount - 1} buildings");
                        break;
                    }

                    // REALISTIC CHANGE 2: Buildings are rarely penetrated completely - use wall thickness instead of full path
                    float pathLength = CalculateWallThickness(hit.collider, building);

                    // REALISTIC CHANGE 3: Reduce material losses for more realistic values
                    float materialLoss = building.material.penetrationLoss * 0.5f; // Reduce by 50%
                    float buildingAttenuation = materialLoss * pathLength;

                    // Limit per building (more lenient)
                    buildingAttenuation = Mathf.Clamp(buildingAttenuation, 0f, 20f); // Max 20dB per building

                    totalLoss += buildingAttenuation;

                    Debug.Log($"🏠 Building {buildingCount}: {building.material.materialName}");
                    Debug.Log($"   📏 Wall thickness: {pathLength:F2}m");
                    Debug.Log($"   📉 Attenuation: {buildingAttenuation:F1}dB");
                    Debug.Log($"   📊 Total loss so far: {totalLoss:F1}dB");
                }
            }

            // REALISTIC CHANGE 4: More lenient total limit
            totalLoss = Mathf.Clamp(totalLoss, 0f, 50f); // Max 50dB total loss

            Debug.Log($"🎯 Final building penetration loss: {totalLoss:F1}dB");
            return totalLoss;
        }

        // New helper method for more realistic wall thickness calculation
        private static float CalculateWallThickness(Collider buildingCollider, Building building)
        {
            // Use building material thickness if available
            if (building.material != null && building.material.thickness > 0)
            {
                return building.material.thickness;
            }

            // Fallback: estimate wall thickness from building bounds
            Bounds bounds = buildingCollider.bounds;
            float minDimension = Mathf.Min(bounds.size.x, bounds.size.z);

            // Typical wall thickness: 10% of smallest building dimension, clamped to realistic values
            float wallThickness = Mathf.Clamp(minDimension * 0.1f, 0.2f, 1.0f);

            Debug.Log($"   🧱 Estimated wall thickness: {wallThickness:F2}m");
            return wallThickness;
        }

        private static float CalculateBuildingPathLength(Collider buildingCollider, Vector3 start, Vector3 end)
        {
            Bounds bounds = buildingCollider.bounds;
            Vector3 direction = (end - start).normalized;

            // Find where ray enters and exits the building bounds
            if (bounds.IntersectRay(new Ray(start, direction), out float entryDistance))
            {
                // Create ray from the back to find exit point
                Vector3 backPoint = end - direction * bounds.size.magnitude;
                if (bounds.IntersectRay(new Ray(backPoint, direction), out float exitFromBack))
                {
                    float pathLength = bounds.size.magnitude - exitFromBack;

                    // Sanity check: path length should be reasonable
                    pathLength = Mathf.Clamp(pathLength, 0.1f, Mathf.Min(bounds.size.x, bounds.size.z, bounds.size.y));

                    Debug.Log($"   🧮 Calculated path: entry={entryDistance:F2}m, exit calc, final={pathLength:F2}m");
                    return pathLength;
                }
            }

            // Fallback: use smallest building dimension as approximation
            float fallbackLength = Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f;
            Debug.Log($"   🔄 Fallback path length: {fallbackLength:F2}m");
            return fallbackLength;
        }

        private static Vector3 GetExitPoint(Collider buildingCollider, Vector3 start, Vector3 end)
        {
            // Simple approximation - find where ray exits the building
            Bounds bounds = buildingCollider.bounds;
            Vector3 direction = (end - start).normalized;

            // Raycast from the back of the building towards the receiver
            Vector3 backPoint = bounds.center + direction * bounds.size.magnitude;

            if (Physics.Raycast(backPoint, -direction, out RaycastHit hit, bounds.size.magnitude * 2))
            {
                if (hit.collider == buildingCollider)
                {
                    return hit.point;
                }
            }

            // Fallback to bounds intersection
            return bounds.center;
        }

    }
}
