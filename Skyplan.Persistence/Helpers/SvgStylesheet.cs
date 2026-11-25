using System.Text.RegularExpressions;

namespace Skyplan.Persistence.Helpers {
	// The class rules of an SVG's <style> blocks. Illustrator writes every style this way
	// (.st0{stroke:#333333;...} plus class="st0"). Only plain class selectors are read; other
	// selectors are rare in exported SVGs and are ignored.
	public sealed class SvgStylesheet {
		private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
		// Innermost "selectors{declarations}", so rules inside an @media block are still found
		private static readonly Regex Rule = new(@"([^{}]+)\{([^{}]*)\}", RegexOptions.Compiled);
		private static readonly Regex ClassSelector = new(@"^\.(-?[A-Za-z_][\w-]*)$", RegexOptions.Compiled);

		// In stylesheet order: for one specificity, a later rule wins
		private readonly List<(string Class, Dictionary<string, string> Declarations)> m_Rules = [];

		public int Count => m_Rules.Count;

		public static SvgStylesheet Parse(string css) {
			SvgStylesheet sheet = new();
			foreach (Match m in Rule.Matches(Comment.Replace(css, ""))) {
				Dictionary<string, string> declarations = ParseDeclarations(m.Groups[2].Value);
				foreach (string selector in m.Groups[1].Value.Split([','])) {
					Match c = ClassSelector.Match(selector.Trim());
					if (c.Success) sheet.m_Rules.Add((c.Groups[1].Value, declarations));
				}
			}
			return sheet;
		}

		// Adds the declarations of every rule matching one of the space separated classes
		public void Apply(string? classAttribute, Dictionary<string, string> style) {
			if (classAttribute is null || m_Rules.Count == 0) return;
			string[] classes = classAttribute.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
			if (classes.Length == 0) return;
			foreach ((string cls, Dictionary<string, string> declarations) in m_Rules) {
				if (Array.IndexOf(classes, cls) < 0) continue;
				foreach (KeyValuePair<string, string> d in declarations) style[d.Key] = d.Value;
			}
		}

		// "a:1;b:2" -> {a:1, b:2}; the format of both a CSS rule body and a style="" attribute
		public static Dictionary<string, string> ParseDeclarations(string declarations) {
			Dictionary<string, string> result = [];
			foreach (string part in declarations.Split([';'])) {
				int colon = part.IndexOf(':');
				if (colon <= 0) continue;
				string name = part.Substring(0, colon).Trim();
				string value = part.Substring(colon + 1).Trim();
				if (name.Length > 0 && value.Length > 0) result[name] = value;
			}
			return result;
		}
	}
}
