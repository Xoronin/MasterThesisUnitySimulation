using UnityEngine;
using UnityEngine.UI;

public class GroundGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public Material gridLineMaterial;
    public float gridSize = 10f; 
    public int gridCount = 100; 
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
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startColor = gridColor;
        line.endColor = gridColor;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
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
        float snappedX = Mathf.Round(worldPosition.x / gridSize) * gridSize;
        float snappedZ = Mathf.Round(worldPosition.z / gridSize) * gridSize;
        return new Vector3(snappedX, worldPosition.y, snappedZ);
    }
}