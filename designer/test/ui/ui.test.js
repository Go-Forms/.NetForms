// node --test: the designer webview driven with a real mouse in a real browser (headless Edge or
// Chrome via puppeteer-core), against the real host. Each gesture is checked where it matters - in
// the .Designer.cs on disk - and screenshots of every step go to test/ui/out/ for a human look.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { start, findHost } = require('./harness.js');

let puppeteer;
try { puppeteer = require('puppeteer-core'); } catch { puppeteer = null; }

const browsers = [
	process.env.NETFORMS_TEST_BROWSER,
	'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
	'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
	'C:/Program Files/Google/Chrome/Application/chrome.exe',
	'/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser', '/usr/bin/microsoft-edge',
].filter(Boolean);
const browserPath = browsers.find((p) => fs.existsSync(p));
const skip = !puppeteer ? 'puppeteer-core is not installed' : !browserPath ? 'no Chrome/Edge found' : !findHost() ? 'build tools/NetFormsDesigner.Host first' : false;

const repo = path.resolve(__dirname, '..', '..', '..');
const out = path.join(__dirname, 'out');

test('the canvas edits the form with the mouse', { skip, timeout: 120000 }, async (t) => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-'));
	for (const f of ['MainForm.cs', 'MainForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'HelloForms', f), path.join(dir, f));
	const designer = path.join(dir, 'MainForm.Designer.cs');
	const code = path.join(dir, 'MainForm.cs');
	fs.mkdirSync(out, { recursive: true });

	const harness = await start(designer);
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1100, height: 700 });
	const errors = [];
	page.on('pageerror', (e) => errors.push(e.message));

	const settled = () => page.evaluate(() => window.__settled || 0);
	/** Runs a gesture and waits until every message it caused has been answered. */
	const gesture = async (fn) => {
		await new Promise((r) => setTimeout(r, 50));
		const before = await settled();
		await fn();
		await page.waitForFunction((n) => (window.__settled || 0) > n, { timeout: 20000 }, before);
		await new Promise((r) => setTimeout(r, 150));
		await page.waitForFunction(() => !document.body.style.cursor, { timeout: 20000 });
	};
	const box = async (selector) => {
		const handle = await page.waitForSelector(selector);
		return handle.boundingBox();
	};
	const shot = (name) => page.screenshot({ path: path.join(out, name + '.png') });
	const file = () => fs.readFileSync(designer, 'utf8');

	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');
	await page.waitForSelector('.tb-item');
	await shot('1-open');

	// Drag button1 by (-40, -30): the file gets the new Location.
	const b = await box('.hit[data-id="button1"]');
	await gesture(async () => {
		await page.mouse.move(b.x + b.width / 2, b.y + b.height / 2);
		await page.mouse.down();
		for (let i = 1; i <= 10; i++) await page.mouse.move(b.x + b.width / 2 - 4 * i, b.y + b.height / 2 - 3 * i);
		await page.mouse.up();
	});
	const moved = /button1\.Location = new Point\((\d+), (\d+)\);/.exec(file());
	assert.ok(moved, 'button1 has a Location');
	assert.ok(Math.abs(Number(moved[1]) - 157) <= 6 && Math.abs(Number(moved[2]) - 75) <= 6, `moved to ${moved[1]}, ${moved[2]}`);
	await shot('2-moved');

	// Locked, the same drag changes nothing and shows no handles; unlocked again, handles return.
	const lock = await page.evaluateHandle(() => [...document.querySelectorAll('#toolbar button')].find((n) => n.title.startsWith('Lock')));
	await lock.click();
	const beforeLocked = file();
	const bl = await box('.hit[data-id="button1"]');
	await page.mouse.move(bl.x + bl.width / 2, bl.y + bl.height / 2);
	await page.mouse.down();
	for (let i = 1; i <= 10; i++) await page.mouse.move(bl.x + bl.width / 2 + 4 * i, bl.y + bl.height / 2 + 3 * i);
	await page.mouse.up();
	await new Promise((r) => setTimeout(r, 500));
	assert.equal(file(), beforeLocked, 'a locked canvas does not move controls');
	assert.equal(await page.$$eval('.handle', (n) => n.length), 0, 'no handles while locked');
	await (await page.evaluateHandle(() => [...document.querySelectorAll('#toolbar button')].find((n) => n.title.startsWith('Lock')))).click();
	await page.waitForSelector('.handle');

	// The property grid: set Text through the text box, commit with Enter.
	await page.waitForFunction(() => [...document.querySelectorAll('.row .name')].some((n) => n.textContent === 'Text'));
	await gesture(async () => {
		const input = await page.evaluateHandle(() => [...document.querySelectorAll('.row')].find((r) => r.querySelector('.name')?.textContent === 'Text').querySelector('input'));
		await input.click({ clickCount: 3 });
		await input.type('Сохранить');
		await page.keyboard.press('Enter');
	});
	assert.match(file(), /button1\.Text = "Сохранить";/);

	// Resize with the south-east handle.
	const handle = await box('.handle[style*="se-resize"]');
	await gesture(async () => {
		await page.mouse.move(handle.x + 3, handle.y + 3);
		await page.mouse.down();
		for (let i = 1; i <= 8; i++) await page.mouse.move(handle.x + 3 + 3 * i, handle.y + 3 + 2 * i);
		await page.mouse.up();
	});
	const size = /button1\.Size = new Size\((\d+), (\d+)\);/.exec(file());
	assert.ok(Number(size[1]) > 90 && Number(size[2]) > 30, `resized to ${size[1]} x ${size[2]}`);
	await shot('3-resized');

	// The toolbox: a click adds a CheckBox, selected, with the VS defaults.
	await gesture(async () => {
		const item = await page.evaluateHandle(() => [...document.querySelectorAll('.tb-item')].find((n) => n.firstChild.textContent === 'CheckBox'));
		await item.click();
	});
	assert.match(file(), /checkBox1\.Text = "checkBox1";/);
	await page.waitForSelector('.hit.primary[data-id="checkBox1"]');
	await shot('4-added');

	// Ctrl+Z takes it away again; Ctrl+Y brings it back.
	await gesture(async () => { await page.click('#status'); await page.keyboard.down('Control'); await page.keyboard.press('z'); await page.keyboard.up('Control'); });
	assert.doesNotMatch(file(), /checkBox1/);
	await gesture(async () => { await page.keyboard.down('Control'); await page.keyboard.press('y'); await page.keyboard.up('Control'); });
	assert.match(file(), /checkBox1/);

	// Delete removes the selection.
	await gesture(async () => {
		const c = await box('.hit[data-id="checkBox1"]');
		await page.mouse.click(c.x + 3, c.y + 3);
	});
	await gesture(async () => { await page.keyboard.press('Delete'); });
	assert.doesNotMatch(file(), /checkBox1/);

	// Double-click on the label wires its default event and writes the handler stub.
	const l = await box('.hit[data-id="label1"]');
	await gesture(async () => { await page.mouse.click(l.x + 5, l.y + 5, { clickCount: 2 }); });
	await page.waitForFunction(() => true);
	await new Promise((r) => setTimeout(r, 1500));
	assert.match(file(), /label1\.Click \+= label1_Click;/);
	assert.match(fs.readFileSync(code, 'utf8'), /private void label1_Click\(object sender, EventArgs e\)/);

	// The Events tab: "+" creates a handler with the default name in one click, "✕" unbinds it
	// (the method stays in the code, as in VS).
	const eventRow = (name) => page.evaluateHandle((n) => [...document.querySelectorAll('.row')].find((r) => r.querySelector('.name')?.textContent === n), name);
	await gesture(async () => {
		const tab = await page.evaluateHandle(() => [...document.querySelectorAll('.insp-tabs button')].find((b) => b.textContent === 'Events'));
		await tab.click();
	});
	await page.waitForFunction(() => [...document.querySelectorAll('.row .name')].some((n) => n.textContent === 'MouseEnter'));
	assert.equal(await (await eventRow('MouseEnter')).evaluate((r) => r.querySelector('.create')?.title), 'Create the handler label1_MouseEnter');
	await gesture(async () => { await (await (await eventRow('MouseEnter')).evaluateHandle((r) => r.querySelector('.create'))).click(); });
	await new Promise((r) => setTimeout(r, 500));
	assert.match(file(), /label1\.MouseEnter \+= label1_MouseEnter;/);
	assert.match(fs.readFileSync(code, 'utf8'), /private void label1_MouseEnter\(object sender, EventArgs e\)/);
	await page.waitForFunction(() => [...document.querySelectorAll('.row')].some((r) => r.querySelector('.name')?.textContent === 'MouseEnter' && r.querySelector('.unbind')));
	await shot('5-event');
	await gesture(async () => { await (await (await eventRow('MouseEnter')).evaluateHandle((r) => r.querySelector('.unbind'))).click(); });
	assert.doesNotMatch(file(), /label1\.MouseEnter/);
	assert.match(fs.readFileSync(code, 'utf8'), /private void label1_MouseEnter\(object sender, EventArgs e\)/);
	await page.waitForFunction(() => [...document.querySelectorAll('.row')].some((r) => r.querySelector('.name')?.textContent === 'MouseEnter' && r.querySelector('.create')));
	await shot('5-final');

	assert.deepEqual(errors, [], 'no script errors in the webview');
});

