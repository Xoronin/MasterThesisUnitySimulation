using RFSimulation.Visualization;
using System.Collections.Generic;
using UnityEngine;

namespace RFSimulation.Visualization
{
    /// <summary>
    /// Handles visual representation of connections between transmitters and receivers
    /// </summary>
    public class ConnectionLineRenderer : MonoBehaviour
    {
        [Header("Line Settings")]
        public Material defaultLineMaterial;
        public float lineWidth = 0.3f;
        public bool useWorldSpace = true;

        [Header("Quality Colors")]
        public Color excellentSignalColor = Color.green;
        public Color goodSignalColor = Color.yellow;
        public Color fairSignalColor = new Color(1f, 0.5f, 0f);
        public Color poorSignalColor = Color.red;
        public Color noSignalColor = Color.gray;

        [Header("Animation")]
        public bool animateLines = true;
        public float animationSpeed = 2f;
        public AnimationType animationType = AnimationType.Pulse;

        [Header("Performance")]
        public int maxLines = 100;
        public bool enableLOD = true;
        public float lodDistance = 200f;

        public enum AnimationType
        {
            None,
            Pulse,
            Flow,
            Glow
        }

        private Dictionary<string, LineRenderer> activeLines = new Dictionary<string, LineRenderer>();
        private Queue<LineRenderer> linePool = new Queue<LineRenderer>();
        private Camera mainCamera;

        void Start()
        {
            mainCamera = Camera.main;
            InitializeLinePool();
        }

        void Update()
        {
            if (animateLines)
            {
                UpdateLineAnimations();
            }

            if (enableLOD)
            {
                UpdateLevelOfDetail();
            }
        }

        private void InitializeLinePool()
        {
            // Pre-create line renderers for performance
            for (int i = 0; i < maxLines; i++)
            {
                GameObject lineObj = new GameObject($"ConnectionLine_{i}");
                lineObj.transform.SetParent(transform);

                LineRenderer line = lineObj.AddComponent<LineRenderer>();
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
        }

        public void CreateConnection(string connectionId, Vector3 startPos, Vector3 endPos, float signalStrength, float sensitivity)
        {
            // Remove existing connection if it exists
            RemoveConnection(connectionId);

            // Get line from pool
            LineRenderer line = GetLineFromPool();
            if (line == null) return; // Pool exhausted

            // Configure the line
            line.gameObject.SetActive(true);
            line.SetPosition(0, startPos);
            line.SetPosition(1, endPos);

            // Set color based on signal quality
            Color lineColor = GetSignalQualityColor(signalStrength, sensitivity);
            line.material.SetColor("_BaseColor", lineColor);
            line.material.SetColor("_BaseColor", lineColor);

            // Store the connection
            activeLines[connectionId] = line;
        }

        public void UpdateConnection(string connectionId, Vector3 startPos, Vector3 endPos, float signalStrength, float sensitivity)
        {
            if (activeLines.TryGetValue(connectionId, out LineRenderer line))
            {
                line.SetPosition(0, startPos);
                line.SetPosition(1, endPos);

                Color lineColor = GetSignalQualityColor(signalStrength, sensitivity);
                line.material.SetColor("_BaseColor", lineColor);
                line.material.SetColor("_BaseColor", lineColor);
            }
        }

        public void RemoveConnection(string connectionId)
        {
            if (activeLines.TryGetValue(connectionId, out LineRenderer line))
            {
                line.gameObject.SetActive(false);
                linePool.Enqueue(line);
                activeLines.Remove(connectionId);
            }
        }

        public void ClearAllConnections()
        {
            foreach (var line in activeLines.Values)
            {
                line.gameObject.SetActive(false);
                linePool.Enqueue(line);
            }
            activeLines.Clear();
        }

        public void SetLineVisibility(bool visible)
        {
            foreach (var line in activeLines.Values)
            {
                line.enabled = visible;
            }
        }

        public void SetLineWidth(float width)
        {
            lineWidth = width;
            foreach (var line in activeLines.Values)
            {
                line.startWidth = width;
                line.endWidth = width;
            }
        }

        private LineRenderer GetLineFromPool()
        {
            if (linePool.Count > 0)
            {
                return linePool.Dequeue();
            }

            // Pool exhausted - try to create new one or reuse oldest
            if (activeLines.Count < maxLines)
            {
                GameObject lineObj = new GameObject($"ConnectionLine_Extra_{activeLines.Count}");
                lineObj.transform.SetParent(transform);

                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                ConfigureLineRenderer(line);
                return line;
            }

            Debug.LogWarning($"[ConnectionLineRenderer] Maximum lines ({maxLines}) reached!");
            return null;
        }

        private Color GetSignalQualityColor(float signalStrength, float sensitivity)
        {
            if (float.IsNegativeInfinity(signalStrength))
                return noSignalColor;

            float margin = signalStrength - sensitivity;

            if (margin < 0f) return noSignalColor;
            if (margin < 5f) return poorSignalColor;
            if (margin < 10f) return fairSignalColor;
            if (margin < 15f) return goodSignalColor;
            return excellentSignalColor;
        }

        private void UpdateLineAnimations()
        {
            float time = Time.time * animationSpeed;

            foreach (var line in activeLines.Values)
            {
                if (!line.gameObject.activeSelf) continue;

                switch (animationType)
                {
                    case AnimationType.Pulse:
                        UpdatePulseAnimation(line, time);
                        break;
                    case AnimationType.Flow:
                        UpdateFlowAnimation(line, time);
                        break;
                    case AnimationType.Glow:
                        UpdateGlowAnimation(line, time);
                        break;
                }
            }
        }

        private void UpdatePulseAnimation(LineRenderer line, float time)
        {
            float alpha = 0.5f + 0.5f * Mathf.Sin(time);
            Color color = line.startColor;
            color.a = alpha;
            line.startColor = color;
            line.endColor = color;
        }

        private void UpdateFlowAnimation(LineRenderer line, float time)
        {
            // Create flowing effect by offsetting material texture
            if (line.material.HasProperty("_MainTex"))
            {
                Vector2 offset = new Vector2(time % 1f, 0);
                line.material.SetTextureOffset("_MainTex", offset);
            }
        }

        private void UpdateGlowAnimation(LineRenderer line, float time)
        {
            float intensity = 1f + 0.5f * Mathf.Sin(time * 2f);
            line.startWidth = lineWidth * intensity;
            line.endWidth = lineWidth * intensity;
        }

        private void UpdateLevelOfDetail()
        {
            if (mainCamera == null) return;

            Vector3 cameraPos = mainCamera.transform.position;

            foreach (var kvp in activeLines)
            {
                LineRenderer line = kvp.Value;
                if (!line.gameObject.activeSelf) continue;

                // Calculate distance to camera
                Vector3 lineCenter = (line.GetPosition(0) + line.GetPosition(1)) * 0.5f;
                float distance = Vector3.Distance(cameraPos, lineCenter);

                // Adjust line based on distance
                if (distance > lodDistance)
                {
                    // Reduce quality for distant lines
                    line.startWidth = lineWidth * 0.5f;
                    line.endWidth = lineWidth * 0.5f;

                    // Optionally disable animation for distant lines
                    if (animateLines && animationType == AnimationType.Flow)
                    {
                        line.material.SetTextureOffset("_MainTex", Vector2.zero);
                    }
                }
                else
                {
                    // Full quality for near lines
                    line.startWidth = lineWidth;
                    line.endWidth = lineWidth;
                }
            }
        }

        private Material CreateDefaultMaterial()
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha blend
            mat.SetFloat("_ZWrite", 0);  // Disable depth writes
            mat.SetInt("_Cull", 2);      // Backface culling
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", Color.white);
            return mat;
        }

