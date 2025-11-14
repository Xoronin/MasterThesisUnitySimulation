using UnityEngine;
using System;
using System.Numerics;

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

    [CreateAssetMenu(fileName = "New Building Material", menuName = "Radio Propagation Simulation/Building Material")]
    public class BuildingMaterial : ScriptableObject
    {
        [Header("Material Properties")]
        public MaterialType materialType;
        public string materialName;

        [Header("Permittivity and Conductivity")]
        public float relativePermittivity;
        public float conductivityCoefficient;
        public float conductivityExponent;

        [Header("Surface Roughness")]
        [Min(0f)]
        public float roughness = 0.02f;

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
                    material.roughness = 3.0f;
                    material.relativePermittivity = 5.24f;
                    material.conductivityCoefficient = 0.0462f;
                    material.conductivityExponent = 0.7822f;
                    break;

                case MaterialType.Brick:
                    material.materialName = "Brick";
                    material.roughness = 1.0f;
                    material.scatterAlbedo = 0.35f;
                    material.relativePermittivity = 3.91f;
                    material.conductivityCoefficient = 0.0238f;
                    material.conductivityExponent = 0.16f;
                    break;

                case MaterialType.Metal:
                    material.materialName = "Metal";
                    material.roughness = 0.03f;
                    material.relativePermittivity = 1.0f;
                    material.conductivityCoefficient = 1e7f;
                    material.conductivityExponent = 0f;
                    break;

                case MaterialType.Glass:
                    material.materialName = "Glass";
                    material.roughness = 0.03f;
                    material.relativePermittivity = 6.31f;
                    material.conductivityCoefficient = 0.0036f;
                    material.conductivityExponent = 1.3394f;
                    break;
            }

            return material;
        }

    }
}