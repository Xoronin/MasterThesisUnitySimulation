using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using RFSimulation.Core.Components;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Connections;
using RFSimulation.Visualization;
using RFSimulation.Propagation.Core;
using RFSimulation.Utils;

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
        public Dropdown transmitterPropagationModelDropdown;
        public InputField transmitterPowerInput;
        public InputField transmitterFrequencyInput;
        public Dropdown receiverTechnologyDropdown;
        public InputField receiverSensitivityInput;
        public InputField gridSizeInput;

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
        public LayerMask buildingLayerMask = 8;
        public float transmitterHeight = 8f;
        public float receiverHeight = 1f;
        public float maxTransmitterHeight = 30f;

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

        private PropagationModel _defaultTxModel = PropagationModel.Auto;

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

            // Grid size input
            if (gridSizeInput != null)
            {
                gridSizeInput.onEndEdit.AddListener(OnGridSizeChanged);
            }

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
            InitializeTransmitterPropagationModelDropdown();

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

        private static readonly string[] ModelNames =
            { "Auto", "Free Space", "Log Distance", "Hata", "COST 231 Hata", "Ray Tracing" };

        private static PropagationModel ModelFromIndex(int i)
        {
            switch (i)
            {
                case 0: return PropagationModel.Auto;
                case 1: return PropagationModel.FreeSpace;
                case 2: return PropagationModel.LogDistance;
                case 3: return PropagationModel.Hata;          // adjust if your enum is OkumuraHata
                case 4: return PropagationModel.COST231;   // adjust to your exact enum name
                default: return PropagationModel.Auto;
            }
        }

        private static int IndexFromModel(PropagationModel m)
        {
            switch (m)
            {
                case PropagationModel.Auto: return 0;
                case PropagationModel.FreeSpace: return 1;
                case PropagationModel.LogDistance: return 2;
                case PropagationModel.Hata: return 3;  // adjust if needed
                case PropagationModel.COST231: return 4;  // adjust if needed
                case PropagationModel.RayTracing:
                default: return 0;
            }
        }

        private void InitializeTransmitterPropagationModelDropdown()
        {
            if (transmitterPropagationModelDropdown == null) return;

            transmitterPropagationModelDropdown.ClearOptions();
            transmitterPropagationModelDropdown.AddOptions(new List<string>(ModelNames));
            transmitterPropagationModelDropdown.value = IndexFromModel(_defaultTxModel); // was 5 (out of range)
            transmitterPropagationModelDropdown.onValueChanged.AddListener(i =>
            {
                _defaultTxModel = ModelFromIndex(i);
            });
        }

        private void SetDefaultUIValues()
        {
            if (showConnectionsToggle != null) showConnectionsToggle.isOn = true;
            if (showGridToggle != null) showGridToggle.isOn = true;

            if (showBuildingsToggle != null)
                showBuildingsToggle.isOn = BuildingManager.AreBuildingsEnabled();

            if (showHeatmapToggle != null) showHeatmapToggle.isOn = false;

            if (showRaysToggle != null) showRaysToggle.isOn = false;

            if (transmitterHeightInput != null)
            {
                transmitterHeightInput.text = transmitterHeight.ToString("F1");
            }

            if (receiverHeightInput != null)
            {
                receiverHeightInput.text = receiverHeight.ToString("F1");
            }

            if (groundGridComponent != null && gridSizeInput != null)
            {
                gridSizeInput.text = groundGridComponent.gridSize.ToString("F1");
            }
        }

        private void Update()
        {
            HandlePlacement();
        }

        #region Grid Size

        void OnGridSizeChanged(string value)
        {
            if (float.TryParse(value, out float size))
            {
                size = Mathf.Clamp(size, 1f, 100f);
                if (groundGridComponent != null)
                {
                    groundGridComponent.UpdateGridSize(size);
                    UpdateStatusText($"Grid size set to {size:F1} units");
                }
            }
        }

        #endregion

        #region Height Controls

        private void OnTransmitterHeightChanged(string value)
        {
            if (float.TryParse(value, out float h))
            {
                if (h > maxTransmitterHeight)
                {
                    transmitterHeight = Mathf.Clamp(maxTransmitterHeight, 0.1f, 200f);
                    UpdateStatusText(value + $" exceeds max transmitter height of {maxTransmitterHeight}m. Clamped to max.");
                }
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
            SimulationManager.Instance?.RecomputeAllSignalStrength();

        }

        private void OnBuildingsToggled(bool enabled)
        {
            if (showBuildingsToggle != null)
                showBuildingsToggle.SetIsOnWithoutNotify(enabled);

            UpdateStatusText($"Buildings {(enabled ? "enabled" : "disabled")} - RF calculations updated");
            SimulationManager.Instance?.RecomputeAllSignalStrength();
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
                if (enabled)
                {
                    tx.EnableRayVisualization();   // ensures RT model + viz flags
                    RecomputeRaysFor(tx);          // <— force draw NOW
                }
                else
                {
                    tx.DisableRayVisualization();
                }
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
            BeginPlacement(transmitterPrefab, 0f);
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
            // 1) snap XZ to grid (if enabled)
            if (enableGridSnap && groundGridComponent != null)
                position = groundGridComponent.SnapToGrid(position);

            // 2) force Y to real terrain height (removes any grid heightOffset)
            if (groundGridComponent != null)
            {
                // copy of your grid’s probe but returning pure hit.point.y
                var probe = new Vector3(position.x, groundGridComponent.raycastStartHeight, position.z);
                if (Physics.Raycast(new Ray(probe, Vector3.down), out var hit, Mathf.Infinity, groundGridComponent.terrainMask, QueryTriggerInteraction.Ignore))
                    position.y = hit.point.y;
            }

            var go = Instantiate(transmitterPrefab, position, Quaternion.identity);
            var tx = go.GetComponent<Transmitter>();
            if (tx != null)
            {
                if (transmitterPowerInput != null && float.TryParse(transmitterPowerInput.text, out float p))
                    tx.transmitterPower = p;

                if (transmitterFrequencyInput != null && float.TryParse(transmitterFrequencyInput.text, out float f))
                    tx.frequency = f;

                if (transmitterPropagationModelDropdown != null)
                {
                    tx.SetPropagationModel(_defaultTxModel);
                }

                if (transmitterHeightInput != null && float.TryParse(transmitterHeightInput.text, out float h))
                {
                    tx.SetTransmitterHeight(Mathf.Max(0f, h));
                }

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

        private bool TryGetGroundPosition(out Vector3 pos)
        {
            pos = default;
            if (mainCamera == null) mainCamera = Camera.main;

            if (RaycastUtil.RayToGround(mainCamera, Input.mousePosition, placementLayerMask, out var hit))
            {
                pos = hit.point;
                return true;
            }
            return false;
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

            if (mainCamera == null) mainCamera = Camera.main;
            var ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            // If we are placing a receiver and the ray is over a building -> hide preview
            if (isPlacingReceiver && RayHitsBuilding(ray))
            {
                previewInstance.SetActive(false);
                UpdateStatusText("Can't place receiver on buildings");
                return;
            }

            if (RaycastSkippingBuildings(ray, out var hit))
            {
                if (TryGetGroundPosition(out var pos))
                {
                    if (enableGridSnap) pos = SnapToGrid(pos);

                    pos.y = hit.point.y + previewHeightOffset;
                    previewInstance.transform.position = pos;
                    previewInstance.SetActive(true);
                }
                else
                {
                    previewInstance.SetActive(false);
                }

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

        private bool RaycastSkippingBuildings(Ray ray, out RaycastHit hit, float maxDist = 10000f)
        {
            hit = default;
            // Use terrainOnlyMask if provided, else fall back to placementLayerMask
            var mask = (terrainLayerMask.value != 0) ? terrainLayerMask : placementLayerMask;

            var hits = Physics.RaycastAll(ray, maxDist, mask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                bool isBuilding = (buildingLayerMask.value & (1 << h.collider.gameObject.layer)) != 0;
                if (isBuilding) continue;                 // skip buildings
                hit = h;
                return true;
            }
            return false;
        }

        // Utility: are we currently pointing at a building?
        private bool RayHitsBuilding(Ray ray, float maxDist = 10000f)
        {
            if (buildingLayerMask.value == 0) return false;
            return Physics.Raycast(ray, maxDist, buildingLayerMask, QueryTriggerInteraction.Ignore);
        }

        private void ConfirmPlacement()
        {
            if (previewInstance == null || previewSourcePrefab == null) return;

            if (isPlacingReceiver)
            {
                if (mainCamera == null) mainCamera = Camera.main;
                var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (RayHitsBuilding(ray))
                {
                    UpdateStatusText("Receiver placement blocked: pointer over building.");
                    return;
                }
            }

            if (TryGetGroundPosition(out var pos))
            {
                if (enableGridSnap) pos = SnapToGrid(pos);

                if (isPlacingReceiver)
                    pos.y = pos.y + receiverHeight;
                else if (isPlacingTransmitter)
                    pos.y = pos.y + transmitterHeight;
            }

            var go = Instantiate(previewSourcePrefab, pos, previewInstance.transform.rotation);

            // Initialize fields from UI (same as your existing PlaceTransmitter/Receiver)
            if (isPlacingTransmitter)
            {
                var tx = go.GetComponent<Transmitter>();
                if (tx != null)
                {
                    float desiredH = transmitterHeight;
                    if (transmitterHeightInput != null && float.TryParse(transmitterHeightInput.text, out float h))
                        desiredH = Mathf.Max(0f, h);

                    // 3) apply by moving the whole model (uses your TX setter)
                    tx.SetTransmitterHeight(desiredH);

                    if (transmitterPowerInput != null && float.TryParse(transmitterPowerInput.text, out float p))
                        tx.transmitterPower = p;

                    if (transmitterFrequencyInput != null && float.TryParse(transmitterFrequencyInput.text, out float f))
                        tx.frequency = f;

                    if (transmitterPropagationModelDropdown != null)
                        tx.SetPropagationModel(_defaultTxModel);

                    if (showRaysToggle != null && showRaysToggle.isOn)
                    {
                        tx.EnableRayVisualization();
                        RecomputeRaysFor(tx);
                    }

                    tx.showConnections = showConnectionsToggle != null ? showConnectionsToggle.isOn : true;

                    if (statusUI != null)
                    {
                        statusUI.ShowTransmitter(tx);
                    }

                    UpdateStatusText($"Transmitter placed: {tx.transmitterPower:F1} dBm, {tx.frequency:F0} MHz @ {pos}");
                    StartCoroutine(SelectTransmitterNextFrame(tx));
                    SimulationManager.Instance?.RecomputeAllSignalStrength();
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

                    var txs = SimulationManager.Instance != null
                        ? SimulationManager.Instance.transmitters.ToArray()
                        : GameObject.FindObjectsByType<Transmitter>(FindObjectsSortMode.InstanceID);

                    foreach (var t in txs)
                    {
                        if (t == null) continue;
                        if (t.CanConnectTo(rx))
                            t.ConnectToReceiver(rx);
                    }

                    if (statusUI != null)
                    {
                        statusUI.ShowReceiver(rx);
                    }

                    if (showRaysToggle != null && showRaysToggle.isOn)
                    {
                        foreach (var t in txs)
                            RecomputeRaysFor(t);
                    }

                    UpdateStatusText($"Receiver placed: {rx.technology}, {rx.sensitivity:F1} dBm @ {pos}");
                    StartCoroutine(SelectReceiverNextFrame(rx));
                    SimulationManager.Instance?.RecomputeAllSignalStrength();
                }
            }

            // Done
            CancelPlacement();
        }

        private float SampleTerrainY(Vector3 at)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            Vector3 start = new Vector3(at.x, at.y + 1000f, at.z);
            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 2000f, placementLayerMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return at.y;
        }

        private System.Collections.IEnumerator SelectTransmitterNextFrame(Transmitter tx)
        {
            yield return null;              // wait one frame
            statusUI?.ShowTransmitter(tx);
        }
        private System.Collections.IEnumerator SelectReceiverNextFrame(Receiver rx)
        {
            yield return null;              // wait one frame
            statusUI?.ShowReceiver(rx);
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

        private void RecomputeRaysFor(Transmitter tx)
        {
            if (tx == null) return;
            var rxs = FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < rxs.Length; i++)
            {
                var rx = rxs[i];
                if (rx != null) tx.CalculateSignalStrength(rx.transform.position);
            }
        }

        private void UpdateStatusText(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private void OnDestroy()
        {
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.OnBuildingsToggled -= OnBuildingsToggled;
        }
    }
}
