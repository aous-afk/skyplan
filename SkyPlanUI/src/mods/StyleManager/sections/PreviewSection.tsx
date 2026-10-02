import React from 'react';
import { useStyle } from '../../StyleContext';
import { toInlineStyle } from '../../utils/style';
import styles from '../StyleManager.module.scss';

const VIEW_W = 250;
const VIEW_H = 100;
const DISPLAY_W = 234;
const DISPLAY_H = Math.round(DISPLAY_W * VIEW_H / VIEW_W);

const PreviewSection: React.FC = () => {
	const { styleTarget, draftStyle, draftLabelStyle } = useStyle();
	if (!draftStyle) return null;
	const inline = toInlineStyle(draftStyle);

	function renderLabelPreview(): React.ReactElement | null {
		return (
			<text x={125} y={60}
				textAnchor="middle"
				dominantBaseline="middle"
				fontSize={draftLabelStyle?.fontSize ?? 15}
				fill={draftLabelStyle?.color ?? '#ffffff'}
				fontWeight={draftLabelStyle?.fontWeight ?? 'normal'}
				opacity={draftLabelStyle?.opacity ?? 1}
				style={{ paintOrder: 'stroke', stroke: 'rgba(0,0,0,0.6)', strokeWidth: 3 }}
			>
				Label
			</text>
		)
	}

	function renderShape(): React.ReactElement | null {
		switch (styleTarget?.tool) {
			case "path":
				return (
					<React.Fragment >
						<path d="M 5 50 L 125 10 L 245 50" style={inline} />
						{renderLabelPreview()}
					</React.Fragment>
				)
			case "polygon":
				return (
					<React.Fragment >
						<polygon points="10,10 230,10 230,60 200,70 200,90 10,90" style={inline} />
						{renderLabelPreview()}
					</React.Fragment>
				)
			case "curve":
				return (

					<React.Fragment >
						<path d="M 20 46 Q 120 -10 220 46" style={inline} />
						{renderLabelPreview()}
					</React.Fragment >
				)
			case "point":
				return (
					<React.Fragment >
						<circle cx="125" cy="20" r="10" style={inline} />
						{renderLabelPreview()}
					</React.Fragment>
				)
			case "text":
				return (
					<React.Fragment >
						{renderLabelPreview()}
					</React.Fragment>
				);
			default:
				return <path d="M 20 46 L 110 18 L 220 40" style={inline} />
		}
	}

	return (
		<div className={styles.section}>
			<span className={styles.section_title}>Preview</span>
			{/* Keyed on the draft so every change remounts the sample: GameFace kept an old inline
			    stroke-dasharray after it changed (1 22 → Solid still drew dashes). */}
			<svg key={JSON.stringify(draftStyle)} className={styles.preview} width={DISPLAY_W} height={DISPLAY_H} viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}>
				{renderShape()}
			</svg>
		</div>
	);
};

export default PreviewSection;
