using UnityEngine;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Core.Managers;
using RFSimulation.Utils;

namespace RFSimulation.Core.Components
{
    /// <summary>
    /// Simplified Receiver - constant visuals (mesh + sphere), no signal-based color changes.
    /// </summary>
    public class Receiver : MonoBehaviour
    {
        #region Core Properties
        [Header("Receiver Properties")]
        public string uniqueID;
        public string technology = "5G";
        public float sensitivity = -90f;      // dBm
        public float connectionMargin = 10f;  // dB above sensitivity needed
        public float minimumSINR = -6f;       // dB
        public float receiverHeight;   // meters

        [Header("Status")]
        public float currentSignalStrength = float.NegativeInfinity;
        public float currentSINR = float.NegativeInfinity;

        [Header("Visualization")]
        public bool showSignalSphere = true;

        [Tooltip("Constant base color of the receiver mesh (not tied to signal).")]
        public Color receiverBaseColor = new Color(0.1176f, 0.6549f, 0.9921f); // #1EA7FD

        [Header("Sphere Appearance (constant)")]
        [Tooltip("Constant color of the signal sphere (not tied to signal).")]
        public Color sphereColor = new Color(0.2f, 0.85f, 1f, 1f); // cyan-ish
        [Range(0f, 1f)] public float sphereAlpha = 0.35f;
        #endregion

        #region Private Fields
        private Transmitter connectedTransmitter = null;
        private Renderer receiverRenderer;
        private GameObject signalSphereVisual;
        private Material signalSphereMatInstance;
        private SignalQualityMetrics currentQuality;
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
            UpdateSignalStatus();   // still compute signal, but visuals remain constant
            // No dynamic visual updates needed, but keep sphere active state in sync:
            if (signalSphereVisual != null) signalSphereVisual.SetActive(showSignalSphere);
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

            // keep metrics available for UI/stats (visuals won't change)
            var techType = GetTechnologyType();
            currentQuality = new SignalQualityMetrics(sinr, techType);
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
        }

        public bool IsConnected() => connectedTransmitter != null;

        public void SetTechnology(string tech) => technology = tech;
        #endregion

        #region Signal Helpers (still used by status/throughput, not visuals)
        private SignalQualityCategory GetSignalQualityCategory()
        {
            float margin = currentSignalStrength - sensitivity;
            if (margin < 0f) return SignalQualityCategory.NoService;
            if (margin < 8f) return SignalQualityCategory.Poor;
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
        #endregion

        #region Visualization (constant)
        private void SetupVisualization()
        {
            // Receiver mesh: constant color
            receiverRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>(true);
            if (receiverRenderer != null) SetRendererBaseColor(receiverRenderer, receiverBaseColor);

            // Sphere: create once, constant color
            CreateSignalVisualization();
            ApplySphereColor();
            if (signalSphereVisual != null) signalSphereVisual.SetActive(showSignalSphere);
        }

        private void CreateSignalVisualization()
        {
            if (!showSignalSphere) return;

            signalSphereVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            signalSphereVisual.name = $"SignalSphere_{uniqueID}";
            signalSphereVisual.transform.SetParent(transform);
            signalSphereVisual.transform.localPosition = Vector3.zero;
            signalSphereVisual.transform.localScale = Vector3.one * 0.5f;

            var col = signalSphereVisual.GetComponent<Collider>();
            if (col) Destroy(col);

            // Transparent URP Lit material instance
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
            mat.SetFloat("_ZWrite", 0);  // No depth write
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;

            signalSphereMatInstance = mat;
            var r = signalSphereVisual.GetComponent<Renderer>();
            r.sharedMaterial = signalSphereMatInstance;
        }

        private void ApplySphereColor()
        {
            if (signalSphereMatInstance == null) return;

            var c = sphereColor;
            c.a = sphereAlpha;

            if (signalSphereMatInstance.HasProperty("_BaseColor"))
                signalSphereMatInstance.SetColor("_BaseColor", c);
            else signalSphereMatInstance.color = c;
        }

        public void ToggleVisualization(bool /*showBars unused*/ _, bool showSphere)
        {
            showSignalSphere = showSphere;
            if (signalSphereVisual != null) signalSphereVisual.SetActive(showSphere);
        }

        private void CleanupVisualization()
        {
            if (signalSphereVisual != null)
            {
                DestroyImmediate(signalSphereVisual);
                signalSphereVisual = null;
            }
            signalSphereMatInstance = null;
        }
        #endregion

        #region Runtime Signal Updates (no visual changes)
        private void UpdateSignalStatus()
        {
            if (connectedTransmitter != null)
            {
                float signal = connectedTransmitter.CalculateSignalStrength(this); // <<<
                UpdateSignalStrength(signal, currentSINR);
            }
        }
        #endregion

        #region Utilities
        private void InitializeReceiver()
        {
            if (string.IsNullOrEmpty(uniqueID))
                uniqueID = "RX" + GetInstanceID();

            // Ensure collider for interaction (drag/select)
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
            }

            receiverHeight = GeometryHelper.GetHeightAboveGround(transform.position);
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
                status += $"\nTransmitter: {connectedTransmitter.uniqueID}";
            return status;
        }

        public Transmitter GetConnectedTransmitter() => connectedTransmitter;

        public SignalQualityCategory GetSignalQuality() => GetSignalQualityCategory();

        public float GetExpectedThroughput()
        {
            var q = GetSignalQualityCategory();
            return q switch
            {
                SignalQualityCategory.Excellent => 100f,
                SignalQualityCategory.Good => 75f,
                SignalQualityCategory.Fair => 50f,
                SignalQualityCategory.Poor => 25f,
                _ => 0f
            };
        }

        public float GetConnectionReliability()
        {
            var q = GetSignalQualityCategory();
            return q switch
            {
                SignalQualityCategory.Excellent => 0.99f,
                SignalQualityCategory.Good => 0.95f,
                SignalQualityCategory.Fair => 0.85f,
                SignalQualityCategory.Poor => 0.70f,
                _ => 0f
            };
        }

        public void UpdateSINR(float sinr)
        {
            currentSINR = sinr;
            var techType = GetTechnologyType();
            currentQuality = new SignalQualityMetrics(sinr, techType);
            // visuals remain constant
        }

        private static void SetRendererBaseColor(Renderer r, Color c)
        {
            if (r == null) return;
            var mat = r.material; // instance per receiver
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else mat.color = c;
        }
        #endregion

    }
}
