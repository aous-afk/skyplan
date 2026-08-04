import React, {createContext, useContext, useState, useEffect, useMemo, useCallback} from 'react';
import {useValue, trigger} from 'cs2/api';
import {panelVisible$, shapes$, preview$, highlight$, layersConfig$} from '../bindings';
import {ToolId, ShapeData, LayerDef} from './types';

interface SkyplanCtx {
	visible: boolean;
	shapes: ShapeData[];
	preview: ShapeData | null;
	highlightId: string | null;
	activeTool: ToolId;
	activeLayer: LayerDef | null;
	visibleLayers: LayerDef[];
	svgSize: { w: number; h: number };
	viewMode: boolean;
	toolbarPos: { left: number; top: number } | null;
	onToolbarPosChange: (pos: { left: number; top: number }) => void;
	onViewModeToggle: () => void;
	onToolChange: (t: ToolId) => void;
	onLayerChange: (l: LayerDef) => void;
	onUndo: () => void;
	onClear: () => void;
	onClearAll: () => void;
	onClose: () => void;
}

const SkyplanContext = createContext<SkyplanCtx | null>(null);

export const useSkyplan = (): SkyplanCtx => {
	const ctx = useContext(SkyplanContext);
	if (!ctx) throw new Error('useSkyplan used outside SkyplanProvider');
	return ctx;
};

export const SkyplanProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
	const visible = useValue(panelVisible$);
	const shapesJson = useValue(shapes$);
	const previewJson = useValue(preview$);
	const highlightRaw = useValue(highlight$);
	const layersConfigJson = useValue(layersConfig$);

	const shapes = useMemo<ShapeData[]>(() => {
		try { return JSON.parse(shapesJson) ?? []; }
		catch { return []; }
	}, [shapesJson]);

	const preview = useMemo<ShapeData | null>(() => {
		try { return previewJson ? JSON.parse(previewJson) : null; }
		catch { return null; }
	}, [previewJson]);

	const layerConfig = useMemo<{ layers: LayerDef[] }>(() => {
		try { return JSON.parse(layersConfigJson); }
		catch { return { layers: [] }; }
	}, [layersConfigJson]);

	const [activeTool, setActiveTool] = useState<ToolId>('path');
	const [activeLayer, setActiveLayer] = useState<LayerDef | null>(null);
	const [svgSize, setSvgSize] = useState({ w: 1920, h: 1080 });
	const [viewMode, setViewMode] = useState(false);
	const [toolbarPos, setToolbarPos] = useState<{ left: number; top: number } | null>(null);

	const visibleLayers = layerConfig.layers.filter(l => l.allowedTools.includes(activeTool));
	const highlightId = highlightRaw || null;

	useEffect(() => {
		const onResize = () => setSvgSize({ w: window.innerWidth || 1920, h: window.innerHeight || 1080 });
		onResize();
		window.addEventListener('resize', onResize);
		return () => window.removeEventListener('resize', onResize);
	}, []);

	useEffect(() => {
		const visible = layerConfig.layers.filter(l => l.allowedTools.includes(activeTool));
		if (visible.length > 0 && !visible.find(l => l.id === activeLayer?.id))
			setActiveLayer(visible[0]);
	}, [activeTool, layerConfig]);

	useEffect(() => {
		if (!visible || !activeLayer) return;
		const dto = {
			...activeLayer,
			style: Object.fromEntries(Object.entries(activeLayer.style).map(([k, v]) => [k, String(v)])),
		};
		trigger('skyplan', 'setLayer', JSON.stringify(dto));
	}, [visible, activeLayer]);

	const onToolChange = useCallback((t: ToolId) => {
		setActiveTool(t);
		trigger('skyplan', 'setTool', t);
	}, []);

	const onLayerChange = useCallback((l: LayerDef) => {
		setActiveLayer(l);
		const dto = {
			...l,
			style: Object.fromEntries(Object.entries(l.style).map(([k, v]) => [k, String(v)])),
		};
		trigger('skyplan', 'setLayer', JSON.stringify(dto));
	}, []);

	const onClear = useCallback(() => {
		if (!activeLayer) return;
		trigger('skyplan', 'clearLayer', activeLayer.id);
	}, [activeLayer]);

	const onClearAll = useCallback(() => trigger('skyplan', 'clearAll', ''), []);
	const onClose = useCallback(() => trigger('skyplan', 'panelClosed', ''), []);
	const onUndo = useCallback(() => trigger('skyplan', 'undo', ''), []);
	const onViewModeToggle = useCallback(() => setViewMode(v => !v), []);

	const value: SkyplanCtx = {
		visible,
		shapes,
		preview,
		highlightId,
		activeTool,
		activeLayer,
		visibleLayers,
		svgSize,
		viewMode,
		toolbarPos,
		onToolbarPosChange: setToolbarPos,
		onViewModeToggle,
		onToolChange,
		onLayerChange,
		onUndo,
		onClear,
		onClearAll,
		onClose,
	};

	return (
		<SkyplanContext.Provider value={value}>
			{children}
		</SkyplanContext.Provider>
	);
};
