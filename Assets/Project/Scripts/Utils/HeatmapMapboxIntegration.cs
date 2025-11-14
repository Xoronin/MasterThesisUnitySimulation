using UnityEngine;
using RFSimulation.Visualization;
using RFSimulation.Core.Components;
using System.Collections;

namespace RFSimulation.Utils
{

    public class MapboxHeatmapIntegration : MonoBehaviour
    {
        [Header("Mapbox References")]
        [SerializeField] private Transform mapboxRoot;
        [SerializeField] private LayerMask mapboxTerrainLayers = 6;

        [Header("Heatmap Settings")]
        [SerializeField] private HeatmapVisualization heatmapComponent;
        [SerializeField] private bool autoFindMapboxObjects = true;

        [Header("Coordinate System")]
        [SerializeField] private bool useMapboxCoordinates = true;
        [SerializeField] private Vector3 mapboxOffset = Vector3.zero; 

        private Bounds mapboxBounds;
        private bool isInitialized = false;

        void Start()
        {
            StartCoroutine(InitializeWithDelay());
        }

        private IEnumerator InitializeWithDelay()
        {
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
            if (mapboxRoot == null)
            {
                string[] mapboxRootNames = { "BuildingsMap3D" };

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

            if (heatmapComponent == null)
            {
                heatmapComponent = GetComponent<HeatmapVisualization>();
                if (heatmapComponent == null)
                {
                    heatmapComponent = gameObject.AddComponent<HeatmapVisualization>();
                }
            }
        }

        private void CalculateMapboxBounds()
        {
            if (mapboxRoot == null)
            {
                mapboxBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
                return;
            }

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

            Vector3 targetPosition;

            if (useMapboxCoordinates && mapboxRoot != null)
            {
                targetPosition = mapboxBounds.center + mapboxOffset;
            }
            else
            {
                targetPosition = transform.position + mapboxOffset;
            }

            transform.position = targetPosition;

            var settings = heatmapComponent.settings;
            if (settings != null)
            {
                settings.terrainLayer = mapboxTerrainLayers;
                settings.buildingLayer = 1 << 8; 

                if (mapboxBounds.size.magnitude > 0)
                {
                    float recommendedRadius = 0.5f * Mathf.Min(mapboxBounds.size.x, mapboxBounds.size.z) * 0.95f;
                    settings.sampleRadius = Mathf.Clamp(recommendedRadius, 500f, 5000f);
                }
            }
        }

        public float GetMapboxTerrainHeight(Vector3 worldPosition)
        {
            if (Physics.Raycast(worldPosition + Vector3.up * 1000f, Vector3.down, out RaycastHit hit, 2000f, mapboxTerrainLayers))
            {
                return hit.point.y;
            }

            return mapboxBounds.center.y - mapboxBounds.size.y * 0.5f;
        }

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

    }
}