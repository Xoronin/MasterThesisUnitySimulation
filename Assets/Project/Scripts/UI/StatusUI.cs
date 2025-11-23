using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RFSimulation.Core.Components;
using RFSimulation.Propagation.Core;
using RFSimulation.Core.Managers;
using RFSimulation.Utils;
using System;

namespace RFSimulation.UI
{
    public class StatusUI : MonoBehaviour
    {
        [Header("Header")]
        public Text nameHeader;

        [Header("Common Position (applies to selected object)")]
        public GameObject positionGroup;
        public InputField posXInput;
        public InputField posYInput;
        public InputField posZInput;
        public Button removeButton;

        [Header("Transmitter Group")]
        public GameObject transmitterGroup;
        public InputField txPowerInput;
        public InputField txFreqInput;
        public InputField txHeightInput;
        public InputField txMaxReflectionsInput;
        public InputField txMaxDiffractionsInput;
        public InputField txMaxScatteringInput;
        public Dropdown txModelDropdown;
        public Dropdown txTechDropdown;
        public Text txConnectedReceivers;

        [Header("Receiver Group")]
        public GameObject receiverGroup;
        public Dropdown rxTechDropdown;
        public InputField rxSensitivityInput;
        public InputField rxConnectionMarginInput;
        public InputField rxHeightInput;
        public Text rxSignalLabel;
        public Text rxConnectedTransmitter;
        public Text distanceToTransmitter;

        [Header("Options")]
        public bool useInvariantDecimal = true;
        public bool liveRefreshPosition = true;

        private Transmitter _selectedTx;
        private Receiver _selectedRx;
        private bool _isUpdatingUI;

        private SimulationManager simulationManager;
        private CultureInfo Ci => useInvariantDecimal ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;

        void Awake()
        {
            SetupManagerReferences();
            WireCommon();
            WireTransmitter();
            WireReceiver();
            EnsureRxTechOptions();
            EnsureTxTechOptions();
            EnsureTxModelOptions();
            ClearSelection();
        }

        void Start()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (liveRefreshPosition && !_isUpdatingUI)
            {
                var t = GetSelectedTransform();
                if (t != null)
                {
                    _isUpdatingUI = true;
                    posXInput?.SetTextWithoutNotify(t.position.x.ToString("F2", Ci));
                    posYInput?.SetTextWithoutNotify(t.position.y.ToString("F2", Ci));
                    posZInput?.SetTextWithoutNotify(t.position.z.ToString("F2", Ci));
                    _isUpdatingUI = false;
                }
            }

            if (_selectedRx != null && rxSignalLabel != null)
            {
                rxSignalLabel.text = _selectedRx.currentSignalStrength.ToString("F2", Ci);
            }

            if (_selectedRx != null && distanceToTransmitter != null && rxConnectedTransmitter != null)
            {
                var tx = _selectedRx.GetConnectedTransmitter();
                if (tx != null)
                {
                    rxConnectedTransmitter.text = tx.uniqueID;
                    float d = Vector3.Distance(tx.transform.position, _selectedRx.transform.position);
                    distanceToTransmitter.text = d.ToString("F2", Ci) + " m";
                }
                else
                {
                    rxConnectedTransmitter.text = "—";
                    distanceToTransmitter.text = "—";
                }
            }
        }

        public void ShowTransmitter(Transmitter tx)
        {
            gameObject.SetActive(true);

            _selectedTx = tx;
            _selectedRx = null;
            RefreshUIFromSelection();
        }

        public void ShowReceiver(Receiver rx)
        {
            gameObject.SetActive(true);

            _selectedRx = rx;
            _selectedTx = null;
            RefreshUIFromSelection();
        }

        public void ClearSelection()
        {
            gameObject.SetActive(false);

            _selectedTx = null;
            _selectedRx = null;
            SetHeader("Nothing selected");
            SetGroupActive(transmitterGroup, false);
            SetGroupActive(receiverGroup, false);
            SetGroupActive(positionGroup, false);
            SetButtonActive(removeButton, false);
            ClearCommon();
        }

        private void SetupManagerReferences()
        {
            if (SimulationManager.Instance != null)
                simulationManager = SimulationManager.Instance;
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
            if (txMaxDiffractionsInput) txMaxDiffractionsInput.onEndEdit.AddListener(OnTxMaxDiffractionsEdited);
            if (txMaxReflectionsInput) txMaxReflectionsInput.onEndEdit.AddListener(OnTxMaxReflectionsEdited);
            if (txMaxScatteringInput) txMaxScatteringInput.onEndEdit.AddListener(OnTxMaxScatteringEdited);
            if (txConnectedReceivers) txConnectedReceivers.text = "—";
        }

