// Control libraries in the designer (decision 157, docs/designer-control-libraries.md): the commands
// NetForms: Add Control Library…, Remove Control Library and Rescan Toolbox, and what a designer session
// asks for its project. A library is only ever added by one of these commands - nothing is installed on
// its own when a form opens or a control is dropped. All sources meet in one pipeline: a reference in the
// csproj (MSBuild resolves packages and dependencies, not us), `dotnet build`, the build output loaded by
// the host, a toolbox group.
import * as fs from 'fs';
import * as https from 'https';
import * as os from 'os';
import * as path from 'path';
import * as vscode from 'vscode';
import { HostClient, LibraryItem, LibraryReport, PackageReport } from './hostClient';
import {
	addDllReference, assembliesToLoad, findOutput, folderNamespace, LibraryEntry, nupkgUrl, packagesFromAssets, parseNupkgName,
	projectInfo, ProjectInfo, projectKey, projectToolbox, readConfig, RecentLibrary, removeDllReference, ResolvedPackage, searchUrl,
	toolboxLibraries, ToolboxLibrary, versionsUrl, withLibrary, withoutLibrary, withRecent, writeConfig,
} from './libraries';
import { dotnet } from './scaffold';

/** Fired with a project file when its libraries changed (added, removed, rebuilt): its designers reload them. */
export const librariesChanged = new vscode.EventEmitter<string>();

const recentKey = 'netforms.recentLibraries';

/** The nearest *.csproj above a file. */
export function projectFileOf(file: string): string | undefined {
	for (let dir = path.dirname(file); ; dir = path.dirname(dir)) {
		const project = fs.existsSync(dir) ? fs.readdirSync(dir).find((f) => f.toLowerCase().endsWith('.csproj')) : undefined;
		if (project) return path.join(dir, project);
		if (path.dirname(dir) === dir) return undefined;
	}
}

function workspaceFolderOf(file: string): string {
	return vscode.workspace.getWorkspaceFolder(vscode.Uri.file(file))?.uri.fsPath ?? path.dirname(file);
}

function readAssets(project: ProjectInfo): ResolvedPackage[] {
	try {
		const assets = JSON.parse(fs.readFileSync(path.join(project.dir, 'obj', 'project.assets.json'), 'utf8'));
		return packagesFromAssets(assets, project.targetFrameworks[0]);
	} catch { return []; }
}

/** What a designer of a form of this project needs: the toolbox groups and the assemblies to load. */
export interface ProjectLibraries {
	project: ProjectInfo;
	output?: string;
	groups: ToolboxLibrary[];
	/** Empty in a restricted (untrusted) window: the toolbox is read from metadata, no code is loaded. */
	load: string[];
	/** Said in the toolbox: why the project's controls are not there, or not usable. */
	notice?: string;
	/** Folders whose change means a new build: this project's bin/obj, the referenced projects' bin. */
	watch: string[];
}

export function resolveLibraries(designerFile: string): ProjectLibraries | undefined {
	const file = projectFileOf(designerFile);
	if (!file) return undefined;
	const text = fs.readFileSync(file, 'utf8');
	const project = projectInfo(file, text);
	const toolbox = projectToolbox(readConfig(workspaceFolderOf(file)), projectKey(workspaceFolderOf(file), file));
	const output = findOutput(project);
	const referenced: Record<string, string> = {};
	const watch = [path.join(project.dir, 'bin'), path.join(project.dir, 'obj')];
	for (const lib of toolbox.libraries) {
		if (lib.source !== 'projectReference' || !lib.path) continue;
		const other = path.resolve(project.dir, lib.path);
		if (!fs.existsSync(other)) continue;
		const info = projectInfo(other, fs.readFileSync(other, 'utf8'));
		watch.push(path.join(info.dir, 'bin'));
		const out = findOutput(info);
		if (out) referenced[lib.id] = out;
	}
	const groups = toolboxLibraries(project, text, toolbox, output, readAssets(project), referenced);
	const trusted = vscode.workspace.isTrusted;
	const notice = !output
		? vscode.l10n.t('Build the project to see its own controls here (Rescan Toolbox builds it).')
		: !trusted ? vscode.l10n.t('Restricted mode: the project\'s controls are listed, but their code is not loaded. Trust this folder to use them.') : undefined;
	return { project, output, groups, load: trusted ? assembliesToLoad(output, groups) : [], notice, watch };
}

