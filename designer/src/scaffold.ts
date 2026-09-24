// New projects and forms, made by `dotnet new` from the NetForms.Templates package on NuGet - the same
// templates a user gets on the command line. The extension ships no copy of them: the package version it
// asks for is `netformsVersion` in package.json (kept equal to Directory.Build.props by test/version.test.js),
// and a new project references the NetForms package of that version.
import * as cp from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';

const identifier = /^[A-Za-z_][A-Za-z0-9_]*$/;
const templatesPackage = 'NetForms.Templates';

function dotnetPath(): string {
	return vscode.workspace.getConfiguration('netforms').get<string>('dotnetPath') || 'dotnet';
}

/** Runs dotnet with the CLI's first-run noise off; resolves with the exit code and all output. */
export function dotnet(args: string[], cwd: string): Promise<{ code: number; output: string }> {
	return new Promise((resolve) => {
		// English output whatever the OS language: installedTemplates() reads it.
		const env = { ...process.env, DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_CLI_UI_LANGUAGE: 'en' };
		cp.execFile(dotnetPath(), args, { cwd, env, maxBuffer: 16 * 1024 * 1024 }, (err, stdout, stderr) => {
			const code = err ? (typeof (err as any).code === 'number' ? (err as any).code : -1) : 0;
			resolve({ code, output: `${stdout}${stderr}${err && code === -1 ? err.message : ''}` });
		});
	});
}

/** The NetForms version this extension creates projects for. */
export function netformsVersion(context: vscode.ExtensionContext): string {
	return context.extension.packageJSON.netformsVersion as string;
}

/** The installed version of the templates package, from `dotnet new uninstall` (which lists what is installed). */
export async function installedTemplates(): Promise<string | undefined> {
	const { output } = await dotnet(['new', 'uninstall'], process.cwd());
	const lines = output.split(/\r?\n/);
	const at = lines.findIndex((l) => l.trim() === templatesPackage);
	if (at < 0) return undefined;
	for (const line of lines.slice(at + 1, at + 4)) {
		const m = /^\s*Version:\s*(\S+)/.exec(line);
		if (m) return m[1];
	}
	return undefined;
}

/** Makes sure `dotnet new` has NetForms.Templates of this extension's version, installing it from NuGet. */
async function ensureTemplates(context: vscode.ExtensionContext, log: vscode.OutputChannel): Promise<boolean> {
	const wanted = netformsVersion(context);
	if (await installedTemplates() === wanted) return true;
	const result = await vscode.window.withProgress(
		{ location: vscode.ProgressLocation.Notification, title: vscode.l10n.t('Installing the NetForms templates {0} from NuGet…', wanted) },
		() => dotnet(['new', 'install', `${templatesPackage}::${wanted}`], process.cwd()));
	log.appendLine(`dotnet new install ${templatesPackage}::${wanted}\n${result.output}`);
	if (result.code === 0) return true;
	log.show(true);
	void vscode.window.showErrorMessage(vscode.l10n.t('Could not install {0} from NuGet. Check the network and your NuGet sources (details in the NetForms output).', `${templatesPackage} ${wanted}`));
	return false;
}

async function runNew(args: string[], cwd: string, log: vscode.OutputChannel): Promise<boolean> {
	const result = await dotnet(['new', ...args], cwd);
	log.appendLine(`dotnet new ${args.join(' ')}\n${result.output}`);
	if (result.code === 0) return true;
	log.show(true);
	void vscode.window.showErrorMessage(vscode.l10n.t('dotnet new {0} failed (details in the NetForms output).', args[0]));
	return false;
}

export async function newProject(context: vscode.ExtensionContext, log: vscode.OutputChannel) {
	const parent = await vscode.window.showOpenDialog({ canSelectFolders: true, canSelectFiles: false, title: vscode.l10n.t('Where to create the project'), openLabel: vscode.l10n.t('Create here') });
	if (!parent) return;
	const name = await vscode.window.showInputBox({
		title: vscode.l10n.t('Project name'), value: 'NetFormsApp1',
		validateInput: (v) => !identifier.test(v) ? vscode.l10n.t('A C# identifier: letters, digits and _, not starting with a digit.')
			: fs.existsSync(path.join(parent[0].fsPath, v)) ? vscode.l10n.t('{0} already exists.', v) : undefined,
	});
	if (!name) return;
	if (!await ensureTemplates(context, log)) return;
	const dir = path.join(parent[0].fsPath, name);
	if (!await runNew(['netforms', '-n', name, '-o', dir], parent[0].fsPath, log)) return;
	const openHere = vscode.l10n.t('Open Folder'), openNew = vscode.l10n.t('Open in New Window');
	const open = await vscode.window.showInformationMessage(vscode.l10n.t('Created {0}.', name), openHere, openNew);
	if (open) await vscode.commands.executeCommand('vscode.openFolder', vscode.Uri.file(dir), open === openNew);
}

/** The RootNamespace of the nearest project above a folder, plus the folders below it - as Visual Studio names new items. */
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

export async function newForm(context: vscode.ExtensionContext, log: vscode.OutputChannel, folderUri?: vscode.Uri) {
	let folder = folderUri?.fsPath;
	if (!folder) {
		const picked = await vscode.window.showOpenDialog({ canSelectFolders: true, canSelectFiles: false, title: vscode.l10n.t('Folder for the new form') });
		if (!picked) return;
		folder = picked[0].fsPath;
	}
	const kind = await vscode.window.showQuickPick([
		{ label: vscode.l10n.t('Form'), value: 'netforms-form', stem: 'Form', nameTitle: vscode.l10n.t('Form name') },
		{ label: vscode.l10n.t('User Control'), value: 'netforms-usercontrol', stem: 'UserControl', nameTitle: vscode.l10n.t('User control name') },
	], { title: vscode.l10n.t('New') });
	if (!kind) return;
	let n = 1;
	while (fs.existsSync(path.join(folder, `${kind.stem}${n}.cs`))) n++;
	const exists = (v: string) => fs.existsSync(path.join(folder!, v + '.cs')) || fs.existsSync(path.join(folder!, v + '.Designer.cs'));
	const name = await vscode.window.showInputBox({
		title: kind.nameTitle, value: `${kind.stem}${n}`,
		validateInput: (v) => !identifier.test(v) ? vscode.l10n.t('A C# identifier.') : exists(v) ? vscode.l10n.t('{0}.cs already exists.', v) : undefined,
	});
	if (!name) return;
	if (!await ensureTemplates(context, log)) return;
	// The namespace is passed, so the item template does not need the project restored to read it; a project
	// that has never been restored does not meet the template's C#-project constraint yet, hence --force
	// (safe: both files were checked not to exist).
	const args = [kind.value, '-n', name, '-o', folder, '--Namespace', namespaceFor(folder)];
	const first = await dotnet(['new', ...args], folder);
	log.appendLine(`dotnet new ${args.join(' ')}\n${first.output}`);
	if (first.code !== 0 && !await runNew([...args, '--force'], folder, log)) return;
	const designer = path.join(folder, `${name}.Designer.cs`);
	if (fs.existsSync(designer)) await vscode.commands.executeCommand('vscode.openWith', vscode.Uri.file(designer), 'netforms.designer');
}
