// Pure geometry of the designer canvas: snap lines, alignment, hit-testing. No DOM, no state -
// loaded by the webview as a script and by `node --test` as a module, so the math the user feels
// under the mouse is the math the tests check (gate of phase 5.4, docs/PLAN.md).
//
// A rect is {x, y, w, h} in canvas pixels. A neighbour for snapping also carries `margin`
// ([left, top, right, bottom], the control's Margin) so edges can snap at the spacing WinForms
// layout would use.
(function (root, factory) {
	if (typeof module === 'object' && module.exports) module.exports = factory();
	else root.NetFormsGeometry = factory();
})(typeof self !== 'undefined' ? self : this, function () {
	'use strict';

	/** Pixels within which an edge is pulled onto a snap line, as in the VS designer. */
	const SNAP = 6;

	const right = (r) => r.x + r.w;
	const bottom = (r) => r.y + r.h;
	const centerX = (r) => r.x + Math.floor(r.w / 2);
	const centerY = (r) => r.y + Math.floor(r.h / 2);
	const margin = (r) => r.margin || [0, 0, 0, 0];

	/**
	 * Where the moving rect's edges could go along one axis, from one neighbour: the neighbour's
	 * edges (aligned), its centre (centred), and just past it at the two margins' distance (spaced).
	 * Each candidate says which edge of the moving rect it is for.
	 */
	function candidates(axis, moving, other) {
		const m = margin(moving), o = margin(other);
		if (axis === 'x') {
			return [
				{ edge: 'start', pos: other.x, kind: 'align' },
				{ edge: 'end', pos: right(other), kind: 'align' },
				{ edge: 'center', pos: centerX(other), kind: 'center' },
				{ edge: 'start', pos: right(other) + o[2] + m[0], kind: 'margin' },
				{ edge: 'end', pos: other.x - o[0] - m[2], kind: 'margin' },
			];
		}
		return [
			{ edge: 'start', pos: other.y, kind: 'align' },
			{ edge: 'end', pos: bottom(other), kind: 'align' },
			{ edge: 'center', pos: centerY(other), kind: 'center' },
			{ edge: 'start', pos: bottom(other) + o[3] + m[1], kind: 'margin' },
			{ edge: 'end', pos: other.y - o[1] - m[3], kind: 'margin' },
		];
	}

	function edgeValue(axis, rect, edge) {
		if (axis === 'x') return edge === 'start' ? rect.x : edge === 'end' ? right(rect) : centerX(rect);
		return edge === 'start' ? rect.y : edge === 'end' ? bottom(rect) : centerY(rect);
	}

	/**
	 * The container's inner edges: its client area inset by its Padding and the moving control's
	 * Margin - where a control lands when pushed against the side of a panel or the form.
	 */
	function containerCandidates(axis, moving, container) {
		if (!container) return [];
		const m = margin(moving), p = container.padding || [0, 0, 0, 0];
		return axis === 'x'
			? [{ edge: 'start', pos: container.x + p[0] + m[0], kind: 'margin' }, { edge: 'end', pos: right(container) - p[2] - m[2], kind: 'margin' }]
			: [{ edge: 'start', pos: container.y + p[1] + m[1], kind: 'margin' }, { edge: 'end', pos: bottom(container) - p[3] - m[3], kind: 'margin' }];
	}

	/** The best (nearest) snap along one axis, or null. */
	function bestSnap(axis, moving, others, container, threshold) {
		let best = null;
		const all = [];
		for (const o of others) for (const c of candidates(axis, moving, o)) all.push({ c, o });
		for (const c of containerCandidates(axis, moving, container)) all.push({ c, o: null });
		for (const { c, o } of all) {
			const d = c.pos - edgeValue(axis, moving, c.edge);
			if (Math.abs(d) > threshold) continue;
			if (!best || Math.abs(d) < Math.abs(best.delta) || (Math.abs(d) === Math.abs(best.delta) && c.kind === 'align' && best.kind !== 'align')) {
				best = { delta: d, pos: c.pos, kind: c.kind, other: o };
			}
		}
		return best;
	}

	/** A guide line to draw for a snap: across the moving rect and the neighbour it snapped to. */
	function guide(axis, snap, moved) {
		const o = snap.other || moved;
		if (axis === 'x') {
			const y1 = Math.min(moved.y, o.y), y2 = Math.max(bottom(moved), bottom(o));
			return { axis: 'x', pos: snap.pos, from: y1, to: y2, kind: snap.kind };
		}
		const x1 = Math.min(moved.x, o.x), x2 = Math.max(right(moved), right(o));
		return { axis: 'y', pos: snap.pos, from: x1, to: x2, kind: snap.kind };
	}

	/**
	 * Snaps a rect being dragged: returns the adjusted rect and the guide lines. `others` are the
	 * siblings (same container, not moving); `container` is the container's client rect with its
	 * padding. Holding Alt in the designer bypasses this (threshold 0).
	 */
	function snapMove(moving, others, container, threshold) {
		const t = threshold === undefined ? SNAP : threshold;
		const sx = bestSnap('x', moving, others, container, t);
		const sy = bestSnap('y', moving, others, container, t);
		const rect = { x: moving.x + (sx ? sx.delta : 0), y: moving.y + (sy ? sy.delta : 0), w: moving.w, h: moving.h, margin: moving.margin };
		const guides = [];
		if (sx) guides.push(guide('x', sx, rect));
		if (sy) guides.push(guide('y', sy, rect));
		return { rect, guides };
	}

	/**
	 * Snaps a rect being resized by the edges in `edges` ('n', 's', 'e', 'w' letters): only the
	 * moving edges snap, and never below a 1px size.
	 */
	function snapResize(rect, edges, others, container, threshold) {
		const t = threshold === undefined ? SNAP : threshold;
		const r = { x: rect.x, y: rect.y, w: rect.w, h: rect.h, margin: rect.margin };
		const guides = [];
		const pick = (axis, edge) => {
			let best = null;
			const all = [];
			for (const o of others) for (const c of candidates(axis, r, o)) if (c.edge === edge) all.push({ c, o });
			for (const c of containerCandidates(axis, r, container)) if (c.edge === edge) all.push({ c, o: null });
			for (const { c, o } of all) {
				const d = c.pos - edgeValue(axis, r, edge);
				if (Math.abs(d) <= t && (!best || Math.abs(d) < Math.abs(best.delta))) best = { delta: d, pos: c.pos, kind: c.kind, other: o };
			}
			return best;
		};
		if (edges.includes('e')) { const s = pick('x', 'end'); if (s) { r.w = Math.max(1, r.w + s.delta); guides.push(guide('x', s, r)); } }
		if (edges.includes('w')) { const s = pick('x', 'start'); if (s && r.w - s.delta >= 1) { r.x += s.delta; r.w -= s.delta; guides.push(guide('x', s, r)); } }
		if (edges.includes('s')) { const s = pick('y', 'end'); if (s) { r.h = Math.max(1, r.h + s.delta); guides.push(guide('y', s, r)); } }
		if (edges.includes('n')) { const s = pick('y', 'start'); if (s && r.h - s.delta >= 1) { r.y += s.delta; r.h -= s.delta; guides.push(guide('y', s, r)); } }
		return { rect: r, guides };
	}

	/** Applies a resize drag of (dx, dy) on the given edges to a rect; never below 1px. */
	function resize(rect, edges, dx, dy) {
		let { x, y, w, h } = rect;
		if (edges.includes('e')) w = Math.max(1, w + dx);
		if (edges.includes('s')) h = Math.max(1, h + dy);
		if (edges.includes('w')) { const nw = Math.max(1, w - dx); x += w - nw; w = nw; }
		if (edges.includes('n')) { const nh = Math.max(1, h - dy); y += h - nh; h = nh; }
		return { x, y, w, h, margin: rect.margin };
	}

	/**
	 * The Format menu of the VS designer: every rect is lined up with the primary one - the last
	 * selected, as VS does. `how` is one of left, center, right, top, middle, bottom, sameWidth,
	 * sameHeight, sameSize. Returns new rects by id; the primary is unchanged.
	 */
	function align(rects, primaryId, how) {
		const p = rects[primaryId];
		const out = {};
		for (const id of Object.keys(rects)) {
			const r = rects[id];
			const n = { x: r.x, y: r.y, w: r.w, h: r.h };
			if (id !== primaryId) {
				switch (how) {
					case 'left': n.x = p.x; break;
					case 'right': n.x = right(p) - r.w; break;
					case 'center': n.x = centerX(p) - Math.floor(r.w / 2); break;
					case 'top': n.y = p.y; break;
					case 'bottom': n.y = bottom(p) - r.h; break;
					case 'middle': n.y = centerY(p) - Math.floor(r.h / 2); break;
					case 'sameWidth': n.w = p.w; break;
					case 'sameHeight': n.h = p.h; break;
					case 'sameSize': n.w = p.w; n.h = p.h; break;
					default: throw new Error('unknown alignment ' + how);
				}
			}
			out[id] = n;
		}
		return out;
	}

	/**
	 * Spacing commands: make the gaps between the rects equal along one axis ('x' or 'y'), keeping
	 * the first and the last where they are (Format ▸ Horizontal/Vertical Spacing ▸ Make Equal).
	 */
	function spaceEvenly(rects, axis) {
		const ids = Object.keys(rects);
		const out = {};
		for (const id of ids) out[id] = { ...rects[id] };
		if (ids.length < 3) return out;
		const key = axis === 'x' ? 'x' : 'y', size = axis === 'x' ? 'w' : 'h';
		ids.sort((a, b) => rects[a][key] - rects[b][key]);
		const first = rects[ids[0]], last = rects[ids[ids.length - 1]];
		const total = ids.reduce((s, id) => s + rects[id][size], 0);
		const gap = (last[key] + last[size] - first[key] - total) / (ids.length - 1);
		let pos = first[key];
		for (const id of ids) {
			out[id][key] = Math.round(pos);
			pos += rects[id][size] + gap;
		}
		return out;
	}

	/**
	 * The item under a canvas point: the last visible one containing it (items come in paint order,
	 * parents before children, so the last hit is the front-most and innermost). `skip` excludes ids.
	 */
	function hitTest(items, x, y, skip) {
		for (let i = items.length - 1; i >= 0; i--) {
			const it = items[i];
			if (!it.visible || (skip && skip.has(it.id))) continue;
			if (x >= it.x && y >= it.y && x < it.x + it.w && y < it.y + it.h) return it;
		}
		return null;
	}

	/** The deepest container under a point that can take a drop (not one of the moving ids nor inside them). */
	function dropTarget(items, x, y, moving) {
		const inside = (id) => {
			for (let cur = byId(items, id); cur; cur = byId(items, cur.parent)) if (moving.has(cur.id)) return true;
			return false;
		};
		for (let i = items.length - 1; i >= 0; i--) {
			const it = items[i];
			if (!it.visible || !it.container || inside(it.id)) continue;
			if (x >= it.x && y >= it.y && x < it.x + it.w && y < it.y + it.h) return it;
		}
		return null;
	}

	function byId(items, id) {
		if (!id) return null;
		for (const it of items) if (it.id === id) return it;
		return null;
	}

	/**
	 * The edges a control's size can be pulled by: all eight, or for a docked control only the one that
	 * is not against its container (a Top-docked strip grows downwards), as the WinForms designer does.
	 */
	function resizeEdges(dock) {
		switch (dock) {
			case 'Top': return ['s'];
			case 'Bottom': return ['n'];
			case 'Left': return ['e'];
			case 'Right': return ['w'];
			case 'Fill': return [];
			default: return ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'];
		}
	}

	/** The union of rects, for moving a group. */
	function union(rects) {
		let x1 = Infinity, y1 = Infinity, x2 = -Infinity, y2 = -Infinity;
		for (const r of rects) { x1 = Math.min(x1, r.x); y1 = Math.min(y1, r.y); x2 = Math.max(x2, r.x + r.w); y2 = Math.max(y2, r.y + r.h); }
		return { x: x1, y: y1, w: x2 - x1, h: y2 - y1 };
	}

	return { SNAP, snapMove, snapResize, resize, align, spaceEvenly, hitTest, dropTarget, union, resizeEdges };
});
