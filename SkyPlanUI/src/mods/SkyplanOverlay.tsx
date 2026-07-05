import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {useValue, trigger} from 'cs2/api';
import {panelVisible$, shapes$, preview$, highlight$} from '../bindings';
import Toolbar from './Toolbar/Toolbar';
import DrawingCanvas from './DrawingCanvas/DrawingCanvas';
import {ToolId, Layer, ShapeData, LayerDef} from './types';
import layerConfig from '../layers.json';

const SkyplanOverlay: React.FC = () => {
	const visible = useValue(panelVisible$);
	const shapesJson = useValue(shapes$);
	const previewJson = useValue(preview$);
	const highlightRaw = useValue(highlight$);

	const shapes = useMemo<ShapeData[]>(() => { try { return JSON.parse(shapesJson) ?? []; } catch { return []; } }, [shapesJson]);
	const preview = useMemo<ShapeData | null>(() => { try { return previewJson ? JSON.parse(previewJson) : null; } catch { return null; } }, [previewJson]);
	const highlightId = highlightRaw || null;

	const [activeTool, setActiveTool] = useState<ToolId>('line');
	const [activeLayer, setActiveLayer] = useState<LayerDef>(layerConfig.layers[0]);
	const [svgSize, setSvgSize] = useState({ w: 1920, h: 1080 });

	const visibleLayers = layerConfig.layers.filter(l => l.allowedTools.includes(activeTool));

	useEffect(() => {
		const onResize = () => setSvgSize({ w: window.innerWidth || 1920, h: window.innerHeight || 1080 });
		onResize();
		window.addEventListener('resize', onResize);
		return () => window.removeEventListener('resize', onResize);
	}, []);

	useEffect(() => {
		const visible = layerConfig.layers.filter(l => l.allowedTools.includes(activeTool));
		if (visible.length > 0 && !visible.find(l => l.id === activeLayer.id))
			setActiveLayer(visible[0]);
	}, [activeTool]);

	useEffect(() => {
		if (!visible) return;
		const dto = {
			...activeLayer,
			style: Object.fromEntries(
				Object.entries(activeLayer.style).map(([k, v]) => [k, String(v)])
			),
		};
		trigger('skyplan', 'setLayer', JSON.stringify(dto));
	}, [visible]);

	const handleTool = useCallback((t: ToolId) => {
		setActiveTool(t);
		trigger('skyplan', 'setTool', t);
	}, []);

	const handleLayer = useCallback((l: LayerDef) => {
		setActiveLayer(l);
		const dto = {
			...l,
			style: Object.fromEntries(
				Object.entries(l.style).map(([k, v]) => [k, String(v)])
			),
		};
		trigger('skyplan', 'setLayer', JSON.stringify(dto));
	}, []);

	const handleClear = useCallback(() => {
		trigger('skyplan', 'clearLayer', activeLayer.id);
	}, [activeLayer]);

	const handleClose = useCallback(() => {
		trigger('skyplan', 'panelClosed', '');
	}, []);

	const HandleUndo = useCallback(() => {
		trigger('skyplan', 'undo', '');
	}, [])


	if (!visible) return null;

	return (
	  <div>
		<div data-skyplan-ui
		style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, zIndex: 9000, pointerEvents: 'none' }}>

			<Toolbar
				activeTool={activeTool}
				activeLayer={activeLayer}
				layers={visibleLayers}
				onToolChange={handleTool}
				onLayerChange={handleLayer}
				onUndo={HandleUndo}
				onClear={handleClear}
				onClose={handleClose}
			/>
			</div>
			<div className="main-container">
			<DrawingCanvas
				activeTool={activeTool}
				shapes={shapes}
				preview={preview}
				highlightId={highlightId}
				svgSize={svgSize}
			/>

		</div>
		</div>
	);
};

export default SkyplanOverlay;
