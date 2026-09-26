import React from 'react';
import {FOCUS_DISABLED} from 'cs2/input';
import {useStyle} from '../../StyleContext';
import {ColorField, CheckBox} from '../gameComponents';
import {hexToRgba, rgbaToHex, parseOpacity, formatOpacity, Rgba} from '../../utils/style';
import styles from '../StyleManager.module.scss';

const FALLBACK_FILL = '#888888';

const ColorSection: React.FC = () => {
	const { layerById, styleTarget, draftStyle, onDraftStyleChange } = useStyle();
	if (!draftStyle || !styleTarget) return null;
	const fillNone = !draftStyle.fill || draftStyle.fill === 'none';

	// Turning fill back on restores the layer's own fill when it has one.
	const onFillNoneToggle = () => {
		if (!fillNone) {
			onDraftStyleChange({ fill: 'none' });
			return;
		}
		const layerFill = layerById[styleTarget.layerId]?.style.fill;
		onDraftStyleChange({ fill: layerFill !== undefined && layerFill !== 'none' ? String(layerFill) : FALLBACK_FILL });
	};

	return (
		<div className={styles.section}>
			<span className={styles.section_title} >Colour</span>
			<div className={styles.row}>
				<span className={styles.row_label} style={{ color: 'rgba(255,255,255,0.8)' }}>Line</span>
				<ColorField
					focusKey={FOCUS_DISABLED}
					value={hexToRgba(draftStyle.stroke, parseOpacity(draftStyle['stroke-opacity']))}
					alpha
					hexInput
					onChange={(c: Rgba) => onDraftStyleChange({ stroke: rgbaToHex(c), 'stroke-opacity': formatOpacity(c.a) })}
					className={styles.color_field}
				/>
			</div>
			<div className={styles.row}>
				<span className={styles.row_label} style={{ color: 'rgba(255,255,255,0.8)' }}>Fill</span>
				<ColorField
					focusKey={FOCUS_DISABLED}
					disabled={fillNone}
					value={hexToRgba(fillNone ? FALLBACK_FILL : draftStyle.fill, parseOpacity(draftStyle['fill-opacity']))}
					alpha
					hexInput
					onChange={(c: Rgba) => onDraftStyleChange({ fill: rgbaToHex(c), 'fill-opacity': formatOpacity(c.a) })}
					className={styles.color_field}
				/>
			</div>
			<label className={styles.row}>
				<CheckBox focusKey={FOCUS_DISABLED} checked={fillNone} onChange={onFillNoneToggle} />
				<span className={styles.row_label} style={{ color: 'rgba(255,255,255,0.8)' }}>No fill</span>
			</label>
		</div>
	);
};

export default ColorSection;
