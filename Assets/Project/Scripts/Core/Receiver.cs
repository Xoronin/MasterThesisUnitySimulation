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

        public void ValidateReceiver()
        {
            Debug.Log("Receiver validated.");
        }
    }
}