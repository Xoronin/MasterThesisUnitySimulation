// Individual transmitter properties and behavior
//-Position and orientation
//- Power output and frequency
//- Antenna characteristics
//- Coverage area management

using System.Collections.Generic;
using UnityEngine;
using RadioSignalSimulation.Propagation;

namespace RadioSignalSimulation.Core
{
    public class Transmitter : MonoBehaviour
    {
        // Static counter for unique naming
        private static int transmitterCount = 0;
        public string uniqueID;

        // Transmitter Properties
        // TODO: Find out proper values for these properties 
        [Header("Transmitter Properties")]
        public float transmitterPower = 5f;         // dBm, output power of the transmitter
        public float frequency = 2400f;         // MHz, carrier frequency of the transmitter
        public float coverageRadius = 1000f;    // m, coverage radius of the transmitter
        public float antennaGain = 1.0f;        // Linear scale, gain of the transmitting antenna (isotropic antenna = 1.0)
        public Vector3 position;

        [Header("Visualization")]
        public bool showConnections = true;
        public Material connectionLineMaterial;

        private Dictionary<Receiver, LineRenderer> connectionLines = new Dictionary<Receiver, LineRenderer>();

        private void Start()
        {
            position = transform.position;
            AssignUniqueID();
            InitializeTransmitter();
            SimulationManager.Instance.RegisterTransmitter(this);
        }

        private void AssignUniqueID()
        {
            transmitterCount++;
            uniqueID = $"Transmitter{transmitterCount}";
            gameObject.name = uniqueID;
            Debug.Log($"{uniqueID} created at position {transform.position}");
        }

        public void InitializeTransmitter()
        {
            Debug.Log($"{uniqueID} initialized with {transmitterPower}dBm at {frequency}MHz");
        }

        public void Update()
        {
            // Handle real-time updates here
            // e.g., Update transmitter state, manage coverage area, etc.
        }

        public void ValidateTransmitter()
        {
            Debug.Log($"{uniqueID} validated.");
        }


        // Calculate signal strength at a given receiver position
        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            // If the receiver is within coverage radius calculate signal strength
            if (IsReceiverInRange(receiverPosition))
            {
                // Calculate signal strength 
                float d = Vector3.Distance(transform.position, receiverPosition);
                //float signalStrength = CalculateReceivedPowerFSPL(d);
                float signalStrength = PropagationModels.CalculatePathLossWithObstacles(
                    transform.position,
                    receiverPosition,
                    transmitterPower,
                    antennaGain,
                    frequency,
                    PropagationModels.PropagationModel.LogDistance, // Changed from FreeSpace
                    PropagationModels.EnvironmentType.Urban,        // Changed from FreeSpace
                    1 << 8 // Building layer mask
                );

                Debug.Log($"{uniqueID} → Signal strength at receiver: {signalStrength:F1} dBm (distance: {d:F1}m)");
                return signalStrength;
            }
            else
            {
                Debug.Log($"{uniqueID} → Receiver is out of coverage area.");
                return float.NegativeInfinity; // Indicate no signal
            }
        }

        // Check if the receiver is within the coverage radius
        public bool IsReceiverInRange(Vector3 receiverPosition)
        {
            float distance = Vector3.Distance(transform.position, receiverPosition);
            bool inRange = distance <= coverageRadius;

            Debug.Log($"{uniqueID} → Distance check: {distance:F2}m <= {coverageRadius}m = {inRange}");

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
            float Pt = Mathf.Pow(10f, (transmitterPower - 30f) / 10f); // Transmitted power in watts

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
            float f = frequency * 1e6f;                 // Convert MHz to Hz
            float λ = c / f;                            // Wavelength in m

            //Debug.Log($"Wavelength: {λ} meters");

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

            float d = distance / 1000f;     // Convert km
            float f = frequency;            // Frequency in MHz
            float c = 3e8f;                 // Speed of light
            float fspl = 20f * Mathf.Log10(d) + 20f * Mathf.Log10(f) + 32.44f;

            Debug.Log($"{uniqueID} → Distance: {distance}m, FSPL: {fspl:F1}dB");

            // Return path loss in dB
            return fspl;
        }

        public float CalculateReceivedPowerFSPL(float distance)
        {
            float pathLoss = CalculateSimpleFSPL(distance);     // Free space path loss in dB
            float receivedPower = transmitterPower - pathLoss;       // Received power in dBm

            Debug.Log($"{uniqueID} → Distance: {distance}m, FSPL: {pathLoss}dB, Received: {receivedPower}dBm");

            return receivedPower;
        }

        public void UpdateConnectionLines()
        {
            if (!showConnections) return;

            ClearAllLines();

            foreach (Receiver receiver in SimulationManager.Instance.receivers)
            {
                CreateConnectionLine(receiver);
            }
        }

        // Create a connection line to a receiver
        private void CreateConnectionLine(Receiver receiver)
        {
            // Only create a line if the receiver is within range
            if (!IsReceiverInRange(receiver.transform.position))
                return;

            // Create a new LineRenderer for the receiver
            GameObject lineObject = new GameObject($"ConnectionLine_{receiver.name}");
            //lineObject.transform.SetParent(transform);

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

            // Set line visuals
            lineRenderer.material = connectionLineMaterial;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;

            Transform cubeTransform = receiver.transform.GetChild(0);
            Vector3 cubePosition = cubeTransform != null ? cubeTransform.position : receiver.transform.position;

            // Set positions of the line renderer
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, cubePosition);

            // Set the line color based on the signal strength
            float signalStrength = CalculateSignalStrength(receiver.transform.position);
            Color lineColor = GetSignalColor(signalStrength, receiver.sensitivity);
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            connectionLines[receiver] = lineRenderer;

            Debug.Log($"{uniqueID} → Connection line created to {receiver.uniqueID} with color: {lineColor}");
        }

        // Get the color based on signal strength and sensitivity
        private Color GetSignalColor(float signalStrength, float sensitivity)
        {
            Debug.Log($"{uniqueID} → Signal: {signalStrength:F1}dBm, Sensitivity: {sensitivity}dBm");

            if (float.IsNegativeInfinity(signalStrength))
                return Color.clear;

            if (signalStrength > sensitivity)
                return Color.green; 
            else if (signalStrength > sensitivity - 10f)
                return Color.yellow; 
            else
                return Color.red; 
        }

        // Clear all existing connection lines
        public void ClearAllLines()
        {
            foreach (var line in connectionLines.Values)
            {
                if (line != null)
                    DestroyImmediate(line.gameObject);
            }
            connectionLines.Clear();
        }

        public static void ResetCounter()
        {
            transmitterCount = 0;
        }
    }
}

