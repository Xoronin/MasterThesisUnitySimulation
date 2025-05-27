using System.Collections.Generic;
using UnityEngine;

public class RadioHeatmapManager : MonoBehaviour
{
    [Header("Transmitter Settings")]
    public Transform transmitter;
    public float transmitterPower = 20f; // dBm
    public float frequency = 2400f; // MHz (2.4 GHz WiFi)

    [Header("Heatmap Settings")]
    public int heatmapResolution = 64; // 64x64 Heatmap
    public float heatmapSize = 200f; // 200m x 200m Bereich
    public float heatmapHeight = 10f; // 10m über dem Boden

    [Header("Signal Thresholds (dBm)")]
    public float excellentThreshold = -30f; // Grün
    public float goodThreshold = -60f;      // Gelb-Grün  
    public float fairThreshold = -80f;      // Orange
    public float poorThreshold = -100f;     // Rot

    [Header("Visualization")]
    public Material heatmapMaterial;
    public bool showTransmitter = true;
    public GameObject transmitterVisualization;

    [Header("Interactive Features")]
    public bool enableTransmitterMovement = true;
    public KeyCode regenerateKey = KeyCode.R;
    public KeyCode toggleHeatmapKey = KeyCode.H;
    public KeyCode cycleCameraKey = KeyCode.C;

    private bool heatmapVisible = true;
    private int cameraMode = 0; // 0=Top, 1=Angle, 2=Ground
    private Camera mainCamera;

    private Texture2D heatmapTexture;
    private GameObject heatmapPlane;
    private float[,] signalStrengthGrid;

    void Start()
    {
        mainCamera = Camera.main;
        SetupTransmitter();
        CreateHeatmapTexture();
        CreateHeatmapPlane();
        StartCoroutine(GenerateHeatmap());
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        // Heatmap neu generieren
        if (Input.GetKeyDown(regenerateKey))
        {
            RegenerateHeatmap();
        }

        // Heatmap ein/ausblenden
        if (Input.GetKeyDown(toggleHeatmapKey))
        {
            ToggleHeatmap();
        }

        // Kamera-Modi wechseln
        if (Input.GetKeyDown(cycleCameraKey))
        {
            CycleCameraMode();
        }

        // Transmitter mit Maus bewegen
        if (enableTransmitterMovement && Input.GetMouseButton(0))
        {
            MoveTransmitterWithMouse();
        }

        // Transmitter mit Pfeiltasten bewegen
        if (enableTransmitterMovement)
        {
            MoveTransmitterWithKeys();
        }
    }

    void MoveTransmitterWithMouse()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Raycast auf Y=50 Ebene (Transmitter-Höhe)
        float targetY = 50f;
        float t = (targetY - ray.origin.y) / ray.direction.y;

