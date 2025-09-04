using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Propagation;
using RFSimulation.Visualization;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;

// Connection management component
namespace RFSimulation.Core
{
    public class TransmitterConnectionManager : MonoBehaviour
    {
        private Transmitter transmitter;
        private List<Receiver> connectedReceivers = new List<Receiver>();
        private ConnectionLineRenderer lineRenderer;

        [Header("Connection Settings")]
        public bool showConnections = true;
        public Color connectionColor = Color.yellow;
        public Material lineMaterial;
        public float lineWidth = 0.1f;

        public void Initialize(Transmitter parentTransmitter)
        {
            transmitter = parentTransmitter;
            SetupLineRenderer();
        }

        public void Cleanup()
        {
            ClearAllLines();
        }

        public void AddConnectionToReceiver(Receiver receiver)
        {
            if (receiver == null || connectedReceivers.Contains(receiver)) return;

            connectedReceivers.Add(receiver);
            CreateConnectionLine(receiver);
        }

        public void ClearConnectionToReceiver(Receiver receiver)
        {
            if (receiver == null || !connectedReceivers.Contains(receiver)) return;

            connectedReceivers.Remove(receiver);
            RemoveConnectionLine(receiver);
        }

        public void ClearAllLines()
        {
            lineRenderer?.ClearAllConnections();
            connectedReceivers.Clear();
        }

        public void UpdateConnectionLines()
        {
            if (!showConnections || lineRenderer == null) return;

            foreach (var receiver in connectedReceivers)
            {
                if (receiver != null)
                {
                    UpdateConnectionLine(receiver);
                }
            }
        }

        public List<Receiver> GetConnectedReceivers() => new List<Receiver>(connectedReceivers);
        public int GetConnectionCount() => connectedReceivers.Count;
        public bool IsConnectedTo(Receiver receiver) => connectedReceivers.Contains(receiver);

        private void SetupLineRenderer()
        {
            lineRenderer = GetComponent<ConnectionLineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<ConnectionLineRenderer>();
            }

            lineRenderer.defaultLineMaterial = lineMaterial;
            lineRenderer.lineWidth = lineWidth;
        }

        private void CreateConnectionLine(Receiver receiver)
        {
            if (!showConnections || lineRenderer == null || receiver == null) return;

            string connectionId = GetConnectionId(receiver);
            float signalStrength = transmitter.CalculateSignalStrength(receiver.transform.position);

            lineRenderer.CreateConnection(
                connectionId,
                transmitter.position,
                receiver.transform.position,
                signalStrength,
                receiver.sensitivity
            );
        }

        private void UpdateConnectionLine(Receiver receiver)
        {
            string connectionId = GetConnectionId(receiver);
            float signalStrength = transmitter.CalculateSignalStrength(receiver.transform.position);

            lineRenderer.UpdateConnection(
                connectionId,
                transmitter.position,
                receiver.transform.position,
                signalStrength,
                receiver.sensitivity
            );
        }

        private void RemoveConnectionLine(Receiver receiver)
        {
            if (lineRenderer == null || receiver == null) return;
            lineRenderer.RemoveConnection(GetConnectionId(receiver));
        }

        private string GetConnectionId(Receiver receiver)
        {
            return $"{transmitter.uniqueID}_to_{receiver.uniqueID}";
        }

        public void SetConnectionVisibility(bool visible)
        {
            showConnections = visible;
            lineRenderer?.SetLineVisibility(visible);
        }
    }
}