        private void WireReceiver()
        {
            if (rxSensitivityInput) rxSensitivityInput.onEndEdit.AddListener(OnRxSensitivityEdited);
            if (rxConnectionMarginInput) rxConnectionMarginInput.onEndEdit.AddListener(OnRxConnectionMarginEdited);
            if (rxHeightInput) rxHeightInput.onEndEdit.AddListener(OnRxHeightEdited);
            if (rxSignalLabel) rxSignalLabel.text = "—";
            if (rxConnectedTransmitter) rxConnectedTransmitter.text = "—";
        }

        private void EnsureRxTechOptions()
        {
            if (rxTechDropdown == null) return;

            var techNames = new System.Collections.Generic.List<string>();
            var specs = TechnologySpecifications.GetAllSpecs();
            foreach (var spec in specs)
                techNames.Add(spec.Name);

            rxTechDropdown.ClearOptions();
            rxTechDropdown.AddOptions(techNames);
            rxTechDropdown.value = 0;
            rxTechDropdown.RefreshShownValue();
            rxTechDropdown.onValueChanged.AddListener(OnRxTechChanged);
        }

        private void EnsureTxTechOptions()
        {
            if (txTechDropdown == null) return;

            var techNames = new System.Collections.Generic.List<string>();
            var specs = TechnologySpecifications.GetAllSpecs();
            foreach (var spec in specs)
                techNames.Add(spec.Name);

            txTechDropdown.ClearOptions();
            txTechDropdown.AddOptions(techNames);
            txTechDropdown.value = 0;
            txTechDropdown.RefreshShownValue();
            txTechDropdown.onValueChanged.AddListener(OnTxTechChanged);
        }

        private void EnsureTxModelOptions()
        {
            if (txModelDropdown == null) return;
            txModelDropdown.ClearOptions();
            txModelDropdown.AddOptions(new System.Collections.Generic.List<string>(
                new[] { "FreeSpace", "LogD", "LogNShadow", "Hata", "COST231", "RayTracing" }
            ));
            txModelDropdown.onValueChanged.AddListener(OnTxModelChanged);
        }

        private void OnPosXEdited(string s)
        {
            var t = GetSelectedTransform();
            if (t == null) return;
            if (TryParseFloat(s, out float v))
                t.position = new Vector3(v, t.position.y, t.position.z);
            _selectedTx.ClearPathLossCache();
            RefreshCommonFromTransform();
            RecomputeAll(true);
        }

        private void OnPosYEdited(string s)
        {
            var t = GetSelectedTransform();
            if (t == null) return;
            if (TryParseFloat(s, out float v))
                t.position = new Vector3(t.position.x, v, t.position.z);
            _selectedTx.ClearPathLossCache();
            RefreshCommonFromTransform();
            RecomputeAll(true);
        }

        private void OnPosZEdited(string s)
        {
            var t = GetSelectedTransform();
            if (t == null) return;
            if (TryParseFloat(s, out float v))
                t.position = new Vector3(t.position.x, t.position.y, v);
            _selectedTx.ClearPathLossCache();
            RefreshCommonFromTransform();
            RecomputeAll(true);
        }

        private void OnTxPowerEdited(string s)
        {
            if (_selectedTx == null) return;
            if (TryParseFloat(s, out float v))
                _selectedTx.UpdateTransmitterPower((float)System.Math.Round(v, 2));
            _selectedTx.ClearPathLossCache();  
            RefreshTransmitterFields();
            RecomputeForTx();
        }

        private void OnTxFreqEdited(string s)
        {
            if (_selectedTx == null) return;

            if (TryParseFloat(s, out float fGHz))
            {
                float fMHz = UnitConversionHelper.GHzToMHz(fGHz);
                _selectedTx.UpdateFrequency((float)System.Math.Round(fMHz, 2));
            }

            _selectedTx.ClearPathLossCache();
            RefreshTransmitterFields();
            RecomputeForTx();
        }

        private void OnTxModelChanged(int i)
        {
            if (_selectedTx == null) return;
            _selectedTx.SetPropagationModel(ModelFromIndex(i));
            _selectedTx.ClearPathLossCache();
            RefreshTransmitterFields();
            RecomputeForTx();
        }

        private void OnTxTechChanged(int idx)
        {
            if (_selectedTx == null || txTechDropdown == null) return;

            var tech = TechnologySpecifications.ParseTechnologyString(txTechDropdown.options[idx].text);
            _selectedTx.SetTechnology(tech);

            _selectedTx.ClearPathLossCache();
            RefreshTransmitterFields();
            RecomputeForTx();
        }

