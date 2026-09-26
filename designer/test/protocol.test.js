// node --test: the extension's side of the host protocol, against the real host process -
// spawned with dotnet, one JSON object per line, exactly as src/hostClient.ts talks to it.
const test = require('node:test');
const assert = require('node:assert/strict');
const cp = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const readline = require('node:readline');

const repo = path.resolve(__dirname, '..', '..');
// NETFORMS_TEST_HOST: another host, e.g. the one inside a packaged .vsix (docs/RELEASING.md).
const host = process.env.NETFORMS_TEST_HOST || ['Release', 'Debug'].map((c) => path.join(repo, 'tools', 'NetFormsDesigner.Host', 'bin', c, 'net10.0', 'NetFormsDesigner.Host.dll')).find(fs.existsSync);

function startHost() {
	const proc = cp.spawn('dotnet', [host], { stdio: ['pipe', 'pipe', 'pipe'] });
	const pending = new Map();
	let next = 1;
	readline.createInterface({ input: proc.stdout }).on('line', (line) => {
		const m = JSON.parse(line);
		const p = pending.get(m.id);
		pending.delete(m.id);
		if (p) p(m);
	});
	const call = (method, params) => new Promise((resolve) => {
		const id = next++;
		pending.set(id, resolve);
		proc.stdin.write(JSON.stringify({ id, method, params }) + '\n');
	});
	return { proc, call };
}

test('the host opens a form, applies edits, and writes the file', { skip: !host && 'build tools/NetFormsDesigner.Host first' }, async () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-protocol-'));
	for (const f of ['MainForm.cs', 'MainForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'HelloForms', f), path.join(dir, f));
	const designer = path.join(dir, 'MainForm.Designer.cs');
	const { proc, call } = startHost();
	try {
		const hello = await call('hello');
		assert.equal(hello.result.protocol, 1);

		const opened = await call('open', { path: designer });
		assert.equal(opened.result.className, 'MainForm');
		assert.ok(Buffer.from(opened.result.png, 'base64').subarray(1, 4).toString() === 'PNG');
		assert.ok(opened.result.items.some((i) => i.id === 'button1'));

		const moved = await call('apply', { ops: [{ op: 'setBounds', id: 'button1', x: 30, y: 40 }] });
		const b = moved.result.items.find((i) => i.id === 'button1');
		assert.deepEqual([b.x, b.y], [30, 40]);
		assert.match(fs.readFileSync(designer, 'utf8'), /button1\.Location = new Point\(30, 40\);/);

		const added = await call('apply', { ops: [{ op: 'add', type: 'System.Windows.Forms.TextBox', parent: '', x: 12, y: 60 }] });
		assert.ok(added.result.items.some((i) => i.id === 'textBox1'));

		const rows = await call('properties', { id: 'textBox1' });
		assert.equal(rows.result.find((r) => r.name === 'Name').value, 'textBox1');

		const events = await call('events', { id: 'textBox1' });
		assert.equal(events.result.find((r) => r.isDefault).name, 'TextChanged');

		const refused = await call('apply', { ops: [{ op: 'setProp', id: 'textBox1', prop: 'TabIndex', value: 'x' }] });
		assert.equal(refused.error.kind, 'edit');

		const undone = await call('undo');
		assert.ok(!undone.result.items.some((i) => i.id === 'textBox1'));

		const closed = await call('close');
		assert.equal(closed.result.closed, true);
	} finally {
		proc.kill();
		fs.rmSync(dir, { recursive: true, force: true });
	}
});

// Decision 157: the project's own controls. The fixture library (tests/Fixtures/ControlLibrary) stands for
// a project: its build output is found, loaded, and its controls are in the toolbox and on the canvas.
const fixture = path.join(repo, 'tests', 'Fixtures', 'ControlLibrary');
const fixtureBuilt = ['Debug', 'Release'].some((c) => fs.existsSync(path.join(fixture, 'bin', c, 'net10.0', 'ControlLibrary.dll')));

test('the host loads the project\'s build and its controls go to the toolbox and the form', { skip: (!host && 'build tools/NetFormsDesigner.Host first') || (!fixtureBuilt && 'build tests/Fixtures/ControlLibrary first') }, async () => {
	const esbuild = require('esbuild');
	const { code } = esbuild.transformSync(fs.readFileSync(path.join(repo, 'designer', 'src', 'libraries.ts'), 'utf8'), { loader: 'ts', format: 'cjs' });
	const lib = { exports: {} };
	new Function('module', 'exports', 'require', code)(lib, lib.exports, require);
	const { projectInfo, findOutput, toolboxLibraries, assembliesToLoad } = lib.exports;

	const projectFile = path.join(fixture, 'ControlLibrary.csproj');
	const project = projectInfo(projectFile, fs.readFileSync(projectFile, 'utf8'));
	const output = findOutput(project);
	assert.ok(output && output.endsWith('ControlLibrary.dll'));
	const groups = toolboxLibraries(project, fs.readFileSync(projectFile, 'utf8'), { libraries: [] }, output);
	assert.deepEqual(groups.map((g) => g.name), ['ControlLibrary Components']);

	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-protocol-lib-'));
	for (const f of ['MainForm.cs', 'MainForm.Designer.cs']) fs.copyFileSync(path.join(repo, 'samples', 'HelloForms', f), path.join(dir, f));
	const designer = path.join(dir, 'MainForm.Designer.cs');
	const { proc, call } = startHost();
	try {
		const loaded = await call('libraries', { assemblies: assembliesToLoad(output, groups) });
		assert.deepEqual(loaded.result.assemblies, ['ControlLibrary']);
		const toolbox = (await call('toolbox', { libraries: groups })).result;
		const group = toolbox.find((c) => c.name === 'ControlLibrary Components');
		assert.ok(group.library);
		const gauge = group.items.find((i) => i.name === 'Gauge');
		assert.equal(gauge.type, 'ControlLibrary.Gauge');
		assert.ok(gauge.icon && !gauge.unavailable);
		assert.ok(group.items.find((i) => i.name === 'Ticker').tray);

		await call('open', { path: designer });
		const added = await call('apply', { ops: [{ op: 'add', type: 'ControlLibrary.Gauge', parent: '', x: 10, y: 150 }] });
		assert.ok(added.result.items.some((i) => i.id === 'gauge1'), JSON.stringify(added.error));
		assert.match(fs.readFileSync(designer, 'utf8'), /gauge1 = new ControlLibrary\.Gauge\(\);/);
		const value = (await call('properties', { id: 'gauge1' })).result.find((r) => r.name === 'Value');
		assert.equal(value.category, 'Behavior');

		// Unloaded (a restricted window): the form still opens, the gauge is a placeholder that keeps its code.
		const unloaded = await call('libraries', { assemblies: [] });
		assert.ok(unloaded.result.view.items.some((i) => i.id === 'gauge1'));
		const listed = (await call('toolbox', { libraries: groups })).result.find((c) => c.name === 'ControlLibrary Components');
		assert.ok(listed.items.find((i) => i.name === 'Gauge').unavailable);
		await call('close');
	} finally {
		proc.kill();
		fs.rmSync(dir, { recursive: true, force: true });
	}
});
