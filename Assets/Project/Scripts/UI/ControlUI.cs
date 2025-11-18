using RFSimulation.Core.Components;
using RFSimulation.Core.Managers;
using RFSimulation.Propagation.Core;
using RFSimulation.Utils;
using RFSimulation.Visualization;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        public Dropdown propagationModelDropdown;
        public InputField transmitterPowerInput;
        public InputField transmitterFrequencyInput;
        public Dropdown technologyDropdown;
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
        public float transmitterHeight = 10f;
        public float receiverHeight = 1.5f;
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

        private PropagationModel _defaultTxModel = PropagationModel.RayTracing;

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

            // Dropdowns
            if (technologyDropdown != null)
                technologyDropdown.onValueChanged.AddListener(OnTechnologyChanged);

            if (propagationModelDropdown != null)
                propagationModelDropdown.onValueChanged.AddListener(OnPropagationModelChanged);

            // Auto-find GroundGrid if not assigned
            if (groundGridComponent == null)
            {
                groundGridComponent = FindFirstObjectByType<GroundGrid>();
            }

            // Initialize dropdowns
            InitializeTechnologyDropdown();
            InitializePropagationModelDropdown();

            // Defaults
            SetDefaultUIValues();

            UpdateStatusText("Ready to place objects");
        }

        private void InitializeTechnologyDropdown()
        {
            if (technologyDropdown != null)
            {
                var technologies = new List<string>();
                var technologiesSpec = TechnologySpecifications.GetAllSpecs().ToList();
                for (int i = 0; i < technologiesSpec.Count; i++)
                {
                    technologies.Add(technologiesSpec[i].Name);
                }

                technologyDropdown.ClearOptions();
                technologyDropdown.AddOptions(technologies);
                technologyDropdown.value = 0;

            }
        }

        private void OnTechnologyChanged(int index)
        {
            if (technologyDropdown == null) return;

            var tech = TechnologySpecifications.ParseTechnologyString(technologyDropdown.options[index].text);
            var spec = TechnologySpecifications.GetSpec(tech);

            // RX defaults
            if (receiverSensitivityInput != null)
                receiverSensitivityInput.text = spec.SensitivityDbm.ToString("F1");

            if (receiverHeightInput != null)
                receiverHeightInput.text = spec.TypicalRxHeight.ToString("F1");

            // TX defaults 
            if (transmitterFrequencyInput != null)
                transmitterFrequencyInput.text = MathHelper.MHzToGHz(spec.TypicalFrequencyMHz).ToString("F2");

            if (transmitterPowerInput != null)
                transmitterPowerInput.text = spec.TypicalTxPowerDbm.ToString("F1");

            if (transmitterHeightInput != null)
                transmitterHeightInput.text = spec.TypicalTxHeight.ToString("F1");
        }

        private static readonly string[] ModelNames =
            { "FreeSpace", "LogD", "LogNShadow", "Hata", "COST231", "RayTracing" };

        private static PropagationModel ModelFromIndex(int i)
        {
            switch (i)
            {
                case 0: return PropagationModel.FreeSpace;
                case 1: return PropagationModel.LogD;
                case 2: return PropagationModel.LogNShadow;
                case 3: return PropagationModel.Hata;    
                case 4: return PropagationModel.COST231;   
                case 5: return PropagationModel.RayTracing;
                default: return PropagationModel.RayTracing;
            }
        }

        private static int IndexFromModel(PropagationModel m)
        {
            switch (m)
            {
                case PropagationModel.FreeSpace: return 0;
                case PropagationModel.LogD: return 1;
                case PropagationModel.LogNShadow: return 2;
                case PropagationModel.Hata: return 3; 
                case PropagationModel.COST231: return 4;  
                case PropagationModel.RayTracing: return 5;
                default: return 5;
            }
        }

        private void InitializePropagationModelDropdown()
        {
            if (propagationModelDropdown == null) return;

            propagationModelDropdown.ClearOptions();
            propagationModelDropdown.AddOptions(new List<string>(ModelNames));

            if (transmitterPrefab != null)
            {
                var tx = transmitterPrefab.GetComponent<Transmitter>();
                if (tx != null)
                    _defaultTxModel = tx.settings.propagationModel;
            }

            propagationModelDropdown.value = IndexFromModel(_defaultTxModel);
            propagationModelDropdown.RefreshShownValue();
        }

        private void OnPropagationModelChanged(int i)
        {
            _defaultTxModel = ModelFromIndex(i);
        }


        private void SetDefaultUIValues()
        {
            var allSpecs = TechnologySpecifications.GetAllSpecs().ToList();
            if (allSpecs.Count > 0)
            {
                var spec = allSpecs[0];

                if (receiverSensitivityInput != null)
                    receiverSensitivityInput.text = spec.SensitivityDbm.ToString("F1");

                if (receiverHeightInput != null)
                    receiverHeightInput.text = spec.TypicalRxHeight.ToString("F1");

                if (transmitterFrequencyInput != null)
                    transmitterFrequencyInput.text = MathHelper.MHzToGHz(spec.TypicalFrequencyMHz).ToString("F2");

                if (transmitterPowerInput != null)
                    transmitterPowerInput.text = spec.TypicalTxPowerDbm.ToString("F1");

                if (transmitterHeightInput != null)
                    transmitterHeightInput.text = spec.TypicalTxHeight.ToString("F1");
            }

            if (showConnectionsToggle != null) showConnectionsToggle.isOn = true;
            if (showGridToggle != null) showGridToggle.isOn = true;

            if (showBuildingsToggle != null)
                showBuildingsToggle.isOn = BuildingManager.AreBuildingsEnabled();

            if (showHeatmapToggle != null) showHeatmapToggle.isOn = false;

            if (showRaysToggle != null) showRaysToggle.isOn = false;

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
            SimulationManager.Instance?.RecomputeAllSignalStrength(true);
        }

        private void OnBuildingsToggled(bool enabled)
        {
            if (showBuildingsToggle != null)
                showBuildingsToggle.SetIsOnWithoutNotify(enabled);

            UpdateStatusText($"Buildings {(enabled ? "enabled" : "disabled")} - RF calculations updated");
            SimulationManager.Instance?.RecomputeAllSignalStrength(true);
        }

        private void ToggleHeatmap(bool enabled)
        {
            var heatmap = FindAnyObjectByType<HeatmapVisualization>();
            heatmap.SetUIEnabled(enabled);

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
                    tx.settings.showRayPaths = true;
                    tx.EnableRayVisualization();
                    RecomputeRaysFor(tx);          
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
            if (enableGridSnap && groundGridComponent != null)
                position = groundGridComponent.SnapToGrid(position);

            if (groundGridComponent != null)
            {
                var probe = new Vector3(position.x, groundGridComponent.raycastStartHeight, position.z);
                if (Physics.Raycast(new Ray(probe, Vector3.down), out var hit, Mathf.Infinity, groundGridComponent.terrainMask, QueryTriggerInteraction.Ignore))
                    position.y = hit.point.y;
            }

            var go = Instantiate(transmitterPrefab, position, Quaternion.identity);
            PlaceObjectsHelper.Organize(go);

            var tx = go.GetComponent<Transmitter>();
            if (tx != null)
            {
                if (transmitterPowerInput != null && float.TryParse(transmitterPowerInput.text, out float p))
                    tx.settings.transmitterPower = p;

                if (transmitterFrequencyInput != null && float.TryParse(transmitterFrequencyInput.text, out float fGHz))
                {
                    float fMHz = MathHelper.GHzToMHz(fGHz);
                    tx.settings.frequency = fMHz;
                }

                if (propagationModelDropdown != null)
                {
                    var chosenModel = (propagationModelDropdown != null)
                        ? ModelFromIndex(propagationModelDropdown.value)
                        : _defaultTxModel; 

                    tx.SetPropagationModel(chosenModel);
                }

                if (transmitterHeightInput != null && float.TryParse(transmitterHeightInput.text, out float h))
                {
                    tx.SetTransmitterHeight(Mathf.Max(0f, h));
                }

                var tech = TechnologySpecifications.ParseTechnologyString(
                    technologyDropdown.options[technologyDropdown.value].text
                );
                tx.SetTechnology(tech);

                tx.settings.showConnections = showConnectionsToggle != null ? showConnectionsToggle.isOn : true;

                UpdateStatusText($"Transmitter placed: {tx.settings.transmitterPower:F1} dBm, {tx.settings.frequency:F0} MHz @ {position}");

                statusUI?.ShowTransmitter(tx);
            }
        }

        private void PlaceReceiver(Vector3 position)
        {
            position.y += receiverHeight;

            var go = Instantiate(receiverPrefab, position, Quaternion.identity);
            PlaceObjectsHelper.Organize(go);
            var rx = go.GetComponent<Receiver>();
            if (rx != null)
            {
                if (technologyDropdown != null)
                {
                    var tech = TechnologySpecifications.ParseTechnologyString(technologyDropdown.options[technologyDropdown.value].text);
                    rx.SetTechnology(tech); 
                }

                if (receiverSensitivityInput != null &&
                    float.TryParse(receiverSensitivityInput.text, out float s))
                {
                    rx.sensitivity = s;
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

            if (RaycastHelper.RayToGround(mainCamera, Input.mousePosition, placementLayerMask, out var hit))
            {
                pos = hit.point;
                return true;
            }
            return false;
        }

        private void BeginPlacement(GameObject prefab, float heightOffset)
        {
            if (previewInstance != null) Destroy(previewInstance);

            previewSourcePrefab = prefab;
            previewHeightOffset = heightOffset;

            previewInstance = Instantiate(prefab);
            previewInstance.name = prefab.name + "_PREVIEW";
            previewInstance.layer = 2;

            foreach (var col in previewInstance.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            var renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (previewMaterial != null)
                {
                    r.sharedMaterial = previewMaterial;
                }
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

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                previewInstance.SetActive(false);
                return;
            }

            if (mainCamera == null) mainCamera = Camera.main;
            var ray = mainCamera.ScreenPointToRay(Input.mousePosition);

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
            var mask = (terrainLayerMask.value != 0) ? terrainLayerMask : placementLayerMask;

            var hits = Physics.RaycastAll(ray, maxDist, mask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                bool isBuilding = (buildingLayerMask.value & (1 << h.collider.gameObject.layer)) != 0;
                if (isBuilding) continue;                 
                hit = h;
                return true;
            }
            return false;
        }

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
            }

            var go = Instantiate(previewSourcePrefab, pos, previewInstance.transform.rotation);
            PlaceObjectsHelper.Organize(go);

            if (isPlacingTransmitter)
            {
                var tx = go.GetComponent<Transmitter>();
                if (tx != null)
                {
                    float desiredH = transmitterHeight;
                    if (transmitterHeightInput != null && float.TryParse(transmitterHeightInput.text, out float h))
                        desiredH = Mathf.Max(0f, h);

                    tx.SetTransmitterHeight(desiredH);

                    if (transmitterPowerInput != null && float.TryParse(transmitterPowerInput.text, out float p))
                        tx.settings.transmitterPower = p;

                    if (transmitterFrequencyInput != null && float.TryParse(transmitterFrequencyInput.text, out float fGHz))
                    {
                        float fMHz = MathHelper.GHzToMHz(fGHz);
                        tx.settings.frequency = fMHz;
                    }

                    if (propagationModelDropdown != null)
                    {
                        var chosenModel = (propagationModelDropdown != null)
                            ? ModelFromIndex(propagationModelDropdown.value)
                            : _defaultTxModel;

                        tx.SetPropagationModel(chosenModel);
                    }

                    if (technologyDropdown != null)
                    {
                        var tech = TechnologySpecifications.ParseTechnologyString(
                            technologyDropdown.options[technologyDropdown.value].text
                        );
                        tx.SetTechnology(tech);
                    }

                    if (showRaysToggle != null && showRaysToggle.isOn)
                    {
                        tx.EnableRayVisualization();
                        RecomputeRaysFor(tx);
                    }

                    tx.settings.showConnections = showConnectionsToggle != null ? showConnectionsToggle.isOn : true;

                    if (statusUI != null)
                    {
                        statusUI.ShowTransmitter(tx);
                    }

                    UpdateStatusText($"Transmitter placed: {tx.settings.transmitterPower:F1} dBm, {tx.settings.frequency:F0} MHz @ {pos}");
                    StartCoroutine(SelectTransmitterNextFrame(tx));
                    SimulationManager.Instance?.RecomputeAllSignalStrength(true);
                }
            }
            else if (isPlacingReceiver)
            {
                var rx = go.GetComponent<Receiver>();
                if (rx != null)
                {
                    if (technologyDropdown != null)
                    {
                        var tech = TechnologySpecifications.ParseTechnologyString(technologyDropdown.options[technologyDropdown.value].text);
                        rx.SetTechnology(tech);
                    }

                    if (receiverSensitivityInput != null &&
                        float.TryParse(receiverSensitivityInput.text, out float s))
                    {
                        rx.sensitivity = s;
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
                        statusUI.ShowReceiver(rx);

                    if (showRaysToggle != null && showRaysToggle.isOn)
                    {
                        foreach (var t in txs)
                            RecomputeRaysFor(t);
                    }

                    UpdateStatusText($"Receiver placed: {rx.technology}, {rx.sensitivity:F1} dBm @ {pos}");
                    StartCoroutine(SelectReceiverNextFrame(rx));
                }
            }

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
            if (groundGridComponent == null)
                groundGridComponent = FindFirstObjectByType<GroundGrid>(FindObjectsInactive.Include);

            if (groundGridComponent != null)
            {
                groundGridComponent.SetGridVisibility(show);
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
                    if (transmitterPowerInput != null) transmitterPowerInput.text = tx.settings.transmitterPower.ToString();
                    if (transmitterFrequencyInput != null)
                        transmitterFrequencyInput.text = MathHelper.MHzToGHz(tx.settings.frequency).ToString("F2");
                }
            }

            if (receiverPrefab != null)
            {
                var rx = receiverPrefab.GetComponent<Receiver>();
                if (rx != null)
                {
                    if (receiverSensitivityInput != null) receiverSensitivityInput.text = rx.sensitivity.ToString();

                    if (technologyDropdown != null)
                    {
                        var opts = technologyDropdown.options;
                        for (int i = 0; i < opts.Count; i++)
                        {
                            if (opts[i].text.ToLower() == rx.technology.ToString().ToLower())
                            {
                                technologyDropdown.value = i;
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
            if (tx.settings.propagationModel != PropagationModel.RayTracing) return;
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
