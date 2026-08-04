import React from 'react';
import {SkyplanProvider, useSkyplan} from './SkyplanContext';
import Toolbar from './Toolbar/Toolbar';
import DrawingCanvas from './DrawingCanvas/DrawingCanvas';

const SkyplanOverlayInner: React.FC = () => {
	const { visible } = useSkyplan();
	if (!visible) return null;

	return (
		<div>
			<div data-skyplan-ui
				style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, zIndex: 9000, pointerEvents: 'none' }}>
				<Toolbar />
			</div>
			<DrawingCanvas />
		</div>
	);
};

const SkyplanOverlay: React.FC = () => (
	<SkyplanProvider>
		<SkyplanOverlayInner />
	</SkyplanProvider>
);

export default SkyplanOverlay;
