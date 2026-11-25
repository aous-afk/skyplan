namespace Skyplan.Persistence.Helpers {
	// Distinct styles of imported shapes, each rendered as one CSS class (.sp-s-{index}) rather than
	// inline style on every shape. Equal styles share an index.
	public sealed class ShapeStyleTable {
		// SVG's initial values. Every table style gets these for the keys it lacks: the shapes also sit in
		// their layer's <g class="sp-{layer}"> and carry that class, whose style would otherwise fill in
		// the gaps (stroke-dasharray is inherited, so one dashed layer style dashes every line).
		private static readonly KeyValuePair<string, string>[] Initial = [
			new("fill", "black"),
			new("fill-opacity", "1"),
			new("stroke", "none"),
			new("stroke-width", "1"),
			new("stroke-opacity", "1"),
			// "0", not "none": GameFace drops "none" (and "initial") here, so the inherited dash stays.
			// "0" draws solid; "1,0" also parses but draws faded.
			new("stroke-dasharray", "0"),
			new("stroke-linecap", "butt"),
			new("stroke-linejoin", "miter"),
			new("opacity", "1"),
		];

		public readonly List<Dictionary<string, string>> Styles = [];
		private readonly Dictionary<string, int> m_IndexByKey = [];

		public int Add(IReadOnlyDictionary<string, string> style) {
			Dictionary<string, string> complete = [];
			foreach (KeyValuePair<string, string> kv in Initial) complete[kv.Key] = kv.Value;
			foreach (KeyValuePair<string, string> kv in style) complete[kv.Key] = kv.Value;
			// Files write "none" too (Inkscape does), which GameFace would drop the same way
			if (complete["stroke-dasharray"].Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
				complete["stroke-dasharray"] = "0";

			string key = string.Join(";", complete.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}:{kv.Value}"));
			if (m_IndexByKey.TryGetValue(key, out int index)) return index;
			index = Styles.Count;
			Styles.Add(complete);
			m_IndexByKey[key] = index;
			return index;
		}
	}
}
