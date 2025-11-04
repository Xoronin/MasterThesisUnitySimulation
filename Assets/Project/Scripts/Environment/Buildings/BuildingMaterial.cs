using UnityEngine;

namespace RFSimulation.Environment
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
        public float penetrationLoss;

        [Tooltip("Reflection coefficient (0-1)")]
        [Range(0f, 1f)]
        public float reflectionCoefficient;

        [Tooltip("Frequency-dependent attenuation factor")]
        public float frequencyFactor = 1f;

        [Header("Physical Properties")]
        public float density = 2000f; // kg/m³
        public float thickness = 0.2f; // meters

        [Header("Surface Roughness (for specular reduction)")]
        [Tooltip("RMS surface height σ_h in meters (~0.0–0.01 for smooth glass, ~0.02–0.05 concrete, ~0.1 rough brick)")]
        [Min(0f)]
        public float roughnessSigmaMeters = 0.02f;

        [Header("Diffuse Scattering")]
        [Tooltip("Fraction of incident power (post-specular reduction) redistributed diffusely (0..1).")]
        [Range(0f, 1f)]
        public float scatterAlbedo = 0.2f;

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
                    material.roughnessSigmaMeters = 0.03f;
                    material.scatterAlbedo = 0.25f;
                    break;

                case MaterialType.Brick:
                    material.materialName = "Brick";
                    material.penetrationLoss = 4.25f;
                    material.reflectionCoefficient = 0.6f;
                    material.density = 1800f;
                    material.roughnessSigmaMeters = 0.05f;
                    material.scatterAlbedo = 0.35f;
                    break;

                case MaterialType.Metal:
                    material.materialName = "Metal";
                    material.penetrationLoss = 50f;
                    material.reflectionCoefficient = 0.9f;
                    material.density = 7800f;
                    material.roughnessSigmaMeters = 0.005f;
                    material.scatterAlbedo = 0.05f;
                    break;

                case MaterialType.Glass:
                    material.materialName = "Glass";
                    material.penetrationLoss = 2f;
                    material.reflectionCoefficient = 0.4f;
                    material.density = 2500f;
                    material.roughnessSigmaMeters = 0.002f;
                    material.scatterAlbedo = 0.10f;
                    break;

                case MaterialType.Wood:
                    material.materialName = "Wood";
                    material.penetrationLoss = 3f;
                    material.reflectionCoefficient = 0.2f;
                    material.density = 600f;
                    material.roughnessSigmaMeters = 0.04f;
                    material.scatterAlbedo = 0.30f;
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