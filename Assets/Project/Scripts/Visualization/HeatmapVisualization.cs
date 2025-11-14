using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Core;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Components;
using RFSimulation.Environment;
using RFSimulation.Utils;
using RFSimulation.UI;
using System.Collections;
using Mapbox.Unity.Map;


namespace RFSimulation.Visualization
{
    [System.Serializable]
    public class HeatmapSettings
    {
        [Header("Heatmap Area & Sampling")]
        private AbstractMap mapboxMap;
        [Min(8)] public int resolution = 32;
        public float sampleRadius = 5000f;
        public float heightOffset = 1.5f;
        public float sampleHeight = 1.5f;
        public float probeHeight = 1000f;
        public float neighborhoodStepMeters = 1.0f;
        public int neighborhoodSearchRadius = 10;

        [Header("Layers")]
        public LayerMask terrainLayer;
        public LayerMask buildingLayer;
        public int heatmapLayer = 2;

        [Header("Visual Settings")]
        public Material heatmapMaterial;
        public float minSignalStrength = -120f;
        public float maxSignalStrength = -10f;

        [Header("Colors")]
        public Color noSignalColor = Color.red;
        public Color lowSignalColor = Color.orange;
        public Color mediumSignalColor = Color.yellow;
        public Color highSignalColor = Color.yellowGreen;
        public Color excellentSignalColor = Color.green;

        [Header("Color Mapping")]
        public bool autoScaleColors = false;
        public float clampMin = -120f;
        public float clampMax = 10f;

        [Header("Absolute Band Cutoffs (dBm)")]
        public bool useAbsoluteBands = true;
        public float excellentCutoffDbm = -70f;
        public float goodCutoffDbm = -85f;
        public float poorCutoffDbm = -100f;
        public float veryPoorCutoffDbm = -110f;
    }

    public class HeatmapVisualization : MonoBehaviour
    {
        public HeatmapSettings settings = new HeatmapSettings();
        private Transform centerPoint;
        private List<Transmitter> transmitters = new List<Transmitter>();
        private bool keepRootOnTerrain = true;
        public bool enabledByUI = false;

        private GroundGrid groundGrid;

        public HeatmapUI heatmapUI;
        private GameObject heatmapObject;
        private MeshRenderer heatmapRenderer;
        private MeshFilter heatmapFilter;
        private Texture2D heatmapTexture;
        private float lastUpdateTime;

        private readonly Dictionary<Vector2Int, float> _terrainHeightCache = new Dictionary<Vector2Int, float>();

        private float _halfSize;
        private float _stepSize;

        void Awake()
        {
            if (centerPoint == null) centerPoint = transform;
            FindTransmitters();
        }

        void Start()
        {
            FindTransmitters();
            if (centerPoint == null) centerPoint = transform;

            if (!enabledByUI) return;

            if (!HasTransmitters()) return;

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
                DestroyHeatmapIfExists();
                return;
            }

            UpdateHeatmap();
        }

        void Update()
        {
            if (!enabledByUI)
            {
                if (heatmapObject != null) DestroyHeatmapIfExists();
                return;
            }
        }

        public void UpdateHeatmap()
        {
            DestroyHeatmapIfExists();

            FindTransmitters();

            if (!HasTransmitters())
            {
                DestroyHeatmapIfExists();
                return;
            }

            EnsureHeatmapCreated();

            if (keepRootOnTerrain)
            {
                float h = GetTerrainHeightAtPosition(centerPoint.position, out bool ok);
                if (ok) transform.position = new Vector3(centerPoint.position.x, h, centerPoint.position.z);
            }

            GenerateHeatmap();
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
                heatmapObject.layer = settings.heatmapLayer;
                heatmapFilter = heatmapObject.AddComponent<MeshFilter>();
                heatmapRenderer = heatmapObject.AddComponent<MeshRenderer>();
            }

