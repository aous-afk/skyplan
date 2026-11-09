namespace Skyplan.Persistence.Helpers {
	public readonly struct SvgPoint {
		public readonly float X;
		public readonly float Y;

		public SvgPoint(float x, float y) {
			X = x;
			Y = y;
		}

		public override string ToString() => FormattableString.Invariant($"{X},{Y}");
	}

	public sealed class SvgSubpath {
		// Absolute coordinates, in path data units
		public readonly List<SvgPoint> Points = [];
		// Ended with Z. The closing segment back to Points[0] is implied, not stored as a point.
		public bool Closed;
	}

	// Turns path data into absolute subpaths. Each M starts a new subpath, so separate pieces don't get
	// joined by a straight line.
	public static class SvgPathParser {
		public static List<SvgSubpath> Parse(string d) {
			List<SvgPathToken> tokens = SvgPathTokenizer.Tokenize(d);
			List<SvgSubpath> subpaths = [];
			SvgSubpath? current = null;
			SvgPoint pos = new(0f, 0f);
			SvgPoint start = pos;
			int i = 0;

			while (i < tokens.Count) {
				SvgPathToken cmdToken = tokens[i];
				if (!cmdToken.IsCommand)
					throw new FormatException($"expected a command at {cmdToken.Index} in path data, got {cmdToken}");
				char cmd = cmdToken.Command;
				bool relative = char.IsLower(cmd);
				i++;

				if (current == null && char.ToUpperInvariant(cmd) != 'M')
					throw new FormatException($"path data must start with M, got '{cmd}' at {cmdToken.Index}");

				switch (char.ToUpperInvariant(cmd)) {
					case 'M': {
						SvgPoint p = ReadPoint(tokens, ref i, cmdToken);
						pos = relative ? Add(pos, p) : p;
						start = pos;
						current = new SvgSubpath();
						current.Points.Add(pos);
						subpaths.Add(current);
						// Further pairs after M are implicit L (l after m)
						while (NextIsNumber(tokens, i)) {
							p = ReadPoint(tokens, ref i, cmdToken);
							pos = relative ? Add(pos, p) : p;
							current.Points.Add(pos);
						}
						break;
					}
					case 'L':
						current = Continue(current!, subpaths, start);
						do {
							SvgPoint p = ReadPoint(tokens, ref i, cmdToken);
							pos = relative ? Add(pos, p) : p;
							current.Points.Add(pos);
						} while (NextIsNumber(tokens, i));
						break;
					case 'H':
						current = Continue(current!, subpaths, start);
						do {
							float x = ReadNumber(tokens, ref i, cmdToken);
							pos = new SvgPoint(relative ? pos.X + x : x, pos.Y);
							current.Points.Add(pos);
						} while (NextIsNumber(tokens, i));
						break;
					case 'V':
						current = Continue(current!, subpaths, start);
						do {
							float y = ReadNumber(tokens, ref i, cmdToken);
							pos = new SvgPoint(pos.X, relative ? pos.Y + y : y);
							current.Points.Add(pos);
						} while (NextIsNumber(tokens, i));
						break;
					case 'Z':
						current!.Closed = true;
						// Illustrator often repeats the start point before Z
						if (current.Points.Count > 1 && SamePoint(current.Points[^1], start))
							current.Points.RemoveAt(current.Points.Count - 1);
						pos = start;
						break;
					default:
						throw new NotSupportedException($"path command '{cmd}' at {cmdToken.Index} is not supported yet");
				}
			}
			return subpaths;
		}

		// After Z, a drawing command without a new M starts another subpath at the closed one's start.
		private static SvgSubpath Continue(SvgSubpath current, List<SvgSubpath> subpaths, SvgPoint start) {
			if (!current.Closed) return current;
			SvgSubpath next = new();
			next.Points.Add(start);
			subpaths.Add(next);
			return next;
		}

		private static bool NextIsNumber(List<SvgPathToken> tokens, int i) => i < tokens.Count && !tokens[i].IsCommand;

		private static float ReadNumber(List<SvgPathToken> tokens, ref int i, SvgPathToken cmd) {
			if (!NextIsNumber(tokens, i))
				throw new FormatException($"path command '{cmd.Command}' at {cmd.Index} is missing a number");
			return tokens[i++].Number;
		}

		private static SvgPoint ReadPoint(List<SvgPathToken> tokens, ref int i, SvgPathToken cmd) {
			float x = ReadNumber(tokens, ref i, cmd);
			float y = ReadNumber(tokens, ref i, cmd);
			return new SvgPoint(x, y);
		}

		private static SvgPoint Add(SvgPoint a, SvgPoint b) => new(a.X + b.X, a.Y + b.Y);

		private static bool SamePoint(SvgPoint a, SvgPoint b) =>
			Math.Abs(a.X - b.X) < 0.001f && Math.Abs(a.Y - b.Y) < 0.001f;
	}
}
