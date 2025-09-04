using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RFSimulation.Core;
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss;
using RFSimulation.Propagation.SignalQuality;

namespace RFSimulation.Visualization
{
    /// <summary>
    /// Safe heatmap that works without Mapbox coordinate dependencies
    /// Falls back to simple world positioning if Mapbox fails
    /// </summary>
    public class SafeSignalHeatmap : MonoBehaviour
    {
        [Header("Heatmap Configuration")]
        public Vector2 heatmapSize = new Vector2(1000f, 1000f);
        [Range(64, 512)]
        public int resolution = 128;
        public float samplingHeight = 1.5f;
        public float heightAboveGround = 0.5f;

        [Header("Visual Settings")]
        public bool showHeatmap = true;
        [Range(0.1f, 1f)]
        public float transparency = 0.7f;

        [Header("Signal Range")]
        public float minSignalDbm = -120f;
        public float maxSignalDbm = -30f;
        public float sensitivityThreshold = -105f;

        [Header("Performance")]
        public float updateInterval = 2f;
        public bool autoUpdate = true;
        public int pixelsPerFrame = 64;

        [Header("Colors")]
        public Color noSignalColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        public Color weakSignalColor = Color.red;
        public Color fairSignalColor = Color.yellow;
        public Color goodSignalColor = Color.green;
        public Color excellentSignalColor = Color.cyan;

        [Header("Positioning")]
        public bool useWorldPositioning = true; // Fallback mode
        public Vector3 worldCenter = Vector3.zero;

        // Private components
        private Texture2D heatmapTexture;
        private GameObject heatmapPlane;
        private Renderer heatmapRenderer;
        private Material heatmapMaterial;
        private float[] signalData;

        // Performance tracking
        private Coroutine updateCoroutine;
        private bool isCalculating = false;
        private float lastUpdateTime;

        void Start()
        {
            InitializeHeatmap();
        }

        void Update()
        {
            if (autoUpdate && showHeatmap && Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateHeatmap();
            }
        }

        private void InitializeHeatmap()
        {
            // Create texture
            heatmapTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            heatmapTexture.filterMode = FilterMode.Bilinear;
            heatmapTexture.wrapMode = TextureWrapMode.Clamp;

            // Initialize data array
            signalData = new float[resolution * resolution];

            // Create visual plane
            CreateHeatmapPlane();

            // Setup material
            CreateHeatmapMaterial();

            Debug.Log("✅ Safe heatmap initialized");

            // Initial update
            if (showHeatmap)
            {
                UpdateHeatmap();
            }
        }

        private void CreateHeatmapPlane()
        {
            // Create plane GameObject
            heatmapPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            heatmapPlane.name = "SafeHeatmapPlane";
            heatmapPlane.transform.SetParent(transform);

            // SAFE POSITIONING - Use world coordinates, avoid Mapbox
            Vector3 centerPosition;

            if (useWorldPositioning || worldCenter != Vector3.zero)
            {
                // Use specified world center
                centerPosition = worldCenter;
            }
            else
            {
                // Auto-detect center from transmitters
                centerPosition = CalculateTransmitterCenter();
            }

            // Validate position before setting
            if (IsValidPosition(centerPosition))
            {
                heatmapPlane.transform.position = centerPosition + Vector3.up * heightAboveGround;
                Debug.Log($"✅ Heatmap positioned at: {centerPosition}");
            }
            else
            {
                // Ultimate fallback
                heatmapPlane.transform.position = new Vector3(0, heightAboveGround, 0);
                Debug.LogWarning("⚠️ Used fallback position (0,0,0)");
            }

            heatmapPlane.transform.rotation = Quaternion.identity;

            // Scale to match coverage area (Unity plane is 10x10 units)
            float scaleX = heatmapSize.x / 10f;
            float scaleZ = heatmapSize.y / 10f;
            heatmapPlane.transform.localScale = new Vector3(scaleX, 1f, scaleZ);

            // Remove collider
            DestroyImmediate(heatmapPlane.GetComponent<Collider>());

            heatmapRenderer = heatmapPlane.GetComponent<Renderer>();
            heatmapPlane.SetActive(showHeatmap);

            Debug.Log($"Heatmap plane scale: {heatmapPlane.transform.localScale}");
        }

