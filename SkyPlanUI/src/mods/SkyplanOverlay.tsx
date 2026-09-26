import React from 'react';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome';
import {faCircleQuestion} from '@fortawesome/free-solid-svg-icons';
import {StyleProvider} from './StyleContext';
import {SkyplanProvider, useSkyplan} from './SkyplanContext';
import {DrawingProvider} from './DrawingContext';
import Toolbar from './Toolbar/Toolbar';
import DraggablePanel from './DraggablePanel/DraggablePanel';
import panelStyles from './DraggablePanel/DraggablePanel.module.scss';
import DrawingCanvas from './DrawingCanvas/DrawingCanvas';
import ServiceCatchmentOverlay from './ServiceCatchmentOverlay';
import WhatsNewPanel from './WhatsNew/WhatsNewPanel';

const SkyplanOverlayInner: React.FC = () => {
	const { visible, showWhatsNew, onCloseWhatsNew, onClose, onOpenWhatsNew } = useSkyplan();
	if (!visible) return null;

	return (
		<div>
			<div data-skyplan-ui
				style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, zIndex: 10, pointerEvents: 'none' }}>
				<DraggablePanel
					persistKey="toolbar"
					title="SkyPlan"
					onClose={onClose}
					headerExtra={
						<button onClick={onOpenWhatsNew} className={`${panelStyles.btn_base} ${panelStyles.btn_right}`}>
							<FontAwesomeIcon icon={faCircleQuestion} className={panelStyles.svg} />
							<span className={panelStyles.tooltip}>What's New</span>
						</button>
					}
				>
					<Toolbar />
				</DraggablePanel>
				{showWhatsNew && <WhatsNewPanel onClose={onCloseWhatsNew} />}
			</div>
			<DrawingCanvas />
			<ServiceCatchmentOverlay />
		</div>
	);
};

const SkyplanOverlay: React.FC = () => (
	<StyleProvider>
		<SkyplanProvider>
			<DrawingProvider>
				<SkyplanOverlayInner />
			</DrawingProvider>
		</SkyplanProvider>
	</StyleProvider>
);

export default SkyplanOverlay;
