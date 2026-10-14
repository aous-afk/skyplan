using Skyplan.Models;
using Skyplan.Models.dto;
using System.Globalization;
using System.Text.RegularExpressions;
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
				if (el.Attribute("data-icon-glyph") != null) continue;
				Shape? s = ParsePathLike(el, ref nextId);
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
			foreach (XElement el in Descendants(root, "text")) {
				Shape? s = ParseText(el, ref nextId);
				if (s != null) shapes.Add(s);
			}
			return shapes;
		}

		// Handles both namespaced and plain elements
		private static IEnumerable<XElement> Descendants(XElement root, string localName) =>
			root.Descendants(SvgNs + localName).Concat(root.Descendants(localName));

		// Tools.path is just Tools.curve with zero controls - one parser for both, distinguished
		// by whether any "Q" command (and so any handles) turned up at all. Mirrors
		// SVGExporter.ExportPathLike's "data-y" list convention (not the old discrete
		// data-y0/data-y1 attrs a plain 2-point path used before path/curve export merged).
		private static Shape? ParsePathLike(XElement el, ref int nextId) {
			string? d = el.Attribute("d")?.Value?.Trim();
			if (string.IsNullOrEmpty(d)) return null;

			List<Vector3> pts = [];
			List<Vector3> handles = [];

			float[] yPts = [.. (el.Attribute("data-y")?.Value ?? "")
				.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(s => Attr(s) ?? 0f)];
			float[] yHandles = [.. (el.Attribute("data-handle-y")?.Value ?? "")
				.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(s => Attr(s) ?? 0f)];

			// A command may carry several coordinate sets ("L x1 y1,x2 y2,..." or "M x y x2 y2"): per the
			// SVG spec the extra sets repeat the command (after M they're implicit L). Other SVG tools
			// (e.g. city-roads) write whole polylines this way.
			foreach (Match m in Regex.Matches(d, @"([MLQ])\s*([^MLQ]*)")) {
				string cmd = m.Groups[1].Value;
				float[] nums = [.. m.Groups[2].Value
					.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
					.Select(t => float.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture))];

				if (cmd == "M" || cmd == "L") {
					for (int i = 0; i + 1 < nums.Length; i += 2) {
						float y = pts.Count < yPts.Length ? yPts[pts.Count] : 0f;
						pts.Add(new Vector3(nums[i], y, nums[i + 1]));
					}
				} else if (cmd == "Q") {
					for (int i = 0; i + 3 < nums.Length; i += 4) {
						float hy = handles.Count < yHandles.Length ? yHandles[handles.Count] : 0f;
						handles.Add(new Vector3(nums[i], hy, nums[i + 1]));
						float py = pts.Count < yPts.Length ? yPts[pts.Count] : 0f;
						pts.Add(new Vector3(nums[i + 2], py, nums[i + 3]));
					}
				}
			}
			if (pts.Count < 2) return null;

			return new Shape {
				id = $"s{nextId++}",
				Type = handles.Count > 0 ? Tools.curve : Tools.path,
				layer = ParseLayer(el),
				pts = pts,
				handles = handles,
				Label = el.Attribute("data-label")?.Value,
				Description = el.Attribute("data-description")?.Value,
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
			if (pts.Count >= 2 && Vector3.Distance(pts[0], pts[^1]) < 0.01f)
				pts.RemoveAt(pts.Count - 1);

			if (pts.Count < 3) return null;

			return new Shape {
				id = $"s{nextId++}",
				Type = Tools.polygon,
				layer = ParseLayer(el),
				pts = pts,
				Label = el.Attribute("data-label")?.Value,
				Description = el.Attribute("data-description")?.Value,
			};
		}

		private static Shape? ParseCircle(XElement el, ref int nextId) {
			float? cx = Attr(el, "cx"), cz = Attr(el, "cy");
			if (cx == null || cz == null) return null;
			float y = Attr(el, "data-y") ?? 0f;

			LayerDefDto layer = ParseLayer(el);
			XElement? iconEl = el.Parent?.Elements()
				.FirstOrDefault(e => e.Name.LocalName == "path" && e.Attribute("data-icon-glyph") != null);
			if (iconEl != null) {
				layer.Icon = new LayerIconDto {
					Path = iconEl.Attribute("d")?.Value ?? "",
					Color = iconEl.Attribute("fill")?.Value,
				};
			}

			return new Shape {
				id = $"s{nextId++}",
				Type = Tools.point,
				layer = layer,
				pts = [new Vector3(cx.Value, y, cz.Value)],
				Label = el.Attribute("data-label")?.Value,
				Description = el.Attribute("data-description")?.Value,
			};
		}

		private static Shape? ParseText(XElement el, ref int nextId) {
			float? x = Attr(el, "x"), z = Attr(el, "y");
			if (x == null || z == null) return null;
			string? label = el.Value?.Trim();
			if (string.IsNullOrEmpty(label)) return null;
			float y = Attr(el, "data-y") ?? 0f;
			return new Shape {
				id = $"s{nextId++}",
				Type = Tools.text,
				layer = ParseLayer(el),
				pts = [new Vector3(x.Value, y, z.Value)],
				Label = label,
				Description = el.Attribute("data-description")?.Value,
			};
		}

		// Shapes from SVGs not written by Skyplan have no data-layer; they share this layer, which
		// DrawingSystem adds to the layers config (LayerMerger.AppendMissingLayers) so it renders.
		public const string ImportedLayerId = "imported";

		private static readonly string[] StyleAttributes = [
			"stroke", "stroke-width", "stroke-opacity", "stroke-dasharray", "stroke-linecap", "stroke-linejoin",
			"fill", "fill-opacity", "opacity",
		];

		private static LayerDefDto ParseLayer(XElement el) {
			string? layerId = el.Attribute("data-layer")?.Value;
			return new LayerDefDto {
				Id = layerId ?? ImportedLayerId,
				Label = layerId ?? "Imported",
				Style = InheritedStyle(el),
			};
		}

		// Presentation attributes and style="" from the outermost ancestor down to the element, so
		// nearer values win, as in SVG itself (e.g. <g stroke="#1a1a1a"> around plain <path>s).
		private static Dictionary<string, string> InheritedStyle(XElement el) {
			Dictionary<string, string> style = [];
			foreach (XElement node in el.AncestorsAndSelf().Reverse()) {
				foreach (string name in StyleAttributes) {
					string? v = node.Attribute(name)?.Value;
					if (!string.IsNullOrWhiteSpace(v)) style[name] = v.Trim();
				}
				foreach (string part in (node.Attribute("style")?.Value ?? "").Split(';')) {
					int colon = part.IndexOf(':');
					if (colon > 0) style[part[..colon].Trim()] = part[(colon + 1)..].Trim();
				}
			}
			return style;
		}

		private static float? Attr(XElement el, string name) => Attr(el.Attribute(name)?.Value);
		private static float? Attr(string? s) {
			if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
			return null;
		}
	}
}
