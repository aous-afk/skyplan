(function () {
    'use strict';

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
#sp-tool-erase.active { background: #3a1a00; color: #ffaa55; outline: 1px solid #ff8800; }
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
  <button class="sp-btn sp-tool" id="sp-tool-erase">Erase</button>
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

    // ── Refs ─────────────────────────────────────────────────────────────────
    const toolbar = document.getElementById('skyplan-toolbar');
    const svg     = document.getElementById('skyplan-svg');
    const preview = document.getElementById('sp-preview');
    const NS      = 'http://www.w3.org/2000/svg';

    var currentTool = 'line';
    let drawing = false;
    var highlightedId = null;

    // toolbar drag
    let panelDragging = false;
    let toolbarDown   = false;
    let toolbarDownX  = 0, toolbarDownY = 0;
    let dragOX = 0, dragOY = 0;
    const DRAG_THRESHOLD = 6;

    // deduplicate pointer vs mouse events
    let lastInputType = null;

    // ── Debug bridge ─────────────────────────────────────────────────────────
    function debug(msg) {
        if (typeof engine !== 'undefined') engine.trigger('skyplan.debug', String(msg));
    }

    // ── SVG size ─────────────────────────────────────────────────────────────
    function syncViewBox() {
        const w = window.innerWidth  || document.documentElement.clientWidth  || 1920;
        const h = window.innerHeight || document.documentElement.clientHeight || 1080;
        svg.setAttribute('width',   w);
        svg.setAttribute('height',  h);
        svg.setAttribute('viewBox', '0 0 ' + w + ' ' + h);
    }
    syncViewBox();
    window.addEventListener('resize', syncViewBox);

    // ── Toolbar ───────────────────────────────────────────────────────────────
    root.querySelectorAll('.sp-tool').forEach(function(btn) {
        btn.addEventListener('click', function() {
            root.querySelectorAll('.sp-tool').forEach(function(b) { b.classList.remove('active'); });
            btn.classList.add('active');
            var tool = btn.id.replace('sp-tool-', '');
            currentTool = tool;
            if (typeof engine !== 'undefined') engine.trigger('skyplan.setTool', tool);
        });
    });

    root.querySelectorAll('.sp-layer').forEach(function(btn) {
        btn.addEventListener('click', function() {
            root.querySelectorAll('.sp-layer').forEach(function(b) { b.classList.remove('active'); });
            btn.classList.add('active');
            var layer = btn.id.replace('sp-layer-', '');
            if (typeof engine !== 'undefined') engine.trigger('skyplan.setLayer', layer);
        });
    });

    document.getElementById('sp-clear').addEventListener('click', function() {
        var active = root.querySelector('.sp-layer.active');
        var layer  = active ? active.id.replace('sp-layer-', '') : 'roads';
        if (typeof engine !== 'undefined') engine.trigger('skyplan.clearLayer', layer);
    });

    document.getElementById('sp-close').addEventListener('click', hide);

    // ── Shape DOM management ──────────────────────────────────────────────────
    // C# sends flat JSON objects: {id, tag, layer, ...svgAttrs}
    // Create/update via setAttribute — never innerHTML (Coherent crashes).
    var shapeEls  = {};   // id → SVGElement
    var previewEl = null;

    function applyShapes(jsonStr) {
        if (!jsonStr) return;
        var shapes = JSON.parse(jsonStr);
        var seen   = {};
        for (var i = 0; i < shapes.length; i++) {
            var s  = shapes[i];
            seen[s.id] = true;
            var el = shapeEls[s.id];
            if (!el) {
                el = document.createElementNS(NS, s.tag);
                el.setAttribute('pointer-events', 'all'); // override SVG parent's pointer-events:none
                el.style.cursor = 'pointer';
                (function(sid) {
                    el.addEventListener('click', function(ev) {
                        if (ev.shiftKey && typeof engine !== 'undefined')
                            engine.trigger('skyplan.deleteShape', sid);
                    }, true);
                })(s.id);
                document.getElementById('sp-g-' + s.layer).appendChild(el);
                shapeEls[s.id] = el;
            }
            var keys = Object.keys(s);
            for (var k = 0; k < keys.length; k++) {
                var key = keys[k];
                if (key !== 'id' && key !== 'tag' && key !== 'layer')
                    el.setAttribute(key, s[key]);
            }
        }
        // Remove shapes absent from the update
        var ids = Object.keys(shapeEls);
        for (var j = 0; j < ids.length; j++) {
            if (!seen[ids[j]]) {
                shapeEls[ids[j]].remove();
                delete shapeEls[ids[j]];
            }
        }
    }

    function applyPreview(jsonStr) {
        if (!jsonStr) {
            if (previewEl) { previewEl.remove(); previewEl = null; }
            return;
        }
        var s = JSON.parse(jsonStr);
        if (!previewEl || previewEl.tagName !== s.tag) {
            if (previewEl) previewEl.remove();
            previewEl = document.createElementNS(NS, s.tag);
            preview.appendChild(previewEl);
        }
        var keys = Object.keys(s);
        for (var k = 0; k < keys.length; k++) {
            var key = keys[k];
            if (key !== 'id' && key !== 'tag' && key !== 'layer')
                previewEl.setAttribute(key, s[key]);
        }
    }

    function applyHighlight(id) {
        var ids = Object.keys(shapeEls);
        for (var i = 0; i < ids.length; i++) {
            shapeEls[ids[i]].setAttribute('opacity', id ? '0.3' : '1');
        }
        highlightedId = id || null;
        if (highlightedId && shapeEls[highlightedId]) {
            shapeEls[highlightedId].setAttribute('opacity', '1');
        }
    }

    // ── Hit testing ───────────────────────────────────────────────────────────
    function rectContains(r, cx, cy) {
        return cx >= r.left && cx <= r.right && cy >= r.top && cy <= r.bottom;
    }
    function inToolbar(cx, cy) { return rectContains(toolbar.getBoundingClientRect(), cx, cy); }
    function panelVisible()    { return root.style.display !== 'none'; }

    // ── Input handlers ────────────────────────────────────────────────────────
    function handleDown(cx, cy, inputType) {
        if (!panelVisible()) return false;
        if (lastInputType === 'pointer' && inputType === 'mouse') return false;
        if (inToolbar(cx, cy)) {
            toolbarDown = true; toolbarDownX = cx; toolbarDownY = cy;
            return false;
        }
        lastInputType = inputType;
        drawing = true;
        if (typeof engine !== 'undefined') engine.trigger('skyplan.drawStart', cx + ',' + cy);
        return true;
    }

    function handleMove(cx, cy, inputType) {
        // Erase hover — send even when not drawing so C# can highlight nearest shape
        if (!drawing && currentTool === 'erase' && panelVisible() && !inToolbar(cx, cy)) {
            if (typeof engine !== 'undefined') engine.trigger('skyplan.eraseHover', cx + ',' + cy);
        }
        var consumed = false;
        if (toolbarDown && !panelDragging) {
            var dx = cx - toolbarDownX, dy = cy - toolbarDownY;
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
            if (typeof engine !== 'undefined') engine.trigger('skyplan.drawMove', cx + ',' + cy);
            consumed = true;
        }
        return consumed;
    }

    function handleUp(cx, cy, inputType) {
        toolbarDown = false;
        var consumed = false;
        if (panelDragging) {
            panelDragging = false;
            toolbar.classList.remove('dragging');
            consumed = true;
        }
        if (drawing) {
            if (lastInputType === 'pointer' && inputType === 'mouse') return false;
            drawing       = false;
            lastInputType = null;
            if (typeof engine !== 'undefined') engine.trigger('skyplan.drawEnd', cx + ',' + cy);
            consumed = true;
        }
        return consumed;
    }

    // Mouse events
    document.addEventListener('mousedown', function(e) {
        if (e.button !== 0) return;
        if (handleDown(e.clientX, e.clientY, 'mouse')) { e.stopImmediatePropagation(); e.preventDefault(); }
    }, true);
    document.addEventListener('mousemove', function(e) {
        if (handleMove(e.clientX, e.clientY, 'mouse')) { e.stopImmediatePropagation(); e.preventDefault(); }
    }, true);
    document.addEventListener('mouseup', function(e) {
        if (e.button !== 0) return;
        if (handleUp(e.clientX, e.clientY, 'mouse')) { e.stopImmediatePropagation(); e.preventDefault(); }
    }, true);

    // Pointer events (GameFace may route input as pointer instead of mouse)
    document.addEventListener('pointerdown', function(e) {
        if (e.button !== 0) return;
        if (handleDown(e.clientX, e.clientY, 'pointer')) { e.stopImmediatePropagation(); e.preventDefault(); }
    }, true);
    document.addEventListener('pointermove', function(e) {
        if (handleMove(e.clientX, e.clientY, 'pointer')) { e.stopImmediatePropagation(); e.preventDefault(); }
    }, true);
    document.addEventListener('pointerup', function(e) {
        if (e.button !== 0) return;
        if (handleUp(e.clientX, e.clientY, 'pointer')) { e.stopImmediatePropagation(); e.preventDefault(); }
    }, true);

    document.addEventListener('keydown', function(e) {
        if (!panelVisible()) return;
        if (e.key === 'Escape') { hide(); return; }
        if (e.ctrlKey && (e.key === 'z' || e.key === 'Z')) {
            if (typeof engine !== 'undefined') engine.trigger('skyplan.undo', '');
            e.stopImmediatePropagation(); e.preventDefault();
        }
    }, true);

    // ── Panel visibility ──────────────────────────────────────────────────────
    function centerToolbar() {
        toolbar.style.left = Math.round((window.innerWidth  - toolbar.offsetWidth)  / 2) + 'px';
        toolbar.style.top  = '12px';
    }

    function show() {
        root.style.display = 'block';
        if (!toolbar.style.left) centerToolbar();
    }

    function hide() {
        root.style.display = 'none';
        drawing       = false;
        panelDragging = false;
        if (previewEl) { previewEl.remove(); previewEl = null; }
        applyHighlight('');
        if (typeof engine !== 'undefined') engine.trigger('skyplan.panelClosed');
    }

    // ── Coherent bridge ───────────────────────────────────────────────────────
    if (typeof engine !== 'undefined') {
        engine.on('skyplan.togglePanel',   function(visible) { if (visible) show(); else hide(); });
        engine.on('skyplan.shapesUpdate',    applyShapes);
        engine.on('skyplan.previewUpdate',   applyPreview);
        engine.on('skyplan.highlightShape',  applyHighlight);
    }

})();