// --- building --------------------------------------------------------------------------------------

/** `dotnet build` of the project, with a progress notification; the output goes to the NetForms channel. */
export async function buildProject(projectFile: string, log: vscode.OutputChannel): Promise<boolean> {
	const result = await vscode.window.withProgress(
		{ location: vscode.ProgressLocation.Notification, title: vscode.l10n.t('Building {0}…', path.basename(projectFile)) },
		() => dotnet(['build', projectFile, '-nologo', '-v', 'q'], path.dirname(projectFile)));
	log.appendLine(`dotnet build ${projectFile}\n${result.output}`);
	if (result.code === 0) return true;
	log.show(true);
	void vscode.window.showErrorMessage(vscode.l10n.t('The build of {0} failed; the designer keeps the last successful build (details in the NetForms output).', path.basename(projectFile)));
	return false;
}

async function run(args: string[], cwd: string, log: vscode.OutputChannel): Promise<boolean> {
	const result = await dotnet(args, cwd);
	log.appendLine(`dotnet ${args.join(' ')}\n${result.output}`);
	if (result.code === 0) return true;
	log.show(true);
	void vscode.window.showErrorMessage(vscode.l10n.t('dotnet {0} failed (details in the NetForms output).', args.slice(0, 2).join(' ')));
	return false;
}

// --- the commands ------------------------------------------------------------------------------------

type HostProvider = () => HostClient;

/** The project the command is for: the one of the file (or open designer), else one picked from the workspace. */
async function pickProject(file: string | undefined): Promise<string | undefined> {
	const own = file ? projectFileOf(file) : undefined;
	if (own) return own;
	const found = await vscode.workspace.findFiles('**/*.csproj', '**/{bin,obj,node_modules}/**', 50);
	if (found.length <= 1) return found[0]?.fsPath;
	return (await vscode.window.showQuickPick(found.map((f) => ({ label: path.basename(f.fsPath), description: vscode.workspace.asRelativePath(f), file: f.fsPath })),
		{ title: vscode.l10n.t('Project') }))?.file;
}

function requireTrust(): void {
	if (!vscode.workspace.isTrusted)
		throw new Error(vscode.l10n.t('Control libraries run their code in the designer: trust this folder first (Manage Workspace Trust).'));
}

/** NetForms: Rescan Toolbox - builds the project and reloads its libraries in every designer of it. */
export async function rescanToolbox(log: vscode.OutputChannel, file: string | undefined) {
	const project = await pickProject(file);
	if (!project) throw new Error(vscode.l10n.t('No .csproj in the workspace.'));
	await buildProject(project, log);
	librariesChanged.fire(project);
}

interface SourcePick extends vscode.QuickPickItem { source?: 'package' | 'dll' | 'feed' | 'folder' | 'projectReference' | 'projectPackage'; recent?: RecentLibrary }

