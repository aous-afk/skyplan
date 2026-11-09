using Skyplan.Persistence.Helpers;

namespace Skyplan.Tests {
	public class SvgPathParserTests {
		// One string per subpath: "x,y x,y ..." plus " Z" when closed
		static string[] Parsed(string d) => [.. SvgPathParser.Parse(d).Select(s =>
			string.Join(" ", s.Points.Select(p => FormattableString.Invariant($"{Round(p.X)},{Round(p.Y)}"))) + (s.Closed ? " Z" : ""))];

		// Relative coordinates add up float error; compare at the precision Skyplan exports (0.1)
		static float Round(float v) => (float)Math.Round(v, 1);

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

		[Theory]
		[InlineData("M0,0 C1,1 2,2 3,3", 'C')]
		[InlineData("M0,0 q1,1 2,2", 'q')]
		[InlineData("M0,0 A5,5 0 0 1 10,0", 'A')]
		public void Curve_commands_are_not_supported_yet(string d, char cmd) {
			NotSupportedException ex = Assert.Throws<NotSupportedException>(() => SvgPathParser.Parse(d));
			Assert.Equal($"path command '{cmd}' at 5 is not supported yet", ex.Message);
		}
	}
}
