import React, {useEffect} from 'react';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome';
import {faCircleQuestion} from '@fortawesome/free-solid-svg-icons';
import {getModule} from 'cs2/modding';
import {StyleProvider, useStyle} from './StyleContext';
import {SkyplanProvider, useSkyplan} from './SkyplanContext';
import {DrawingProvider} from './DrawingContext';
import Toolbar from './Toolbar/Toolbar';
import DraggablePanel from './DraggablePanel/DraggablePanel';
import panelStyles from './DraggablePanel/DraggablePanel.module.scss';
import DrawingCanvas from './DrawingCanvas/DrawingCanvas';
import ServiceCatchmentOverlay from './ServiceCatchmentOverlay';
import WhatsNewPanel from './WhatsNew/WhatsNewPanel';
import StyleManager from './StyleManager/StyleManager';

const PortalContainerProvider = getModule('game-ui/common/portal/portal.tsx', 'PortalContainerProvider') as any;

const SkyplanOverlayInner: React.FC = () => {
	const { visible, showWhatsNew, onCloseWhatsNew, onClose, onOpenWhatsNew } = useSkyplan();
	const { styleTarget, onStyleTarget } = useStyle();

	// The providers outlive the panel, so an open Style Manager would otherwise reappear with its
	// old draft on the next Alt+P.
	useEffect(() => {
		if (!visible) onStyleTarget(null);
	}, [visible, onStyleTarget]);

	if (!visible) return null;

	return (
		<div>
			<div data-skyplan-ui
				style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, zIndex: 10, pointerEvents: 'none' }}>
				{/* Game popups (e.g. ColorField's picker) portal to document.body by default, which sits
				    under this fixed overlay. The provider makes the div below their container instead:
				    popups append after the panels (so on top), stay inside data-skyplan-ui (so clicks
				    don't draw), and position against the full-screen overlay. The div only holds
				    absolutely positioned panels, so it's zero-height and its 'auto' doesn't block the
				    map; it's needed because popups inherit pointer-events and the overlay is 'none'. */}
				<PortalContainerProvider>
					<div style={{ pointerEvents: 'auto' }}>
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
						{styleTarget && <StyleManager />}
					</div>
				</PortalContainerProvider>
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
