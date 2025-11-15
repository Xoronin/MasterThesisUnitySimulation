using UnityEngine;


namespace RFSimulation.Utils
{
    public static class RaycastHelper
    {
        public static bool RayToGround(Camera cam, Vector3 screenPos, LayerMask groundMask, out RaycastHit hit)
        {
            hit = default;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(screenPos);
            int mask = groundMask;
            return Physics.Raycast(ray, out hit, 10000f, mask, QueryTriggerInteraction.Ignore);
        }

        public static bool IsLineOfSight(Vector3 txPos, Vector3 rxPos, LayerMask buildingMask)
        {
            Vector3 dir = rxPos - txPos;
            float dist = dir.magnitude;
            int mask = buildingMask;
            return !Physics.Raycast(txPos, dir.normalized, dist, mask, QueryTriggerInteraction.Ignore);
        }

        public static bool TryGetGroundY(Vector3 worldPos, LayerMask groundMask, out float groundY)
        {
            const float span = 20000f;

            var origin = worldPos + Vector3.up * span * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, span, groundMask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }
            groundY = worldPos.y;
            return false;
        }

        public static float GetHeightAboveGround(Vector3 worldPos)
        {
            var groundMask = LayerMask.GetMask("Terrain");

            TryGetGroundY(worldPos, groundMask, out var gy);
            return Mathf.Max(0f, worldPos.y - gy);
        }

        public static bool IsSegmentBlocked(
            Vector3 start,
            Vector3 end,
            LayerMask obstacleMask,
            out RaycastHit hit,
            float margin = 0.05f)
        {
            var dir = end - start;
            float dist = dir.magnitude;
            hit = default;

            if (dist < 0.01f)
                return false;

            dir /= dist;

            // Add margin to avoid self-intersection
            var origin = start + dir * margin;
            var maxDist = Mathf.Max(0f, dist - 2f * margin);

            return Physics.Raycast(origin, dir, out hit, maxDist, obstacleMask,  QueryTriggerInteraction.Ignore);
        }

        public static bool HasLineOfSightWithMargin(
            Vector3 from,
            Vector3 to,
            LayerMask obstacleMask,
            float margin = 0.05f)
        {
            return !IsSegmentBlocked(from, to, obstacleMask, out _, margin);
        }
    }
}