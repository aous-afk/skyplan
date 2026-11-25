using Skyplan.Persistence.Helpers;

namespace Skyplan.Tests {
	public class SvgPathParserTests {
		// One string per subpath: "x,y x,y ...", a curved segment as "Q cx,cy x,y", plus " Z" when closed
		static string[] Parsed(string d) => [.. SvgPathParser.Parse(d).Select(Format)];

		static string Format(SvgSubpath s) {
			Assert.Equal(Math.Max(0, s.Points.Count - 1), s.Controls.Count);
			List<string> parts = [Pt(s.Points[0])];
			for (int i = 1; i < s.Points.Count; i++) {
				SvgPoint? c = s.Controls[i - 1];
				parts.Add(c == null ? Pt(s.Points[i]) : $"Q {Pt(c.Value)} {Pt(s.Points[i])}");
			}
			return string.Join(" ", parts) + (s.Closed ? " Z" : "");
		}

		// Relative coordinates add up float error; compare at the precision Skyplan exports (0.1)
		static string Pt(SvgPoint p) => FormattableString.Invariant($"{Math.Round(p.X, 1)},{Math.Round(p.Y, 1)}");

		[Fact]
		public void Skyplan_export_is_unchanged() {
			Assert.Equal(["654.3,-6145.3 599.2,-6100.1"], Parsed("M 654.3 -6145.3 L 599.2 -6100.1"));
		}

		[Fact]
		public void Illustrator_relative_line() {
			Assert.Equal(["4469.9,584.4 4438.4,610.2"], Parsed("M4469.9,584.4l-31.5,25.8"));
		}

		[Fact]
		public void Repeated_pairs_repeat_the_command() {
			Assert.Equal(["0,0 1,1 2,0 3,1"], Parsed("M0,0 L1,1 2,0 3,1"));
			Assert.Equal(["0,0 1,1 2,1"], Parsed("M0,0 l1,1 1,0"));
		}

		[Fact]
		public void Pairs_after_M_are_lines() {
			Assert.Equal(["0,0 10,0 10,10"], Parsed("M0,0 10,0 10,10"));
			// ...relative after m
			Assert.Equal(["5,5 15,5 15,15"], Parsed("m5,5 10,0 0,10"));
		}

		[Fact]
		public void Horizontal_and_vertical() {
			Assert.Equal(["0,0 10,0 10,20 4,20 4,25"], Parsed("M0,0 H10 V20 h-6 v5"));
			Assert.Equal(["0,0 1,0 3,0"], Parsed("M0,0 H1 3"));
		}

		[Fact]
		public void Each_M_starts_a_new_subpath() {
			Assert.Equal(["0,0 1,0", "5,5 6,5"], Parsed("M0,0 L1,0 M5,5 L6,5"));
			// relative m is relative to the end of the previous subpath
			Assert.Equal(["0,0 1,0", "3,2 4,2"], Parsed("M0,0 l1,0 m2,2 l1,0"));
		}

		[Fact]
		public void Z_closes_and_returns_to_start() {
			Assert.Equal(["0,0 10,0 10,10 Z"], Parsed("M0,0 L10,0 L10,10 Z"));
			// relative after Z continues from the start point
			Assert.Equal(["0,0 10,0 10,10 Z", "0,0 0,5"], Parsed("M0,0 h10 v10 z l0,5"));
		}

		[Fact]
		public void Z_drops_a_repeated_start_point() {
			Assert.Equal(["0,0 10,0 10,10 Z"], Parsed("M0,0 L10,0 L10,10 L0,0 Z"));
		}

		[Fact]
		public void Move_only_subpath_is_kept_as_a_single_point() {
			Assert.Equal(["1,2", "3,4 5,6"], Parsed("M1,2 M3,4 L5,6"));
		}

		[Fact]
		public void Empty_path_has_no_subpaths() {
			Assert.Empty(SvgPathParser.Parse(""));
		}

		[Theory]
		[InlineData("L1,2", "path data must start with M, got 'L' at 0")]
		[InlineData("1,2", "expected a command at 0 in path data, got 1")]
		[InlineData("M1", "path command 'M' at 0 is missing a number")]
		[InlineData("M1,2 L3", "path command 'L' at 5 is missing a number")]
		[InlineData("M1,2 L3,4,5", "path command 'L' at 5 is missing a number")]
		public void Throws_on_malformed_data(string d, string message) {
			FormatException ex = Assert.Throws<FormatException>(() => SvgPathParser.Parse(d));
			Assert.Equal(message, ex.Message);
		}

		[Fact]
		public void Skyplan_curve_export_is_unchanged() {
			Assert.Equal(["-5372.3,-4145.1 Q -4575.2,-3736.7 -3685.1,-2909.8 Q -2759.9,-2050.3 -2358.7,-1570.3"],
				Parsed("M -5372.3 -4145.1 Q -4575.2 -3736.7 -3685.1 -2909.8 Q -2759.9 -2050.3 -2358.7 -1570.3"));
		}

		[Fact]
		public void Quadratic_absolute_relative_and_repeated() {
			Assert.Equal(["0,0 Q 5,10 10,0"], Parsed("M0,0 Q5,10 10,0"));
			Assert.Equal(["10,10 Q 15,20 20,10"], Parsed("M10,10 q5,10 10,0"));
			Assert.Equal(["0,0 Q 5,10 10,0 Q 15,-10 20,0"], Parsed("M0,0 Q5,10 10,0 15,-10 20,0"));
		}

		[Fact]
		public void Straight_and_curved_segments_keep_their_order() {
			// The control belongs to the second segment, not the first
			Assert.Equal(["0,0 10,0 Q 15,5 20,0 30,0"], Parsed("M0,0 L10,0 Q15,5 20,0 L30,0"));
		}

