// node --test: the snap and alignment math of the canvas (gate of phase 5.4).
const test = require('node:test');
const assert = require('node:assert/strict');
const g = require('../media/geometry.js');

const button = (x, y, w = 75, h = 23) => ({ x, y, w, h, margin: [3, 3, 3, 3] });

test('a left edge within the threshold snaps onto a neighbour\'s left edge', () => {
	const { rect, guides } = g.snapMove(button(14, 80), [button(12, 20)], null);
	assert.equal(rect.x, 12);
	assert.equal(rect.y, 80);
	assert.equal(guides.length, 1);
	assert.equal(guides[0].axis, 'x');
	assert.equal(guides[0].pos, 12);
	assert.equal(guides[0].kind, 'align');
	// The guide spans both controls.
	assert.equal(guides[0].from, 20);
	assert.equal(guides[0].to, 103);
});

test('beyond the threshold nothing snaps', () => {
	const { rect, guides } = g.snapMove(button(30, 80), [button(12, 20)], null);
	assert.equal(rect.x, 30);
	assert.equal(guides.length, 0);
});

test('side by side, the gap snaps to both margins (3 + 3)', () => {
	// Neighbour's right edge is 87; with margins the next control belongs at 93.
	const { rect, guides } = g.snapMove(button(95, 20), [button(12, 20)], null);
	assert.equal(rect.x, 93);
	assert.ok(guides.some((l) => l.axis === 'x' && l.kind === 'margin'));
	// And the tops line up at the same time.
	assert.equal(rect.y, 20);
});

test('centres snap to centres', () => {
	const wide = { x: 10, y: 10, w: 200, h: 23, margin: [3, 3, 3, 3] };
	const moving = button(70, 60); // centre 107, wide's centre 110
	const { rect, guides } = g.snapMove(moving, [wide], null);
	assert.equal(rect.x + Math.floor(rect.w / 2), 110);
	assert.ok(guides.some((l) => l.kind === 'center'));
});

test('a control pushed against the container snaps inside its padding and margin', () => {
	const container = { x: 0, y: 0, w: 300, h: 200, padding: [0, 0, 0, 0] };
	const { rect } = g.snapMove(button(5, 7), [], container);
	assert.equal(rect.x, 3);
	assert.equal(rect.y, 3);
	const far = g.snapMove(button(300 - 75 - 1, 150), [], container);
	assert.equal(far.rect.x + far.rect.w, 297);
});

test('the nearest snap wins, and Alt (threshold 0) disables snapping', () => {
	const others = [button(12, 20), button(16, 60)];
	assert.equal(g.snapMove(button(15, 100), others, null).rect.x, 16);
	assert.equal(g.snapMove(button(15, 100), others, null, 0).rect.x, 15);
});

test('resizing snaps only the moving edge', () => {
	const other = button(12, 20); // right = 87
	const { rect } = g.snapResize({ x: 12, y: 60, w: 73, h: 23, margin: [3, 3, 3, 3] }, 'e', [other], null);
	assert.equal(rect.x, 12);
	assert.equal(rect.w, 75);
	const west = g.snapResize({ x: 14, y: 60, w: 70, h: 23 }, 'w', [other], null);
	assert.equal(west.rect.x, 12);
	assert.equal(west.rect.x + west.rect.w, 84);
});

test('resize never goes below 1px and keeps the opposite edge', () => {
	assert.deepEqual(g.resize({ x: 10, y: 10, w: 20, h: 20 }, 'se', 5, -30), { x: 10, y: 10, w: 25, h: 1, margin: undefined });
	const nw = g.resize({ x: 10, y: 10, w: 20, h: 20 }, 'nw', 5, 5);
	assert.equal(nw.x + nw.w, 30);
	assert.equal(nw.y + nw.h, 30);
});

test('alignment lines everything up with the primary (the last selected)', () => {
	const rects = { a: { x: 10, y: 10, w: 50, h: 20 }, b: { x: 40, y: 50, w: 100, h: 30 }, p: { x: 20, y: 90, w: 80, h: 40 } };
	assert.deepEqual(g.align(rects, 'p', 'left').a, { x: 20, y: 10, w: 50, h: 20 });
	assert.equal(g.align(rects, 'p', 'right').b.x, 0);
	assert.equal(g.align(rects, 'p', 'center').a.x, 35);
	assert.equal(g.align(rects, 'p', 'bottom').a.y, 110);
	assert.equal(g.align(rects, 'p', 'middle').b.y, 95);
	assert.deepEqual(g.align(rects, 'p', 'sameSize').b, { x: 40, y: 50, w: 80, h: 40 });
	assert.deepEqual(g.align(rects, 'p', 'top').p, rects.p);
});

test('equal spacing keeps the ends and evens the gaps', () => {
	const rects = { a: { x: 0, y: 0, w: 10, h: 10 }, b: { x: 13, y: 0, w: 10, h: 10 }, c: { x: 60, y: 0, w: 10, h: 10 } };
	const out = g.spaceEvenly(rects, 'x');
	assert.equal(out.a.x, 0);
	assert.equal(out.b.x, 30);
	assert.equal(out.c.x, 60);
});

test('hit-testing takes the front-most, innermost visible item', () => {
	const items = [
		{ id: 'panel1', x: 0, y: 0, w: 100, h: 100, visible: true, container: true, parent: '' },
		{ id: 'button1', x: 10, y: 10, w: 50, h: 20, visible: true, parent: 'panel1' },
		{ id: 'hidden', x: 10, y: 10, w: 50, h: 20, visible: false, parent: 'panel1' },
	];
	assert.equal(g.hitTest(items, 20, 15).id, 'button1');
	assert.equal(g.hitTest(items, 80, 80).id, 'panel1');
	assert.equal(g.hitTest(items, 200, 200), null);
	assert.equal(g.hitTest(items, 20, 15, new Set(['button1'])).id, 'panel1');
});

test('a drop never lands inside what is being moved', () => {
	const items = [
		{ id: 'panel1', x: 0, y: 0, w: 100, h: 100, visible: true, container: true, parent: '' },
		{ id: 'panel2', x: 10, y: 10, w: 50, h: 50, visible: true, container: true, parent: 'panel1' },
	];
	assert.equal(g.dropTarget(items, 20, 20, new Set()).id, 'panel2');
	assert.equal(g.dropTarget(items, 20, 20, new Set(['panel2'])).id, 'panel1');
	assert.equal(g.dropTarget(items, 20, 20, new Set(['panel1'])), null);
});

test('a docked control is resized only by its free edge', () => {
	assert.deepEqual(g.resizeEdges('Top'), ['s']);
	assert.deepEqual(g.resizeEdges('Right'), ['w']);
	assert.deepEqual(g.resizeEdges('Fill'), []);
	assert.equal(g.resizeEdges('None').length, 8);
});
