import React, {createContext, useContext, useMemo, useCallback, useState} from 'react';
import {useValue} from 'cs2/api';
import {layersConfig$} from '../bindings';
import {LayerDef, LabelStyle} from './types';

export interface StyleTarget {
	shapeId: string;
	layerId: string;
}

interface StyleCtx {
	allLayers: LayerDef[];
	layerById: Record<string, LayerDef>;
	globalLabelStyle: LabelStyle;
	// Layer label style over the global one, then hard defaults.
	labelStyleFor: (layerId: string) => Required<LabelStyle>;

	// Style Manager: the shape being restyled and the unsaved style for its layer. The draft is
	// only drawn on the target shape until it's saved.
	styleTarget: StyleTarget | null;
	draftStyle: Record<string, string> | null;
	draftLabelStyle: Required<LabelStyle> | null;
	onStyleTarget: (target: StyleTarget | null) => void;
	onDraftStyleChange: (patch: Record<string, string>) => void;
	onDraftLabelStyleChange: (patch: Partial<LabelStyle>) => void;
	onResetDraft: () => void;
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

	const [styleTarget, setStyleTarget] = useState<StyleTarget | null>(null);
	const [draftStyle, setDraftStyle] = useState<Record<string, string> | null>(null);
	const [draftLabelStyle, setDraftLabelStyle] = useState<Required<LabelStyle> | null>(null);

	const layerStyleStrings = useCallback((layerId: string): Record<string, string> =>
		Object.fromEntries(Object.entries(layerById[layerId]?.style ?? {}).map(([k, v]) => [k, String(v)])),
		[layerById]
	);

	const onStyleTarget = useCallback((target: StyleTarget | null) => {
		setStyleTarget(target);
		setDraftStyle(target ? layerStyleStrings(target.layerId) : null);
		setDraftLabelStyle(target ? labelStyleFor(target.layerId) : null);
	}, [layerStyleStrings, labelStyleFor]);

	const onDraftStyleChange = useCallback((patch: Record<string, string>) => {
		setDraftStyle(prev => prev ? { ...prev, ...patch } : prev);
	}, []);

	const onDraftLabelStyleChange = useCallback((patch: Partial<LabelStyle>) => {
		setDraftLabelStyle(prev => prev ? { ...prev, ...patch } : prev);
	}, []);

	const onResetDraft = useCallback(() => {
		if (!styleTarget) return;
		setDraftStyle(layerStyleStrings(styleTarget.layerId));
		setDraftLabelStyle(labelStyleFor(styleTarget.layerId));
	}, [styleTarget, layerStyleStrings, labelStyleFor]);

	const value = useMemo<StyleCtx>(() => ({
		allLayers,
		layerById,
		globalLabelStyle,
		labelStyleFor,
		styleTarget,
		draftStyle,
		draftLabelStyle,
		onStyleTarget,
		onDraftStyleChange,
		onDraftLabelStyleChange,
		onResetDraft,
	}), [allLayers, layerById, globalLabelStyle, labelStyleFor, styleTarget, draftStyle, draftLabelStyle,
		onStyleTarget, onDraftStyleChange, onDraftLabelStyleChange, onResetDraft]);

	return (
		<StyleContext.Provider value={value}>
			{children}
		</StyleContext.Provider>
	);
};
