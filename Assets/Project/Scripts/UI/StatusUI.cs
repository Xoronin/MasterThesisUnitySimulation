using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RFSimulation.Core.Components; // Transmitter, Receiver
using RFSimulation.Core.Managers; // Transmitter, Receiver

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
        public Text txCoverage;                     // coverage radius in m
        public Text txConnectedReceivers;           // number of connected receivers

        [Header("Receiver Group")]
        public GameObject receiverGroup;                // parent container
        public Dropdown rxTechDropdown;             // "5G", "LTE", etc.
        public InputField rxSensitivityInput;       // dBm
        public InputField rxHeightInput;            // m (writes to transform.y)
        public Text rxSignalLabel;           // optional, read-only
        public Text rxConnectedTransmitter;  // optional, read-only

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

        private void OnTxHeightEdited(string s)
        {
            if (_selectedTx == null) return;
            if (TryParseFloat(s, out float v))
                _selectedTx.transform.position = new Vector3(_selectedTx.transform.position.x, v, _selectedTx.transform.position.z);
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
            if (TryParseFloat(s, out float v))
                _selectedRx.transform.position = new Vector3(_selectedRx.transform.position.x, v, _selectedRx.transform.position.z);
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
            txHeightInput?.SetTextWithoutNotify(_selectedTx.transform.position.y.ToString("F1", Ci));
            if (txCoverage != null)
            {
                int coverage = _selectedTx.EstimateCoverageRadius() < 0 ? 0 : Mathf.RoundToInt(_selectedTx.EstimateCoverageRadius());
                txCoverage.text = coverage.ToString();
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
            rxHeightInput?.SetTextWithoutNotify(_selectedRx.transform.position.y.ToString("F1", Ci));
            // Signal label (optional real value)
            if (rxSignalLabel != null) rxSignalLabel.text = _selectedRx.currentSignalStrength.ToString();
            _isUpdatingUI = false;
            // Connected transmitter (optional)
            if (rxConnectedTransmitter != null)
            {
                var tx = _selectedRx.GetConnectedTransmitter();
                rxConnectedTransmitter.text = tx != null ? tx.uniqueID : "—";
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
