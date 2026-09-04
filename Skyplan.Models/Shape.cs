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
		[JsonProperty("planId")]
		public int PlanId;
		public Bounds Extents;

		public IReadOnlyList<Vector3> GetSnapVertices() => pts;

		public IEnumerable<(Vector3 a, Vector3 b)> GetSnapSegments() {
		  for (int i = 0; i < pts.Count - 1; i++)
			yield return (pts[i], pts[i + 1]);
		  if (Type == Tools.polygon && pts.Count > 2)
			yield return (pts[pts.Count - 1], pts[0]);
		}
	}
}
