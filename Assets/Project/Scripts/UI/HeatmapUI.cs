using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RFSimulation.Core.Components;
using RFSimulation.Propagation.Core;
using RFSimulation.Core.Managers;
using RFSimulation.Utils;
using RFSimulation.Visualization;
using System.Collections.Generic;
using System.Collections;

namespace RFSimulation.UI
{
    public class HeatmapUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public Dropdown resolutionDropdown;

        [Header("Resolution Options")]
        public int[] resolutionOptions = { 32, 64, 128, 256 };
        public int defaultResolution = 64;

        [Header("Feedback")]
        public GameObject loadingPanel;
        public Text loadingText;

        private HeatmapVisualization heatmapVisualization;

        void Awake()
        {
            if (heatmapVisualization == null)
                heatmapVisualization = FindFirstObjectByType<HeatmapVisualization>();
        }

        void Start()
        {
            SetupUI();

            loadingPanel.SetActive(false);
        }

        private void SetupUI()
        {
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();

                List<string> options = new List<string>();
                int defaultIndex = 0;

                for (int i = 0; i < resolutionOptions.Length; i++)
                {
                    options.Add($"{resolutionOptions[i]} x {resolutionOptions[i]}");

                    if (resolutionOptions[i] == defaultResolution)
                        defaultIndex = i;
                }

                resolutionDropdown.AddOptions(options);
                resolutionDropdown.value = defaultIndex;
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

                OnResolutionChanged(defaultIndex);
            }
        }

        private void OnResolutionChanged(int index)
        {
            if (heatmapVisualization == null) return;

            if (index < 0 || index >= resolutionOptions.Length)
            {
                Debug.LogWarning($"[HeatmapUI] Invalid resolution index: {index}");
                return;
            }

            int newResolution = resolutionOptions[index];

            heatmapVisualization.settings.resolution = newResolution;

            RefreshHeatmap();
        }

        public void ToggleHeatmapPanel(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void ToggleLoadingPanel(bool visible)
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(visible);

            if (loadingText != null)
                loadingText.text = $"Generating {heatmapVisualization.settings.resolution}x{heatmapVisualization.settings.resolution} heatmap...";
        }

        private void RefreshHeatmap()
        {
            if (heatmapVisualization == null) return;

            heatmapVisualization.SetUIEnabled(true);
            heatmapVisualization.UpdateHeatmap();
        }

        void OnDestroy()
        {
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        }
    }
}
