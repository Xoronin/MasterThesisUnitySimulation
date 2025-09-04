using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RFSimulation.Visualization;
using RFSimulation.Core;

namespace RFSimulation.UI
{
    /// <summary>
    /// UI controls for the signal strength heatmap - Compatible with corrected SignalHeatmap
    /// </summary>
    public class HeatmapControls : MonoBehaviour
    {
        [Header("Heatmap Reference")]
        public SignalHeatmap heatmap;

        [Header("Main Controls")]
        public Toggle enableHeatmapToggle;
        public Button updateHeatmapButton;
        public Button clearHeatmapButton;

        [Header("Visual Settings")]
        public Slider transparencySlider;
        public Text transparencyLabel;
        public Slider resolutionSlider;
        public Text resolutionLabel;

        [Header("Signal Range")]
        public Slider minSignalSlider;
        public Text minSignalLabel;
        public Slider maxSignalSlider;
        public Text maxSignalLabel;
        public Slider sensitivitySlider;
        public Text sensitivityLabel;

        [Header("Area Settings")]
        public InputField areaSizeXInput;
        public InputField areaSizeYInput;
        public Button applyAreaSizeButton;
        public Slider samplingHeightSlider;
        public Text samplingHeightLabel;

        [Header("Performance Settings")]
        public Slider updateIntervalSlider;
        public Text updateIntervalLabel;
        public Toggle autoUpdateToggle;

        [Header("Status Display")]
        public Text statusText;
        public Text heatmapInfoText;
        public Text transmitterCountText;

        [Header("Color Preview")]
        public Image noSignalColorPreview;
        public Image weakSignalColorPreview;
        public Image fairSignalColorPreview;
        public Image goodSignalColorPreview;
        public Image excellentSignalColorPreview;

        void Start()
        {
            FindHeatmapIfNeeded();
            SetupUI();
            UpdateUIFromHeatmap();
        }

        private void FindHeatmapIfNeeded()
        {
            if (heatmap == null)
            {
                heatmap = FindObjectOfType<SignalHeatmap>();
                if (heatmap == null)
                {
                    Debug.LogWarning("No SignalHeatmap found in scene!");
                    UpdateStatus("No heatmap found - please add SignalHeatmap to scene");
                    return;
                }
            }
            UpdateStatus("Heatmap controls ready");
        }

        private void SetupUI()
        {
            SetupMainControls();
            SetupVisualControls();
            SetupRangeControls();
            SetupAreaControls();
            SetupPerformanceControls();
            SetupColorPreviews();
        }

        private void SetupMainControls()
        {
            // Enable/disable toggle
            if (enableHeatmapToggle != null)
            {
                enableHeatmapToggle.onValueChanged.AddListener(OnHeatmapToggled);
            }

            // Action buttons
            if (updateHeatmapButton != null)
                updateHeatmapButton.onClick.AddListener(OnUpdateHeatmap);

            if (clearHeatmapButton != null)
                clearHeatmapButton.onClick.AddListener(OnClearHeatmap);

        }

        private void SetupVisualControls()
        {
            // Transparency slider
            if (transparencySlider != null)
            {
                transparencySlider.minValue = 0.1f;
                transparencySlider.maxValue = 1f;
                transparencySlider.onValueChanged.AddListener(OnTransparencyChanged);
            }

            // Resolution slider
            if (resolutionSlider != null)
            {
                resolutionSlider.minValue = 64f;
                resolutionSlider.maxValue = 512f;
                resolutionSlider.onValueChanged.AddListener(OnResolutionChanged);
            }
        }

        private void SetupRangeControls()
        {
            // Min signal slider
            if (minSignalSlider != null)
            {
                minSignalSlider.minValue = -150f;
                minSignalSlider.maxValue = -30f;
                minSignalSlider.onValueChanged.AddListener(OnMinSignalChanged);
            }

            // Max signal slider  
            if (maxSignalSlider != null)
            {
                maxSignalSlider.minValue = -100f;
                maxSignalSlider.maxValue = 0f;
                maxSignalSlider.onValueChanged.AddListener(OnMaxSignalChanged);
            }

            // Sensitivity threshold slider
            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = -120f;
                sensitivitySlider.maxValue = -80f;
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            }
        }

