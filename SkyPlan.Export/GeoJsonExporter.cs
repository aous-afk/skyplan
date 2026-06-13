using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Skyplan.Models;

namespace SkyPlan.Export {
	public static class GeoJsonExporter {
		public static string Export(List<Shape> shapes, int srid, double originX, double originY) {
			var sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine("  \"type\": \"FeatureCollection\",");

			if (srid != 4326) {
				sb.AppendLine($"  \"crs\": {{ \"type\": \"name\", \"properties\": {{ \"name\": \"urn:ogc:def:crs:EPSG::{srid}\" }} }},");
			}

			sb.AppendLine("  \"features\": [");

			for (int i = 0; i < shapes.Count; i++) {
				AppendFeature(sb, shapes[i], srid, originX, originY);
				if (i < shapes.Count - 1) sb.Append(",");
				sb.AppendLine();
			}

			sb.AppendLine("  ]");
			sb.Append("}");
			return sb.ToString();
		}

		private static void AppendFeature(StringBuilder sb, Shape s, int srid, double originX, double originY) {
			string geomType = s.type == "rect" ? "Polygon" : "LineString";

			sb.AppendLine("    {");
			sb.AppendLine("      \"type\": \"Feature\",");
			sb.AppendLine($"      \"properties\": {{ \"id\": \"{s.id}\", \"type\": \"{s.type}\", \"layer\": \"{s.layer}\" }},");
			sb.AppendLine($"      \"geometry\": {{");
			sb.AppendLine($"        \"type\": \"{geomType}\",");

			if (s.type == "rect") {
				sb.Append("        \"coordinates\": [[");
				var corners = RectCorners(s, srid, originX, originY);
				for (int i = 0; i < corners.Count; i++) {
					sb.Append(Coord(corners[i].x, corners[i].y));
					if (i < corners.Count - 1) sb.Append(", ");
				}
				sb.AppendLine("]]");
			} else {
				sb.Append("        \"coordinates\": [");
				for (int i = 0; i < s.pts.Count; i++) {
					var (x, y) = CoordinateConverter.Convert(s.pts[i].x, s.pts[i].z, srid, originX, originY);
					sb.Append(Coord(x, y));
					if (i < s.pts.Count - 1) sb.Append(", ");
				}
				sb.AppendLine("]");
			}

			sb.AppendLine("      }");
			sb.Append("    }");
		}

		private static List<(double x, double y)> RectCorners(Shape s, int srid, double originX, double originY) {
			float x1 = s.pts[0].x, z1 = s.pts[0].z;
			float x2 = s.pts[1].x, z2 = s.pts[1].z;

			var c1 = CoordinateConverter.Convert(x1, z1, srid, originX, originY);
			var c2 = CoordinateConverter.Convert(x2, z1, srid, originX, originY);
			var c3 = CoordinateConverter.Convert(x2, z2, srid, originX, originY);
			var c4 = CoordinateConverter.Convert(x1, z2, srid, originX, originY);

			return new List<(double, double)> { c1, c2, c3, c4, c1 };
		}

		private static string Coord(double x, double y) =>
			$"[{x.ToString("F6", CultureInfo.InvariantCulture)}, {y.ToString("F6", CultureInfo.InvariantCulture)}]";
	}
}
