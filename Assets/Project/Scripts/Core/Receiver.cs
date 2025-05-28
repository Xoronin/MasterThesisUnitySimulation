// Individual receiver for signal measurements
// -Position and sensitivity
// - Signal strength recording
// - Quality metrics calculation

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

namespace RadioSignalSimulation.Core
{

    public class Receiver : MonoBehaviour
    {
        // Receiver Properties
        public Vector3 position;
        public float sensitivity = -90f; // dBm, minimum detectable signal strength
        public float signalStrength; // Current signal strength in dBm
        public float qualityMetric; // e.g., Signal-to-Noise Ratio (SNR) or Bit Error Rate (BER)

        private void Start()
        {
            position = transform.position;
            InitializeReceiver();
            SimulationManager.Instance.RegisterReceiver(this);
        }

        public void InitializeReceiver()
        {
            Debug.Log("Receiver initialized.");
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
            Debug.Log($"Receiver at ({position.x:F2}, {position.y:F2}, {position.z:F2}) updated signal strength: {newSignalStrength} dBm");

            ChangeReceiverColor();
        }

        // Change the color of the receiver based on signal strength
        private void ChangeReceiverColor()
        {
            Debug.Log("ChangeReceiverColor() called!"); // Add this debug line

            Renderer renderer = GetComponent<Renderer>();
            Debug.Log($"Renderer on main object: {renderer != null}");

            if (renderer == null)
            {
                renderer = GetComponentInChildren<Renderer>();
                Debug.Log($"Renderer on child object: {renderer != null}");
            }

            if (renderer != null)
            {
                Debug.Log($"Signal: {signalStrength:F1} dBm, Sensitivity: {sensitivity} dBm");
                Color oldColor = renderer.material.color;

                if (signalStrength > sensitivity)
                {
                    renderer.material.color = Color.green; // Good signal
                    Debug.Log($"Setting GREEN: {signalStrength:F1} > {sensitivity} = TRUE");

                }
                else if (signalStrength > sensitivity - 10f)
                {
                    renderer.material.color = Color.yellow; // Weak signal
                    Debug.Log($"Setting YELLOW: {signalStrength:F1} > {sensitivity - 10f}");

                }
                else
                {
                    renderer.material.color = Color.red; // No signal
                    Debug.Log($"Setting RED: signal too weak");

                }

                Debug.Log($"Color changed from {oldColor} to {renderer.material.color}");
            }
            else
            {
                Debug.LogError("NO RENDERER FOUND! Check if cube has MeshRenderer component.");
            }
        }

        public void ValidateReceiver()
        {
            Debug.Log("Receiver validated.");
        }
    }
}