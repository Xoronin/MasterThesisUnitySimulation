using UnityEngine;

namespace RFSimulation.Environment
{
    [System.Serializable]
    public enum MaterialType
    {
        Concrete,
        Brick,
        Metal,
        Glass
    }

    [CreateAssetMenu(fileName = "New Building Material", menuName = "RF Simulation/Building Material")]
    public class BuildingMaterial : ScriptableObject
    {
        [Header("Material Properties")]
        public MaterialType materialType;
        public string materialName;

        [Header("Surface Roughness (for specular reduction)")]
        [Min(0f)]
        public float roughnessSigmaMeters = 0.02f;

        public float reflectionCoefficient;

        [Header("Diffuse Scattering")]
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
                    material.roughnessSigmaMeters = 0.03f;
                    material.scatterAlbedo = 0.25f;
                    break;

                case MaterialType.Brick:
                    material.materialName = "Brick";
                    material.roughnessSigmaMeters = 0.05f;
                    material.scatterAlbedo = 0.35f;
                    break;

                case MaterialType.Metal:
                    material.materialName = "Metal";
                    material.roughnessSigmaMeters = 0.005f;
                    material.scatterAlbedo = 0.05f;
                    break;

                case MaterialType.Glass:
                    material.materialName = "Glass";
                    material.roughnessSigmaMeters = 0.002f;
                    material.scatterAlbedo = 0.10f;
                    break;
            }

            return material;
        }

        // Returns reflection coefficient based on frequency
        public static float GetReflectionCoefficient(float frequency_GHz, BuildingMaterial material)
        {
            // Low frequency (< 1 GHz) - VHF/UHF bands
            if (frequency_GHz < 1.0f)
            {
                return GetLowFrequencyReflection(material);
            }
            // Mid frequency (1-6 GHz) - Sub-6 5G, WiFi
            else if (frequency_GHz <= 6.0f)
            {
                return GetMidFrequencyReflection(material);
            }
            // High frequency (6-30 GHz) - mmWave 5G
            else if (frequency_GHz <= 30.0f)
            {
                return GetHighFrequencyReflection(material);
            }
            // Very high frequency (> 30 GHz)
            else
            {
                return GetVeryHighFrequencyReflection(material);
            }
        }

        private static float GetLowFrequencyReflection(BuildingMaterial mat)
        {
            switch (mat.materialType)
            {
                case MaterialType.Concrete: return 0.35f; // Lower reflection
                case MaterialType.Brick: return 0.40f;
                case MaterialType.Glass: return 0.55f;
                case MaterialType.Metal: return 0.90f;
                default: return 0.40f;
            }
        }

        private static float GetMidFrequencyReflection(BuildingMaterial mat)
        {
            switch (mat.materialType)
            {
                case MaterialType.Concrete: return 0.45f; // Standard values
                case MaterialType.Brick: return 0.50f;
                case MaterialType.Glass: return 0.70f;
                case MaterialType.Metal: return 0.95f;
                default: return 0.50f;
            }
        }

        private static float GetHighFrequencyReflection(BuildingMaterial mat)
        {
            switch (mat.materialType)
            {
                case MaterialType.Concrete: return 0.55f; // Higher reflection
                case MaterialType.Brick: return 0.60f;
                case MaterialType.Glass: return 0.75f;
                case MaterialType.Metal: return 0.98f;
                default: return 0.60f;
            }
        }

        private static float GetVeryHighFrequencyReflection(BuildingMaterial mat)
        {
            switch (mat.materialType)
            {
                case MaterialType.Concrete: return 0.60f; // Even higher
                case MaterialType.Brick: return 0.65f;
                case MaterialType.Glass: return 0.80f;
                case MaterialType.Metal: return 0.99f;
                default: return 0.65f;
            }
        }

    }
}