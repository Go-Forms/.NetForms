// node --test: the project commands' text work (src/project.ts, compiled on the fly with esbuild).
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const esbuild = require('esbuild');

const source = fs.readFileSync(path.join(__dirname, '..', 'src', 'project.ts'), 'utf8');
const { code } = esbuild.transformSync(source, { loader: 'ts', format: 'cjs' });
const project = {};
const loaded = { exports: project };
new Function('module', 'exports', code)(loaded, project);
Object.assign(project, loaded.exports);

const program = `namespace Shop
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
`;

test('the class a form file declares', () => {
	assert.deepEqual(project.formClass('namespace Shop.Views;\n\npublic partial class SettingsForm : Form { }'), { ns: 'Shop.Views', name: 'SettingsForm' });
	assert.deepEqual(project.formClass('namespace Shop\n{\n    // class Old\n    partial class MainForm\n    {\n    }\n}'), { ns: 'Shop', name: 'MainForm' });
	assert.equal(project.formClass('namespace Shop;'), undefined);
});

test('Application.Run starts the chosen form, named as C# resolves it from Program', () => {
	assert.ok(project.startsAForm(program));
	assert.ok(!project.startsAForm('class Foo { void M() { var f = new MainForm(); Application.Run(f); } }'));

	const same = project.setStartupForm(program, { ns: 'Shop', name: 'LoginForm' });
	assert.equal(same.previous, 'MainForm');
	assert.match(same.text, /Application\.Run\(new LoginForm\(\)\);/);
	assert.equal(same.text.replace('LoginForm', 'MainForm'), program);

	assert.match(project.setStartupForm(program, { ns: 'Shop.Views', name: 'AboutBox' }).text, /Application\.Run\(new Views\.AboutBox\(\)\);/);
	assert.match(project.setStartupForm(program, { ns: 'Other', name: 'Main' }).text, /Application\.Run\(new Other\.Main\(\)\);/);
	assert.match(project.setStartupForm(program, { name: 'Global' }).text, /Application\.Run\(new Global\(\)\);/);
	const spaced = program.replace('Application.Run(new MainForm());', 'Application.Run( new Shop.MainForm( ) );');
	assert.match(project.setStartupForm(spaced, { ns: 'Shop', name: 'X' }).text, /Application\.Run\( new X\( \) \);/);
	assert.equal(project.setStartupForm('class P { }', { name: 'X' }), undefined);
});

test('dotnet publish arguments', () => {
	assert.deepEqual([...project.publishTargets], ['win-x64', 'win-arm64', 'linux-x64', 'linux-arm64']);
	assert.deepEqual(project.publishArgs('/p/App.csproj', 'linux-x64', true, '/p/publish/linux-x64'),
		['publish', '/p/App.csproj', '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'true', '-o', '/p/publish/linux-x64', '-nologo']);
});

test('versions compare as NuGet compares them', () => {
	const sorted = ['0.1.0', '0.1.0-preview.10', '0.0.9', '0.1.0-preview.9', '0.1.0-preview.2', 'v0.2.0', '0.1.1-alpha'].sort(project.compareVersions);
	assert.deepEqual(sorted, ['0.0.9', '0.1.0-preview.2', '0.1.0-preview.9', '0.1.0-preview.10', '0.1.0', '0.1.1-alpha', 'v0.2.0']);
	assert.equal(project.newest(['0.1.0-preview.4', undefined, '0.1.0-preview.5']), '0.1.0-preview.5');
	assert.equal(project.compareVersions('0.1.0-preview.5', 'v0.1.0-preview.5'), 0);
});

test('the NetForms version of a project, read and changed', () => {
	const csproj = '<Project>\n  <ItemGroup>\n    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />\n    <PackageReference Include="NetForms" Version="0.1.0-preview.4" />\n  </ItemGroup>\n</Project>\n';
	assert.equal(project.netformsVersionIn(csproj), '0.1.0-preview.4');
	const updated = project.withNetFormsVersion(csproj, '0.1.0-preview.6');
	assert.equal(updated, csproj.replace('0.1.0-preview.4', '0.1.0-preview.6'));
	assert.equal(project.netformsVersionIn('<PackageVersion Version="1.2.3" Include="NetForms" />'), '1.2.3');
	assert.equal(project.netformsVersionIn('<PackageReference Include="NetForms.Drawing" Version="1.0.0" />'), undefined);
});

test('the .NET SDK: what is installed and how to update it', () => {
	assert.equal(project.newestSdk('8.0.125 [/usr/lib/dotnet/sdk]\n10.0.104 [/usr/lib/dotnet/sdk]\n10.0.112 [/usr/lib/dotnet/sdk]\n'), '10.0.112');
	assert.equal(project.newestSdk('8.0.125 [/usr/lib/dotnet/sdk]'), undefined);
	assert.equal(project.sdkCommand('win32', '', true), 'winget upgrade Microsoft.DotNet.SDK.10');
	assert.match(project.sdkCommand('linux', 'NAME="Ubuntu"\nID=ubuntu\nID_LIKE=debian\n', true), /apt install --only-upgrade dotnet-sdk-10\.0/);
	assert.match(project.sdkCommand('linux', 'ID="fedora"\n', false), /dnf install dotnet-sdk-10\.0/);
	assert.match(project.sdkCommand('linux', 'ID=arch\n', false), /dotnet-install\.sh/);
});
