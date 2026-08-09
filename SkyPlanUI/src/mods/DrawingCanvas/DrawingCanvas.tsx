import React, {useEffect, useMemo, useRef, useState} from 'react';
import {trigger} from 'cs2/api';
import {ToolId, ShapeData, Tag, LayerDef, LabelStyle} from '../types';
import {buildPath, buildPolygon, centroid} from 'mods/utils/buildSvg';
import {useSkyplan} from '../SkyplanContext';
import {useDrawingContext} from 'mods/DrawingContext';

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

function resolveLabelStyle(layerDef: LayerDef | undefined, global: LabelStyle): Required<LabelStyle> {
	return {
		color:      layerDef?.labelStyle?.color      ?? global.color      ?? '#ffffff',
		fontSize:   layerDef?.labelStyle?.fontSize   ?? global.fontSize   ?? 12,
		fontWeight: layerDef?.labelStyle?.fontWeight ?? global.fontWeight ?? 'normal',
		opacity:    layerDef?.labelStyle?.opacity    ?? global.opacity    ?? 1,
	};
}

function labelPosition(s: ShapeData): { x: number; y: number } | null {
	if (!s.pts.length) return null;
	if (s.tag === Tag.polygon) return centroid(s.pts);
	if (s.tag === Tag.path)    return centroid(s.pts);
	if (s.tag === Tag.circle)  return { x: s.pts[0].x, y: s.pts[0].y - 12 };
	return null;
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
		case Tag.text: {
			const p = s.pts[0];
			if (!p || !s.label) return null;
			return (
				<text key={s.id} x={p.x} y={p.y}
					textAnchor="middle" dominantBaseline="middle"
					fontSize={13} fill="#facc15"
					style={{ paintOrder: 'stroke', stroke: 'rgba(0,0,0,0.7)', strokeWidth: 3, ...style }}
				>
					{s.label}
				</text>
			);
		}
		default: return null;
	}
}

const DrawingCanvas: React.FC = () => {
	const { activeTool, viewMode, globalLabelStyle, allLayers } = useSkyplan();
	const layerDefsMap = useMemo(() =>
		Object.fromEntries(allLayers.map(l => [l.id, l])),
		[allLayers]
	);
	const {shapes, preview, highlightId, svgSize, globalOpacity, layerOpacities, layerVisible, layerLabels, showDescriptions} = useDrawingContext();

	const [cursorPos, setCursorPos] = useState<{ x: number; y: number } | null>(null);

	const drawingRef = useRef(false);
	const lastInputRef = useRef<string | null>(null);
	const toolRef = useRef<ToolId>('path');
	const viewModeRef = useRef(true);

	useEffect(() => { toolRef.current = activeTool; }, [activeTool]);
	useEffect(() => { viewModeRef.current = viewMode; }, [viewMode]);

	useEffect(() => {
		const onMove = (e: MouseEvent) => {
			if (toolRef.current === 'text' && !viewModeRef.current) {
				setCursorPos({ x: e.clientX, y: e.clientY });
			}
			else {
				setCursorPos(null);
			}
		};
		const onLeave = () => setCursorPos(null);
		document.addEventListener('mousemove', onMove, true);
		document.addEventListener('mouseleave', onLeave, true);
		return () => {
			document.removeEventListener('mousemove', onMove, true);
			document.removeEventListener('mouseleave', onLeave, true);
		};
	}, []);

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
			if (e.buttons & 2) return;
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
			if ((e.target as Element).closest('[data-skyplan-ui]')) return;
			if (onDown(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pm = (e: PointerEvent) => {
			if (viewModeRef.current) return;
			if (e.buttons & 2) return;
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

	const shapesByLayer = useMemo( ()=> {
	  const map = new Map<string, ShapeData[]>();
	  for (const s of shapes) {
		if (!map.has(s.layerId)) map.set(s.layerId, []);
		map.get(s.layerId)!.push(s);
	  }
	  return map;

	}, [shapes]);

	const hasHighlight = highlightId !== null;
	const layerCSS = buildLayerCSS(shapes, preview);

	const showCursor = !!cursorPos && activeTool === 'text' && !viewMode;
	if (shapes.length === 0 && !preview && !showCursor) return null;

	return (
		<svg
			key={shapes.length}
			style={{ position: 'absolute', top: 0, left: 0, pointerEvents: 'none', overflow: 'hidden', opacity: globalOpacity }}
			width={svgSize.w} height={svgSize.h * 0.93}
			viewBox={`0 0 ${svgSize.w} ${svgSize.h * 0.93}`}
		>
			<defs>
				<style>{layerCSS}</style>
			</defs>


			{Array.from(shapesByLayer.entries()).map(([layerId, layerShapes]) => {
				const ls = resolveLabelStyle(layerDefsMap[layerId], globalLabelStyle);
				const descFontSize = Math.max(8, ls.fontSize - 2);
				const descOpacity = ls.opacity * 0.7;
				return (
				  <g key={layerId} display={layerVisible[layerId] === false ? 'none' : undefined} opacity={layerOpacities[layerId] ?? 1}>
					{layerShapes.map(s => renderShape(s, hasHighlight ? (s.id === highlightId ? '1' : '0.3') : undefined))}
					{layerLabels[layerId] && layerShapes.map(s => {
						if (s.tag === Tag.text) return null;
						if (!s.label) return null;
						const pos = labelPosition(s);
						if (!pos) return null;
						return (
						  <text key={`lbl-${s.id}`}
							x={pos.x} y={pos.y}
							textAnchor="middle" dominantBaseline="middle"
							fontSize={ls.fontSize} fill={ls.color}
							fontWeight={ls.fontWeight} opacity={ls.opacity}
							style={{ paintOrder: 'stroke', stroke: 'rgba(0,0,0,0.6)', strokeWidth: 3 }}
						  >
							{s.label}
						  </text>
						);
					})}
					{showDescriptions && layerShapes.map(s => {
						if (!s.description) return null;
						if (s.tag === Tag.text) {
							if (!s.pts[0]) return null;
							return (
							  <text key={`desc-${s.id}`}
								x={s.pts[0].x} y={s.pts[0].y + 18}
								textAnchor="middle" dominantBaseline="middle"
								fontSize={descFontSize} fill={ls.color}
								opacity={descOpacity}
								style={{ paintOrder: 'stroke', stroke: 'rgba(0,0,0,0.6)', strokeWidth: 2 }}
							  >
								{s.description}
							  </text>
							);
						}
						const pos = labelPosition(s);
						if (!pos) return null;
						const descY = s.tag === Tag.circle ? s.pts[0].y + 20 : pos.y + 16;
						return (
						  <text key={`desc-${s.id}`}
							x={pos.x} y={descY}
							textAnchor="middle" dominantBaseline="middle"
							fontSize={descFontSize} fill={ls.color}
							opacity={descOpacity}
							style={{ paintOrder: 'stroke', stroke: 'rgba(0,0,0,0.6)', strokeWidth: 2 }}
						  >
							{s.description}
						  </text>
						);
					})}
				  </g>
				);
			})}
			{preview && renderShape(preview)}
			{showCursor && (
				<circle
					cx={cursorPos.x} cy={cursorPos.y} r={5}
					fill="rgba(250,204,21,0.25)" stroke="#facc15" strokeWidth={1.5}
					style={{ pointerEvents: 'none' }}
				/>
			)}
		</svg>
	);
};

export default DrawingCanvas;
