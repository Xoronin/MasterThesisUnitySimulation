using RFSimulation.Core.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using RFSimulation.Core.Components;

/// <summary>
/// Click-and-drag mover for world objects (TX/RX).
/// - Drags over ground using a LayerMask
/// - Optional grid snap
/// - Keeps the object's height offset above terrain
/// - Q/E to rotate while dragging (optional)
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

		[Header("Rotation While Dragging")]
		public bool allowRotate = true;
		public float rotateSpeed = 120f; 

		private Camera _cam;
		private bool _dragging;
		private float _capturedOffset; 
		private Transform _t;
		private float _yAtGrab; 
		private Vector3 _grabLocalDelta;

		private GameObject _grabObject;

		[SerializeField] private RFSimulation.UI.GroundGrid groundGrid;

		void Awake()
		{
			_t = transform;
			_cam = Camera.main;

			if (groundGrid == null)
				groundGrid = FindFirstObjectByType<RFSimulation.UI.GroundGrid>(FindObjectsInactive.Include);
        }

		void OnMouseDown()
		{
			if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				return;

			_dragging = true;

            float terrainY = _t.position.y;
			if (RaycastHelper.TryGetGroundY(_t.position, groundMask, out var gy))
				terrainY = gy;
			_capturedOffset = Mathf.Max(0f, _t.position.y - terrainY);
			if (heightOffset != 0f) _capturedOffset = heightOffset; 

			_yAtGrab = _t.position.y;

			if (TryGetMouseGround(out Vector3 hit))
				_grabLocalDelta = _t.position - hit;
			else
				_grabLocalDelta = Vector3.zero;
		}

		void OnMouseDrag()
		{
			if (!_dragging) return;

			if (TryGetMouseGround(out Vector3 pos))
			{
                _grabObject = gameObject;

                pos += _grabLocalDelta;

				pos = groundGrid.SnapToGrid(pos);

				float terrainY = pos.y;
				if (RaycastHelper.TryGetGroundY(pos, groundMask, out var gy))
					terrainY = gy;
				pos.y = terrainY + _capturedOffset;

				_t.position = pos;

				if (allowRotate)
				{
					if (Input.GetKey(KeyCode.Q)) _t.Rotate(0f, -rotateSpeed * Time.deltaTime, 0f, Space.World);
					if (Input.GetKey(KeyCode.E)) _t.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
				}
			}
			else
			{
				Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
				float t = (_yAtGrab - ray.origin.y) / Mathf.Max(0.0001f, ray.direction.y);
				Vector3 fallback = ray.origin + ray.direction * t;

				_t.position = groundGrid ? groundGrid.SnapToGrid(new Vector3(fallback.x, _yAtGrab, fallback.z))
										 : new Vector3(fallback.x, _yAtGrab, fallback.z);
			}
		}

		void OnMouseUp()
		{
			if (!_dragging) return;
			_dragging = false;

            if (_grabObject != null)
            {
                if (_grabObject.TryGetComponent<RFSimulation.Core.Components.Transmitter>(out var transmitter))
                {
                    SimulationManager.Instance?.RecomputeForTransmitter(transmitter);
                }
                else if (_grabObject.TryGetComponent<RFSimulation.Core.Components.Receiver>(out var receiver))
                {
                    SimulationManager.Instance?.RecomputeForReceiver(receiver);
                }

                _grabObject = null;
            }

            BroadcastMessage("OnWorldDragged", SendMessageOptions.DontRequireReceiver);
		}

		private bool TryGetMouseGround(out Vector3 hitPoint)
		{
			hitPoint = default;

			if (_cam == null) _cam = Camera.main;
			if (_cam == null) return false;

			int mask = groundMask & ~forbiddenMask;

			if (RaycastHelper.RayToGround(_cam, Input.mousePosition, mask, out RaycastHit hit))
			{
				hitPoint = hit.point;
				return true;
			}
			return false;
		}
	}
}
