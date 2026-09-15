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

	// One entry per lane, in click order - no Count multiplier (removed 2026-09-15, see dev_doc.md:
	// a {layer,count} queue can't represent Train->Subway->Train's actual click order, only a flat
	// ordered list can).
	public class ParallelLane {
		[JsonProperty("layerId")]
		public string LayerId;
		// Independent from the parent Shape's own Label/Description - each lane is a visually
		// distinct line (own layer/style), so it gets its own name/note too, even though it shares
		// the parent's geometry. Set via setLaneLabel/setLaneNote (DrawingSystem.cs), scoped by
		// shapeId+layerId, not the shared setShapeLabel/setShapeNote (shapeId only).
		[JsonProperty("label")]
		public string? Label;
		[JsonProperty("description")]
		public string? Description;
	}
}
