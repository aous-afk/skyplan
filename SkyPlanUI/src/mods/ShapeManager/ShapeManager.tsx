import React, {useMemo, useState, useCallback} from 'react';
import {trigger} from 'cs2/api';
import {FOCUS_DISABLED} from 'cs2/input';
import {getModule} from 'cs2/modding';
import {faChevronDown, faChevronRight, faDrawPolygon, faEye, faEyeSlash, faFont, faLink, faLocationDot, faRoad} from '@fortawesome/free-solid-svg-icons';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome';
import {ShapeData, Tag} from 'mods/types';
import {useDrawingContext} from 'mods/DrawingContext';
import {useSkyplan} from 'mods/SkyplanContext';
import shared from '../shared.module.scss';
import styles from './ShapeManager.module.scss';

const Slider = getModule('game-ui/common/input/slider/slider.tsx', 'Slider') as any;
const CheckBox = getModule('game-ui/common/input/toggle/checkbox/checkbox.tsx', 'Checkbox') as any;

function tagIcon(tag: Tag) {
	switch (tag) {
		case Tag.path:    return faRoad;
		case Tag.polygon: return faDrawPolygon;
		case Tag.circle:  return faLocationDot;
		case Tag.text:    return faFont;
		default:          return faRoad;
	}
}

// A parallel-lane clone isn't its own ShapeData - it's an entry on the source shape's own
// parallelLanes list, targeting a different layer's group. It shares the source shape's geometry (so
// hover-highlight still targets sourceShape.id) but has its OWN independent label/description (the
// `lane` object), edited via setLaneLabel/setLaneNote rather than setShapeLabel/setShapeNote.
type Lane = NonNullable<ShapeData['parallelLanes']>[number];
type ShapeRow =
	| { kind: 'real'; shape: ShapeData }
	| { kind: 'clone'; sourceShape: ShapeData; lane: Lane };

interface ShapeManagerProps {
	shapes: ShapeData[];
	globalOpacity: number;
	onGlobalOpacityChange: (v: number) => void;
	layerOpacities: Record<string, number>;
	onLayerOpacityChange: (layerId: string, v: number) => void;
	layerVisible: Record<string, boolean>;
	onLayerVisibleToggle: (layerId: string) => void;
	layerLabels: Record<string, boolean>;
	onLayerLabelsToggle: (layerId: string) => void;
}

