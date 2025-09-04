using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Connections
{

    // <summary>
    /// Emergency/Coverage strategy - connects to any available signal
    /// Relaxed thresholds for emergency scenarios or coverage maximization
    /// </summary>
    public class EmergencyCoverageStrategy : IConnectionStrategy
    {
        public string StrategyName => "Emergency Coverage";
        public string Description => "Connects to any available signal with relaxed quality requirements";

        public void UpdateConnections(List<Transmitter> transmitters, List<Receiver> receivers, ConnectionSettings settings)
        {
            foreach (var receiver in receivers)
            {
                if (receiver == null) continue;

                Transmitter bestTx = null;
                float bestSignal = float.NegativeInfinity;

                // Relaxed thresholds for emergency scenarios
                float emergencyThreshold = settings.minimumSignalThreshold - 10f; // 10dB more sensitive
                float emergencyMargin = settings.handoverMargin * 2f; // Increased hysteresis

                foreach (var transmitter in transmitters)
                {
                    if (transmitter == null) continue;

                    float signal = transmitter.CalculateSignalStrength(receiver.transform.position);

                    // Apply increased handover margin for stability
                    float effectiveSignal = signal;
                    if (receiver.GetConnectedTransmitter() == transmitter)
                    {
                        effectiveSignal += emergencyMargin;
                    }

                    if (effectiveSignal > bestSignal && signal > emergencyThreshold)
                    {
                        bestSignal = signal;
                        bestTx = transmitter;
                    }
                }

                // Update receiver connection
                receiver.SetConnectedTransmitter(bestTx);
                receiver.UpdateSignalStrength(bestSignal);
                receiver.UpdateSINR(bestSignal > float.NegativeInfinity ? 0f : float.NegativeInfinity); // Assume poor but usable SINR

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
                    Debug.Log($"[Emergency] {receiver.uniqueID} → {bestTx.uniqueID}: {bestSignal:F1}dBm (emergency mode)");
                }
            }
        }
    }
}
