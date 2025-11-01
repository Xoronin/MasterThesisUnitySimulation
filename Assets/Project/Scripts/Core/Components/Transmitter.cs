using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Components;
using RFSimulation.Utils;
using RFSimulation.Visualization;

namespace RFSimulation.Core.Components
{
    /// <summary>
    /// Settings for ray tracing functionality.
    /// </summary>
    [System.Serializable]
    public class TransmitterSettings
    {
        [Header("Ray Tracing")]
        public bool enableRayTracing = true;
        public PropagationModel propagationModel;

        [Header("Performance")]
        public bool usePerformanceOptimizations = true;
        public int maxReflections = 2;
        public int maxDiffractions = 2;
        public float maxCalculationDistance = 3000f;

        [Header("Mapbox Integration")]
        public LayerMask mapboxBuildingLayer = 1 << 8;
        public LayerMask terrainLayer = 6;
        public bool enableBuildingMaterialDetection = true;
    }

    /// <summary>
    /// Unified Transmitter combining RF functionality with optional ray tracing.
    /// </summary>
    public class Transmitter : MonoBehaviour
    {
        #region Data Contracts
        /// <summary>
        /// Snapshot of transmitter configuration, state, and derived values.
        /// </summary>
        public sealed class TransmitterInfo
        {
            public string UniqueID { get; set; }
            public string Technology { get; set; }
            public TechnologyType TechnologyType { get; set; }

            public float TransmitterPowerDbm { get; set; }
            public float AntennaGainDbi { get; set; }
            public float FrequencyMHz { get; set; }
            public float TransmitterHeightM { get; set; }

            public PropagationModel PropagationModel { get; set; }
            public bool RayTracingEnabled { get; set; }
            public float MaxCalculationDistanceM { get; set; }
            public int MaxReflections { get; set; }
            public int MaxDiffractions { get; set; }

            public bool ShowConnections { get; set; }
            public bool ShowRayPaths { get; set; }

            public int ConnectedReceiverCount { get; set; }
            public Vector3 WorldPosition { get; set; }
            public Vector3 AntennaWorldPosition { get; set; }

        }
        #endregion

        #region Inspector Fields
        [Header("Transmitter Properties")]
        public string uniqueID;
        public string technology = "5G sub-6 GHz";
        public float transmitterPower = 40f;
        public float antennaGain = 12f;
        public float frequency = 2400f;
        public float transmitterHeight;

        [Header("Propagation Settings")]
        public PropagationModel propagationModel;

        [Header("Settings")]
        public TransmitterSettings settings = new TransmitterSettings();

        [Header("Visualization")]
        private RayVisualization rayVisualization;
        public bool showConnections = true;
        public bool showRayPaths = false;

        [Header("Defaults")]
        public float defaultReceiverSensitivityDbm = -95f;

        [SerializeField] private ConnectionLineRenderer connectionRenderer;
        #endregion

        #region Private Fields
        private readonly Dictionary<Receiver, LineRenderer> connectionLines = new Dictionary<Receiver, LineRenderer>();
        private readonly List<Receiver> connectedReceivers = new List<Receiver>();
        private PathLossCalculator pathLossCalculator;
        private PropagationModel _lastAppliedModel;
        public Transform antennaOrigin;

        [SerializeField] private float baseHalfHeight = 0.6f;
        [SerializeField] private float mastHalfHeight = 8.0f;
        [SerializeField] private float mastRadius = 0.25f;
        [SerializeField] private Vector3 panelSize = new Vector3(0.35f, 1.6f, 0.12f);
        [SerializeField] private float panelForwardOffset = 0.6f;
        [SerializeField] private float panelMountHeightFromBaseTop = 12.0f;
        [SerializeField] private float panelElectricalDowntiltDeg = 3.0f;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            InitializeTransmitter();
            panelMountHeightFromBaseTop = transmitterHeight;
            if (connectionRenderer == null) connectionRenderer = FindAnyObjectByType<ConnectionLineRenderer>();
            if (rayVisualization == null) rayVisualization = FindAnyObjectByType<RayVisualization>();
            SimulationManager.Instance?.RegisterTransmitter(this);
            _lastAppliedModel = propagationModel;
        }

