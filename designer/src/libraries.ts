// Control libraries of a project (decision 157, docs/designer-control-libraries.md) - the part without
// vscode in it (test/libraries.test.js): where the build output is, what the csproj references, which
// packages obj/project.assets.json resolved, what .vscode/netforms.json keeps, and the toolbox groups the
// host is asked for. Every source ends up the same way: a reference in the csproj, `dotnet build`, the
// build output loaded by the host, a toolbox group.
import * as fs from 'fs';
import * as path from 'path';

/** Where a library came from (the six sources of "Add Control Library…"). */
export type LibrarySource = 'package' | 'dll' | 'feed' | 'folder' | 'projectReference' | 'projectPackage';

/** One library of a project, as .vscode/netforms.json keeps it (committed: colleagues get the same toolbox). */
export interface LibraryEntry {
	/** Unique within the project: `package:ZedGraph`, `dll:libs/Gauges.dll`, `folder:Controls`. */
	id: string;
	/** The toolbox group. */
	name: string;
	source: LibrarySource;
	/** NuGet id and version (package, feed, projectPackage). */
	package?: string;
	version?: string;
	/** The referenced file, relative to the project folder (dll: the .dll; projectReference: the .csproj; folder: the folder). */
	path?: string;
	/** A folder of the project: the namespace its types are in. */
	namespace?: string;
	/** The assembly files (names) of the library in the build output; empty for a folder. */
	assemblies: string[];
	/** The types ticked when it was added; absent for all. */
	types?: string[];
}

/** One project's part of .vscode/netforms.json. */
export interface ProjectToolbox {
	libraries: LibraryEntry[];
	/** False hides the automatic "<Project> Components" group. */
	projectComponents?: boolean;
}

/** .vscode/netforms.json: the toolbox of each project, keyed by the project file relative to the workspace folder (forward slashes). */
export interface NetFormsConfig {
	projects?: Record<string, ProjectToolbox>;
	[other: string]: unknown;
}

/** What the host's toolbox request takes per group (DesignerToolboxLibrary). */
export interface ToolboxLibrary {
	id: string;
	name: string;
	assemblies: string[];
	namespaces?: string[];
	excludeNamespaces?: string[];
	types?: string[];
}

// --- the project file ---------------------------------------------------------------------------

export interface ProjectInfo {
	/** The project file. */
	file: string;
	dir: string;
	name: string;
	assemblyName: string;
	rootNamespace: string;
	targetFrameworks: string[];
}

function property(text: string, name: string): string | undefined {
	const m = new RegExp(`<${name}>\\s*([^<]*?)\\s*</${name}>`).exec(text);
	return m && m[1] && !m[1].includes('$(') ? m[1] : undefined;
}

/** The names a project's build uses: AssemblyName and RootNamespace default to the file name, as in MSBuild. */
export function projectInfo(file: string, text: string): ProjectInfo {
	const name = path.basename(file).replace(/\.csproj$/i, '');
	const frameworks = (property(text, 'TargetFrameworks') ?? property(text, 'TargetFramework') ?? 'net10.0').split(';').map((f) => f.trim()).filter(Boolean);
	return {
		file, dir: path.dirname(file), name,
		assemblyName: property(text, 'AssemblyName') ?? name,
		rootNamespace: property(text, 'RootNamespace') ?? name.replace(/[^A-Za-z0-9_.]/g, '_'),
		targetFrameworks: frameworks,
	};
}

/**
 * The project's own assembly from its last build: bin/<Configuration>/<tfm>[/<rid>]/<AssemblyName>.dll with its
 * .deps.json, the newest of them (the designer uses the last successful build). Undefined when never built.
 */
