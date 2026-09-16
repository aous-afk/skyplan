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
		private List<Vector3> _handles = [];
		private Tools m_CurrentTool;
		private LayerDefDto m_CurrentLayer = new() {
			Id = "default", Label = "Default",
			Style = new Dictionary<string, string> { { "stroke", "#ffffff" }, { "strokeWidth", "2" } }
		};
		internal int m_NextId;
		private string m_EraseTarget;
		private List<ParallelLane> m_QueuedParallelLanes = [];
		// Persisted setting, not a constant - same pattern as m_ShowDescriptionsBinding/snapEnabled
		// below (LoadDisplaySettings/SaveSettings, disk-backed), so a future shift+wheel adjustment
		// persists across sessions and is available client-side even when no draw is in progress
		// (unlike piggybacking on the transient preview object).
		private float m_ParallelSpacing;
		private ValueBinding<float> m_ParallelSpacingBinding;

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
			m_ParallelSpacing = LoadDisplaySettings("parallelSpacing", 8f);
			m_ParallelSpacingBinding = new ValueBinding<float>("skyplan", "parallelSpacing", m_ParallelSpacing);

			AddBinding(m_PanelVisibleBinding);
			AddBinding(m_ShapesBinding);
			AddBinding(m_PreviewBinding);
			AddBinding(m_HighlightBinding);
			AddBinding(m_LayersConfigBinding);
			AddBinding(m_ShowDescriptionsBinding);
			AddBinding(m_IndicatorBinding);
			AddBinding(m_SnapEnabledBinding);
			AddBinding(m_LayerVisibleBinding);
			AddBinding(m_ParallelSpacingBinding);

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

			// For the planned shift+wheel adjustment - not wired to any input yet, but the persisted
			// setting + trigger round-trip is in place so that work only needs to add the client-side
			// wheel handler.
			AddBinding(new TriggerBinding<string>("skyplan", "setParallelSpacing", val => {
				if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float newValue)) return;
				m_ParallelSpacing = newValue;
				m_ParallelSpacingBinding.Update(newValue);
				SaveSettings("parallelSpacing", newValue);
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

			AddBinding(new TriggerBinding<string>("skyplan", "setParallelLayers", json => m_QueuedParallelLanes = JsonConvert.DeserializeObject<List<ParallelLane>>(json) ?? []));

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

			AddBinding(new TriggerBinding("skyplan", "clearErase", () => {
				m_EraseTarget = null;
				m_HighlightBinding.Update("");
			}));

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
			_handles.Clear();
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
			if (m_CurrentTool == Tools.polygon || m_CurrentTool == Tools.curve) _points.Add(world);
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

			if (m_ActiveShape.Type == Tools.curve) {
				// Always append the cursor as a tentative next point - the renderer falls back to
				// a straight line for any segment with no matching control yet, so pending-control
				// state gets a plain rubber-band toward the cursor; pending-anchor state gets the
				// real bend toward the cursor (the control is already locked).
				List<Vector3> previewPts = new(_points) { world };
				Shape temp = new() {
					id = "__preview__", Type = Tools.curve, layer = m_ActiveShape.layer,
					pts = previewPts, handles = new List<Vector3>(_handles),
					// Mirrors the queued parallel lanes onto the preview, same as the line case in
					// UpdatePreviewJson - shows the full corridor before the curve is even committed.
					ParallelLanes = [.. m_QueuedParallelLanes],
					ParallelSpacing = m_ParallelSpacing,
				};
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

			if (m_ActiveShape.Type == Tools.curve) {
				// Every other click is a control (locked-in, continuing the previous segment's
				// exit tangent) or an anchor (locks the segment the last control started) -
				// alternating, driven purely by which list is currently shorter.
				if (_handles.Count < _points.Count) {
					Vector3? prevControl = _handles.Count > 0 ? _handles[_handles.Count - 1] : (Vector3?)null;
					_handles.Add(CurveMath.ProjectControl(world, _points[_points.Count - 1], prevControl));
				} else {
					_points.Add(world);
				}
				return;
			}

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
		// only path/polygon/curve benefit; mid-draw feedback is already the moving preview shape itself.
		private void HandleDrawHover(float sx, float sy) {
			if (!m_Camera.IsReady || m_ActiveShape != null) return;
			if (m_CurrentTool != Tools.path && m_CurrentTool != Tools.polygon && m_CurrentTool != Tools.curve) {
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

			if (m_ActiveShape.Type == Tools.curve) {
				// Already complete (every locked control has its matching anchor) unless we're
				// either mid-segment (a control was placed, awaiting its anchor) or nothing has
				// locked yet (only the starting anchor exists, closes as a straight line) - both
				// cases resolve the final click as the awaited anchor.
				bool alreadyComplete = _handles.Count < _points.Count && _points.Count >= 2;
				if (!alreadyComplete && m_Camera.ScreenToWorld(sx, sy, out Vector3 finalWorld)) {
					ApplySnap(ref finalWorld, sx, sy);
					_points.Add(finalWorld);
				}
				m_ActiveShape.pts.Clear();
				m_ActiveShape.pts.AddRange(_points);
				m_ActiveShape.handles.Clear();
				m_ActiveShape.handles.AddRange(_handles);
				if (m_ActiveShape.pts.Count >= 2) {
					m_ActiveShape.CalcBounds();
					m_Shapes.Add(m_ActiveShape);
					PushUndo(new Op { type = OpType.Draw, shape = m_ActiveShape });
					// Curves can't use the line corridor's cheap dx/dy-offset lanes - a single rigid
					// translate is only exact for a straight line, not a bend (see CurveMath.
					// OffsetPolyline). Each extra queued layer instead becomes its own real, ordinary
					// Tools.curve Shape: sample this curve into a dense polyline, offset every sampled
					// point along its own local normal, store the result as pts with empty handles (the
					// curve renderer already falls back to straight segments with no matching handle,
					// so a dense all-straight polyline renders correctly with zero new rendering code).
					// No ParallelLanes/ParallelSpacing needed on these - they're ordinary independent
					// shapes, not lightweight render hints, so hover/erase/export/import/Shape Manager
					// grouping all already just work.
					if (m_QueuedParallelLanes.Count > 0) {
						List<Vector3> sampled = CurveMath.Sample(m_ActiveShape.pts, m_ActiveShape.handles);
						int laneIndex = 1;
						foreach (ParallelLane lane in m_QueuedParallelLanes) {
							Shape extraCurve = new() {
								id = $"s{m_NextId++}",
								Type = Tools.curve,
								layer = new LayerDefDto { Id = lane.LayerId },
								pts = CurveMath.OffsetPolyline(sampled, m_ParallelSpacing * laneIndex),
							};
							extraCurve.CalcBounds();
							m_Shapes.Add(extraCurve);
							PushUndo(new Op { type = OpType.Draw, shape = extraCurve });
							laneIndex++;
						}
					}
					if (m_Camera.IsReady) {
						UpdateShapesJson();
					}
				}
				m_ActiveShape = null;
				_points.Clear();
				_handles.Clear();
				m_PreviewBinding.Update("");
				return;
			}

			HandleDrawMove(sx, sy);
			if (m_ActiveShape.pts.Count >= 2) {
				// Each extra queued layer becomes its own real, independent Tools.path Shape (not a
				// shared render hint), so it can be erased/edited independently of the others.
				if (m_QueuedParallelLanes.Count > 0) {
					// Temporarily set purely so GetParallelLaneOffsets (perpendicular-normal math,
					// shared with the preview) can compute the world-space offsets - cleared below.
					m_ActiveShape.ParallelLanes = [.. m_QueuedParallelLanes];
					m_ActiveShape.ParallelSpacing = m_ParallelSpacing;
					foreach ((ParallelLane lane, Vector3 offset) in m_ActiveShape.GetParallelLaneOffsets()) {
						Shape extraLine = new() {
							id = $"s{m_NextId++}",
							Type = Tools.path,
							layer = new LayerDefDto { Id = lane.LayerId },
							pts = [m_ActiveShape.pts[0] + offset, m_ActiveShape.pts[1] + offset],
						};
						extraLine.CalcBounds();
						m_Shapes.Add(extraLine);
						PushUndo(new Op { type = OpType.Draw, shape = extraLine });
					}
					// Primary has no lane metadata of its own - it's an ordinary shape, the extras
					// above are independent Shapes.
					m_ActiveShape.ParallelLanes = [];
					m_ActiveShape.ParallelSpacing = 0f;
				}
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
					Tools.curve => Tag.curve,
					Tools.point => Tag.circle,
					Tools.text => Tag.text,
					_ => Tag.none
				}
			};
			foreach (Vector3 pt in shape.pts) {
				if (!m_Camera.WorldToSVG(pt, out Vector2 p)) return null;
				shapeDto.Pts.Add(new ScreenPt { x = p.x, y = p.y });
			}
			foreach (Vector3 h in shape.handles) {
				if (!m_Camera.WorldToSVG(h, out Vector2 hp)) return null;
				shapeDto.Handles.Add(new ScreenPt { x = hp.x, y = hp.y });
			}
			// Mid-draw preview only (the temp "__preview__" shape - see HandleDrawMove/HandleDrawEnd;
			// real committed line shapes never carry ParallelLanes, each lane is baked into its own
			// independent Shape at commit time, same as curves). One extra reprojected world point per
			// lane, converted to a screen-space translate delta relative to the line's own first anchor
			// - a straight line's perpendicular offset is a uniform vector everywhere along it, so a
			// single delta is exact for this live rubber-band preview.
			if (shape.Type == Tools.path && shape.pts.Count == 2 && shape.ParallelLanes.Count > 0
					&& m_Camera.WorldToSVG(shape.pts[0], out Vector2 anchorScreen)) {
				shapeDto.ParallelSpacing = shape.ParallelSpacing;
				foreach ((ParallelLane lane, Vector3 offset) in shape.GetParallelLaneOffsets()) {
					if (!m_Camera.WorldToSVG(shape.pts[0] + offset, out Vector2 offsetScreen)) continue;
					shapeDto.ParallelLanes.Add(new ParallelLaneDto {
						LayerId = lane.LayerId,
						Dx = offsetScreen.x - anchorScreen.x,
						Dy = offsetScreen.y - anchorScreen.y,
					});
				}
			}
			// Curve mid-draw preview only (the temp "__preview__" shape - see HandleDrawMove; real
			// committed curve lanes are ordinary Shapes and never carry ParallelLanes at all). Unlike
			// a line, a single dx/dy delta can't represent a curve's offset - each lane needs its own
			// full sampled+offset polyline, same math as the real commit path (CurveMath.Sample +
			// OffsetPolyline), just also reprojected to screen space here for display.
			if (shape.Type == Tools.curve && shape.ParallelLanes.Count > 0) {
				List<Vector3> sampled = CurveMath.Sample(shape.pts, shape.handles);
				int laneIndex = 1;
				foreach (ParallelLane lane in shape.ParallelLanes) {
					List<Vector3> offsetPts = CurveMath.OffsetPolyline(sampled, m_ParallelSpacing * laneIndex);
					PreviewCurveLaneDto laneDto = new() { LayerId = lane.LayerId };
					bool ok = true;
					foreach (Vector3 p in offsetPts) {
						if (!m_Camera.WorldToSVG(p, out Vector2 sp)) { ok = false; break; }
						laneDto.Pts.Add(new ScreenPt { x = sp.x, y = sp.y });
					}
					if (ok) shapeDto.PreviewCurveLanes.Add(laneDto);
					laneIndex++;
				}
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
				// Mirror the queued parallel lanes onto the preview too, so the rubber-band shows
				// the full corridor before the line is even committed.
				ParallelLanes = [.. m_QueuedParallelLanes],
				ParallelSpacing = m_ParallelSpacing,
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

		private static float LoadDisplaySettings(string key, float defaultValue) {
			try {
				string path = Paths.DisplaySettingsPath;
				if (!File.Exists(path)) return defaultValue;
				var json = JObject.Parse(File.ReadAllText(path));
				return json[key]?.Value<float>() ?? defaultValue;
			} catch (Exception ex) {
				Mod.log.Warn($"[Skyplan] Failed to load display settings: {ex.Message}");
				return defaultValue;
			}
		}

		private static void SaveSettings(string key, float value) {
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