/** NetForms: Add Control Library… */
export async function addControlLibrary(context: vscode.ExtensionContext, log: vscode.OutputChannel, host: HostProvider, file: string | undefined) {
	requireTrust();
	const projectFile = await pickProject(file);
	if (!projectFile) throw new Error(vscode.l10n.t('No .csproj in the workspace.'));
	const project = projectInfo(projectFile, fs.readFileSync(projectFile, 'utf8'));
	const recent = context.globalState.get<RecentLibrary[]>(recentKey, []);
	const items: SourcePick[] = [];
	if (recent.length) {
		items.push({ label: vscode.l10n.t('Recent'), kind: vscode.QuickPickItemKind.Separator });
		for (const r of recent) {
			if (r.source === 'dll' && (!r.file || !fs.existsSync(r.file))) continue;
			items.push({ label: `$(history) ${r.name}`, description: r.source === 'package' ? `NuGet ${r.version ?? ''}` : r.file, recent: r });
		}
		items.push({ label: vscode.l10n.t('Sources'), kind: vscode.QuickPickItemKind.Separator });
	}
	items.push(
		{ label: `$(package) ${vscode.l10n.t('NuGet package…')}`, detail: vscode.l10n.t('Search nuget.org, pick a version; the package is checked before it is added'), source: 'package' },
		{ label: `$(file-binary) ${vscode.l10n.t('.dll file…')}`, detail: vscode.l10n.t('A control library as a file; copied into libs/ so the project builds elsewhere too'), source: 'dll' },
		{ label: `$(archive) ${vscode.l10n.t('.nupkg or local feed folder…')}`, detail: vscode.l10n.t('Your own or your company\'s packages: the folder becomes a package source in nuget.config'), source: 'feed' },
		{ label: `$(folder) ${vscode.l10n.t('Folder of this project…')}`, detail: vscode.l10n.t('The project\'s own controls in that folder get a toolbox group of their own'), source: 'folder' },
		{ label: `$(project) ${vscode.l10n.t('Another project…')}`, detail: vscode.l10n.t('A project reference (dotnet add reference); its controls follow its builds'), source: 'projectReference' },
		{ label: `$(references) ${vscode.l10n.t('NuGet package already in the project…')}`, detail: vscode.l10n.t('Packages the project references: pick those to show in the toolbox'), source: 'projectPackage' },
	);
	const picked = await vscode.window.showQuickPick(items, { title: vscode.l10n.t('Add Control Library to {0}', project.name), matchOnDetail: true });
	if (!picked) return;
	const adder = new LibraryAdder(context, log, host, project);
	if (picked.recent) await adder.recent(picked.recent);
	else if (picked.source === 'package') await adder.package();
	else if (picked.source === 'dll') await adder.dll();
	else if (picked.source === 'feed') await adder.feed();
	else if (picked.source === 'folder') await adder.folder();
	else if (picked.source === 'projectReference') await adder.projectReference();
	else if (picked.source === 'projectPackage') await adder.projectPackage();
}

/** NetForms: Remove Control Library - the group leaves the toolbox; the reference leaves the csproj if asked. */
export async function removeControlLibrary(log: vscode.OutputChannel, file: string | undefined) {
	const projectFile = await pickProject(file);
	if (!projectFile) throw new Error(vscode.l10n.t('No .csproj in the workspace.'));
	const folder = workspaceFolderOf(projectFile);
	const key = projectKey(folder, projectFile);
	const config = readConfig(folder);
	const libraries = projectToolbox(config, key).libraries;
	if (!libraries.length) throw new Error(vscode.l10n.t('{0} has no control libraries in the toolbox.', path.basename(projectFile)));
	const lib = (await vscode.window.showQuickPick(libraries.map((l) => ({ label: l.name, description: describe(l), lib: l })),
		{ title: vscode.l10n.t('Remove Control Library') }))?.lib;
	if (!lib) return;
	let removeReference = false;
	if (lib.source !== 'folder') {
		const toolboxOnly = vscode.l10n.t('From the toolbox only');
		const both = vscode.l10n.t('From the toolbox and the project');
		const how = await vscode.window.showQuickPick([
			{ label: toolboxOnly, detail: vscode.l10n.t('The project keeps the reference and still builds with the library') },
			{ label: both, detail: lib.source === 'dll' ? vscode.l10n.t('The <Reference> goes from the csproj; the .dll file stays') : vscode.l10n.t('dotnet remove: the reference goes from the csproj') },
		], { title: vscode.l10n.t('Remove {0}', lib.name) });
		if (!how) return;
		removeReference = how.label === both;
	}
	if (removeReference) {
		const dir = path.dirname(projectFile);
		if ((lib.source === 'package' || lib.source === 'feed' || lib.source === 'projectPackage') && lib.package) {
			if (!await run(['remove', projectFile, 'package', lib.package], dir, log)) return;
		} else if (lib.source === 'projectReference' && lib.path) {
			if (!await run(['remove', projectFile, 'reference', lib.path], dir, log)) return;
		} else if (lib.source === 'dll' && lib.path) {
			const text = fs.readFileSync(projectFile, 'utf8');
			fs.writeFileSync(projectFile, removeDllReference(text, path.basename(lib.path).replace(/\.dll$/i, '')), 'utf8');
		}
	}
	writeConfig(folder, withoutLibrary(config, key, lib.id));
	librariesChanged.fire(projectFile);
	void vscode.window.showInformationMessage(vscode.l10n.t('{0} is no longer in the toolbox.', lib.name));
}

