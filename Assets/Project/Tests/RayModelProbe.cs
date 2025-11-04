// Assets/Tests/RayModelProbe.cs
using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.PathLoss.Models;
using UnityEngine;

public class RayModelProbe : MonoBehaviour
{
    public RayTracingModel model;

    [Header("Probe Settings")]
    public float freqMHz = 2000f;   // 2 GHz
    public float txPowerDbm = 0f;   // for readability
    public int buildingLayer = 8;

    void Start()
    {
        Application.targetFrameRate = 10;
        Random.InitState(42); // deterministic

        // --- 1) Minimal geometry ---
        var tx = Spawn("TX", new Vector3(-50, 2, 0));
        var rx = Spawn("RX", new Vector3(50, 2, 0));

        // Single wall (20m wide, 8m tall) at x=0
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.position = new Vector3(0, 4, 0);
        wall.transform.localScale = new Vector3(0.5f, 8f, 20f);
        wall.layer = buildingLayer;
        var col = wall.GetComponent<BoxCollider>(); col.enabled = true;

        // Attach your Building component + material if you have it
        var bld = wall.AddComponent<RFSimulation.Environment.Building>();
        bld.material = ScriptableObject.CreateInstance<RFSimulation.Environment.BuildingMaterial>();
        bld.material.reflectionCoefficient = 0.7f;
        bld.material.roughnessSigmaMeters = 0.03f;
        bld.material.scatterAlbedo = 0.3f;

        // --- 2) Prepare model + context ---
        if (model == null) model = new RayTracingModel();
        model.enableRayVisualization = false; // keep output clean
        model.enableDiffuseScattering = true; // toggle on/off to compare
        model.maxReflections = 1;
        model.maxDiffractions = 1;
        model.mapboxBuildingLayer = 1 << buildingLayer;

        var ctx = new PropagationContext
        {
            TransmitterPosition = tx.transform.position,
            ReceiverPosition = rx.transform.position,
            FrequencyMHz = freqMHz,
            TransmitterPowerDbm = txPowerDbm,
            BuildingLayers = model.mapboxBuildingLayer
        };

        // --- 3) Run once and print a concise report ---
        float pathLossDb = model.CalculatePathLoss(ctx);
        float prxDbm = txPowerDbm - pathLossDb;

        Debug.Log($"[Probe] f={freqMHz} MHz  d={Vector3.Distance(ctx.TransmitterPosition, ctx.ReceiverPosition):F1} m");
        Debug.Log($"[Probe] PathLoss={pathLossDb:F2} dB  Prx={prxDbm:F2} dBm");

        // --- 4) Sanity checks against simple expectations ---
        // S1: LOS only (temporarily disable wall by moving it aside)
        var losDist = Vector3.Distance(tx.transform.position, rx.transform.position);
        float fspl = 32.44f + 20f * Mathf.Log10(losDist * 0.001f) + 20f * Mathf.Log10(freqMHz);
        Debug.Log($"[Expect] FSPL(LOS) ≈ {fspl:F2} dB");
    }

    private GameObject Spawn(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        return go;
    }
}
