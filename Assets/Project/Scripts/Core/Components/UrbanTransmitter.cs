using UnityEngine;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;
using RFSimulation.Core.Components;
using System.Collections.Generic;

namespace RFSimulation.Core.Components
{
    /// <summary>
    /// Integration component to upgrade existing Transmitters with urban ray tracing capabilities
    /// Add this to your transmitter prefab or existing transmitters to enable Mapbox-aware propagation
    /// </summary>
    [System.Serializable]
    public class UrbanTransmitterSettings
    {
        [Header("Urban Ray Tracing")]
        public bool enableUrbanRayTracing = true;
        public bool autoDetectUrbanEnvironment = true;
        public PropagationModel urbanModel = PropagationModel.BasicRayTracing;
        public PropagationModel fallbackModel = PropagationModel.LogDistance;

        [Header("Urban Performance")]
        public bool usePerformanceOptimizations = true;
        public int maxReflections = 2; // Reduced for real-time performance
        public int maxDiffractions = 2;
        public float maxUrbanCalculationDistance = 1500f;

        [Header("Mapbox Integration")]
        public LayerMask mapboxBuildingLayer = 1 << 8;
        public bool enableBuildingMaterialDetection = true;
        public float buildingDetectionRadius = 1000f;
    }

    /// <summary>
    /// Enhanced Transmitter component with urban ray tracing support
    /// Either replace your existing Transmitter or add this as a component alongside it
    /// </summary>
    public class UrbanTransmitter : MonoBehaviour
    {
        [Header("Basic Transmitter Properties")]
        public string uniqueID;
        public float transmitterPower = 40f; // dBm
        public float antennaGain = 12f; // dBi
        public float frequency = 2400f; // MHz

        [Header("Urban Settings")]
        public UrbanTransmitterSettings urbanSettings = new UrbanTransmitterSettings();

        [Header("Visualization")]
        public bool showConnections = true;
        public bool showUrbanRayPaths = false; // Debug visualization
        public Material connectionLineMaterial;

        // Enhanced path loss calculator with urban support
        private UrbanPathLossCalculator urbanPathLossCalculator;
        private bool isUrbanEnvironmentDetected = false;
        private float lastUrbanDetectionTime = 0f;
        private const float URBAN_DETECTION_INTERVAL = 5f; // Check every 5 seconds

        void Awake()
        {
            InitializeUrbanTransmitter();
        }

        void Start()
        {
            InitializeUrbanCalculator();
            DetectUrbanEnvironment();
        }

        private void InitializeUrbanTransmitter()
        {
            if (string.IsNullOrEmpty(uniqueID))
                uniqueID = "UTX_" + GetInstanceID();

            // Ensure collider for interaction
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 1f;
            }
        }

        private void InitializeUrbanCalculator()
        {
            // Initialize the urban-enhanced path loss calculator
            urbanPathLossCalculator = new UrbanPathLossCalculator();

            // Configure urban settings
            urbanPathLossCalculator.autoDetectUrbanEnvironment = urbanSettings.autoDetectUrbanEnvironment;
            urbanPathLossCalculator.urbanDetectionRadius = urbanSettings.buildingDetectionRadius;
            urbanPathLossCalculator.mapboxBuildingLayer = urbanSettings.mapboxBuildingLayer;
            urbanPathLossCalculator.preferUrbanRayTracing = urbanSettings.enableUrbanRayTracing;
            urbanPathLossCalculator.maxUrbanDistance = urbanSettings.maxUrbanCalculationDistance;

            Debug.Log($"[UrbanTransmitter] {uniqueID} initialized with urban ray tracing");
        }

