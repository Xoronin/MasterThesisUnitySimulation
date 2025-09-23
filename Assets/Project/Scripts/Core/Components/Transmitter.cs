using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;
using RFSimulation.Core;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Components;

namespace RFSimulation.Core.Components
{
    /// <summary>
    /// Settings for  ray tracing functionality
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
    /// Unified Transmitter combining basic RF functionality with advanced  ray tracing
    /// </summary>
    public class Transmitter : MonoBehaviour
    {
        #region Core Properties
        [Header("Transmitter Properties")]
        public string uniqueID;
        public float transmitterPower = 40f; // dBm
        public float antennaGain = 12f; // dBi
        public float frequency = 2400f; // MHz

        [Header("Propagation Settings")]
        public PropagationModel propagationModel = PropagationModel.LogDistance;

        [Header("Settings")]
        public TransmitterSettings settings = new TransmitterSettings();

        [Header("Visualization")]
        public bool showConnections = true;
        public bool showRayPaths = false;
        public Material connectionLineMaterial;
        #endregion

        #region Private Fields
        private List<Receiver> connectedReceivers = new List<Receiver>();
        private List<LineRenderer> connectionLines = new List<LineRenderer>();

        // Path loss calculators
        private PathLossCalculator pathLossCalculator;

        // Model management
        private PropagationModel _lastAppliedModel;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            InitializeTransmitter();
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
            {
                UpdatePropagationModel();
            }
        }

        void OnDestroy()
        {
            ClearAllConnections();
            CleanupVisualization();
            SimulationManager.Instance?.RemoveTransmitter(this);
        }
        #endregion

