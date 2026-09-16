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
		// Mid-draw preview only, lines (Tools.path) - populated on the transient "__preview__" shape
		// while dragging. One independent <path transform="translate(dx,dy)"> per entry client-side,
		// placed inside that entry's OWN layer group. A single dx/dy delta is exact for a line's live
		// rubber-band but wrong for a curve (see PreviewCurveLanes below and CurveMath.OffsetPolyline).
		[JsonProperty("parallelLanes")]
		public List<ParallelLaneDto> ParallelLanes = [];
		// World-unit (metre) spacing between adjacent lanes - same value for every lane on this
		// shape. Only meaningful alongside ParallelLanes; the client can't derive this from the
		// already-computed screen-space Dx/Dy deltas alone (that requires knowing the current
		// camera zoom scale too), so it's sent verbatim for UI display (e.g. a "Xm" hover tooltip).
		[JsonProperty("parallelSpacing")]
		public float ParallelSpacing;
		// Curve (Tools.curve) mid-draw preview only, populated on the transient "__preview__" shape
		// while dragging. A curve lane can't be expressed as one dx/dy delta like a line lane (see
		// ParallelLaneDto) - each needs its own full offset polyline, already sampled/offset/projected
		// to screen space server-side.
		[JsonProperty("previewCurveLanes")]
		public List<PreviewCurveLaneDto> PreviewCurveLanes = [];
	}

	public class PreviewCurveLaneDto {
		[JsonProperty("layerId")]
		public string? LayerId;
		[JsonProperty("pts")]
		public List<ScreenPt> Pts = [];
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
