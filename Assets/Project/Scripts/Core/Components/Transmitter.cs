using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;
using RFSimulation.Core;
using RFSimulation.Core.Managers;

namespace RFSimulation.Core.Components
{
    /// <summary>
    /// Simplified Transmitter - combines connection, visualization, and validation logic
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

        [Header("Visualization")]
        public bool showConnections = true;
        public Material connectionLineMaterial;
        #endregion

        #region Private Fields
        private List<Receiver> connectedReceivers = new List<Receiver>();
        private List<LineRenderer> connectionLines = new List<LineRenderer>();
        private PathLossCalculator pathLossCalculator;
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
            pathLossCalculator = new PathLossCalculator();
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

        #region Core Functionality
        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            var context = PropagationContext.Create(transform.position, receiverPosition, transmitterPower, frequency);
            context.AntennaGainDbi = antennaGain;
            context.Model = propagationModel;

            return pathLossCalculator.CalculateReceivedPower(context);
        }

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

            // 1) Re-evaluate existing links (drop weak ones)
            RefreshConnections();

            // 2) Optionally try to connect to *newly* eligible receivers
            TryConnectEligibleReceivers();

            // 3) Recolor lines to reflect new RSSI under the new model
            RefreshConnections();
        }

        private void TryConnectEligibleReceivers()
        {
            // Prefer SimulationManager if vorhanden, sonst Fallback:
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

        #region Visualization - Integrated

        private void CreateTransmitterModel()
        {
            // Create antenna tower
            CreateAntennaTower();
            CreateTowerBase();
        }

        private void CreateAntennaTower()
        {
            // Create main antenna pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "AntennaPole";
            pole.transform.SetParent(transform);
            pole.transform.localPosition = Vector3.up * 5f; // 10m high tower
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

            // Apply material to main pole
            var renderer = pole.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", Color.blue);
                renderer.material = material;
            }

            // Apply material to antenna elements
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

            // Apply material
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

            // Get signal strength and determine color
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

        // Helper method to determine line color based on signal quality
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

        public void ToggleVisualization(bool showConnections)
        {
            this.showConnections = showConnections;

            // Update connection lines visibility
            foreach (var line in connectionLines)
            {
                if (line != null) line.gameObject.SetActive(showConnections);
            }
        }

        private void CleanupVisualization()
        {
            // Cleanup connection lines
            foreach (var line in connectionLines)
            {
                if (line != null && line.gameObject != null)
                    DestroyImmediate(line.gameObject);
            }
            connectionLines.Clear();
        }
        #endregion

        #region Utilities
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

        public List<Receiver> GetConnectedReceivers() => new List<Receiver>(connectedReceivers);
        public int GetConnectionCount() => connectedReceivers.Count;
        public bool IsConnectedTo(Receiver receiver) => connectedReceivers.Contains(receiver);
        #endregion
    }
}