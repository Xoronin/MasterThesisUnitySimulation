using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Core;
using RFSimulation.Core.Managers;
using RFSimulation.Core.Components;
using RFSimulation.Environment;
using RFSimulation.Utils;

namespace RFSimulation.Visualization
{
    [System.Serializable]
    public class HeatmapSettings
    {
        [Header("Heatmap Area & Sampling")]
        [Min(8)] public int resolution = 32;
        [Tooltip("Half-size in meters from center. The heatmap spans 2x this radius.")]
        public float sampleRadius = 3000f;
        [Tooltip("Vertical offset above terrain to avoid z-fighting.")]
        public float heightOffset = 1.5f;
        [Tooltip("Approx receiver height for sampling.")]
        public float sampleHeight = 1.5f;

        [Header("Layers")]
        [Tooltip("Layer(s) that Mapbox terrain tiles are on. Set this to your Terrain layer or Default if Mapbox tiles are Default.")]
        public LayerMask terrainLayer = 6;
        [Tooltip("Layer(s) that buildings are on.")]
        public LayerMask buildingLayer = 8; 
        [Tooltip("Layer for the heatmap object so our raycasts never hit ourselves.")]
        public int heatmapLayer = 2; // 2 = IgnoreRaycast by Unity default

        [Header("Visual Settings")]
        public Material heatmapMaterial;
        public float minSignalStrength = -120f; // SignalStrengthToColor
        public float maxSignalStrength = -10f;  // dBm

        [Header("Colors")]
        public Color noSignalColor = Color.red; // red
        public Color lowSignalColor = Color.orange; // orange
        public Color mediumSignalColor = Color.yellow; // yellow
        public Color highSignalColor = Color.yellowGreen; // yellow-green
        public Color excellentSignalColor = Color.green; // green

        [Header("Color Mapping")]
        public bool autoScaleColors = false;
        public float clampMin = -120f;  // never map below this
        public float clampMax = 10f;  // never map above this

        [Header("Absolute Band Cutoffs (dBm)")]
        public bool useAbsoluteBands = true;
        // rssi >= -70  → excellentSignalColor
        public float excellentCutoffDbm = -70f;
        // rssi >= -85  → highSignalColor (good/usable)
        public float goodCutoffDbm = -85f;
        // rssi >= -100 → mediumSignalColor (poor)
        public float poorCutoffDbm = -100f;
        // rssi >= -110 → lowSignalColor (very poor); below → noSignalColor
        public float veryPoorCutoffDbm = -110f;

        [Header("Robustness")]
        [Tooltip("Meters to cast from above and below when probing the terrain.")]
        public float probeHeight = 1000f;
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
        [SerializeField] private bool keepRootOnTerrain = true;
        [SerializeField] public bool enabledByUI = false;

        private GameObject heatmapObject;
        private MeshRenderer heatmapRenderer;
        private MeshFilter heatmapFilter;
        private Texture2D heatmapTexture;
        private float lastUpdateTime;

        // Cache for terrain heights to avoid repeated raycasts
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
            // Use your helper: single robust ray from far above
            if (GeometryHelper.TryGetGroundY(worldPos, settings.terrainLayer, out var gy)) // uses LayerMask directly
            {
                hitOK = true;
                return gy;
            }

            // Small deterministic neighborhood retry (helps tiny seams)
            const float step = 0.5f; // meters
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

            // Fallbacks so we never create a "hole"
            hitOK = false;
            return transform.position.y;
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
            float eps = Mathf.Max(0.1f, _stepSize * 0.1f);
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
                height = hits[hits.Count / 2]; 
                return true;
            }