test('the property grid keeps its place while a property is edited', { skip, timeout: 90000 }, async (t) => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-grid-'));
	for (const f of ['MainForm.cs', 'MainForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'HelloForms', f), path.join(dir, f));
	const designer = path.join(dir, 'MainForm.Designer.cs');
	const harness = await start(designer);
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1100, height: 480 });
	const errors = [];
	page.on('pageerror', (e) => errors.push(e.message));
	const settled = () => page.evaluate(() => window.__settled || 0);
	const gesture = async (fn) => {
		await new Promise((r) => setTimeout(r, 50));
		const before = await settled();
		await fn();
		await page.waitForFunction((n) => (window.__settled || 0) > n + 1, { timeout: 20000 }, before); // the view, then the properties
		await new Promise((r) => setTimeout(r, 150));
		await page.waitForFunction(() => !document.body.style.cursor, { timeout: 20000 });
	};
	const anchorLine = () => (/button1\.Anchor = (.*);/.exec(fs.readFileSync(designer, 'utf8')) || [, ''])[1];
	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');

	const b = await (await page.waitForSelector('.hit[data-id="button1"]')).boundingBox();
	await page.mouse.click(b.x + 5, b.y + 5);
	await page.waitForFunction(() => document.querySelector('#inspector [data-prop="Anchor"] button.mini'));
	// Scrolled down to Anchor, its check boxes open.
	const scroll = await page.evaluate(() => {
		const body = document.querySelector('.insp-body');
		document.querySelector('#inspector [data-prop="Anchor"] button.mini').click();
		body.scrollTop = body.scrollHeight;
		return body.scrollTop;
	});
	const state = () => page.evaluate(() => ({
		open: document.querySelector('[data-flags-of="Anchor"]')?.style.display !== 'none',
		scroll: document.querySelector('.insp-body').scrollTop,
		selected: document.querySelector('.hit.primary')?.dataset.id,
		target: document.querySelector('#inspector .target')?.textContent,
	}));
	const check = (flag) => gesture(() => page.evaluate((f) => [...document.querySelectorAll('[data-flags-of="Anchor"] input')].find((c) => c.value === f).click(), flag));

	// Three flags one after another, without looking for Anchor again: the grid stays where it was.
	await check('Top');
	assert.match(anchorLine(), /Top/);
	assert.deepEqual(await state(), { open: true, scroll, selected: 'button1', target: 'button1' });
	await check('Left');
	assert.match(anchorLine(), /Left/);
	await check('Right');
	const anchor = anchorLine();
	assert.ok(/Top/.test(anchor) && /Left/.test(anchor) && !/Right/.test(anchor) && /Bottom/.test(anchor), anchor);
	assert.deepEqual(await state(), { open: true, scroll, selected: 'button1', target: 'button1' });
	await page.screenshot({ path: path.join(out, '8-anchor.png') });

	// A committed text box leaves the focus in the next one, which Tab moved it to.
	await page.evaluate(() => { const body = document.querySelector('.insp-body'); body.scrollTop = 0; });
	await gesture(async () => {
		const input = await page.evaluateHandle(() => document.querySelector('#inspector [data-prop="Text"] input'));
		await input.click({ clickCount: 3 });
		await input.type('OK');
		await page.keyboard.press('Tab');
	});
	assert.match(fs.readFileSync(designer, 'utf8'), /button1\.Text = "OK";/);
	assert.equal(await page.evaluate(() => document.activeElement.tagName !== 'BODY' && !!document.activeElement.closest('#inspector')), true, 'the focus stays in the grid');

	assert.deepEqual(errors, [], 'no script errors in the webview');
});

