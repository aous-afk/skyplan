import React, {createContext, useCallback, useContext, useEffect, useMemo, useState} from "react";
import {useValue} from 'cs2/api';
import {shapes$, preview$, highlight$} from '../bindings';
import {ShapeData} from './types';

interface DrawingCtx {
	shapes: ShapeData[];
	preview: ShapeData | null;
	highlightId: string | null;
	svgSize: { w: number; h: number };
	globalOpacity: number;
	layerOpacities: Record<string, number>;
	layerVisible: Record<string, boolean>;
	onGlobalOpacityChange: (v: number) => void;
	onLayerOpacityChange: (layerId: string, v: number) => void;
	onLayerVisibleToggle: (layerId: string) => void;
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

	const shapes = useMemo<ShapeData[]>(() => {
		try { return JSON.parse(shapesJson) ?? []; }
		catch { return []; }
	}, [shapesJson]);

	const preview = useMemo<ShapeData | null>(() => {
		try { return previewJson ? JSON.parse(previewJson) : null; }
		catch { return null; }
	}, [previewJson]);

	const [svgSize, setSvgSize] = useState({ w: 1920, h: 1080 });
	const [globalOpacity, setGlobalOpacity] = useState(1);
	const [layerOpacities, setLayerOpacities] = useState<Record<string, number>>({});
	const [layerVisible, setLayerVisible] = useState<Record<string, boolean>>({});

	const highlightId = highlightRaw || null;

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
		setLayerVisible(prev => ({ ...prev, [layerId]: !(prev[layerId] ?? true) }));
	}, []);

	const value: DrawingCtx = {
		shapes,
		preview,
		highlightId,
		svgSize,
		globalOpacity,
		layerOpacities,
		layerVisible,
		onGlobalOpacityChange: setGlobalOpacity,
		onLayerOpacityChange,
		onLayerVisibleToggle,
	};

	return (
		<DrawingContext.Provider value={value}>
			{children}
		</DrawingContext.Provider>
	);
};