            height = 0f;
            return false;
        }

        private bool ProbeTerrain(Vector3 worldPos, out float height)
        {
            height = 0f;
            float up = Mathf.Abs(settings.probeHeight);
            float radius = Mathf.Max(0.2f, _stepSize * 0.25f); 

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

            int N = settings.resolution * settings.resolution;
            float[] rssiBuf = new float[N];
            Color[] pixels = new Color[settings.resolution * settings.resolution];

            float dataMin = float.PositiveInfinity;
            float dataMax = float.NegativeInfinity;

            foreach (var t in transmitters) { 
                t.ClearPathLossCache();
            }

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

                    //if (IsPositionBlockedByBuildings(samplePos))
                    //{
                    //    pixels[y * settings.resolution + x] = new Color(0f, 0f, 0f, 0.35f); // transparent over buildings
                    //    continue;
                    //}

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
                }
            }

            // choose mapping range
            float lo = settings.minSignalStrength;
            float hi = settings.maxSignalStrength;

            if (settings.autoScaleColors && dataMin < dataMax)
            {
                lo = Mathf.Clamp(dataMin, settings.clampMin, settings.clampMax - 1f);
                hi = Mathf.Clamp(dataMax, lo + 1f, settings.clampMax);
            }

            // second pass: colorize
            for (int i = 0; i < N; i++)
                pixels[i] = SignalStrengthToColor(rssiBuf[i], lo, hi);
                //pixels[i] = MapRssiToColorDebug(rssiBuf[i]).col;


            heatmapTexture.SetPixels(pixels);
            heatmapTexture.Apply(false, false);
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

            if (settings.useAbsoluteBands)
            {
                // Absolute dBm bands (no relative autoscale)
                if (rssi >= settings.excellentCutoffDbm) return settings.excellentSignalColor; // -50..-70 (and better)
                if (rssi >= settings.goodCutoffDbm) return settings.highSignalColor;      // -70..-85
                if (rssi >= settings.poorCutoffDbm) return settings.mediumSignalColor;    // -85..-100
                if (rssi >= settings.veryPoorCutoffDbm) return settings.lowSignalColor;       // -100..-110
                return settings.noSignalColor;                                                 // <= -110
            }

            // Fallback: relative gradient (only if you re-enable autoScaleColors)
            float t = Mathf.InverseLerp(lo, hi, rssi);
            t = Mathf.Clamp01(t);
            return EvaluateFiveStop(
                t,
                settings.noSignalColor,
                settings.lowSignalColor,
                settings.mediumSignalColor,
                settings.highSignalColor,
                settings.excellentSignalColor
            );
        }

        private static Color EvaluateFiveStop(float t, Color c0, Color c1, Color c2, Color c3, Color c4)
        {
            // Which two stops are we between?
            const int n = 4;                 // 5 stops → 4 segments
            float scaled = t * n;            // 0..4
            int i = Mathf.FloorToInt(scaled);
            if (i >= n) return c4;           // t == 1
            float u = scaled - i;            // 0..1 within the segment

            switch (i)
            {
                case 0: return Color.Lerp(c0, c1, u);
                case 1: return Color.Lerp(c1, c2, u);
                case 2: return Color.Lerp(c2, c3, u);
                default: return Color.Lerp(c3, c4, u);
            }
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



        // DEBUG

        // Returns (color, bandLabel, reason)
        private (Color col, string band, string reason) MapRssiToColorDebug(float rssi)
        {
            // 1) sanity
            if (float.IsNaN(rssi) || float.IsInfinity(rssi))
                return (settings.noSignalColor, "No signal", "NaN/Inf");

            // 2) absolute bands (recommended: useAbsoluteBands = true)
            if (settings.autoScaleColors == false) // absolute range intended
            {
                // Your hard bands:
                // ≥ -70 => Excellent/Good
                // ≥ -85 => Good/Usable
                // ≥ -100 => Poor
                // ≥ -110 => Very Poor
                // < -110 => No signal
                if (rssi >= -70f) return (settings.excellentSignalColor, "Excellent/Good (≥ -70)", "ABS:excellent");
                if (rssi >= -85f) return (settings.highSignalColor, "Good/Usable (-70..-85)", "ABS:good");
                if (rssi >= -100f) return (settings.mediumSignalColor, "Poor (-85..-100)", "ABS:poor");
                if (rssi >= -110f) return (settings.lowSignalColor, "Very Poor (-100..-110)", "ABS:very-poor");
                return (settings.noSignalColor, "Unusable (≤ -110)", "ABS:no-signal");
            }

            // 3) relative gradient fallback (autoScaleColors = true)
            float lo = settings.minSignalStrength;
            float hi = settings.maxSignalStrength;
            // If you're autoscaling on, you might be adjusting lo/hi elsewhere — clamp to configured bounds
            lo = Mathf.Clamp(lo, settings.clampMin, settings.clampMax - 1f);
            hi = Mathf.Clamp(hi, lo + 1f, settings.clampMax);

            float t = Mathf.InverseLerp(lo, hi, rssi);
            t = Mathf.Clamp01(t);
            var col = EvaluateFiveStop(t,
                settings.noSignalColor,
                settings.lowSignalColor,
                settings.mediumSignalColor,
                settings.highSignalColor,
                settings.excellentSignalColor
            );
            return (col, $"Relative t={t:F2} (range {lo:F0}..{hi:F0} dBm)", "REL:gradient");
        }


        // --- DEBUG ---
        [SerializeField] private bool debugOverlay = true;          // toggle HUD
        [SerializeField] private bool debugCursorProbe = true;       // sample under mouse
        [SerializeField] private int debugFontSize = 14;
        [SerializeField] private Vector2 debugHudAnchor = new Vector2(12, 12);
        private Vector3 _lastProbeWorld;
        private float _lastProbeRssi = float.NegativeInfinity;
        private Color _lastProbeColor = Color.gray;
        private string _lastProbeBand = "";

        private float SampleRssiAtWorld(Vector3 worldPos)
        {
            if (transmitters == null || transmitters.Count == 0) return float.NegativeInfinity;

            // place probe at terrain + sampleHeight (same as heatmap)
            float h = GetTerrainHeightAtPosition(worldPos, out bool ok);
            if (ok) worldPos.y = h + settings.sampleHeight;

            float best = float.NegativeInfinity;
            for (int i = 0; i < transmitters.Count; i++)
            {
                var t = transmitters[i];
                if (t == null) continue;
                float rssi = t.CalculateSignalStrength(worldPos);
                if (rssi > best) best = rssi;
            }
            return best;
        }
        private string BandNameForRssi(float rssi)
        {
            if (float.IsNegativeInfinity(rssi) || float.IsNaN(rssi)) return "No signal";
            // Match your absolute bands:
            // >= -70 → Excellent, >= -85 → Good, >= -100 → Poor, >= -110 → Very Poor, else No signal
            if (rssi >= -70f) return "Excellent/Good (≥ -70)";
            if (rssi >= -85f) return "Good/Usable (-70..-85)";
            if (rssi >= -100f) return "Poor (-85..-100)";
            if (rssi >= -110f) return "Very Poor (-100..-110)";
            return "Unusable (≤ -110)";
        }
        void LateUpdate()
        {
            if (!debugOverlay || !debugCursorProbe) return;
            if (Camera.main == null) return;

            // Raycast from camera to world (onto XZ plane at terrain height near center)
            Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);

            // Simple plane at the heatmap root’s Y
            Plane ground = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            if (ground.Raycast(r, out float t))
            {
                Vector3 hit = r.GetPoint(t);
                _lastProbeWorld = hit;

                float rssi = SampleRssiAtWorld(hit);
                _lastProbeRssi = rssi;

                // Use the exact same mapping as GenerateHeatmap()
                float lo = settings.minSignalStrength;
                float hi = settings.maxSignalStrength;
                if (settings.autoScaleColors)
                {
                    // When autoscale is on, at least use the configured clamps for HUD context.
                    lo = settings.clampMin;
                    hi = settings.clampMax;
                }
                var map = MapRssiToColorDebug(rssi);
                _lastProbeColor = map.col;
                _lastProbeBand = map.band + $" [{map.reason}]";
            }
        }
        void OnGUI()
        {
            if (!debugOverlay) return;

            var prev = GUI.color;
            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = debugFontSize };
            var lab = new GUIStyle(GUI.skin.label) { fontSize = debugFontSize };

            Rect box = new Rect(debugHudAnchor.x, debugHudAnchor.y, 420, 140);
            GUI.Box(box, "Heatmap Debug", style);

            float y = box.y + 24;
            GUI.Label(new Rect(box.x + 8, y, box.width - 16, 22),
                $"Probe world: {_lastProbeWorld.x:F1}, {_lastProbeWorld.y:F1}, {_lastProbeWorld.z:F1}", lab);
            y += 20;

            string rssiTxt = float.IsNegativeInfinity(_lastProbeRssi) ? "−∞" : $"{_lastProbeRssi:F1} dBm";
            GUI.Label(new Rect(box.x + 8, y, box.width - 16, 22),
                $"RSRP: {rssiTxt}    Band: {_lastProbeBand}", lab);
            y += 20;

            GUI.color = _lastProbeColor;
            GUI.Box(new Rect(box.x + 8, y, 40, 18), GUIContent.none);
            GUI.color = prev;

            var c = _lastProbeColor;
            GUI.Label(new Rect(box.x + 56, y - 2, box.width - 64, 24),
                $"Color: RGBA({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})", lab);
            y += 22;

            GUI.Label(new Rect(box.x + 8, y, box.width - 16, 22),
                $"Mapping range: [{settings.minSignalStrength:F0}, {settings.maxSignalStrength:F0}] dBm   (autoScale={(settings.autoScaleColors ? "ON" : "OFF")})",
                lab);
        }
        [ContextMenu("Debug: Dump cursor sample")]
        public void DebugDumpCursorSample()
        {
            Debug.Log($"[HeatmapDebug] Pos={_lastProbeWorld}  RSRP={_lastProbeRssi:F1} dBm  Band={_lastProbeBand}  Color={_lastProbeColor}");
        }

    }
}
