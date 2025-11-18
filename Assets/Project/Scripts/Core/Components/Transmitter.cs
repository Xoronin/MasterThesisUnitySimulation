using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.Models;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Components;
using RFSimulation.Utils;
using RFSimulation.Visualization;

namespace RFSimulation.Core.Components
{

    [System.Serializable]
    public class TransmitterSettings
    {
        [Header("Propagation Settings")]
        public float transmitterPower = 20.0f;
        public float antennaGain = 0f;
        public float frequency = 3500f;
        public float transmitterHeight = 10f;
        public PropagationModel propagationModel;
        public TechnologyType technology;

        [Header("Visualization")]
        public bool showConnections = true;
        public bool showRayPaths = false;

        [Header("Performance")]
        public int maxReflections = 2;
        public int maxDiffractions = 2;
        public int maxScattering = 2;
        public float maxCalculationDistance = 5000f;

        [Header("Mapbox Integration")]
        public LayerMask buildingLayer;
        public LayerMask terrainLayer;
    }


    public class Transmitter : MonoBehaviour
    {
        #region Data Contracts

        public class TransmitterInfo
        {
            public string UniqueID { get; set; }
            public TechnologyType TechnologyType { get; set; }

            public float TransmitterPowerDbm { get; set; }
            public float AntennaGainDbi { get; set; }
            public float FrequencyMHz { get; set; }
            public float TransmitterHeightM { get; set; }

            public PropagationModel PropagationModel { get; set; }
            public float MaxCalculationDistanceM { get; set; }
            public int MaxReflections { get; set; }
            public int MaxDiffractions { get; set; }
            public int MaxScattering { get; set; }

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


        [Header("Settings")]
        public TransmitterSettings settings = new TransmitterSettings();

        [Header("Visualization")]
        private ConnectionLineVisualization lineVisualization;
        private HeatmapVisualization heatmapVisualization;
        private RayVisualization rayVisualization;

        [Header("Defaults")]
        public float defaultReceiverSensitivityDbm;


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
            panelMountHeightFromBaseTop = settings.transmitterHeight;
            if (lineVisualization == null) lineVisualization = FindAnyObjectByType<ConnectionLineVisualization>();
            if (rayVisualization == null) rayVisualization = FindAnyObjectByType<RayVisualization>();
            SimulationManager.Instance?.RegisterTransmitter(this);
            _lastAppliedModel = settings.propagationModel;
        }

        void Start()
        {
            if (pathLossCalculator == null) InitializeCalculators();
            if (heatmapVisualization == null) heatmapVisualization = FindFirstObjectByType<HeatmapVisualization>();
            panelMountHeightFromBaseTop = settings.transmitterHeight;
            CreateTransmitterModel();
        }