        if (t > 0)
        {
            Vector3 newPos = ray.origin + ray.direction * t;
            transmitter.position = new Vector3(newPos.x, targetY, newPos.z);

            // Transmitter Visualisierung aktualisieren
            if (transmitterVisualization != null)
            {
                transmitterVisualization.transform.position = transmitter.position;
            }
        }
    }

    void MoveTransmitterWithKeys()
    {
        float moveSpeed = 10f * Time.deltaTime;
        Vector3 movement = Vector3.zero;

        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
            movement.z += moveSpeed;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
            movement.z -= moveSpeed;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            movement.x -= moveSpeed;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            movement.x += moveSpeed;

        if (movement != Vector3.zero)
        {
            transmitter.position += movement;
            if (transmitterVisualization != null)
            {
                transmitterVisualization.transform.position = transmitter.position;
            }
        }
    }

    void ToggleHeatmap()
    {
        heatmapVisible = !heatmapVisible;
        if (heatmapPlane != null)
        {
            heatmapPlane.SetActive(heatmapVisible);
        }
        Debug.Log($"Heatmap {(heatmapVisible ? "enabled" : "disabled")}");
    }

    void CycleCameraMode()
    {
        if (mainCamera == null) return;

        cameraMode = (cameraMode + 1) % 3;

        switch (cameraMode)
        {
            case 0: // Top View
                mainCamera.transform.position = new Vector3(0, 100, 0);
                mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
                Debug.Log("Camera: Top View");
                break;

            case 1: // Angled View
                mainCamera.transform.position = new Vector3(-50, 80, -50);
                mainCamera.transform.LookAt(Vector3.zero);
                Debug.Log("Camera: Angled View");
                break;

            case 2: // Ground Level
                mainCamera.transform.position = new Vector3(0, 20, -80);
                mainCamera.transform.rotation = Quaternion.Euler(10, 0, 0);
                Debug.Log("Camera: Ground Level");
                break;
        }
    }

    void SetupTransmitter()
    {
        if (transmitter == null)
        {
            GameObject txGO = new GameObject("Radio Transmitter");
            transmitter = txGO.transform;
            transmitter.position = new Vector3(0, 50, 0);
        }

        // Transmitter Visualisierung
        if (showTransmitter && transmitterVisualization == null)
        {
            transmitterVisualization = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            transmitterVisualization.transform.position = transmitter.position;
            transmitterVisualization.transform.localScale = new Vector3(2f, 10f, 2f);
            transmitterVisualization.name = "Transmitter Antenna";

            // Rot färben
            Renderer txRenderer = transmitterVisualization.GetComponent<Renderer>();
            txRenderer.material.color = Color.red;

            // WICHTIG: Collider entfernen damit Raycasts nicht blockiert werden
            DestroyImmediate(transmitterVisualization.GetComponent<Collider>());
        }
    }

    void CreateHeatmapTexture()
    {
        heatmapTexture = new Texture2D(heatmapResolution, heatmapResolution, TextureFormat.RGB24, false);
        heatmapTexture.filterMode = FilterMode.Bilinear;
        heatmapTexture.wrapMode = TextureWrapMode.Clamp;

        signalStrengthGrid = new float[heatmapResolution, heatmapResolution];
    }

    void CreateHeatmapPlane()
    {
        heatmapPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        heatmapPlane.name = "Signal Heatmap";
        heatmapPlane.transform.position = new Vector3(0, heatmapHeight, 0);
        heatmapPlane.transform.localScale = new Vector3(heatmapSize / 10f, 1f, heatmapSize / 10f);

        // Einfacheres Material Setup
        if (heatmapMaterial == null)
        {
            heatmapMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        heatmapPlane.GetComponent<Renderer>().material = heatmapMaterial;

        // Collider entfernen (nur Visualisierung)
        DestroyImmediate(heatmapPlane.GetComponent<Collider>());
    }

    System.Collections.IEnumerator GenerateHeatmap()
    {
        Debug.Log("Generating signal strength heatmap...");

        float startX = -heatmapSize / 2f;
        float startZ = -heatmapSize / 2f;
        float stepSize = heatmapSize / heatmapResolution;

        int processedPixels = 0;
        int totalPixels = heatmapResolution * heatmapResolution;

        // Debug: Position des Transmitters in Grid-Koordinaten
        Vector3 txPos = transmitter.position;
        int txGridX = Mathf.RoundToInt((txPos.x - startX) / stepSize);
        int txGridZ = Mathf.RoundToInt((txPos.z - startZ) / stepSize);
        Debug.Log($"Transmitter at world pos {txPos} -> Grid coords ({txGridX}, {txGridZ})");

        for (int x = 0; x < heatmapResolution; x++)
        {
            for (int z = 0; z < heatmapResolution; z++)
            {
                // Weltposition für diesen Pixel
                Vector3 worldPos = new Vector3(
                    startX + x * stepSize,
                    heatmapHeight,
                    startZ + z * stepSize
                );

                // Signalstärke berechnen
                float signalStrength = CalculateSignalStrength(transmitter.position, worldPos);
                signalStrengthGrid[x, z] = signalStrength;

                // Debug für Grid-Position nahe dem Transmitter
                if (Mathf.Abs(x - txGridX) <= 2 && Mathf.Abs(z - txGridZ) <= 2)
                {
                    Debug.Log($"Near TX - Grid[{x},{z}] = World{worldPos} = {signalStrength:F1}dBm");
                }

                processedPixels++;

                // Performance: Alle 100 Pixel warten
                if (processedPixels % 100 == 0)
                {
                    Debug.Log($"Heatmap progress: {processedPixels}/{totalPixels} ({(float)processedPixels / totalPixels * 100f:F1}%)");
                    yield return new WaitForEndOfFrame();
                }
            }
        }

        // Textur aus Daten erstellen
        UpdateHeatmapTexture();
        Debug.Log("Heatmap generation complete!");
    }

    void UpdateHeatmapTexture()
    {
        Color[] pixels = new Color[heatmapResolution * heatmapResolution];

        // Min/Max für Normalisierung finden
        float minSignal = float.MaxValue;
        float maxSignal = float.MinValue;

        for (int x = 0; x < heatmapResolution; x++)
        {
            for (int z = 0; z < heatmapResolution; z++)
            {
                float signal = signalStrengthGrid[x, z];
                if (signal < minSignal) minSignal = signal;
                if (signal > maxSignal) maxSignal = signal;
            }
        }

        // Pixels färben - KORREKTUR: Koordinaten-Mapping
        for (int x = 0; x < heatmapResolution; x++)
        {
            for (int z = 0; z < heatmapResolution; z++)
            {
                float signalStrength = signalStrengthGrid[x, z];
                Color pixelColor = GetHeatmapColor(signalStrength);

                // KORRIGIERT: Texture2D Koordinaten richtig mappen
                // Unity Texture: [0,0] = links unten, aber Array: [0,0] = links oben
                int textureX = x;
                int textureY = (heatmapResolution - 1) - z; // Z umkehren für korrekte Orientierung
                int pixelIndex = textureY * heatmapResolution + textureX;

                pixels[pixelIndex] = pixelColor;
            }
        }

        heatmapTexture.SetPixels(pixels);
        heatmapTexture.Apply();

        // Textur dem Material zuweisen
        heatmapMaterial.mainTexture = heatmapTexture;

        Debug.Log($"Heatmap updated - Signal range: {minSignal:F1} to {maxSignal:F1} dBm");
    }

    Color GetHeatmapColor(float signalStrength)
    {
        Color color;
        float alpha = 0.7f; // Transparenz

        if (signalStrength >= excellentThreshold)
        {
            // Excellent: Hellgrün
            color = Color.green;
        }
        else if (signalStrength >= goodThreshold)
        {
            // Good: Grün zu Gelb
            float t = (signalStrength - goodThreshold) / (excellentThreshold - goodThreshold);
            color = Color.Lerp(Color.yellow, Color.green, t);
        }
        else if (signalStrength >= fairThreshold)
        {
            // Fair: Gelb zu Orange
            float t = (signalStrength - fairThreshold) / (goodThreshold - fairThreshold);
            color = Color.Lerp(new Color(1f, 0.5f, 0f), Color.yellow, t); // Orange zu Gelb
        }
        else if (signalStrength >= poorThreshold)
        {
            // Poor: Orange zu Rot
            float t = (signalStrength - poorThreshold) / (fairThreshold - poorThreshold);
            color = Color.Lerp(Color.red, new Color(1f, 0.5f, 0f), t); // Rot zu Orange
        }
        else
        {
            // Very Poor: Dunkelrot
            color = new Color(0.5f, 0f, 0f); // Dunkelrot
        }

        color.a = alpha;
        return color;
    }

    float CalculateSignalStrength(Vector3 transmitterPos, Vector3 receiverPos)
    {
        // 1. Distanz
        float distance = Vector3.Distance(transmitterPos, receiverPos);
        if (distance < 1f) distance = 1f; // Minimum Distanz

        // 2. Free Space Path Loss
        float freeSpacePathLoss = CalculateFreeSpacePathLoss(distance, frequency);

        // 3. Obstruction Loss (Gebäude-Hindernisse)
        float obstructionLoss = CalculateObstructionLoss(transmitterPos, receiverPos);

        // 4. Gesamte empfangene Signalstärke
        float receivedPower = transmitterPower - freeSpacePathLoss - obstructionLoss;

        // Debug für Punkte nahe dem Transmitter
        if (distance < 20f)
        {
            Debug.Log($"Close to TX - Distance: {distance:F1}m, FSPL: {freeSpacePathLoss:F1}dB, ObsLoss: {obstructionLoss:F1}dB, Total: {receivedPower:F1}dBm");
        }

        return receivedPower;
    }

    float CalculateFreeSpacePathLoss(float distance, float frequencyMHz)
    {
        // FSPL (dB) = 20*log10(d) + 20*log10(f) + 32.45
        // d in km, f in MHz

        float distanceKm = distance / 1000f;
        if (distanceKm < 0.001f) distanceKm = 0.001f;

        float fspl = 20f * Mathf.Log10(distanceKm) + 20f * Mathf.Log10(frequencyMHz) + 32.45f;
        return fspl;
    }

    float CalculateObstructionLoss(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        float distance = Vector3.Distance(from, to);

        // Raycast mit LayerMask um nur Gebäude zu treffen
        int buildingLayerMask = LayerMask.GetMask("Default"); // Oder spezifischer Building Layer

        // Raycast leicht vom Transmitter weg starten um Selbst-Kollision zu vermeiden
        Vector3 startOffset = direction * 2f; // 2m Offset
        Vector3 startPos = from + startOffset;
        float adjustedDistance = distance - 2f;

        RaycastHit hit;
        if (Physics.Raycast(startPos, direction, out hit, adjustedDistance, buildingLayerMask))
        {
            if (hit.collider.CompareTag("Building"))
            {
                // Verlust basierend auf Gebäude-Durchdringung
                float penetrationDepth = Vector3.Distance(hit.point, to);
                float buildingLoss = Mathf.Min(penetrationDepth * 2f, 25f); // Max 25dB
                return buildingLoss;
            }
        }

        return 0f; // Freie Sichtlinie
    }

    // Heatmap zur Laufzeit aktualisieren
    [ContextMenu("Regenerate Heatmap")]
    public void RegenerateHeatmap()
    {
        if (heatmapTexture != null)
        {
            StartCoroutine(GenerateHeatmap());
        }
    }

    // GUI für Live-Info
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"📡 Radio Signal Simulation", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
        GUILayout.Label($"Transmitter: {transmitter.position}");
        GUILayout.Label($"Power: {transmitterPower} dBm");
        GUILayout.Label($"Frequency: {frequency} MHz");
        GUILayout.Label($"Heatmap: {heatmapResolution}x{heatmapResolution}");
        GUILayout.Label($"Coverage: {heatmapSize}m x {heatmapSize}m");

        GUILayout.Space(10);
        GUILayout.Label("Signal Quality Legend:");
        GUILayout.Label($"🟢 Excellent: > {excellentThreshold} dBm");
        GUILayout.Label($"🟡 Good: {excellentThreshold} to {goodThreshold} dBm");
        GUILayout.Label($"🟠 Fair: {goodThreshold} to {fairThreshold} dBm");
        GUILayout.Label($"🔴 Poor: < {fairThreshold} dBm");

        if (GUILayout.Button("Regenerate Heatmap"))
        {
            RegenerateHeatmap();
        }

        GUILayout.EndArea();
    }
}