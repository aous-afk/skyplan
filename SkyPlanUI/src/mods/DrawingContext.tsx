import React, {createContext, useCallback, useContext, useEffect, useMemo, useState} from "react";
import {useValue, trigger} from 'cs2/api';
import {shapes$, preview$, highlight$, showDescriptions$, indicator$, snapEnabled$, layerVisible$} from '../bindings';
import {ShapeData} from './types';

export interface SnapIndicator {
	x: number;
	y: number;
	kind: 'vertex' | 'edge';
}

interface DrawingCtx {
	shapes: ShapeData[];
	preview: ShapeData | null;
	highlightId: string | null;
	indicator: SnapIndicator | null;
	svgSize: { w: number; h: number };
	globalOpacity: number;
	layerOpacities: Record<string, number>;
	layerVisible: Record<string, boolean>;
	layerLabels: Record<string, boolean>;
	showDescriptions: boolean;
	snapEnabled: boolean;
	onGlobalOpacityChange: (v: number) => void;
	onLayerOpacityChange: (layerId: string, v: number) => void;
	onLayerVisibleToggle: (layerId: string) => void;
	onLayerLabelsToggle: (layerId: string) => void;
	onShowDescriptionsToggle: () => void;
	onSnapEnabledToggle: () => void;
	onHoverShape: (id: string | null) => void;
}

const DrawingContext = createContext<DrawingCtx | null>(null);

export const useDrawingContext = (): DrawingCtx => {
	const context = useContext(DrawingContext);
	if (!context) throw new Error('useDrawingContext used outside DrawingProvider');
	return context;
};

export const DrawingProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
	const shapesJson = useValue(shapes$);
	const previewJson = useValue(preview$);
	const highlightRaw = useValue(highlight$);
	const showDescriptions = useValue(showDescriptions$);
	const indicatorRaw = useValue(indicator$);
	const snapEnabled = useValue(snapEnabled$);
	const layerVisibleRaw = useValue(layerVisible$);

	const shapes = useMemo<ShapeData[]>(() => {
		try { return JSON.parse(shapesJson) ?? []; }
		catch { return []; }
	}, [shapesJson]);

	const preview = useMemo<ShapeData | null>(() => {
		try { return previewJson ? JSON.parse(previewJson) : null; }
		catch { return null; }
	}, [previewJson]);

	const indicator = useMemo<SnapIndicator | null>(() => {
		if (!indicatorRaw) return null;
		const [xRaw, yRaw, kind] = indicatorRaw.split(',');
		const x = parseFloat(xRaw), y = parseFloat(yRaw);
		if (Number.isNaN(x) || Number.isNaN(y) || (kind !== 'vertex' && kind !== 'edge')) return null;
		return { x, y, kind };
	}, [indicatorRaw]);

	const layerVisible = useMemo<Record<string, boolean>>(() => {
		try { return JSON.parse(layerVisibleRaw) ?? {}; }
		catch { return {}; }
	}, [layerVisibleRaw]);

	const [svgSize, setSvgSize] = useState({ w: 1920, h: 1080 });
	const [globalOpacity, setGlobalOpacity] = useState(1);
	const [layerOpacities, setLayerOpacities] = useState<Record<string, number>>({});
	const [layerLabels, setLayerLabels] = useState<Record<string, boolean>>({});
	const [hoverShapeId, setHoverShapeId] = useState<string | null>(null);

	const highlightId = hoverShapeId ?? (highlightRaw || null);

	useEffect(() => {
		const onResize = () => setSvgSize({ w: window.innerWidth || 1920, h: window.innerHeight || 1080 });
		onResize();
		window.addEventListener('resize', onResize);
		return () => window.removeEventListener('resize', onResize);
	}, []);

	const onLayerOpacityChange = useCallback((layerId: string, v: number) => {
		setLayerOpacities(prev => ({ ...prev, [layerId]: v }));
	}, []);

	const onLayerVisibleToggle = useCallback((layerId: string) => {
		const next = !(layerVisible[layerId] ?? true);
		trigger('skyplan', 'setLayerVisible', `${layerId}|${next}`);
	}, [layerVisible]);

	const onLayerLabelsToggle = useCallback((layerId: string) => {
		setLayerLabels(prev => ({ ...prev, [layerId]: !(prev[layerId] ?? false) }));
	}, []);

	const onShowDescriptionsToggle = useCallback(() => {
		trigger('skyplan', 'setShowDescriptions', (!showDescriptions).toString());
	}, [showDescriptions]);

	const onSnapEnabledToggle = useCallback(() => {
		trigger('skyplan', 'setSnapEnabled', (!snapEnabled).toString());
	}, [snapEnabled]);

	const onHoverShape = useCallback((id: string | null) => {
		setHoverShapeId(id);
	}, []);

	const value: DrawingCtx = {
		shapes,
		preview,
		highlightId,
		indicator,
		svgSize,
		globalOpacity,
		layerOpacities,
		layerVisible,
		layerLabels,
		showDescriptions,
		snapEnabled,
		onGlobalOpacityChange: setGlobalOpacity,
		onLayerOpacityChange,
		onLayerVisibleToggle,
		onLayerLabelsToggle,
		onShowDescriptionsToggle,
		onSnapEnabledToggle,
		onHoverShape,
	};

	return (
		<DrawingContext.Provider value={value}>
			{children}
		</DrawingContext.Provider>
	);
};
