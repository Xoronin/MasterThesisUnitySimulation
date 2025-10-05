using UnityEngine;
using UnityEngine.UI;
using RFSimulation.Core.Components;
using RFSimulation.UI;

namespace RFSimulation.Utils
{
    public class ClickHandler : MonoBehaviour
    {
        [Header("UI References")]
        public StatusUI statusUI;

        [Header("Settings")]
        public LayerMask clickableLayerMask = -1;
        public KeyCode clearKey = KeyCode.Escape;

        private Camera mainCamera;

        void Start()
        {
            mainCamera = Camera.main;

            // Ensure panel starts in a known state
            if (statusUI != null) statusUI.ClearSelection();
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
            }

            if (Input.GetKeyDown(clearKey))
            {
                if (statusUI != null) statusUI.ClearSelection();
            }
        }

        private void HandleMouseClick()
        {
            if (statusUI == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, clickableLayerMask))
            {
                // Prefer Transmitter, then Receiver (same as before)
                var tx = hit.collider.GetComponent<Transmitter>();
                if (tx != null)
                {
                    statusUI.ShowTransmitter(tx);
                    return;
                }

                var rx = hit.collider.GetComponent<Receiver>();
                if (rx != null)
                {
                    statusUI.ShowReceiver(rx);
                    return;
                }
            }
        }
    }
}
