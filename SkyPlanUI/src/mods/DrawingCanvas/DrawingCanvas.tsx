import React, {useEffect, useMemo, useRef, useState} from 'react';
import {trigger} from 'cs2/api';
import {getModule} from 'cs2/modding';
import {TOOLS, ToolId, ShapeData, Tag, LayerDef, LayerIcon, LabelStyle} from '../types';
import {buildPath, buildPolygon, buildCurve, centroid} from 'mods/utils/buildSvg';
import {useSkyplan} from '../SkyplanContext';
import {useDrawingContext} from 'mods/DrawingContext';
import styles from './DrawingCanvas.module.scss';

const FloatingMouseTooltip = getModule(
	'game-ui/common/tooltip/floating-mouse-tooltip/floating-mouse-tooltip.tsx',
	'FloatingMouseTooltip'
) as any;

function buildLayerCSS(shapes: ShapeData[], preview: ShapeData | null, layerDefsMap: Record<string, LayerDef>, extraLayerIds: string[] = []): string {
	const seen = new Set<string>();
	const rules: string[] = [];
	const ensure = (layerId: string) => {
		if (seen.has(layerId)) return;
		const style = layerDefsMap[layerId]?.style;
		if (!style) return;
		seen.add(layerId);
		const decls = Object.entries(style).map(([k, v]) => `${k}:${v}`).join(';');
		rules.push(`.sp-${layerId}{${decls}}`);
		// Separate from .sp-{layerId} above (which is fill:none for line layers - correct for the
		// actual line/curve geometry, wrong for a translucent indicator circle) - same swatch color
		// used as both fill and stroke instead, for the cursor-position preview circles below.
		const swatch = (style.stroke ?? style.fill) as string | undefined;
		if (swatch) rules.push(`.sp-cursor-${layerId}{fill:${swatch};fill-opacity:0.25;stroke:${swatch};stroke-width:1.5}`);
	};
	const all = preview ? [...shapes, preview] : shapes;
	for (const s of all) {
		ensure(s.layerId);
		// A parallel-lane <use> may target a layer with zero real shapes of its own (e.g. "Subway"
		// queued but nothing drawn as Subway yet) - it still needs a .sp-{layerId} rule to render
		// styled at all.
		s.parallelLanes?.forEach(lane => ensure(lane.layerId));
	}
	// Queued-but-not-yet-drawn layers (the pre-click cursor circles) aren't referenced by any shape
	// or preview yet, so they'd otherwise get no .sp-cursor-{layerId} rule at all.
	extraLayerIds.forEach(ensure);
	return rules.join('');
}

function resolveLabelStyle(layerDef: LayerDef | undefined, global: LabelStyle): Required<LabelStyle> {
	return {
		color: layerDef?.labelStyle?.color ?? global.color ?? '#ffffff',
		fontSize: layerDef?.labelStyle?.fontSize ?? global.fontSize ?? 12,
		fontWeight: layerDef?.labelStyle?.fontWeight ?? global.fontWeight ?? 'normal',
		opacity: layerDef?.labelStyle?.opacity ?? global.opacity ?? 1,
	};
}

function labelPosition(s: ShapeData): { x: number; y: number } | null {
	if (!s.pts.length) return null;
	if (s.tag === Tag.polygon) return centroid(s.pts);
	if (s.tag === Tag.path) return centroid(s.pts);
	if (s.tag === Tag.curve) return centroid(s.pts);
	if (s.tag === Tag.circle) return { x: s.pts[0].x, y: s.pts[0].y - 12 };
	return null;
}

function renderText(s: ShapeData, ls: LabelStyle | undefined): React.ReactElement | null {
	if (!s.description) return null;
	if (!ls) return null;

	const descFontSize = Math.max(8, ls.fontSize ?? 10 - 2);
	const descOpacity = ls.opacity ?? 1 * 0.7;
	// the text needs to be textPath href="#lineAC"
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
}

