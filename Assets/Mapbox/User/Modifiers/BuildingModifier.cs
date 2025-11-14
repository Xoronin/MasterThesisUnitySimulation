using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mapbox.Unity.MeshGeneration.Data;
using RFSimulation.Environment;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Mapbox.VectorTile;
using Mapbox.VectorTile.Geometry;

[CreateAssetMenu(menuName = "Mapbox/Modifiers/Building Modifier")]
public class BuildingModifier : GameObjectModifier
{
    [Header("Default Materials (ScriptableObjects)")]
    public BuildingMaterial brick;
    public BuildingMaterial concrete;
    public BuildingMaterial metal;
    public BuildingMaterial glass;

    [Header("RF Simulation Configuration")]
    [Tooltip("Layer for RF ray tracing (default: 8 = Buildings)")]
    public int rfBuildingLayer = 8;

    [Tooltip("Tag for building identification")]
    public string rfBuildingTag = "Building";

    [Tooltip("Enable RF Building component and collider setup")]
    public bool enableRFSimulation = true;

    [Tooltip("Use MeshCollider for accurate ray tracing")]
    public bool useMeshCollider = true;

    [Header("Determinism")]
    [Tooltip("Global seed so the same city gets the same assignments every run.")]
    public int worldSeed = 123456;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    // Optional: cache within a session (helps if Mapbox re-spawns tiles during panning/zoom)
    private static readonly Dictionary<string, BuildingMaterial> _assignedCache = new();

    public override void Run(VectorEntity ve, UnityTile tile)
    {
        var go = ve.GameObject;
        // Must have a mesh to be usable
        var mf = go.GetComponent<MeshFilter>();
        var mesh = mf ? mf.sharedMesh : null;

        if (enableRFSimulation)
        {
            SetupRFSimulation(go, mesh);
        }

        // Ensure RF Building component exists
        var building = go.GetComponent<Building>();
        if (!building) building = go.AddComponent<Building>();

        // Extract OSM properties
        var props = ve.Feature?.Properties ?? new Dictionary<string, object>();

        // Deterministic key for this building
        string key = BuildStableKey(props, tile, go);

        if (!_assignedCache.TryGetValue(key, out var mat))
        {
            mat = DetermineMaterialDeterministic(props, tile, go, key);
            building.buildingMaterial = mat.materialName;
            _assignedCache[key] = mat;
        }

        building.material = mat ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete);
        SetBuildingProperties(building, props, go);

