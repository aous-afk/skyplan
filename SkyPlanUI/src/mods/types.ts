export const TOOLS = ['line', 'rect', 'circle', 'free', 'erase'] as const;
export type Tool = typeof TOOLS[number];

export const LAYERS = ['roads', 'zoning', 'transit', 'notes'] as const;
export type Layer = typeof LAYERS[number];

export interface ShapeData {
	id: string;
	tag: 'line' | 'polygon' | 'path' | 'ellipse' | 'rect';
	layer: string;
	[key: string]: string;
}
