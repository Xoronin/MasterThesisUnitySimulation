using UnityEngine;
using UnityEngine.EventSystems;

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
		[Tooltip("Layers considered as 'ground' to drag over (same as your placement layers).")]
		public LayerMask groundMask;
		public LayerMask forbiddenMask;

		[Tooltip("How high above the terrain to keep the object while dragging.")]
		public float heightOffset = 0f;

		[Tooltip("How far above the object to start terrain probes.")]
		public float raycastStartHeight = 1000f;

		[Header("Rotation While Dragging")]
		public bool allowRotate = true;
		public float rotateSpeed = 120f; // deg/sec (Q/E)

		private Camera _cam;
		private bool _dragging;
		private float _capturedOffset; // preserves original height offset
		private Transform _t;
		private int _selfLayer;
		private float _yAtGrab; // used when ground has gaps
		private Vector3 _grabLocalDelta; // optional: keep pointer-relative offset

		[SerializeField] private RFSimulation.UI.GroundGrid groundGrid;

		void Awake()
		{
			_t = transform;
			_cam = Camera.main;
			_selfLayer = gameObject.layer;

			if (groundGrid == null)
				groundGrid = FindFirstObjectByType<RFSimulation.UI.GroundGrid>(FindObjectsInactive.Include);
		}

		void OnMouseDown()
		{
			// Ignore if clicking through UI
			if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				return;

			_dragging = true;

			// Compute current offset above terrain so we keep it while dragging
			float terrainY = _t.position.y;
			if (RFSimulation.Utils.GeometryHelper.TryGetGroundY(_t.position, groundMask, out var gy))
				terrainY = gy;
			_capturedOffset = Mathf.Max(0f, _t.position.y - terrainY);
			if (heightOffset != 0f) _capturedOffset = heightOffset; // allow forcing a fixed offset

			_yAtGrab = _t.position.y;

			// Optional: keep pointer-relative delta so object doesn't jump
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
				pos += _grabLocalDelta;

				pos = groundGrid.SnapToGrid(pos);

				// Keep height above ground
				float terrainY = pos.y;
				if (GeometryHelper.TryGetGroundY(pos, groundMask, out var gy))
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
				// No ground hit? Project onto plane at last Y, then snap with the grid's origin-aware method
				Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
				float t = (_yAtGrab - ray.origin.y) / Mathf.Max(0.0001f, ray.direction.y);
				Vector3 fallback = ray.origin + ray.direction * t;

				// Use the grid's SnapToGrid (respects origin, terrain height, and offset)
				_t.position = groundGrid ? groundGrid.SnapToGrid(new Vector3(fallback.x, _yAtGrab, fallback.z))
										 : new Vector3(fallback.x, _yAtGrab, fallback.z);
			}
		}

		void OnMouseUp()
		{
			if (!_dragging) return;
			_dragging = false;

			BroadcastMessage("OnWorldDragged", SendMessageOptions.DontRequireReceiver);
		}

		private bool TryGetMouseGround(out Vector3 hitPoint)
		{
			hitPoint = default;

			if (_cam == null) _cam = Camera.main;
			if (_cam == null) return false;

			// Combine masks: ground minus forbidden
			int mask = groundMask & ~forbiddenMask;

			if (RaycastUtil.RayToGround(_cam, Input.mousePosition, mask, _selfLayer, out RaycastHit hit))
			{
				hitPoint = hit.point;
				return true;
			}
			return false;
		}
	}
}
