// Project-level commands without vscode in them (test/project.test.js): the startup form Program.cs runs, dotnet publish.

export interface FormClass { ns?: string; name: string }

const namespacePattern = /^\s*namespace\s+([\w.]+)\s*[;{]/m;

/** The class a form's file declares (MainForm.cs or MainForm.Designer.cs): its namespace and name. */
export function formClass(source: string): FormClass | undefined {
	const cls = /\bclass\s+([A-Za-z_]\w*)/.exec(source.replace(/\/\/.*$/gm, ''));
	if (!cls) return undefined;
	return { ns: namespacePattern.exec(source)?.[1], name: cls[1] };
}

/** The form the entry point starts: `Application.Run(new MainForm())`, spelled any way C# allows. */
const runPattern = /Application\s*\.\s*Run\s*\(\s*new\s+((?:global::)?[\w.]+)\s*\(\s*\)\s*\)/;

/** True for the file that starts the application with a form of its own making. */
export function startsAForm(source: string): boolean {
	return runPattern.test(source);
}

/**
 * Program.cs with Application.Run starting <paramref name="form"/> instead: the class alone when it is in
 * Program's namespace (or a namespace inside it, relative, as C# resolves it), else by its full name.
 * Undefined when the file has no Application.Run(new …()).
 */
export function setStartupForm(program: string, form: FormClass): { text: string; previous: string } | undefined {
	const m = runPattern.exec(program);
	if (!m) return undefined;
	const programNs = namespacePattern.exec(program)?.[1];
	let name = form.name;
	if (form.ns && form.ns !== programNs) {
		name = programNs && form.ns.startsWith(programNs + '.') ? form.ns.slice(programNs.length + 1) + '.' + form.name : form.ns + '.' + form.name;
	}
	const text = program.slice(0, m.index) + m[0].replace(m[1], name) + program.slice(m.index + m[0].length);
	return { text, previous: m[1] };
}

/** The runtime identifiers NetForms publishes for (its SkiaSharp and Avalonia natives): Windows and Linux. */
export const publishTargets = ['win-x64', 'win-arm64', 'linux-x64', 'linux-arm64'] as const;

/** The dotnet publish arguments: self-contained (the runtime goes along) or framework-dependent. */
export function publishArgs(project: string, rid: string, selfContained: boolean, output: string): string[] {
	return ['publish', project, '-c', 'Release', '-r', rid, '--self-contained', String(selfContained), '-o', output, '-nologo'];
}

// --- versions -------------------------------------------------------------------------------------

/**
 * Compares two NuGet/SemVer versions (0.1.0-preview.10 > 0.1.0-preview.9 > 0.0.9; a release is above its
 * previews): negative, zero or positive.
 */
export function compareVersions(a: string, b: string): number {
	const [coreA, preA] = split(a), [coreB, preB] = split(b);
	for (let i = 0; i < Math.max(coreA.length, coreB.length); i++) {
		const d = (coreA[i] ?? 0) - (coreB[i] ?? 0);
		if (d) return d;
	}
	if (!preA.length || !preB.length) return preB.length - preA.length;
	for (let i = 0; i < Math.max(preA.length, preB.length); i++) {
		if (i >= preA.length) return -1;
		if (i >= preB.length) return 1;
		const x = preA[i], y = preB[i];
		const nx = /^\d+$/.test(x), ny = /^\d+$/.test(y);
		const d = nx && ny ? Number(x) - Number(y) : nx ? -1 : ny ? 1 : x < y ? -1 : x > y ? 1 : 0;
		if (d) return d;
	}
	return 0;
}

function split(v: string): [number[], string[]] {
	const clean = v.trim().replace(/^v/i, '').split('+')[0];
	const dash = clean.indexOf('-');
	const core = (dash < 0 ? clean : clean.slice(0, dash)).split('.').map((n) => Number(n) || 0);
	const pre = dash < 0 ? [] : clean.slice(dash + 1).split('.');
	return [core, pre];
}

/** The highest of the versions, or undefined for none. */
export function newest(versions: (string | undefined)[]): string | undefined {
	return versions.filter((v): v is string => !!v).sort(compareVersions).pop();
}

/**
 * The NetForms version a project file (or Directory.Packages.props) names:
 * <PackageReference Include="NetForms" Version="…"/> or <PackageVersion Include="NetForms" Version="…"/>.
 */
export function netformsVersionIn(text: string): string | undefined {
	for (const tag of text.match(/<(?:PackageReference|PackageVersion)\b[^>]*>/g) ?? []) {
		if (!/\bInclude\s*=\s*"NetForms"/.test(tag)) continue;
		const m = /\bVersion\s*=\s*"([^"]+)"/.exec(tag);
		if (m) return m[1];
	}
	return undefined;
}

/** The same file with NetForms at <paramref name="version"/>. */
export function withNetFormsVersion(text: string, version: string): string {
	return text.replace(/<(?:PackageReference|PackageVersion)\b[^>]*>/g, (tag) =>
		/\bInclude\s*=\s*"NetForms"/.test(tag) ? tag.replace(/(\bVersion\s*=\s*")[^"]+(")/, `$1${version}$2`) : tag);
}

/** The newest .NET 10 SDK of `dotnet --list-sdks` ("10.0.112 [/usr/lib/dotnet/sdk]"). */
export function newestSdk(listSdks: string, major = 10): string | undefined {
	return newest(listSdks.split(/\r?\n/).map((l) => l.trim().split(/\s+/)[0]).filter((v) => v && v.startsWith(`${major}.`)));
}

/**
 * The command that installs or updates the .NET 10 SDK here: winget on Windows, the distribution's package
 * manager on Ubuntu/Debian and Fedora/RHEL, Microsoft's install script elsewhere. The user runs it (it may
 * ask for sudo); the extension only types it into a terminal.
 */
export function sdkCommand(platform: string, osRelease: string, installed: boolean): string {
	if (platform === 'win32') return installed ? 'winget upgrade Microsoft.DotNet.SDK.10' : 'winget install Microsoft.DotNet.SDK.10';
	const id = /^ID_LIKE=(.*)$/m.exec(osRelease)?.[1] + ' ' + /^ID=(.*)$/m.exec(osRelease)?.[1];
	if (/ubuntu|debian/.test(id)) return installed ? 'sudo apt update && sudo apt install --only-upgrade dotnet-sdk-10.0' : 'sudo apt update && sudo apt install dotnet-sdk-10.0';
	if (/fedora|rhel|centos/.test(id)) return installed ? 'sudo dnf upgrade dotnet-sdk-10.0' : 'sudo dnf install dotnet-sdk-10.0';
	return 'curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0';
}
