using Colossal.UI;
using Game;
using UnityEngine;

namespace skyplan.Systems {
	public partial class CameraSystem : GameSystemBase, ICameraSystem {
		private Matrix4x4 m_LastMatrix;
		private bool m_HasBaseline;

		public bool IsReady => m_HasBaseline && GetCamera() != null;

		protected override void OnCreate() {
			base.OnCreate();
		}

		protected override void OnUpdate() { }

		public void SetBaseline() {
			Camera cam = GetCamera();
			if (cam == null) return;
			m_LastMatrix = cam.worldToCameraMatrix;
			m_HasBaseline = true;
			Mod.log.Info($"CameraSystem.SetBaseline: cam={cam.name} px={cam.pixelWidth}x{cam.pixelHeight}");
		}

		public bool HasChanged() {
			if (!m_HasBaseline) return false;
			Camera cam = GetCamera();
			if (cam == null) return false;
			bool same = cam.worldToCameraMatrix == m_LastMatrix;
			if (!same) {
				m_LastMatrix = cam.worldToCameraMatrix;
				Mod.log.Info("CameraSystem.HasChanged: matrix changed");
			}
			return !same;
		}

		public Vector2 WorldToSVG(Vector3 world) {
			Camera cam = GetCamera();
			if (cam == null) return Vector2.zero;
			Vector3 s = cam.WorldToScreenPoint(world);
			return new Vector2(s.x, cam.pixelHeight - s.y);
		}

		public bool ScreenToWorldXZ(float sx, float sy, out Vector2 xz) {
			Camera cam = GetCamera();
			if (cam == null) { xz = Vector2.zero; return false; }
			Ray ray = cam.ScreenPointToRay(new Vector3(sx, cam.pixelHeight - sy, 0f));
			if (Mathf.Abs(ray.direction.y) < 0.0001f) { xz = Vector2.zero; return false; }
			float t = -ray.origin.y / ray.direction.y;
			if (t < 0f) { xz = Vector2.zero; return false; }
			Vector3 w = ray.origin + ray.direction * t;
			xz = new Vector2(w.x, w.z);
			return true;
		}

		private static Camera GetCamera() =>
			UIManager.defaultUISystem.defaultUIView.RenderingCamera ?? Camera.main;
	}
}
