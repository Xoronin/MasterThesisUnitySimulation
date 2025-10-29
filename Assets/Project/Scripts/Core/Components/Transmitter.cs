using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;
using RFSimulation.Core;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Components;
using RFSimulation.Visualization;
using RFSimulation.Utils;

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
        public PropagationModel propagationModel = PropagationModel.RayTracing;
        public PropagationModel fallbackModel = PropagationModel.LogDistance;

        [Header("Performance")]
        public bool usePerformanceOptimizations = true;
        public int maxReflections = 2;
        public int maxDiffractions = 2;
        public float maxCalculationDistance = 1500f;

        [Header("Mapbox Integration")]
        public LayerMask mapboxBuildingLayer = 1 << 8;
        public LayerMask terrainLayer = 6;
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
        public float transmitterHeight;

        [Header("Propagation Settings")]
        public PropagationModel propagationModel = PropagationModel.LogDistance;

        [Header("Settings")]
        public TransmitterSettings settings = new TransmitterSettings();

        [Header("Visualization")]
        public bool showConnections = true;
        public bool showRayPaths = false;

        [Header("Defaults")]
        public float defaultReceiverSensitivityDbm = -95f;


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

        public Transform antennaOrigin;

        // Quick knobs for proportions (meters)
        [SerializeField] private float baseHalfHeight = 0.6f;   // base cylinder half-height (total 1.2m)
        [SerializeField] private float mastHalfHeight = 8.0f;   // mast half-height (total 16m)
        [SerializeField] private float mastRadius = 0.25f;

        // Panel dimensions (approx. 1.6m x 0.35m x 0.12m)
        [SerializeField] private Vector3 panelSize = new Vector3(0.35f, 1.6f, 0.12f);
        [SerializeField] private float panelForwardOffset = 0.6f; // distance from mast center to panel center
        [SerializeField] private float panelMountHeightFromBaseTop = 12.0f; // where panels sit on the mast
        [SerializeField] private float panelElectricalDowntiltDeg = 3.0f;   // slight downward tilt

        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            InitializeTransmitter();

            panelMountHeightFromBaseTop = transmitterHeight;

            if (connectionRenderer == null)
                connectionRenderer = FindAnyObjectByType<ConnectionLineRenderer>();

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
            if (_lastAppliedModel != propagationModel)
                UpdatePropagationModel();

            if (!showConnections || connectionRenderer == null) return;

            var rt = pathLossCalculator?.GetRayTracingModel();
            if (rt != null && rt.enableRayVisualization && !rt.persistentRays)
                rt.ClearAllRays();

            panelMountHeightFromBaseTop = transmitterHeight;
            SetAntennaOrigin(transmitterHeight);

            // Keep visual lines in sync (positions/color) via the centralized renderer
            foreach (var kv in connectionLines)
            {
                var rx = kv.Key; if (rx == null) continue;
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
                uniqueID = "TX" + GetInstanceID();

            // Ensure collider for interaction
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 1f;
            }

            transmitterHeight = GeometryHelper.GetHeightAboveGround(GetAntennaWorldPos());
        }

        private void InitializeCalculators()
        {
            pathLossCalculator = new PathLossCalculator();

            // Configure settings
            pathLossCalculator.mapboxBuildingLayer = settings.mapboxBuildingLayer;
            pathLossCalculator.preferRayTracing = settings.enableRayTracing;
            pathLossCalculator.maxDistance = settings.maxCalculationDistance;
        }
        #endregion

        #region Core Signal Calculation
        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            var context = CreatePropagationContext(receiverPosition, null); // uses default
            context.Model = SelectOptimalModel(context);
            try { return pathLossCalculator.CalculateReceivedPower(context); }
            catch { return float.NegativeInfinity; }
        }

        public float CalculateSignalStrength(Receiver receiver)
        {
            if (receiver == null) return float.NegativeInfinity;
            var context = CreatePropagationContext(receiver.transform.position, receiver.sensitivity); // <<<
            context.Model = SelectOptimalModel(context);
            try { return pathLossCalculator.CalculateReceivedPower(context); }
            catch { return float.NegativeInfinity; }
        }


        public Vector3 GetAntennaWorldPos()
        {
            return antennaOrigin != null ? antennaOrigin.position : transform.position;
        }

        private PropagationContext CreatePropagationContext(
            Vector3 receiverPosition,
            float? receiverSensitivityDbm = null)
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

            context.AntennaGainDbi = antennaGain;
            context.ReceiverSensitivityDbm = receiverSensitivityDbm ?? defaultReceiverSensitivityDbm; // <<<
            context.BuildingLayers = settings.mapboxBuildingLayer;
            return context;
        }


        public void SetTransmitterHeight(float newHeight)
        {
            newHeight = Mathf.Max(0f, newHeight);
            transmitterHeight = newHeight;

            // 1) remember the mount height used by sector creation
            panelMountHeightFromBaseTop = newHeight;

            // 2) slide the three brackets to this Y so panels climb the mast
            MoveSectorBracketsTo(newHeight);

            // 3) move the antenna origin to the mast center at this Y
            SetAntennaOrigin(newHeight);
        }

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
            float rssi = CalculateSignalStrength(receiver);                
            return rssi >= (receiver.sensitivity + receiver.connectionMargin);
        }

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
                TryConnectEligibleReceivers();
                SimulationManager.Instance?.RecomputeForTransmitter(this);
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
                TryConnectEligibleReceivers();
                SimulationManager.Instance?.RecomputeForTransmitter(this);
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
                TryConnectEligibleReceivers();
                SimulationManager.Instance?.RecomputeForTransmitter(this);
                return true;
            }
            Debug.LogWarning($"Invalid gain: {newGainDbi} dBi. Must be 0–50 dBi.");
            return false;
        }

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
            RefreshConnections();
            TryConnectEligibleReceivers();
            pathLossCalculator.ClearCache(); 
            SimulationManager.Instance?.RecomputeForTransmitter(this);
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
            var txPos = GetAntennaWorldPos();
            var baseContext = CreatePropagationContext(txPos + Vector3.forward);
            return pathLossCalculator.EstimateCoverageRadius(baseContext);
        }

        public float SampleGroundHeight(Vector3 worldPos)
        {
            // cast from high above to be robust over buildings
            var origin = new Vector3(worldPos.x, worldPos.y + 2000f, worldPos.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 5000f, settings.terrainLayer, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            // Fallback: no terrain hit -> treat current Y as ground (height = 0)
            return worldPos.y;
        }

        public float GetReceiverHeightMeters(Vector3 receiverWorldPos)
        {
            float groundY = SampleGroundHeight(receiverWorldPos);
            float rxHeight = Mathf.Max(0f, receiverWorldPos.y - groundY);
            return rxHeight;
        }
        #endregion

        #region Visualization
        private void CreateTransmitterModel()
        {
            // Build order matters so heights are correct
            CreateTowerBase();
            CreateMast();
            CreateSectors();
            SetAntennaOrigin(transmitterHeight);
            RebuildTransmitterCollider(); // fit single root collider to all visuals
        }

        private float GetBaseTopLocalY()
        {
            var baseObj = transform.Find("TowerBase");
            return baseObj ? baseObj.localPosition.y + baseObj.localScale.y : 0f;
        }

        private void CreateTowerBase()
        {
            // Concrete pedestal
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "TowerBase";
            baseObj.transform.SetParent(transform, false);
            baseObj.transform.localScale = new Vector3(1.2f, baseHalfHeight, 1.2f); // ≈ 2.4m diameter, 1.2m tall
            baseObj.transform.localPosition = new Vector3(0f, baseHalfHeight, 0f);

            var baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            baseMat.SetColor("_BaseColor", new Color(0.75f, 0.75f, 0.75f)); // concrete gray
            baseObj.GetComponent<Renderer>().material = baseMat;

            // Remove child collider — we'll keep only the root collider
            var col = baseObj.GetComponent<Collider>(); if (col) Destroy(col);
        }

        private void CreateMast()
        {
            // Cylindrical mast centered on base
            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Mast";
            mast.transform.SetParent(transform, false);
            mast.transform.localScale = new Vector3(mastRadius * 2f, mastHalfHeight, mastRadius * 2f);
            mast.transform.localPosition = new Vector3(0f, GetBaseTopLocalY() + mastHalfHeight, 0f);

            var mastMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mastMat.SetColor("_BaseColor", new Color(0.75f, 0.78f, 0.8f)); // galvanized steel
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
            // Bracket anchored to the mast, rotated to azimuth
            var bracket = new GameObject($"SectorBracket_{Mathf.RoundToInt(azimuthDeg)}");
            bracket.transform.SetParent(transform, false);

            float y = panelMountHeightFromBaseTop;
            bracket.transform.localPosition = new Vector3(0f, y, 0f);
            bracket.transform.localRotation = Quaternion.Euler(0f, azimuthDeg, 0f);

            // Visual bracket arm (optional)
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            arm.transform.SetParent(bracket.transform, false);
            arm.transform.localScale = new Vector3(0.06f, 0.06f, panelForwardOffset); // a slim bar
            arm.transform.localPosition = new Vector3(0f, 0f, panelForwardOffset * 0.5f);

            var armMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            armMat.SetColor("_BaseColor", new Color(0.65f, 0.68f, 0.7f));
            arm.GetComponent<Renderer>().material = armMat;
            var armCol = arm.GetComponent<Collider>(); if (armCol) Destroy(armCol);

            // Panel parent (to apply tilt cleanly)
            var panelParent = new GameObject("PanelParent");
            panelParent.transform.SetParent(bracket.transform, false);
            panelParent.transform.localPosition = new Vector3(0f, 0f, panelForwardOffset);
            panelParent.transform.localRotation = Quaternion.Euler(panelElectricalDowntiltDeg * -1f, 0f, 0f); // slight downward tilt

            // Sector panel (cube)
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "SectorPanel";
            panel.transform.SetParent(panelParent.transform, false);
            panel.transform.localScale = panelSize;
            panel.transform.localPosition = Vector3.zero;

            var panelMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            panelMat.SetColor("_BaseColor", new Color(0.95f, 0.96f, 0.97f)); // off-white radome
            panel.GetComponent<Renderer>().material = panelMat;

            var panelCol = panel.GetComponent<Collider>(); if (panelCol) Destroy(panelCol);

        }

        public void SetAntennaOrigin(float? localY = null)
        {
            // Default height: base top + panel mount height
            float y = localY ?? (panelMountHeightFromBaseTop);

            Vector3 world = transform.TransformPoint(new Vector3(0f, y, 0f));
            if (antennaOrigin == null)
            {
                var go = new GameObject("AntennaOrigin");
                antennaOrigin = go.transform;
                antennaOrigin.SetParent(transform, false);
            }
            // Align with mast / root
            antennaOrigin.SetPositionAndRotation(world, transform.rotation);
        }

        private Vector3 GetAntennaOrigin()
        {
            return antennaOrigin != null ? antennaOrigin.position : transform.position;
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

            // 1) Must be on a ray-tracing model
            settings.enableRayTracing = true;
            propagationModel = PropagationModel.RayTracing; // or your RT enum

            // 2) Ensure calculator/model exist
            var model = pathLossCalculator?.GetRayTracingModel();
            if (model == null)
            {
                // Warm-up: run one solve to force model creation
                var anyRx = FindFirstObjectByType<Receiver>();
                if (anyRx != null) CalculateSignalStrength(anyRx.transform.position);
                model = pathLossCalculator?.GetRayTracingModel();
            }

            if (model == null)
            {
                Debug.LogWarning("[TX] Ray tracing model not available. Check propagationModel and settings.enableRayTracing.");
                return;
            }

            // 3) Turn on viz flags
            model.enableRayVisualization = true;
            model.persistentRays = true;       // keep on screen
            model.rayDisplayDuration = 10f;    // seconds per batch

            // 4) Force a recompute for all receivers so rays appear now
            var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < receivers.Length; i++)
                CalculateSignalStrength(receivers[i].transform.position);
        }

        public void DisableRayVisualization()
        {
            showRayPaths = false;

            var model = pathLossCalculator?.GetRayTracingModel();
            if (model != null)
            {
                model.enableRayVisualization = false;
                model.ClearAllRays();
            }
        }

        #endregion

        #region Public Properties and Utilities
        public List<Receiver> GetConnectedReceivers() => new List<Receiver>(connectedReceivers);
        public int GetConnectionCount() => connectedReceivers.Count;
        public bool IsConnectedTo(Receiver receiver) => connectedReceivers.Contains(receiver);
        public void SetPropagationModel(PropagationModel model)
        {
            propagationModel = model;
            UpdatePropagationModel();
            Debug.Log($"[TX] {uniqueID} propagation model set to {propagationModel}");
        }
        public void ClearPathLossCache()
        {
            pathLossCalculator?.ClearCache();  
        }
        #endregion
    }
}
