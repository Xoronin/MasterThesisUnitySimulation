using UnityEngine;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Core.Managers;

namespace RFSimulation.Core.Components
{
    /// <summary>
    /// Simplified Receiver - combines all receiver functionality in one place
    /// </summary>
    public class Receiver : MonoBehaviour
    {
        #region Core Properties
        [Header("Receiver Properties")]
        public string uniqueID;
        public string technology = "5G";
        public float sensitivity = -90f; // dBm
        public float connectionMargin = 10f; // dB above sensitivity needed
        public float minimumSINR = -6f; // dB

        [Header("Status")]
        public float currentSignalStrength = float.NegativeInfinity;
        public float currentSINR = float.NegativeInfinity;

        [Header("Visualization")]
        public bool showSignalSphere = true;
        #endregion

        #region Private Fields
        private Transmitter connectedTransmitter = null;
        private Renderer receiverRenderer;
        private GameObject signalSphereVisual;
        private SignalQualityMetrics currentQuality;

        // Signal quality colors
        private readonly Color excellentColor = Color.green;
        private readonly Color goodColor = Color.yellow;
        private readonly Color fairColor = new Color(1f, 0.5f, 0f);
        private readonly Color poorColor = Color.red;
        private readonly Color noSignalColor = Color.gray;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            InitializeReceiver();
            SimulationManager.Instance?.RegisterReceiver(this);
        }

        void Start()
        {
            SetupVisualization();
        }

        void Update()
        {
            UpdateSignalStatus();
            UpdateVisualization();
        }

        void OnDestroy()
        {
            ClearConnection();
            CleanupVisualization();
            SimulationManager.Instance?.RemoveReceiver(this);
        }
        #endregion

        #region Core Functionality
        public void UpdateSignalStrength(float signalStrength, float sinr = 0f)
        {
            currentSignalStrength = signalStrength;
            currentSINR = sinr;

            // Update signal quality metrics
            var technology = GetTechnologyType();
            currentQuality = new SignalQualityMetrics(sinr, technology);

            UpdateVisualFeedback();
        }

        public bool CanConnect()
        {
            return currentSignalStrength >= (sensitivity + connectionMargin) &&
                   currentSINR >= minimumSINR;
        }

        public void SetConnectedTransmitter(Transmitter transmitter)
        {
            connectedTransmitter = transmitter;
        }

        public void ClearConnection()
        {
            connectedTransmitter = null;
            currentSignalStrength = float.NegativeInfinity;
            currentSINR = float.NegativeInfinity;
            UpdateVisualFeedback();
        }

        public bool IsConnected() => connectedTransmitter != null;

        public void SetTechnology(string tech)
        {
            technology = tech;
        }
        #endregion

        #region Signal Quality Assessment
        private SignalQualityCategory GetSignalQualityCategory()
        {
            float margin = currentSignalStrength - sensitivity;

            if (margin < 0f) return SignalQualityCategory.NoService;
            if (margin < 5f) return SignalQualityCategory.Poor;
            if (margin < 10f) return SignalQualityCategory.Fair;
            if (margin < 15f) return SignalQualityCategory.Good;
            return SignalQualityCategory.Excellent;
        }

        private TechnologyType GetTechnologyType()
        {
            return technology.ToUpper() switch
            {
                "5G" => TechnologyType.FiveG,
                "LTE" => TechnologyType.LTE,
                _ => TechnologyType.LTE
            };
        }

        private Color GetSignalQualityColor()
        {
            return GetSignalQualityCategory() switch
            {
                SignalQualityCategory.Excellent => excellentColor,
                SignalQualityCategory.Good => goodColor,
                SignalQualityCategory.Fair => fairColor,
                SignalQualityCategory.Poor => poorColor,
                _ => noSignalColor
            };
        }
        #endregion

        #region Visualization - Integrated
        private void SetupVisualization()
        {
            receiverRenderer = GetComponent<Renderer>();
            CreateSignalVisualization();
        }

