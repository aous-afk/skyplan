using Skyplan.Persistence.Helpers;

namespace Skyplan.Tests {
	public class SvgPathTokenizerTests {
		static string Joined(string d) => string.Join(" ", SvgPathTokenizer.Tokenize(d));

		[Theory]
		// Skyplan's own export
		[InlineData("M 654.3 -6145.3 L 599.2 -6100.1", "M 654.3 -6145.3 L 599.2 -6100.1")]
		// Illustrator: no separators around commands, comma between pairs
		[InlineData("M4469.9,584.4l-31.5,25.8", "M 4469.9 584.4 l -31.5 25.8")]
		// A second '.' starts a new number
		[InlineData("c.5.5", "c 0.5 0.5")]
		// A sign starts a new number
		[InlineData("l-1-2", "l -1 -2")]
		[InlineData("l+1+2", "l 1 2")]
		// Exponent sign belongs to the number
		[InlineData("L1e-3,2E+2", "L 0.001 200")]
		[InlineData("L1.5e2-3", "L 150 -3")]
		// Trailing dot
		[InlineData("L5.,6", "L 5 6")]
		// Mixed whitespace and commas as separators
		[InlineData(" M 1 ,\t2\r\n3,4 z ", "M 1 2 3 4 z")]
		// city-roads: several pairs after one command
		[InlineData("M1 2 L3 4,5 6", "M 1 2 L 3 4 5 6")]
		public void Splits_into_commands_and_numbers(string d, string expected) {
			Assert.Equal(expected, Joined(d));
		}

		[Fact]
		public void Every_command_letter_is_a_command() {
			List<SvgPathToken> tokens = SvgPathTokenizer.Tokenize("MmLlHhVvCcSsQqTtAaZz");
			Assert.Equal(20, tokens.Count);
			Assert.All(tokens, t => Assert.True(t.IsCommand));
		}

		[Fact]
		public void Empty_path_has_no_tokens() {
			Assert.Empty(SvgPathTokenizer.Tokenize(""));
			Assert.Empty(SvgPathTokenizer.Tokenize("  , "));
		}

		[Fact]
		public void Tokens_keep_their_offset() {
			List<SvgPathToken> tokens = SvgPathTokenizer.Tokenize("M10,20l-3,4");
			Assert.Equal([0, 1, 4, 6, 7, 10], tokens.Select(t => t.Index));
		}

		[Theory]
		[InlineData("M1,2 X3,4", 5, 'X')]
		[InlineData("M1,2 L3;4", 7, ';')]
		[InlineData("M1,2 L3,4 #", 10, '#')]
		[InlineData("M1,2 L-.", 6, '-')]
		public void Throws_on_anything_that_is_not_a_separator(string d, int index, char c) {
			FormatException ex = Assert.Throws<FormatException>(() => SvgPathTokenizer.Tokenize(d));
			Assert.Equal($"unexpected '{c}' at {index} in path data", ex.Message);
		}
	}
}
