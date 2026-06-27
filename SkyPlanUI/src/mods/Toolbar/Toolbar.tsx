import React, {useEffect, useRef, useState} from "react";
import {Tool, ToolId, TOOLS, Layer, LAYERS} from '../types';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faLinesLeaning, faXmark } from '@fortawesome/free-solid-svg-icons'
import { faSquare } from '@fortawesome/free-regular-svg-icons'
import styles from './Toolbar.module.scss';

type FAStyle = React.CSSProperties & { [K in `--fa-font-${string}`]?: string };
const FA_STYLE: FAStyle = {
	display: 'block',
	width: 13,
	height: 13,
	overflow: 'visible',
	flexShrink: 0,
};

const DRAG_THRESHOLD = 6;

const LAYER_ACTIVE_STYLE: Record<Layer, React.CSSProperties> = {
	roads: { background: '#7a0000', color: '#ff8888', outline: '1px solid #ff4444' },
	zoning: { background: '#0a4a0a', color: '#88ee88', outline: '1px solid #44dd44' },
	transit: { background: '#001a6e', color: '#88aaff', outline: '1px solid #4488ff' },
	notes: { background: '#6a5000', color: '#ffdd66', outline: '1px solid #ffcc00' },
};

interface ToolbarProps {
	activeTool: ToolId;
	activeLayer: Layer;
	onToolChange: (t: ToolId) => void;
	onLayerChange: (l: Layer) => void;
	onClear: () => void;
	onClose: () => void;
}

const Toolbar: React.FC<ToolbarProps> = ({ activeTool, activeLayer, onToolChange, onLayerChange, onClear, onClose }) => {
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
		<div ref={toolbarEl} style={{
			position: 'absolute',
			left: toolbarPos?.left ?? 0,
			top: toolbarPos?.top ?? 12,
			display: 'inline-flex',
			alignItems: 'center',
			gap: 2,
			padding: '8px 10px',
			background: 'rgba(18,18,18,0.88)',
			borderRadius: 8, border: '1px solid rgba(255,255,255,0.12)',
			pointerEvents: 'auto', userSelect: 'none', cursor: 'grab',
		}}>
			{TOOLS.map(t => {
				const active = activeTool === t.id;
				const base: React.CSSProperties = {
					color: active ? '#fff' : '#bbb',
					background: active ? 'rgba(255,255,255,0.2)' : 'rgba(255,255,255,0.07)',
					outline: active ? '1px solid rgba(255,255,255,0.35)' : 'none',
				};
				const erase: React.CSSProperties = (t.id === 'erase' && active)
					? { background: '#3a1a00', color: '#ffaa55', outline: '1px solid #ff8800' } : {};
				return <button key={t.id}
				onClick={() => onToolChange(t.id)}
				className={`${styles.btn_base} ${active ? styles.btn_active : ''} ${t.id === 'erase' ? styles.btn_erase : ''}`}
				// className={styles.btn_base}
				// style={{...base, ...erase }}
				>
				<FontAwesomeIcon className={`${styles.svg} ${active ? styles.svg_active : ''}`} icon={t.icon} 
				// style={FA_STYLE}
				/>
				<span className={styles.tooltip}>{t.label}</span>
				{activeTool + t.id}
				</button>;
			})}

			<div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.18)', margin: '0 4px' }} />

			{LAYERS.map(l => {
				const active = activeLayer === l;
				return (
					<button key={l} onClick={() => onLayerChange(l)} style={{
						padding: '5px 12px', borderRadius: 5, border: 'none', cursor: 'pointer',
						fontSize: 13, fontWeight: 600,
						color: active ? '#fff' : '#bbb',
						background: active ? 'rgba(255,255,255,0.2)' : 'rgba(255,255,255,0.07)',
						...(active ? LAYER_ACTIVE_STYLE[l] : {}),
					}}>
						{l[0].toUpperCase() + l.slice(1)}
					</button>
				);
			})}

			<div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.18)', margin: '0 4px' }} />

			<button onClick={onClear} style={{ padding: '5px 12px', borderRadius: 5, border: 'none', cursor: 'pointer', fontSize: 13, fontWeight: 600, color: '#ff7070', background: 'rgba(255,255,255,0.07)' }}>Clear</button>
			<button onClick={onClose} 
				style={{ 
				  padding: '5px 12px',
				  borderRadius: 5,
				  border: 'none',
				  cursor: 'pointer', 
				  fontSize: 13,
				  color: '#888',
				  background: 'rgba(255,255,255,0.07)' }}>
				  <FontAwesomeIcon icon={faXmark} style={FA_STYLE} />
			</button>
		</div>
	);
};

export default Toolbar;
