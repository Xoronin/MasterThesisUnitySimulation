using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Click-and-drag mover for world objects (TX/RX).
/// - Drags over ground using a LayerMask
/// - Optional grid snap
/// - Keeps the object's height offset above terrain
/// - Q/E to rotate while dragging (optional)
/// </summary>
[RequireComponent(typeof(Collider))]
public class MoveObjectsHelper : MonoBehaviour
{
	[Header("Ground Raycast")]
	[Tooltip("Layers considered as 'ground' to drag over (same as your placement layers).")]
	public LayerMask groundMask = 6;

	[Tooltip("How high above the terrain to keep the object while dragging.")]
	public float heightOffset = 0f;

	[Tooltip("How far above the object to start terrain probes.")]
	public float raycastStartHeight = 1000f;

	[Header("Grid Snap")]
	public bool snapToGrid = true;
	public float gridSize = 1f;

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

	void Awake()
	{
		_t = transform;
		_cam = Camera.main;
		_selfLayer = gameObject.layer;
	}

	void OnMouseDown()
	{
		// Ignore if clicking through UI
		if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
			return;

		_dragging = true;

		// Compute current offset above terrain so we keep it while dragging
		float terrainY = SampleTerrainY(_t.position);
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

			if (snapToGrid)
			{
				pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
				pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
			}

			// Keep height above ground
			float terrainY = SampleTerrainY(pos);
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
			// No ground hit? Keep last Y, move in horizontal plane
			Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
			float t = (_yAtGrab - ray.origin.y) / Mathf.Max(0.0001f, ray.direction.y);
			Vector3 fallback = ray.origin + ray.direction * t;
			if (snapToGrid)
			{
				fallback.x = Mathf.Round(fallback.x / gridSize) * gridSize;
				fallback.z = Mathf.Round(fallback.z / gridSize) * gridSize;
			}
			_t.position = new Vector3(fallback.x, _yAtGrab, fallback.z);
		}
	}

	void OnMouseUp()
	{
		if (!_dragging) return;
		_dragging = false;

		// Optional: notify others that we moved (StatusUI will pick up position automatically)
		BroadcastMessage("OnWorldDragged", SendMessageOptions.DontRequireReceiver);
	}

	private bool TryGetMouseGround(out Vector3 hitPoint)
	{
		hitPoint = default;
		if (_cam == null) _cam = Camera.main;
		if (_cam == null) return false;

		Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

		// Exclude our own layer from the ground mask so we don't hit ourselves
		int effectiveMask = groundMask & ~(1 << _selfLayer);

		if (Physics.Raycast(ray, out RaycastHit hit, 10000f, effectiveMask, QueryTriggerInteraction.Ignore))
		{
			hitPoint = hit.point;
			return true;
		}
		return false;
	}

	private float SampleTerrainY(Vector3 at)
	{
		// Probe downward to find terrain height
		Vector3 start = new Vector3(at.x, at.y + raycastStartHeight, at.z);
		if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, raycastStartHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
			return hit.point.y;

		// Fallback: keep current Y if no terrain found
		return at.y;
	}
}
