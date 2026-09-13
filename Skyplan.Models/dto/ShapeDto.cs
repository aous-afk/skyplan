using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Skyplan.Models.dto {
	public class ShapeDto {
		[JsonProperty("id")]
		public string? Id;
		[JsonProperty("tag")]
		[JsonConverter(typeof(StringEnumConverter))]
		public Tag Tag;
		[JsonProperty("layerId")]
		public string? LayerId;
		[JsonProperty("pts")]
		public List<ScreenPt> Pts = [];
		[JsonProperty("handles")]
		public List<ScreenPt> Handles = [];
		[JsonProperty("inFrame")]
		public bool InFrame;
		[JsonProperty("label")]
		public string? Label;
		[JsonProperty("description")]
		public string? Description;
		// Lines (Tools.path) only - one <use transform="translate(dx,dy)"> per entry client-side,
		// placed inside that entry's OWN layer group (not necessarily this shape's own layer), so
		// per-layer opacity/visibility keeps working correctly even when lanes mix layers. Empty for
		// curves/ineligible shapes.
		[JsonProperty("parallelLanes")]
		public List<ParallelLaneDto> ParallelLanes = [];
	}

	public class ParallelLaneDto {
		[JsonProperty("layerId")]
		public string? LayerId;
		[JsonProperty("dx")]
		public float Dx;
		[JsonProperty("dy")]
		public float Dy;
	}

	public class ScreenPt {
		[JsonProperty("x")]
		public float x;
		[JsonProperty("y")]
		public float y;
	}

	public enum Tag {
	  none,
	  path,
	  polygon,
	  curve,
	  circle,
	  text,
	}
}