        private void OnTxHeightEdited(string s)
        {
            if (_selectedTx == null) return;
            if (TryParseFloat(s, out float h))
                _selectedTx.SetTransmitterHeight(Mathf.Max(0f, h));
            _selectedTx.ClearPathLossCache();
            RefreshTransmitterFields();
            RefreshCommonFromTransform();
            RecomputeForTx();
        }

        private void OnTxMaxDiffractionsEdited(string s)
        {
            if (_selectedTx == null) return;
            int i = Int32.Parse(s);
            _selectedTx.SetMaxDiffractions(i);
            _selectedTx.ClearPathLossCache();
            RefreshTransmitterFields();
            RefreshCommonFromTransform();
            RecomputeForTx();
        }

        private void OnTxMaxReflectionsEdited(string s)
        {
            if (_selectedTx == null) return;
            int i = Int32.Parse(s);
            _selectedTx.SetMaxReflections(i);
            _selectedTx.ClearPathLossCache();
            RefreshTransmitterFields();
            RefreshCommonFromTransform();
            RecomputeForTx();
        }

        private void OnTxMaxScatteringEdited(string s)
        {
            if (_selectedTx == null) return;
            int i = Int32.Parse(s);
            _selectedTx.SetMaxScattering(i);
            _selectedTx.ClearPathLossCache();
            RefreshTransmitterFields();
            RefreshCommonFromTransform();
            RecomputeForTx();
        }

        private void OnRxTechChanged(int idx)
        {
            if (_selectedRx == null || rxTechDropdown == null) return;

            var tech = TechnologySpecifications.ParseTechnologyString(
                rxTechDropdown.options[idx].text
            );
            var spec = TechnologySpecifications.GetSpec(tech);

            _selectedRx.SetTechnology(tech);
            _selectedRx.sensitivity = spec.SensitivityDbm;
            _selectedRx.receiverHeight = spec.TypicalRxHeight;

            if (_selectedRx.GetConnectedTransmitter() != null)
                _selectedRx.GetConnectedTransmitter().ClearPathLossCache();

            RefreshReceiverFields();
            RecomputeForRx();
        }

        private void OnRxSensitivityEdited(string s)
        {
            if (_selectedRx == null) return;
            if (TryParseFloat(s, out float v))
                _selectedRx.sensitivity = (float)System.Math.Round(v, 2);

            if (_selectedRx.GetConnectedTransmitter() != null)
                _selectedRx.GetConnectedTransmitter().ClearPathLossCache();

            RefreshReceiverFields();
            RecomputeForRx();
        }

        private void OnRxConnectionMarginEdited(string s)
        {
            if (_selectedRx == null) return;
            if (TryParseFloat(s, out float v))
                _selectedRx.connectionMargin = (float)System.Math.Round(v, 2);

            if (_selectedRx.GetConnectedTransmitter() != null)
                _selectedRx.GetConnectedTransmitter().ClearPathLossCache();

            RefreshReceiverFields();
            RecomputeForRx();
        }

        private void OnRxHeightEdited(string s)
        {
            if (_selectedRx == null) return;
            if (!TryParseFloat(s, out float h)) return;

            float roundedH = (float)System.Math.Round(h, 2);
            var pos = _selectedRx.transform.position;
            float groundY = pos.y - _selectedRx.receiverHeight;
            _selectedRx.receiverHeight = roundedH;
            _selectedRx.transform.position = new Vector3(pos.x, groundY + roundedH, pos.z);

            if (_selectedRx.GetConnectedTransmitter() != null)
                _selectedRx.GetConnectedTransmitter().ClearPathLossCache();

            RefreshReceiverFields();
            RefreshCommonFromTransform();
            RecomputeForRx();
        }

        private void OnRemoveClicked()
        {
            if (_selectedTx != null)
            {
                var go = _selectedTx.gameObject;
                SimulationManager.Instance?.RemoveTransmitter(_selectedTx);
                _selectedTx = null;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                RecomputeAll(true);
                ClearSelection();
                return;
            }

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
            else ClearSelection();
        }

        private void RefreshCommonFromTransform()
        {
            var t = GetSelectedTransform();
            if (t == null) { ClearCommon(); return; }
            _isUpdatingUI = true;
            posXInput?.SetTextWithoutNotify(t.position.x.ToString("F2", Ci));
            posYInput?.SetTextWithoutNotify(t.position.y.ToString("F2", Ci));
            posZInput?.SetTextWithoutNotify(t.position.z.ToString("F2", Ci));
            _isUpdatingUI = false;
        }

