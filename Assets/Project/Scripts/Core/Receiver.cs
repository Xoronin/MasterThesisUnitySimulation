using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Interfaces;

namespace RFSimulation.Core
{
    /// <summary>
    /// Represents a receiver in the RF simulation system. Handles signal strength, connection status, and visual feedback.
    /// </summary>
    public class Receiver : MonoBehaviour
    {
        #region Inspector Properties

        [Header("Receiver Properties")]
        public string uniqueID;
        public string technology = "5G"; // Technology type for realistic settings
        public float sensitivity = -90f; // dBm
        public Vector3 position;

        [Header("Connection Requirements")]
        public float minimumSINR = -6f; // dB (realistic 5G requirement)
        public float connectionMargin = 10f; // dB above sensitivity needed

        [Header("Connection Status")]
        public float currentSignalStrength = float.NegativeInfinity;
        public float currentSINR = float.NegativeInfinity; // Signal-to-Interference+Noise Ratio

        [Header("Visual Settings")]
        public Color excellentSignalColor = Color.green;
        public Color goodSignalColor = Color.yellow;
        public Color fairSignalColor = new Color(1f, 0.5f, 0f);
        public Color poorSignalColor = Color.red;
        public Color noSignalColor = Color.gray;

        [Header("Click Interaction")]
        public bool isClickable = true;

        // Connection tracking
        private Transmitter connectedTransmitter = null;
        private List<Transmitter> multipleConnections = new List<Transmitter>();
        private Renderer receiverRenderer;

        // Signal quality tracking
        private SignalQualityMetrics currentQuality;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Registers the receiver with the SimulationManager on Awake.
        /// </summary>
        void Awake()
        {
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.RegisterReceiver(this);
            }
        }

