using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using RFSimulation.Core.Components;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Connections;
using RFSimulation.Visualization;

namespace RFSimulation.UI
{
    public class ControlUI : MonoBehaviour
    {

        [Header("Status Panel")]
        public StatusUI statusUI;

        [Header("Object Placement")]
        public Button addTransmitterButton;
        public Button addReceiverButton;
        public Button removeAllButton;

        [Header("Parameter Inputs")]
        public InputField transmitterPowerInput;
        public InputField transmitterFrequencyInput;
        public Dropdown receiverTechnologyDropdown;
        public InputField receiverSensitivityInput;

        [Header("Height Controls")]
        public InputField transmitterHeightInput;
        public InputField receiverHeightInput;

        [Header("Toggle Controls")]
        public Toggle showConnectionsToggle;
        public Toggle showGridToggle;
        public Toggle showBuildingsToggle;
        public Toggle showHeatmapToggle;
        public Toggle showRaysToggle;

        [Header("Status")]
        public Text statusText;

        [Header("Prefab References")]
        public GameObject transmitterPrefab;
        public GameObject receiverPrefab;

        [Header("Placement Settings")]
        public LayerMask placementLayerMask = 6;
        public float transmitterHeight = 15f;
        public float receiverHeight = 1f;

        [Header("Placement Preview")]
        public Material previewMaterial;     
        [Range(0.05f, 1f)] public float previewAlpha = 0.5f;
        public LayerMask previewIgnoreLayers = 1 << 2;

        private GameObject previewInstance;
        private GameObject previewSourcePrefab; 
        private float previewHeightOffset;   

        [Header("Ground Settings")]
        public float groundLevel = 0f;
        public LayerMask terrainLayerMask = 6;

        [Header("Grid Settings")]
        public bool enableGridSnap = true;
        public GroundGrid groundGridComponent;

        private Camera mainCamera;
        private bool isPlacingTransmitter = false;
        private bool isPlacingReceiver = false;

        // Managers we actually need here (no ScenarioManager here)
        private ConnectionManager connectionManager;

        void Start()
        {
            mainCamera = Camera.main;
            SetupManagerReferences();
            CreateUI();
            InitializeUIFromPrefabs();
        }

        private void SetupManagerReferences()
        {
            if (SimulationManager.Instance != null)
            {
                connectionManager = SimulationManager.Instance.connectionManager;
            }

            // BuildingManager is allowed here for the “show buildings” toggle
            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.OnBuildingsToggled += OnBuildingsToggled;
            }
        }

        private void CreateUI()
        {
            // Placement buttons
            if (addTransmitterButton != null)
                addTransmitterButton.onClick.AddListener(StartPlacingTransmitter);

            if (addReceiverButton != null)
                addReceiverButton.onClick.AddListener(StartPlacingReceiver);

            if (removeAllButton != null)
                removeAllButton.onClick.AddListener(RemoveAllObjects);

            // Height inputs
            if (transmitterHeightInput != null)
                transmitterHeightInput.onValueChanged.AddListener(OnTransmitterHeightChanged);

            if (receiverHeightInput != null)
                receiverHeightInput.onValueChanged.AddListener(OnReceiverHeightChanged);

            // Toggles
            if (showConnectionsToggle != null)
                showConnectionsToggle.onValueChanged.AddListener(ToggleConnections);

            if (showGridToggle != null)
                showGridToggle.onValueChanged.AddListener(ToggleGrid);

            if (showBuildingsToggle != null)
                showBuildingsToggle.onValueChanged.AddListener(ToggleBuildings);

            if (showHeatmapToggle != null)
                showHeatmapToggle.onValueChanged.AddListener(ToggleHeatmap);

            if (showRaysToggle != null)
                showRaysToggle.onValueChanged.AddListener(ToggleRays);

            // Auto-find GroundGrid if not assigned
            if (groundGridComponent == null)
            {
                groundGridComponent = FindFirstObjectByType<GroundGrid>();
            }

            // Initialize dropdowns
            InitializeReceiverTechnologyDropdown();

            // Defaults
            SetDefaultUIValues();

            UpdateStatusText("Ready to place objects");
        }

        private void InitializeReceiverTechnologyDropdown()
        {
            if (receiverTechnologyDropdown != null)
            {
                var technologies = new List<string> { "5G", "LTE" };
                receiverTechnologyDropdown.ClearOptions();
                receiverTechnologyDropdown.AddOptions(technologies);
                receiverTechnologyDropdown.value = 0; // Default 5G
            }
        }

