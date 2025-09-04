using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Propagation;
using RFSimulation.Visualization;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;

namespace RFSimulation.Core
{
    /// <summary>
    /// Represents a radio transmitter in the simulation, handling signal propagation, connections, and visualization.
    /// </summary>
    public class Transmitter : MonoBehaviour
    {
        #region Inspector Properties

        [Header("Transmitter Properties")]
        public string uniqueID;
        public float transmitterPower = 40f; // dBm
        public float antennaGain = 12f; // dBi
        public float frequency = 2400f; // MHz
        public Vector3 position;

        [Header("Click Interaction")]
        public bool isClickable = true;

        [Header("Propagation Settings")]
        public PropagationModel propagationModel = PropagationModel.LogDistance;
        public EnvironmentType environmentType = EnvironmentType.Urban;

        [Header("Components")]
        public TransmitterConnectionManager connectionManager;
        public TransmitterVisualizer visualizer;
        public TransmitterValidator validator;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Registers the transmitter with the simulation manager on awake.
        /// </summary>
        void Awake()
        {
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.RegisterTransmitter(this);
            }
        }

        /// <summary>
        /// Initializes transmitter and its components at the start of the simulation.
        /// </summary>
        void Start()
        {
            InitializeTransmitter();
            SetupComponents();
            RegisterWithSimulation();
        }

        /// <summary>
        /// Cleans up components and unregisters transmitter when destroyed.
        /// </summary>
        void OnDestroy()
        {
            CleanupComponents();
            UnregisterFromSimulation();
        }

        #endregion

        #region Initialization & Core Logic

        /// <summary>
        /// Initializes transmitter properties and ensures collider for interaction.
        /// </summary>
        private void InitializeTransmitter()
        {
            if (string.IsNullOrEmpty(uniqueID))
            {
                uniqueID = "TX_" + GetInstanceID();
            }
            position = transform.position;

            // Ensure collider exists for clicking
            if (isClickable && GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 1f; // Make it easier to click
            }
        }

        /// <summary>
        /// Calculates the received signal strength at a given receiver position.
        /// </summary>
        /// <param name="receiverPosition">Position of the receiver.</param>
        /// <returns>Received signal strength in dBm.</returns>
        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            var context = PropagationContext.Create(position, receiverPosition, transmitterPower, frequency);
            context.AntennaGainDbi = antennaGain;
            context.Model = propagationModel;
            context.Environment = environmentType;

