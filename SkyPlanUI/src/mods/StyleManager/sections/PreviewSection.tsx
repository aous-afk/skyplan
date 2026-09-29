import React from 'react';
import {useStyle} from '../../StyleContext';
import {toInlineStyle} from '../../utils/style';
import styles from '../StyleManager.module.scss';

// A sample line, area and label drawn with the draft, so the result shows even when the target
// shape is off screen.
const PreviewSection: React.FC = () => {
	const { draftStyle, draftLabelStyle } = useStyle();
	if (!draftStyle) return null;
	const inline = toInlineStyle(draftStyle);

	return (
		<div className={styles.section}>
			<span className={styles.section_title}>Preview</span>
			{/* Keyed on the draft so every change remounts the sample: GameFace kept an old inline
			    stroke-dasharray after it changed (1 22 → Solid still drew dashes). */}
			<svg key={JSON.stringify(draftStyle)} className={styles.preview} width={240} height={64} viewBox="0 0 240 64">
				<path d="M 14 44 L 110 20" style={inline} />
				<rect x={140} y={14} width={80} height={36} />
				{draftLabelStyle && (
					<text x={180} y={32} textAnchor="middle" dominantBaseline="middle"
						fontSize={draftLabelStyle.fontSize} fill={draftLabelStyle.color}
						fontWeight={draftLabelStyle.fontWeight} opacity={draftLabelStyle.opacity}
						style={{ paintOrder: 'stroke', stroke: 'rgba(0,0,0,0.6)', strokeWidth: 3 }}
					>
						Label
					</text>
				)}
			</svg>
		</div>
	);
};

export default PreviewSection;