        private Vector3 CalculateTransmitterCenter()
        {
            if (SimulationManager.Instance == null || SimulationManager.Instance.transmitters.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 center = Vector3.zero;
            int count = 0;

            foreach (var tx in SimulationManager.Instance.transmitters)
            {
                if (tx != null)
                {
                    center += tx.transform.position;
                    count++;
                }
            }

            if (count > 0)
            {
                center /= count;
                center.y = 0f; // Ground level
                Debug.Log($"Calculated transmitter center: {center}");
                return center;
            }

            return Vector3.zero;
        }

        private bool IsValidPosition(Vector3 position)
        {
            return !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z) &&
                   !float.IsInfinity(position.x) && !float.IsInfinity(position.y) && !float.IsInfinity(position.z);
        }

        private void CreateHeatmapMaterial()
        {
            // Create material with high visibility
            heatmapMaterial = new Material(Shader.Find("Unlit/Transparent"));
            heatmapMaterial.SetFloat("_Mode", 3f); // Transparent mode
            heatmapMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            heatmapMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            heatmapMaterial.SetInt("_ZWrite", 0);
            heatmapMaterial.DisableKeyword("_ALPHATEST_ON");
            heatmapMaterial.EnableKeyword("_ALPHABLEND_ON");
            heatmapMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            heatmapMaterial.renderQueue = 3000;

            heatmapMaterial.mainTexture = heatmapTexture;
            heatmapMaterial.color = new Color(1f, 1f, 1f, transparency);

            // Apply to renderer
            if (heatmapRenderer != null)
            {
                heatmapRenderer.material = heatmapMaterial;
                heatmapRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                heatmapRenderer.receiveShadows = false;
            }
        }

        public void UpdateHeatmap()
        {
            if (isCalculating) return;

            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
            }

            updateCoroutine = StartCoroutine(GenerateHeatmapCoroutine());
        }

