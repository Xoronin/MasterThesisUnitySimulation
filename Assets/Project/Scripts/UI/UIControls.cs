using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RFSimulation.Core;
using RFSimulation.Connections;
using System.Collections.Generic;

public class UIControls : MonoBehaviour
{
    [Header("Scenario Controls")]
    public Dropdown scenarioDropdown;
    public Button loadScenarioButton;
    public Button saveScenarioButton;
    public InputField saveScenarioNameInput;
    public Button refreshScenariosButton;
    public Text currentScenarioText;

    [Header("Connection Strategy Controls")] 
    public Dropdown strategyDropdown;
    public Text strategyDescriptionText;
    public Slider signalThresholdSlider;
    public Text signalThresholdText;
    public Toggle debugLogsToggle;

    [Header("Object Placement Controls")]
    public Button addTransmitterButton;
    public Button addReceiverButton;
    public Button removeAllButton;
    public Button pauseResumeButton;

    [Header("Parameter Inputs")]
    public InputField transmitterPowerInput;
    public InputField transmitterFrequencyInput;
    public InputField transmitterCoverageInput;
    public Dropdown receiverTechnologyDropdown; 
    public InputField receiverSensitivityInput;

    [Header("Toggle Controls")]
    public Toggle showConnectionsToggle;
    public Toggle showGridToggle;
    public Toggle showCoverageToggle; 

    [Header("Status and Speed")]
    public Text statusText;
    public Text connectionStatsText; 
    public Slider speedSlider;
    public Text speedText;

    [Header("Prefab References")]
    public GameObject transmitterPrefab;
    public GameObject receiverPrefab;

    [Header("Placement Settings")]
    public LayerMask placementLayerMask = 1;
    public float transmitterHeight = 15f;
    public float receiverHeight = 1f;

    [Header("Ground Settings")]
    public float groundLevel = 0f;
    public LayerMask groundLayerMask = 1;

    [Header("Grid Settings")]
    public bool enableGridSnap = true;
    public GroundGrid groundGridComponent;

    private Camera mainCamera;
    private bool isPlacingTransmitter = false;
    private bool isPlacingReceiver = false;
    private bool isPaused = false;

    // NEW: References to managers
    private ConnectionManager connectionManager;

    void Start()
    {
        mainCamera = Camera.main;
        SetupManagerReferences();
        SetupScenarioManager();
        CreateUI();
        InitializeUIFromPrefabs();
    }

    // NEW: Get references to the new manager systems
    private void SetupManagerReferences()
    {
        if (SimulationManager.Instance != null)
        {
            connectionManager = SimulationManager.Instance.connectionManager;
        }
    }

    private void SetupScenarioManager()
    {
        // Subscribe to scenario manager events
        if (ScenarioManager.Instance != null)
        {
            ScenarioManager.Instance.OnScenariosLoaded += UpdateScenarioDropdown;
            ScenarioManager.Instance.OnScenarioChanged += OnScenarioChanged;
            ScenarioManager.Instance.OnScenarioLoaded += OnScenarioLoaded; // NEW: Detailed scenario event
        }

        // NEW: Subscribe to connection manager events
        if (connectionManager != null)
        {
            connectionManager.OnStrategyChanged += OnStrategyChanged;
            connectionManager.OnConnectionsUpdated += OnConnectionsUpdated;
        }
    }

    void CreateUI()
    {
        // Scenario controls
        if (scenarioDropdown != null)
            scenarioDropdown.onValueChanged.AddListener(OnScenarioDropdownChanged);

        if (loadScenarioButton != null)
            loadScenarioButton.onClick.AddListener(LoadSelectedScenario);

        if (saveScenarioButton != null)
            saveScenarioButton.onClick.AddListener(SaveCurrentScenario);

        if (refreshScenariosButton != null)
            refreshScenariosButton.onClick.AddListener(RefreshScenarios);

        // NEW: Strategy controls
        if (strategyDropdown != null)
            strategyDropdown.onValueChanged.AddListener(OnStrategyDropdownChanged);

        if (signalThresholdSlider != null)
            signalThresholdSlider.onValueChanged.AddListener(OnSignalThresholdChanged);

        if (debugLogsToggle != null)
            debugLogsToggle.onValueChanged.AddListener(OnDebugLogsToggled);

        // Object placement controls
        if (addTransmitterButton != null)
            addTransmitterButton.onClick.AddListener(StartPlacingTransmitter);

        if (addReceiverButton != null)
            addReceiverButton.onClick.AddListener(StartPlacingReceiver);

        if (removeAllButton != null)
            removeAllButton.onClick.AddListener(RemoveAllObjects);

        if (pauseResumeButton != null)
            pauseResumeButton.onClick.AddListener(TogglePauseResume);

        // Toggle controls
        if (showConnectionsToggle != null)
            showConnectionsToggle.onValueChanged.AddListener(ToggleConnections);

        if (showCoverageToggle != null) // NEW
            showCoverageToggle.onValueChanged.AddListener(ToggleCoverage);

        if (speedSlider != null)
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);

