using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Environment;

namespace RFSimulation.Core.Managers
{
    public class BuildingManager : MonoBehaviour
    {
        private static BuildingManager _instance;

        // NEW: shutdown guard + quick checker
        private static bool _isShuttingDown;
        public static bool HasInstance => _instance != null && !_isShuttingDown;

        public static BuildingManager Instance
        {
            get
            {
                // Never create while tearing down
                if (_isShuttingDown) return _instance;

                if (_instance == null)
                {
                    // Don't create when not playing (e.g., domain reload/editor close)
                    if (!Application.isPlaying) return _instance;

                    _instance = FindAnyObjectByType<BuildingManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[BuildingManager]");
                        _instance = go.AddComponent<BuildingManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Building State")]
        [SerializeField] private bool buildingsEnabled = true;

        [Header("Building Detection")]
        [SerializeField] private LayerMask originalBuildingLayers = (1 << 8);
        [SerializeField] private int disabledBuildingLayer = 2; // IgnoreRaycast

        [Header("Events")]
        public System.Action<bool> OnBuildingsToggled;

        private List<BuildingData> managedBuildings = new List<BuildingData>();
        private bool initialized = false;

        private struct BuildingData
        {
            public GameObject gameObject;
            public int originalLayer;
            public Building buildingComponent;
            public bool originalBlockSignals;
            public Renderer renderer;
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            Initialize();
        }

        void Start()
        {
            if (!initialized) Initialize();
        }

        private void Initialize()
        {
            RefreshBuildingsList();
            initialized = true;
        }

        // ---------- STATIC HELPERS (no lazy-creation calls!) ----------

        public static bool AreBuildingsEnabled()
            => HasInstance && _instance.buildingsEnabled;

        public static LayerMask GetActiveBuildingLayers()
            => (HasInstance && _instance.buildingsEnabled) ? _instance.originalBuildingLayers : 0;

        public static void RegisterBuilding(GameObject building)
        {
            if (!HasInstance || building == null) return;
            _instance.AddBuildingToManagement(building);
            _instance.ApplyBuildingState();
        }

        public static void UnregisterBuilding(GameObject building)
        {
            if (!HasInstance || building == null) return;
            for (int i = _instance.managedBuildings.Count - 1; i >= 0; i--)
                if (_instance.managedBuildings[i].gameObject == building)
                    _instance.managedBuildings.RemoveAt(i);
        }

        // --------------------------------------------------------------

        public void ToggleBuildings() => SetBuildingsEnabled(!buildingsEnabled);

        public void SetBuildingsEnabled(bool enabled)
        {
            if (buildingsEnabled == enabled) return;
            buildingsEnabled = enabled;
            ApplyBuildingState();
            OnBuildingsToggled?.Invoke(buildingsEnabled);
        }

        private void ApplyBuildingState()
        {
            foreach (var b in managedBuildings)
            {
                if (b.gameObject == null) continue;

                if (buildingsEnabled)
                {
                    b.gameObject.layer = b.originalLayer;
                    if (b.renderer != null) b.renderer.enabled = true;
                    if (b.buildingComponent != null) b.buildingComponent.blockSignals = b.originalBlockSignals;
                }
                else
                {
                    b.gameObject.layer = disabledBuildingLayer;
                    if (b.renderer != null) b.renderer.enabled = false;
                    if (b.buildingComponent != null) b.buildingComponent.blockSignals = false;
                }
            }
        }

        public void RefreshBuildingsList()
        {
            managedBuildings.Clear();

            var buildingComponents = FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var b in buildingComponents) AddBuildingToManagement(b.gameObject);

            var all = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var obj in all)
            {
                if (((1 << obj.layer) & originalBuildingLayers) == 0) continue;

                bool already = false;
                for (int i = 0; i < managedBuildings.Count; i++)
                    if (managedBuildings[i].gameObject == obj) { already = true; break; }

                if (!already) AddBuildingToManagement(obj);
            }

            ApplyBuildingState();
        }

        private void AddBuildingToManagement(GameObject buildingObj)
        {
            var data = new BuildingData
            {
                gameObject = buildingObj,
                originalLayer = buildingObj.layer,
                buildingComponent = buildingObj.GetComponent<Building>(),
                renderer = buildingObj.GetComponent<Renderer>(),
                originalBlockSignals = buildingObj.GetComponent<Building>()?.blockSignals ?? true
            };
            managedBuildings.Add(data);
        }

        // Context menu helpers unchanged...

        void OnApplicationQuit() { _isShuttingDown = true; }
        void OnDestroy()
        {
            _isShuttingDown = true;       // mark before nulling to prevent re-creation
            if (_instance == this) _instance = null;
        }
    }
}
