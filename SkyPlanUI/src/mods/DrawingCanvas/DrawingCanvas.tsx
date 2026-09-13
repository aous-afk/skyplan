import React, {useEffect, useMemo, useRef, useState} from 'react';
import {trigger} from 'cs2/api';
import {ToolId, ShapeData, Tag, LayerDef, LayerIcon, LabelStyle} from '../types';
import {buildPath, buildPolygon, buildCurve, centroid} from 'mods/utils/buildSvg';
import {useSkyplan} from '../SkyplanContext';
import {useDrawingContext} from 'mods/DrawingContext';

function buildLayerCSS(shapes: ShapeData[], preview: ShapeData | null, layerDefsMap: Record<string, LayerDef>): string {
	const seen = new Set<string>();
	const rules: string[] = [];
	const ensure = (layerId: string) => {
		if (seen.has(layerId)) return;
		const style = layerDefsMap[layerId]?.style;
		if (!style) return;
		seen.add(layerId);
		const decls = Object.entries(style).map(([k, v]) => `${k}:${v}`).join(';');
		rules.push(`.sp-${layerId}{${decls}}`);
	};
	const all = preview ? [...shapes, preview] : shapes;
	for (const s of all) {
		ensure(s.layerId);
		// A parallel-lane <use> may target a layer with zero real shapes of its own (e.g. "Subway"
		// queued but nothing drawn as Subway yet) - it still needs a .sp-{layerId} rule to render
		// styled at all.
		s.parallelLanes?.forEach(lane => ensure(lane.layerId));
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
	if (s.tag === Tag.curve)   return centroid(s.pts);
	if (s.tag === Tag.circle)  return { x: s.pts[0].x, y: s.pts[0].y - 12 };
	return null;
}

function renderShape(s: ShapeData, icon: LayerIcon | undefined, opacity?: string): React.ReactElement | null {
	const cn = `sp-${s.layerId}`;
	const style = opacity !== undefined ? { opacity } : undefined;

	switch (s.tag) {
		case Tag.path: {
			const d = buildPath(s.pts);
			if (!d) return null;
			// id is required here (not just key) - parallel-lane <use> clones need a real DOM id
			// to reference via href="#...". <use> must never sit inside <defs> (never renders).
			return <path key={s.id} id={s.id} className={cn} d={d} style={style} />;
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
		case Tag.curve: {
			const d = buildCurve(s.pts, s.handles);
			if (!d) return null;
			return <path key={s.id} className={cn} d={d} style={style} />;
		}
		case Tag.circle: {
			const p = s.pts[0];
			if (!icon) return <circle key={s.id} className={cn} cx={p.x} cy={p.y} r={6} style={style} />;
			// Icon paths are authored against a r=6 baseline circle - scale them with whatever
			// radius the icon-carrying circle actually uses so the two stay proportional.
			const POINT_RADIUS_WITH_ICON = 10;
			const iconScale = POINT_RADIUS_WITH_ICON / 6;
			return (
				<React.Fragment key={s.id}>
					<circle className={cn} cx={p.x} cy={p.y} r={POINT_RADIUS_WITH_ICON} style={style} />
					<g transform={`translate(${p.x},${p.y}) scale(${iconScale})`} style={style}>
						<path d={icon.path} fill={icon.color ?? 'black'} fillRule="evenodd" />
					</g>
				</React.Fragment>
			);
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
	const { activeTool, activeLayers, viewMode, globalLabelStyle, allLayers, showWhatsNew } = useSkyplan();
	const layerDefsMap = useMemo(() =>
		Object.fromEntries(allLayers.map(l => [l.id, l])),
		[allLayers]
	);
	const {shapes, preview, highlightId, indicator, svgSize, globalOpacity, layerOpacities, layerVisible, layerLabels, showDescriptions} = useDrawingContext();

	const [cursorPos, setCursorPos] = useState<{ x: number; y: number } | null>(null);

	// cohtml doesn't repaint the region a removed node used to occupy - keep the indicator
	// circle always mounted and toggle opacity instead of conditionally rendering it.
	const lastIndicatorRef = useRef({ x: 0, y: 0, kind: 'vertex' as 'vertex' | 'edge' });
	if (indicator) lastIndicatorRef.current = indicator;
	const shownIndicator = indicator ?? lastIndicatorRef.current;

	const drawingRef = useRef(false);
	const lastInputRef = useRef<string | null>(null);
	const toolRef = useRef<ToolId | null>('path');
	const viewModeRef = useRef(true);
	// Boolean gate only - "is any layer currently queued" - never dereferenced for its properties
	// here, so activeLayers (a list, see SkyplanContext) collapses to just this presence check.
	const hasActiveLayerRef = useRef(false);
	// Blocks all drawing/keyboard input while in view mode OR while a blocking panel (e.g.
	// What's New) is open - same semantics as viewModeRef already had, just OR'd with the panel
	// state so nothing extra needs to change at each of the many gate sites below.
	const blockInputRef = useRef(true);

	useEffect(() => { toolRef.current = activeTool; }, [activeTool]);
	useEffect(() => { viewModeRef.current = viewMode; }, [viewMode]);
	useEffect(() => { blockInputRef.current = viewMode || showWhatsNew; }, [viewMode, showWhatsNew]);
	useEffect(() => {
		hasActiveLayerRef.current = activeLayers.length > 0;
		if (activeLayers.length === 0) trigger('skyplan', 'clearIndicator', '');
	}, [activeLayers]);

	useEffect(() => {
		const onMove = (e: MouseEvent) => {
			if (toolRef.current === 'text' && !viewModeRef.current) {
				setCursorPos({ x: e.clientX, y: e.clientY });
			}
			else {
				setCursorPos(null);
			}
		};
		const onLeave = () => {
			setCursorPos(null);
			trigger('skyplan', 'clearIndicator', '');
			trigger('skyplan', 'clearErase', '');
		};
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
			if (toolRef.current !== 'erase' && !hasActiveLayerRef.current) return false;
			lastInputRef.current = type;
			if (toolRef.current === 'polygon' || toolRef.current === 'curve') {
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
			if (toolRef.current === 'erase' || toolRef.current === 'point' || toolRef.current === 'text') drawingRef.current = false;
			return true;
		}

		function onMove(cx: number, cy: number, type: string): boolean {
			if (lastInputRef.current === 'pointer' && type === 'mouse') return false;
			if (viewModeRef.current) return false;
			if (!drawingRef.current && toolRef.current === 'erase') {
				trigger('skyplan', 'eraseHover', `${cx},${cy}`);
				return true;
			}
			if (!drawingRef.current && hasActiveLayerRef.current && (toolRef.current === 'path' || toolRef.current === 'polygon' || toolRef.current === 'curve')) {
				trigger('skyplan', 'drawHover', `${cx},${cy}`);
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
			// Curve resolves its own final anchor server-side (HandleDrawEnd already gets this
			// screen pos) - firing an extra addPoint here would corrupt its control/anchor parity.
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
			if (blockInputRef.current) return;
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
			if (blockInputRef.current) return;
			if (e.buttons & 2) return;
			// Cursor is over our own UI (toolbar etc), not the map - any stale hover feedback
			// (erase highlight, snap indicator) needs clearing, or it sticks until a real canvas
			// hover happens to land on nothing.
			if ((e.target as Element).closest('[data-skyplan-ui]')) {
				trigger('skyplan', 'clearIndicator', '');
				trigger('skyplan', 'clearErase', '');
				return;
			}
			if (onMove(e.clientX, e.clientY, 'mouse')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const mu = (e: MouseEvent) => {
			if (blockInputRef.current) return;
			if (e.button !== 2) return;
			if (onUp(e.clientX, e.clientY, 'mouse')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pd = (e: PointerEvent) => {
			if (blockInputRef.current) return;
			if (e.button !== 0) return;
			if ((e.target as Element).closest('[data-skyplan-ui]')) return;
			if (onDown(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pm = (e: PointerEvent) => {
			if (blockInputRef.current) return;
			if (e.buttons & 2) return;
			if ((e.target as Element).closest('[data-skyplan-ui]')) {
				trigger('skyplan', 'clearIndicator', '');
				trigger('skyplan', 'clearErase', '');
				return;
			}
			if (onMove(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};
		const pu = (e: PointerEvent) => {
			if (blockInputRef.current) return;
			if (e.button !== 2) return;
			if (onUp(e.clientX, e.clientY, 'pointer')) {
				e.stopImmediatePropagation();
				e.preventDefault();
			}
		};

		const kd = (e: KeyboardEvent) => {
			if (blockInputRef.current) return;
			if (e.key === 'Escape') {
				drawingRef.current = false;
				trigger('skyplan', 'panelClosed', '');
				return;
			}
			if (e.ctrlKey && (e.key === 'z' || e.key === 'Z')) {
				trigger('skyplan', 'undo', '');
				e.stopImmediatePropagation(); e.preventDefault();
			}
			if (e.ctrlKey && (e.key === 'y' || e.key === 'Y')) {
				trigger('skyplan', 'redo', '');
				e.stopImmediatePropagation(); e.preventDefault();
			}
		};

		document.addEventListener('mousedown', md, true);
		document.addEventListener('mousemove', mm, true);
		document.addEventListener('mouseup', mu, true);
		document.addEventListener('pointerdown', pd, true);
		document.addEventListener('pointermove', pm, true);
		document.addEventListener('pointerup', pu, true);
		document.addEventListener('keydown', kd, true);
		return () => {
			document.removeEventListener('mousedown', md, true);
			document.removeEventListener('mousemove', mm, true);
			document.removeEventListener('mouseup', mu, true);
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

	// Parallel-lane <use> clones, keyed by the TARGET layer they're styled as (not the source
	// shape's own layer) - a lane targeting a layer with zero real shapes drawn still needs its own
	// group below, so the render loop iterates the union of both maps' keys, not just this one's.
	const parallelClonesByLayer = useMemo(() => {
	  const map = new Map<string, { shapeId: string; dx: number; dy: number }[]>();
	  for (const s of shapes) {
		if (!s.parallelLanes) continue;
		for (const lane of s.parallelLanes) {
		  if (!map.has(lane.layerId)) map.set(lane.layerId, []);
		  map.get(lane.layerId)!.push({ shapeId: s.id, dx: lane.dx, dy: lane.dy });
		}
	  }
	  return map;
	}, [shapes]);

	const allGroupLayerIds = useMemo(
	  () => Array.from(new Set([...shapesByLayer.keys(), ...parallelClonesByLayer.keys()])),
	  [shapesByLayer, parallelClonesByLayer]
	);

	const hasHighlight = highlightId !== null;
	const layerCSS = buildLayerCSS(shapes, preview, layerDefsMap);

	const showCursor = !!cursorPos && activeTool === 'text' && !viewMode;
	if (shapes.length === 0 && !preview && !showCursor && !indicator) return null;

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


			{allGroupLayerIds.map(layerId => {
				const layerShapes = shapesByLayer.get(layerId) ?? [];
				const ls = resolveLabelStyle(layerDefsMap[layerId], globalLabelStyle);
				const descFontSize = Math.max(8, ls.fontSize - 2);
				const descOpacity = ls.opacity * 0.7;
				return (
				  <g key={layerId} display={layerVisible[layerId] === false ? 'none' : undefined} opacity={layerOpacities[layerId] ?? 1}>
					{layerShapes.map(s => renderShape(s, layerDefsMap[layerId]?.icon, hasHighlight ? (s.id === highlightId ? '1' : '0.3') : undefined))}
					{parallelClonesByLayer.get(layerId)?.map((clone, i) => (
						// xlinkHref (-> xlink:href), not href: GameFace only supports the SVG1.1
						// namespaced form on <use> - confirmed 2026-09-13, plain href="#id" rendered
						// in the DOM correctly but the reference silently didn't resolve.
						<use key={`${clone.shapeId}-lane-${i}`} xlinkHref={`#${clone.shapeId}`} className={`sp-${layerId}`} transform={`translate(${clone.dx},${clone.dy})`} />
					))}
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
			{preview && renderShape(preview, layerDefsMap[preview.layerId]?.icon)}
			{preview?.parallelLanes?.map((lane, i) => (
				// Quick pass: rendered outside any per-layer group (unlike the committed-shape
				// clones), so it doesn't respect other layers' opacity/visibility toggles during
				// the transient mid-draw preview - acceptable for a rubber-band that only exists
				// for a second or two.
				<use key={`preview-lane-${i}`} xlinkHref={`#${preview.id}`} className={`sp-${lane.layerId}`} transform={`translate(${lane.dx},${lane.dy})`} />
			))}
			<circle
				cx={shownIndicator.x} cy={shownIndicator.y}
				r={shownIndicator.kind === 'vertex' ? 6 : 5}
				fill="none"
				stroke={shownIndicator.kind === 'vertex' ? '#4ade80' : '#38bdf8'}
				strokeWidth={2}
				opacity={indicator ? 1 : 0}
				style={{ pointerEvents: 'none' }}
			/>
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