function describe(l: LibraryEntry): string {
	switch (l.source) {
		case 'package': case 'projectPackage': return `NuGet ${l.package} ${l.version ?? ''}`;
		case 'feed': return vscode.l10n.t('local package {0} {1}', l.package ?? '', l.version ?? '');
		case 'dll': return l.path ?? '';
		case 'folder': return l.namespace ?? '';
		case 'projectReference': return l.path ?? '';
	}
}

/** One "Add Control Library…" run: the source-specific part, then the common end (config, build, reload). */
class LibraryAdder {
	private readonly workspaceFolder: string;
	private readonly key: string;

	constructor(private readonly context: vscode.ExtensionContext, private readonly log: vscode.OutputChannel, private readonly host: HostProvider, private readonly project: ProjectInfo) {
		this.workspaceFolder = workspaceFolderOf(project.file);
		this.key = projectKey(this.workspaceFolder, project.file);
	}

	// --- 1. NuGet by name ---

	async package(fixed?: { id: string; version?: string; types?: string[] }) {
		const id = fixed?.id ?? await searchPackage();
		if (!id) return;
		const version = fixed?.version ?? await pickVersion(id);
		if (!version) return;
		const nupkg = await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: vscode.l10n.t('Downloading {0} {1}…', id, version) },
			() => download(nupkgUrl(id, version), path.join(os.tmpdir(), `netforms-${id}.${version}.nupkg`)));
		const report = await this.host().scanPackage(nupkg);
		await this.addPackage(report, id, version, 'package', fixed?.types);
	}

	private async addPackage(report: PackageReport, id: string, version: string, source: 'package' | 'feed', types?: string[]) {
		if (!await accept(`${id} ${version}`, report.verdict, report.findings.concat(report.assemblies.flatMap((a) => a.findings)))) return;
		const picked = types ? { all: false, types } : await pickTypes(`${id} ${version}`, report.assemblies);
		if (!picked) return;
		if (!await run(['add', this.project.file, 'package', id, '--version', version], this.project.dir, this.log)) return;
		await this.finish({
			id: `package:${id.toLowerCase()}`, name: id, source, package: id, version,
			assemblies: report.assemblies.filter((a) => a.verdict !== 'error').map((a) => path.posix.basename(a.path)),
			types: picked.all ? undefined : picked.types,
		}, { source: 'package', name: `${id} ${version}`, package: id, version, types: picked.all ? undefined : picked.types });
	}

	// --- 2. a .dll ---

	async dll(fixedFile?: string, types?: string[]) {
		const file = fixedFile ?? (await vscode.window.showOpenDialog({ canSelectMany: false, filters: { [vscode.l10n.t('Control library')]: ['dll'] }, title: vscode.l10n.t('Control library (.dll)') }))?.[0].fsPath;
		if (!file) return;
		const [report] = await this.host().scan([file]);
		if (!await accept(path.basename(file), report.verdict, report.findings)) return;
		const picked = types ? { all: false, types } : await pickTypes(path.basename(file), [report]);
		if (!picked) return;
		let target = file;
		const inside = !path.relative(this.project.dir, file).startsWith('..') && !path.isAbsolute(path.relative(this.project.dir, file));
		if (!inside) {
			const copy = vscode.l10n.t('Copy into libs/');
			const keep = vscode.l10n.t('Reference it where it is');
			const answer = fixedFile ? copy : await vscode.window.showInformationMessage(
				vscode.l10n.t('{0} is outside the project. Copy it into libs/ of the project? Otherwise the project will not build on another machine.', path.basename(file)),
				{ modal: true }, copy, keep);
			if (!answer) return;
			if (answer === copy) {
				const libs = path.join(this.project.dir, 'libs');
				fs.mkdirSync(libs, { recursive: true });
				for (const ext of ['.dll', '.pdb', '.xml']) {
					const from = file.replace(/\.dll$/i, ext);
					if (fs.existsSync(from)) fs.copyFileSync(from, path.join(libs, path.basename(from)));
				}
				target = path.join(libs, path.basename(file));
			}
		}
		const relative = path.relative(this.project.dir, target).split(path.sep).join('/');
		const text = fs.readFileSync(this.project.file, 'utf8');
		const edited = addDllReference(text, relative);
		if (edited !== text) fs.writeFileSync(this.project.file, edited, 'utf8');
		await this.finish({
			id: `dll:${relative.toLowerCase()}`, name: report.name, source: 'dll', path: relative,
			assemblies: [path.basename(target)], types: picked.all ? undefined : picked.types,
		}, { source: 'dll', name: path.basename(file), file, types: picked.all ? undefined : picked.types });
	}

	// --- 3. a .nupkg or a local feed ---

	async feed() {
		const chosen = (await vscode.window.showOpenDialog({ canSelectMany: false, canSelectFiles: true, canSelectFolders: true, filters: { [vscode.l10n.t('NuGet package')]: ['nupkg'] }, title: vscode.l10n.t('A .nupkg file or a folder of them') }))?.[0].fsPath;
		if (!chosen) return;
		let nupkg = chosen;
		if (fs.statSync(chosen).isDirectory()) {
			const packages = fs.readdirSync(chosen).filter((f) => f.toLowerCase().endsWith('.nupkg') && !f.toLowerCase().endsWith('.symbols.nupkg'));
			if (!packages.length) throw new Error(vscode.l10n.t('{0} has no .nupkg files.', chosen));
			const pick = await vscode.window.showQuickPick(packages, { title: vscode.l10n.t('Package') });
			if (!pick) return;
			nupkg = path.join(chosen, pick);
		}
		const feed = path.dirname(nupkg);
		const report = await this.host().scanPackage(nupkg);
		const name = { id: report.id, version: report.version, ...(report.id ? {} : parseNupkgName(nupkg)) };
		if (!name.id || !name.version) throw new Error(vscode.l10n.t('{0} is not a NuGet package (no id and version).', path.basename(nupkg)));
		// Not from nuget.org: its code will run in the designer - say so once more.
		const add = vscode.l10n.t('Add');
		if (await vscode.window.showWarningMessage(vscode.l10n.t('{0} {1} is not from nuget.org. Its controls run in the designer as your own code does. Add it?', name.id, name.version), { modal: true }, add) !== add) return;
		if (!await this.ensureSource(feed)) return;
		await this.addPackage(report, name.id, name.version, 'feed');
	}

	/** The folder as a package source in the project's nuget.config (created, with nuget.org, when there is none). */
	private async ensureSource(feed: string): Promise<boolean> {
		const config = path.join(this.project.dir, 'nuget.config');
		if (!fs.existsSync(config) && !fs.existsSync(path.join(this.project.dir, 'NuGet.Config'))) {
			if (!await run(['new', 'nugetconfig', '-o', this.project.dir], this.project.dir, this.log)) return false;
		}
		const file = fs.existsSync(config) ? config : path.join(this.project.dir, 'NuGet.Config');
		if (fs.readFileSync(file, 'utf8').includes(feed)) return true;
		const name = `local-${path.basename(feed).replace(/[^A-Za-z0-9_.-]/g, '-')}`;
		return run(['nuget', 'add', 'source', feed, '--name', name, '--configfile', file], this.project.dir, this.log);
	}

	// --- 4. a folder of this project ---

	async folder() {
		const chosen = (await vscode.window.showOpenDialog({ canSelectMany: false, canSelectFiles: false, canSelectFolders: true, defaultUri: vscode.Uri.file(this.project.dir), title: vscode.l10n.t('Folder with controls') }))?.[0].fsPath;
		if (!chosen) return;
		const relative = path.relative(this.project.dir, chosen);
		if (relative.startsWith('..') || path.isAbsolute(relative)) throw new Error(vscode.l10n.t('{0} is not a folder of {1}.', chosen, path.basename(this.project.file)));
		const ns = folderNamespace(this.project, chosen);
		const name = await vscode.window.showInputBox({ title: vscode.l10n.t('Toolbox group'), value: path.basename(chosen) || this.project.name, prompt: vscode.l10n.t('The controls of namespace {0} go into this group.', ns) });
		if (!name) return;
		await this.finish({ id: `folder:${relative.split(path.sep).join('/').toLowerCase()}`, name, source: 'folder', path: relative.split(path.sep).join('/'), namespace: ns, assemblies: [] }, undefined, false);
	}

	// --- 5. another project ---

	async projectReference() {
		const found = (await vscode.workspace.findFiles('**/*.csproj', '**/{bin,obj,node_modules}/**', 100)).map((f) => f.fsPath).filter((f) => path.resolve(f) !== path.resolve(this.project.file));
		const browse = vscode.l10n.t('Browse…');
		const pick = await vscode.window.showQuickPick([...found.map((f) => ({ label: path.basename(f), description: vscode.workspace.asRelativePath(f), file: f })), { label: `$(folder-opened) ${browse}`, file: '' }],
			{ title: vscode.l10n.t('Project with controls') });
		if (!pick) return;
		const other = pick.file || (await vscode.window.showOpenDialog({ canSelectMany: false, filters: { 'C# project': ['csproj'] } }))?.[0].fsPath;
		if (!other) return;
		const relative = path.relative(this.project.dir, other);
		if (!await run(['add', this.project.file, 'reference', other], this.project.dir, this.log)) return;
		if (!await buildProject(this.project.file, this.log)) return;
		const info = projectInfo(other, fs.readFileSync(other, 'utf8'));
		const built = findOutput(info) ?? (findOutput(this.project) ? path.join(path.dirname(findOutput(this.project)!), info.assemblyName + '.dll') : undefined);
		if (!built || !fs.existsSync(built)) throw new Error(vscode.l10n.t('{0} has no build output.', path.basename(other)));
		const [report] = await this.host().scan([built]);
		if (!await accept(info.name, report.verdict, report.findings)) return;
		const picked = await pickTypes(info.name, [report]);
		if (!picked) return;
		await this.finish({
			id: `project:${relative.split(path.sep).join('/').toLowerCase()}`, name: info.name, source: 'projectReference', path: relative.split(path.sep).join('/'),
			assemblies: [info.assemblyName + '.dll'], types: picked.all ? undefined : picked.types,
		}, undefined, false);
	}

	// --- 6. a package the project already references ---

	async projectPackage() {
		let packages = readAssets(this.project);
		if (!packages.length) {
			// Never restored: the assets file is written by the build.
			if (!await buildProject(this.project.file, this.log)) return;
			packages = readAssets(this.project);
		}
		const candidates = packages.filter((p) => p.assemblies.length && !/^(NetForms|Avalonia|SkiaSharp|HarfBuzzSharp|Microsoft\.|System\.|runtime\.)/i.test(p.id));
		if (!candidates.length) throw new Error(vscode.l10n.t('{0} references no packages with assemblies (besides NetForms and .NET).', path.basename(this.project.file)));
		const picks = await vscode.window.showQuickPick(candidates.map((p) => ({ label: p.id, description: p.version, detail: p.direct ? undefined : vscode.l10n.t('brought in by another package'), pkg: p })),
			{ title: vscode.l10n.t('Packages to show in the toolbox'), canPickMany: true });
		if (!picks?.length) return;
		const output = findOutput(this.project);
		let config = readConfig(this.workspaceFolder);
		let added = 0;
		for (const { pkg } of picks) {
			const files = pkg.assemblies.map((a) => (output && fs.existsSync(path.join(path.dirname(output), a)) ? path.join(path.dirname(output), a) : undefined)
				?? (pkg.folder && pkg.runtime ? path.join(pkg.folder, ...(pkg.runtime.find((r) => path.posix.basename(r) === a) ?? a).split('/')) : a))
				.filter((f) => fs.existsSync(f));
			if (!files.length) { void vscode.window.showWarningMessage(vscode.l10n.t('The assemblies of {0} were not found; build the project first.', pkg.id)); continue; }
			const reports = await this.host().scan(files);
			if (!await accept(`${pkg.id} ${pkg.version}`, worst(reports), reports.flatMap((r) => r.findings))) continue;
			const picked = picks.length === 1 ? await pickTypes(pkg.id, reports) : { all: true, types: [] };
			if (!picked) continue;
			if (!picked.all && !picked.types.length) continue;
			if (!reports.some((r) => r.items.length)) { void vscode.window.showWarningMessage(vscode.l10n.t('{0} has no controls or components for the toolbox.', pkg.id)); continue; }
			config = withLibrary(config, this.key, {
				id: `package:${pkg.id.toLowerCase()}`, name: pkg.id, source: 'projectPackage', package: pkg.id, version: pkg.version,
				assemblies: reports.filter((r) => r.verdict !== 'error').map((r) => path.basename(r.path)), types: picked.all ? undefined : picked.types,
			});
			added++;
		}
		if (!added) return;
		writeConfig(this.workspaceFolder, config);
		librariesChanged.fire(this.project.file);
		void vscode.window.showInformationMessage(vscode.l10n.t('Added to the toolbox: {0}.', picks.map((p) => p.label).join(', ')));
	}

	// --- recent ---

	async recent(r: RecentLibrary) {
		if (r.source === 'package' && r.package) await this.package({ id: r.package, version: r.version, types: r.types });
		else if (r.source === 'dll' && r.file) await this.dll(r.file, r.types);
	}

	// --- the common end ---

	private async finish(entry: LibraryEntry, recent: RecentLibrary | undefined, build = true) {
		writeConfig(this.workspaceFolder, withLibrary(readConfig(this.workspaceFolder), this.key, entry));
		if (recent) await this.context.globalState.update(recentKey, withRecent(this.context.globalState.get<RecentLibrary[]>(recentKey, []), recent));
		if (build) await buildProject(this.project.file, this.log);
		librariesChanged.fire(this.project.file);
		void vscode.window.showInformationMessage(vscode.l10n.t('{0} is in the toolbox of {1}.', entry.name, this.project.name));
	}
}

