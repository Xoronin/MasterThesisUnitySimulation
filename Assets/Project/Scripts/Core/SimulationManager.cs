// Main controller managing the entire simulation
//-Initialize simulation components
//- Coordinate propagation calculations
//- Handle real-time updates
//- Manage transmitter/receiver arrays

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

namespace RadioSignalSimulation.Core
{
    public class SimulationManager : MonoBehaviour
    {

        public static SimulationManager Instance { get; private set; }
        public List<Transmitter> transmiters = new List<Transmitter>();
        public List<Receiver> receivers = new List<Receiver>();

        public bool isSimulationRunning = false;
        public float updateInterval = 0.1f; // Time interval for updates in seconds
        private float lastUpdateTime = 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSimulation();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (isSimulationRunning)
            {
                if (Time.time - lastUpdateTime >= updateInterval)
                {
                    UpdateSimulation();
                    lastUpdateTime = Time.time;
                }
            }
        }

        public void InitializeSimulation()
        {
            Debug.Log("Simulation initialized.");
            StartSimulation();
        }

        public void UpdateSimulation()
        {
            // Handle real-time updates here
            foreach (var receiver in receivers)
            {
                float totalSignalStrength = 0f;

                // Update each receiver's state, e.g., calculate signal strength from each transmitter
                foreach (var transmitter in transmiters)
                {
                    float signalFromCurrentTransmitter = transmitter.CalculateSignalStrength(receiver.transform.position);
                    totalSignalStrength += Mathf.Pow(10f, signalFromCurrentTransmitter / 10f); // Convert from dB to linear, sum
                }

                // Convert total signal strength back to dBm
                float totalSignalStrength_Dbm = 10f * Mathf.Log10(totalSignalStrength);

                // Update receiver's signal strength
                receiver.UpdateSignalStrength(totalSignalStrength_Dbm);
            }
        }

        public void ValidateSimulation()
        {
            // Check if all components are functioning correctly
            // e.g., Ensure all transmitters and receivers are properly initialized
            Debug.Log("Simulation validated.");
        }

        public void StartSimulation()
        {
            isSimulationRunning = true;
            Debug.Log("Simulation started.");
        }

        public void StopSimulation()
        {
            isSimulationRunning = false;
            Debug.Log("Simulation stopped.");
        }

        public void PauseSimulation()
        {
            Debug.Log("Simulation paused.");
        }

        public void ResumeSimulation()
        {
            Debug.Log("Simulation resumed.");
        }

        public void RegisterTransmitter(Transmitter transmitter)
        {
            if (!transmiters.Contains(transmitter))
            {
                transmiters.Add(transmitter);
                Debug.Log($"Transmitter registered at {transmitter.position}. Total: {transmiters.Count}");
            }
        }

        public void AddTransmitter(Transmitter transmitter)
        {

        }

        public void RemoveTransmitter(Transmitter transmitter)
        {
        }

        public void RegisterReceiver(Receiver receiver)
        {
            if (!receivers.Contains(receiver))
            {
                receivers.Add(receiver);
                Debug.Log($"Receiver registered at {receiver.position}. Total: {receivers.Count}");
            }
        }

        public void AddReceiver(Receiver receiver)
        {
        }

        public void RemoveReceiver(Receiver receiver)
        {
        }

        public void GetSignalStrength(Vector3 position)
        {
            // Calculate signal strength at the given position
            // This would typically involve checking distances to transmitters and applying propagation models
        }

    }
}
