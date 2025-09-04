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
    /// Signal strength heatmap visualization for RF simulation
    /// Generates a 2D texture showing signal coverage across an area
    /// </summary>
    public class SignalHeatmap : MonoBehaviour
    {
        [Header("Material")]
        public Material heatmapMaterial;

        [Header("Heatmap Configuration")]
        public Vector2 heatmapSize = new Vector2(1000f, 1000f);
        [Range(64, 512)]
        public int resolution = 128;
        public float samplingHeight = 1.5f;

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

        [Header("Colors")]
        public Color noSignalColor = new Color(0.2f, 0.2f, 0.2f, 0.1f);
        public Color weakSignalColor = Color.red;
        public Color fairSignalColor = Color.yellow;
        public Color goodSignalColor = Color.green;
        public Color excellentSignalColor = Color.cyan;


        // Private components
        private Texture2D heatmapTexture;
        private GameObject heatmapPlane;
        private Renderer heatmapRenderer;
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
            heatmapPlane.name = "HeatmapPlane";
            heatmapPlane.transform.SetParent(transform);

            // Position slightly above ground to avoid z-fighting
            heatmapPlane.transform.position = Vector3.zero + new Vector3(0, 0.01f, 0);
            heatmapPlane.transform.rotation = Quaternion.identity;

            // Scale to match heatmap size (Unity plane is 10x10 units)
            Vector3 scale = new Vector3(heatmapSize.x / 10f, 1f, heatmapSize.y / 10f);
            heatmapPlane.transform.localScale = scale;

            // Remove collider
            DestroyImmediate(heatmapPlane.GetComponent<Collider>());

            heatmapRenderer = heatmapPlane.GetComponent<Renderer>();
            heatmapPlane.SetActive(showHeatmap);
        }

        private void CreateHeatmapMaterial()
        {
            if (heatmapMaterial == null)
            {
                heatmapMaterial = new Material(Shader.Find("Unlit/Transparent"));
            }
            else
            {
                heatmapMaterial.shader = Shader.Find("Unlit/Transparent");
            }

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
                isCalculating = false;
                yield break;
            }

            var transmitterList = SimulationManager.Instance.transmitters;
            Color[] pixels = new Color[resolution * resolution];

            Vector3 center = transform.position;
            Vector3 bottomLeft = center - new Vector3(heatmapSize.x * 0.5f, 0, heatmapSize.y * 0.5f);

            float stepX = heatmapSize.x / (resolution - 1);
            float stepZ = heatmapSize.y / (resolution - 1);

            int processedPixels = 0;

            // PERFORMANCE: Process in smaller batches and yield more frequently
            int batchSize = Mathf.Max(1, resolution / 10); // Process 1/10th of a row at a time

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

                    // PERFORMANCE: Yield more frequently to prevent lag
                    if (processedPixels % batchSize == 0)
                    {
                        yield return null; // Yield every batch
                    }
                }
            }

            heatmapTexture.SetPixels(pixels);
            heatmapTexture.Apply();

            isCalculating = false;
        }

        private float CalculateBestSignalAtPosition(Vector3 position, List<Transmitter> transmitters)
        {
            float bestSignal = float.NegativeInfinity;

            foreach (Transmitter tx in transmitters)
            {
                if (tx == null) continue;

                try
                {
                    float signal = tx.CalculateSignalStrength(position);

                    // Check for infinity/NaN before comparison
                    if (float.IsInfinity(signal) || float.IsNaN(signal))
                    {
                        continue; // Skip this transmitter
                    }

                    if (signal > bestSignal)
                    {
                        bestSignal = signal;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error calculating signal from {tx.uniqueID}: {e.Message}");
                }
            }

            // Additional safety check
            if (float.IsInfinity(bestSignal) || float.IsNaN(bestSignal))
            {
                return -120f; // Return a safe default value
            }

            return bestSignal;
        }

        private Color SignalToColor(float signalDbm)
        {

            // Check for infinity/NaN in color conversion
            if (float.IsInfinity(signalDbm) || float.IsNaN(signalDbm))
            {
                return noSignalColor;
            }

            // Handle no signal case
            if (float.IsNegativeInfinity(signalDbm) || signalDbm < minSignalDbm)
            {
                return noSignalColor;
            }

            // Normalize signal to 0-1 range
            float normalized = Mathf.Clamp01((signalDbm - minSignalDbm) / (maxSignalDbm - minSignalDbm));

            // Check if normalization created infinity
            if (float.IsInfinity(normalized) || float.IsNaN(normalized))
            {
                return noSignalColor;
            }

            // Create color based on signal strength
            Color resultColor;

            if (signalDbm < sensitivityThreshold)
            {
                // Below sensitivity - red to orange gradient
                float localNorm = Mathf.Clamp01((signalDbm - minSignalDbm) / (sensitivityThreshold - minSignalDbm));
                resultColor = Color.Lerp(noSignalColor, weakSignalColor, localNorm);
            }
            else if (normalized < 0.4f)
            {
                // Weak signal - red to orange
                resultColor = Color.Lerp(weakSignalColor, new Color(1f, 0.5f, 0f), (normalized - 0.2f) / 0.2f);
            }
            else if (normalized < 0.6f)
            {
                // Fair signal - orange to yellow
                resultColor = Color.Lerp(new Color(1f, 0.5f, 0f), fairSignalColor, (normalized - 0.4f) / 0.2f);
            }
            else if (normalized < 0.8f)
            {
                // Good signal - yellow to green
                resultColor = Color.Lerp(fairSignalColor, goodSignalColor, (normalized - 0.6f) / 0.2f);
            }
            else
            {
                // Excellent signal - green to cyan
                resultColor = Color.Lerp(goodSignalColor, excellentSignalColor, (normalized - 0.8f) / 0.2f);
            }

            // Apply transparency
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

        public void SetTransparency(float alpha)
        {
            transparency = Mathf.Clamp01(alpha);

            if (heatmapMaterial != null)
            {
                Color color = heatmapMaterial.color;
                color.a = transparency;
                heatmapMaterial.color = color;
            }
        }

        // Context menu debug methods
        [ContextMenu("Force Update Heatmap")]
        public void ForceUpdate()
        {
            UpdateHeatmap();
        }

        [ContextMenu("Toggle Visibility")]
        public void ToggleVisibility()
        {
            SetVisibility(!showHeatmap);
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