// NetForms: Check for Updates - everything a NetForms project stands on, brought to the newest release in one
// go: the NetForms package of the workspace's projects, the templates, this extension (from the GitHub
// release, while it is not in the Marketplace) and the .NET SDK (its command is typed into a terminal for the
// user to run: it may need sudo). Once a day the same check runs quietly and only says when there is something.
import * as cp from 'child_process';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import * as vscode from 'vscode';
import { compareVersions, netformsVersionIn, newest, newestSdk, sdkCommand, withNetFormsVersion } from './project';
import { dotnet, installedTemplates, netformsVersion } from './scaffold';

const nugetIndex = 'https://api.nuget.org/v3-flatcontainer/netforms/index.json';
const releasesApi = 'https://api.github.com/repos/Go-Forms/.NetForms/releases?per_page=10';
const sdkReleases = 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json';
const sdkDownload = 'https://dotnet.microsoft.com/download/dotnet/10.0';
const lastCheckKey = 'netforms.lastUpdateCheck';

interface Release { version: string; vsix?: string }

interface Status {
	netforms?: string; // newest NetForms on NuGet
	release?: Release; // newest GitHub release with this platform's extension
	projects: { file: string; version: string }[];
	templates?: string;
	sdk?: string; // newest installed .NET 10 SDK
	latestSdk?: string;
}

/** An update in the list; `last` runs after the others (the extension's asks to reload the window). */
type Update = vscode.QuickPickItem & { run: () => Promise<void>; last?: boolean };

async function getJson(url: string): Promise<any> {
	const response = await fetch(url, { headers: { 'User-Agent': 'netforms-designer', Accept: 'application/json' }, signal: AbortSignal.timeout(15000) });
	if (!response.ok) throw new Error(`${url}: HTTP ${response.status}`);
	return response.json();
}

/** Whatever cannot be found out (offline, a proxy) stays undefined: the check reports what it could see. */
async function quietly<T>(what: Promise<T>, log: vscode.OutputChannel, label: string): Promise<T | undefined> {
	try { return await what; } catch (err) {
		log.appendLine(`Check for updates: ${label}: ${err instanceof Error ? err.message : String(err)}`);
		return undefined;
	}
}

async function status(log: vscode.OutputChannel): Promise<Status> {
	const vsixName = `netforms-designer-${process.platform}-${process.arch}.vsix`;
	const dotnetPath = vscode.workspace.getConfiguration('netforms').get<string>('dotnetPath') || 'dotnet';
	const [nuget, releases, sdks, latestSdk, templates, projectFiles] = await Promise.all([
		quietly(getJson(nugetIndex), log, 'NuGet'),
		quietly(getJson(releasesApi), log, 'GitHub releases'),
		new Promise<string>((resolve) => cp.execFile(dotnetPath, ['--list-sdks'], (err, out) => resolve(err ? '' : out))),
		quietly(getJson(sdkReleases), log, '.NET release metadata'),
		quietly(installedTemplates(), log, 'templates'),
		vscode.workspace.findFiles('**/{*.csproj,Directory.Packages.props}', '**/{bin,obj,node_modules}/**', 200),
	]);
	const release = (Array.isArray(releases) ? releases : [])
		.filter((r: any) => !r.draft && typeof r.tag_name === 'string')
		.map((r: any): Release => ({ version: r.tag_name.replace(/^v/, ''), vsix: r.assets?.find((a: any) => a.name === vsixName)?.browser_download_url }))
		.sort((a: Release, b: Release) => compareVersions(a.version, b.version)).pop();
	const projects: Status['projects'] = [];
	for (const f of projectFiles) {
		const version = netformsVersionIn((await vscode.workspace.openTextDocument(f)).getText());
		if (version) projects.push({ file: f.fsPath, version });
	}
	return {
		netforms: newest(nuget?.versions ?? []),
		release,
		projects,
		templates,
		sdk: newestSdk(sdks),
		latestSdk: latestSdk?.['latest-sdk'],
	};
}

/** What is behind, as items of the update list (all picked). */
function updates(s: Status, context: vscode.ExtensionContext, log: vscode.OutputChannel): Update[] {
	const items: Update[] = [];
	const own = netformsVersion(context);
	const target = newest([s.netforms, own])!;

	if (s.release?.vsix && compareVersions(s.release.version, own) > 0) {
		const vsix = s.release.vsix;
		items.push({
			label: vscode.l10n.t('NetForms Designer extension'),
			description: vscode.l10n.t('{0} → the one of release {1}', context.extension.packageJSON.version, s.release.version),
			picked: true,
			last: true,
			run: () => installExtension(vsix),
		});
	}
	for (const p of s.projects) {
		if (compareVersions(p.version, target) >= 0) continue;
		items.push({
			label: vscode.l10n.t('NetForms in {0}', path.basename(p.file)),
			description: `${p.version} → ${target}`,
			detail: vscode.workspace.asRelativePath(p.file),
			picked: true,
			run: () => updateProject(p.file, target),
		});
	}
	if (!s.templates || compareVersions(s.templates, target) < 0) {
		items.push({
			label: vscode.l10n.t('NetForms templates (dotnet new)'),
			description: `${s.templates ?? vscode.l10n.t('not installed')} → ${target}`,
			picked: true,
			run: () => installTemplates(target, log),
		});
	}
	if (!s.sdk || (s.latestSdk && compareVersions(s.sdk, s.latestSdk) < 0)) {
		items.push({
			label: s.sdk ? vscode.l10n.t('.NET SDK') : vscode.l10n.t('.NET 10 SDK (not installed)'),
			description: s.sdk ? `${s.sdk} → ${s.latestSdk}` : s.latestSdk ?? '',
			detail: vscode.l10n.t('The command is typed into a terminal for you to check and run'),
			picked: true,
			run: () => updateSdk(!!s.sdk),
		});
	}
	return items;
}

