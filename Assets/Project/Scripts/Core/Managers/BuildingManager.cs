using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Environment;

namespace RFSimulation.Core.Managers
{
    /// <summary>
    /// Global singleton manager for controlling building visibility and RF interactions
    /// This integrates with all signal calculations throughout the project
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        private static BuildingManager _instance;
        public static BuildingManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<BuildingManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("BuildingManager");
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
        [SerializeField] private int disabledBuildingLayer = 2; // IgnoreRaycast layer

        [Header("Events")]
        public System.Action<bool> OnBuildingsToggled;

        // Internal state
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
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            RefreshBuildingsList();
            initialized = true;
            Debug.Log($"[GlobalBuildingManager] Initialized with {managedBuildings.Count} buildings");
        }

        /// <summary>
        /// Check if buildings are currently enabled for RF calculations
        /// This is the main method other classes should call
        /// </summary>
        public static bool AreBuildingsEnabled()
        {
            return Instance.buildingsEnabled;
        }

        /// <summary>
        /// Get the current building layers that should be used for raycasting
        /// Returns 0 (no layers) when buildings are disabled
        /// </summary>
        public static LayerMask GetActiveBuildingLayers()
        {
            if (!Instance.buildingsEnabled)
                return 0; // No building layers when disabled

            return Instance.originalBuildingLayers;
        }

        /// <summary>
        /// Toggle buildings on/off globally
        /// </summary>
        public void ToggleBuildings()
        {
            SetBuildingsEnabled(!buildingsEnabled);
        }

        /// <summary>
        /// Set buildings enabled/disabled state
        /// </summary>
        public void SetBuildingsEnabled(bool enabled)
        {
            if (buildingsEnabled == enabled) return;

            buildingsEnabled = enabled;
            ApplyBuildingState();
            OnBuildingsToggled?.Invoke(buildingsEnabled);

            Debug.Log($"[GlobalBuildingManager] Buildings {(buildingsEnabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Apply the current building state to all managed buildings
        /// </summary>
        private void ApplyBuildingState()
        {
            foreach (var building in managedBuildings)
            {
                if (building.gameObject == null) continue;

                if (buildingsEnabled)
                {
                    // Restore buildings
                    building.gameObject.layer = building.originalLayer;

                    if (building.renderer != null)
                        building.renderer.enabled = true;

                    if (building.buildingComponent != null)
                        building.buildingComponent.blockSignals = building.originalBlockSignals;
                }
                else
                {
                    // Disable buildings for RF simulation
                    building.gameObject.layer = disabledBuildingLayer;

                    // Optionally hide visually too
                    if (building.renderer != null)
                        building.renderer.enabled = false;

                    if (building.buildingComponent != null)
                        building.buildingComponent.blockSignals = false;
                }
            }
        }

        /// <summary>
        /// Refresh the list of managed buildings
        /// </summary>
        public void RefreshBuildingsList()
        {
            managedBuildings.Clear();

            // Find all Building components
            Building[] buildingComponents = FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var building in buildingComponents)
            {
                AddBuildingToManagement(building.gameObject);
            }

            // Find objects on building layers without Building component
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                if (((1 << obj.layer) & originalBuildingLayers) != 0)
                {
                    // Check if already added
                    bool alreadyManaged = false;
                    foreach (var managed in managedBuildings)
                    {
                        if (managed.gameObject == obj)
                        {
                            alreadyManaged = true;
                            break;
                        }
                    }

                    if (!alreadyManaged)
                    {
                        AddBuildingToManagement(obj);
                    }
                }
            }

            // Apply current state to all buildings
            ApplyBuildingState();

            Debug.Log($"[GlobalBuildingManager] Refreshed building list: {managedBuildings.Count} buildings found");
        }

        /// <summary>
        /// Add a building to management
        /// </summary>
        private void AddBuildingToManagement(GameObject buildingObj)
        {
            var buildingData = new BuildingData
            {
                gameObject = buildingObj,
                originalLayer = buildingObj.layer,
                buildingComponent = buildingObj.GetComponent<Building>(),
                renderer = buildingObj.GetComponent<Renderer>()
            };

            if (buildingData.buildingComponent != null)
            {
                buildingData.originalBlockSignals = buildingData.buildingComponent.blockSignals;
            }
            else
            {
                buildingData.originalBlockSignals = true; // Default assumption
            }

            managedBuildings.Add(buildingData);
        }

        /// <summary>
        /// Register a new building at runtime
        /// </summary>
        public static void RegisterBuilding(GameObject building)
        {
            Instance.AddBuildingToManagement(building);
            Instance.ApplyBuildingState(); // Apply current state immediately
        }

        /// <summary>
        /// Unregister a building
        /// </summary>
        public static void UnregisterBuilding(GameObject building)
        {
            for (int i = Instance.managedBuildings.Count - 1; i >= 0; i--)
            {
                if (Instance.managedBuildings[i].gameObject == building)
                {
                    Instance.managedBuildings.RemoveAt(i);
                    break;
                }
            }
        }

        // Context menu helpers for testing
        [ContextMenu("Toggle Buildings")]
        public void ToggleBuildingsContextMenu()
        {
            ToggleBuildings();
        }

        [ContextMenu("Refresh Buildings List")]
        public void RefreshBuildingsListContextMenu()
        {
            RefreshBuildingsList();
        }

        [ContextMenu("Enable Buildings")]
        public void EnableBuildings()
        {
            SetBuildingsEnabled(true);
        }

        [ContextMenu("Disable Buildings")]
        public void DisableBuildings()
        {
            SetBuildingsEnabled(false);
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}