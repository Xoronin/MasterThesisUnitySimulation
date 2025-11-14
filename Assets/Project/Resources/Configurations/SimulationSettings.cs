//using UnityEngine;
//using RFSimulation.Propagation;
//using RFSimulation.Propagation.Core;
//using RFSimulation.Core.Components;
//using RFSimulation.Core.Managers;

//namespace RFSimulation.Data
//{
//    /// <summary>
//    /// Global simulation settings and configuration
//    /// </summary>
//    [CreateAssetMenu(fileName = "SimulationSettings", menuName = "Radio Simulation/Simulation Settings")]
//    public class SimulationSettings : ScriptableObject
//    {
//        [Header("General Settings")]
//        [Tooltip("Auto-start simulation when scene loads")]
//        public bool autoStartSimulation = true;

//        [Tooltip("Update frequency in Hz")]
//        [Range(0.1f, 10f)]
//        public float updateFrequency = 1f;

//        [Tooltip("Enable debug logging")]
//        public bool enableDebugLogs = false;

//        [Header("Default Propagation")]
//        [Tooltip("Default propagation model for new transmitters")]
//        public PropagationModel defaultPropagationModel = PropagationModel.LogD;

//        [Header("Signal Thresholds")]
//        [Tooltip("Minimum signal threshold in dBm")]
//        [Range(-140f, -50f)]
//        public float minimumSignalThreshold = -110f;

//        [Tooltip("Connection margin above sensitivity in dB")]
//        [Range(0f, 20f)]
//        public float connectionMargin = 10f;

//        [Tooltip("Handover margin to prevent ping-ponging in dB")]
//        [Range(1f, 10f)]
//        public float handoverMargin = 3f;

//        [Tooltip("Minimum SINR for acceptable connection in dB")]
//        [Range(-20f, 10f)]
//        public float minimumSINR = -6f;

//        [Header("Technology Defaults")]
//        [Tooltip("Default transmitter power in dBm")]
//        [Range(0f, 60f)]
//        public float defaultTransmitterPower = 43f;

//        [Tooltip("Default antenna gain in dBi")]
//        [Range(0f, 30f)]
//        public float defaultAntennaGain = 18f;

//        [Tooltip("Default frequency in MHz")]
//        public float defaultFrequency = 3500f;

//        [Tooltip("Default receiver sensitivity in dBm")]
//        [Range(-150f, -50f)]
//        public float defaultReceiverSensitivity = -105f;

//        [Header("Visualization")]
//        [Tooltip("Show connection lines by default")]
//        public bool showConnectionsByDefault = true;

//        [Tooltip("Show coverage areas by default")]
//        public bool showCoverageByDefault = false;

//        [Tooltip("Default line width for connections")]
//        [Range(0.01f, 1f)]
//        public float defaultLineWidth = 0.1f;

//        [Tooltip("Animate connection lines")]
//        public bool animateConnections = true;

//        [Tooltip("Animation speed multiplier")]
//        [Range(0.1f, 5f)]
//        public float animationSpeed = 2f;

//        [Header("Performance")]
//        [Tooltip("Maximum number of connection lines")]
//        [Range(10, 1000)]
//        public int maxConnectionLines = 100;

//        [Tooltip("Enable level of detail for distant objects")]
//        public bool enableLOD = true;

//        [Tooltip("Distance threshold for LOD in meters")]
//        [Range(50f, 1000f)]
//        public float lodDistance = 200f;

//        [Tooltip("Enable connection caching for performance")]
//        public bool enableConnectionCaching = true;

//        [Header("Multi-Connection Settings")]
//        [Tooltip("Maximum simultaneous connections per receiver")]
//        [Range(1, 10)]
//        public int maxSimultaneousConnections = 3;

//        [Tooltip("Enable load balancing for multi-connect")]
//        public bool allowLoadBalancing = true;

//        [Header("Building Penetration")]
//        [Tooltip("Consider building obstacles")]
//        public bool enableBuildingPenetration = false;

//        [Tooltip("Layer mask for building objects")]
//        public LayerMask buildingLayers = 1;

//        [Tooltip("Maximum buildings to penetrate")]
//        [Range(1, 5)]
//        public int maxBuildingPenetration = 2;

//        /// <summary>
//        /// Get update interval in seconds
//        /// </summary>
//        public float UpdateInterval => 1f / updateFrequency;

//        /// <summary>
//        /// Create connection settings from this configuration
//        /// </summary>
//        public ConnectionSettings CreateConnectionSettings()
//        {
//            return new ConnectionSettings
//            {
//                minimumSignalThreshold = minimumSignalThreshold,
//                connectionMargin = connectionMargin,
//                handoverMargin = handoverMargin,
//                minimumSINR = minimumSINR,
//                excellentSignalThreshold = 15f,
//                goodSignalThreshold = 10f,
//            };
//        }

//        /// <summary>
//        /// Validate settings and fix any invalid values
//        /// </summary>
//        [ContextMenu("Validate Settings")]
//        public void ValidateSettings()
//        {
//            bool hasChanges = false;

//            // Ensure frequency is reasonable
//            if (defaultFrequency <= 0f || defaultFrequency > 100000f)
//            {
//                defaultFrequency = 3500f;
//                hasChanges = true;
//                Debug.LogWarning("Fixed invalid default frequency");
//            }

//            // Ensure power is reasonable
//            if (defaultTransmitterPower <= 0f || defaultTransmitterPower > 80f)
//            {
//                defaultTransmitterPower = 43f;
//                hasChanges = true;
//                Debug.LogWarning("Fixed invalid default transmitter power");
//            }

//            // Ensure sensitivity is reasonable
//            if (defaultReceiverSensitivity > -50f || defaultReceiverSensitivity < -150f)
//            {
//                defaultReceiverSensitivity = -105f;
//                hasChanges = true;
//                Debug.LogWarning("Fixed invalid default receiver sensitivity");
//            }

//            // Ensure thresholds make sense
//            if (minimumSignalThreshold >= defaultReceiverSensitivity + connectionMargin)
//            {
//                minimumSignalThreshold = defaultReceiverSensitivity + connectionMargin - 5f;
//                hasChanges = true;
//                Debug.LogWarning("Fixed minimum signal threshold");
//            }

//            // Ensure SINR is reasonable
//            if (minimumSINR < -20f || minimumSINR > 20f)
//            {
//                minimumSINR = -6f;
//                hasChanges = true;
//                Debug.LogWarning("Fixed minimum SINR");
//            }
//        }

//        void OnValidate()
//        {
//            // Automatically validate when values change in inspector
//            ValidateSettings();
//        }
//    }
//}