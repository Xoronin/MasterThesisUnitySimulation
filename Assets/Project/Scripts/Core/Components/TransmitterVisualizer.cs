using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Propagation;
using RFSimulation.Visualization;
using RFSimulation.Propagation.SignalQuality;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;

namespace RFSimulation.Core
{
    public class TransmitterVisualizer : MonoBehaviour
    {
        private Transmitter transmitter;
        private Renderer transmitterRenderer;
        private CoverageAreaRenderer coverageAreaRenderer;

        [Header("Transmitter Visual Settings")]
        public Color transmitterColor = Color.blue;

        [Header("Coverage Settings")]
        public bool showCoverageArea = false;
        public Color coverageColor = new Color(0f, 0f, 1f, 0.1f);
        public Material coverageMaterial;

        // Cache the coverage radius to ensure consistency
        private float cachedCoverageRadius = -1f;
        private bool radiusCalculated = false;

        public void Initialize(Transmitter parentTransmitter)
        {
            transmitter = parentTransmitter;
            SetupVisuals();
            CalculateAndCacheCoverage(); // Calculate once at startup
        }

        public void Cleanup()
        {
            if (coverageAreaRenderer != null)
            {
                coverageAreaRenderer.Cleanup();
            }
        }

        private void SetupVisuals()
        {
            transmitterRenderer = GetComponent<Renderer>();
            if (transmitterRenderer != null)
            {
                transmitterRenderer.material.color = transmitterColor;
            }

            SetupCoverageAreaRenderer();
        }

        private void SetupCoverageAreaRenderer()
        {
            coverageAreaRenderer = GetComponent<CoverageAreaRenderer>();
            if (coverageAreaRenderer == null)
            {
                coverageAreaRenderer = gameObject.AddComponent<CoverageAreaRenderer>();
            }

            coverageAreaRenderer.showCoverageArea = showCoverageArea;
            coverageAreaRenderer.coverageColor = coverageColor;

            // Pass the material to the renderer
            if (coverageMaterial != null)
            {
                coverageAreaRenderer.SetCustomMaterial(coverageMaterial);
            }

            coverageAreaRenderer.Initialize(transmitter);
        }

        // Calculate coverage once and cache it
        private void CalculateAndCacheCoverage()
        {
            if (transmitter == null)
            {
                cachedCoverageRadius = 100f;
                radiusCalculated = true;
                return;
            }

            var context = PropagationContext.Create(
                transmitter.position,
                transmitter.position + Vector3.forward,
                transmitter.transmitterPower,
                transmitter.frequency
            );

            context.AntennaGainDbi = transmitter.antennaGain;
            context.Model = (PropagationModel)transmitter.propagationModel;
            context.Environment = (EnvironmentType)transmitter.environmentType;
            context.ReceiverSensitivityDbm = -105f;

            var calculator = new PathLossCalculator();
            cachedCoverageRadius = calculator.EstimateCoverageRadius(context);
            radiusCalculated = true;

            Debug.Log($"Coverage calculated for {transmitter.uniqueID}: {cachedCoverageRadius:F0}m");
        }

        // Public method to get the consistent coverage radius
        public float GetCoverageRadius()
        {
            if (!radiusCalculated)
            {
                CalculateAndCacheCoverage();
            }
            return cachedCoverageRadius;
        }

        // Recalculate coverage when transmitter parameters change
        public void RecalculateCoverage()
        {
            radiusCalculated = false;
            cachedCoverageRadius = -1f;
            CalculateAndCacheCoverage();
            UpdateCoverageVisualization();
        }

        public void UpdateCoverageVisualization()
        {
            if (coverageAreaRenderer != null)
            {
                // Tell the renderer to use our calculated radius
                coverageAreaRenderer.SetCoverageRadius(GetCoverageRadius());
            }
        }

        public void ToggleCoverageArea(bool show)
        {
            showCoverageArea = show;

            if (coverageAreaRenderer != null)
            {
                coverageAreaRenderer.SetCoverageVisibility(show);

                // If showing for first time, make sure it's created
                if (show && !coverageAreaRenderer.IsCoverageVisible())
                {
                    coverageAreaRenderer.CreateCoverageArea();
                    coverageAreaRenderer.SetCoverageRadius(GetCoverageRadius());
                }
            }
        }

        // Other existing methods...
        public void ToggleConnections(bool show)
        {
            var connectionManager = GetComponent<TransmitterConnectionManager>();
            connectionManager?.SetConnectionVisibility(show);
        }

        public void SetCoverageColor(Color newColor)
        {
            coverageColor = newColor;
            coverageAreaRenderer?.SetCoverageColor(newColor);
        }

        // Information getters that use cached values
        public float GetCurrentCoverageRadiusMeters()
        {
            return GetCoverageRadius();
        }

        public bool IsCoverageVisible()
        {
            return coverageAreaRenderer?.IsCoverageVisible() ?? false;
        }
    }
}