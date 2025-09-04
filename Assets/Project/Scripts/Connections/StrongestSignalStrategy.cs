using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Connections
{
    /// <summary>
    /// Simple strongest signal strategy - connects to highest signal strength only
    /// No interference consideration - baseline comparison strategy
    /// </summary>
    public class StrongestSignalStrategy : IConnectionStrategy
    {
        public string StrategyName => "Strongest Signal";
        public string Description => "Connects to transmitter with highest signal strength (no interference calculation)";

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
                receiver.SetConnectedTransmitter(bestTx);
                receiver.UpdateSignalStrength(bestSignal);
                receiver.UpdateSINR(bestSignal > float.NegativeInfinity ? 20f : float.NegativeInfinity); // Assume good SINR

                // Clear connections to other transmitters
                foreach (var transmitter in transmitters)
                {
                    if (transmitter != bestTx)
                    {
                        transmitter.ClearConnectionToReceiver(receiver);
                    }
                }

                if (settings.enableDebugLogs && bestTx != null)
                {
                    Debug.Log($"[StrongestSignal] {receiver.uniqueID} → {bestTx.uniqueID}: {bestSignal:F1}dBm");
                }
            }
        }
    }
}