using System.Collections.Generic;
using Skyplan.Models;
using Skyplan.Models.Results;
using UnityEngine;

namespace Skyplan.Cross {
	/// <summary>
	/// Distance is XZ-only: screen-pixel tolerance is a 2D concept, mixing in per-shape terrain
	/// height (Y) would make the snap radius inconsistent on slopes.
	/// </summary>
	public static class SnapQuery {
		public static bool TrySnap(IReadOnlyList<Shape> shapes, Shape skip, Vector3 world, float toleranceWorld, out SnapHit snapped) {
			float tolSq = toleranceWorld * toleranceWorld;

			snapped.Point = world;
			snapped.Shape = null;
			snapped.VertexIndex = -1;

			// Pass 1: vertices - a vertex hit always wins over an edge hit inside tolerance.
			float bestSq = tolSq;
			bool found = false;
			foreach (Shape shape in shapes) {
				if (shape == skip) continue;
				IReadOnlyList<Vector3> verts = shape.GetSnapVertices();
				for (int i = 0; i < verts.Count; i++) {
					float d = SqXZDistance(verts[i], world);
					if (d < bestSq) {
					  bestSq = d;
					  snapped.Point = verts[i];
					  snapped.Shape = shape;
					  snapped.VertexIndex = i;
					  found = true;
					}
				}
			}
			if (found) return true;

			// Pass 2: edges - only runs when no vertex was inside tolerance.
			bestSq = tolSq;
			foreach (Shape shape in shapes) {
				if (shape == skip) continue;
				foreach ((Vector3 a, Vector3 b) in shape.GetSnapSegments()) {
					Vector3 candidate = ClosestPointOnSegment(a, b, world);
					float d = SqXZDistance(candidate, world);
					if (d < bestSq) {
					  bestSq = d;
					  snapped.Point = candidate;
					  snapped.Shape = shape;
					  found = true;
					}
				}
			}
			return found;
		}

		private static float SqXZDistance(Vector3 a, Vector3 b) {
			float dx = a.x - b.x, dz = a.z - b.z;
			return (dx * dx) + (dz * dz);
		}

		private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p) {
			float abx = b.x - a.x, abz = b.z - a.z;
			float lenSq = (abx * abx) + (abz * abz);
			if (lenSq < 1e-8f) return a;
			float t = (((p.x - a.x) * abx) + ((p.z - a.z) * abz)) / lenSq;
			t = Mathf.Clamp01(t);
			return new Vector3(a.x + (abx * t), a.y + ((b.y - a.y) * t), a.z + (abz * t));
		}
	}
}