            if (heatmapTexture == null || heatmapTexture.width != settings.resolution)
            {
                heatmapTexture = new Texture2D(settings.resolution, settings.resolution, TextureFormat.RGBA32, false);
                heatmapTexture.filterMode = FilterMode.Trilinear;
                heatmapTexture.wrapMode = TextureWrapMode.Clamp;
                heatmapTexture.anisoLevel = 9;
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
                    if (!ok || float.IsNaN(height))
                    {
                        height = 0f;
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
            if (GeometryHelper.TryGetGroundY(worldPos, settings.terrainLayer, out var gy))
            {
                hitOK = true;
                return gy;
            }

            const float step = 0.5f;
            Vector3[] offsets = {
                new Vector3( step, 0,  0),
                new Vector3(-step, 0,  0),
                new Vector3( 0,    0,  step),
                new Vector3( 0,    0, -step),
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                var test = worldPos + offsets[i];
                if (GeometryHelper.TryGetGroundY(test, settings.terrainLayer, out gy))
                {
                    hitOK = true;
                    return gy;
                }
            }

            hitOK = false;
            return float.NaN;
        }

        public void GenerateHeatmap()
        {
            StartCoroutine(GenerateHeatmapAsync());
        }

        public IEnumerator GenerateHeatmapAsync()
        {
            if (transmitters.Count == 0)
            {
                yield break;
            }    

            int N = settings.resolution * settings.resolution;
            float[] rssiBuf = new float[N];
            Color[] pixels = new Color[settings.resolution * settings.resolution];

            float dataMin = float.PositiveInfinity;
            float dataMax = float.NegativeInfinity;

            foreach (var t in transmitters)
            {
                t.ClearPathLossCache();
            }

            if (heatmapUI != null)
            {
                heatmapUI.ToggleHeatmapPanel(true);
                heatmapUI.ToggleLoadingPanel(true);
            }

            int samplesPerFrame = 100;
            int sampleCount = 0;

            for (int y = 0; y < settings.resolution; y++)
            {
                for (int x = 0; x < settings.resolution; x++)
                {
                    int idx = y * settings.resolution + x;

                    float localX = -_halfSize + (x * _stepSize);
                    float localZ = -_halfSize + (y * _stepSize);

                    Vector3 samplePos = transform.TransformPoint(new Vector3(localX, 0f, localZ));
                    float terrainH = GetTerrainHeightAtPosition(samplePos, out bool ok);

                    samplePos.y = terrainH + settings.sampleHeight;

                    if (IsPositionBlockedByBuildings(samplePos))
                    {
                        pixels[y * settings.resolution + x] = new Color(0f, 0f, 0f, 0f);
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

                    rssiBuf[idx] = maxRssi;

                    if (!float.IsNegativeInfinity(maxRssi))
                    {
                        if (maxRssi < dataMin) dataMin = maxRssi;
                        if (maxRssi > dataMax) dataMax = maxRssi;
                    }

                    sampleCount++;
                    if (sampleCount % samplesPerFrame == 0)
                    {
                        yield return null;
                    }
                }
            }

            float lo = settings.minSignalStrength;
            float hi = settings.maxSignalStrength;

            if (settings.autoScaleColors && dataMin < dataMax)
            {
                lo = Mathf.Clamp(dataMin, settings.clampMin, settings.clampMax - 1f);
                hi = Mathf.Clamp(dataMax, lo + 1f, settings.clampMax);
            }

            for (int i = 0; i < N; i++)
                pixels[i] = SignalStrengthToColor(rssiBuf[i], lo, hi);


            heatmapTexture.SetPixels(pixels);
            heatmapTexture.Apply(false, false);

            if (heatmapUI != null)
            {
                heatmapUI.ToggleLoadingPanel(false);
            }
        }

        private readonly Collider[] _overlapCache = new Collider[16];

        private bool IsPositionBlockedByBuildings(Vector3 position)
        {
            if (!BuildingManager.AreBuildingsEnabled()) return false;

            LayerMask mask = BuildingManager.GetActiveBuildingLayers();
            if (mask == 0) return false;

            const float r = 0.15f;
            int n = Physics.OverlapSphereNonAlloc(position, r, _overlapCache, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var b = _overlapCache[i].GetComponentInParent<RFSimulation.Environment.Building>();
                if (b != null && b.blockSignals) return true;
            }
            return false;
        }

        private Color SignalStrengthToColor(float rssi, float lo, float hi)
        {
            if (float.IsNegativeInfinity(rssi) || float.IsNaN(rssi))
                return settings.noSignalColor;

            if (rssi >= settings.excellentCutoffDbm) return settings.excellentSignalColor;
            if (rssi >= settings.goodCutoffDbm) return settings.highSignalColor;
            if (rssi >= settings.poorCutoffDbm) return settings.mediumSignalColor;
            if (rssi >= settings.veryPoorCutoffDbm) return settings.lowSignalColor;
            return settings.noSignalColor;
        }

        private bool HasTransmitters()
        {
            transmitters.RemoveAll(t => t == null);
            return transmitters.Count > 0;
        }

        private void EnsureHeatmapCreated()
        {
            if (!enabledByUI) return;
            if (heatmapObject != null) return;

            if (groundGrid != null)
            {
                Vector3 aligned = groundGrid.SnapToGrid(centerPoint.position);
                transform.position = aligned;
            }
            else
            {
                transform.position = centerPoint.position;
            }

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
            if (heatmapUI != null)
                heatmapUI.ToggleHeatmapPanel(false);
        }

        public void SetCenter(Transform newCenter)
        {
            centerPoint = newCenter != null ? newCenter : transform;
            _terrainHeightCache.Clear();
            CreateHeatmapMesh();
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
