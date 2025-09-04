////using System.Collections;
////using System.Collections.Generic;
////using NUnit.Framework;
////using UnityEngine;
////using UnityEngine.TestTools;
////using RFSimulation.Core;

//namespace RFSimulationTests
//{
////    public class TransmitterTests
////    {
////        private GameObject transmitterGameObject;
////        private Transmitter transmitter;

////        [SetUp]
////        public void SetUp()
////        {
////            // Create a test transmitter for each test
////            transmitterGameObject = new GameObject("TestTransmitter");
////            transmitter = transmitterGameObject.AddComponent<Transmitter>();
            
////            // Set default test values
////            transmitter.transmitterPower = 20f;     // 20 dBm
////            transmitter.frequency = 2400f;     // 2.4 GHz
////            transmitter.coverageRadius = 100f; // 100 meters
////            transmitter.antennaGain = 1.0f;
////        }

////        [TearDown]
////        public void TearDown()
////        {
////            // Clean up after each test
////            if (transmitterGameObject != null)
////                Object.DestroyImmediate(transmitterGameObject);
////        }

////        [Test]
////        public void CalculateWaveLength_Returns_Correct_Value()
////        {
////            // Arrange
////            transmitter.frequency = 2400f; // 2.4 GHz
////            float expectedWavelength = 0.125f; // c/f = 3e8/(2.4e9) = 0.125m
            
////            // Act
////            float actualWavelength = transmitter.CalculateWaveLength();
            
////            // Assert
////            Assert.AreEqual(expectedWavelength, actualWavelength, 0.001f, 
////                "Wavelength calculation should be accurate to 3 decimal places");
////        }

////        [Test]
////        public void CalculateSimpleFSPL_Returns_Expected_PathLoss()
////        {
////            // Arrange
////            transmitter.frequency = 2400f; // 2.4 GHz
////            float distance = 1000f; // 1 km
            
////            // Expected FSPL = 20*log10(1) + 20*log10(2400) + 32.45
////            // = 0 + 20*3.38 + 32.45 = 100.05 dB approximately
////            float expectedFSPL = 100.05f;
            
////            // Act
////            float actualFSPL = transmitter.CalculateSimpleFSPL(distance);
            
////            // Assert
////            Assert.AreEqual(expectedFSPL, actualFSPL, 1.0f, 
////                "FSPL calculation should be within 1 dB of expected value");
////        }

////        [Test]
////        public void CalculateReceivedPowerFSPL_Returns_Correct_Power()
////        {
////            // Arrange
////            transmitter.transmitterPower = 20f; // 20 dBm
////            transmitter.frequency = 2400f;
////            float distance = 1000f; // 1 km
            
////            // Expected: Tx Power - Path Loss
////            float expectedPathLoss = transmitter.CalculateSimpleFSPL(distance);
////            float expectedReceivedPower = 20f - expectedPathLoss;
            
////            // Act
////            float actualReceivedPower = transmitter.CalculateReceivedPowerFSPL(distance);
            
////            // Assert
////            Assert.AreEqual(expectedReceivedPower, actualReceivedPower, 0.1f,
////                "Received power should equal transmit power minus path loss");
////        }

////        [Test]
////        public void IsReceiverInRange_Returns_True_When_Within_Coverage()
////        {
////            // Arrange
////            transmitter.coverageRadius = 100f;
////            transmitterGameObject.transform.position = Vector3.zero;
////            Vector3 receiverPosition = new Vector3(50f, 0f, 0f); // 50m away
            
////            // Act
////            bool isInRange = transmitter.IsReceiverInRange(receiverPosition);
            
////            // Assert
////            Assert.IsTrue(isInRange, "Receiver within coverage radius should be in range");
////        }

////        [Test]
////        public void IsReceiverInRange_Returns_False_When_Outside_Coverage()
////        {
////            // Arrange
////            transmitter.coverageRadius = 100f;
////            transmitterGameObject.transform.position = Vector3.zero;
////            Vector3 receiverPosition = new Vector3(150f, 0f, 0f); // 150m away
            
