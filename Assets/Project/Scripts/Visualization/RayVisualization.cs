using System.Collections.Generic;
using UnityEngine;

namespace RFSimulation.Visualization
{

	public class RayVisualization : MonoBehaviour
	{
		[Header("Hierarchy")]
		public Transform root;

		[Header("Appearance")]
        public Material lineMaterial;
        public float lineWidth = 0.02f;

		[Header("Lifetime")]
        public bool persistent = false;

		private readonly List<LineRenderer> pool = new();
		private int used;

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

		public void BeginFrame()
		{
			used = 0;
		}

		public void EndFrame()
		{
			for (int i = used; i < pool.Count; i++)
				pool[i].enabled = persistent && pool[i].enabled; 
		}

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

		public void DrawSegment(Vector3 a, Vector3 b, Color color, string label = null)
		{
			var lr = Rent();
			Setup(lr, 2, color);
			lr.SetPosition(0, a);
			lr.SetPosition(1, b);
			if (label != null) labels[lr] = label;
		}

		public void DrawPolyline(IReadOnlyList<Vector3> points, Color color, string label = null)
		{
			if (points == null || points.Count < 2) return;

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

			lr.material.color = color;
			lr.startColor = lr.endColor = color;
		}
	}
}
