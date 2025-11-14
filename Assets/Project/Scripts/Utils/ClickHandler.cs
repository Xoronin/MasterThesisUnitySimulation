using UnityEngine;
using RFSimulation.Core.Components;
using RFSimulation.Utils; 
using RFSimulation.UI;

namespace RFSimulation.Core.Managers
{
    public class ClickHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _cam;

        [Header("Layer Masks")]
        [SerializeField] private LayerMask selectableMask;
        [SerializeField] private LayerMask forbiddenMask;

        [Header("UI / Managers")]
        [SerializeField] private StatusUI statusUI;
        [SerializeField] private ControlUI controlUI;

        private void Awake()
        {
            if (_cam == null)
                _cam = Camera.main;
        }

        private void Update()
        {
            if (UIInput.IsTyping())
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                statusUI.ClearSelection();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (TryPickSelectable(out var hit))
                    HandleClick(hit);
            }
        }

        private bool TryPickSelectable(out RaycastHit hit)
        {
            hit = default;
            if (_cam == null)
                return false;

            int mask = selectableMask & ~forbiddenMask;

            if (RaycastUtil.RayToGround(_cam, Input.mousePosition, mask, out hit))
            {
                if ((forbiddenMask.value & (1 << hit.collider.gameObject.layer)) != 0)
                    return false;

                return true;
            }

            return false;
        }

        private void HandleClick(RaycastHit hit)
        {
            if (hit.collider == null)
                return;

            var go = hit.collider.gameObject;

            if (go.TryGetComponent(out Transmitter tx))
            {
                statusUI.ShowTransmitter(tx);
                return;
            }

            if (go.TryGetComponent(out Receiver rx))
            {
                statusUI.ShowReceiver(rx);
                return;
            }

            statusUI.ClearSelection();
        }
    }
}
