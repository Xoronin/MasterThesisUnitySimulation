using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RFSimulation.Core.Components; // Transmitter, Receiver
using RFSimulation.Propagation.Core; // Transmitter, Receiver
using RFSimulation.Core.Managers; // Transmitter, Receiver
using RFSimulation.Utils;

namespace RFSimulation.UI
{
    public class StatusUI : MonoBehaviour
    {
        [Header("Header")]
        public Text nameHeader;

        [Header("Common Position (applies to selected object)")]
        public GameObject positionGroup;         // parent container
        public InputField posXInput;
        public InputField posYInput;
        public InputField posZInput;
        public Button removeButton;

        [Header("Transmitter Group")]
        public GameObject transmitterGroup;         // parent container
        public InputField txPowerInput;             // dBm
        public InputField txFreqInput;              // MHz
        public InputField txHeightInput;            // m (writes to transform.y)
        public Dropdown txModelDropdown;
        public Text txCoverage;                     // coverage radius in m
        public Text txConnectedReceivers;           // number of connected receivers

        [Header("Receiver Group")]
        public GameObject receiverGroup;                // parent container
        public Dropdown rxTechDropdown;             // "5G", "LTE", etc.
        public InputField rxSensitivityInput;       // dBm
        public InputField rxHeightInput;            // m (writes to transform.y)
        public Text rxSignalLabel;           // optional, read-only
        public Text rxConnectedTransmitter;  // optional, read-only
        public Text distanceToTransmitter;

        [Header("Options")]
        public bool useInvariantDecimal = true;         // dot decimals
        public bool liveRefreshPosition = true;         // updates XYZ display while moving selected

        // current selection
        private Transmitter _selectedTx;
        private Receiver _selectedRx;

        // guard against recursive UI updates when we set field.text from code
        private bool _isUpdatingUI;

        private SimulationManager simulationManager;

        private CultureInfo Ci => useInvariantDecimal ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;

        void Awake()
        {
            SetupManagerReferences();
            WireCommon();
            WireTransmitter();
            WireReceiver();
            EnsureTechOptions();
            EnsureTxModelOptions();
            ClearSelection();
        }

        void Update()
        {
            // Optional live refresh of pos fields (without stealing focus)
            if (liveRefreshPosition && !_isUpdatingUI)
            {
                var t = GetSelectedTransform();
                if (t != null)
                {
                    _isUpdatingUI = true;
                    posXInput?.SetTextWithoutNotify(t.position.x.ToString("F1", Ci));
                    posYInput?.SetTextWithoutNotify(t.position.y.ToString("F1", Ci));
                    posZInput?.SetTextWithoutNotify(t.position.z.ToString("F1", Ci));
                    _isUpdatingUI = false;
                }
            }

            // Example: if you later have a live signal value on Receiver, update here
            if (_selectedRx != null && rxSignalLabel != null)
            {
                rxSignalLabel.text = _selectedRx.currentSignalStrength.ToString(); // replace with your live value if available
            }
        }

        // --------- Public API (call these from your click/selection handler) ---------

        public void ShowTransmitter(Transmitter tx)
        {
            _selectedTx = tx;
            _selectedRx = null;
            RefreshUIFromSelection();
        }

        public void ShowReceiver(Receiver rx)
        {
            _selectedRx = rx;
            _selectedTx = null;
            RefreshUIFromSelection();
        }

        public void ClearSelection()
        {
            _selectedTx = null;
            _selectedRx = null;
            SetHeader("Nothing selected");
            SetGroupActive(transmitterGroup, false);
            SetGroupActive(receiverGroup, false);
            SetGroupActive(positionGroup, false);
            SetButtonActive(removeButton, false);
            ClearCommon();
        }

        // --------- Wiring ---------

        private void SetupManagerReferences()
        {
            if (SimulationManager.Instance != null)
            {
                simulationManager = SimulationManager.Instance;
            }
        }

