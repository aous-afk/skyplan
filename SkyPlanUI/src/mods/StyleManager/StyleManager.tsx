import React, { useEffect, useState } from 'react';
import DraggablePanel from '../DraggablePanel/DraggablePanel';
import { useStyle } from '../StyleContext';
import { useSkyplan } from '../SkyplanContext';
import { ToolId } from '../types';
import PreviewSection from './sections/PreviewSection';
import WidthSection from './sections/WidthSection';
import DashSection from './sections/DashSection';
import ColorSection from './sections/ColorSection';
import LabelSection from './sections/LabelSection';
import styles from './StyleManager.module.scss';
import shared from '../shared.module.scss';

const Separator: React.FC = () => <div className={styles.separator} />;

// Tools whose shapes have a stroke worth dashing.
const DASH_TOOLS: ToolId[] = ['path', 'curve', 'polygon'];
// Text shapes only use the label style, so the shape-style sections (width, colour) don't apply.
const SHAPE_STYLE_TOOLS: ToolId[] = ['path', 'curve', 'polygon', 'point'];

// Container width (270) plus the panel's own padding/border; only used to place it at the right edge.
const PANEL_WIDTH = 300;
// Body is 270 wide; minus header padding (18) and the close button (~37), with some slack.
const TITLE_MAX_WIDTH = 200;
const EDGE_MARGIN = 12;

// "Train" and "Train copy" both give "Train copy", then "Train copy 2", ...
function copyLabel(label: string, existing: string[]): string {
	const base = label.replace(/ copy( \d+)?$/i, '');
	const taken = new Set(existing.map(l => l.toLowerCase()));
	let name = `${base} copy`;
	for (let n = 2; taken.has(name.toLowerCase()); n++) name = `${base} copy ${n}`;
	return name;
}

const StyleManager: React.FC = () => {
	const { allLayers, layerById, styleTarget, onStyleTarget, onResetDraft, onSave, onSaveAs } = useStyle();
	const { primaryLayer, activeTool, onLayerSelect } = useSkyplan();
	const [pendingLayerId, setPendingLayerId] = useState<string | null>(null);

	// Save as: the new layer exists only after C# reloads the config.
	useEffect(() => {
		if (!pendingLayerId || !layerById[pendingLayerId]) return;
		onLayerSelect(layerById[pendingLayerId]);
		setPendingLayerId(null);
	}, [pendingLayerId, layerById, onLayerSelect]);

	// Follows the toolbar only when its layer or tool changes. styleTarget is deliberately not a dependency:
	// opening the panel from a Shape Manager layer card must keep that layer, not snap to the toolbar's.
	const primaryLayerId = primaryLayer?.id;
	useEffect(() => {
		if (!styleTarget || !primaryLayerId || !activeTool || activeTool === 'erase') return;
		if (primaryLayerId === styleTarget.layerId && activeTool === styleTarget.tool) return;
		onStyleTarget({ layerId: primaryLayerId, tool: activeTool });
	}, [primaryLayerId, activeTool]);

	// null: the name input is closed.
	const [saveAsName, setSaveAsName] = useState<string | null>(null);
	useEffect(() => setSaveAsName(null), [styleTarget?.layerId, styleTarget?.tool]);

	if (!styleTarget) return null;
	const layer = layerById[styleTarget.layerId];
	const layerLabel = layer?.label ?? styleTarget.layerId;
	const title = `Style: ${layerLabel}`;

	const openSaveAs = () => setSaveAsName(copyLabel(layerLabel, allLayers.map(l => l.label)));

	const confirmSaveAs = () => {
		if (!saveAsName?.trim()) return;
		const newId = onSaveAs(saveAsName);
		if (newId) setPendingLayerId(newId);
		setSaveAsName(null);
	};

	return (
		<DraggablePanel
			persistKey="style-manager"
			title={title}
			titleMaxWidth={TITLE_MAX_WIDTH}
			onClose={() => onStyleTarget(null)}
			defaultPosition={{ left: Math.max(EDGE_MARGIN, window.innerWidth - PANEL_WIDTH - EDGE_MARGIN), top: EDGE_MARGIN }}
		>
			<div className={styles.container}>
				<PreviewSection />
				<Separator />
				{SHAPE_STYLE_TOOLS.includes(styleTarget.tool) && <>
					<WidthSection />
					<Separator />
				</>}
				{DASH_TOOLS.includes(styleTarget.tool) && <>
					<DashSection />
					<Separator />
				</>}
				{SHAPE_STYLE_TOOLS.includes(styleTarget.tool) && <>
					<ColorSection />
					<Separator />
				</>}
				<LabelSection />
				<Separator />
				{saveAsName !== null && (
					<div className={styles.section}>
						<div className={styles.name_row}>
							<input
								className={styles.name_input}
								value={saveAsName}
								placeholder="Layer name…"
								autoFocus
								onChange={e => setSaveAsName(e.currentTarget.value)}
								onKeyDown={e => {
									// Keeps typing out of the game's hotkeys.
									e.stopPropagation();
									if (e.key === 'Enter') confirmSaveAs();
									if (e.key === 'Escape') setSaveAsName(null);
								}}
							/>
							<div className={styles.name_buttons}>
								<button onClick={confirmSaveAs} disabled={!saveAsName.trim()} className={`${shared.btn_base} ${styles.button_ok}`}>OK</button>
								<button onClick={() => setSaveAsName(null)} className={`${shared.btn_base} ${styles.button_danger}`}>Cancel</button>
							</div>
						</div>
					</div>
				)}
				<div className={styles.footer}>
					<div className={styles.footer_group}>
						<button onClick={onSave} className={shared.btn_base}>Save</button>
						<button onClick={openSaveAs} className={shared.btn_base}>Save as</button>
					</div>
					<button className={`${styles.preset} ${styles.button_danger}`} onClick={onResetDraft}>Reset</button>
				</div>
			</div>
		</DraggablePanel>
	);
};

export default StyleManager;
