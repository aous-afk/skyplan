using Skyplan.Models;
using Skyplan.Persistence;
using UnityEngine;

namespace Skyplan.Tests {
	public class SvgImporterTests {
		static List<string> warnings = [];

		// xunit runs the tests of one class one after another, so the static hook isn't shared
		static List<Shape> Import(string svg, out int skipped) {
			warnings = [];
			SVGImporter.LogWarn = warnings.Add;
			int nextId = 1;
			return SVGImporter.Import(svg, ref nextId, out skipped);
		}

		// Wraps elements the way Skyplan's exporter does
		static List<Shape> ImportSkyplan(string elements) {
			List<Shape> shapes = Import(
				"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"-7168.0 -7168.0 14336.0 14336.0\">\n" +
				elements + "\n</svg>", out int skipped);
			Assert.Equal(0, skipped);
			Assert.Empty(warnings);
			return shapes;
		}

		static Shape Single(string element) => Assert.Single(ImportSkyplan(element));

		static string V(Vector3 v) => FormattableString.Invariant($"{v.x:F1},{v.y:F1},{v.z:F1}");
		static string Pts(List<Vector3> pts) => string.Join(" ", pts.Select(V));

		// Skyplan's own exports. The elements are copied from real exports in ModsData.

		[Fact]
		public void Skyplan_straight_path() {
			Shape s = Single("""<path d="M -1425.6 -834.2 L -53.8 -829.4" data-layer="train" data-y="245.4,250.0" data-handle-y="" fill="none" style="stroke:#333333;stroke-width:5;stroke-dasharray:14 5;stroke-linecap:round;stroke-linejoin:round;stroke-opacity:0.8"/>""");
			Assert.Equal(Tools.path, s.Type);
			Assert.Equal("s1", s.id);
			Assert.Equal("train", s.layer.Id);
			Assert.Equal("-1425.6,245.4,-834.2 -53.8,250.0,-829.4", Pts(s.pts));
			Assert.Empty(s.handles);
			Assert.Equal("none", s.layer.Style["fill"]);
			Assert.Equal("#333333", s.layer.Style["stroke"]);
			Assert.Equal("14 5", s.layer.Style["stroke-dasharray"]);
		}

		[Fact]
		public void Skyplan_curve_keeps_handle_heights() {
			Shape s = Single("""<path d="M -1373.6 -1097.2 Q -122.2 -1080.9 -270.3 -383.3" data-layer="train" data-y="248.0,248.5" data-handle-y="250.5" fill="none" style="fill:none;stroke:#333333;stroke-width:5"/>""");
			Assert.Equal(Tools.curve, s.Type);
			Assert.Equal("-1373.6,248.0,-1097.2 -270.3,248.5,-383.3", Pts(s.pts));
			Assert.Equal("-122.2,250.5,-1080.9", Pts(s.handles));
		}

		[Fact]
		public void Skyplan_multi_segment_curve() {
			Shape s = Single("""<path d="M -5372.3 -4145.1 Q -4575.2 -3736.7 -3685.1 -2909.8 Q -2759.9 -2050.3 -2358.7 -1570.3" data-layer="highway" data-y="1,2,3" data-handle-y="4,5" fill="none"/>""");
			Assert.Equal("-5372.3,1.0,-4145.1 -3685.1,2.0,-2909.8 -2358.7,3.0,-1570.3", Pts(s.pts));
			Assert.Equal("-4575.2,4.0,-3736.7 -2759.9,5.0,-2050.3", Pts(s.handles));
		}

		[Fact]
		public void Skyplan_old_path_heights_are_ignored() {
			// Before path/curve export merged, a 2-point path wrote data-y0/data-y1; those were never read
			Shape s = Single("""<path d="M -1318.9 -982.0 L -1073.7 992.5" data-layer="subway" data-y0="247.6" data-y1="222.3" data-label="Hello" data-description="desc" fill="none" style="stroke:#5533ff;stroke-width:4"/>""");
			Assert.Equal("-1318.9,0.0,-982.0 -1073.7,0.0,992.5", Pts(s.pts));
			Assert.Equal("Hello", s.Label);
			Assert.Equal("desc", s.Description);
		}

