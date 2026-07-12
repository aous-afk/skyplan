import {faDrawPolygon, faEraser, faLocationDot, faRoad} from '@fortawesome/free-solid-svg-icons';

export const TOOLS = [
  { id: 'path',    label: 'Line',    icon: faRoad,        activeStyle: {} },
  { id: 'polygon', label: 'Polygon', icon: faDrawPolygon, activeStyle: {} },
  { id: 'point',   label: 'Point',   icon: faLocationDot, activeStyle: {} },
  { id: 'erase',   label: 'Erase',   icon: faEraser,      activeStyle: { background: '#3a1a00', color: '#ffaa55' } },
  // {id :'path', lap},
  // {id :'rect'},
  // {id :'circle'},
  // {id :'free'},
  // {id :'erase'}
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
	tag: 'path' | 'polygon' | 'circle' | 'ellipse' | 'rect';
	layer: string;
	layerDef?: ShapeLayerDef;
	[key: string]: string | ShapeLayerDef | undefined;
}

export interface LayerDef {
	id: string;
	label: string;
	allowedTools: ToolId[];
	style: Record<string, string | number>;
}
