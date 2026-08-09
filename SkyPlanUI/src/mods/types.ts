import {faDrawPolygon, faEraser, faFont, faLocationDot, faRoad} from '@fortawesome/free-solid-svg-icons';

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

export interface ShapeLayerDef {
	id: string;
	label: string;
	style: Record<string, string>;
}

export interface ShapeData {
	id: string;
	tag: Tag;
	layerId: string;
	layerDef?: ShapeLayerDef;
	pts: { x: number; y: number }[];
	inFrame: boolean;
	label?: string;
	description?: string;
}

export interface LabelStyle {
	color?: string;
	fontSize?: number;
	fontWeight?: string;
	opacity?: number;
}

export interface LayerDef {
	id: string;
	label: string;
	allowedTools: ToolId[];
	style: Record<string, string | number>;
	labelStyle?: LabelStyle;
}

export enum Tag {
  none = 'none',
  path = 'path',
  polygon = 'polygon',
  circle = 'circle',
  text = 'text',
}
