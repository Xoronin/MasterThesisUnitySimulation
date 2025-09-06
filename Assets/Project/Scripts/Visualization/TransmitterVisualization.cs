using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using RFSimulation.Core;

namespace RFSimulation.Visualization
{
    /// <summary>
    /// Enhanced 3D visualization for transmitters with antenna patterns, signal strength indicators, and labels
    /// </summary>
    public class TransmitterVisualizer : MonoBehaviour
    {
        [Header("Transmitter Appearance")]
        public GameObject antennaModel;
        public GameObject towerBase;
        public Material transmitterMaterial;
        public Color transmitterColor = Color.blue;
        public float antennaHeight = 10f;
        public float baseRadius = 2f;

        [Header("Signal Visualization")]
        public bool showSignalRings = true;
        public bool showAntennaPattern = true;
        public bool showCoverageArea = false;
        public int signalRingCount = 5;
        public float maxRingRadius = 100f;
        public Color signalRingColor = new Color(0.2f, 0.8f, 1f, 0.3f);

        [Header("Power Level Indicator")]
        public bool showPowerIndicator = true;
        public GameObject powerBarPrefab;
        public float powerBarHeight = 5f;
        public Color lowPowerColor = Color.red;
        public Color highPowerColor = Color.green;

        [Header("World Space UI")]
        public bool showWorldSpaceLabel = true;
        public Canvas worldSpaceCanvas;
        public Text labelText;
        public Image statusIcon;
        public float labelHeight = 15f;
        public float labelScale = 0.01f;

        [Header("Animation")]
        public float animationSpeed = 2f;
        public bool pulseOnTransmission = true;

        private Transmitter transmitter;
        private LineRenderer antennaPatternRenderer;
        private float animationTime = 0f;

        public void Initialize(Transmitter parentTransmitter)
        {
            transmitter = parentTransmitter;
            CreateTransmitterModel();
            CreateAntennaPattern();
        }

        private void CreateTransmitterModel()
        {
            // Create main transmitter structure
            if (antennaModel == null)
                CreateDefaultAntennaModel();

            if (towerBase == null)
                CreateDefaultTowerBase();

            // Apply transmitter material and color
            ApplyMaterialToChildren();
        }

        private void CreateDefaultAntennaModel()
        {
            // Create antenna pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "AntennaPole";
            pole.transform.SetParent(transform);
            pole.transform.localPosition = Vector3.up * (antennaHeight / 2f);
            pole.transform.localScale = new Vector3(0.2f, antennaHeight / 2f, 0.2f);

            // Create antenna elements
            for (int i = 0; i < 3; i++)
            {
                GameObject element = GameObject.CreatePrimitive(PrimitiveType.Cube);
                element.name = $"AntennaElement_{i}";
                element.transform.SetParent(pole.transform);
                element.transform.localPosition = Vector3.up * (0.6f + i * 0.3f);
                element.transform.localScale = new Vector3(2f, 0.1f, 0.1f);
            }

            antennaModel = pole;
        }

        private void CreateDefaultTowerBase()
        {
            towerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerBase.name = "TowerBase";
            towerBase.transform.SetParent(transform);
            towerBase.transform.localPosition = Vector3.up * 0.5f;
            towerBase.transform.localScale = new Vector3(baseRadius, 0.5f, baseRadius);
        }

        private void CreateAntennaPattern()
        {
            if (!showAntennaPattern) return;

            GameObject patternObj = new GameObject("AntennaPattern");
            patternObj.transform.SetParent(transform);
            patternObj.transform.localPosition = Vector3.up * antennaHeight;

            antennaPatternRenderer = patternObj.AddComponent<LineRenderer>();
            antennaPatternRenderer.material = CreatePatternMaterial();
            antennaPatternRenderer.startWidth = 0.3f;
            antennaPatternRenderer.endWidth = 0.3f;
            antennaPatternRenderer.useWorldSpace = false;

            // Create directional antenna pattern
            CreateDirectionalPattern();
        }

        private void CreateDirectionalPattern()
        {
            int segments = 32;
            antennaPatternRenderer.positionCount = segments + 1;

            Vector3[] points = new Vector3[segments + 1];
            float maxGain = transmitter ? transmitter.antennaGain : 10f;
            float patternScale = 20f;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;

                // Simple directional pattern (stronger forward, weaker backward)
                float gain = Mathf.Cos(angle * 0.5f);
                gain = Mathf.Max(gain, 0.2f); // Minimum gain

                float radius = gain * patternScale;
                points[i] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );
            }

            antennaPatternRenderer.SetPositions(points);
        }

        private Color GetStatusColor()
        {
            if (transmitter == null) return Color.gray;

            int connections = transmitter.GetConnectionCount();
            if (connections == 0) return Color.red;
            if (connections < 3) return Color.yellow;
            return Color.green;
        }

        private IEnumerator FaceCameraCoroutine()
        {
            while (worldSpaceCanvas != null)
            {
                if (Camera.main != null)
                {
                    worldSpaceCanvas.transform.LookAt(Camera.main.transform);
                    worldSpaceCanvas.transform.Rotate(0, 180, 0); // Face camera properly
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private Material CreatePatternMaterial()
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha blend
            mat.SetFloat("_ZWrite", 0);
            mat.SetInt("_Cull", 2);
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", new Color(1f, 1f, 0f, 0.7f)); // Yellow, semi-transparent
            return mat;
        }

        private void ApplyMaterialToChildren()
        {
            if (transmitterMaterial == null)
            {
                transmitterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                transmitterMaterial.SetColor("_BaseColor", transmitterColor);
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.name.Contains("AntennaPattern"))
                    continue;

                renderer.material = transmitterMaterial;
            }
        }

        public void SetVisibilityOptions(bool pattern, bool coverage)
        {
            showAntennaPattern = pattern;
            showCoverageArea = coverage;

            if (antennaPatternRenderer != null)
                antennaPatternRenderer.gameObject.SetActive(pattern);
        }
    }
}