        private void CreateSignalVisualization()
        {
            // Create signal sphere
            if (showSignalSphere)
            {
                signalSphereVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                signalSphereVisual.name = $"SignalSphere_{uniqueID}";
                signalSphereVisual.transform.SetParent(transform);
                signalSphereVisual.transform.localPosition = Vector3.zero;
                signalSphereVisual.transform.localScale = Vector3.one * 0.5f;

                // Make it semi-transparent
                var renderer = signalSphereVisual.GetComponent<Renderer>();
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

                // Set up for transparency
                material.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
                material.SetFloat("_Blend", 0);   // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
                material.SetFloat("_AlphaClip", 0); // Disable alpha clipping
                material.SetFloat("_ZWrite", 0);  // Disable depth writing for transparency
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.renderQueue = 3000; // Transparent queue

                // Set the color with alpha
                material.SetColor("_BaseColor", new Color(1, 1, 1, 0.3f));

                // Enable transparency keywords
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
        }

        private void UpdateSignalStatus()
        {
            if (connectedTransmitter != null)
            {
                float signal = connectedTransmitter.CalculateSignalStrength(transform.position);
                UpdateSignalStrength(signal, currentSINR);
            }
        }

        private void UpdateVisualization()
        {
            UpdateVisualFeedback();
        }

        private void UpdateVisualFeedback()
        {
            Color signalColor = GetSignalQualityColor();

            // Update receiver color
            if (receiverRenderer != null)
            {
                receiverRenderer.material.color = signalColor;
            }

            // Update signal sphere color
            if (signalSphereVisual != null)
            {
                var renderer = signalSphereVisual.GetComponent<Renderer>();
                var material = renderer.material;
                material.color = new Color(signalColor.r, signalColor.g, signalColor.b, 0.3f);
            }
        }

        public void ToggleVisualization(bool showBars, bool showSphere)
        {
            showSignalSphere = showSphere;

            if (signalSphereVisual != null)
                signalSphereVisual.SetActive(showSphere);
        }

        private void CleanupVisualization()
        {
            if (signalSphereVisual != null)
                DestroyImmediate(signalSphereVisual);
        }
        #endregion

        #region Utilities
        private void InitializeReceiver()
        {
            if (string.IsNullOrEmpty(uniqueID))
                uniqueID = "RX_" + GetInstanceID();

            // Ensure collider for interaction
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
            }
        }

        public string GetStatusText()
        {
            string status = $"Technology: {technology}\n" +
                           $"Sensitivity: {sensitivity:F1} dBm\n" +
                           $"Signal: {currentSignalStrength:F1} dBm\n" +
                           $"SINR: {currentSINR:F1} dB\n" +
                           $"Quality: {GetSignalQualityCategory()}\n" +
                           $"Connected: {(IsConnected() ? "Yes" : "No")}";

            if (connectedTransmitter != null)
            {
                status += $"\nTransmitter: {connectedTransmitter.uniqueID}";
            }

            return status;
        }
        #endregion

        public Transmitter GetConnectedTransmitter() => connectedTransmitter;

        public SignalQualityCategory GetSignalQuality() => GetSignalQualityCategory();

        public float GetExpectedThroughput()
        {
            var qualityCategory = GetSignalQualityCategory();
            return qualityCategory switch
            {
                SignalQualityCategory.Excellent => 100f, // Mbps
                SignalQualityCategory.Good => 75f,
                SignalQualityCategory.Fair => 50f,
                SignalQualityCategory.Poor => 25f,
                _ => 0f
            };
        }

        public float GetConnectionReliability()
        {
            var qualityCategory = GetSignalQualityCategory();
            return qualityCategory switch
            {
                SignalQualityCategory.Excellent => 0.99f, // 99% reliability
                SignalQualityCategory.Good => 0.95f,
                SignalQualityCategory.Fair => 0.85f,
                SignalQualityCategory.Poor => 0.70f,
                _ => 0f
            };
        }

        public void UpdateSINR(float sinr)
        {
            currentSINR = sinr;
            // Update signal quality metrics
            var technology = GetTechnologyType();
            currentQuality = new SignalQualityMetrics(sinr, technology);
            UpdateVisualFeedback();
        }


        #region Debug
        [ContextMenu("Debug Receiver Status")]
        public void DebugReceiverStatus()
        {
            Debug.Log($"=== {uniqueID} Debug ===");
            Debug.Log(GetStatusText());
        }
        #endregion
    }
}