        private IEnumerator GenerateHeatmapCoroutine()
        {
            isCalculating = true;
            lastUpdateTime = Time.time;

            if (SimulationManager.Instance == null || SimulationManager.Instance.transmitters.Count == 0)
            {
                CreateTestPattern(); // Show test pattern if no transmitters
                isCalculating = false;
                yield break;
            }

            var transmitterList = SimulationManager.Instance.transmitters;
            Color[] pixels = new Color[resolution * resolution];

            Vector3 center = heatmapPlane.transform.position;
            center.y = 0f; // Ground level for calculation
            Vector3 bottomLeft = center - new Vector3(heatmapSize.x * 0.5f, 0, heatmapSize.y * 0.5f);

            float stepX = heatmapSize.x / (resolution - 1);
            float stepZ = heatmapSize.y / (resolution - 1);

            int processedPixels = 0;

            Debug.Log($"Generating heatmap from {bottomLeft} with steps {stepX}x{stepZ}");

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector3 samplePosition = bottomLeft + new Vector3(
                        x * stepX,
                        samplingHeight,
                        y * stepZ
                    );

                    float bestSignal = CalculateBestSignalAtPosition(samplePosition, transmitterList);

                    int dataIndex = y * resolution + x;
                    signalData[dataIndex] = bestSignal;
                    pixels[dataIndex] = SignalToColor(bestSignal);

                    processedPixels++;

                    // Yield for smooth performance
                    if (processedPixels % pixelsPerFrame == 0)
                    {
                        yield return null;
                    }
                }
            }

            heatmapTexture.SetPixels(pixels);
            heatmapTexture.Apply();

            Debug.Log($"✅ Heatmap updated: {processedPixels} pixels processed");
            isCalculating = false;
        }

        private void CreateTestPattern()
        {
            Color[] pixels = new Color[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float normalizedX = (float)x / resolution;
                    float normalizedY = (float)y / resolution;

                    // Create rainbow test pattern
                    Color color = Color.HSVToRGB(normalizedX, 1f, 1f);
                    color.a = transparency;

                    pixels[y * resolution + x] = color;
                }
            }

            heatmapTexture.SetPixels(pixels);
            heatmapTexture.Apply();

            Debug.Log("🎨 Created test pattern - you should see rainbow colors");
        }

        private float CalculateBestSignalAtPosition(Vector3 worldPosition, List<Transmitter> transmitters)
        {
            float bestSignal = float.NegativeInfinity;

            foreach (Transmitter tx in transmitters)
            {
                if (tx == null) continue;

                try
                {
                    float signal = tx.CalculateSignalStrength(worldPosition);

                    if (!float.IsInfinity(signal) && !float.IsNaN(signal) && signal > bestSignal)
                    {
                        bestSignal = signal;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error calculating signal from {tx.uniqueID}: {e.Message}");
                }
            }

            return float.IsNegativeInfinity(bestSignal) ? -120f : bestSignal;
        }

        private Color SignalToColor(float signalDbm)
        {
            if (float.IsInfinity(signalDbm) || float.IsNaN(signalDbm) || signalDbm < minSignalDbm)
            {
                return noSignalColor;
            }

            // Normalize signal to 0-1 range
            float normalized = Mathf.Clamp01((signalDbm - minSignalDbm) / (maxSignalDbm - minSignalDbm));

            Color resultColor;

            if (signalDbm < sensitivityThreshold)
            {
                float localNorm = Mathf.Clamp01((signalDbm - minSignalDbm) / (sensitivityThreshold - minSignalDbm));
                resultColor = Color.Lerp(noSignalColor, weakSignalColor, localNorm);
            }
            else if (normalized < 0.4f)
            {
                resultColor = Color.Lerp(weakSignalColor, new Color(1f, 0.5f, 0f), (normalized - 0.2f) / 0.2f);
            }
            else if (normalized < 0.6f)
            {
                resultColor = Color.Lerp(new Color(1f, 0.5f, 0f), fairSignalColor, (normalized - 0.4f) / 0.2f);
            }
            else if (normalized < 0.8f)
            {
                resultColor = Color.Lerp(fairSignalColor, goodSignalColor, (normalized - 0.6f) / 0.2f);
            }
            else
            {
                resultColor = Color.Lerp(goodSignalColor, excellentSignalColor, (normalized - 0.8f) / 0.2f);
            }

            resultColor.a = transparency;
            return resultColor;
        }

        // Public interface methods
        public void SetVisibility(bool visible)
        {
            showHeatmap = visible;
            if (heatmapPlane != null)
            {
                heatmapPlane.SetActive(visible);
            }

            if (visible && !isCalculating)
            {
                UpdateHeatmap();
            }
        }

        [ContextMenu("Force Update")]
        public void ForceUpdate()
        {
            UpdateHeatmap();
        }

        [ContextMenu("Create Test Pattern")]
        public void ForceTestPattern()
        {
            CreateTestPattern();
        }

        [ContextMenu("Recenter on Transmitters")]
        public void RecenterOnTransmitters()
        {
            if (heatmapPlane != null)
            {
                Vector3 center = CalculateTransmitterCenter();
                if (IsValidPosition(center))
                {
                    heatmapPlane.transform.position = center + Vector3.up * heightAboveGround;
                    Debug.Log($"Recentered heatmap at: {center}");
                    UpdateHeatmap();
                }
            }
        }

        void OnDestroy()
        {
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
            }

            if (heatmapTexture != null)
            {
                DestroyImmediate(heatmapTexture);
            }

            if (heatmapMaterial != null)
            {
                DestroyImmediate(heatmapMaterial);
            }
        }
    }
}