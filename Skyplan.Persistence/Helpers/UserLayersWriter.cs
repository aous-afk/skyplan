using Newtonsoft.Json.Linq;
using Skyplan.Models.dto;

namespace Skyplan.Persistence.Helpers {

	public static class UserLayersWriter {
		// Same fallbacks as StyleContext.labelStyleFor. JS sends the fully resolved label style, so without
		// these a key nobody sets would always look changed and get written.
		private static readonly JObject HardLabelDefaults = new() {
			["color"] = "#ffffff",
			["fontSize"] = 12,
			["fontWeight"] = "normal",
			["opacity"] = 1,
		};

		public static void SaveStyle(JObject userRoot, SaveLayerStyleDto dto, JObject? defaultLayer, JObject? globalLabelStyle) {
			JArray layers = LayersOf(userRoot);
			JObject? entry = FindLayer(layers, dto.LayerId);
			if (entry is null) {
				entry = new JObject { ["id"] = dto.LayerId };
				layers.Add(entry);
			}

			entry["style"] = JObject.FromObject(dto.Style);

			JObject labelStyle = entry["labelStyle"] as JObject ?? [];
			WriteLabelDiff(labelStyle, dto.LabelStyle, defaultLayer?["labelStyle"] as JObject, globalLabelStyle);
			entry["labelStyle"] = labelStyle;
		}

		public static bool SaveStyleAs(JObject userRoot, SaveLayerStyleAsDto dto, JObject sourceMerged, JObject? globalLabelStyle) {
			JArray layers = LayersOf(userRoot);
			if (FindLayer(layers, dto.NewId) is not null) return false;

			JObject copy = (JObject)sourceMerged.DeepClone();
			copy["id"] = dto.NewId;
			copy["label"] = dto.Label;
			copy["style"] = JObject.FromObject(dto.Style);

			JObject labelStyle = [];
			WriteLabelDiff(labelStyle, dto.LabelStyle, null, globalLabelStyle);
			copy["labelStyle"] = labelStyle;

			layers.Add(copy);
			return true;
		}

		public static JObject? FindLayer(JObject root, string id) =>
			root["layers"] is JArray layers ? FindLayer(layers, id) : null;

		private static JObject? FindLayer(JArray layers, string id) =>
			layers.OfType<JObject>().FirstOrDefault(l =>
				string.Equals(l["id"]?.Value<string>(), id, StringComparison.OrdinalIgnoreCase));

		private static void WriteLabelDiff(JObject target, JObject? draft, JObject? layerDefaults, JObject? globalLabelStyle) {
			if (draft is null) return;
			foreach (JProperty prop in draft.Properties()) {
				JToken? inherited = layerDefaults?[prop.Name] ?? globalLabelStyle?[prop.Name] ?? HardLabelDefaults[prop.Name];
				if (inherited is not null && JToken.DeepEquals(inherited, prop.Value))
					target.Remove(prop.Name);
				else
					target[prop.Name] = prop.Value.DeepClone();
			}
		}

		private static JArray LayersOf(JObject userRoot) {
			if (userRoot["layers"] is JArray layers) return layers;
			layers = [];
			userRoot["layers"] = layers;
			return layers;
		}
	}
}
