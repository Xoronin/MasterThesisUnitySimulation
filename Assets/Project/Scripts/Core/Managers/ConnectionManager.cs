using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Core.Components;

namespace RFSimulation.Core.Managers
{
    [System.Serializable]
    public class ConnectionSettings
    {
        [UnityEngine.Header("Signal Thresholds")]
        public float minimumSignalThreshold = -90f; // dBm
        public float connectionMargin = 10f; // dB above sensitivity
        public float handoverMargin = 3f; // dB to prevent ping-ponging

        [UnityEngine.Header("Quality Requirements")]
        public float minimumSINR = -6f; // dB
        public float excellentSignalThreshold = 15f; // dB above sensitivity
        public float goodSignalThreshold = 10f; // dB above sensitivity

        [UnityEngine.Header("Multi-Connection Settings")]
        public int maxSimultaneousConnections = 3;
        public bool allowLoadBalancing = true;

        [UnityEngine.Header("Debug")]
        public bool enableDebugLogs = false;
    }

    /// <summary>
    /// Manages all connection strategies and switching between them
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        [Header("Connection Settings")]
        public ConnectionSettings settings = new ConnectionSettings();

        [Header("Performance")]
        public float updateInterval = 1f;
        private float lastUpdateTime = 0f;

        // Events for UI updates
        public System.Action<int, int> OnConnectionsUpdated; // (totalConnections, totalReceivers)

        void Start()
        {
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
            if (SimulationManager.Instance == null) return;

            var transmitters = SimulationManager.Instance.transmitters;
            var receivers = SimulationManager.Instance.receivers;

            if (transmitters.Count == 0 || receivers.Count == 0) return;

            // Apply the current strategy
            UpdateConnections(transmitters, receivers, settings);

            // Notify UI of connection statistics
            int totalConnections = CountTotalConnections(receivers);
            OnConnectionsUpdated?.Invoke(totalConnections, receivers.Count);
        }


        public void UpdateConnections(List<Transmitter> transmitters, List<Receiver> receivers, ConnectionSettings settings)
        {
            foreach (var receiver in receivers)
            {
                if (receiver == null) continue;

                Transmitter bestTx = null;
                float bestSignal = float.NegativeInfinity;

                foreach (var transmitter in transmitters)
                {
                    if (transmitter == null) continue;

                    float signal = transmitter.CalculateSignalStrength(receiver.transform.position);

                    // Apply handover margin to current serving cell
                    float effectiveSignal = signal;
                    if (receiver.GetConnectedTransmitter() == transmitter)
                    {
                        effectiveSignal += settings.handoverMargin;
                    }

                    if (effectiveSignal > bestSignal && signal > settings.minimumSignalThreshold)
                    {
                        bestSignal = signal;
                        bestTx = transmitter;
                    }
                }

                // Update receiver connection
                receiver.UpdateSignalStrength(bestSignal);
                receiver.UpdateSINR(bestSignal > float.NegativeInfinity ? 20f : float.NegativeInfinity); // Assume good SINR

                // Clear connections to other transmitters
                foreach (var transmitter in transmitters)
                {
                    if (transmitter != bestTx)
                    {
                        transmitter.DisconnectFromReceiver(receiver);
                    }
                }

                // Establish new connection if we found a suitable transmitter
                if (bestTx != null)
                {
                    bestTx.ConnectToReceiver(receiver);
                }
                else
                {
                    receiver.SetConnectedTransmitter(null);
                }
            }
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



        // Public getters for UI

        public ConnectionSettings GetSettings()
        {
            return settings;
        }

        // Methods for runtime adjustment
        public void UpdateMinimumSignalThreshold(float threshold)
        {
            settings.minimumSignalThreshold = threshold;
        }

        public void UpdateHandoverMargin(float margin)
        {
            settings.handoverMargin = margin;
        }

        public void ApplySettings(ConnectionSettings newSettings)
        {
            settings = newSettings;
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