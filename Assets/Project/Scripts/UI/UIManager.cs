using UnityEngine;
using UnityEngine.UI;
using RFSimulation.UI;
using RFSimulation.Core;

namespace RFSimulation.UI
{
    /// <summary>
    /// Central UI manager that coordinates all UI components
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI Components")]
        public UIControls mainUIControls;
        public ScenarioUI scenarioUI;
        public StatusDisplay statusDisplay;

        [Header("UI Panels")]
        public GameObject mainControlPanel;
        public GameObject scenarioPanel;
        public GameObject statusPanel;
        public GameObject advancedPanel;

        [Header("Persistent UI Elements")]
        public GameObject topButtonBar;  
        public Button basicModeButton;   
        public Button advancedModeButton;
        public Button statusModeButton;
        public Button scenarioModeButton;
        public Button resetUIButton;     

        [Header("UI Modes")]
        public bool showAdvancedControls = false;
        public bool autoHidePanels = false;
        public UIMode currentMode = UIMode.Basic;

        public enum UIMode
        {
            Basic,      // Simple controls only
            Advanced,   // All controls visible
            Status,     // Status display prominent
            Scenario    // Scenario management focused
        }

        // Events
        public System.Action<UIMode> OnUIModeChanged;

        void Start()
        {
            InitializeUI();
            SetupPersistentTopBar();
            SetUIMode(currentMode);
        }

        private void InitializeUI()
        {
            // Ensure all UI components are connected
            if (mainUIControls == null)
                mainUIControls = FindObjectOfType<UIControls>();

            if (scenarioUI == null)
                scenarioUI = FindObjectOfType<ScenarioUI>();

            if (statusDisplay == null)
                statusDisplay = FindObjectOfType<StatusDisplay>();

            // Subscribe to events for coordination
            if (scenarioUI != null)
            {
                scenarioUI.OnScenarioSelected += OnScenarioSelected;
            }

            Debug.Log("UIManager initialized");
        }

        private void SetupPersistentTopBar()
        {
            // Create top button bar if it doesn't exist
            if (topButtonBar == null)
            {
                CreatePersistentTopBar();
            }

            // Ensure top bar is always active
            if (topButtonBar != null)
            {
                topButtonBar.SetActive(true);
            }

            // Setup button listeners
            if (basicModeButton != null)
                basicModeButton.onClick.AddListener(() => SetUIMode(UIMode.Basic));

            if (advancedModeButton != null)
                advancedModeButton.onClick.AddListener(() => SetUIMode(UIMode.Advanced));

            if (statusModeButton != null)
                statusModeButton.onClick.AddListener(() => SetUIMode(UIMode.Status));

            if (scenarioModeButton != null)
                scenarioModeButton.onClick.AddListener(() => SetUIMode(UIMode.Scenario));

            if (resetUIButton != null)
                resetUIButton.onClick.AddListener(ResetUILayout);

            Debug.Log("Persistent top bar configured");
        }

