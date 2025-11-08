using RFSimulation.Core.Components;
using RFSimulation.UI;
using RFSimulation.Visualization;
using System.Collections.Generic;
using UnityEngine;

namespace RFSimulation.Core.Managers
{
    public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        [Header("Simulation Control")]
        public bool isSimulationRunning = false;

        [Header("Equipment Lists")]
        public List<Transmitter> transmitters = new List<Transmitter>();
        public List<Receiver> receivers = new List<Receiver>();

        [Header("Managers")]
        public ConnectionManager connectionManager;
        public BuildingManager buildingManager;
        public ScenarioManager scenarioManager;
        public UIManager uiManager;

        public SignalHeatmap signalHeatmap;

        public System.Action OnSimulationStarted;
        public System.Action OnSimulationStopped;
        public System.Action<int, int> OnEquipmentCountChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeSimulation();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            StartSimulation();
        }

        private void InitializeSimulation()
        {
            if (connectionManager == null)
            {
                connectionManager = GetComponent<ConnectionManager>();

            }

            if (connectionManager != null && !connectionManager.enabled)
            {
                connectionManager.enabled = true;
            }

            if (buildingManager == null)
            {
                buildingManager = GetComponent<BuildingManager>();

            }

            if (scenarioManager == null)
            {
                scenarioManager = GetComponent<ScenarioManager>();

            }

            if (uiManager == null)
            {
                uiManager = GetComponent<UIManager>();
            }


            if (signalHeatmap == null)
            {
                signalHeatmap = GetComponent<SignalHeatmap>();
            }
        }

        public void StartSimulation()
        {
            isSimulationRunning = true;
            OnSimulationStarted?.Invoke();
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

            transmitter.ClearAllConnections();
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

            foreach (var transmitter in transmitters)
            {
                transmitter?.DisconnectFromReceiver(receiver);
            }

            receivers.Remove(receiver);
            OnEquipmentCountChanged?.Invoke(transmitters.Count, receivers.Count);
        }

        public void ClearAllEquipment()
        {
            ClearAllConnections();

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
                transmitter?.ClearAllConnections();
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

        public void RecomputeAllSignalStrength()
        {
            var txs = transmitters?.ToArray();
            var rxs = receivers?.ToArray();
            if (txs == null || rxs == null) return;

            foreach (var tx in txs)
            {
                if (tx == null) continue;
                foreach (var rx in rxs)
                {
                    if (rx == null) continue;
                    float rssi = tx.CalculateSignalStrength(rx);
                    rx.UpdateSignalStrength(rssi);

                    if (tx.CanConnectTo(rx))
                        tx.ConnectToReceiver(rx);
                    else if (tx.IsConnectedTo(rx))
                        tx.DisconnectFromReceiver(rx);
                }
            }

            if (connectionManager != null)
                connectionManager.UpdateAllConnections();

            var heatmap = FindFirstObjectByType<SignalHeatmap>();
            if (heatmap != null && heatmap.enabledByUI)
                heatmap.UpdateHeatmap();
        }

        public void RecomputeForTransmitter(Transmitter tx)
        {
            if (tx == null || receivers == null) return;

            foreach (var rx in receivers)
            {
                if (rx == null) continue;
                float rssi = tx.CalculateSignalStrength(rx);
                rx.UpdateSignalStrength(rssi);

                if (tx.CanConnectTo(rx))
                    tx.ConnectToReceiver(rx);
                else if (tx.IsConnectedTo(rx))
                    tx.DisconnectFromReceiver(rx);
            }

            if (connectionManager != null)
                connectionManager.UpdateAllConnections();

            var heatmap = FindFirstObjectByType<SignalHeatmap>();
            if (heatmap != null && heatmap.enabledByUI)
                heatmap.UpdateHeatmap();
        }

        public void RecomputeForReceiver(Receiver rx)
        {
            if (rx == null || transmitters == null) return;

            foreach (var tx in transmitters)
            {
                if (tx == null) continue;
                float rssi = tx.CalculateSignalStrength(rx);
                rx.UpdateSignalStrength(rssi);

                if (tx.CanConnectTo(rx))
                    tx.ConnectToReceiver(rx);
                else if (tx.IsConnectedTo(rx))
                    tx.DisconnectFromReceiver(rx);
            }

            if (connectionManager != null)
                connectionManager.UpdateAllConnections();

            var heatmap = FindFirstObjectByType<SignalHeatmap>();
            if (heatmap != null && heatmap.enabledByUI)
                heatmap.UpdateHeatmap();
        }


        #endregion


    }
}