function worst(reports: LibraryReport[]): 'ok' | 'warning' | 'error' {
	const all = reports.map((r) => r.verdict);
	return all.length && all.every((v) => v === 'error') ? 'error' : all.includes('warning') || all.includes('error') ? 'warning' : 'ok';
}

/** The check before adding: an error refuses with the reasons; a warning asks. */
async function accept(what: string, verdict: string, findings: { severity: string; message: string }[]): Promise<boolean> {
	const text = [...new Set(findings.map((f) => f.message))].join('\n\n');
	if (verdict === 'error') {
		void vscode.window.showErrorMessage(vscode.l10n.t('{0} cannot be used by the designer.', what), { modal: true, detail: text });
		return false;
	}
	if (verdict === 'warning') {
		const add = vscode.l10n.t('Add anyway');
		return await vscode.window.showWarningMessage(vscode.l10n.t('{0}: read this before adding it.', what), { modal: true, detail: text }, add) === add;
	}
	return true;
}

/** "Found N controls": the user unticks what the toolbox should not show. Undefined when cancelled. */
async function pickTypes(what: string, reports: LibraryReport[]): Promise<{ all: boolean; types: string[] } | undefined> {
	const items: LibraryItem[] = reports.flatMap((r) => r.items);
	if (!items.length) {
		void vscode.window.showWarningMessage(vscode.l10n.t('{0} has no controls or components for the toolbox.', what));
		return undefined;
	}
	const picks = await vscode.window.showQuickPick(
		items.map((i) => ({ label: i.name, description: i.namespace, detail: i.tray ? vscode.l10n.t('component (tray)') : undefined, picked: true, type: i.type })),
		{ title: vscode.l10n.t('{0}: found {1} controls and components', what, items.length), canPickMany: true });
	if (!picks?.length) return undefined;
	return { all: picks.length === items.length, types: picks.map((p) => p.type) };
}