const ShapeManager: React.FC<ShapeManagerProps> = ({
	shapes, globalOpacity, onGlobalOpacityChange,
	layerOpacities, onLayerOpacityChange,
	layerVisible, onLayerVisibleToggle,
	layerLabels, onLayerLabelsToggle,
}) => {
	const { onHoverShape, showDescriptions, onShowDescriptionsToggle } = useDrawingContext();
	const { allLayers } = useSkyplan();
	const layerDefsMap = useMemo(() =>
		Object.fromEntries(allLayers.map(l => [l.id, l])),
		[allLayers]
	);
	// Keyed by rowKey (see below), not shape id - a clone row edits independently of its source
	// shape's own row and of any other clone on the same source, even though they share geometry.
	const [editingRowKey, setEditingRowKey] = useState<string | null>(null);
	const [editName, setEditName] = useState('');
	const [editNote, setEditNote] = useState('');
	const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

	const toggleCollapsed = useCallback((layerId: string) => {
		setCollapsed(prev => ({ ...prev, [layerId]: !(prev[layerId] ?? true) }));
	}, []);

	const shapeGroups = useMemo(() => {
		const map = new Map<string, { layerId: string; label: string; color: string; rows: ShapeRow[] }>();
		const ensure = (layerId: string) => {
			let group = map.get(layerId);
			if (group) return group;
			const layerDef = layerDefsMap[layerId];
			if (!layerDef) return null;
			group = { layerId, label: layerDef.label, color: (layerDef.style.stroke ?? layerDef.style.fill ?? '#888') as string, rows: [] };
			map.set(layerId, group);
			return group;
		};
		for (const s of shapes) {
			ensure(s.layerId)?.rows.push({ kind: 'real', shape: s });
			// A clone may target a layer with zero real shapes of its own (e.g. queued but nothing
			// drawn as that layer yet) - still needs its own row/group entry.
			s.parallelLanes?.forEach(lane => {
				ensure(lane.layerId)?.rows.push({ kind: 'clone', sourceShape: s, lane });
			});
		}
		return Array.from(map.values());
	}, [shapes, layerDefsMap]);

	const rowKey = (row: ShapeRow) => row.kind === 'real' ? row.shape.id : `${row.sourceShape.id}-clone-${row.lane.layerId}`;

	const startEdit = useCallback((row: ShapeRow) => {
		setEditingRowKey(rowKey(row));
		setEditName((row.kind === 'real' ? row.shape.label : row.lane.label) ?? '');
		setEditNote((row.kind === 'real' ? row.shape.description : row.lane.description) ?? '');
	}, []);

	const saveName = useCallback((row: ShapeRow, value: string) => {
		if (row.kind === 'real') trigger('skyplan', 'setShapeLabel', `${row.shape.id}|${value}`);
		else trigger('skyplan', 'setLaneLabel', `${row.sourceShape.id}|${row.lane.layerId}|${value}`);
	}, []);

	const commitNote = useCallback((row: ShapeRow, value: string) => {
		if (row.kind === 'real') trigger('skyplan', 'setShapeNote', `${row.shape.id}|${value}`);
		else trigger('skyplan', 'setLaneNote', `${row.sourceShape.id}|${row.lane.layerId}|${value}`);
	}, []);

	return (
		<div className={styles.container}>
			<div className={shared.opacity_row}>
				<span className={shared.opacity_label}>All Layers</span>
				<Slider
					focusKey={FOCUS_DISABLED}
					value={globalOpacity}
					start={0}
					end={1}
					onChange={onGlobalOpacityChange}
					className={shared.opacity_slider}
				/>
				<span className={shared.opacity_value}>{Math.round(globalOpacity * 100)}%</span>
			</div>

			{shapeGroups.length > 0 && (
				<div className={styles.layer_list} onWheel={e => e.stopPropagation()}>
					{shapeGroups.map(group => {
						const isCollapsed = collapsed[group.layerId] ?? true;
						return (
						<div key={group.layerId} className={styles.layer_card}>
							<div className={styles.shape_group_header}>
								<button
									className={styles.collapse_toggle}
									onClick={() => toggleCollapsed(group.layerId)}
									title={isCollapsed ? 'Expand' : 'Collapse'}
								>
									<FontAwesomeIcon icon={isCollapsed ? faChevronRight : faChevronDown} className={shared.svg} />
								</button>
								<span className={styles.shape_dot} style={{ background: group.color }} />
								<span className={styles.shape_group_label}>{group.label}</span>
								<span className={styles.shape_count}>{group.rows.length}</span>
								<button
									className={styles.labels_toggle}
									onClick={() => onLayerLabelsToggle(group.layerId)}
									title={layerLabels[group.layerId] ? 'Hide labels' : 'Show labels'}
								>
									<FontAwesomeIcon icon={layerLabels[group.layerId] ? faEye : faEyeSlash} className={shared.svg} />
								</button>
								<CheckBox
									focusKey={FOCUS_DISABLED}
									checked={layerVisible[group.layerId] ?? true}
									onChange={() => onLayerVisibleToggle(group.layerId)}
								/>
							</div>

							{!isCollapsed && <>
							<div className={shared.opacity_row}>
								<span className={shared.opacity_label}>Opacity</span>
								<Slider
									focusKey={FOCUS_DISABLED}
									value={layerOpacities[group.layerId] ?? 1}
									start={0}
									end={1}
									onChange={(v: number) => onLayerOpacityChange(group.layerId, v)}
									className={shared.opacity_slider}
								/>
								<span className={shared.opacity_value}>{Math.round((layerOpacities[group.layerId] ?? 1) * 100)}%</span>
							</div>

							<div className={styles.shape_row_list}>
								{group.rows.map((row, i) => {
									const s = row.kind === 'real' ? row.shape : row.sourceShape;
									const key = rowKey(row);
									const isEditing = editingRowKey === key;
									const ownLabel = row.kind === 'real' ? row.shape.label : row.lane.label;
									const fallback = `${s.tag === Tag.text ? 'Text' : s.tag === Tag.circle ? 'Point' : s.tag === Tag.polygon ? 'Area' : 'Line'} ${i + 1}`;
									return (
										<div key={key} className={styles.shape_row}>
											<div
												className={styles.shape_row_header}
												onClick={() => isEditing ? setEditingRowKey(null) : startEdit(row)}
												onMouseEnter={() => onHoverShape(s.id)}
												onMouseLeave={() => onHoverShape(null)}
											>
												<FontAwesomeIcon icon={tagIcon(s.tag)} className={styles.shape_row_icon} />
												<span className={styles.shape_row_name} style={{ color: 'rgba(255,255,255,0.8)' }}>{ownLabel || fallback}</span>
												{row.kind === 'clone' && (
													<FontAwesomeIcon icon={faLink} className={styles.shape_row_clone_icon} title="Same line as another layer" />
												)}
											</div>
											{isEditing && (
												<div className={styles.shape_row_edit}>
													<input
														className={styles.shape_input}
														style={{ width: '100%', boxSizing: 'border-box' }}
														value={editName}
														placeholder="Name…"
														onChange={e => setEditName(e.currentTarget.value)}
														onKeyDown={e => {
															e.stopPropagation();
															if (e.key === 'Enter') { saveName(row, editName); setEditingRowKey(null); }
															if (e.key === 'Escape') setEditingRowKey(null);
														}}
														onBlur={() => saveName(row, editName)}
													/>
													<textarea
														className={styles.shape_textarea}
														style={{ width: '100%', boxSizing: 'border-box' }}
														value={editNote}
														placeholder="Description…"
														rows={3}
														onChange={e => setEditNote(e.currentTarget.value)}
														onKeyDown={e => e.stopPropagation()}
														onBlur={() => commitNote(row, editNote)}
													/>
												</div>
											)}
										</div>
									);
								})}
							</div>
							</>}
						</div>
						);
					})}
				</div>
			)}

			<div className={styles.display_section}>
				<span className={styles.display_section_title}>Display</span>
				<label className={styles.display_row}>
					<CheckBox
						focusKey={FOCUS_DISABLED}
						checked={showDescriptions}
						onChange={onShowDescriptionsToggle}
					/>
					<span className={styles.display_row_label} style={{ color: 'rgba(255,255,255,0.75)'}}>Show descriptions</span>
				</label>
			</div>
		</div>
	);
};

export default ShapeManager;
