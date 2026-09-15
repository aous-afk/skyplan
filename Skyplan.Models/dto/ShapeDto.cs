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
		// Lines (Tools.path) only - one independent <path transform="translate(dx,dy)"> per entry
		// client-side, placed inside that entry's OWN layer group (not necessarily this shape's own
		// layer), so per-layer opacity/visibility keeps working correctly even when lanes mix layers.
		// Empty for curves/ineligible shapes - a single dx/dy delta is exact for a line but wrong for
		// a curve (see PreviewCurveLanes below and CurveMath.OffsetPolyline).
		[JsonProperty("parallelLanes")]
		public List<ParallelLaneDto> ParallelLanes = [];
		// World-unit (metre) spacing between adjacent lanes - same value for every lane on this
		// shape. Only meaningful alongside ParallelLanes; the client can't derive this from the
		// already-computed screen-space Dx/Dy deltas alone (that requires knowing the current
		// camera zoom scale too), so it's sent verbatim for UI display (e.g. a "Xm" hover tooltip).
		[JsonProperty("parallelSpacing")]
		public float ParallelSpacing;
		// Curve (Tools.curve) mid-draw preview only - real committed curve lanes are ordinary
		// independent Shapes (see DrawingSystem.HandleDrawEnd), so this only ever gets populated on
		// the transient "__preview__" shape while dragging. A curve lane can't be expressed as one
		// dx/dy delta like a line lane (see ParallelLaneDto) - each needs its own full offset polyline,
		// already sampled/offset/projected to screen space server-side.
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
		// Own name/note, independent of the parent shape's Label/Description - see ParallelLane.
		[JsonProperty("label")]
		public string? Label;
		[JsonProperty("description")]
		public string? Description;
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
