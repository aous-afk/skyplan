import { faDrawPolygon, faEraser, faLocationDot, faRoad } from '@fortawesome/free-solid-svg-icons';
import styles from './Toolbar.module.scss';

export const TOOLS = [
  { id: 'line',    label: 'Line',    icon: faRoad,        activeStyle: {} },
  { id: 'polygon', label: 'Polygon', icon: faDrawPolygon, activeStyle: {} },
  { id: 'point',   label: 'Point',   icon: faLocationDot, activeStyle: {} },
  { id: 'erase',   label: 'Erase',   icon: faEraser,      activeStyle: { background: '#3a1a00', color: '#ffaa55' } },
  // {id :'line', lap},
  // {id :'rect'},
  // {id :'circle'},
  // {id :'free'},
  // {id :'erase'}
] as const;
export type Tool = typeof TOOLS[number];
export type ToolId = typeof TOOLS[number]['id'];

export const LAYERS = ['roads', 'zoning', 'transit', 'notes'] as const;
export type Layer = typeof LAYERS[number];

export interface ShapeData {
	id: string;
	tag: 'line' | 'polygon' | 'path' | 'ellipse' | 'rect';
	layer: string;
	[key: string]: string;
}
