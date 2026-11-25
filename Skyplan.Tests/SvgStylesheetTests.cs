using Skyplan.Persistence.Helpers;

namespace Skyplan.Tests {
	public class SvgStylesheetTests {
		static string Applied(string css, string? classes) {
			Dictionary<string, string> style = [];
			SvgStylesheet.Parse(css).Apply(classes, style);
			return string.Join(";", style.Select(kv => $"{kv.Key}:{kv.Value}"));
		}

		[Fact]
		public void Illustrator_class_rule() {
			Assert.Equal("fill:none;stroke:#333333;stroke-dasharray:8,2.8571",
				Applied("\n\t.st0{fill:none;stroke:#333333;stroke-dasharray:8,2.8571;}\n\t.st1{fill:#CC44FF;}", "st0"));
		}

		[Fact]
		public void Unknown_or_missing_class_adds_nothing() {
			Assert.Equal("", Applied(".st0{fill:none;}", "st9"));
			Assert.Equal("", Applied(".st0{fill:none;}", null));
			Assert.Equal("", Applied("", "st0"));
		}

		[Fact]
		public void Several_classes_apply_in_stylesheet_order() {
			// The later rule wins, whatever the order in the class attribute
			Assert.Equal("stroke:#222222;fill:none", Applied(".a{stroke:#111111;} .b{stroke:#222222;fill:none}", "b a"));
		}

		[Fact]
		public void Selector_list_shares_the_declarations() {
			Assert.Equal("stroke:red", Applied(".a, .b{stroke:red}", "b"));
		}

		[Fact]
		public void Comments_are_ignored() {
			Assert.Equal("stroke:red", Applied("/* .a{stroke:blue} */ .a{stroke:red /* note */}", "a"));
		}

		[Fact]
		public void Rules_inside_media_are_read() {
			Assert.Equal("stroke:red", Applied("@media screen { .a{stroke:red} }", "a"));
		}

		[Theory]
		// Not plain class selectors
		[InlineData("path.a{stroke:red}")]
		[InlineData("g .a{stroke:red}")]
		[InlineData("#a{stroke:red}")]
		[InlineData("path{stroke:red}")]
		public void Other_selectors_are_ignored(string css) {
			Assert.Equal(0, SvgStylesheet.Parse(css).Count);
		}

		[Fact]
		public void Declarations_skip_empty_parts() {
			Assert.Equal(new Dictionary<string, string> { ["a"] = "1", ["b"] = "x y" },
				SvgStylesheet.ParseDeclarations(" a : 1 ;; b:x y; c: ; :d"));
		}
	}
}