        private void SetDefaultUIValues()
        {
            if (showConnectionsToggle != null) showConnectionsToggle.isOn = true;
            if (showGridToggle != null) showGridToggle.isOn = true;

            if (showBuildingsToggle != null)
                showBuildingsToggle.isOn = BuildingManager.AreBuildingsEnabled();

            if (showHeatmapToggle != null) showHeatmapToggle.isOn = true;
            if (showRaysToggle != null) showRaysToggle.isOn = true;

            if (transmitterHeightInput != null)
            {
                transmitterHeightInput.text = transmitterHeight.ToString("F1");
            }

            if (receiverHeightInput != null)
            {
                receiverHeightInput.text = receiverHeight.ToString("F1");
            }
        }

        private void Update()
        {
            HandlePlacement();
        }

        #region Height Controls

        private void OnTransmitterHeightChanged(string value)
        {
            if (float.TryParse(value, out float h))
            {
                transmitterHeight = Mathf.Clamp(h, 0.1f, 200f);
            }
        }

        private void OnReceiverHeightChanged(string value)
        {
            if (float.TryParse(value, out float h))
            {
                receiverHeight = Mathf.Clamp(h, 0.1f, 50f);
            }
        }

        #endregion

        #region Building / Heatmap / Rays

        private void ToggleBuildings(bool enabled)
        {
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.SetBuildingsEnabled(enabled);

            UpdateStatusText($"Buildings {(enabled ? "enabled" : "disabled")}");
        }

        private void OnBuildingsToggled(bool enabled)
        {
            if (showBuildingsToggle != null)
                showBuildingsToggle.SetIsOnWithoutNotify(enabled);

            UpdateStatusText($"Buildings {(enabled ? "enabled" : "disabled")} - RF calculations updated");
        }

        private void ToggleHeatmap(bool enabled)
        {
            var heatmaps = FindObjectsByType<SignalHeatmap>(FindObjectsSortMode.None);
            foreach (var hm in heatmaps)
                hm?.SetUIEnabled(enabled);

            UpdateStatusText($"Heatmap {(enabled ? "enabled" : "disabled")}");
        }

        private void ToggleRays(bool enabled)
        {
            var transmitters = FindObjectsByType<Transmitter>(FindObjectsSortMode.None);
            foreach (var tx in transmitters)
            {
                if (tx == null) continue;
                if (enabled) tx.EnableRayVisualization();
                else tx.DisableRayVisualization();
            }

            UpdateStatusText($"Ray visualization {(enabled ? "enabled" : "disabled")}");
        }

        #endregion

        #region Placement

