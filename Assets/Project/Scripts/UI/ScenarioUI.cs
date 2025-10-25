using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using RFSimulation.Core.Managers;
using RFSimulation.Propagation;
using RFSimulation.Core.Connections;
using RFSimulation.Core.Components;
using RFSimulation.Propagation.Core;
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
        public string exportSubfolder = "Project/Data/Exports";
        public Button screenshotButton;
        public InputField screenshotFileNameInput;
        public string screenshotSubfolder = "Project/Data/Screenshots";
        public Camera targetCamera;
        public Canvas uiCanvas;
        public int width = 1920;
        public int height = 1080;

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

            if (exportButton != null)
                exportButton.onClick.AddListener(ExportCurrentScenario);

            if (screenshotButton != null)
                screenshotButton.onClick.AddListener(OnScreenshotButtonClicked);

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

            SetStatusText("Scenarios loaded", Color.green);

        }

        private void OnScenarioDropdownChanged(int index)
        {
            var s = GetSelectedScenario();
            if (s != null) SetStatusText($"Selected: {s.scenarioName}", Color.cyan);
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
                SetStatusText("❌ Please enter a scenario name", Color.red);
                return;
            }

            ValidateScenario(scenarioManager.GetCurrentScenario());
            scenarioManager.SaveCurrentScenario(scenarioName);
            SetStatusText($"✅ Saved: {scenarioName}", Color.green);
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

            SetStatusText("Ready to create new scenario", Color.blue);
        }

        public void OnScreenshotButtonClicked()
        {
            StartCoroutine(SaveScreenshot());
        }

        #endregion

        #region Data Export

        private void ExportCurrentScenario()
        {
            try
            {
                // --- Gather scene objects ---
                var receivers = FindObjectsByType<Receiver>(FindObjectsSortMode.None);
                var transmitters = FindObjectsByType<Transmitter>(FindObjectsSortMode.None);

                if (receivers == null || receivers.Length == 0)
                    throw new System.Exception("No receivers in scene.");
                if (transmitters == null || transmitters.Length == 0)
                    throw new System.Exception("No transmitters in scene.");

                // --- Build path under Assets/<exportSubfolder> ---
                string assetsRoot = Application.dataPath; // absolute path to Assets/
                string folderAbs = System.IO.Path.Combine(assetsRoot, exportSubfolder);
                System.IO.Directory.CreateDirectory(folderAbs);

                // filename from UI or timestamp fallback
                string fileName = (exportFileNameInput != null) ? exportFileNameInput.text.Trim() : "";
                if (string.IsNullOrEmpty(fileName))
                    fileName = "export_" + System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");

                // sanitize filename
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(c, '_');

                string fileAbs = System.IO.Path.Combine(folderAbs, fileName + System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ") + ".csv");

                // --- Write CSV ---
                using (var sw = new System.IO.StreamWriter(fileAbs, false, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine("scenario,timestamp,rx_id,rx_x,rx_y,rx_z,rx_height_m,rx_sensitivity_dbm,rx_power_dbm," +
                                 "tx_id,tx_x,tx_y,tx_z,tx_height_m,tx_power_dbm,tx_frequency,distance_m");

                    string ts = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");

                    foreach (var rx in receivers)
                    {
                        Vector3 rxPos = rx.transform.position;
                        float rxHeight = SafeFloat(rx, nameof(rx.receiverHeight), float.NaN);
                        float rxPower = SafeFloat(rx, nameof(rx.currentSignalStrength), float.NaN);
                        float rxSens = SafeFloat(rx, nameof(rx.sensitivity), float.NaN);

                        Transmitter tx = null;
                        try { tx = rx.GetConnectedTransmitter(); } catch { }
                        if (tx == null)
                            tx = transmitters.OrderBy(t => Vector3.Distance(rx.transform.position, t.transform.position)).First();

                        Vector3 txPos = tx.transform.position;
                        float txHeight = SafeFloat(tx, nameof(tx.transmitterHeight), float.NaN);
                        float txPower = SafeFloat(tx, nameof(tx.transmitterPower), float.NaN);
                        float txFreq = SafeFloat(tx, nameof(tx.frequency), float.NaN);

                        float dist = Vector3.Distance(rxPos, txPos);

                        sw.WriteLine(string.Join(",", new string[] {
                    Esc(CurrentScenarioNameOrFallback()), // scenario
                    ts,                                    // timestamp
                    Esc(SafeString(rx, nameof(rx.uniqueID), rx.name)),
                    rxPos.x.ToString("F3"), rxPos.y.ToString("F3"), rxPos.z.ToString("F3"),
                    FormatFloat(rxHeight, "F3"),
                    FormatFloat(rxSens, "F1"),
                    (float.IsNegativeInfinity(rxPower) ? "" : FormatFloat(rxPower, "F1")),
                    Esc(SafeString(tx, nameof(tx.uniqueID), tx.name)),
                    txPos.x.ToString("F3"), txPos.y.ToString("F3"), txPos.z.ToString("F3"),
                    FormatFloat(txHeight, "F3"),
                    FormatFloat(txPower, "F1"),
                    FormatFloat(txFreq, "F0"),
                    dist.ToString("F3")
                }));
                    }
                }

                SetStatusText($"✅ Saved: Assets/{exportSubfolder}/{fileName}.csv", Color.green);
            }
            catch (System.Exception ex)
            {
                SetStatusText($"❌ Export failed: {ex.Message}", Color.red);
                Debug.LogError($"[ScenarioUI] Export failed: {ex}");
            }
        }

        private IEnumerator SaveScreenshot()
        {
            // Fallbacks (optional): find main camera / canvases if not set in Inspector
            if (targetCamera == null) targetCamera = Camera.main;

            // 1) Hide UI
            if (uiCanvas != null)
                uiCanvas.enabled = false;

            // wait one frame so the UI is actually hidden
            yield return null;

            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                // 2) Render only the target camera to a RT
                rt = new RenderTexture(width, height, 24);
                targetCamera.targetTexture = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);

                targetCamera.Render();

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                // 3) Save PNG under Assets/<screenshotSubfolder>/
                string assetsRoot = Application.dataPath;
                string folderAbs = Path.Combine(assetsRoot, screenshotSubfolder);
                Directory.CreateDirectory(folderAbs);

                string baseName = (screenshotFileNameInput != null) ? screenshotFileNameInput.text.Trim() : "";
                if (string.IsNullOrEmpty(baseName))
                    baseName = "screenshot";

                // sanitize filename
                foreach (char c in Path.GetInvalidFileNameChars())
                    baseName = baseName.Replace(c, '_');

                string stamped = $"{baseName}_{System.DateTime.UtcNow:yyyy-MM-ddTHH-mm-ssZ}.png";
                string fileAbs = Path.Combine(folderAbs, stamped);

                File.WriteAllBytes(fileAbs, tex.EncodeToPNG());
                SetStatusText($"📸 Saved: Assets/{screenshotSubfolder}/{stamped}", Color.green);
            }
            catch (Exception ex)
            {
                SetStatusText($"❌ Screenshot failed: {ex.Message}", Color.red);
                Debug.LogError($"[ScenarioUI] Screenshot failed: {ex}");
            }
            finally
            {
                // 4) Cleanup & re-enable UI
                if (targetCamera != null) targetCamera.targetTexture = null;
                RenderTexture.active = null;
                if (rt != null) Destroy(rt);
                // (Texture2D can be left for GC; destroy if you create many in a session)
                // if (tex != null) Destroy(tex);

                if (uiCanvas != null)
                    uiCanvas.enabled = true;
            }
        }

        #endregion

        #region UI Updates

        private void OnScenarioChanged(string scenarioName)
        {
            SetStatusText($"Current Scenario: {scenarioName}", Color.green);
        }

        private void OnScenarioLoaded(Scenario scenario)
        {
            SetStatusText($"Scenario Loaded: {scenario.scenarioName}", Color.green);
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
            return true; 
        }

        private void DeleteScenarioFile(string scenarioName)
        {
            string filePath = Application.dataPath + "/Project/Data/Scenarios/" + scenarioName + ".json";
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                    SetStatusText($"✅ Deleted: {scenarioName}", Color.green);
                }
                catch (System.Exception e)
                {
                    SetStatusText($"❌ Failed to delete: {e.Message}", Color.red);
                }
            }
        }

        private void CreateScenarioCopy(Scenario original, string newName)
        {
            // This would create a copy of the scenario
            // Implementation would depend on your specific needs
            SetStatusText($"📋 Scenario copied to: {newName}", Color.blue);
        }

        private void ValidateCurrentScenario()
        {
            var scenario = GetSelectedScenario();
            if (scenario == null)
            {
                SetStatusText("❌ No scenario selected", Color.red);
                return;
            }

            // Validate scenario
            var issues = ValidateScenario(scenario);
            if (issues.Count == 0)
            {
                SetStatusText("✅ Scenario is valid", Color.green);
            }
            else
            {
                string issueText = string.Join(", ", issues);
                SetStatusText($"⚠️ Issues: {issueText}", Color.yellow);
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

                if (tx.frequency <= 0)
                    issues.Add($"Invalid frequency: {tx.frequency}MHz");
            }

            return issues;
        }

        private string CurrentScenarioNameOrFallback()
        {
            var s = GetSelectedScenario();
            if (s != null && !string.IsNullOrEmpty(s.scenarioName)) return s.scenarioName;
            return "current_scene";
        }

        private static string SafeString(object obj, string fieldOrProp, string fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            var p = t.GetProperty(fieldOrProp);
            if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(obj);
            var f = t.GetField(fieldOrProp);
            if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
            return fallback;
        }

        private static float SafeFloat(object obj, string fieldOrProp, float fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            var p = t.GetProperty(fieldOrProp);
            if (p != null && (p.PropertyType == typeof(float) || p.PropertyType == typeof(double)))
                return Convert.ToSingle(p.GetValue(obj));
            var f = t.GetField(fieldOrProp);
            if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(double)))
                return Convert.ToSingle(f.GetValue(obj));
            return fallback;
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return (s.Contains(",") || s.Contains("\"")) ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }

        private static string FormatFloat(float v, string fmt) => float.IsNaN(v) ? "" : v.ToString(fmt);

        private static bool IsTypingInUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            var go = es.currentSelectedGameObject;
            if (go == null) return false;

            // Legacy InputField
            var lf = go.GetComponent<InputField>();
            if (lf != null && lf.isFocused) return true;

            // TextMeshPro input
            var tmp = go.GetComponent<TMP_InputField>();
            if (tmp != null && tmp.isFocused) return true;

            return false;
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