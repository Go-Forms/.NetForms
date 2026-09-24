// Copies ../templates (what `dotnet new` installs) into the extension, which scaffolds from the same files.
// The old copy is removed first: cpSync over existing files fails on some Windows file systems (OneDrive).
const fs = require('fs');
const path = require('path');

const target = path.join(__dirname, '..', 'templates');
fs.rmSync(target, { recursive: true, force: true });
fs.cpSync(path.join(__dirname, '..', '..', 'templates'), target, {
	recursive: true,
	filter: s => !/[\/](bin|obj)([\/]|$)/.test(s),
});
