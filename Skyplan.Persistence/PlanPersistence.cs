using Newtonsoft.Json;
using Skyplan.Models;
using System.Collections.Generic;

namespace Skyplan.Persistence {
	public static class PlanPersistence {
		private static readonly JsonSerializerSettings Settings = new() {
			Converters = { new Vector3Converter() },
			Formatting = Formatting.Indented,
		};

		public static string Export(List<Shape> shapes) => JsonConvert.SerializeObject(shapes, Settings);

		public static List<Shape> Import(string json, ref int nextId) {
			List<Shape> shapes = JsonConvert.DeserializeObject<List<Shape>>(json, Settings) ?? [];
			foreach (Shape s in shapes) {
				if (TryParseIdNumber(s.id, out int n) && n >= nextId) nextId = n + 1;
			}
			return shapes;
		}

		private static bool TryParseIdNumber(string id, out int n) {
			n = 0;
			if (string.IsNullOrEmpty(id) || id[0] != 's') return false;
			return int.TryParse(id.Substring(1), out n);
		}
	}
}
