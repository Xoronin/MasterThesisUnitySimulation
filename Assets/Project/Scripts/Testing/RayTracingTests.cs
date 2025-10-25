//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;
//using RFSimulation.Propagation.Core;
//using RFSimulation.Propagation.PathLoss.Models;
//using RFSimulation.Environment;

//namespace RFSimulation.Testing
//{
//	/// <summary>
//	/// Specialized validator for ray tracing models
//	/// Tests against simpler models and physical expectations
//	/// </summary>
//	public class RayTracingValidator : MonoBehaviour
//	{
//		[Header("Ray Tracing Models")]
//		public BasicRayTracingModel basicRayTracing;
//		public AdvancedRayTracingModel advancedRayTracing;

//		[Header("Reference Models")]
//		public FreeSpaceModel freeSpaceModel;
//		public LogDistanceModel logDistanceModel;

//		[Header("Test Configuration")]
//		public LayerMask buildingLayers = 1 << 8;
//		public int numTestPoints = 50;
//		public float maxTestRadius = 1000f;
//		public bool enableVisualization = true;

//		[Header("Physical Validation")]
//		public float losToleranceDb = 3f;
//		public float nlosMinLossDb = 10f;
//		public float maxReasonableSignalDbm = 50f;
//		public float minReasonableSignalDbm = -150f;

//		private List<RayTracingTestResult> testResults = new List<RayTracingTestResult>();

//		void Start()
//		{
//			InitializeModels();
//		}

//		private void InitializeModels()
//		{
//			if (basicRayTracing == null)
//				basicRayTracing = new BasicRayTracingModel();

//			if (freeSpaceModel == null)
//				freeSpaceModel = new FreeSpaceModel();

//			if (logDistanceModel == null)
//				logDistanceModel = new LogDistanceModel();
//		}

//		[ContextMenu("Validate Ray Tracing Models")]
//		public void ValidateRayTracingModels()
//		{
//			Debug.Log("🔬 Starting Ray Tracing Model Validation");

//			testResults.Clear();

//			// Phase 1: LOS validation (should match free space closely)
//			ValidateLineOfSightAccuracy();

//			// Phase 2: NLOS validation (should show appropriate losses)
//			ValidateNonLineOfSightBehavior();

//			// Phase 3: Reflection validation
//			ValidateReflectionBehavior();

//			// Phase 4: Physical consistency checks
//			ValidatePhysicalConsistency();

//			// Phase 5: Performance validation
//			ValidatePerformance();

//			GenerateRayTracingReport();
//		}

//		#region LOS Validation

//		private void ValidateLineOfSightAccuracy()
//		{
//			Debug.Log("👁️ Validating Line-of-Sight accuracy");

//			var losTestPoints = GenerateLOSTestPoints(20);

//			foreach (var testPoint in losTestPoints)
//			{
//				var context = CreateTestContext(Vector3.zero, testPoint, 2400f);

//				// Calculate with different models
//				float freeSpaceResult = freeSpaceModel.Calculate(context);
//				float rayTracingResult = basicRayTracing.Calculate(context);

//				float difference = Mathf.Abs(rayTracingResult - freeSpaceResult);
//				bool isAccurate = difference <= losToleranceDb;

//				var result = new RayTracingTestResult
//				{
//					testType = "LOS_Accuracy",
//					position = testPoint,
//					hasLOS = true,
//					freeSpaceResult = freeSpaceResult,
//					rayTracingResult = rayTracingResult,
//					difference = difference,
//					isValid = isAccurate
//				};

//				testResults.Add(result);

//				string status = isAccurate ? "✅" : "❌";
//				Debug.Log($"{status} LOS @ {Vector3.Distance(Vector3.zero, testPoint):F0}m: " +
//						 $"FS={freeSpaceResult:F1}dBm, RT={rayTracingResult:F1}dBm, Δ={difference:F1}dB");
//			}
//		}

