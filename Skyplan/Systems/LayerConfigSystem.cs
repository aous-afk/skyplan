using Colossal.UI.Binding;
using Game.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Skyplan.Cross;
using Skyplan.Models.dto;
using Skyplan.Persistence.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Skyplan.Systems {

	public partial class LayerConfigSystem : UISystemBase {
		private ValueBinding<string> m_LayersConfigBinding;
		private readonly Dictionary<string, LayerDefDto> m_LayerById = new(StringComparer.OrdinalIgnoreCase);
		private JObject m_Merged = [];

		protected override void OnCreate() {
			base.OnCreate();
			m_LayersConfigBinding = new ValueBinding<string>("skyplan", "layersConfig", "{\"layers\":[]}");
			AddBinding(new TriggerBinding<string>("skyplan", "saveLayerStyle", val => {
				SaveLayerStyleDto dto = JsonConvert.DeserializeObject<SaveLayerStyleDto>(val);
				HandleSaveStyle(dto);
			}));
			AddBinding(new TriggerBinding<string>("skyplan", "saveLayerStyleAs", val => {
				SaveLayerStyleAsDto dto = JsonConvert.DeserializeObject<SaveLayerStyleAsDto>(val);
				HandleSaveAsStyle(dto);
			}));
			AddBinding(m_LayersConfigBinding);
			Reload();
		}

		private void HandleSaveAsStyle(SaveLayerStyleAsDto dto) {
			if (dto?.LayerId == null || dto.NewId == null || dto.Label == null || dto.Style == null) {
				Mod.log.Warn("[LayerConfig] saveLayerStyleAs: bad payload");
				return;
			}
			if (GetLayer(dto.NewId) != null) {
				Mod.log.Warn($"[LayerConfig] saveLayerStyleAs: layer '{dto.NewId}' already exists");
				return;
			}
			JObject source = UserLayersWriter.FindLayer(m_Merged, dto.LayerId);
			if (source == null) {
				Mod.log.Warn($"[LayerConfig] saveLayerStyleAs: source layer '{dto.LayerId}' not found");
				return;
			}
			try {
				JObject userRoot = JObject.Parse(File.ReadAllText(Paths.UserLayers));
				if (!UserLayersWriter.SaveStyleAs(userRoot, dto, source, m_Merged["labelStyle"] as JObject)) {
					Mod.log.Warn($"[LayerConfig] saveLayerStyleAs: layer '{dto.NewId}' already in the user file");
					return;
				}
				File.WriteAllText(Paths.UserLayers, userRoot.ToString(Formatting.Indented));
			} catch (Exception ex) {
				Mod.log.Error($"[LayerConfig] saveLayerStyleAs failed: {ex.Message}");
				return;
			}
			Reload();
		}

		private void HandleSaveStyle(SaveLayerStyleDto dto) {
			if (dto?.LayerId == null || dto.Style == null) {
				Mod.log.Warn("[LayerConfig] saveLayerStyle: bad payload");
				return;
			}
			try {
				JObject userRoot = JObject.Parse(File.ReadAllText(Paths.UserLayers));
				// The default layer, not the merged one: the merged one already contains the user's own values.
				JObject defaults = JObject.Parse(File.ReadAllText(Paths.DefaultLayers));
				UserLayersWriter.SaveStyle(userRoot, dto, UserLayersWriter.FindLayer(defaults, dto.LayerId), m_Merged["labelStyle"] as JObject);
				File.WriteAllText(Paths.UserLayers, userRoot.ToString(Formatting.Indented));
			} catch (Exception ex) {
				Mod.log.Error($"[LayerConfig] saveLayerStyle failed: {ex.Message}");
				return;
			}
			Reload();
		}

		public void Reload() {
			JObject merged = LayerMerger.LoadAndMerge(Paths.DefaultLayers, Paths.UserLayers);
			m_Merged = merged;
			m_LayersConfigBinding.Update(merged.ToString(Formatting.None));

			m_LayerById.Clear();
			if (merged["layers"] is not JArray layers) return;
			foreach (JObject layer in layers.OfType<JObject>()) {
				LayerDefDto dto = layer.ToObject<LayerDefDto>();
				if (dto?.Id != null) m_LayerById[dto.Id] = dto;
			}
		}

		public LayerDefDto GetLayer(string layerId) =>
			layerId != null && m_LayerById.TryGetValue(layerId, out LayerDefDto layer) ? layer : null;

	}
}
