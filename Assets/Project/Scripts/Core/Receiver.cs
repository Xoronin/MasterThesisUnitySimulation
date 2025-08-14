// Individual receiver for signal measurements
// -Position and sensitivity
// - Signal strength recording
// - Quality metrics calculation

using UnityEngine;

namespace RadioSignalSimulation.Core
    {
        public class Receiver : MonoBehaviour
        {
            // Static counter for unique naming
            private static int receiverCount = 0;
            public string uniqueID;

            // Receiver Properties
            public Vector3 position;
            public float sensitivity = -90f; // dBm, minimum detectable signal strength
            public float signalStrength; // Current signal strength in dBm
            public float qualityMetric; // e.g., Signal-to-Noise Ratio (SNR) or Bit Error Rate (BER)

            public bool enableVisualUpdates = true;

            private void Start()
            {
                position = transform.position;
                AssignUniqueID();
                InitializeReceiver();
                SimulationManager.Instance.RegisterReceiver(this);
            }

            private void AssignUniqueID()
            {
                receiverCount++;
                uniqueID = $"Receiver{receiverCount}";
                gameObject.name = uniqueID;
                Debug.Log($"{uniqueID} created at position {transform.position}");
            }

            public void InitializeReceiver()
            {
                Debug.Log($"{uniqueID} initialized with sensitivity {sensitivity}dBm");
            }

            public void Update()
            {
                // Handle real-time updates here
                // e.g., Update signal strength, calculate quality metrics, etc.
            }

            public void UpdateSignalStrength(float newSignalStrength)
            {
                signalStrength = newSignalStrength;
                position = transform.position;
                Debug.Log($"{uniqueID} at ({position.x:F2}, {position.y:F2}, {position.z:F2}) → Signal: {newSignalStrength:F1} dBm");

                // Signal to Noise Ratio (SNR) calculation
                float noiseFloor = -100f;
                qualityMetric = signalStrength - noiseFloor;
                Debug.Log($"{uniqueID} → SNR: {qualityMetric:F1} dB");

                ChangeReceiverColor();
            }

            // Change the color of the receiver based on signal strength
            private void ChangeReceiverColor()
            {
                if (!enableVisualUpdates) return; // Skip if visual updates are disabled

                Debug.Log($"{uniqueID} → ChangeReceiverColor() called!");

                Renderer renderer = GetComponent<Renderer>();

                if (renderer == null)
                {
                    renderer = GetComponentInChildren<Renderer>();
                }

                if (renderer != null)
                {
                    Debug.Log($"{uniqueID} → Signal: {signalStrength:F1} dBm, Sensitivity: {sensitivity} dBm");
                    Color oldColor = renderer.material.color;

                    if (signalStrength > sensitivity)
                    {
                        renderer.material.color = Color.green; // Good signal
                        Debug.Log($"{uniqueID} → Setting GREEN: {signalStrength:F1} > {sensitivity} = TRUE");
                    }
                    else if (signalStrength > sensitivity - 10f)
                    {
                        renderer.material.color = Color.yellow; // Weak signal
                        Debug.Log($"{uniqueID} → Setting YELLOW: {signalStrength:F1} > {sensitivity - 10f}");
                    }
                    else
                    {
                        renderer.material.color = Color.red; // No signal
                        Debug.Log($"{uniqueID} → Setting RED: signal too weak");
                    }
                }
                else
                {
                    Debug.LogError($"{uniqueID} → NO RENDERER FOUND! Check if cube has MeshRenderer component.");
                }
            }

            public void ValidateReceiver()
            {
                Debug.Log($"{uniqueID} validated.");

            }

            public static void ResetCounter()
            {
                receiverCount = 0;
            }

        }

}