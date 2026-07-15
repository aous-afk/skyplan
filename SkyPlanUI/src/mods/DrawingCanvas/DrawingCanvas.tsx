import React, {useEffect, useRef} from 'react';
import {trigger} from 'cs2/api';
import {ToolId, ShapeData} from '../types';

interface DrawingCanvasProps {
	activeTool: ToolId;
	shapes: ShapeData[];
	preview: ShapeData | null;
	highlightId: string | null;
	svgSize: { w: number; h: number };
}

const SKIP = new Set(['id', 'tag', 'layer', 'layerDef']);

function buildLayerCSS(shapes: ShapeData[], preview: ShapeData | null): string {
	const seen = new Set<string>();
	const rules: string[] = [];
	const all = preview ? [...shapes, preview] : shapes;
	for (const s of all) {
		if (!s.layerDef?.style || seen.has(s.layer)) continue;
		seen.add(s.layer);
		const decls = Object.entries(s.layerDef.style)
			.map(([k, v]) => `${k}:${v}`)
			.join(';');
		rules.push(`.sp-${s.layer}{${decls}}`);
	}
	return rules.join('');
}

function renderShape(s: ShapeData, opacity?: string): React.ReactElement | null {
	const attrs: Record<string, string> = {};
	for (const k of Object.keys(s)) {
		if (!SKIP.has(k)) attrs[k] = s[k] as string;
	}
	const style: Record<string, string> = {};
	if (opacity !== undefined) style.opacity = opacity;
	const cn = `sp-${s.layer}`;

	switch (s.tag) {
		case 'path':
			return <path key={s.id} className={cn} {...attrs} style={style} />;
		case 'polygon':
			return <polygon key={s.id} className={cn} {...attrs} style={style} />;
		case 'circle':
			return <circle key={s.id} className={cn} {...attrs} style={style} />;
		default: return null;
	}
}

const DrawingCanvas: React.FC<DrawingCanvasProps> = ({ activeTool, shapes, preview, highlightId, svgSize }) => {
	const drawingRef = useRef(false);
	const lastInputRef = useRef<string | null>(null);
	const toolRef = useRef<ToolId>('path');

	useEffect(() => {
		toolRef.current = activeTool;
	}, [activeTool]);

	useEffect(() => {
		function onDown(cx: number, cy: number, type: string): boolean {
			if (lastInputRef.current === 'pointer' && type === 'mouse') return false;
			lastInputRef.current = type;
			if (toolRef.current === 'polygon') {
				if (!drawingRef.current) {
					drawingRef.current = true;
					trigger('skyplan', 'drawStart', `${cx},${cy}`);
				} else {
					trigger('skyplan', 'addPoint', `${cx},${cy}`);
				}
				return true;
			}

			endDraw(cx, cy);
			drawingRef.current = true;
			trigger('skyplan', 'drawStart', `${cx},${cy}`);
			if (toolRef.current === 'erase' || toolRef.current === 'point') drawingRef.current = false;
			return true;
		}

		function onMove(cx: number, cy: number, type: string): boolean {
			if (lastInputRef.current === 'pointer' && type === 'mouse') return false;

			if (!drawingRef.current && toolRef.current === 'erase') {
				trigger('skyplan', 'eraseHover', `${cx},${cy}`);
				return true;
			}

			if (drawingRef.current) {
				trigger('skyplan', 'drawMove', `${cx},${cy}`);
				return true;
			}
			return false;
		}

		function endDraw(cx: number, cy: number) {
			if (toolRef.current === 'polygon') {
				if (drawingRef.current) {
					trigger('skyplan', 'addPoint', `${cx},${cy}`);
				}
			}
			drawingRef.current = false;
			lastInputRef.current = null;
			trigger('skyplan', 'drawEnd', `${cx},${cy}`);
		}

		function onUp(cx: number, cy: number, type: string): boolean {
			if (!drawingRef.current) return false;
			if (lastInputRef.current === 'pointer' && type === 'mouse') return false;
			endDraw(cx, cy);
			return true;
		}

		const md = (e: MouseEvent) => {
			switch (e.button) {
				case 0:
					if ((e.target as Element).closest('[data-skyplan-ui]')) return;
					if (onDown(e.clientX, e.clientY, 'mouse')) {
						e.stopImmediatePropagation();
						e.preventDefault();
					}
					break;
				case 1:
					break;
				case 2:
					if (onUp(e.clientX, e.clientY, 'mouse')) {
						e.stopImmediatePropagation();
						e.preventDefault();
					}
					break;
			}
		};

		const mm = (e: MouseEvent) => {
			if (onMove(e.clientX, e.clientY, 'mouse')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const mu = (e: MouseEvent) => {
			if (e.button !== 2) return;
			if (onUp(e.clientX, e.clientY, 'mouse')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pd = (e: PointerEvent) => {
			if (e.button !== 0) return;
			if (onDown(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pm = (e: PointerEvent) => {
			if (onMove(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pu = (e: PointerEvent) => {
			if (e.button !== 2) return;
			if (onUp(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};

		const kd = (e: KeyboardEvent) => {
			if (e.key === 'Escape') {
				drawingRef.current = false;
				trigger('skyplan', 'panelClosed', '');
				return;
			}
			if (e.ctrlKey && (e.key === 'z' || e.key === 'Z')) {
				trigger('skyplan', 'undo', '');
				e.stopImmediatePropagation(); e.preventDefault();
			}
		};

		document.addEventListener('mousedown', md, true);
		document.addEventListener('mousemove', mm, true);
		document.addEventListener('pointerdown', pd, true);
		document.addEventListener('pointermove', pm, true);
		document.addEventListener('pointerup', pu, true);
		document.addEventListener('keydown', kd, true);
		return () => {
			document.removeEventListener('mousedown', md, true);
			document.removeEventListener('mousemove', mm, true);
			document.removeEventListener('pointerdown', pd, true);
			document.removeEventListener('pointermove', pm, true);
			document.removeEventListener('pointerup', pu, true);
			document.removeEventListener('keydown', kd, true);
		};
	}, []);

	const hasHighlight = highlightId !== null;
	const layerCSS = buildLayerCSS(shapes, preview);

	if (shapes.length === 0 && !preview) return null;

	return (
		<svg
			key={shapes.length}
			style={{ position: 'absolute', top: 0, left: 0, pointerEvents: 'none', overflow: 'hidden' }}
			width={svgSize.w} height={svgSize.h * 0.93}
			viewBox={`0 0 ${svgSize.w} ${svgSize.h * 0.93}`}
		>
			<defs>
				<style>{layerCSS}</style>
			</defs>
			{shapes.map(s => renderShape(s, hasHighlight ? (s.id === highlightId ? '1' : '0.3') : undefined))}
			{preview && renderShape(preview)}
		</svg>
	);
};

export default DrawingCanvas;
