using Colossal.UI;
using Game;
using Game.SceneFlow;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace skyplan.Systems {
	public partial class DrawingSystem : GameSystemBase {
		public static DrawingSystem instance;
		private bool m_PanelVisible;
		private bool m_Injected;

		// CS2 sets worldToCameraMatrix directly; transform.position never changes.
		private bool      m_HasBaseline;
		private Matrix4x4 m_LastViewMatrix;

		private class Shape {
			public string        id;
			public string        type;   // line | rect | circle | free | erase
			public string        layer;  // roads | zoning | transit | notes
			public List<Vector2> pts = new List<Vector2>(); // world XZ (Y=0)
		}

		// ── Undo stack ────────────────────────────────────────────────────────
		private enum OpType { Draw, Delete, ClearLayer }
		private class Op {
			public OpType      type;
			public Shape       shape;    // Draw | Delete
			public string      layer;   // ClearLayer
			public List<Shape> cleared; // ClearLayer — shapes removed
		}

		private readonly List<Shape> m_Shapes    = new List<Shape>();
		private readonly List<Op> m_UndoStack = new List<Op>(); // used as stack: Add=push, last=top
		private Shape  m_ActiveShape;
		private string m_CurrentTool  = "line";
		private string m_CurrentLayer = "roads";
		private int    m_NextId;
		private string m_EraseTarget; // ID of shape under cursor when erase tool active

		protected override void OnCreate() {
			base.OnCreate();
			instance = this;
			Mod.log.Info("DrawingSystem.OnCreate");
		}

		protected override void OnUpdate() {
			// GameMode is [Flags] — bitwise check, not equality
			bool inGame = GameManager.instance != null &&
				(GameManager.instance.gameMode & GameMode.Game) != 0;
			if (inGame && Mod.m_ToggleAction != null && Mod.m_ToggleAction.WasPressedThisFrame())
				TogglePanel();
			if (m_PanelVisible) {
				if (!inGame) {
					m_PanelVisible = false;
					UIManager.defaultUISystem.defaultUIView.View.TriggerEvent("skyplan.togglePanel", false);
				} else {
					SyncCamera();
				}
			}
		}

		public void TogglePanel() {
			EnsureInjected();
			m_PanelVisible = !m_PanelVisible;
			if (m_PanelVisible) {
				Camera cam = GetCamera();
				if (cam != null) {
					m_LastViewMatrix = cam.worldToCameraMatrix;
					m_HasBaseline = true;
					SendShapesUpdate(cam);
				}
			}
			UIManager.defaultUISystem.defaultUIView.View.TriggerEvent("skyplan.togglePanel", m_PanelVisible);
			Mod.log.Info($"Skyplan panel {(m_PanelVisible ? "shown" : "hidden")}");
		}

		// ── Camera ────────────────────────────────────────────────────────────

		private static Camera GetCamera() =>
			UIManager.defaultUISystem.defaultUIView.RenderingCamera ?? Camera.main;

		private static Vector2 WorldToSVG(Camera cam, Vector3 world) {
			Vector3 s = cam.WorldToScreenPoint(world);
			return new Vector2(s.x, cam.pixelHeight - s.y);
		}

		private static bool ScreenToWorldXZ(Camera cam, float sx, float sy, out Vector2 xz) {
			Ray ray = cam.ScreenPointToRay(new Vector3(sx, cam.pixelHeight - sy, 0f));
			if (Mathf.Abs(ray.direction.y) < 0.0001f) { xz = Vector2.zero; return false; }
			float t = -ray.origin.y / ray.direction.y;
			if (t < 0f) { xz = Vector2.zero; return false; }
			Vector3 w = ray.origin + ray.direction * t;
			xz = new Vector2(w.x, w.z);
			return true;
		}

		private void SyncCamera() {
			if (!m_HasBaseline) return;
			Camera cam = GetCamera();
			if (cam == null) return;
			if (cam.worldToCameraMatrix == m_LastViewMatrix) return;
			m_LastViewMatrix = cam.worldToCameraMatrix;
			SendShapesUpdate(cam);
		}

		// ── Drawing handlers ──────────────────────────────────────────────────

		private void HandleDrawStart(float sx, float sy) {
			Camera cam = GetCamera();
			if (cam == null) return;

			if (m_CurrentTool == "erase") {
				EraseNearest(cam);
				return;
			}

			if (!ScreenToWorldXZ(cam, sx, sy, out Vector2 xz)) return;
			m_ActiveShape = new Shape {
				id    = $"s{m_NextId++}",
				type  = m_CurrentTool,
				layer = m_CurrentLayer,
			};
			m_ActiveShape.pts.Add(xz);
		}

		private void HandleDrawMove(float sx, float sy) {
			if (m_ActiveShape == null) return;
			Camera cam = GetCamera();
			if (cam == null || !ScreenToWorldXZ(cam, sx, sy, out Vector2 xz)) return;

			if (m_ActiveShape.type == "free") {
				if (m_ActiveShape.pts.Count == 0 ||
					Vector2.Distance(m_ActiveShape.pts[m_ActiveShape.pts.Count - 1], xz) > 5f)
					m_ActiveShape.pts.Add(xz);
			} else {
				if (m_ActiveShape.pts.Count > 1) m_ActiveShape.pts[1] = xz;
				else m_ActiveShape.pts.Add(xz);
			}
			SendPreviewUpdate(cam);
		}

		private void HandleDrawEnd(float sx, float sy) {
			if (m_ActiveShape == null) return;
			HandleDrawMove(sx, sy);
			Camera cam = GetCamera();
			if (m_ActiveShape.pts.Count >= 2) {
				m_Shapes.Add(m_ActiveShape);
				m_UndoStack.Add(new Op { type = OpType.Draw, shape = m_ActiveShape });
				if (cam != null) SendShapesUpdate(cam);
			}
			m_ActiveShape = null;
			UIManager.defaultUISystem.defaultUIView.View.TriggerEvent("skyplan.previewUpdate", "");
		}

		private void HandleClearLayer(string layer) {
			var removed = m_Shapes.FindAll(s => s.layer == layer);
			if (removed.Count > 0)
				m_UndoStack.Add(new Op { type = OpType.ClearLayer, layer = layer, cleared = removed });
			m_Shapes.RemoveAll(s => s.layer == layer);
			if (m_ActiveShape != null && m_ActiveShape.layer == layer)
				m_ActiveShape = null;
			var view = UIManager.defaultUISystem.defaultUIView.View;
			Camera cam = GetCamera();
			if (cam != null) SendShapesUpdate(cam);
			view.TriggerEvent("skyplan.previewUpdate", "");
		}

		private void HandleUndo() {
			if (m_UndoStack.Count == 0) return;
			Op op = m_UndoStack[m_UndoStack.Count - 1];
			m_UndoStack.RemoveAt(m_UndoStack.Count - 1);
			switch (op.type) {
				case OpType.Draw:
					m_Shapes.Remove(op.shape);
					break;
				case OpType.Delete:
					m_Shapes.Add(op.shape);
					break;
				case OpType.ClearLayer:
					m_Shapes.AddRange(op.cleared);
					break;
			}
			Camera cam = GetCamera();
			if (cam != null) SendShapesUpdate(cam);
		}

		// ── Erase tool — proximity hover + click-to-delete ───────────────────

		private static Vector2 ShapeScreenCentroid(Camera cam, Shape s) {
			Vector2 sum = Vector2.zero;
			foreach (var p in s.pts)
				sum += WorldToSVG(cam, new Vector3(p.x, 0, p.y));
			return sum / s.pts.Count;
		}

		private void HandleEraseHover(float sx, float sy) {
			Camera cam = GetCamera();
			if (cam == null) return;
			const float Threshold = 80f;
			Vector2 cursor = new Vector2(sx, sy);
			float  best   = float.MaxValue;
			string found  = null;
			foreach (var s in m_Shapes) {
				float d = Vector2.Distance(ShapeScreenCentroid(cam, s), cursor);
				if (d < best) { best = d; found = s.id; }
			}
			string newTarget = (found != null && best <= Threshold) ? found : null;
			if (newTarget == m_EraseTarget) return; // no change
			m_EraseTarget = newTarget;
			UIManager.defaultUISystem.defaultUIView.View
				.TriggerEvent("skyplan.highlightShape", m_EraseTarget ?? "");
		}

		private void EraseNearest(Camera cam) {
			if (m_EraseTarget == null) return;
			Shape target = m_Shapes.Find(s => s.id == m_EraseTarget);
			if (target == null) return;
			m_UndoStack.Add(new Op { type = OpType.Delete, shape = target });
			m_Shapes.Remove(target);
			m_EraseTarget = null;
			UIManager.defaultUISystem.defaultUIView.View.TriggerEvent("skyplan.highlightShape", "");
			SendShapesUpdate(cam);
		}

		// ── SVG / JSON generation ─────────────────────────────────────────────

		private static string LayerColor(string layer) {
			switch (layer) {
				case "roads":   return "#ff4444";
				case "zoning":  return "#44dd44";
				case "transit": return "#4488ff";
				case "notes":   return "#ffcc00";
				default:        return "#ffffff";
			}
		}

		private string ShapeToJSON(Camera cam, Shape s) {
			string c = LayerColor(s.layer);
			var sb = new StringBuilder();
			sb.Append($"{{\"id\":\"{s.id}\",\"layer\":\"{s.layer}\"");

			switch (s.type) {
				case "line": {
					if (s.pts.Count < 2) return null;
					Vector2 p0 = WorldToSVG(cam, new Vector3(s.pts[0].x, 0, s.pts[0].y));
					Vector2 p1 = WorldToSVG(cam, new Vector3(s.pts[1].x, 0, s.pts[1].y));
					sb.Append(",\"tag\":\"line\"");
					sb.Append($",\"x1\":\"{F(p0.x)}\",\"y1\":\"{F(p0.y)}\"");
					sb.Append($",\"x2\":\"{F(p1.x)}\",\"y2\":\"{F(p1.y)}\"");
					sb.Append($",\"stroke\":\"{c}\",\"stroke-width\":\"4\",\"stroke-linecap\":\"round\",\"fill\":\"none\"");
					break;
				}
				case "rect": {
					if (s.pts.Count < 2) return null;
					Vector2 a  = WorldToSVG(cam, new Vector3(s.pts[0].x, 0, s.pts[0].y));
					Vector2 b  = WorldToSVG(cam, new Vector3(s.pts[1].x, 0, s.pts[0].y));
					Vector2 c2 = WorldToSVG(cam, new Vector3(s.pts[1].x, 0, s.pts[1].y));
					Vector2 d  = WorldToSVG(cam, new Vector3(s.pts[0].x, 0, s.pts[1].y));
					sb.Append(",\"tag\":\"polygon\"");
					sb.Append($",\"points\":\"{F(a.x)},{F(a.y)} {F(b.x)},{F(b.y)} {F(c2.x)},{F(c2.y)} {F(d.x)},{F(d.y)}\"");
					sb.Append($",\"stroke\":\"{c}\",\"stroke-width\":\"4\",\"fill\":\"none\"");
					break;
				}
				case "circle": {
					if (s.pts.Count < 2) return null;
					float wcx = (s.pts[0].x + s.pts[1].x) * 0.5f;
					float wcz = (s.pts[0].y + s.pts[1].y) * 0.5f;
					float wrx = Mathf.Abs(s.pts[1].x - s.pts[0].x) * 0.5f;
					float wrz = Mathf.Abs(s.pts[1].y - s.pts[0].y) * 0.5f;
					const int N = 32;
					var cPts = new StringBuilder();
					for (int i = 0; i < N; i++) {
						float ang = 2f * Mathf.PI * i / N;
						Vector2 sp = WorldToSVG(cam, new Vector3(
							wcx + wrx * Mathf.Cos(ang), 0f,
							wcz + wrz * Mathf.Sin(ang)));
						if (i > 0) cPts.Append(' ');
						cPts.Append($"{F(sp.x)},{F(sp.y)}");
					}
					sb.Append(",\"tag\":\"polygon\"");
					sb.Append($",\"points\":\"{cPts}\"");
					sb.Append($",\"stroke\":\"{c}\",\"stroke-width\":\"4\",\"fill\":\"none\"");
					break;
				}
				case "free": {
					if (s.pts.Count < 2) return null;
					var d = new StringBuilder("M ");
					for (int i = 0; i < s.pts.Count; i++) {
						Vector2 p = WorldToSVG(cam, new Vector3(s.pts[i].x, 0, s.pts[i].y));
						if (i > 0) d.Append(" L ");
						d.Append($"{F(p.x)},{F(p.y)}");
					}
					sb.Append(",\"tag\":\"path\"");
					sb.Append($",\"d\":\"{d}\"");
					sb.Append($",\"stroke\":\"{c}\",\"stroke-width\":\"4\"");
					sb.Append(",\"stroke-linecap\":\"round\",\"stroke-linejoin\":\"round\",\"fill\":\"none\"");
					break;
				}
				default: return null;
			}
			sb.Append('}');
			return sb.ToString();
		}

		private void SendShapesUpdate(Camera cam) {
			var sb = new StringBuilder("[");
			bool first = true;
			foreach (var s in m_Shapes) {
				string json = ShapeToJSON(cam, s);
				if (json == null) continue;
				if (!first) sb.Append(',');
				first = false;
				sb.Append(json);
			}
			sb.Append(']');
			UIManager.defaultUISystem.defaultUIView.View
				.TriggerEvent("skyplan.shapesUpdate", sb.ToString());
		}

		private void SendPreviewUpdate(Camera cam) {
			if (m_ActiveShape == null || m_ActiveShape.pts.Count < 2) {
				UIManager.defaultUISystem.defaultUIView.View.TriggerEvent("skyplan.previewUpdate", "");
				return;
			}
			var temp = new Shape {
				id    = "__preview__",
				type  = m_ActiveShape.type,
				layer = m_ActiveShape.layer,
				pts   = m_ActiveShape.pts,
			};
			string json = ShapeToJSON(cam, temp);
			UIManager.defaultUISystem.defaultUIView.View
				.TriggerEvent("skyplan.previewUpdate", json ?? "");
		}

		private static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

		// ── Injection ─────────────────────────────────────────────────────────

		private void EnsureInjected() {
			if (m_Injected) return;
			if (string.IsNullOrEmpty(Mod.modPath)) { Mod.log.Warn("modPath not set"); return; }
			string jsPath = Path.Combine(Mod.modPath, "UI", "app.js");
			if (!File.Exists(jsPath)) { Mod.log.Warn($"app.js not found at {jsPath}"); return; }
			var view = UIManager.defaultUISystem.defaultUIView.View;
			view.RegisterForEvent("skyplan.debug",      (System.Action<string>)(msg => Mod.log.Info($"[JS] {msg}")));
			view.RegisterForEvent("skyplan.setTool",    (System.Action<string>)(t   => {
				m_CurrentTool = t;
				if (t != "erase" && m_EraseTarget != null) {
					m_EraseTarget = null;
					UIManager.defaultUISystem.defaultUIView.View.TriggerEvent("skyplan.highlightShape", "");
				}
			}));
			view.RegisterForEvent("skyplan.setLayer",   (System.Action<string>)(l   => m_CurrentLayer = l));
			view.RegisterForEvent("skyplan.drawStart",  (System.Action<string>)(csv => { var p = CSV2(csv); HandleDrawStart(p.x, p.y); }));
			view.RegisterForEvent("skyplan.drawMove",   (System.Action<string>)(csv => { var p = CSV2(csv); HandleDrawMove (p.x, p.y); }));
			view.RegisterForEvent("skyplan.drawEnd",    (System.Action<string>)(csv => { var p = CSV2(csv); HandleDrawEnd  (p.x, p.y); }));
			view.RegisterForEvent("skyplan.eraseHover",  (System.Action<string>)(csv => { var p = CSV2(csv); HandleEraseHover(p.x, p.y); }));
			view.RegisterForEvent("skyplan.clearLayer", (System.Action<string>)(l   => HandleClearLayer(l)));
			view.RegisterForEvent("skyplan.undo",       (System.Action<string>)(_ => HandleUndo()));
			view.ExecuteScript(File.ReadAllText(jsPath));
			m_Injected = true;
			Mod.log.Info("Skyplan UI injected");
		}

		private static Vector2 CSV2(string csv) {
			var p = csv.Split(',');
			return new Vector2(
				float.Parse(p[0], CultureInfo.InvariantCulture),
				float.Parse(p[1], CultureInfo.InvariantCulture));
		}
	}
}
