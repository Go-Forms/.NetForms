// node --test: control libraries of a project (src/libraries.ts, compiled on the fly with esbuild) -
// decision 157, docs/designer-control-libraries.md.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const os = require('os');
const path = require('path');
const esbuild = require('esbuild');

function load(file) {
	const { code } = esbuild.transformSync(fs.readFileSync(path.join(__dirname, '..', 'src', file), 'utf8'), { loader: 'ts', format: 'cjs' });
	const module = { exports: {} };
	new Function('module', 'exports', 'require', code)(module, module.exports, require);
	return module.exports;
}
const lib = load('libraries.ts');

const csproj = `<Project Sdk="Microsoft.NET.Sdk">\r
  <PropertyGroup>\r
    <OutputType>WinExe</OutputType>\r
    <TargetFramework>net10.0</TargetFramework>\r
    <RootNamespace>Shop.App</RootNamespace>\r
  </PropertyGroup>\r
  <ItemGroup>\r
    <PackageReference Include="NetForms" Version="0.1.0-preview.6" />\r
    <PackageReference Include="ScottPlot.WinForms">\r
      <Version>5.0.0</Version>\r
    </PackageReference>\r
    <ProjectReference Include="..\\Controls\\Controls.csproj" />\r
    <Reference Include="Gauges, Version=1.0.0.0, Culture=neutral">\r
      <HintPath>libs\\Gauges.dll</HintPath>\r
    </Reference>\r
  </ItemGroup>\r
</Project>\r
`;

function tempDir() {
	return fs.mkdtempSync(path.join(os.tmpdir(), 'netforms-libs-'));
}

test('the project file: names, frameworks and references', () => {
	const info = lib.projectInfo('/src/Shop/Shop.csproj', csproj);
	assert.equal(info.name, 'Shop');
	assert.equal(info.assemblyName, 'Shop');
	assert.equal(info.rootNamespace, 'Shop.App');
	assert.deepEqual(info.targetFrameworks, ['net10.0']);
	assert.deepEqual(lib.projectInfo('/x/My App.csproj', '<Project><PropertyGroup><TargetFrameworks>net8.0;net10.0</TargetFrameworks><AssemblyName>$(Foo)</AssemblyName></PropertyGroup></Project>'),
		{ file: '/x/My App.csproj', dir: '/x', name: 'My App', assemblyName: 'My App', rootNamespace: 'My_App', targetFrameworks: ['net8.0', 'net10.0'] });
	assert.deepEqual(lib.packageReferences(csproj), [{ id: 'NetForms', version: '0.1.0-preview.6' }, { id: 'ScottPlot.WinForms', version: '5.0.0' }]);
	assert.deepEqual(lib.projectReferences(csproj), ['..\\Controls\\Controls.csproj']);
	assert.deepEqual(lib.dllReferences(csproj), [{ include: 'Gauges, Version=1.0.0.0, Culture=neutral', hintPath: 'libs\\Gauges.dll' }]);
});

test('a .dll reference is added and removed the way the project file is written', () => {
	const added = lib.addDllReference(csproj, 'libs/Dials.dll');
	assert.match(added, /  <ItemGroup>\r\n    <Reference Include="Dials">\r\n      <HintPath>libs\\Dials\.dll<\/HintPath>\r\n    <\/Reference>\r\n  <\/ItemGroup>\r\n<\/Project>/);
	assert.equal(lib.addDllReference(added, 'libs/Dials.dll'), added, 'added once');
	assert.equal(lib.addDllReference(csproj, 'libs/Gauges.dll'), csproj, 'Gauges is referenced already (with a full name)');
	assert.equal(lib.removeDllReference(added, 'Dials'), csproj);
	const without = lib.removeDllReference(csproj, 'Gauges');
	assert.doesNotMatch(without, /Gauges/);
	assert.match(without, /<ProjectReference/);
});

test('project.assets.json: the packages of the project with their runtime assemblies', () => {
	const assets = {
		targets: {
			'net10.0': {
				'ScottPlot.WinForms/5.0.0': { type: 'package', runtime: { 'lib/net8.0/ScottPlot.WinForms.dll': {} } },
				'ScottPlot/5.0.0': { type: 'package', runtime: { 'lib/net8.0/ScottPlot.dll': {}, 'lib/net8.0/_._': {} } },
				'Controls/1.0.0': { type: 'project' },
			},
			'net10.0/linux-x64': {},
		},
		libraries: { 'ScottPlot.WinForms/5.0.0': { path: 'scottplot.winforms/5.0.0' }, 'ScottPlot/5.0.0': { path: 'scottplot/5.0.0' } },
		packageFolders: { '/home/me/.nuget/packages/': {} },
		project: { frameworks: { 'net10.0': { dependencies: { 'ScottPlot.WinForms': {}, NetForms: {} } } } },
	};
	const packages = lib.packagesFromAssets(assets, 'net10.0');
	assert.deepEqual(packages.map((p) => [p.id, p.version, p.direct, p.assemblies]), [
		['ScottPlot.WinForms', '5.0.0', true, ['ScottPlot.WinForms.dll']],
		['ScottPlot', '5.0.0', false, ['ScottPlot.dll']],
	]);
	assert.equal(packages[0].folder, path.join('/home/me/.nuget/packages/', 'scottplot.winforms/5.0.0'));
	assert.deepEqual(lib.packagesFromAssets({}, 'net10.0'), []);
});

