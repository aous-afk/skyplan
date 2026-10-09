using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Skyplan.Models.dto {
	public class SaveLayerStyleDto {
		// Save as: the source layer.
		[JsonProperty("layerId")]
		public string LayerId { get; set; }
		[JsonProperty("style")]
		public Dictionary<string, string> Style { get; set; }
		// JObject, not a typed DTO, so label keys C# doesn't know yet survive deserialization.
		[JsonProperty("labelStyle")]
		public JObject LabelStyle { get; set; }
	}

	public class SaveLayerStyleAsDto : SaveLayerStyleDto {
		[JsonProperty("newId")]
		public string NewId { get; set; }
		[JsonProperty("label")]
		public string Label { get; set; }
	}
}
