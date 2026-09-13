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
		// Parallel to pts, world-space absolute handle point (anchor + tangent) per curve anchor.
		// handles[i] == pts[i] means "no drag, auto-tangent" - can't use Vector3.zero as that sentinel
		// since it's a real map location.
		[JsonProperty("handles")]
		public List<Vector3> handles = [];
		[JsonProperty("label")]
		public string? Label;
		[JsonProperty("description")]
		public string? Description;
		[JsonProperty("planId")]
		public int PlanId;
		[JsonProperty("parallelLanes")]
		public List<ParallelLane> ParallelLanes = [];
		[JsonProperty("parallelSpacing")]
		public float ParallelSpacing;
		public Bounds Extents;
	}

	public class ParallelLane {
		[JsonProperty("layerId")]
		public string LayerId;
		[JsonProperty("count")]
		public int Count;
	}
}
