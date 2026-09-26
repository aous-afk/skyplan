import React from 'react';
import {FOCUS_DISABLED} from 'cs2/input';
import {useStyle} from '../../StyleContext';
import {Slider} from '../gameComponents';
import shared from '../../shared.module.scss';
import styles from '../StyleManager.module.scss';

const MAX_WIDTH = 20;

const WidthSection: React.FC = () => {
	const { draftStyle, onDraftStyleChange } = useStyle();
	if (!draftStyle) return null;
	const width = parseFloat(draftStyle['stroke-width'] ?? '1') || 0;

	return (
		<div className={styles.section}>
			<span className={styles.section_title}>Width</span>
			<div className={styles.row}>
				<Slider
					focusKey={FOCUS_DISABLED}
					value={width}
					start={0}
					end={MAX_WIDTH}
					onChange={(v: number) => onDraftStyleChange({ 'stroke-width': String(Math.round(v * 2) / 2) })}
					className={shared.opacity_slider}
				/>
				<span className={shared.opacity_value}>{width}</span>
			</div>
		</div>
	);
};

export default WidthSection;
