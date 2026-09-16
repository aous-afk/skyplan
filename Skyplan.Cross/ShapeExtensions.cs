using Skyplan.Models;
using UnityEngine;

namespace Skyplan.Cross {
	public static class ShapeExtensions {
		// Perpendicular-normal offset for a 2-point line, in queue order - shared by
		// DrawingSystem.CreateDto (screen-space preview deltas) and HandleDrawEnd (baking real
		// per-lane geometry at commit time).
		public static IEnumerable<(ParallelLane Lane, Vector3 Offset)> GetParallelLaneOffsets(this Shape s) {
			if (s.Type != Tools.path || s.pts.Count != 2 || s.ParallelLanes.Count == 0) yield break;
			Vector3 dir = s.pts[1] - s.pts[0];
			dir.y = 0f;
			if (dir.sqrMagnitude < 1e-8f) yield break;
			Vector3 normal = new Vector3(-dir.z, 0f, dir.x).normalized;
			int laneIndex = 1;
			foreach (ParallelLane lane in s.ParallelLanes) {
				yield return (lane, normal * s.ParallelSpacing * laneIndex);
				laneIndex++;
			}
		}

		public static IReadOnlyList<Vector3> GetSnapVertices(this Shape s) => s.pts;

		public static IEnumerable<(Vector3 a, Vector3 b)> GetSnapSegments(this Shape s) {
			if (s.Type == Tools.curve) {
				List<Vector3> sampled = CurveMath.Sample(s.pts, s.handles);
				for (int i = 0; i < sampled.Count - 1; i++)
					yield return (sampled[i], sampled[i + 1]);
				yield break;
			}
			for (int i = 0; i < s.pts.Count - 1; i++)
				yield return (s.pts[i], s.pts[i + 1]);
			if (s.Type == Tools.polygon && s.pts.Count > 2)
				yield return (s.pts[s.pts.Count - 1], s.pts[0]);
		}
	}
}
