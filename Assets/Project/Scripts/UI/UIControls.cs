using TMPro;
using UnityEngine;
using UnityEngine.UI;
//using UnityEngine.UIElements;
using RadioSignalSimulation.Core;

public class UIControls : MonoBehaviour
{
    //public UIDocument uiDocument;

    [Header("UI References")]
    public Button addTransmitterButton;
    public Button addReceiverButton;
    public Button removeAllButton;
    public Button pauseResumeButton;
    public InputField transmitterPowerInput;
    public InputField transmitterFrequencyInput;
    public InputField transmitterCoverageInput;
    public InputField receiverSensitivityInput;
    public Toggle showConnectionsToggle;
    public Toggle showGridToggle;
    public Text statusText;
    public Slider speedSlider;
    public Text speedText;

    [Header("Prefab References")]
    public GameObject transmitterPrefab;
    public GameObject receiverPrefab;

    [Header("Placement Settings")]
    public LayerMask placementLayerMask = 1;
    public float placementHeight = 10f;

    [Header("Ground Settings")]
    public float groundLevel = 0f;
    public LayerMask groundLayerMask = 1;

    [Header("Grid Settings")]
    public bool enableGridSnap = true;
    public GroundGrid groundGridComponent;

    private Camera mainCamera;
    private bool isPlacingTransmitter = false;
    private bool isPlacingReceiver = false;
    private bool isPaused = false;

    void Start()
    {
        mainCamera = Camera.main;

        CreateUI();
    }

    void CreateUI()
    {
        // Setup button listeners
        if (addTransmitterButton != null)
            addTransmitterButton.onClick.AddListener(StartPlacingTransmitter);

        if (addReceiverButton != null)
            addReceiverButton.onClick.AddListener(StartPlacingReceiver);

        if (removeAllButton != null)
            removeAllButton.onClick.AddListener(RemoveAllObjects);

        if (pauseResumeButton != null)
            pauseResumeButton.onClick.AddListener(TogglePauseResume);

        if (showConnectionsToggle != null)
            showConnectionsToggle.onValueChanged.AddListener(ToggleConnections);

        if (speedSlider != null)
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);

        if (showGridToggle != null)
            showGridToggle.onValueChanged.AddListener(ToggleGrid);

        if (groundGridComponent == null)
        {
            groundGridComponent = FindObjectOfType<GroundGrid>();
            if (groundGridComponent != null)
            {
                Debug.Log($"Auto-found GroundGrid component on: {groundGridComponent.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("No GroundGrid component found! Please assign it in the inspector.");
            }
        }

        // Set default values
        if (transmitterPowerInput != null) transmitterPowerInput.text = "40";
        if (transmitterFrequencyInput != null) transmitterFrequencyInput.text = "2400";
        if (transmitterCoverageInput != null) transmitterCoverageInput.text = "1500";
        if (receiverSensitivityInput != null) receiverSensitivityInput.text = "-90";
        if (showConnectionsToggle != null) showConnectionsToggle.isOn = true;
        if (showGridToggle != null) showGridToggle.isOn = true;
        if (speedSlider != null) speedSlider.value = 1f;

        UpdateStatusText("Ready to place objects");
        UpdateSpeedText(1f);
    }

    private void Update()
    {
        HandlePlacement();
    }

    private void HandlePlacement()
    {
        if (isPlacingTransmitter || isPlacingReceiver)
        {
            if (Input.GetMouseButtonDown(0))
            {
                PlaceObjectAtMousePosition();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
            }
        }
    }

    public void StartPlacingTransmitter()
    {
        isPlacingTransmitter = true;
        isPlacingReceiver = false;
        UpdateStatusText("Click to place transmitter (ESC to cancel)");
    }

    public void StartPlacingReceiver()
    {
        isPlacingReceiver = true;
        isPlacingTransmitter = false;
        UpdateStatusText("Click to place receiver (ESC to cancel)");
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        if (!enableGridSnap) return position;

        if (groundGridComponent != null)
        {
            return groundGridComponent.SnapToGrid(position);
        }

        // Fallback if no GroundGrid (shouldn't happen)
        Debug.LogWarning("No GroundGrid component found for snapping!");
        return position;
    }

    private void PlaceObjectAtMousePosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Vector3 placementPosition;
        bool foundPosition = false;

