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
			foreach (Shape s in shapes) s.id = $"s{nextId++}";
			return shapes;
		}
	}
}