        #region Initialization
        private void InitializeTransmitter()
        {
            if (string.IsNullOrEmpty(uniqueID))
                uniqueID = "UTX_" + GetInstanceID();

            // Ensure collider for interaction
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 1f;
            }
        }

        private void InitializeCalculators()
        {
            // Initialize basic path loss calculator
            pathLossCalculator = new PathLossCalculator();

            // Initialize -enhanced calculator
            PathLossCalculator = new PathLossCalculator();

            // Configure  settings
            PathLossCalculator.mapboxBuildingLayer = settings.mapboxBuildingLayer;
            PathLossCalculator.preferRayTracing = settings.enableRayTracing;
            PathLossCalculator.maxDistance = settings.maxCalculationDistance;

            Debug.Log($"[UnifiedTransmitter] {uniqueID} initialized with  ray tracing support");
        }
        #endregion

        #region Core Signal Calculation
        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            // Create propagation context
            var context = CreatePropagationContext(receiverPosition);

            // Select optimal model based on environment and settings
            context.Model = SelectOptimalModel(context);

            try
            {
                // Use  calculator if  features are enabled, otherwise basic calculator
                float receivedPower;
                if (settings.enableRayTracing && ShouldUseCalculator(context))
                {
                    receivedPower = PathLossCalculator.CalculateReceivedPower(context);

                    if (showRayPaths)
                    {
                        VisualizeRayPath(transform.position, receiverPosition, receivedPower);
                    }
                }
                else
                {
                    receivedPower = pathLossCalculator.CalculateReceivedPower(context);
                }

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

        private bool ShouldUseCalculator(PropagationContext context)
        {
            // Check if distance is within  calculation limits
            if (context.Distance > settings.maxCalculationDistance)
                return false;

            // Use  calculator if in  environment or  ray tracing is forced
            return settings.enableRayTracing;
        }

        private PropagationModel SelectOptimalModel(PropagationContext context)
        {
            float distance = context.Distance;

            // If  ray tracing is disabled, use the selected propagation model
            if (!settings.enableRayTracing)
            {
                return propagationModel;
            }

            // Performance optimization: use simpler models for very long distances
            if (distance > settings.maxCalculationDistance)
            {
                return settings.fallbackModel;
            }

            // Default to selected propagation model
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
            if (!connectedReceivers.Contains(receiver) && CanConnectTo(receiver))
            {
                connectedReceivers.Add(receiver);
                CreateConnectionLine(receiver);
                receiver.SetConnectedTransmitter(this);
            }
        }

        public void DisconnectFromReceiver(Receiver receiver)
        {
            if (connectedReceivers.Remove(receiver))
            {
                RemoveConnectionLine(receiver);
                receiver.ClearConnection();
            }
        }

        public void ClearAllConnections()
        {
            foreach (var receiver in connectedReceivers.ToArray())
            {
                DisconnectFromReceiver(receiver);
            }
        }

        private void RefreshConnections()
        {
            // Re-evaluate all current connections
            foreach (var receiver in connectedReceivers.ToArray())
            {
                if (!CanConnectTo(receiver))
                {
                    DisconnectFromReceiver(receiver);
                }
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
            Debug.LogWarning($"Invalid power value: {newPowerDbm}dBm. Must be 0-80 dBm.");
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
            Debug.LogWarning($"Invalid frequency: {newFrequencyMHz}MHz. Must be > 0 MHz.");
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
            Debug.LogWarning($"Invalid gain: {newGainDbi}dBi. Must be 0-50 dBi.");
            return false;
        }

        private void UpdatePropagationModel()
        {
            _lastAppliedModel = propagationModel;
            RefreshConnections();
            TryConnectEligibleReceivers();
        }
        #endregion

        #region  Configuration Methods
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

            if (settings.enableRayTracing && ShouldUseCalculator(baseContext))
            {
                return PathLossCalculator.EstimateCoverageRadius(baseContext);
            }
            else
            {
                return pathLossCalculator.EstimateCoverageRadius(baseContext);
            }
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
            // Create main antenna pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "AntennaPole";
            pole.transform.SetParent(transform);
            pole.transform.localPosition = Vector3.up * 5f;
            pole.transform.localScale = new Vector3(0.2f, 5f, 0.2f);

            // Create antenna elements (cross-arms)
            for (int i = 0; i < 3; i++)
            {
                GameObject element = GameObject.CreatePrimitive(PrimitiveType.Cube);
                element.name = $"AntennaElement_{i}";
                element.transform.SetParent(pole.transform);
                element.transform.localPosition = Vector3.up * (0.6f + i * 0.3f);
                element.transform.localScale = new Vector3(2f, 0.1f, 0.1f);
            }

            // Apply materials
            var renderer = pole.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", Color.blue);
                renderer.material = material;
            }

            foreach (Transform child in pole.transform)
            {
                var childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    var elementMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    elementMaterial.SetColor("_BaseColor", Color.cyan);
                    childRenderer.material = elementMaterial;
                }
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
        }

        private void CreateConnectionLine(Receiver receiver)
        {
            if (!showConnections) return;

            GameObject lineObj = new GameObject($"ConnectionLine_{uniqueID}_to_{receiver.uniqueID}");
            lineObj.transform.SetParent(transform);

            LineRenderer line = lineObj.AddComponent<LineRenderer>();

            // Create material with dynamic color based on signal strength
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            float signalStrength = CalculateSignalStrength(receiver.transform.position);
            Color lineColor = GetSignalQualityColor(signalStrength, receiver.sensitivity);
            material.SetColor("_BaseColor", lineColor);

            line.material = material;
            line.startWidth = 0.3f;
            line.endWidth = 0.3f;
            line.positionCount = 2;
            line.useWorldSpace = true;

            line.SetPosition(0, transform.position);
            line.SetPosition(1, receiver.transform.position);

            connectionLines.Add(line);
        }

        private Color GetSignalQualityColor(float signalStrength, float sensitivity)
        {
            float margin = signalStrength - sensitivity;

            if (margin < 5f) return Color.red;      // Poor signal
            if (margin < 10f) return Color.yellow;  // Fair signal  
            if (margin < 15f) return Color.green;   // Good signal
            return Color.cyan;                      // Excellent signal
        }

        private void RemoveConnectionLine(Receiver receiver)
        {
            var lineToRemove = connectionLines.Find(line =>
                line != null && line.GetPosition(1) == receiver.transform.position);

            if (lineToRemove != null)
            {
                connectionLines.Remove(lineToRemove);
                if (lineToRemove.gameObject != null)
                    DestroyImmediate(lineToRemove.gameObject);
            }
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

        public void ToggleVisualization(bool showConnections)
        {
            this.showConnections = showConnections;

            foreach (var line in connectionLines)
            {
                if (line != null) line.gameObject.SetActive(showConnections);
            }
        }

        private void CleanupVisualization()
        {
            foreach (var line in connectionLines)
            {
                if (line != null && line.gameObject != null)
                    DestroyImmediate(line.gameObject);
            }
            connectionLines.Clear();
        }
        #endregion

        #region Ray Visualization
        public void VisualizeRaysToReceivers()
        {
            if (PathLossCalculator != null)
            {
                var model = PathLossCalculator.GetRayTracingModel();
                if (model != null)
                {
                    model.enableRayVisualization = true;

                    var receivers = FindObjectsByType<RFSimulation.Core.Components.Receiver>(FindObjectsSortMode.None);
                    foreach (var receiver in receivers)
                    {
                        CalculateSignalStrength(receiver.transform.position);
                    }
                }
            }
        }

        public void EnableRayVisualization()
        {
            showRayPaths = true;

            var model = PathLossCalculator?.GetRayTracingModel();
            if (model != null)
            {
                model.enableRayVisualization = true;
                model.showDirectRays = true;
                model.showReflectionRays = true;
                model.showDiffractionRays = true;
                model.persistentRays = true;
                model.rayDisplayDuration = 10f;

                var receivers = FindObjectsByType<RFSimulation.Core.Components.Receiver>(FindObjectsSortMode.None);
                foreach (var receiver in receivers)
                {
                    CalculateSignalStrength(receiver.transform.position);
                }
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