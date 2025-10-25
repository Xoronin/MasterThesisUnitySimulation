using System.Collections.Generic;
using UnityEngine;

namespace RFSimulation.Visualization
{
    /// <summary>
    /// Handles visual representation of connections between transmitters and receivers
    /// (pooling, color, width, simple LOD). Transmitters call into this class to
    /// create/update/remove lines; this class owns the actual LineRenderer objects.
    /// </summary>
    public class ConnectionLineRenderer : MonoBehaviour
    {
        [Header("Line Settings")]
        public Material defaultLineMaterial;
        public float lineWidth = 0.3f;
        public bool useWorldSpace = true;

        [Header("Colors")]
        public Color noSignalColor = Color.lightGray; // red
        public Color lowSignalColor = Color.lightBlue; // orange
        public Color mediumSignalColor = Color.cyan; // yellow
        public Color highSignalColor = Color.royalBlue; // yellow-green
        public Color excellentSignalColor = Color.purple; // green

        [Header("Quality Thresholds (dB above sensitivity)")]
        public float lowThresh = 0f;   // < 0 → no signal (below sensitivity)
        public float mediumThresh = 5f;   // [0,5)
        public float highThresh = 10f;  // [5,10)
        public float excellentThresh = 15f;  // [10,15)

        [Header("Performance")]
        [Tooltip("Preallocated lines in the pool and soft cap for active lines.")]
        public int maxLines = 100;
        public bool enableLOD = true;
        public float lodDistance = 200f;

        // connectionId -> active LineRenderer
        private readonly Dictionary<string, LineRenderer> activeLines = new Dictionary<string, LineRenderer>();
        // pooled lines waiting for reuse
        private readonly Queue<LineRenderer> linePool = new Queue<LineRenderer>();

        private Camera mainCamera;

        void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            InitializeLinePool();
        }

