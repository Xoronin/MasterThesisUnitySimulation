using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Core;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Components;
using RFSimulation.Environment;

namespace RFSimulation.Visualization
{
    [System.Serializable]
    public class HeatmapSettings
    {
        [Header("Heatmap Area & Sampling")]
        [Min(8)] public int resolution = 64;
        [Tooltip("Half-size in meters from center. The heatmap spans 2x this radius.")]
        public float sampleRadius = 1000f;
        [Tooltip("Vertical offset above terrain to avoid z-fighting.")]
        public float heightOffset = 0.7f;
        [Tooltip("Approx receiver height for sampling.")]
        public float sampleHeight = 1.5f;

        [Header("Layers")]
        [Tooltip("Layer(s) that Mapbox terrain tiles are on. Set this to your Terrain layer or Default if Mapbox tiles are Default.")]
        public LayerMask terrainLayer = 1 << 0; // Default
        [Tooltip("Layer(s) that buildings are on.")]
        public LayerMask buildingLayer = 1 << 8; // Example
        [Tooltip("Layer for the heatmap object so our raycasts never hit ourselves.")]
        public int heatmapLayer = 2; // 2 = IgnoreRaycast by Unity default

        [Header("Visual Settings")]
        public Material heatmapMaterial;
        public float minSignalStrength = -120f; // dBm
        public float maxSignalStrength = -40f;  // dBm

        [Header("Colors")]
        public Color noSignalColor = Color.clear; // transparent where invalid/blocked
        public Color weakSignalColor = Color.red;
        public Color mediumSignalColor = Color.yellow;
        public Color strongSignalColor = Color.green;

        [Header("Robustness")]
        [Tooltip("Meters to cast from above and below when probing the terrain.")]
        public float probeHeight = 5000f;
        [Tooltip("Extra tries in a small neighborhood if a direct ray miss happens. Helps avoid 'walls' at map edges or gaps between tiles.")]
        [Range(0, 4)] public int neighborhoodSearchRadius = 2; // grid steps
        [Tooltip("Meters per neighborhood step when searching around a miss.")]
        public float neighborhoodStepMeters = 2f;
    }

    public class SignalHeatmap : MonoBehaviour
    {
        [SerializeField] public HeatmapSettings settings = new HeatmapSettings();
        [SerializeField] private Transform centerPoint;
        [SerializeField] private List<Transmitter> transmitters = new List<Transmitter>();
        [SerializeField] private bool autoFindTransmitters = true;
        [SerializeField] private bool updateInRealTime = false;
        [SerializeField] private float updateInterval = 1f;
        [SerializeField] private bool keepRootOnTerrain = true;
        [SerializeField] private bool enabledByUI = false;

        private GameObject heatmapObject;
        private MeshRenderer heatmapRenderer;
        private MeshFilter heatmapFilter;
        private Texture2D heatmapTexture;
        private float lastUpdateTime;

        // Cache for terrain heights to avoid repeated raycasts
        private readonly Dictionary<Vector2Int, float> _terrainHeightCache = new Dictionary<Vector2Int, float>();

        private float _halfSize; // convenience
        private float _stepSize;

        void Awake()
        {
            if (centerPoint == null) centerPoint = transform;
            if (autoFindTransmitters) FindTransmitters();
        }

        void Start()
        {
            if (autoFindTransmitters) FindTransmitters();
            if (centerPoint == null) centerPoint = transform;

            // If UI toggle is OFF, do not create anything at startup
            if (!enabledByUI) return;

            if (!HasTransmitters()) return;

            // Ensure root sits on terrain before creating
            transform.position = centerPoint.position;
            float h = GetTerrainHeightAtPosition(transform.position, out bool ok);
            if (ok) transform.position = new Vector3(transform.position.x, h, transform.position.z);

            InitializeHeatmap();
            GenerateHeatmap();
        }

        public void SetUIEnabled(bool enabled)
        {
            enabledByUI = enabled;

            if (!enabledByUI)
            {
                // turn OFF → fully remove the GPU/texture stuff to save memory
                DestroyHeatmapIfExists();
                return;
            }

            // turn ON → (re)create lazily if we can
            if (HasTransmitters())
            {
                EnsureHeatmapCreated();
                if (heatmapObject != null) GenerateHeatmap();
            }
        }

