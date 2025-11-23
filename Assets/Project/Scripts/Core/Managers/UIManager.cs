using UnityEngine;
using UnityEngine.UI;

namespace RFSimulation.UI
{

    public class UIManager : MonoBehaviour
    {
        [Header("UI Components (Optional)")]
        public ControlUI controlUI;
        public ScenarioUI scenarioUI;
        public StatusUI statusUI;
        public HeatmapUI heatmapUI;

        [Header("UI Panels")]
        public GameObject controlPanel;
        public GameObject scenarioPanel;
        public GameObject statusPanel;
        public GameObject heatmapPanel;

        [Header("Top Bar (Optional)")]
        public GameObject topButtonBar;
        public Button controlButton;
        public Button scenarioButton;
        public Button statusButton;
        public Button heatmapButton;

        public System.Action<bool, bool, bool, bool> OnPanelVisibilityChanged;

        void Start()
        {
            InitializeComponents();
            WireButtons();

            if (statusPanel != null)
            {
                statusPanel.SetActive(false);
            }

            FireVisibilityEvent();
        }

        private void InitializeComponents()
        {
            if (controlUI == null) controlUI = FindFirstObjectByType<ControlUI>();
            if (scenarioUI == null) scenarioUI = FindFirstObjectByType<ScenarioUI>();
            if (statusUI == null) statusUI = FindFirstObjectByType<StatusUI>();
            if (heatmapUI == null) heatmapUI = FindFirstObjectByType<HeatmapUI>();
        }

        private void WireButtons()
        {
            if (controlButton != null)
            {
                controlButton.onClick.RemoveAllListeners();
                controlButton.onClick.AddListener(ToggleControlPanel);
            }

            if (scenarioButton != null)
            {
                scenarioButton.onClick.RemoveAllListeners();
                scenarioButton.onClick.AddListener(ToggleScenarioPanel);
            }

            if (statusButton != null)
            {
                statusButton.onClick.RemoveAllListeners();
                statusButton.onClick.AddListener(ToggleStatusPanel);
            }

            if (heatmapButton != null)
            {
                heatmapButton.onClick.RemoveAllListeners();
                heatmapButton.onClick.AddListener(ToggleHeatmapPanel);
            }
        }

        public void ToggleControlPanel() => TogglePanel(controlPanel, controlButton);
        public void ToggleScenarioPanel() => TogglePanel(scenarioPanel, scenarioButton);
        public void ToggleStatusPanel() => TogglePanel(statusPanel, statusButton);
        public void ToggleHeatmapPanel() => TogglePanel(heatmapPanel, heatmapButton);

        public void ShowControlPanel(bool show) => SetPanel(controlPanel, controlButton, show);
        public void ShowScenarioPanel(bool show) => SetPanel(scenarioPanel, scenarioButton, show);
        public void ShowStatusPanel(bool show) => SetPanel(statusPanel, statusButton, show);
        public void ShowHeatmapPanel(bool show) => SetPanel(heatmapPanel, heatmapButton, show);

        private void TogglePanel(GameObject panel, Button button)
        {
            if (panel == null) return;

            bool newState = !panel.activeSelf;
            SetPanel(panel, button, newState);
        }

        private void SetPanel(GameObject targetPanel, Button targetButton, bool show)
        {
            if (targetPanel == null) return;

            targetPanel.SetActive(show);

            FireVisibilityEvent();
        }

        private void FireVisibilityEvent()
        {
            bool c = controlPanel != null && controlPanel.activeSelf;
            bool s = scenarioPanel != null && scenarioPanel.activeSelf;
            bool t = statusPanel != null && statusPanel.activeSelf;
            bool h = heatmapPanel != null && heatmapPanel.activeSelf;
            OnPanelVisibilityChanged?.Invoke(c, s, t, h);
        }
    }
}