test('in Russian, the chrome speaks Russian', { skip, timeout: 60000 }, async (t) => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-ru-'));
	for (const f of ['GalleryForm.cs', 'GalleryForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'Gallery', f), path.join(dir, f));
	const harness = await start(path.join(dir, 'GalleryForm.Designer.cs'), { lang: 'ru' });
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1200, height: 720 });
	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');
	await page.waitForFunction(() => document.querySelectorAll('.cat-head').length > 0);
	const text = await page.evaluate(() => document.body.innerText);
	for (const word of ['Свойства', 'События', 'Стандартные элементы управления', 'Контейнеры', 'Внешний вид', 'Поведение']) assert.ok(text.includes(word), word);
	await page.screenshot({ path: path.join(out, '6-russian.png') });
});

test('tree nodes are edited as indented lines', { skip, timeout: 90000 }, async (t) => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-tree-'));
	for (const f of ['CollectionsForm.cs', 'CollectionsForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'Gallery', f), path.join(dir, f));
	const designer = path.join(dir, 'CollectionsForm.Designer.cs');
	const harness = await start(designer);
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1200, height: 720 });
	const errors = [];
	page.on('pageerror', (e) => errors.push(e.message));
	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');

	const tree = await (await page.waitForSelector('.hit[data-id="treeView1"]')).boundingBox();
	await page.mouse.click(tree.x + 10, tree.y + 10);
	await page.waitForFunction(() => [...document.querySelectorAll('.row .name')].some((n) => n.textContent === 'Nodes'));
	await page.evaluate(() => [...document.querySelectorAll('.row')].find((r) => r.querySelector('.name')?.textContent === 'Nodes').querySelector('button.mini').click());
	const area = await page.waitForSelector('textarea');
	assert.equal(await area.evaluate((a) => a.value), 'Root\n  Child\nSecond');

	// A new line under Second, indented with Tab: a child of Second.
	await area.evaluate((a) => { a.focus(); a.selectionStart = a.selectionEnd = a.value.length; });
	await page.keyboard.press('Enter');
	await page.keyboard.press('Tab');
	await page.keyboard.type('Third');
	assert.equal(await area.evaluate((a) => a.value), 'Root\n  Child\nSecond\n  Third');
	const before = fs.readFileSync(designer, 'utf8');
	await page.evaluate(() => [...document.querySelectorAll('button')].find((b) => b.textContent === 'OK').click());
	await page.waitForFunction(() => !document.body.style.cursor, { timeout: 20000 });
	for (let i = 0; i < 40 && fs.readFileSync(designer, 'utf8') === before; i++) await new Promise((r) => setTimeout(r, 250));
	const file = fs.readFileSync(designer, 'utf8');
	assert.match(file, /TreeNode treeNode3 = new TreeNode\("Third"\);/);
	assert.match(file, /TreeNode treeNode4 = new TreeNode\("Second", new TreeNode\[\] \{ treeNode3 \}\);/);
	assert.deepEqual(errors, []);
});