        void Update()
        {
            if (_lastAppliedModel != settings.propagationModel) UpdatePropagationModel();

            if (!settings.showConnections || lineVisualization == null) return;

            panelMountHeightFromBaseTop = settings.transmitterHeight;
            UpdateMastHeight(settings.transmitterHeight);
            SetAntennaOrigin(settings.transmitterHeight);

            foreach (var kv in connectionLines)
            {
                var rx = kv.Key;
                if (rx == null) continue;
                float rssi = CalculateSignalStrength(rx);
                lineVisualization.UpdateConnection(
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
                lineVisualization?.RemoveConnection(ConnId(rx));
            connectionLines.Clear();

            rayVisualization.ClearAll();

            SimulationManager.Instance?.RemoveTransmitter(this);
        }
        #endregion

        #region Public API

        public TransmitterInfo GetInfo()
        {
            return new TransmitterInfo
            {
                UniqueID = uniqueID,
                TechnologyType = settings.technology,

                TransmitterPowerDbm = settings.transmitterPower,
                AntennaGainDbi = settings.antennaGain,
                FrequencyMHz = settings.frequency,
                TransmitterHeightM = settings.transmitterHeight,

                PropagationModel = settings.propagationModel,
                MaxCalculationDistanceM = settings.maxCalculationDistance,
                MaxReflections = settings.maxReflections,
                MaxDiffractions = settings.maxDiffractions,
                MaxScattering = settings.maxScattering,

                ShowConnections = settings.showConnections,
                ShowRayPaths = settings.showRayPaths,

                ConnectedReceiverCount = connectedReceivers.Count,
                WorldPosition = transform.position,
                AntennaWorldPosition = GetAntennaWorldPos(),
            };
        }


        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            var context = CreatePropagationContext(receiverPosition, null);
            try { return pathLossCalculator.CalculateReceivedPower(context); }
            catch { return float.NegativeInfinity; }
        }

        public float CalculateSignalStrength(Receiver receiver)
        {
            if (receiver == null) return float.NegativeInfinity;
            var context = CreatePropagationContext(receiver.transform.position, receiver.sensitivity);
            try { return pathLossCalculator.CalculateReceivedPower(context); }
            catch { return float.NegativeInfinity; }
        }

        public Vector3 GetAntennaWorldPos()
        {
            return antennaOrigin != null ? antennaOrigin.position : transform.position;
        }

        public List<Receiver> GetConnectedReceivers() => new List<Receiver>(connectedReceivers);


        public int GetConnectionCount() => connectedReceivers.Count;

        public bool IsConnectedTo(Receiver receiver) => connectedReceivers.Contains(receiver);

        public void SetPropagationModel(PropagationModel model)
        {
            settings.propagationModel = model;
            UpdatePropagationModel();
        }

        public void SetMaxReflections(int maxReflections)
        {
            settings.maxReflections = maxReflections;
        }

        public void SetMaxDiffractions(int maxDiffractions)
        {
            settings.maxDiffractions = maxDiffractions;
        }

        public void SetMaxScattering(int maxScattering)
        {
            settings.maxScattering = maxScattering;
        }

        public void ClearPathLossCache()
        {
            pathLossCalculator?.ClearCache();
        }
        #endregion

        #region Core Signal Calculation
        private PropagationContext CreatePropagationContext(Vector3 receiverPosition, float? receiverSensitivityDbm)
        {
            var txPos = GetAntennaWorldPos();
            var receiverHeight = RaycastHelper.GetHeightAboveGround(receiverPosition);
            var isLOS = RaycastHelper.IsLineOfSight(
                txPos,
                receiverPosition,
                settings.buildingLayer,
                out var hit
            );

            var context = PropagationContext.Create(
                txPos,
                receiverPosition,
                settings.transmitterPower,
                settings.frequency,
                settings.transmitterHeight,
                receiverHeight
            );

            context.IsLOS = isLOS;
            context.Model = settings.propagationModel;
            context.AntennaGainDbi = settings.antennaGain;
            context.ReceiverGainDbi = 0f;
            context.ReceiverSensitivityDbm = receiverSensitivityDbm ?? defaultReceiverSensitivityDbm;
            context.BuildingLayer = settings.buildingLayer;
            context.MaxReflections = settings.maxReflections;
            context.MaxDiffractions = settings.maxDiffractions;
            context.MaxScattering = settings.maxScattering;
            context.MaxDistanceMeters = settings.maxCalculationDistance;
            context.Technology = settings.technology;

            return context;
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
            if (receiver == null) return;

            var previousTx = receiver.GetConnectedTransmitter();
            if (previousTx != null && previousTx != this)
            {
                previousTx.DisconnectFromReceiver(receiver);
            }

            if (connectedReceivers.Contains(receiver)) return;

            if (!CanConnectTo(receiver)) return;

            connectedReceivers.Add(receiver);
            receiver.SetConnectedTransmitter(this);

            if (settings.showConnections && lineVisualization != null && !connectionLines.ContainsKey(receiver))
            {
                float rssi = CalculateSignalStrength(receiver);
                var lr = lineVisualization.CreateConnection(
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

            if (connectionLines.ContainsKey(receiver))
            {
                lineVisualization?.RemoveConnection(ConnId(receiver));
                connectionLines.Remove(receiver);
            }

            if (connectedReceivers.Remove(receiver))
                receiver.ClearConnection();
        }

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

        public bool UpdateTransmitterPower(float newPowerDbm)
        {
            if (newPowerDbm >= 0f && newPowerDbm <= 80f)
            {
                settings.transmitterPower = newPowerDbm;
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
                settings.frequency = newFrequencyMHz;
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
                settings.antennaGain = newGainDbi;
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
            settings.transmitterHeight = newHeightM;
            SetAntennaOrigin(settings.transmitterHeight);
            RefreshConnections();
            TryConnectEligibleReceivers();
            SimulationManager.Instance?.RecomputeForTransmitter(this);
        }

        private void UpdatePropagationModel()
        {
            _lastAppliedModel = settings.propagationModel;
            if (pathLossCalculator == null) InitializeCalculators();

            RefreshConnections();
            TryConnectEligibleReceivers();

            pathLossCalculator.ClearCache();
            SimulationManager.Instance?.RecomputeForTransmitter(this);
        }
        #endregion

        #region Configuration

        public void SetMaxDistance(float maxDistance)
        {
            settings.maxCalculationDistance = maxDistance;
        }


        public void UpdateSettings(TransmitterSettings newSettings)
        {
            settings = newSettings;
            InitializeCalculators();
        }

        public float SampleGroundHeight(Vector3 worldPos)
        {
            var origin = new Vector3(worldPos.x, worldPos.y + 2000f, worldPos.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 5000f, settings.terrainLayer, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return worldPos.y;
        }

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
            SetAntennaOrigin(settings.transmitterHeight);
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
            mast.transform.localScale = new Vector3(mastRadius * 2f, mastHalfHeight * 2f, mastRadius * 2f);
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

        public void UpdateMastHeight(float additionalHeight)
        {
            additionalHeight = Mathf.Max(0f, additionalHeight + 2f);
            float half = additionalHeight * 0.5f;
            var mast = transform.Find("Mast");
            if (mast != null)
            {
                mast.transform.localScale = new Vector3(mastRadius * 2f, half, mastRadius * 2f);
                mast.transform.localPosition = new Vector3(0f, GetBaseTopLocalY() + half, 0f);
            }
        }

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


        public void ToggleConnectionLineVisualization(bool show)
        {
            settings.showConnections = show;
            lineVisualization?.SetLineVisibility(settings.showConnections);
        }
        #endregion

        #region Ray Visualization

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


        public void EnableRayVisualization()
        {
            settings.showRayPaths = true;

            if (settings.propagationModel != PropagationModel.RayTracing)
            {
                settings.propagationModel = PropagationModel.RayTracing;
                if (pathLossCalculator != null)
                {
                    pathLossCalculator = null;
                    InitializeCalculators();
                }
            }

            if (pathLossCalculator == null)
            {
                InitializeCalculators();
            }

            if (rayVisualization == null)
            {
                rayVisualization = FindAnyObjectByType<RayVisualization>();
            }

            var model = pathLossCalculator?.GetRayTracingModel();
            if (model == null)
            {
                Debug.LogWarning("[TX] Ray tracing model not available after initialization. Check propagation model.");
                return;
            }

            model.enableRayVisualization = true;
            model.Visualizer = rayVisualization;

            var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);
            if (receivers.Length == 0)
            {
                Debug.Log("[TX] No receivers found - rays will appear when receivers are added.");
                return;
            }

            foreach (var receiver in receivers)
            {
                if (receiver != null)
                {
                    CalculateSignalStrength(receiver.transform.position);
                }
            }
        }

        public void DisableRayVisualization()
        {
            settings.showRayPaths = false;

            var model = pathLossCalculator?.GetRayTracingModel();
            if (model != null)
            {
                model.enableRayVisualization = false;
            }

            if (rayVisualization != null)
            {
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

            settings.transmitterHeight = RaycastHelper.GetHeightAboveGround(GetAntennaWorldPos());
        }

        private void InitializeCalculators()
        {
            pathLossCalculator = new PathLossCalculator();

            var rt = pathLossCalculator.GetRayTracingModel();
            if (rt != null)
            {
                rt.Visualizer = rayVisualization;
                rt.enableRayVisualization = settings.showRayPaths;
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


        public void SetTransmitterHeight(float newHeight)
        {
            newHeight = Mathf.Max(0f, newHeight);
            settings.transmitterHeight = newHeight;
            panelMountHeightFromBaseTop = newHeight;
            MoveSectorBracketsTo(newHeight);
            SetAntennaOrigin(newHeight);
        }

        public void SetTechnology(TechnologyType tech)
        {
            settings.technology = tech;

            var spec = TechnologySpecifications.GetSpec(tech);
            settings.transmitterPower = spec.TypicalTxPowerDbm;
            settings.frequency = spec.TypicalFrequencyMHz;
            SetTransmitterHeight(spec.TypicalTxHeight);

            ClearPathLossCache();
            SimulationManager.Instance?.RecomputeForTransmitter(this);
        }

        private void RefreshHeatmap()
        {
            var heatmap = FindFirstObjectByType<HeatmapVisualization>();
            if (heatmap != null && heatmap.enabled)
                heatmap.UpdateHeatmap();
        }
        #endregion
    }
}
