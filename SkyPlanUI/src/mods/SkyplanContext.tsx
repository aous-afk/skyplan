import React, {createContext, useContext, useState, useEffect, useMemo, useCallback, useRef} from 'react';
import {useValue, trigger} from 'cs2/api';
import {panelVisible$, layersConfig$} from '../bindings';
import {ToolId, LayerDef, LabelStyle} from './types';

export interface LayerSelection {
	layer: LayerDef;
	count: number;
}

interface SkyplanCtx {
	visible: boolean;
	activeTool: ToolId | null;
	activeLayers: LayerSelection[];
	primaryLayer: LayerDef | null;
	visibleLayers: LayerDef[];
	allLayers: LayerDef[];
	globalLabelStyle: LabelStyle;
	viewMode: boolean;
	onViewModeToggle: () => void;
	showWhatsNew: boolean;
	onOpenWhatsNew: () => void;
	onCloseWhatsNew: () => void;
	onToolChange: (t: ToolId | null) => void;
	onLayerAdd: (l: LayerDef) => void;
	onLayerRemove: (l: LayerDef) => void;
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

	const [activeTool, setActiveTool] = useState<ToolId | null>('path');
	const [activeLayers, setActiveLayers] = useState<LayerSelection[]>([]);
	const [viewMode, setViewMode] = useState(false);
	const [showWhatsNew, setShowWhatsNew] = useState(false);

	const primaryLayer = activeLayers[0]?.layer ?? null;

	const visibleLayers = activeTool ? layerConfig.layers.filter(l => l.allowedTools.includes(activeTool)) : [];


	const prevVisibleRef = useRef(false);
	useEffect(() => {
		const justOpened = visible && !prevVisibleRef.current;
		prevVisibleRef.current = visible;
		if (justOpened) {
			setActiveLayers([]);
			return;
		}
		if (!visible || !activeTool) return;
		const visibleForTool = layerConfig.layers.filter(l => l.allowedTools.includes(activeTool));
		if (visibleForTool.length > 0 && !visibleForTool.find(l => l.id === primaryLayer?.id))
			setActiveLayers([{ layer: visibleForTool[0], count: 1 }]);
	}, [activeTool, layerConfig, visible]);

	useEffect(() => {
		if (!visible || !primaryLayer) return;
		const dto = {
			...primaryLayer,
			style: Object.fromEntries(Object.entries(primaryLayer.style).map(([k, v]) => [k, String(v)])),
		};
		trigger('skyplan', 'setLayer', JSON.stringify(dto));
	}, [visible, primaryLayer]);

	// Everything beyond the one real shape that'll actually get drawn: the primary (first queued)
	// layer's own count minus 1 (its first lane IS the real shape), plus every other queued layer's
	// full count. C# just stores this verbatim and attaches it to the next drawn line
	useEffect(() => {
		if (!visible) return;
		const extraLanes = activeLayers
			.map((entry, i) => ({ layerId: entry.layer.id, count: i === 0 ? entry.count - 1 : entry.count }))
			.filter(e => e.count > 0);
		trigger('skyplan', 'setParallelLayers', JSON.stringify(extraLanes));
	}, [visible, activeLayers]);

	const onToolChange = useCallback((t: ToolId | null) => {
		setActiveTool(t);
		if (t) trigger('skyplan', 'setTool', t);
	}, []);

	const onLayerAdd = useCallback((l: LayerDef) => {
		setActiveLayers(prev => {
			const idx = prev.findIndex(e => e.layer.id === l.id);
			if (idx === -1) return [...prev, { layer: l, count: 1 }];
			const next = [...prev];
			next[idx] = { ...next[idx], count: next[idx].count + 1 };
			return next;
		});
	}, []);

	const onLayerRemove = useCallback((l: LayerDef) => {
		setActiveLayers(prev => {
			const idx = prev.findIndex(e => e.layer.id === l.id);
			if (idx === -1) return prev;
			const nextCount = prev[idx].count - 1;
			if (nextCount <= 0) return prev.filter((_, i) => i !== idx);
			const next = [...prev];
			next[idx] = { ...next[idx], count: nextCount };
			return next;
		});
	}, []);

	const onClear = useCallback(() => {
		if (!primaryLayer) return;
		trigger('skyplan', 'clearLayer', primaryLayer.id);
	}, [primaryLayer]);

	const onClearAll = useCallback(() => trigger('skyplan', 'clearAll', ''), []);
	const onClose = useCallback(() => trigger('skyplan', 'panelClosed', ''), []);
	const onUndo = useCallback(() => trigger('skyplan', 'undo', ''), []);
	const onRedo = useCallback(() => trigger('skyplan', 'redo', ''), []);
	const onViewModeToggle = useCallback(() => setViewMode(v => !v), []);
	const onOpenWhatsNew = useCallback(() => setShowWhatsNew(true), []);
	const onCloseWhatsNew = useCallback(() => setShowWhatsNew(false), []);

	const value: SkyplanCtx = {
		visible,
		activeTool,
		activeLayers,
		primaryLayer,
		visibleLayers,
		allLayers: layerConfig.layers,
		globalLabelStyle: layerConfig.labelStyle ?? {},
		viewMode,
		onViewModeToggle,
		showWhatsNew,
		onOpenWhatsNew,
		onCloseWhatsNew,
		onToolChange,
		onLayerAdd,
		onLayerRemove,
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
