using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Connections
{
    /// <summary>
    /// Manages all connection strategies and switching between them
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        public enum StrategyType
        {
            StrongestSignal,
            BestServerWithInterference,  // Your existing strategy
            LoadBalanced,
            QualityFirst,
            EmergencyCoverage,
            NearestTransmitter
        }

        [Header("Strategy Selection")]
        public StrategyType currentStrategyType = StrategyType.StrongestSignalStrategy;

        private Dictionary<StrategyType, IConnectionStrategy> strategies;

        private void InitializeStrategies()
        {
            strategies = new Dictionary<StrategyType, IConnectionStrategy>
            {
                { StrategyType.StrongestSignal, new StrongestSignalStrategy() },
                { StrategyType.BestServerWithInterference, new BestServerWithInterferenceStrategy() },
                { StrategyType.LoadBalanced, new LoadBalancedStrategy() },
                { StrategyType.QualityFirst, new QualityFirstStrategy() },
                { StrategyType.EmergencyCoverage, new EmergencyCoverageStrategy() },
                { StrategyType.NearestTransmitter, new NearestTransmitterStrategy() }
            };
        }

        void Start()
        {
            InitializeStrategies();
            strategy = strategies[currentStrategyType];

            if (SimulationManager.Instance != null && !SimulationManager.Instance.isSimulationRunning)
            {
                SimulationManager.Instance.StartSimulation();
            }

            UpdateAllConnections();
    }

        void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateAllConnections();
                lastUpdateTime = Time.time;
            }
        }

        public void UpdateAllConnections()
        {
            if (strategy == null || SimulationManager.Instance == null) return;

            var transmitters = SimulationManager.Instance.transmitters;
            var receivers = SimulationManager.Instance.receivers;

            if (transmitters.Count == 0 || receivers.Count == 0) return;

            // Apply the current strategy
            strategy.UpdateConnections(transmitters, receivers, settings);

            // Update visual connections
            foreach (var transmitter in transmitters)
            {
                transmitter?.UpdateConnectionLines();
            }

            // Notify UI of connection statistics
            int totalConnections = CountTotalConnections(receivers);
            OnConnectionsUpdated?.Invoke(totalConnections, receivers.Count);
        }

        private int CountTotalConnections(List<Receiver> receivers)
        {
            int count = 0;
            foreach (var receiver in receivers)
            {
                if (receiver != null && receiver.IsConnected())
                {
                    count++;
                }
            }
            return count;
        }

        public void SetConnectionStrategy(StrategyType strategyType)
        {
            currentStrategyType = strategyType;
            strategy = strategies[strategyType];

            if (settings.enableDebugLogs)
            {
                Debug.Log($"[ConnectionManager] Strategy changed to: {strategy.StrategyName}");
            }

            // Force immediate update with new strategy
            UpdateAllConnections();
        }

        public List<string> GetAvailableStrategies()
        {
            var strategyNames = new List<string>();
            foreach (var kvp in strategies)
            {
                strategyNames.Add(kvp.Value.StrategyName);
            }
            return strategyNames;
        }


        // Public getters for UI

        public string GetCurrentStrategyDescription()
        {
            return strategy?.Description ?? "No strategy selected";
        }

        public string GetCurrentStrategyName()
        {
            return strategy?.StrategyName ?? "None";
        }

        public ConnectionSettings GetSettings()
        {
            return settings;
        }

        // Methods for runtime adjustment
        public void UpdateMinimumSignalThreshold(float threshold)
        {
            settings.minimumSignalThreshold = threshold;
            if (settings.enableDebugLogs)
            {
                Debug.Log($"[ConnectionManager] Signal threshold updated to {threshold:F1}dBm");
            }
        }

        public void UpdateHandoverMargin(float margin)
        {
            settings.handoverMargin = margin;
            if (settings.enableDebugLogs)
            {
                Debug.Log($"[ConnectionManager] Handover margin updated to {margin:F1}dB");
            }
        }

        public void ToggleDebugLogs(bool enable)
        {
            settings.enableDebugLogs = enable;
        }

        // Statistics for UI
        public Dictionary<string, object> GetConnectionStatistics()
        {
            var stats = new Dictionary<string, object>();

            if (SimulationManager.Instance == null) return stats;

            var receivers = SimulationManager.Instance.receivers;
            var transmitters = SimulationManager.Instance.transmitters;

            int connectedReceivers = 0;
            float averageSignalStrength = 0f;
            float minSignal = float.PositiveInfinity;
            float maxSignal = float.NegativeInfinity;

            foreach (var receiver in receivers)
            {
                if (receiver != null && receiver.IsConnected())
                {
                    connectedReceivers++;
                    float signal = receiver.currentSignalStrength;
                    averageSignalStrength += signal;

                    if (signal < minSignal) minSignal = signal;
                    if (signal > maxSignal) maxSignal = signal;
                }
            }

            if (connectedReceivers > 0)
            {
                averageSignalStrength /= connectedReceivers;
            }

            stats["connectedReceivers"] = connectedReceivers;
            stats["totalReceivers"] = receivers.Count;
            stats["connectionPercentage"] = receivers.Count > 0 ? (connectedReceivers / (float)receivers.Count) * 100f : 0f;
            stats["averageSignalStrength"] = averageSignalStrength;
            stats["minSignalStrength"] = minSignal == float.PositiveInfinity ? 0f : minSignal;
            stats["maxSignalStrength"] = maxSignal == float.NegativeInfinity ? 0f : maxSignal;
            stats["totalTransmitters"] = transmitters.Count;

            return stats;
        }
    }
}