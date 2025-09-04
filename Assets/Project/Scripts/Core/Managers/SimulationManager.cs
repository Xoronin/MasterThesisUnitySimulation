using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Connections;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Core
{
    /// <summary>
    /// Main simulation orchestrator - delegates responsibilities to specialized managers
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        [Header("Simulation Control")]
        public bool autoStart = true;
        public bool isSimulationRunning = false;

        [Header("Equipment Lists")]
        public List<Transmitter> transmitters = new List<Transmitter>();
        public List<Receiver> receivers = new List<Receiver>();

        [Header("Managers")]
        public ConnectionManager connectionManager;

        // Events for UI and other systems
        public System.Action OnSimulationStarted;
        public System.Action OnSimulationStopped;
        public System.Action<int, int> OnEquipmentCountChanged; // (transmitters, receivers)

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSimulation();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (autoStart)
            {
                StartSimulation();
            }
        }

        private void InitializeSimulation()
        {
            // Find or create ConnectionManager
            if (connectionManager == null)
            {
                connectionManager = GetComponent<ConnectionManager>();
                if (connectionManager == null)
                {
                    connectionManager = gameObject.AddComponent<ConnectionManager>();
                }
            }

            if (connectionManager != null && !connectionManager.enabled)
            {
                connectionManager.enabled = true;
            }

            Debug.Log("✅ SimulationManager initialized");
        }

        #region Simulation Control

        public void StartSimulation()
        {
            isSimulationRunning = true;
            OnSimulationStarted?.Invoke();
            Debug.Log("▶️ Simulation started");
        }

        public void StopSimulation()
        {
            isSimulationRunning = false;
            OnSimulationStopped?.Invoke();
            Debug.Log("⏹️ Simulation stopped");
        }

        public void PauseSimulation()
        {
            isSimulationRunning = false;
            Debug.Log("⏸️ Simulation paused");
        }

        public void ResumeSimulation()
        {
            isSimulationRunning = true;
            Debug.Log("▶️ Simulation resumed");
        }

        public void RestartSimulation()
        {
            StopSimulation();
            ClearAllConnections();
            StartSimulation();
            Debug.Log("🔄 Simulation restarted");
        }

        #endregion

        #region Equipment Management

        public void RegisterTransmitter(Transmitter transmitter)
        {
            if (transmitter == null || transmitters.Contains(transmitter)) return;

            transmitters.Add(transmitter);
            OnEquipmentCountChanged?.Invoke(transmitters.Count, receivers.Count);

            if (connectionManager != null && isSimulationRunning)
            {
                StartCoroutine(DelayedConnectionUpdate());
            }
        }

        public void RemoveTransmitter(Transmitter transmitter)
        {
            if (transmitter == null || !transmitters.Contains(transmitter)) return;

            // Clear all connections to this transmitter
            transmitter.ClearAllLines();
            transmitters.Remove(transmitter);

            OnEquipmentCountChanged?.Invoke(transmitters.Count, receivers.Count);
        }

        public void RegisterReceiver(Receiver receiver)
        {
            if (receiver == null || receivers.Contains(receiver)) return;

            receivers.Add(receiver);
            OnEquipmentCountChanged?.Invoke(transmitters.Count, receivers.Count);

            if (connectionManager != null && isSimulationRunning)
            {
                StartCoroutine(DelayedConnectionUpdate());
            }
        }

        public void RemoveReceiver(Receiver receiver)
        {
            if (receiver == null || !receivers.Contains(receiver)) return;

            // Clear all connections to this receiver
            foreach (var transmitter in transmitters)
            {
                transmitter?.ClearConnectionToReceiver(receiver);
            }

            receivers.Remove(receiver);
            OnEquipmentCountChanged?.Invoke(transmitters.Count, receivers.Count);
        }

        public void ClearAllEquipment()
        {
            // Clear connections first
            ClearAllConnections();

            // Remove all equipment
            foreach (var tx in transmitters.ToArray())
            {
                if (tx != null) DestroyImmediate(tx.gameObject);
            }

            foreach (var rx in receivers.ToArray())
            {
                if (rx != null) DestroyImmediate(rx.gameObject);
            }

            transmitters.Clear();
            receivers.Clear();

            OnEquipmentCountChanged?.Invoke(0, 0);
        }

        #endregion

        #region Connection Management

        public void ClearAllConnections()
        {
            foreach (var transmitter in transmitters)
            {
                transmitter?.ClearAllLines();
            }
        }

        public void SetConnectionStrategy(string strategyName)
        {
            if (connectionManager != null)
            {
                connectionManager.SetStrategy(strategyName);
            }
        }

        public void UpdateConnectionSettings(ConnectionSettings settings)
        {
            if (connectionManager != null)
            {
                connectionManager.settings = settings;
            }
        }

        private System.Collections.IEnumerator DelayedConnectionUpdate()
        {
            yield return new WaitForEndOfFrame(); // Wait for object to fully initialize
            yield return new WaitForSeconds(0.1f); // Small additional delay

            if (connectionManager != null)
            {
                connectionManager.UpdateAllConnections();
            }
        }

        #endregion

        #region Validation and Statistics

        public void ValidateSimulation()
        {
            int issues = 0;

            // Check for null references
            transmitters.RemoveAll(tx => tx == null);
            receivers.RemoveAll(rx => rx == null);

            // Validate transmitter configurations
            foreach (var tx in transmitters)
            {
                if (tx.transmitterPower <= 0)
                {
                    issues++;
                }

                if (tx.frequency <= 0)
                {
                    issues++;
                }
            }

            // Validate receiver configurations
            foreach (var rx in receivers)
            {
                if (rx.sensitivity > -10f) // Unrealistically high sensitivity
                {
                    issues++;
                }
            }

            if (issues == 0)
            {
                Debug.Log("✅ Simulation validation passed");
            }
            else
            {
                Debug.Log($"⚠️ Simulation validation found {issues} issues");
            }
        }

        public SimulationStatistics GetStatistics()
        {
            var stats = new SimulationStatistics
            {
                transmitterCount = transmitters.Count,
                receiverCount = receivers.Count,
                isRunning = isSimulationRunning
            };

            if (connectionManager != null)
            {
                var connectionStats = connectionManager.GetConnectionStatistics();
                stats.connectedReceivers = (int)connectionStats.GetValueOrDefault("connectedReceivers", 0);
                stats.connectionPercentage = (float)connectionStats.GetValueOrDefault("connectionPercentage", 0f);
                stats.averageSignalStrength = (float)connectionStats.GetValueOrDefault("averageSignalStrength", 0f);
                stats.currentStrategy = (string)connectionStats.GetValueOrDefault("currentStrategy", "Unknown");
            }

            return stats;
        }

        #endregion

        #region Debug and Testing

        [ContextMenu("Validate Simulation")]
        public void ContextMenuValidateSimulation()
        {
            ValidateSimulation();
        }

        [ContextMenu("Print Statistics")]
        public void ContextMenuPrintStatistics()
        {
            var stats = GetStatistics();
            Debug.Log($"📊 Simulation Statistics:\n" +
                     $"   Transmitters: {stats.transmitterCount}\n" +
                     $"   Receivers: {stats.receiverCount}\n" +
                     $"   Connected: {stats.connectedReceivers} ({stats.connectionPercentage:F1}%)\n" +
                     $"   Avg Signal: {stats.averageSignalStrength:F1}dBm\n" +
                     $"   Strategy: {stats.currentStrategy}\n" +
                     $"   Status: {(stats.isRunning ? "Running" : "Stopped")}");
        }

        [ContextMenu("Force Update Connections")]
        public void ContextMenuForceUpdateConnections()
        {
            if (connectionManager != null)
            {
                connectionManager.UpdateAllConnections();
                Debug.Log("🔄 Forced connection update");
            }
        }

        #endregion
    }

    /// <summary>
    /// Data structure for simulation statistics
    /// </summary>
    [System.Serializable]
    public class SimulationStatistics
    {
        public int transmitterCount;
        public int receiverCount;
        public int connectedReceivers;
        public float connectionPercentage;
        public float averageSignalStrength;
        public string currentStrategy;
        public bool isRunning;
    }
}