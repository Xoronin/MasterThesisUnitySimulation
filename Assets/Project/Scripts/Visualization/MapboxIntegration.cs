using UnityEngine;
using RFSimulation.Visualization;
using RFSimulation.Core.Components;
using System.Collections;

namespace RFSimulation.Visualization
{
    /// <summary>
    /// Integration component that handles positioning heatmap correctly in Mapbox world coordinates
    /// </summary>
    public class MapboxHeatmapIntegration : MonoBehaviour
    {
        [Header("Mapbox References")]
        [SerializeField] private Transform mapboxRoot; 
        [SerializeField] private GameObject terrainObject; 
        [SerializeField] private LayerMask mapboxTerrainLayers = 6; 

        [Header("Heatmap Settings")]
        [SerializeField] private SignalHeatmap heatmapComponent;
        [SerializeField] private bool autoFindMapboxObjects = true;

        [Header("Coordinate System")]
        [SerializeField] private bool useMapboxCoordinates = true;
        [SerializeField] private Vector3 mapboxOffset = Vector3.zero; // Manual offset if needed

        private Bounds mapboxBounds;
        private bool isInitialized = false;

        void Start()
        {
            StartCoroutine(InitializeWithDelay());
        }

        private IEnumerator InitializeWithDelay()
        {
            // Wait for Mapbox to fully initialize
            yield return new WaitForSeconds(1f);

            if (autoFindMapboxObjects)
            {
                FindMapboxObjects();
            }

            CalculateMapboxBounds();
            InitializeHeatmapPositioning();

            isInitialized = true;

        }

        private void FindMapboxObjects()
        {
            // Try to find Mapbox root object
            if (mapboxRoot == null)
            {
                // Common Mapbox root object names
                string[] mapboxRootNames = { "Map", "MapboxMap", "Mapbox", "AbstractMap", "map" };

                foreach (string name in mapboxRootNames)
                {
                    GameObject found = GameObject.Find(name);
                    if (found != null)
                    {
                        mapboxRoot = found.transform;
                        break;
                    }
                }
            }

            // Try to find terrain/ground object
            if (terrainObject == null)
            {
                // Look for terrain objects
                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);
                foreach (GameObject obj in allObjects)
                {
                    if (IsTerrainObject(obj))
                    {
                        terrainObject = obj;
                        break;
                    }
                }
            }

            // Find or create heatmap component
            if (heatmapComponent == null)
            {
                heatmapComponent = GetComponent<SignalHeatmap>();
                if (heatmapComponent == null)
                {
                    heatmapComponent = gameObject.AddComponent<SignalHeatmap>();
                }
            }
        }

        private bool IsTerrainObject(GameObject obj)
        {
            string name = obj.name.ToLower();

            // Check for terrain-like names
            if (name.Contains("terrain") || name.Contains("ground") ||
                name.Contains("plane") || name.Contains("surface"))
            {
                return true;
            }

            // Check if object has terrain-like properties
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Large, flat objects are likely terrain
                Bounds bounds = renderer.bounds;
                if (bounds.size.x > 100f && bounds.size.z > 100f && bounds.size.y < 10f)
                {
                    return true;
                }
            }