test('tab pages switch with a click and are edited by their captions', { skip, timeout: 90000 }, async (t) => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-tabs-'));
	for (const f of ['GalleryForm.cs', 'GalleryForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'Gallery', f), path.join(dir, f));
	const designer = path.join(dir, 'GalleryForm.Designer.cs');
	const harness = await start(designer);
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1200, height: 720 });
	const errors = [];
	page.on('pageerror', (e) => errors.push(e.message));
	fs.mkdirSync(out, { recursive: true });
	await page.evaluateOnNewDocument(() => window.addEventListener('message', (e) => { if (e.data && e.data.view) window.__view = e.data.view; }));
	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');
	const file = () => fs.readFileSync(designer, 'utf8');
	const waitFile = async (before) => {
		await page.waitForFunction(() => !document.body.style.cursor, { timeout: 20000 });
		for (let i = 0; i < 40 && file() === before; i++) await new Promise((r) => setTimeout(r, 250));
		return file();
	};

	// A click on the second tab's header brings its page (and its controls) to the canvas.
	assert.equal(await page.$('.hit[data-id="tabPage2"]'), null, 'the second page is hidden at first');
	const canvas = await page.evaluate(() => {
		const r = document.querySelector('.canvas').getBoundingClientRect();
		return { x: r.left, y: r.top };
	});
	// The header rectangles the host sent, in canvas coordinates.
	const headers = await page.evaluate(() => window.__view.items.find((i) => i.id === 'tabControl1').tabs);
	assert.ok(headers && headers.length === 3, 'the TabControl knows its tab headers');
	let before = file();
	await page.mouse.click(canvas.x + headers[1][0] + headers[1][2] / 2, canvas.y + headers[1][1] + headers[1][3] / 2);
	let text = await waitFile(before);
	assert.match(text, /tabControl1\.SelectedIndex = 1;/);
	await page.waitForSelector('.hit[data-id="tabPage2"]');
	await page.waitForSelector('.hit[data-id="splitContainer1"]');
	assert.equal(await page.$('.hit[data-id="tabPage1"]'), null, 'the first page is hidden now');
	await page.screenshot({ path: path.join(out, 'tabs-1-switched.png') });

	// TabPages lists the captions, and renaming, adding and removing a page goes to the file.
	await page.waitForFunction(() => [...document.querySelectorAll('.row .name')].some((n) => n.textContent === 'TabPages'));
	await page.evaluate(() => [...document.querySelectorAll('.row')].find((r) => r.querySelector('.name')?.textContent === 'TabPages').querySelector('button.mini').click());
	await page.waitForSelector('.coll-editor input');
	assert.deepEqual(await page.$$eval('.coll-editor input', (n) => n.map((i) => i.value)), ['Controls', 'Layout', 'Panels']);
	const inputs = await page.$$('.coll-editor input');
	await inputs[0].click({ clickCount: 3 });
	await page.keyboard.type('Номенклатура');
	await page.evaluate(() => [...document.querySelectorAll('.coll-editor button')].find((b) => b.textContent === 'Add').click());
	await page.keyboard.type('Приход');
	await page.evaluate(() => document.querySelectorAll('.coll-editor .coll-row')[2].querySelector('button[title^="Remove"]').click());
	await page.screenshot({ path: path.join(out, 'tabs-2-editor.png') });
	before = file();
	await page.evaluate(() => [...document.querySelectorAll('.coll-editor button')].find((b) => b.textContent === 'OK').click());
	text = await waitFile(before);
	assert.match(text, /tabPage1\.Text = "Номенклатура";/);
	// The removed page's name is free again: the new page takes it, as in VS.
	assert.match(text, /tabControl1\.Controls\.Add\(tabPage3\);/);
	assert.match(text, /tabPage3\.Text = "Приход";/);
	assert.doesNotMatch(text, /"Panels"/);
	assert.match(text, /tabPage1\.Controls\.Add\(button1\);/, 'the renamed page keeps its controls');
	await page.screenshot({ path: path.join(out, 'tabs-3-edited.png') });
	assert.deepEqual(errors, []);
});

