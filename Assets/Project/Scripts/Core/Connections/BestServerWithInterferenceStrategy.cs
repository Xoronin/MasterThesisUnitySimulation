using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Core;
using RFSimulation.Core.Components;

namespace RFSimulation.Core.Connections
{
    /// <summary>
    /// Best server with interference calculation
    /// Connects to best server while calculating interference from others
    /// </summary>
    public class BestServerWithInterferenceStrategy : IConnectionStrategy
    {
        public string StrategyName => "Best Server With Interference";
        public string Description => "Connects to the best server and applies interference with others";
        public StrategyType StrategyType => StrategyType.BestServerWithInterference;

        public void UpdateConnections(
            List<Transmitter> transmitters,
            List<Receiver> receivers,
            ConnectionSettings settings)
        {
            foreach (var receiver in receivers)
            {
                if (receiver == null) continue;

                float bestSignal = float.NegativeInfinity;
                Transmitter servingTransmitter = null;
                float interferenceLevel = 0f;

                // First pass: find best serving transmitter
                foreach (var transmitter in transmitters)
                {
                    if (transmitter == null) continue;

                    float signal = transmitter.CalculateSignalStrength(receiver.transform.position);

                    // Apply handover margin to prevent ping-ponging
                    float effectiveSignal = signal;
                    if (receiver.GetConnectedTransmitter() == transmitter)
                    {
                        effectiveSignal += settings.handoverMargin;
                    }

                    // Check if this is the best signal and meets requirements
                    if (effectiveSignal > bestSignal &&
                        signal > settings.minimumSignalThreshold &&
                        signal >= receiver.sensitivity + settings.connectionMargin)
                    {
                        bestSignal = signal; // Use actual signal, not biased
                        servingTransmitter = transmitter;
                    }
                }

                // Second pass: calculate interference from all other transmitters
                if (servingTransmitter != null)
                {
                    foreach (var transmitter in transmitters)
                    {
                        if (transmitter != servingTransmitter && transmitter != null)
                        {
                            float signal = transmitter.CalculateSignalStrength(receiver.transform.position);

                            // Only count interference if signal is above noise floor
                            if (signal > settings.minimumSignalThreshold)
                            {
                                // Convert to linear power and add
                                interferenceLevel += Mathf.Pow(10f, signal / 10f);
                            }
                        }
                    }

                    // Calculate SINR (Signal to Interference + Noise Ratio)
                    float interferenceDbm = interferenceLevel > 0 ?
                        10f * Mathf.Log10(interferenceLevel) : float.NegativeInfinity;

                    float sinr = float.IsNegativeInfinity(interferenceDbm) ?
                        bestSignal - settings.minimumSignalThreshold : // Use noise floor if no interference
                        bestSignal - interferenceDbm;

                    // Check if SINR meets requirements
                    bool sinrAcceptable = sinr >= settings.minimumSINR;

                    if (sinrAcceptable)
                    {
                        receiver.UpdateSignalStrength(bestSignal);
                        receiver.UpdateSINR(sinr);
                        servingTransmitter.ConnectToReceiver(receiver);
                    }
                    else
                    {
                        // SINR too poor - drop connection
                        receiver.UpdateSignalStrength(float.NegativeInfinity);
                        receiver.UpdateSINR(sinr);
                        receiver.SetConnectedTransmitter(null);
                        servingTransmitter = null;
                    }

                    // Debug logging
                    if (settings.enableDebugLogs)
                    {
                        Debug.Log($"[Interference] {receiver.uniqueID}: Signal={bestSignal:F1}dBm, " +
                                 $"Interference={interferenceDbm:F1}dBm, SINR={sinr:F1}dB, " +
                                 $"Connected={(sinrAcceptable ? servingTransmitter.uniqueID : "None")}");
                    }
                }
                else
                {
                    // No suitable transmitter found
                    receiver.UpdateSignalStrength(float.NegativeInfinity);
                    receiver.UpdateSINR(float.NegativeInfinity);
                    receiver.SetConnectedTransmitter(null);

                    if (settings.enableDebugLogs)
                    {
                        Debug.Log($"[Interference] {receiver.uniqueID}: No suitable transmitter found");
                    }
                }

                // Clear connections to all non-serving transmitters
                foreach (var transmitter in transmitters)
                {
                    if (transmitter != servingTransmitter)
                    {
                        transmitter.DisconnectFromReceiver(receiver);
                    }
                }
            }
        }
    }
}