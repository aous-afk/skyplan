import React, {useEffect} from 'react';
import DraggablePanel from '../DraggablePanel/DraggablePanel';
import {useStyle} from '../StyleContext';
import {useSkyplan} from '../SkyplanContext';
import {useDrawingContext} from '../DrawingContext';
import PreviewSection from './sections/PreviewSection';
import WidthSection from './sections/WidthSection';
import DashSection from './sections/DashSection';
import ColorSection from './sections/ColorSection';
import LabelSection from './sections/LabelSection';
import styles from './StyleManager.module.scss';

const Separator: React.FC = () => <div className={styles.separator} />;

// Container width (270) plus the panel's own padding/border; only used to place it at the right edge.
const PANEL_WIDTH = 300;
const EDGE_MARGIN = 12;

const StyleManager: React.FC = () => {
	const { layerById, styleTarget, onStyleTarget, onResetDraft } = useStyle();
	const { primaryLayer, activeTool } = useSkyplan();
	const { shapes } = useDrawingContext();

	// Layer mode follows the toolbar's main selected layer (and its tool); shape mode stays on its shape.
	useEffect(() => {
		if (!styleTarget || styleTarget.shapeId) return;
		if (!primaryLayer || !activeTool || activeTool === 'erase') return;
		if (primaryLayer.id === styleTarget.layerId && activeTool === styleTarget.tool) return;
		onStyleTarget({ layerId: primaryLayer.id, tool: activeTool });
	}, [styleTarget, primaryLayer, activeTool, onStyleTarget]);

	if (!styleTarget) return null;
	const layer = layerById[styleTarget.layerId];
	const layerLabel = layer?.label ?? styleTarget.layerId;
	const shapeLabel = styleTarget.shapeId ? shapes.find(s => s.id === styleTarget.shapeId)?.label : undefined;
	const title = styleTarget.shapeId ? `Style: ${layerLabel} – ${shapeLabel || 'shape'}` : `Style: ${layerLabel}`;

	return (
		<DraggablePanel
			persistKey="style-manager"
			title={title}
			onClose={() => onStyleTarget(null)}
			defaultPosition={{ left: Math.max(EDGE_MARGIN, window.innerWidth - PANEL_WIDTH - EDGE_MARGIN), top: EDGE_MARGIN }}
		>
			<div className={styles.container}>
				<PreviewSection />
				<Separator />
				<WidthSection />
				<Separator />
				<DashSection />
				<Separator />
				<ColorSection />
				<Separator />
				<LabelSection />
				<Separator />
				<div className={styles.footer}>
					<button className={styles.preset} style={{ color: 'rgba(255,255,255,0.8)' }} onClick={onResetDraft}>Reset</button>
				</div>
			</div>
		</DraggablePanel>
	);
};

export default StyleManager;
