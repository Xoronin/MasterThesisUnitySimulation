using UnityEngine;

namespace RadioSignalSimulation.Environment
{
    [System.Serializable]
    public enum MaterialType
    {
        Concrete,
        Brick,
        Metal,
        Glass,
        Wood
    }

    [CreateAssetMenu(fileName = "New Building Material", menuName = "RF Simulation/Building Material")]
    public class BuildingMaterial : ScriptableObject
    {
        [Header("Material Properties")]
        public MaterialType materialType;
        public string materialName;

        [Header("RF Properties")]
        [Tooltip("Penetration loss in dB per meter")]
        public float penetrationLoss = 10f;

        [Tooltip("Reflection coefficient (0-1)")]
        [Range(0f, 1f)]
        public float reflectionCoefficient = 0.3f;

        [Tooltip("Frequency-dependent attenuation factor")]
        public float frequencyFactor = 1f;

        [Header("Physical Properties")]
        public float density = 2000f; // kg/m³
        public float thickness = 0.2f; // meters

        public static BuildingMaterial GetDefaultMaterial(MaterialType type)
        {
            var material = CreateInstance<BuildingMaterial>();
            material.materialType = type;

            switch (type)
            {
                case MaterialType.Concrete:
                    material.materialName = "Concrete";
                    material.penetrationLoss = 10.62f;
                    material.reflectionCoefficient = 0.7f;
                    material.density = 2400f;
                    break;

                case MaterialType.Brick:
                    material.materialName = "Brick";
                    material.penetrationLoss = 4.25f;
                    material.reflectionCoefficient = 0.6f;
                    material.density = 1800f;
                    break;

                case MaterialType.Metal:
                    material.materialName = "Metal";
                    material.penetrationLoss = 50f;
                    material.reflectionCoefficient = 0.9f;
                    material.density = 7800f;
                    break;

                case MaterialType.Glass:
                    material.materialName = "Glass";
                    material.penetrationLoss = 2f;
                    material.reflectionCoefficient = 0.4f;
                    material.density = 2500f;
                    break;

                case MaterialType.Wood:
                    material.materialName = "Wood";
                    material.penetrationLoss = 3f;
                    material.reflectionCoefficient = 0.2f;
                    material.density = 600f;
                    break;
            }

            return material;
        }

        public float CalculatePenetrationLoss(float frequency, float thickness)
        {
            // Frequency-dependent penetration loss
            float frequencyMultiplier = 1f + (frequency / 2400f - 1f) * frequencyFactor;
            return penetrationLoss * thickness * frequencyMultiplier;
        }
    }
}