//		private List<Vector3> GenerateLOSTestPoints(int count)
//		{
//			var points = new List<Vector3>();

//			for (int i = 0; i < count; i++)
//			{
//				// Generate points in open areas (no buildings blocking)
//				Vector3 candidate;
//				bool hasLOS;
//				int attempts = 0;

//				do
//				{
//					float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
//					float distance = Random.Range(50f, maxTestRadius);
//					candidate = new Vector3(
//						Mathf.Cos(angle) * distance,
//						1.5f, // Receiver height
//						Mathf.Sin(angle) * distance
//					);

//					hasLOS = !Physics.Raycast(Vector3.zero, candidate.normalized,
//						Vector3.Distance(Vector3.zero, candidate), buildingLayers);

//					attempts++;
//				} while (!hasLOS && attempts < 50);

//				if (hasLOS)
//					points.Add(candidate);
//			}

//			return points;
//		}

//		#endregion

//		#region NLOS Validation

//		private void ValidateNonLineOfSightBehavior()
//		{
//			Debug.Log("🚫 Validating Non-Line-of-Sight behavior");

//			var nlosTestPoints = GenerateNLOSTestPoints(20);

//			foreach (var testPoint in nlosTestPoints)
//			{
//				var context = CreateTestContext(Vector3.zero, testPoint, 2400f);

//				float freeSpaceResult = freeSpaceModel.Calculate(context);
//				float rayTracingResult = basicRayTracing.Calculate(context);

//				// NLOS should have additional loss compared to free space
//				float additionalLoss = freeSpaceResult - rayTracingResult;
//				bool hasAppropriateNLOSLoss = additionalLoss >= nlosMinLossDb;

//				var result = new RayTracingTestResult
//				{
//					testType = "NLOS_Behavior",
//					position = testPoint,
//					hasLOS = false,
//					freeSpaceResult = freeSpaceResult,
//					rayTracingResult = rayTracingResult,
//					difference = additionalLoss,
//					isValid = hasAppropriateNLOSLoss
//				};

//				testResults.Add(result);

//				string status = hasAppropriateNLOSLoss ? "✅" : "❌";
//				Debug.Log($"{status} NLOS @ {Vector3.Distance(Vector3.zero, testPoint):F0}m: " +
//						 $"Additional Loss={additionalLoss:F1}dB (min expected: {nlosMinLossDb}dB)");
//			}
//		}

//		private List<Vector3> GenerateNLOSTestPoints(int count)
//		{
//			var points = new List<Vector3>();

//			for (int i = 0; i < count; i++)
//			{
//				Vector3 candidate;
//				bool hasLOS;
//				int attempts = 0;

//				do
//				{
//					float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
//					float distance = Random.Range(100f, maxTestRadius);
//					candidate = new Vector3(
//						Mathf.Cos(angle) * distance,
//						1.5f,
//						Mathf.Sin(angle) * distance
//					);

//					hasLOS = !Physics.Raycast(Vector3.zero, candidate.normalized,
//						Vector3.Distance(Vector3.zero, candidate), buildingLayers);

//					attempts++;
//				} while (hasLOS && attempts < 50);

//				if (!hasLOS)
//					points.Add(candidate);
//			}

//			return points;
//		}

//		#endregion

//		#region Reflection Validation

//		private void ValidateReflectionBehavior()
//		{
//			Debug.Log("🔄 Validating reflection behavior");

//			// Test specific scenarios where reflections should be significant
//			var reflectionTests = new[]
//			{
//				new ReflectionTest("Street_Canyon", Vector3.zero, new Vector3(200f, 1.5f, 50f)),
//				new ReflectionTest("Building_Corner", Vector3.zero, new Vector3(100f, 1.5f, 100f)),
//				new ReflectionTest("Rooftop_Reflection", new Vector3(0f, 30f, 0f), new Vector3(300f, 1.5f, 0f))
//			};

