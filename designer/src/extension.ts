// NetForms Designer for VS Code: the custom editor for *.Designer.cs and the commands around it.
import * as cp from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { convertProject } from './convert';
import { companionOf, DesignerEditorProvider } from './designerEditorProvider';
import { findHost, HostClient } from './hostClient';
import { addControlLibrary, removeControlLibrary, rescanToolbox } from './libraryCommands';
import { formClass, publishArgs, publishTargets, setStartupForm, startsAForm } from './project';
import { installedTemplates, netformsVersion, newForm, newProject } from './scaffold';
import { checkForUpdates, scheduleUpdateCheck } from './updates';

export function activate(context: vscode.ExtensionContext) {
	const log = vscode.window.createOutputChannel('NetForms');
	context.subscriptions.push(log);

	context.subscriptions.push(vscode.window.registerCustomEditorProvider(
		DesignerEditorProvider.viewType,
		new DesignerEditorProvider(context, log),
		{ webviewOptions: { retainContextWhenHidden: true }, supportsMultipleEditorsPerDocument: false },
	));

	const command = (id: string, fn: (...args: any[]) => unknown) =>
		context.subscriptions.push(vscode.commands.registerCommand(id, async (...args: any[]) => {
			try { await fn(...args); }
			catch (err) { void vscode.window.showErrorMessage(err instanceof Error ? err.message : String(err)); }
		}));

	const activeFile = (uri?: vscode.Uri) => uri?.fsPath ?? vscode.window.activeTextEditor?.document.uri.fsPath ?? DesignerEditorProvider.active?.file;

	command('netforms.openDesigner', async (uri?: vscode.Uri) => {
		const file = activeFile(uri);
		if (!file) throw new Error(vscode.l10n.t('Open a *.Designer.cs file first.'));
		const designer = /\.Designer\.cs$/i.test(file) ? file : file.replace(/\.cs$/i, '.Designer.cs');
		if (!fs.existsSync(designer)) throw new Error(vscode.l10n.t('{0} does not exist.', path.basename(designer)));
		await vscode.commands.executeCommand('vscode.openWith', vscode.Uri.file(designer), DesignerEditorProvider.viewType);
	});
	command('netforms.openAsText', async (uri?: vscode.Uri) => {
		const file = activeFile(uri);
		if (file) await vscode.commands.executeCommand('vscode.openWith', vscode.Uri.file(file), 'default');
	});
	command('netforms.viewCode', async () => {
		const session = DesignerEditorProvider.active;
		if (session) { await session.viewCode(); return; }
		const file = activeFile();
		if (file && /\.Designer\.cs$/i.test(file)) await vscode.window.showTextDocument(vscode.Uri.file(companionOf(file)));
	});
	command('netforms.viewDesigner', async () => {
		const file = activeFile();
		if (!file) return;
		const designer = file.replace(/(\.Designer)?\.cs$/i, '.Designer.cs');
		if (!fs.existsSync(designer)) throw new Error(vscode.l10n.t('{0} has no designer file.', path.basename(file)));
		await vscode.commands.executeCommand('vscode.openWith', vscode.Uri.file(designer), DesignerEditorProvider.viewType);
	});
	command('netforms.tidy', async () => {
		const session = DesignerEditorProvider.active;
		if (!session) throw new Error(vscode.l10n.t('Open the file in the NetForms Designer to tidy it.'));
		await session.tidy();
	});
	command('netforms.newProject', () => newProject(context, log));
	command('netforms.newForm', (uri?: vscode.Uri) => newForm(context, log, uri));
	command('netforms.convertProject', (uri?: vscode.Uri) => convertProject(context.extensionPath, uri));
	command('netforms.setHostPath', async () => {
		const picked = await vscode.window.showOpenDialog({ canSelectMany: false, filters: { [vscode.l10n.t('Designer host')]: ['dll', 'exe'] }, title: 'NetFormsDesigner.Host.dll' });
		if (picked) await vscode.workspace.getConfiguration('netforms').update('designerHostPath', picked[0].fsPath, vscode.ConfigurationTarget.Global);
	});
	command('netforms.run', (uri?: vscode.Uri) => runProject(activeFile(uri)));
	command('netforms.setStartupForm', (uri?: vscode.Uri) => setStartup(activeFile(uri)));
	command('netforms.publish', (uri?: vscode.Uri) => publishProject(activeFile(uri)));
	command('netforms.checkForUpdates', () => checkForUpdates(context, log));

	// Control libraries (decision 157). The scan before adding needs a host: the open designer's, else one of
	// the commands' own, started on first use.
	let toolHost: HostClient | undefined;
	const host = () => {
		const session = DesignerEditorProvider.active;
		if (session) return session.hostClient();
		if (toolHost?.alive) return toolHost;
		const hostPath = findHost(context.extensionPath, activeFile());
		if (!hostPath) throw new Error(vscode.l10n.t('The NetForms designer host was not found. Build tools/NetFormsDesigner.Host or set "netforms.designerHostPath".'));
		toolHost = new HostClient(hostPath, log);
		return toolHost;
	};
	context.subscriptions.push({ dispose: () => toolHost?.dispose() });
	command('netforms.addControlLibrary', (uri?: vscode.Uri) => addControlLibrary(context, log, host, activeFile(uri)));
	command('netforms.removeControlLibrary', (uri?: vscode.Uri) => removeControlLibrary(log, activeFile(uri)));
	command('netforms.rescanToolbox', (uri?: vscode.Uri) => rescanToolbox(log, activeFile(uri)));
	scheduleUpdateCheck(context, log);
	command('netforms.checkSetup', async () => {
		const dotnet = vscode.workspace.getConfiguration('netforms').get<string>('dotnetPath') || 'dotnet';
		const version = await new Promise<string>((resolve) => cp.execFile(dotnet, ['--version'], (err, out) => resolve(err ? vscode.l10n.t('not found ({0})', err.message) : out.trim())));
		const host = findHost(context.extensionPath, activeFile());
		const lines = [
			`dotnet: ${version}`,
			vscode.l10n.t('designer host: {0}', host ?? vscode.l10n.t('not found — build tools/NetFormsDesigner.Host or set netforms.designerHostPath')),
			vscode.l10n.t('templates: {0} installed, {1} expected (installed from NuGet on the first New Project / New Form)', (await installedTemplates()) ?? vscode.l10n.t('none'), netformsVersion(context)),
		];
		log.appendLine('--- NetForms: Check Setup ---');
		for (const l of lines) log.appendLine(l);
		log.show(true);
	});
}

