using UnityEngine;
using UnityEngine.UI;

namespace RFSimulation.UI
{
    /// <summary>
    /// Simple panel toggle manager: each button toggles its corresponding panel.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI Components (Optional)")]
        public ControlUI controlUI;
        public ScenarioUI scenarioUI;
        public StatusUI statusUI;

        [Header("UI Panels")]
        public GameObject controlPanel;
        public GameObject scenarioPanel;
        public GameObject statusPanel;

        [Header("Top Bar (Optional)")]
        public GameObject topButtonBar;
        public Button controlButton;
        public Button scenarioButton;
        public Button statusButton;

        [Header("Behavior")]
        [Tooltip("If enabled, showing one panel will hide the others.")]
        public bool autoHideOthers = false;

        // Fired after any visibility change: (controlVisible, scenarioVisible, statusVisible)
        public System.Action<bool, bool, bool> OnPanelVisibilityChanged;

        void Start()
        {
            InitializeComponents();
            WireButtons();

            // Ensure initial highlights match current active states
            FireVisibilityEvent();
        }

        private void InitializeComponents()
        {
            if (controlUI == null) controlUI = FindFirstObjectByType<ControlUI>();
            if (scenarioUI == null) scenarioUI = FindFirstObjectByType<ScenarioUI>();
            if (statusUI == null) statusUI = FindFirstObjectByType<StatusUI>();

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
        }

        // -------- Public toggle API --------

        public void ToggleControlPanel() => TogglePanel(controlPanel, controlButton, hideOthers: autoHideOthers);
        public void ToggleScenarioPanel() => TogglePanel(scenarioPanel, scenarioButton, hideOthers: autoHideOthers);
        public void ToggleStatusPanel() => TogglePanel(statusPanel, statusButton, hideOthers: autoHideOthers);

        public void ShowControlPanel(bool show) => SetPanel(controlPanel, controlButton, show, autoHideOthers);
        public void ShowScenarioPanel(bool show) => SetPanel(scenarioPanel, scenarioButton, show, autoHideOthers);
        public void ShowStatusPanel(bool show) => SetPanel(statusPanel, statusButton, show, autoHideOthers);

        // -------- Internals --------

        private void TogglePanel(GameObject panel, Button button, bool hideOthers)
        {
            if (panel == null) return;

            bool newState = !panel.activeSelf;
            SetPanel(panel, button, newState, hideOthers);
        }

        private void SetPanel(GameObject targetPanel, Button targetButton, bool show, bool hideOthers)
        {
            if (targetPanel == null) return;

            if (hideOthers && show)
            {
                // Hide others first
                SetActiveSafe(controlPanel, false, except: targetPanel);
                SetActiveSafe(scenarioPanel, false, except: targetPanel);
                SetActiveSafe(statusPanel, false, except: targetPanel);
            }

            targetPanel.SetActive(show);

            FireVisibilityEvent();
        }

        private void SetActiveSafe(GameObject panel, bool active, GameObject except = null)
        {
            if (panel == null || panel == except) return;
            panel.SetActive(active);
        }

        private void FireVisibilityEvent()
        {
            bool c = controlPanel != null && controlPanel.activeSelf;
            bool s = scenarioPanel != null && scenarioPanel.activeSelf;
            bool t = statusPanel != null && statusPanel.activeSelf;
            OnPanelVisibilityChanged?.Invoke(c, s, t);
        }
    }
}