////            // Act
////            bool isInRange = transmitter.IsReceiverInRange(receiverPosition);
            
////            // Assert
////            Assert.IsFalse(isInRange, "Receiver outside coverage radius should not be in range");
////        }

////        [Test]
////        public void CalculateSignalStrength_Returns_NegativeInfinity_When_OutOfRange()
////        {
////            // Arrange
////            transmitter.coverageRadius = 100f;
////            transmitterGameObject.transform.position = Vector3.zero;
////            Vector3 receiverPosition = new Vector3(200f, 0f, 0f); // Outside range
            
////            // Act
////            float signalStrength = transmitter.CalculateSignalStrength(receiverPosition);
            
////            // Assert
////            Assert.IsTrue(float.IsNegativeInfinity(signalStrength), 
////                "Signal strength should be negative infinity when receiver is out of range");
////        }

////        [Test]
////        [TestCase(100f, 2400f, 20f)] // 100m, 2.4GHz, 20dBm
////        [TestCase(500f, 5000f, 30f)] // 500m, 5GHz, 30dBm
////        [TestCase(50f, 900f, 10f)]   // 50m, 900MHz, 10dBm
////        public void CalculateSignalStrength_Returns_Valid_Values_For_Different_Parameters(
////            float distance, float frequency, float power)
////        {
////            // Arrange
////            transmitter.coverageRadius = distance + 100f; // Ensure in range
////            transmitter.frequency = frequency;
////            transmitter.transmitterPower = power;
////            transmitterGameObject.transform.position = Vector3.zero;
////            Vector3 receiverPosition = new Vector3(distance, 0f, 0f);
            
////            // Act
////            float signalStrength = transmitter.CalculateSignalStrength(receiverPosition);
            
////            // Assert
////            Assert.IsFalse(float.IsNegativeInfinity(signalStrength), 
////                "Signal strength should be a valid number");
////            Assert.IsTrue(signalStrength < power, 
////                "Received signal should be weaker than transmitted signal");
////            Assert.IsTrue(signalStrength > -200f, 
////                "Signal strength should be reasonable (> -200 dBm)");
////        }

////        [Test]
////        public void FSPL_Increases_With_Distance()
////        {
////            // Arrange
////            transmitter.frequency = 2400f;
////            float distance1 = 100f;
////            float distance2 = 200f;
            
////            // Act
////            float fspl1 = transmitter.CalculateSimpleFSPL(distance1);
////            float fspl2 = transmitter.CalculateSimpleFSPL(distance2);
            
////            // Assert
////            Assert.Greater(fspl2, fspl1, 
////                "Path loss should increase with distance");
            
////            // Verify 6dB rule: doubling distance increases FSPL by ~6dB
////            float expectedIncrease = 6f; // 20*log10(2) ≈ 6dB
////            float actualIncrease = fspl2 - fspl1;
////            Assert.AreEqual(expectedIncrease, actualIncrease, 0.1f,
////                "Doubling distance should increase FSPL by approximately 6dB");
////        }

////        [Test]
////        public void FSPL_Increases_With_Frequency()
////        {
////            // Arrange
////            float distance = 1000f;
////            transmitter.frequency = 2400f; // 2.4 GHz
////            float fspl1 = transmitter.CalculateSimpleFSPL(distance);
            
////            transmitter.frequency = 4800f; // 4.8 GHz (double)
////            float fspl2 = transmitter.CalculateSimpleFSPL(distance);
            
////            // Act & Assert
////            Assert.Greater(fspl2, fspl1, 
////                "Path loss should increase with frequency");
            
////            // Verify 6dB rule: doubling frequency increases FSPL by ~6dB
////            float expectedIncrease = 6f; // 20*log10(2) ≈ 6dB
////            float actualIncrease = fspl2 - fspl1;
////            Assert.AreEqual(expectedIncrease, actualIncrease, 0.1f,
////                "Doubling frequency should increase FSPL by approximately 6dB");
////        }
////    }
//}