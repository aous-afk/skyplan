using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Skyplan.Persistence {
	public class Vector3Converter : JsonConverter<Vector3> {
		public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer) {
			writer.WriteStartObject();
			writer.WritePropertyName("x"); writer.WriteValue(value.x);
			writer.WritePropertyName("y"); writer.WriteValue(value.y);
			writer.WritePropertyName("z"); writer.WriteValue(value.z);
			writer.WriteEndObject();
		}

		public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer) {
			float x = 0, y = 0, z = 0;
			if (reader.TokenType != JsonToken.StartObject) return default;
			while (reader.Read() && reader.TokenType != JsonToken.EndObject) {
				if (reader.TokenType != JsonToken.PropertyName) continue;
				string name = (string)reader.Value;
				reader.Read();
				float v = Convert.ToSingle(reader.Value);
				switch (name) {
					case "x": x = v; break;
					case "y": y = v; break;
					case "z": z = v; break;
				}
			}
			return new Vector3(x, y, z);
		}
	}
}
