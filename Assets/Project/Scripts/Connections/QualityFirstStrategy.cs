using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Connections
{
    // <summary>
    /// Quality-first strategy - prioritizes signal quality over raw strength
    /// Focuses on SINR and connection stability
    /// </summary>
    public class QualityFirstStrategy : IConnectionStrategy
    {
        public string StrategyName => "Quality First";
        public string Description => "Prioritizes signal quality (SINR) over raw signal strength";
        public StrategyType StrategyType => StrategyType.QualityFirst;

        public void UpdateConnections(List<Transmitter> transmitters, List<Receiver> receivers, ConnectionSettings settings)
        {
            foreach (var receiver in receivers)
            {
                if (receiver == null) continue;

                Transmitter bestTx = null;
                float bestSINR = float.NegativeInfinity;
                float bestSignal = float.NegativeInfinity;

                // Calculate SINR for each potential serving transmitter
                foreach (var candidateTx in transmitters)
                {
                    if (candidateTx == null) continue;

                    float signal = candidateTx.CalculateSignalStrength(receiver.transform.position);

                    if (signal > settings.minimumSignalThreshold)
                    {
                        // Calculate interference from all other transmitters
                        float interferenceLevel = 0f;
                        foreach (var interferer in transmitters)
                        {
                            if (interferer != candidateTx && interferer != null)
                            {
                                float interferenceSignal = interferer.CalculateSignalStrength(receiver.transform.position);
                                if (interferenceSignal > settings.minimumSignalThreshold)
                                {
                                    interferenceLevel += Mathf.Pow(10f, interferenceSignal / 10f);
                                }
                            }
                        }

                        float interferenceDbm = interferenceLevel > 0 ?
                            10f * Mathf.Log10(interferenceLevel) : settings.minimumSignalThreshold;

                        float sinr = signal - interferenceDbm;

                        // Apply handover margin to current serving cell
                        if (receiver.GetConnectedTransmitter() == candidateTx)
                        {
                            sinr += settings.handoverMargin;
                        }

                        // Quality first: prioritize SINR, then signal strength
                        if (sinr > settings.minimumSINR &&
                            (sinr > bestSINR || (sinr == bestSINR && signal > bestSignal)))
                        {
                            bestSINR = sinr;
                            bestSignal = signal;
                            bestTx = candidateTx;
                        }
                    }
                }

                // Update receiver connection
                receiver.SetConnectedTransmitter(bestTx);
                receiver.UpdateSignalStrength(bestSignal);
                receiver.UpdateSINR(bestSINR);

                // Clear connections to other transmitters
                foreach (var transmitter in transmitters)
                {
                    if (transmitter != bestTx)
                    {
                        transmitter.ClearConnectionToReceiver(receiver);
                    }
                }

                if (settings.enableDebugLogs)
                {
                    if (bestTx != null)
                    {
                        Debug.Log($"[QualityFirst] {receiver.uniqueID} → {bestTx.uniqueID}: " +
                                 $"Signal={bestSignal:F1}dBm, SINR={bestSINR:F1}dB");
                    }
                    else
                    {
                        Debug.Log($"[QualityFirst] {receiver.uniqueID}: No suitable transmitter (quality requirements not met)");
                    }
                }
            }
        }
    }
}