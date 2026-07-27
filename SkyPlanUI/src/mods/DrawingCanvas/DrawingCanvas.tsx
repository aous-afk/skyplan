import React, {useEffect, useRef} from 'react';
import {trigger} from 'cs2/api';
import {ToolId, ShapeData, Tag} from '../types';
import {buildPath, buildPolygon} from 'mods/utils/buildSvg';

interface DrawingCanvasProps {
	activeTool: ToolId;
	viewMode: boolean ;
	shapes: ShapeData[];
	preview: ShapeData | null;
	highlightId: string | null;
	svgSize: { w: number; h: number };
}

function buildLayerCSS(shapes: ShapeData[], preview: ShapeData | null): string {
	const seen = new Set<string>();
	const rules: string[] = [];
	const all = preview ? [...shapes, preview] : shapes;
	for (const s of all) {
		if (!s.layerDef?.style || seen.has(s.layerDef.id)) continue;
		seen.add(s.layerDef.id);
		const decls = Object.entries(s.layerDef.style)
			.map(([k, v]) => `${k}:${v}`)
			.join(';');
		rules.push(`.sp-${s.layerDef.id}{${decls}}`);
	}
	return rules.join('');
}

function renderShape(s: ShapeData, opacity?: string): React.ReactElement | null {
	const cn = `sp-${s.layerDef?.id ?? ''}`;
	const style = opacity !== undefined ? { opacity } : undefined;

	switch (s.tag) {
		case Tag.path: {
			const d = buildPath(s.pts);
			if (!d) return null;
			return <path key={s.id} className={cn} d={d} style={style} />;
		}
		case Tag.polygon: {
			if (s.pts.length < 3) {
				const d = buildPath(s.pts);
				if (!d) return null;
				return <path key={s.id} className={cn} d={d} style={style} />;
			}
			const points = buildPolygon(s.pts);
			return <polygon key={s.id} className={cn} points={points} style={style} />;
		}
		case Tag.circle: {
			const p = s.pts[0];
			return <circle key={s.id} className={cn} cx={p.x} cy={p.y} r={6} style={style} />;
		}
		default: return null;
	}
}

const DrawingCanvas: React.FC<DrawingCanvasProps> = ({ activeTool, viewMode, shapes, preview, highlightId, svgSize }) => {
	const drawingRef = useRef(false);
	const lastInputRef = useRef<string | null>(null);
	const toolRef = useRef<ToolId>('path');
	const viewModeRef = useRef(true);

	useEffect(() => {
		toolRef.current = activeTool;
	}, [activeTool]);

	useEffect(() => {
		viewModeRef.current = viewMode;
	}, [viewMode]);

	useEffect(() => {
		function onDown(cx: number, cy: number, type: string): boolean {
			if (lastInputRef.current === 'pointer' && type === 'mouse') return false;
			if (viewModeRef.current) return false;
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

			if (viewModeRef.current) return false;
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
			if (viewModeRef.current) return;
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
			if (viewModeRef.current) return false;
			if (!drawingRef.current) return false;
			if (lastInputRef.current === 'pointer' && type === 'mouse') return false;
			endDraw(cx, cy);
			return true;
		}

		const md = (e: MouseEvent) => {
			if (viewModeRef.current) return;
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
			if (viewModeRef.current) return;
			if (e.buttons & 2) return; // right-drag = camera pan, let it through
			if (onMove(e.clientX, e.clientY, 'mouse')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const mu = (e: MouseEvent) => {
			if (viewModeRef.current) return;
			if (e.button !== 2) return;
			if (onUp(e.clientX, e.clientY, 'mouse')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pd = (e: PointerEvent) => {
			if (viewModeRef.current) return;
			if (e.button !== 0) return;
			if (onDown(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pm = (e: PointerEvent) => {
			if (viewModeRef.current) return;
			if (e.buttons & 2) return; // right-drag = camera pan, let it through
			if (onMove(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pu = (e: PointerEvent) => {
			if (viewModeRef.current) return;
			if (e.button !== 2) return;
			if (onUp(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};

		const kd = (e: KeyboardEvent) => {
			if (viewModeRef.current) return;
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
