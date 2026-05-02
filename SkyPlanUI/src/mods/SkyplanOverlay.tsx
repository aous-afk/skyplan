import React, { useState, useEffect, useMemo, useRef, useCallback } from 'react';
import { useValue, trigger } from 'cs2/api';
import { panelVisible$, shapes$, preview$, highlight$ } from '../bindings';

// ── Types ─────────────────────────────────────────────────────────────────────

interface ShapeData {
  id: string;
  tag: 'line' | 'polygon' | 'path' | 'ellipse' | 'rect';
  layer: string;
  [key: string]: string;
}

// ── Constants ─────────────────────────────────────────────────────────────────

const DRAG_THRESHOLD = 6;

const TOOLS = ['line', 'rect', 'circle', 'free', 'erase'] as const;
type Tool = typeof TOOLS[number];

const LAYERS = ['roads', 'zoning', 'transit', 'notes'] as const;
type Layer = typeof LAYERS[number];

const LAYER_ACTIVE_STYLE: Record<Layer, React.CSSProperties> = {
  roads:   { background: '#7a0000', color: '#ff8888', outline: '1px solid #ff4444' },
  zoning:  { background: '#0a4a0a', color: '#88ee88', outline: '1px solid #44dd44' },
  transit: { background: '#001a6e', color: '#88aaff', outline: '1px solid #4488ff' },
  notes:   { background: '#6a5000', color: '#ffdd66', outline: '1px solid #ffcc00' },
};

// ── SVG shape renderer ────────────────────────────────────────────────────────

const SKIP = new Set(['id', 'tag', 'layer']);

function renderShape(s: ShapeData, opacity?: string): React.ReactElement | null {
  const attrs: Record<string, string> = {};
  for (const k of Object.keys(s)) {
    if (!SKIP.has(k)) attrs[k] = s[k];
  }
  if (opacity !== undefined) attrs.opacity = opacity;

  switch (s.tag) {
    case 'line':    return <line    key={s.id} {...attrs} />;
    case 'polygon': return <polygon key={s.id} {...attrs} />;
    case 'path':    return <path    key={s.id} {...attrs} />;
    case 'ellipse': return <ellipse key={s.id} {...attrs} />;
    case 'rect':    return <rect    key={s.id} {...attrs} />;
    default:        return null;
  }
}

// ── Main component ────────────────────────────────────────────────────────────

