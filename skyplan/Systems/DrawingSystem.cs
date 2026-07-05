using Colossal.UI.Binding;
using Newtonsoft.Json;
using Game;
using Game.SceneFlow;
using Game.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Skyplan.Models;
using Skyplan.Models.dto;
using System.Text.RegularExpressions;
using System;

namespace skyplan.Systems {

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

		internal readonly List<Shape> m_Shapes = [];
		private readonly List<Op> m_UndoStack = [];
		private Shape m_ActiveShape;
		private List<Vector3> _points = [];
		private string m_CurrentTool = "line";
		private LayerDefDto m_CurrentLayer = new() {
			Id = "default", Label = "Default",
			Style = new Dictionary<string, string> { { "stroke", "#ffffff" }, { "strokeWidth", "2" } }
		};
		private int m_NextId;
		private string m_EraseTarget;

		private ValueBinding<bool> m_PanelVisibleBinding;
		private ValueBinding<string> m_ShapesBinding;
		private ValueBinding<string> m_ShapesBaselineBinding;
		private ValueBinding<string> m_TransformBinding;
		private ValueBinding<string> m_PreviewBinding;
		private ValueBinding<string> m_HighlightBinding;

		protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode) {
			try {
				base.OnGamePreload(purpose, mode);
			} catch (InvalidOperationException ex) {
				Mod.log.Warn($"[DrawingSystem] OnGamePreload caught InvalidOperationException (system state destroyed during world rebuild): {ex.Message}");
			}
		}

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

			AddBinding(new TriggerBinding<string>("skyplan", "drawStart", csv => {
				Vector2 p = CSV2(csv);
				HandleDrawStart(p.x, p.y);
			}));

			AddBinding(new TriggerBinding<string>("skyplan", "drawMove", csv => {
				Vector2 p = CSV2(csv);
				HandleDrawMove(p.x, p.y);
			}));

			AddBinding(new TriggerBinding<string>("skyplan", "drawEnd", csv => {
				Vector2 p = CSV2(csv);
				HandleDrawEnd(p.x, p.y);
			}));

			AddBinding(new TriggerBinding<string>("skyplan", "addPoint", csv => {
				Vector2 p = CSV2(csv);
				AddPoint(p.x, p.y);
			}));

			AddBinding(new TriggerBinding<string>("skyplan", "setTool", t => {
				Tools tool = (Tools)Enum.Parse(typeof(Tools), t, true);
				m_CurrentTool = t;
				if (t != "erase") {
					m_EraseTarget = null;
					m_HighlightBinding.Update("");
				}
			}));

			AddBinding(new TriggerBinding<string>("skyplan", "setLayer", json => m_CurrentLayer = JsonConvert.DeserializeObject<LayerDefDto>(json)));

			AddBinding(new TriggerBinding<string>("skyplan", "clearLayer", HandleClearLayer));

			AddBinding(new TriggerBinding<string>("skyplan", "undo", _ => HandleUndo()));

			AddBinding(new TriggerBinding<string>("skyplan", "eraseHover", csv => {
				Vector2 p = CSV2(csv);
				HandleEraseHover(p.x, p.y);

			}));

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
			if (inGame && Mod.m_ToggleAction?.WasPressedThisFrame() == true)
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
				if (m_Camera.IsReady) {
					UpdateShapesJson();
					UpdateShapesJsonBaseline();
				}
			} else {
				m_ActiveShape = null;
				m_PreviewBinding.Update("");
			}
			m_PanelVisibleBinding.Update(m_PanelVisible);
			Mod.log.Info($"Skyplan panel {(m_PanelVisible ? "shown" : "hidden")}");
		}

		// private void SyncCamera() {
		// 	if (!m_Camera.HasChanged()) return;
		// 	UpdateShapesJson();
		// 	if (m_ActiveShape != null) UpdatePreviewJson();
		// 	// string matrix = m_Camera.ComputeTransformMatrix();
		// 	// if (matrix != null) m_TransformBinding.Update(matrix);
		// }

		/// <summary>
		/// Converts a screen-space point to a world-space position on the XZ plane (y = 0) using the specified camera.
		/// </summary>
		/// <remarks>The method returns <see langword="false"/> if the ray from the screen point is parallel to the XZ
		/// plane or points away from it. The resulting XZ coordinates are valid only when the method returns <see
		/// langword="true"/>.</remarks>
		/// <param name="cam">The camera used to perform the screen-to-world transformation.</param>
		/// <param name="sx">The horizontal screen coordinate, in pixels.</param>
		/// <param name="sy">The vertical screen coordinate, in pixels.</param>
		/// <param name="xz">When this method returns, contains the world-space XZ coordinates corresponding to the screen point if the
		/// conversion succeeds; otherwise, contains <see cref="Vector2.zero"/>.</param>
		/// <returns><see langword="true"/> if the screen point projects onto the XZ plane; otherwise, <see langword="false"/>.</returns>
		private static bool ScreenToWorldXZ(Camera cam, float sx, float sy, out Vector2 xz) {
			Ray ray = cam.ScreenPointToRay(new Vector3(sx, cam.pixelHeight - sy, 0f));
			if (Mathf.Abs(ray.direction.y) < 0.0001f) { xz = Vector2.zero; return false; }
			float t = -ray.origin.y / ray.direction.y;
			if (t < 0f) { xz = Vector2.zero; return false; }
			Vector3 w = ray.origin + ray.direction * t;
			xz = new Vector2(w.x, w.z);
			return true;
		}

		/// <summary>
		/// Synchronizes the camera state with the current view if a baseline is available and updates related shape data as
		/// needed.
		/// </summary>
		/// <remarks>This method performs no action if there is no baseline, if the camera is unavailable, or if the
		/// camera's view matrix has not changed since the last synchronization. It updates shape data only when necessary to
		/// reflect the latest camera state.</remarks>
		private void SyncCamera() {
			if (!m_Camera.HasChanged()) return;
			UpdateShapesJson();
			if (m_ActiveShape != null) UpdatePreviewJson();
			// string matrix = m_Camera.ComputeTransformMatrix();
			// if (matrix != null) m_TransformBinding.Update(matrix);
		}

		private void HandleDrawStart(float sx, float sy) {
			if (!m_Camera.IsReady) return;
			if (m_CurrentTool == "erase") {
				EraseNearest();
				return;
			}

			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) return;
			m_ActiveShape = new Shape {
				id = $"s{m_NextId++}",
				type = m_CurrentTool,
				layer = m_CurrentLayer,
			};
			m_ActiveShape.pts.Add(world);
			if (m_CurrentTool == "polygon") _points.Add(world);
		}

		private void HandleDrawMove(float sx, float sy) {
			if (m_ActiveShape == null || !m_Camera.IsReady) return;
			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) return;

			if (m_ActiveShape.type == "polygon") {
				var previewPts = new List<Vector3>(_points) { world };
				Shape temp = new() { id = "__preview__", type = "polygon", layer = m_ActiveShape.layer, pts = previewPts };
				m_PreviewBinding.Update(ShapeToJSON(temp) ?? "");
				return;
			}

			if (m_ActiveShape.pts.Count > 1)
				m_ActiveShape.pts[1] = world;
			else
				m_ActiveShape.pts.Add(world);
			UpdatePreviewJson();
		}

		private void AddPoint(float sx, float sy){
			if (m_ActiveShape == null || !m_Camera.IsReady) return;
			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) return;
			_points.Add(world);
		}

		private void HandleDrawEnd(float sx, float sy) {
			if (m_ActiveShape == null) return;

			if (m_ActiveShape.type == "polygon") {
				m_ActiveShape.pts.Clear();
				m_ActiveShape.pts.AddRange(_points);
				if (m_ActiveShape.pts.Count >= 3) {
					m_Shapes.Add(m_ActiveShape);
					m_UndoStack.Add(new Op { type = OpType.Draw, shape = m_ActiveShape });
					if (m_Camera.IsReady) {
					  UpdateShapesJson();
					  UpdateShapesJsonBaseline();
					}
				}
				m_ActiveShape = null;
				_points.Clear();
				m_PreviewBinding.Update("");
				return;
			}

			HandleDrawMove(sx, sy);
			if (m_ActiveShape.pts.Count >= 2) {
				m_Shapes.Add(m_ActiveShape);
				m_UndoStack.Add(new Op { type = OpType.Draw, shape = m_ActiveShape });
				if (m_Camera.IsReady) { UpdateShapesJson(); UpdateShapesJsonBaseline(); }
			}
			m_ActiveShape = null;
			_points = [];
			m_PreviewBinding.Update("");
		}

		private void HandleClearLayer(string layer) {
			var removed = m_Shapes.FindAll(s => s.layer.Id == layer);
			if (removed.Count > 0)
				m_UndoStack.Add(new Op { type = OpType.ClearLayer, layer = layer, cleared = removed });
			m_Shapes.RemoveAll(s => s.layer.Id == layer);
			if (m_ActiveShape != null && m_ActiveShape.layer.Id == layer)
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
			var sb = new StringBuilder();
			sb.Append($"{{\"id\":\"{s.id}\",\"layer\":\"{s.layer}\"");

			if (s.layer?.Style != null) {
				foreach (KeyValuePair<string, string> x in s.layer.Style) {
					string key = Regex.Replace(x.Key, "([A-Z])", "-$1").ToLower();
					sb.Append($",\"{key}\":\"{x.Value}\"");
				}
			} else {
				sb.Append(",\"stroke\":\"#ffffff\",\"stroke-width\":\"2\"");
			}
			switch (s.type) {
				case "line":
					if (s.pts.Count < 2) return null;
					sb.Append(CreateLineString(s.pts[0], s.pts[1]));
					break;

				case "polygon":
					if (s.pts.Count < 2) return null;
					sb.Append(CreatePolygon(s.pts));
					// if (s.pts.Count == 2) {
					//   sb.Append(CreateLineString(s.pts[0], s.pts[1]));
					//   break;
					// }
					// // todo it needs to preview with for or while update the shape needs to be snapped to the last line
					// Vector2 a = Proj(s.pts[0]);
					// Vector2 b = Proj(new Vector3(s.pts[1].x, s.pts[0].y, s.pts[0].z));
					// Vector2 c2 = Proj(s.pts[1]);
					// sb.Append(",\"tag\":\"polygon\"");
					// sb.Append($",\"points\":\"{F(a.x)},{F(a.y)} {F(b.x)},{F(b.y)} {F(c2.x)},{F(c2.y)} {F(a.x)},{F(a.y)}\"");
					break;
				default: return null;
			}
			sb.Append('}');
			return sb.ToString();
		}

		Vector2 Proj(Vector3 w) =>
		   m_Camera.WorldToSVG(w);
		private string CreateLineString(Vector3 pointa, Vector3 pointb){
			StringBuilder sb = new();
			Vector2 p0 = Proj(pointa);
			Vector2 p1 = Proj(pointb);
			sb.Append(",\"tag\":\"line\"");
			sb.Append($",\"x1\":\"{F(p0.x)}\",\"y1\":\"{F(p0.y)}\"");
			sb.Append($",\"x2\":\"{F(p1.x)}\",\"y2\":\"{F(p1.y)}\"");
			return sb.ToString();
		}

		private string CreatePolygon(List<Vector3> points){
		  if (points.Count < 2) {
			return string.Empty;
		  }
		  // invalid polygon
		  if (points.Count == 2) {
			return CreateLineString(points[0], points[1]);
		  }
		  StringBuilder sb = new();
		  Vector2 startingPoint = Proj(points[0]);
		  sb.Append(",\"tag\":\"polygon\"");
		  sb.Append($",\"points\":\"{F(startingPoint.x)},{F(startingPoint.y)} ");
		  for (int i = 1; i < points.Count ; i ++){
			Vector2 p = Proj(points[i]);
			sb.Append($" {F(p.x)},{F(p.y)} ");
		  }
		  sb.Append($"{F(startingPoint.x)},{F(startingPoint.y)}");
		  sb.Append('\"');

		  return sb.ToString();
		}

		private void UpdateShapesJson() {
			var sb = new StringBuilder("[");
			bool first = true;
			foreach (Shape s in m_Shapes) {
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
			Shape temp = new() {
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
