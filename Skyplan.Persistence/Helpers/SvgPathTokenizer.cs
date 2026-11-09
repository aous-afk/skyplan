using System.Globalization;
using System.Text.RegularExpressions;

namespace Skyplan.Persistence.Helpers {
	public readonly struct SvgPathToken {
		// '\0' when the token is a number
		public readonly char Command;
		public readonly float Number;
		// Offset in the path data, for error messages
		public readonly int Index;

		public bool IsCommand => Command != '\0';

		public SvgPathToken(char command, float number, int index) {
			Command = command;
			Number = number;
			Index = index;
		}

		public override string ToString() => IsCommand ? Command.ToString() : Number.ToString(CultureInfo.InvariantCulture);
	}

	public static class SvgPathTokenizer {
		// Greedy, so a number ends where the next one has to start: "584.4l-31.5" -> 584.4 l -31.5,
		// ".5.5" -> .5 .5, "-1-2" -> -1 -2. Illustrator writes paths this compactly.
		private static readonly Regex Token = new(
			@"[MmLlHhVvCcSsQqTtAaZz]|[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?",
			RegexOptions.Compiled);

		public static List<SvgPathToken> Tokenize(string d) {
			List<SvgPathToken> tokens = [];
			int pos = 0;
			foreach (Match m in Token.Matches(d)) {
				// Matches() skips what it can't match; anything there but separators is an error, not something to drop.
				CheckSeparators(d, pos, m.Index);
				char c = m.Value[0];
				if (char.IsLetter(c) && m.Length == 1) {
					tokens.Add(new SvgPathToken(c, 0f, m.Index));
				} else {
					float n = float.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
					tokens.Add(new SvgPathToken('\0', n, m.Index));
				}
				pos = m.Index + m.Length;
			}
			CheckSeparators(d, pos, d.Length);
			return tokens;
		}

		private static void CheckSeparators(string d, int from, int to) {
			for (int i = from; i < to; i++) {
				char c = d[i];
				if (c != ',' && !char.IsWhiteSpace(c))
					throw new FormatException($"unexpected '{c}' at {i} in path data");
			}
		}
	}
}
