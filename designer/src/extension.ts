// NetForms Designer for VS Code: the custom editor for *.Designer.cs and the commands around it.
import * as cp from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { convertProject } from './convert';
import { companionOf, DesignerEditorProvider } from './designerEditorProvider';
import { findHost } from './hostClient';
import { newForm, newProject } from './scaffold';

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
	command('netforms.newProject', () => newProject(context.extensionPath));
	command('netforms.newForm', (uri?: vscode.Uri) => newForm(context.extensionPath, uri));
	command('netforms.convertProject', (uri?: vscode.Uri) => convertProject(context.extensionPath, uri));
	command('netforms.setHostPath', async () => {
		const picked = await vscode.window.showOpenDialog({ canSelectMany: false, filters: { [vscode.l10n.t('Designer host')]: ['dll', 'exe'] }, title: 'NetFormsDesigner.Host.dll' });
		if (picked) await vscode.workspace.getConfiguration('netforms').update('designerHostPath', picked[0].fsPath, vscode.ConfigurationTarget.Global);
	});
	command('netforms.run', (uri?: vscode.Uri) => runProject(activeFile(uri)));
	command('netforms.checkSetup', async () => {
		const dotnet = vscode.workspace.getConfiguration('netforms').get<string>('dotnetPath') || 'dotnet';
		const version = await new Promise<string>((resolve) => cp.execFile(dotnet, ['--version'], (err, out) => resolve(err ? vscode.l10n.t('not found ({0})', err.message) : out.trim())));
		const host = findHost(context.extensionPath, activeFile());
		const lines = [
			`dotnet: ${version}`,
			vscode.l10n.t('designer host: {0}', host ?? vscode.l10n.t('not found — build tools/NetFormsDesigner.Host or set netforms.designerHostPath')),
			vscode.l10n.t('framework path: {0}', vscode.workspace.getConfiguration('netforms').get<string>('frameworkPath') || vscode.l10n.t('(the NetForms package)')),
		];
		log.appendLine('--- NetForms: Check Setup ---');
		for (const l of lines) log.appendLine(l);
		log.show(true);
	});
}

/** The project a file belongs to: the nearest *.csproj above it, else the only one in the workspace. */
async function projectOf(file: string | undefined): Promise<string | undefined> {
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
		{ title: vscode.l10n.t('Project to run') }))?.uri.fsPath;
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

export function deactivate() { /* sessions dispose their hosts with their editors */ }