        void Update()
        {
            if (!enabledByUI)          // UI OFF: ensure nothing exists and skip work
            {
                if (heatmapObject != null) DestroyHeatmapIfExists();
                return;
            }
            if (!updateInRealTime) return;
            if (Time.time - lastUpdateTime <= updateInterval) return;

            if (autoFindTransmitters) FindTransmitters();

            if (!HasTransmitters())
            {
                // no TX → remove heatmap and stop here
                DestroyHeatmapIfExists();
                lastUpdateTime = Time.time;
                return;
            }

            // ensure heatmap exists once we have TX
            EnsureHeatmapCreated();

            if (keepRootOnTerrain)
            {
                float h = GetTerrainHeightAtPosition(centerPoint.position, out bool ok);
                if (ok) transform.position = new Vector3(centerPoint.position.x, h, centerPoint.position.z);
            }

            GenerateHeatmap();
            lastUpdateTime = Time.time;
        }

        private void FindTransmitters()
        {
            transmitters.Clear();
            transmitters.AddRange(FindObjectsByType<Transmitter>(FindObjectsSortMode.InstanceID));
        }

        private void InitializeHeatmap()
        {
            _halfSize = settings.sampleRadius;
            _stepSize = (settings.sampleRadius * 2f) / settings.resolution;

            if (heatmapObject == null)
            {
                heatmapObject = new GameObject("SignalHeatmap_Mesh");
                heatmapObject.transform.SetParent(transform, false);
                heatmapObject.layer = settings.heatmapLayer; // avoid self-hits
                heatmapFilter = heatmapObject.AddComponent<MeshFilter>();
                heatmapRenderer = heatmapObject.AddComponent<MeshRenderer>();
            }

            // Create/assign texture
            if (heatmapTexture == null || heatmapTexture.width != settings.resolution)
            {
                heatmapTexture = new Texture2D(settings.resolution, settings.resolution, TextureFormat.RGBA32, false);
                heatmapTexture.filterMode = FilterMode.Bilinear;
                heatmapTexture.wrapMode = TextureWrapMode.Clamp;
            }

            // Setup material
            if (settings.heatmapMaterial == null)
            {
                settings.heatmapMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                settings.heatmapMaterial.SetFloat("_Surface", 1); // Transparent
                settings.heatmapMaterial.SetFloat("_Blend", 0);   // Alpha blend
            }

            settings.heatmapMaterial.mainTexture = heatmapTexture;
            heatmapRenderer.sharedMaterial = settings.heatmapMaterial;

            CreateHeatmapMesh();
        }