//			foreach (var test in reflectionTests)
//			{
//				ValidateReflectionTest(test);
//			}
//		}

//		private void ValidateReflectionTest(ReflectionTest test)
//		{
//			var context = CreateTestContext(test.txPosition, test.rxPosition, 2400f);

//			// Compare with and without reflections (if possible)
//			float rayTracingResult = basicRayTracing.Calculate(context);
//			float logDistanceResult = logDistanceModel.Calculate(context);

//			// Ray tracing should provide more accurate results in reflection scenarios
//			bool isReasonable = IsResultPhysicallyReasonable(rayTracingResult);

//			var result = new RayTracingTestResult
//			{
//				testType = "Reflection_" + test.name,
//				position = test.rxPosition,
//				hasLOS = CheckLOS(test.txPosition, test.rxPosition),
//				rayTracingResult = rayTracingResult,
//				difference = Mathf.Abs(rayTracingResult - logDistanceResult),
//				isValid = isReasonable
//			};

//			testResults.Add(result);

//			string status = isReasonable ? "✅" : "❌";
//			Debug.Log($"{status} {test.name}: RT={rayTracingResult:F1}dBm, " +
//					 $"LogDist={logDistanceResult:F1}dBm");
//		}

//		#endregion

//		#region Physical Consistency

//		private void ValidatePhysicalConsistency()
//		{
//			Debug.Log("⚖️ Validating physical consistency");

//			ValidateEnergyConservation();
//			ValidateMonotonicBehavior();
//			ValidateReasonableRanges();
//		}

//		private void ValidateEnergyConservation()
//		{
//			Debug.Log("🔋 Testing energy conservation");

//			// Received power should never exceed transmitted power + gains
//			var testPoints = GenerateRandomTestPoints(30);

//			foreach (var point in testPoints)
//			{
//				var context = CreateTestContext(Vector3.zero, point, 2400f);
//				float result = basicRayTracing.Calculate(context);

//				float maxPossiblePower = context.TransmitterPowerDbm + context.AntennaGainDbi;
//				bool conservesEnergy = result <= maxPossiblePower + 1f; // 1dB tolerance

//				if (!conservesEnergy)
//				{
//					Debug.LogWarning($"❌ Energy not conserved! RX={result:F1}dBm > TX+Gain={maxPossiblePower:F1}dBm");
//				}

//				testResults.Add(new RayTracingTestResult
//				{
//					testType = "Energy_Conservation",
//					position = point,
//					rayTracingResult = result,
//					isValid = conservesEnergy
//				});
//			}
//		}

//		private void ValidateMonotonicBehavior()
//		{
//			Debug.Log("📈 Testing monotonic distance behavior");

//			// Test along a clear LOS path
//			var distances = new float[] { 50f, 100f, 200f, 500f, 1000f };
//			var results = new List<float>();

//			foreach (float distance in distances)
//			{
//				var context = CreateTestContext(Vector3.zero, Vector3.forward * distance, 2400f);
//				results.Add(basicRayTracing.Calculate(context));
//			}

//			// Check that signal decreases with distance (allowing some tolerance for reflections)
//			bool isMonotonic = true;
//			for (int i = 1; i < results.Count; i++)
//			{
//				if (results[i] > results[i - 1] + 3f) // 3dB tolerance for reflections
//				{
//					isMonotonic = false;
//					Debug.LogWarning($"❌ Non-monotonic behavior: {distances[i - 1]}m={results[i - 1]:F1}dBm, " +
//								   $"{distances[i]}m={results[i]:F1}dBm");
//				}
//			}

//			testResults.Add(new RayTracingTestResult
//			{
//				testType = "Monotonic_Behavior",
//				isValid = isMonotonic
//			});

//			string status = isMonotonic ? "✅" : "❌";
//			Debug.Log($"{status} Distance monotonicity check");
//		}

