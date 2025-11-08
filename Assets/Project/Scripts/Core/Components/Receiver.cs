using UnityEngine;
using System;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Core.Managers;
using RFSimulation.Utils;
using RFSimulation.Core;

namespace RFSimulation.Core.Components
{
    public class Receiver : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Receiver Properties")]
        public string uniqueID;

        [Tooltip("Technology type: LTE, 5GmmWave, 5GSub6")]
        public string technology = "5GSub6";

        [Header("Auto-configured from Technology (can override)")]
        public float sensitivity = -100f;
        public float connectionMargin = 6f;
        public float minimumSINR = -3f;
        public float receiverHeight = 1.5f;
        public float receiverGainDbi = 0f;

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
        private TechnologySpec techSpec;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            InitializeReceiver();
            SimulationManager.Instance?.RegisterReceiver(this);
        }

        void Start()
        {
            ApplyTechnologySpec();

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

        #region Technology Configuration
        /// <summary>
        /// Apply technology specifications to receiver parameters.
        /// Call this after changing technology string.
        /// </summary>
        public void ApplyTechnologySpec()
        {
            techSpec = TechnologySpecifications.GetSpec(technology);

            sensitivity = techSpec.SensitivityDbm;
            connectionMargin = techSpec.ConnectionMarginDb;
            minimumSINR = techSpec.MinimumSINRDb;
        }

        /// <summary>
        /// Change technology and auto-reconfigure.
        /// </summary>
        public void SetTechnology(string tech)
        {
            technology = tech;
            ApplyTechnologySpec();
        }

        /// <summary>
        /// Get the parsed technology type.
        /// </summary>
        public TechnologyType GetTechnologyType()
        {
            return TechnologySpecifications.ParseTechnologyString(technology);
        }

        /// <summary>
        /// Get current technology specification.
        /// </summary>
        public TechnologySpec GetTechnologySpec()
        {
            if (techSpec == null)
            {
                techSpec = TechnologySpecifications.GetSpec(technology);
            }
            return techSpec;
        }
        #endregion

        #region Signal Quality (Technology-Aware)
        /// <summary>
        /// Get signal quality using technology-specific thresholds.
        /// </summary>
        private SignalQualityCategory GetSignalQualityCategory()
        {
            float margin = currentSignalStrength - sensitivity;

            // No service if below sensitivity
            if (margin < 0f) return SignalQualityCategory.NoService;

            // Use technology-specific thresholds
            var spec = GetTechnologySpec();

            if (margin < spec.PoorThresholdDb)
                return SignalQualityCategory.Poor;

            if (margin < spec.FairThresholdDb)
                return SignalQualityCategory.Fair;

            if (margin < spec.GoodThresholdDb)
                return SignalQualityCategory.Good;

            return SignalQualityCategory.Excellent;
        }
        #endregion

        #region Public API
        public bool CanConnect()
        {
            return currentSignalStrength >= (sensitivity + connectionMargin) &&
                   currentSINR >= minimumSINR;
        }

        public bool IsAboveSensitivity()
        {
            return currentSignalStrength >= sensitivity;
        }

        public void UpdateSignalStrength(float signalStrength, float sinr = 0f)
        {
            if (signalStrength >= sensitivity)
            {
                currentSignalStrength = signalStrength;
                currentSINR = sinr;
                currentQuality = new SignalQualityMetrics(sinr, GetTechnologyType());
            }
            else
            {
                // Below sensitivity = no usable signal
                ClearSignalMetrics();
            }
        }

        public void UpdateSINR(float sinr)
        {
            currentSINR = sinr;
            currentQuality = new SignalQualityMetrics(sinr, GetTechnologyType());
        }

        public void SetConnectedTransmitter(Transmitter transmitter)
        {
            connectedTransmitter = transmitter;
        }

        public void ClearConnection()
        {
            connectedTransmitter = null;
            ClearSignalMetrics();
        }

        /// <summary>
        /// Clear all signal metrics (used when no connection or below sensitivity).
        /// </summary>
        private void ClearSignalMetrics()
        {
            currentSignalStrength = float.NegativeInfinity;
            currentSINR = float.NegativeInfinity;
            currentQuality = null;
        }

        public bool IsConnected() => connectedTransmitter != null;
        public Transmitter GetConnectedTransmitter() => connectedTransmitter;
        public SignalQualityCategory GetSignalQuality() => GetSignalQualityCategory();

        public bool HasValidSignal()
        {
            return !float.IsNegativeInfinity(currentSignalStrength) &&
                   currentSignalStrength >= sensitivity;
        }
        #endregion

        #region Metrics
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
