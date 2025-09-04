using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using RFSimulation.Core;
using RFSimulation.Connections;

namespace RFSimulation.UI
{
    /// <summary>
    /// Simplified status display showing only essential simulation metrics
    /// </summary>
    public class StatusDisplay : MonoBehaviour
    {
        [Header("Essential Status")]
        public Text statusText; // Main status line
        public Text equipmentText; // TX/RX count
        public Text connectionText; // Connection info
        public Text strategyText; // Current strategy

        [Header("Visual Indicator")]
        public Image statusIndicator; // Simple color indicator

        [Header("Update Settings")]
        public float updateInterval = 1f;
        public bool enableRealTimeUpdates = true;

        // Internal tracking
        private float lastUpdateTime = 0f;
        private SimulationManager simulationManager;
        private ConnectionManager connectionManager;

        void Start()
        {
            Initialize();
        }

        void Update()
        {
            if (enableRealTimeUpdates && Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateDisplay();
                lastUpdateTime = Time.time;
            }
        }

        private void Initialize()
        {
            simulationManager = SimulationManager.Instance;
            if (simulationManager != null)
            {
                connectionManager = simulationManager.connectionManager;
            }
        }

        public void UpdateDisplay()
        {
            UpdateStatusText();
            UpdateEquipmentCount();
            UpdateConnectionInfo();
            UpdateStrategy();
            UpdateStatusIndicator();
        }

        private void UpdateStatusText()
        {
            string status = "Ready";

            if (simulationManager != null)
            {
                status = simulationManager.isSimulationRunning ? "Running" : "Stopped";
            }

            SetText(statusText, status);
        }

        private void UpdateEquipmentCount()
        {
            if (simulationManager == null)
            {
                SetText(equipmentText, "Equipment: 0 TX, 0 RX");
                return;
            }

            int txCount = simulationManager.transmitters.Count;
            int rxCount = simulationManager.receivers.Count;

            SetText(equipmentText, $"Equipment: {txCount} TX, {rxCount} RX");
        }

        private void UpdateConnectionInfo()
        {
            if (simulationManager == null)
            {
                SetText(connectionText, "Connections: 0/0 (0%)");
                return;
            }

            var receivers = simulationManager.receivers;
            int connectedCount = receivers.Count(r => r != null && r.IsConnected());
            float percentage = receivers.Count > 0 ? (connectedCount / (float)receivers.Count) * 100f : 0f;

            SetText(connectionText, $"Connections: {connectedCount}/{receivers.Count} ({percentage:F0}%)");
        }

        private void UpdateStrategy()
        {
            if (connectionManager != null)
            {
                string strategyName = connectionManager.GetCurrentStrategyName();
                SetText(strategyText, $"Strategy: {strategyName}");
            }
            else
            {
                SetText(strategyText, "Strategy: None");
            }
        }

        private void UpdateStatusIndicator()
        {
            if (statusIndicator == null || simulationManager == null) return;

            Color indicatorColor = Color.gray; // Default: no data

            if (simulationManager.receivers.Count > 0)
            {
                int connectedCount = simulationManager.receivers.Count(r => r != null && r.IsConnected());
                float connectionPercentage = (connectedCount / (float)simulationManager.receivers.Count) * 100f;

                if (connectionPercentage >= 80f)
                    indicatorColor = Color.green; // Good connectivity
                else if (connectionPercentage >= 50f)
                    indicatorColor = Color.yellow; // Moderate connectivity  
                else
                    indicatorColor = Color.red; // Poor connectivity
            }

            statusIndicator.color = indicatorColor;
        }

        private void SetText(Text textComponent, string value)
        {
            if (textComponent != null)
            {
                textComponent.text = value;
            }
        }

        // Public methods for manual updates
        public void ForceUpdate()
        {
            UpdateDisplay();
        }

        public void ToggleRealTimeUpdates(bool enabled)
        {
            enableRealTimeUpdates = enabled;
            if (enabled)
            {
                UpdateDisplay();
            }
        }

        // Context menu for debugging
        [ContextMenu("Force Update Display")]
        public void ForceUpdateContext()
        {
            UpdateDisplay();
        }

        [ContextMenu("Print Simple Statistics")]
        public void PrintSimpleStatistics()
        {
            if (simulationManager == null) return;

            Debug.Log("=== Simple Statistics ===");
            Debug.Log($"Transmitters: {simulationManager.transmitters.Count}");
            Debug.Log($"Receivers: {simulationManager.receivers.Count}");

            var receivers = simulationManager.receivers;
            int connected = receivers.Count(r => r != null && r.IsConnected());
            float percentage = receivers.Count > 0 ? (connected / (float)receivers.Count) * 100f : 0f;

            Debug.Log($"Connected: {connected}/{receivers.Count} ({percentage:F1}%)");
            Debug.Log($"Strategy: {connectionManager?.GetCurrentStrategyName() ?? "None"}");
        }
    }
}