        private void CreatePersistentTopBar()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("No Canvas found! Cannot create top bar.");
                return;
            }

            // Create top bar container
            GameObject topBar = new GameObject("TopButtonBar");
            topBar.transform.SetParent(canvas.transform, false);

            RectTransform topBarRect = topBar.AddComponent<RectTransform>();

            // Position at top of screen
            topBarRect.anchorMin = new Vector2(0, 1);
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.anchoredPosition = new Vector2(0, 0);
            topBarRect.sizeDelta = new Vector2(0, 50); // 50 pixels high

            // Add background
            Image background = topBar.AddComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark semi-transparent

            // Add horizontal layout
            HorizontalLayoutGroup layout = topBar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;

            topButtonBar = topBar;

            Debug.Log("Persistent top bar created programmatically");
        }

        public void SetUIMode(UIMode mode)
        {
            currentMode = mode;

            switch (mode)
            {
                case UIMode.Basic:
                    SetBasicMode();
                    break;
                case UIMode.Advanced:
                    SetAdvancedMode();
                    break;
                case UIMode.Status:
                    SetStatusMode();
                    break;
                case UIMode.Scenario:
                    SetScenarioMode();
                    break;
            }

            if (topButtonBar != null)
            {
                topButtonBar.SetActive(true);
            }

            OnUIModeChanged?.Invoke(mode);
            Debug.Log($"UI Mode changed to: {mode}");
        }

        private void UpdateModeButtons()
        {
            // Reset all button colors
            ResetButtonColor(basicModeButton);
            ResetButtonColor(advancedModeButton);
            ResetButtonColor(statusModeButton);
            ResetButtonColor(scenarioModeButton);

            // Highlight current mode button
            Button activeButton = null;
            switch (currentMode)
            {
                case UIMode.Basic: activeButton = basicModeButton; break;
                case UIMode.Advanced: activeButton = advancedModeButton; break;
                case UIMode.Status: activeButton = statusModeButton; break;
                case UIMode.Scenario: activeButton = scenarioModeButton; break;
            }

            if (activeButton != null)
            {
                HighlightButton(activeButton);
            }
        }

        private void ResetButtonColor(Button button)
        {
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                button.colors = colors;
            }
        }

        private void HighlightButton(Button button)
        {
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.green;
                button.colors = colors;
            }
        }

        private void SetBasicMode()
        {
            SetPanelActive(mainControlPanel, true);
            SetPanelActive(scenarioPanel, false);
            SetPanelActive(statusPanel, false);
            SetPanelActive(advancedPanel, false);
        }

        private void SetAdvancedMode()
        {
            SetPanelActive(mainControlPanel, true);
            SetPanelActive(scenarioPanel, true);
            SetPanelActive(statusPanel, true);
            SetPanelActive(advancedPanel, true);
        }

        private void SetStatusMode()
        {
            SetPanelActive(mainControlPanel, false);
            SetPanelActive(scenarioPanel, false);
            SetPanelActive(statusPanel, true);
            SetPanelActive(advancedPanel, false);
        }

        private void SetScenarioMode()
        {
            SetPanelActive(mainControlPanel, false);
            SetPanelActive(scenarioPanel, true);
            SetPanelActive(statusPanel, false);
            SetPanelActive(advancedPanel, false);
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        // Event handlers
        private void OnScenarioSelected(string scenarioName)
        {
            // Coordinate UI updates when scenario changes
            if (mainUIControls != null)
            {
                mainUIControls.RefreshAllUI();
            }

            if (statusDisplay != null)
            {
                statusDisplay.ForceUpdate();
            }
        }

        // Public methods for UI control
        public void ToggleAdvancedControls()
        {
            showAdvancedControls = !showAdvancedControls;
            SetUIMode(showAdvancedControls ? UIMode.Advanced : UIMode.Basic);
        }

        public void ToggleStatusDisplay()
        {
            SetUIMode(currentMode == UIMode.Status ? UIMode.Basic : UIMode.Status);
        }

        public void ToggleScenarioUI()
        {
            SetUIMode(currentMode == UIMode.Scenario ? UIMode.Basic : UIMode.Scenario);
        }

        public void ResetUILayout()
        {
            SetUIMode(UIMode.Basic);
        }

        [ContextMenu("Emergency UI Reset")]
        public void EmergencyUIReset()
        {
            // Force show everything
            if (topButtonBar != null) topButtonBar.SetActive(true);
            if (mainControlPanel != null) mainControlPanel.SetActive(true);
            if (scenarioPanel != null) scenarioPanel.SetActive(true);
            if (statusPanel != null) statusPanel.SetActive(true);
            if (advancedPanel != null) advancedPanel.SetActive(true);

            SetUIMode(UIMode.Advanced);
            Debug.Log("Emergency UI Reset - All panels shown");
        }

        // NEW: Method to add keyboard shortcuts for mode switching
        void Update()
        {
            // Keyboard shortcuts for UI modes
            if (Input.GetKeyDown(KeyCode.F1)) SetUIMode(UIMode.Basic);
            if (Input.GetKeyDown(KeyCode.F2)) SetUIMode(UIMode.Advanced);
            if (Input.GetKeyDown(KeyCode.F3)) SetUIMode(UIMode.Status);
            if (Input.GetKeyDown(KeyCode.F4)) SetUIMode(UIMode.Scenario);
            if (Input.GetKeyDown(KeyCode.Escape)) ResetUILayout(); // Emergency reset
        }

        // Context menu methods
        [ContextMenu("Switch to Basic Mode")]
        public void SwitchToBasicMode() => SetUIMode(UIMode.Basic);

        [ContextMenu("Switch to Advanced Mode")]
        public void SwitchToAdvancedMode() => SetUIMode(UIMode.Advanced);

        [ContextMenu("Switch to Status Mode")]
        public void SwitchToStatusMode() => SetUIMode(UIMode.Status);

        [ContextMenu("Switch to Scenario Mode")]
        public void SwitchToScenarioMode() => SetUIMode(UIMode.Scenario);

        void OnDestroy()
        {
            // Unsubscribe from events
            if (scenarioUI != null)
            {
                scenarioUI.OnScenarioSelected -= OnScenarioSelected;
            }
        }
    }
}