        private void WireCommon()
        {
            if (posXInput) posXInput.onEndEdit.AddListener(OnPosXEdited);
            if (posYInput) posYInput.onEndEdit.AddListener(OnPosYEdited);
            if (posZInput) posZInput.onEndEdit.AddListener(OnPosZEdited);
            if (removeButton) removeButton.onClick.AddListener(OnRemoveClicked);
        }

        private void WireTransmitter()
        {
            if (txPowerInput) txPowerInput.onEndEdit.AddListener(OnTxPowerEdited);
            if (txFreqInput) txFreqInput.onEndEdit.AddListener(OnTxFreqEdited);
            if (txHeightInput) txHeightInput.onEndEdit.AddListener(OnTxHeightEdited);
            if (txCoverage) txCoverage.text = "—";
            if (txConnectedReceivers) txConnectedReceivers.text = "—";
        }

        private void WireReceiver()
        {
            if (rxTechDropdown) rxTechDropdown.onValueChanged.AddListener(OnRxTechChanged);
            if (rxSensitivityInput) rxSensitivityInput.onEndEdit.AddListener(OnRxSensitivityEdited);
            if (rxHeightInput) rxHeightInput.onEndEdit.AddListener(OnRxHeightEdited);
            if (rxSignalLabel) rxSignalLabel.text = "—";
            if (rxConnectedTransmitter) rxConnectedTransmitter.text = "—";
        }

        private void EnsureTechOptions()
        {
            if (rxTechDropdown == null) return;
            rxTechDropdown.ClearOptions(); // wipe A/B/C
            rxTechDropdown.AddOptions(new System.Collections.Generic.List<Dropdown.OptionData> {
                new Dropdown.OptionData("5G"),
                new Dropdown.OptionData("LTE"),
            });
            rxTechDropdown.value = 0;
            rxTechDropdown.RefreshShownValue();
        }

        private void EnsureTxModelOptions()
        {
            if (txModelDropdown == null) return;
            txModelDropdown.ClearOptions();
            txModelDropdown.AddOptions(new System.Collections.Generic.List<string>(
                new[] { "Auto", "Free Space", "Log Distance", "Hata", "COST 231", "Ray Tracing" }
            ));
            txModelDropdown.onValueChanged.AddListener(OnTxModelChanged);
        }

        // --------- UI -> Object handlers ---------

        private void OnPosXEdited(string s)
        {
            var t = GetSelectedTransform(); if (t == null) return;
            if (TryParseFloat(s, out float v)) t.position = new Vector3(v, t.position.y, t.position.z);
            RefreshCommonFromTransform();
        }

        private void OnPosYEdited(string s)
        {
            var t = GetSelectedTransform(); if (t == null) return;
            if (TryParseFloat(s, out float v)) t.position = new Vector3(t.position.x, v, t.position.z);
            RefreshCommonFromTransform();
        }

        private void OnPosZEdited(string s)
        {
            var t = GetSelectedTransform(); if (t == null) return;
            if (TryParseFloat(s, out float v)) t.position = new Vector3(t.position.x, t.position.y, v);
            RefreshCommonFromTransform();
        }

        private void OnTxPowerEdited(string s)
        {
            if (_selectedTx == null) return;
            if (TryParseFloat(s, out float v)) _selectedTx.transmitterPower = v;
            RefreshTransmitterFields();
        }

        private void OnTxFreqEdited(string s)
        {
            if (_selectedTx == null) return;
            if (TryParseFloat(s, out float v)) _selectedTx.frequency = v;
            RefreshTransmitterFields();
        }

        private void OnTxModelChanged(int i)
        {
            if (_selectedTx == null) return;
            _selectedTx.SetPropagationModel(ModelFromIndex(i));
            RefreshTransmitterFields();
        }