// --- nuget.org ----------------------------------------------------------------------------------

function getJson(url: string): Promise<any> {
	return new Promise((resolve, reject) => {
		https.get(url, { headers: { 'User-Agent': 'NetForms-Designer' } }, (res) => {
			if (res.statusCode && res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) { res.resume(); getJson(res.headers.location).then(resolve, reject); return; }
			if (res.statusCode !== 200) { res.resume(); reject(new Error(`${url}: HTTP ${res.statusCode}`)); return; }
			let body = '';
			res.setEncoding('utf8');
			res.on('data', (d) => (body += d));
			res.on('end', () => { try { resolve(JSON.parse(body)); } catch (e) { reject(e); } });
		}).on('error', reject);
	});
}

function download(url: string, target: string): Promise<string> {
	return new Promise((resolve, reject) => {
		https.get(url, { headers: { 'User-Agent': 'NetForms-Designer' } }, (res) => {
			if (res.statusCode && res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) { res.resume(); download(res.headers.location, target).then(resolve, reject); return; }
			if (res.statusCode !== 200) { res.resume(); reject(new Error(vscode.l10n.t('Downloading {0} failed: HTTP {1}.', url, String(res.statusCode)))); return; }
			const out = fs.createWriteStream(target);
			res.pipe(out);
			out.on('finish', () => out.close(() => resolve(target)));
			out.on('error', reject);
		}).on('error', reject);
	});
}