        private void SetupAreaControls()
        {
            // Area size inputs
            if (applyAreaSizeButton != null)
                applyAreaSizeButton.onClick.AddListener(OnApplyAreaSize);

            // Sampling height slider
            if (samplingHeightSlider != null)
            {
                samplingHeightSlider.minValue = 0.5f;
                samplingHeightSlider.maxValue = 10f;
                samplingHeightSlider.onValueChanged.AddListener(OnSamplingHeightChanged);
            }
        }

        private void SetupPerformanceControls()
        {
            // Update interval slider
            if (updateIntervalSlider != null)
            {
                updateIntervalSlider.minValue = 0.5f;
                updateIntervalSlider.maxValue = 10f;
                updateIntervalSlider.onValueChanged.AddListener(OnUpdateIntervalChanged);
            }

            // Auto update toggle
            if (autoUpdateToggle != null)
            {
                autoUpdateToggle.onValueChanged.AddListener(OnAutoUpdateToggled);
            }
        }

        private void SetupColorPreviews()
        {
            if (heatmap == null) return;

            // Set color preview images to show the heatmap color scheme
            if (noSignalColorPreview != null) noSignalColorPreview.color = heatmap.noSignalColor;
            if (weakSignalColorPreview != null) weakSignalColorPreview.color = heatmap.weakSignalColor;
            if (fairSignalColorPreview != null) fairSignalColorPreview.color = heatmap.fairSignalColor;
            if (goodSignalColorPreview != null) goodSignalColorPreview.color = heatmap.goodSignalColor;
            if (excellentSignalColorPreview != null) excellentSignalColorPreview.color = heatmap.excellentSignalColor;
        }

        #region Event Handlers

        private void OnHeatmapToggled(bool enabled)
        {
            if (heatmap != null)
            {
                heatmap.SetVisibility(enabled);
                UpdateStatus(enabled ? "Heatmap enabled" : "Heatmap disabled");
                UpdateInfo();
            }
        }

        private void OnUpdateHeatmap()
        {
            if (heatmap != null)
            {
                heatmap.UpdateHeatmap();
                UpdateStatus("Updating heatmap...");
            }
        }

        private void OnClearHeatmap()
        {
            if (heatmap != null)
            {
                heatmap.SetVisibility(false);
                UpdateStatus("Heatmap hidden");
            }
        }

        private void OnTransparencyChanged(float value)
        {
            if (heatmap != null)
            {
                heatmap.SetTransparency(value);
            }
            UpdateLabels();
        }

        private void OnResolutionChanged(float value)
        {
            int resolution = Mathf.RoundToInt(value);
            if (heatmap != null)
            {
                heatmap.resolution = resolution;
            }
            UpdateLabels();
            UpdateStatus($"Resolution set to {resolution}x{resolution} (update required)");
        }

        private void OnMinSignalChanged(float value)
        {
            if (heatmap != null)
            {
                heatmap.minSignalDbm = value;

                // Ensure max is always greater than min
                if (value >= heatmap.maxSignalDbm)
                {
                    heatmap.maxSignalDbm = value + 10f;
                    if (maxSignalSlider != null)
                        maxSignalSlider.value = heatmap.maxSignalDbm;
                }
            }
            UpdateLabels();
        }

        private void OnMaxSignalChanged(float value)
        {
            if (heatmap != null)
            {
                heatmap.maxSignalDbm = value;

                // Ensure min is always less than max
                if (value <= heatmap.minSignalDbm)
                {
                    heatmap.minSignalDbm = value - 10f;
                    if (minSignalSlider != null)
                        minSignalSlider.value = heatmap.minSignalDbm;
                }
            }
            UpdateLabels();
        }

