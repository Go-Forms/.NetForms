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