        public int GetActiveLineCount()
        {
            return activeLines.Count;
        }

        public int GetAvailableLineCount()
        {
            return linePool.Count;
        }

        public int GetMaxLineCount()
        {
            return maxLines;
        }

        public void SetAnimationType(AnimationType type)
        {
            animationType = type;
        }

        public void SetAnimationSpeed(float speed)
        {
            animationSpeed = speed;
        }

        public void SetMaxLines(int maxCount)
        {
            maxLines = Mathf.Max(10, maxCount);
        }

        public bool IsConnectionActive(string connectionId)
        {
            return activeLines.ContainsKey(connectionId);
        }

        public LineRenderer GetConnectionLine(string connectionId)
        {
            activeLines.TryGetValue(connectionId, out LineRenderer line);
            return line;
        }

        public void PauseAnimations()
        {
            animateLines = false;
        }

        public void ResumeAnimations()
        {
            animateLines = true;
        }

        // Debug method to visualize all lines
        [ContextMenu("Debug Line Info")]
        public void DebugLineInfo()
        {
            Debug.Log($"[ConnectionLineRenderer] Active: {activeLines.Count}, Available: {linePool.Count}, Max: {maxLines}");
            Debug.Log($"Animations: {animateLines} ({animationType} @ {animationSpeed:F1}x), LOD: {enableLOD} ({lodDistance:F0}m)");
        }

        [ContextMenu("Clear All Lines")]
        public void DebugClearAll()
        {
            ClearAllConnections();
        }
    }

    /// <summary>
    /// Statistics structure for ConnectionLineRenderer
    /// </summary>
    [System.Serializable]
    public struct ConnectionLineStats
    {
        public int activeLines;
        public int availableLines;
        public int maxLines;
        public bool animationsEnabled;
        public bool lodEnabled;
        public float lodDistance;

        public float PoolUtilization => maxLines > 0 ? (activeLines / (float)maxLines) * 100f : 0f;
        public bool IsPoolNearFull => activeLines >= maxLines * 0.9f;
        public bool HasAvailableLines => availableLines > 0;
    }
}