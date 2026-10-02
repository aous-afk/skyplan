import React, {createContext, useContext, useMemo, useCallback, useState} from 'react';
import {useValue} from 'cs2/api';
import {layersConfig$} from '../bindings';
import {LayerDef, LabelStyle, ToolId} from './types';

// Layer mode (from the toolbar): layerId + tool. Shape mode (from the Shape Manager): also shapeId.
export interface StyleTarget {
	layerId: string;
	tool: ToolId;
	shapeId?: string;
}

interface Draft {
	// The layer it was seeded from; a draft is only reused for that same layer.
	layerId: string;
	style: Record<string, string>;
	labelStyle: Required<LabelStyle>;
}

interface StyleCtx {
	allLayers: LayerDef[];
	layerById: Record<string, LayerDef>;
	globalLabelStyle: LabelStyle;
	// Layer label style over the global one, then hard defaults.
	labelStyleFor: (layerId: string) => Required<LabelStyle>;

	// Style Manager: what's being restyled, and the unsaved values for the target's tool + layer.
	// Drafts are kept per tool and layer for the whole game session (closing the panel keeps them);
	// the first open of a tool/layer pair seeds it from the layer. In shape mode the draft is drawn
	// on the target shape; in layer mode only in the panel's preview.
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
	// drafts[tool][layerId]: each layer keeps its own draft per tool, so switching layers neither
	// resets nor leaks edits.
	const [drafts, setDrafts] = useState<Partial<Record<ToolId, Record<string, Draft>>>>({});

	const seedDraft = useCallback((layerId: string): Draft => ({
		layerId,
		style: Object.fromEntries(Object.entries(layerById[layerId]?.style ?? {}).map(([k, v]) => [k, String(v)])),
		labelStyle: labelStyleFor(layerId),
	}), [layerById, labelStyleFor]);

	const setDraft = (prev: Partial<Record<ToolId, Record<string, Draft>>>, tool: ToolId, d: Draft) =>
		({ ...prev, [tool]: { ...prev[tool], [d.layerId]: d } });

	// Seeds from the layer only the first time this tool/layer pair is opened.
	const onStyleTarget = useCallback((target: StyleTarget | null) => {
		setStyleTarget(target);
		if (target) setDrafts(prev => prev[target.tool]?.[target.layerId]
			? prev
			: setDraft(prev, target.tool, seedDraft(target.layerId)));
	}, [seedDraft]);

	const draft = styleTarget ? drafts[styleTarget.tool]?.[styleTarget.layerId] ?? null : null;

	const patchDraft = useCallback((patch: (d: Draft) => Draft) => {
		if (!styleTarget) return;
		const { tool, layerId } = styleTarget;
		setDrafts(prev => {
			const d = prev[tool]?.[layerId];
			return d ? setDraft(prev, tool, patch(d)) : prev;
		});
	}, [styleTarget]);

	const onDraftStyleChange = useCallback((p: Record<string, string>) =>
		patchDraft(d => ({ ...d, style: { ...d.style, ...p } })), [patchDraft]);

	const onDraftLabelStyleChange = useCallback((p: Partial<LabelStyle>) =>
		patchDraft(d => ({ ...d, labelStyle: { ...d.labelStyle, ...p } })), [patchDraft]);

	const onResetDraft = useCallback(() => {
		if (!styleTarget) return;
		setDrafts(prev => setDraft(prev, styleTarget.tool, seedDraft(styleTarget.layerId)));
	}, [styleTarget, seedDraft]);

	const draftStyle = draft?.style ?? null;
	const draftLabelStyle = draft?.labelStyle ?? null;

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