function renderShape(s: ShapeData, icon: LayerIcon | undefined, opacity?: string): React.ReactElement | null {
	const cn = `sp-${s.layerId}`;
	const style = opacity !== undefined ? { opacity } : undefined;

	switch (s.tag) {
		case Tag.path: {
			// Renders normally even with parallelLanes - those are only the EXTRA queued layers now
			// (see parallelClonesByLayer in DrawingCanvas), rendered as their own independent <path>s
			// alongside this one, not instead of it.
			const d = buildPath(s.pts);
			if (!d) return null;
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
	const { activeTool, activeLayers, primaryLayer, viewMode, globalLabelStyle, allLayers, showWhatsNew } = useSkyplan();
	const layerDefsMap = useMemo(() =>
		Object.fromEntries(allLayers.map(l => [l.id, l])),
		[allLayers]
	);
	const { shapes, preview, highlightId, indicator, svgSize, globalOpacity, layerOpacities, layerVisible, layerLabels, showDescriptions } = useDrawingContext();

	const [cursorPos, setCursorPos] = useState<{ x: number; y: number } | null>(null);

	// Mirrors Skyplan/Systems/DrawingSystem.cs's DefaultParallelSpacing - only needed as a hover-time
	// display fallback before a preview exists (drawing hasn't started, so there's no server-computed
	// preview.parallelSpacing yet). Update this alongside that constant until spacing becomes
	// server-driven/configurable.
	const FALLBACK_PARALLEL_SPACING_M = 8;
	// Same "more than one lane queued" check SkyplanContext's setParallelLayers effect uses - true as
	// soon as the corridor is queued in the toolbar, before drawing has even started. Gated by the
	// same allowMultiSelect flag Toolbar.tsx uses, not a hardcoded tool id - stays correct as more
	// tools gain real corridor support.
	const multiSelectAllowed = TOOLS.find(t => t.id === activeTool)?.allowMultiSelect ?? false;
	const hasQueuedCorridor = multiSelectAllowed && activeLayers.length > 1;

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
			if (!viewModeRef.current) {
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

	const shapesByLayer = useMemo(() => {
		const map = new Map<string, ShapeData[]>();
		for (const s of shapes) {
			if (!map.has(s.layerId)) map.set(s.layerId, []);
			map.get(s.layerId)!.push(s);
		}
		return map;

	}, [shapes]);

	// Parallel-lane clones, keyed by the TARGET layer they're styled as (not the source shape's own
	// layer) - a lane targeting a layer with zero real shapes drawn still needs its own group below,
	// so the render loop iterates the union of both maps' keys, not just this one's.
	//
	// Independent real <path> per lane, not a shared <use xlink:href> - confirmed 2026-09-13 that
	// GameFace leaks stroke-dasharray between sibling <use> instances of the same referenced
	// geometry regardless of which element in the ancestor chain carries the class (tried: class on
	// the <use> itself, on a <g> wrapping just that <use>, on the shared per-layer <g>, in every
	// combination - dasharray from whichever lane paints first always won). Looks like a paint-state
	// caching bug in the rasterizer keyed off the shared geometry, upstream of CSS resolution
	// entirely - no class placement can fix it. A plain independent <path> has no shared source to
	// leak from, at the cost of recomputing the same `d` string per lane instead of cloning it once.
	const parallelClonesByLayer = useMemo(() => {
		const map = new Map<string, { shapeId: string; d: string; dx: number; dy: number; labelPos: { x: number; y: number } | null; label?: string; description?: string }[]>();
		for (const s of shapes) {
			if (!s.parallelLanes || s.parallelLanes.length === 0) continue;
			const d = buildPath(s.pts);
			if (!d) continue;
			const labelPos = labelPosition(s);
			for (const lane of s.parallelLanes) {
				if (!map.has(lane.layerId)) map.set(lane.layerId, []);
				map.get(lane.layerId)!.push({ shapeId: s.id, d, dx: lane.dx, dy: lane.dy, labelPos, label: lane.label, description: lane.description });
			}
		}
		return map;
	}, [shapes]);

	const allGroupLayerIds = useMemo(
		() => Array.from(new Set([...shapesByLayer.keys(), ...parallelClonesByLayer.keys()])),
		[shapesByLayer, parallelClonesByLayer]
	);

	const hasHighlight = highlightId !== null;
	const layerCSS = buildLayerCSS(shapes, preview, layerDefsMap, activeLayers.map(l => l.id));

	const showCursor = !!cursorPos && !viewMode;
	if (shapes.length === 0 && !preview && !showCursor && !indicator) return null;

	// Screen-pixel stagger for the pre-click case only - there's no real line/curve direction yet
	// (needs two points), so there's no true perpendicular offset to compute; not meter-accurate,
	// just a visual size reference. Reuses the actual spacing setting (not a disconnected magic
	// number) so this stays in sync once spacing becomes user-configurable.
	const CURSOR_CIRCLE_STAGGER_PX = preview?.parallelSpacing ?? FALLBACK_PARALLEL_SPACING_M;

	// One unified list instead of a solo cursor circle plus separate per-lane-type maps - the
	// primary is entry 0, every queued lane (line or curve) is another entry. Each carries a layerId,
	// not a baked color, so rendering can go through a <g className="sp-cursor-{layerId}"> wrapper
	// (see below) instead of inline style.
	const previewCircles = (() => {
		if (!showCursor || !cursorPos) return [];
		if (preview?.parallelLanes?.length) {
			return [
				{ x: cursorPos.x, y: cursorPos.y, layerId: preview.layerId },
				...preview.parallelLanes.map(lane => ({
					x: cursorPos.x + lane.dx, y: cursorPos.y + lane.dy, layerId: lane.layerId,
				})),
			];
		}
		if (preview?.previewCurveLanes?.length) {
			return [
				{ x: cursorPos.x, y: cursorPos.y, layerId: preview.layerId },
				...preview.previewCurveLanes.flatMap(lane => {
					const tip = lane.pts[lane.pts.length - 1];
					return tip ? [{ x: tip.x, y: tip.y, layerId: lane.layerId }] : [];
				}),
			];
		}
		// Not drawing yet - stagger one circle per queued layer along a fixed axis at the cursor
		// instead of the real offset (not knowable yet). Fans out to the real offsets above the
		// moment drawing actually starts.
		if (hasQueuedCorridor) {
			return activeLayers.map((l, i) => ({
				x: cursorPos.x + i * CURSOR_CIRCLE_STAGGER_PX, y: cursorPos.y, layerId: l.id,
			}));
		}
		return primaryLayer ? [{ x: cursorPos.x, y: cursorPos.y, layerId: primaryLayer.id }] : [];
	})();

	return (
		<>
		<svg
			key={shapes.length}
			style={{ position: 'absolute', top: 0, left: 0, pointerEvents: 'none', overflow: 'hidden', opacity: globalOpacity }}
			width={svgSize.w} height={svgSize.h * 0.93}
			viewBox={`0 0 ${svgSize.w} ${svgSize.h * 0.93}`}
		>
			<defs>
				<style>{layerCSS}</style>
				<circle id="cursor-circle-template" cx="0" cy="0" r="5" />
			</defs>


			{allGroupLayerIds.map(layerId => {
				const layerShapes = shapesByLayer.get(layerId) ?? [];
				const ls = resolveLabelStyle(layerDefsMap[layerId], globalLabelStyle);
				return (
					<g key={layerId} className={`sp-${layerId}`} display={layerVisible[layerId] === false ? 'none' : undefined} opacity={layerOpacities[layerId] ?? 1}>
						{layerShapes.map(s => renderShape(s, layerDefsMap[layerId]?.icon, hasHighlight ? (s.id === highlightId ? '1' : '0.3') : undefined))}

						{/* class comes from the wrapping <g> above, not restated per-path */}
						{parallelClonesByLayer.get(layerId)?.map((clone, i) => (
							<path key={`${clone.shapeId}-lane-${i}`} d={clone.d}
								transform={`translate(${clone.dx},${clone.dy})`}
								style={hasHighlight ? { opacity: clone.shapeId === highlightId ? '1' : '0.3' } : undefined}
							/>
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

						{layerLabels[layerId] && parallelClonesByLayer.get(layerId)?.map((clone, i) => {
							if (!clone.label || !clone.labelPos) return null;
							return (
								<text key={`lbl-${clone.shapeId}-lane-${i}`}
									x={clone.labelPos.x + clone.dx} y={clone.labelPos.y + clone.dy}
									textAnchor="middle" dominantBaseline="middle"
									fontSize={ls.fontSize} fill={ls.color}
									fontWeight={ls.fontWeight} opacity={ls.opacity}
									style={{ paintOrder: 'stroke', stroke: 'rgba(0,0,0,0.6)', strokeWidth: 3 }}
								>
									{clone.label}
								</text>
							);
						})}

						{showDescriptions
							&& layerShapes.map(s => renderText(s, ls))}

						{showDescriptions && parallelClonesByLayer.get(layerId)?.map((clone, i) => {
							if (!clone.description || !clone.labelPos) return null;
							const descFontSize = Math.max(8, ls.fontSize ?? 10 - 2);
							const descOpacity = ls.opacity ?? 1 * 0.7;
							return (
								<text key={`desc-${clone.shapeId}-lane-${i}`}
									x={clone.labelPos.x + clone.dx} y={clone.labelPos.y + clone.dy + 16}
									textAnchor="middle" dominantBaseline="middle"
									fontSize={descFontSize} fill={ls.color}
									opacity={descOpacity}
									style={{ paintOrder: 'stroke', stroke: 'rgba(0,0,0,0.6)', strokeWidth: 2 }}
								>
									{clone.description}
								</text>
							);
						})}
					</g>
				);
			})}
			{preview && renderShape(preview, layerDefsMap[preview.layerId]?.icon)}
			{preview && Array.from(
				preview.parallelLanes?.reduce((map, lane) => {
					if (!map.has(lane.layerId)) map.set(lane.layerId, []);
					map.get(lane.layerId)!.push(lane);
					return map;
				}, new Map<string, { layerId: string; dx: number; dy: number }[]>()) ?? []
			).map(([layerId, lanes]) => (
				// Quick pass: rendered outside any per-layer group (unlike the committed-shape
				// clones), so it doesn't respect other layers' opacity/visibility toggles during
				// the transient mid-draw preview - acceptable for a rubber-band that only exists
				// for a second or two.
				<g key={`preview-${layerId}`} className={`sp-${layerId}`}>
					{lanes.map((lane, i) => {
						const d = buildPath(preview!.pts);
						return d && <path key={i} d={d} transform={`translate(${lane.dx},${lane.dy})`} />;
					})}
				</g>
			))}
			{preview?.previewCurveLanes?.map((lane, i) => {
				// Already a dense sampled+offset point list (server-side, same math as the real
				// commit path) - buildPath (straight segments), not buildCurve, matches the data.
				const d = buildPath(lane.pts);
				if (!d) return null;
				return (
					<g key={`preview-curve-lane-${i}`} className={`sp-${lane.layerId}`}>
						<path d={d} />
					</g>
				);
			})}
			<circle
				cx={shownIndicator.x} cy={shownIndicator.y}
				r={shownIndicator.kind === 'vertex' ? 6 : 5}
				fill="none"
				stroke={shownIndicator.kind === 'vertex' ? '#4ade80' : '#38bdf8'}
				strokeWidth={2}
				opacity={indicator ? 1 : 0}
				style={{ pointerEvents: 'none' }}
			/>
			{previewCircles.map((c, i) => (
				// <use> cloning one shared template circle, styled via a matched CSS class
				// (sp-cursor-{layerId}, see buildLayerCSS) on the wrapping <g> - not inline `style` on
				// the <use> itself, which didn't propagate into shadow content (confirmed 2026-09-15).
				// Class-driven styling is the one <use> mechanism actually proven to work in this
				// engine (same as the old line-clone approach, before that hit the separate
				// stroke-dasharray leak - a different property than what's used here).
				<g key={`cursor-circle-${i}`} className={`sp-cursor-${c.layerId}`}>
					<use xlinkHref="#cursor-circle-template" transform={`translate(${c.x},${c.y})`} style={{ pointerEvents: 'none' }} />
				</g>
			))}
		</svg>
		{hasQueuedCorridor && cursorPos && !viewMode && (
			<FloatingMouseTooltip
				position={cursorPos}
				screenSpacePosition
				forceVisible
				tooltip={
					<span className={styles.spacing_hint}>
						<span>{preview?.parallelSpacing ?? FALLBACK_PARALLEL_SPACING_M}m spacing</span>
						<span className={styles.key_cap}>Shift</span>
						{/* Base game's own scroll-wheel icon, referenced by its asset path directly -
						    same "Media/..." path scheme game-ui/.../control-icons.tsx uses for mouse
						    icons (Media/Mouse/Scrollwheel.svg). No keyboard equivalent exists for
						    Shift in the game's own asset set (see dev_doc.md) - it renders that as a
						    plain text key-cap too, same as the span above. */}
						<img src="Media/Mouse/Scrollwheel.svg" className={styles.wheel_icon} />
					</span>
				}
			/>
		)}
		</>
	);
};

export default DrawingCanvas;
