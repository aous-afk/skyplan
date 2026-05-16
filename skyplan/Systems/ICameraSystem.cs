using UnityEngine;

namespace skyplan.Systems {
	public interface ICameraSystem {
		bool IsReady { get; }
		void SetBaseline();
		bool HasChanged();
		Vector2 WorldToSVG(Vector3 world);
		bool ScreenToWorldXZ(float sx, float sy, out Vector2 xz);
	}
}
