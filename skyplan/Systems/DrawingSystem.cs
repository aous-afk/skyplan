using Colossal.UI;
using Colossal.UI.Binding;
using Game;
using Game.SceneFlow;
using Game.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace skyplan.Systems {
	public partial class DrawingSystem : UISystemBase {
		public static DrawingSystem instance;
		private bool m_PanelVisible;

		private bool m_HasBaseline;
		private Matrix4x4 m_LastViewMatrix;

		private class Shape {
			public string id;
			public string type;   // line | rect | circle | free | erase
			public string layer;  // roads | zoning | transit | notes
			public List<Vector2> pts = new();
		}

		private enum OpType { Draw, Delete, ClearLayer }
		private class Op {
			public OpType type;
			public Shape shape;
			public string layer;
			public List<Shape> cleared;
		}

		private readonly List<Shape> m_Shapes = new List<Shape>();
		private readonly List<Op> m_UndoStack = new List<Op>();
		private Shape m_ActiveShape;
		private string m_CurrentTool = "line";
		private string m_CurrentLayer = "roads";
		private int m_NextId;
		private string m_EraseTarget;

		private ValueBinding<bool> m_PanelVisibleBinding;
		private ValueBinding<string> m_ShapesBinding;
		private ValueBinding<string> m_PreviewBinding;
		private ValueBinding<string> m_HighlightBinding;

		protected override void OnCreate() {
			base.OnCreate();
			instance = this;
			Mod.log.Info("DrawingSystem.OnCreate");

			m_PanelVisibleBinding = new ValueBinding<bool>("skyplan", "panelVisible", false);
			m_ShapesBinding = new ValueBinding<string>("skyplan", "shapes", "[]");
			m_PreviewBinding = new ValueBinding<string>("skyplan", "preview", "");
			m_HighlightBinding = new ValueBinding<string>("skyplan", "highlight", "");

			AddBinding(m_PanelVisibleBinding);
			AddBinding(m_ShapesBinding);
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

		/// <summary>
		/// Toggles the visibility of the Skyplan panel, updating related state and bindings accordingly.
		/// </summary>
		/// <remarks>When the panel is shown, the method updates the baseline view matrix and shape data if a camera
		/// is available. When the panel is hidden, it clears the active shape and resets the preview binding. The method also
		/// updates the panel visibility binding and logs the visibility change.</remarks>
		public void TogglePanel() {
			m_PanelVisible = !m_PanelVisible;
			if (m_PanelVisible) {
				Camera cam = GetCamera();
				if (cam != null) {
					m_LastViewMatrix = cam.worldToCameraMatrix;
					m_HasBaseline = true;
					UpdateShapesJson(cam);
				}
			} else {
				m_ActiveShape = null;
				m_PreviewBinding.Update("");
			}
			m_PanelVisibleBinding.Update(m_PanelVisible);
			Mod.log.Info($"Skyplan panel {(m_PanelVisible ? "shown" : "hidden")}");
		}

		/// <summary>
		/// Retrieves the camera used for rendering the default UI view.
		/// </summary>
		/// <remarks>This method prioritizes the camera assigned to the default UI system's view. If no such camera is
		/// set, it falls back to the main camera. This is useful for ensuring UI rendering is always associated with an
		/// active camera.</remarks>
		/// <returns>The camera instance associated with the default UI view if available; otherwise, the main camera in the scene.</returns>
		private static Camera GetCamera() =>
			UIManager.defaultUISystem.defaultUIView.RenderingCamera ?? Camera.main;

		/// <summary>
		/// Converts a world space position to SVG coordinate space using the specified camera.
		/// </summary>
		/// <remarks>The resulting coordinates use the SVG convention with the y-axis increasing downward, matching
		/// typical SVG rendering behavior.</remarks>
		/// <param name="cam">The camera used to project the world position to screen coordinates.</param>
		/// <param name="world">The position in world space to convert.</param>
		/// <returns>A 2D vector representing the position in SVG coordinate space, where the origin is at the top-left corner.</returns>
		private static Vector2 WorldToSVG(Camera cam, Vector3 world) {
			Vector3 s = cam.WorldToScreenPoint(world);
			return new Vector2(s.x, cam.pixelHeight - s.y);
		}

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
			if (!m_HasBaseline) return;
			Camera cam = GetCamera();
			if (cam == null) return;
			if (cam.worldToCameraMatrix == m_LastViewMatrix) return;
			m_LastViewMatrix = cam.worldToCameraMatrix;
			UpdateShapesJson(cam);
			if (m_ActiveShape != null) UpdatePreviewJson(cam);
		}

		/// <summary>
		/// Begins a new drawing or erasing operation at the specified screen coordinates.
		/// </summary>
		/// <remarks>If the current tool is set to erase, the method erases the nearest shape instead of starting a
		/// new drawing. The operation is ignored if the camera is unavailable or if the screen coordinates cannot be
		/// converted to world coordinates.</remarks>
		/// <param name="sx">The horizontal screen coordinate where the draw or erase action is initiated.</param>
		/// <param name="sy">The vertical screen coordinate where the draw or erase action is initiated.</param>
		private void HandleDrawStart(float sx, float sy) {
			Camera cam = GetCamera();
			if (cam == null) return;
			if (m_CurrentTool == "erase") { EraseNearest(cam); return; }
			if (!ScreenToWorldXZ(cam, sx, sy, out Vector2 xz)) return;
			m_ActiveShape = new Shape {
				id = $"s{m_NextId++}",
				type = m_CurrentTool,
				layer = m_CurrentLayer,
			};
			m_ActiveShape.pts.Add(xz);
		}

		/// <summary>
		/// Handles the drawing logic for the active shape in response to a pointer move event at the specified screen
		/// coordinates.
		/// </summary>
		/// <remarks>This method updates the points of the active shape as the user moves the pointer, allowing for
		/// interactive shape creation or modification. The behavior may differ depending on the type of shape being
		/// drawn.</remarks>
		/// <param name="sx">The X-coordinate of the pointer position in screen space.</param>
		/// <param name="sy">The Y-coordinate of the pointer position in screen space.</param>
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
			UpdatePreviewJson(cam);
		}

		/// <summary>
		/// Completes the drawing operation for the active shape using the specified screen coordinates.
		/// </summary>
		/// <remarks>If the active shape contains at least two points, it is finalized and added to the collection of
		/// shapes. The method also updates the undo stack and shape data as needed.</remarks>
		/// <param name="sx">The X-coordinate, in screen space, where the drawing operation ends.</param>
		/// <param name="sy">The Y-coordinate, in screen space, where the drawing operation ends.</param>
		private void HandleDrawEnd(float sx, float sy) {
			if (m_ActiveShape == null) return;
			HandleDrawMove(sx, sy);
			Camera cam = GetCamera();
			if (m_ActiveShape.pts.Count >= 2) {
				m_Shapes.Add(m_ActiveShape);
				m_UndoStack.Add(new Op { type = OpType.Draw, shape = m_ActiveShape });
				if (cam != null) UpdateShapesJson(cam);
			}
			m_ActiveShape = null;
			m_PreviewBinding.Update("");
		}

		/// <summary>
		/// Removes all shapes from the specified layer and updates the application state accordingly.
		/// </summary>
		/// <remarks>This method also updates the undo stack to allow restoration of the cleared shapes and resets the
		/// active shape if it belongs to the cleared layer. The method triggers updates to the camera and preview binding to
		/// reflect the changes.</remarks>
		/// <param name="layer">The name of the layer from which all shapes will be removed. Cannot be null or empty.</param>
		private void HandleClearLayer(string layer) {
			var removed = m_Shapes.FindAll(s => s.layer == layer);
			if (removed.Count > 0)
				m_UndoStack.Add(new Op { type = OpType.ClearLayer, layer = layer, cleared = removed });
			m_Shapes.RemoveAll(s => s.layer == layer);
			if (m_ActiveShape != null && m_ActiveShape.layer == layer)
				m_ActiveShape = null;
			Camera cam = GetCamera();
			if (cam != null) UpdateShapesJson(cam);
			m_PreviewBinding.Update("");
		}

		/// <summary>
		/// Reverts the most recent operation performed, restoring the previous state of the shapes collection.
		/// </summary>
		/// <remarks>This method undoes the last draw, delete, or clear layer action. If there are no operations to
		/// undo, the method has no effect. After undoing, the shapes data is updated if a camera is available.</remarks>
		private void HandleUndo() {
			if (m_UndoStack.Count == 0) return;
			Op op = m_UndoStack[m_UndoStack.Count - 1];
			m_UndoStack.RemoveAt(m_UndoStack.Count - 1);
			switch (op.type) {
				case OpType.Draw: m_Shapes.Remove(op.shape); break;
				case OpType.Delete: m_Shapes.Add(op.shape); break;
				case OpType.ClearLayer: m_Shapes.AddRange(op.cleared); break;
			}
			Camera cam = GetCamera();
			if (cam != null) UpdateShapesJson(cam);
		}

		/// <summary>
		/// Calculates the centroid of a shape in screen coordinates using the specified camera.
		/// </summary>
		/// <remarks>The centroid is computed by averaging the screen positions of all points in the shape as
		/// projected by the camera.</remarks>
		/// <param name="cam">The camera used to convert world coordinates to screen coordinates.</param>
		/// <param name="s">The shape whose centroid is to be calculated. Must contain at least one point.</param>
		/// <returns>A Vector2 representing the centroid of the shape in screen coordinates.</returns>
		private static Vector2 ShapeScreenCentroid(Camera cam, Shape s) {
			Vector2 sum = Vector2.zero;
			foreach (var p in s.pts)
				sum += WorldToSVG(cam, new Vector3(p.x, 0, p.y));
			return sum / s.pts.Count;
		}

		/// <summary>
		/// Handles hover detection for the erase tool by identifying the nearest shape to the specified screen coordinates
		/// and updating the erase target highlight accordingly.
		/// </summary>
		/// <remarks>If no shape is within a predefined threshold distance from the cursor, the erase target is
		/// cleared and the highlight is removed.</remarks>
		/// <param name="sx">The X coordinate of the cursor position in screen space.</param>
		/// <param name="sy">The Y coordinate of the cursor position in screen space.</param>
		private void HandleEraseHover(float sx, float sy) {
			Camera cam = GetCamera();
			if (cam == null) return;
			const float Threshold = 80f;
			Vector2 cursor = new Vector2(sx, sy);
			float best = float.MaxValue;
			string found = null;
			foreach (var s in m_Shapes) {
				float d = Vector2.Distance(ShapeScreenCentroid(cam, s), cursor);
				if (d < best) { best = d; found = s.id; }
			}
			string newTarget = (found != null && best <= Threshold) ? found : null;
			if (newTarget == m_EraseTarget) return;
			m_EraseTarget = newTarget;
			m_HighlightBinding.Update(m_EraseTarget ?? "");
		}

		/// <summary>
		/// Removes the shape nearest to the current erase target and updates the shape collection and related state.
		/// </summary>
		/// <remarks>If there is no current erase target or the target shape cannot be found, the method performs no
		/// action. The operation is recorded for undo functionality, and the shape data is refreshed to reflect the
		/// removal.</remarks>
		/// <param name="cam">The camera context used to update the shape data after erasure.</param>
		private void EraseNearest(Camera cam) {
			if (m_EraseTarget == null) return;
			Shape target = m_Shapes.Find(s => s.id == m_EraseTarget);
			if (target == null) return;
			m_UndoStack.Add(new Op { type = OpType.Delete, shape = target });
			m_Shapes.Remove(target);
			m_EraseTarget = null;
			m_HighlightBinding.Update("");
			UpdateShapesJson(cam);
		}

		/// <summary>
		/// Returns the hexadecimal color code associated with the specified map layer name.
		/// </summary>
		/// <remarks>This method is typically used to assign consistent colors to different map layers in a
		/// visualization or UI. The returned color codes are intended for use in web or graphical applications that accept
		/// hexadecimal color values.</remarks>
		/// <param name="layer">The name of the map layer for which to retrieve the color code. Common values include "roads", "zoning",
		/// "transit", and "notes". If the value does not match a known layer, a default color is returned.</param>
		/// <returns>A string containing the hexadecimal color code for the specified layer. Returns "#ffffff" if the layer name is not
		/// recognized.</returns>
		private static string LayerColor(string layer) {
			return layer switch {
				"roads" => "#ff4444",
				"zoning" => "#44dd44",
				"transit" => "#4488ff",
				"notes" => "#ffcc00",
				_ => "#ffffff",
			};
		}

		/// <summary>
		/// Converts the specified shape to a JSON string representation suitable for SVG rendering.
		/// </summary>
		/// <remarks>Supported shape types include line, rectangle, circle, and freeform path. The returned JSON
		/// includes SVG attributes such as coordinates, stroke color, and shape type. The method returns null if the shape
		/// does not have enough points or if the type is not recognized.</remarks>
		/// <param name="cam">The camera used to transform world coordinates to SVG coordinates.</param>
		/// <param name="s">The shape to convert to a JSON representation. Must have at least two points for supported shape types.</param>
		/// <returns>A JSON string representing the shape in SVG-compatible format, or null if the shape type is unsupported or has
		/// insufficient points.</returns>
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
						Vector2 a = WorldToSVG(cam, new Vector3(s.pts[0].x, 0, s.pts[0].y));
						Vector2 b = WorldToSVG(cam, new Vector3(s.pts[1].x, 0, s.pts[0].y));
						Vector2 c2 = WorldToSVG(cam, new Vector3(s.pts[1].x, 0, s.pts[1].y));
						Vector2 d = WorldToSVG(cam, new Vector3(s.pts[0].x, 0, s.pts[1].y));
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
						var path = new StringBuilder("M ");
						for (int i = 0; i < s.pts.Count; i++) {
							Vector2 p = WorldToSVG(cam, new Vector3(s.pts[i].x, 0, s.pts[i].y));
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

		/// <summary>
		/// Updates the JSON representation of all shapes using the specified camera for coordinate conversion.
		/// </summary>
		/// <remarks>This method serializes all shapes in the collection to a single JSON array and updates the data
		/// binding with the result. Shapes that cannot be converted to JSON are skipped.</remarks>
		/// <param name="cam">The camera used to convert shape coordinates when generating the JSON representation. Cannot be null.</param>
		private void UpdateShapesJson(Camera cam) {
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
			m_ShapesBinding.Update(sb.ToString());
		}

		/// <summary>
		/// Updates the preview JSON representation of the currently active shape using the specified camera.
		/// </summary>
		/// <remarks>If there is no active shape or the active shape contains fewer than two points, the preview is
		/// cleared. Otherwise, the preview is updated to reflect the current state of the active shape as seen by the
		/// specified camera.</remarks>
		/// <param name="cam">The camera to use when generating the preview JSON for the active shape.</param>
		private void UpdatePreviewJson(Camera cam) {
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
			m_PreviewBinding.Update(ShapeToJSON(cam, temp) ?? "");
		}

		/// <summary>
		/// Converts the specified floating-point value to its string representation with one decimal place using the
		/// invariant culture.
		/// </summary>
		/// <param name="v">The floating-point value to convert to a string.</param>
		/// <returns>A string representation of the value with one digit after the decimal point, formatted using the invariant
		/// culture.</returns>
		private static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

		/// <summary>
		/// Parses a comma-separated string containing two floating-point values into a Vector2 instance.
		/// </summary>
		/// <remarks>The input string must contain exactly two values separated by a comma, with no extra whitespace.
		/// Both values must be valid floating-point numbers in invariant culture format. An exception is thrown if the input
		/// is not in the expected format.</remarks>
		/// <param name="csv">A string in the format "x,y" where x and y are floating-point numbers, using invariant culture formatting.</param>
		/// <returns>A Vector2 whose X and Y components are set to the parsed values from the input string.</returns>
		private static Vector2 CSV2(string csv) {
			var p = csv.Split(',');
			return new Vector2(
				float.Parse(p[0], CultureInfo.InvariantCulture),
				float.Parse(p[1], CultureInfo.InvariantCulture));
		}
	}
}
