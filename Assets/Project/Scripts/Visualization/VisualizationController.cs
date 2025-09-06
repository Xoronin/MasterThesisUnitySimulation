using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using RFSimulation.Core;

namespace RFSimulation.Visualization
{
    /// <summary>
    /// Centralized controller for all RF visualization effects
    /// Provides easy toggles and presets for different visualization modes
    /// </summary>
    public class RFVisualizationController : MonoBehaviour
    {
        [Header("Visualization Presets")]
        public VisualizationPreset defaultPreset = VisualizationPreset.Balanced;
        public bool autoApplyToNewObjects = true;

        [Header("Global Toggles")]
        public bool showTransmitterSignalRings = true;
        public bool showTransmitterAntennaPatterns = true;
        public bool showTransmitterPowerIndicators = true;
        public bool showTransmitterLabels = true;

        [Space]
        public bool showReceiverSignalBars = true;
        public bool showReceiverSignalSpheres = true;
        public bool showReceiverConnectionLines = true;
        public bool showReceiverLabels = true;

        [Header("Animation Settings")]
        public bool enableAnimations = true;
        public float globalAnimationSpeed = 1f;

        [Header("UI Controls")]
        public Toggle transmitterRingsToggle;
        public Toggle transmitterPatternsToggle;
        public Toggle transmitterPowerToggle;
        public Toggle transmitterLabelsToggle;
        public Toggle receiverBarsToggle;
        public Toggle receiverSpheresToggle;
        public Toggle receiverConnectionsToggle;
        public Toggle receiverLabelsToggle;
        public Dropdown presetDropdown;
        public Slider animationSpeedSlider;
        public Button refreshVisualizationsButton;

        [Header("Performance")]
        public int maxVisibleObjects = 50;
        public float updateInterval = 0.5f;
        public bool enableLOD = true;
        public float lodDistance = 100f;

        private Dictionary<GameObject, TransmitterVisualizer> transmitterVisualizers = new Dictionary<GameObject, TransmitterVisualizer>();
        private Dictionary<GameObject, ReceiverVisualizer> receiverVisualizers = new Dictionary<GameObject, ReceiverVisualizer>();
        private float lastUpdateTime = 0f;

        public enum VisualizationPreset
        {
            Minimal,        // Basic shapes only
            Essential,      // Key indicators only
            Balanced,       // Good mix of info and performance
            Detailed,       // Most features enabled
            Presentation,   // Beautiful for demos
            Debug          // Everything visible for troubleshooting
        }

        void Start()
        {
            InitializeController();
            SetupUIControls();
            ApplyPreset(defaultPreset);
            RefreshAllVisualizations();
        }

