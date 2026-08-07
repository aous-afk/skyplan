import React, {useMemo, useState, useCallback} from 'react';
import {trigger} from 'cs2/api';
import {FOCUS_DISABLED} from 'cs2/input';
import {getModule} from 'cs2/modding';
import {faDrawPolygon, faEye, faEyeSlash, faFont, faLocationDot, faRoad} from '@fortawesome/free-solid-svg-icons';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome';
import {ShapeData, Tag} from 'mods/types';
import {useDrawingContext} from 'mods/DrawingContext';
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
	const { onHoverShape } = useDrawingContext();
	const [editingShapeId, setEditingShapeId] = useState<string | null>(null);
	const [editName, setEditName] = useState('');
	const [editNote, setEditNote] = useState('');

	const shapeGroups = useMemo(() => {
		const map = new Map<string, { layerId: string; label: string; color: string; shapes: ShapeData[] }>();
		for (const s of shapes) {
			if (!s.layerDef) continue;
			if (!map.has(s.layerId)) {
				const style = s.layerDef.style;
				map.set(s.layerId, {
					layerId: s.layerId,
					label: s.layerDef.label,
					color: (style.stroke ?? style.fill ?? '#888') as string,
					shapes: [],
				});
			}
			map.get(s.layerId)!.shapes.push(s);
		}
		return Array.from(map.values());
	}, [shapes]);

	const startEdit = useCallback((s: ShapeData) => {
		setEditingShapeId(s.id);
		setEditName(s.label ?? '');
		setEditNote(s.description ?? '');
	}, []);

	const commitName = useCallback((shapeId: string, value: string) => {
		trigger('skyplan', 'setShapeLabel', `${shapeId}|${value}`);
		setEditingShapeId(null);
	}, []);

	const commitNote = useCallback((shapeId: string, value: string) => {
		trigger('skyplan', 'setShapeNote', `${shapeId}|${value}`);
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
					{shapeGroups.map(group => (
						<div key={group.layerId} className={styles.layer_card}>
							<div className={styles.shape_group_header}>
								<span className={styles.shape_dot} style={{ background: group.color }} />
								<span className={styles.shape_group_label}>{group.label}</span>
								<span className={styles.shape_count}>{group.shapes.length}</span>
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
								{group.shapes.map((s, i) => {
									const isEditing = editingShapeId === s.id;
									const fallback = `${s.tag === Tag.text ? 'Text' : s.tag === Tag.circle ? 'Point' : s.tag === Tag.polygon ? 'Area' : 'Line'} ${i + 1}`;
									return (
										<div key={s.id} className={styles.shape_row}>
											<div
												className={styles.shape_row_header}
												onClick={() => isEditing ? setEditingShapeId(null) : startEdit(s)}
												onMouseEnter={() => onHoverShape(s.id)}
												onMouseLeave={() => onHoverShape(null)}
											>
												<FontAwesomeIcon icon={tagIcon(s.tag)} className={styles.shape_row_icon} />
												<span className={styles.shape_row_name} style={{ color: 'rgba(255,255,255,0.8)' }}>{s.label || fallback}</span>
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
															if (e.key === 'Enter') commitName(s.id, editName);
															if (e.key === 'Escape') setEditingShapeId(null);
														}}
														onBlur={() => commitName(s.id, editName)}
													/>
													<textarea
														className={styles.shape_textarea}
														style={{ width: '100%', boxSizing: 'border-box' }}
														value={editNote}
														placeholder="Description…"
														rows={3}
														onChange={e => setEditNote(e.currentTarget.value)}
														onKeyDown={e => e.stopPropagation()}
														onBlur={() => commitNote(s.id, editNote)}
													/>
												</div>
											)}
										</div>
									);
								})}
							</div>
						</div>
					))}
				</div>
			)}
		</div>
	);
};

export default ShapeManager;
