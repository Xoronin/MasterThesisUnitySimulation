using UnityEngine;

namespace RFSimulation.Utils
{
    public static class GeometryHelper
    {
        /// <summary>
        /// Calculate ground reflection point for two-ray models
        /// </summary>
        public static Vector3 CalculateGroundReflectionPoint(Vector3 transmitter, Vector3 receiver)
        {
            // Find reflection point on the ground (y = 0)
            if (Mathf.Approximately(transmitter.y, receiver.y))
            {
                // Same height - reflection point is midway
                return new Vector3(
                    (transmitter.x + receiver.x) * 0.5f,
                    0f,
                    (transmitter.z + receiver.z) * 0.5f
                );
            }

            // Calculate using similar triangles
            float t = transmitter.y / (transmitter.y + receiver.y);

            Vector3 reflectionPoint = new Vector3(
                transmitter.x + t * (receiver.x - transmitter.x),
                0f,
                transmitter.z + t * (receiver.z - transmitter.z)
            );

            return reflectionPoint;
        }

        /// <summary>
        /// Calculate Fresnel zone radius at a given point
        /// </summary>
        public static float CalculateFresnelRadius(float distance1, float distance2, float frequency)
        {
            float wavelength = MathHelper.CalculateWavelength(frequency);
            float totalDistance = distance1 + distance2;

            return Mathf.Sqrt((wavelength * distance1 * distance2) / totalDistance);
        }

        /// <summary>
        /// Check if path is obstructed by terrain/buildings
        /// </summary>
        public static bool IsPathObstructed(Vector3 start, Vector3 end, LayerMask obstacleLayers, float clearance = 0f)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            if (clearance > 0f)
            {
                // Check multiple rays for clearance
                Vector3 up = Vector3.up * clearance;
                Vector3 right = Vector3.Cross(direction.normalized, Vector3.up) * clearance;

                return Physics.Raycast(start, direction.normalized, distance, obstacleLayers) ||
                       Physics.Raycast(start + up, direction.normalized, distance, obstacleLayers) ||
                       Physics.Raycast(start + right, direction.normalized, distance, obstacleLayers) ||
                       Physics.Raycast(start - right, direction.normalized, distance, obstacleLayers);
            }

            return Physics.Raycast(start, direction.normalized, distance, obstacleLayers);
        }

        /// <summary>
        /// Calculate elevation angle between two points
        /// </summary>
        public static float CalculateElevationAngle(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;

            if (horizontalDistance < 0.001f)
                return direction.y > 0 ? 90f : -90f;

            return Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Calculate azimuth angle between two points
        /// </summary>
        public static float CalculateAzimuthAngle(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            // Normalize to 0-360 degrees
            if (angle < 0f) angle += 360f;

            return angle;
        }
    }
}