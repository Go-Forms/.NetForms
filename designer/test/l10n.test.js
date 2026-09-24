// node --test: every string the extension shows has its Russian translation, and every %key% of
// package.json has its text in both package.nls files.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const read = (f) => fs.readFileSync(path.join(root, f), 'utf8');

test('every UI string has a Russian translation', () => {
	const keys = new Set();
	const collect = (text, re) => { for (const m of text.matchAll(re)) keys.add(m[1].replace(/\\'/g, "'")); };
	for (const f of fs.readdirSync(path.join(root, 'src'))) collect(read('src/' + f), /vscode\.l10n\.t\('((?:[^'\\]|\\.)*)'/g);
	collect(read('media/designer.js'), /\bT\('((?:[^'\\]|\\.)*)'/g);
	const ru = JSON.parse(read('l10n/bundle.l10n.ru.json'));
	const missing = [...keys].filter((k) => !(k in ru));
	assert.deepEqual(missing, []);
	for (const [k, v] of Object.entries(ru)) {
		const places = (s) => [...s.matchAll(/\{(\d+)\}/g)].map((m) => m[1]).sort().join();
		assert.equal(places(v), places(k), `the placeholders of "${k}"`);
	}
});

test('every package.json %key% is in package.nls.json and package.nls.ru.json', () => {
	const used = [...read('package.json').matchAll(/"%([^%"]+)%"/g)].map((m) => m[1]);
	assert.ok(used.length > 10);
	for (const file of ['package.nls.json', 'package.nls.ru.json']) {
		const nls = JSON.parse(read(file));
		assert.deepEqual(used.filter((k) => !(k in nls)), [], file);
	}
});

test('the webview scripts parse', () => {
	const vm = require('node:vm');
	for (const f of ['media/geometry.js', 'media/designer.js']) new vm.Script(read(f), { filename: f });
});