/** The project a file belongs to: the nearest *.csproj above it, else the only one in the workspace. */
async function projectOf(file: string | undefined, title = vscode.l10n.t('Project to run')): Promise<string | undefined> {
	if (file) {
		for (let dir = path.dirname(file); ; dir = path.dirname(dir)) {
			const project = fs.existsSync(dir) ? fs.readdirSync(dir).find((f) => f.endsWith('.csproj')) : undefined;
			if (project) return path.join(dir, project);
			if (path.dirname(dir) === dir) break;
		}
	}
	const found = await vscode.workspace.findFiles('**/*.csproj', '**/{bin,obj,node_modules}/**', 50);
	if (found.length <= 1) return found[0]?.fsPath;
	return (await vscode.window.showQuickPick(found.map((f) => ({ label: path.basename(f.fsPath), description: vscode.workspace.asRelativePath(f), uri: f })),
		{ title }))?.uri.fsPath;
}

/** NetForms: Run - `dotnet run` in a terminal, so a build error is the compiler's own, with a clickable place. */
async function runProject(file: string | undefined) {
	const project = await projectOf(file);
	if (!project) throw new Error(vscode.l10n.t('No .csproj in the workspace.'));
	const dotnet = vscode.workspace.getConfiguration('netforms').get<string>('dotnetPath') || 'dotnet';
	const name = `NetForms: ${path.basename(project, '.csproj')}`;
	vscode.window.terminals.find((t) => t.name === name)?.dispose();
	const terminal = vscode.window.createTerminal({ name, cwd: path.dirname(project) });
	terminal.show(true);
	terminal.sendText(`${/\s/.test(dotnet) ? `"${dotnet}"` : dotnet} run --project "${project}"`);
}

/** The .cs files of a project's folder, bin/obj/node_modules aside. */
function projectSources(dir: string): string[] {
	const out: string[] = [];
	for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
		if (entry.isDirectory()) {
			if (!/^(bin|obj|node_modules|\..*)$/i.test(entry.name)) out.push(...projectSources(path.join(dir, entry.name)));
		} else if (entry.name.endsWith('.cs') && !entry.name.endsWith('.Designer.cs')) out.push(path.join(dir, entry.name));
	}
	return out;
}