        void Start()
        {
            if (pathLossCalculator == null) InitializeCalculators();
            panelMountHeightFromBaseTop = transmitterHeight;
            CreateTransmitterModel();
            SetAntennaOrigin(transmitterHeight);
        }

        void Update()
        {
            if (_lastAppliedModel != propagationModel) UpdatePropagationModel();

            if (!showConnections || connectionRenderer == null) return;

            panelMountHeightFromBaseTop = transmitterHeight;
            SetAntennaOrigin(transmitterHeight);

            foreach (var kv in connectionLines)
            {
                var rx = kv.Key;
                if (rx == null) continue;
                float rssi = CalculateSignalStrength(rx);
                connectionRenderer.UpdateConnection(
                    ConnId(rx),
                    GetAntennaOrigin(),
                    rx.transform.position,
                    rssi,
                    rx.sensitivity
                );
            }
        }

        void OnDestroy()
        {
            foreach (var rx in new List<Receiver>(connectionLines.Keys))
                connectionRenderer?.RemoveConnection(ConnId(rx));
            connectionLines.Clear();

            rayVisualization.ClearAll();

            SimulationManager.Instance?.RemoveTransmitter(this);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Returns a single object containing all transmitter parameters and derived values.
        /// </summary>
        public TransmitterInfo GetInfo()
        {
            return new TransmitterInfo
            {
                UniqueID = uniqueID,
                Technology = technology,

                TransmitterPowerDbm = transmitterPower,
                AntennaGainDbi = antennaGain,
                FrequencyMHz = frequency,
                TransmitterHeightM = transmitterHeight,

                PropagationModel = propagationModel,
                RayTracingEnabled = settings.enableRayTracing,
                MaxCalculationDistanceM = settings.maxCalculationDistance,
                MaxReflections = settings.maxReflections,
                MaxDiffractions = settings.maxDiffractions,

                ShowConnections = showConnections,
                ShowRayPaths = showRayPaths,

                ConnectedReceiverCount = connectedReceivers.Count,
                WorldPosition = transform.position,
                AntennaWorldPosition = GetAntennaWorldPos(),
            };
        }

        /// <summary>
        /// Calculates received power (dBm) at a world position.
        /// </summary>
        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            var context = CreatePropagationContext(receiverPosition, null);
            try { return pathLossCalculator.CalculateReceivedPower(context); }
            catch { return float.NegativeInfinity; }
        }

        /// <summary>
        /// Calculates received power (dBm) for a receiver instance.
        /// </summary>
        public float CalculateSignalStrength(Receiver receiver)
        {
            if (receiver == null) return float.NegativeInfinity;
            var context = CreatePropagationContext(receiver.transform.position, receiver.sensitivity);
            try { return pathLossCalculator.CalculateReceivedPower(context); }
            catch { return float.NegativeInfinity; }
        }

        /// <summary>
        /// Returns the antenna world position.
        /// </summary>
        public Vector3 GetAntennaWorldPos()
        {
            return antennaOrigin != null ? antennaOrigin.position : transform.position;
        }

        /// <summary>
        /// Returns connected receivers.
        /// </summary>
        public List<Receiver> GetConnectedReceivers() => new List<Receiver>(connectedReceivers);

        /// <summary>
        /// Returns the connection count.
        /// </summary>
        public int GetConnectionCount() => connectedReceivers.Count;

        /// <summary>
        /// Returns whether the transmitter is connected to the given receiver.
        /// </summary>
        public bool IsConnectedTo(Receiver receiver) => connectedReceivers.Contains(receiver);

        /// <summary>
        /// Sets the propagation model and triggers updates.
        /// </summary>
        public void SetPropagationModel(PropagationModel model)
        {
            propagationModel = model;
            UpdatePropagationModel();
        }

        /// <summary>
        /// Clears cached path-loss computations.
        /// </summary>
        public void ClearPathLossCache()
        {
            pathLossCalculator?.ClearCache();
        }
        #endregion

