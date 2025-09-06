using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using RFSimulation.Core;

namespace RFSimulation.Visualization
{
    /// <summary>
    /// Enhanced visualization for receivers with signal quality indicators and connection status
    /// </summary>
    public class ReceiverVisualizer : MonoBehaviour
    {
        [Header("Receiver Appearance")]
        public GameObject receiverModel;
        public Material receiverMaterial;
        public Color baseReceiverColor = Color.green;
        public float deviceSize = 1f;

        [Header("Signal Quality Visualization")]
        public bool showSignalBars = true;
        public bool showSignalSphere = true;
        public int signalBarCount = 5;
        public float signalBarSpacing = 0.3f;
        public float signalBarMaxHeight = 3f;

        [Header("Connection Visualization")]
        public bool showConnectionStatus = true;
        public GameObject connectionBeam;
        public LineRenderer connectionLine;
        public Color connectedColor = Color.green;
        public Color disconnectedColor = Color.red;
        public Color weakConnectionColor = Color.yellow;

        [Header("Technology Indicator")]
        public bool showTechnologyLabel = true;
        public Canvas worldSpaceCanvas;
        public Text technologyText;
        public Image qualityIndicator;
        public float labelHeight = 3f;

        [Header("Animation")]
        public bool animateSignalQuality = true;
        public float pulseSpeed = 2f;

        private Receiver receiver;
        private GameObject[] signalBars;
        private GameObject signalSphere;
        private Material sphereMaterial;
        private float animationTime = 0f;

        public void Initialize(Receiver parentReceiver)
        {
            receiver = parentReceiver;
            CreateReceiverModel();
            CreateSignalSphere();
            CreateTechnologyLabel();
            CreateConnectionVisualization();
        }

        private void CreateReceiverModel()
        {
            if (receiverModel == null)
                CreateDefaultReceiverModel();

            ApplyReceiverMaterial();
        }

        private void CreateDefaultReceiverModel()
        {
            // Create main device body
            receiverModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            receiverModel.name = "ReceiverDevice";
            receiverModel.transform.SetParent(transform);
            receiverModel.transform.localPosition = Vector3.up * (deviceSize / 2f);
            receiverModel.transform.localScale = Vector3.one * deviceSize;

            // Create small antenna
            GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antenna.name = "ReceiverAntenna";
            antenna.transform.SetParent(receiverModel.transform);
            antenna.transform.localPosition = Vector3.up * 0.8f;
            antenna.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
        }

        private void CreateSignalSphere()
        {
            if (!showSignalSphere) return;

            signalSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            signalSphere.name = "SignalSphere";
            signalSphere.transform.SetParent(transform);
            signalSphere.transform.localPosition = Vector3.up * deviceSize;
            signalSphere.transform.localScale = Vector3.one * 2f;

            // Use URP Lit shader with transparency
            sphereMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            sphereMaterial.SetFloat("_Surface", 1); // Transparent
            sphereMaterial.SetFloat("_Blend", 0);   // Alpha blend
            sphereMaterial.SetFloat("_ZWrite", 0);
            sphereMaterial.SetInt("_Cull", 2);
            sphereMaterial.renderQueue = 3000;

            Color initialColor = new Color(0f, 1f, 0f, 0.3f); // greenish transparent
            sphereMaterial.SetColor("_BaseColor", initialColor);

            signalSphere.GetComponent<Renderer>().material = sphereMaterial;

            DestroyImmediate(signalSphere.GetComponent<Collider>());
        }


        private void CreateTechnologyLabel()
        {
            if (!showTechnologyLabel) return;

            GameObject canvasObj = new GameObject("ReceiverLabel");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = Vector3.up * labelHeight;

            worldSpaceCanvas = canvasObj.AddComponent<Canvas>();
            worldSpaceCanvas.renderMode = RenderMode.WorldSpace;
            worldSpaceCanvas.worldCamera = Camera.main;

            RectTransform canvasRect = worldSpaceCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(150, 80);
            canvasRect.localScale = Vector3.one * 0.01f;

            // Technology text
            GameObject textObj = new GameObject("TechnologyText");
            textObj.transform.SetParent(canvasObj.transform, false);

            technologyText = textObj.AddComponent<Text>();
            technologyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            technologyText.text = receiver ? receiver.technology : "5G";
            technologyText.fontSize = 20;
            technologyText.color = Color.white;
            technologyText.alignment = TextAnchor.MiddleCenter;

            RectTransform textRect = technologyText.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(150, 40);
            textRect.anchoredPosition = Vector2.zero;

            // Quality indicator
            GameObject indicatorObj = new GameObject("QualityIndicator");
            indicatorObj.transform.SetParent(canvasObj.transform, false);

            qualityIndicator = indicatorObj.AddComponent<Image>();
            qualityIndicator.color = GetQualityColor();

            RectTransform indicatorRect = qualityIndicator.GetComponent<RectTransform>();
            indicatorRect.sizeDelta = new Vector2(100, 20);
            indicatorRect.anchoredPosition = new Vector2(0, -20);

            StartCoroutine(FaceCameraCoroutine());
        }