/** A quick pick that searches nuget.org as the user types; Enter on typed text takes it as the id. */
function searchPackage(): Promise<string | undefined> {
	return new Promise((resolve) => {
		const qp = vscode.window.createQuickPick<vscode.QuickPickItem & { id?: string }>();
		qp.title = vscode.l10n.t('NuGet package');
		qp.placeholder = vscode.l10n.t('Search nuget.org (e.g. ScottPlot.WinForms)');
		let timer: NodeJS.Timeout | undefined;
		let done = false;
		const finish = (v: string | undefined) => { if (done) return; done = true; qp.hide(); resolve(v); };
		qp.onDidChangeValue((value) => {
			if (timer) clearTimeout(timer);
			if (!value.trim()) { qp.items = []; return; }
			timer = setTimeout(async () => {
				qp.busy = true;
				try {
					const result = await getJson(searchUrl(value.trim()));
					const found = (result.data ?? []).map((p: any) => ({ label: p.id, description: p.version, detail: `${(p.description ?? '').slice(0, 120)}`, id: p.id, alwaysShow: true }));
					qp.items = found.length ? found : [{ label: value.trim(), description: vscode.l10n.t('use this id'), id: value.trim(), alwaysShow: true }];
				} catch {
					qp.items = [{ label: value.trim(), description: vscode.l10n.t('nuget.org search is not reachable: use this id'), id: value.trim(), alwaysShow: true }];
				} finally { qp.busy = false; }
			}, 300);
		});
		qp.onDidAccept(() => finish(qp.selectedItems[0]?.id ?? (qp.value.trim() || undefined)));
		qp.onDidHide(() => finish(undefined));
		qp.show();
	});
}

async function pickVersion(id: string): Promise<string | undefined> {
	let versions: string[];
	try { versions = ((await getJson(versionsUrl(id))).versions ?? []).slice().reverse(); }
	catch { return vscode.window.showInputBox({ title: vscode.l10n.t('Version of {0}', id), prompt: vscode.l10n.t('nuget.org is not reachable; type the version.') }); }
	if (!versions.length) throw new Error(vscode.l10n.t('{0} is not on nuget.org.', id));
	const stable = versions.filter((v) => !v.includes('-'));
	const ordered = [...stable, ...versions.filter((v) => v.includes('-'))];
	return (await vscode.window.showQuickPick(ordered.map((v, i) => ({ label: v, description: i === 0 ? vscode.l10n.t('latest') : undefined })), { title: vscode.l10n.t('Version of {0}', id) }))?.label;
}