        private void HandlePlacement()
        {
            if (!(isPlacingTransmitter || isPlacingReceiver)) return;

            UpdatePlacementPreview();

            if (Input.GetMouseButtonDown(0))
            {
                // Ignore clicks on UI
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                    ConfirmPlacement();
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                CancelPlacement();
        }

        public void StartPlacingTransmitter()
        {
            BeginPlacement(transmitterPrefab, transmitterHeight);
            isPlacingTransmitter = true;
            isPlacingReceiver = false;
            UpdateStatusText("Click to place transmitter (ESC to cancel)");
        }

        public void StartPlacingReceiver()
        {
            BeginPlacement(receiverPrefab, receiverHeight);
            isPlacingReceiver = true;
            isPlacingTransmitter = false;
            UpdateStatusText("Click to place receiver (ESC to cancel)");
        }

        private Vector3 SnapToGrid(Vector3 pos)
        {
            if (!enableGridSnap) return pos;
            if (groundGridComponent != null) return groundGridComponent.SnapToGrid(pos);
            Debug.LogWarning("No GroundGrid for snapping");
            return pos;
        }

        private void PlaceObjectAtMousePosition()
        {
            // Ignore UI clicks
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 placementPosition;

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    10000f,
                    placementLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                placementPosition = hit.point;
            }
            else
            {
                // Fallback: intersect with a horizontal plane at groundLevel
                var groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevel, 0));
                if (!groundPlane.Raycast(ray, out float dist)) return;
                placementPosition = ray.GetPoint(dist);
            }

            if (enableGridSnap) placementPosition = SnapToGrid(placementPosition);

            if (isPlacingTransmitter) PlaceTransmitter(placementPosition);
            else if (isPlacingReceiver) PlaceReceiver(placementPosition);

            CancelPlacement();
        }

        private void PlaceTransmitter(Vector3 position)
        {
            position.y += transmitterHeight;

            var go = Instantiate(transmitterPrefab, position, Quaternion.identity);
            var tx = go.GetComponent<Transmitter>();
            if (tx != null)
            {
                if (transmitterPowerInput != null && float.TryParse(transmitterPowerInput.text, out float p))
                    tx.transmitterPower = p;

                if (transmitterFrequencyInput != null && float.TryParse(transmitterFrequencyInput.text, out float f))
                    tx.frequency = f;

                tx.showConnections = showConnectionsToggle != null ? showConnectionsToggle.isOn : true;

                UpdateStatusText($"Transmitter placed: {tx.transmitterPower:F1} dBm, {tx.frequency:F0} MHz @ {position}");
                
                statusUI?.ShowTransmitter(tx);
            }
        }

        private void PlaceReceiver(Vector3 position)
        {
            position.y += receiverHeight;

            var go = Instantiate(receiverPrefab, position, Quaternion.identity);
            var rx = go.GetComponent<Receiver>();
            if (rx != null)
            {
                if (receiverSensitivityInput != null && float.TryParse(receiverSensitivityInput.text, out float s))
                    rx.sensitivity = s;

                if (receiverTechnologyDropdown != null)
                {
                    string tech = receiverTechnologyDropdown.options[receiverTechnologyDropdown.value].text;
                    rx.SetTechnology(tech);
                }

                UpdateStatusText($"Receiver placed: {rx.technology}, {rx.sensitivity:F1} dBm @ {position}");

                statusUI?.ShowReceiver(rx);
            }
        }

        private void CancelPlacement()
        {
            isPlacingTransmitter = false;
            isPlacingReceiver = false;
            DestroyPreview();
            UpdateStatusText("Ready to place objects");
        }

        public void RemoveAllObjects()
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.ClearAllEquipment();

            UpdateStatusText("All objects removed");
        }

        private void BeginPlacement(GameObject prefab, float heightOffset)
        {
            // clean up any old preview
            if (previewInstance != null) Destroy(previewInstance);

            previewSourcePrefab = prefab;
            previewHeightOffset = heightOffset;

            // Create ghost preview from the prefab (no side-effects on the actual prefab)
            previewInstance = Instantiate(prefab);
            previewInstance.name = prefab.name + "_PREVIEW";
            previewInstance.layer = 2; // IgnoreRaycast by default

            // Make it semi-transparent and non-interactive
            foreach (var col in previewInstance.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            var renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (previewMaterial != null)
                {
                    r.sharedMaterial = previewMaterial; // use your transparent material
                }
                // force tint alpha (works if material uses _BaseColor/_Color)
                if (r.sharedMaterial.HasProperty("_BaseColor"))
                {
                    var c = r.sharedMaterial.GetColor("_BaseColor");
                    c.a = previewAlpha;
                    r.sharedMaterial.SetColor("_BaseColor", c);
                }
                else if (r.sharedMaterial.HasProperty("_Color"))
                {
                    var c = r.sharedMaterial.color;
                    c.a = previewAlpha;
                    r.sharedMaterial.color = c;
                }
            }
        }

        private void UpdatePlacementPreview()
        {
            if (previewInstance == null) return;

            // Ignore UI clicks/hover for raycast targeting
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                previewInstance.SetActive(false);
                return;
            }

            if (TryGetMouseWorld(out Vector3 pos))
            {
                if (enableGridSnap) pos = SnapToGrid(pos);
                pos.y += previewHeightOffset;

                previewInstance.transform.position = pos;
                previewInstance.SetActive(true);

                // Optional: rotate with Q/E keys for orientation control
                if (Input.GetKey(KeyCode.Q)) previewInstance.transform.Rotate(0f, -120f * Time.deltaTime, 0f, Space.World);
                if (Input.GetKey(KeyCode.E)) previewInstance.transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            }
            else
            {
                previewInstance.SetActive(false);
            }
        }

        private bool TryGetMouseWorld(out Vector3 worldPos)
        {
            worldPos = default;
            if (mainCamera == null) mainCamera = Camera.main;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f, placementLayerMask, QueryTriggerInteraction.Ignore))
            {
                worldPos = hit.point;
                return true;
            }

            // Fallback plane
            var groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevel, 0));
            if (groundPlane.Raycast(ray, out float dist))
            {
                worldPos = ray.GetPoint(dist);
                return true;
            }
            return false;
        }

        private void ConfirmPlacement()
        {
            if (previewInstance == null || previewSourcePrefab == null) return;

            // Instantiate the real object at the preview transform
            Vector3 pos = previewInstance.transform.position;
            Quaternion rot = previewInstance.transform.rotation;

            var go = Instantiate(previewSourcePrefab, pos, rot);

            // Initialize fields from UI (same as your existing PlaceTransmitter/Receiver)
            if (isPlacingTransmitter)
            {
                var tx = go.GetComponent<Transmitter>();
                if (tx != null)
                {
                    if (transmitterPowerInput != null && float.TryParse(transmitterPowerInput.text, out float p))
                        tx.transmitterPower = p;

                    if (transmitterFrequencyInput != null && float.TryParse(transmitterFrequencyInput.text, out float f))
                        tx.frequency = f;

                    tx.showConnections = showConnectionsToggle != null ? showConnectionsToggle.isOn : true;

                    UpdateStatusText($"Transmitter placed: {tx.transmitterPower:F1} dBm, {tx.frequency:F0} MHz @ {pos}");
                    statusUI?.ShowTransmitter(tx);
                }
            }
            else if (isPlacingReceiver)
            {
                var rx = go.GetComponent<Receiver>();
                if (rx != null)
                {
                    if (receiverSensitivityInput != null && float.TryParse(receiverSensitivityInput.text, out float s))
                        rx.sensitivity = s;

                    if (receiverTechnologyDropdown != null)
                    {
                        string tech = receiverTechnologyDropdown.options[receiverTechnologyDropdown.value].text;
                        rx.SetTechnology(tech);
                    }

                    UpdateStatusText($"Receiver placed: {rx.technology}, {rx.sensitivity:F1} dBm @ {pos}");
                    statusUI?.ShowReceiver(rx);
                }
            }

            // Done
            CancelPlacement();
        }

        private void DestroyPreview()
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }
            previewSourcePrefab = null;
        }

        #endregion

        #region Connections + Grid

        public void ToggleConnections(bool enabled)
        {
            if (SimulationManager.Instance != null)
            {
                foreach (var tx in SimulationManager.Instance.transmitters)
                    if (tx != null) tx.ToggleConnectionLineVisualization(enabled);
            }
                     
            UpdateStatusText($"Connections {(enabled ? "enabled" : "disabled")}");
        }

        private void ToggleGrid(bool show)
        {
            // Prefer the cached reference; if missing, include inactive in the search
            if (groundGridComponent == null)
                groundGridComponent = FindFirstObjectByType<GroundGrid>(FindObjectsInactive.Include);

            if (groundGridComponent != null)
            {
                groundGridComponent.SetGridVisibility(show); // << use the API in GroundGrid
                UpdateStatusText($"Grid {(show ? "enabled" : "disabled")}");
            }
            else
            {
                Debug.LogWarning("[ControlUI] GroundGrid not found.");
            }
        }
        #endregion

        private void InitializeUIFromPrefabs()
        {
            if (transmitterPrefab != null)
            {
                var tx = transmitterPrefab.GetComponent<Transmitter>();
                if (tx != null)
                {
                    if (transmitterPowerInput != null) transmitterPowerInput.text = tx.transmitterPower.ToString();
                    if (transmitterFrequencyInput != null) transmitterFrequencyInput.text = tx.frequency.ToString();
                }
            }

            if (receiverPrefab != null)
            {
                var rx = receiverPrefab.GetComponent<Receiver>();
                if (rx != null)
                {
                    if (receiverSensitivityInput != null) receiverSensitivityInput.text = rx.sensitivity.ToString();

                    if (receiverTechnologyDropdown != null)
                    {
                        var opts = receiverTechnologyDropdown.options;
                        for (int i = 0; i < opts.Count; i++)
                        {
                            if (opts[i].text.ToLower() == rx.technology.ToLower())
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

        private void UpdateStatusText(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[ControlUI] {msg}");
        }

        private void OnDestroy()
        {
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.OnBuildingsToggled -= OnBuildingsToggled;
        }
    }
}
