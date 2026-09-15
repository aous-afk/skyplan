import React, {createContext, useContext, useState, useEffect, useMemo, useCallback, useRef} from 'react';
import {useValue, trigger} from 'cs2/api';
import {panelVisible$, layersConfig$} from '../bindings';
import {ToolId, LayerDef, LabelStyle} from './types';

interface SkyplanCtx {
	visible: boolean;
	activeTool: ToolId | null;
	// Flat, ordered, duplicates allowed - literally the click sequence (Train, Subway, Train stays
	// three entries in that order), not grouped-by-layer-with-a-count. A {layer,count} shape can't
	// represent interleaved repeats of the same layer; only the raw sequence can. See dev_doc.md.
	activeLayers: LayerDef[];
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
	onLayerSelect: (l: LayerDef) => void;
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
	const [activeLayers, setActiveLayers] = useState<LayerDef[]>([]);
	const [viewMode, setViewMode] = useState(false);
	const [showWhatsNew, setShowWhatsNew] = useState(false);

	const primaryLayer = activeLayers[0] ?? null;

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
			setActiveLayers([visibleForTool[0]]);
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
	// entry's own lane IS the real shape, so only the tail is "extra" - sent in the same order it was
	// queued, so C# (and everything downstream) sees the actual click sequence, not a per-layer tally.
	useEffect(() => {
		if (!visible) return;
		const extraLanes = activeLayers.slice(1).map(l => ({ layerId: l.id }));
		trigger('skyplan', 'setParallelLayers', JSON.stringify(extraLanes));
	}, [visible, activeLayers]);

	const onToolChange = useCallback((t: ToolId | null) => {
		setActiveTool(t);
		if (t) trigger('skyplan', 'setTool', t);
	}, []);

	// Always appends - clicking the same layer twice queues it twice, in order, rather than merging
	// into one entry with a count (see activeLayers' own comment above for why that distinction matters).
	const onLayerAdd = useCallback((l: LayerDef) => {
		setActiveLayers(prev => [...prev, l]);
	}, []);

	// For tools without allowMultiSelect (see types.ts's TOOLS) - clicking a layer replaces the whole
	// queue with just this one, single-select-style, instead of adding/incrementing.
	const onLayerSelect = useCallback((l: LayerDef) => {
		setActiveLayers([l]);
	}, []);

	// Removes the LAST occurrence of this layer id - undoes the most recent click of that specific
	// layer, not an arbitrary one, matching onLayerAdd always appending at the end.
	const onLayerRemove = useCallback((l: LayerDef) => {
		setActiveLayers(prev => {
			const idx = prev.map(x => x.id).lastIndexOf(l.id);
			if (idx === -1) return prev;
			return [...prev.slice(0, idx), ...prev.slice(idx + 1)];
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
		onLayerSelect,
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