        private void OnSensitivityChanged(float value)
        {
            if (heatmap != null)
            {
                heatmap.sensitivityThreshold = value;
            }
            UpdateLabels();
        }

        private void OnApplyAreaSize()
        {
            if (heatmap == null) return;

            try
            {
                float sizeX = float.Parse(areaSizeXInput?.text ?? "1000");
                float sizeY = float.Parse(areaSizeYInput?.text ?? "1000");

                heatmap.heatmapSize = new Vector2(sizeX, sizeY);
                UpdateStatus($"Heatmap area set to {sizeX}x{sizeY}m (update required)");
                UpdateInfo();
            }
            catch (System.Exception e)
            {
                UpdateStatus($"Invalid area size input: {e.Message}");
            }
        }

        private void OnSamplingHeightChanged(float value)
        {
            if (heatmap != null)
            {
                heatmap.samplingHeight = value;
            }
            UpdateLabels();
        }

        private void OnUpdateIntervalChanged(float value)
        {
            if (heatmap != null)
            {
                heatmap.updateInterval = value;
            }
            UpdateLabels();
        }

        private void OnAutoUpdateToggled(bool enabled)
        {
            if (heatmap != null)
            {
                heatmap.autoUpdate = enabled;
            }
            UpdateStatus($"Auto update: {(enabled ? "ON" : "OFF")}");
        }

        #endregion

        #region UI Updates

        private void UpdateUIFromHeatmap()
        {
            if (heatmap == null) return;

            // Update controls to match heatmap settings
            if (enableHeatmapToggle != null)
                enableHeatmapToggle.isOn = heatmap.showHeatmap;

            if (transparencySlider != null)
                transparencySlider.value = heatmap.transparency;

            if (resolutionSlider != null)
                resolutionSlider.value = heatmap.resolution;

            if (minSignalSlider != null)
                minSignalSlider.value = heatmap.minSignalDbm;

            if (maxSignalSlider != null)
                maxSignalSlider.value = heatmap.maxSignalDbm;

            if (sensitivitySlider != null)
                sensitivitySlider.value = heatmap.sensitivityThreshold;

            if (samplingHeightSlider != null)
                samplingHeightSlider.value = heatmap.samplingHeight;

            if (updateIntervalSlider != null)
                updateIntervalSlider.value = heatmap.updateInterval;

            if (autoUpdateToggle != null)
                autoUpdateToggle.isOn = heatmap.autoUpdate;

            // Update area size inputs
            if (areaSizeXInput != null)
                areaSizeXInput.text = heatmap.heatmapSize.x.ToString();

            if (areaSizeYInput != null)
                areaSizeYInput.text = heatmap.heatmapSize.y.ToString();

            UpdateLabels();
            UpdateInfo();
        }

        private void UpdateLabels()
        {
            if (heatmap == null) return;

            if (transparencyLabel != null)
                transparencyLabel.text = $"Transparency: {heatmap.transparency:F2}";

            if (resolutionLabel != null)
                resolutionLabel.text = $"Resolution: {heatmap.resolution}x{heatmap.resolution}";

            if (minSignalLabel != null)
                minSignalLabel.text = $"Min Signal: {heatmap.minSignalDbm:F0} dBm";

            if (maxSignalLabel != null)
                maxSignalLabel.text = $"Max Signal: {heatmap.maxSignalDbm:F0} dBm";

            if (sensitivityLabel != null)
                sensitivityLabel.text = $"Sensitivity: {heatmap.sensitivityThreshold:F0} dBm";

            if (samplingHeightLabel != null)
                samplingHeightLabel.text = $"Height: {heatmap.samplingHeight:F1}m";

            if (updateIntervalLabel != null)
                updateIntervalLabel.text = $"Update: {heatmap.updateInterval:F1}s";
        }

