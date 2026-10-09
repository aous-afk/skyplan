using Skyplan.Models;
using Skyplan.Models.dto;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Skyplan.Persistence {
	public static class SVGExporter {
		private const float DefaultHalfSize = 7168f;

		public static string Export(List<Shape> shapes, Func<string, LayerDefDto?>? layerLookup = null, float halfSize = DefaultHalfSize) {
			float vx = -halfSize, vz = -halfSize;
			float vw = halfSize * 2f,  vh = halfSize * 2f;

			StringBuilder sb = new();
			sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{F(vx)} {F(vz)} {F(vw)} {F(vh)}\">");
			sb.AppendLine("  <!-- Skyplan export: world XZ coordinates. x=east, y=north. -->");

			foreach (Shape shape in shapes) {
				LayerDefDto? layer = (shape.layer?.Id != null ? layerLookup?.Invoke(shape.layer.Id) : null) ?? shape.layer;
				string? elem = shape.Type switch {
					Tools.path    when shape.pts.Count >= 2 => ExportPathLike(shape, layer),
					Tools.curve   when shape.pts.Count >= 2 => ExportPathLike(shape, layer),
					Tools.polygon when shape.pts.Count >= 3 => ExportPolygon(shape, layer),
					Tools.point   when shape.pts.Count >= 1 => ExportCircle(shape, layer),
					Tools.text    when shape.pts.Count >= 1 => ExportText(shape),
					_ => null
				};
				if (elem != null) sb.AppendLine($"  {elem}");
			}

			sb.AppendLine("</svg>");
			return sb.ToString();
		}

		// Tools.path is just Tools.curve with zero controls (every segment falls to L) - one
		// exporter for both. handles[i] is the control for segment pts[i]->pts[i+1]; a segment
		// with no matching handles entry is a straight line.
		private static string ExportPathLike(Shape s, LayerDefDto? layer) {
			StringBuilder d = new();
			d.Append($"M {F(s.pts[0].x)} {F(s.pts[0].z)}");
			for (int i = 0; i < s.pts.Count - 1; i++) {
				Vector3 p = s.pts[i + 1];
				if (i < s.handles.Count) {
					Vector3 c = s.handles[i];
					d.Append($" Q {F(c.x)} {F(c.z)} {F(p.x)} {F(p.z)}");
				} else {
					d.Append($" L {F(p.x)} {F(p.z)}");
				}
			}
			string dataY = string.Join(",", s.pts.Select(p => F(p.y)));
			string dataHandleY = string.Join(",", s.handles.Select(h => F(h.y)));
			return $"<path d=\"{d}\"" +
			       $" data-layer=\"{s.layer?.Id}\" data-y=\"{dataY}\" data-handle-y=\"{dataHandleY}\"" +
			       $"{Meta(s)} fill=\"none\" style=\"{BuildStyle(layer)}\"/>";
		}

		private static string ExportPolygon(Shape s, LayerDefDto? layer) {
			string pts = string.Join(" ", s.pts.Select(p => $"{F(p.x)},{F(p.z)}"));
			string dataY = string.Join(",", s.pts.Select(p => F(p.y)));
			return $"<polygon points=\"{pts}\"" +
			       $" data-layer=\"{s.layer?.Id}\" data-y=\"{dataY}\"" +
			       $"{Meta(s)} style=\"{BuildStyle(layer)}\"/>";
		}

		// Icon is authored in screen-space pixels (fixed visual size regardless of zoom) - the
		// export has no camera/zoom, so it needs a fixed world-unit size instead. Reuses the same
		// ratio the point marker already established: r=6 (screen px, in-game) -> r=100 (world
		// units, exported), so the icon stays proportioned to the circle the same way in both.
		private const float IconWorldScale = 100f / 6f;

		private static string ExportCircle(Shape s, LayerDefDto? layer) {
			Vector3 p = s.pts[0];
			string circle = $"<circle cx=\"{F(p.x)}\" cy=\"{F(p.z)}\" r=\"100\"" +
			       $" data-layer=\"{s.layer?.Id}\" data-y=\"{F(p.y)}\"" +
			       $"{Meta(s)} style=\"{BuildStyle(layer)}\"/>";
			if (layer?.Icon == null) return circle;
			string color = Esc(layer.Icon.Color ?? "black");
			string icon = $"<path d=\"{layer.Icon.Path}\" data-icon-glyph=\"true\"" +
			       $" transform=\"translate({F(p.x)},{F(p.z)}) scale({F(IconWorldScale)})\"" +
			       $" fill=\"{color}\"/>";
			return $"<g>{circle}{icon}</g>";
		}

		private static string ExportText(Shape s) {
			Vector3 p = s.pts[0];
			string text = Esc(s.Label ?? "");
			return $"<text x=\"{F(p.x)}\" y=\"{F(p.z)}\"" +
			       $" data-layer=\"{s.layer?.Id}\" data-y=\"{F(p.y)}\"" +
			       $"{Meta(s)}>{text}</text>";
		}

		private static string Meta(Shape s) {
			string result = "";
			if (!string.IsNullOrEmpty(s.Label)) {
			  result += $" data-label=\"{Esc(s.Label)}\"";
			}
			if (!string.IsNullOrEmpty(s.Description)) result += $" data-description=\"{Esc(s.Description)}\"";
			return result;
		}

		private static string Esc(string v) =>
			v.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

		private static string BuildStyle(LayerDefDto? layer) {
			if (layer?.Style == null) return "stroke:#ffffff;stroke-width:2";
			return string.Join(";", layer.Style.Select(kv => {
				string key = Regex.Replace(kv.Key, "([A-Z])", "-$1").ToLower();
				return $"{key}:{kv.Value}";
			}));
		}

		private static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
	}
}
