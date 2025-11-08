using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation;
using RFSimulation.Core;
using System.IO;
using System.Linq;
using RFSimulation.Propagation.Core;
using RFSimulation.Core.Components;
using System;

namespace RFSimulation.Core.Managers
{
    [System.Serializable]
    public class Scenario
    {
        [Header("Basic Info")]
        public string scenarioName;

        [Header("Equipment")]
        public List<TransmitterConfig> transmitters;
        public List<ReceiverConfig> receiverPositions;

        [Header("Propagation")]
        public PropagationModel propagationModel;

        [Header("Settings")] 
        public ScenarioSettings settings = new ScenarioSettings();
    }

    [System.Serializable]
    public class TransmitterConfig
    {
        public Vector3 position;
        public float powerDbm;
        public float antennaGain;
        public float frequency;
        public float transmitterHeight;

        public PropagationModel propagationModel = PropagationModel.LogDistance;
    }

    [System.Serializable]
    public class ReceiverConfig
    {
        public Vector3 position;
        public string technology;
        public float sensitivity;
        public float receiverHeight;
    }

    [System.Serializable]
    public class ScenarioSettings
    {
        public bool showConnections = true;
    }

    public class ScenarioManager : MonoBehaviour
    {
        public static ScenarioManager Instance { get; private set; }

        [Header("Scenario Settings")]
        public List<Scenario> scenarios = new List<Scenario>();
        public int currentScenarioIndex = 0;

        [Header("Prefab References")]
        public GameObject transmitterPrefab;
        public GameObject receiverPrefab;

        [Header("Auto-Load Control")]
        public bool autoLoadScenariosOnStart = false;
        public bool autoRunFirstScenario = false;


        // Events for UI updates
        public System.Action<List<string>> OnScenariosLoaded;
        public System.Action<string> OnScenarioChanged;
        public System.Action<Scenario> OnScenarioLoaded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {

            // Only load if auto-load is enabled
            if (autoLoadScenariosOnStart)
            {
                LoadAllScenarios();

                if (autoRunFirstScenario && scenarios.Count > 0)
                {
                    RunScenario(currentScenarioIndex);
                }

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
                return;
            }

            // Load all JSON files in the scenarios folder
            string[] files = Directory.GetFiles(scenarioPath, "*.json");

            foreach (string filePath in files)
            {
                try
                {
                    string json = File.ReadAllText(filePath);

                    // Try to load as new format first, fallback to old format
                    Scenario scenario = LoadScenarioFromJson(json, filePath);

                    if (scenario != null && !string.IsNullOrEmpty(scenario.scenarioName))
                    {
                        scenarios.Add(scenario);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"❌ Failed to load scenario from {filePath}: {e.Message}");
                }
            }

            // Notify UI about loaded scenarios
            List<string> scenarioNames = scenarios.Select(s => s.scenarioName).ToList();
            OnScenariosLoaded?.Invoke(scenarioNames);
        }

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
                Debug.LogWarning($"JSON parsing error: {e.Message}");
            }

            return null;
        }

        public void SelectScenario(int index)
        {
            if (index < 0 || index >= scenarios.Count)
            {
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
        }

        public void RunScenario(int index)
        {
            if (index < 0 || index >= scenarios.Count) return;

            // Clear existing objects
            ClearCurrentScenario();

            Scenario scenario = scenarios[index];

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

        }

        private void ApplyScenarioSettings(Scenario scenario)
        {
            if (SimulationManager.Instance?.connectionManager != null)
            {
                var connectionManager = SimulationManager.Instance.connectionManager;

                // Apply scenario settings
                var s = connectionManager.GetSettings();
                connectionManager.ApplySettings(s); 

            }
        }

        private void CreateTransmitterFromConfig(TransmitterConfig config)
        {
            if (transmitterPrefab == null)
            {
                return;
            }

            GameObject txObj = Instantiate(transmitterPrefab, config.position, Quaternion.identity);
            Transmitter transmitter = txObj.GetComponent<Transmitter>();

            if (transmitter != null)
            {
                // Apply configuration
                transmitter.transmitterPower = config.powerDbm;
                transmitter.antennaGain = config.antennaGain;
                transmitter.frequency = config.frequency;

                // NEW: Apply propagation settings
                transmitter.propagationModel = config.propagationModel;

                transmitter.SetTransmitterHeight(Mathf.Max(0f, config.transmitterHeight));
            }
        }

        private void CreateReceiverFromConfig(ReceiverConfig config)
        {
            if (receiverPrefab == null)
            {
                return;
            }

            GameObject rxObj = Instantiate(receiverPrefab, config.position, Quaternion.identity);
            Receiver receiver = rxObj.GetComponent<Receiver>();

            if (receiver != null)
            {
                // Apply configuration
                receiver.SetTechnology(config.technology);
                receiver.sensitivity = config.sensitivity;
                receiver.receiverHeight = config.receiverHeight;
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
                propagationModel = PropagationModel.LogDistance, 
                settings = new ScenarioSettings()
            };

            if (SimulationManager.Instance.connectionManager != null)
            {
                var currentSettings = SimulationManager.Instance.connectionManager.GetSettings();
            }

            // Collect transmitter configurations
            foreach (var transmitter in SimulationManager.Instance.transmitters)
            {
                if (transmitter != null)
                {
                    var pos = transmitter.transform.position;

                    var config = new TransmitterConfig
                    {
                        position = new Vector3(
                            (float)Math.Round(pos.x, 2),
                            (float)Math.Round(pos.y, 2),
                            (float)Math.Round(pos.z, 2)
                        ),

                        powerDbm = (float)Math.Round(transmitter.transmitterPower, 2),
                        antennaGain = (float)Math.Round(transmitter.antennaGain, 2),
                        frequency = (float)Math.Round(transmitter.frequency, 2),
                        transmitterHeight = (float)Math.Round(transmitter.transmitterHeight, 2),

                        propagationModel = transmitter.propagationModel
                    };
                    newScenario.transmitters.Add(config);
                }
            }

            // Collect receiver configurations
            foreach (var receiver in SimulationManager.Instance.receivers)
            {
                if (receiver != null)
                {
                    var pos = receiver.transform.position;

                    var config = new ReceiverConfig
                    {
                        position = new Vector3(
                            (float)Math.Round(pos.x, 2),
                            (float)Math.Round(pos.y, 2),
                            (float)Math.Round(pos.z, 2)
                        ),

                        technology = receiver.technology,
                        sensitivity = (float)Math.Round(receiver.sensitivity, 2),
                        receiverHeight = (float)Math.Round(receiver.receiverHeight, 2)
                    };
                    newScenario.receiverPositions.Add(config);
                }
            }

            // Infer propagation model from first transmitter
            if (newScenario.transmitters.Count > 0)
            {
                newScenario.propagationModel = newScenario.transmitters[0].propagationModel;
            }

            // Save to file
            string scenarioPath = Application.dataPath + "/Project/Data/Scenarios/";
            string filePath = scenarioPath + scenarioName + ".json";

            try
            {
                string json = JsonUtility.ToJson(newScenario, true);
                File.WriteAllText(filePath, json);

                // Reload scenarios to include the new one
                LoadAllScenarios();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"❌ Failed to save scenario: {e.Message}");
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
    }


    // Helper class for backward compatibility with old scenario format
    [System.Serializable]
    internal class OldScenarioFormat
    {
        public string scenarioName;
        public List<TransmitterConfig> transmitters;
        public List<Vector3> receiverPositions; // Old format: just positions
        public PropagationModel propagationModel;
    }
}