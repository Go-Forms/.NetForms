// Trims the published host (host/) to what the extension ships: SkiaSharp/HarfBuzz natives only for the
// platforms VS Code desktop runs on, no native debug symbols (300 MB of them for Windows alone), and no
// satellite resource assemblies (Roslyn's localized messages - the host reports in English).
const fs = require('fs');
const path = require('path');

const host = path.join(__dirname, '..', 'host');
const keep = new Set(['win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx']);

const runtimes = path.join(host, 'runtimes');
if (fs.existsSync(runtimes)) {
	for (const rid of fs.readdirSync(runtimes)) {
		if (!keep.has(rid)) fs.rmSync(path.join(runtimes, rid), { recursive: true, force: true });
	}
}

(function walk(dir) {
	for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
		const full = path.join(dir, entry.name);
		if (entry.isDirectory()) walk(full);
		else if (entry.name.endsWith('.pdb') && dir !== host) fs.rmSync(full);
	}
})(host);

for (const entry of fs.readdirSync(host, { withFileTypes: true })) {
	if (!entry.isDirectory() || entry.name === 'runtimes') continue;
	const dir = path.join(host, entry.name);
	if (fs.readdirSync(dir).every(f => f.endsWith('.resources.dll'))) fs.rmSync(dir, { recursive: true, force: true });
}