const SkyplanOverlay: React.FC = () => {
    console.log("Hello SkyPlanUI!");
  // C# → UI bindings (replaces engine.on state)
  const visible      = useValue(panelVisible$);
  const shapesJson   = useValue(shapes$);
  const previewJson  = useValue(preview$);
  const highlightRaw = useValue(highlight$);

  const shapes      = useMemo<ShapeData[]>(() => { try { return JSON.parse(shapesJson) ?? []; } catch { return []; } }, [shapesJson]);
  const preview     = useMemo<ShapeData | null>(() => { try { return previewJson ? JSON.parse(previewJson) : null; } catch { return null; } }, [previewJson]);
  const highlightId = highlightRaw || null;

  // Local UI state
  const [activeTool,  setActiveTool]  = useState<Tool>('line');
  const [activeLayer, setActiveLayer] = useState<Layer>('roads');
  const [svgSize,     setSvgSize]     = useState({ w: 1920, h: 1080 });
  const [toolbarPos,  setToolbarPos]  = useState<{ left: number; top: number } | null>(null);

  // Refs for capture-phase handlers (avoid stale closures)
  const visibleRef    = useRef(false);
  const toolRef       = useRef<Tool>('line');
  const drawingRef    = useRef(false);
  const lastInputRef  = useRef<string | null>(null);
  const tbDownRef     = useRef(false);
  const tbDownPosRef  = useRef({ x: 0, y: 0 });
  const draggingRef   = useRef(false);
  const dragOffRef    = useRef({ x: 0, y: 0 });
  const toolbarEl     = useRef<HTMLDivElement>(null);
  const tbCentered    = useRef(false);

  useEffect(() => { visibleRef.current = visible; },     [visible]);
  useEffect(() => { toolRef.current    = activeTool; }, [activeTool]);

  // SVG viewport sync
  useEffect(() => {
    const onResize = () => setSvgSize({ w: window.innerWidth || 1920, h: window.innerHeight || 1080 });
    onResize();
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);

  // ── Capture-phase document input handlers ──────────────────────────────────

  useEffect(() => {
    function inToolbar(cx: number, cy: number): boolean {
      if (!toolbarEl.current) return false;
      const r = toolbarEl.current.getBoundingClientRect();
      return cx >= r.left && cx <= r.right && cy >= r.top && cy <= r.bottom;
    }

    function onDown(cx: number, cy: number, type: string): boolean {
      if (!visibleRef.current) return false;
      if (lastInputRef.current === 'pointer' && type === 'mouse') return false;
      if (inToolbar(cx, cy)) {
        tbDownRef.current    = true;
        tbDownPosRef.current = { x: cx, y: cy };
        return false;
      }
      lastInputRef.current = type;
      drawingRef.current   = true;
      trigger('skyplan', 'drawStart', `${cx},${cy}`);
      return true;
    }

    function onMove(cx: number, cy: number, type: string): boolean {
      if (!visibleRef.current) return false;

      if (!drawingRef.current && toolRef.current === 'erase' && !inToolbar(cx, cy))
        trigger('skyplan', 'eraseHover', `${cx},${cy}`);

      let consumed = false;

      if (tbDownRef.current && !draggingRef.current) {
        const dx = cx - tbDownPosRef.current.x, dy = cy - tbDownPosRef.current.y;
        if (dx * dx + dy * dy > DRAG_THRESHOLD * DRAG_THRESHOLD) {
          draggingRef.current = true;
          const el = toolbarEl.current;
          if (el) dragOffRef.current = { x: tbDownPosRef.current.x - el.offsetLeft, y: tbDownPosRef.current.y - el.offsetTop };
        }
      }
      if (draggingRef.current) {
        setToolbarPos({ left: cx - dragOffRef.current.x, top: cy - dragOffRef.current.y });
        consumed = true;
      }
      if (drawingRef.current) {
        if (lastInputRef.current === 'pointer' && type === 'mouse') return false;
        trigger('skyplan', 'drawMove', `${cx},${cy}`);
        consumed = true;
      }
      return consumed;
    }

    function onUp(cx: number, cy: number, type: string): boolean {
      tbDownRef.current = false;
      let consumed = false;
      if (draggingRef.current) { draggingRef.current = false; consumed = true; }
      if (drawingRef.current) {
        if (lastInputRef.current === 'pointer' && type === 'mouse') return false;
        drawingRef.current   = false;
        lastInputRef.current = null;
        trigger('skyplan', 'drawEnd', `${cx},${cy}`);
        consumed = true;
      }
      return consumed;
    }

    const md = (e: MouseEvent)   => { if (e.button !== 0) return; if (onDown(e.clientX, e.clientY, 'mouse'))   { e.stopImmediatePropagation(); e.preventDefault(); } };
    const mm = (e: MouseEvent)   => { if (onMove(e.clientX, e.clientY, 'mouse'))                               { e.stopImmediatePropagation(); e.preventDefault(); } };
    const mu = (e: MouseEvent)   => { if (e.button !== 0) return; if (onUp(e.clientX, e.clientY, 'mouse'))     { e.stopImmediatePropagation(); e.preventDefault(); } };
    const pd = (e: PointerEvent) => { if (e.button !== 0) return; if (onDown(e.clientX, e.clientY, 'pointer')) { e.stopImmediatePropagation(); e.preventDefault(); } };
    const pm = (e: PointerEvent) => { if (onMove(e.clientX, e.clientY, 'pointer'))                             { e.stopImmediatePropagation(); e.preventDefault(); } };
    const pu = (e: PointerEvent) => { if (e.button !== 0) return; if (onUp(e.clientX, e.clientY, 'pointer'))   { e.stopImmediatePropagation(); e.preventDefault(); } };
    const kd = (e: KeyboardEvent) => {
      if (!visibleRef.current) return;
      if (e.key === 'Escape') {
        drawingRef.current  = false;
        draggingRef.current = false;
        trigger('skyplan', 'panelClosed');
        return;
      }
      if (e.ctrlKey && (e.key === 'z' || e.key === 'Z')) {
        trigger('skyplan', 'undo', '');
        e.stopImmediatePropagation(); e.preventDefault();
      }
    };

    document.addEventListener('mousedown',   md, true);
    document.addEventListener('mousemove',   mm, true);
    document.addEventListener('mouseup',     mu, true);
    document.addEventListener('pointerdown', pd, true);
    document.addEventListener('pointermove', pm, true);
    document.addEventListener('pointerup',   pu, true);
    document.addEventListener('keydown',     kd, true);
    return () => {
      document.removeEventListener('mousedown',   md, true);
      document.removeEventListener('mousemove',   mm, true);
      document.removeEventListener('mouseup',     mu, true);
      document.removeEventListener('pointerdown', pd, true);
      document.removeEventListener('pointermove', pm, true);
      document.removeEventListener('pointerup',   pu, true);
      document.removeEventListener('keydown',     kd, true);
    };
  }, []);

  // Center toolbar on first show
  useEffect(() => {
    if (visible && !tbCentered.current && toolbarEl.current) {
      setToolbarPos({ left: Math.round((window.innerWidth - toolbarEl.current.offsetWidth) / 2), top: 12 });
      tbCentered.current = true;
    }
  }, [visible]);

  // ── Toolbar actions ────────────────────────────────────────────────────────

  const handleTool = useCallback((t: Tool) => {
    setActiveTool(t);
    trigger('skyplan', 'setTool', t);
  }, []);

  const handleLayer = useCallback((l: Layer) => {
    setActiveLayer(l);
    trigger('skyplan', 'setLayer', l);
  }, []);

  const handleClear = useCallback(() => {
    trigger('skyplan', 'clearLayer', activeLayer);
  }, [activeLayer]);

  const handleClose = useCallback(() => {
    drawingRef.current = false;
    trigger('skyplan', 'panelClosed');
  }, []);

  // ── Render ─────────────────────────────────────────────────────────────────

  if (!visible) return null;

  const hasHighlight = highlightId !== null;

  return (
    <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, zIndex: 9000, pointerEvents: 'none' }}>

      {/* Toolbar */}
      <div
        ref={toolbarEl}
        style={{
          position: 'absolute',
          left: toolbarPos?.left ?? 0,
          top:  toolbarPos?.top  ?? 12,
          display: 'flex', alignItems: 'center', gap: 6,
          padding: '8px 14px',
          background: 'rgba(18,18,18,0.88)',
          borderRadius: 8, border: '1px solid rgba(255,255,255,0.12)',
          pointerEvents: 'auto', userSelect: 'none', cursor: 'grab',
        }}
      >
        {TOOLS.map(t => {
          const active = activeTool === t;
          const base: React.CSSProperties = {
            padding: '5px 12px', borderRadius: 5, border: 'none', cursor: 'pointer',
            fontSize: 13, fontWeight: 600,
            color:      active ? '#fff' : '#bbb',
            background: active ? 'rgba(255,255,255,0.2)' : 'rgba(255,255,255,0.07)',
            outline:    active ? '1px solid rgba(255,255,255,0.35)' : 'none',
          };
          const erase: React.CSSProperties = (t === 'erase' && active)
            ? { background: '#3a1a00', color: '#ffaa55', outline: '1px solid #ff8800' } : {};
          return <button key={t} onClick={() => handleTool(t)} style={{ ...base, ...erase }}>{t[0].toUpperCase() + t.slice(1)}</button>;
        })}

        <div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.18)', margin: '0 4px' }} />

        {LAYERS.map(l => {
          const active = activeLayer === l;
          return (
            <button key={l} onClick={() => handleLayer(l)} style={{
              padding: '5px 12px', borderRadius: 5, border: 'none', cursor: 'pointer',
              fontSize: 13, fontWeight: 600,
              color:      active ? '#fff' : '#bbb',
              background: active ? 'rgba(255,255,255,0.2)' : 'rgba(255,255,255,0.07)',
              ...(active ? LAYER_ACTIVE_STYLE[l] : {}),
            }}>
              {l[0].toUpperCase() + l.slice(1)}
            </button>
          );
        })}

        <div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.18)', margin: '0 4px' }} />

        <button onClick={handleClear} style={{ padding: '5px 12px', borderRadius: 5, border: 'none', cursor: 'pointer', fontSize: 13, fontWeight: 600, color: '#ff7070', background: 'rgba(255,255,255,0.07)' }}>Clear</button>
        <button onClick={handleClose} style={{ padding: '3px 9px',  borderRadius: 5, border: 'none', cursor: 'pointer', fontSize: 16, color: '#888',   background: 'rgba(255,255,255,0.07)' }}>X</button>
      </div>

      {/* SVG drawing canvas */}
      <svg
        style={{ position: 'absolute', top: 0, left: 0, pointerEvents: 'none', overflow: 'visible' }}
        width={svgSize.w} height={svgSize.h}
        viewBox={`0 0 ${svgSize.w} ${svgSize.h}`}
      >
        {shapes.map(s => renderShape(s, hasHighlight ? (s.id === highlightId ? '1' : '0.3') : undefined))}
        {preview && renderShape(preview)}
      </svg>

    </div>
  );
};

export default SkyplanOverlay;
