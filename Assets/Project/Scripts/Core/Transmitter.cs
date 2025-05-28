// Individual transmitter properties and behavior
//-Position and orientation
//- Power output and frequency
//- Antenna characteristics
//- Coverage area management

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

namespace RadioSignalSimulation.Core
{
    public class Transmitter : MonoBehaviour
    {
        // Transmitter Properties
        // TODO: Find out proper values for these properties 
        public float powerOutput = 20f;     // dBm
        public float frequency = 2400f;     // MHz (2.4 GHz WiFi)
        public float coverageRadius = 100f; // meters
        public float antennaGain = 1.0f;    // Gain factor for the antenna
        public Vector3 position;

        private void Start()
        {
            position = transform.position;
            InitializeTransmitter();
            SimulationManager.Instance.RegisterTransmitter(this);
        }

        public void InitializeTransmitter()
        {
            Debug.Log("Transmitter initialized.");
        }

        public void Update()
        {
            // Handle real-time updates here
            // e.g., Update transmitter state, manage coverage area, etc.
        }

        public void ValidateTransmitter()
        {
            Debug.Log("Transmitter validated.");
        }

        // Calculate signal strength at a given receiver position
        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            // If the receiver is within coverage radius calculate signal strength
            if (IsReceiverInRange(receiverPosition))
            {
                // Calculate signal strength 
                float d = Vector3.Distance(transform.position, receiverPosition);
                //float signalStrength = CalculateFriisFreeSpaceEquation(powerOutput, d);
                float signalStrength = CalculateReceivedPowerFSPL(d);
                Debug.Log($"Signal strength at receiver: {signalStrength} dBm");
                return signalStrength;
            }
            else
            {
                Debug.Log("Receiver is out of coverage area.");
                return float.NegativeInfinity; // Indicate no signal
            }
        }

        // Check if the receiver is within the coverage radius
        public bool IsReceiverInRange(Vector3 receiverPosition)
        {
            float distance = Vector3.Distance(transform.position, receiverPosition);
            bool inRange = distance <= coverageRadius;

            Debug.Log($"Distance check: {distance:F2}m <= {coverageRadius}m = {inRange}");

            return inRange;
        }

        // Calculate the free space path loss using the Friis transmission equation
        // Friis equation: Pr = (Pt * Gt * Gr * λ²) / ((4π)² * d² * L)
        // Pr = received power (dBm)
        // Pt = transmitted power (dBm)
        // Gt = gain of the transmitting antenna (linear scale)
        // Gr = gain of the receiving antenna (linear scale)
        // λ = wavelength (meters)
        // d = distance between transmitter and receiver (meters)
        // L = system losses (linear scale, typically 1 for free space)
        public float CalculateFriisFreeSpaceEquation(float distance)
        {
            // Pt: Convert transmitter power from dBm to watts
            // Pt(watts) = 10^((Pt(dBm) - 30) / 10)
            float Pt = Mathf.Pow(10f, (powerOutput - 30f) / 10f); // Transmitted power in watts

            // Gt: Gain of the transmitting antenna (isotropic antenna, linear scale)
            float Gt = antennaGain;

            // Gr: Gain of the receiving antenna (isotropic antenna, linear scale)
            float Gr = 1f;

            // L: System losses (linear scale, typically 1 for free space)
            float L = 1f;

            // d: Distance between transmitter and receiver in meters
            float d = distance;

            // λ: Calculate wavelength in meters
            float λ = CalculateWaveLength();

            // Gt and Gr: Assuming isotropic antennas, gain is 1 (linear scale)
            // G = 4π * Ae / λ²
            // Ae = the effective aperture of the antenna
            // Calculate the free space path loss

            // Pr: Apply Friis equation
            // Pr = (Pt * Gt * Gr * λ²) / ((4π)² * d² * L)
            float numerator = Pt * Gt * Gr * Mathf.Pow(λ, 2f);
            float denominator = Mathf.Pow(4f * Mathf.PI, 2f) * d * d * L;
            float Pr = numerator / denominator; // Received power in watts

            // Convert received power from watts to dBm
            // Pr(dBm) = 10 * log10(Pr(watts)) * 1000
            float Pr_dBm = 10f * Mathf.Log10(Pr * 1000f); // Received power in dBm

            return Pr_dBm; // Return the transmitter power in dBm
        }

        // Calculate the wavelength based on the frequency
        // Formula: λ = c / f = 2π * c / Wc
        // where:
        // λ = wavelength in meters 
        // c = speed of light in meters per second (approximately 299,792,458 m/s)
        // f = carrier frequency in Hz
        // Wc = carrier frequency in radians per second
        public float CalculateWaveLength()
        {
            float c = 3e8f;                             // Speed of light in m/s
            float f = frequency * 1e6f;     // Convert MHz to Hz
            float λ = c / f;                            // Wavelength in m

            Debug.Log($"Wavelength: {λ} meters");

            return λ;
        }

        // Calculate the free space path loss (FSPL) in dB
        // FSPL = 20 * log10(d) + 20 * log10(f) + 20 * log10(4π/c)
        // where:
        // d = distance in meters
        // f = frequency in Hz
        // c = speed of light in m/s (approximately 3e8 m/s)
        public float CalculateSimpleFSPL(float distance)
        {

            float d = distance / 1000f;     // Convert m to km
            float f = frequency;            // Frequency in MHz
            float c = 3e8f;                 // Speed of light
            float fspl = 20f * Mathf.Log10(d) + 20f * Mathf.Log10(f) + 32.45f;

            Debug.Log($"Distance: {distance}m ({d}km), FSPL: {fspl:F1}dB");

            // Return path loss in dB
            return fspl;
        }

        public float CalculateReceivedPowerFSPL(float distance)
        {
            float pathLoss = CalculateSimpleFSPL(distance);     // Free space path loss in dB
            float receivedPower = powerOutput - pathLoss;       // Received power in dBm

            Debug.Log($"Distance: {distance}m, FSPL: {pathLoss}dB, Received: {receivedPower}dBm");

            return receivedPower;
        }
    }
}

