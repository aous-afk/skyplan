using Newtonsoft.Json;

namespace Skyplan.Models.dto {
	public class LayerDefDto {
		[JsonProperty("id")]
		public string Id { get; set; }
		[JsonProperty("label")]
		public string Label { get; set; }
		[JsonProperty("style")]
		public Dictionary<string, string> Style { get; set; }
	}
}
