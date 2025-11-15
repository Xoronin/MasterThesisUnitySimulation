using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using RFSimulation.Core.Managers;
using RFSimulation.Propagation;
using RFSimulation.Core.Components;
using RFSimulation.Propagation.Core;
using RFSimulation.Utils;
using System.Globalization;
using System.IO;
using System.Text;
using System;
using System.Collections;


namespace RFSimulation.UI
{
    /// <summary>
    /// Dedicated UI component for scenario management and configuration
    /// </summary>
    public class ScenarioUI : MonoBehaviour
    {
        [Header("Scenario Selection")]
        public Dropdown scenarioDropdown;
        public Button loadScenarioButton;
        public Button deleteScenarioButton;

        [Header("Scenario Creation")]
        public InputField newScenarioNameInput;
        public Button saveScenarioButton;
        public Button newScenarioButton;

        [Header("Status")]
        public Text statusText;

        [Header("Data Export")]
        public Button exportButton;
        public InputField exportFileNameInput; 
        public Button screenshotButton;
        public InputField screenshotFileNameInput;

        // Events
        public System.Action<string> OnScenarioSelected;
        public System.Action<Scenario> OnScenarioValidated;

        private ControlUI controlUI;
        private ScenarioManager scenarioManager;

        void Start()
        {
            Initialize();
            SetupEventListeners();
            RefreshScenarioList();
        }

        private void Initialize()
        {
            scenarioManager = ScenarioManager.Instance;
            controlUI = FindAnyObjectByType<ControlUI>();
            SetStatusText($"Ready to manage scenarios.", Color.white);
        }

        private void SetupEventListeners()
        {
            // Scenario selection
            if (scenarioDropdown != null)
                scenarioDropdown.onValueChanged.AddListener(OnScenarioDropdownChanged);

            if (loadScenarioButton != null)
                loadScenarioButton.onClick.AddListener(LoadSelectedScenario);

            if (deleteScenarioButton != null)
                deleteScenarioButton.onClick.AddListener(DeleteSelectedScenario);

            // Scenario creation
            if (saveScenarioButton != null)
                saveScenarioButton.onClick.AddListener(SaveCurrentScenario);

            if (newScenarioButton != null)
                newScenarioButton.onClick.AddListener(CreateNewScenario);

            // Data export
            if (exportButton != null)
                exportButton.onClick.AddListener(OnExportButtonClicked);

            if (screenshotButton != null)
                screenshotButton.onClick.AddListener(OnScreenshotButtonClicked);

            // Subscribe to scenario manager events
            if (scenarioManager != null)
            {
                scenarioManager.OnScenariosLoaded += OnScenariosLoaded;
                scenarioManager.OnScenarioChanged += OnScenarioChanged;
                scenarioManager.OnScenarioLoaded += OnScenarioLoaded;
                scenarioManager.OnScenarioSaved += OnScenarioSaved;
            }
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (scenarioManager != null)
            {
                scenarioManager.OnScenariosLoaded -= OnScenariosLoaded;
                scenarioManager.OnScenarioChanged -= OnScenarioChanged;
                scenarioManager.OnScenarioLoaded -= OnScenarioLoaded;
                scenarioManager.OnScenarioSaved -= OnScenarioSaved;
            }
        }

        #region Scenario Management

        public void RefreshScenarioList()
        {
            if (scenarioManager != null)
            {
                scenarioManager.LoadAllScenarios();
            }
        }

        private void LoadSelectedScenario()
        {
            if (scenarioDropdown == null || scenarioManager == null) return;

            int selectedIndex = scenarioDropdown.value - 1;
            if (selectedIndex >= 0 && selectedIndex < scenarioManager.scenarios.Count)
            {
                scenarioManager.SelectScenario(selectedIndex);
                OnScenarioSelected?.Invoke(scenarioManager.scenarios[selectedIndex].scenarioName);
            }
            else
            {
                SetStatusText("No scenario selected", Color.red);
            }
        }

        private void DeleteSelectedScenario()
        {
            if (scenarioDropdown == null || scenarioManager == null) return;

            try
            {
                Scenario selectedScenario = GetSelectedScenario();
                string scenarioName = selectedScenario.scenarioName;
                scenarioManager.DeleteScenario(scenarioName);
                SetStatusText($"Deleted: { scenarioName}", Color.green);
                RefreshScenarioList();
                scenarioManager.ClearCurrentScenario();
            }
            catch (Exception ex)
            {
                SetStatusText($"Deletion failed: {ex.Message}", Color.red);
                return;
            }

        }

        private void SaveCurrentScenario()
        {
            if (newScenarioNameInput == null || scenarioManager == null) return;

            string scenarioName = newScenarioNameInput.text.Trim();
            if (string.IsNullOrEmpty(scenarioName))
            {
                SetStatusText("Please enter a scenario name", Color.red);
                return;
            }

            if (!ValidateScenario())
            {
                SetStatusText("Scenario validation failed", Color.red);
                return;
            }

            bool overwrite = false;

            var current = scenarioManager.GetCurrentScenario();
            if (current != null && string.Equals(current.scenarioName, scenarioName, StringComparison.OrdinalIgnoreCase))
            {
                overwrite = true;
            }

            scenarioManager.SaveCurrentScenario(scenarioName, overwrite);

            if (overwrite)
                SetStatusText($"Overwritten: {scenarioName}", Color.green);
            else
                SetStatusText($"Saved new scenario: {scenarioName}", Color.green);
        }