        #region Core Signal Calculation
        private PropagationContext CreatePropagationContext(Vector3 receiverPosition, float? receiverSensitivityDbm)
        {
            var txPos = GetAntennaWorldPos();
            var receiverHeight = GeometryHelper.GetHeightAboveGround(receiverPosition);

            var context = PropagationContext.Create(
                txPos,
                receiverPosition,
                transmitterPower,
                frequency,
                transmitterHeight,
                receiverHeight
            );

            context.Model = propagationModel;
            context.AntennaGainDbi = antennaGain;
            context.ReceiverSensitivityDbm = receiverSensitivityDbm ?? defaultReceiverSensitivityDbm;
            context.BuildingLayers = settings.mapboxBuildingLayer;
            return context;
        }

        #endregion

        #region Connection Management
        /// <summary>
        /// Returns whether a receiver can connect based on received power.
        /// </summary>
        public bool CanConnectTo(Receiver receiver)
        {
            float rssi = CalculateSignalStrength(receiver);
            return rssi >= (receiver.sensitivity + receiver.connectionMargin);
        }

        /// <summary>
        /// Connects to a receiver and creates a connection line if enabled.
        /// </summary>
        public void ConnectToReceiver(Receiver receiver)
        {
            if (receiver == null || connectedReceivers.Contains(receiver)) return;
            if (!CanConnectTo(receiver)) return;

            connectedReceivers.Add(receiver);
            receiver.SetConnectedTransmitter(this);

            if (showConnections && connectionRenderer != null && !connectionLines.ContainsKey(receiver))
            {
                float rssi = CalculateSignalStrength(receiver);
                var lr = connectionRenderer.CreateConnection(
                    ConnId(receiver),
                    GetAntennaOrigin(),
                    receiver.transform.position,
                    rssi,
                    receiver.sensitivity
                );
                if (lr != null) connectionLines[receiver] = lr;
            }
        }

        /// <summary>
        /// Disconnects from a receiver and removes its connection line.
        /// </summary>
        public void DisconnectFromReceiver(Receiver receiver)
        {
            if (receiver == null) return;

            if (connectionLines.ContainsKey(receiver))
            {
                connectionRenderer?.RemoveConnection(ConnId(receiver));
                connectionLines.Remove(receiver);
            }

            if (connectedReceivers.Remove(receiver))
                receiver.ClearConnection();
        }

        /// <summary>
        /// Disconnects from all receivers.
        /// </summary>
        public void ClearAllConnections()
        {
            foreach (var rx in new List<Receiver>(connectedReceivers))
                DisconnectFromReceiver(rx);
        }

        private void RefreshConnections()
        {
            foreach (var receiver in new List<Receiver>(connectedReceivers))
                if (!CanConnectTo(receiver)) DisconnectFromReceiver(receiver);
        }

        private void TryConnectEligibleReceivers()
        {
            var allReceivers = SimulationManager.Instance != null
                ? SimulationManager.Instance.receivers.ToArray()
                : GameObject.FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);