//		private void ValidateReasonableRanges()
//		{
//			Debug.Log("🎯 Testing reasonable result ranges");

//			var testPoints = GenerateRandomTestPoints(50);
//			int reasonableCount = 0;

//			foreach (var point in testPoints)
//			{
//				var context = CreateTestContext(Vector3.zero, point, 2400f);
//				float result = basicRayTracing.Calculate(context);

//				bool isReasonable = IsResultPhysicallyReasonable(result);
//				if (isReasonable) reasonableCount++;

//				if (!isReasonable)
//				{
//					Debug.LogWarning($"❌ Unreasonable result: {result:F1}dBm at {point}");
//				}
//			}

//			float reasonablePercentage = (reasonableCount / (float)testPoints.Count) * 100f;
//			bool overallReasonable = reasonablePercentage >= 90f;

//			Debug.Log($"{(overallReasonable ? "✅" : "❌")} Reasonable results: {reasonablePercentage:F1}%");

//			testResults.Add(new RayTracingTestResult
//			{
//				testType = "Reasonable_Ranges",
//				difference = reasonablePercentage,
//				isValid = overallReasonable
//			});
//		}

//		#endregion

//		#region Performance Validation

//		private void ValidatePerformance()
//		{
//			Debug.Log("⚡ Validating performance");

//			int numTestCalculations = 100;
//			var testPoints = GenerateRandomTestPoints(numTestCalculations);

//			// Measure ray tracing performance
//			var stopwatch = System.Diagnostics.Stopwatch.StartNew();

//			foreach (var point in testPoints)
//			{
//				var context = CreateTestContext(Vector3.zero, point, 2400f);
//				basicRayTracing.Calculate(context);
//			}

//			stopwatch.Stop();

//			float avgTimeMs = stopwatch.ElapsedMilliseconds / (float)numTestCalculations;
//			bool isPerformant = avgTimeMs < 10f; // Should be under 10ms per calculation

//			Debug.Log($"{(isPerformant ? "✅" : "❌")} Performance: {avgTimeMs:F2}ms average per calculation");

//			testResults.Add(new RayTracingTestResult
//			{
//				testType = "Performance",
//				difference = avgTimeMs,
//				isValid = isPerformant
//			});
//		}

//		#endregion

//		#region Helper Methods

//		private PropagationContext CreateTestContext(Vector3 txPos, Vector3 rxPos, float frequency)
//		{
//			var context = PropagationContext.Create(txPos, rxPos, 43f, frequency);
//			context.AntennaGainDbi = 18f;
//			context.BuildingLayers = buildingLayers;
//			return context;
//		}

//		private bool CheckLOS(Vector3 from, Vector3 to)
//		{
//			Vector3 direction = to - from;
//			return !Physics.Raycast(from, direction.normalized, direction.magnitude, buildingLayers);
//		}

//		private bool IsResultPhysicallyReasonable(float result)
//		{
//			return !float.IsInfinity(result) &&
//				   !float.IsNaN(result) &&
//				   result >= minReasonableSignalDbm &&
//				   result <= maxReasonableSignalDbm;
//		}

//		private List<Vector3> GenerateRandomTestPoints(int count)
//		{
//			var points = new List<Vector3>();

//			for (int i = 0; i < count; i++)
//			{
//				float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
//				float distance = Random.Range(50f, maxTestRadius);
//				points.Add(new Vector3(
//					Mathf.Cos(angle) * distance,
//					Random.Range(1f, 3f), // Receiver height variation
//					Mathf.Sin(angle) * distance
//				));
//			}

//			return points;
//		}

//		#endregion

//		#region Reporting

//		private void GenerateRayTracingReport()
//		{
//			Debug.Log("📊 Generating Ray Tracing Validation Report");

//			var report = new System.Text.StringBuilder();
//			report.AppendLine("🔬 RAY TRACING VALIDATION REPORT");
//			report.AppendLine("==================================");
//			report.AppendLine($"Generated: {System.DateTime.Now}");
//			report.AppendLine($"Total Tests: {testResults.Count}");
//			report.AppendLine();

