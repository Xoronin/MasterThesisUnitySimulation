using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Propagation;
using RFSimulation.Core;
using System.IO;
using System.Linq;
using RFSimulation.Propagation.Core;
using RFSimulation.Core.Components;
using System;
using RFSimulation.Utils;

namespace RFSimulation.Core.Managers
{
    [System.Serializable]
    public class Scenario
    {
        [Header("Basic Info")]
        public string scenarioName;
        public int id;

        [Header("Equipment")]
        public List<TransmitterConfig> transmitters;
        public List<ReceiverConfig> receivers;

        [Header("Propagation")]
        public PropagationModel propagationModel;
        public TechnologyType technology;
    }

    [System.Serializable]
    public class TransmitterConfig
    {
        public Vector3 position;
        public float powerDbm;
        public float antennaGain;
        public float frequency;
        public float transmitterHeight;
        public float maxReflections;
        public float maxDiffractions;

        public PropagationModel propagationModel;
    }

    [System.Serializable]
    public class ReceiverConfig
    {
        public Vector3 position;
        public float sensitivity;
        public float receiverHeight;
        public float connectionMargin;
        public TechnologyType technology;
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

        public string scenariosfolder = "Project/Data/Scenarios";


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
            LoadAllScenarios();
        }

        public void LoadAllScenarios()
        {
            scenarios.Clear();

            string scenarioPath = System.IO.Path.Combine(Application.dataPath, scenariosfolder);

            if (!Directory.Exists(scenarioPath))
            {
                Directory.CreateDirectory(scenarioPath);
                return;
            }

            string[] files = Directory.GetFiles(scenarioPath, "*.json");

            foreach (string filePath in files)
            {
                try
                {
                    string json = File.ReadAllText(filePath);

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

            List<string> scenarioNames = scenarios.Select(s => s.scenarioName).ToList();
            OnScenariosLoaded?.Invoke(scenarioNames);
        }

        private Scenario LoadScenarioFromJson(string json, string filePath)
        {
            try
            {
                Scenario scenario = JsonUtility.FromJson<Scenario>(json);

                if (scenario != null && scenario.transmitters != null && scenario.receivers != null)
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

        public int GetScenarioIndex(int id)
        {
            for (int i = 0; i < scenarios.Count; i++)
            {
                if (scenarios[i].id == id)
                {
                    return i;
                }
            }
            return -1;
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

        public void DeleteScenario(string scenarioName)
        {
            scenarioName = scenarioName + ".json";
            string filePath = System.IO.Path.Combine(Application.dataPath, scenariosfolder, scenarioName); 
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void SaveCurrentScenario(string scenarioName)
        {
            if (SimulationManager.Instance == null) return;

            Scenario newScenario = new Scenario
            {
                id = scenarios.Count,
                scenarioName = scenarioName,
                transmitters = new List<TransmitterConfig>(),
                receivers = new List<ReceiverConfig>(),
                propagationModel = PropagationModel.RayTracing,
                technology = TechnologyType.FiveGSub6
            };

            if (SimulationManager.Instance.connectionManager != null)
            {
                var currentSettings = SimulationManager.Instance.connectionManager.GetSettings();
            }

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

                        powerDbm = (float)Math.Round(transmitter.settings.transmitterPower, 2),
                        antennaGain = (float)Math.Round(transmitter.settings.antennaGain, 2),
                        frequency = (float)Math.Round(transmitter.settings.frequency, 2),
                        transmitterHeight = (float)Math.Round(transmitter.settings.transmitterHeight, 2),
                        maxReflections = transmitter.settings.maxReflections,
                        maxDiffractions = transmitter.settings.maxDiffractions,

                        propagationModel = transmitter.settings.propagationModel
                    };
                    newScenario.transmitters.Add(config);
                }
            }

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
                        receiverHeight = (float)Math.Round(receiver.receiverHeight, 2),
                        connectionMargin = (float)Math.Round(receiver.connectionMargin, 2)
                    };
                    newScenario.receivers.Add(config);
                }
            }

            if (newScenario.transmitters.Count > 0)
            {
                newScenario.propagationModel = newScenario.transmitters[0].propagationModel;
            }

            // Save to file
            string scenarioPath = System.IO.Path.Combine(Application.dataPath, scenariosfolder);
            scenarioName = scenarioName + ".json";
            string filePath = System.IO.Path.Combine(scenarioPath, scenarioName);

            try
            {
                string json = JsonUtility.ToJson(newScenario, true);
                File.WriteAllText(filePath, json);

                LoadAllScenarios();
                currentScenarioIndex = GetScenarioIndex(newScenario.id);
                SelectScenario(currentScenarioIndex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"❌ Failed to save scenario: {e.Message}");
            }
        }

        public void RunScenario(int index)
        {
            if (index < 0 || index >= scenarios.Count) return;

            ClearCurrentScenario();
            Scenario scenario = scenarios[index];
            ApplyScenarioSettings(scenario);

            foreach (var txConfig in scenario.transmitters)
            {
                CreateTransmitterFromConfig(txConfig);
            }

            foreach (var rxConfig in scenario.receivers)
            {
                CreateReceiverFromConfig(rxConfig);
            }

            OnScenarioChanged?.Invoke(scenario.scenarioName);
            OnScenarioLoaded?.Invoke(scenario);

        }

        private void ApplyScenarioSettings(Scenario scenario)
        {
            if (SimulationManager.Instance?.connectionManager != null)
            {
                var connectionManager = SimulationManager.Instance.connectionManager;

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
            PlaceObjectsHelper.Organize(txObj);
            Transmitter transmitter = txObj.GetComponent<Transmitter>();

            if (transmitter != null)
            {
                transmitter.settings.transmitterPower = config.powerDbm;
                transmitter.settings.antennaGain = config.antennaGain;
                transmitter.settings.frequency = config.frequency;
                transmitter.SetPropagationModel(config.propagationModel);
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
            PlaceObjectsHelper.Organize(rxObj);
            Receiver receiver = rxObj.GetComponent<Receiver>();

            if (receiver != null)
            {
                // Apply configuration
                receiver.SetTechnology(config.technology);
                receiver.sensitivity = config.sensitivity;
                receiver.receiverHeight = config.receiverHeight;
            }
        }

        public void ClearCurrentScenario()
        {
            // Clear all existing transmitters and receivers
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.ClearAllEquipment();
            }
        }

        public Scenario GetCurrentScenario()
        {
            if (currentScenarioIndex >= 0 && currentScenarioIndex < scenarios.Count)
            {
                return scenarios[currentScenarioIndex];
            }
            return null;
        }

    }
}