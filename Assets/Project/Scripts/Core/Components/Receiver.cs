using UnityEngine;
using System;
using System.Runtime.CompilerServices;
using System.Runtime;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Core.Managers;
using RFSimulation.Utils;

namespace RFSimulation.Core.Components
{
    /// <summary>
    /// Receiver with constant visuals; computes signal and SINR for status/metrics.
    /// </summary>
    public class Receiver : MonoBehaviour
    {
        #region Data Contracts
        /// <summary>
        /// Immutable snapshot of a receiver's state and derived metrics.
        /// </summary>
        public sealed class ReceiverInfo
        {
            public string UniqueID { get; set; }
            public string Technology { get; set; }
            public TechnologyType TechnologyType { get; set; }
            public float Sensitivity { get; set; }
            public float ConnectionMargin { get; set; }
            public float MinimumSINR { get; set; }
            public float ReceiverHeight { get; set; }

            public float CurrentSignalStrength { get; set; }
            public float CurrentSINR { get; set; }
            public SignalQualityCategory Quality { get; set; }
            public float ExpectedThroughput { get; set; }
            public float ConnectionReliability { get; set; }

            public bool IsConnected { get; set; }
            public string ConnectedTransmitterID { get; set; }

            public Vector3 WorldPosition { get; set; }
        }
        #endregion

        #region Inspector Fields
        [Header("Receiver Properties")]
        public string uniqueID;
        public string technology = "5GSUB6";
        public float sensitivity = -90f;
        public float connectionMargin = 6f;
        public float minimumSINR = -5f;
        public float receiverHeight;

        [Header("Status")]
        public float currentSignalStrength = float.NegativeInfinity;
        public float currentSINR = float.NegativeInfinity;

        [Header("Visualization")]
        public Color receiverBaseColor = new Color(0.1176f, 0.6549f, 0.9921f);
        public Color sphereColor = new Color(0.2f, 0.85f, 1f, 1f);
        [Range(0f, 1f)] public float sphereAlpha = 0.35f;
        #endregion

        #region Private Fields
        private Transmitter connectedTransmitter;
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
            UpdateSignalStatus();
        }

        void OnDestroy()
        {
            ClearConnection();
            CleanupVisualization();
            SimulationManager.Instance?.RemoveReceiver(this);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Returns a single object containing all receiver parameters and derived values.
        /// </summary>
        public ReceiverInfo GetInfo()
        {
            var techType = GetTechnologyType();
            var quality = GetSignalQualityCategory();

            return new ReceiverInfo
            {
                UniqueID = uniqueID,
                Technology = technology,
                TechnologyType = techType,
                Sensitivity = sensitivity,
                ConnectionMargin = connectionMargin,
                MinimumSINR = minimumSINR,
                ReceiverHeight = receiverHeight,

                CurrentSignalStrength = currentSignalStrength,
                CurrentSINR = currentSINR,
                Quality = quality,
                ExpectedThroughput = GetExpectedThroughputInternal(quality),
                ConnectionReliability = GetConnectionReliabilityInternal(quality),

                IsConnected = IsConnected(),
                ConnectedTransmitterID = connectedTransmitter != null ? connectedTransmitter.uniqueID : null,

                WorldPosition = transform.position,
            };
        }

        /// <summary>
        /// Updates measured signal strength and SINR.
        /// </summary>
        public void UpdateSignalStrength(float signalStrength, float sinr = 0f)
        {
            currentSignalStrength = signalStrength;
            currentSINR = sinr;
            currentQuality = new SignalQualityMetrics(sinr, GetTechnologyType());
        }

        /// <summary>
        /// Updates SINR and derived quality metrics.
        /// </summary>
        public void UpdateSINR(float sinr)
        {
            currentSINR = sinr;
            currentQuality = new SignalQualityMetrics(sinr, GetTechnologyType());
        }

        /// <summary>
        /// Returns whether receiver meets connection thresholds.
        /// </summary>
        public bool CanConnect()
        {
            return currentSignalStrength >= (sensitivity + connectionMargin) &&
                   currentSINR >= minimumSINR;
        }

        /// <summary>
        /// Sets the currently connected transmitter.
        /// </summary>
        public void SetConnectedTransmitter(Transmitter transmitter)
        {
            connectedTransmitter = transmitter;
        }

        /// <summary>
        /// Clears connection and resets metrics.
        /// </summary>
        public void ClearConnection()
        {
            connectedTransmitter = null;
            currentSignalStrength = float.NegativeInfinity;
            currentSINR = float.NegativeInfinity;
        }

        public bool IsConnected() => connectedTransmitter != null;

        public void SetTechnology(string tech) => technology = tech;

        public Transmitter GetConnectedTransmitter() => connectedTransmitter;

        public SignalQualityCategory GetSignalQuality() => GetSignalQualityCategory();
        #endregion

        #region Metrics & Classification
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
            var t = (technology ?? string.Empty).ToUpperInvariant().Replace("-", " ").Replace("_", " ").Trim();
            if (t.Contains("5GMMWAVE") || t.Contains("MM WAVE")) return TechnologyType.FiveGmmWave;
            if (t.Contains("5GSUB6") || t.Contains("SUB6") || t.Contains("SUB 6") || t.Contains("SUB-6")) return TechnologyType.FiveGSub6;
            if (t.Contains("LTE")) return TechnologyType.LTE;
            return TechnologyType.FiveGSub6;
        }

        private float GetExpectedThroughputInternal(SignalQualityCategory q)
        {
            return q switch
            {
                SignalQualityCategory.Excellent => 100f,
                SignalQualityCategory.Good => 75f,
                SignalQualityCategory.Fair => 50f,
                SignalQualityCategory.Poor => 25f,
                _ => 0f
            };
        }

        private float GetConnectionReliabilityInternal(SignalQualityCategory q)
        {
            return q switch
            {
                SignalQualityCategory.Excellent => 0.99f,
                SignalQualityCategory.Good => 0.95f,
                SignalQualityCategory.Fair => 0.85f,
                SignalQualityCategory.Poor => 0.70f,
                _ => 0f
            };
        }
        #endregion

        #region Visualization
        private void SetupVisualization()
        {
            receiverRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>(true);
            if (receiverRenderer != null) SetRendererBaseColor(receiverRenderer, receiverBaseColor);
            CreateSignalVisualization();
            ApplySphereColor();
        }

        private void CreateSignalVisualization()
        {
            signalSphereVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            signalSphereVisual.name = $"SignalSphere_{uniqueID}";
            signalSphereVisual.transform.SetParent(transform);
            signalSphereVisual.transform.localPosition = Vector3.zero;
            signalSphereVisual.transform.localScale = Vector3.one * 0.5f;

            var col = signalSphereVisual.GetComponent<Collider>();
            if (col) Destroy(col);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_ZWrite", 0);
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
            else
                signalSphereMatInstance.color = c;
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

        #region Runtime
        private void UpdateSignalStatus()
        {
            if (connectedTransmitter != null)
            {
                float signal = connectedTransmitter.CalculateSignalStrength(this);
                UpdateSignalStrength(signal, currentSINR);
            }
        }
        #endregion

        #region Initialization & Utilities
        private void InitializeReceiver()
        {
            if (string.IsNullOrEmpty(uniqueID))
                uniqueID = "RX" + GetInstanceID();

            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
            }

            receiverHeight = GeometryHelper.GetHeightAboveGround(transform.position);
        }

        private static void SetRendererBaseColor(Renderer r, Color c)
        {
            if (r == null) return;
            var mat = r.material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else mat.color = c;
        }
        #endregion
    }
}
