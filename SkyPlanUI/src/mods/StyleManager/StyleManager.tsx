import React from 'react';
import DraggablePanel from '../DraggablePanel/DraggablePanel';
import {useStyle} from '../StyleContext';
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
	if (!styleTarget) return null;
	const layer = layerById[styleTarget.layerId];

	return (
		<DraggablePanel
			persistKey="style-manager"
			title={`Style: ${layer?.label ?? styleTarget.layerId}`}
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
					<button className={styles.preset} onClick={onResetDraft}>Reset</button>
				</div>
			</div>
		</DraggablePanel>
	);
};

export default StyleManager;
