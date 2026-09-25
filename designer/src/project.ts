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