		[Fact]
		public void Skyplan_polygon() {
			Shape s = Single("""<polygon points="-1390.7,-664.1 -1342.0,-633.4 -1359.0,-671.5 -1367.4,-631.6" data-layer="test-filter" data-y="243.4,243.0,244.0,242.6" style="stroke:#ff3366;stroke-width:2;fill:#ff3366;fill-opacity:1;filter:drop-shadow(0 0 20px rgba(255,0,0,1))"/>""");
			Assert.Equal(Tools.polygon, s.Type);
			Assert.Equal("-1390.7,243.4,-664.1 -1342.0,243.0,-633.4 -1359.0,244.0,-671.5 -1367.4,242.6,-631.6", Pts(s.pts));
			// Style keys are copied as they are, even ones Skyplan doesn't draw itself
			Assert.Equal("drop-shadow(0 0 20px rgba(255,0,0,1))", s.layer.Style["filter"]);
		}

		[Fact]
		public void Skyplan_polygon_drops_a_repeated_start_point() {
			Shape s = Single("""<polygon points="0,0 10,0 10,10 0,0" data-layer="residential"/>""");
			Assert.Equal("0.0,0.0,0.0 10.0,0.0,0.0 10.0,0.0,10.0", Pts(s.pts));
		}

		[Fact]
		public void Skyplan_point_with_icon() {
			// The glyph is part of the point's layer, not a shape of its own
			Shape s = Single("""<g><circle cx="-1611.2" cy="79.7" r="100" data-layer="poi" data-y="234.8" data-label="test" data-description="test desc" style="fill:#ffcc00;stroke:#ffcc00;stroke-width:1"/><path d="M0,-4 L4,0 L0,4 L-4,0 Z" data-icon-glyph="true" transform="translate(-1611.2,79.7) scale(16.7)" fill="black"/></g>""");
			Assert.Equal(Tools.point, s.Type);
			Assert.Equal("-1611.2,234.8,79.7", Pts(s.pts));
			Assert.Equal("test", s.Label);
			Assert.Equal("test desc", s.Description);
			Assert.Equal("M0,-4 L4,0 L0,4 L-4,0 Z", s.layer.Icon?.Path);
			Assert.Equal("black", s.layer.Icon?.Color);
		}

		[Fact]
		public void Skyplan_text() {
			Shape s = Single("""<text x="-4437.0" y="-283.5" data-layer="annotations" data-y="252.3" data-label="here some Notes for this area">here some Notes for this area</text>""");
			Assert.Equal(Tools.text, s.Type);
			Assert.Equal("-4437.0,252.3,-283.5", Pts(s.pts));
			Assert.Equal("here some Notes for this area", s.Label);
		}

		[Fact]
		public void Ids_follow_element_type_then_document_order() {
			List<Shape> shapes = ImportSkyplan("""
				<circle cx="0" cy="0"/>
				<path d="M0,0 L1,1" fill="none"/>
				<polygon points="0,0 1,0 1,1"/>
				<path d="M5,5 L6,6" fill="none"/>
				""");
			Assert.Equal(["s1 path", "s2 path", "s3 polygon", "s4 point"], shapes.Select(s => $"{s.id} {s.Type}"));
		}