test('controls are copied, pasted and duplicated, by keys and from the context menu', { skip, timeout: 90000 }, async (t) => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-clip-'));
	for (const f of ['MainForm.cs', 'MainForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'HelloForms', f), path.join(dir, f));
	const designer = path.join(dir, 'MainForm.Designer.cs');
	const harness = await start(designer);
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1200, height: 720 });
	const errors = [];
	page.on('pageerror', (e) => errors.push(e.message));
	fs.mkdirSync(out, { recursive: true });
	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');
	const file = () => fs.readFileSync(designer, 'utf8');
	const waitFile = async (before) => {
		for (let i = 0; i < 40 && file() === before; i++) await new Promise((r) => setTimeout(r, 250));
		await page.waitForFunction(() => !document.body.style.cursor, { timeout: 20000 });
		return file();
	};
	const selected = () => page.$$eval('.hit.primary, .hit.selected', (n) => n.map((x) => x.dataset.id));

	// Ctrl+C, Ctrl+V: a copy, selected, with the original's handler.
	const b = await (await page.waitForSelector('.hit[data-id="button1"]')).boundingBox();
	await page.mouse.click(b.x + b.width / 2, b.y + b.height / 2);
	await page.keyboard.down('Control'); await page.keyboard.press('c'); await page.keyboard.up('Control');
	await page.waitForSelector('.toast');
	assert.match(harness.clipboard.text, /netforms\/components-1/);
	let before = file();
	await page.keyboard.down('Control'); await page.keyboard.press('v'); await page.keyboard.up('Control');
	let text = await waitFile(before);
	assert.match(text, /button2\.Click \+= button1_Click;/);
	await page.waitForFunction(() => document.querySelector('.hit.primary')?.dataset.id === 'button2');

	// Ctrl+D duplicates the selection.
	before = file();
	await page.keyboard.down('Control'); await page.keyboard.press('d'); await page.keyboard.up('Control');
	text = await waitFile(before);
	assert.match(text, /button3 = new Button\(\);/);
	assert.deepEqual(await selected(), ['button3']);

	// Right-click: the menu; Delete from it removes the control.
	const b3 = await (await page.waitForSelector('.hit[data-id="button3"]')).boundingBox();
	await page.mouse.click(b3.x + b3.width / 2, b3.y + b3.height / 2, { button: 'right' });
	await page.waitForSelector('.ctx-menu');
	const labels = await page.$$eval('.ctx-menu .ctx-label', (n) => n.map((x) => x.textContent));
	for (const l of ['View Code', 'Cut', 'Copy', 'Paste', 'Duplicate', 'Delete', 'Bring to Front', 'Send to Back', 'Lock Controls', 'Properties']) assert.ok(labels.includes(l), l);
	await page.screenshot({ path: path.join(out, 'clip-menu.png') });
	before = file();
	await page.evaluate(() => [...document.querySelectorAll('.ctx-item')].find((n) => n.textContent.startsWith('Delete')).dispatchEvent(new MouseEvent('mousedown', { bubbles: true })));
	text = await waitFile(before);
	assert.doesNotMatch(text, /button3/);
	assert.equal(await page.$('.ctx-menu'), null, 'the menu closes');

	// Cut from the menu, paste back: the same name again (it is free once cut).
	const b2 = await (await page.waitForSelector('.hit[data-id="button2"]')).boundingBox();
	await page.mouse.click(b2.x + b2.width / 2, b2.y + b2.height / 2, { button: 'right' });
	await page.waitForSelector('.ctx-menu');
	before = file();
	await page.evaluate(() => [...document.querySelectorAll('.ctx-item')].find((n) => n.textContent.startsWith('Cut')).dispatchEvent(new MouseEvent('mousedown', { bubbles: true })));
	text = await waitFile(before);
	assert.doesNotMatch(text, /button2/);
	before = file();
	await page.keyboard.down('Control'); await page.keyboard.press('v'); await page.keyboard.up('Control');
	text = await waitFile(before);
	assert.match(text, /button2 = new Button\(\);/);
	assert.deepEqual(errors, []);
});

