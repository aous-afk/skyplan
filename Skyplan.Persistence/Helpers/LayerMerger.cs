using Newtonsoft.Json.Linq;
using Skyplan.Models.Enums;

namespace Skyplan.Persistence.Helpers {
	public static class LayerMerger {
		public static Action<string>? LogInfo;
		public static Action<string>? LogWarn;

		public static JObject LoadAndMerge(string defaultLayerPath, string userLayersPath) {
			JObject merged = [];
			JObject defaultLayers = JObject.Parse(File.ReadAllText(defaultLayerPath));
			JObject? userLayers = null;

			if (!File.Exists(userLayersPath)) {
				LogInfo?.Invoke($"[LayerMerger] no user layers file found at '{userLayersPath}', using defaults");
				return defaultLayers;
			}

			try {
				userLayers = JObject.Parse(File.ReadAllText(userLayersPath));
				LogInfo?.Invoke($"[LayerMerger] loaded user layers from '{userLayersPath}'");
			} catch (Exception ex) {
				LogWarn?.Invoke($"[LayerMerger] failed to parse user layers file: {ex.Message}");
			}

			if (userLayers is null) {
				LogWarn?.Invoke("[LayerMerger] user layers is null after parse, falling back to defaults");
				return defaultLayers;
			}

			// logic for global label style
			if (defaultLayers["labelStyle"] is JObject defaultGlobalLabelStyel && userLayers["labelStyle"] is JObject userGlobalLabelStyel) {
				merged["labelStyle"] = MergeObjects(defaultGlobalLabelStyel, userGlobalLabelStyel);
				LogInfo?.Invoke("[LayerMerger] merged global labelStyle");
			}

			// logic for the layers
			List<JObject> defaultLayersDef = defaultLayers["layers"] is JArray defDefaultArray ? [.. defDefaultArray.OfType<JObject>()] : [];
			List<JObject> userLayersDef = userLayers["layers"] is JArray defUserArray ? [.. defUserArray.OfType<JObject>()] : [];

			LogInfo?.Invoke($"[LayerMerger] merging {defaultLayersDef.Count} default layers with {userLayersDef.Count} user layers");
			List<JObject> mergedLayers = MergeLayers(defaultLayersDef, userLayersDef, MergePolicies.UserWins);
			LogInfo?.Invoke($"[LayerMerger] merge result: {mergedLayers.Count} layers");

			merged["layers"] = JArray.FromObject(mergedLayers);
			return merged;
		}

		public static JObject MergeObjects(JObject @default, JObject user) {
			JObject clonedDefaults = (JObject)@default.DeepClone();
			clonedDefaults.Merge(user, new JsonMergeSettings {
				MergeArrayHandling = MergeArrayHandling.Union
			});
			return clonedDefaults;
		}

		public static List<JObject> MergeLayers(List<JObject> @default, List<JObject> user, MergePolicies mergeObjects) {
			List<JObject> output = [];
			Dictionary<string?, JObject> userById = user.Where(layer =>
					layer["id"]?.Value<string>() is not null)
				.ToDictionary(l => l["id"]!.Value<string>(), StringComparer.OrdinalIgnoreCase);

			HashSet<string> defIds = new(StringComparer.OrdinalIgnoreCase);
			foreach (JObject def in @default) {
				string? defId = def["id"]?.Value<string>();
				if (defId is null) {
					LogWarn?.Invoke("[LayerMerger] skipping default layer with missing id");
					continue;
				}
				defIds.Add(defId);
				if (userById.TryGetValue(defId, out JObject? userDef)) {
					LogInfo?.Invoke($"[LayerMerger] merging user override for layer '{defId}'");
					output.Add(MergeObjects(def, userDef));
				} else {
					output.Add(def);
				}
			}

			foreach (JObject userLayer in user) {
				string id = userLayer["id"]?.Value<string>() ?? "";
				if (!defIds.Contains(id)) {
					LogInfo?.Invoke($"[LayerMerger] appending user-only layer '{id}'");
					output.Add(userLayer);
				}
			}

			return output;
		}
	}
}
