using System.Collections.Generic;
using UnityEngine;

namespace Skyplan.Cross {
	/// <summary>
	/// Distance is XZ-only elsewhere in the codebase (see SnapQuery), but curve control math needs
	/// full 3D positions so sampled points keep the shape's actual terrain height.
	/// </summary>
	public static class CurveMath {
		private static Vector3 EvaluateQuadratic(Vector3 p0, Vector3 c, Vector3 p2, float t) {
			float u = 1f - t;
			return (u * u * p0) + (2f * u * t * c) + (t * t * p2);
		}

		/// <summary>
		/// Projects a raw click into a control point that continues the previous segment's exit
		/// tangent smoothly - keeps the click's distance from the shared joint, forces the
		/// direction. First segment has no previous tangent to continue, so its control is unprojected.
		/// </summary>
		public static Vector3 ProjectControl(Vector3 rawClick, Vector3 joint, Vector3? prevControl) {
			if (prevControl is not Vector3 prev) return rawClick;
			Vector3 dir = joint - prev;
			if (dir.sqrMagnitude < 1e-8f) return rawClick;
			dir.Normalize();
			float dist = Vector3.Distance(rawClick, joint);
			return joint + (dir * dist);
		}

		/// <summary>
		/// Chained quadratic beziers, one control per segment: anchors are true on-curve points,
		/// each interior click is a permanently-locked control (once the model swap it belongs to
		/// commits). A segment with no matching control (mid-draw preview, or fewer than 2 anchors)
		/// falls back to a straight line.
		/// </summary>
		public static List<Vector3> Sample(IReadOnlyList<Vector3> pts, IReadOnlyList<Vector3> handles, int stepsPerSegment = 16) {
			List<Vector3> result = new();
			if (pts.Count == 0) return result;
			result.Add(pts[0]);
			for (int i = 0; i < pts.Count - 1; i++) {
				if (handles != null && i < handles.Count) {
					for (int s = 1; s <= stepsPerSegment; s++) {
						float t = (float)s / stepsPerSegment;
						result.Add(EvaluateQuadratic(pts[i], handles[i], pts[i + 1], t));
					}
				} else {
					result.Add(pts[i + 1]);
				}
			}
			return result;
		}

		/// <summary>
		/// Offsets an already-sampled polyline by a constant distance, perpendicular to the curve's
		/// own local direction at each point (central difference of its neighbors, forward/backward
		/// difference at the two endpoints) - not a single rigid translate. A uniform translate is
		/// only exact for a straight line; a curve's true parallel offset varies direction point by
		/// point along the bend, so each sampled point needs its own normal. XZ-plane only (same
		/// convention as the straight-line perpendicular offset in ShapeExtensions.GetParallelLaneOffsets),
		/// height carried over from the source point unchanged.
		/// </summary>
		public static List<Vector3> OffsetPolyline(IReadOnlyList<Vector3> sampled, float distance) {
			List<Vector3> result = new(sampled.Count);
			for (int i = 0; i < sampled.Count; i++) {
				Vector3 prev = sampled[i > 0 ? i - 1 : i];
				Vector3 next = sampled[i < sampled.Count - 1 ? i + 1 : i];
				Vector3 dir = next - prev;
				dir.y = 0f;
				Vector3 normal = dir.sqrMagnitude < 1e-8f ? Vector3.zero : new Vector3(-dir.z, 0f, dir.x).normalized;
				result.Add(sampled[i] + (normal * distance));
			}
			return result;
		}
	}
}