		// Issue #101: the same kind of file re-saved by Illustrator 28. Relative commands, compact
		// numbers, styles in CSS classes, no data-* attributes.
		const string IllustratorFile = """
			<?xml version="1.0" encoding="utf-8"?>
			<!-- Generator: Adobe Illustrator 28.0.0, SVG Export Plug-In . SVG Version: 6.00 Build 0)  -->
			<svg version="1.0" id="Layer_1" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px"
				 viewBox="0 0 8192 8192" style="enable-background:new 0 0 8192 8192;" xml:space="preserve">
			<style type="text/css">
				.st0{fill:none;stroke:#333333;stroke-width:2.8571;stroke-linecap:round;stroke-linejoin:round;stroke-miterlimit:2.2857;stroke-opacity:0.8;stroke-dasharray:8,2.8571;}
				.st8{fill:#00AE1A;fill-opacity:0.5;stroke:#00AE1A;stroke-width:1.1429;stroke-miterlimit:2.2857;}
				.st9{fill:none;stroke:#FF4444;stroke-width:3.4286;stroke-linecap:round;stroke-linejoin:round;stroke-miterlimit:2.2857;stroke-opacity:0.8;}
			</style>
			<path class="st0" d="M4469.9,584.4l-31.5,25.8"/>
			<path class="st0" d="M4438.4,610.2l-500.2,74.1"/>
			<polygon class="st8" points="5152.2,4330.5 4996.9,4328.1 4998,4232.5 5054.4,4173.3 5182.6,4295.5 "/>
			<path class="st9" d="M4124.1,1052.3c18.1,26.7,41.5,32.6,70.2,17.6"/>
			<path class="st9" d="M4194.3,1069.9l183.4-90.2"/>
			</svg>
			""";

		[Fact]
		public void Illustrator_lines_and_polygons_import() {
			List<Shape> shapes = Import(IllustratorFile, out int skipped);
			Assert.Equal([
				"path 4469.9,0.0,584.4 4438.4,0.0,610.2",
				"path 4438.4,0.0,610.2 3938.2,0.0,684.3",
				"path 4194.3,0.0,1069.9 4377.7,0.0,979.7",
				"polygon 5152.2,0.0,4330.5 4996.9,0.0,4328.1 4998.0,0.0,4232.5 5054.4,0.0,4173.3 5182.6,0.0,4295.5",
			], shapes.Select(s => $"{s.Type} {Pts(s.pts)}"));
			Assert.All(shapes, s => Assert.Equal(SVGImporter.ImportedLayerId, s.layer.Id));

			// Styles come from the classes
			Assert.Equal("#333333", shapes[0].layer.Style["stroke"]);
			Assert.Equal("8,2.8571", shapes[0].layer.Style["stroke-dasharray"]);
			Assert.Equal("#FF4444", shapes[2].layer.Style["stroke"]);
			Assert.Equal("none", shapes[2].layer.Style["fill"]);
			Assert.Equal("#00AE1A", shapes[3].layer.Style["fill"]);
			Assert.Equal("0.5", shapes[3].layer.Style["fill-opacity"]);

			// The cubic curve waits for C/S support
			Assert.Equal(1, skipped);
			Assert.Equal("[SVGImporter] skipped <path> at line 13: NotSupportedException: path command 'c' at 14 is not supported yet", Assert.Single(warnings));
		}

		// Shapes from other SVG tools

		[Fact]
		public void Each_subpath_is_its_own_shape() {
			List<Shape> shapes = ImportSkyplan("""<path d="M0,0 l10,0 M5,5 l0,10" fill="none"/>""");
			Assert.Equal(["0.0,0.0,0.0 10.0,0.0,0.0", "5.0,0.0,5.0 5.0,0.0,15.0"], shapes.Select(s => Pts(s.pts)));
			Assert.Equal(["s1", "s2"], shapes.Select(s => s.id));
		}

		[Fact]
		public void Straight_segment_before_a_curve_gets_a_midpoint_handle() {
			Shape s = Single("""<path d="M0,0 L10,0 Q15,5 20,0 L30,0" fill="none"/>""");
			Assert.Equal(Tools.curve, s.Type);
			Assert.Equal("0.0,0.0,0.0 10.0,0.0,0.0 20.0,0.0,0.0 30.0,0.0,0.0", Pts(s.pts));
			// No handle for the trailing straight segment
			Assert.Equal("5.0,0.0,0.0 15.0,0.0,5.0", Pts(s.handles));
		}

