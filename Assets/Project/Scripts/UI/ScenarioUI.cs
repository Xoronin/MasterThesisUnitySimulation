using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using RFSimulation.Core.Managers;
using RFSimulation.Propagation;
using RFSimulation.Core.Connections;
using RFSimulation.Core.Components;
using RFSimulation.Propagation.Core;

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
        public Text currentScenarioText;

        [Header("Scenario Creation")]
        public InputField newScenarioNameInput;
        public Button saveScenarioButton;
        public Button newScenarioButton;

        [Header("Scenario Details")]
        public Text scenarioDescriptionText;
        public Text equipmentCountText;

        [Header("Validation")]
        public Text validationStatusText;
        public Button validateButton;

        // Events
        public System.Action<string> OnScenarioSelected;
        public System.Action<Scenario> OnScenarioValidated;

        private ScenarioManager scenarioManager;
        private ConnectionManager connectionManager;
        private List<Scenario> availableScenarios = new List<Scenario>();

        void Start()
        {
            Initialize();
            SetupEventListeners();
            RefreshScenarioList();
        }

        private void Initialize()
        {
            scenarioManager = ScenarioManager.Instance;
            if (SimulationManager.Instance != null)
            {
                connectionManager = SimulationManager.Instance.connectionManager;
            }
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

            // Quick actions
            if (validateButton != null)
                validateButton.onClick.AddListener(ValidateCurrentScenario);

            // Subscribe to scenario manager events
            if (scenarioManager != null)
            {
                scenarioManager.OnScenariosLoaded += OnScenariosLoaded;
                scenarioManager.OnScenarioChanged += OnScenarioChanged;
                scenarioManager.OnScenarioLoaded += OnScenarioLoaded;
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
                scenarioDropdown.AddOptions(scenarioNames);
                scenarioDropdown.interactable = true;
                SetButtonsInteractable(true);
            }

            UpdateScenarioDetails();
        }

        private void OnScenarioDropdownChanged(int index)
        {
            UpdateScenarioDetails();
        }

        private void LoadSelectedScenario()
        {
            if (scenarioDropdown == null || scenarioManager == null) return;

            int selectedIndex = scenarioDropdown.value;
            if (selectedIndex >= 0 && selectedIndex < scenarioManager.scenarios.Count)
            {
                scenarioManager.SelectScenario(selectedIndex);
                OnScenarioSelected?.Invoke(scenarioManager.scenarios[selectedIndex].scenarioName);
            }
        }

        private void DeleteSelectedScenario()
        {
            if (scenarioDropdown == null || scenarioManager == null) return;

            int selectedIndex = scenarioDropdown.value;
            if (selectedIndex >= 0 && selectedIndex < scenarioManager.scenarios.Count)
            {
                string scenarioName = scenarioManager.scenarios[selectedIndex].scenarioName;

                // Show confirmation dialog (simplified)
                if (ConfirmDeletion(scenarioName))
                {
                    DeleteScenarioFile(scenarioName);
                    RefreshScenarioList();
                }
            }
        }

        private void SaveCurrentScenario()
        {
            if (newScenarioNameInput == null || scenarioManager == null) return;

            string scenarioName = newScenarioNameInput.text.Trim();
            if (string.IsNullOrEmpty(scenarioName))
            {
                SetValidationStatus("❌ Please enter a scenario name", Color.red);
                return;
            }

            scenarioManager.SaveCurrentScenario(scenarioName);
            SetValidationStatus($"✅ Saved: {scenarioName}", Color.green);
            RefreshScenarioList();
        }

        private void CreateNewScenario()
        {
            // Clear current scenario
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.ClearAllEquipment();
            }

            if (newScenarioNameInput != null)
            {
                newScenarioNameInput.text = "New Scenario";
            }

            SetValidationStatus("Ready to create new scenario", Color.blue);
        }

        #endregion

        #region UI Updates

        private void OnScenarioChanged(string scenarioName)
        {
            UpdateCurrentScenarioDisplay(scenarioName);
            UpdateScenarioDetails();
        }

        private void OnScenarioLoaded(Scenario scenario)
        {
            UpdateScenarioDetails();
        }

        private void UpdateCurrentScenarioDisplay(string scenarioName)
        {
            if (currentScenarioText != null)
            {
                currentScenarioText.text = $"Current: {scenarioName}";
            }
        }

        private void UpdateScenarioDetails()
        {
            var currentScenario = GetSelectedScenario();
            if (currentScenario == null)
            {
                ClearScenarioDetails();
                return;
            }

            // Update description
            if (scenarioDescriptionText != null)
            {
                string description = CreateScenarioDescription(currentScenario);
                scenarioDescriptionText.text = description;
            }

            // Update equipment count
            if (equipmentCountText != null)
            {
                equipmentCountText.text = $"Equipment: {currentScenario.transmitters.Count} TX, {currentScenario.receiverPositions.Count} RX";
            }
        }

        private void ClearScenarioDetails()
        {
            if (scenarioDescriptionText != null)
                scenarioDescriptionText.text = "No scenario selected";

            if (equipmentCountText != null)
                equipmentCountText.text = "Equipment: 0 TX, 0 RX";
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (loadScenarioButton != null) loadScenarioButton.interactable = interactable;
            if (deleteScenarioButton != null) deleteScenarioButton.interactable = interactable;
        }

        private void SetValidationStatus(string message, Color color)
        {
            if (validationStatusText != null)
            {
                validationStatusText.text = message;
                validationStatusText.color = color;
            }
            Debug.Log($"[ScenarioUI] {message}");
        }

        #endregion

        #region Helper Methods

        private Scenario GetSelectedScenario()
        {
            if (scenarioDropdown == null || scenarioManager == null) return null;

            int selectedIndex = scenarioDropdown.value;
            if (selectedIndex >= 0 && selectedIndex < scenarioManager.scenarios.Count)
            {
                return scenarioManager.scenarios[selectedIndex];
            }
            return null;
        }

        private string CreateScenarioDescription(Scenario scenario)
        {
            var description = new System.Text.StringBuilder();

            description.AppendLine($"Name: {scenario.scenarioName}");
            description.AppendLine($"Propagation: {scenario.propagationModel}");

            if (scenario.settings != null)
            {
                description.AppendLine($"Connection Margin: {scenario.settings.connectionMargin:F1} dB");
            }

            return description.ToString().Trim();
        }

        private bool ConfirmDeletion(string scenarioName)
        {
            // In a real implementation, you'd show a proper confirmation dialog
            // For now, we'll just return true (or you could use Debug.Log and return false for safety)
            Debug.Log($"Deleting scenario: {scenarioName}");
            return true; // Set to false for safety during development
        }

        private void DeleteScenarioFile(string scenarioName)
        {
            string filePath = Application.dataPath + "/Project/Data/Scenarios/" + scenarioName + ".json";
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                    SetValidationStatus($"✅ Deleted: {scenarioName}", Color.green);
                }
                catch (System.Exception e)
                {
                    SetValidationStatus($"❌ Failed to delete: {e.Message}", Color.red);
                }
            }
        }

        private void CreateScenarioCopy(Scenario original, string newName)
        {
            // This would create a copy of the scenario
            // Implementation would depend on your specific needs
            SetValidationStatus($"📋 Scenario copied to: {newName}", Color.blue);
        }

        private void ValidateCurrentScenario()
        {
            var scenario = GetSelectedScenario();
            if (scenario == null)
            {
                SetValidationStatus("❌ No scenario selected", Color.red);
                return;
            }

            // Validate scenario
            var issues = ValidateScenario(scenario);
            if (issues.Count == 0)
            {
                SetValidationStatus("✅ Scenario is valid", Color.green);
            }
            else
            {
                string issueText = string.Join(", ", issues);
                SetValidationStatus($"⚠️ Issues: {issueText}", Color.yellow);
            }

            OnScenarioValidated?.Invoke(scenario);
        }

        private List<string> ValidateScenario(Scenario scenario)
        {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(scenario.scenarioName))
                issues.Add("No name");

            if (scenario.transmitters == null || scenario.transmitters.Count == 0)
                issues.Add("No transmitters");

            if (scenario.receiverPositions == null || scenario.receiverPositions.Count == 0)
                issues.Add("No receivers");

            // Check for realistic values
            foreach (var tx in scenario.transmitters)
            {
                if (tx.powerDbm <= 0 || tx.powerDbm > 80)
                    issues.Add($"Invalid TX power: {tx.powerDbm}dBm");

                if (tx.frequencyMHz <= 0)
                    issues.Add($"Invalid frequency: {tx.frequencyMHz}MHz");
            }

            return issues;
        }

        #endregion

        void OnDestroy()
        {
            // Unsubscribe from events
            if (scenarioManager != null)
            {
                scenarioManager.OnScenariosLoaded -= OnScenariosLoaded;
                scenarioManager.OnScenarioChanged -= OnScenarioChanged;
                scenarioManager.OnScenarioLoaded -= OnScenarioLoaded;
            }
        }
    }
}