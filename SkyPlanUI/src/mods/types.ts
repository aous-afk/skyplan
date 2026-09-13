import {faBezierCurve, faDrawPolygon, faEraser, faFont, faLocationDot, faRoad} from '@fortawesome/free-solid-svg-icons';

export const TOOLS = [
  { id: 'path',
	label: 'Line',
	icon: faRoad,
	activeStyle: {},
	allowMultiSelect: true
  },

  { id: 'polygon',
	label: 'Polygon',
	icon: faDrawPolygon,
	activeStyle: {},
	allowMultiSelect: false
  },

  { id: 'curve',
	label: 'Curve',
	icon: faBezierCurve,
	activeStyle: {},
	allowMultiSelect: true
  },

  { id: 'point',
	label: 'Point',
	icon: faLocationDot,
	activeStyle: {},
	// Revisit later - server-side placement existed briefly (offset extra layers along +X) but
	// looked wrong visually and was reverted; disabled here until a real design lands.
	allowMultiSelect: false
  },

  { id: 'text',
	label: 'Annotate',
	icon: faFont,
	activeStyle: {},
	allowMultiSelect: false
  },

  { id: 'erase',
	label: 'Erase',
	icon: faEraser,
	activeStyle: { background: '#3a1a00', color: '#ffaa55' },
	allowMultiSelect: false
  },

] as const;
export type Tool = typeof TOOLS[number];
export type ToolId = typeof TOOLS[number]['id'];

export type Layer = string;

export interface ShapeData {
	id: string;
	tag: Tag;
	layerId: string;
	pts: { x: number; y: number }[];
	handles: { x: number; y: number }[];
	inFrame: boolean;
	label?: string;
	description?: string;
	// Lines only - one independent <path> per entry, placed inside that entry's own layer group
	// (not necessarily this shape's own layer) and translated by dx/dy. label/description are the
	// lane's own, independent of this shape's own label/description - set via setLaneLabel/setLaneNote.
	parallelLanes?: { layerId: string; dx: number; dy: number; label?: string; description?: string }[];
	// World-unit (metre) spacing between adjacent lanes - only meaningful alongside parallelLanes
	parallelSpacing?: number;
}

export interface LabelStyle {
	color?: string;
	fontSize?: number;
	fontWeight?: string;
	opacity?: number;
}

export interface LayerIcon {
	path: string;
	color?: string;
}

export interface LayerDef {
	id: string;
	label: string;
	allowedTools: ToolId[];
	style: Record<string, string | number>;
	labelStyle?: LabelStyle;
	icon?: LayerIcon;
}

export enum Tag {
  none = 'none',
  path = 'path',
  polygon = 'polygon',
  curve = 'curve',
  circle = 'circle',
  text = 'text',
}