export function findOutput(project: ProjectInfo): string | undefined {
	const bin = path.join(project.dir, 'bin');
	const found: { file: string; time: number }[] = [];
	const visit = (dir: string, depth: number) => {
		let entries: fs.Dirent[];
		try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
		for (const e of entries) {
			const full = path.join(dir, e.name);
			if (e.isDirectory()) {
				if (depth < 3 && !/^(publish|runtimes|ref|refint)$/i.test(e.name)) visit(full, depth + 1);
			} else if (e.name.toLowerCase() === project.assemblyName.toLowerCase() + '.dll' && fs.existsSync(full.replace(/\.dll$/i, '.deps.json'))) {
				found.push({ file: full, time: fs.statSync(full).mtimeMs });
			}
		}
	};
	visit(bin, 0);
	found.sort((a, b) => b.time - a.time);
	return found[0]?.file;
}

/** `<PackageReference Include="X" Version="1.0"/>` of the project file (Version may be in Directory.Packages.props). */
export function packageReferences(text: string): { id: string; version?: string }[] {
	const out: { id: string; version?: string }[] = [];
	for (const m of text.matchAll(/<PackageReference\b([^>]*?)(\/>|>([\s\S]*?)<\/PackageReference>)/g)) {
		const id = /\bInclude\s*=\s*"([^"]+)"/.exec(m[1])?.[1];
		if (!id) continue;
		const version = /\bVersion\s*=\s*"([^"]+)"/.exec(m[1])?.[1] ?? (m[3] ? /<Version>\s*([^<]+?)\s*<\/Version>/.exec(m[3])?.[1] : undefined);
		out.push({ id, version });
	}
	return out;
}

/** `<ProjectReference Include="..\Lib\Lib.csproj"/>`: the paths as written. */
export function projectReferences(text: string): string[] {
	return [...text.matchAll(/<ProjectReference\b[^>]*\bInclude\s*=\s*"([^"]+)"/g)].map((m) => m[1]);
}

/** `<Reference Include="Gauges"><HintPath>libs\Gauges.dll</HintPath></Reference>`: include and hint path. */
export function dllReferences(text: string): { include: string; hintPath?: string }[] {
	const out: { include: string; hintPath?: string }[] = [];
	for (const m of text.matchAll(/<Reference\b([^>]*?)(\/>|>([\s\S]*?)<\/Reference>)/g)) {
		const include = /\bInclude\s*=\s*"([^"]+)"/.exec(m[1])?.[1];
		if (!include) continue;
		const hintPath = (/\bHintPath\s*=\s*"([^"]+)"/.exec(m[1]) ?? (m[3] ? /<HintPath>\s*([^<]+?)\s*<\/HintPath>/.exec(m[3]) : null))?.[1];
		out.push({ include, hintPath });
	}
	return out;
}

/** The project file's line break and one level of indent. */
function style(text: string): { nl: string; indent: string } {
	const nl = text.includes('\r\n') ? '\r\n' : '\n';
	const indent = /\n([ \t]+)</.exec(text)?.[1] ?? '  ';
	return { nl, indent };
}