		[Fact]
		public void Closed_filled_path_is_a_polygon() {
			// No fill attribute means black fill in SVG
			Shape s = Single("""<path d="M0,0 h10 v10 h-10 z"/>""");
			Assert.Equal(Tools.polygon, s.Type);
			Assert.Equal("0.0,0.0,0.0 10.0,0.0,0.0 10.0,0.0,10.0 0.0,0.0,10.0", Pts(s.pts));
		}

		[Fact]
		public void Closed_filled_curve_is_sampled_into_the_polygon() {
			Shape s = Single("""<path d="M0,0 L10,0 Q5,10 0,0 Z" style="fill:#ff0000"/>""");
			Assert.Equal(Tools.polygon, s.Type);
			// Start, corner, then 7 samples on the curve back to the start; the straight edge isn't sampled
			Assert.Equal(9, s.pts.Count);
			Assert.Equal("5.0,0.0,5.0", V(s.pts[5]));
		}

		[Fact]
		public void Closed_unfilled_path_returns_to_its_start() {
			Shape s = Single("""<path d="M0,0 h10 v10 z" fill="none"/>""");
			Assert.Equal(Tools.path, s.Type);
			Assert.Equal("0.0,0.0,0.0 10.0,0.0,0.0 10.0,0.0,10.0 0.0,0.0,0.0", Pts(s.pts));
		}

		[Fact]
		public void Fill_none_is_inherited_from_a_group() {
			Shape s = Single("""<g fill="none" stroke="#1a1a1a"><path d="M0,0 h10 v10 z"/></g>""");
			Assert.Equal(Tools.path, s.Type);
			Assert.Equal("#1a1a1a", s.layer.Style["stroke"]);
		}

		[Fact]
		public void Class_beats_attribute_and_style_beats_class() {
			List<Shape> shapes = Import("""
				<svg><style>.a{stroke:#222222;stroke-width:3}</style>
				<path class="a" stroke="#111111" d="M0,0 L1,1" fill="none"/>
				<path class="a" style="stroke:#333333" d="M0,0 L1,1" fill="none"/>
				</svg>
				""", out _);
			Assert.Equal(["#222222 3", "#333333 3"], shapes.Select(s => $"{s.layer.Style["stroke"]} {s.layer.Style["stroke-width"]}"));
		}

		[Fact]
		public void Class_fill_none_keeps_a_closed_path_a_path() {
			Shape s = Single("""<style>.line{fill:none;stroke:red}</style><path class="line" d="M0,0 h10 v10 z"/>""");
			Assert.Equal(Tools.path, s.Type);
		}

		[Fact]
		public void Compact_polygon_points() {
			Shape s = Single("""<polygon points="0,0 10-5 .5.5"/>""");
			Assert.Equal("0.0,0.0,0.0 10.0,0.0,-5.0 0.5,0.0,0.5", Pts(s.pts));
		}

		[Theory]
		[InlineData("""<polygon points="0,0 10,0 5"/>""", "FormatException: points has an odd number of coordinates (5)")]
		[InlineData("""<polygon points="0,0 L10,0 5,5"/>""", "FormatException: unexpected 'L' at 4 in points")]
		[InlineData("""<path d="M0,0 C1,1 2,2 3,3"/>""", "NotSupportedException: path command 'C' at 5 is not supported yet")]
		public void Bad_element_is_skipped_with_a_warning(string element, string reason) {
			List<Shape> shapes = Import($"""<svg>{element}<circle cx="1" cy="2"/></svg>""", out int skipped);
			Assert.Equal(1, skipped);
			Assert.Equal(Tools.point, Assert.Single(shapes).Type);
			Assert.EndsWith(reason, Assert.Single(warnings));
		}
	}
}
