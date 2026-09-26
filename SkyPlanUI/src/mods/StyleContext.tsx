import React, {createContext, useContext, useMemo, useCallback} from 'react';
import {useValue} from 'cs2/api';
import {layersConfig$} from '../bindings';
import {LayerDef, LabelStyle} from './types';

interface StyleCtx {
	allLayers: LayerDef[];
	layerById: Record<string, LayerDef>;
	globalLabelStyle: LabelStyle;
	// Layer label style over the global one, then hard defaults.
	labelStyleFor: (layerId: string) => Required<LabelStyle>;
}

const StyleContext = createContext<StyleCtx | null>(null);

export const useStyle = (): StyleCtx => {
	const ctx = useContext(StyleContext);
	if (!ctx) throw new Error('useStyle used outside StyleProvider');
	return ctx;
};

export const StyleProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
	const layersConfigJson = useValue(layersConfig$);

	const layerConfig = useMemo<{ labelStyle?: LabelStyle; layers: LayerDef[] }>(() => {
		try { return JSON.parse(layersConfigJson); }
		catch { return { layers: [] }; }
	}, [layersConfigJson]);

	const allLayers = layerConfig.layers;
	const globalLabelStyle = useMemo(() => layerConfig.labelStyle ?? {}, [layerConfig]);

	const layerById = useMemo(() =>
		Object.fromEntries(allLayers.map(l => [l.id, l])) as Record<string, LayerDef>,
		[allLayers]
	);

	const labelStyleFor = useCallback((layerId: string): Required<LabelStyle> => {
		const own = layerById[layerId]?.labelStyle;
		return {
			color: own?.color ?? globalLabelStyle.color ?? '#ffffff',
			fontSize: own?.fontSize ?? globalLabelStyle.fontSize ?? 12,
			fontWeight: own?.fontWeight ?? globalLabelStyle.fontWeight ?? 'normal',
			opacity: own?.opacity ?? globalLabelStyle.opacity ?? 1,
		};
	}, [layerById, globalLabelStyle]);

	const value = useMemo<StyleCtx>(() => ({
		allLayers,
		layerById,
		globalLabelStyle,
		labelStyleFor,
	}), [allLayers, layerById, globalLabelStyle, labelStyleFor]);

	return (
		<StyleContext.Provider value={value}>
			{children}
		</StyleContext.Provider>
	);
};