        /// <summary>
        /// Initializes receiver properties and registers with the SimulationManager.
        /// </summary>
        void Start()
        {
            if (string.IsNullOrEmpty(uniqueID))
            {
                uniqueID = "RX_" + GetInstanceID();
            }

            SetRealisticSensitivity();
            position = transform.position;
            receiverRenderer = GetComponent<Renderer>();

            if (isClickable && GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 1f;
            }

            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.RegisterReceiver(this);
            }
        }

        /// <summary>
        /// Unregisters the receiver from the SimulationManager on destroy.
        /// </summary>
        void OnDestroy()
        {
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.RemoveReceiver(this);
            }
        }

        #endregion

        #region Initialization and Sensitivity

        /// <summary>
        /// Sets realistic sensitivity values based on the technology type.
        /// </summary>
        private void SetRealisticSensitivity()
        {
            switch (technology.ToLower())
            {
                case "5g":
                    sensitivity = -105f;
                    minimumSINR = -3f;
                    connectionMargin = 10f;
                    break;
                case "lte":
                    sensitivity = -110f;
                    minimumSINR = -6f;
                    connectionMargin = 8f;
                    break;
                case "iot":
                    sensitivity = -120f;
                    minimumSINR = -10f;
                    connectionMargin = 5f;
                    break;
                case "emergency":
                    sensitivity = -115f;
                    minimumSINR = -8f;
                    connectionMargin = 5f;
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Signal and Quality Updates

        /// <summary>
        /// Updates the current signal strength and visual feedback.
        /// </summary>
        /// <param name="signalStrengthDbm">Signal strength in dBm.</param>
        public void UpdateSignalStrength(float signalStrengthDbm)
        {
            currentSignalStrength = signalStrengthDbm;
            UpdateSignalQuality();
            UpdateVisuals();

            if (Time.frameCount % 60 == 0)
            {
                string connectionInfo = GetConnectionInfo();
                Debug.Log($"{uniqueID}: {GetShortStatusText()} {connectionInfo}");
            }
        }

        /// <summary>
        /// Updates the current SINR value and signal quality.
        /// </summary>
        /// <param name="sinrDb">SINR in dB.</param>
        public void UpdateSINR(float sinrDb)
        {
            currentSINR = sinrDb;
            UpdateSignalQuality();
        }

        /// <summary>
        /// Updates comprehensive signal quality metrics.
        /// </summary>
        private void UpdateSignalQuality()
        {
            if (float.IsNegativeInfinity(currentSINR))
            {
                currentSINR = currentSignalStrength - (-110f);
            }

            TechnologyType techType = GetTechnologyType();
            currentQuality = new SignalQualityMetrics(currentSINR, techType);
        }

        /// <summary>
        /// Returns the technology type as an enum.
        /// </summary>
        private TechnologyType GetTechnologyType()
        {
            switch (technology.ToLower())
            {
                case "5g": return TechnologyType.FiveG;
                case "lte": return TechnologyType.LTE;
                case "iot": return TechnologyType.IoT;
                case "emergency": return TechnologyType.Emergency;
                default: return TechnologyType.LTE;
            }
        }

        #endregion

        #region Connection Logic

        /// <summary>
        /// Checks if the signal and SINR are sufficient for a connection.
        /// </summary>
        public bool IsSignalViable()
        {
            bool signalOK = currentSignalStrength >= (sensitivity + connectionMargin);
            bool sinrOK = float.IsNegativeInfinity(currentSINR) || currentSINR >= minimumSINR;
            return signalOK && sinrOK;
        }

        /// <summary>
        /// Sets the connected transmitter, checks signal viability, and manages the connection.
        /// </summary>
        /// <param name="transmitter">Transmitter instance.</param>
        public void SetConnectedTransmitter(Transmitter transmitter)
        {
            if (connectedTransmitter != transmitter)
            {
                if (connectedTransmitter != null)
                {
                    connectedTransmitter.ClearConnectionToReceiver(this);
                }

                connectedTransmitter = transmitter;

                if (connectedTransmitter != null)
                {
                    if (IsSignalViable())
                    {
                        connectedTransmitter.AddConnectionToReceiver(this);
                    }
                    else
                    {
                        connectedTransmitter = null;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the currently connected transmitter.
        /// </summary>
        public Transmitter GetConnectedTransmitter()
        {
            return connectedTransmitter;
        }

        /// <summary>
        /// Adds a transmitter to the multiple connections list if the signal is sufficient.
        /// </summary>
        /// <param name="transmitter">Transmitter instance.</param>
        public void AddConnectedTransmitter(Transmitter transmitter)
        {
            if (transmitter == null || multipleConnections.Contains(transmitter)) return;

            float signal = transmitter.CalculateSignalStrength(transform.position);
            if (signal >= sensitivity + connectionMargin)
            {
                multipleConnections.Add(transmitter);
                transmitter.AddConnectionToReceiver(this);
            }
        }

        /// <summary>
        /// Sets multiple connections to the provided list, checking signal viability.
        /// </summary>
        /// <param name="transmitters">List of transmitters.</param>
        public void SetMultipleConnections(List<Transmitter> transmitters)
        {
            foreach (var tx in multipleConnections)
            {
                if (tx != null)
                    tx.ClearConnectionToReceiver(this);
            }

            multipleConnections.Clear();

            foreach (var tx in transmitters)
            {
                if (tx != null)
                {
                    float signal = tx.CalculateSignalStrength(transform.position);
                    if (signal >= sensitivity + connectionMargin)
                    {
                        multipleConnections.Add(tx);
                        tx.AddConnectionToReceiver(this);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a copy of the current multiple connections.
        /// </summary>
        public List<Transmitter> GetMultipleConnections()
        {
            return new List<Transmitter>(multipleConnections);
        }

        /// <summary>
        /// Returns connection information as a string.
        /// </summary>
        private string GetConnectionInfo()
        {
            if (connectedTransmitter != null)
            {
                string sinrInfo = !float.IsNegativeInfinity(currentSINR) ?
                    $", SINR={currentSINR:F1}dB" : "";
                return $"→ {connectedTransmitter.uniqueID}{sinrInfo}";
            }
            else if (multipleConnections.Count > 0)
            {
                return $"→ {multipleConnections.Count} transmitters";
            }
            else
            {
                return "→ No connection";
            }
        }

        /// <summary>
        /// Checks if connected to at least one transmitter.
        /// </summary>
        public bool IsConnected()
        {
            return connectedTransmitter != null || multipleConnections.Count > 0;
        }

        #endregion

        #region Visualization

        /// <summary>
        /// Updates the receiver's color based on signal quality.
        /// </summary>
        private void UpdateVisuals()
        {
            if (receiverRenderer == null) return;
            Color targetColor = GetSignalQualityColor();
            receiverRenderer.material.color = targetColor;
        }

        /// <summary>
        /// Returns the color corresponding to the current signal quality.
        /// </summary>
        private Color GetSignalQualityColor()
        {
            if (currentSignalStrength <= float.NegativeInfinity)
                return noSignalColor;

            float margin = currentSignalStrength - sensitivity;

            if (margin < 0f) return noSignalColor;
            if (margin < 5f) return poorSignalColor;
            if (margin < 10f) return fairSignalColor;
            if (margin < 15f) return goodSignalColor;
            return excellentSignalColor;
        }

        #endregion

        #region Status and Metrics

        /// <summary>
        /// Calculates the signal quality as a percentage.
        /// </summary>
        public float GetSignalQuality()
        {
            if (currentSignalStrength <= sensitivity)
                return 0f;

            float margin = currentSignalStrength - sensitivity;
            float maxMargin = 30f;

            float quality = Mathf.Clamp01(margin / maxMargin) * 100f;

            if (!float.IsNegativeInfinity(currentSINR))
            {
                float sinrQuality = Mathf.Clamp01((currentSINR - minimumSINR) / 20f);
                quality = quality * 0.7f + sinrQuality * 100f * 0.3f;
            }

            return quality;
        }

        /// <summary>
        /// Returns the current signal quality metrics.
        /// </summary>
        public SignalQualityMetrics GetSignalMetrics()
        {
            return currentQuality ?? new SignalQualityMetrics(currentSINR, GetTechnologyType());
        }

        /// <summary>
        /// Returns a short status text for logging.
        /// </summary>
        public string GetShortStatusText()
        {
            string signal = $"Signal: {currentSignalStrength:F1}dBm";
            string quality = $"Quality: {GetSignalQuality():F0}%";
            return $"{signal}, {quality}";
        }

        /// <summary>
        /// Returns a detailed status text with metrics and connection status.
        /// </summary>
        public string GetStatusText()
        {
            if (!IsConnected())
            {
                return $"No Signal\nTechnology: {technology}\nSensitivity: {sensitivity:F1}dBm";
            }

            string signalText = $"Signal: {currentSignalStrength:F1} dBm";
            string qualityText = $"Quality: {GetSignalQuality():F0}%";
            string categoryText = $"Category: {currentQuality?.category ?? SignalQualityCategory.NoService}";

            string sinrText = "";
            if (!float.IsNegativeInfinity(currentSINR))
            {
                sinrText = $"\nSINR: {currentSINR:F1} dB";
            }

            string throughputText = "";
            if (currentQuality != null)
            {
                throughputText = $"\nThroughput: ~{currentQuality.throughputMbps:F1} Mbps";
            }

            string connectionText = "";
            if (connectedTransmitter != null)
            {
                connectionText = $"\nConnected: {connectedTransmitter.uniqueID}";
            }
            else if (multipleConnections.Count > 0)
            {
                connectionText = $"\nConnections: {multipleConnections.Count}";
            }

            return $"{signalText}\n{qualityText}\n{categoryText}{sinrText}{throughputText}{connectionText}";
        }

        #endregion

        #region Runtime and Analysis Functions

        /// <summary>
        /// Sets the technology and updates sensitivity and signal quality.
        /// </summary>
        /// <param name="newTechnology">Technology name.</param>
        public void SetTechnology(string newTechnology)
        {
            technology = newTechnology;
            SetRealisticSensitivity();
            UpdateSignalQuality();
        }

        /// <summary>
        /// Checks if a given SINR would be acceptable.
        /// </summary>
        /// <param name="testSINR">Test SINR value.</param>
        public bool WouldSINRBeAcceptable(float testSINR)
        {
            return testSINR >= minimumSINR;
        }

        /// <summary>
        /// Returns the expected throughput for current conditions.
        /// </summary>
        public float GetExpectedThroughput()
        {
            if (currentQuality != null)
                return currentQuality.throughputMbps;

            return SignalQualityAnalyzer.EstimateThroughput(currentSINR, GetTechnologyType());
        }

        /// <summary>
        /// Returns the connection reliability.
        /// </summary>
        public float GetConnectionReliability()
        {
            if (currentQuality != null)
                return currentQuality.reliability;

            return SignalQualityAnalyzer.CalculateReliability(currentSINR, GetTechnologyType());
        }

        #endregion

        #region Debug & Test Utilities

        /// <summary>
        /// Debugs signal calculation for all transmitters in the SimulationManager.
        /// </summary>
        [ContextMenu("Debug Signal Calculation")]
        public void DebugSignalCalculation()
        {
            if (SimulationManager.Instance == null)
            {
                Debug.LogError("No SimulationManager found!");
                return;
            }

            Debug.Log($"=== {uniqueID} Signal Debug ===");
            Debug.Log($"Current Signal Strength: {currentSignalStrength}");
            Debug.Log($"Transmitter Count: {SimulationManager.Instance.transmitters.Count}");

            foreach (var tx in SimulationManager.Instance.transmitters)
            {
                if (tx != null)
                {
                    float signal = tx.CalculateSignalStrength(transform.position);
                    Debug.Log($"Signal from {tx.uniqueID}: {signal:F1} dBm");
                    Debug.Log($"Viable? {signal >= (sensitivity + connectionMargin)} (need {sensitivity + connectionMargin:F1} dBm)");
                }
            }
        }

        #endregion
    }
}