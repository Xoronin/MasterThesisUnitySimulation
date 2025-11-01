// RFSimulation/Visualization/RayVisualization.cs
using System.Collections.Generic;
using UnityEngine;

namespace RFSimulation.Visualization
{
	/// <summary>
	/// Centralized, pooled ray visualization (single + multi-segment).
	/// Call BeginFrame() before drawing a new solution, EndFrame() after.
	/// </summary>
	public class RayVisualization : MonoBehaviour
	{
		[Header("Hierarchy")]
		[Tooltip("Parent for all line objects. If empty, a child will be created.")]
		[SerializeField] private Transform root;

		[Header("Appearance")]
		[SerializeField] private Material lineMaterial;      // Optional; will create a default if null
		[SerializeField] private float lineWidth = 0.02f;

		[Header("Lifetime")]
		[Tooltip("If true, lines remain enabled; otherwise, EndFrame disables unused ones.")]
		[SerializeField] private bool persistent = false;

		// Pool
		private readonly List<LineRenderer> pool = new();
		private int used;

		// Optional labels/metadata holder per line (if you ever want to show tooltips)
		private readonly Dictionary<LineRenderer, string> labels = new();

		void Awake()
		{
			if (root == null)
			{
				var go = new GameObject("RF_Ray_Visualizations");
				go.transform.SetParent(transform, false);
				root = go.transform;
			}

			if (lineMaterial == null)
			{
				var shader = Shader.Find("Sprites/Default");
				lineMaterial = new Material(shader);
			}
		}

		/// <summary>Call this before you render a new set of rays.</summary>
		public void BeginFrame()
		{
			used = 0;
		}

		/// <summary>Call this after you rendered all rays this frame. Disables leftovers.</summary>
		public void EndFrame()
		{
			// disable the unused tail
			for (int i = used; i < pool.Count; i++)
				pool[i].enabled = persistent && pool[i].enabled; // keep or hide depending on 'persistent'
		}

		/// <summary>Remove ALL lines (hard clear).</summary>
		public void ClearAll()
		{
			for (int i = 0; i < pool.Count; i++)
			{
				if (pool[i] != null)
					Destroy(pool[i].gameObject);
			}
			pool.Clear();
			labels.Clear();
			used = 0;
		}

		/// <summary>Draw a single straight segment.</summary>
		public void DrawSegment(Vector3 a, Vector3 b, Color color, string label = null)
		{
			var lr = Rent();
			Setup(lr, 2, color);
			lr.SetPosition(0, a);
			lr.SetPosition(1, b);
			if (label != null) labels[lr] = label;
		}

		/// <summary>Draw a polyline (multi-segment ray: reflections, diffractions, etc.).</summary>
		public void DrawPolyline(IReadOnlyList<Vector3> points, Color color, string label = null)
		{
			if (points == null || points.Count < 2) return;

			// One renderer for the whole chain is enough
			var lr = Rent();
			Setup(lr, points.Count, color);
			for (int i = 0; i < points.Count; i++)
				lr.SetPosition(i, points[i]);

			if (label != null) labels[lr] = label;
		}

		private LineRenderer Rent()
		{
			if (used < pool.Count)
			{
				var lr = pool[used++];
				lr.enabled = true;
				return lr;
			}

			var go = new GameObject("RaySeg");
			go.transform.SetParent(root, false);

			var lrNew = go.AddComponent<LineRenderer>();
			pool.Add(lrNew);
			used++;
			return lrNew;
		}

		private void Setup(LineRenderer lr, int positionCount, Color color)
		{
			lr.material = lineMaterial;
			lr.widthMultiplier = lineWidth;
			lr.alignment = LineAlignment.View;
			lr.numCornerVertices = 4;
			lr.numCapVertices = 4;
			lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			lr.receiveShadows = false;
			lr.useWorldSpace = true;
			lr.positionCount = positionCount;

			// Important: set material color (works with Sprites/Default)
			lr.material.color = color;
			lr.startColor = lr.endColor = color;
		}
	}
}
