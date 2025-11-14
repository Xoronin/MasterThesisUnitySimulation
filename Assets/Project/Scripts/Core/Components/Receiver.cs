using UnityEngine;
using System;
using RFSimulation.Propagation.Core;
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

        public TechnologyType technology;

        [Header("Auto-configured from Technology (can override)")]
        public float sensitivity = -100f;
        public float connectionMargin = 6f;
        public float receiverHeight = 1.5f;
        public float receiverGainDbi = 0f;

        [Header("Status")]
        public float currentSignalStrength = float.NegativeInfinity;

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

        public void ApplyTechnologySpec()
        {
            techSpec = TechnologySpecifications.GetSpec(technology);

            sensitivity = techSpec.SensitivityDbm;
            connectionMargin = techSpec.ConnectionMarginDb;
            receiverHeight = techSpec.TypicalRxHeight;
        }

        public void SetTechnology(TechnologyType tech)
        {
            technology = tech;
            ApplyTechnologySpec();
        }

        public TechnologySpec GetTechnologySpec()
        {
            if (techSpec == null)
            {
                techSpec = TechnologySpecifications.GetSpec(technology);
            }
            return techSpec;
        }
        #endregion

        #region Public API
        public bool CanConnect()
        {
            return currentSignalStrength >= (sensitivity + connectionMargin);
        }

        public bool IsAboveSensitivity()
        {
            return currentSignalStrength >= sensitivity;
        }

        public void UpdateSignalStrength(float signalStrength)
        {
            if (signalStrength >= sensitivity)
            {
                currentSignalStrength = signalStrength;
            }
            else
            {
                ClearSignalMetrics();
            }
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

        private void ClearSignalMetrics()
        {
            currentSignalStrength = float.NegativeInfinity;
        }

        public bool IsConnected() => connectedTransmitter != null;
        public Transmitter GetConnectedTransmitter() => connectedTransmitter;

        public bool HasValidSignal()
        {
            return !float.IsNegativeInfinity(currentSignalStrength) &&
                   currentSignalStrength >= sensitivity;
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
            signalSphereVisual.transform.localScale = Vector3.one * 1f;

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
                UpdateSignalStrength(signal);
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