		[Fact]
		public void T_mirrors_the_previous_control() {
			Assert.Equal(["0,0 Q 5,10 10,0 Q 15,-10 20,0"], Parsed("M0,0 Q5,10 10,0 T20,0"));
			Assert.Equal(["0,0 Q 5,10 10,0 Q 15,-10 20,0 Q 25,10 30,0"], Parsed("M0,0 Q5,10 10,0 T20,0 30,0"));
			Assert.Equal(["0,0 Q 5,10 10,0 Q 15,-10 20,0"], Parsed("M0,0 Q5,10 10,0 t10,0"));
		}

		[Fact]
		public void T_without_a_preceding_curve_is_straight() {
			// Control = current point
			Assert.Equal(["0,0 10,0 Q 10,0 20,0"], Parsed("M0,0 L10,0 T20,0"));
			// An L in between resets the mirror
			Assert.Equal(["0,0 Q 5,10 10,0 15,0 Q 15,0 20,0"], Parsed("M0,0 Q5,10 10,0 L15,0 T20,0"));
		}

		[Fact]
		public void Z_keeps_a_curved_segment_into_the_start() {
			Assert.Equal(["0,0 10,0 Q 5,10 0,0 Z"], Parsed("M0,0 L10,0 Q5,10 0,0 Z"));
		}

		[Fact]
		public void Straight_cubic_is_one_quadratic() {
			Assert.Equal(["0,0 Q 1.5,0 3,0"], Parsed("M0,0 C1,0 2,0 3,0"));
		}

		[Fact]
		public void Cubic_that_is_a_quadratic_gives_that_quadratic_back() {
			// Q(0,0 5,10 10,0) degree-elevated: controls at 2/3 of the way to the quadratic control
			Assert.Equal(["0,0 Q 5,10 10,0"], Parsed("M0,0 C3.3333333,6.6666667 6.6666667,6.6666667 10,0"));
		}

		[Fact]
		public void Curved_cubic_is_split_within_the_tolerance() {
			SvgPoint p0 = new(0, 0), c1 = new(0, 100), c2 = new(100, 100), p3 = new(100, 0);
			SvgSubpath sub = Assert.Single(SvgPathParser.Parse("M0,0 C0,100 100,100 100,0"));
			Assert.True(sub.Points.Count > 2);
			Assert.All(sub.Controls, c => Assert.NotNull(c));
			Assert.Equal(p3.ToString(), sub.Points[sub.Points.Count - 1].ToString());

			// Every point of the cubic is near the quadratics
			List<SvgPoint> quads = [];
			for (int i = 0; i < sub.Controls.Count; i++) {
				for (int s = 0; s <= 50; s++)
					quads.Add(Quad(sub.Points[i], sub.Controls[i]!.Value, sub.Points[i + 1], s / 50f));
			}
			for (int s = 0; s <= 200; s++) {
				SvgPoint p = Cubic(p0, c1, c2, p3, s / 200f);
				double nearest = quads.Min(q => Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y)));
				Assert.True(nearest <= SvgPathParser.CubicTolerance, $"{p} is {nearest} from the quadratics");
			}
		}

		static SvgPoint Quad(SvgPoint a, SvgPoint c, SvgPoint b, float t) {
			float u = 1 - t;
			return new(u * u * a.X + 2 * u * t * c.X + t * t * b.X, u * u * a.Y + 2 * u * t * c.Y + t * t * b.Y);
		}

		static SvgPoint Cubic(SvgPoint a, SvgPoint c1, SvgPoint c2, SvgPoint b, float t) {
			float u = 1 - t;
			return new(u * u * u * a.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * b.X,
				u * u * u * a.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * b.Y);
		}

		[Fact]
		public void Relative_and_repeated_cubics() {
			Assert.Equal(Parsed("M10,10 C10,20 20,20 20,10"), Parsed("M10,10 c0,10 10,10 10,0"));
			Assert.Equal(Parsed("M0,0 C0,10 10,10 10,0 C10,-10 20,-10 20,0"), Parsed("M0,0 C0,10 10,10 10,0 10,-10 20,-10 20,0"));
		}

		[Fact]
		public void S_mirrors_the_previous_second_control() {
			Assert.Equal(Parsed("M0,0 C0,10 10,10 10,0 C10,-10 20,-10 20,0"), Parsed("M0,0 C0,10 10,10 10,0 S20,-10 20,0"));
			Assert.Equal(Parsed("M0,0 C0,10 10,10 10,0 C10,-10 20,-10 20,0"), Parsed("M0,0 C0,10 10,10 10,0 s10,-10 10,0"));
		}

		[Fact]
		public void S_without_a_preceding_cubic_starts_at_the_current_point() {
			Assert.Equal(Parsed("M0,0 L10,0 C10,0 20,10 30,0"), Parsed("M0,0 L10,0 S20,10 30,0"));
			// A Q's control is not a cubic control
			Assert.Equal(Parsed("M0,0 Q5,5 10,0 C10,0 20,10 30,0"), Parsed("M0,0 Q5,5 10,0 S20,10 30,0"));
		}

		[Fact]
		public void T_after_a_cubic_is_straight() {
			Assert.EndsWith("10,0 Q 10,0 20,0", Assert.Single(Parsed("M0,0 C0,10 10,10 10,0 T20,0")));
		}

		[Fact]
		public void Arcs_are_not_supported_yet() {
			NotSupportedException ex = Assert.Throws<NotSupportedException>(() => SvgPathParser.Parse("M0,0 A5,5 0 0 1 10,0"));
			Assert.Equal("path command 'A' at 5 is not supported yet", ex.Message);
		}
	}
}
