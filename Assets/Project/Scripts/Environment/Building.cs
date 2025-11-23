using UnityEngine;

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
        public LayerMask buildingLayer = 1 << 8;

        private void Start()
        {
            gameObject.layer = 8;

            if (material == null)
            {
                material = BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete);
            }

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