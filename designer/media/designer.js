// NetForms Designer - the webview. It is the editing chrome only: the form is a PNG painted by
// NetForms in the host process, and every rectangle, property and event comes from there. What
// happens here is selection, handles, dragging with snap lines, the property grid and the toolbox;
// each finished gesture becomes one batch of ops the host applies, writes and paints again.
// (Ported in spirit from GoFormsDesigner's media/designer.js, minus its per-control previews.)
(function () {
	'use strict';
	const vscode = acquireVsCodeApi();
	const G = window.NetFormsGeometry;
	const $ = (id) => document.getElementById(id);
	const el = (tag, cls, text) => {
		const e = document.createElement(tag);
		if (cls) e.className = cls;
		if (text !== undefined) e.textContent = text;
		return e;
	};

	// The UI's strings, translated by the extension's l10n bundle (sent with init); English is the key.
	const L = {};
	const T = (s, ...args) => (L[s] || s).replace(/\{(\d+)\}/g, (_, i) => String(args[i]));

	const saved = vscode.getState() || {};
	const state = {
		view: null,
		toolbox: [],
		selection: [], // ids; the last one is the primary (VS aligns to it); '' is the form
		zoom: saved.zoom || 1,
		tab: saved.tab || 'properties',
		collapsed: new Set(saved.collapsed || []),
		toolboxCollapsed: new Set(saved.toolboxCollapsed || []),
		filter: '',
		props: null, // { id, rows }
		events: null,
		busy: false,
		tabOrder: false,
		tabOrderNext: 0,
		locked: !!saved.locked, // select and inspect, but no drag, resize or arrow-key move
		selectNew: null, // ids present before an add: select whatever appears
		drag: null,
		parseError: null,
		snap: true,
	};

	function remember() {
		vscode.setState({ zoom: state.zoom, tab: state.tab, locked: state.locked, collapsed: [...state.collapsed], toolboxCollapsed: [...state.toolboxCollapsed] });
	}

	function post(message) { vscode.postMessage(message); }

	function apply(ops, select) {
		if (!ops.length) return;
		state.busy = true;
		document.body.style.cursor = 'progress';
		post({ type: 'apply', ops, select });
	}

	// --- messages ---------------------------------------------------------------------------------

	window.addEventListener('message', (e) => {
		const m = e.data;
		switch (m.type) {
			case 'init':
				Object.assign(L, m.strings || {});
				state.toolbox = m.toolbox || [];
				state.snap = m.snap !== false;
				receiveView(m.view, null);
				renderToolbox();
				break;
			case 'view':
				receiveView(m.view, m.select, m.reset);
				break;
			case 'properties':
				if (m.id === primaryId()) { state.props = { id: m.id, rows: m.rows }; renderInspector(); }
				break;
			case 'events':
				if (m.id === primaryId()) { state.events = { id: m.id, rows: m.rows }; renderInspector(); }
				break;
			case 'error':
				done();
				toast(m.message, 'error');
				renderStage();
				break;
			case 'parseError':
				done();
				state.parseError = m;
				renderAll();
				break;
		}
	});

	function done() { state.busy = false; document.body.style.cursor = ''; }

	function receiveView(view, select, reset) {
		done();
		state.parseError = null;
		const before = state.selectNew;
		state.view = view;
		if (before) {
			const fresh = allIds().filter((id) => !before.has(id));
			if (fresh.length) state.selection = [fresh.find((id) => itemById(id)?.kind !== 'nested') || fresh[0]];
			state.selectNew = null;
		} else if (select) {
			state.selection = select;
		}
		if (reset) state.selection = [''];
		const ids = new Set(allIds());
		state.selection = state.selection.filter((id) => id === '' || ids.has(id));
		if (!state.selection.length) state.selection = [''];
		state.props = null;
		state.events = null;
		renderAll();
		requestInspector();
	}

	function allIds() {
		if (!state.view) return [];
		return state.view.items.map((i) => i.id).concat(state.view.tray.map((t) => t.id));
	}

	function itemById(id) { return state.view && state.view.items.find((i) => i.id === id); }
	function trayById(id) { return state.view && state.view.tray.find((t) => t.id === id); }
	function primaryId() { return state.selection.length ? state.selection[state.selection.length - 1] : ''; }
	function shortType(t) { return (t || '').split('.').pop(); }

	function requestInspector() {
		const id = primaryId();
		if (state.tab === 'events') post({ type: 'events', id });
		else post({ type: 'properties', id });
	}

	// --- layout -----------------------------------------------------------------------------------

	function renderAll() {
		renderToolbar();
		renderStage();
		renderTray();
		renderInspector();
		renderStatus();
	}

	function button(label, title, onClick, opts) {
		const b = el('button', opts && opts.on ? 'on' : '', label);
		b.title = title;
		if (opts && opts.disabled) b.disabled = true;
		b.addEventListener('click', onClick);
		return b;
	}

	function renderToolbar() {
		const bar = $('toolbar');
		bar.textContent = '';
		const v = state.view;
		bar.append(
			button('↶', T('Undo (Ctrl+Z)'), () => post({ type: 'undo' }), { disabled: !v || !v.canUndo }),
			button('↷', T('Redo (Ctrl+Y)'), () => post({ type: 'redo' }), { disabled: !v || !v.canRedo }),
			el('span', 'sep'),
			button('−', T('Zoom out'), () => setZoom(state.zoom / 1.25)),
			el('span', 'zoom', Math.round(state.zoom * 100) + '%'),
			button('+', T('Zoom in'), () => setZoom(state.zoom * 1.25)),
			button('1:1', T('Actual size'), () => setZoom(1)),
			el('span', 'sep'),
			button('🔒', T('Lock: select and inspect, but refuse every drag and resize'), () => toggleLock(), { on: state.locked }),
			button('⇥', T('Tab order: click the controls in the order the Tab key should visit them'), () => toggleTabOrder(), { on: state.tabOrder }),
			button('⬒', T('Bring to front'), () => zorder('bringToFront'), { disabled: !movableSelection().length }),
			button('⬓', T('Send to back'), () => zorder('sendToBack'), { disabled: !movableSelection().length }),
			el('span', 'sep'),
			button('▶', T('Run the project (dotnet run)'), () => post({ type: 'run' })),
			button('{ }', T('View code (F7)'), () => post({ type: 'viewCode' })),
			button('≡', T('Open as text'), () => post({ type: 'openAsText' })),
		);
	}

	function setZoom(z) {
		state.zoom = Math.min(4, Math.max(0.25, Math.round(z * 100) / 100));
		remember();
		renderToolbar();
		renderStage();
	}

	function zorder(op) {
		apply(movableSelection().map((id) => ({ op, id })), state.selection);
	}

	function toggleLock() {
		state.locked = !state.locked;
		remember();
		renderToolbar();
		renderStage();
	}

	function toggleTabOrder() {
		state.tabOrder = !state.tabOrder;
		state.tabOrderNext = 0;
		renderToolbar();
		renderStage();
	}

	// --- toolbox ----------------------------------------------------------------------------------

	function renderToolbox() {
		const box = $('toolbox');
		box.textContent = '';
		const search = el('input', 'search');
		search.placeholder = T('Search toolbox');
		search.value = state.filter;
		search.addEventListener('input', () => { state.filter = search.value.toLowerCase(); renderToolboxGroups(groups); });
		const groups = el('div');
		box.append(search, groups);
		renderToolboxGroups(groups);
	}

	function renderToolboxGroups(host) {
		host.textContent = '';
		for (const cat of state.toolbox) {
			const items = cat.items.filter((i) => !state.filter || i.name.toLowerCase().includes(state.filter));
			if (!items.length) continue;
			const g = el('div', 'tb-group' + (state.toolboxCollapsed.has(cat.name) && !state.filter ? ' collapsed' : ''));
			const head = el('div', 'tb-head', T(cat.name));
			head.addEventListener('click', () => {
				if (state.toolboxCollapsed.has(cat.name)) state.toolboxCollapsed.delete(cat.name); else state.toolboxCollapsed.add(cat.name);
				remember();
				g.classList.toggle('collapsed');
			});
			const list = el('div', 'tb-items');
			for (const item of items) {
				const row = el('div', 'tb-item', item.name);
				if (item.tray) row.append(el('span', 'tray-mark', '  ' + T('(tray)')));
				row.title = item.tray ? T('Click to add to the component tray') : T('Click to add to the selected container, or drag onto the form');
				row.draggable = !item.tray;
				row.addEventListener('dragstart', (e) => { e.dataTransfer.setData('text/netforms-type', item.type); e.dataTransfer.effectAllowed = 'copy'; });
				row.addEventListener('click', () => addFromToolbox(item));
				list.append(row);
			}
			g.append(head, list);
			host.append(g);
		}
	}

	/** A click in the toolbox adds into the selected container (or the selected control's container), near its top left. */
	function addFromToolbox(item) {
		if (!state.view) return;
		state.selectNew = new Set(allIds());
		if (item.tray) { apply([{ op: 'add', type: item.type }]); return; }
		let target = itemById(primaryId());
		if (target && !target.container) target = itemById(target.parent);
		const siblings = state.view.items.filter((i) => i.parent === (target ? target.id : '') && i.kind === 'control');
		const offset = 12 + (siblings.length % 8) * 8;
		apply([{ op: 'add', type: item.type, parent: target ? target.id : '', x: offset, y: offset }]);
	}

	// --- stage: the form ------------------------------------------------------------------------

	function renderStage() {
		const stage = $('stage');
		stage.textContent = '';
		if (state.parseError) { renderParseError(stage); return; }
		const v = state.view;
		if (!v) { stage.append(el('div', '', T('Opening the form…'))); return; }

		const frame = el('div', 'form-frame');
		frame.style.transform = `scale(${state.zoom})`;
		const title = el('div', 'form-title' + (state.selection.includes('') ? ' selected' : ''));
		title.append(el('span', 'caption', v.text || v.className), el('span', 'caption-buttons', '–▢✕'));
		title.addEventListener('mousedown', (e) => { if (e.button === 0) select('', e.ctrlKey || e.shiftKey); });

		const canvas = el('div', 'canvas');
		canvas.style.width = v.width + 'px';
		canvas.style.height = v.height + 'px';
		const img = el('img', 'picture');
		img.src = 'data:image/png;base64,' + v.png;
		img.width = v.width;
		img.height = v.height;
		img.draggable = false;
		canvas.append(img);

		for (const it of v.items) {
			if (!it.visible) continue;
			// A docked control sits where its dock puts it: a dashed outline says dragging it does nothing.
			const hit = el('div', 'hit' + (it.kind === 'item' ? ' item-kind' : '') + (it.dock && it.dock !== 'None' && it.kind === 'control' ? ' docked' : ''));
			hit.dataset.id = it.id;
			place(hit, it);
			hit.title = `${it.id} (${shortType(it.type)})`;
			if (state.selection.includes(it.id)) hit.classList.add(it.id === primaryId() ? 'primary' : 'selected');
			canvas.append(hit);
		}
		if (!state.drag) drawHandles(canvas);
		if (state.tabOrder) drawTabBadges(canvas);

		for (const edge of ['e', 's', 'se']) {
			const grip = el('div', 'form-grip ' + edge);
			grip.style.left = (edge === 's' ? v.width / 2 : v.width) - 3 + 'px';
			grip.style.top = (edge === 'e' ? v.height / 2 : v.height) - 3 + 'px';
			grip.addEventListener('mousedown', (e) => startFormResize(e, edge));
			canvas.append(grip);
		}

		canvas.addEventListener('mousedown', onCanvasDown);
		canvas.addEventListener('dblclick', onCanvasDoubleClick);
		canvas.addEventListener('dragover', (e) => { if ([...e.dataTransfer.types].includes('text/netforms-type')) { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; } });
		canvas.addEventListener('drop', (e) => onToolboxDrop(e, canvas));
		frame.append(title, canvas);
		stage.append(frame);
		stage.style.minWidth = (v.width + 2) * state.zoom + 48 + 'px';
		stage.style.minHeight = (v.height + 32) * state.zoom + 48 + 'px';
		state.canvas = canvas;
	}

	function place(node, r) {
		node.style.left = r.x + 'px';
		node.style.top = r.y + 'px';
		node.style.width = r.w + 'px';
		node.style.height = r.h + 'px';
	}

	function renderParseError(stage) {
		const box = el('div', 'parse-error');
		box.append(el('h2', '', T('The designer cannot open this file')));
		const where = state.parseError.line ? T(' (line {0}, column {1})', state.parseError.line, state.parseError.column) : '';
		box.append(el('p', '', T('InitializeComponent contains something the designer does not interpret{0}. Fix it in the text editor; the designer reopens when the file is saved.', where)));
		box.append(el('pre', '', state.parseError.message));
		box.append(button(T('Open as text'), T('Open the file in the text editor'), () => post({ type: 'openAsText' })));
		stage.append(box);
	}

	/** Mouse position in form client coordinates (the canvas is scaled by the zoom). */
	function canvasPoint(e) {
		const r = state.canvas.getBoundingClientRect();
		return { x: Math.round((e.clientX - r.left) / state.zoom), y: Math.round((e.clientY - r.top) / state.zoom) };
	}

	function select(id, additive) {
		if (additive && id !== '') {
			const i = state.selection.indexOf(id);
			const without = state.selection.filter((s) => s !== '');
			if (i >= 0) state.selection = without.filter((s) => s !== id);
			else state.selection = without.concat([id]);
			if (!state.selection.length) state.selection = [''];
		} else {
			state.selection = [id];
		}
		state.props = null;
		state.events = null;
		refreshSelection();
		renderTray();
		renderToolbar();
		renderInspector();
		renderStatus();
		requestInspector();
	}

	/**
	 * Selection changes restyle the canvas in place rather than rebuilding it: a rebuilt canvas
	 * between the two clicks of a double-click would swallow the dblclick.
	 */
	function refreshSelection() {
		const canvas = state.canvas;
		if (!canvas || !canvas.isConnected) { renderStage(); return; }
		const primary = primaryId();
		canvas.querySelectorAll('.hit').forEach((n) => {
			n.classList.remove('selected', 'primary');
			if (state.selection.includes(n.dataset.id)) n.classList.add(n.dataset.id === primary ? 'primary' : 'selected');
		});
		canvas.querySelectorAll('.handle').forEach((n) => n.remove());
		drawHandles(canvas);
		const title = canvas.parentNode && canvas.parentNode.querySelector('.form-title');
		if (title) title.classList.toggle('selected', state.selection.includes(''));
	}

	function movableSelection() {
		return state.selection.filter((id) => { const it = itemById(id); return it && it.movable; });
	}

	// --- handles and badges -------------------------------------------------------------------------

	function drawHandles(canvas) {
		const id = primaryId();
		const it = itemById(id);
		if (!it || state.selection.length !== 1 || !it.resizable || !it.visible || state.locked) return;
		for (const edge of G.resizeEdges(it.dock)) {
			const h = el('div', 'handle primary');
			const x = edge.includes('w') ? it.x : edge.includes('e') ? it.x + it.w : it.x + it.w / 2;
			const y = edge.includes('n') ? it.y : edge.includes('s') ? it.y + it.h : it.y + it.h / 2;
			h.style.left = x + 'px';
			h.style.top = y + 'px';
			h.style.cursor = edge + '-resize';
			h.addEventListener('mousedown', (e) => startResize(e, it, edge));
			canvas.append(h);
		}
	}

	function drawTabBadges(canvas) {
		for (const it of state.view.items) {
			if (!it.visible || it.kind !== 'control' || it.tabIndex < 0) continue;
			const b = el('div', 'tab-badge', String(it.tabIndex));
			b.style.left = it.x + 'px';
			b.style.top = it.y + 'px';
			b.addEventListener('mousedown', (e) => {
				e.stopPropagation();
				// Click order becomes tab order among the siblings, as VS's Tab Order view.
				apply([{ op: 'setProp', id: it.id, prop: 'TabIndex', value: String(state.tabOrderNext++) }], state.selection);
			});
			canvas.append(b);
		}
	}

	// --- mouse: select, move, drop ------------------------------------------------------------------

	function onCanvasDown(e) {
		if (e.button !== 0 || state.busy || state.tabOrder) return;
		if (e.target.classList.contains('handle') || e.target.classList.contains('form-grip')) return;
		const p = canvasPoint(e);
		const hit = G.hitTest(state.view.items, p.x, p.y);
		const additive = e.ctrlKey || e.shiftKey;
		if (!hit) { select('', false); return; }
		if (additive) { select(hit.id, true); return; }
		if (!state.selection.includes(hit.id)) select(hit.id, false);
		if (state.locked) return;
		const ids = movableSelection();
		if (!ids.length || !ids.includes(hit.id)) return;
		// A group moves together; they must share a container (as the canvas can only drop into one).
		const parent = itemById(ids[0]).parent;
		const moving = ids.filter((id) => itemById(id).parent === parent);
		state.drag = { kind: 'move', start: p, ids: moving, moved: false, target: null, dx: 0, dy: 0 };
		e.preventDefault();
		window.addEventListener('mousemove', onDragMove);
		window.addEventListener('mouseup', onDragUp, { once: true });
	}

	function containerRect(parentId) {
		const v = state.view;
		if (!parentId) return { x: 0, y: 0, w: v.width, h: v.height, padding: [0, 0, 0, 0] };
		const p = itemById(parentId);
		return { x: p.clientX, y: p.clientY, w: p.w - (p.clientX - p.x), h: p.h - (p.clientY - p.y), padding: p.padding };
	}

	function onDragMove(e) {
		const d = state.drag;
		const p = canvasPoint(e);
		let dx = p.x - d.start.x, dy = p.y - d.start.y;
		if (!d.moved && Math.abs(dx) + Math.abs(dy) < 3) return;
		d.moved = true;
		const items = d.ids.map(itemById);
		const group = G.union(items);
		group.margin = items.length === 1 ? items[0].margin : [3, 3, 3, 3];
		const target = G.dropTarget(state.view.items, p.x, p.y, new Set(d.ids));
		const targetId = target ? target.id : '';
		const parentId = items[0].parent;
		const others = state.view.items.filter((i) => i.visible && i.kind === 'control' && i.parent === targetId && !d.ids.includes(i.id));
		const snapOn = !e.altKey && state.snap;
		const moved = { x: group.x + dx, y: group.y + dy, w: group.w, h: group.h, margin: group.margin };
		const snapped = G.snapMove(moved, others, containerRect(targetId), snapOn ? G.SNAP : 0);
		d.dx = snapped.rect.x - group.x;
		d.dy = snapped.rect.y - group.y;
		d.target = targetId !== parentId ? targetId : null;
		drawDragFeedback(items.map((i) => ({ x: i.x + d.dx, y: i.y + d.dy, w: i.w, h: i.h })), snapped.guides, target && d.target !== null ? target : null);
		renderStatus(`${items.length > 1 ? T('{0} controls', items.length) : items[0].id}: ${items[0].left + d.dx}, ${items[0].top + d.dy}`);
	}

	function drawDragFeedback(rects, guides, dropTarget) {
		const canvas = state.canvas;
		canvas.querySelectorAll('.ghost, .guide, .handle').forEach((n) => n.remove());
		canvas.querySelectorAll('.drop-target').forEach((n) => n.classList.remove('drop-target'));
		for (const r of rects) { const g = el('div', 'ghost'); place(g, r); canvas.append(g); }
		for (const l of guides) {
			const g = el('div', 'guide ' + l.axis + ' ' + l.kind);
			if (l.axis === 'x') { g.style.left = l.pos + 'px'; g.style.top = l.from + 'px'; g.style.height = (l.to - l.from) + 'px'; }
			else { g.style.top = l.pos + 'px'; g.style.left = l.from + 'px'; g.style.width = (l.to - l.from) + 'px'; }
			canvas.append(g);
		}
		if (dropTarget) { const n = canvas.querySelector(`.hit[data-id="${CSS.escape(dropTarget.id)}"]`); if (n) n.classList.add('drop-target'); }
	}

	function onDragUp() {
		window.removeEventListener('mousemove', onDragMove);
		const d = state.drag;
		state.drag = null;
		if (!d || !d.moved) return; // a plain click: nothing was drawn, keep the canvas (a double-click needs it)
		if (d.dx === 0 && d.dy === 0 && d.target === null) { renderStage(); return; }
		const ops = [];
		for (const id of d.ids) {
			const it = itemById(id);
			if (d.target !== null) {
				const c = containerRect(d.target);
				ops.push({ op: 'setParent', id, parent: d.target, x: it.x + d.dx - c.x, y: it.y + d.dy - c.y });
			} else {
				ops.push({ op: 'setBounds', id, x: it.left + d.dx, y: it.top + d.dy });
			}
		}
		apply(ops, state.selection);
	}

	function onToolboxDrop(e, canvas) {
		const type = e.dataTransfer.getData('text/netforms-type');
		if (!type) return;
		e.preventDefault();
		const p = canvasPoint(e);
		const target = G.dropTarget(state.view.items, p.x, p.y, new Set());
		const c = containerRect(target ? target.id : '');
		state.selectNew = new Set(allIds());
		apply([{ op: 'add', type, parent: target ? target.id : '', x: Math.max(0, p.x - c.x), y: Math.max(0, p.y - c.y) }]);
	}

	/** Double-click: wire the component's default event (Click, Load, CheckedChanged…) and go to it, as VS does. */
	function onCanvasDoubleClick(e) {
		const p = canvasPoint(e);
		const hit = G.hitTest(state.view.items, p.x, p.y);
		wireDefaultEvent(hit ? hit.id : '');
	}

	function wireDefaultEvent(id) {
		state.pendingDefaultEvent = id;
		post({ type: 'events', id });
	}

	// --- resizing -----------------------------------------------------------------------------------

	function startResize(e, it, edges) {
		if (e.button !== 0 || state.busy || state.locked) return;
		e.stopPropagation();
		e.preventDefault();
		state.drag = { kind: 'resize', item: it, edges, start: canvasPoint(e), rect: { x: it.x, y: it.y, w: it.w, h: it.h, margin: it.margin } };
		window.addEventListener('mousemove', onResizeMove);
		window.addEventListener('mouseup', onResizeUp, { once: true });
	}

	function onResizeMove(e) {
		const d = state.drag;
		const p = canvasPoint(e);
		const raw = G.resize({ x: d.item.x, y: d.item.y, w: d.item.w, h: d.item.h, margin: d.item.margin }, d.edges, p.x - d.start.x, p.y - d.start.y);
		const others = state.view.items.filter((i) => i.visible && i.kind === 'control' && i.parent === d.item.parent && i.id !== d.item.id);
		const snapped = G.snapResize(raw, d.edges, others, containerRect(d.item.parent), e.altKey || !state.snap ? 0 : G.SNAP);
		d.rect = snapped.rect;
		drawDragFeedback([d.rect], snapped.guides, null);
		renderStatus(`${d.item.id}: ${d.rect.w} × ${d.rect.h}`);
	}

	function onResizeUp() {
		window.removeEventListener('mousemove', onResizeMove);
		const d = state.drag;
		state.drag = null;
		const it = d.item, r = d.rect;
		if (r.x === it.x && r.y === it.y && r.w === it.w && r.h === it.h) { renderStage(); return; }
		apply([{ op: 'setBounds', id: it.id, x: it.left + (r.x - it.x), y: it.top + (r.y - it.y), w: r.w, h: r.h }], state.selection);
	}

	function startFormResize(e, edge) {
		if (e.button !== 0 || state.busy || state.locked) return;
		e.stopPropagation();
		e.preventDefault();
		const v = state.view;
		state.drag = { kind: 'form', edge, start: canvasPoint(e), w: v.width, h: v.height };
		const move = (ev) => {
			const p = canvasPoint(ev);
			const d = state.drag;
			d.w = edge === 's' ? v.width : Math.max(40, v.width + p.x - d.start.x);
			d.h = edge === 'e' ? v.height : Math.max(20, v.height + p.y - d.start.y);
			const canvas = state.canvas;
			canvas.querySelectorAll('.ghost').forEach((n) => n.remove());
			const g = el('div', 'ghost');
			place(g, { x: 0, y: 0, w: d.w, h: d.h });
			canvas.append(g);
			renderStatus(`${v.className}: ${d.w} × ${d.h}`);
		};
		window.addEventListener('mousemove', move);
		window.addEventListener('mouseup', () => {
			window.removeEventListener('mousemove', move);
			const d = state.drag;
			state.drag = null;
			if (d.w === v.width && d.h === v.height) { renderStage(); return; }
			apply([{ op: 'setForm', w: d.w, h: d.h }], state.selection);
		}, { once: true });
	}

	// --- keyboard -----------------------------------------------------------------------------------

	function isTyping(t) { return t && (t.tagName === 'INPUT' || t.tagName === 'SELECT' || t.tagName === 'TEXTAREA'); }

	document.addEventListener('keydown', (e) => {
		if (isTyping(e.target) || !state.view) return;
		const ctrl = e.ctrlKey || e.metaKey;
		if (ctrl && e.key.toLowerCase() === 'z' && !e.shiftKey) { post({ type: 'undo' }); e.preventDefault(); return; }
		if (ctrl && (e.key.toLowerCase() === 'y' || (e.key.toLowerCase() === 'z' && e.shiftKey))) { post({ type: 'redo' }); e.preventDefault(); return; }
		if (e.key === 'F7') { post({ type: 'viewCode' }); e.preventDefault(); return; }
		if (state.busy) return;
		if (e.key === 'Delete') {
			const ids = state.selection.filter((id) => id !== '' && (itemById(id)?.kind !== 'nested'));
			if (ids.length) apply(ids.map((id) => ({ op: 'remove', id })), ['']);
			e.preventDefault();
			return;
		}
		if (e.key === 'Escape') {
			// Up one level, as in VS: the container of the selection, then the form.
			const it = itemById(primaryId());
			select(it ? it.parent : '', false);
			e.preventDefault();
			return;
		}
		const arrows = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
		if (arrows[e.key] && !state.locked) {
			const [ax, ay] = arrows[e.key];
			const step = ctrl ? 8 : 1;
			const ops = [];
			if (e.shiftKey) {
				const it = itemById(primaryId());
				if (it && it.resizable) ops.push({ op: 'setBounds', id: it.id, w: Math.max(1, it.w + ax * step), h: Math.max(1, it.h + ay * step) });
			} else {
				for (const id of movableSelection()) { const it = itemById(id); ops.push({ op: 'setBounds', id, x: it.left + ax * step, y: it.top + ay * step }); }
			}
			apply(ops, state.selection);
			e.preventDefault();
		}
	});

	// --- tray ---------------------------------------------------------------------------------------

	function renderTray() {
		const tray = $('tray');
		tray.textContent = '';
		if (!state.view) return;
		for (const t of state.view.tray) {
			const n = el('div', 'tray-item' + (state.selection.includes(t.id) ? ' selected' : ''), `${t.id} : ${shortType(t.type)}`);
			n.addEventListener('mousedown', (e) => select(t.id, e.ctrlKey || e.shiftKey));
			n.addEventListener('dblclick', () => wireDefaultEvent(t.id));
			tray.append(n);
		}
	}

	// --- inspector: properties and events -------------------------------------------------------------

	function renderInspector() {
		const box = $('inspector');
		box.textContent = '';
		if (!state.view || state.parseError) return;
		const id = primaryId();
		const it = itemById(id), tr = trayById(id);
		const head = el('div', 'insp-head');
		head.append(el('div', 'target', id === '' ? state.view.className : id));
		head.append(el('div', 'target-type', id === '' ? state.view.rootType : (it || tr) ? (it || tr).type : ''));
		box.append(head);

		if (state.selection.length > 1) box.append(alignBar());

		const tabs = el('div', 'insp-tabs');
		for (const [key, label] of [['properties', T('Properties')], ['events', T('Events')]]) {
			const b = el('button', state.tab === key ? 'on' : '', label);
			b.addEventListener('click', () => { state.tab = key; remember(); renderInspector(); requestInspector(); });
			tabs.append(b);
		}
		box.append(tabs);

		const tools = el('div', 'insp-tools');
		const search = el('input');
		search.placeholder = T('Search');
		search.value = state.inspectorFilter || '';
		search.addEventListener('input', () => { state.inspectorFilter = search.value.toLowerCase(); renderRows(body); });
		tools.append(search);
		box.append(tools);

		const body = el('div', 'insp-body');
		box.append(body);
		const help = el('div', 'insp-help');
		help.id = 'help';
		box.append(help);
		renderRows(body);
	}

	function renderRows(body) {
		body.textContent = '';
		const data = state.tab === 'events' ? state.events : state.props;
		if (!data) { body.append(el('div', 'row', T('Loading…'))); return; }
		const filter = state.inspectorFilter || '';
		const byCat = new Map();
		for (const r of data.rows) {
			if (filter && !r.name.toLowerCase().includes(filter)) continue;
			if (!byCat.has(r.category)) byCat.set(r.category, []);
			byCat.get(r.category).push(r);
		}
		for (const [cat, rows] of byCat) {
			const c = el('div', 'cat' + (state.collapsed.has(cat) && !filter ? ' collapsed' : ''));
			const h = el('div', 'cat-head', T(cat));
			h.addEventListener('click', () => {
				if (state.collapsed.has(cat)) state.collapsed.delete(cat); else state.collapsed.add(cat);
				remember();
				c.classList.toggle('collapsed');
			});
			const list = el('div', 'rows');
			for (const r of rows) list.append(state.tab === 'events' ? eventRow(data.id, r) : propertyRow(data.id, r));
			c.append(h, list);
			body.append(c);
		}
	}

	function showHelp(name, text) {
		const help = $('help');
		if (!help) return;
		help.textContent = '';
		help.append(el('b', '', name), el('span', '', text || ''));
	}

	function propertyRow(id, r) {
		const row = el('div', 'row');
		const name = el('div', 'name' + (r.modified ? ' modified' : ''), r.name);
		name.title = r.description;
		const value = el('div', 'value');
		row.append(name, value);
		row.addEventListener('mouseenter', () => showHelp(r.name, r.description));
		const commit = (v) => { if (v !== r.value) apply([{ op: 'setProp', id, prop: r.name, value: v }], state.selection); };

		switch (r.editor) {
			case 'bool':
			case 'enum':
			case 'component': {
				const s = el('select');
				const options = r.editor === 'bool' ? ['True', 'False'] : r.options || [];
				for (const o of options) { const opt = el('option', '', o === '' ? '(none)' : o); opt.value = o; s.append(opt); }
				s.value = r.value ?? '';
				s.addEventListener('change', () => commit(s.value));
				value.append(s);
				break;
			}
			case 'flags': {
				const input = textInput(r.value, commit);
				const more = el('button', 'mini', '▾');
				more.title = 'Choose flags';
				const panel = el('div', 'flags');
				panel.style.display = 'none';
				const set = new Set((r.value || '').split(',').map((s) => s.trim()).filter(Boolean));
				for (const o of r.options || []) {
					const label = el('label');
					const cb = el('input');
					cb.type = 'checkbox';
					cb.checked = set.has(o);
					cb.addEventListener('change', () => {
						if (cb.checked) set.add(o); else set.delete(o);
						const list = (r.options || []).filter((x) => set.has(x) && (x !== 'None' || set.size === 1));
						commit(list.length ? list.join(', ') : (r.options || []).includes('None') ? 'None' : '0');
					});
					label.append(cb, document.createTextNode(' ' + o));
					panel.append(label);
				}
				more.addEventListener('click', () => { panel.style.display = panel.style.display === 'none' ? '' : 'none'; });
				value.append(input, more);
				queueMicrotask(() => row.parentNode && row.parentNode.insertBefore(panel, row.nextSibling));
				break;
			}
			case 'color': {
				const sw = el('span', 'swatch');
				sw.style.background = cssColor(r.value);
				const input = textInput(r.value, commit);
				if (r.options) {
					const listId = 'colors-' + r.name;
					const dl = el('datalist');
					dl.id = listId;
					for (const o of r.options) { const opt = el('option'); opt.value = o; dl.append(opt); }
					input.setAttribute('list', listId);
					value.append(dl);
				}
				value.prepend(sw);
				value.append(input);
				break;
			}
			case 'collection': {
				const input = textInput(r.value, () => undefined);
				input.readOnly = true;
				const edit = el('button', 'mini', '…');
				edit.title = T('Edit items, one per line');
				edit.addEventListener('click', () => editCollection(id, r, row));
				value.append(input, edit);
				break;
			}
			case 'readonly': {
				const input = textInput(r.value, () => undefined);
				input.readOnly = true;
				value.append(input);
				break;
			}
			default:
				value.append(textInput(r.value, commit));
		}
		if (r.modified && r.name !== 'Name' && r.editor !== 'readonly' && r.editor !== 'collection') {
			const reset = el('button', 'mini', '↺');
			reset.title = T('Reset to the default');
			reset.addEventListener('click', () => apply([{ op: 'resetProp', id, prop: r.name }], state.selection));
			value.append(reset);
		}
		return row;
	}

	function textInput(val, commit) {
		const input = el('input');
		input.type = 'text';
		input.value = val ?? '';
		input.addEventListener('keydown', (e) => {
			if (e.key === 'Enter') { input.blur(); e.preventDefault(); }
			if (e.key === 'Escape') { input.value = val ?? ''; input.blur(); }
		});
		input.addEventListener('change', () => commit(input.value));
		return input;
	}

	function editCollection(id, r, row) {
		const area = el('textarea');
		area.value = (r.items || []).join('\n');
		area.rows = Math.min(12, Math.max(4, (r.items || []).length + 1));
		area.style.width = '100%';
		area.title = r.itemsFormat === 'tree' ? T('One node per line; indent a child two spaces under its parent (Tab / Shift+Tab)')
			: r.itemsFormat === 'columns' ? T('One item per line; separate the sub-items with |')
			: T('Edit items, one per line');
		area.placeholder = area.title;
		if (r.itemsFormat === 'tree') {
			// Tab and Shift+Tab indent and outdent the current line, as the levels of the tree.
			area.addEventListener('keydown', (e) => {
				if (e.key !== 'Tab') return;
				e.preventDefault();
				const start = area.value.lastIndexOf('\n', area.selectionStart - 1) + 1;
				const caret = area.selectionStart;
				if (e.shiftKey) {
					if (area.value.startsWith('  ', start)) {
						area.value = area.value.slice(0, start) + area.value.slice(start + 2);
						area.selectionStart = area.selectionEnd = Math.max(start, caret - 2);
					}
				} else {
					area.value = area.value.slice(0, start) + '  ' + area.value.slice(start);
					area.selectionStart = area.selectionEnd = caret + 2;
				}
			});
		}
		const ok = button('OK', T('Replace the items'), () => {
			const values = area.value.split(/\r?\n/).filter((l, i, a) => !(i === a.length - 1 && l === ''));
			apply([{ op: 'setItems', id, prop: r.name, values }], state.selection);
		});
		const box = el('div');
		box.append(area, ok);
		row.after(box);
		area.focus();
	}

	function cssColor(text) {
		if (!text) return 'transparent';
		const parts = text.split(',').map((s) => s.trim());
		if (parts.length === 3 && parts.every((p) => /^\d+$/.test(p))) return `rgb(${parts.join(',')})`;
		if (parts.length === 4 && parts.every((p) => /^\d+$/.test(p))) return `rgba(${parts[1]},${parts[2]},${parts[3]},${parts[0] / 255})`;
		return text.replace(/\s+/g, '').toLowerCase();
	}

	function eventRow(id, r) {
		const row = el('div', 'row');
		const name = el('div', 'name' + (r.handler ? ' modified' : ''), r.name);
		name.title = `${r.description}\n(${r.parameters})`;
		row.addEventListener('mouseenter', () => showHelp(r.name, `${r.description} ${T('Handler')}: void (${r.parameters})`));
		const value = el('div', 'value');
		const input = textInput(r.handler || '', (v) => apply([{ op: 'setEvent', id, event: r.name, handler: v.trim() }], state.selection));
		input.placeholder = '';
		input.addEventListener('dblclick', () => {
			if (r.handler) post({ type: 'gotoHandler', handler: r.handler });
			else apply([{ op: 'setEvent', id, event: r.name, handler: r.defaultHandler }], state.selection);
		});
		value.append(input);
		if (r.handler) {
			const go = el('button', 'mini', '→');
			go.title = T('Go to the handler');
			go.addEventListener('click', () => post({ type: 'gotoHandler', handler: r.handler }));
			value.append(go);
		}
		name.addEventListener('dblclick', () => {
			if (r.handler) post({ type: 'gotoHandler', handler: r.handler });
			else apply([{ op: 'setEvent', id, event: r.name, handler: r.defaultHandler }], state.selection);
		});
		row.append(name, value);
		return row;
	}

	// Default-event double-clicks arrive as an events answer; finish the gesture here.
	window.addEventListener('message', (e) => {
		const m = e.data;
		if (m.type !== 'events' || state.pendingDefaultEvent === undefined || m.id !== state.pendingDefaultEvent) return;
		const id = state.pendingDefaultEvent;
		state.pendingDefaultEvent = undefined;
		const def = m.rows.find((r) => r.isDefault);
		if (!def) return;
		if (def.handler) post({ type: 'gotoHandler', handler: def.handler });
		else apply([{ op: 'setEvent', id, event: def.name, handler: def.defaultHandler }], state.selection);
	});

	// --- Format: alignment of a multiple selection --------------------------------------------------

	function alignBar() {
		const bar = el('div', 'align-bar');
		const cmds = [
			['⇤', 'left', T('Align lefts')], ['↔', 'center', T('Align centers')], ['⇥', 'right', T('Align rights')],
			['⤒', 'top', T('Align tops')], ['↕', 'middle', T('Align middles')], ['⤓', 'bottom', T('Align bottoms')],
			['W', 'sameWidth', T('Make same width')], ['H', 'sameHeight', T('Make same height')], ['▣', 'sameSize', T('Make same size')],
			['|‥|', 'spaceX', T('Make horizontal spacing equal')], ['≡', 'spaceY', T('Make vertical spacing equal')],
		];
		for (const [label, how, title] of cmds) bar.append(button(label, title + ' ' + T('(to the last selected)'), () => alignSelection(how)));
		return bar;
	}

	function alignSelection(how) {
		const ids = movableSelection();
		if (ids.length < 2) return;
		const rects = {};
		for (const id of ids) { const it = itemById(id); rects[id] = { x: it.x, y: it.y, w: it.w, h: it.h }; }
		const primary = ids.includes(primaryId()) ? primaryId() : ids[ids.length - 1];
		const out = how === 'spaceX' ? G.spaceEvenly(rects, 'x') : how === 'spaceY' ? G.spaceEvenly(rects, 'y') : G.align(rects, primary, how);
		const ops = [];
		for (const id of ids) {
			const it = itemById(id), r = out[id];
			if (r.x === it.x && r.y === it.y && r.w === it.w && r.h === it.h) continue;
			ops.push({ op: 'setBounds', id, x: it.left + (r.x - it.x), y: it.top + (r.y - it.y), w: r.w, h: r.h });
		}
		apply(ops, state.selection);
	}

	// --- status and toasts --------------------------------------------------------------------------

	function renderStatus(text) {
		const s = $('status');
		if (text) { s.textContent = text; return; }
		const v = state.view;
		if (!v) { s.textContent = ''; return; }
		const it = itemById(primaryId());
		s.textContent = it
			? `${it.id} (${shortType(it.type)})  ·  ${it.left}, ${it.top}  ·  ${it.w} × ${it.h}${it.dock !== 'None' ? '  ·  Dock ' + it.dock : ''}`
			: `${v.className}  ·  ${v.width} × ${v.height}` + (state.selection.length > 1 ? '  ·  ' + T('{0} selected', state.selection.length) : '');
	}

	let toastTimer;
	function toast(message, kind) {
		document.querySelectorAll('.toast').forEach((n) => n.remove());
		const t = el('div', 'toast ' + (kind || ''), message);
		document.body.append(t);
		clearTimeout(toastTimer);
		toastTimer = setTimeout(() => t.remove(), kind === 'error' ? 8000 : 3000);
		t.addEventListener('click', () => t.remove());
	}

	renderAll();
	post({ type: 'ready' });
})();
