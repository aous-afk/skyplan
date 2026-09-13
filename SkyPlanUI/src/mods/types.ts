import {faBezierCurve, faDrawPolygon, faEraser, faFont, faLocationDot, faRoad} from '@fortawesome/free-solid-svg-icons';

export const TOOLS = [
  { id: 'path',
	label: 'Line',
	icon: faRoad,
	activeStyle: {}
  },

  { id: 'polygon',
	label: 'Polygon',
	icon: faDrawPolygon,
	activeStyle: {} },

  { id: 'curve',
	label: 'Curve',
	icon: faBezierCurve,
	activeStyle: {} },

  { id: 'point',
	label: 'Point',
	icon: faLocationDot,
	activeStyle: {} },

  { id: 'text',
	label: 'Annotate',
	icon: faFont,
	activeStyle: {} },

  { id: 'erase',
	label: 'Erase',
	icon: faEraser,
	activeStyle: { background: '#3a1a00', color: '#ffaa55' }
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
	// (not necessarily this shape's own layer) and translated by dx/dy
	parallelLanes?: { layerId: string; dx: number; dy: number }[];
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