        private void RefreshTransmitterFields()
        {
            if (_selectedTx == null) return;
            _isUpdatingUI = true;
            txPowerInput?.SetTextWithoutNotify(_selectedTx.settings.transmitterPower.ToString("F2", Ci));
            txFreqInput?.SetTextWithoutNotify(UnitConversionHelper.MHzToGHz(_selectedTx.settings.frequency).ToString("F2", Ci));
            txHeightInput?.SetTextWithoutNotify(_selectedTx.settings.transmitterHeight.ToString("F2", Ci));
            txMaxReflectionsInput?.SetTextWithoutNotify(_selectedTx.settings.maxReflections.ToString());
            txMaxDiffractionsInput?.SetTextWithoutNotify(_selectedTx.settings.maxDiffractions.ToString());
            txMaxScatteringInput?.SetTextWithoutNotify(_selectedTx.settings.maxScattering.ToString());

            if (txModelDropdown != null)
                txModelDropdown.SetValueWithoutNotify(IndexFromModel(_selectedTx.settings.propagationModel));

            if (txTechDropdown != null)
            {
                var spec = TechnologySpecifications.GetSpec(_selectedTx.settings.technology);
                var techName = spec.Name;

                var idx = txTechDropdown.options.FindIndex(o => o.text == techName);
                if (idx >= 0) txTechDropdown.SetValueWithoutNotify(idx);
            }

            if (txConnectedReceivers != null)
            {
                int count = _selectedTx.GetConnectedReceivers()?.Count ?? 0;
                txConnectedReceivers.text = count.ToString();
            }

            _isUpdatingUI = false;
        }

        private void RefreshReceiverFields()
        {
            if (_selectedRx == null) return;
            _isUpdatingUI = true;

            if (rxTechDropdown != null)
            {
                var spec = TechnologySpecifications.GetSpec(_selectedRx.technology);
                var techName = spec.Name;

                var idx = rxTechDropdown.options.FindIndex(o => o.text == techName);
                if (idx >= 0) rxTechDropdown.SetValueWithoutNotify(idx);
            }

            rxSensitivityInput?.SetTextWithoutNotify(_selectedRx.sensitivity.ToString("F2", Ci));
            rxHeightInput?.SetTextWithoutNotify(_selectedRx.receiverHeight.ToString("F2", Ci));
            rxConnectionMarginInput?.SetTextWithoutNotify(_selectedRx.connectionMargin.ToString("F2", Ci));

            if (rxSignalLabel != null)
                rxSignalLabel.text = _selectedRx.currentSignalStrength.ToString("F2", Ci);

            _isUpdatingUI = false;

            if (rxConnectedTransmitter != null && distanceToTransmitter != null)
            {
                var tx = _selectedRx.GetConnectedTransmitter();
                if (tx != null)
                {
                    rxConnectedTransmitter.text = tx.uniqueID;
                    distanceToTransmitter.text =
                        Vector3.Distance(tx.transform.position, _selectedRx.transform.position)
                            .ToString("F2", Ci) + " m";
                }
                else
                {
                    rxConnectedTransmitter.text = "—";
                    distanceToTransmitter.text = "—";
                }
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

        private static readonly string[] ModelNames =
            { "FreeSpace", "LogD", "LogDShadow", "Hata", "COST231", "RayTracing" };

        private static PropagationModel ModelFromIndex(int i)
        {
            switch (i)
            {
                case 0: return PropagationModel.FreeSpace;
                case 1: return PropagationModel.LogD;
                case 2: return PropagationModel.LogNShadow;
                case 3: return PropagationModel.Hata;
                case 4: return PropagationModel.COST231;
                case 5: return PropagationModel.RayTracing;
                default: return PropagationModel.RayTracing;
            }
        }

        private static int IndexFromModel(PropagationModel m)
        {
            switch (m)
            {
                case PropagationModel.FreeSpace: return 0;
                case PropagationModel.LogD: return 1;
                case PropagationModel.LogNShadow: return 2;
                case PropagationModel.Hata: return 3;
                case PropagationModel.COST231: return 4;
                case PropagationModel.RayTracing: return 5;
                default: return 5;
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

        private void RecomputeAll(bool heatmapUpdate) => SimulationManager.Instance?.RecomputeAllSignalStrength(heatmapUpdate);
        private void RecomputeForTx() { if (_selectedTx) SimulationManager.Instance?.RecomputeForTransmitter(_selectedTx); }
        private void RecomputeForRx() { if (_selectedRx) SimulationManager.Instance?.RecomputeForReceiver(_selectedRx); }

    }
}
