import React, {createContext, useContext, useState, useEffect, useMemo, useCallback, useRef} from 'react';
import {useValue, trigger} from 'cs2/api';
import {panelVisible$, layersConfig$} from '../bindings';
import {ToolId, LayerDef, LabelStyle} from './types';

interface SkyplanCtx {
	visible: boolean;
	activeTool: ToolId;
	activeLayer: LayerDef | null;
	visibleLayers: LayerDef[];
	allLayers: LayerDef[];
	globalLabelStyle: LabelStyle;
	viewMode: boolean;
	toolbarPos: { left: number; top: number } | null;
	onToolbarPosChange: (pos: { left: number; top: number }) => void;
	onViewModeToggle: () => void;
	onToolChange: (t: ToolId) => void;
	onLayerChange: (l: LayerDef) => void;
	onUndo: () => void;
	onRedo: () => void;
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
	const layersConfigJson = useValue(layersConfig$);


	const layerConfig = useMemo<{ labelStyle?: LabelStyle; layers: LayerDef[] }>(() => {
		try { return JSON.parse(layersConfigJson); }
		catch { return { layers: [] }; }
	}, [layersConfigJson]);

	const [activeTool, setActiveTool] = useState<ToolId>('path');
	const [activeLayer, setActiveLayer] = useState<LayerDef | null>(null);
	const [viewMode, setViewMode] = useState(false);
	const [toolbarPos, setToolbarPos] = useState<{ left: number; top: number } | null>(null);

	const visibleLayers = layerConfig.layers.filter(l => l.allowedTools.includes(activeTool));


	const prevVisibleRef = useRef(false);
	useEffect(() => {
		const justOpened = visible && !prevVisibleRef.current;
		prevVisibleRef.current = visible;
		if (justOpened) {
			setActiveLayer(null);
			return;
		}
		if (!visible) return;
		const visibleForTool = layerConfig.layers.filter(l => l.allowedTools.includes(activeTool));
		if (visibleForTool.length > 0 && !visibleForTool.find(l => l.id === activeLayer?.id))
			setActiveLayer(visibleForTool[0]);
	}, [activeTool, layerConfig, visible]);

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
	const onRedo = useCallback(() => trigger('skyplan', 'redo', ''), []);
	const onViewModeToggle = useCallback(() => setViewMode(v => !v), []);

	const value: SkyplanCtx = {
		visible,
		activeTool,
		activeLayer,
		visibleLayers,
		allLayers: layerConfig.layers,
		globalLabelStyle: layerConfig.labelStyle ?? {},
		viewMode,
		toolbarPos,
		onToolbarPosChange: setToolbarPos,
		onViewModeToggle,
		onToolChange,
		onLayerChange,
		onUndo,
		onRedo,
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
