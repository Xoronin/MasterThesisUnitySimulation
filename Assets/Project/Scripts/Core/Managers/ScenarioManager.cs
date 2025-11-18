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
using System.Collections;
using RFSimulation.UI;


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
        public int maxReflections;
        public int maxDiffractions;
        public int maxScattering;
        public TechnologyType technology;
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
        public int currentScenarioIndex = -1;

        [Header("Prefab References")]
        public GameObject transmitterPrefab;
        public GameObject receiverPrefab;

        [Header("Export Settings")]
        public string csvSubfolder = "Project/Data/Exports";
        public string screenshotSubfolder = "Project/Data/Screenshots";
        public string scenariosfolder = "Project/Data/Scenarios";

        public Canvas uiCanvas;
        public ControlUI controlUI;


        // Events for UI updates
        public System.Action<List<string>> OnScenariosLoaded;
        public System.Action<string> OnScenarioChanged;
        public System.Action<Scenario> OnScenarioLoaded;
        public System.Action<Scenario> OnScenarioSaved;

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

            string assetsRoot = Application.dataPath;
            string scenarioPath = Path.Combine(assetsRoot, scenariosfolder);
            Directory.CreateDirectory(scenarioPath);

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
                    Debug.LogWarning($"Failed to load scenario from {filePath}: {e.Message}");
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

        public void SaveCurrentScenario(string scenarioName, bool overwriteCurrent)
        {
            if (SimulationManager.Instance == null) return;

            int newId;
            string fileName = scenarioName;

            if (overwriteCurrent && currentScenarioIndex >= 0 && currentScenarioIndex < scenarios.Count)
            {
                var current = scenarios[currentScenarioIndex];
                newId = current.id;
                fileName = current.scenarioName; 
            }
            else
            {
                newId = (scenarios.Count > 0) ? scenarios.Max(s => s.id) + 1 : 0;
                fileName = scenarioName;
            }

            Scenario newScenario = new Scenario
            {
                id = scenarios.Count,
                scenarioName = fileName,
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
                        maxScattering = transmitter.settings.maxScattering,

                        technology = transmitter.settings.technology,
                        propagationModel = transmitter.settings.propagationModel,
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
                newScenario.technology = newScenario.transmitters[0].technology;
            }

            // Save to file
            string scenarioPath = System.IO.Path.Combine(Application.dataPath, scenariosfolder);
            string filePath = System.IO.Path.Combine(scenarioPath, scenarioName + ".json");

            try
            {
                string json = JsonUtility.ToJson(newScenario, true);
                File.WriteAllText(filePath, json);

                LoadAllScenarios();
                currentScenarioIndex = GetScenarioIndex(newScenario.id);
                if (currentScenarioIndex >= 0)
                    SelectScenario(currentScenarioIndex);
                OnScenarioSaved?.Invoke(newScenario);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to save scenario: {e.Message}");
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
                transmitter.SetTechnology(config.technology);
                transmitter.SetTransmitterHeight(Mathf.Max(0f, config.transmitterHeight));
                transmitter.SetMaxReflections(config.maxReflections);
                transmitter.SetMaxDiffractions(config.maxDiffractions);
                transmitter.SetMaxScattering(config.maxScattering);
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
                receiver.connectionMargin = config.connectionMargin;
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

        public void ExportSnapshotCsv(string baseName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = "snapshot";

                foreach (char c in Path.GetInvalidFileNameChars())
                    baseName = baseName.Replace(c, '_');

                string assetsRoot = Application.dataPath;
                string folderAbs = Path.Combine(assetsRoot, csvSubfolder);
                Directory.CreateDirectory(folderAbs);

                string stamped = $"{baseName}.csv";
                string fileAbs = Path.Combine(folderAbs, stamped);

                string csv = BuildSnapshotCsv();
                File.WriteAllText(fileAbs, csv);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScenarioManager] CSV export failed: {ex}");
            }
        }

        private string BuildSnapshotCsv()
        {
            var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.None);
            var transmitters = FindObjectsByType<Transmitter>(FindObjectsSortMode.None);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ScenarioName,PropagationModel,Technology," +
                            "RxId,RxPosX,RxPosY,RxPosZ,RxHeight,RxSensitivity,RxSignalStrength,RxConnectMargin," +
                            "TxId,TxPosX,TxPosY,TxPosZ,TxHeight,TxPower,TxFrequency,TxMaxReflections,TxMaxDiffractions,TxMaxScattering," +
                            "Distance,BuildingsOn");

            foreach (var rx in receivers)
            {
                if (!rx.HasValidSignal()) continue;

                Vector3 rxPos = rx.transform.position;
                float rxHeight = FormatHelper.SafeFloat(rx, nameof(rx.receiverHeight), float.NaN);
                float rxPower = FormatHelper.SafeFloat(rx, nameof(rx.currentSignalStrength), float.NaN);
                float rxSens = FormatHelper.SafeFloat(rx, nameof(rx.sensitivity), float.NaN);

                Transmitter tx = null;
                try { tx = rx.GetConnectedTransmitter(); } catch { }
                if (tx == null)
                    tx = transmitters.OrderBy(t => Vector3.Distance(rx.transform.position, t.transform.position)).First();

                Transmitter.TransmitterInfo txInfo = tx.GetInfo();
                Vector3 txPos = txInfo.WorldPosition;
                float txHeight = txInfo.TransmitterHeightM;
                float txPower =txInfo.TransmitterPowerDbm;
                float txFreq = txInfo.FrequencyMHz;

                float dist = Vector3.Distance(rxPos, txPos);

                sb.AppendLine(string.Join(",", new string[] {
                FormatHelper.Esc(GetCurrentScenario().scenarioName),
                FormatHelper.Esc(Enum.GetName(typeof(PropagationModel), txInfo.PropagationModel)),
                FormatHelper.Esc(Enum.GetName(typeof(TechnologyType), rx.technology)),
                FormatHelper.Esc(FormatHelper.SafeString(rx, nameof(rx.uniqueID), rx.name)),
                rxPos.x.ToString("F3"), rxPos.y.ToString("F3"), rxPos.z.ToString("F3"),
                FormatHelper.FormatFloat(rxHeight, "F2"),
                FormatHelper.FormatFloat(rxSens, "F1"),
                (float.IsNegativeInfinity(rxPower) ? "" : FormatHelper.FormatFloat(rxPower, "F1")),
                rx.connectionMargin.ToString("F0"),
                FormatHelper.Esc(FormatHelper.SafeString(tx, nameof(tx.uniqueID), tx.name)),
                txPos.x.ToString("F3"), txPos.y.ToString("F3"), txPos.z.ToString("F3"),
                FormatHelper.FormatFloat(txHeight, "F2"),
                FormatHelper.FormatFloat(txPower, "F1"),
                FormatHelper.FormatFloat(txFreq, "F0"),
                txInfo.MaxReflections.ToString("F0"),
                txInfo.MaxDiffractions.ToString("F0"),
                txInfo.MaxScattering.ToString("F0"),
                dist.ToString("F3"),
                FormatHelper.Esc(controlUI.showBuildingsToggle.IsActive().ToString())
                }));
            }

            return sb.ToString();
        }

        public IEnumerator CaptureScreenshotCoroutine(string baseName)
        {
            if (uiCanvas != null)
                uiCanvas.enabled = false;

            yield return new WaitForEndOfFrame();

            try
            {
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = "screenshot";

                foreach (char c in Path.GetInvalidFileNameChars())
                    baseName = baseName.Replace(c, '_');

                string assetsRoot = Application.dataPath;
                string folderAbs = Path.Combine(assetsRoot, screenshotSubfolder);
                Directory.CreateDirectory(folderAbs);

                string stamped = $"{baseName}.png";
                string fileAbs = Path.Combine(folderAbs, stamped);

                ScreenCapture.CaptureScreenshot(fileAbs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScenarioManager] Screenshot failed: {ex}");
            }
            finally
            {
                if (uiCanvas != null)
                    uiCanvas.enabled = true;
            }
        }

    }
}