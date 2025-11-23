using RFSimulation.Core.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using RFSimulation.Core.Components;

/// <summary>
/// Click-and-drag mover for simulation objects.
/// </summary>

namespace RFSimulation.Utils
{

	[RequireComponent(typeof(Collider))]
	public class MoveObjectsHelper : MonoBehaviour
	{
		[Header("Ground Raycast")]
		public LayerMask groundMask;
		public LayerMask forbiddenMask;

		public float heightOffset = 0f;
		public float raycastStartHeight = 1000f;

        [Header("Change detection")]
        private float moveThreshold = 0.01f;
        private float rotationThresholdDeg = 1f;

        private Camera cam;
		private bool dragging;
		private float capturedOffset; 
		private Transform t;
		private float yAtGrab; 
		private Vector3 grabLocalDelta;

		private GameObject grabObject;

        private Vector3 initialWorldPos;
        private Quaternion initialWorldRot;
        private bool hasMovedDuringDrag;

        private RFSimulation.UI.GroundGrid groundGrid;

		void Awake()
		{
			t = transform;
			cam = Camera.main;

			if (groundGrid == null)
				groundGrid = FindFirstObjectByType<RFSimulation.UI.GroundGrid>(FindObjectsInactive.Include);
        }

		void OnMouseDown()
		{
			if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				return;

			dragging = true;

            float terrainY = t.position.y;
			if (RaycastHelper.TryGetGroundY(t.position, groundMask, out var gy))
				terrainY = gy;
			capturedOffset = Mathf.Max(0f, t.position.y - terrainY);
			if (heightOffset != 0f) capturedOffset = heightOffset; 

			yAtGrab = t.position.y;

			if (TryGetMouseGround(out Vector3 hit))
				grabLocalDelta = t.position - hit;
			else
				grabLocalDelta = Vector3.zero;

            initialWorldPos = t.position;
            initialWorldRot = t.rotation;
            hasMovedDuringDrag = false;
        }

		void OnMouseDrag()
		{
			if (!dragging) return;

			if (TryGetMouseGround(out Vector3 pos))
			{
				grabObject = gameObject;

				pos += grabLocalDelta;

				pos = groundGrid.SnapToGrid(pos);

				float terrainY = pos.y;
				if (RaycastHelper.TryGetGroundY(pos, groundMask, out var gy))
					terrainY = gy;
				pos.y = terrainY + capturedOffset;

				t.position = pos;

				if (hasMovedDuringDrag)
				{
					if (Vector3.Distance(initialWorldPos, t.position) > moveThreshold)
						hasMovedDuringDrag = true;
					else if (Quaternion.Angle(initialWorldRot, t.rotation) > rotationThresholdDeg)
						hasMovedDuringDrag = true;
				}
			}
			else
			{
				Ray ray = cam.ScreenPointToRay(Input.mousePosition);
				float transform = (yAtGrab - ray.origin.y) / Mathf.Max(0.0001f, ray.direction.y);
				Vector3 fallback = ray.origin + ray.direction * transform;

				t.position = groundGrid ? groundGrid.SnapToGrid(new Vector3(fallback.x, yAtGrab, fallback.z))
										 : new Vector3(fallback.x, yAtGrab, fallback.z);

                if (!hasMovedDuringDrag)
                {
                    if (Vector3.Distance(initialWorldPos, t.position) > moveThreshold)
                        hasMovedDuringDrag = true;
                }
            }
		}

		void OnMouseUp()
		{
			if (!dragging) return;
			dragging = false;

            if (grabObject != null)
            {
                bool moved = hasMovedDuringDrag ||
                             Vector3.Distance(initialWorldPos, grabObject.transform.position) > moveThreshold ||
                             Quaternion.Angle(initialWorldRot, grabObject.transform.rotation) > rotationThresholdDeg;

                if (moved)
                {
                    if (grabObject.TryGetComponent<RFSimulation.Core.Components.Transmitter>(out var transmitter))
                    {
                        SimulationManager.Instance?.RecomputeForTransmitter(transmitter);
                    }
                    else if (grabObject.TryGetComponent<RFSimulation.Core.Components.Receiver>(out var receiver))
                    {
                        SimulationManager.Instance?.RecomputeForReceiver(receiver);
                    }
                }

                grabObject = null;
            }

            BroadcastMessage("OnWorldDragged", SendMessageOptions.DontRequireReceiver);
		}

		private bool TryGetMouseGround(out Vector3 hitPoint)
		{
			hitPoint = default;

			if (cam == null) cam = Camera.main;
			if (cam == null) return false;

			int mask = groundMask & ~forbiddenMask;

			if (RaycastHelper.RayToGround(cam, Input.mousePosition, mask, out RaycastHit hit))
			{
				hitPoint = hit.point;
				return true;
			}
			return false;
		}
	}
}
