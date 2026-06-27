using UnityEngine;

namespace skyplan.Systems {
	public interface ICameraSystem {
		#region Core
		bool IsReady { get; }
		bool HasChanged();
		Vector2 WorldToSVG(Vector3 world);
		bool ScreenToWorld(float sx, float sy, out Vector3 world);
		#endregion

		#region CSS Transform / Baseline
		void SetBaseline();
		Vector2 WorldToSVGBaseline(Vector3 world);
		string ComputeTransformMatrix();
		#endregion
	}
}
