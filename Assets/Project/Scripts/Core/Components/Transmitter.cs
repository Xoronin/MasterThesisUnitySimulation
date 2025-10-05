using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;
using RFSimulation.Core;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Components;
using RFSimulation.Visualization;

namespace RFSimulation.Core.Components
{
    /// <summary>
    /// Settings for ray tracing functionality
    /// </summary>
    [System.Serializable]
    public class TransmitterSettings
    {
        [Header("Ray Tracing")]
        public bool enableRayTracing = true;
        public PropagationModel propagationModel = PropagationModel.BasicRayTracing;
        public PropagationModel fallbackModel = PropagationModel.LogDistance;

        [Header("Performance")]
        public bool usePerformanceOptimizations = true;
        public int maxReflections = 2;
        public int maxDiffractions = 2;
        public float maxCalculationDistance = 1500f;

        [Header("Mapbox Integration")]
        public LayerMask mapboxBuildingLayer = 1 << 8;
        public bool enableBuildingMaterialDetection = true;
    }

    /// <summary>
    /// Unified Transmitter combining basic RF functionality with advanced ray tracing
    /// </summary>
    public class Transmitter : MonoBehaviour
    {
        #region Core Properties
        [Header("Transmitter Properties")]
        public string uniqueID;
        public float transmitterPower = 40f; // dBm
        public float antennaGain = 12f;      // dBi
        public float frequency = 2400f;      // MHz

        [Header("Propagation Settings")]
        public PropagationModel propagationModel = PropagationModel.LogDistance;

        [Header("Settings")]
        public TransmitterSettings settings = new TransmitterSettings();

        [Header("Visualization")]
        public bool showConnections = true;
        public bool showRayPaths = false;
        #endregion

        // Centralized line manager (creates/updates/removes LineRenderers)
        [SerializeField] private ConnectionLineRenderer connectionRenderer;

        // Per-receiver line bookkeeping (we still keep the list on the transmitter)
        private readonly Dictionary<Receiver, LineRenderer> connectionLines = new();

        // Stable id builder for the centralized renderer
        private string ConnId(Receiver r) => $"{uniqueID}->{r.uniqueID}";

        #region Private Fields
        private readonly List<Receiver> connectedReceivers = new List<Receiver>();

        // Path loss calculators
        private PathLossCalculator pathLossCalculator;

        // Model management
        private PropagationModel _lastAppliedModel;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            InitializeTransmitter();

            if (connectionRenderer == null)
                connectionRenderer = FindAnyObjectByType<ConnectionLineRenderer>();

            SimulationManager.Instance?.RegisterTransmitter(this);
            _lastAppliedModel = propagationModel;
        }

        void Start()
        {
            InitializeCalculators();
            CreateTransmitterModel();
        }

        void Update()
        {
            if (_lastAppliedModel != propagationModel)
                UpdatePropagationModel();

            if (!showConnections || connectionRenderer == null) return;

            // Keep visual lines in sync (positions/color) via the centralized renderer
            foreach (var kv in connectionLines)
            {
                var rx = kv.Key;
                if (rx == null) continue;

                float rssi = CalculateSignalStrength(rx.transform.position);
                connectionRenderer.UpdateConnection(
                    ConnId(rx),
                    transform.position,
                    rx.transform.position,
                    rssi,
                    rx.sensitivity
                );
            }
        }

        void OnDestroy()
        {
            // Return all lines to the renderer pool
            foreach (var rx in new List<Receiver>(connectionLines.Keys))
            {
                connectionRenderer?.RemoveConnection(ConnId(rx));
            }
            connectionLines.Clear();

            SimulationManager.Instance?.RemoveTransmitter(this);
        }
        #endregion