        if (showGridToggle != null)
            showGridToggle.onValueChanged.AddListener(ToggleGrid);

        // Auto-find GroundGrid if not assigned
        if (groundGridComponent == null)
        {
            groundGridComponent = FindFirstObjectByType<GroundGrid>();
        }

        // NEW: Initialize strategy dropdown
        InitializeStrategyDropdown();

        // NEW: Initialize receiver technology dropdown
        InitializeReceiverTechnologyDropdown();

        // Set default values
        SetDefaultUIValues();

        UpdateStatusText("Ready to place objects");
        UpdateSpeedText(1f);
    }

    // NEW: Initialize strategy dropdown with available strategies
    private void InitializeStrategyDropdown()
    {
        if (strategyDropdown != null && connectionManager != null)
        {
            var strategies = connectionManager.GetAvailableStrategies();
            strategyDropdown.ClearOptions();
            strategyDropdown.AddOptions(strategies);

            // Set current strategy
            string currentStrategy = connectionManager.GetCurrentStrategyName();
            for (int i = 0; i < strategies.Count; i++)
            {
                if (strategies[i] == currentStrategy)
                {
                    strategyDropdown.value = i;
                    break;
                }
            }

            UpdateStrategyDescription();
        }
    }

    private void InitializeReceiverTechnologyDropdown()
    {
        if (receiverTechnologyDropdown != null)
        {
            var technologies = new List<string> { "5G", "LTE", "WiFi", "WiFi6", "IoT", "Emergency" };
            receiverTechnologyDropdown.ClearOptions();
            receiverTechnologyDropdown.AddOptions(technologies);
            receiverTechnologyDropdown.value = 0; // Default to 5G
        }
    }

    private void SetDefaultUIValues()
    {
        if (showConnectionsToggle != null) showConnectionsToggle.isOn = true;
        if (showGridToggle != null) showGridToggle.isOn = true;
        if (showCoverageToggle != null) showCoverageToggle.isOn = false;
        if (speedSlider != null) speedSlider.value = 1f;
        if (saveScenarioNameInput != null) saveScenarioNameInput.text = "New Scenario";

        // NEW: Set signal threshold slider
        if (signalThresholdSlider != null)
        {
            signalThresholdSlider.minValue = -140f;
            signalThresholdSlider.maxValue = -50f;
            signalThresholdSlider.value = -110f;
        }
    }

    private void Update()
    {
        HandlePlacement();

        if (Time.frameCount % 60 == 0) // Every 60 frames
        {
            UpdateConnectionStatistics();
        }
    }

    #region Scenario Management

    private void UpdateScenarioDropdown(List<string> scenarioNames)
    {
        if (scenarioDropdown == null) return;

        scenarioDropdown.ClearOptions();

        if (scenarioNames.Count == 0)
        {
            scenarioDropdown.options.Add(new Dropdown.OptionData("No scenarios found"));
            scenarioDropdown.interactable = false;
            if (loadScenarioButton != null) loadScenarioButton.interactable = false;
        }
        else
        {
            scenarioDropdown.AddOptions(scenarioNames);
            scenarioDropdown.interactable = true;
            if (loadScenarioButton != null) loadScenarioButton.interactable = true;

            // Set to current scenario if available
            if (ScenarioManager.Instance != null)
            {
                int currentIndex = ScenarioManager.Instance.currentScenarioIndex;
                if (currentIndex >= 0 && currentIndex < scenarioNames.Count)
                {
                    scenarioDropdown.value = currentIndex;
                }
            }
        }

        UpdateStatusText($"Found {scenarioNames.Count} scenarios");
    }

    private void OnScenarioDropdownChanged(int index)
    {
        UpdateCurrentScenarioText();
    }

    private void LoadSelectedScenario()
    {
        if (scenarioDropdown == null || ScenarioManager.Instance == null) return;

        int selectedIndex = scenarioDropdown.value;
        ScenarioManager.Instance.SelectScenario(selectedIndex);
        UpdateStatusText($"Loaded scenario: {scenarioDropdown.options[selectedIndex].text}");
    }

    private void SaveCurrentScenario()
    {
        if (saveScenarioNameInput == null || ScenarioManager.Instance == null) return;

        string scenarioName = saveScenarioNameInput.text.Trim();
        if (string.IsNullOrEmpty(scenarioName))
        {
            UpdateStatusText("❌ Please enter a scenario name");
            return;
        }

        ScenarioManager.Instance.SaveCurrentScenario(scenarioName);
        UpdateStatusText($"Saved scenario: {scenarioName}");
    }

    private void RefreshScenarios()
    {
        if (ScenarioManager.Instance != null)
        {
            ScenarioManager.Instance.LoadAllScenarios();
            UpdateStatusText("Refreshed scenario list");
        }
    }

    private void OnScenarioChanged(string scenarioName)
    {
        UpdateCurrentScenarioText();
        UpdateStatusText($"Scenario changed to: {scenarioName}");
    }

    // NEW: Handle detailed scenario loading
    private void OnScenarioLoaded(Scenario scenario)
    {
        // Update UI to match scenario settings
        if (scenario.settings != null)
        {
            if (signalThresholdSlider != null)
                signalThresholdSlider.value = scenario.settings.minimumSignalThreshold;

            if (debugLogsToggle != null)
                debugLogsToggle.isOn = scenario.settings.enableDebugLogs;

            if (showConnectionsToggle != null)
                showConnectionsToggle.isOn = scenario.settings.showConnections;
        }

        // Update strategy dropdown
        if (strategyDropdown != null && !string.IsNullOrEmpty(scenario.strategyName))
        {
            var strategies = connectionManager?.GetAvailableStrategies();
            if (strategies != null)
            {
                int strategyIndex = strategies.IndexOf(scenario.strategyName);
                if (strategyIndex >= 0)
                {
                    strategyDropdown.value = strategyIndex;
                }
            }
        }
    }

    private void UpdateCurrentScenarioText()
    {
        if (currentScenarioText == null || ScenarioManager.Instance == null) return;

        string currentScenario = ScenarioManager.Instance.GetCurrentScenarioName();
        currentScenarioText.text = $"Current: {currentScenario}";
    }

    #endregion

    #region NEW: Strategy Management

    private void OnStrategyDropdownChanged(int index)
    {
        if (connectionManager != null && strategyDropdown != null)
        {
            var strategies = connectionManager.GetAvailableStrategies();
            if (index >= 0 && index < strategies.Count)
            {
                connectionManager.SetConnectionStrategy((StrategyType)index);
                UpdateStrategyDescription();
            }
        }
    }

    private void OnStrategyChanged(string strategyName)
    {
        UpdateStatusText($"Strategy: {strategyName}");
        UpdateStrategyDescription();
    }

    private void UpdateStrategyDescription()
    {
        if (strategyDescriptionText != null && connectionManager != null)
        {
            string description = connectionManager.GetCurrentStrategyDescription();
            strategyDescriptionText.text = description;
        }
    }

    private void OnSignalThresholdChanged(float value)
    {
        if (connectionManager != null)
        {
            connectionManager.UpdateMinimumSignalThreshold(value);
        }

        if (signalThresholdText != null)
        {
            signalThresholdText.text = $"Signal Threshold: {value:F1} dBm";
        }
    }

    private void OnDebugLogsToggled(bool enabled)
    {
        if (connectionManager != null)
        {
            connectionManager.ToggleDebugLogs(enabled);
        }

        UpdateStatusText($"Debug logs {(enabled ? "enabled" : "disabled")}");
    }

    private void OnConnectionsUpdated(int connectedReceivers, int totalReceivers)
    {
        // This gets called when connections change - could update UI here
    }

    private void UpdateConnectionStatistics()
    {
        if (connectionStatsText == null || connectionManager == null) return;

        var stats = connectionManager.GetConnectionStatistics();

        string statsText = $"Connected: {stats.GetValueOrDefault("connectedReceivers", 0)}/{stats.GetValueOrDefault("totalReceivers", 0)} " +
                          $"({stats.GetValueOrDefault("connectionPercentage", 0f):F1}%)\n" +
                          $"Avg Signal: {stats.GetValueOrDefault("averageSignalStrength", 0f):F1} dBm\n" +
                          $"Strategy: {stats.GetValueOrDefault("currentStrategy", "None")}";

        connectionStatsText.text = statsText;
    }

    #endregion

    #region Object Placement (existing functionality with minor updates)

    private void InitializeUIFromPrefabs()
    {
        // Get values from transmitter prefab
        if (transmitterPrefab != null)
        {
            Transmitter prefabTransmitter = transmitterPrefab.GetComponent<Transmitter>();
            if (prefabTransmitter != null)
            {
                if (transmitterPowerInput != null)
                    transmitterPowerInput.text = prefabTransmitter.transmitterPower.ToString();
                if (transmitterFrequencyInput != null)
                    transmitterFrequencyInput.text = prefabTransmitter.frequency.ToString();
            }
        }

        // Get values from receiver prefab
        if (receiverPrefab != null)
        {
            Receiver prefabReceiver = receiverPrefab.GetComponent<Receiver>();
            if (prefabReceiver != null)
            {
                if (receiverSensitivityInput != null)
                    receiverSensitivityInput.text = prefabReceiver.sensitivity.ToString();

                if (receiverTechnologyDropdown != null)
                {
                    var options = receiverTechnologyDropdown.options;
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (options[i].text.ToLower() == prefabReceiver.technology.ToLower())
                        {
                            receiverTechnologyDropdown.value = i;
                            break;
                        }
                    }
                }
            }
        }

        UpdateStatusText("UI initialized with prefab values");
    }

    private void HandlePlacement()
    {
        if (isPlacingTransmitter || isPlacingReceiver)
        {
            if (Input.GetMouseButtonDown(0))
            {
                PlaceObjectAtMousePosition();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
            }
        }
    }

    public void StartPlacingTransmitter()
    {
        isPlacingTransmitter = true;
        isPlacingReceiver = false;
        UpdateStatusText("Click to place transmitter (ESC to cancel)");
    }

    public void StartPlacingReceiver()
    {
        isPlacingReceiver = true;
        isPlacingTransmitter = false;
        UpdateStatusText("Click to place receiver (ESC to cancel)");
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        if (!enableGridSnap) return position;

        if (groundGridComponent != null)
        {
            return groundGridComponent.SnapToGrid(position);
        }

        Debug.LogWarning("No GroundGrid component found for snapping!");
        return position;
    }

    private void PlaceObjectAtMousePosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Vector3 placementPosition;
        bool foundPosition = false;

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayerMask))
        {
            placementPosition = hit.point;
            foundPosition = true;
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevel, 0));
            float distance;

            if (groundPlane.Raycast(ray, out distance))
            {
                placementPosition = ray.GetPoint(distance);
                foundPosition = true;
            }
            else
            {
                Vector3 rayDir = ray.direction;
                if (Mathf.Abs(rayDir.y) > 0.001f)
                {
                    float t = (groundLevel - ray.origin.y) / rayDir.y;
                    placementPosition = ray.origin + rayDir * t;
                    foundPosition = true;
                }
                else
                {
                    Debug.LogWarning("Could not determine placement position");
                    return;
                }
            }
        }

        if (!foundPosition) return;

        if (enableGridSnap)
        {
            placementPosition = SnapToGrid(placementPosition);
        }

        if (isPlacingTransmitter)
        {
            PlaceTransmitter(placementPosition);
        }
        else if (isPlacingReceiver)
        {
            PlaceReceiver(placementPosition);
        }

        CancelPlacement();
    }

    private void PlaceTransmitter(Vector3 position)
    {
        position.y += transmitterHeight;

        GameObject newTransmitter = Instantiate(transmitterPrefab, position, Quaternion.identity);
        Transmitter transmitterComponent = newTransmitter.GetComponent<Transmitter>();

        if (transmitterComponent != null)
        {
            // Apply UI settings
            if (transmitterPowerInput != null && float.TryParse(transmitterPowerInput.text, out float power))
                transmitterComponent.transmitterPower = power;

            if (transmitterFrequencyInput != null && float.TryParse(transmitterFrequencyInput.text, out float freq))
                transmitterComponent.frequency = freq;

            transmitterComponent.showConnections = showConnectionsToggle != null ? showConnectionsToggle.isOn : true;
            transmitterComponent.showCoverageArea = showCoverageToggle != null ? showCoverageToggle.isOn : false;

            UpdateStatusText($"Transmitter placed: {transmitterComponent.transmitterPower:F1}dBm, {transmitterComponent.frequency:F0}MHz at {position}");
        }
    }

    private void PlaceReceiver(Vector3 position)
    {
        position.y += receiverHeight;

        GameObject newReceiver = Instantiate(receiverPrefab, position, Quaternion.identity);
        Receiver receiverComponent = newReceiver.GetComponent<Receiver>();

        if (receiverComponent != null)
        {
            // Apply UI settings
            if (receiverSensitivityInput != null && float.TryParse(receiverSensitivityInput.text, out float sensitivity))
                receiverComponent.sensitivity = sensitivity;

            // NEW: Apply selected technology
            if (receiverTechnologyDropdown != null)
            {
                string selectedTech = receiverTechnologyDropdown.options[receiverTechnologyDropdown.value].text;
                receiverComponent.SetTechnology(selectedTech);
            }

            UpdateStatusText($"Receiver placed: {receiverComponent.technology}, {receiverComponent.sensitivity:F1}dBm at {position}");
        }
    }

    private void CancelPlacement()
    {
        isPlacingTransmitter = false;
        isPlacingReceiver = false;
        UpdateStatusText("Ready to place objects");
    }

    public void RemoveAllObjects()
    {
        if (SimulationManager.Instance != null)
        {
            SimulationManager.Instance.ClearAllEquipment();
        }

        UpdateStatusText("All objects removed");
    }

    #endregion

    #region UI Controls

    public void ToggleConnections(bool enabled)
    {
        if (SimulationManager.Instance != null)
        {
            foreach (var transmitter in SimulationManager.Instance.transmitters)
            {
                if (transmitter != null)
                {
                    transmitter.ToggleConnections(enabled);
                }
            }
        }

        UpdateStatusText($"Connections {(enabled ? "enabled" : "disabled")}");
    }

    // NEW: Toggle coverage areas
    public void ToggleCoverage(bool enabled)
    {
        if (SimulationManager.Instance != null)
        {
            foreach (var transmitter in SimulationManager.Instance.transmitters)
            {
                if (transmitter != null)
                {
                    transmitter.ToggleCoverageArea(enabled);
                }
            }
        }

        UpdateStatusText($"Coverage areas {(enabled ? "enabled" : "disabled")}");
    }

    public void TogglePauseResume()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.PauseSimulation();

            if (pauseResumeButton != null)
                pauseResumeButton.GetComponentInChildren<Text>().text = "Resume";
            UpdateStatusText("Simulation paused");
        }
        else
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.ResumeSimulation();

            if (pauseResumeButton != null)
                pauseResumeButton.GetComponentInChildren<Text>().text = "Pause";
            UpdateStatusText("Simulation resumed");
        }
    }

    void ToggleGrid(bool show)
    {
        GroundGrid grid = FindFirstObjectByType<GroundGrid>();
        if (grid != null)
        {
            grid.gameObject.SetActive(show);
            UpdateStatusText($"Grid {(show ? "enabled" : "disabled")}");
        }
    }

    void OnSpeedChanged(float value)
    {
        if (connectionManager != null)
        {
            connectionManager.updateInterval = 0.1f / value; // Adjust update interval
        }
        UpdateSpeedText(value);
    }

    void UpdateSpeedText(float speed)
    {
        if (speedText != null)
            speedText.text = $"Speed: {speed:F1}x";
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"UI: {message}");
    }

    #endregion

    #region NEW: Advanced Features

    // Method to update all UI elements when scenario changes
    public void RefreshAllUI()
    {
        InitializeStrategyDropdown();
        UpdateCurrentScenarioText();
        UpdateConnectionStatistics();
        UpdateStrategyDescription();
    }

    // Method to apply settings from a scenario
    public void ApplyScenarioToUI(Scenario scenario)
    {
        if (scenario?.settings != null)
        {
            if (signalThresholdSlider != null)
                signalThresholdSlider.value = scenario.settings.minimumSignalThreshold;

            if (debugLogsToggle != null)
                debugLogsToggle.isOn = scenario.settings.enableDebugLogs;

            if (showConnectionsToggle != null)
                showConnectionsToggle.isOn = scenario.settings.showConnections;

            if (showCoverageToggle != null)
                showCoverageToggle.isOn = scenario.settings.showCoverage;
        }
    }

    // Method to get current UI settings for saving
    public ScenarioSettings GetCurrentUISettings()
    {
        var settings = new ScenarioSettings();

        if (signalThresholdSlider != null)
            settings.minimumSignalThreshold = signalThresholdSlider.value;

        if (debugLogsToggle != null)
            settings.enableDebugLogs = debugLogsToggle.isOn;

        if (showConnectionsToggle != null)
            settings.showConnections = showConnectionsToggle.isOn;

        if (showCoverageToggle != null)
            settings.showCoverage = showCoverageToggle.isOn;

        return settings;
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ScenarioManager.Instance != null)
        {
            ScenarioManager.Instance.OnScenariosLoaded -= UpdateScenarioDropdown;
            ScenarioManager.Instance.OnScenarioChanged -= OnScenarioChanged;
            ScenarioManager.Instance.OnScenarioLoaded -= OnScenarioLoaded;
        }

        if (connectionManager != null)
        {
            connectionManager.OnStrategyChanged -= OnStrategyChanged;
            connectionManager.OnConnectionsUpdated -= OnConnectionsUpdated;
        }
    }
}