// Decision 157: the project's controls in the toolbox. The fixture library stands for the project; its
// group comes after NetForms's, with the [ToolboxBitmap] icon, and a click puts a Gauge on the form.
test('the project\'s own controls are in the toolbox and go on the form', { skip, timeout: 120000 }, async (t) => {
	const fixture = path.join(repo, 'tests', 'Fixtures', 'ControlLibrary');
	const output = ['Debug', 'Release'].map((c) => path.join(fixture, 'bin', c, 'net10.0', 'ControlLibrary.dll')).find(fs.existsSync);
	if (!output) { t.skip('build tests/Fixtures/ControlLibrary first'); return; }
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-lib-'));
	for (const f of ['MainForm.cs', 'MainForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'HelloForms', f), path.join(dir, f));
	const designer = path.join(dir, 'MainForm.Designer.cs');
	fs.mkdirSync(out, { recursive: true });

	const groups = [{ id: 'project', name: 'ControlLibrary Components', assemblies: [output], excludeNamespaces: ['ControlLibrary.Extras'] }];
	const harness = await start(designer, { libraries: { load: [output], groups } });
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1100, height: 700 });
	const errors = [];
	page.on('pageerror', (e) => errors.push(e.message));
	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');
	await page.waitForSelector('.tb-head.library');

	const group = await page.evaluate(() => {
		const head = [...document.querySelectorAll('.tb-head.library')].find((h) => h.textContent === 'ControlLibrary Components');
		return head && [...head.parentElement.querySelectorAll('.tb-item')].map((r) => ({ name: r.textContent.replace(/\s+\(tray\)$/, '').replace(/^⚙/, ''), icon: !!r.querySelector('img.tb-icon') }));
	});
	assert.ok(group, 'the library group is shown');
	assert.deepEqual(group.map((i) => i.name), ['AddressBox', 'FaultyConstructor', 'FaultyPainter', 'Gauge', 'Ticker']);
	assert.ok(group.find((i) => i.name === 'Gauge').icon, 'Gauge has its [ToolboxBitmap] icon');
	assert.ok(await page.$('.tb-actions .tb-action'), 'Add Control Library and Rescan are in the toolbox');
	await page.screenshot({ path: path.join(out, '10-library-toolbox.png') });

	const before = await page.evaluate(() => window.__settled || 0);
	await page.evaluate(() => [...document.querySelectorAll('.tb-item')].find((r) => r.textContent === 'Gauge').click());
	await page.waitForFunction((n) => (window.__settled || 0) > n, { timeout: 20000 }, before);
	await page.waitForSelector('.hit.primary[data-id="gauge1"]');
	assert.match(fs.readFileSync(designer, 'utf8'), /gauge1 = new ControlLibrary\.Gauge\(\);/);
	await page.screenshot({ path: path.join(out, '11-library-gauge.png') });

	// Rescan and Add Control Library are the extension's commands: the webview only asks for them.
	await page.evaluate(() => document.querySelectorAll('.tb-actions .tb-action')[1].click());
	await page.evaluate(() => document.querySelectorAll('.tb-actions .tb-action')[0].click());
	await new Promise((r) => setTimeout(r, 300));
	const posted = await page.evaluate(() => window.__posted.map((m) => m.type));
	assert.ok(posted.includes('rescan') && posted.includes('addLibrary'));
	assert.deepEqual(errors, []);
});