        #region Initialization
        private void InitializeTransmitter()
        {
            if (string.IsNullOrEmpty(uniqueID))
                uniqueID = "TX_" + GetInstanceID();

            // Ensure collider for interaction
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 1f;
            }
        }

        private void InitializeCalculators()
        {
            pathLossCalculator = new PathLossCalculator();

            // Configure settings
            pathLossCalculator.mapboxBuildingLayer = settings.mapboxBuildingLayer;
            pathLossCalculator.preferRayTracing = settings.enableRayTracing;
            pathLossCalculator.maxDistance = settings.maxCalculationDistance;

            Debug.Log($"[UnifiedTransmitter] {uniqueID} initialized with ray tracing support");
        }
        #endregion

        #region Core Signal Calculation
        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            var context = CreatePropagationContext(receiverPosition);
            context.Model = SelectOptimalModel(context);

            try
            {
                float receivedPower = pathLossCalculator.CalculateReceivedPower(context);

                if (showRayPaths)
                    VisualizeRayPath(transform.position, receiverPosition, receivedPower);

                return receivedPower;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UnifiedTransmitter] Error calculating signal strength: {e.Message}");
                return float.NegativeInfinity;
            }
        }

        private PropagationContext CreatePropagationContext(Vector3 receiverPosition)
        {
            var context = PropagationContext.Create(
                transform.position,
                receiverPosition,
                transmitterPower,
                frequency
            );

            context.AntennaGainDbi = antennaGain;
            context.ReceiverSensitivityDbm = -105f;
            context.BuildingLayers = settings.mapboxBuildingLayer;

            return context;
        }

        private PropagationModel SelectOptimalModel(PropagationContext context)
        {
            float distance = context.Distance;

            if (!settings.enableRayTracing)
                return propagationModel;

            if (distance > settings.maxCalculationDistance)
                return settings.fallbackModel;

            return propagationModel;
        }
        #endregion

        #region Connection Management
        public bool CanConnectTo(Receiver receiver)
        {
            float signalStrength = CalculateSignalStrength(receiver.transform.position);
            return signalStrength >= (receiver.sensitivity + receiver.connectionMargin);
        }

        public void ConnectToReceiver(Receiver receiver)
        {
            if (receiver == null || connectedReceivers.Contains(receiver))
                return;

            if (!CanConnectTo(receiver))
                return;

            connectedReceivers.Add(receiver);
            receiver.SetConnectedTransmitter(this);

            // Create the visual line via the centralized renderer
            if (showConnections && connectionRenderer != null && !connectionLines.ContainsKey(receiver))
            {
                float rssi = CalculateSignalStrength(receiver.transform.position);
                var lr = connectionRenderer.CreateConnection(
                    ConnId(receiver),
                    transform.position,
                    receiver.transform.position,
                    rssi,
                    receiver.sensitivity
                );
                if (lr != null)
                    connectionLines[receiver] = lr;
            }
        }

        public void DisconnectFromReceiver(Receiver receiver)
        {
            if (receiver == null) return;

            // Remove visual first
            if (connectionLines.ContainsKey(receiver))
            {
                connectionRenderer?.RemoveConnection(ConnId(receiver));
                connectionLines.Remove(receiver);
            }

            if (connectedReceivers.Remove(receiver))
                receiver.ClearConnection();
        }

        public void ClearAllConnections()
        {
            // Copy to avoid modifying collection during iteration
            foreach (var rx in new List<Receiver>(connectedReceivers))
                DisconnectFromReceiver(rx);
        }

        private void RefreshConnections()
        {
            // Drop links that no longer meet criteria
            foreach (var receiver in new List<Receiver>(connectedReceivers))
            {
                if (!CanConnectTo(receiver))
                    DisconnectFromReceiver(receiver);
            }
        }

        private void TryConnectEligibleReceivers()
        {
            var allReceivers = SimulationManager.Instance != null
                ? SimulationManager.Instance.receivers.ToArray()
                : GameObject.FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);

            foreach (var r in allReceivers)
            {
                if (r == null) continue;
                if (!IsConnectedTo(r) && CanConnectTo(r))
                    ConnectToReceiver(r);
            }
        }
        #endregion

        #region Parameter Updates with Validation
        public bool UpdateTransmitterPower(float newPowerDbm)
        {
            if (newPowerDbm >= 0f && newPowerDbm <= 80f)
            {
                transmitterPower = newPowerDbm;
                RefreshConnections();
                return true;
            }
            Debug.LogWarning($"Invalid power value: {newPowerDbm} dBm. Must be 0–80 dBm.");
            return false;
        }

        public bool UpdateFrequency(float newFrequencyMHz)
        {
            if (newFrequencyMHz > 0f && newFrequencyMHz <= 100000f)
            {
                frequency = newFrequencyMHz;
                RefreshConnections();
                return true;
            }
            Debug.LogWarning($"Invalid frequency: {newFrequencyMHz} MHz. Must be > 0.");
            return false;
        }

        public bool UpdateAntennaGain(float newGainDbi)
        {
            if (newGainDbi >= 0f && newGainDbi <= 50f)
            {
                antennaGain = newGainDbi;
                RefreshConnections();
                return true;
            }
            Debug.LogWarning($"Invalid gain: {newGainDbi} dBi. Must be 0–50 dBi.");
            return false;
        }

        private void UpdatePropagationModel()
        {
            _lastAppliedModel = propagationModel;
            RefreshConnections();
            TryConnectEligibleReceivers();
        }
        #endregion

        #region Configuration Methods
        public void SetRayTracingEnabled(bool enabled)
        {
            settings.enableRayTracing = enabled;
            RefreshConnections();
        }

        public void SetMaxDistance(float maxDistance)
        {
            settings.maxCalculationDistance = maxDistance;
        }

        public void Updatesettings(TransmitterSettings newSettings)
        {
            settings = newSettings;
            InitializeCalculators(); // Reinitialize with new settings
        }

        public float EstimateCoverageRadius()
        {
            var baseContext = CreatePropagationContext(transform.position + Vector3.forward);
            return pathLossCalculator.EstimateCoverageRadius(baseContext);
        }
        #endregion

        #region Visualization
        private void CreateTransmitterModel()
        {
            CreateAntennaTower();
            CreateTowerBase();
        }

        private void CreateAntennaTower()
        {
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "AntennaPole";
            pole.transform.SetParent(transform);
            pole.transform.localPosition = Vector3.up * 5f;
            pole.transform.localScale = new Vector3(0.2f, 5f, 0.2f);

            var renderer = pole.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", Color.blue);
                renderer.material = material;
            }
        }

        private void CreateTowerBase()
        {
            GameObject towerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerBase.name = "TowerBase";
            towerBase.transform.SetParent(transform);
            towerBase.transform.localPosition = Vector3.up * 0.5f;
            towerBase.transform.localScale = new Vector3(1f, 0.5f, 1f);

            var renderer = towerBase.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", Color.gray);
                renderer.material = material;
            }

            // Remove the child collider so raycasts hit the ROOT collider we add below
            var childCol = towerBase.GetComponent<Collider>();
            if (childCol) Destroy(childCol);

            // Build/refresh a single CapsuleCollider on the ROOT that fits all children
            RebuildTransmitterCollider();
        }

        /// <summary>
        /// Ensures a single CapsuleCollider on the root that encapsulates all child renderers.
        /// Disables any other child colliders so selection/drags hit the root only.
        /// </summary>
        private void RebuildTransmitterCollider()
        {
            // Collect bounds of all child renderers (base, pole, etc.)
            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;

            Bounds world = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) world.Encapsulate(rends[i].bounds);

            // Ensure a CapsuleCollider on the root
            var cap = GetComponent<CapsuleCollider>();
            if (cap == null) cap = gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1; // Y axis

            // Convert world center to local
            cap.center = transform.InverseTransformPoint(world.center);

            // Approximate radius from X/Z; height from Y size (assumes uniform root scale)
            cap.radius = Mathf.Max(world.extents.x, world.extents.z);
            cap.height = world.size.y;

            // Disable any other colliders under this object (so root gets the hit)
            var allCols = GetComponentsInChildren<Collider>(true);
            foreach (var c in allCols)
                if (c != cap) Destroy(c);
        }


        private void VisualizeRayPath(Vector3 start, Vector3 end, float signalStrength)
        {
            Debug.DrawLine(start, end, GetSignalStrengthColor(signalStrength), 2f);
        }

        private Color GetSignalStrengthColor(float signalStrength)
        {
            if (float.IsNegativeInfinity(signalStrength)) return Color.black;

            float normalized = Mathf.Clamp01((signalStrength + 120f) / 40f); // -120 to -80 dBm range

            if (normalized > 0.8f) return Color.green;
            if (normalized > 0.6f) return Color.yellow;
            if (normalized > 0.4f) return Color.blue;
            return Color.red;
        }

        public void ToggleConnectionLineVisualization(bool show)
        {
            showConnections = show;
            connectionRenderer?.SetLineVisibility(showConnections);
        }
        #endregion

        #region Ray Visualization
        public void VisualizeRaysToReceivers()
        {
            if (pathLossCalculator != null)
            {
                var model = pathLossCalculator.GetRayTracingModel();
                if (model != null)
                {
                    model.enableRayVisualization = true;

                    var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.None);
                    foreach (var receiver in receivers)
                        CalculateSignalStrength(receiver.transform.position);
                }
            }
        }

        public void EnableRayVisualization()
        {
            showRayPaths = true;

            var model = pathLossCalculator?.GetRayTracingModel();
            if (model != null)
            {
                model.enableRayVisualization = true;
                model.showDirectRays = true;
                model.showReflectionRays = true;
                model.showDiffractionRays = true;
                model.persistentRays = true;
                model.rayDisplayDuration = 10f;

                var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.None);
                foreach (var receiver in receivers)
                    CalculateSignalStrength(receiver.transform.position);
            }
            else
            {
                Debug.LogError("Could not access RayTracingModel for visualization");
            }
        }

        public void DisableRayVisualization()
        {
            showRayPaths = false;

            var urbanModel = pathLossCalculator?.GetRayTracingModel();
            if (urbanModel != null)
            {
                urbanModel.enableRayVisualization = false;
                urbanModel.ClearAllRays();
                Debug.Log("Ray visualization disabled and cleared");
            }
        }
        #endregion

        #region Public Properties and Utilities
        public List<Receiver> GetConnectedReceivers() => new List<Receiver>(connectedReceivers);
        public int GetConnectionCount() => connectedReceivers.Count;
        public bool IsConnectedTo(Receiver receiver) => connectedReceivers.Contains(receiver);
        #endregion
    }
}
