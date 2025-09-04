using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation;
using RFSimulation.Connections;
using System.IO;
using System.Linq;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Core
{
    [System.Serializable]
    public class Scenario
    {
        [Header("Basic Info")]
        public string scenarioName;

        [Header("Connection Strategy")] // NEW: Strategy per scenario
        public string connectionStrategy = "Best + Interference";

        [Header("Equipment")]
        public List<TransmitterConfig> transmitters;
        public List<ReceiverConfig> receiverPositions; // UPDATED: More detailed receiver config

        [Header("Propagation")]
        public PropagationModel propagationModel;
        public EnvironmentType environmentType;

        [Header("Settings")] // NEW: Scenario-specific settings
        public ScenarioSettings settings = new ScenarioSettings();
    }

    [System.Serializable]
    public class TransmitterConfig
    {
        public Vector3 position;
        public float powerDbm;
        public float antennaGain;
        public float frequencyMHz;

        // NEW: Additional realistic parameters
        public PropagationModel propagationModel = PropagationModel.LogDistance;
        public EnvironmentType environmentType = EnvironmentType.Urban;
    }

    [System.Serializable]
    public class ReceiverConfig // UPDATED: Was just Vector3, now has more config
    {
        public Vector3 position;
        public string technology = "5G"; // NEW: Technology type
        public float sensitivity = -105f; // NEW: Custom sensitivity

        public ReceiverConfig(Vector3 pos)
        {
            position = pos;
        }

        public ReceiverConfig(Vector3 pos, string tech, float sens = -105f)
        {
            position = pos;
            technology = tech;
            sensitivity = sens;
        }
    }

    [System.Serializable]
    public class ScenarioSettings // NEW: Scenario-specific settings
    {
        public float minimumSignalThreshold = -110f;
        public float connectionMargin = 10f;
        public float handoverMargin = 3f;
        public bool enableDebugLogs = false;
        public bool showConnections = true;
        public bool showCoverage = false;
    }

    public class ScenarioManager : MonoBehaviour
    {
        public static ScenarioManager Instance { get; private set; }

        [Header("Scenario Settings")]
        public List<Scenario> scenarios = new List<Scenario>();
        public int currentScenarioIndex = 0;

        [Header("Debug Settings")]
        public bool enableDebugLogs = true;

        [Header("Prefab References")]
        public GameObject transmitterPrefab;
        public GameObject receiverPrefab;

        [Header("Auto-Load Control")]
        public bool autoLoadScenariosOnStart = false;
        public bool autoRunFirstScenario = false;


        // Events for UI updates
        public System.Action<List<string>> OnScenariosLoaded;
        public System.Action<string> OnScenarioChanged;
        public System.Action<Scenario> OnScenarioLoaded; // NEW: More detailed event

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                DebugLog("ScenarioManager Instance created");
            }
            else
            {
                DebugLog("Destroying duplicate ScenarioManager");
                Destroy(gameObject);
            }
        }

        void Start()
        {
            DebugLog("ScenarioManager Start() called");

            // Only load if auto-load is enabled
            if (autoLoadScenariosOnStart)
            {
                LoadAllScenarios();

                if (autoRunFirstScenario && scenarios.Count > 0)
                {
                    RunScenario(currentScenarioIndex);
                }
                else
                {
                    DebugLog("Auto-run disabled or no scenarios found");
                }
            }
            else
            {
                DebugLog("Auto-load scenarios disabled. Use LoadAllScenarios() manually.");
            }
        }

        public void LoadAllScenarios()
        {
            scenarios.Clear();

            string scenarioPath = Application.dataPath + "/Project/Data/Scenarios/";

            // Create directory if it doesn't exist
            if (!Directory.Exists(scenarioPath))
            {
                Directory.CreateDirectory(scenarioPath);
                DebugLog("Created Scenarios directory at: " + scenarioPath);
                return;
            }

            // Load all JSON files in the scenarios folder
            string[] files = Directory.GetFiles(scenarioPath, "*.json");
            DebugLog($"Found {files.Length} JSON files in {scenarioPath}");

            foreach (string filePath in files)
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    DebugLog($"Processing file: {Path.GetFileName(filePath)}");

                    // Try to load as new format first, fallback to old format
                    Scenario scenario = LoadScenarioFromJson(json, filePath);

                    if (scenario != null && !string.IsNullOrEmpty(scenario.scenarioName))
                    {
                        scenarios.Add(scenario);
                        DebugLog($"✅ Loaded scenario: '{scenario.scenarioName}' from {Path.GetFileName(filePath)}");
                    }
                    else
                    {
                        DebugLog($"❌ Failed to parse scenario from {filePath}");
                    }
                }
                catch (System.Exception e)
                {
                    DebugLog($"❌ Failed to load scenario from {filePath}: {e.Message}");
                }
            }

            // Notify UI about loaded scenarios
            List<string> scenarioNames = scenarios.Select(s => s.scenarioName).ToList();
            OnScenariosLoaded?.Invoke(scenarioNames);

            DebugLog($"📁 Final result: Loaded {scenarios.Count} scenarios");
        }

        // NEW: Smart scenario loading with backward compatibility
        private Scenario LoadScenarioFromJson(string json, string filePath)
        {
            try
            {
                // Try new format first
                Scenario scenario = JsonUtility.FromJson<Scenario>(json);

                // Check if we got a valid new format scenario
                if (scenario != null && scenario.transmitters != null && scenario.receiverPositions != null)
                {
                    return scenario;
                }
            }
            catch (System.Exception e)
            {
                DebugLog($"JSON parsing error: {e.Message}");
            }

            return null;
        }

        public void SelectScenario(int index)
        {
            if (index < 0 || index >= scenarios.Count)
            {
                DebugLog($"❌ Invalid scenario index: {index}");
                return;
            }

            currentScenarioIndex = index;
            RunScenario(currentScenarioIndex);
        }

        public void SelectScenarioByName(string scenarioName)
        {
            int index = scenarios.FindIndex(s => s.scenarioName == scenarioName);
            if (index >= 0)
            {
                SelectScenario(index);
            }
            else
            {
                DebugLog($"❌ Scenario not found: {scenarioName}");
            }
        }

        public void RunScenario(int index)
        {
            if (index < 0 || index >= scenarios.Count) return;

            // Clear existing objects
            ClearCurrentScenario();

            Scenario scenario = scenarios[index];
            DebugLog($"▶ Running scenario: {scenario.scenarioName}");

            // NEW: Apply scenario settings to ConnectionManager
            ApplyScenarioSettings(scenario);

            // Create transmitters from scenario
            foreach (var txConfig in scenario.transmitters)
            {
                CreateTransmitterFromConfig(txConfig);
            }

            // Create receivers from scenario (updated for new format)
            foreach (var rxConfig in scenario.receiverPositions)
            {
                CreateReceiverFromConfig(rxConfig);
            }

            // Notify UI about scenario change
            OnScenarioChanged?.Invoke(scenario.scenarioName);
            OnScenarioLoaded?.Invoke(scenario);

            DebugLog($"✅ Scenario loaded: {scenario.transmitters.Count} transmitters, {scenario.receiverPositions.Count} receivers");
        }

        private void ApplyScenarioSettings(Scenario scenario)
        {
            if (SimulationManager.Instance?.connectionManager != null)
            {
                var connectionManager = SimulationManager.Instance.connectionManager;

                // Apply connection strategy
                if (!string.IsNullOrEmpty(scenario.connectionStrategy))
                {
                    connectionManager.SetStrategy(scenario.connectionStrategy);
                }

                // Apply scenario settings
                var settings = connectionManager.GetSettings();
                settings.minimumSignalThreshold = scenario.settings.minimumSignalThreshold;
                settings.connectionMargin = scenario.settings.connectionMargin;
                settings.handoverMargin = scenario.settings.handoverMargin;
                settings.enableDebugLogs = scenario.settings.enableDebugLogs;

                DebugLog($"Applied scenario settings: Strategy={scenario.connectionStrategy}, " +
                        $"Threshold={scenario.settings.minimumSignalThreshold:F1}dBm");
            }
        }

        private void CreateTransmitterFromConfig(TransmitterConfig config)
        {
            if (transmitterPrefab == null)
            {
                DebugLog("❌ Transmitter prefab not assigned!");
                return;
            }

            GameObject txObj = Instantiate(transmitterPrefab, config.position, Quaternion.identity);
            Transmitter transmitter = txObj.GetComponent<Transmitter>();

            if (transmitter != null)
            {
                // Apply configuration
                transmitter.transmitterPower = config.powerDbm;
                transmitter.antennaGain = config.antennaGain;
                transmitter.frequency = config.frequencyMHz;

                // NEW: Apply propagation settings
                transmitter.propagationModel = config.propagationModel;
                transmitter.environmentType = config.environmentType;
            }
        }

        private void CreateReceiverFromConfig(ReceiverConfig config)
        {
            if (receiverPrefab == null)
            {
                DebugLog("❌ Receiver prefab not assigned!");
                return;
            }

            GameObject rxObj = Instantiate(receiverPrefab, config.position, Quaternion.identity);
            Receiver receiver = rxObj.GetComponent<Receiver>();

            if (receiver != null)
            {
                // Apply configuration
                receiver.SetTechnology(config.technology);
                receiver.sensitivity = config.sensitivity;
            }
        }

        private void ClearCurrentScenario()
        {
            // Clear all existing transmitters and receivers
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.ClearAllEquipment();
            }
        }

        public void SaveCurrentScenario(string scenarioName)
        {
            if (SimulationManager.Instance == null) return;

            Scenario newScenario = new Scenario
            {
                scenarioName = scenarioName,
                transmitters = new List<TransmitterConfig>(),
                receiverPositions = new List<ReceiverConfig>(),
                propagationModel = PropagationModel.LogDistance, // Default
                environmentType = EnvironmentType.Urban, // Default
                settings = new ScenarioSettings()
            };

            // NEW: Save current connection strategy
            if (SimulationManager.Instance.connectionManager != null)
            {
                newScenario.connectionStrategy = SimulationManager.Instance.connectionManager.GetCurrentStrategyName();

                var currentSettings = SimulationManager.Instance.connectionManager.GetSettings();
                newScenario.settings.minimumSignalThreshold = currentSettings.minimumSignalThreshold;
                newScenario.settings.connectionMargin = currentSettings.connectionMargin;
                newScenario.settings.handoverMargin = currentSettings.handoverMargin;
                newScenario.settings.enableDebugLogs = currentSettings.enableDebugLogs;
            }

            // Collect transmitter configurations
            foreach (var transmitter in SimulationManager.Instance.transmitters)
            {
                if (transmitter != null)
                {
                    var config = new TransmitterConfig
                    {
                        position = transmitter.transform.position,
                        powerDbm = transmitter.transmitterPower,
                        antennaGain = transmitter.antennaGain,
                        frequencyMHz = transmitter.frequency,
                        propagationModel = transmitter.propagationModel,
                        environmentType = transmitter.environmentType
                    };
                    newScenario.transmitters.Add(config);
                }
            }

            // Collect receiver configurations
            foreach (var receiver in SimulationManager.Instance.receivers)
            {
                if (receiver != null)
                {
                    var config = new ReceiverConfig(
                        receiver.transform.position,
                        receiver.technology,
                        receiver.sensitivity
                    );
                    newScenario.receiverPositions.Add(config);
                }
            }

            // Infer propagation model from first transmitter
            if (newScenario.transmitters.Count > 0)
            {
                newScenario.propagationModel = newScenario.transmitters[0].propagationModel;
                newScenario.environmentType = newScenario.transmitters[0].environmentType;
            }

            // Save to file
            string scenarioPath = Application.dataPath + "/Project/Data/Scenarios/";
            string filePath = scenarioPath + scenarioName + ".json";

            try
            {
                string json = JsonUtility.ToJson(newScenario, true);
                File.WriteAllText(filePath, json);
                DebugLog($"✅ Scenario saved: {filePath}");
                DebugLog($"Strategy: {newScenario.connectionStrategy}, Equipment: {newScenario.transmitters.Count}TX + {newScenario.receiverPositions.Count}RX");

                // Reload scenarios to include the new one
                LoadAllScenarios();
            }
            catch (System.Exception e)
            {
                DebugLog($"❌ Failed to save scenario: {e.Message}");
            }
        }

        // Helper methods
        public List<string> GetScenarioNames()
        {
            return scenarios.Select(s => s.scenarioName).ToList();
        }

        public string GetCurrentScenarioName()
        {
            if (currentScenarioIndex >= 0 && currentScenarioIndex < scenarios.Count)
            {
                return scenarios[currentScenarioIndex].scenarioName;
            }
            return "None";
        }

        public Scenario GetCurrentScenario()
        {
            if (currentScenarioIndex >= 0 && currentScenarioIndex < scenarios.Count)
            {
                return scenarios[currentScenarioIndex];
            }
            return null;
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ScenarioManager] {message}");
            }
        }

        public static Scenario LoadScenario(string fileName)
        {
            string path = Application.dataPath + "/Project/Data/Scenarios/" + fileName + ".json";
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<Scenario>(json);
            }
            else
            {
                Debug.LogError($"❌ Scenario file not found: {path}");
                return null;
            }
        }

        // Context menu methods for testing
        [ContextMenu("Test Load Scenarios")]
        public void TestLoadScenarios()
        {
            LoadAllScenarios();
        }

        [ContextMenu("Print Current Scenario Info")]
        public void PrintCurrentScenarioInfo()
        {
            var scenario = GetCurrentScenario();
            if (scenario != null)
            {
                DebugLog($"Current Scenario: {scenario.scenarioName}");
                DebugLog($"Strategy: {scenario.connectionStrategy}");
                DebugLog($"Equipment: {scenario.transmitters.Count} transmitters, {scenario.receiverPositions.Count} receivers");
                DebugLog($"Propagation: {scenario.propagationModel} in {scenario.environmentType} environment");
            }
            else
            {
                DebugLog("No current scenario");
            }
        }
    }

    // Helper class for backward compatibility with old scenario format
    [System.Serializable]
    internal class OldScenarioFormat
    {
        public string scenarioName;
        public List<TransmitterConfig> transmitters;
        public List<Vector3> receiverPositions; // Old format: just positions
        public PropagationModel propagationModel;
        public EnvironmentType environmentType;
    }
}