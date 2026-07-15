import React, {useEffect, useRef, useState} from "react";
import {ToolId, TOOLS, Layer, LayerDef} from '../types';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome'
import {faArrowLeft, faXmark} from '@fortawesome/free-solid-svg-icons'
import styles from './Toolbar.module.scss';

const DRAG_THRESHOLD = 6;


interface ToolbarProps {
	activeTool: ToolId;
	activeLayer: LayerDef;
	layers: LayerDef[];
	onToolChange: (t: ToolId) => void;
	onLayerChange: (l: LayerDef) => void;
	onUndo: () => void;
	onClear: () => void;
	onClearAll: () => void;
	onClose: () => void;
}

const Toolbar: React.FC<ToolbarProps> = ({ activeTool, activeLayer, layers, onToolChange, onLayerChange, onUndo, onClear, onClearAll, onClose }) => {
	const toolbarEl = useRef<HTMLDivElement>(null);
	const tbDownRef = useRef(false);
	const tbDownPosRef = useRef({ x: 0, y: 0 });
	const draggingRef = useRef(false);
	const dragOffRef = useRef({ x: 0, y: 0 });
	const tbCentered = useRef(false);

	const [toolbarPos, setToolbarPos] = useState<{ left: number; top: number } | null>(null);

	// center on first mount
	useEffect(() => {
		if (!tbCentered.current && toolbarEl.current) {
			setToolbarPos({ left: Math.round((window.innerWidth - toolbarEl.current.offsetWidth) / 2), top: 12 });
			tbCentered.current = true;
		}
	}, []);

	// drag logic
	useEffect(() => {
		function inToolbar(cx: number, cy: number) {
			if (!toolbarEl.current) return false;
			const r = toolbarEl.current.getBoundingClientRect();
			return cx >= r.left && cx <= r.right && cy >= r.top && cy <= r.bottom;
		}
		const md = (e: MouseEvent) => {
			if (e.button !== 0 || !inToolbar(e.clientX, e.clientY)) return;
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
				setToolbarPos({ left: e.clientX - dragOffRef.current.x, top: e.clientY - dragOffRef.current.y });
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
			pointerEvents: 'auto', userSelect: 'none', cursor: 'grab',
		}}>

			<div className={styles.actions_container}>
				<button onClick={onClose} className={styles.btn_base}>
					<FontAwesomeIcon icon={faXmark} className={styles.svg} />
				</button>
				<button onClick={onUndo} className={styles.btn_base}>
					<FontAwesomeIcon icon={faArrowLeft} className={styles.svg} />
				</button>
				<button onClick={onClear} className={styles.btn_base} style={{ color: '#ff7070' }}>Clear</button>
				<button onClick={onClearAll} className={styles.btn_base} style={{ color: '#ff4444' }}>Clear All</button>
			</div>

			<div className={styles.body}>
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
					<div className={styles.search_bar}>
						<span className={styles.search_icon}>⌕</span>
						<span className={styles.search_placeholder}>Search layers...</span>
					</div>
					<div className={styles.layers_grid}>
						{layers.map(l => {
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
				</div>
			</div>
		</div>
	);
};

export default Toolbar;