        void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (enableLOD) UpdateLevelOfDetail();
        }

        private void InitializeLinePool()
        {
            for (int i = 0; i < maxLines; i++)
            {
                var lineObj = new GameObject($"ConnectionLine_{i}");
                lineObj.transform.SetParent(transform, false);

                var line = lineObj.AddComponent<LineRenderer>();
                ConfigureLineRenderer(line);

                lineObj.SetActive(false);
                linePool.Enqueue(line);
            }
        }

        private void ConfigureLineRenderer(LineRenderer line)
        {
            line.material = defaultLineMaterial != null ? defaultLineMaterial : CreateDefaultMaterial();
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.positionCount = 2;
            line.useWorldSpace = useWorldSpace;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            // Initial color (will be overridden by ApplyStyle)
            line.startColor = noSignalColor;
            line.endColor = noSignalColor;
        }

        /// <summary>
        /// Create or replace a connection line for the given id.
        /// Returns the LineRenderer used, or null if pool/cap is exhausted.
        /// </summary>
        public LineRenderer CreateConnection(string connectionId, Vector3 startPos, Vector3 endPos, float signalStrength, float sensitivity)
        {
            // If it exists already, recycle it first
            RemoveConnection(connectionId);

            var line = GetLineFromPool();
            if (line == null) return null; // pool/cap exhausted

            line.gameObject.name = $"ConnectionLine_{connectionId}";
            line.gameObject.SetActive(true);
            line.SetPosition(0, startPos);
            line.SetPosition(1, endPos);

            ApplyStyle(line, signalStrength, sensitivity);

            activeLines[connectionId] = line;
            return line;
        }

        /// <summary>
        /// Update an existing connection's positions & style.
        /// </summary>
        public void UpdateConnection(string connectionId, Vector3 startPos, Vector3 endPos, float signalStrength, float sensitivity)
        {
            if (!activeLines.TryGetValue(connectionId, out var line) || line == null) return;

            line.SetPosition(0, startPos);
            line.SetPosition(1, endPos);
            ApplyStyle(line, signalStrength, sensitivity);
        }

        /// <summary>
        /// Remove (recycle) a connection by id.
        /// </summary>
        public void RemoveConnection(string connectionId)
        {
            if (activeLines.TryGetValue(connectionId, out var line) && line != null)
            {
                line.gameObject.SetActive(false);
                // Reset width to current global width so it’s correct when reused
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                linePool.Enqueue(line);
                activeLines.Remove(connectionId);
            }
        }

        /// <summary>
        /// Remove (recycle) all active connections.
        /// </summary>
        public void ClearAllConnections()
        {
            foreach (var line in activeLines.Values)
            {
                if (line == null) continue;
                line.gameObject.SetActive(false);
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                linePool.Enqueue(line);
            }
            activeLines.Clear();
        }

        /// <summary>
        /// Toggle visibility for all active lines (keeps them active in pool).
        /// </summary>
        public void SetLineVisibility(bool visible)
        {
            foreach (var line in activeLines.Values)
                if (line != null) line.enabled = visible;
        }

        /// <summary>
        /// Set a new global width and apply to both active and pooled lines.
        /// </summary>
        public void SetLineWidth(float width)
        {
            lineWidth = width;

            foreach (var line in activeLines.Values)
            {
                if (line == null) continue;
                line.startWidth = width;
                line.endWidth = width;
            }

            // Also update pooled ones so new rentals have correct width
            foreach (var line in linePool)
            {
                if (line == null) continue;
                line.startWidth = width;
                line.endWidth = width;
            }
        }

        private LineRenderer GetLineFromPool()
        {
            if (linePool.Count > 0)
                return linePool.Dequeue();

            // Pool exhausted — allow expansion up to maxLines (soft cap)
            if (activeLines.Count < maxLines)
            {
                var lineObj = new GameObject($"ConnectionLine_Extra_{activeLines.Count}");
                lineObj.transform.SetParent(transform, false);

                var line = lineObj.AddComponent<LineRenderer>();
                ConfigureLineRenderer(line);
                return line;
            }

            Debug.LogWarning($"[ConnectionLineRenderer] Maximum lines ({maxLines}) reached!");
            return null;
        }

        private void ApplyStyle(LineRenderer line, float signalStrength, float sensitivity)
        {
            if (line == null) return;
            var c = GetSignalQualityColor(signalStrength, sensitivity);
            line.startColor = c;
            line.endColor = c;
            // Width is managed by LOD and SetLineWidth; no need to reset here.
        }

        private Color GetSignalQualityColor(float signalStrength, float sensitivity)
        {
            if (float.IsNegativeInfinity(signalStrength)) return noSignalColor;

            float margin = signalStrength - sensitivity;

            if (margin < lowThresh) return noSignalColor;        // below sensitivity
            if (margin < mediumThresh) return lowSignalColor;   // very low
            if (margin < highThresh) return mediumSignalColor;       // low
            if (margin < excellentThresh) return highSignalColor;    // medium
            return excellentSignalColor;                              // high / excellent
        }

        private void UpdateLevelOfDetail()
        {
            if (mainCamera == null) return;

            Vector3 cameraPos = mainCamera.transform.position;

            foreach (var kvp in activeLines)
            {
                var line = kvp.Value;
                if (line == null || !line.gameObject.activeSelf) continue;

                var p0 = line.GetPosition(0);
                var p1 = line.GetPosition(1);
                float distance = Vector3.Distance(cameraPos, (p0 + p1) * 0.5f);

                if (distance > lodDistance)
                {
                    line.startWidth = lineWidth * 0.5f;
                    line.endWidth = lineWidth * 0.5f;
                }
                else
                {
                    line.startWidth = lineWidth;
                    line.endWidth = lineWidth;
                }
            }
        }

        private Material CreateDefaultMaterial()
        {
            // Simple transparent unlit for predictable coloring
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha blend
            mat.SetFloat("_ZWrite", 0);  // Disable depth writes
            mat.SetInt("_Cull", 2);      // Backface culling
            mat.renderQueue = 3000;
            return mat;
        }

        // --- Diagnostics / helpers ---

        public int GetActiveLineCount() => activeLines.Count;
        public int GetAvailableLineCount() => linePool.Count;
        public int GetMaxLineCount() => maxLines;

        public void SetMaxLines(int maxCount)
        {
            maxLines = Mathf.Max(1, maxCount);
        }

        public bool IsConnectionActive(string connectionId)
            => activeLines.ContainsKey(connectionId);

        public LineRenderer GetConnectionLine(string connectionId)
        {
            activeLines.TryGetValue(connectionId, out var line);
            return line;
        }

        [ContextMenu("Debug Line Info")]
        public void DebugLineInfo()
        {
            Debug.Log($"[ConnectionLineRenderer] Active: {activeLines.Count}, Available: {linePool.Count}, Max: {maxLines}");
        }

        [ContextMenu("Clear All Lines")]
        public void DebugClearAll()
        {
            ClearAllConnections();
        }
    }

    /// <summary>
    /// Optional stats struct (kept for compatibility/inspections)
    /// </summary>
    [System.Serializable]
    public struct ConnectionLineStats
    {
        public int activeLines;
        public int availableLines;
        public int maxLines;
        public bool lodEnabled;
        public float lodDistance;

        public float PoolUtilization => maxLines > 0 ? (activeLines / (float)maxLines) * 100f : 0f;
        public bool IsPoolNearFull => activeLines >= maxLines * 0.9f;
        public bool HasAvailableLines => availableLines > 0;
    }
}
