using Colossal.UI;
using Game;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace skyplan.Systems {
	public partial class DrawingSystem : GameSystemBase {
		public static DrawingSystem instance;
		private bool m_PanelVisible;
		private bool m_Injected;

		// Camera sync — Option A: SVG group transform
		// Shapes stored in baseline screen coords. Forward matrix moves them to current screen.
		// Inverse matrix converts incoming mouse coords back to baseline space.
		private bool      m_HasBaseline;
		private Vector2   m_BaseA, m_BaseB;   // baseline screen pos of world anchors
		private Vector3   m_LastCamPos;
		private Quaternion m_LastCamRot;
		private float     m_LastCamFOV;

		// World-space anchors, set dynamically at baseline from camera center ray
		// so they're always in front of and visible from the camera.
		private Vector3 m_AnchorA, m_AnchorB;

		protected override void OnCreate() {
			base.OnCreate();
			instance = this;
			Mod.log.Info("DrawingSystem.OnCreate");
		}

		protected override void OnUpdate() {
			if (Mod.m_ToggleAction != null && Mod.m_ToggleAction.WasPressedThisFrame())
				TogglePanel();
			if (m_PanelVisible)
				SyncCamera();
		}

		public void TogglePanel() {
			EnsureInjected();
			m_PanelVisible = !m_PanelVisible;
			if (m_PanelVisible) EstablishBaseline();
			UIManager.defaultUISystem.defaultUIView.View.TriggerEvent("skyplan.togglePanel", m_PanelVisible);
			Mod.log.Info($"Skyplan panel {(m_PanelVisible ? "shown" : "hidden")}");
		}

		// ── Camera helpers ────────────────────────────────────────────────────

		private static Camera GetCamera() {
			Camera cam = Camera.main;
			if (cam == null)
				cam = UIManager.defaultUISystem.defaultUIView.RenderingCamera;
			return cam;
		}

		// Unity screen: (0,0)=bottom-left, Y up. SVG: (0,0)=top-left, Y down.
		private static Vector2 WorldToSVG(Camera cam, Vector3 world) {
			Vector3 s = cam.WorldToScreenPoint(world);
			return new Vector2(s.x, cam.pixelHeight - s.y);
		}

		// Project a screen point onto the Y=0 world plane. Returns false if ray
		// is parallel to the plane or points away from it.
		private static bool ScreenToWorldY0(Camera cam, Vector2 screen, out Vector3 world) {
			Ray ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
			if (Mathf.Abs(ray.direction.y) < 0.0001f) { world = Vector3.zero; return false; }
			float t = -ray.origin.y / ray.direction.y;
			if (t < 0f) { world = Vector3.zero; return false; }
			world = ray.origin + ray.direction * t;
			return true;
		}

		private void EstablishBaseline() {
			Camera cam = GetCamera();
			if (cam == null) { Mod.log.Warn("No camera for baseline"); return; }

			// Anchors: screen-center and screen 75%-right projected to Y=0
			// This keeps them always in front of and visible from the camera.
			float cx = cam.pixelWidth  * 0.5f;
			float cy = cam.pixelHeight * 0.5f;
			float rx = cam.pixelWidth  * 0.75f;

			if (!ScreenToWorldY0(cam, new Vector2(cx, cy), out m_AnchorA) ||
				!ScreenToWorldY0(cam, new Vector2(rx, cy), out m_AnchorB)) {
				Mod.log.Warn("Could not project anchors to Y=0 — camera may be too low");
				return;
			}

			m_BaseA        = WorldToSVG(cam, m_AnchorA);
			m_BaseB        = WorldToSVG(cam, m_AnchorB);
			m_LastCamPos   = cam.transform.position;
			m_LastCamRot   = cam.transform.rotation;
			m_LastCamFOV   = cam.fieldOfView;
			m_HasBaseline  = true;

			const string identity = "matrix(1,0,0,1,0,0)";
			UIManager.defaultUISystem.defaultUIView.View
				.TriggerEvent("skyplan.cameraTransform", identity, identity);

			Mod.log.Info($"Baseline set. A={m_AnchorA} B={m_AnchorB}");
		}

		private void SyncCamera() {
			if (!m_HasBaseline) return;
			Camera cam = GetCamera();
			if (cam == null) return;

			if (cam.transform.position == m_LastCamPos &&
				cam.transform.rotation == m_LastCamRot &&
				cam.fieldOfView        == m_LastCamFOV) return;

			Mod.log.Info($"Camera moved → pos={cam.transform.position} fov={cam.fieldOfView}");
			m_LastCamPos = cam.transform.position;
			m_LastCamRot = cam.transform.rotation;
			m_LastCamFOV = cam.fieldOfView;

			// Skip if either anchor is behind the camera (z<0 → garbage screen coords)
			Vector3 sA = cam.WorldToScreenPoint(m_AnchorA);
			Vector3 sB = cam.WorldToScreenPoint(m_AnchorB);
			if (sA.z <= 0f || sB.z <= 0f) return;

			Vector2 curA = new Vector2(sA.x, cam.pixelHeight - sA.y);
			Vector2 curB = new Vector2(sB.x, cam.pixelHeight - sB.y);
			Vector2 dBase = m_BaseB - m_BaseA;
			Vector2 dCur  = curB   - curA;

			float lenBase = dBase.magnitude;
			if (lenBase < 0.001f) return;

			float scale    = dCur.magnitude / lenBase;
			float angle    = Mathf.Atan2(dCur.y, dCur.x) - Mathf.Atan2(dBase.y, dBase.x);
			float cosS     = Mathf.Cos(angle) * scale;   // a/d in SVG matrix
			float sinS     = Mathf.Sin(angle) * scale;   // b/-c in SVG matrix

			// Forward: baseline → current
			// SVG matrix(a,b,c,d,e,f): x'=a*x+c*y+e, y'=b*x+d*y+f
			// x' = cosS*x - sinS*y + tx
			// y' = sinS*x + cosS*y + ty
			float tx  = curA.x - (cosS * m_BaseA.x - sinS * m_BaseA.y);
			float ty  = curA.y - (sinS * m_BaseA.x + cosS * m_BaseA.y);
			string fwd = Mat(cosS, sinS, -sinS, cosS, tx, ty);

			// Inverse: current → baseline
			// 2×2 det = cosS²+sinS² = scale²
			float s2  = scale * scale;
			string inv = Mat(
				 cosS / s2,                              // ia
				-sinS / s2,                              // ib
				 sinS / s2,                              // ic
				 cosS / s2,                              // id
				(-cosS * tx - sinS * ty) / s2,           // ie
				( sinS * tx - cosS * ty) / s2            // if
			);

			UIManager.defaultUISystem.defaultUIView.View
				.TriggerEvent("skyplan.cameraTransform", fwd, inv);
		}

		private static string F(float v) =>
			v.ToString("F6", CultureInfo.InvariantCulture);

		private static string Mat(float a, float b, float c, float d, float e, float f) =>
			$"matrix({F(a)},{F(b)},{F(c)},{F(d)},{F(e)},{F(f)})";

		// ── Injection ─────────────────────────────────────────────────────────

		private void EnsureInjected() {
			if (m_Injected) return;
			if (string.IsNullOrEmpty(Mod.modPath)) {
				Mod.log.Warn("modPath not set — cannot inject UI");
				return;
			}
			string jsPath = Path.Combine(Mod.modPath, "UI", "app.js");
			if (!File.Exists(jsPath)) {
				Mod.log.Warn($"app.js not found at {jsPath}");
				return;
			}
			var view = UIManager.defaultUISystem.defaultUIView.View;
			view.RegisterForEvent("skyplan.debug", (System.Action<string>)(msg => Mod.log.Info($"[JS] {msg}")));
			view.ExecuteScript(File.ReadAllText(jsPath));
			m_Injected = true;
			Mod.log.Info("Skyplan UI injected");
		}
	}
}
