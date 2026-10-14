using Newtonsoft.Json.Linq;
using Skyplan.Models.Enums;

namespace Skyplan.Persistence.Helpers {
	public static class LayerMerger {
		public static Action<string>? LogInfo;
		public static Action<string>? LogDebug;
		public static Action<string>? LogWarn;

		public static JObject LoadAndMerge(string defaultLayerPath, string userLayersPath) {
			JObject merged = [];
			JObject defaultLayers = JObject.Parse(File.ReadAllText(defaultLayerPath));
			JObject? userLayers = null;

			if (!File.Exists(userLayersPath)) {
				LogInfo?.Invoke($"[LayerMerger] no user layers file found, creating one from defaults at '{userLayersPath}'");
				Directory.CreateDirectory(Path.GetDirectoryName(userLayersPath)!);
				File.Copy(defaultLayerPath, userLayersPath);
				return defaultLayers;
			}

			try {
				userLayers = JObject.Parse(File.ReadAllText(userLayersPath));
				LogDebug?.Invoke($"[LayerMerger] loaded user layers from '{userLayersPath}'");
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
				LogDebug?.Invoke("[LayerMerger] merged global labelStyle");
			}

			// logic for the layers
			List<JObject> defaultLayersDef = defaultLayers["layers"] is JArray defDefaultArray ? [.. defDefaultArray.OfType<JObject>()] : [];
			List<JObject> userLayersDef = userLayers["layers"] is JArray defUserArray ? [.. defUserArray.OfType<JObject>()] : [];

			List<JObject> mergedLayers = MergeLayers(defaultLayersDef, userLayersDef, MergePolicies.UserWins);
			LogInfo?.Invoke($"[LayerMerger] merged {defaultLayersDef.Count} default + {userLayersDef.Count} user layers -> {mergedLayers.Count} total");

			merged["layers"] = JArray.FromObject(mergedLayers);
			return merged;
		}

		// A shape's layer as seen in the plan: its id, label, style snapshot and the tool that drew it.
		public class UsedLayer {
			public string Id { get; }
			public string Label { get; }
			public IDictionary<string, string> Style { get; }
			public string Tool { get; }

			public UsedLayer(string id, string label, IDictionary<string, string> style, string tool) {
				Id = id;
				Label = label;
				Style = style;
				Tool = tool;
			}
		}

		// Shapes can reference layers the config doesn't have (imported SVGs, a user layer deleted
		// from layers.json). The UI only styles config layers, so those shapes would draw invisible.
		// Appends one layer per missing id, built from the first shape's style snapshot, with
		// allowedTools = every tool used on it. Not persisted; the config is rebuilt on each load.
		public static int AppendMissingLayers(JObject merged, IEnumerable<UsedLayer> used) {
			if (merged["layers"] is not JArray layers) {
				layers = [];
				merged["layers"] = layers;
			}
			HashSet<string> known = new(
				layers.OfType<JObject>().Select(l => l["id"]?.Value<string>()).OfType<string>(),
				StringComparer.OrdinalIgnoreCase);

			int added = 0;
			foreach (IGrouping<string, UsedLayer> group in used
				.Where(u => !string.IsNullOrEmpty(u.Id) && !known.Contains(u.Id))
				.GroupBy(u => u.Id, StringComparer.OrdinalIgnoreCase)) {
				UsedLayer first = group.First();
				layers.Add(new JObject {
					["id"] = first.Id,
					["label"] = string.IsNullOrEmpty(first.Label) ? first.Id : first.Label,
					["allowedTools"] = new JArray(group.Select(u => u.Tool).Distinct()),
					["style"] = JObject.FromObject(first.Style),
					["labelStyle"] = new JObject(),
				});
				added++;
			}
			if (added > 0) LogInfo?.Invoke($"[LayerMerger] appended {added} layer(s) used by shapes but missing from the config");
			return added;
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
					LogDebug?.Invoke($"[LayerMerger] merging user override for layer '{defId}'");
					output.Add(MergeObjects(def, userDef));
				} else {
					output.Add(def);
				}
			}

			foreach (JObject userLayer in user) {
				string id = userLayer["id"]?.Value<string>() ?? "";
				if (!defIds.Contains(id)) {
					LogDebug?.Invoke($"[LayerMerger] appending user-only layer '{id}'");
					output.Add(userLayer);
				}
			}

			return output;
		}
	}
}
