using UnityEngine;
using RFSimulation.Environment;

namespace RFSimulation.Environment
{
    public class Building : MonoBehaviour
    {
        [Header("Building Properties")]
        public BuildingMaterial material;
        public float height;
        public int floors;
        public string buildingMaterial;

        [Header("RF Properties")]
        public bool blockSignals = true;
        public LayerMask buildingLayer = 1 << 8; // Layer 8 for buildings

        private void Start()
        {
            // Set building to the correct layer
            gameObject.layer = 8;

            // Ensure we have a material
            if (material == null)
            {
                material = BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete);
            }

            // Ensure collider exists for raycast detection
            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        public bool IsPositionInside(Vector3 position)
        {
            Bounds bounds = GetComponent<Collider>().bounds;
            return bounds.Contains(position);
        }
    }
}