//			// Group results by test type
//			var groupedResults = testResults.GroupBy(r => r.testType);

//			foreach (var group in groupedResults)
//			{
//				var results = group.ToList();
//				int passed = results.Count(r => r.isValid);
//				float passRate = (passed / (float)results.Count) * 100f;

//				report.AppendLine($"📋 {group.Key}:");
//				report.AppendLine($"   Pass Rate: {passed}/{results.Count} ({passRate:F1}%)");

//				if (group.Key.Contains("LOS_Accuracy"))
//				{
//					float avgError = results.Where(r => r.isValid).Average(r => r.difference);
//					report.AppendLine($"   Average Error: {avgError:F2}dB");
//				}
//				else if (group.Key.Contains("NLOS"))
//				{
//					float avgLoss = results.Where(r => r.isValid).Average(r => r.difference);
//					report.AppendLine($"   Average Additional Loss: {avgLoss:F2}dB");
//				}

//				report.AppendLine();
//			}

//			// Overall assessment
//			int totalPassed = testResults.Count(r => r.isValid);
//			float overallPassRate = (totalPassed / (float)testResults.Count) * 100f;

//			report.AppendLine($"🎯 OVERALL ASSESSMENT:");
//			report.AppendLine($"   Pass Rate: {totalPassed}/{testResults.Count} ({overallPassRate:F1}%)");

//			if (overallPassRate >= 90f)
//				report.AppendLine("   Status: ✅ EXCELLENT - Ray tracing models are working correctly");
//			else if (overallPassRate >= 75f)
//				report.AppendLine("   Status: ⚠️ GOOD - Minor issues detected, review failed tests");
//			else if (overallPassRate >= 60f)
//				report.AppendLine("   Status: ❌ POOR - Significant issues detected, requires investigation");
//			else
//				report.AppendLine("   Status: 🔥 CRITICAL - Major problems, models may be broken");

//			string reportText = report.ToString();
//			Debug.Log(reportText);

//			// Save to file
//			string filePath = System.IO.Path.Combine(Application.dataPath, "RayTracingValidationReport.txt");
//			System.IO.File.WriteAllText(filePath, reportText);
//			Debug.Log($"📄 Report saved to: {filePath}");
//		}

//		#endregion

//		#region Data Structures

//		[System.Serializable]
//		public struct ReflectionTest
//		{
//			public string name;
//			public Vector3 txPosition;
//			public Vector3 rxPosition;

//			public ReflectionTest(string name, Vector3 txPosition, Vector3 rxPosition)
//			{
//				this.name = name;
//				this.txPosition = txPosition;
//				this.rxPosition = rxPosition;
//			}
//		}

//		public struct RayTracingTestResult
//		{
//			public string testType;
//			public Vector3 position;
//			public bool hasLOS;
//			public float freeSpaceResult;
//			public float rayTracingResult;
//			public float difference;
//			public bool isValid;
//		}

//		#endregion

//		#region Visualization

//		void OnDrawGizmos()
//		{
//			if (!enableVisualization || testResults == null) return;

//			// Visualize test results
//			foreach (var result in testResults.Take(50)) // Limit for performance
//			{
//				if (result.position == Vector3.zero) continue;

//				// Color code based on result validity
//				Gizmos.color = result.isValid ? Color.green : Color.red;

//				// Different shapes for LOS vs NLOS
//				if (result.hasLOS)
//				{
//					Gizmos.DrawSphere(result.position, 2f);
//				}
//				else
//				{
//					Gizmos.DrawCube(result.position, Vector3.one * 3f);
//				}

//				// Draw line to show test path
//				Gizmos.color = Color.white;
//				Gizmos.DrawLine(Vector3.zero, result.position);
//			}
//		}

//		#endregion
//	}
//}