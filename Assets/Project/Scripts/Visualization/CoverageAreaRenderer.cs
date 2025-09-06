using UnityEngine;
using RFSimulation.Core;

namespace RFSimulation.Visualization
{
    /// <summary>
    /// Simple coverage area renderer - just displays the radius it's told to display
    /// </summary>
    public class CoverageAreaRenderer : MonoBehaviour
    {
        [Header("Visual Settings")]
        public bool showCoverageArea = false;
        public Color coverageColor = new Color(0.2f, 0.8f, 1f, 0.15f);
        public float coverageHeight = 0.2f;
        public float worldScale = 1f;

        [Header("Limits")]
        public float maxRadiusMeters = 1000f;
        public float minRadiusMeters = 50f;

        // Private components
        private Transmitter transmitter;
        private GameObject coverageAreaObject;
        private Renderer coverageRenderer;
        private Material coverageMaterial;

        // What radius to display (set by TransmitterVisualizer)
        private float displayRadius = 100f;

        public void Initialize(Transmitter parentTransmitter)
        {
            transmitter = parentTransmitter;
            if (showCoverageArea)
            {
                CreateCoverageArea();
            }
        }

        public void Cleanup()
        {
            if (coverageAreaObject != null)
            {
                DestroyImmediate(coverageAreaObject);
                coverageAreaObject = null;
            }
        }

        public void SetCoverageRadius(float radiusMeters)
        {
            displayRadius = radiusMeters;
            UpdateVisualDisplay();
        }

        public void SetCoverageVisibility(bool visible)
        {
            showCoverageArea = visible;
            if (coverageAreaObject != null)
            {
                coverageAreaObject.SetActive(visible);
            }
        }

        public void SetCoverageColor(Color newColor)
        {
            coverageColor = newColor;
            if (coverageMaterial != null)
            {
                coverageMaterial.SetColor("_BaseColor", newColor);
            }
        }

        public void CreateCoverageArea()
        {
            if (transmitter == null) return;

            if (coverageAreaObject != null)
            {
                DestroyImmediate(coverageAreaObject);
            }

            coverageAreaObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coverageAreaObject.name = $"Coverage_{transmitter.uniqueID}";
            coverageAreaObject.transform.SetParent(transform);
            coverageAreaObject.transform.localPosition = Vector3.zero;

            // Remove collider
            var collider = coverageAreaObject.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);

            // Setup rendering
            coverageRenderer = coverageAreaObject.GetComponent<Renderer>();
            SetupMaterial();

            UpdateVisualDisplay();
        }

        private void SetupMaterial()
        {
            if (coverageRenderer == null) return;

            if (coverageMaterial == null)
            {
                coverageMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                coverageMaterial.SetFloat("_Surface", 1); // Transparent
                coverageMaterial.SetFloat("_Blend", 0);   // Alpha blend
                coverageMaterial.SetFloat("_ZWrite", 0);
                coverageMaterial.SetInt("_Cull", 2);
                coverageMaterial.renderQueue = 3000;
            }

            // Create instance so multiple coverage areas don’t share the same asset
            Material materialInstance = new Material(coverageMaterial);
            materialInstance.SetColor("_BaseColor", coverageColor);

            coverageRenderer.material = materialInstance;
            coverageRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coverageRenderer.receiveShadows = false;
        }

        public void SetCustomMaterial(Material customMaterial)
        {
            coverageMaterial = customMaterial;
        }

        private void UpdateVisualDisplay()
        {
            if (coverageAreaObject == null || !showCoverageArea) return;

            // Apply limits
            float clampedRadius = Mathf.Clamp(displayRadius, minRadiusMeters, maxRadiusMeters);
            float diameterUnityUnits = clampedRadius * worldScale * 2f;

            // Safety clamp
            diameterUnityUnits = Mathf.Clamp(diameterUnityUnits, 20f, 2000f);

            // Apply scale
            coverageAreaObject.transform.localScale = new Vector3(diameterUnityUnits, coverageHeight, diameterUnityUnits);
        }

        // Simple getters
        public float GetCurrentRadiusMeters() => displayRadius;
        public bool IsCoverageVisible() => showCoverageArea && coverageAreaObject != null && coverageAreaObject.activeInHierarchy;
    }
}