/** The project file with a reference to a .dll (the path relative to the project, as MSBuild writes it: backslashes). */
export function addDllReference(text: string, dllPath: string): string {
	const include = path.basename(dllPath).replace(/\.dll$/i, '');
	const hint = dllPath.replace(/\//g, '\\');
	if (dllReferences(text).some((r) => r.include.split(',')[0].trim() === include)) return text;
	const { nl, indent } = style(text);
	const block = `${indent}<ItemGroup>${nl}${indent}${indent}<Reference Include="${include}">${nl}${indent}${indent}${indent}<HintPath>${hint}</HintPath>${nl}${indent}${indent}</Reference>${nl}${indent}</ItemGroup>${nl}`;
	const end = text.lastIndexOf('</Project>');
	if (end < 0) throw new Error('The project file has no </Project>.');
	return text.slice(0, end) + block + text.slice(end);
}

/** The project file without the reference to <paramref name="include"/> (and without its ItemGroup if it was alone there). */
export function removeDllReference(text: string, include: string): string {
	const re = new RegExp(`[ \\t]*<Reference\\b[^>]*\\bInclude\\s*=\\s*"${escape(include)}(,[^"]*)?"[^>]*?(\\/>|>[\\s\\S]*?<\\/Reference>)[ \\t]*\\r?\\n?`);
	const without = text.replace(re, '');
	return without.replace(/[ \t]*<ItemGroup>\s*<\/ItemGroup>[ \t]*\r?\n?/g, '');
}

function escape(s: string): string {
	return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

// --- obj/project.assets.json ----------------------------------------------------------------------

export interface ResolvedPackage {
	id: string;
	version: string;
	/** Referenced by the project itself (not only through another package). */
	direct: boolean;
	/** The runtime assemblies' file names (Gauges.dll). */
	assemblies: string[];
	/** Where the package is on disk (the NuGet cache), when the assets file says. */
	folder?: string;
	/** The runtime assets as the assets file lists them (lib/net8.0/Gauges.dll), relative to the folder. */
	runtime?: string[];
}

/**
 * The packages NuGet resolved for the project (what `dotnet restore` wrote to obj/project.assets.json) for one
 * target framework, with their runtime assemblies: the sixth source - a package already in the csproj.
 */
export function packagesFromAssets(assets: any, targetFramework?: string): ResolvedPackage[] {
	const targets: Record<string, Record<string, any>> = assets?.targets ?? {};
	const keys = Object.keys(targets);
	const key = keys.find((k) => !k.includes('/') && (!targetFramework || frameworkMatches(k, targetFramework))) ?? keys.find((k) => !k.includes('/')) ?? keys[0];
	if (!key) return [];
	const frameworks: Record<string, any> = assets?.project?.frameworks ?? {};
	const fwKey = Object.keys(frameworks).find((k) => frameworkMatches(key, k)) ?? Object.keys(frameworks)[0];
	const direct = new Set(Object.keys(frameworks[fwKey]?.dependencies ?? {}).map((d) => d.toLowerCase()));
	const packageFolder = Object.keys(assets?.packageFolders ?? {})[0];
	const libraries: Record<string, any> = assets?.libraries ?? {};
	const out: ResolvedPackage[] = [];
	for (const [name, entry] of Object.entries(targets[key])) {
		if (entry?.type !== 'package') continue;
		const slash = name.indexOf('/');
		const id = name.slice(0, slash), version = name.slice(slash + 1);
		const runtime = Object.keys(entry.runtime ?? {}).filter((f) => f.toLowerCase().endsWith('.dll'));
		const assemblies = runtime.map((f) => path.posix.basename(f));
		const libPath: string | undefined = libraries[name]?.path;
		out.push({
			id, version, direct: direct.has(id.toLowerCase()), assemblies, runtime,
			folder: packageFolder && libPath ? path.join(packageFolder, libPath) : undefined,
		});
	}
	return out.sort((a, b) => Number(b.direct) - Number(a.direct) || a.id.localeCompare(b.id));
}

/** net10.0 matches net10.0 and net10.0-windows7.0 (the target key has the platform version the tfm alias may not). */
function frameworkMatches(targetKey: string, tfm: string): boolean {
	const norm = (s: string) => s.toLowerCase().replace(/^\.netcoreapp,version=v/, 'net');
	const a = norm(targetKey), b = norm(tfm);
	return a === b || a.startsWith(b + '-') || b.startsWith(a + '-') || a.split('-')[0] === b.split('-')[0];
}

// --- .vscode/netforms.json --------------------------------------------------------------------------

export const configFile = path.join('.vscode', 'netforms.json');

/** The key of a project in the configuration: its path relative to the workspace folder, with forward slashes. */
export function projectKey(workspaceFolder: string, projectFile: string): string {
	return path.relative(workspaceFolder, projectFile).split(path.sep).join('/');
}

export function readConfig(workspaceFolder: string): NetFormsConfig {
	try { return JSON.parse(fs.readFileSync(path.join(workspaceFolder, configFile), 'utf8')); } catch { return {}; }
}

export function writeConfig(workspaceFolder: string, config: NetFormsConfig): void {
	const file = path.join(workspaceFolder, configFile);
	fs.mkdirSync(path.dirname(file), { recursive: true });
	fs.writeFileSync(file, JSON.stringify(config, null, '\t') + '\n', 'utf8');
}

export function projectToolbox(config: NetFormsConfig, key: string): ProjectToolbox {
	return config.projects?.[key] ?? { libraries: [] };
}

/** The configuration with <paramref name="entry"/> added to the project (replacing one with the same id). */
export function withLibrary(config: NetFormsConfig, key: string, entry: LibraryEntry): NetFormsConfig {
	const projects = { ...(config.projects ?? {}) };
	const current = projects[key] ?? { libraries: [] };
	projects[key] = { ...current, libraries: [...current.libraries.filter((l) => l.id !== entry.id), entry] };
	return { ...config, projects };
}

export function withoutLibrary(config: NetFormsConfig, key: string, id: string): NetFormsConfig {
	const projects = { ...(config.projects ?? {}) };
	const current = projects[key];
	if (!current) return config;
	projects[key] = { ...current, libraries: current.libraries.filter((l) => l.id !== id) };
	return { ...config, projects };
}

/** The namespace of a folder of the project: RootNamespace plus the folder path, as Visual Studio names new classes there. */
export function folderNamespace(project: ProjectInfo, folder: string): string {
	const relative = path.relative(project.dir, folder).split(path.sep).filter(Boolean);
	return [project.rootNamespace, ...relative.map((p) => p.replace(/[^A-Za-z0-9_]/g, '_').replace(/^(\d)/, '_$1'))].join('.');
}

// --- the toolbox groups -----------------------------------------------------------------------------

/**
 * The groups the host's toolbox request takes for a project: its own controls ("<Project> Components", as in
 * Visual Studio, without the namespaces of the folders that have a group of their own), then each library.
 * A library whose reference has left the csproj, or whose assemblies are not in the build output, is left
 * out - the toolbox hides it rather than break (Rescan removes it for good).
 */
export function toolboxLibraries(project: ProjectInfo, projectText: string, toolbox: ProjectToolbox, output: string | undefined,
	packages: ResolvedPackage[] = [], referencedOutputs: Record<string, string> = {}): ToolboxLibrary[] {
	if (!output) return [];
	const outDir = path.dirname(output);
	const groups: ToolboxLibrary[] = [];
	const folders = toolbox.libraries.filter((l) => l.source === 'folder' && l.namespace);
	if (toolbox.projectComponents !== false) {
		groups.push({ id: 'project', name: `${project.name} Components`, assemblies: [output], excludeNamespaces: folders.map((f) => f.namespace!) });
	}
	for (const lib of toolbox.libraries) {
		if (lib.source === 'folder') {
			if (lib.namespace) groups.push({ id: lib.id, name: lib.name, assemblies: [output], namespaces: [lib.namespace] });
			continue;
		}
		if (!stillReferenced(lib, projectText)) continue;
		const assemblies = lib.assemblies.map((a) => newer(referencedOutputs[lib.id], libraryAssembly(lib, a, outDir, packages))).filter((a): a is string => !!a);
		if (!assemblies.length) continue;
		groups.push({ id: lib.id, name: lib.name, assemblies, types: lib.types });
	}
	return groups;
}

/**
 * Of a referenced project's own output and its copy in this project's output, the one built last: the other
 * project may have been rebuilt on its own, and its controls should not wait for this project's next build.
 */
function newer(a: string | undefined, b: string | undefined): string | undefined {
	if (!a || !fs.existsSync(a)) return b;
	if (!b) return a;
	return fs.statSync(a).mtimeMs > fs.statSync(b).mtimeMs ? a : b;
}

/**
 * Where an assembly of a library is: the build output (an application copies its packages there), else - for a
 * package of a class library, which does not - the package's folder in the NuGet cache.
 */
export function libraryAssembly(lib: LibraryEntry, assembly: string, outDir: string, packages: ResolvedPackage[]): string | undefined {
	const local = path.join(outDir, assembly);
	if (fs.existsSync(local)) return local;
	if (!lib.package) return undefined;
	const pkg = packages.find((p) => p.id.toLowerCase() === lib.package!.toLowerCase());
	return pkg ? packageAssemblyFiles(pkg).find((f) => path.basename(f).toLowerCase() === assembly.toLowerCase()) : undefined;
}

/** The runtime assemblies of a resolved package in the NuGet cache (what the assets file says the project takes). */
export function packageAssemblyFiles(pkg: ResolvedPackage & { runtime?: string[] }): string[] {
	if (!pkg.folder) return [];
	return (pkg.runtime ?? []).map((r) => path.join(pkg.folder!, ...r.split('/'))).filter((f) => fs.existsSync(f));
}

/**
 * What the host loads for a project: its own assembly (what is beside it comes along), and the assemblies of
 * its libraries that are elsewhere (a class library's packages stay in the NuGet cache).
 */
export function assembliesToLoad(output: string | undefined, groups: ToolboxLibrary[]): string[] {
	if (!output) return [];
	const outDir = path.dirname(output).toLowerCase();
	const extra = groups.flatMap((g) => g.assemblies).filter((a) => path.dirname(a).toLowerCase() !== outDir);
	// Elsewhere first: an assembly is loaded once, from the first folder that has it - a referenced project's
	// fresh build rather than the copy in this project's output.
	return [...new Set(extra), output];
}

/** Whether the csproj still references the library (a package, project or .dll the user may have removed by hand). */
export function stillReferenced(lib: LibraryEntry, projectText: string): boolean {
	switch (lib.source) {
		case 'package': case 'feed': case 'projectPackage':
			// A package brought in by another one is not in the csproj; the assets file decides then (the build output has it).
			return !lib.package || lib.source === 'projectPackage' || packageReferences(projectText).some((p) => p.id.toLowerCase() === lib.package!.toLowerCase());
		case 'projectReference':
			return !lib.path || projectReferences(projectText).some((r) => samePath(r, lib.path!));
		case 'dll':
			return !lib.path || dllReferences(projectText).some((r) => (r.hintPath && samePath(r.hintPath, lib.path!)) || r.include.split(',')[0].trim() === path.basename(lib.path!).replace(/\.dll$/i, ''));
		default:
			return true;
	}
}

function samePath(a: string, b: string): boolean {
	const n = (p: string) => path.normalize(p.replace(/\\/g, '/')).toLowerCase();
	return n(a) === n(b);
}

// --- NuGet ---------------------------------------------------------------------------------------------

/** nuget.org's search (the same the NuGet gallery uses), for the package picker. */
export function searchUrl(query: string, prerelease = false): string {
	return `https://azuresearch-usnc.nuget.org/query?q=${encodeURIComponent(query)}&take=20&prerelease=${prerelease}&semVerLevel=2.0.0`;
}

/** The versions of a package on nuget.org (flat container: lowercase id). */
export function versionsUrl(id: string): string {
	return `https://api.nuget.org/v3-flatcontainer/${id.toLowerCase()}/index.json`;
}

export function nupkgUrl(id: string, version: string): string {
	const lower = id.toLowerCase(), v = version.toLowerCase();
	return `https://api.nuget.org/v3-flatcontainer/${lower}/${v}/${lower}.${v}.nupkg`;
}

/** `Gauges.1.2.3-beta.nupkg` → id and version (the version starts at the first dot followed by a digit that ends in a valid version). */
export function parseNupkgName(file: string): { id: string; version: string } | undefined {
	const base = path.basename(file).replace(/\.nupkg$/i, '');
	const m = /^(.+?)\.(\d+(?:\.\d+){1,3}(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?)$/.exec(base);
	return m ? { id: m[1], version: m[2] } : undefined;
}

// --- recent libraries ------------------------------------------------------------------------------

/** A library added before, in any project: offered first by "Add Control Library…" (globalStorage). */
export interface RecentLibrary {
	source: 'package' | 'dll';
	name: string;
	package?: string;
	version?: string;
	/** A .dll: its absolute path when it was added. */
	file?: string;
	types?: string[];
}

export function withRecent(recent: RecentLibrary[], added: RecentLibrary, max = 10): RecentLibrary[] {
	const key = (r: RecentLibrary) => r.source === 'package' ? `p:${r.package?.toLowerCase()}` : `d:${r.file}`;
	return [added, ...recent.filter((r) => key(r) !== key(added))].slice(0, max);
}
