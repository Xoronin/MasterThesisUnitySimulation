using UnityEngine;
using UnityEngine.UI;

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

        Debug.Log($"Grid created with {(gridCount + 1) * 2} lines");
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
        int segments = 50; // more segments = smoother curve
        Vector3[] positions = new Vector3[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 pos = Vector3.Lerp(start, end, t);

            // Cast a ray downwards from above the terrain to get the ground height
            Ray ray = new Ray(pos + Vector3.up * 1000f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Terrain")))
            {
                pos.y = hit.point.y + 0.05f; // slightly above ground
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
            DestroyImmediate(gridContainer);
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

        Vector3 snappedPos = new Vector3(snappedX, 1000f, snappedZ); // start above terrain

        // Raycast down to terrain to find exact ground height
        Ray ray = new Ray(snappedPos, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Terrain")))
        {
            snappedPos = hit.point;
        }
        else
        {
            // fallback: keep original Y if terrain wasn't hit
            snappedPos.y = worldPosition.y;
        }

        return snappedPos;
    }
}