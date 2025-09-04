using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Connections
{
    // <summary>
    /// Distance-based strategy - connects to nearest transmitter above threshold
    /// Simple geometric approach for comparison
    /// </summary>
    public class NearestTransmitterStrategy : IConnectionStrategy
    {
        public string StrategyName => "Nearest Transmitter";
        public string Description => "Connects to nearest transmitter above signal threshold";

        public void UpdateConnections(List<Transmitter> transmitters, List<Receiver> receivers, ConnectionSettings settings)
        {
            foreach (var receiver in receivers)
            {
                if (receiver == null) continue;

                Transmitter nearestTx = null;
                float nearestDistance = float.PositiveInfinity;
                float bestSignal = float.NegativeInfinity;

                foreach (var transmitter in transmitters)
                {
                    if (transmitter == null) continue;

                    float distance = Vector3.Distance(transmitter.transform.position, receiver.transform.position);
                    float signal = transmitter.CalculateSignalStrength(receiver.transform.position);

                    // Apply handover margin based on distance instead of signal
                    float effectiveDistance = distance;
                    if (receiver.GetConnectedTransmitter() == transmitter)
                    {
                        effectiveDistance -= 50f; // 50m hysteresis distance
                    }

                    if (effectiveDistance < nearestDistance &&
                        signal > settings.minimumSignalThreshold)
                    {
                        nearestDistance = distance;
                        nearestTx = transmitter;
                        bestSignal = signal;
                    }
                }

                // Update receiver connection
                receiver.SetConnectedTransmitter(nearestTx);
                receiver.UpdateSignalStrength(bestSignal);
                receiver.UpdateSINR(bestSignal > float.NegativeInfinity ? 15f : float.NegativeInfinity); // Assume decent SINR

                // Clear connections to other transmitters
                foreach (var transmitter in transmitters)
                {
                    if (transmitter != nearestTx)
                    {
                        transmitter.ClearConnectionToReceiver(receiver);
                    }
                }

                if (settings.enableDebugLogs && nearestTx != null)
                {
                    Debug.Log($"[Nearest] {receiver.uniqueID} → {nearestTx.uniqueID}: " +
                             $"Distance={nearestDistance:F1}m, Signal={bestSignal:F1}dBm");
                }
            }
        }
    }
}