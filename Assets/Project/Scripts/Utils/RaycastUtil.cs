using UnityEngine;

namespace RFSimulation.Utils
{
    public static class RaycastUtil
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
    }
}