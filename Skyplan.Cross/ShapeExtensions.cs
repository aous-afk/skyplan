using Skyplan.Models;
using UnityEngine;

namespace Skyplan.Cross {
	public static class ShapeExtensions {
		// Shared by DrawingSystem.CreateDto (screen-space rendering deltas) and the snap seam below
		// (world-space snap targets) - single source of truth for the perpendicular-normal offset
		// math, world-space here, in lane order (matching how DrawingSystem flattens
		// Shape.ParallelLanes: the primary's own extra copies first, then each further queued
		// layer's lanes). Lines (Tools.path, exactly 2 points) only.
		// Yields the full ParallelLane (not just its LayerId) so callers that need per-lane metadata
		// (Label/Description - see DrawingSystem.CreateDto) don't have to re-look it up by id.
		public static IEnumerable<(ParallelLane Lane, Vector3 Offset)> GetParallelLaneOffsets(this Shape s) {
			if (s.Type != Tools.path || s.pts.Count != 2 || s.ParallelLanes.Count == 0) yield break;
			Vector3 dir = s.pts[1] - s.pts[0];
			dir.y = 0f;
			if (dir.sqrMagnitude < 1e-8f) yield break;
			Vector3 normal = new Vector3(-dir.z, 0f, dir.x).normalized;
			int laneIndex = 1;
			foreach (ParallelLane lane in s.ParallelLanes) {
				for (int i = 0; i < lane.Count; i++) {
					yield return (lane, normal * s.ParallelSpacing * laneIndex);
					laneIndex++;
				}
			}
		}

		public static IReadOnlyList<Vector3> GetSnapVertices(this Shape s) {
			if (s.Type != Tools.path || s.ParallelLanes.Count == 0) return s.pts;
			List<Vector3> result = [.. s.pts];
			foreach ((_, Vector3 offset) in s.GetParallelLaneOffsets()) {
				result.Add(s.pts[0] + offset);
				result.Add(s.pts[1] + offset);
			}
			return result;
		}

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

			foreach ((_, Vector3 offset) in s.GetParallelLaneOffsets())
				yield return (s.pts[0] + offset, s.pts[1] + offset);
		}
	}
}
