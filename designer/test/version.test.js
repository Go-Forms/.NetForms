// node --test: the extension creates projects for the NetForms version the repository builds - the
// templates it installs from NuGet (NetForms.Templates::<netformsVersion>) and the package they reference.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

test('netformsVersion in package.json is the Version of Directory.Build.props', () => {
	const pkg = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'package.json'), 'utf8'));
	const props = fs.readFileSync(path.join(__dirname, '..', '..', 'Directory.Build.props'), 'utf8');
	const version = /<Version>([^<]+)<\/Version>/.exec(props)[1];
	assert.equal(pkg.netformsVersion, version);
});