test('the build output is the newest bin/<config>/<tfm>/<AssemblyName>.dll with its deps.json', () => {
	const dir = tempDir();
	try {
		const info = lib.projectInfo(path.join(dir, 'Shop.csproj'), csproj);
		assert.equal(lib.findOutput(info), undefined);
		const put = (rel, time) => {
			const file = path.join(dir, rel);
			fs.mkdirSync(path.dirname(file), { recursive: true });
			fs.writeFileSync(file, '');
			fs.writeFileSync(file.replace(/\.dll$/, '.deps.json'), '{}');
			fs.utimesSync(file, time, time);
			return file;
		};
		put('bin/Debug/net10.0/Shop.dll', 1000);
		const release = put('bin/Release/net10.0/Shop.dll', 2000);
		put('bin/Release/net10.0/publish/Shop.dll', 3000);
		fs.mkdirSync(path.join(dir, 'bin/Debug/net10.0/other'), { recursive: true });
		fs.writeFileSync(path.join(dir, 'bin/Debug/net10.0/other/Shop.dll'), ''); // no deps.json: not an output
		assert.equal(lib.findOutput(info), release);
	} finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('.vscode/netforms.json keeps each project\'s libraries', () => {
	const dir = tempDir();
	try {
		const key = lib.projectKey(dir, path.join(dir, 'src', 'Shop', 'Shop.csproj'));
		assert.equal(key, 'src/Shop/Shop.csproj');
		assert.deepEqual(lib.readConfig(dir), {});
		const entry = { id: 'package:scottplot.winforms', name: 'ScottPlot.WinForms', source: 'package', package: 'ScottPlot.WinForms', version: '5.0.0', assemblies: ['ScottPlot.WinForms.dll'] };
		let config = lib.withLibrary({ other: 1 }, key, entry);
		config = lib.withLibrary(config, key, { ...entry, version: '5.0.1' });
		lib.writeConfig(dir, config);
		const read = lib.readConfig(dir);
		assert.equal(read.other, 1, 'other settings stay');
		assert.deepEqual(lib.projectToolbox(read, key).libraries.map((l) => l.version), ['5.0.1']);
		assert.deepEqual(lib.projectToolbox(lib.withoutLibrary(read, key, entry.id), key).libraries, []);
		assert.deepEqual(lib.projectToolbox(read, 'none.csproj'), { libraries: [] });
	} finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('the toolbox groups: the project\'s components, its folders, its libraries - and none that left the csproj', () => {
	const dir = tempDir();
	try {
		const info = lib.projectInfo(path.join(dir, 'Shop.csproj'), csproj);
		const out = path.join(dir, 'bin', 'Debug', 'net10.0');
		fs.mkdirSync(out, { recursive: true });
		for (const f of ['Shop.dll', 'ScottPlot.WinForms.dll', 'Gauges.dll', 'Controls.dll']) fs.writeFileSync(path.join(out, f), '');
		const output = path.join(out, 'Shop.dll');
		assert.equal(lib.folderNamespace(info, path.join(dir, 'Controls', 'Fancy 1')), 'Shop.App.Controls.Fancy_1');
		const toolbox = {
			libraries: [
				{ id: 'folder:controls', name: 'Shop Controls', source: 'folder', path: 'Controls', namespace: 'Shop.App.Controls', assemblies: [] },
				{ id: 'package:scottplot.winforms', name: 'ScottPlot', source: 'package', package: 'ScottPlot.WinForms', assemblies: ['ScottPlot.WinForms.dll'], types: ['ScottPlot.WinForms.FormsPlot'] },
				{ id: 'dll:libs/gauges.dll', name: 'Gauges', source: 'dll', path: 'libs/Gauges.dll', assemblies: ['Gauges.dll'] },
				{ id: 'project:../controls/controls.csproj', name: 'Controls', source: 'projectReference', path: '../Controls/Controls.csproj', assemblies: ['Controls.dll'] },
				{ id: 'package:gone', name: 'Gone', source: 'package', package: 'Gone', assemblies: ['Gone.dll'] },
			],
		};
		const groups = lib.toolboxLibraries(info, csproj, toolbox, output);
		assert.deepEqual(groups.map((g) => g.name), ['Shop Components', 'Shop Controls', 'ScottPlot', 'Gauges', 'Controls']);
		assert.deepEqual(groups[0], { id: 'project', name: 'Shop Components', assemblies: [output], excludeNamespaces: ['Shop.App.Controls'] });
		assert.deepEqual(groups[1].namespaces, ['Shop.App.Controls']);
		assert.deepEqual(groups[2].types, ['ScottPlot.WinForms.FormsPlot']);
		assert.deepEqual(lib.toolboxLibraries(info, csproj, toolbox, undefined), [], 'nothing before the first build');
		// Removed from the csproj by hand: hidden.
		const noGauges = lib.removeDllReference(csproj, 'Gauges');
		assert.ok(!lib.toolboxLibraries(info, noGauges, toolbox, output).some((g) => g.name === 'Gauges'));
		assert.deepEqual(lib.toolboxLibraries(info, csproj, { ...toolbox, projectComponents: false }, output)[0].name, 'Shop Controls');

		// A referenced project rebuilt on its own: its fresh output, loaded before this project's copy.
		const otherOut = path.join(dir, 'other', 'Controls.dll');
		fs.mkdirSync(path.dirname(otherOut), { recursive: true });
		fs.writeFileSync(otherOut, '');
		fs.utimesSync(path.join(out, 'Controls.dll'), 1000, 1000);
		const fresh = lib.toolboxLibraries(info, csproj, toolbox, output, [], { 'project:../controls/controls.csproj': otherOut });
		assert.deepEqual(fresh.find((g) => g.name === 'Controls').assemblies, [otherOut]);
		assert.deepEqual(lib.assembliesToLoad(output, fresh), [otherOut, output]);
		assert.deepEqual(lib.assembliesToLoad(undefined, fresh), []);
	} finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('a package of a class library comes from the NuGet cache', () => {
	const dir = tempDir();
	try {
		const cache = path.join(dir, 'cache', 'dials', '1.0.0');
		fs.mkdirSync(path.join(cache, 'lib', 'net8.0'), { recursive: true });
		fs.writeFileSync(path.join(cache, 'lib', 'net8.0', 'Dials.dll'), '');
		const pkg = { id: 'Dials', version: '1.0.0', direct: true, assemblies: ['Dials.dll'], runtime: ['lib/net8.0/Dials.dll'], folder: cache };
		const entry = { id: 'package:dials', name: 'Dials', source: 'projectPackage', package: 'Dials', assemblies: ['Dials.dll'] };
		assert.equal(lib.libraryAssembly(entry, 'Dials.dll', path.join(dir, 'bin'), [pkg]), path.join(cache, 'lib', 'net8.0', 'Dials.dll'));
		assert.equal(lib.libraryAssembly(entry, 'Dials.dll', path.join(dir, 'bin'), []), undefined);
	} finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('nuget.org addresses, package file names and the recent list', () => {
	assert.equal(lib.nupkgUrl('ScottPlot.WinForms', '5.0.0-Beta'), 'https://api.nuget.org/v3-flatcontainer/scottplot.winforms/5.0.0-beta/scottplot.winforms.5.0.0-beta.nupkg');
	assert.equal(lib.versionsUrl('ZedGraph'), 'https://api.nuget.org/v3-flatcontainer/zedgraph/index.json');
	assert.match(lib.searchUrl('scott plot'), /\?q=scott%20plot&take=20&prerelease=false/);
	assert.deepEqual(lib.parseNupkgName('/feed/My.Gauges.2.1.0-beta.3.nupkg'), { id: 'My.Gauges', version: '2.1.0-beta.3' });
	assert.deepEqual(lib.parseNupkgName('Dials.1.0.nupkg'), { id: 'Dials', version: '1.0' });
	assert.equal(lib.parseNupkgName('readme.nupkg'), undefined);
	let recent = [];
	for (const v of ['1', '2']) recent = lib.withRecent(recent, { source: 'package', name: `ZedGraph ${v}`, package: 'ZedGraph', version: v });
	recent = lib.withRecent(recent, { source: 'dll', name: 'Gauges.dll', file: '/libs/Gauges.dll' });
	assert.deepEqual(recent.map((r) => r.name), ['Gauges.dll', 'ZedGraph 2']);
	for (let i = 0; i < 20; i++) recent = lib.withRecent(recent, { source: 'package', name: `P${i}`, package: `P${i}` });
	assert.equal(recent.length, 10);
});
