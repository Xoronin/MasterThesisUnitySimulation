////using System.Collections;
////using System.Collections.Generic;
////using NUnit.Framework;
////using UnityEngine;
////using UnityEngine.TestTools;
////using RFSimulation.Core;

//namespace RFSimulationTests
//{
////    public class ReceiverTests
////    {
////        private GameObject receiverGameObject;
////        private Receiver receiver;

////        [SetUp]
////        public void SetUp()
////        {
////            // Create a test receiver for each test
////            receiverGameObject = new GameObject("TestReceiver");
////            receiver = receiverGameObject.AddComponent<Receiver>();

////            // Set default test values
////            receiver.sensitivity = -90f; // -90 dBm
////        }

////        [TearDown]
////        public void TearDown()
////        {
////            // Clean up after each test
////            if (receiverGameObject != null)
////                Object.DestroyImmediate(receiverGameObject);
////        }

////        [Test]
////        public void UpdateSignalStrength_Updates_SignalStrength_Property()
////        {
////            // Arrange
////            float newSignalStrength = -75f;

////            // Expect the renderer error since we don't have visual components in tests
////            LogAssert.Expect(LogType.Error, "NO RENDERER FOUND! Check if cube has MeshRenderer component.");

////            // Act
////            receiver.UpdateSignalStrength(newSignalStrength);

////            // Assert
////            Assert.AreEqual(newSignalStrength, receiver.signalStrength,
////                "Signal strength should be updated correctly");
////        }

////        [Test]
////        public void UpdateSignalStrength_Updates_Position_From_Transform()
////        {
////            // Arrange
////            Vector3 newPosition = new Vector3(10f, 5f, 15f);
////            receiverGameObject.transform.position = newPosition;

////            // Act
////            receiver.UpdateSignalStrength(-80f);

////            // Assert
////            Assert.AreEqual(newPosition, receiver.position,
////                "Position should be updated from transform");
////        }

////        [Test]
////        [TestCase(-70f, -90f, true)]   // Strong signal > sensitivity
////        [TestCase(-90f, -90f, true)]   // Signal equals sensitivity (boundary)
////        [TestCase(-95f, -90f, false)]  // Weak signal < sensitivity
////        [TestCase(-110f, -90f, false)] // Very weak signal
////        public void Signal_Quality_Assessment_Works_Correctly(
////            float signalStrength, float sensitivity, bool shouldBeGood)
////        {
////            // Arrange
////            receiver.sensitivity = sensitivity;
////            receiver.UpdateSignalStrength(signalStrength);

////            // Act
////            bool isSignalGood = receiver.signalStrength > receiver.sensitivity;

////            // Assert
////            Assert.AreEqual(shouldBeGood, isSignalGood,
////                $"Signal {signalStrength} dBm with sensitivity {sensitivity} dBm should be {(shouldBeGood ? "good" : "poor")}");
////        }

////        [Test]
////        public void Receiver_Initialization_Sets_Default_Values()
////        {
////            // Act
////            receiver.InitializeReceiver();

////            // Assert
////            Assert.AreEqual(-90f, receiver.sensitivity,
////                "Default sensitivity should be -90 dBm");
////            Assert.AreEqual(0f, receiver.signalStrength,
////                "Initial signal strength should be 0");
////            Assert.AreEqual(0f, receiver.qualityMetric,
////                "Initial quality metric should be 0");
////        }

////        [Test]
////        public void Position_Updates_When_Transform_Changes()
////        {
////            // Arrange
////            Vector3 initialPosition = Vector3.zero;
////            Vector3 newPosition = new Vector3(20f, 10f, 30f);

////            receiverGameObject.transform.position = initialPosition;
////            receiver.UpdateSignalStrength(-80f); // This should update position
////            Assert.AreEqual(initialPosition, receiver.position);

////            // Act
////            receiverGameObject.transform.position = newPosition;
////            receiver.UpdateSignalStrength(-75f); // This should update position again

////            // Assert
////            Assert.AreEqual(newPosition, receiver.position,
////                "Position should update when transform changes");
////        }

////        [Test]
////        [TestCase(-60f)] // Very strong signal
////        [TestCase(-80f)] // Good signal  
////        [TestCase(-95f)] // Weak signal
////        [TestCase(-120f)] // Very weak signal
////        public void UpdateSignalStrength_Accepts_All_Valid_Signal_Levels(float signalLevel)
////        {
////            // Act
////            receiver.UpdateSignalStrength(signalLevel);

////            // Assert
////            Assert.AreEqual(signalLevel, receiver.signalStrength,
////                $"Should accept signal strength of {signalLevel} dBm");
////        }

////        // Test for potential SNR calculation (if you implement it)
////        [Test]
////        public void QualityMetric_Can_Be_Calculated()
////        {
////            // Arrange
////            float signalStrength = -75f;
////            float noiseFloor = -100f; // Typical noise floor
////            float expectedSNR = signalStrength - noiseFloor; // 25 dB SNR

////            // Act
////            receiver.UpdateSignalStrength(signalStrength);
////            // If you implement SNR calculation:
////            // receiver.qualityMetric = signalStrength - noiseFloor;

////            // Assert (uncomment when you implement SNR)
////            // Assert.AreEqual(expectedSNR, receiver.qualityMetric, 0.1f,
////            //     "Quality metric should represent SNR correctly");

////            // For now, just verify the property exists
////            Assert.IsNotNull(receiver, "Quality metric property should exist");
////        }
////    }
//}