        private void OnTxHeightEdited(string s)
        {
            if (_selectedTx == null) return;
            if (TryParseFloat(s, out float v))
                _selectedTx.SetTransmitterHeight(v);
            RefreshTransmitterFields();
            RefreshCommonFromTransform();
        }

        private void OnRxTechChanged(int idx)
        {
            if (_selectedRx == null || rxTechDropdown == null) return;
            var tech = rxTechDropdown.options[idx].text;
            _selectedRx.SetTechnology(tech);
        }

        private void OnRxSensitivityEdited(string s)
        {
            if (_selectedRx == null) return;
            if (TryParseFloat(s, out float v)) _selectedRx.sensitivity = v;
            RefreshReceiverFields();
        }

        private void OnRxHeightEdited(string s)
        {
            if (_selectedRx == null) return;
            if (!TryParseFloat(s, out float h)) return;

            // interpret UI value as height above ground
            var pos = _selectedRx.transform.position;

            // infer ground Y from current state: worldY = groundY + receiverHeight
            float groundY = pos.y - _selectedRx.receiverHeight;

            // apply new height
            _selectedRx.receiverHeight = h;
            _selectedRx.transform.position = new Vector3(pos.x, groundY + h, pos.z);

            RefreshReceiverFields();
            RefreshCommonFromTransform();
        }


        private void OnRemoveClicked()
        {
            // delete TX
            if (_selectedTx != null)
            {
                var go = _selectedTx.gameObject;
                SimulationManager.Instance?.RemoveTransmitter(_selectedTx); // update lists & connections
                _selectedTx = null;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                ClearSelection();
                return;
            }

            // delete RX
            if (_selectedRx != null)
            {
                var go = _selectedRx.gameObject;
                SimulationManager.Instance?.RemoveReceiver(_selectedRx);
                _selectedRx = null;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                ClearSelection();
                return;
            }

            Debug.LogWarning("[StatusUI] Remove clicked with nothing selected.");
        }

        // --------- Object -> UI refresh ---------

        private void RefreshUIFromSelection()
        {
            if (_selectedTx != null)
            {
                SetHeader("Transmitter: " + _selectedTx.uniqueID);
                SetGroupActive(transmitterGroup, true);
                SetGroupActive(receiverGroup, false);
                SetGroupActive(positionGroup, true);
                SetButtonActive(removeButton, true);
                RefreshCommonFromTransform();
                RefreshTransmitterFields();
            }
            else if (_selectedRx != null)
            {
                SetHeader("Receiver: " + _selectedRx.uniqueID);
                SetGroupActive(transmitterGroup, false);
                SetGroupActive(receiverGroup, true);
                SetGroupActive(positionGroup, true);
                SetButtonActive(removeButton, true);
                RefreshCommonFromTransform();
                RefreshReceiverFields();
            }
            else
            {
                ClearSelection();
            }
        }

        private void RefreshCommonFromTransform()
        {
            var t = GetSelectedTransform(); if (t == null) { ClearCommon(); return; }
            _isUpdatingUI = true;
            posXInput?.SetTextWithoutNotify(t.position.x.ToString("F1", Ci));
            posYInput?.SetTextWithoutNotify(t.position.y.ToString("F1", Ci));
            posZInput?.SetTextWithoutNotify(t.position.z.ToString("F1", Ci));
            _isUpdatingUI = false;
        }

        private void RefreshTransmitterFields()
        {
            if (_selectedTx == null) return;
            _isUpdatingUI = true;
            txPowerInput?.SetTextWithoutNotify(_selectedTx.transmitterPower.ToString("F1", Ci));
            txFreqInput?.SetTextWithoutNotify(_selectedTx.frequency.ToString("F0", Ci));
            txHeightInput?.SetTextWithoutNotify(_selectedTx.transmitterHeight.ToString("F1", Ci));
            if (txModelDropdown != null)
                txModelDropdown.SetValueWithoutNotify(IndexFromModel(_selectedTx.propagationModel));

            if (txCoverage != null)
            {
                float cov = TryGetCoverageRadius(_selectedTx);
                txCoverage.text = cov > 0 ? Mathf.RoundToInt(cov).ToString() : "—";
            }
            if (txConnectedReceivers != null)
            {
                int count = _selectedTx.GetConnectedReceivers() != null ? _selectedTx.GetConnectedReceivers().Count : 0;
                txConnectedReceivers.text = count.ToString();
            }

            _isUpdatingUI = false;
        }

