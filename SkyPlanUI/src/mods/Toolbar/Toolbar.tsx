import React, {useEffect, useMemo, useRef} from "react";
import {ShapeData, Tag, TOOLS} from '../types';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome'
import {faArrowLeft, faXmark} from '@fortawesome/free-solid-svg-icons'
import styles from './Toolbar.module.scss';
import shared from '../shared.module.scss';
import {getModule} from 'cs2/modding';
import {FOCUS_DISABLED} from 'cs2/input';
import {useSkyplan} from '../SkyplanContext';
import {useDrawingContext} from "mods/DrawingContext";
import ShapeManager from "mods/ShapeManager/ShapeManager";


const DRAG_THRESHOLD = 6;

const TAG_LABEL: Record<string, string> = {
	[Tag.path]: 'Line',
	[Tag.polygon]: 'Polygon',
	[Tag.circle]: 'Point',
};

const Toolbar: React.FC = () => {
	const {
		activeTool, activeLayer, visibleLayers, viewMode,
		toolbarPos, onToolbarPosChange,
		onViewModeToggle, onToolChange, onLayerChange,
		onUndo, onClear, onClearAll, onClose,
	} = useSkyplan();

	const {
	  shapes,
	  globalOpacity,
	  onGlobalOpacityChange,
	  layerVisible,
	  onLayerVisibleToggle,
	} = useDrawingContext();


	const toolbarEl = useRef<HTMLDivElement>(null);
	const dragHandleEl = useRef<HTMLDivElement>(null);
	const tbDownRef = useRef(false);
	const tbDownPosRef = useRef({ x: 0, y: 0 });
	const draggingRef = useRef(false);
	const dragOffRef = useRef({ x: 0, y: 0 });


	useEffect(() => {
		if (!toolbarPos && toolbarEl.current) {
			onToolbarPosChange({ left: 12, top: 12 });
		}
	}, []);

	useEffect(() => {
		function inDragHandle(cx: number, cy: number) {
			if (!dragHandleEl.current) return false;
			const r = dragHandleEl.current.getBoundingClientRect();
			return cx >= r.left && cx <= r.right && cy >= r.top && cy <= r.bottom;
		}
		const md = (e: MouseEvent) => {
			if (e.button !== 0 || !inDragHandle(e.clientX, e.clientY)) return;
			tbDownRef.current = true;
			tbDownPosRef.current = { x: e.clientX, y: e.clientY };
		};
		const mm = (e: MouseEvent) => {
			if (!tbDownRef.current) return;
			const dx = e.clientX - tbDownPosRef.current.x, dy = e.clientY - tbDownPosRef.current.y;
			if (!draggingRef.current && dx * dx + dy * dy > DRAG_THRESHOLD * DRAG_THRESHOLD) {
				draggingRef.current = true;
				const el = toolbarEl.current;
				if (el) dragOffRef.current = { x: tbDownPosRef.current.x - el.offsetLeft, y: tbDownPosRef.current.y - el.offsetTop };
			}
			if (draggingRef.current)
				onToolbarPosChange({ left: e.clientX - dragOffRef.current.x, top: e.clientY - dragOffRef.current.y });
		};
		const mu = () => { tbDownRef.current = false; draggingRef.current = false; };

		document.addEventListener('mousedown', md, true);
		document.addEventListener('mousemove', mm, true);
		document.addEventListener('mouseup', mu, true);
		return () => {
			document.removeEventListener('mousedown', md, true);
			document.removeEventListener('mousemove', mm, true);
			document.removeEventListener('mouseup', mu, true);
		};
	}, []);

	return (
		<div ref={toolbarEl} className={styles.toolbar} style={{
			position: 'absolute',
			left: toolbarPos?.left ?? 0,
			top: toolbarPos?.top ?? 12,
			pointerEvents: 'auto', userSelect: 'none',
		}}>

			<div ref={dragHandleEl} className={styles.drag_handle}>
			  <span className={styles.title_handel_bar}>SkyPlan</span>
			  <button onClick={onClose} className={`${styles.btn_base} ${styles.btn_right}`}>
				<FontAwesomeIcon icon={faXmark} className={styles.svg} />
			  </button>
			</div>

			<div className={styles.actions_container}>
				<button onClick={onUndo} className={styles.btn_base}>
					<FontAwesomeIcon icon={faArrowLeft} className={styles.svg} />
				</button>
				<label className={styles.mode_toggle} onClick={onViewModeToggle}>
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
			  layerVisible={layerVisible}
			  onLayerVisibleToggle={onLayerVisibleToggle}
			  />}
			{!viewMode && <div className={styles.body}>
				<div className={styles.tools_column}>
					{TOOLS.map(t => {
						const active = activeTool === t.id;
						return <button key={t.id}
							onClick={() => onToolChange(t.id)}
							className={`${styles.btn_base} ${active ? styles.btn_active : ''} ${t.id === 'erase' ? styles.btn_erase : ''}`}
							style={{
								border: active && activeLayer ? `2px solid ${activeLayer.style.stroke}` : '2px solid transparent',
							}}
						>
							<FontAwesomeIcon className={`${styles.svg} ${active ? styles.svg_active : ''}`} icon={t.icon} />
							<span className={styles.tooltip}>{t.label}</span>
						</button>;
					})}
				</div>

				<div className={styles.layers_panel}>
					<div className={styles.layers_grid}>
						{visibleLayers.map(l => {
							const active = activeLayer?.id === l.id;
							return (
								<button key={l.id}
									onClick={() => onLayerChange(l)}
									className={`${styles.layer_btn} ${active ? styles.layer_btn_active : ''}`}
									style={{
										border: active ? `2px solid ${l.style.stroke}` : '2px solid transparent',
									}}
								>
									{l.label}
								</button>
							);
						})}
					</div>
					<div className={styles.layer_actions}>
						<button onClick={onClear} className={`${styles.btn_base} ${styles.btn_clear}`}>Clear</button>
						<button onClick={onClearAll} className={`${styles.btn_base} ${styles.btn_clear_all}`}>Clear All</button>
					</div>
				</div>
			</div>}
		</div>
	);
};

export default Toolbar;
