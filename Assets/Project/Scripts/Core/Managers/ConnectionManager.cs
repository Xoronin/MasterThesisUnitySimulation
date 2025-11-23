using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core.Components;

namespace RFSimulation.Core.Managers
{
    [System.Serializable]
    public class ConnectionSettings
    {
        [UnityEngine.Header("Signal Thresholds")]
        public float minimumSignalThreshold = -90f;         
        public float connectionMargin = 10f;                
        public float handoverMargin = 3f;                   
    }

    public class ConnectionManager : MonoBehaviour
    {
        [Header("Connection Settings")]
        public ConnectionSettings settings = new ConnectionSettings();

        [Header("Performance")]
        public float updateInterval = 1f;
        private float lastUpdateTime = 0f;

        public System.Action<int, int> OnConnectionsUpdated;

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

            UpdateConnections(transmitters, receivers, settings);

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

                receiver.UpdateSignalStrength(bestSignal);

                foreach (var transmitter in transmitters)
                {
                    if (transmitter != bestTx)
                    {
                        transmitter.DisconnectFromReceiver(receiver);
                    }
                }

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

        public void ApplySettings(ConnectionSettings newSettings)
        {
            settings = newSettings;
        }
    }
}