/** The command: the list of what is behind, then the picked updates one after another. */
export async function checkForUpdates(context: vscode.ExtensionContext, log: vscode.OutputChannel) {
	const s = await vscode.window.withProgress(
		{ location: vscode.ProgressLocation.Notification, title: vscode.l10n.t('Checking for NetForms updates…') },
		() => status(log));
	void context.globalState.update(lastCheckKey, Date.now());
	const items = updates(s, context, log);
	if (!items.length) {
		const parts = [
			`NetForms ${newest([s.netforms, netformsVersion(context)])}`,
			vscode.l10n.t('extension {0}', context.extension.packageJSON.version),
			s.sdk ? `.NET SDK ${s.sdk}` : undefined,
		].filter(Boolean);
		const unknown = !s.netforms || !s.release ? ' ' + vscode.l10n.t('(NuGet or GitHub could not be reached: details in the NetForms output.)') : '';
		void vscode.window.showInformationMessage(vscode.l10n.t('Everything is up to date: {0}.', parts.join(', ')) + unknown);
		return;
	}
	const picked = await vscode.window.showQuickPick(items, { canPickMany: true, title: vscode.l10n.t('NetForms updates'), placeHolder: vscode.l10n.t('Pick what to update') });
	if (!picked?.length) return;
	for (const item of [...picked.filter((i) => !i.last), ...picked.filter((i) => i.last)]) {
		try { await item.run(); } catch (err) {
			void vscode.window.showErrorMessage(`${item.label}: ${err instanceof Error ? err.message : String(err)}`);
		}
	}
}

/** Once a day, after start-up: quiet unless something is behind. */
export function scheduleUpdateCheck(context: vscode.ExtensionContext, log: vscode.OutputChannel) {
	if (!vscode.workspace.getConfiguration('netforms').get<boolean>('checkForUpdates', true)) return;
	const last = context.globalState.get<number>(lastCheckKey, 0);
	if (Date.now() - last < 24 * 60 * 60 * 1000) return;
	const timer = setTimeout(async () => {
		const s = await status(log);
		void context.globalState.update(lastCheckKey, Date.now());
		const items = updates(s, context, log);
		if (!items.length) return;
		const update = vscode.l10n.t('Update…'), later = vscode.l10n.t('Later');
		const answer = await vscode.window.showInformationMessage(vscode.l10n.t('NetForms updates: {0}.', items.map((i) => i.label).join(', ')), update, later);
		if (answer === update) await vscode.commands.executeCommand('netforms.checkForUpdates');
	}, 15000);
	context.subscriptions.push({ dispose: () => clearTimeout(timer) });
}

async function updateProject(file: string, version: string) {
	const doc = await vscode.workspace.openTextDocument(file);
	const text = withNetFormsVersion(doc.getText(), version);
	const edit = new vscode.WorkspaceEdit();
	edit.replace(doc.uri, new vscode.Range(doc.positionAt(0), doc.positionAt(doc.getText().length)), text);
	await vscode.workspace.applyEdit(edit);
	await doc.save();
	void vscode.window.showInformationMessage(vscode.l10n.t('{0} now references NetForms {1}; the next build restores it.', path.basename(file), version));
}

async function installTemplates(version: string, log: vscode.OutputChannel) {
	const result = await vscode.window.withProgress(
		{ location: vscode.ProgressLocation.Notification, title: vscode.l10n.t('Installing the NetForms templates {0} from NuGet…', version) },
		() => dotnet(['new', 'install', `NetForms.Templates::${version}`], os.homedir()));
	log.appendLine(`dotnet new install NetForms.Templates::${version}\n${result.output}`);
	if (result.code !== 0) throw new Error(vscode.l10n.t('dotnet new install failed (details in the NetForms output).'));
}

/** The .vsix of the release, downloaded and installed over this extension; then a reload. */
async function installExtension(url: string) {
	const file = path.join(os.tmpdir(), path.basename(new URL(url).pathname));
	await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: vscode.l10n.t('Downloading the NetForms Designer extension…') }, async () => {
		const response = await fetch(url, { headers: { 'User-Agent': 'netforms-designer' }, signal: AbortSignal.timeout(300000) });
		if (!response.ok) throw new Error(`${url}: HTTP ${response.status}`);
		fs.writeFileSync(file, Buffer.from(await response.arrayBuffer()));
		await vscode.commands.executeCommand('workbench.extensions.installExtension', vscode.Uri.file(file));
	});
	const reload = vscode.l10n.t('Reload Window');
	if (await vscode.window.showInformationMessage(vscode.l10n.t('The NetForms Designer extension is updated. Reload the window to use it.'), reload) === reload)
		await vscode.commands.executeCommand('workbench.action.reloadWindow');
}

/** The SDK's own installer is the user's to run: the command goes into a terminal, not executed. */
async function updateSdk(installed: boolean) {
	let osRelease = '';
	try { osRelease = fs.readFileSync('/etc/os-release', 'utf8'); } catch { /* not Linux */ }
	const command = sdkCommand(process.platform, osRelease, installed);
	const terminal = vscode.window.createTerminal({ name: vscode.l10n.t('NetForms: .NET SDK') });
	terminal.show();
	terminal.sendText(command, false);
	const page = vscode.l10n.t('Open the Download Page');
	if (await vscode.window.showInformationMessage(vscode.l10n.t('The command to update the .NET SDK is in the terminal: check it and press Enter. Restart VS Code afterwards.'), page) === page)
		await vscode.env.openExternal(vscode.Uri.parse(sdkDownload));
}
