// Packages one .vsix per VS Code platform (vsce package --target): the designer host is the same managed
// code everywhere, only its SkiaSharp/HarfBuzz natives differ, so each package keeps just its own
// (scripts/prune-host.js reads NETFORMS_VSCE_TARGET during vscode:prepublish).
//   node scripts/package-targets.js                 all targets
//   node scripts/package-targets.js linux-x64 ...   the given ones
//   ... --pre-release                               options are passed on to vsce package
// The universal package (npm run package) stays for installing by hand.
const { execFileSync } = require('child_process');
const path = require('path');

const all = ['win32-x64', 'win32-arm64', 'linux-x64', 'linux-arm64', 'darwin-x64', 'darwin-arm64'];
const args = process.argv.slice(2);
const options = args.filter(a => a.startsWith('--'));
const named = args.filter(a => !a.startsWith('--'));
const wanted = named.length ? named : all;
const vsce = path.join(__dirname, '..', 'node_modules', '@vscode', 'vsce', 'vsce');

for (const target of wanted) {
	if (!all.includes(target)) throw new Error(`Unknown target ${target}; one of ${all.join(', ')}.`);
	console.log(`\n== ${target}`);
	execFileSync(process.execPath, [vsce, 'package', '--target', target, '--out', `netforms-designer-${target}.vsix`, ...options], {
		cwd: path.join(__dirname, '..'),
		stdio: 'inherit',
		env: { ...process.env, NETFORMS_VSCE_TARGET: target },
	});
}