            foreach (var r in allReceivers)
                if (r != null && !IsConnectedTo(r) && CanConnectTo(r))
                    ConnectToReceiver(r);
        }
        #endregion

        #region Parameter Updates
        /// <summary>
        /// Updates transmitter power (dBm) with validation.
        /// </summary>
        public bool UpdateTransmitterPower(float newPowerDbm)
        {
            if (newPowerDbm >= 0f && newPowerDbm <= 80f)
            {
                transmitterPower = newPowerDbm;
                RefreshConnections();
                TryConnectEligibleReceivers();
                SimulationManager.Instance?.RecomputeForTransmitter(this);
                return true;
            }
            Debug.LogWarning($"Invalid power value: {newPowerDbm} dBm. Must be 0–80 dBm.");
            return false;
        }

        /// <summary>
        /// Updates frequency (MHz) with validation.
        /// </summary>
        public bool UpdateFrequency(float newFrequencyMHz)
        {
            if (newFrequencyMHz > 0f && newFrequencyMHz <= 100000f)
            {
                frequency = newFrequencyMHz;
                RefreshConnections();
                TryConnectEligibleReceivers();
                SimulationManager.Instance?.RecomputeForTransmitter(this);
                return true;
            }
            Debug.LogWarning($"Invalid frequency: {newFrequencyMHz} MHz. Must be > 0.");
            return false;
        }

        /// <summary>
        /// Updates antenna gain (dBi) with validation.
        /// </summary>
        public bool UpdateAntennaGain(float newGainDbi)
        {
            if (newGainDbi >= 0f && newGainDbi <= 50f)
            {
                antennaGain = newGainDbi;
                RefreshConnections();
                TryConnectEligibleReceivers();
                SimulationManager.Instance?.RecomputeForTransmitter(this);
                return true;
            }
            Debug.LogWarning($"Invalid gain: {newGainDbi} dBi. Must be 0–50 dBi.");
            return false;
        }

        /// <summary>
        /// Updates transmitter height (m) and refreshes connections.
        /// </summary>
        public void UpdateTransmitterHeight(float newHeightM)
        {
            transmitterHeight = newHeightM;
            SetAntennaOrigin(transmitterHeight);
            RefreshConnections();
            TryConnectEligibleReceivers();
            SimulationManager.Instance?.RecomputeForTransmitter(this);
        }

        private void UpdatePropagationModel()
        {
            _lastAppliedModel = propagationModel;
            if (pathLossCalculator == null) InitializeCalculators();

            RefreshConnections();
            TryConnectEligibleReceivers();

            pathLossCalculator.ClearCache();
            SimulationManager.Instance?.RecomputeForTransmitter(this);
        }
        #endregion

        #region Configuration
        /// <summary>
        /// Enables or disables ray tracing.
        /// </summary>
        public void SetRayTracingEnabled(bool enabled)
        {
            settings.enableRayTracing = enabled;
            RefreshConnections();
        }

        /// <summary>
        /// Sets the maximum calculation distance (m).
        /// </summary>
        public void SetMaxDistance(float maxDistance)
        {
            settings.maxCalculationDistance = maxDistance;
        }

        /// <summary>
        /// Applies new settings and reinitializes calculators.
        /// </summary>
        public void UpdateSettings(TransmitterSettings newSettings)
        {
            settings = newSettings;
            InitializeCalculators();
        }

        /// <summary>
        /// Estimates a coverage radius based on current configuration.
        /// </summary>
        public float EstimateCoverageRadius()
        {
            var txPos = GetAntennaWorldPos();
            var baseContext = CreatePropagationContext(txPos + Vector3.forward, null);
            return pathLossCalculator.EstimateCoverageRadius(baseContext);
        }

        /// <summary>
        /// Samples ground height at a world position.
        /// </summary>
        public float SampleGroundHeight(Vector3 worldPos)
        {
            var origin = new Vector3(worldPos.x, worldPos.y + 2000f, worldPos.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 5000f, settings.terrainLayer, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return worldPos.y;
        }

        /// <summary>
        /// Returns receiver height above local ground (m).
        /// </summary>
        public float GetReceiverHeightMeters(Vector3 receiverWorldPos)
        {
            float groundY = SampleGroundHeight(receiverWorldPos);
            float rxHeight = Mathf.Max(0f, receiverWorldPos.y - groundY);
            return rxHeight;
        }
        #endregion

        #region Visualization (Model)
        private void CreateTransmitterModel()
        {
            CreateTowerBase();
            CreateMast();
            CreateSectors();
            SetAntennaOrigin(transmitterHeight);
            RebuildTransmitterCollider();
        }

        private float GetBaseTopLocalY()
        {
            var baseObj = transform.Find("TowerBase");
            return baseObj ? baseObj.localPosition.y + baseObj.localScale.y : 0f;
        }

        private void CreateTowerBase()
        {
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "TowerBase";
            baseObj.transform.SetParent(transform, false);
            baseObj.transform.localScale = new Vector3(1.2f, baseHalfHeight, 1.2f);
            baseObj.transform.localPosition = new Vector3(0f, baseHalfHeight, 0f);

            var baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            baseMat.SetColor("_BaseColor", new Color(0.75f, 0.75f, 0.75f));
            baseObj.GetComponent<Renderer>().material = baseMat;

            var col = baseObj.GetComponent<Collider>(); if (col) Destroy(col);
        }

        private void CreateMast()
        {
            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Mast";
            mast.transform.SetParent(transform, false);
            mast.transform.localScale = new Vector3(mastRadius * 2f, mastHalfHeight, mastRadius * 2f);
            mast.transform.localPosition = new Vector3(0f, GetBaseTopLocalY() + mastHalfHeight, 0f);

            var mastMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mastMat.SetColor("_BaseColor", new Color(0.75f, 0.78f, 0.8f));
            mast.GetComponent<Renderer>().material = mastMat;

            var col = mast.GetComponent<Collider>(); if (col) Destroy(col);
        }

        private void CreateSectors()
        {
            CreateSectorPanel(0f);
            CreateSectorPanel(120f);
            CreateSectorPanel(240f);
        }

        private void CreateSectorPanel(float azimuthDeg)
        {
            var bracket = new GameObject($"SectorBracket_{Mathf.RoundToInt(azimuthDeg)}");
            bracket.transform.SetParent(transform, false);

            float y = panelMountHeightFromBaseTop;
            bracket.transform.localPosition = new Vector3(0f, y, 0f);
            bracket.transform.localRotation = Quaternion.Euler(0f, azimuthDeg, 0f);

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            arm.transform.SetParent(bracket.transform, false);
            arm.transform.localScale = new Vector3(0.06f, 0.06f, panelForwardOffset);
            arm.transform.localPosition = new Vector3(0f, 0f, panelForwardOffset * 0.5f);

            var armMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            armMat.SetColor("_BaseColor", new Color(0.65f, 0.68f, 0.7f));
            arm.GetComponent<Renderer>().material = armMat;
            var armCol = arm.GetComponent<Collider>(); if (armCol) Destroy(armCol);

            var panelParent = new GameObject("PanelParent");
            panelParent.transform.SetParent(bracket.transform, false);
            panelParent.transform.localPosition = new Vector3(0f, 0f, panelForwardOffset);
            panelParent.transform.localRotation = Quaternion.Euler(panelElectricalDowntiltDeg * -1f, 0f, 0f);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "SectorPanel";
            panel.transform.SetParent(panelParent.transform, false);
            panel.transform.localScale = panelSize;
            panel.transform.localPosition = Vector3.zero;

            var panelMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            panelMat.SetColor("_BaseColor", new Color(0.95f, 0.96f, 0.97f));
            panel.GetComponent<Renderer>().material = panelMat;

            var panelCol = panel.GetComponent<Collider>(); if (panelCol) Destroy(panelCol);
        }

        /// <summary>
        /// Sets or creates the antenna origin at the given local Y height.
        /// </summary>
        public void SetAntennaOrigin(float? localY = null)
        {
            float y = localY ?? panelMountHeightFromBaseTop;
            Vector3 world = transform.TransformPoint(new Vector3(0f, y, 0f));
            if (antennaOrigin == null)
            {
                var go = new GameObject("AntennaOrigin");
                antennaOrigin = go.transform;
                antennaOrigin.SetParent(transform, false);
            }
            antennaOrigin.SetPositionAndRotation(world, transform.rotation);
        }

        private Vector3 GetAntennaOrigin()
        {
            return antennaOrigin != null ? antennaOrigin.position : transform.position;
        }

        /// <summary>
        /// Ensures a single CapsuleCollider on the root sized to child renderers.
        /// </summary>
        private void RebuildTransmitterCollider()
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;

            Bounds world = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) world.Encapsulate(rends[i].bounds);

            var cap = GetComponent<CapsuleCollider>();
            if (cap == null) cap = gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.center = transform.InverseTransformPoint(world.center);
            cap.radius = Mathf.Max(world.extents.x, world.extents.z);
            cap.height = world.size.y;

            var allCols = GetComponentsInChildren<Collider>(true);
            foreach (var c in allCols)
                if (c != cap) Destroy(c);
        }

        /// <summary>
        /// Sets line visibility for all connection renderers.
        /// </summary>
        public void ToggleConnectionLineVisualization(bool show)
        {
            showConnections = show;
            connectionRenderer?.SetLineVisibility(showConnections);
        }
        #endregion

        #region Ray Visualization
        /// <summary>
        /// Computes and displays transient rays to all receivers.
        /// </summary>
        public void VisualizeRaysToReceivers()
        {
            if (pathLossCalculator == null) return;
            var model = pathLossCalculator.GetRayTracingModel();
            if (model == null) return;

            model.enableRayVisualization = true;
            var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.None);
            foreach (var receiver in receivers)
                CalculateSignalStrength(receiver.transform.position);
        }

        /// <summary>
        /// Enables persistent ray visualization and recomputes paths.
        /// </summary>
        public void EnableRayVisualization()
        {
            showRayPaths = true;
            settings.enableRayTracing = true;
            propagationModel = PropagationModel.RayTracing;

            var model = pathLossCalculator?.GetRayTracingModel();
            if (model == null)
            {
                var anyRx = FindFirstObjectByType<Receiver>();
                if (anyRx != null) CalculateSignalStrength(anyRx.transform.position);
                model = pathLossCalculator?.GetRayTracingModel();
                model.enableRayVisualization = true;
                model.Visualizer = rayVisualization;
            }
            if (model == null)
            {
                Debug.LogWarning("[TX] Ray tracing model not available. Check propagationModel and settings.enableRayTracing.");
                return;
            }

            var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < receivers.Length; i++)
                CalculateSignalStrength(receivers[i].transform.position);
        }

        /// <summary>
        /// Disables ray visualization and clears rays.
        /// </summary>
        public void DisableRayVisualization()
        {
            showRayPaths = false;
            var model = pathLossCalculator?.GetRayTracingModel();
            if (model != null)
            {
                model.enableRayVisualization = false;
                rayVisualization.ClearAll();
            }
        }
        #endregion

        #region Initialization & Utilities
        private void InitializeTransmitter()
        {
            if (string.IsNullOrEmpty(uniqueID))
                uniqueID = "TX" + GetInstanceID();

            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 1f;
            }

            transmitterHeight = GeometryHelper.GetHeightAboveGround(GetAntennaWorldPos());
        }

        private void InitializeCalculators()
        {
            pathLossCalculator = new PathLossCalculator
            {
                mapboxBuildingLayer = settings.mapboxBuildingLayer,
                preferRayTracing = settings.enableRayTracing,
                maxDistance = settings.maxCalculationDistance
            };

            var rt = pathLossCalculator.GetRayTracingModel();
            if (rt != null)
            {
                rt.Visualizer = rayVisualization;
                rt.enableRayVisualization = showRayPaths;
                rt.showDirectRays = true;
                rt.showReflectionRays = true;
                rt.showDiffractionRays = true;
                rt.mapboxBuildingLayer = settings.mapboxBuildingLayer;
                rt.maxDistance = settings.maxCalculationDistance;
            }
        }

        private string ConnId(Receiver r) => $"{uniqueID}->{r.uniqueID}";

        private void MoveSectorBracketsTo(float localY)
        {
            var trs = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var t = trs[i];
                if (t != null && t.name.StartsWith("SectorBracket_"))
                {
                    var lp = t.localPosition;
                    lp.y = localY;
                    t.localPosition = lp;
                }
            }
        }

        /// <summary>
        /// Sets the full transmitter height, moves sector brackets, and antenna origin.
        /// </summary>
        public void SetTransmitterHeight(float newHeight)
        {
            newHeight = Mathf.Max(0f, newHeight);
            transmitterHeight = newHeight;
            panelMountHeightFromBaseTop = newHeight;
            MoveSectorBracketsTo(newHeight);
            SetAntennaOrigin(newHeight);
        }
        #endregion
    }
}
