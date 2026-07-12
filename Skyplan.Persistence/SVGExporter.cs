using Skyplan.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Skyplan.Persistence {
	public static class SVGExporter {
		private const float DefaultHalfSize = 7168f;

		public static string Export(List<Shape> shapes, float halfSize = DefaultHalfSize) {
			float vx = -halfSize, vz = -halfSize;
			float vw = halfSize * 2f,  vh = halfSize * 2f;

			var sb = new StringBuilder();
			sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{F(vx)} {F(vz)} {F(vw)} {F(vh)}\">");
			sb.AppendLine("  <!-- Skyplan export: world XZ coordinates. x=east, y=north. -->");

			foreach (Shape shape in shapes) {
				string? elem = shape.type switch {
					"path"    when shape.pts.Count >= 2 => ExportPath(shape),
					"polygon" when shape.pts.Count >= 3 => ExportPolygon(shape),
					"point"   when shape.pts.Count >= 1 => ExportCircle(shape),
					_ => null
				};
				if (elem != null) sb.AppendLine($"  {elem}");
			}

			sb.AppendLine("</svg>");
			return sb.ToString();
		}

		private static string ExportPath(Shape s) {
			var p0 = s.pts[0];
			var p1 = s.pts[1];
			return $"<path d=\"M {F(p0.x)} {F(p0.z)} L {F(p1.x)} {F(p1.z)}\"" +
			       $" data-layer=\"{s.layer?.Id}\" data-y0=\"{F(p0.y)}\" data-y1=\"{F(p1.y)}\"" +
			       $" fill=\"none\" style=\"{BuildStyle(s)}\"/>";
		}

		private static string ExportPolygon(Shape s) {
			string pts = string.Join(" ", s.pts.Select(p => $"{F(p.x)},{F(p.z)}"));
			string dataY = string.Join(",", s.pts.Select(p => F(p.y)));
			return $"<polygon points=\"{pts}\"" +
			       $" data-layer=\"{s.layer?.Id}\" data-y=\"{dataY}\"" +
			       $" style=\"{BuildStyle(s)}\"/>";
		}

		private static string ExportCircle(Shape s) {
			var p = s.pts[0];
			return $"<circle cx=\"{F(p.x)}\" cy=\"{F(p.z)}\" r=\"100\"" +
			       $" data-layer=\"{s.layer?.Id}\" data-y=\"{F(p.y)}\"" +
			       $" style=\"{BuildStyle(s)}\"/>";
		}

		private static string BuildStyle(Shape s) {
			if (s.layer?.Style == null) return "stroke:#ffffff;stroke-width:2";
			return string.Join(";", s.layer.Style.Select(kv => {
				string key = Regex.Replace(kv.Key, "([A-Z])", "-$1").ToLower();
				return $"{key}:{kv.Value}";
			}));
		}

		private static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
	}
}