        void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateVisualizations();
                lastUpdateTime = Time.time;
            }
        }

        private void InitializeController()
        {
            // Subscribe to simulation manager events
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.OnEquipmentCountChanged += OnEquipmentCountChanged;
            }
        }

        private void SetupUIControls()
        {
            // Setup toggle listeners
            if (transmitterRingsToggle != null)
                transmitterRingsToggle.onValueChanged.AddListener(ToggleTransmitterRings);

            if (transmitterPatternsToggle != null)
                transmitterPatternsToggle.onValueChanged.AddListener(ToggleTransmitterPatterns);

            if (transmitterPowerToggle != null)
                transmitterPowerToggle.onValueChanged.AddListener(ToggleTransmitterPower);

            if (transmitterLabelsToggle != null)
                transmitterLabelsToggle.onValueChanged.AddListener(ToggleTransmitterLabels);

            if (receiverBarsToggle != null)
                receiverBarsToggle.onValueChanged.AddListener(ToggleReceiverBars);

            if (receiverSpheresToggle != null)
                receiverSpheresToggle.onValueChanged.AddListener(ToggleReceiverSpheres);

            if (receiverConnectionsToggle != null)
                receiverConnectionsToggle.onValueChanged.AddListener(ToggleReceiverConnections);

            if (receiverLabelsToggle != null)
                receiverLabelsToggle.onValueChanged.AddListener(ToggleReceiverLabels);

            // Setup preset dropdown
            if (presetDropdown != null)
            {
                presetDropdown.ClearOptions();
                var presetNames = new List<string>();
                foreach (VisualizationPreset preset in System.Enum.GetValues(typeof(VisualizationPreset)))
                {
                    presetNames.Add(preset.ToString());
                }
                presetDropdown.AddOptions(presetNames);
                presetDropdown.onValueChanged.AddListener(OnPresetChanged);
            }

            // Setup animation speed slider
            if (animationSpeedSlider != null)
            {
                animationSpeedSlider.minValue = 0.1f;
                animationSpeedSlider.maxValue = 5f;
                animationSpeedSlider.value = globalAnimationSpeed;
                animationSpeedSlider.onValueChanged.AddListener(OnAnimationSpeedChanged);
            }

            // Setup refresh button
            if (refreshVisualizationsButton != null)
                refreshVisualizationsButton.onClick.AddListener(RefreshAllVisualizations);

            UpdateUIFromSettings();
        }

        private void UpdateUIFromSettings()
        {
            // Update toggles to match current settings
            if (transmitterRingsToggle != null) transmitterRingsToggle.isOn = showTransmitterSignalRings;
            if (transmitterPatternsToggle != null) transmitterPatternsToggle.isOn = showTransmitterAntennaPatterns;
            if (transmitterPowerToggle != null) transmitterPowerToggle.isOn = showTransmitterPowerIndicators;
            if (transmitterLabelsToggle != null) transmitterLabelsToggle.isOn = showTransmitterLabels;

            if (receiverBarsToggle != null) receiverBarsToggle.isOn = showReceiverSignalBars;
            if (receiverSpheresToggle != null) receiverSpheresToggle.isOn = showReceiverSignalSpheres;
            if (receiverConnectionsToggle != null) receiverConnectionsToggle.isOn = showReceiverConnectionLines;
            if (receiverLabelsToggle != null) receiverLabelsToggle.isOn = showReceiverLabels;
        }

        #region Preset Management

        public void ApplyPreset(VisualizationPreset preset)
        {
            defaultPreset = preset;

            switch (preset)
            {
                case VisualizationPreset.Minimal:
                    SetAllSettings(false, false, false, true, false, false, false, true);
                    break;

                case VisualizationPreset.Essential:
                    SetAllSettings(false, false, true, true, true, false, true, true);
                    break;

                case VisualizationPreset.Balanced:
                    SetAllSettings(true, false, true, true, true, true, true, true);
                    break;

                case VisualizationPreset.Detailed:
                    SetAllSettings(true, true, true, true, true, true, true, true);
                    break;

                case VisualizationPreset.Presentation:
                    SetAllSettings(true, true, false, true, false, true, true, true);
                    enableAnimations = true;
                    globalAnimationSpeed = 1.5f;
                    break;

                case VisualizationPreset.Debug:
                    SetAllSettings(true, true, true, true, true, true, true, true);
                    enableAnimations = false;
                    break;
            }

            ApplySettingsToAllObjects();
            UpdateUIFromSettings();

            Debug.Log($"Applied visualization preset: {preset}");
        }

        private void SetAllSettings(bool txRings, bool txPatterns, bool txPower, bool txLabels,
                                   bool rxBars, bool rxSpheres, bool rxConnections, bool rxLabels)
        {
            showTransmitterSignalRings = txRings;
            showTransmitterAntennaPatterns = txPatterns;
            showTransmitterPowerIndicators = txPower;
            showTransmitterLabels = txLabels;

            showReceiverSignalBars = rxBars;
            showReceiverSignalSpheres = rxSpheres;
            showReceiverConnectionLines = rxConnections;
            showReceiverLabels = rxLabels;
        }

        #endregion

        #region Object Management

        public void RefreshAllVisualizations()
        {
            ClearVisualizationCache();
            FindAndInitializeObjects();
            ApplySettingsToAllObjects();
        }

        private void FindAndInitializeObjects()
        {
            // Find transmitters
            var transmitters = FindObjectsByType<Transmitter>(FindObjectsSortMode.InstanceID);
            foreach (var transmitter in transmitters)
            {
                EnsureTransmitterVisualization(transmitter);
            }

            // Find receivers
            var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);
            foreach (var receiver in receivers)
            {
                EnsureReceiverVisualization(receiver);
            }

            Debug.Log($"Initialized visualizations for {transmitters.Length} transmitters and {receivers.Length} receivers");
        }

        private void EnsureTransmitterVisualization(Transmitter transmitter)
        {
            if (transmitter == null) return;

            if (!transmitterVisualizers.ContainsKey(transmitter.gameObject))
            {
                var visualizer = transmitter.GetComponent<TransmitterVisualizer>();
                if (visualizer == null)
                {
                    visualizer = transmitter.gameObject.AddComponent<TransmitterVisualizer>();
                    visualizer.Initialize(transmitter);
                }
                transmitterVisualizers[transmitter.gameObject] = visualizer;
            }
        }

        private void EnsureReceiverVisualization(Receiver receiver)
        {
            if (receiver == null) return;

            if (!receiverVisualizers.ContainsKey(receiver.gameObject))
            {
                var visualizer = receiver.GetComponent<ReceiverVisualizer>();
                if (visualizer == null)
                {
                    visualizer = receiver.gameObject.AddComponent<ReceiverVisualizer>();
                    visualizer.Initialize(receiver);
                }
                receiverVisualizers[receiver.gameObject] = visualizer;
            }
        }

        private void ClearVisualizationCache()
        {
            // Remove null references
            var txToRemove = new List<GameObject>();
            foreach (var kvp in transmitterVisualizers)
            {
                if (kvp.Key == null || kvp.Value == null)
                    txToRemove.Add(kvp.Key);
            }
            foreach (var key in txToRemove)
            {
                transmitterVisualizers.Remove(key);
            }

            var rxToRemove = new List<GameObject>();
            foreach (var kvp in receiverVisualizers)
            {
                if (kvp.Key == null || kvp.Value == null)
                    rxToRemove.Add(kvp.Key);
            }
            foreach (var key in rxToRemove)
            {
                receiverVisualizers.Remove(key);
            }
        }

        private void ApplySettingsToAllObjects()
        {
            // Apply to transmitters
            foreach (var visualizer in transmitterVisualizers.Values)
            {
                if (visualizer != null)
                {
                    visualizer.SetVisibilityOptions(
                        showTransmitterAntennaPatterns,
                        false
                    );
                }
            }

            // Apply to receivers
            foreach (var visualizer in receiverVisualizers.Values)
            {
                if (visualizer != null)
                {
                    visualizer.SetVisibilityOptions(
                        showReceiverSignalBars,
                        showReceiverSignalSpheres,
                        showReceiverConnectionLines,
                        showReceiverLabels
                    );
                }
            }
        }

        private void UpdateVisualizations()
        {
            if (enableLOD)
            {
                ApplyLevelOfDetail();
            }

            // Check for new objects if auto-apply is enabled
            if (autoApplyToNewObjects)
            {
                CheckForNewObjects();
            }
        }

        private void ApplyLevelOfDetail()
        {
            if (Camera.main == null) return;

            Vector3 cameraPos = Camera.main.transform.position;

            // LOD for transmitters
            foreach (var kvp in transmitterVisualizers)
            {
                if (kvp.Key == null || kvp.Value == null) continue;

                float distance = Vector3.Distance(cameraPos, kvp.Key.transform.position);
                bool isNear = distance <= lodDistance;

                // Reduce detail for distant objects
                kvp.Value.SetVisibilityOptions(
                    showTransmitterAntennaPatterns && isNear,
                    false
                );
            }

            // LOD for receivers
            foreach (var kvp in receiverVisualizers)
            {
                if (kvp.Key == null || kvp.Value == null) continue;

                float distance = Vector3.Distance(cameraPos, kvp.Key.transform.position);
                bool isNear = distance <= lodDistance;

                kvp.Value.SetVisibilityOptions(
                    showReceiverSignalBars && isNear,
                    showReceiverSignalSpheres && isNear,
                    showReceiverConnectionLines,
                    showReceiverLabels
                );
            }
        }

        private void CheckForNewObjects()
        {
            // Check for new transmitters
            var currentTransmitters = FindObjectsByType<Transmitter>(FindObjectsSortMode.InstanceID);
            foreach (var transmitter in currentTransmitters)
            {
                if (!transmitterVisualizers.ContainsKey(transmitter.gameObject))
                {
                    EnsureTransmitterVisualization(transmitter);
                    ApplyTransmitterSettings(transmitterVisualizers[transmitter.gameObject]);
                }
            }

            // Check for new receivers
            var currentReceivers = FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);
            foreach (var receiver in currentReceivers)
            {
                if (!receiverVisualizers.ContainsKey(receiver.gameObject))
                {
                    EnsureReceiverVisualization(receiver);
                    ApplyReceiverSettings(receiverVisualizers[receiver.gameObject]);
                }
            }
        }

        #endregion

        #region Event Handlers

        private void OnEquipmentCountChanged(int transmitterCount, int receiverCount)
        {
            if (autoApplyToNewObjects)
            {
                // Delay to ensure objects are fully initialized
                Invoke(nameof(RefreshAllVisualizations), 0.1f);
            }
        }

        private void OnPresetChanged(int presetIndex)
        {
            if (presetIndex >= 0 && presetIndex < System.Enum.GetValues(typeof(VisualizationPreset)).Length)
            {
                ApplyPreset((VisualizationPreset)presetIndex);
            }
        }

        private void OnAnimationSpeedChanged(float speed)
        {
            globalAnimationSpeed = speed;
            // Apply to all objects that support animation speed
            // This would need to be implemented in the visualizer classes
        }

        #endregion

        #region Toggle Methods

        private void ToggleTransmitterRings(bool enabled)
        {
            showTransmitterSignalRings = enabled;
            ApplyTransmitterSettingsToAll();
        }

        private void ToggleTransmitterPatterns(bool enabled)
        {
            showTransmitterAntennaPatterns = enabled;
            ApplyTransmitterSettingsToAll();
        }

        private void ToggleTransmitterPower(bool enabled)
        {
            showTransmitterPowerIndicators = enabled;
            ApplyTransmitterSettingsToAll();
        }

        private void ToggleTransmitterLabels(bool enabled)
        {
            showTransmitterLabels = enabled;
            ApplyTransmitterSettingsToAll();
        }

        private void ToggleReceiverBars(bool enabled)
        {
            showReceiverSignalBars = enabled;
            ApplyReceiverSettingsToAll();
        }

        private void ToggleReceiverSpheres(bool enabled)
        {
            showReceiverSignalSpheres = enabled;
            ApplyReceiverSettingsToAll();
        }

        private void ToggleReceiverConnections(bool enabled)
        {
            showReceiverConnectionLines = enabled;
            ApplyReceiverSettingsToAll();
        }

        private void ToggleReceiverLabels(bool enabled)
        {
            showReceiverLabels = enabled;
            ApplyReceiverSettingsToAll();
        }

        private void ApplyTransmitterSettingsToAll()
        {
            foreach (var visualizer in transmitterVisualizers.Values)
            {
                if (visualizer != null)
                    ApplyTransmitterSettings(visualizer);
            }
        }

        private void ApplyReceiverSettingsToAll()
        {
            foreach (var visualizer in receiverVisualizers.Values)
            {
                if (visualizer != null)
                    ApplyReceiverSettings(visualizer);
            }
        }

        private void ApplyTransmitterSettings(TransmitterVisualizer visualizer)
        {
            visualizer.SetVisibilityOptions(
                showTransmitterAntennaPatterns,
                false
            );
        }

        private void ApplyReceiverSettings(ReceiverVisualizer visualizer)
        {
            visualizer.SetVisibilityOptions(
                showReceiverSignalBars,
                showReceiverSignalSpheres,
                showReceiverConnectionLines,
                showReceiverLabels
            );
        }

        #endregion

        #region Public API

        public void SetPreset(VisualizationPreset preset)
        {
            ApplyPreset(preset);
            if (presetDropdown != null)
            {
                presetDropdown.value = (int)preset;
            }
        }

        public void ToggleAllVisualizations(bool enabled)
        {
            showTransmitterSignalRings = enabled;
            showTransmitterAntennaPatterns = enabled;
            showTransmitterPowerIndicators = enabled;
            showTransmitterLabels = enabled;
            showReceiverSignalBars = enabled;
            showReceiverSignalSpheres = enabled;
            showReceiverConnectionLines = enabled;
            showReceiverLabels = enabled;

            ApplySettingsToAllObjects();
            UpdateUIFromSettings();
        }

        public void SetLODDistance(float distance)
        {
            lodDistance = distance;
        }

        public void EnableAutoApply(bool enable)
        {
            autoApplyToNewObjects = enable;
        }

        public Dictionary<string, object> GetVisualizationStats()
        {
            return new Dictionary<string, object>
            {
                ["transmitterVisualizers"] = transmitterVisualizers.Count,
                ["receiverVisualizers"] = receiverVisualizers.Count,
                ["currentPreset"] = defaultPreset.ToString(),
                ["animationsEnabled"] = enableAnimations,
                ["lodEnabled"] = enableLOD,
                ["lodDistance"] = lodDistance
            };
        }

        #endregion

        #region Context Menu

        [ContextMenu("Apply Minimal Preset")]
        public void ApplyMinimalPreset() => ApplyPreset(VisualizationPreset.Minimal);

        [ContextMenu("Apply Balanced Preset")]
        public void ApplyBalancedPreset() => ApplyPreset(VisualizationPreset.Balanced);

        [ContextMenu("Apply Detailed Preset")]
        public void ApplyDetailedPreset() => ApplyPreset(VisualizationPreset.Detailed);

        [ContextMenu("Apply Presentation Preset")]
        public void ApplyPresentationPreset() => ApplyPreset(VisualizationPreset.Presentation);

        [ContextMenu("Refresh All Visualizations")]
        public void ContextRefreshAll() => RefreshAllVisualizations();

        [ContextMenu("Print Visualization Stats")]
        public void PrintStats()
        {
            var stats = GetVisualizationStats();
            Debug.Log("=== VISUALIZATION STATS ===");
            foreach (var kvp in stats)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value}");
            }
        }

        #endregion

        void OnDestroy()
        {
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.OnEquipmentCountChanged -= OnEquipmentCountChanged;
            }
        }
    }
}