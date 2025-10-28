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
    }
}