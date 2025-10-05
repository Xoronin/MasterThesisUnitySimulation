using UnityEngine;
using UnityEngine.UI;

namespace RFSimulation.UI
{
    public class GroundGrid : MonoBehaviour
    {
        [Header("Grid Settings")]
        public Material gridLineMaterial;
        public float gridSize = 5f;     // 5x5 meters per grid square
        public int gridCount = 100;     // Number of grid lines in each direction (total lines = gridCount + 1)
        public float lineWidth = 0.1f;
        public Color gridColor = new Color(1f, 1f, 1f, 0.3f); // Semi-transparent white

        [Header("Grid Control")]
        public bool showGrid = true;

        [Header("Height & Sampling")]
        [Tooltip("How far above the terrain the grid should float.")]
        public float heightOffset = 0.10f;

        [Tooltip("Upward start height for the downwards terrain probe.")]
        public float raycastStartHeight = 1000f;

        [Tooltip("Which layers count as terrain for the grid to sit on.")]
        public LayerMask terrainMask = 6;

        [Tooltip("Segments per grid line (higher = smoother following of terrain).")]
        public int segments = 80;

        private GameObject gridContainer;

        void Start()
        {
            CreateGrid();
            SetGridVisibility(showGrid);
        }

        void CreateGrid()
        {
            // Create grid container as child of this object
            gridContainer = new GameObject("GridContainer");
            gridContainer.transform.SetParent(this.transform);
            gridContainer.transform.localPosition = Vector3.zero;

            float totalSize = gridSize * gridCount;
            float halfSize = totalSize / 2f;

            // Create vertical lines (North-South)
            for (int i = 0; i <= gridCount; i++)
            {
                float x = -halfSize + (i * gridSize);
                CreateGridLine(
                    new Vector3(x, 0.1f, -halfSize),
                    new Vector3(x, 0.1f, halfSize),
                    gridContainer.transform,
                    $"GridLine_V_{i}"
                );
            }

            // Create horizontal lines (East-West)
            for (int i = 0; i <= gridCount; i++)
            {
                float z = -halfSize + (i * gridSize);
                CreateGridLine(
                    new Vector3(-halfSize, 0.1f, z),
                    new Vector3(halfSize, 0.1f, z),
                    gridContainer.transform,
                    $"GridLine_H_{i}"
                );
            }
        }

        void CreateGridLine(Vector3 start, Vector3 end, Transform parent, string name)
        {
            GameObject lineObj = new GameObject(name);
            lineObj.transform.SetParent(parent);

            LineRenderer line = lineObj.AddComponent<LineRenderer>();

            // Use default material if none assigned
            if (gridLineMaterial != null)
                line.material = gridLineMaterial;
            else
                line.material = new Material(Shader.Find("Sprites/Default"));

            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.startColor = gridColor;
            line.endColor = gridColor;
            line.useWorldSpace = true;

            // Break line into segments so it can bend with terrain
            int segs = Mathf.Max(2, segments);
            Vector3[] positions = new Vector3[segs + 1];

            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 pos = Vector3.Lerp(start, end, t);

                // Cast a ray downwards from above the terrain to get the ground height
                Ray ray = new Ray(pos + Vector3.up * raycastStartHeight, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainMask, QueryTriggerInteraction.Ignore))
                {
                    pos.y = hit.point.y + heightOffset;
                }
                else
                {
                    pos.y = pos.y + heightOffset;
                }

                positions[i] = pos;
            }

            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }

        public void SetGridVisibility(bool visible)
        {
            showGrid = visible;

            if (gridContainer != null)
            {
                gridContainer.SetActive(visible);
                Debug.Log($"Grid visibility set to: {visible}");
            }
            else
            {
                Debug.LogWarning("Grid container is null!");
            }
        }

        public void ToggleGrid()
        {
            SetGridVisibility(!showGrid);
        }

        // Update grid size at runtime
        public void UpdateGridSize(float newSize)
        {
            gridSize = newSize;
            RefreshGrid();
        }

        public void RefreshGrid()
        {
            if (gridContainer != null)
            {
                Destroy(gridContainer);
            }
            CreateGrid();
            SetGridVisibility(showGrid);
        }

        // Get snap position for objects
        public Vector3 SnapToGrid(Vector3 worldPosition)
        {
            // Snap X and Z to nearest grid points
            float snappedX = Mathf.Round(worldPosition.x / gridSize) * gridSize;
            float snappedZ = Mathf.Round(worldPosition.z / gridSize) * gridSize;

            Vector3 probeStart = new Vector3(snappedX, raycastStartHeight, snappedZ);

            // Raycast down to terrain to find exact ground height
            if (Physics.Raycast(new Ray(probeStart, Vector3.down), out RaycastHit hit, Mathf.Infinity, terrainMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * heightOffset;
            }

            return new Vector3(snappedX, worldPosition.y + heightOffset, snappedZ);
        }
    }
}