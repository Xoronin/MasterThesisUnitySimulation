using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Core;
using RFSimulation.Core.Components;
using RFSimulation.Core.Managers;
using RFSimulation.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace RFSimulation.Utils
{
    /// <summary>
    /// Allows cycling through transmitters and receivers in the scene using keyboard controls
    /// - Tab: Cycle through all objects (transmitters first, then receivers)
    /// - T: Cycle through transmitters only
    /// - R: Cycle through receivers only
    /// - Shift+Tab: Cycle backwards through all objects
    /// - F: Focus camera on selected object
    /// </summary>
    public class ObjectCycler : MonoBehaviour
    {
        [Header("Status Panel")]
        public StatusUI statusUI;

        [Header("Cycling Controls")]
        public KeyCode cycleAllKey = KeyCode.Tab;
        public KeyCode cycleTransmittersKey = KeyCode.T;
        public KeyCode cycleReceiversKey = KeyCode.R;
        public KeyCode focusKey = KeyCode.F;

        [Header("Camera Settings")]
        public Camera targetCamera;
        public float focusDistance = 30f;
        public float focusHeight = 20f;
        public float cameraTransitionSpeed = 3f;
        public bool smoothCameraTransition = true;

        [Header("Visual Feedback")]
        public Material highlightMaterial;
        public Color highlightColor = Color.yellow;
        public float highlightPulseSpeed = 2f;
        public bool showInfoPopup = true;

        // Internal state
        private List<Transmitter> transmitters = new List<Transmitter>();
        private List<Receiver> receivers = new List<Receiver>();
        private int currentTransmitterIndex = 0;
        private int currentReceiverIndex = 0;
        private int currentAllIndex = 0;
        private GameObject currentlySelected = null;

        // Highlighting system
        private Dictionary<GameObject, HighlightInfo> highlightedObjects = new Dictionary<GameObject, HighlightInfo>();
        private Material defaultHighlightMaterial;

        // Camera movement
        private Vector3 targetCameraPosition;
        private Vector3 targetCameraRotation;
        private bool isMovingCamera = false;

        private struct HighlightInfo
        {
            public Renderer renderer;
            public Material originalMaterial;
            public bool isHighlighted;
        }

        void Start()
        {
            InitializeSystem();
            RefreshObjectLists();

            // Auto-select first object if available
            if (GetTotalObjectCount() > 0)
            {
                CycleToNextAll();
            }
        }

        void Update()
        {
            if (UIInput.IsTyping()) return;
            HandleInput();
            UpdateCameraMovement();
            UpdateHighlightEffects();
            UpdateObjectLists();
        }

        private void InitializeSystem()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (highlightMaterial == null)
                CreateDefaultHighlightMaterial();
        }

        private void CreateDefaultHighlightMaterial()
        {
            defaultHighlightMaterial = new Material(Shader.Find("Lit"));
            defaultHighlightMaterial.color = highlightColor;
            defaultHighlightMaterial.SetFloat("_Mode", 2); // Fade mode
            defaultHighlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            defaultHighlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            defaultHighlightMaterial.SetInt("_ZWrite", 0);
            defaultHighlightMaterial.EnableKeyword("_ALPHABLEND_ON");
            defaultHighlightMaterial.renderQueue = 3000;
        }

        private void HandleInput()
        {
            // Cycle through all objects
            if (Input.GetKeyDown(cycleAllKey))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    CycleToPreviousAll();
                }
                else
                {
                    CycleToNextAll();
                }
            }

            // Cycle through transmitters only
            if (Input.GetKeyDown(cycleTransmittersKey))
            {
                CycleToNextTransmitter();
            }

            // Cycle through receivers only
            if (Input.GetKeyDown(cycleReceiversKey))
            {
                CycleToNextReceiver();
            }

            // Focus camera on selected object
            if (Input.GetKeyDown(focusKey))
            {
                FocusOnSelectedObject();
            }
        }

        #region Object Cycling Methods

        private void CycleToNextAll()
        {
            int totalObjects = GetTotalObjectCount();
            if (totalObjects == 0) return;

            currentAllIndex = (currentAllIndex + 1) % totalObjects;
            SelectObjectByGlobalIndex(currentAllIndex);
        }

        private void CycleToPreviousAll()
        {
            int totalObjects = GetTotalObjectCount();
            if (totalObjects == 0) return;

            currentAllIndex = (currentAllIndex - 1 + totalObjects) % totalObjects;
            SelectObjectByGlobalIndex(currentAllIndex);
        }

        private void CycleToNextTransmitter()
        {
            if (transmitters.Count == 0) return;

            currentTransmitterIndex = (currentTransmitterIndex + 1) % transmitters.Count;
            SelectTransmitter(currentTransmitterIndex);

            // Update global index
            currentAllIndex = currentTransmitterIndex;
        }

        private void CycleToNextReceiver()
        {
            if (receivers.Count == 0) return;

            currentReceiverIndex = (currentReceiverIndex + 1) % receivers.Count;
            SelectReceiver(currentReceiverIndex);

            // Update global index
            currentAllIndex = transmitters.Count + currentReceiverIndex;
        }

        private void SelectObjectByGlobalIndex(int globalIndex)
        {
            if (globalIndex < transmitters.Count)
            {
                // Select transmitter
                currentTransmitterIndex = globalIndex;
                SelectTransmitter(currentTransmitterIndex);
            }
            else
            {
                // Select receiver
                int receiverIndex = globalIndex - transmitters.Count;
                if (receiverIndex < receivers.Count)
                {
                    currentReceiverIndex = receiverIndex;
                    SelectReceiver(currentReceiverIndex);
                }
            }
        }

        private void SelectTransmitter(int index)
        {
            if (index < 0 || index >= transmitters.Count) return;

            var transmitter = transmitters[index];
            statusUI?.ClearSelection();
            statusUI?.ShowTransmitter(transmitter);
            SelectObject(transmitter.gameObject, "Transmitter");
        }

        private void SelectReceiver(int index)
        {
            if (index < 0 || index >= receivers.Count) return;

            var receiver = receivers[index];
            statusUI?.ClearSelection();
            statusUI?.ShowReceiver(receiver);
            SelectObject(receiver.gameObject, "Receiver");
        }

        private void SelectObject(GameObject obj, string objectType)
        {
            // Clear previous selection
            ClearHighlight();

            // Set new selection
            currentlySelected = obj;

            // Apply highlight
            ApplyHighlight(obj);

            // Auto-focus if enabled
            if (smoothCameraTransition)
            {
                SetCameraTarget(obj.transform.position);
            }
        }

        #endregion

        #region Highlighting System

        private void ApplyHighlight(GameObject obj)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            if (!highlightedObjects.ContainsKey(obj))
            {
                var highlightInfo = new HighlightInfo
                {
                    renderer = renderer,
                    originalMaterial = renderer.material,
                    isHighlighted = true
                };
                highlightedObjects[obj] = highlightInfo;
            }

            // Apply highlight material
            Material materialToUse = highlightMaterial ?? defaultHighlightMaterial;
            renderer.material = materialToUse;
        }

        private void ClearHighlight()
        {
            if (currentlySelected != null && highlightedObjects.ContainsKey(currentlySelected))
            {
                var highlightInfo = highlightedObjects[currentlySelected];
                if (highlightInfo.renderer != null)
                {
                    highlightInfo.renderer.material = highlightInfo.originalMaterial;
                }
                highlightedObjects.Remove(currentlySelected);
            }
        }

        private void UpdateHighlightEffects()
        {
            if (currentlySelected == null) return;

            // Pulse effect
            float pulseValue = 0.5f + 0.5f * Mathf.Sin(Time.time * highlightPulseSpeed);

            if (highlightedObjects.ContainsKey(currentlySelected))
            {
                var renderer = highlightedObjects[currentlySelected].renderer;
                if (renderer != null && renderer.material != null)
                {
                    Color color = highlightColor;
                    color.a = pulseValue;
                    renderer.material.color = color;
                }
            }
        }

        #endregion

        #region Camera Control

        private void SetCameraTarget(Vector3 objectPosition)
        {
            if (targetCamera == null) return;

            // Calculate ideal camera position
            Vector3 offset = new Vector3(0, focusHeight, -focusDistance);
            targetCameraPosition = objectPosition + offset;

            // Calculate rotation to look at object
            Vector3 direction = (objectPosition - targetCameraPosition).normalized;
            targetCameraRotation = Quaternion.LookRotation(direction).eulerAngles;

            isMovingCamera = true;
        }

        private void UpdateCameraMovement()
        {
            if (!isMovingCamera || targetCamera == null) return;

            // Smoothly move camera to target position
            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                targetCameraPosition,
                cameraTransitionSpeed * Time.deltaTime
            );

            // Smoothly rotate camera to target rotation
            targetCamera.transform.rotation = Quaternion.Lerp(
                targetCamera.transform.rotation,
                Quaternion.Euler(targetCameraRotation),
                cameraTransitionSpeed * Time.deltaTime
            );

            // Check if we're close enough to stop moving
            if (Vector3.Distance(targetCamera.transform.position, targetCameraPosition) < 0.5f)
            {
                isMovingCamera = false;
            }
        }

        private void FocusOnSelectedObject()
        {
            if (currentlySelected != null)
            {
                SetCameraTarget(currentlySelected.transform.position);
            }
        }

        #endregion

        #region Object Management

        private void RefreshObjectLists()
        {
            UpdateObjectLists();
        }

        private void UpdateObjectLists()
        {
            // Only update every few frames for performance
            if (Time.frameCount % 30 != 0) return;

            // Get objects from SimulationManager if available
            if (SimulationManager.Instance != null)
            {
                transmitters = new List<Transmitter>(SimulationManager.Instance.transmitters);
                receivers = new List<Receiver>(SimulationManager.Instance.receivers);
            }
            else
            {
                // Fallback: find all objects in scene
                transmitters = new List<Transmitter>(FindObjectsByType<Transmitter>(FindObjectsSortMode.InstanceID));
                receivers = new List<Receiver>(FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID));
            }

            // Remove null references
            transmitters.RemoveAll(t => t == null);
            receivers.RemoveAll(r => r == null);

            // Validate current indices
            if (currentTransmitterIndex >= transmitters.Count)
                currentTransmitterIndex = 0;

            if (currentReceiverIndex >= receivers.Count)
                currentReceiverIndex = 0;

            if (currentAllIndex >= GetTotalObjectCount())
                currentAllIndex = 0;
        }

        private int GetTotalObjectCount()
        {
            return transmitters.Count + receivers.Count;
        }

        #endregion

        #region Public Methods

        public void SelectTransmitterByID(string uniqueID)
        {
            for (int i = 0; i < transmitters.Count; i++)
            {
                if (transmitters[i].uniqueID == uniqueID)
                {
                    currentTransmitterIndex = i;
                    currentAllIndex = i;
                    SelectTransmitter(i);
                    return;
                }
            }
        }

        public void SelectReceiverByID(string uniqueID)
        {
            for (int i = 0; i < receivers.Count; i++)
            {
                if (receivers[i].uniqueID == uniqueID)
                {
                    currentReceiverIndex = i;
                    currentAllIndex = transmitters.Count + i;
                    SelectReceiver(i);
                    return;
                }
            }
        }

        public GameObject GetCurrentlySelected()
        {
            return currentlySelected;
        }

        public void SetCameraTransitionSpeed(float speed)
        {
            cameraTransitionSpeed = speed;
        }

        #endregion

        void OnDestroy()
        {
            // Clean up highlight materials
            ClearHighlight();

            if (defaultHighlightMaterial != null)
            {
                DestroyImmediate(defaultHighlightMaterial);
            }
        }
    }
}