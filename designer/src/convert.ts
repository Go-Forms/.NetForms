// "Convert WinForms Project…" (phase 5.7): the analysis and the rewrite are done by the converter
// (tools/NetForms.Convert); this shows its report and asks before anything is changed.
import * as cp from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';

interface Issue { severity: 'error' | 'warning' | 'info'; category: string; message: string; file?: string; line?: number; column?: number; }
interface Report {
	project: string;
	sdkStyle: boolean;
	targetFrameworks: string[];
	usesWindowsForms: boolean;
	alreadyNetForms: boolean;
	actions: string[];
	issues: Issue[];
	forms: { file: string; opens: boolean; message?: string }[];
	compiled: boolean;
	applied: boolean;
	backup?: string;
}

function findConverter(extensionPath: string, near: string): string | undefined {
	const configured = vscode.workspace.getConfiguration('netforms').get<string>('designerHostPath');
	const candidates = [
		configured ? path.join(path.dirname(configured), 'NetForms.Convert.dll') : '',
		path.join(extensionPath, 'host', 'NetForms.Convert.dll'),
	];
	for (let dir = path.dirname(near); ; dir = path.dirname(dir)) {
		for (const config of ['Release', 'Debug']) candidates.push(path.join(dir, 'tools', 'NetForms.Convert', 'bin', config, 'net10.0', 'NetForms.Convert.dll'));
		if (path.dirname(dir) === dir) break;
	}
	for (const dir of [extensionPath]) {
		for (const config of ['Release', 'Debug']) candidates.push(path.join(dir, '..', 'tools', 'NetForms.Convert', 'bin', config, 'net10.0', 'NetForms.Convert.dll'));
	}
	return candidates.find((c) => c && fs.existsSync(c));
}

function run(converter: string, args: string[]): Promise<Report> {
	const dotnet = vscode.workspace.getConfiguration('netforms').get<string>('dotnetPath') || 'dotnet';
	return new Promise((resolve, reject) => {
		cp.execFile(dotnet, [converter, ...args, '--json'], { maxBuffer: 64 * 1024 * 1024, windowsHide: true }, (err, stdout, stderr) => {
			try { resolve(JSON.parse(stdout) as Report); }
			catch { reject(new Error((stderr || String(err) || vscode.l10n.t('The converter failed.')).trim())); }
		});
	});
}

function markdown(r: Report): string {
	const lines: string[] = [];
	lines.push('# ' + vscode.l10n.t('Converting {0} to NetForms', path.basename(r.project)), '');
	if (r.alreadyNetForms) lines.push(vscode.l10n.t('The project already references NetForms.'), '');
	if (!r.sdkStyle) lines.push(vscode.l10n.t('**This is an old-style (.NET Framework) project.** Convert it to the SDK style first (for example with `dotnet upgrade-assistant`), then run this command again.'), '');
	lines.push(vscode.l10n.t('Target frameworks: {0} · uses WinForms: {1}', r.targetFrameworks.join(', ') || vscode.l10n.t('(none)'), r.usesWindowsForms ? vscode.l10n.t('yes') : vscode.l10n.t('no')), '');
	if (r.actions.length) { lines.push('## ' + vscode.l10n.t('What will change'), ''); for (const a of r.actions) lines.push(`- ${a}`); lines.push(''); }
	const errors = r.issues.filter((i) => i.severity === 'error');
	const warnings = r.issues.filter((i) => i.severity !== 'error');
	lines.push('## ' + vscode.l10n.t('Compatibility: {0}', r.compiled ? vscode.l10n.t('the code compiles against NetForms as it is') : vscode.l10n.t('{0} place(s) need attention', errors.length)), '');
	const link = (i: Issue) => i.file ? ` — [${path.basename(i.file)}${i.line ? ':' + i.line : ''}](${vscode.Uri.file(i.file).with({ fragment: i.line ? `L${i.line}` : '' }).toString()})` : '';
	for (const i of errors.slice(0, 200)) lines.push(`- **${i.category}**: ${i.message}${link(i)}`);
	if (errors.length > 200) lines.push('- ' + vscode.l10n.t('… and {0} more', errors.length - 200));
	if (warnings.length) { lines.push('', '### ' + vscode.l10n.t('Worth checking'), ''); for (const i of warnings.slice(0, 200)) lines.push(`- ${i.category}: ${i.message}${link(i)}`); }
	if (r.forms.length) {
		lines.push('', '## ' + vscode.l10n.t('Forms in the designer'), '');
		for (const f of r.forms) lines.push(`- ${f.opens ? '✔' : '✘'} ${path.basename(f.file)}${f.message ? ' — ' + f.message : ''}`);
	}
	if (r.applied) lines.push('', vscode.l10n.t('**Converted.** The original project file is saved as `{0}`.', r.backup ? path.basename(r.backup) : '(backup)'));
	return lines.join('\n');
}

export async function convertProject(extensionPath: string, uri?: vscode.Uri) {
	let project = uri?.fsPath;
	if (!project) {
		const found = await vscode.workspace.findFiles('**/*.csproj', '**/{bin,obj,node_modules}/**', 50);
		if (!found.length) { void vscode.window.showErrorMessage(vscode.l10n.t('No .csproj in the workspace.')); return; }
		const pick = found.length === 1 ? found[0] : (await vscode.window.showQuickPick(found.map((f) => ({ label: path.basename(f.fsPath), description: vscode.workspace.asRelativePath(f), uri: f })), { title: vscode.l10n.t('Project to convert') }))?.uri;
		if (!pick) return;
		project = pick.fsPath;
	}
	const converter = findConverter(extensionPath, project);
	if (!converter) { void vscode.window.showErrorMessage(vscode.l10n.t('The NetForms converter (tools/NetForms.Convert) was not found.')); return; }

	const report = await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: vscode.l10n.t('Analysing {0}…', path.basename(project)) },
		() => run(converter, [project!]));
	const doc = await vscode.workspace.openTextDocument({ language: 'markdown', content: markdown(report) });
	await vscode.commands.executeCommand('markdown.showPreview', doc.uri).then(undefined, () => vscode.window.showTextDocument(doc));
	if (!report.sdkStyle || report.alreadyNetForms || !report.usesWindowsForms) return;

	const cross = vscode.l10n.t('Windows + Linux'), windowsOnly = vscode.l10n.t('Windows only');
	const choice = await vscode.window.showInformationMessage(
		vscode.l10n.t('Convert {0} to NetForms? The original .csproj is kept as a backup.', path.basename(project)), { modal: true },
		cross, windowsOnly);
	if (!choice) return;
	const framework = vscode.workspace.getConfiguration('netforms').get<string>('frameworkPath');
	const args = [project, '--apply', '--target', choice === windowsOnly ? 'windows' : 'cross'];
	if (framework) args.push('--framework-path', framework);
	const applied = await run(converter, args);
	const done = await vscode.workspace.openTextDocument({ language: 'markdown', content: markdown(applied) });
	await vscode.commands.executeCommand('markdown.showPreview', done.uri).then(undefined, () => vscode.window.showTextDocument(done));
}