        private void CreateConnectionVisualization()
        {
            if (!showConnectionStatus) return;

            GameObject connectionObj = new GameObject("ConnectionVisualization");
            connectionObj.transform.SetParent(transform);

            connectionLine = connectionObj.AddComponent<LineRenderer>();
            connectionLine.material = new Material(Shader.Find("Sprites/Default"));
            connectionLine.startWidth = 0.1f;
            connectionLine.endWidth = 0.1f;
            connectionLine.positionCount = 2;
            connectionLine.enabled = false;
        }

        private void Update()
        {
            UpdateSignalVisualization();
            UpdateConnectionVisualization();
            UpdateLabels();

            if (animateSignalQuality)
            {
                animationTime += Time.deltaTime * pulseSpeed;
                AnimateSignalEffects();
            }
        }

        private void UpdateSignalVisualization()
        {
            if (receiver == null) return;

            float signalQuality = receiver.GetSignalQuality() / 100f; // 0-1 range

            // Update signal bars
            if (signalBars != null)
            {
                for (int i = 0; i < signalBars.Length; i++)
                {
                    if (signalBars[i] == null) continue;

                    float threshold = (float)(i + 1) / signalBars.Length;
                    bool shouldBeActive = signalQuality >= threshold;

                    Renderer renderer = signalBars[i].GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Color barColor = shouldBeActive ?
                            Color.Lerp(Color.red, Color.green, signalQuality) :
                            Color.gray;
                        renderer.material.color = barColor;
                    }
                }
            }

            // Update signal sphere
            if (signalSphere != null && sphereMaterial != null)
            {
                Color sphereColor = Color.Lerp(disconnectedColor, connectedColor, signalQuality);
                sphereColor.a = 0.3f + 0.4f * signalQuality;
                sphereMaterial.color = sphereColor;

                // Scale sphere based on signal strength
                float scale = 1f + signalQuality * 1.5f;
                signalSphere.transform.localScale = Vector3.one * scale;
            }
        }

        private void UpdateConnectionVisualization()
        {
            if (connectionLine == null || receiver == null) return;

            var connectedTx = receiver.GetConnectedTransmitter();

            if (connectedTx != null)
            {
                connectionLine.enabled = true;
                connectionLine.SetPosition(0, transform.position);
                connectionLine.SetPosition(1, connectedTx.transform.position);

                // Color based on signal quality
                float quality = receiver.GetSignalQuality() / 100f;
                Color lineColor = Color.Lerp(weakConnectionColor, connectedColor, quality);
                connectionLine.material.color = lineColor;
            }
            else
            {
                connectionLine.enabled = false;
            }
        }

        private void UpdateLabels()
        {
            if (technologyText != null && receiver != null)
            {
                technologyText.text = $"{receiver.technology}\n{receiver.GetSignalQuality():F0}%";
            }

            if (qualityIndicator != null)
            {
                qualityIndicator.color = GetQualityColor();
            }
        }

        private void AnimateSignalEffects()
        {
            if (signalSphere != null && receiver != null && receiver.IsConnected())
            {
                float pulse = 0.8f + 0.2f * Mathf.Sin(animationTime);
                Vector3 scale = signalSphere.transform.localScale;
                scale = scale.normalized * (2f * pulse);
                signalSphere.transform.localScale = scale;
            }
        }

        private Color GetQualityColor()
        {
            if (receiver == null) return Color.gray;

            float quality = receiver.GetSignalQuality();
            if (quality >= 80f) return Color.green;
            if (quality >= 60f) return Color.yellow;
            if (quality >= 40f) return new Color(1f, 0.5f, 0f);
            if (quality >= 20f) return Color.red;
            return Color.gray;
        }

        private void ApplyReceiverMaterial()
        {
            if (receiverMaterial == null)
            {
                receiverMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                receiverMaterial.SetColor("_BaseColor", baseReceiverColor);
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.name.Contains("SignalBar") || renderer.name.Contains("SignalSphere"))
                    continue;

                renderer.material = receiverMaterial;
            }
        }


        private IEnumerator FaceCameraCoroutine()
        {
            while (worldSpaceCanvas != null)
            {
                if (Camera.main != null)
                {
                    worldSpaceCanvas.transform.LookAt(Camera.main.transform);
                    worldSpaceCanvas.transform.Rotate(0, 180, 0);
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        public void SetVisibilityOptions(bool signalBars, bool signalSphere, bool connectionViz, bool labels)
        {
            showSignalBars = signalBars;
            showSignalSphere = signalSphere;
            showConnectionStatus = connectionViz;
            showTechnologyLabel = labels;

            if (signalBars && this.signalBars != null)
            {
                foreach (var bar in this.signalBars)
                {
                    if (bar != null) bar.SetActive(signalBars);
                }
            }

            if (this.signalSphere != null)
                this.signalSphere.SetActive(signalSphere);

            if (connectionLine != null)
                connectionLine.gameObject.SetActive(connectionViz);

            if (worldSpaceCanvas != null)
                worldSpaceCanvas.gameObject.SetActive(labels);
        }

        void OnDestroy()
        {
            if (sphereMaterial != null)
                DestroyImmediate(sphereMaterial);
        }
    }
}