// Decision 170: a TableLayoutPanel places by cell. Added controls take free cells, a drag puts a control in the
// cell under the mouse (onto a taken one: they swap), the grips between columns resize them, the context menu
// inserts columns, and the Columns editor sets each column's size type and size.
test('a table: controls go to cells, move between them, and its columns are edited', { skip, timeout: 120000 }, async (t) => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-table-'));
	for (const f of ['MainForm.cs', 'MainForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'HelloForms', f), path.join(dir, f));
	const designer = path.join(dir, 'MainForm.Designer.cs');
	const harness = await start(designer);
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1200, height: 760 });
	const errors = [];
	page.on('pageerror', (e) => errors.push(e.message));
	fs.mkdirSync(out, { recursive: true });
	await page.evaluateOnNewDocument(() => window.addEventListener('message', (e) => { if (e.data && e.data.view) window.__view = e.data.view; }));
	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');
	await page.waitForSelector('.tb-item');
	const file = () => fs.readFileSync(designer, 'utf8');
	const waitFile = async (before) => {
		for (let i = 0; i < 40 && file() === before; i++) await new Promise((r) => setTimeout(r, 250));
		await page.waitForFunction(() => !document.body.style.cursor, { timeout: 20000 });
		await new Promise((r) => setTimeout(r, 200));
		return file();
	};
	const toolbox = (name) => page.evaluate((n) => [...document.querySelectorAll('.tb-item')].find((i) => i.firstChild.textContent === n).click(), name);
	const view = (id) => page.evaluate((i) => window.__view.items.find((x) => x.id === i), id);
	const origin = () => page.evaluate(() => { const r = document.querySelector('.canvas').getBoundingClientRect(); return { x: r.left, y: r.top }; });
	/** The middle of a cell of the table, in page coordinates. */
	const cellCenter = async (column, row) => {
		const o = await origin();
		const table = (await view('tableLayoutPanel1')).table;
		let x = table.x, y = table.y;
		for (let i = 0; i < column; i++) x += table.border + table.columns[i];
		for (let i = 0; i < row; i++) y += table.border + table.rows[i];
		return { x: o.x + x + table.border + table.columns[column] / 2, y: o.y + y + table.border + table.rows[row] / 2 };
	};
	const drag = async (from, to) => {
		await page.mouse.move(from.x, from.y);
		await page.mouse.down();
		for (let i = 1; i <= 10; i++) await page.mouse.move(from.x + (to.x - from.x) * i / 10, from.y + (to.y - from.y) * i / 10);
		await page.mouse.up();
	};
	const centerOf = async (id) => { const b = await (await page.waitForSelector(`.hit[data-id="${id}"]`)).boundingBox(); return { x: b.x + b.width / 2, y: b.y + b.height / 2 }; };

	// A new table: 2 × 2, 50% each, drawn as dashed cells.
	let before = file();
	await toolbox('TableLayoutPanel');
	let text = await waitFile(before);
	assert.match(text, /tableLayoutPanel1\.ColumnCount = 2;/);
	await page.waitForSelector('.hit.primary[data-id="tableLayoutPanel1"]');
	assert.equal(await page.$$eval('.tl-line', (n) => n.length), 6, 'three lines across, three down');

	// With the table selected, a button from the toolbox takes the first free cell.
	before = file();
	await toolbox('Button');
	text = await waitFile(before);
	assert.match(text, /tableLayoutPanel1\.Controls\.Add\(button2, 0, 0\);/);

	// Dragged, it goes to the cell under the mouse.
	before = file();
	await drag(await centerOf('button2'), await cellCenter(1, 1));
	text = await waitFile(before);
	assert.match(text, /tableLayoutPanel1\.Controls\.Add\(button2, 1, 1\);/);

	// button1 from the form into a cell of the table, then onto button2's: they swap.
	before = file();
	await drag(await centerOf('button1'), await cellCenter(0, 1));
	text = await waitFile(before);
	assert.match(text, /tableLayoutPanel1\.Controls\.Add\(button1, 0, 1\);/);
	before = file();
	await drag(await centerOf('button1'), await cellCenter(1, 1));
	text = await waitFile(before);
	assert.match(text, /tableLayoutPanel1\.Controls\.Add\(button1, 1, 1\);/);
	assert.match(text, /tableLayoutPanel1\.Controls\.Add\(button2, 0, 1\);/);
	await page.screenshot({ path: path.join(out, 'table-1-cells.png') });

	// The grip between the columns: dragged right, the first column gets the larger share.
	const grip = await (await page.waitForSelector('.tl-grip.v')).boundingBox();
	before = file();
	await drag({ x: grip.x + grip.width / 2, y: grip.y + 10 }, { x: grip.x + grip.width / 2 + 40, y: grip.y + 10 });
	text = await waitFile(before);
	const shares = [...text.matchAll(/ColumnStyles\.Add\(new ColumnStyle\(SizeType\.Percent, ([\d.]+)F\)\);/g)].map((m) => Number(m[1]));
	assert.equal(shares.length, 2);
	assert.ok(shares[0] > 60 && Math.abs(shares[0] + shares[1] - 100) < 0.1, `shares ${shares}`);

	// Right-click in the first column: insert one to its left; the controls move along.
	const c00 = await cellCenter(0, 0);
	await page.mouse.click(c00.x, c00.y, { button: 'right' });
	await page.waitForSelector('.ctx-menu');
	const labels = await page.$$eval('.ctx-menu .ctx-label', (n) => n.map((x) => x.textContent));
	for (const l of ['Insert Column Left', 'Insert Column Right', 'Delete Column 1', 'Insert Row Above', 'Insert Row Below', 'Delete Row 1', 'Edit Columns and Rows…']) assert.ok(labels.includes(l), l);
	before = file();
	await page.evaluate(() => [...document.querySelectorAll('.ctx-item')].find((n) => n.textContent.startsWith('Insert Column Left')).dispatchEvent(new MouseEvent('mousedown', { bubbles: true })));
	text = await waitFile(before);
	assert.match(text, /tableLayoutPanel1\.ColumnCount = 3;/);
	assert.match(text, /tableLayoutPanel1\.Controls\.Add\(button1, 2, 1\);/);
	assert.match(text, /tableLayoutPanel1\.Controls\.Add\(button2, 1, 1\);/);

	// The Columns editor: the first column becomes Absolute 80.
	const c = await cellCenter(0, 0);
	await page.mouse.click(c.x, c.y);
	await page.waitForFunction(() => document.querySelector('#inspector [data-prop="Columns"] button.mini'));
	await page.evaluate(() => document.querySelector('#inspector [data-prop="Columns"] button.mini').click());
	await page.waitForSelector('.styles-editor .style-row');
	assert.equal(await page.$$eval('.styles-editor .style-row', (n) => n.length), 3);
	await page.select('.styles-editor .style-row select', 'Absolute');
	const size = await page.waitForSelector('.styles-editor .style-row input[type=number]');
	await size.click({ clickCount: 3 });
	await page.keyboard.type('80');
	await page.screenshot({ path: path.join(out, 'table-2-columns.png') });
	before = file();
	await page.evaluate(() => [...document.querySelectorAll('.styles-editor button')].find((b) => b.textContent === 'OK').click());
	text = await waitFile(before);
	assert.match(text, /tableLayoutPanel1\.ColumnStyles\.Add\(new ColumnStyle\(SizeType\.Absolute, 80F\)\);/);
	await page.screenshot({ path: path.join(out, 'table-3-edited.png') });
	assert.deepEqual(errors, []);
});

