using Colossal.UI.Binding;
using Game;
using Game.SceneFlow;
using Game.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace skyplan.Systems {
	internal class Shape {
		public string id;
		public string type;
		public string layer;
		public List<Vector3> pts = new();
	}

	internal enum OpType { Draw, Delete, ClearLayer }

	internal class Op {
		public OpType type;
		public Shape shape;
		public string layer;
		public List<Shape> cleared;
	}

	public partial class DrawingSystem : UISystemBase {
		public static DrawingSystem instance;

		private ICameraSystem m_Camera;
		private bool m_PanelVisible;

		internal readonly List<Shape> m_Shapes = new();
		private readonly List<Op> m_UndoStack = new();
		private Shape m_ActiveShape;
		private string m_CurrentTool = "line";
		private string m_CurrentLayer = "roads";
		private int m_NextId;
		private string m_EraseTarget;

		private ValueBinding<bool> m_PanelVisibleBinding;
		private ValueBinding<string> m_ShapesBinding;
		private ValueBinding<string> m_ShapesBaselineBinding;
		private ValueBinding<string> m_TransformBinding;
		private ValueBinding<string> m_PreviewBinding;
		private ValueBinding<string> m_HighlightBinding;

		protected override void OnCreate() {
			base.OnCreate();
			instance = this;
			m_Camera = World.GetOrCreateSystemManaged<CameraSystem>();
			Mod.log.Info("DrawingSystem.OnCreate");

			m_PanelVisibleBinding = new ValueBinding<bool>("skyplan", "panelVisible", false);
			m_ShapesBinding = new ValueBinding<string>("skyplan", "shapes", "[]");
			m_ShapesBaselineBinding = new ValueBinding<string>("skyplan", "shapesBaseline", "[]");
			m_TransformBinding = new ValueBinding<string>("skyplan", "transform", "");
			m_PreviewBinding = new ValueBinding<string>("skyplan", "preview", "");
			m_HighlightBinding = new ValueBinding<string>("skyplan", "highlight", "");

			AddBinding(m_PanelVisibleBinding);
			AddBinding(m_ShapesBinding);
			AddBinding(m_ShapesBaselineBinding);
			AddBinding(m_TransformBinding);
			AddBinding(m_PreviewBinding);
			AddBinding(m_HighlightBinding);

			AddBinding(new TriggerBinding<string>("skyplan", "drawStart", csv => { var p = CSV2(csv); HandleDrawStart(p.x, p.y); }));
			AddBinding(new TriggerBinding<string>("skyplan", "drawMove", csv => { var p = CSV2(csv); HandleDrawMove(p.x, p.y); }));
			AddBinding(new TriggerBinding<string>("skyplan", "drawEnd", csv => { var p = CSV2(csv); HandleDrawEnd(p.x, p.y); }));
			AddBinding(new TriggerBinding<string>("skyplan", "setTool", t => { m_CurrentTool = t; if (t != "erase") { m_EraseTarget = null; m_HighlightBinding.Update(""); } }));
			AddBinding(new TriggerBinding<string>("skyplan", "setLayer", l => m_CurrentLayer = l));
			AddBinding(new TriggerBinding<string>("skyplan", "clearLayer", l => HandleClearLayer(l)));
			AddBinding(new TriggerBinding<string>("skyplan", "undo", _ => HandleUndo()));
			AddBinding(new TriggerBinding<string>("skyplan", "eraseHover", csv => { var p = CSV2(csv); HandleEraseHover(p.x, p.y); }));
			AddBinding(new TriggerBinding("skyplan", "panelClosed", () => {
				m_PanelVisible = false;
				m_ActiveShape = null;
				m_PanelVisibleBinding.Update(false);
				m_PreviewBinding.Update("");
			}));
		}

		protected override void OnUpdate() {
			base.OnUpdate();
			bool inGame = GameManager.instance != null &&
				(GameManager.instance.gameMode & GameMode.Game) != 0;
			if (inGame && Mod.m_ToggleAction != null && Mod.m_ToggleAction.WasPressedThisFrame())
				TogglePanel();
			if (m_PanelVisible) {
				if (!inGame) {
					m_PanelVisible = false;
					m_ActiveShape = null;
					m_PanelVisibleBinding.Update(false);
					m_PreviewBinding.Update("");
				} else {
					SyncCamera();
				}
			}
		}

		public void TogglePanel() {
			m_PanelVisible = !m_PanelVisible;
			if (m_PanelVisible) {
				m_Camera.SetBaseline();
				if (m_Camera.IsReady) { UpdateShapesJson(); UpdateShapesJsonBaseline(); }
			} else {
				m_ActiveShape = null;
				m_PreviewBinding.Update("");
			}
			m_PanelVisibleBinding.Update(m_PanelVisible);
			Mod.log.Info($"Skyplan panel {(m_PanelVisible ? "shown" : "hidden")}");
		}

		private void SyncCamera() {
			if (!m_Camera.HasChanged()) return;
			UpdateShapesJson();
			if (m_ActiveShape != null) UpdatePreviewJson();
			string matrix = m_Camera.ComputeTransformMatrix();
			if (matrix != null) m_TransformBinding.Update(matrix);
		}

		private void HandleDrawStart(float sx, float sy) {
			if (!m_Camera.IsReady) return;
			if (m_CurrentTool == "erase") { EraseNearest(); return; }
			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) return;
			m_ActiveShape = new Shape {
				id = $"s{m_NextId++}",
				type = m_CurrentTool,
				layer = m_CurrentLayer,
			};
			m_ActiveShape.pts.Add(world);
		}

		private void HandleDrawMove(float sx, float sy) {
			if (m_ActiveShape == null || !m_Camera.IsReady) return;
			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) return;
			if (m_ActiveShape.type == "free") {
				if (m_ActiveShape.pts.Count == 0 ||
					Vector3.Distance(m_ActiveShape.pts[m_ActiveShape.pts.Count - 1], world) > 5f)
					m_ActiveShape.pts.Add(world);
			} else {
				if (m_ActiveShape.pts.Count > 1) m_ActiveShape.pts[1] = world;
				else m_ActiveShape.pts.Add(world);
			}
			UpdatePreviewJson();
		}

		private void HandleDrawEnd(float sx, float sy) {
			if (m_ActiveShape == null) return;
			HandleDrawMove(sx, sy);
			if (m_ActiveShape.pts.Count >= 2) {
				m_Shapes.Add(m_ActiveShape);
				m_UndoStack.Add(new Op { type = OpType.Draw, shape = m_ActiveShape });
				if (m_Camera.IsReady) { UpdateShapesJson(); UpdateShapesJsonBaseline(); }
			}
			m_ActiveShape = null;
			m_PreviewBinding.Update("");
		}

		private void HandleClearLayer(string layer) {
			var removed = m_Shapes.FindAll(s => s.layer == layer);
			if (removed.Count > 0)
				m_UndoStack.Add(new Op { type = OpType.ClearLayer, layer = layer, cleared = removed });
			m_Shapes.RemoveAll(s => s.layer == layer);
			if (m_ActiveShape != null && m_ActiveShape.layer == layer)
				m_ActiveShape = null;
			if (m_Camera.IsReady) { UpdateShapesJson(); UpdateShapesJsonBaseline(); }
			m_PreviewBinding.Update("");
		}

		private void HandleUndo() {
			if (m_UndoStack.Count == 0) return;
			Op op = m_UndoStack[m_UndoStack.Count - 1];
			m_UndoStack.RemoveAt(m_UndoStack.Count - 1);
			switch (op.type) {
				case OpType.Draw: m_Shapes.Remove(op.shape); break;
				case OpType.Delete: m_Shapes.Add(op.shape); break;
				case OpType.ClearLayer: m_Shapes.AddRange(op.cleared); break;
			}
			if (m_Camera.IsReady) { UpdateShapesJson(); UpdateShapesJsonBaseline(); }
		}

		private void HandleEraseHover(float sx, float sy) {
			if (!m_Camera.IsReady) return;
			const float Threshold = 80f;
			Vector2 cursor = new(sx, sy);
			float best = float.MaxValue;
			string found = null;
			foreach (var s in m_Shapes) {
				float d = Vector2.Distance(ShapeScreenCentroid(s), cursor);
				if (d < best) { best = d; found = s.id; }
			}
			string newTarget = (found != null && best <= Threshold) ? found : null;
			if (newTarget == m_EraseTarget) return;
			m_EraseTarget = newTarget;
			m_HighlightBinding.Update(m_EraseTarget ?? "");
		}

		private void EraseNearest() {
			if (m_EraseTarget == null) return;
			Shape target = m_Shapes.Find(s => s.id == m_EraseTarget);
			if (target == null) return;
			m_UndoStack.Add(new Op { type = OpType.Delete, shape = target });
			m_Shapes.Remove(target);
			m_EraseTarget = null;
			m_HighlightBinding.Update("");
			if (m_Camera.IsReady) UpdateShapesJson();
		}

		private Vector2 ShapeScreenCentroid(Shape s) {
			Vector2 sum = Vector2.zero;
			foreach (var p in s.pts)
				sum += m_Camera.WorldToSVG(p);
			return sum / s.pts.Count;
		}

		private static string LayerColor(string layer) => layer switch {
			"roads" => "#ff4444",
			"zoning" => "#44dd44",
			"transit" => "#4488ff",
			"notes" => "#ffcc00",
			_ => "#ffffff",
		};

		private string ShapeToJSON(Shape s, bool baseline = false) {
			string c = LayerColor(s.layer);
			var sb = new StringBuilder();
			sb.Append($"{{\"id\":\"{s.id}\",\"layer\":\"{s.layer}\"");

			Vector2 Proj(Vector3 w) =>
				baseline ? m_Camera.WorldToSVGBaseline(w) : m_Camera.WorldToSVG(w);

			switch (s.type) {
				case "line": {
						if (s.pts.Count < 2) return null;
						Vector2 p0 = Proj(s.pts[0]);
						Vector2 p1 = Proj(s.pts[1]);
						sb.Append(",\"tag\":\"line\"");
						sb.Append($",\"x1\":\"{F(p0.x)}\",\"y1\":\"{F(p0.y)}\"");
						sb.Append($",\"x2\":\"{F(p1.x)}\",\"y2\":\"{F(p1.y)}\"");
						sb.Append($",\"stroke\":\"{c}\",\"stroke-width\":\"4\",\"stroke-linecap\":\"round\",\"fill\":\"none\"");
						break;
					}
				case "rect": {
						if (s.pts.Count < 2) return null;
						Vector2 a = Proj(s.pts[0]);
						Vector2 b = Proj(new Vector3(s.pts[1].x, s.pts[0].y, s.pts[0].z));
						Vector2 c2 = Proj(s.pts[1]);
						Vector2 d = Proj(new Vector3(s.pts[0].x, s.pts[1].y, s.pts[1].z));
						sb.Append(",\"tag\":\"polygon\"");
						sb.Append($",\"points\":\"{F(a.x)},{F(a.y)} {F(b.x)},{F(b.y)} {F(c2.x)},{F(c2.y)} {F(d.x)},{F(d.y)}\"");
						sb.Append($",\"stroke\":\"{c}\",\"stroke-width\":\"4\",\"fill\":\"none\"");
						break;
					}
				case "circle": {
						if (s.pts.Count < 2) return null;
						float wcx = (s.pts[0].x + s.pts[1].x) * 0.5f;
						float wcy = (s.pts[0].y + s.pts[1].y) * 0.5f;
						float wcz = (s.pts[0].z + s.pts[1].z) * 0.5f;
						float wrx = Mathf.Abs(s.pts[1].x - s.pts[0].x) * 0.5f;
						float wrz = Mathf.Abs(s.pts[1].z - s.pts[0].z) * 0.5f;
						const int N = 32;
						var cPts = new StringBuilder();
						for (int i = 0; i < N; i++) {
							float ang = 2f * Mathf.PI * i / N;
							Vector2 sp = Proj(new Vector3(wcx + wrx * Mathf.Cos(ang), wcy, wcz + wrz * Mathf.Sin(ang)));
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
						var path = new StringBuilder("M ");
						for (int i = 0; i < s.pts.Count; i++) {
							Vector2 p = Proj(s.pts[i]);
							if (i > 0) path.Append(" L ");
							path.Append($"{F(p.x)},{F(p.y)}");
						}
						sb.Append(",\"tag\":\"path\"");
						sb.Append($",\"d\":\"{path}\"");
						sb.Append($",\"stroke\":\"{c}\",\"stroke-width\":\"4\"");
						sb.Append(",\"stroke-linecap\":\"round\",\"stroke-linejoin\":\"round\",\"fill\":\"none\"");
						break;
					}
				default: return null;
			}
			sb.Append('}');
			return sb.ToString();
		}

		private void UpdateShapesJson() {
			var sb = new StringBuilder("[");
			bool first = true;
			foreach (var s in m_Shapes) {
				string json = ShapeToJSON(s, baseline: false);
				if (json == null) continue;
				if (!first) sb.Append(',');
				first = false;
				sb.Append(json);
			}
			sb.Append(']');
			m_ShapesBinding.Update(sb.ToString());
		}

		private void UpdateShapesJsonBaseline() {
			var sb = new StringBuilder("[");
			bool first = true;
			foreach (var s in m_Shapes) {
				string json = ShapeToJSON(s, baseline: true);
				if (json == null) continue;
				if (!first) sb.Append(',');
				first = false;
				sb.Append(json);
			}
			sb.Append(']');
			m_ShapesBaselineBinding.Update(sb.ToString());
		}

		private void UpdatePreviewJson() {
			if (m_ActiveShape == null || m_ActiveShape.pts.Count < 2) {
				m_PreviewBinding.Update("");
				return;
			}
			var temp = new Shape {
				id = "__preview__",
				type = m_ActiveShape.type,
				layer = m_ActiveShape.layer,
				pts = m_ActiveShape.pts,
			};
			m_PreviewBinding.Update(ShapeToJSON(temp) ?? "");
		}

		private static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

		private static Vector2 CSV2(string csv) {
			var p = csv.Split(',');
			return new Vector2(
				float.Parse(p[0], CultureInfo.InvariantCulture),
				float.Parse(p[1], CultureInfo.InvariantCulture));
		}
	}
}
