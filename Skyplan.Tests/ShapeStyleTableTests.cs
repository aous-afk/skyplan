using Skyplan.Persistence.Helpers;

namespace Skyplan.Tests {
	public class ShapeStyleTableTests {
		[Fact]
		public void Equal_styles_share_an_index() {
			ShapeStyleTable table = new();
			Assert.Equal(0, table.Add(new Dictionary<string, string> { ["stroke"] = "#333333", ["fill"] = "none" }));
			Assert.Equal(1, table.Add(new Dictionary<string, string> { ["stroke"] = "#ffffff" }));
			// Key order doesn't matter
			Assert.Equal(0, table.Add(new Dictionary<string, string> { ["fill"] = "none", ["stroke"] = "#333333" }));
			Assert.Equal(2, table.Styles.Count);
		}

		[Fact]
		public void Missing_keys_get_the_svg_initial_value() {
			ShapeStyleTable table = new();
			table.Add(new Dictionary<string, string> { ["stroke"] = "#ffffff", ["fill"] = "none" });
			Dictionary<string, string> style = table.Styles[0];
			Assert.Equal("#ffffff", style["stroke"]);
			Assert.Equal("none", style["fill"]);
			// So a dashed layer style can't leak into a solid line; "0" because GameFace drops "none"
			Assert.Equal("0", style["stroke-dasharray"]);
			Assert.Equal("1", style["stroke-width"]);
		}

		[Fact]
		public void Dasharray_none_from_a_file_becomes_0() {
			ShapeStyleTable table = new();
			int a = table.Add(new Dictionary<string, string> { ["stroke-dasharray"] = "none" });
			int b = table.Add(new Dictionary<string, string>());
			Assert.Equal("0", table.Styles[a]["stroke-dasharray"]);
			Assert.Equal(a, b);
		}

		[Fact]
		public void Unset_and_initial_values_are_the_same_style() {
			ShapeStyleTable table = new();
			int a = table.Add(new Dictionary<string, string> { ["stroke"] = "red" });
			int b = table.Add(new Dictionary<string, string> { ["stroke"] = "red", ["stroke-width"] = "1" });
			Assert.Equal(a, b);
		}

		[Fact]
		public void Other_keys_are_kept() {
			ShapeStyleTable table = new();
			table.Add(new Dictionary<string, string> { ["stroke-miterlimit"] = "2.2857" });
			Assert.Equal("2.2857", table.Styles[0]["stroke-miterlimit"]);
		}
	}
}