        private void CreateNewScenario()
        {
            scenarioManager.ClearCurrentScenario();

            if (newScenarioNameInput != null)
            {
                newScenarioNameInput.text = $"NewScenario";
            }

            SetStatusText("Ready to create new scenario", Color.blue);
        }

        private Scenario GetSelectedScenario()
        {
            if (scenarioDropdown == null || scenarioManager == null) return null;

            int selectedIndex = scenarioDropdown.value - 1;
            if (selectedIndex >= 0 && selectedIndex < scenarioManager.scenarios.Count)
            {
                return scenarioManager.scenarios[selectedIndex];
            }
            return null;
        }

        private bool ValidateScenario()
        {
            bool isValid = true;

            int txCount = SimulationManager.Instance.transmitters.Count;
            int rxCount = SimulationManager.Instance.receivers.Count;

            if (txCount == 0)
            {
                SetStatusText("No transmitters", Color.red);
                isValid = false;
                return isValid;
            }

            if (rxCount == 0)
            {
                SetStatusText("No receivers", Color.red);
                isValid = false;
                return isValid;
            }

            return isValid;
        }


        #endregion

        #region Events

        private void OnScenariosLoaded(List<string> scenarioNames)
        {
            if (scenarioDropdown == null) return;

            scenarioDropdown.ClearOptions();

            if (scenarioNames.Count == 0)
            {
                scenarioDropdown.options.Add(new Dropdown.OptionData("No scenarios available"));
                scenarioDropdown.interactable = false;
                SetButtonsInteractable(false);
            }
            else
            {
                var options = new List<string>();
                options.Add("-- Select Scenario --");  
                options.AddRange(scenarioNames);
                scenarioDropdown.AddOptions(options);
                scenarioDropdown.interactable = true;
                scenarioDropdown.value = 0;
                ScenarioManager.Instance.currentScenarioIndex = -1;
                SetButtonsInteractable(true);
            }
        }

        private void OnScenarioDropdownChanged(int index)
        {
            if (index <= 0)
            {
                ScenarioManager.Instance.currentScenarioIndex = -1;
                SetStatusText($"No scenario selected.", Color.red);
                return;
            }
            int scenarioIndex = index - 1; 
            if (scenarioIndex < 0 || scenarioIndex >= scenarioManager.scenarios.Count)
                return;

            var s = scenarioManager.scenarios[scenarioIndex];
            SetStatusText($"Selected: {s.scenarioName}", Color.cyan);
        }

        public void OnExportButtonClicked()
        {
            if (ScenarioManager.Instance == null)
            {
                Debug.LogError("[ScenarioPanel] ScenarioManager.Instance is null");
                return;
            }

            string baseName = exportFileNameInput != null
                ? exportFileNameInput.text.Trim()
                : "snapshot";

            ScenarioManager.Instance.ExportSnapshotCsv(baseName);
            SetStatusText($"Exported CSV: {baseName}", Color.green);
        }

        public void OnScreenshotButtonClicked()
        {
            if (ScenarioManager.Instance == null)
            {
                Debug.LogError("[ScenarioPanel] ScenarioManager.Instance is null");
                return;
            }

            string baseName = screenshotFileNameInput != null
                ? screenshotFileNameInput.text.Trim()
                : "screenshot";

            StartCoroutine(ScenarioManager.Instance.CaptureScreenshotCoroutine(baseName));
        }

        #endregion

        #region UI Updates

        private void OnScenarioChanged(string scenarioName)
        {
            if (scenarioDropdown != null)
            {
                int idx = scenarioDropdown.options.FindIndex(o => o.text == scenarioName);
                if (idx >= 0)
                {
                    scenarioDropdown.value = idx;
                    scenarioDropdown.RefreshShownValue();
                }
            }

            SetStatusText($"Current Scenario: {scenarioName}", Color.green);
        }

        private void OnScenarioLoaded(Scenario scenario)
        {
            if (newScenarioNameInput != null && scenario != null)
            {
                newScenarioNameInput.text = scenario.scenarioName;
            }

            SetStatusText($"Scenario Loaded: {scenario.scenarioName}", Color.green);
        }

        private void OnScenarioSaved(Scenario scenario)
        {
            if (newScenarioNameInput != null && scenario != null)
            {
                newScenarioNameInput.text = scenario.scenarioName;
                scenarioDropdown.value = scenarioManager.currentScenarioIndex + 1;
                scenarioDropdown.RefreshShownValue();
            }
            SetStatusText($"Scenario Saved: {scenario.scenarioName}", Color.green);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (loadScenarioButton != null) loadScenarioButton.interactable = interactable;
            if (deleteScenarioButton != null) deleteScenarioButton.interactable = interactable;
        }

        private void SetStatusText(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
        }

        #endregion

    }
}