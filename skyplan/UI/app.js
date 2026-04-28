(function () {
    'use strict';

    // Idempotent — safe to execute multiple times
    if (document.getElementById('skyplan-root')) return;

    // ── Inline styles ────────────────────────────────────────────────────────
    const styleEl = document.createElement('style');
    styleEl.id = 'skyplan-style';
    styleEl.textContent = `
#skyplan-root {
    position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 9000;
    display: none; pointer-events: none;
}
#skyplan-toolbar {
    position: absolute;
    display: flex; align-items: center; gap: 6px;
    padding: 8px 14px;
    background: rgba(18, 18, 18, 0.88);
    border-radius: 8px; border: 1px solid rgba(255,255,255,0.12);
    pointer-events: auto; user-select: none;
    cursor: grab;
}
#skyplan-toolbar.dragging { cursor: grabbing; }
.sp-sep {
    width: 1px; align-self: stretch;
    background: rgba(255,255,255,0.18); margin: 0 4px;
}
.sp-btn {
    padding: 5px 12px; border-radius: 5px; border: none; cursor: pointer;
    font-size: 13px; font-weight: 600; color: #bbb;
    background: rgba(255,255,255,0.07);
    transition: background 0.12s, color 0.12s;
}
.sp-btn:hover { background: rgba(255,255,255,0.16); color: #fff; }
.sp-tool.active {
    background: rgba(255,255,255,0.2);
    outline: 1px solid rgba(255,255,255,0.35);
    color: #fff;
}
#sp-layer-roads.active   { background: #7a0000; color: #ff8888; outline: 1px solid #ff4444; }
#sp-layer-zoning.active  { background: #0a4a0a; color: #88ee88; outline: 1px solid #44dd44; }
#sp-layer-transit.active { background: #001a6e; color: #88aaff; outline: 1px solid #4488ff; }
#sp-layer-notes.active   { background: #6a5000; color: #ffdd66; outline: 1px solid #ffcc00; }
#sp-clear { color: #ff7070; }
#sp-close { color: #888; font-size: 16px; padding: 3px 9px; cursor: pointer; }
#skyplan-svg {
    position: absolute; top: 0; left: 0;
    pointer-events: none; overflow: visible;
}
`;
    document.head.appendChild(styleEl);

    // ── DOM ──────────────────────────────────────────────────────────────────
    const root = document.createElement('div');
    root.id = 'skyplan-root';
    root.innerHTML = `
<div id="skyplan-toolbar">
  <button class="sp-btn sp-tool active" id="sp-tool-line">Line</button>
  <button class="sp-btn sp-tool" id="sp-tool-rect">Rect</button>
  <button class="sp-btn sp-tool" id="sp-tool-circle">Circle</button>
  <button class="sp-btn sp-tool" id="sp-tool-free">Freehand</button>
  <div class="sp-sep"></div>
  <button class="sp-btn sp-layer active" id="sp-layer-roads">Roads</button>
  <button class="sp-btn sp-layer" id="sp-layer-zoning">Zoning</button>
  <button class="sp-btn sp-layer" id="sp-layer-transit">Transit</button>
  <button class="sp-btn sp-layer" id="sp-layer-notes">Notes</button>
  <div class="sp-sep"></div>
  <button class="sp-btn" id="sp-clear">Clear</button>
  <button class="sp-btn" id="sp-close">X</button>
</div>
<svg id="skyplan-svg" xmlns="http://www.w3.org/2000/svg">
  <g id="sp-drawings">
    <g id="sp-g-roads"></g>
    <g id="sp-g-zoning"></g>
    <g id="sp-g-transit"></g>
    <g id="sp-g-notes"></g>
    <g id="sp-preview"></g>
  </g>
</svg>`;
    document.body.appendChild(root);

    // ── Refs & state ─────────────────────────────────────────────────────────
    const toolbar   = document.getElementById('skyplan-toolbar');
    const svg       = document.getElementById('skyplan-svg');
    const drawings  = document.getElementById('sp-drawings');
    const preview   = document.getElementById('sp-preview');
    const NS      = 'http://www.w3.org/2000/svg';

    const COLORS = {
        roads:   '#ff4444',
        zoning:  '#44dd44',
        transit: '#4488ff',
        notes:   '#ffcc00',
    };

    let tool    = 'line';
    let layer   = 'roads';
    let drawing = false;
    let x0 = 0, y0 = 0;
    let freePts = [];

    // toolbar drag state
    let panelDragging  = false;
    let toolbarDown    = false;   // mousedown happened on toolbar
    let toolbarDownX   = 0, toolbarDownY = 0;
    let dragOX = 0, dragOY = 0;
    const DRAG_THRESHOLD = 6;     // px before toolbar drag activates

    // deduplicate pointer vs mouse events (GameFace may fire both)
    let lastInputType = null;

    // ── Debug bridge ─────────────────────────────────────────────────────────
    function debug(msg) {
        if (typeof engine !== 'undefined') engine.trigger('skyplan.debug', String(msg));
    }

    // Set SVG size explicitly — Coherent GameFace may not support inset/100% CSS
    function syncViewBox() {
        const w = window.innerWidth  || document.documentElement.clientWidth  || screen.width  || 1920;
        const h = window.innerHeight || document.documentElement.clientHeight || screen.height || 1080;
        svg.setAttribute('width',   w);
        svg.setAttribute('height',  h);
        svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
        debug(`syncViewBox ${w}x${h}`);
    }
    syncViewBox();
    window.addEventListener('resize', syncViewBox);

    // ── Toolbar setup ─────────────────────────────────────────────────────────
    root.querySelectorAll('.sp-tool').forEach(btn => btn.addEventListener('click', () => {
        root.querySelectorAll('.sp-tool').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        tool = btn.id.replace('sp-tool-', '');
    }));

    root.querySelectorAll('.sp-layer').forEach(btn => btn.addEventListener('click', () => {
        root.querySelectorAll('.sp-layer').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        layer = btn.id.replace('sp-layer-', '');
    }));

    document.getElementById('sp-clear').addEventListener('click', () => {
        const g = document.getElementById(`sp-g-${layer}`);
        while (g.firstChild) g.removeChild(g.firstChild);
        notify();
    });

    document.getElementById('sp-close').addEventListener('click', hide);

    // ── Camera transform state ────────────────────────────────────────────────
    // C# sends two SVG matrix strings each frame the camera moves:
    //   fwd: baseline → current screen (applied to sp-drawings group)
    //   inv: current screen → baseline (applied to incoming mouse coords)
    // matrix(a,b,c,d,e,f): x'=a*x+c*y+e, y'=b*x+d*y+f
    let invMat = [1, 0, 0, 1, 0, 0]; // [a,b,c,d,e,f] — identity until C# sends first transform

    // ── Coordinate transform ─────────────────────────────────────────────────
    // Convert current-screen coords to baseline coords using the inverse camera matrix.
    function svgPt(clientX, clientY) {
        const [a, b, c, d, e, f] = invMat;
        return {
            x: a * clientX + c * clientY + e,
            y: b * clientX + d * clientY + f,
        };
    }

    // ── Hit testing ──────────────────────────────────────────────────────────
    function rectContains(domRect, cx, cy) {
        return cx >= domRect.left && cx <= domRect.right &&
               cy >= domRect.top  && cy <= domRect.bottom;
    }

    function inToolbar(cx, cy) {
        return rectContains(toolbar.getBoundingClientRect(), cx, cy);
    }

    function panelVisible() {
        return root.style.display !== 'none';
    }

    function makePath(pts) {
        return 'M ' + pts.map(([x, y]) => `${x},${y}`).join(' L ');
    }

    // ── Preview element (reused, never recreated per-frame) ──────────────────
    let previewEl = null;

    function ensurePreview(tag) {
        if (previewEl && previewEl.tagName === tag) return previewEl;
        if (previewEl && previewEl.parentNode) previewEl.parentNode.removeChild(previewEl);
        previewEl = document.createElementNS(NS, tag);
        previewEl.setAttribute('fill', 'none');
        previewEl.setAttribute('stroke-linecap', 'round');
        if (tag === 'path') previewEl.setAttribute('stroke-linejoin', 'round');
        preview.appendChild(previewEl);
        return previewEl;
    }

    function updatePreview(cx, cy) {
        const c = COLORS[layer];
        const sw = '4';
        if (tool === 'line') {
            const el = ensurePreview('line');
            el.setAttribute('x1', x0);   el.setAttribute('y1', y0);
            el.setAttribute('x2', cx);   el.setAttribute('y2', cy);
            el.setAttribute('stroke', c); el.setAttribute('stroke-width', sw);
        } else if (tool === 'rect') {
            const el = ensurePreview('rect');
            el.setAttribute('x',      Math.min(x0, cx));
            el.setAttribute('y',      Math.min(y0, cy));
            el.setAttribute('width',  Math.abs(cx - x0));
            el.setAttribute('height', Math.abs(cy - y0));
            el.setAttribute('stroke', c); el.setAttribute('stroke-width', sw);
        } else if (tool === 'circle') {
            const el = ensurePreview('ellipse');
            el.setAttribute('cx', (x0 + cx) / 2);
            el.setAttribute('cy', (y0 + cy) / 2);
            el.setAttribute('rx', Math.abs(cx - x0) / 2);
            el.setAttribute('ry', Math.abs(cy - y0) / 2);
            el.setAttribute('stroke', c); el.setAttribute('stroke-width', sw);
        } else if (tool === 'free') {
            freePts.push([cx, cy]);
            const el = ensurePreview('path');
            el.setAttribute('d', makePath(freePts));
            el.setAttribute('stroke', c); el.setAttribute('stroke-width', sw);
        }
    }

    function clearPreview() {
        if (previewEl && previewEl.parentNode) previewEl.parentNode.removeChild(previewEl);
        previewEl = null;
    }

    // ── Unified input handler ────────────────────────────────────────────────
    function handleDown(cx, cy, inputType) {
        if (!panelVisible()) return false;
        if (lastInputType === 'pointer' && inputType === 'mouse') return false;
        debug(`down ${inputType} ${cx},${cy} toolbar=${inToolbar(cx, cy)}`);

        if (inToolbar(cx, cy)) {
            toolbarDown  = true;
            toolbarDownX = cx;
            toolbarDownY = cy;
            return false;
        }

        lastInputType = inputType;
        const p = svgPt(cx, cy);
        x0 = p.x; y0 = p.y;
        freePts = [[p.x, p.y]];
        drawing = true;
        return true;
    }

    function handleMove(cx, cy, inputType) {
        let consumed = false;

        if (toolbarDown && !panelDragging) {
            const dx = cx - toolbarDownX, dy = cy - toolbarDownY;
            if (dx * dx + dy * dy > DRAG_THRESHOLD * DRAG_THRESHOLD) {
                panelDragging = true;
                dragOX = toolbarDownX - toolbar.offsetLeft;
                dragOY = toolbarDownY - toolbar.offsetTop;
                toolbar.classList.add('dragging');
            }
        }

        if (panelDragging) {
            toolbar.style.left = (cx - dragOX) + 'px';
            toolbar.style.top  = (cy - dragOY) + 'px';
            consumed = true;
        }

        if (drawing) {
            if (lastInputType === 'pointer' && inputType === 'mouse') return false;
            const p = svgPt(cx, cy);
            updatePreview(p.x, p.y);
            consumed = true;
        }

        return consumed;
    }

    function handleUp(cx, cy, inputType) {
        toolbarDown = false;
        let consumed = false;

        if (panelDragging) {
            panelDragging = false;
            toolbar.classList.remove('dragging');
            consumed = true;
        }

        if (drawing) {
            if (lastInputType === 'pointer' && inputType === 'mouse') return false;
            drawing = false;
            lastInputType = null;
            const p = svgPt(cx, cy);

            // Finalize the preview element into the layer group
            updatePreview(p.x, p.y);
            const el = previewEl;
            previewEl = null; // disown so clearPreview won't remove it

            debug(`up — shape committed: ${!!el} tool=${tool}`);

            if (el) {
                el.style.cursor = 'pointer';
                el.addEventListener('click', ev => { if (ev.shiftKey) { el.remove(); notify(); } }, true);
                document.getElementById(`sp-g-${layer}`).appendChild(el);
                notify();
            }
            consumed = true;
        }

        return consumed;
    }

    // Mouse events
    document.addEventListener('mousedown', e => {
        if (e.button !== 0) return;
        if (handleDown(e.clientX, e.clientY, 'mouse')) {
            e.stopImmediatePropagation(); e.preventDefault();
        }
    }, true);
    document.addEventListener('mousemove', e => {
        if (handleMove(e.clientX, e.clientY, 'mouse')) {
            e.stopImmediatePropagation(); e.preventDefault();
        }
    }, true);
    document.addEventListener('mouseup', e => {
        if (e.button !== 0) return;
        if (handleUp(e.clientX, e.clientY, 'mouse')) {
            e.stopImmediatePropagation(); e.preventDefault();
        }
    }, true);

    // Pointer events (GameFace may route input as pointer instead of mouse)
    document.addEventListener('pointerdown', e => {
        if (e.button !== 0) return;
        if (handleDown(e.clientX, e.clientY, 'pointer')) {
            e.stopImmediatePropagation(); e.preventDefault();
        }
    }, true);
    document.addEventListener('pointermove', e => {
        if (handleMove(e.clientX, e.clientY, 'pointer')) {
            e.stopImmediatePropagation(); e.preventDefault();
        }
    }, true);
    document.addEventListener('pointerup', e => {
        if (e.button !== 0) return;
        if (handleUp(e.clientX, e.clientY, 'pointer')) {
            e.stopImmediatePropagation(); e.preventDefault();
        }
    }, true);

    // Escape closes
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && panelVisible()) hide();
    }, true);

    // ── Panel visibility ─────────────────────────────────────────────────────
    function centerToolbar() {
        // Position toolbar in pixels so drag works correctly
        toolbar.style.left = Math.round((window.innerWidth  - toolbar.offsetWidth)  / 2) + 'px';
        toolbar.style.top  = '12px';
    }

    function show() {
        root.style.display = 'block';
        // Center on first show; after that, toolbar stays where user dragged it
        if (!toolbar.style.left) centerToolbar();
    }

    function hide() {
        root.style.display = 'none';
        drawing = false;
        panelDragging = false;
        preview.innerHTML = '';
        if (typeof engine !== 'undefined') engine.trigger('skyplan.panelClosed');
    }

    // ── Coherent bridge ──────────────────────────────────────────────────────
    function notify() {
        if (typeof engine === 'undefined') return;
        const data = {};
        ['roads', 'zoning', 'transit', 'notes'].forEach(l => {
            data[l] = document.getElementById(`sp-g-${l}`).innerHTML;
        });
        engine.trigger('skyplan.drawingUpdated', JSON.stringify(data));
    }

    if (typeof engine !== 'undefined') {
        engine.on('skyplan.togglePanel', visible => { if (visible) show(); else hide(); });

        engine.on('skyplan.cameraTransform', (fwd, inv) => {
            // Apply forward matrix to drawings group so shapes track the camera
            drawings.setAttribute('transform', fwd);
            // Parse inverse matrix for coord conversion on next mouse event
            const nums = inv.replace('matrix(', '').replace(')', '').split(',').map(Number);
            if (nums.length === 6 && nums.every(isFinite)) invMat = nums;
        });
    }

})();
