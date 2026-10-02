import React, {useEffect, useRef, useState} from 'react';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome';
import {faXmark} from '@fortawesome/free-solid-svg-icons';
import {getPanelPosition, setPanelPosition} from './panelPositions';
import styles from './DraggablePanel.module.scss';

const DRAG_THRESHOLD = 6;

interface DraggablePanelProps {
	// Identifies this panel's remembered position in the shared position store - use a stable,
	// unique string per call site (e.g. "toolbar", "whats-new").
	persistKey: string;
	title: string;
	onClose: () => void;
	// Extra buttons rendered in the header, before the close button (e.g. Toolbar's "?" button).
	headerExtra?: React.ReactNode;
	defaultPosition?: { left: number; top: number };
	// Longer titles wrap at this width instead of widening the panel.
	titleMaxWidth?: number;
	className?: string;
	children: React.ReactNode;
}

const DEFAULT_POSITION = { left: 12, top: 12 };

const DraggablePanel: React.FC<DraggablePanelProps> = ({persistKey, title, onClose, headerExtra, defaultPosition, titleMaxWidth, className, children}) => {
	const [pos, setPos] = useState(() => getPanelPosition(persistKey, defaultPosition ?? DEFAULT_POSITION));

	const panelEl = useRef<HTMLDivElement>(null);
	const dragHandleEl = useRef<HTMLDivElement>(null);
	const tbDownRef = useRef(false);
	const tbDownPosRef = useRef({ x: 0, y: 0 });
	const draggingRef = useRef(false);
	const dragOffRef = useRef({ x: 0, y: 0 });

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
				const el = panelEl.current;
				if (el) dragOffRef.current = { x: tbDownPosRef.current.x - el.offsetLeft, y: tbDownPosRef.current.y - el.offsetTop };
			}
			if (draggingRef.current) {
				const next = { left: e.clientX - dragOffRef.current.x, top: e.clientY - dragOffRef.current.y };
				setPos(next);
				setPanelPosition(persistKey, next);
			}
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
	}, [persistKey]);

	return (
		<div ref={panelEl} className={`${styles.panel} ${className ?? ''}`} style={{
			position: 'absolute',
			left: pos.left,
			top: pos.top,
			pointerEvents: 'auto', userSelect: 'none',
		}}>
			<div ref={dragHandleEl} className={styles.drag_handle}>
				<span className={styles.title} style={titleMaxWidth !== undefined ? { maxWidth: titleMaxWidth } : undefined}>{title}</span>
				<div className={styles.btn_group}>
					{headerExtra}
					<button onClick={onClose} className={`${styles.btn_base} ${styles.btn_right}`}>
						<FontAwesomeIcon icon={faXmark} className={styles.svg} />
					</button>
				</div>
			</div>
			{children}
		</div>
	);
};

export default DraggablePanel;
