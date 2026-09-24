// New projects and forms, from the same template files `dotnet new netforms` uses (../templates).
// The extension does the substitution itself, so it works without the templates being installed.
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';

/** The templates folder: packaged with the extension, or the repository's when run from a checkout. */
export function templatesDir(extensionPath: string): string {
	for (const candidate of [path.join(extensionPath, 'templates'), path.join(extensionPath, '..', 'templates')]) {
		if (fs.existsSync(path.join(candidate, 'netforms-app'))) return candidate;
	}
	throw new Error(vscode.l10n.t('The NetForms templates were not found next to the extension.'));
}

const identifier = /^[A-Za-z_][A-Za-z0-9_]*$/;

/**
 * Copies a template folder, renaming `sourceName` in file names and contents and replacing the
 * other tokens; `<!--#if (HasFrameworkPath) -->` blocks are resolved the way dotnet new resolves them.
 */
export function instantiate(templateDir: string, targetDir: string, replacements: Record<string, string>, frameworkPath: string | undefined): string[] {
	const written: string[] = [];
	const walk = (dir: string) => {
		for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
			if (entry.name === '.template.config' || entry.name === 'bin' || entry.name === 'obj') continue;
			const source = path.join(dir, entry.name);
			if (entry.isDirectory()) { walk(source); continue; }
			let rel = path.relative(templateDir, source);
			let text = fs.readFileSync(source, 'utf8');
			for (const [from, to] of Object.entries(replacements)) {
				rel = rel.split(from).join(to);
				text = text.split(from).join(to);
			}
			text = resolveConditionals(text, !!frameworkPath);
			const target = path.join(targetDir, rel);
			if (fs.existsSync(target)) throw new Error(`${target} already exists.`);
			fs.mkdirSync(path.dirname(target), { recursive: true });
			fs.writeFileSync(target, text, 'utf8');
			written.push(target);
		}
	};
	walk(templateDir);
	return written;
}

function resolveConditionals(text: string, hasFrameworkPath: boolean): string {
	return text.replace(/<!--#if \(HasFrameworkPath\) -->\r?\n([\s\S]*?)<!--#else -->\r?\n([\s\S]*?)<!--#endif -->\r?\n/g,
		(_, yes: string, no: string) => (hasFrameworkPath ? yes : no));
}

async function askFrameworkPath(): Promise<string | undefined | null> {
	const configured = vscode.workspace.getConfiguration('netforms').get<string>('frameworkPath');
	if (configured) return configured;
	const choice = await vscode.window.showQuickPick([
		{ label: vscode.l10n.t('NetForms package'), description: 'PackageReference Include="NetForms"', value: 'package' },
		{ label: vscode.l10n.t('A local NetForms checkout…'), description: 'ProjectReference → src/NetForms/NetForms.csproj', value: 'checkout' },
	], { title: vscode.l10n.t('Reference NetForms from') });
	if (!choice) return null;
	if (choice.value === 'package') return undefined;
	const picked = await vscode.window.showOpenDialog({ canSelectFolders: true, canSelectFiles: false, title: vscode.l10n.t('The NetForms checkout (the folder with NetForms.slnx)') });
	if (!picked) return null;
	const dir = picked[0].fsPath;
	if (!fs.existsSync(path.join(dir, 'src', 'NetForms', 'NetForms.csproj'))) throw new Error(vscode.l10n.t('{0} has no src/NetForms/NetForms.csproj.', dir));
	await vscode.workspace.getConfiguration('netforms').update('frameworkPath', dir, vscode.ConfigurationTarget.Global);
	return dir;
}

export async function newProject(extensionPath: string) {
	const parent = await vscode.window.showOpenDialog({ canSelectFolders: true, canSelectFiles: false, title: vscode.l10n.t('Where to create the project'), openLabel: vscode.l10n.t('Create here') });
	if (!parent) return;
	const name = await vscode.window.showInputBox({
		title: vscode.l10n.t('Project name'), value: 'NetFormsApp1',
		validateInput: (v) => identifier.test(v) ? undefined : vscode.l10n.t('A C# identifier: letters, digits and _, not starting with a digit.'),
	});
	if (!name) return;
	const framework = await askFrameworkPath();
	if (framework === null) return;
	const dir = path.join(parent[0].fsPath, name);
	const replacements: Record<string, string> = { NetFormsApp1: name };
	if (framework) replacements['NETFORMS_FRAMEWORK_PATH'] = path.relative(dir, framework).split(path.sep).join('/');
	instantiate(path.join(templatesDir(extensionPath), 'netforms-app'), dir, replacements, framework ?? undefined);
	const openHere = vscode.l10n.t('Open Folder'), openNew = vscode.l10n.t('Open in New Window');
	const open = await vscode.window.showInformationMessage(vscode.l10n.t('Created {0}.', name), openHere, openNew);
	if (open) await vscode.commands.executeCommand('vscode.openFolder', vscode.Uri.file(dir), open === openNew);
}

/** The RootNamespace of the nearest project above a folder, else the project's name. */
function namespaceFor(folder: string): string {
	for (let dir = folder; ; dir = path.dirname(dir)) {
		const project = fs.existsSync(dir) ? fs.readdirSync(dir).find((f) => f.endsWith('.csproj')) : undefined;
		if (project) {
			const text = fs.readFileSync(path.join(dir, project), 'utf8');
			const m = /<RootNamespace>([^<]+)<\/RootNamespace>/.exec(text);
			const root = m ? m[1].trim() : path.basename(project, '.csproj');
			const sub = path.relative(dir, folder).split(path.sep).filter(Boolean).filter((p) => identifier.test(p));
			return [root, ...sub].join('.');
		}
		if (path.dirname(dir) === dir) return 'NetFormsApp';
	}
}

export async function newForm(extensionPath: string, folderUri?: vscode.Uri) {
	let folder = folderUri?.fsPath;
	if (!folder) {
		const picked = await vscode.window.showOpenDialog({ canSelectFolders: true, canSelectFiles: false, title: vscode.l10n.t('Folder for the new form') });
		if (!picked) return;
		folder = picked[0].fsPath;
	}
	const kind = await vscode.window.showQuickPick([
		{ label: vscode.l10n.t('Form'), value: 'netforms-form', source: 'Form1', stem: 'Form', nameTitle: vscode.l10n.t('Form name') },
		{ label: vscode.l10n.t('User Control'), value: 'netforms-usercontrol', source: 'UserControl1', stem: 'UserControl', nameTitle: vscode.l10n.t('User control name') },
	], { title: vscode.l10n.t('New') });
	if (!kind) return;
	let n = 1;
	while (fs.existsSync(path.join(folder, `${kind.stem}${n}.cs`))) n++;
	const name = await vscode.window.showInputBox({
		title: kind.nameTitle, value: `${kind.stem}${n}`,
		validateInput: (v) => !identifier.test(v) ? vscode.l10n.t('A C# identifier.') : fs.existsSync(path.join(folder!, v + '.cs')) ? vscode.l10n.t('{0}.cs already exists.', v) : undefined,
	});
	if (!name) return;
	const files = instantiate(path.join(templatesDir(extensionPath), kind.value), folder,
		{ [kind.source]: name, NetFormsNamespace: namespaceFor(folder) }, undefined);
	const designer = files.find((f) => f.endsWith('.Designer.cs'));
	if (designer) await vscode.commands.executeCommand('vscode.openWith', vscode.Uri.file(designer), 'netforms.designer');
}