        private void CreateHeatmapMesh()
        {
            var mesh = new Mesh { name = "HeatmapMesh" };

            int vertsPerSide = settings.resolution + 1;
            int vertCount = vertsPerSide * vertsPerSide;
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[settings.resolution * settings.resolution * 6];

            for (int y = 0; y <= settings.resolution; y++)
            {
                for (int x = 0; x <= settings.resolution; x++)
                {
                    int index = y * vertsPerSide + x;

                    float localX = -_halfSize + (x * _stepSize);
                    float localZ = -_halfSize + (y * _stepSize);

                    Vector3 worldPos = transform.TransformPoint(new Vector3(localX, 0f, localZ));
                    float height = GetTerrainHeightAtPosition(worldPos, out bool ok);
                    if (!ok)
                    {
                        // Use center height as a sane fallback to avoid vertical "walls"
                        float centerH = GetTerrainHeightAtPosition(centerPoint.position, out bool centerOk);
                        height = centerOk ? centerH : transform.position.y;
                    }

                    Vector3 worldVertexPos = new Vector3(worldPos.x, height + settings.heightOffset, worldPos.z);
                    vertices[index] = transform.InverseTransformPoint(worldVertexPos);
                    uvs[index] = new Vector2((float)x / settings.resolution, (float)y / settings.resolution);
                }
            }

            int triIndex = 0;
            for (int y = 0; y < settings.resolution; y++)
            {
                for (int x = 0; x < settings.resolution; x++)
                {
                    int bl = y * vertsPerSide + x;
                    int br = bl + 1;
                    int tl = bl + vertsPerSide;
                    int tr = tl + 1;

                    triangles[triIndex++] = bl;
                    triangles[triIndex++] = tl;
                    triangles[triIndex++] = br;

                    triangles[triIndex++] = br;
                    triangles[triIndex++] = tl;
                    triangles[triIndex++] = tr;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            heatmapFilter.sharedMesh = mesh;
        }

        private float GetTerrainHeightAtPosition(Vector3 worldPos, out bool hitOK)
        {
            // Use integer-based cache key to avoid floating point precision issues
            Vector2Int key = new Vector2Int(
                Mathf.RoundToInt(worldPos.x * 100f), // 1cm precision
                Mathf.RoundToInt(worldPos.z * 100f)
            );

            if (_terrainHeightCache.TryGetValue(key, out float cached))
            {
                hitOK = true;
                return cached;
            }

            // Single deterministic probe attempt - no random fallbacks
            if (ProbeTerrainDeterministic(worldPos, out float height))
            {
                _terrainHeightCache[key] = height;
                hitOK = true;
                return height;
            }

            // If direct probe fails, try a small deterministic offset grid
            float[] offsets = { 0f, 0.1f, -0.1f, 0.2f, -0.2f }; // Fixed, ordered offsets

            for (int x = 0; x < offsets.Length; x++)
            {
                for (int z = 0; z < offsets.Length; z++)
                {
                    Vector3 testPos = new Vector3(
                        worldPos.x + offsets[x],
                        worldPos.y,
                        worldPos.z + offsets[z]
                    );

                    if (ProbeTerrainDeterministic(testPos, out height))
                    {
                        _terrainHeightCache[key] = height;
                        hitOK = true;
                        return height;
                    }
                }
            }

            // Final fallback - use center terrain height if available
            if (_terrainHeightCache.Count > 0)
            {
                // Get center position cache if it exists
                Vector2Int centerKey = new Vector2Int(
                    Mathf.RoundToInt(centerPoint.position.x * 100f),
                    Mathf.RoundToInt(centerPoint.position.z * 100f)
                );

                if (_terrainHeightCache.TryGetValue(centerKey, out float centerHeight))
                {
                    _terrainHeightCache[key] = centerHeight;
                    hitOK = true;
                    return centerHeight;
                }
            }

            // Ultimate fallback
            float fallbackHeight = transform.position.y;
            _terrainHeightCache[key] = fallbackHeight;
            hitOK = false;
            return fallbackHeight;
        }

        private bool ProbeTerrainDeterministic(Vector3 worldPos, out float height)
        {
            height = 0f;
            float probeDistance = Mathf.Abs(settings.probeHeight);

            // Single raycast down from above - no multiple attempts
            Vector3 startPos = worldPos + Vector3.up * probeDistance;

            if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit,
                probeDistance * 2f, settings.terrainLayer, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }

            return false;
        }

        private bool ProbeTerrainSeamSafe(Vector3 worldPos, out float height)
        {
            // First try a spherecast (grabs tiny gaps)
            if (ProbeTerrain(worldPos, out height))
                return true;

            // If miss, try tiny plus-pattern around point (seam dodge)
            float eps = Mathf.Max(0.1f, _stepSize * 0.1f); // ~10% of cell step
            Vector3[] offsets = new[]
            {
                Vector3.zero,
                new Vector3( eps, 0f,  0f),
                new Vector3(-eps, 0f,  0f),
                new Vector3( 0f, 0f,  eps),
                new Vector3( 0f, 0f, -eps),
            };

            List<float> hits = new List<float>(5);
            foreach (var o in offsets)
            {
                if (ProbeTerrain(worldPos + o, out float h)) hits.Add(h);
            }

            if (hits.Count > 0)
            {
                hits.Sort();
                height = hits[hits.Count / 2]; // median
                return true;
            }

            height = 0f;
            return false;
        }

        private bool ProbeTerrain(Vector3 worldPos, out float height)
        {
            height = 0f;
            float up = Mathf.Abs(settings.probeHeight);
            float radius = Mathf.Max(0.2f, _stepSize * 0.25f); // small but forgiving

            Vector3 startAbove = worldPos + Vector3.up * up;

            // SphereCast down first (best for seams)
            if (Physics.SphereCast(startAbove, radius, Vector3.down,
                out RaycastHit hitD, up * 2f, settings.terrainLayer, QueryTriggerInteraction.Ignore))
            { height = hitD.point.y; return true; }

            // Ray down
            if (Physics.Raycast(startAbove, Vector3.down,
                out hitD, up * 2f, settings.terrainLayer, QueryTriggerInteraction.Ignore))
            { height = hitD.point.y; return true; }

            // Ray up from below
            Vector3 startBelow = worldPos + Vector3.down * up;
            if (Physics.Raycast(startBelow, Vector3.up,
                out RaycastHit hitU, up * 2f, settings.terrainLayer, QueryTriggerInteraction.Ignore))
            { height = hitU.point.y; return true; }

            return false;
        }


        public void GenerateHeatmap()
        {
            if (transmitters.Count == 0)
            {
                return;
            }

            Color[] pixels = new Color[settings.resolution * settings.resolution];

            for (int y = 0; y < settings.resolution; y++)
            {
                for (int x = 0; x < settings.resolution; x++)
                {
                    float localX = -_halfSize + (x * _stepSize);
                    float localZ = -_halfSize + (y * _stepSize);

                    Vector3 samplePos = transform.TransformPoint(new Vector3(localX, 0f, localZ));
                    float terrainH = GetTerrainHeightAtPosition(samplePos, out bool ok);
                    if (!ok)
                    {
                        pixels[y * settings.resolution + x] = Color.clear;
                        continue;
                    }

                    samplePos.y = terrainH + settings.sampleHeight;

                    if (IsPositionBlockedByBuildings(samplePos))
                    {
                        pixels[y * settings.resolution + x] = Color.clear; // transparent over buildings
                        continue;
                    }

                    float maxRssi = float.NegativeInfinity;
                    for (int i = 0; i < transmitters.Count; i++)
                    {
                        var t = transmitters[i];
                        if (t == null) continue;
                        float rssi = t.CalculateSignalStrength(samplePos);
                        if (rssi > maxRssi) maxRssi = rssi;
                    }

                    pixels[y * settings.resolution + x] = SignalStrengthToColor(maxRssi);
                }
            }

            heatmapTexture.SetPixels(pixels);
            heatmapTexture.Apply(false, false);
        }

        private bool IsPositionBlockedByBuildings(Vector3 position)
        {
            // Check global building state first
            if (!BuildingManager.AreBuildingsEnabled())
                return false; // Buildings are globally disabled

            const float probeRadius = 0.5f;

            // Use the active building layers from the global manager
            LayerMask activeBuildingLayers = BuildingManager.GetActiveBuildingLayers();
            if (activeBuildingLayers == 0)
                return false; // No active building layers

            // Check for overlap with buildings
            Collider[] overlapping = Physics.OverlapSphere(position, probeRadius, activeBuildingLayers, QueryTriggerInteraction.Ignore);

            foreach (var collider in overlapping)
            {
                // Additional check: if the object has a Building component, verify it should block signals
                var building = collider.GetComponent<Building>();
                if (building != null && !building.blockSignals)
                {
                    continue; // This building is set to not block signals
                }

                // If we get here, there's a blocking building
                return true;
            }

            return false;
        }

        private Color SignalStrengthToColor(float rssi)
        {
            if (float.IsNegativeInfinity(rssi)) return settings.noSignalColor;
            float n = Mathf.InverseLerp(settings.minSignalStrength, settings.maxSignalStrength, rssi);
            n = Mathf.Clamp01(n);

            if (n < 0.33f)
                return Color.Lerp(settings.noSignalColor, settings.weakSignalColor, n / 0.33f);
            else if (n < 0.66f)
                return Color.Lerp(settings.weakSignalColor, settings.mediumSignalColor, (n - 0.33f) / 0.33f);
            else
                return Color.Lerp(settings.mediumSignalColor, settings.strongSignalColor, (n - 0.66f) / 0.34f);
        }

        private bool HasTransmitters()
        {
            // prune nulls just in case
            transmitters.RemoveAll(t => t == null);
            return transmitters.Count > 0;
        }

        private void EnsureHeatmapCreated()
        {
            if (!enabledByUI) return;
            if (heatmapObject != null) return;

            // snap root to terrain before creating mesh
            float h = GetTerrainHeightAtPosition(centerPoint.position, out bool ok);
            if (ok) transform.position = new Vector3(centerPoint.position.x, h, centerPoint.position.z);

            InitializeHeatmap();
        }

        private void DestroyHeatmapIfExists()
        {
            if (heatmapTexture != null) { DestroyImmediate(heatmapTexture); heatmapTexture = null; }
            if (heatmapObject != null) { DestroyImmediate(heatmapObject); heatmapObject = null; }
            heatmapFilter = null;
            heatmapRenderer = null;
        }

        // Public API
        public void SetCenter(Transform newCenter)
        {
            centerPoint = newCenter != null ? newCenter : transform;
            _terrainHeightCache.Clear();
            CreateHeatmapMesh();
            GenerateHeatmap();
        }

        public void AddTransmitter(Transmitter t)
        {
            if (t != null && !transmitters.Contains(t))
            {
                transmitters.Add(t);
                if (HasTransmitters())
                {
                    EnsureHeatmapCreated();
                    GenerateHeatmap();
                }
            }
        }

        public void RemoveTransmitter(Transmitter t)
        {
            if (t != null && transmitters.Remove(t))
            {
                if (!HasTransmitters())
                    DestroyHeatmapIfExists();
                else
                    GenerateHeatmap();
            }
        }

        public void UpdateSettings(HeatmapSettings s)
        {
            settings = s;
            _terrainHeightCache.Clear();
            InitializeHeatmap();
            GenerateHeatmap();
        }

        public void ToggleVisibility(bool visible)
        {
            if (heatmapRenderer != null) heatmapRenderer.enabled = visible;
        }

        void OnDestroy()
        {
            if (heatmapTexture != null) DestroyImmediate(heatmapTexture);
            _terrainHeightCache.Clear();
        }
    }
}
