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
        public float powerOutput = 20f; // dBm
        public float frequency = 2400f; // MHz (2.4 GHz WiFi)
        public float coverageRadius = 100f; // meters
        public float antennaGainPattern = 1.0f; // Gain factor for the antenna
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
    }
}