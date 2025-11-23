using UnityEngine;

namespace RFSimulation.UI
{
    public class GroundGrid : MonoBehaviour
    {
        [Header("Grid Settings")]
        public Material gridLineMaterial;
        [Min(0.1f)] public float gridSize = 5f; // meters per square
        public int gridCountFallback = 100;
        public float lineWidth = 0.1f;
        public Color gridColor = new Color(1f, 1f, 1f, 0.3f); 

        [Header("Grid Control")]
        public bool showGrid = true;

        [Header("Height & Sampling")]
        public float heightOffset = 0.10f;
        public float raycastStartHeight = 1000f;
        public LayerMask terrainMask = 1 << 6;
        public int segments = 300;

        [Header("Map/Bounds Sync")]
        public Transform mapRoot;
        public bool autoSyncToMapBounds = true;

        private GameObject gridContainer;
        private Bounds mapBounds;
        private bool boundsValid;

        private int gridCountX;
        private int gridCountZ;

        public bool isInitialized = false;

        public void SetGridVisibility(bool visible)
        {
            showGrid = visible;
            if (gridContainer != null) gridContainer.SetActive(visible);
        }

        public void ToggleGrid() => SetGridVisibility(!showGrid);


        public void UpdateGridSize(float newSize)
        {
            gridSize = Mathf.Max(0.1f, newSize);
            SyncCountsToMap();
            RefreshGrid();
        }

        public void ResyncAndRefresh()
        {
            SyncCountsToMap();
            RefreshGrid();
        }

        public void RefreshGrid()
        {
            if (gridContainer != null) Destroy(gridContainer);
            CreateGrid();
            SetGridVisibility(showGrid);
        }

        public void InitializeGroundGrid()
        {
            SyncCountsToMap();
            CreateGrid();
            SetGridVisibility(showGrid);
        }

        public Vector3 SnapToGrid(Vector3 worldPosition)
        {
            Vector3 center = GetGridOrigin();

            int cx = Mathf.Max(1, gridCountX);
            int cz = Mathf.Max(1, gridCountZ);
            float halfX = (cx * gridSize) * 0.5f;
            float halfZ = (cz * gridSize) * 0.5f;

            Vector3 origin = new Vector3(center.x - halfX, 0f, center.z - halfZ);

            float snappedX = Mathf.Round((worldPosition.x - origin.x) / gridSize) * gridSize + origin.x;
            float snappedZ = Mathf.Round((worldPosition.z - origin.z) / gridSize) * gridSize + origin.z;

            Vector3 probeStart = new Vector3(snappedX, raycastStartHeight, snappedZ);
            if (Physics.Raycast(new Ray(probeStart, Vector3.down), out RaycastHit hit, Mathf.Infinity, terrainMask, QueryTriggerInteraction.Ignore))
                return hit.point;

            return new Vector3(snappedX, worldPosition.y + snappedZ);
        }


        private void CreateGrid()
        {
            gridContainer = new GameObject("GridContainer");
            gridContainer.transform.SetParent(transform, false);

            // Center the grid on the map bounds
            Vector3 center = GetGridOrigin();
            gridContainer.transform.position = new Vector3(center.x, 0f, center.z);

            // Compute half-sizes along each axis 
            int cx = Mathf.Max(1, gridCountX);
            int cz = Mathf.Max(1, gridCountZ);
            float totalX = cx * gridSize;
            float totalZ = cz * gridSize;
            float halfX = totalX * 0.5f;
            float halfZ = totalZ * 0.5f;

            // Vertical lines 
            for (int i = 0; i <= cx; i++)
            {
                float x = -halfX + (i * gridSize);
                CreateGridLine(
                    new Vector3(center.x + x, 0f, center.z - halfZ),
                    new Vector3(center.x + x, 0f, center.z + halfZ),
                    gridContainer.transform,
                    $"GridLine_V_{i}"
                );
            }

            // Horizontal lines 
            for (int i = 0; i <= cz; i++)
            {
                float z = -halfZ + (i * gridSize);
                CreateGridLine(
                    new Vector3(center.x - halfX, 0f, center.z + z),
                    new Vector3(center.x + halfX, 0f, center.z + z),
                    gridContainer.transform,
                    $"GridLine_H_{i}"
                );
            }
        }

        private void CreateGridLine(Vector3 start, Vector3 end, Transform parent, string name)
        {
            var lineObj = new GameObject(name);
            lineObj.transform.SetParent(parent, false);

            var line = lineObj.AddComponent<LineRenderer>();
            line.material = gridLineMaterial != null ? gridLineMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.startColor = gridColor;
            line.endColor = gridColor;
            line.useWorldSpace = true;

            int segs = Mathf.Max(2, segments);
            var positions = new Vector3[segs + 1];

            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 pos = Vector3.Lerp(start, end, t);

                // Probe terrain and float above it
                Ray ray = new Ray(new Vector3(pos.x, raycastStartHeight, pos.z), Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainMask, QueryTriggerInteraction.Ignore))
                    pos.y = hit.point.y + heightOffset;
                else
                    pos.y = pos.y + heightOffset;

                positions[i] = pos;
            }

            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }

        private void SyncCountsToMap()
        {
            boundsValid = TryComputeMapBounds(out mapBounds);

            if (boundsValid)
            {
                // Number of steps (squares) per axis to cover the map footprint
                gridCountX = Mathf.Max(1, Mathf.RoundToInt(mapBounds.size.x / gridSize));
                gridCountZ = Mathf.Max(1, Mathf.RoundToInt(mapBounds.size.z / gridSize));
            }
            else
            {
                gridCountX = gridCountFallback;
                gridCountZ = gridCountFallback;
            }
        }

        private bool TryComputeMapBounds(out Bounds b)
        {
            b = default;

            if (mapRoot == null)
                return false;

            var renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                var colliders = mapRoot.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0) return false;

                b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
                return true;
            }
            else
            {
                b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return true;
            }
        }

        private Vector3 GetGridOrigin()
        {
            if (autoSyncToMapBounds && boundsValid) return mapBounds.center;
            return transform.position;
        }
    }
}
