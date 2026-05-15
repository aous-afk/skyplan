import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { useValue, trigger } from 'cs2/api';
import { panelVisible$, shapes$, preview$, highlight$ } from '../bindings';
import Toolbar from './Toolbar/Toolbar';
import DrawingCanvas from './DrawingCanvas/DrawingCanvas';
import { Tool, Layer, ShapeData } from './types';

const SkyplanOverlay: React.FC = () => {
	const visible = useValue(panelVisible$);
	const shapesJson = useValue(shapes$);
	const previewJson = useValue(preview$);
	const highlightRaw = useValue(highlight$);

	const shapes = useMemo<ShapeData[]>(() => { try { return JSON.parse(shapesJson) ?? []; } catch { return []; } }, [shapesJson]);
	const preview = useMemo<ShapeData | null>(() => { try { return previewJson ? JSON.parse(previewJson) : null; } catch { return null; } }, [previewJson]);
	const highlightId = highlightRaw || null;

	const [activeTool, setActiveTool] = useState<Tool>('line');
	const [activeLayer, setActiveLayer] = useState<Layer>('roads');
	const [svgSize, setSvgSize] = useState({ w: 1920, h: 1080 });

	useEffect(() => {
		const onResize = () => setSvgSize({ w: window.innerWidth || 1920, h: window.innerHeight || 1080 });
		onResize();
		window.addEventListener('resize', onResize);
		return () => window.removeEventListener('resize', onResize);
	}, []);

	const handleTool = useCallback((t: Tool) => {
		setActiveTool(t);
		trigger('skyplan', 'setTool', t);
	}, []);

	const handleLayer = useCallback((l: Layer) => {
		setActiveLayer(l);
		trigger('skyplan', 'setLayer', l);
	}, []);

	const handleClear = useCallback(() => {
		trigger('skyplan', 'clearLayer', activeLayer);
	}, [activeLayer]);

	const handleClose = useCallback(() => {
		trigger('skyplan', 'panelClosed');
	}, []);


	if (!visible) return null;

	return (
		<div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, zIndex: 9000, pointerEvents: 'none' }}>

			<Toolbar
				activeTool={activeTool}
				activeLayer={activeLayer}
				onToolChange={handleTool}
				onLayerChange={handleLayer}
				onClear={handleClear}
				onClose={handleClose}
			/>
			<DrawingCanvas
				activeTool={activeTool}
				shapes={shapes}
				preview={preview}
				highlightId={highlightId}
				svgSize={svgSize}
			/>

		</div>
	);
};

export default SkyplanOverlay;
