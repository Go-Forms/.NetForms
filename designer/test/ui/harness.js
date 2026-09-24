// A stand-in for VS Code around the designer webview: serves media/ exactly as the webview loads
// it, stubs acquireVsCodeApi, and does what designerEditorProvider.ts does with each message -
// relayed to a real host process. With it the webview can be driven by a browser in tests (ui.test.js)
// or by hand: `node test/ui/harness.js path/to/Form.Designer.cs` and open the printed URL.
const cp = require('node:child_process');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const readline = require('node:readline');

const media = path.resolve(__dirname, '..', '..', 'media');
const repo = path.resolve(__dirname, '..', '..', '..');

function findHost() {
	return ['Release', 'Debug'].map((c) => path.join(repo, 'tools', 'NetFormsDesigner.Host', 'bin', c, 'net10.0', 'NetFormsDesigner.Host.dll')).find(fs.existsSync);
}

class Host {
	constructor(dll) {
		this.proc = cp.spawn('dotnet', [dll], { stdio: ['pipe', 'pipe', 'inherit'] });
		this.pending = new Map();
		this.next = 1;
		readline.createInterface({ input: this.proc.stdout }).on('line', (line) => {
			const m = JSON.parse(line);
			const p = this.pending.get(m.id);
			this.pending.delete(m.id);
			if (p) p(m);
		});
	}
	call(method, params) {
		return new Promise((resolve) => {
			const id = this.next++;
			this.pending.set(id, resolve);
			this.proc.stdin.write(JSON.stringify({ id, method, params }) + '\n');
		});
	}
	kill() { this.proc.kill(); }
}

const page = `<!DOCTYPE html>
<html><head><meta charset="UTF-8"><link href="/media/designer.css" rel="stylesheet">
<style>body{--vscode-font-family:Segoe UI,sans-serif;--vscode-font-size:13px;--vscode-foreground:#333;--vscode-editor-background:#f3f3f3;--vscode-focusBorder:#0078d4}</style>
<script>
window.__posted = [];
window.acquireVsCodeApi = () => ({
  postMessage(m) {
    window.__posted.push(m);
    fetch('/rpc', { method: 'POST', body: JSON.stringify(m) }).then((r) => r.json()).then((out) => {
      for (const x of out) window.postMessage(x, '*');
      window.__settled = (window.__settled || 0) + 1;
    });
  },
  getState() { return null; },
  setState() {},
});
</script></head>
<body><div id="app"><div id="toolbar"></div><div id="main"><aside id="toolbox"></aside>
<section id="workspace"><div id="stage"></div><div id="tray"></div></section><aside id="inspector"></aside></div>
<div id="status"></div></div>
<script src="/media/geometry.js"></script><script src="/media/designer.js"></script></body></html>`;

/**
 * Starts the stand-in on a free port for one designer file; resolves to { url, close, host, log }.
 * With options.lang ('ru'), the webview gets that l10n bundle, as VS Code in that language sends it.
 */
function start(designerFile, options = {}) {
	const strings = options.lang ? JSON.parse(fs.readFileSync(path.resolve(__dirname, '..', '..', 'l10n', `bundle.l10n.${options.lang}.json`), 'utf8')) : {};
	const dll = findHost();
	if (!dll) throw new Error('build tools/NetFormsDesigner.Host first');
	const host = new Host(dll);
	const log = [];
	const handle = async (m) => {
		log.push(m);
		const wrap = (res, ok) => (res.error ? [{ type: res.error.kind === 'code' ? 'parseError' : 'error', message: res.error.message, line: res.error.line }] : [ok(res.result)]);
		switch (m.type) {
			case 'ready': {
				const opened = await host.call('open', { path: designerFile });
				const toolbox = await host.call('toolbox');
				return wrap(opened, (view) => ({ type: 'init', view, toolbox: toolbox.result, snap: true, strings }));
			}
			case 'apply': return wrap(await host.call('apply', { ops: m.ops }), (view) => ({ type: 'view', view, select: m.select }));
			case 'undo': return wrap(await host.call('undo'), (view) => ({ type: 'view', view }));
			case 'redo': return wrap(await host.call('redo'), (view) => ({ type: 'view', view }));
			case 'properties': return wrap(await host.call('properties', { id: m.id }), (rows) => ({ type: 'properties', id: m.id, rows }));
			case 'events': return wrap(await host.call('events', { id: m.id }), (rows) => ({ type: 'events', id: m.id, rows }));
			default: return [];
		}
	};
	const server = http.createServer(async (req, res) => {
		try {
			if (req.method === 'POST' && req.url === '/rpc') {
				let body = '';
				for await (const chunk of req) body += chunk;
				const out = await handle(JSON.parse(body));
				res.writeHead(200, { 'Content-Type': 'application/json' });
				res.end(JSON.stringify(out));
				return;
			}
			if (req.url.startsWith('/media/')) {
				const file = path.join(media, path.basename(req.url));
				const type = file.endsWith('.css') ? 'text/css' : 'text/javascript';
				res.writeHead(200, { 'Content-Type': type });
				res.end(fs.readFileSync(file));
				return;
			}
			res.writeHead(200, { 'Content-Type': 'text/html' });
			res.end(page);
		} catch (err) {
			res.writeHead(500);
			res.end(String(err));
		}
	});
	return new Promise((resolve) => server.listen(0, '127.0.0.1', () => {
		const url = `http://127.0.0.1:${server.address().port}/`;
		resolve({ url, host, log, close: () => { host.kill(); server.close(); } });
	}));
}

module.exports = { start, findHost };

if (require.main === module) {
	start(path.resolve(process.argv[2])).then(({ url }) => console.log('Designer harness: ' + url));
}
