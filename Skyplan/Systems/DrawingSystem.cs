using Colossal.UI.Binding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Game;
using Game.SceneFlow;
using Game.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Skyplan.Models;
using Skyplan.Models.dto;
using Skyplan.Models.Results;
using System;
using System.IO;
using Skyplan.Cross;
using Skyplan.Persistence.Helpers;

namespace Skyplan.Systems {

	internal enum OpType { Draw, Delete, ClearLayer, ClearAll }

	internal class Op {
		public OpType type;
		public Shape shape;
		public string layer;
		public List<Shape> cleared;
	}

	public partial class DrawingSystem : UISystemBase {
		public static DrawingSystem instance;

		public bool IsPanelVisible => m_PanelVisible;

		private ICameraSystem m_Camera;
		private bool m_PanelVisible;

		internal readonly List<Shape> m_Shapes = [];
		private readonly List<Op> m_UndoStack = [];
		private readonly List<Op> m_RedoStack = [];
		private Shape m_ActiveShape;
		private List<Vector3> _points = [];
		private Tools m_CurrentTool;
		private LayerDefDto m_CurrentLayer = new() {
			Id = "default", Label = "Default",
			Style = new Dictionary<string, string> { { "stroke", "#ffffff" }, { "strokeWidth", "2" } }
		};
		internal int m_NextId;
		private string m_EraseTarget;

		private ValueBinding<bool> m_PanelVisibleBinding;
		private ValueBinding<string> m_ShapesBinding;
		private ValueBinding<string> m_PreviewBinding;
		private ValueBinding<string> m_HighlightBinding;
		private ValueBinding<string> m_LayersConfigBinding;
		private ValueBinding<bool> m_ShowDescriptionsBinding;
		private ValueBinding<string> m_IndicatorBinding;
		private ValueBinding<bool> m_SnapEnabledBinding;
		private ValueBinding<string> m_LayerVisibleBinding;
		private readonly Dictionary<string, bool> m_LayerVisible = [];

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
			m_PreviewBinding = new ValueBinding<string>("skyplan", "preview", "");
			m_HighlightBinding = new ValueBinding<string>("skyplan", "highlight", "");
			m_LayersConfigBinding = new ValueBinding<string>("skyplan", "layersConfig", "{\"layers\":[]}");
			m_ShowDescriptionsBinding = new ValueBinding<bool>("skyplan", "showDescriptions", LoadDisplaySettings("showDescriptions"));
			m_IndicatorBinding = new ValueBinding<string>("skyplan", "indicator", "");
			m_SnapEnabled = LoadDisplaySettings("snapEnabled", true);
			m_SnapEnabledBinding = new ValueBinding<bool>("skyplan", "snapEnabled", m_SnapEnabled);
			m_LayerVisibleBinding = new ValueBinding<string>("skyplan", "layerVisible", "{}");

			AddBinding(m_PanelVisibleBinding);
			AddBinding(m_ShapesBinding);
			AddBinding(m_PreviewBinding);
			AddBinding(m_HighlightBinding);
			AddBinding(m_LayersConfigBinding);
			AddBinding(m_ShowDescriptionsBinding);
			AddBinding(m_IndicatorBinding);
			AddBinding(m_SnapEnabledBinding);
			AddBinding(m_LayerVisibleBinding);

			AddBinding(new TriggerBinding<string>("skyplan", "setSnapEnabled", val => {
				m_SnapEnabled = val == "true";
				m_SnapEnabledBinding.Update(m_SnapEnabled);
				SaveSettings("snapEnabled", m_SnapEnabled);
				if (!m_SnapEnabled) m_IndicatorBinding.Update("");
			}));

			AddBinding(new TriggerBinding<string>("skyplan", "setLayerVisible", payload => {
				int sep = payload.IndexOf('|');
				if (sep < 0) return;
				string layerId = payload[..sep];
				bool visible = payload[(sep + 1)..] == "true";
				m_LayerVisible[layerId] = visible;
				m_LayerVisibleBinding.Update(JsonConvert.SerializeObject(m_LayerVisible));
			}));

