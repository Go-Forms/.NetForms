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
