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
		// Controls[i] is the quadratic control point of segment Points[i] -> Points[i + 1], null for a
		// straight segment. Always one entry per segment: Controls.Count == Points.Count - 1.
		public readonly List<SvgPoint?> Controls = [];
		// Ended with Z. The closing segment back to Points[0] is implied and straight, not stored.
		public bool Closed;

		internal void Add(SvgPoint p, SvgPoint? control) {
			if (Points.Count > 0) Controls.Add(control);
			Points.Add(p);
		}
	}

	// Turns path data into absolute subpaths. Each M starts a new subpath, so separate pieces don't get
	// joined by a straight line.
	public static class SvgPathParser {
		// Skyplan shapes are quadratic only, so a cubic is split into quadratics that stay within this
		// distance of it, in path data units (metres for Skyplan's own exports).
		public const float CubicTolerance = 0.5f;
		// 2^8 = 256 pieces at most per cubic segment
		private const int MaxCubicSplitDepth = 8;

		public static List<SvgSubpath> Parse(string d) {
			List<SvgPathToken> tokens = SvgPathTokenizer.Tokenize(d);
			List<SvgSubpath> subpaths = [];
			SvgSubpath? current = null;
			SvgPoint pos = new(0f, 0f);
			SvgPoint start = pos;
			// T reflects this; only set right after a Q or T
			SvgPoint? lastQuadControl = null;
			// S reflects this (the second cubic control); only set right after a C or S
			SvgPoint? lastCubicControl = null;
			int i = 0;

			while (i < tokens.Count) {
				SvgPathToken cmdToken = tokens[i];
				if (!cmdToken.IsCommand)
					throw new FormatException($"expected a command at {cmdToken.Index} in path data, got {cmdToken}");
				char cmd = cmdToken.Command;
				bool relative = char.IsLower(cmd);
				char upper = char.ToUpperInvariant(cmd);
				i++;

				if (current == null && upper != 'M')
					throw new FormatException($"path data must start with M, got '{cmd}' at {cmdToken.Index}");

				SvgPoint? quadControl = null;
				SvgPoint? cubicControl = null;
				switch (upper) {
					case 'M': {
						SvgPoint p = ReadPoint(tokens, ref i, cmdToken);
						pos = relative ? Add(pos, p) : p;
						start = pos;
						current = new SvgSubpath();
						current.Add(pos, null);
						subpaths.Add(current);
						// Further pairs after M are implicit L (l after m)
						while (NextIsNumber(tokens, i)) {
							p = ReadPoint(tokens, ref i, cmdToken);
							pos = relative ? Add(pos, p) : p;
							current.Add(pos, null);
						}
						break;
					}
					case 'L':
						current = Continue(current!, subpaths, start);
						do {
							SvgPoint p = ReadPoint(tokens, ref i, cmdToken);
							pos = relative ? Add(pos, p) : p;
							current.Add(pos, null);
						} while (NextIsNumber(tokens, i));
						break;
					case 'H':
						current = Continue(current!, subpaths, start);
						do {
							float x = ReadNumber(tokens, ref i, cmdToken);
							pos = new SvgPoint(relative ? pos.X + x : x, pos.Y);
							current.Add(pos, null);
						} while (NextIsNumber(tokens, i));
						break;
					case 'V':
						current = Continue(current!, subpaths, start);
						do {
							float y = ReadNumber(tokens, ref i, cmdToken);
							pos = new SvgPoint(pos.X, relative ? pos.Y + y : y);
							current.Add(pos, null);
						} while (NextIsNumber(tokens, i));
						break;
					case 'Q':
						current = Continue(current!, subpaths, start);
						do {
							SvgPoint c = ReadPoint(tokens, ref i, cmdToken);
							SvgPoint p = ReadPoint(tokens, ref i, cmdToken);
							if (relative) {
								c = Add(pos, c);
								p = Add(pos, p);
							}
							current.Add(p, c);
							pos = p;
							quadControl = c;
						} while (NextIsNumber(tokens, i));
						break;
					case 'T':
						current = Continue(current!, subpaths, start);
						do {
							// Mirror of the previous control around the current point; without a preceding Q/T
							// the control is the current point itself, which makes the segment straight.
							SvgPoint prev = quadControl ?? lastQuadControl ?? pos;
							SvgPoint c = new(2f * pos.X - prev.X, 2f * pos.Y - prev.Y);
							SvgPoint p = ReadPoint(tokens, ref i, cmdToken);
							if (relative) p = Add(pos, p);
							current.Add(p, c);
							pos = p;
							quadControl = c;
						} while (NextIsNumber(tokens, i));
						break;
					case 'C':
					case 'S':
						current = Continue(current!, subpaths, start);
						do {
							// S: the first control mirrors the previous cubic's second one; without a preceding
							// C/S it is the current point.
							SvgPoint c1;
							if (upper == 'C') {
								c1 = ReadPoint(tokens, ref i, cmdToken);
								if (relative) c1 = Add(pos, c1);
							} else {
								SvgPoint prev = cubicControl ?? lastCubicControl ?? pos;
								c1 = new SvgPoint(2f * pos.X - prev.X, 2f * pos.Y - prev.Y);
							}
							SvgPoint c2 = ReadPoint(tokens, ref i, cmdToken);
							SvgPoint p = ReadPoint(tokens, ref i, cmdToken);
							if (relative) {
								c2 = Add(pos, c2);
								p = Add(pos, p);
							}
							AddCubic(current, pos, c1, c2, p, 0);
							pos = p;
							cubicControl = c2;
						} while (NextIsNumber(tokens, i));
						break;
					case 'Z':
						current!.Closed = true;
						// Illustrator often repeats the start point before Z. Only drop it when that last
						// segment is straight, or the curve into the start point would be lost.
						if (current.Points.Count > 1 && SamePoint(current.Points[^1], start) && current.Controls[^1] == null) {
							current.Points.RemoveAt(current.Points.Count - 1);
							current.Controls.RemoveAt(current.Controls.Count - 1);
						}
						pos = start;
						break;
					default:
						throw new NotSupportedException($"path command '{cmd}' at {cmdToken.Index} is not supported yet");
				}
				lastQuadControl = quadControl;
				lastCubicControl = cubicControl;
			}
			return subpaths;
		}

		// One quadratic with control (3(c1 + c2) - p0 - p3) / 4 matches the cubic's ends and end
		// directions; its distance from the cubic is at most sqrt(3)/36 * |p3 - 3c2 + 3c1 - p0|. Too far,
		// and the cubic is halved (de Casteljau) and each half tried again.
		private static void AddCubic(SvgSubpath sub, SvgPoint p0, SvgPoint c1, SvgPoint c2, SvgPoint p3, int depth) {
			float ex = p3.X - 3f * c2.X + 3f * c1.X - p0.X;
			float ey = p3.Y - 3f * c2.Y + 3f * c1.Y - p0.Y;
			float error = 0.0481125f * (float)Math.Sqrt(ex * ex + ey * ey);
			if (error <= CubicTolerance || depth >= MaxCubicSplitDepth) {
				SvgPoint q = new((3f * (c1.X + c2.X) - p0.X - p3.X) / 4f, (3f * (c1.Y + c2.Y) - p0.Y - p3.Y) / 4f);
				sub.Add(p3, q);
				return;
			}
			SvgPoint a = Mid(p0, c1), b = Mid(c1, c2), c = Mid(c2, p3);
			SvgPoint ab = Mid(a, b), bc = Mid(b, c);
			SvgPoint m = Mid(ab, bc);
			AddCubic(sub, p0, a, ab, m, depth + 1);
			AddCubic(sub, m, bc, c, p3, depth + 1);
		}

		private static SvgPoint Mid(SvgPoint a, SvgPoint b) => new((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);

		// After Z, a drawing command without a new M starts another subpath at the closed one's start.
		private static SvgSubpath Continue(SvgSubpath current, List<SvgSubpath> subpaths, SvgPoint start) {
			if (!current.Closed) return current;
			SvgSubpath next = new();
			next.Add(start, null);
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
