using UnityEngine;
using UnityEngine.UI;
using RFSimulation.Core;

namespace RFSimulation.UI
{
    public class ObjectClickHandler : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject infoPopup;
        public Text titleText;
        public Text contentText;
        public Button closeButton;

        [Header("Settings")]
        public LayerMask clickableLayerMask = -1;
        public KeyCode closeKey = KeyCode.Escape;
        public float updateInterval = 0.5f;

        private Camera mainCamera;
        private bool isPopupOpen = false;
        private Transmitter currentTransmitter = null;
        private Receiver currentReceiver = null;
        private float lastUpdateTime = 0f;

        void Start()
        {
            mainCamera = Camera.main;

            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePopup);

            if (infoPopup != null)
                infoPopup.SetActive(false);
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0) && !isPopupOpen)
            {
                HandleMouseClick();
            }

            if (Input.GetKeyDown(closeKey) && isPopupOpen)
            {
                ClosePopup();
            }

            if (isPopupOpen && Time.time - lastUpdateTime >= updateInterval)
            {
                UpdatePopupContent();
                lastUpdateTime = Time.time;
            }
        }

        private void HandleMouseClick()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickableLayerMask))
            {
                Transmitter transmitter = hit.collider.GetComponent<Transmitter>();
                if (transmitter != null)
                {
                    ShowTransmitterInfo(transmitter);
                    return;
                }

                Receiver receiver = hit.collider.GetComponent<Receiver>();
                if (receiver != null)
                {
                    ShowReceiverInfo(receiver);
                    return;
                }
            }
        }

        private void ShowTransmitterInfo(Transmitter transmitter)
        {
            currentTransmitter = transmitter;
            currentReceiver = null;

            string title = $"Transmitter: {transmitter.uniqueID}";
            string content = GetTransmitterInfoStatic(transmitter);

            ShowPopup(title, content);
        }

        private void ShowReceiverInfo(Receiver receiver)
        {
            currentReceiver = receiver;
            currentTransmitter = null;

            string title = $"Receiver: {receiver.uniqueID}";
            string content = GetReceiverInfoStatic(receiver);

            ShowPopup(title, content);
        }

        private void UpdatePopupContent()
        {
            if (!isPopupOpen) return;

            if (currentTransmitter != null)
            {
                if (currentTransmitter == null)
                {
                    ClosePopup();
                    return;
                }

                string content = GetTransmitterInfoStatic(currentTransmitter);
                if (contentText != null) contentText.text = content;
            }
            else if (currentReceiver != null)
            {
                if (currentReceiver == null)
                {
                    ClosePopup();
                    return;
                }

                string content = GetReceiverInfoStatic(currentReceiver);
                if (contentText != null) contentText.text = content;
            }
        }

        private string GetTransmitterInfoStatic(Transmitter tx)
        {
            var info = new System.Text.StringBuilder();

            // Static structure - labels never change, only values update
            info.AppendLine($"Position: {tx.transform.position:F1}");
            info.AppendLine($"Power: {tx.transmitterPower:F1} dBm");
            info.AppendLine($"Frequency: {tx.frequency:F0} MHz");
            info.AppendLine($"Antenna Gain: {tx.antennaGain:F1} dBi");
            info.AppendLine($"Model: {tx.propagationModel}");
            info.AppendLine($"Environment: {tx.environmentType}");
            info.AppendLine();
            info.AppendLine($"Active Connections: {tx.GetConnectionCount()}");

            // Calculate coverage once (since it's static)
            var context = RFSimulation.Propagation.Core.PropagationContext.Create(
                tx.position, tx.position + Vector3.forward, tx.transmitterPower, tx.frequency);
            context.AntennaGainDbi = tx.antennaGain;
            context.ReceiverSensitivityDbm = -105f;

            var calculator = new RFSimulation.Propagation.PathLoss.PathLossCalculator();
            float coverage = calculator.EstimateCoverageRadius(context);
            info.AppendLine($"Coverage Radius: {coverage:F0} m");

            return info.ToString();
        }

        private string GetReceiverInfoStatic(Receiver rx)
        {
            var info = new System.Text.StringBuilder();

            // Static structure - same labels every time, only values change
            info.AppendLine($"Position: {rx.transform.position:F1}");
            info.AppendLine($"Technology: {rx.technology}");
            info.AppendLine($"Sensitivity: {rx.sensitivity:F1} dBm");
            info.AppendLine($"Connection Margin: {rx.connectionMargin:F1} dB");
            info.AppendLine();

            // Connection status - structured consistently
            string status = rx.IsConnected() ? "CONNECTED" : "DISCONNECTED";
            info.AppendLine($"Status: {status}");

            // Always show these fields with consistent formatting
            string signalValue = rx.IsConnected() ? $"{rx.currentSignalStrength:F1} dBm" : "N/A";
            info.AppendLine($"Signal Strength: {signalValue}");

            string sinrValue = (rx.IsConnected() && !float.IsNegativeInfinity(rx.currentSINR)) ?
                $"{rx.currentSINR:F1} dB" : "N/A";
            info.AppendLine($"SINR: {sinrValue}");

            string connectedTo = "None";
            string distance = "N/A";
            if (rx.IsConnected())
            {
                var connectedTx = rx.GetConnectedTransmitter();
                if (connectedTx != null)
                {
                    connectedTo = connectedTx.uniqueID;
                    distance = $"{Vector3.Distance(rx.transform.position, connectedTx.transform.position):F0} m";
                }
            }
            info.AppendLine($"Connected To: {connectedTo}");
            info.AppendLine($"Distance: {distance}");

            info.AppendLine();

            // Performance metrics - always shown with consistent structure
            string quality = rx.IsConnected() ? $"{rx.GetSignalQuality():F0}%" : "0%";
            string throughput = rx.IsConnected() ? $"{rx.GetExpectedThroughput():F1} Mbps" : "0.0 Mbps";
            string reliability = rx.IsConnected() ? $"{rx.GetConnectionReliability():P0}" : "0%";

            info.AppendLine($"Quality: {quality}");
            info.AppendLine($"Throughput: {throughput}");
            info.AppendLine($"Reliability: {reliability}");

            return info.ToString();
        }

        private void ShowPopup(string title, string content)
        {
            if (titleText != null) titleText.text = title;
            if (contentText != null) contentText.text = content;

            if (infoPopup != null)
            {
                infoPopup.SetActive(true);
                isPopupOpen = true;
                lastUpdateTime = Time.time;
            }
        }

        public void ClosePopup()
        {
            if (infoPopup != null)
            {
                infoPopup.SetActive(false);
                isPopupOpen = false;
            }

            currentTransmitter = null;
            currentReceiver = null;
        }
    }
}