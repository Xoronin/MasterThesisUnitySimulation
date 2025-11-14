using System.Collections.Generic;
using UnityEngine;

namespace RFSimulation.Visualization
{
    public class ConnectionLineVisualization : MonoBehaviour
    {
        [Header("Line Settings")]
        public Material lineMaterial;
        public float lineWidth = 0.3f;
        public bool useWorldSpace = true;

        [Header("Colors")]
        public Color noSignalColor = Color.red;             
        public Color lowSignalColor = Color.orange;         
        public Color mediumSignalColor = Color.yellow;      
        public Color highSignalColor = Color.yellowGreen;   
        public Color excellentSignalColor = Color.green;   

        [Header("Absolute band cutoffs (dBm)")]
        public float excellentCutoffDbm = -70f;     
        public float goodCutoffDbm = -85f;          
        public float poorCutoffDbm = -100f;         
        public float veryPoorCutoffDbm = -110f;     

        [Header("Performance")]
        public int maxLines = 200;

        private readonly Dictionary<string, LineRenderer> activeLines = new Dictionary<string, LineRenderer>();
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
            line.material = lineMaterial;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.positionCount = 2;
            line.useWorldSpace = useWorldSpace;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.startColor = noSignalColor;
            line.endColor = noSignalColor;
        }

        public LineRenderer CreateConnection(string connectionId, Vector3 startPos, Vector3 endPos, float signalStrength, float sensitivity)
        {
            RemoveConnection(connectionId);

            var line = GetLineFromPool();
            if (line == null) return null; 

            line.gameObject.name = $"ConnectionLine_{connectionId}";
            line.gameObject.SetActive(true);
            line.SetPosition(0, startPos);
            line.SetPosition(1, endPos);

            ApplyStyle(line, signalStrength, sensitivity);

            activeLines[connectionId] = line;
            return line;
        }


        public void UpdateConnection(string connectionId, Vector3 startPos, Vector3 endPos, float signalStrength, float sensitivity)
        {
            if (!activeLines.TryGetValue(connectionId, out var line) || line == null) return;

            line.SetPosition(0, startPos);
            line.SetPosition(1, endPos);
            ApplyStyle(line, signalStrength, sensitivity);
        }


        public void RemoveConnection(string connectionId)
        {
            if (activeLines.TryGetValue(connectionId, out var line) && line != null)
            {
                line.gameObject.SetActive(false);
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                linePool.Enqueue(line);
                activeLines.Remove(connectionId);
            }
        }


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


        public void SetLineVisibility(bool visible)
        {
            foreach (var line in activeLines.Values)
                if (line != null) line.enabled = visible;
        }

        private LineRenderer GetLineFromPool()
        {
            if (linePool.Count > 0)
                return linePool.Dequeue();

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
        }

        private Color GetSignalQualityColor(float signalStrengthDbm, float sensitivityDbm)
        {
            if (float.IsNaN(signalStrengthDbm) || float.IsInfinity(signalStrengthDbm))
                return noSignalColor;

            if (signalStrengthDbm < sensitivityDbm)
                return noSignalColor;

            if (signalStrengthDbm >= excellentCutoffDbm) return excellentSignalColor;       
            if (signalStrengthDbm >= goodCutoffDbm) return highSignalColor;                 
            if (signalStrengthDbm >= poorCutoffDbm) return mediumSignalColor;               
            if (signalStrengthDbm >= veryPoorCutoffDbm) return lowSignalColor;              
            return noSignalColor;                                                           
        }
    }
}
