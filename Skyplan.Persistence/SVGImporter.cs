using Skyplan.Models;
using Skyplan.Models.dto;
using Skyplan.Persistence.Helpers;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Skyplan.Persistence {
	public static class SVGImporter {
		private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";

		public static Action<string>? LogWarn;

		// A file of thousands of bad paths (city-roads) would otherwise flood the log.
		private const int MaxLoggedFailures = 5;

		// One element can give several shapes (a <path> with several subpaths), or none
		private delegate List<Shape> ElementParser(XElement el);

		public static List<Shape> Import(string svgContent, ref int nextId, out int skipped) {
			List<Shape> shapes = [];
			XDocument doc = XDocument.Parse(svgContent, LoadOptions.SetLineInfo);
			XElement root = doc.Root;
			// Annotated on the document so InheritedStyle can find it from any element without every
			// parser passing it along
			doc.AddAnnotation(ReadStylesheet(root));

			skipped = 0;
			skipped += ParseEach(root, "path", ParsePathLike, shapes, skipped);
			skipped += ParseEach(root, "polygon", el => One(ParsePolygon(el)), shapes, skipped);
			skipped += ParseEach(root, "circle", el => One(ParseCircle(el)), shapes, skipped);
			skipped += ParseEach(root, "text", el => One(ParseText(el)), shapes, skipped);

			foreach (Shape s in shapes) s.id = $"s{nextId++}";
			return shapes;
		}

		private static List<Shape> One(Shape? s) => s == null ? [] : [s];

		// One element that fails to parse is skipped, not allowed to abort the whole import.
		private static int ParseEach(XElement root, string localName, ElementParser parse, List<Shape> shapes, int skippedSoFar) {
			int skipped = 0;
			foreach (XElement el in Descendants(root, localName)) {
				try {
					shapes.AddRange(parse(el));
				} catch (Exception ex) {
					if (skippedSoFar + skipped < MaxLoggedFailures) {
						int line = ((IXmlLineInfo)el).LineNumber;
						LogWarn?.Invoke($"[SVGImporter] skipped <{localName}> at line {line}: {ex.GetType().Name}: {ex.Message}");
					}
					skipped++;
				}
			}
			return skipped;
		}

		// Handles both namespaced and plain elements
		private static IEnumerable<XElement> Descendants(XElement root, string localName) =>
			root.Descendants(SvgNs + localName).Concat(root.Descendants(localName));

		// Tools.path is just Tools.curve with zero controls - one parser for both, distinguished
		// by whether any "Q" command (and so any handles) turned up at all. Mirrors
		// SVGExporter.ExportPathLike's "data-y" list convention (not the old discrete
		// data-y0/data-y1 attrs a plain 2-point path used before path/curve export merged).
		//
		// Each subpath becomes its own shape. A closed (Z) subpath that is filled becomes a polygon;
		// Skyplan's own exports never close a path and always set fill="none".
		private static List<Shape> ParsePathLike(XElement el) {
			if (el.Attribute("data-icon-glyph") != null) return [];
			string? d = el.Attribute("d")?.Value;
			if (d is null || string.IsNullOrWhiteSpace(d)) return [];

			// data-y has one height per point and data-handle-y one per Q control, both in path order
			// across all subpaths.
			float[] yPts = Floats(el.Attribute("data-y")?.Value);
			float[] yHandles = Floats(el.Attribute("data-handle-y")?.Value);
			int ptIndex = 0, handleIndex = 0;

			// Unset fill is black in SVG, so only an explicit "none" counts as unfilled
			bool filled = !InheritedStyle(el).TryGetValue("fill", out string? fill) || fill != "none";

			List<Shape> shapes = [];
			foreach (SvgSubpath sub in SvgPathParser.Parse(d)) {
				List<Vector3> pts = [];
				foreach (SvgPoint p in sub.Points) pts.Add(At(p, Get(yPts, ptIndex++)));

				List<Vector3?> controls = [];
				foreach (SvgPoint? c in sub.Controls)
					controls.Add(c is SvgPoint cp ? At(cp, Get(yHandles, handleIndex++)) : null);

				Shape? shape = sub.Closed && filled ? ClosedToPolygon(pts, controls) : OpenToPath(pts, controls, sub.Closed);
				if (shape == null) continue;
				shape.layer = ParseLayer(el);
				shape.Label = el.Attribute("data-label")?.Value;
				shape.Description = el.Attribute("data-description")?.Value;
				shapes.Add(shape);
			}
			return shapes;
		}

		// controls[i] is the control of segment i, null when straight
		private static Shape? OpenToPath(List<Vector3> pts, List<Vector3?> controls, bool closed) {
			// The closing segment is straight and comes after every curved one, so it needs no handle
			if (closed && pts.Count > 1 && !Same(pts[^1], pts[0])) pts.Add(pts[0]);
			if (pts.Count < 2) return null;

			// handles[i] belongs to segment i, so a straight segment before a curved one needs a
			// handle too: the midpoint keeps it straight. Trailing straight segments need none.
			List<Vector3> handles = [];
			int lastCurved = controls.FindLastIndex(c => c != null);
			for (int i = 0; i <= lastCurved; i++)
				handles.Add(controls[i] ?? (pts[i] + pts[i + 1]) * 0.5f);

			return new Shape {
				Type = handles.Count > 0 ? Tools.curve : Tools.path,
				pts = pts,
				handles = handles,
			};
		}

		// Polygons have no curved edges, so curved segments are sampled into straight pieces
		private const int PolygonCurveSteps = 8;

		private static Shape? ClosedToPolygon(List<Vector3> pts, List<Vector3?> controls) {
			List<Vector3> outline = [];
			for (int i = 0; i < pts.Count; i++) {
				outline.Add(pts[i]);
				if (i >= controls.Count || controls[i] is not Vector3 c) continue;
				Vector3 a = pts[i], b = pts[i + 1];
				for (int s = 1; s < PolygonCurveSteps; s++) {
					float t = (float)s / PolygonCurveSteps, u = 1f - t;
					outline.Add((u * u * a) + (2f * u * t * c) + (t * t * b));
				}
			}
			// A curve back into the start point ends on it; a polygon closes itself
			if (outline.Count > 1 && Same(outline[^1], outline[0])) outline.RemoveAt(outline.Count - 1);
			if (outline.Count < 3) return null;
			return new Shape { Type = Tools.polygon, pts = outline };
		}

		private static bool Same(Vector3 a, Vector3 b) => Math.Abs(a.x - b.x) < 0.01f && Math.Abs(a.z - b.z) < 0.01f;

		private static Vector3 At(SvgPoint p, float y) => new(p.X, y, p.Y);

		private static float Get(float[] values, int i) => i < values.Length ? values[i] : 0f;

		private static float[] Floats(string? list) => [.. (list ?? "")
			.Split([','], StringSplitOptions.RemoveEmptyEntries)
			.Select(s => Attr(s) ?? 0f)];

		private static Shape? ParsePolygon(XElement el) {
			string? pointsStr = el.Attribute("points")?.Value;

			// the compiler was complaining about it, that pointsStr is possibly null
			// thats why check if it is null before check if it is NullOrWhiteSpaces again
			if (pointsStr is null || string.IsNullOrWhiteSpace(pointsStr)) {
				return null;
			}

			float[] elevations = Floats(el.Attribute("data-y")?.Value);

			// Same number grammar as path data, so compact "1-2.5.5" style points parse too
			List<SvgPathToken> tokens = SvgPathTokenizer.Tokenize(pointsStr);
			foreach (SvgPathToken t in tokens) {
				if (t.IsCommand) throw new FormatException($"unexpected '{t.Command}' at {t.Index} in points");
			}
			if (tokens.Count % 2 != 0) throw new FormatException($"points has an odd number of coordinates ({tokens.Count})");

			List<Vector3> pts = [];
			for (int i = 0; i < tokens.Count / 2; i++) {
				pts.Add(new Vector3(tokens[2 * i].Number, Get(elevations, i), tokens[2 * i + 1].Number));
			}

			// Remove duplicate closing point added by exporter
			if (pts.Count >= 2 && Vector3.Distance(pts[0], pts[^1]) < 0.01f)
				pts.RemoveAt(pts.Count - 1);

			if (pts.Count < 3) return null;

			return new Shape {
				Type = Tools.polygon,
				layer = ParseLayer(el),
				pts = pts,
				Label = el.Attribute("data-label")?.Value,
				Description = el.Attribute("data-description")?.Value,
			};
		}

		private static Shape? ParseCircle(XElement el) {
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
				Type = Tools.point,
				layer = layer,
				pts = [new Vector3(cx.Value, y, cz.Value)],
				Label = el.Attribute("data-label")?.Value,
				Description = el.Attribute("data-description")?.Value,
			};
		}

		private static Shape? ParseText(XElement el) {
			float? x = Attr(el, "x"), z = Attr(el, "y");
			if (x == null || z == null) return null;
			string? label = el.Value?.Trim();
			if (string.IsNullOrEmpty(label)) return null;
			float y = Attr(el, "data-y") ?? 0f;
			return new Shape {
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

		private static SvgStylesheet ReadStylesheet(XElement root) {
			string css = string.Join("\n", Descendants(root, "style").Select(s => s.Value));
			return SvgStylesheet.Parse(css);
		}

		// Presentation attributes, class rules and style="" from the outermost ancestor down to the
		// element, so nearer values win, as in SVG itself (e.g. <g stroke="#1a1a1a"> around plain
		// <path>s). On one element, a class rule beats a presentation attribute and style="" beats both.
		private static Dictionary<string, string> InheritedStyle(XElement el) {
			SvgStylesheet? sheet = el.Document?.Annotation<SvgStylesheet>();
			Dictionary<string, string> style = [];
			foreach (XElement node in el.AncestorsAndSelf().Reverse()) {
				foreach (string name in StyleAttributes) {
					string? v = node.Attribute(name)?.Value;
					if (!string.IsNullOrWhiteSpace(v)) style[name] = v.Trim();
				}
				sheet?.Apply(node.Attribute("class")?.Value, style);
				foreach (KeyValuePair<string, string> d in SvgStylesheet.ParseDeclarations(node.Attribute("style")?.Value ?? ""))
					style[d.Key] = d.Value;
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
