using Skyplan.Models;
using Skyplan.Models.dto;
using System.Globalization;
using System.Xml.Linq;
using UnityEngine;

namespace Skyplan.Persistence {
	public static class SVGImporter {
		private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";

		public static List<Shape> Import(string svgContent, ref int nextId) {
			List<Shape> shapes = [];
			XDocument doc = XDocument.Parse(svgContent);
			XElement root = doc.Root;

			foreach (XElement el in Descendants(root, "path")) {
				Shape? s = ParsePath(el, ref nextId);
				if (s != null) shapes.Add(s);
			}
			foreach (XElement el in Descendants(root, "polygon")) {
				Shape? s = ParsePolygon(el, ref nextId);
				if (s != null) shapes.Add(s);
			}
			foreach (XElement el in Descendants(root, "circle")) {
				Shape? s = ParseCircle(el, ref nextId);
				if (s != null) shapes.Add(s);
			}
			return shapes;
		}

		// Handles both namespaced and plain elements
		private static IEnumerable<XElement> Descendants(XElement root, string localName) =>
			root.Descendants(SvgNs + localName).Concat(root.Descendants(localName));

		private static Shape? ParsePath(XElement el, ref int nextId) {
			string? d = el.Attribute("d")?.Value?.Trim();
			if (string.IsNullOrEmpty(d)) return null;

			// Parse "M x1 z1 L x2 z2" — extract 4 numeric tokens, skip M/L commands
			float[] nums = d.Split(new char[]{' ', ',', '\t', '\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)
				.Where(t => float.TryParse(t, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out _))
				.Select(t => float.Parse(t, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture))
				.ToArray();
			if (nums.Length < 4) return null;

			float y0 = Attr(el, "data-y0") ?? 0f;
			float y1 = Attr(el, "data-y1") ?? 0f;

			return new Shape {
				id = $"s{nextId++}",
				type = "path",
				layer = ParseLayer(el),
				pts = [
					new Vector3(nums[0], y0, nums[1]),
					new Vector3(nums[2], y1, nums[3]),
				]
			};
		}

		private static Shape? ParsePolygon(XElement el, ref int nextId) {
			string? pointsStr = el.Attribute("points")?.Value;

			// the compiler was complaining about it, that pointsStr is possibly null
			// thats why check if it is null before check if it is NullOrWhiteSpaces again
			if (pointsStr is null || string.IsNullOrWhiteSpace(pointsStr)) {
				return null;
			}

			float[] elevations = el.Attribute("data-y")?.Value
				.Split(',')
				.Select(s => Attr(s) ?? 0f)
				.ToArray() ?? [];

			List<Vector3> pts = [];
			string[] pairs = pointsStr.Trim()
					.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

			for (int i = 0; i < pairs.Length; i++) {
				string[] xy = pairs[i].Split(',');
				if (xy.Length < 2) continue;
				float? x = Attr(xy[0]), z = Attr(xy[1]);
				if (x == null || z == null) continue;
				float y = i < elevations.Length ? elevations[i] : 0f;
				pts.Add(new Vector3(x.Value, y, z.Value));
			}

			// Remove duplicate closing point added by exporter
			if (pts.Count >= 2 && Vector3.Distance(pts[0], pts[pts.Count - 1]) < 0.01f)
				pts.RemoveAt(pts.Count - 1);

			if (pts.Count < 3) return null;

			return new Shape {
				id = $"s{nextId++}",
				type = "polygon",
				layer = ParseLayer(el),
				pts = pts,
			};
		}

		private static Shape? ParseCircle(XElement el, ref int nextId) {
			float? cx = Attr(el, "cx"), cz = Attr(el, "cy");
			if (cx == null || cz == null) return null;
			float y = Attr(el, "data-y") ?? 0f;
			return new Shape {
				id = $"s{nextId++}",
				type = "point",
				layer = ParseLayer(el),
				pts = [new Vector3(cx.Value, y, cz.Value)],
			};
		}

		private static LayerDefDto ParseLayer(XElement el) {
			string id = el.Attribute("data-layer")?.Value ?? "unknown";
			Dictionary<string, string> style = [];
			foreach (string? part in (el.Attribute("style")?.Value ?? "").Split(';')) {
				int colon = part.IndexOf(':');
				if (colon > 0) {
					style[part.Substring(0, colon).Trim()] = part.Substring(colon + 1).Trim();
				}
			}
			return new LayerDefDto { Id = id, Label = id, Style = style };
		}

		private static float? Attr(XElement el, string name) => Attr(el.Attribute(name)?.Value);
		private static float? Attr(string? s) {
			if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
			return null;
		}
	}
}