        // Layer + collider for RF ray tests (only if mesh is valid)
        go.layer = 8; // make sure layer 8 exists / named appropriately
        if (!go.GetComponent<MeshCollider>())
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh; // assign explicitly
            mc.convex = false;
            mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
                                MeshColliderCookingOptions.CookForFasterSimulation;
        }
    }

    /// <summary>
    /// Sets up the GameObject for RF simulation - layer, tag, and collider
    /// </summary>
    private void SetupRFSimulation(GameObject go, Mesh mesh)
    {
        // Set layer for RF ray tracing
        go.layer = rfBuildingLayer;

        // Set tag for building identification
        try
        {
            go.tag = rfBuildingTag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[RFBuildingModifier] Tag '{rfBuildingTag}' doesn't exist. " +
                               "Create it in Project Settings > Tags and Layers");
        }

        // Ensure proper collider for RF ray tracing
        SetupRFCollider(go, mesh);
    }

    /// Sets up the appropriate collider for RF ray tracing
    /// </summary>
    private void SetupRFCollider(GameObject go, Mesh mesh)
    {
        var existingCollider = go.GetComponent<Collider>();

        if (existingCollider == null)
        {
            if (useMeshCollider && mesh != null)
            {
                // MeshCollider for accurate ray tracing
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false; // More accurate for ray casting
                mc.isTrigger = false; // Important for RF simulation
                mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
                                   MeshColliderCookingOptions.CookForFasterSimulation;
            }
            else
            {
                // BoxCollider fallback (faster but less accurate)
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = false;
            }
        }
        else
        {
            // Ensure existing collider is configured for RF simulation
            if (existingCollider.isTrigger)
            {
                existingCollider.isTrigger = false;
            }
        }
    }


    private static bool IsFinite(Vector3 p) =>
        float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);

    // -------- Deterministic selection --------

    private BuildingMaterial DetermineMaterialDeterministic(
        Dictionary<string, object> props, UnityTile tile, GameObject go, string key)
    {
        // 1) If OSM has an explicit "building:material", map it directly.
        if (TryMapOsmMaterial(props, out var explicitMat))
            return explicitMat;

        // 2) If OSM has "building" type, use type-specific weighted distributions.
        string bType = TryGetStr(props, "building");
        float height = GetBuildingHeight(props, go);

        var rnd = new System.Random(StableHash(worldSeed, key));

        if (!string.IsNullOrEmpty(bType))
        {
            switch (bType.ToLowerInvariant())
            {
                case "residential":
                case "house":
                case "apartments":
                    return WeightedPick(rnd, new (BuildingMaterial mat, float w)[] {
                        (brick ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Brick),     0.60f),
                        (concrete ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete), 0.40f),
                    });

                case "commercial":
                case "office":
                case "retail":
                    return WeightedPick(rnd, new[] {
                        (concrete ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete), 0.50f),
                        (glass    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Glass),    0.30f),
                        (metal    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Metal),    0.20f),
                    });

                case "industrial":
                case "warehouse":
                case "factory":
                    return WeightedPick(rnd, new[] {
                        (metal    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Metal),    0.60f),
                        (concrete ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete), 0.30f),
                        (brick    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Brick),    0.10f),
                    });

                // schools/hospitals/universities lean concrete/glass
                case "school":
                case "hospital":
                case "university":
                    return WeightedPick(rnd, new[] {
                        (concrete ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete), 0.70f),
                        (glass    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Glass),    0.30f),
                    });
            }
        }

        // 3) Height-based fallback (still weighted, more glass/metal when taller)
        if (height > 50f)
            return WeightedPick(rnd, new[] {
                (metal    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Metal),    0.45f),
                (glass    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Glass),    0.35f),
                (concrete ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete), 0.20f),
            });
        if (height > 20f)
            return WeightedPick(rnd, new[] {
                (concrete ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete), 0.55f),
                (glass    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Glass),    0.25f),
                (brick    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Brick),    0.20f),
            });

        // Low-rise default
        return WeightedPick(rnd, new[] {
            (brick    ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Brick),    0.65f),
            (concrete ?? BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete), 0.35f),
        });
    }

    private static BuildingMaterial WeightedPick(System.Random rnd, (BuildingMaterial mat, float w)[] items)
    {
        float total = items.Sum(i => i.w);
        double r = rnd.NextDouble() * total;
        double cum = 0;
        foreach (var i in items)
        {
            cum += i.w;
            if (r <= cum) return i.mat;
        }
        return items[^1].mat;
    }

    private static bool TryMapOsmMaterial(Dictionary<string, object> props, out BuildingMaterial mat)
    {
        mat = null;
        if (props.TryGetValue("building:material", out var v))
        {
            string m = v.ToString().ToLowerInvariant();
            switch (m)
            {
                case "brick": mat = BuildingMaterial.GetDefaultMaterial(MaterialType.Brick); break;
                case "concrete": mat = BuildingMaterial.GetDefaultMaterial(MaterialType.Concrete); break;
                case "metal": mat = BuildingMaterial.GetDefaultMaterial(MaterialType.Metal); break;
                case "glass": mat = BuildingMaterial.GetDefaultMaterial(MaterialType.Glass); break;
            }
        }
        return mat != null;
    }

    private static string BuildStableKey(Dictionary<string, object> props, UnityTile tile, GameObject go)
    {
        // Prefer OSM ids if present
        string id =
            TryGetStr(props, "id") ??
            TryGetStr(props, "osm_id") ??
            TryGetStr(props, "@id"); // Some extracts use @id

        if (!string.IsNullOrEmpty(id))
            return $"osm:{id}";

        // Fallback: tile + quantized position (stable enough for extrusions)
        var uid = tile?.UnwrappedTileId.ToString();
        var p = go.transform.position;
        // Quantize to decimeters to avoid float noise
        int qx = Mathf.RoundToInt(p.x * 10f);
        int qz = Mathf.RoundToInt(p.z * 10f);
        int qy = Mathf.RoundToInt(p.y * 10f);
        return $"tile:{uid}|pos:{qx},{qy},{qz}";
    }

    private static string TryGetStr(Dictionary<string, object> props, string key)
        => props.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static int StableHash(int worldSeed, string s)
    {
        unchecked
        {
            uint hash = 2166136261u ^ (uint)worldSeed;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 16777619;
            }
            return (int)hash;
        }
    }

    private void SetBuildingProperties(Building b, Dictionary<string, object> props, GameObject go)
    {
        float height = GetBuildingHeight(props, go);
        b.height = height;

        if (props.TryGetValue("building:levels", out var lv) && int.TryParse(lv.ToString(), out var levels))
            b.floors = Mathf.Max(1, levels);
        else
            b.floors = Mathf.Max(1, Mathf.RoundToInt(height / 3f));
    }

    private float GetBuildingHeight(Dictionary<string, object> props, GameObject go)
    {
        if (props.TryGetValue("height", out var hv) && float.TryParse(hv.ToString(), out var h))
            return Mathf.Max(1f, h);

        if (props.TryGetValue("building:levels", out var lv) && int.TryParse(lv.ToString(), out var levels))
            return Mathf.Max(1f, levels * 3f);

        var mr = go.GetComponent<Renderer>();
        if (mr != null)
        {
            var b = mr.bounds;
            if (IsFinite(b.center) && IsFinite(b.extents))
            {
                var y = b.size.y;
                if (float.IsFinite(y) && y > 0.1f) return y;
            }
        }
        return 10f;
    }
}
