using UnityEngine;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.VectorTile;
using System.Collections.Generic;
using Mapbox.VectorTile;
using Mapbox.VectorTile.Geometry;

// Add your namespace reference
using RFSimulation.Environment;

[CreateAssetMenu(menuName = "Mapbox/Modifiers/RF Building Modifier")]
public class RFBuildingModifier : GameObjectModifier
{
	[Header("Default Materials")]
	public BuildingMaterial defaultMaterial;
	public BuildingMaterial residentialMaterial;
	public BuildingMaterial commercialMaterial;
	public BuildingMaterial industrialMaterial;

	public override void Run(VectorEntity ve, UnityTile tile)
	{
		GameObject buildingObject = ve.GameObject;

		// Add Building component for RF simulation
		Building building = buildingObject.GetComponent<Building>();
		if (building == null)
		{
			building = buildingObject.AddComponent<Building>();
		}

		// Get properties from the VectorFeature instead
		var properties = ve.Feature.Properties;

		// Determine material based on OSM properties
		BuildingMaterial material = DetermineMaterialFromProperties(building, properties);
        building.material = material;

		// Set building properties from OSM data
		SetBuildingProperties(building, properties);

		// Configure for RF simulation
		buildingObject.layer = 8; // Buildings layer

		// Add collider for raycast detection
		if (buildingObject.GetComponent<Collider>() == null)
		{
			MeshCollider meshCollider = buildingObject.AddComponent<MeshCollider>();
			meshCollider.convex = false;
		}
	}

    private BuildingMaterial DetermineMaterialFromProperties(Building building, Dictionary<string, object> properties)
	{
        if (properties.ContainsKey("building:type"))
        {
            string buildingType = properties["building:type"].ToString().ToLower();
			building.buildingType = buildingType;
        }

		if (properties.ContainsKey("building:material"))
		{
			string material = properties["building:material"].ToString().ToLower();
			building.buildingMaterial = material;
            Debug.Log("Building material: " + material);
		}

        // Check building type from OSM data
        if (properties.ContainsKey("building"))
		{
            string buildingType = properties["building"].ToString().ToLower();

            switch (buildingType)
			{
				case "residential":
				case "house":
				case "apartments":
					return residentialMaterial ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Brick);

				case "commercial":
				case "office":
				case "retail":
					return commercialMaterial ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete);

				case "industrial":
				case "warehouse":
				case "factory":
					return industrialMaterial ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Metal);

				case "school":
				case "hospital":
				case "university":
					return BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete);
			}
		}

		// Check amenity type
		if (properties.ContainsKey("amenity"))
		{
			string amenity = properties["amenity"].ToString().ToLower();
			switch (amenity)
			{
				case "school":
				case "hospital":
				case "library":
					return BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete);
				case "restaurant":
				case "cafe":
					return BuildingMaterial.GetDefaultMaterial(MaterialType.Brick);
			}
		}

		// Height-based fallback
		float height = GetBuildingHeight(properties);
		if (height > 50f)
			return BuildingMaterial.GetDefaultMaterial(MaterialType.Metal);     // High-rise
		else if (height > 20f)
			return BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete); // Mid-rise
		else
			return BuildingMaterial.GetDefaultMaterial(MaterialType.Brick);    // Low-rise
	}

	private void SetBuildingProperties(Building building, Dictionary<string, object> properties)
	{
		float height = GetBuildingHeight(properties);
		building.height = height;

		if (properties.ContainsKey("building:levels"))
		{
			if (int.TryParse(properties["building:levels"].ToString(), out int levels))
			{
				building.floors = levels;
			}
			else
			{
				building.floors = Mathf.Max(1, Mathf.RoundToInt(height / 3f));
			}
		}
		else
		{
			building.floors = Mathf.Max(1, Mathf.RoundToInt(height / 3f));
		}
	}

	private float GetBuildingHeight(Dictionary<string, object> properties)
	{
		if (properties.ContainsKey("height"))
		{
			if (float.TryParse(properties["height"].ToString(), out float height))
			{
				return height;
			}
		}

		if (properties.ContainsKey("building:levels"))
		{
			if (int.TryParse(properties["building:levels"].ToString(), out int levels))
			{
				return levels * 3f; // 3 meters per level
			}
		}

		return 10f; // Default height
	}
}