import React from 'react';
import {FOCUS_DISABLED} from 'cs2/input';
import {useStyle} from '../../StyleContext';
import {ColorField, CheckBox, Slider} from '../gameComponents';
import {hexToRgba, rgbaToHex, Rgba} from '../../utils/style';
import shared from '../../shared.module.scss';
import styles from '../StyleManager.module.scss';

const MIN_FONT = 8;
const MAX_FONT = 32;

// Edits the layer's labelStyle: shape names and descriptions (descriptions derive their size and
// opacity from it).
const LabelSection: React.FC = () => {
	const { draftLabelStyle, onDraftLabelStyleChange } = useStyle();
	if (!draftLabelStyle) return null;
	const bold = draftLabelStyle.fontWeight === 'bold';

	return (
		<div className={styles.section}>
			<span className={styles.section_title}>Label</span>
			<div className={styles.row}>
				<span className={styles.row_label} style={{ color: 'rgba(255,255,255,0.8)' }}>Colour</span>
				<ColorField
					focusKey={FOCUS_DISABLED}
					value={hexToRgba(draftLabelStyle.color, draftLabelStyle.opacity)}
					alpha
					hexInput
					onChange={(c: Rgba) => onDraftLabelStyleChange({ color: rgbaToHex(c), opacity: Math.round(c.a * 100) / 100 })}
					className={styles.color_field}
				/>
			</div>
			<div className={styles.row}>
				<span className={styles.row_label} style={{ color: 'rgba(255,255,255,0.8)' }}>Size</span>
				<Slider
					focusKey={FOCUS_DISABLED}
					value={draftLabelStyle.fontSize}
					start={MIN_FONT}
					end={MAX_FONT}
					onChange={(v: number) => onDraftLabelStyleChange({ fontSize: Math.round(v) })}
					className={shared.opacity_slider}
				/>
				<span className={shared.opacity_value}>{draftLabelStyle.fontSize}</span>
			</div>
			<label className={styles.row}>
				<CheckBox
					focusKey={FOCUS_DISABLED}
					checked={bold}
					onChange={() => onDraftLabelStyleChange({ fontWeight: bold ? 'normal' : 'bold' })}
				/>
				<span className={styles.row_label} style={{ color: 'rgba(255,255,255,0.8)' }}>Bold</span>
			</label>
		</div>
	);
};

export default LabelSection;