        // Method 1: Try to hit actual ground/terrain first
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayerMask))
        {
            placementPosition = hit.point;
            foundPosition = true;
            Debug.Log($"Hit ground at: {placementPosition}");
        }
        // Method 2: Fallback to ground plane
        else
        {
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevel, 0));
            float distance;

            if (groundPlane.Raycast(ray, out distance))
            {
                placementPosition = ray.GetPoint(distance);
                foundPosition = true;
                Debug.Log($"Used ground plane at: {placementPosition}");
            }
            else
            {
                // Method 3: Last resort - project onto XZ plane at current ground level
                Vector3 rayDir = ray.direction;
                if (Mathf.Abs(rayDir.y) > 0.001f) // Avoid division by zero
                {
                    float t = (groundLevel - ray.origin.y) / rayDir.y;
                    placementPosition = ray.origin + rayDir * t;
                    foundPosition = true;
                    Debug.Log($"Used XZ projection at: {placementPosition}");
                }
                else
                {
                    Debug.LogWarning("Could not determine placement position");
                    return;
                }
            }
        }

        if (!foundPosition) return;

        // Optional: Snap to grid (comment out if you want exact mouse placement)
        if (enableGridSnap)
        {
            Vector3 originalPosition = placementPosition;
            placementPosition = SnapToGrid(placementPosition);
            Debug.Log($"Snapped from {originalPosition} to {placementPosition}");
        }

        // Place the object
        if (isPlacingTransmitter)
        {
            PlaceTransmitter(placementPosition);
        }
        else if (isPlacingReceiver)
        {
            PlaceReceiver(placementPosition);
        }

        CancelPlacement();
    }

    private void PlaceTransmitter(Vector3 position)
    {
        position.y = groundLevel + placementHeight;

        GameObject newTransmitter = Instantiate(transmitterPrefab, position, Quaternion.identity);
        Transmitter transmitterComponent = newTransmitter.GetComponent<Transmitter>();

        if (transmitterComponent != null)
        {
            // Apply UI settings
            transmitterComponent.powerOutput = float.Parse(transmitterPowerInput.text);
            transmitterComponent.frequency = float.Parse(transmitterFrequencyInput.text);
            transmitterComponent.coverageRadius = float.Parse(transmitterCoverageInput.text);
            transmitterComponent.showConnections = showConnectionsToggle.isOn;

            UpdateStatusText($"Transmitter placed at {position}");
        }
    }

    private void PlaceReceiver(Vector3 position)
    {
        position.y = groundLevel + 1f;

        GameObject newReceiver = Instantiate(receiverPrefab, position, Quaternion.identity);
        Receiver receiverComponent = newReceiver.GetComponent<Receiver>();

        if (receiverComponent != null)
        {
            // Apply UI settings
            receiverComponent.sensitivity = float.Parse(receiverSensitivityInput.text);

            UpdateStatusText($"Receiver placed at {position}");
        }
    }

    private void CancelPlacement()
    {
        isPlacingTransmitter = false;
        isPlacingReceiver = false;
        UpdateStatusText("Ready to place objects");
    }

    public void RemoveAllObjects()
    {
        // Remove all transmitters
        foreach (var transmitter in SimulationManager.Instance.transmitters.ToArray())
        {
            if (transmitter != null)
            {
                transmitter.ClearAllLines();
                SimulationManager.Instance.RemoveTransmitter(transmitter);
                DestroyImmediate(transmitter.gameObject);
            }
        }

        // Remove all receivers
        foreach (var receiver in SimulationManager.Instance.receivers.ToArray())
        {
            if (receiver != null)
            {
                SimulationManager.Instance.RemoveReceiver(receiver);
                DestroyImmediate(receiver.gameObject);
            }
        }

        UpdateStatusText("All objects removed");
    }

    public void ToggleConnections(bool enabled)
    {
        foreach (var transmitter in SimulationManager.Instance.transmitters)
        {
            if (transmitter != null)
            {
                transmitter.showConnections = enabled;
                if (!enabled)
                {
                    transmitter.ClearAllLines();
                }
            }
        }

        UpdateStatusText($"Connections {(enabled ? "enabled" : "disabled")}");
    }

    public void TogglePauseResume()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            SimulationManager.Instance.StopSimulation();
            if (pauseResumeButton != null)
                pauseResumeButton.GetComponentInChildren<Text>().text = "Resume";
            UpdateStatusText("Simulation paused");
        }
        else
        {
            SimulationManager.Instance.StartSimulation();
            if (pauseResumeButton != null)
                pauseResumeButton.GetComponentInChildren<Text>().text = "Pause";
            UpdateStatusText("Simulation resumed");
        }
    }

    void ToggleGrid(bool show)
    {
        GroundGrid grid = FindObjectOfType<GroundGrid>();
        if (grid != null)
        {
            grid.gameObject.SetActive(show);
            UpdateStatusText($"Grid {(show ? "enabled" : "disabled")}");
        }
    }

    void OnSpeedChanged(float value)
    {
        SimulationManager.Instance.updateInterval = 0.1f / value; // Adjust update interval
        UpdateSpeedText(value);
    }

    void UpdateSpeedText(float speed)
    {
        if (speedText != null)
            speedText.text = $"Speed: {speed:F1}x";
    }


    public void PauseSimulation()
    {
        SimulationManager.Instance.StopSimulation();
        UpdateStatusText("Simulation paused");
    }

    public void ResumeSimulation()
    {
        SimulationManager.Instance.StartSimulation();
        UpdateStatusText("Simulation resumed");
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"UI: {message}");
    }
}