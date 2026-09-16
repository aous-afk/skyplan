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
	// Mid-draw preview only, lines - appears on the transient preview shape while dragging. One
	// independent <path> per entry, placed inside that entry's own layer group, translated by dx/dy.
	parallelLanes?: { layerId: string; dx: number; dy: number }[];
	// World-unit (metre) spacing between adjacent lanes - only meaningful alongside parallelLanes
	parallelSpacing?: number;
	// Curve mid-draw preview only. A curve lane can't use a single dx/dy delta like a line lane, so
	// each carries its own already-offset/projected point list.
	previewCurveLanes?: { layerId: string; pts: { x: number; y: number }[] }[];
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