        private void RefreshReceiverFields()
        {
            if (_selectedRx == null) return;
            _isUpdatingUI = true;
            // Tech
            if (rxTechDropdown != null)
            {
                var idx = rxTechDropdown.options.FindIndex(o => o.text == _selectedRx.technology);
                if (idx >= 0) rxTechDropdown.SetValueWithoutNotify(idx);
            }
            rxSensitivityInput?.SetTextWithoutNotify(_selectedRx.sensitivity.ToString("F1", Ci));
            rxHeightInput?.SetTextWithoutNotify(_selectedRx.receiverHeight.ToString("F1", Ci));
            // Signal label (optional real value)
            if (rxSignalLabel != null) rxSignalLabel.text = _selectedRx.currentSignalStrength.ToString();
            _isUpdatingUI = false;
            // Connected transmitter (optional)
            if (rxConnectedTransmitter != null)
            {
                var tx = _selectedRx.GetConnectedTransmitter();
                rxConnectedTransmitter.text = tx != null ? tx.uniqueID : "—";
                distanceToTransmitter.text = tx != null ? Vector3.Distance(tx.transform.position, _selectedRx.transform.position).ToString("F1", Ci) + " m" : "—";
            }

        }

        private void ClearCommon()
        {
            _isUpdatingUI = true;
            posXInput?.SetTextWithoutNotify("");
            posYInput?.SetTextWithoutNotify("");
            posZInput?.SetTextWithoutNotify("");
            _isUpdatingUI = false;
        }

        // --------- Helpers ---------

        private static readonly string[] ModelNames =
            { "Auto", "Free Space", "Log Distance", "Hata", "COST 231 Hata", "Ray Tracing" };

        private static PropagationModel ModelFromIndex(int i)
        {
            switch (i)
            {
                case 0: return PropagationModel.Auto;
                case 1: return PropagationModel.FreeSpace;
                case 2: return PropagationModel.LogDistance;
                case 3: return PropagationModel.Hata;          
                case 4: return PropagationModel.COST231;  
                case 5: return PropagationModel.RayTracing;  
                default: return PropagationModel.Auto;
            }
        }

        private static int IndexFromModel(PropagationModel m)
        {
            switch (m)
            {
                case PropagationModel.Auto: return 0;
                case PropagationModel.FreeSpace: return 1;
                case PropagationModel.LogDistance: return 2;
                case PropagationModel.Hata: return 3;  // adjust if needed
                case PropagationModel.COST231: return 4;  // adjust if needed
                case PropagationModel.RayTracing: return 5;
                default: return 0;
            }
        }

        private float TryGetCoverageRadius(Transmitter tx)
        {
            if (tx == null) return -1f;
            try
            {
                return tx.EstimateCoverageRadius();  // call ONCE, guarded
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[StatusUI] Coverage not ready: {e.Message}");
                return -1f;
            }
        }

        private void SetHeader(string text)
        {
            if (nameHeader != null) nameHeader.text = text;
        }

        private void SetGroupActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        private void SetButtonActive(Button btn, bool active)
        {
            if (btn != null) btn.gameObject.SetActive(active);
        }

        private Transform GetSelectedTransform()
        {
            if (_selectedTx != null) return _selectedTx.transform;
            if (_selectedRx != null) return _selectedRx.transform;
            return null;
        }

        private bool TryParseFloat(string s, out float v)
            => float.TryParse(s, NumberStyles.Float, Ci, out v);
    }
}