            // Check if it's a Unity Terrain
            return obj.GetComponent<Terrain>() != null;
        }

        private void CalculateMapboxBounds()
        {
            if (mapboxRoot == null)
            {
                // Use world bounds if no Mapbox root found
                mapboxBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
                return;
            }

            // Calculate bounds of all renderers under Mapbox root
            Renderer[] renderers = mapboxRoot.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                mapboxBounds = new Bounds(mapboxRoot.position, Vector3.one * 1000f);
                return;
            }

            mapboxBounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                mapboxBounds.Encapsulate(renderers[i].bounds);
            }

        }

        private void InitializeHeatmapPositioning()
        {
            if (heatmapComponent == null) return;

            // Position the heatmap system correctly in Mapbox world space
            Vector3 targetPosition;

            if (useMapboxCoordinates && mapboxRoot != null)
            {
                // Use Mapbox coordinate system
                targetPosition = mapboxBounds.center + mapboxOffset;
            }
            else
            {
                // Use world coordinates
                targetPosition = transform.position + mapboxOffset;
            }

            // Set the heatmap center
            transform.position = targetPosition;

            // Update heatmap settings for Mapbox environment
            var settings = heatmapComponent.settings;
            if (settings != null)
            {
                // Adjust terrain and building layers for Mapbox
                settings.terrainLayer = mapboxTerrainLayers;
                settings.buildingLayer = 1 << 8; // Your building layer from project knowledge

                // Adjust sample radius based on Mapbox bounds
                if (mapboxBounds.size.magnitude > 0)
                {
                    float recommendedRadius = 0.5f * Mathf.Min(mapboxBounds.size.x, mapboxBounds.size.z) * 0.95f;
                    settings.sampleRadius = Mathf.Clamp(recommendedRadius, 500f, 5000f);
                }
            }
        }

        // Custom terrain height detection for Mapbox
        public float GetMapboxTerrainHeight(Vector3 worldPosition)
        {
            // Try multiple raycast strategies for Mapbox terrain

            // Strategy 1: Direct raycast down
            if (Physics.Raycast(worldPosition + Vector3.up * 1000f, Vector3.down, out RaycastHit hit, 2000f, mapboxTerrainLayers))
            {
                return hit.point.y;
            }

            // Strategy 2: Use terrain object if available
            if (terrainObject != null)
            {
                Collider terrainCollider = terrainObject.GetComponent<Collider>();
                if (terrainCollider != null)
                {
                    Vector3 closestPoint = terrainCollider.ClosestPoint(worldPosition);
                    return closestPoint.y;
                }

                // If it's a Unity Terrain
                Terrain terrain = terrainObject.GetComponent<Terrain>();
                if (terrain != null)
                {
                    Vector3 terrainLocalPos = worldPosition - terrain.transform.position;
                    Vector3 normalizedPos = new Vector3(
                        terrainLocalPos.x / terrain.terrainData.size.x,
                        0,
                        terrainLocalPos.z / terrain.terrainData.size.z
                    );

                    if (normalizedPos.x >= 0 && normalizedPos.x <= 1 && normalizedPos.z >= 0 && normalizedPos.z <= 1)
                    {
                        float height = terrain.terrainData.GetInterpolatedHeight(normalizedPos.x, normalizedPos.z);
                        return height + terrain.transform.position.y;
                    }
                }
            }

            // Strategy 3: Use Mapbox bounds as fallback
            return mapboxBounds.center.y - mapboxBounds.size.y * 0.5f;
        }

        // Method to update heatmap position relative to a specific transmitter or point of interest
        public void CenterHeatmapOn(Vector3 worldPosition)
        {
            if (!isInitialized) return;

            Vector3 adjustedPosition = worldPosition + mapboxOffset;
            transform.position = adjustedPosition;

            if (heatmapComponent != null)
            {
                heatmapComponent.SetCenter(transform);
            }
        }

        public void CenterHeatmapOnTransmitter(Transmitter transmitter)
        {
            if (transmitter != null)
            {
                CenterHeatmapOn(transmitter.transform.position);
            }
        }

        // Method to center heatmap on all transmitters (centroid)
        public void CenterHeatmapOnAllTransmitters()
        {
            Transmitter[] transmitters = FindObjectsByType<Transmitter>(FindObjectsSortMode.InstanceID);

            if (transmitters.Length == 0)
            {
                Debug.LogWarning("[MapboxHeatmap] No transmitters found to center on");
                return;
            }

            Vector3 centroid = Vector3.zero;
            foreach (var tx in transmitters)
            {
                centroid += tx.transform.position;
            }
            centroid /= transmitters.Length;

            CenterHeatmapOn(centroid);
        }


        // Gizmos for debugging positioning
        void OnDrawGizmosSelected()
        {
            if (!isInitialized) return;

            // Draw Mapbox bounds
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(mapboxBounds.center, mapboxBounds.size);

            // Draw heatmap area
            if (heatmapComponent != null && heatmapComponent.settings != null)
            {
                Gizmos.color = Color.yellow;
                float radius = heatmapComponent.settings.sampleRadius;
                Gizmos.DrawWireCube(transform.position, Vector3.one * radius * 2f);

                // Draw center point
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position, 10f);
            }

            // Draw terrain object
            if (terrainObject != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(terrainObject.transform.position, Vector3.one * 50f);
            }
        }

        // Public method to recalculate positioning if Mapbox updates
        public void RecalculatePositioning()
        {
            StartCoroutine(InitializeWithDelay());
        }
    }
}