        private void UpdateInfo()
        {
            if (heatmap == null) return;

            if (heatmapInfoText != null)
            {
                int totalPixels = heatmap.resolution * heatmap.resolution;
                float areaCovered = heatmap.heatmapSize.x * heatmap.heatmapSize.y;
                float pixelSize = Mathf.Sqrt(areaCovered / totalPixels);

                heatmapInfoText.text = $"Area: {heatmap.heatmapSize.x}x{heatmap.heatmapSize.y}m\n" +
                                      $"Pixels: {totalPixels:N0}\n" +
                                      $"Resolution: ~{pixelSize:F1}m per pixel\n" +
                                      $"Status: {(heatmap.showHeatmap ? "Visible" : "Hidden")}";
            }

            // Update transmitter count
            if (transmitterCountText != null)
            {
                int transmitterCount = 0;
                if (SimulationManager.Instance != null)
                {
                    transmitterCount = SimulationManager.Instance.transmitters.Count;
                }
                else
                {
                    var transmitters = FindObjectsOfType<Transmitter>();
                    transmitterCount = transmitters.Length;
                }

                transmitterCountText.text = $"Transmitters: {transmitterCount}";
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[HeatmapControls] {message}");
        }

        #endregion

        #region Public Methods

        public void RefreshHeatmap()
        {
            if (heatmap != null && heatmap.showHeatmap)
            {
                heatmap.ForceUpdate();
                UpdateStatus("Heatmap refreshed");
            }
        }

        public void ResetToDefaults()
        {
            if (heatmap == null) return;

            heatmap.transparency = 0.7f;
            heatmap.resolution = 128;
            heatmap.minSignalDbm = -120f;
            heatmap.maxSignalDbm = -30f;
            heatmap.sensitivityThreshold = -105f;
            heatmap.heatmapSize = new Vector2(1000f, 1000f);
            heatmap.samplingHeight = 1.5f;
            heatmap.updateInterval = 2f;
            heatmap.autoUpdate = true;

            UpdateUIFromHeatmap();
            UpdateStatus("Reset to default settings");
        }

        public void QuickEnable()
        {
            if (heatmap != null)
            {
                heatmap.SetVisibility(true);
                heatmap.UpdateHeatmap();
                UpdateStatus("Quick heatmap enabled");
            }
        }

        #endregion

        #region Context Menu Debug

        [ContextMenu("Refresh UI from Heatmap")]
        public void RefreshUI()
        {
            UpdateUIFromHeatmap();
        }

        [ContextMenu("Reset to Defaults")]
        public void ContextResetDefaults()
        {
            ResetToDefaults();
        }

        [ContextMenu("Force Heatmap Update")]
        public void ContextForceUpdate()
        {
            RefreshHeatmap();
        }

        [ContextMenu("Print Debug Info")]
        public void PrintDebugInfo()
        {
            Debug.Log("=== HEATMAP CONTROLS DEBUG ===");
            Debug.Log($"Heatmap Reference: {(heatmap != null ? "Found" : "NULL")}");

            if (heatmap != null)
            {
                Debug.Log($"Visible: {heatmap.showHeatmap}");
                Debug.Log($"Resolution: {heatmap.resolution}");
                Debug.Log($"Transparency: {heatmap.transparency}");
                Debug.Log($"Area: {heatmap.heatmapSize}");
                Debug.Log($"Signal Range: {heatmap.minSignalDbm} to {heatmap.maxSignalDbm} dBm");
                Debug.Log($"Auto Update: {heatmap.autoUpdate}");
            }

            Debug.Log($"UI Components Connected:");
            Debug.Log($"  Toggle: {enableHeatmapToggle != null}");
            Debug.Log($"  Transparency Slider: {transparencySlider != null}");
            Debug.Log($"  Resolution Slider: {resolutionSlider != null}");
            Debug.Log($"  Status Text: {statusText != null}");
        }

        #endregion

        void Update()
        {
            // Periodically update transmitter count and info
            if (Time.frameCount % 60 == 0) // Every 60 frames
            {
                UpdateInfo();
            }
        }
    }
}