/** NetForms: Set as Startup Form - Application.Run in Program.cs starts this form. */
async function setStartup(file: string | undefined) {
	if (!file || !file.endsWith('.cs')) throw new Error(vscode.l10n.t('Open a form (its .cs or .Designer.cs file) first.'));
	const code = companionOf(file.replace(/(\.Designer)?\.cs$/i, '.Designer.cs'));
	const form = formClass(fs.readFileSync(fs.existsSync(code) ? code : file, 'utf8'));
	if (!form) throw new Error(vscode.l10n.t('{0} declares no class.', path.basename(file)));
	const project = await projectOf(file);
	if (!project) throw new Error(vscode.l10n.t('No .csproj in the workspace.'));
	// The file as the editor holds it, unsaved changes included.
	const read = async (f: string) => (await vscode.workspace.openTextDocument(f)).getText();
	let program: string | undefined;
	for (const f of projectSources(path.dirname(project))) {
		if (startsAForm(await read(f))) { program = f; break; }
	}
	if (!program) throw new Error(vscode.l10n.t('No file of {0} starts a form with Application.Run(new …()).', path.basename(project)));
	const doc = await vscode.workspace.openTextDocument(program);
	const changed = setStartupForm(doc.getText(), form)!;
	if (changed.text === doc.getText()) {
		void vscode.window.showInformationMessage(vscode.l10n.t('{0} is already the startup form.', form.name));
		return;
	}
	const edit = new vscode.WorkspaceEdit();
	edit.replace(doc.uri, new vscode.Range(doc.positionAt(0), doc.positionAt(doc.getText().length)), changed.text);
	await vscode.workspace.applyEdit(edit);
	await doc.save();
	const open = vscode.l10n.t('Open {0}', path.basename(program));
	if (await vscode.window.showInformationMessage(vscode.l10n.t('{0} starts now instead of {1}.', form.name, changed.previous), open) === open)
		await vscode.window.showTextDocument(doc);
}

/** NetForms: Publish Application - dotnet publish for a Windows or Linux machine, into publish/<rid> next to the project. */
async function publishProject(file: string | undefined) {
	const project = await projectOf(file, vscode.l10n.t('Project to publish'));
	if (!project) throw new Error(vscode.l10n.t('No .csproj in the workspace.'));
	const here = `${process.platform === 'win32' ? 'win' : 'linux'}-${process.arch === 'arm64' ? 'arm64' : 'x64'}`;
	const names: Record<string, string> = {
		'win-x64': 'Windows x64', 'win-arm64': 'Windows ARM64', 'linux-x64': 'Linux x64', 'linux-arm64': 'Linux ARM64',
	};
	const targets = [...publishTargets].sort((a, b) => Number(b === here) - Number(a === here))
		.map((rid) => ({ label: names[rid], description: rid === here ? vscode.l10n.t('{0} — this machine', rid) : rid, rid }));
	const target = await vscode.window.showQuickPick(targets, { title: vscode.l10n.t('Publish for') });
	if (!target) return;
	const mode = await vscode.window.showQuickPick([
		{ label: vscode.l10n.t('Self-contained'), description: vscode.l10n.t('Carries .NET along: nothing to install on the target machine'), selfContained: true },
		{ label: vscode.l10n.t('Framework-dependent'), description: vscode.l10n.t('Smaller; needs the .NET 10 runtime on the target machine'), selfContained: false },
	], { title: vscode.l10n.t('How to publish') });
	if (!mode) return;
	const output = path.join(path.dirname(project), 'publish', target.rid);
	const dotnet = vscode.workspace.getConfiguration('netforms').get<string>('dotnetPath') || 'dotnet';
	const task = new vscode.Task({ type: 'process' }, vscode.TaskScope.Workspace, `publish ${path.basename(project, '.csproj')} (${target.rid})`, 'NetForms',
		new vscode.ProcessExecution(dotnet, publishArgs(project, target.rid, mode.selfContained, output), { cwd: path.dirname(project) }));
	const execution = await vscode.tasks.executeTask(task);
	const ended = vscode.tasks.onDidEndTaskProcess(async (e) => {
		if (e.execution !== execution) return;
		ended.dispose();
		if (e.exitCode !== 0) {
			void vscode.window.showErrorMessage(vscode.l10n.t('dotnet publish failed (exit code {0}); the terminal shows why.', String(e.exitCode)));
			return;
		}
		const reveal = vscode.l10n.t('Show the Folder');
		if (await vscode.window.showInformationMessage(vscode.l10n.t('Published to {0}.', output), reveal) === reveal)
			await vscode.commands.executeCommand('revealFileInOS', vscode.Uri.file(output));
	});
}

export function deactivate() { /* sessions dispose their hosts with their editors */ }
