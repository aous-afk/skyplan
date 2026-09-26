import React from 'react';
import {FOCUS_DISABLED} from 'cs2/input';
import {useStyle} from '../../StyleContext';
import {CheckBox} from '../gameComponents';
import styles from '../StyleManager.module.scss';

const DASH_PRESETS: { label: string; patch: Record<string, string> }[] = [
	{ label: 'Solid', patch: { 'stroke-dasharray': 'none' } },
	{ label: 'Dashed', patch: { 'stroke-dasharray': '8 4' } },
	// A zero-length dash only shows as a dot with round caps.
	{ label: 'Dots', patch: { 'stroke-dasharray': '0 8', 'stroke-linecap': 'round' } },
];

const DashSection: React.FC = () => {
	const { draftStyle, onDraftStyleChange } = useStyle();
	if (!draftStyle) return null;
	const dash = draftStyle['stroke-dasharray'];
	const round = draftStyle['stroke-linecap'] === 'round';

	return (
		<div className={styles.section}>
			<span className={styles.section_title} style={{ color: 'rgba(255,255,255,0.8)' }}>Dash</span>
			<div className={styles.row}>
				<input
					className={styles.text_input}
					value={dash && dash !== 'none' ? dash : ''}
					placeholder="e.g. 8 4"
					onChange={e => onDraftStyleChange({ 'stroke-dasharray': e.currentTarget.value.trim() || 'none' })}
					onKeyDown={e => e.stopPropagation()}
				/>
			</div>
			<div className={styles.row}>
				{DASH_PRESETS.map(p => (
					<button key={p.label} className={styles.preset} onClick={() => onDraftStyleChange(p.patch)}>
						<span style={{ color: 'rgba(255,255,255,0.8)' }}>{p.label}</span>
					</button>
				))}
			</div>
			<label className={styles.row}>
				<CheckBox
					focusKey={FOCUS_DISABLED}
					checked={round}
					onChange={() => onDraftStyleChange({ 'stroke-linecap': round ? 'butt' : 'round' })}
				/>
				<span className={styles.row_label} style={{ color: 'rgba(255,255,255,0.8)' }}>Round ends</span>
			</label>
		</div>
	);
};

export default DashSection;