// Decisions 171-172: menus open on the canvas and every item in them is selectable (a double-click wires its Click);
// a ContextMenuStrip opens from the tray; "ToolTip on toolTip1" is in the property grid.
test('menus open on the canvas, their items take handlers, and a tool tip is set in the grid', { skip, timeout: 120000 }, async (t) => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-ui-menu-'));
	for (const f of ['MainForm.cs', 'MainForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'HelloForms', f), path.join(dir, f));
	const designer = path.join(dir, 'MainForm.Designer.cs');
	const code = path.join(dir, 'MainForm.cs');
	const harness = await start(designer);
	const browser = await puppeteer.launch({ executablePath: browserPath, headless: true, args: ['--no-sandbox'] });
	t.after(async () => { await browser.close(); harness.close(); fs.rmSync(dir, { recursive: true, force: true }); });
	const page = await browser.newPage();
	await page.setViewport({ width: 1200, height: 760 });
	const errors = [];
	page.on('pageerror', (e) => errors.push(e.message));
	fs.mkdirSync(out, { recursive: true });
	await page.goto(harness.url);
	await page.waitForSelector('.canvas img.picture');
	await page.waitForSelector('.tb-item');
	const file = () => fs.readFileSync(designer, 'utf8');
	const idle = () => page.waitForFunction(() => !document.body.style.cursor, { timeout: 20000 });
	const waitFile = async (before) => {
		for (let i = 0; i < 40 && file() === before; i++) await new Promise((r) => setTimeout(r, 250));
		await idle();
		await new Promise((r) => setTimeout(r, 200));
		return file();
	};
	const apply = async (ops) => {
		const before = file();
		await page.evaluate((o) => acquireVsCodeApi().postMessage({ type: 'apply', ops: o }), ops);
		return waitFile(before);
	};
	const click = async (selector, options) => {
		const b = await (await page.waitForSelector(selector)).boundingBox();
		await page.mouse.click(b.x + b.width / 2, b.y + b.height / 2, options);
	};

	// A menu File → Open, Recent → (One, Two), Exit, and a ContextMenuStrip with Copy, Paste.
	await apply([{ op: 'add', type: 'System.Windows.Forms.MenuStrip', parent: '' }, { op: 'add', type: 'System.Windows.Forms.ContextMenuStrip' }]);
	await apply([{ op: 'setItems', id: 'menuStrip1', prop: 'Items', values: ['File'], ids: [''] }]);
	await apply([{ op: 'setItems', id: 'toolStripMenuItem1', prop: 'DropDownItems', values: ['Open', 'Recent', 'Exit'], ids: ['', '', ''] }]);
	await apply([{ op: 'setItems', id: 'toolStripMenuItem3', prop: 'DropDownItems', values: ['One', 'Two'], ids: ['', ''] }]);
	let text = await apply([{ op: 'setItems', id: 'contextMenuStrip1', prop: 'Items', values: ['Copy', 'Paste'], ids: ['', ''] }]);
	assert.match(text, /toolStripMenuItem6\.Text = "Two";/);
	assert.equal(await page.$('.dd-picture'), null, 'no menu is open at first');

	// A click on File opens its menu; on Recent, the sub-menu beside it.
	await click('.hit[data-id="toolStripMenuItem1"]');
	await page.waitForSelector('.dd-picture');
	await page.waitForSelector('.hit.in-menu[data-id="toolStripMenuItem3"]');
	await click('.hit.in-menu[data-id="toolStripMenuItem3"]');
	await page.waitForFunction(() => document.querySelectorAll('.dd-picture').length === 2);
	await page.screenshot({ path: path.join(out, 'menu-1-open.png') });

	// A double-click on Two (inside the sub-menu) writes its Click handler.
	let before = file();
	await click('.hit.in-menu[data-id="toolStripMenuItem6"]', { clickCount: 2 });
	text = await waitFile(before);
	assert.match(text, /toolStripMenuItem6\.Click \+= toolStripMenuItem6_Click;/);
	assert.match(fs.readFileSync(code, 'utf8'), /private void toolStripMenuItem6_Click\(object sender, EventArgs e\)/);
	// The menus are still open after the edit.
	await page.waitForFunction(() => document.querySelectorAll('.dd-picture').length === 2);

	// The ContextMenuStrip opens from the tray, alone.
	await click('.tray-item');
	await page.waitForFunction(() => document.querySelectorAll('.dd-picture').length === 1 && document.querySelector('.hit.in-menu[data-id="toolStripMenuItem8"]'));
	await page.screenshot({ path: path.join(out, 'menu-2-context.png') });
	await click('.hit.in-menu[data-id="toolStripMenuItem8"]');
	await page.waitForFunction(() => document.querySelector('#inspector .target')?.textContent === 'toolStripMenuItem8');

	// Anything else selected closes them (button1: label1 is under the open menu).
	await click('.hit[data-id="button1"]');
	await page.waitForFunction(() => !document.querySelector('.dd-picture'));

	// A ToolTip gives button1 "ToolTip on toolTip1".
	before = file();
	await page.evaluate(() => [...document.querySelectorAll('.tb-item')].find((i) => i.firstChild.textContent === 'ToolTip').click());
	await waitFile(before);
	await click('.hit[data-id="button1"]');
	await page.waitForFunction(() => document.querySelector('#inspector [data-prop="ToolTip on toolTip1"] input'));
	before = file();
	const input = await page.$('#inspector [data-prop="ToolTip on toolTip1"] input');
	await input.click({ clickCount: 3 });
	await page.keyboard.type('Сохранить файл');
	await page.keyboard.press('Enter');
	text = await waitFile(before);
	assert.match(text, /toolTip1\.SetToolTip\(button1, "Сохранить файл"\);/);
	assert.deepEqual(errors, []);
});
