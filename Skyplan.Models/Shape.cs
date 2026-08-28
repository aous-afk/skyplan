using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Skyplan.Models.dto;
using UnityEngine;

namespace Skyplan.Models{
	public class Shape {
		[JsonProperty("id")]
		public string id;
		[JsonProperty("type")]
		[JsonConverter(typeof(StringEnumConverter))]
		public Tools Type;
		[JsonProperty("layer")]
		public LayerDefDto layer;
		[JsonProperty("pts")]
		public List<Vector3> pts = [];
		[JsonProperty("label")]
		public string? Label;
		[JsonProperty("description")]
		public string? Description;
	}
}