        public float CalculateSignalStrength(Vector3 receiverPosition)
        {
            // Periodic urban environment detection
            if (Time.time - lastUrbanDetectionTime > URBAN_DETECTION_INTERVAL)
            {
                DetectUrbanEnvironment();
                lastUrbanDetectionTime = Time.time;
            }

            // Create propagation context
            var context = CreatePropagationContext(receiverPosition);

            // Select appropriate model based on environment and settings
            context.Model = SelectOptimalModel(context);

            try
            {
                // Calculate using urban-enhanced calculator
                float receivedPower = urbanPathLossCalculator.CalculateReceivedPower(context);

                if (urbanSettings.enableUrbanRayTracing && showUrbanRayPaths)
                {
                    VisualizeUrbanRayPath(transform.position, receiverPosition, receivedPower);
                }

                return receivedPower;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UrbanTransmitter] Error calculating signal strength: {e.Message}");
                return float.NegativeInfinity;
            }
        }

        private PropagationContext CreatePropagationContext(Vector3 receiverPosition)
        {
            var context = PropagationContext.Create(
                transform.position,
                receiverPosition,
                transmitterPower,
                frequency
            );

            context.AntennaGainDbi = antennaGain;
            context.ReceiverSensitivityDbm = -105f; // Default receiver sensitivity

            // Add Mapbox building layer information
            context.BuildingLayers = urbanSettings.mapboxBuildingLayer;

            return context;
        }

        private PropagationModel SelectOptimalModel(PropagationContext context)
        {
            float distance = context.Distance;

            // If urban ray tracing is disabled, use fallback
            if (!urbanSettings.enableUrbanRayTracing)
            {
                return urbanSettings.fallbackModel;
            }

            // Performance optimization: use simpler models for very long distances
            if (distance > urbanSettings.maxUrbanCalculationDistance)
            {
                Debug.Log($"[UrbanTransmitter] Distance {distance:F0}m exceeds urban limit, using fallback model");
                return urbanSettings.fallbackModel;
            }

            // In detected urban environments, prefer ray tracing
            if (isUrbanEnvironmentDetected)
            {
                return urbanSettings.urbanModel;
            }

            // Auto-selection for non-urban or unknown environments
            return PropagationModel.Auto;
        }

        private void DetectUrbanEnvironment()
        {
            if (!urbanSettings.autoDetectUrbanEnvironment) return;

            // Count nearby Mapbox buildings
            Collider[] nearbyBuildings = Physics.OverlapSphere(
                transform.position,
                urbanSettings.buildingDetectionRadius,
                urbanSettings.mapboxBuildingLayer
            );

            int validBuildings = 0;
            foreach (var building in nearbyBuildings)
            {
                // Validate building height
                Renderer renderer = building.GetComponent<Renderer>();
                if (renderer != null && renderer.bounds.size.y > 2f)
                {
                    validBuildings++;
                }
            }

            bool wasUrban = isUrbanEnvironmentDetected;
            isUrbanEnvironmentDetected = validBuildings >= 5; // Minimum threshold

            if (wasUrban != isUrbanEnvironmentDetected)
            {
                Debug.Log($"[UrbanTransmitter] {uniqueID} environment changed: " +
                         $"Urban={isUrbanEnvironmentDetected} ({validBuildings} buildings detected)");
            }
        }

        private void VisualizeUrbanRayPath(Vector3 start, Vector3 end, float signalStrength)
        {
            // Simple line visualization for debugging
            Debug.DrawLine(start, end, GetSignalStrengthColor(signalStrength), 2f);
        }

        private Color GetSignalStrengthColor(float signalStrength)
        {
            if (float.IsNegativeInfinity(signalStrength)) return Color.black;

            // Normalize signal strength for color mapping
            float normalized = Mathf.Clamp01((signalStrength + 120f) / 40f); // -120 to -80 dBm range

            if (normalized > 0.8f) return Color.green;
            if (normalized > 0.6f) return Color.yellow;
            if (normalized > 0.4f) return Color.blue;
            return Color.red;
        }

        // Enhanced coverage calculation
        public float EstimateCoverageRadius()
        {
            var baseContext = CreatePropagationContext(transform.position + Vector3.forward);
            return urbanPathLossCalculator.EstimateCoverageRadius(baseContext);
        }

        // Compatibility methods for existing Transmitter interface
        public bool CanConnectTo(RFSimulation.Core.Components.Receiver receiver)
        {
            float signalStrength = CalculateSignalStrength(receiver.transform.position);
            return signalStrength >= (receiver.sensitivity + receiver.connectionMargin);
        }

        public void ConnectToReceiver(RFSimulation.Core.Components.Receiver receiver)
        {
            // Implementation depends on your existing connection system
            // This method maintains compatibility with existing code
        }

        public void DisconnectFromReceiver(RFSimulation.Core.Components.Receiver receiver)
        {
            // Implementation depends on your existing connection system
        }

        public void ClearAllConnections()
        {
            // Implementation depends on your existing connection system
        }

        public List<RFSimulation.Core.Components.Receiver> GetConnectedReceivers()
        {
            // Return connected receivers - implement based on your existing system
            return new List<RFSimulation.Core.Components.Receiver>();
        }

        public int GetConnectionCount()
        {
            return GetConnectedReceivers().Count;
        }

        // Configuration methods
        public void SetUrbanRayTracingEnabled(bool enabled)
        {
            urbanSettings.enableUrbanRayTracing = enabled;
            Debug.Log($"[UrbanTransmitter] {uniqueID} urban ray tracing: {enabled}");
        }

        public void SetMaxUrbanDistance(float maxDistance)
        {
            urbanSettings.maxUrbanCalculationDistance = maxDistance;
        }

        public void UpdateUrbanSettings(UrbanTransmitterSettings newSettings)
        {
            urbanSettings = newSettings;
            InitializeUrbanCalculator(); // Reinitialize with new settings
        }

        // Debug and testing methods
        [ContextMenu("Test Urban Signal Calculation")]
        public void TestUrbanSignalCalculation()
        {
            // Find a nearby receiver for testing
            var receivers = FindObjectsByType<RFSimulation.Core.Components.Receiver>(FindObjectsSortMode.None);
            if (receivers.Length > 0)
            {
                var testReceiver = receivers[0];
                float signal = CalculateSignalStrength(testReceiver.transform.position);
                float distance = Vector3.Distance(transform.position, testReceiver.transform.position);

                Debug.Log($"[UrbanTransmitter] Test calculation:");
                Debug.Log($"  Distance: {distance:F1}m");
                Debug.Log($"  Signal: {signal:F1}dBm");
                Debug.Log($"  Urban environment: {isUrbanEnvironmentDetected}");
                Debug.Log($"  Model used: {SelectOptimalModel(CreatePropagationContext(testReceiver.transform.position))}");
            }
            else
            {
                Debug.LogWarning("[UrbanTransmitter] No receivers found for testing");
            }
        }

        [ContextMenu("Force Urban Environment Detection")]
        public void ForceUrbanDetection()
        {
            DetectUrbanEnvironment();
            Debug.Log($"[UrbanTransmitter] Forced detection result: Urban={isUrbanEnvironmentDetected}");
        }

        [ContextMenu("Debug Urban Settings")]
        public void DebugUrbanSettings()
        {
            Debug.Log("=== URBAN TRANSMITTER DEBUG ===");
            Debug.Log($"Transmitter ID: {uniqueID}");
            Debug.Log($"Power: {transmitterPower}dBm, Gain: {antennaGain}dBi, Freq: {frequency}MHz");
            Debug.Log($"Urban ray tracing enabled: {urbanSettings.enableUrbanRayTracing}");
            Debug.Log($"Auto-detect urban: {urbanSettings.autoDetectUrbanEnvironment}");
            Debug.Log($"Current environment: {(isUrbanEnvironmentDetected ? "Urban" : "Non-urban")}");
            Debug.Log($"Max urban distance: {urbanSettings.maxUrbanCalculationDistance}m");
            Debug.Log($"Building layer: {urbanSettings.mapboxBuildingLayer.value}");

            if (urbanPathLossCalculator != null)
            {
                Debug.Log("Urban calculator initialized: ✓");
            }
            else
            {
                Debug.LogWarning("Urban calculator not initialized!");
            }
        }

        // Cleanup
        void OnDestroy()
        {
            ClearAllConnections();
        }
    }
}