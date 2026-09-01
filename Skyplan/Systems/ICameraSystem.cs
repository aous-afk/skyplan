using UnityEngine;

namespace Skyplan.Systems {
	public interface ICameraSystem {
		#region Core
		bool IsReady { get; }
		bool HasChanged();
		Vector2 WorldToSVG(Vector3 world);
		bool WorldToSVG(Vector3 world, out Vector2 svg);
		bool ScreenToWorld(float sx, float sy, out Vector3 world);
		bool IsInView(Vector3 min, Vector3 max);
		bool IsInView(Bounds extents);
		#endregion
	}
}
