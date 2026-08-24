import React, {useMemo} from 'react';
import {useValue} from 'cs2/api';
import {catchment$} from '../bindings';

interface Pt { x: number; y: number }
interface Catchment {
	name: string;
	capacity: number;
	enrolled: number;
	facilityPos: Pt | null;
}

// Scaffold: shows the legend for a clicked school/hospital (name, capacity, over-capacity
// flag). The home/facility markers themselves are drawn world-space via the game's own
// OverlayRenderSystem (see ServiceCatchmentSystem.cs) - this overlay only needs a single
// screen anchor point for the legend box, no per-shape reprojection.
const ServiceCatchmentOverlay: React.FC = () => {
	const json = useValue(catchment$);

	const data = useMemo<Catchment | null>(() => {
		try { return json ? JSON.parse(json) : null; }
		catch { return null; }
	}, [json]);

	if (!data || !data.facilityPos) return null;

	const over = data.capacity > 0 && data.enrolled > data.capacity;

	return (
		<svg
			style={{ position: 'absolute', top: 0, left: 0, width: '100vw', height: '100vh', pointerEvents: 'none', overflow: 'hidden' }}
		>
			<g transform={`translate(${data.facilityPos.x + 16}, ${data.facilityPos.y - 16})`}>
				<rect x={0} y={-14} width={170} height={54} rx={6} fill="rgba(0,0,0,0.7)" />
				<text x={10} y={2} fill="#facc15" fontSize={13} fontWeight="bold">{data.name}</text>
				<text x={10} y={20} fill={over ? '#ff5555' : '#ffffff'} fontSize={12}>
					{data.enrolled} / {data.capacity}
				</text>
				<text x={10} y={36} fill="rgba(255,255,255,0.6)" fontSize={11}>
					{over ? 'over capacity — consider a new one' : 'within capacity'}
				</text>
			</g>
		</svg>
	);
};

export default ServiceCatchmentOverlay;
