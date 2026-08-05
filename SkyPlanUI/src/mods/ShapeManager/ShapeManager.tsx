import React, {useMemo} from 'react';
import {FOCUS_DISABLED} from 'cs2/input';
import {getModule} from 'cs2/modding';
import {ShapeData} from 'mods/types';
import shared from '../shared.module.scss';
import styles from './ShapeManager.module.scss';

const Slider = getModule('game-ui/common/input/slider/slider.tsx', 'Slider') as any;
const CheckBox = getModule('game-ui/common/input/toggle/checkbox/checkbox.tsx', 'Checkbox') as any;

interface ShapeManagerProps {
	shapes: ShapeData[];
	globalOpacity: number;
	onGlobalOpacityChange: (v: number) => void;
	layerOpacities: Record<string, number>;
	onLayerOpacityChange: (layerId: string, v: number) => void;
	layerVisible: Record<string, boolean>;
	onLayerVisibleToggle: (layerId: string) => void;
}

const ShapeManager: React.FC<ShapeManagerProps> = ({ shapes, globalOpacity, onGlobalOpacityChange, layerOpacities, onLayerOpacityChange, layerVisible, onLayerVisibleToggle }) => {

	const shapeGroups = useMemo(() => {
		const map = new Map<string, { layerId: string; label: string; color: string; shapesCount: number }>();
		for (const s of shapes) {
			if (!s.layerDef) continue;
			if (!map.has(s.layerId)) {
				const style = s.layerDef.style;
				map.set(s.layerId, {
					layerId: s.layerId,
					label: s.layerDef.label,
					color: (style.stroke ?? style.fill ?? '#888') as string,
					shapesCount: 1,
				});
			} else {
				map.get(s.layerId)!.shapesCount++;
			}
		}
		return Array.from(map.values());
	}, [shapes]);

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
								<span className={styles.shape_count}>{group.shapesCount}</span>
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
						</div>
					))}
				</div>
			)}
		</div>
	);
};

export default ShapeManager;
