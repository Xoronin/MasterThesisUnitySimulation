using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Connections
{
    /// <summary>
    /// Load balanced strategy - distributes users across transmitters to prevent congestion
    /// Important for peak hour scenarios in urban environments
    /// </summary>
    public class LoadBalancedStrategy : IConnectionStrategy
    {
        public string StrategyName => "Load Balanced";
        public string Description => "Distributes users across transmitters to prevent congestion";
        public StrategyType StrategyType => StrategyType.LoadBalanced;

        public void UpdateConnections(List<Transmitter> transmitters, List<Receiver> receivers, ConnectionSettings settings)
        {
            foreach (var receiver in receivers)
            {
                if (receiver == null) continue;

                Transmitter bestTx = null;
                float bestMetric = float.NegativeInfinity;

                foreach (var transmitter in transmitters)
                {
                    if (transmitter == null) continue;

                    float signal = transmitter.CalculateSignalStrength(receiver.transform.position);

                    if (signal > settings.minimumSignalThreshold)
                    {
                        // Calculate load balancing metric
                        int currentLoad = transmitter.GetConnectionCount();
                        float loadPenalty = currentLoad * 5f; // 5dB penalty per connected user

                        // Apply handover margin to current serving cell
                        if (receiver.GetConnectedTransmitter() == transmitter)
                        {
                            signal += settings.handoverMargin;
                        }

                        float metric = signal - loadPenalty;

                        if (metric > bestMetric)
                        {
                            bestMetric = metric;
                            bestTx = transmitter;
                        }
                    }
                }

                // Update receiver connection
                if (bestTx != null)
                {
                    float actualSignal = bestTx.CalculateSignalStrength(receiver.transform.position);
                    receiver.SetConnectedTransmitter(bestTx);
                    receiver.UpdateSignalStrength(actualSignal);
                    receiver.UpdateSINR(actualSignal - 5f); // Assume some interference
                }
                else
                {
                    receiver.SetConnectedTransmitter(null);
                    receiver.UpdateSignalStrength(float.NegativeInfinity);
                    receiver.UpdateSINR(float.NegativeInfinity);
                }

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
                    Debug.Log($"[LoadBalanced] {receiver.uniqueID} → {bestTx.uniqueID}: " +
                             $"Signal={receiver.currentSignalStrength:F1}dBm, Load={bestTx.GetConnectionCount()}");
                }
            }
        }
    }
}