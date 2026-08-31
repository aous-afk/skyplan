using Skyplan.Models;
using UnityEngine;

namespace Skyplan.Cross {
	public static class GeometryUtils {
		public static void CalcBounds(this Shape shape) {
			Vector3 min = shape.pts[0], max = shape.pts[0];
			foreach (var p in shape.pts)
			{
				min = Vector3.Min(min, p);
				max = Vector3.Max(max, p);
			}
			min.y -= 500f; max.y += 500f; // ignore height, terrain can be tall
			shape.Extents.SetMinMax(min, max);
		}
	}
}