			AddBinding(new TriggerBinding<string>("skyplan", "setShowDescriptions", val => {
				bool newValue = val == "true";
				m_ShowDescriptionsBinding.Update(newValue);
				SaveSettings("showDescriptions", newValue);
			}));

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
				m_CurrentTool = (Tools)Enum.Parse(typeof(Tools), t, true);
				if (m_CurrentTool != Tools.erase) {
					m_EraseTarget = null;
					m_HighlightBinding.Update("");
				}
				m_IndicatorBinding.Update("");
			}));

			AddBinding(new TriggerBinding<string>("skyplan", "setLayer", json => m_CurrentLayer = JsonConvert.DeserializeObject<LayerDefDto>(json)));

			AddBinding(new TriggerBinding<string>("skyplan", "clearLayer", HandleClearLayer));
			AddBinding(new TriggerBinding<string>("skyplan", "clearAll", _ => HandleClearAll()));

			AddBinding(new TriggerBinding<string>("skyplan", "undo", _ => HandleUndo()));
			AddBinding(new TriggerBinding<string>("skyplan", "redo", _ => HandleRedo()));

			AddBinding(new TriggerBinding<string>("skyplan", "eraseHover", csv => {
				Vector2 p = CSV2(csv);
				HandleEraseHover(p.x, p.y);

			}));

			AddBinding(new TriggerBinding<string>("skyplan", "drawHover", csv => {
				Vector2 p = CSV2(csv);
				HandleDrawHover(p.x, p.y);
			}));

			AddBinding(new TriggerBinding("skyplan", "clearIndicator", () => m_IndicatorBinding.Update("")));

			AddBinding(new TriggerBinding("skyplan", "panelClosed", HidePanel));

			AddBinding(new TriggerBinding<string>("skyplan", "setShapeLabel", HandleSetShapeLabel));
			AddBinding(new TriggerBinding<string>("skyplan", "setShapeNote", HandleSetShapeNote));
			AddBinding(new TriggerBinding<string>("skyplan", "commitText", HandleCommitText));
		}

		protected override void OnUpdate() {
			base.OnUpdate();
			bool inGame = GameManager.instance != null &&
				(GameManager.instance.gameMode & GameMode.Game) != 0;
			if (inGame && Mod.m_ToggleAction?.WasPressedThisFrame() == true)
				TogglePanel();
			if (m_PanelVisible) {
				if (!inGame) {
					HidePanel();
				} else if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) {
					HidePanel();
				} else {
					SyncCamera();
				}
			}
		}

		private void HandleSetShapeLabel(string payload) {
			int sep = payload.IndexOf('|');
			if (sep < 0) return;
			string id = payload[..sep];
			string label = payload[(sep + 1)..];
			Shape shape = m_Shapes.Find(s => s.id == id);
			if (shape == null) return;
			shape.Label = string.IsNullOrEmpty(label) ? null : label;
			if (m_Camera.IsReady) UpdateShapesJson();
		}

		private void HandleSetShapeNote(string payload) {
			int sep = payload.IndexOf('|');
			if (sep < 0) return;
			string id = payload[..sep];
			string note = payload[(sep + 1)..];
			Shape shape = m_Shapes.Find(s => s.id == id);
			if (shape == null) return;
			shape.Description = string.IsNullOrEmpty(note) ? null : note;
			if (m_Camera.IsReady) UpdateShapesJson();
		}

		private void HandleCommitText(string payload) {
			int sep = payload.IndexOf('|');
			if (sep < 0) return;
			string id = payload[..sep];
			string text = payload[(sep + 1)..];
			Shape shape = m_Shapes.Find(s => s.id == id);
			if (shape == null) return;
			if (string.IsNullOrEmpty(text)) {
				m_UndoStack.RemoveAll(op => op.shape?.id == id);
				m_Shapes.Remove(shape);
			} else {
				shape.Label = text;
			}
			if (m_Camera.IsReady) UpdateShapesJson();
		}

		private void HidePanel() {
			m_PanelVisible = false;
			m_ActiveShape = null;
			_points.Clear();
			m_PanelVisibleBinding.Update(false);
			m_PreviewBinding.Update("");
			m_IndicatorBinding.Update("");
			PlanPersistenceSystem.instance?.SavePlan();
		}

		public void TogglePanel() {
			m_PanelVisible = !m_PanelVisible;
			if (m_PanelVisible) {
				JObject merged = LayerMerger.LoadAndMerge(Paths.DefaultLayers, Paths.UserLayers);
				m_LayersConfigBinding.Update(merged.ToString(Formatting.None));
				if (m_Camera.IsReady) {
					UpdateShapesJson();
				}
			} else {
				m_ActiveShape = null;
				m_PreviewBinding.Update("");
				m_IndicatorBinding.Update("");
				PlanPersistenceSystem.instance?.SavePlan();
			}
			m_PanelVisibleBinding.Update(m_PanelVisible);
			Mod.log.Info($"Skyplan panel {(m_PanelVisible ? "shown" : "hidden")}");
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
		}

		private void HandleDrawStart(float sx, float sy) {
			if (!m_Camera.IsReady) return;
			if (m_CurrentTool == Tools.erase) {
				EraseNearest();
				return;
			}

			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) return;
			ApplySnap(ref world, sx, sy);
			m_IndicatorBinding.Update("");

			if (m_CurrentTool == Tools.point || m_CurrentTool == Tools.text) {
				Shape s = new() {
					id = $"s{m_NextId++}",
					Type = m_CurrentTool,
					layer = m_CurrentLayer,
					pts = [world],
				};
				s.CalcBounds();
				m_Shapes.Add(s);
				PushUndo(new Op { type = OpType.Draw, shape = s });
				if (m_Camera.IsReady) {
					UpdateShapesJson();
				}
				return;
			}

			m_ActiveShape = new Shape {
				id = $"s{m_NextId++}",
				Type = m_CurrentTool,
				layer = m_CurrentLayer,
			};
			m_ActiveShape.pts.Add(world);
			if (m_CurrentTool == Tools.polygon) _points.Add(world);
		}

		private void HandleDrawMove(float sx, float sy) {
			if (m_ActiveShape == null || !m_Camera.IsReady) return;
			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) return;
			ApplySnap(ref world, sx, sy);

			if (m_ActiveShape.Type == Tools.polygon) {
				var previewPts = new List<Vector3>(_points) { world };
				Shape temp = new() { id = "__preview__", Type = Tools.polygon, layer = m_ActiveShape.layer, pts = previewPts };
				m_PreviewBinding.Update(ShapeToJSON(temp) ?? "");
				return;
			}

			if (m_ActiveShape.pts.Count > 1)
				m_ActiveShape.pts[1] = world;
			else
				m_ActiveShape.pts.Add(world);
			UpdatePreviewJson();
		}

		private void AddPoint(float sx, float sy) {
			if (m_ActiveShape == null || !m_Camera.IsReady) return;
			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) return;
			ApplySnap(ref world, sx, sy);
			_points.Add(world);
		}

		private const float SnapPixelTolerance = 12f;
		private bool m_SnapEnabled;

		// Meters-per-pixel at the cursor's own world depth, recomputed every call since zoom/tilt
		// changes it continuously - can't be cached across frames.
		private float SnapToleranceWorld(float sx, float sy) {
			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 p0)) return 0f;
			if (!m_Camera.ScreenToWorld(sx + 1f, sy, out Vector3 p1)) return 0f;
			float dx = p1.x - p0.x, dz = p1.z - p0.z;
			return Mathf.Sqrt((dx * dx) + (dz * dz)) * SnapPixelTolerance;
		}

		// Absent = visible, matching the JS-side default (prev[layerId] ?? true).
		private bool IsLayerVisible(Shape shape) {
			string layerId = shape.layer?.Id;
			return layerId == null || !m_LayerVisible.TryGetValue(layerId, out bool visible) || visible;
		}

		private bool TrySnapHit(Vector3 world, float sx, float sy, out SnapHit hit) {
			hit = default;
			if (!m_SnapEnabled) return false;
			float tol = SnapToleranceWorld(sx, sy);
			if (tol <= 0f) return false;
			return SnapQuery.TrySnap(m_Shapes, m_ActiveShape, world, tol, out hit, IsLayerVisible);
		}

		private bool ApplySnap(ref Vector3 world, float sx, float sy) {
			if (TrySnapHit(world, sx, sy, out SnapHit hit)) {
				world = hit.Point;
				return true;
			}
			return false;
		}

		// Runs while idle (before the first click), so the user sees where a click would land -
		// only path/polygon benefit; mid-draw feedback is already the moving preview shape itself.
		private void HandleDrawHover(float sx, float sy) {
			if (!m_Camera.IsReady || m_ActiveShape != null) return;
			if (m_CurrentTool != Tools.path && m_CurrentTool != Tools.polygon) {
				m_IndicatorBinding.Update("");
				return;
			}
			if (!m_Camera.ScreenToWorld(sx, sy, out Vector3 world)) {
				m_IndicatorBinding.Update("");
				return;
			}
			if (TrySnapHit(world, sx, sy, out SnapHit hit) && m_Camera.WorldToSVG(hit.Point, out Vector2 svg)) {
				string kind = hit.VertexIndex >= 0 ? "vertex" : "edge";
				m_IndicatorBinding.Update($"{F(svg.x)},{F(svg.y)},{kind}");
			} else {
				m_IndicatorBinding.Update("");
			}
		}

		private void HandleDrawEnd(float sx, float sy) {
			if (m_ActiveShape == null) return;

			if (m_ActiveShape.Type == Tools.polygon) {
				m_ActiveShape.pts.Clear();
				m_ActiveShape.pts.AddRange(_points);
				if (m_ActiveShape.pts.Count >= 3) {
					m_ActiveShape.CalcBounds();
					m_Shapes.Add(m_ActiveShape);
					PushUndo(new Op { type = OpType.Draw, shape = m_ActiveShape });
					if (m_Camera.IsReady) {
						UpdateShapesJson();
					}
				}
				m_ActiveShape = null;
				_points.Clear();
				m_PreviewBinding.Update("");
				return;
			}

			HandleDrawMove(sx, sy);
			if (m_ActiveShape.pts.Count >= 2) {
				m_ActiveShape.CalcBounds();
				m_Shapes.Add(m_ActiveShape);
				PushUndo(new Op { type = OpType.Draw, shape = m_ActiveShape });
				if (m_Camera.IsReady) {
					UpdateShapesJson();
				}
			}
			m_ActiveShape = null;
			_points = [];
			m_PreviewBinding.Update("");
		}

		private void HandleClearAll() {
			if (m_Shapes.Count == 0) return;
			PushUndo(new Op { type = OpType.ClearAll, cleared = [.. m_Shapes] });
			m_Shapes.Clear();
			m_ActiveShape = null;
			if (m_Camera.IsReady) {
				UpdateShapesJson();
			}
			m_PreviewBinding.Update("");
		}

		private void HandleClearLayer(string layer) {
			var removed = m_Shapes.FindAll(s => s.layer?.Id == layer);
			if (removed.Count > 0)
				PushUndo(new Op { type = OpType.ClearLayer, layer = layer, cleared = removed });
			m_Shapes.RemoveAll(s => s.layer?.Id == layer);
			if (m_ActiveShape != null && m_ActiveShape.layer?.Id == layer)
				m_ActiveShape = null;
			if (m_Camera.IsReady) {
				UpdateShapesJson();
			}
			m_PreviewBinding.Update("");
		}

		private void PushUndo(Op op) {
			m_UndoStack.Add(op);
			m_RedoStack.Clear();
		}

		private void HandleUndo() {
			if (m_UndoStack.Count == 0) return;
			Op op = m_UndoStack[^1];
			m_UndoStack.RemoveAt(m_UndoStack.Count - 1);
			switch (op.type) {
				case OpType.Draw: m_Shapes.Remove(op.shape); break;
				case OpType.Delete: m_Shapes.Add(op.shape); break;
				case OpType.ClearLayer:
				case OpType.ClearAll: m_Shapes.AddRange(op.cleared); break;
			}
			m_RedoStack.Add(op);
			if (m_Camera.IsReady) {
				UpdateShapesJson();
			}
		}

		private void HandleRedo() {
			if (m_RedoStack.Count == 0) return;
			Op op = m_RedoStack[^1];
			m_RedoStack.RemoveAt(m_RedoStack.Count - 1);
			switch (op.type) {
				case OpType.Draw: m_Shapes.Add(op.shape); break;
				case OpType.Delete: m_Shapes.Remove(op.shape); break;
				case OpType.ClearLayer:
				case OpType.ClearAll: m_Shapes.RemoveAll(s => op.cleared.Contains(s)); break;
			}
			m_UndoStack.Add(op);
			if (m_Camera.IsReady) {
				UpdateShapesJson();
			}
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
			PushUndo(new Op { type = OpType.Delete, shape = target });
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

		private ShapeDto CreateDto(Shape shape) {
			if (shape.pts.Count == 0) return null;
			ShapeDto shapeDto = new() {
				Id = shape.id,
				LayerId = shape.layer?.Id,
				Label = shape.Label,
				Description = shape.Description,
				Tag = shape.Type switch {
					Tools.path => Tag.path,
					Tools.polygon => Tag.polygon,
					Tools.point => Tag.circle,
					Tools.text => Tag.text,
					_ => Tag.none
				}
			};
			foreach (Vector3 pt in shape.pts) {
				if (!m_Camera.WorldToSVG(pt, out Vector2 p)) return null;
				shapeDto.Pts.Add(new ScreenPt { x = p.x, y = p.y });
			}
			return shapeDto;
		}

		private string ShapeToJSON(Shape s) {
			return JsonConvert.SerializeObject(CreateDto(s));
		}

		private void UpdateShapesJson() {
			List<ShapeDto> shapeDtos = [];
			foreach (Shape s in m_Shapes) {
				if (!ShapeInView(s)) continue;
				ShapeDto dto = CreateDto(s);
				if (dto == null) continue;
				shapeDtos.Add(dto);
			}
			string json = JsonConvert.SerializeObject(shapeDtos);
			m_ShapesBinding.Update(json);
		}
		private bool ShapeInView(Shape s) {
			if (s.pts.Count == 0) return false;
			return m_Camera.IsInView(s.Extents);
		}

		public void LoadShapes(List<Shape> imported) {
			m_Shapes.Clear();
			m_UndoStack.Clear();
			m_ActiveShape = null;
			foreach (Shape s in imported) s.CalcBounds();
			m_Shapes.AddRange(imported);
			if (m_Camera.IsReady) {
				UpdateShapesJson();
			}
			m_PreviewBinding.Update("");
		}

		public void MergeShapes(List<Shape> imported) {
			if (m_Shapes.Count == 0) {
				LoadShapes(imported);
				return;
			}
			HashSet<string> existingIds = [.. m_Shapes.Select(s => s.id)];
			foreach (Shape s in imported) {
				if (existingIds.Add(s.id)) {
					s.CalcBounds();
					m_Shapes.Add(s);
				}
			}
			if (m_Camera.IsReady) {
				UpdateShapesJson();
			}
			m_PreviewBinding.Update("");
		}

		private void UpdatePreviewJson() {
			if (m_ActiveShape == null || m_ActiveShape.pts.Count < 2) {
				m_PreviewBinding.Update("");
				return;
			}
			Shape temp = new() {
				id = "__preview__",
				Type = m_ActiveShape.Type,
				layer = m_ActiveShape.layer,
				pts = m_ActiveShape.pts,
			};
			m_PreviewBinding.Update(ShapeToJSON(temp) ?? "");
		}

		private static bool LoadDisplaySettings(string key, bool defaultValue = false) {
			try {
				string path = Paths.DisplaySettingsPath;
				if (!File.Exists(path)) return defaultValue;
				var json = JObject.Parse(File.ReadAllText(path));
				return json[key]?.Value<bool>() ?? defaultValue;
			} catch (Exception ex) {
				Mod.log.Warn($"[Skyplan] Failed to load display settings: {ex.Message}");
				return defaultValue;
			}
		}

		private static void SaveSettings(string key, bool value) {
			try {
				string path = Paths.DisplaySettingsPath;
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				JObject json = File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : [];
				json[key] = value;
				File.WriteAllText(path, json.ToString(Formatting.Indented));
			} catch (Exception ex) {
				Mod.log.Warn($"[Skyplan] Failed to save display settings: {ex.Message}");
			}
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
