using UnityEngine;
using RFSimulation.Environment;
using System.Collections;

// This works without assembly definitions
namespace RFSimulation.Environment
{
    public class SimpleMapboxModifier : MonoBehaviour
    {
        [Header("Material Settings")]
        public BuildingMaterial defaultMaterial;

        private void Start()
        {
            if (defaultMaterial == null)
                defaultMaterial = BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete);

            StartCoroutine(ProcessMapboxBuildings());
        }

        private IEnumerator ProcessMapboxBuildings()
        {
            // Wait for Mapbox to generate buildings
            yield return new WaitForSeconds(3f);

            // Find all GameObjects that look like buildings
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            int processed = 0;

            foreach (GameObject obj in allObjects)
            {
                if (IsMapboxBuilding(obj))
                {
                    AddBuildingComponent(obj);
                    processed++;
                }
            }
        }

        private bool IsMapboxBuilding(GameObject obj)
        {
            // Check if it has a mesh and looks like a building
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer == null) return false;

            // Must be tall enough to be a building
            if (renderer.bounds.size.y < 2f) return false;

            // Common Mapbox building indicators
            string name = obj.name.ToLower();
            return name.Contains("building") ||
                   name.Contains("extruded") ||
                   name.Contains("mesh") ||
                   (obj.transform.parent != null &&
                    obj.transform.parent.name.ToLower().Contains("building"));
        }


        private void AddBuildingComponent(GameObject obj)
        {
            // Skip if already has Building component
            if (obj.GetComponent<Building>() != null) return;

            // Add Building component
            Building building = obj.AddComponent<Building>();
            building.material = defaultMaterial;
            building.height = obj.GetComponent<MeshRenderer>().bounds.size.y;
            building.floors = Mathf.Max(1, Mathf.RoundToInt(building.height / 3f));

            // Set layer for RF simulation
            obj.layer = 8;

            // Add collider for raycast
            if (obj.GetComponent<Collider>() == null)
            {
                obj.AddComponent<MeshCollider>();
            }
        }
    }
}