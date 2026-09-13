import React, {useEffect, useState} from "react";
import {trigger} from 'cs2/api';
import {TOOLS, Tag} from '../types';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome'
import {faUndo, faRedo, faMagnet} from '@fortawesome/free-solid-svg-icons'
import styles from './Toolbar.module.scss';
import {useSkyplan} from '../SkyplanContext';
import {useDrawingContext} from "mods/DrawingContext";
import ShapeManager from "mods/ShapeManager/ShapeManager";

const Toolbar: React.FC = () => {
	const {
		activeTool, activeLayers, primaryLayer, visibleLayers, viewMode,
		onViewModeToggle, onToolChange, onLayerAdd, onLayerRemove, onLayerSelect,
		onUndo, onRedo, onClear, onClearAll,
	} = useSkyplan();

	// Only tools with allowMultiSelect (TOOLS in types.ts) queue multiple layers - everything else
	// (polygon, text, erase) stays single-select: clicking a layer replaces the queue outright.
	const multiSelectAllowed = TOOLS.find(t => t.id === activeTool)?.allowMultiSelect ?? false;

	const {
	  shapes,
	  globalOpacity,
	  onGlobalOpacityChange,
	  layerOpacities,
	  onLayerOpacityChange,
	  layerVisible,
	  onLayerVisibleToggle,
	  layerLabels,
	  onLayerLabelsToggle,
	  snapEnabled,
	  onSnapEnabledToggle,
	} = useDrawingContext();

	const [pendingTextId, setPendingTextId] = useState<string | null>(null);

	useEffect(() => {
		if (activeTool !== 'text') {
			setPendingTextId(null);
			return;
		}
		const unlabeled = shapes.find(s => s.tag === Tag.text && !s.label);
		setPendingTextId(unlabeled?.id ?? null);
	}, [shapes, activeTool]);

	const commitText = (id: string, text: string) => {
		trigger('skyplan', 'commitText', `${id}|${text}`);
		setPendingTextId(null);
	};

	// React's synthetic onMouseDown/onContextMenu never fire for the right mouse button in this
	// environment (Coherent's React integration filters non-primary-button events before they
	// reach component handlers) - confirmed via raw document.addEventListener seeing the event
	// fine at both capture and bubble phase while React-attached handlers on the same button never
	// ran. Same reason DrawingCanvas.tsx never uses React's mouse props either - raw listener here
	// instead, matching that proven pattern.
	useEffect(() => {
		const handler = (e: MouseEvent) => {
			if (e.button !== 2) return;
			const target = e.target as Element;
			if (target.closest('[data-tool-btn]')) {
				e.preventDefault();
				onToolChange(null);
				// Switching tools invalidates the queued layers (they may not even apply to the new
				// tool) - fully clear the whole queue, not just decrement one.
				for (const entry of activeLayers) {
					for (let i = 0; i < entry.count; i++) onLayerRemove(entry.layer);
				}
				return;
			}
			const layerBtn = target.closest('[data-layer-btn]') as HTMLElement | null;
			if (layerBtn) {
				e.preventDefault();
				const layer = visibleLayers.find(l => l.id === layerBtn.dataset.layerId);
				if (layer) onLayerRemove(layer);
			}
		};
		document.addEventListener('mousedown', handler, true);
		return () => document.removeEventListener('mousedown', handler, true);
	}, [onToolChange, onLayerRemove, activeLayers, visibleLayers]);

	return (
		<>
			<div className={styles.actions_container}>
				<button onClick={onUndo} className={styles.btn_base}>
					<FontAwesomeIcon icon={faUndo} className={styles.svg} />
				</button>
				<button onClick={onRedo} className={styles.btn_base}>
					<FontAwesomeIcon icon={faRedo} className={styles.svg} />
				</button>
				<label className={styles.mode_toggle} onClick={() => { if (pendingTextId) commitText(pendingTextId, ''); onViewModeToggle(); }}>
					<span className={styles.toggle_track} style={{ background: viewMode ? 'rgba(255,255,255,0.15)' : '#4a90d9' }}>
						<span className={styles.toggle_knob} style={{ transform: viewMode ? 'translateX(0)' : 'translateX(18px)' }} />
					</span>
					<span className={styles.toggle_label} style={{ color: viewMode ? 'rgba(255,255,255,0.5)' : '#4a90d9' }}>{viewMode ? ' View' : ' Draw'}</span>
				</label>
			</div>


			{viewMode && <ShapeManager
			  shapes={shapes}
			  globalOpacity={globalOpacity}
			  onGlobalOpacityChange={onGlobalOpacityChange}
			  layerOpacities={layerOpacities}
			  onLayerOpacityChange={onLayerOpacityChange}
			  layerVisible={layerVisible}
			  onLayerVisibleToggle={onLayerVisibleToggle}
			  layerLabels={layerLabels}
			  onLayerLabelsToggle={onLayerLabelsToggle}
			  />}
			{!viewMode && <div className={styles.body}>
				<div className={styles.tools_column}>
					{TOOLS.map(t => {
						const active = activeTool === t.id;
						return <button key={t.id}
							data-tool-btn
							onClick={() => onToolChange(t.id)}
							className={`${styles.btn_base} ${active ? styles.btn_active : ''}`}
							style={{
								border: active && primaryLayer ? `2px solid ${primaryLayer.style.stroke}` : '2px solid transparent',
							}}
						>
							<FontAwesomeIcon className={`${styles.svg} ${active ? styles.svg_active : ''}`} icon={t.icon} />
							<span className={styles.tooltip}>{t.label}</span>
						</button>;
					})}
					<button onClick={onSnapEnabledToggle}
						className={`${styles.btn_base} ${snapEnabled ? styles.btn_active : ''}`}
						style={{ marginTop: 10, borderTop: '1px solid rgba(255,255,255,0.08)', paddingTop: 10 }}
					>
						<FontAwesomeIcon className={`${styles.svg} ${snapEnabled ? styles.svg_active : ''}`} icon={faMagnet} />
						<span className={styles.tooltip}>Snap {snapEnabled ? 'on' : 'off'}</span>
					</button>
				</div>

				<div className={styles.layers_panel}>
					{activeTool === 'text' && pendingTextId ? (
						<div className={styles.text_input_row}>
							<span className={styles.text_input_label}>Annotation text</span>
							<input
								autoFocus
								className={styles.text_input}
								style={{ width: '100%', boxSizing: 'border-box' }}
								placeholder="Type and press Enter…"
								onKeyDown={e => {
									e.stopPropagation();
									if (e.key === 'Enter') commitText(pendingTextId, e.currentTarget.value);
									if (e.key === 'Escape') commitText(pendingTextId, '');
								}}
								onBlur={e => commitText(pendingTextId, e.currentTarget.value)}
							/>
						</div>
					) : (
						<div className={styles.layers_grid}>
							{visibleLayers.map(l => {
								const count = activeLayers.find(e => e.layer.id === l.id)?.count ?? 0;
								const active = count > 0;
								return (
									<button key={l.id}
										data-layer-btn
										data-layer-id={l.id}
										onClick={() => multiSelectAllowed ? onLayerAdd(l) : onLayerSelect(l)}
										className={`${styles.layer_btn} ${active ? styles.layer_btn_active : ''}`}
										style={{
											border: active ? `2px solid ${l.style.stroke}` : '2px solid transparent',
										}}
									>
										{l.label}
										{count > 1 && <span className={styles.layer_btn_count}>{count}</span>}
									</button>
								);
							})}
						</div>
					)}
					<div className={styles.layer_actions}>
						<button onClick={onClear} className={`${styles.btn_base} ${styles.btn_clear}`}>Clear</button>
						<button onClick={onClearAll} className={`${styles.btn_base} ${styles.btn_clear_all}`}>Clear All</button>
					</div>
				</div>
			</div>}
		</>
	);
};

export default Toolbar;
