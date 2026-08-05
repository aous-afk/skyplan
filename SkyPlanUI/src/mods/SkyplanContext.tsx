import React, {createContext, useContext, useState, useEffect, useMemo, useCallback} from 'react';
import {useValue, trigger} from 'cs2/api';
import {panelVisible$, layersConfig$} from '../bindings';
import {ToolId, LayerDef} from './types';

interface SkyplanCtx {
	visible: boolean;
	activeTool: ToolId;
	activeLayer: LayerDef | null;
	visibleLayers: LayerDef[];
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
	const layersConfigJson = useValue(layersConfig$);


	const layerConfig = useMemo<{ layers: LayerDef[] }>(() => {
		try { return JSON.parse(layersConfigJson); }
		catch { return { layers: [] }; }
	}, [layersConfigJson]);

	const [activeTool, setActiveTool] = useState<ToolId>('path');
	const [activeLayer, setActiveLayer] = useState<LayerDef | null>(null);
	const [viewMode, setViewMode] = useState(false);
	const [toolbarPos, setToolbarPos] = useState<{ left: number; top: number } | null>(null);

	const visibleLayers = layerConfig.layers.filter(l => l.allowedTools.includes(activeTool));


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
		activeTool,
		activeLayer,
		visibleLayers,
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