            var calculator = new PathLossCalculator();
            return calculator.CalculateReceivedPower(context);
        }

        /// <summary>
        /// Gets the signal quality category at a specified position.
        /// </summary>
        /// <param name="testPosition">Position to test signal quality.</param>
        /// <returns>Signal quality category.</returns>
        public SignalQualityCategory GetSignalQualityAt(Vector3 testPosition)
        {
            var context = CreatePropagationContext(testPosition);
            var calculator = new PathLossCalculator();
            return calculator.GetSignalQuality(context);
        }

        /// <summary>
        /// Gets detailed signal quality metrics at a specified position for a given technology.
        /// </summary>
        /// <param name="testPosition">Position to test.</param>
        /// <param name="technology">Technology type (default: LTE).</param>
        /// <returns>Signal quality metrics.</returns>
        public SignalQualityMetrics GetSignalQualityMetricsAt(Vector3 testPosition, TechnologyType technology = TechnologyType.LTE)
        {
            var context = CreatePropagationContext(testPosition);
            context.Technology = technology;
            var calculator = new PathLossCalculator();
            return calculator.GetSignalQualityMetrics(context);
        }

        /// <summary>
        /// Determines if a position is within the transmitter's coverage area.
        /// </summary>
        /// <param name="testPosition">Position to test.</param>
        /// <param name="sensitivityDbm">Receiver sensitivity threshold (default: -105 dBm).</param>
        /// <returns>True if in coverage, otherwise false.</returns>
        public bool IsInCoverage(Vector3 testPosition, float sensitivityDbm = -105f)
        {
            float signal = CalculateSignalStrength(testPosition);
            float margin = 10f;
            return signal >= (sensitivityDbm + margin);
        }

        /// <summary>
        /// Creates a propagation context for signal calculations.
        /// </summary>
        /// <param name="receiverPosition">Receiver position.</param>
        /// <returns>Propagation context instance.</returns>
        private PropagationContext CreatePropagationContext(Vector3 receiverPosition)
        {
            var context = PropagationContext.Create(position, receiverPosition, transmitterPower, frequency);
            context.AntennaGainDbi = antennaGain;
            context.Model = propagationModel;
            context.Environment = environmentType;
            context.ReceiverSensitivityDbm = -105f;
            return context;
        }

        #endregion

        #region Component Management

        /// <summary>
        /// Sets up required transmitter components and initializes them.
        /// </summary>
        private void SetupComponents()
        {
            connectionManager = GetOrAddComponent<TransmitterConnectionManager>();
            connectionManager.Initialize(this);

            visualizer = GetOrAddComponent<TransmitterVisualizer>();
            visualizer.Initialize(this);

            validator = GetOrAddComponent<TransmitterValidator>();
        }

        /// <summary>
        /// Cleans up transmitter components.
        /// </summary>
        private void CleanupComponents()
        {
            connectionManager?.Cleanup();
            visualizer?.Cleanup();
        }

        /// <summary>
        /// Gets an existing component or adds it if missing.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <returns>Component instance.</returns>
        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();
            return component;
        }

        #endregion

        #region Public API - Connection Management

        /// <summary>
        /// Adds a connection to the specified receiver.
        /// </summary>
        /// <param name="receiver">Receiver to connect.</param>
        public void AddConnectionToReceiver(Receiver receiver) => connectionManager?.AddConnectionToReceiver(receiver);

        /// <summary>
        /// Clears the connection to the specified receiver.
        /// </summary>
        /// <param name="receiver">Receiver to disconnect.</param>
        public void ClearConnectionToReceiver(Receiver receiver) => connectionManager?.ClearConnectionToReceiver(receiver);

        /// <summary>
        /// Clears all connection lines.
        /// </summary>
        public void ClearAllLines() => connectionManager?.ClearAllLines();

        /// <summary>
        /// Updates the visual connection lines.
        /// </summary>
        public void UpdateConnectionLines() => connectionManager?.UpdateConnectionLines();

        /// <summary>
        /// Gets the list of connected receivers.
        /// </summary>
        /// <returns>List of connected receivers.</returns>
        public List<Receiver> GetConnectedReceivers() => connectionManager?.GetConnectedReceivers() ?? new List<Receiver>();

        /// <summary>
        /// Gets the number of current connections.
        /// </summary>
        /// <returns>Connection count.</returns>
        public int GetConnectionCount() => connectionManager?.GetConnectionCount() ?? 0;

        /// <summary>
        /// Checks if the transmitter is connected to the specified receiver.
        /// </summary>
        /// <param name="receiver">Receiver to check.</param>
        /// <returns>True if connected, otherwise false.</returns>
        public bool IsConnectedTo(Receiver receiver) => connectionManager?.IsConnectedTo(receiver) ?? false;

        #endregion

        #region Public API - Visualization

        /// <summary>
        /// Toggles the visibility of connection lines.
        /// </summary>
        /// <param name="show">Whether to show connections.</param>
        public void ToggleConnections(bool show) => visualizer?.ToggleConnections(show);

        /// <summary>
        /// Toggles the visibility of the coverage area.
        /// </summary>
        /// <param name="show">Whether to show coverage area.</param>
        public void ToggleCoverageArea(bool show) => visualizer?.ToggleCoverageArea(show);

        #endregion

        #region Public API - Parameter Updates

        /// <summary>
        /// Updates the transmitter power after validation.
        /// </summary>
        /// <param name="newPowerDbm">New power value in dBm.</param>
        public void UpdateTransmitterPower(float newPowerDbm)
        {
            if (validator.ValidatePower(newPowerDbm))
            {
                transmitterPower = newPowerDbm;
                visualizer?.RecalculateCoverage();
                Debug.Log($"{uniqueID} power updated to {transmitterPower:F1} dBm");
            }
        }

        /// <summary>
        /// Updates the transmitter frequency after validation.
        /// </summary>
        /// <param name="newFrequencyMHz">New frequency in MHz.</param>
        public void UpdateFrequency(float newFrequencyMHz)
        {
            if (validator.ValidateFrequency(newFrequencyMHz))
            {
                frequency = newFrequencyMHz;
                visualizer?.RecalculateCoverage();
                Debug.Log($"{uniqueID} frequency updated to {frequency:F0} MHz");
            }
        }

        /// <summary>
        /// Updates the antenna gain after validation.
        /// </summary>
        /// <param name="newGainDbi">New antenna gain in dBi.</param>
        public void UpdateAntennaGain(float newGainDbi)
        {
            if (validator.ValidateAntennaGain(newGainDbi))
            {
                antennaGain = newGainDbi;
                visualizer?.RecalculateCoverage();
                Debug.Log($"{uniqueID} antenna gain updated to {antennaGain:F1} dBi");
            }
        }

        #endregion

        #region Status and Information

        /// <summary>
        /// Gets a status summary of the transmitter including coverage estimate.
        /// </summary>
        /// <returns>Status text.</returns>
        public string GetStatusText()
        {
            string basicInfo = $"Power: {transmitterPower:F1} dBm\n" +
                              $"Gain: {antennaGain:F1} dBi\n" +
                              $"Frequency: {frequency:F0} MHz\n" +
                              $"Model: {propagationModel}\n" +
                              $"Environment: {environmentType}\n" +
                              $"Connections: {GetConnectionCount()}";

            // Calculate coverage
            var context = CreatePropagationContext(position + Vector3.forward);
            var calculator = new PathLossCalculator();
            float coverage = calculator.EstimateCoverageRadius(context);

            return basicInfo + $"\nCoverage: ~{coverage:F0}m";
        }

        #endregion

        #region Simulation Registration

        /// <summary>
        /// Registers the transmitter with the simulation manager.
        /// </summary>
        private void RegisterWithSimulation()
        {
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.RegisterTransmitter(this);
            }
        }

        /// <summary>
        /// Unregisters the transmitter from the simulation manager.
        /// </summary>
        private void UnregisterFromSimulation()
        {
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.RemoveTransmitter(this);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the visibility of connection lines.
        /// </summary>
        public bool showConnections
        {
            get
            {
                var connectionManager = GetComponent<TransmitterConnectionManager>();
                return connectionManager != null ? connectionManager.showConnections : true;
            }
            set
            {
                var connectionManager = GetComponent<TransmitterConnectionManager>();
                if (connectionManager != null)
                    connectionManager.showConnections = value;
            }
        }

        /// <summary>
        /// Gets or sets the visibility of the coverage area.
        /// </summary>
        public bool showCoverageArea
        {
            get
            {
                var visualizer = GetComponent<TransmitterVisualizer>();
                return visualizer != null ? visualizer.showCoverageArea : true;
            }
            set
            {
                var visualizer = GetComponent<TransmitterVisualizer>();
                if (visualizer != null)
                    visualizer.showCoverageArea = value;
            }
        }

        #endregion

        #region Debug & Test Utilities

        /// <summary>
        /// Logs transmitter values for debugging.
        /// </summary>
        [ContextMenu("Debug Transmitter Values")]
        public void DebugTransmitterValues()
        {
            Debug.Log($"=== {uniqueID} Debug ===");
            Debug.Log($"Power: {transmitterPower} dBm");
            Debug.Log($"Frequency: {frequency} MHz");
            Debug.Log($"Antenna Gain: {antennaGain} dBi");
            Debug.Log($"Position: {transform.position}");
            Debug.Log($"Propagation Model: {propagationModel}");
            Debug.Log($"Environment: {environmentType}");
        }

        /// <summary>
        /// Tests signal calculation to the nearest receiver and logs results.
        /// </summary>
        [ContextMenu("Test Signal to Nearest Receiver")]
        public void TestSignalToNearestReceiver()
        {
            if (SimulationManager.Instance == null || SimulationManager.Instance.receivers.Count == 0)
            {
                Debug.LogError("No receivers found!");
                return;
            }

            var receiver = SimulationManager.Instance.receivers[0];

            Debug.Log("=== SIMPLE CONNECTION TEST ===");
            Debug.Log($"Transmitter: {uniqueID} at {transform.position}");
            Debug.Log($"Receiver: {receiver.uniqueID} at {receiver.transform.position}");

            float distance = Vector3.Distance(transform.position, receiver.transform.position);
            Debug.Log($"Distance: {distance:F2} meters");

            Debug.Log($"TX Power: {transmitterPower} dBm");
            Debug.Log($"TX Gain: {antennaGain} dBi");
            Debug.Log($"TX Frequency: {frequency} MHz");
            Debug.Log($"Propagation Model: {propagationModel}");

            // Test the calculation step by step
            float signal = CalculateSignalStrength(receiver.transform.position);

            Debug.Log($"CALCULATED SIGNAL: {signal} dBm");
            Debug.Log($"Receiver Sensitivity: {receiver.sensitivity} dBm");
            Debug.Log($"Receiver Connection Margin: {receiver.connectionMargin} dB");
            Debug.Log($"Required Signal: {receiver.sensitivity + receiver.connectionMargin} dBm");

            bool shouldConnect = signal >= (receiver.sensitivity + receiver.connectionMargin);
            Debug.Log($"Should Connect: {shouldConnect}");
            Debug.Log($"Is Actually Connected: {receiver.IsConnected()}");

            if (float.IsInfinity(signal) || float.IsNaN(signal))
            {
                Debug.LogError("*** SIGNAL IS INFINITY - THIS IS THE PROBLEM ***");
            }
        }

        #endregion
    }
}