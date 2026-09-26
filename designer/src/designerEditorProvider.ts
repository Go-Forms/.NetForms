// The visual designer: a custom editor for *.Designer.cs. The webview (media/designer.js) is the
// editing chrome - selection, handles, drag, snap lines, property grid, toolbox - and everything it
// shows comes from the host process, which holds the live form and paints it with NetForms itself.
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { DesignerView, findHost, HostClient, HostRequestError, Op } from './hostClient';
import { librariesChanged, ProjectLibraries, projectFileOf, resolveLibraries } from './libraryCommands';

type FromWebview =
	| { type: 'ready' }
	| { type: 'apply'; ops: Op[]; select?: string[] }
	| { type: 'undo' }
	| { type: 'redo' }
	| { type: 'properties'; id: string }
	| { type: 'events'; id: string }
	| { type: 'gotoHandler'; handler: string }
	| { type: 'viewCode' }
	| { type: 'openAsText' }
	| { type: 'run' }
	| { type: 'setStartup' }
	| { type: 'copy'; ids: string[] }
	| { type: 'cut'; ids: string[]; select?: string[] }
	| { type: 'paste'; parent: string }
	| { type: 'duplicate'; ids: string[] }
	| { type: 'rescan' }
	| { type: 'addLibrary' };

/** The marker of the text the host's copy produces (DesignSurface.ClipboardFormat). */
const clipboardFormat = 'netforms/components-1';

/** The designer file's hand-written half: MainForm.Designer.cs → MainForm.cs. */
export function companionOf(designerPath: string): string {
	return designerPath.replace(/\.Designer\.cs$/i, '.cs');
}

export class DesignerEditorProvider implements vscode.CustomTextEditorProvider {
	static readonly viewType = 'netforms.designer';

	/** The designer that has focus, for the commands (View Code, Open as Text, Tidy). */
	static active: DesignerSession | undefined;

	constructor(private readonly context: vscode.ExtensionContext, private readonly log: vscode.OutputChannel) {}

	async resolveCustomTextEditor(document: vscode.TextDocument, panel: vscode.WebviewPanel): Promise<void> {
		const media = vscode.Uri.joinPath(this.context.extensionUri, 'media');
		panel.webview.options = { enableScripts: true, localResourceRoots: [media] };
		panel.webview.html = html(panel.webview, media);
		const session = new DesignerSession(document, panel, this.context, this.log);
		if (panel.active) DesignerEditorProvider.active = session;
		panel.onDidChangeViewState(() => { if (panel.active) DesignerEditorProvider.active = session; });
		panel.onDidDispose(() => {
			if (DesignerEditorProvider.active === session) DesignerEditorProvider.active = undefined;
			session.dispose();
		});
	}
}

export class DesignerSession implements vscode.Disposable {
	private host: HostClient | undefined;
	private readonly subscriptions: vscode.Disposable[] = [];
	/**
	 * The host saves each edit to disk, and VS Code reports that as a change of the document - late,
	 * whenever its file watcher gets to it. Those changes are ours and must not reload the form (that
	 * would drop the selection and rebuild the inspector); they are told apart by content, not by time.
	 */
	private readonly ownTexts: string[] = [];
	private writing = 0;
	private recheck = false;
	/** The project's libraries as last loaded, and what they were loaded from (to skip a reload that changes nothing). */
	private libraries: ProjectLibraries | undefined;
	private loadedSignature = '';
	private libraryTimer: NodeJS.Timeout | undefined;
	private readonly projectFile: string | undefined;

	constructor(
		readonly document: vscode.TextDocument,
		private readonly panel: vscode.WebviewPanel,
		private readonly context: vscode.ExtensionContext,
		private readonly log: vscode.OutputChannel,
	) {
		this.subscriptions.push(panel.webview.onDidReceiveMessage((m: FromWebview) => this.onMessage(m)));
		this.subscriptions.push(vscode.workspace.onDidChangeTextDocument((e) => {
			if (e.document !== document || e.contentChanges.length === 0) return;
			// Edited as text (or by git): read it again once the edit has been saved.
			this.changed();
		}));
		this.subscriptions.push(vscode.workspace.onDidSaveTextDocument((d) => {
			if (d === document) this.changed();
		}));
		// The project's controls and libraries (decision 157): reloaded after each build of the project (its
		// bin/ and obj/ change), after Add/Remove Control Library and Rescan, and when the folder becomes trusted.
		this.projectFile = projectFileOf(this.file);
		this.subscriptions.push(librariesChanged.event((project) => {
			if (this.projectFile && path.resolve(project) === path.resolve(this.projectFile)) this.librariesSoon(0);
		}));
		this.subscriptions.push(vscode.workspace.onDidGrantWorkspaceTrust(() => this.librariesSoon(0)));
		if (this.projectFile) {
			const dir = path.dirname(this.projectFile);
			for (const pattern of ['{bin,obj}/**/*.{dll,json}', '*.csproj']) {
				const watcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(dir, pattern));
				const soon = () => this.librariesSoon(1500);
				watcher.onDidCreate(soon); watcher.onDidChange(soon); watcher.onDidDelete(soon);
				this.subscriptions.push(watcher);
			}
			const folder = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(this.projectFile));
			if (folder) {
				const config = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(folder, '.vscode/netforms.json'));
				const soon = () => this.librariesSoon(300);
				config.onDidCreate(soon); config.onDidChange(soon); config.onDidDelete(soon);
				this.subscriptions.push(config);
			}
		}
	}

	/** A build writes many files: the libraries are reloaded once it has settled. */
	private librariesSoon(delay: number) {
		if (this.libraryTimer) clearTimeout(this.libraryTimer);
		this.libraryTimer = setTimeout(() => { this.libraryTimer = undefined; void this.reloadLibraries(); }, delay);
	}

	/** What the loaded assemblies were built from: the paths and their times. */
	private static signature(libs: ProjectLibraries | undefined): string {
		if (!libs) return '';
		return libs.load.map((f) => { try { return `${f}@${fs.statSync(f).mtimeMs}`; } catch { return f; } }).join('|');
	}

	/** Loads the project's build output into the host (when it changed) and returns the toolbox groups. */
	private async loadLibraries(host: HostClient): Promise<{ view?: DesignerView }> {
		try { this.libraries = resolveLibraries(this.file); }
		catch (err) { this.log.appendLine(`Libraries of ${this.file}: ${err instanceof Error ? err.message : err}`); this.libraries = undefined; }
		const signature = DesignerSession.signature(this.libraries);
		if (signature === this.loadedSignature) return {};
		this.loadedSignature = signature;
		let result;
		try { result = await host.libraries(this.libraries?.load ?? []); }
		catch (err) {
			// A host older than the libraries (netforms.designerHostPath): the form opens without them.
			this.log.appendLine(`Designer libraries: ${err instanceof Error ? err.message : err}`);
			return {};
		}
		for (const e of result.errors) this.log.appendLine(`Designer libraries: ${e}`);
		if (result.error) this.reportError(new HostRequestError(result.error));
		return { view: result.view };
	}

	private async toolbox(host: HostClient) {
		try { return await host.toolbox(this.libraries?.groups ?? []); }
		catch (err) {
			this.log.appendLine(`Toolbox of the project: ${err instanceof Error ? err.message : err}`);
			return host.toolbox();
		}
	}

	private async reloadLibraries() {
		const host = this.host;
		if (!host?.alive) return;
		try {
			const view: DesignerView | undefined = await this.write(async () => (await this.loadLibraries(host)).view as DesignerView);
			if (view) this.post({ type: 'view', view });
			this.post({ type: 'toolbox', toolbox: await this.toolbox(host), notice: this.libraries?.notice });
		} catch (err) {
			this.reportError(err);
		}
	}

	get file(): string { return this.document.uri.fsPath; }

	private post(message: object) { void this.panel.webview.postMessage(message); }

	private ensureHost(): HostClient {
		if (this.host?.alive) return this.host;
		this.loadedSignature = ''; // a new process has nothing loaded
		const hostPath = findHost(this.context.extensionPath, this.file);
		if (!hostPath) throw new Error(vscode.l10n.t('The NetForms designer host was not found. Build tools/NetFormsDesigner.Host or set "netforms.designerHostPath".'));
		this.log.appendLine(`Starting the designer host: ${hostPath}`);
		this.host = new HostClient(hostPath, this.log);
		return this.host;
	}

	private async onMessage(m: FromWebview) {
		try {
			switch (m.type) {
				case 'ready': {
					const host = this.ensureHost();
					this.loadedSignature = '';
					await this.loadLibraries(host);
					const [view, toolbox] = await Promise.all([host.open(this.file), this.toolbox(host)]);
					const snap = vscode.workspace.getConfiguration('netforms').get<boolean>('snapToLines', true);
					this.post({ type: 'init', view, toolbox, notice: this.libraries?.notice, snap, strings: vscode.l10n.bundle ?? {} });
					break;
				}
				case 'rescan': await vscode.commands.executeCommand('netforms.rescanToolbox', this.document.uri); break;
				case 'addLibrary': await vscode.commands.executeCommand('netforms.addControlLibrary', this.document.uri); break;
				case 'apply': this.show(await this.write(() => this.ensureHost().apply(m.ops)), m.select); break;
				case 'undo': this.show(await this.write(() => this.ensureHost().undo())); break;
				case 'redo': this.show(await this.write(() => this.ensureHost().redo())); break;
				case 'properties': this.post({ type: 'properties', id: m.id, rows: await this.ensureHost().properties(m.id) }); break;
				case 'events': this.post({ type: 'events', id: m.id, rows: await this.ensureHost().events(m.id) }); break;
				case 'gotoHandler': await this.gotoHandler(m.handler); break;
				case 'viewCode': await this.viewCode(); break;
				case 'run': await vscode.commands.executeCommand('netforms.run', this.document.uri); break;
				case 'setStartup': await vscode.commands.executeCommand('netforms.setStartupForm', this.document.uri); break;
				case 'copy': {
					await vscode.env.clipboard.writeText(await this.ensureHost().copy(m.ids));
					this.post({ type: 'copied', count: m.ids.length });
					break;
				}
				case 'cut': {
					const host = this.ensureHost();
					await vscode.env.clipboard.writeText(await host.copy(m.ids));
					this.show(await this.write(() => host.apply(m.ids.map((id) => ({ op: 'remove', id } as Op)))), m.select);
					break;
				}
				case 'paste': {
					const text = await vscode.env.clipboard.readText();
					if (!text.includes(clipboardFormat)) throw new Error(vscode.l10n.t('The clipboard holds no NetForms controls. Copy some in a NetForms designer first.'));
					this.show(await this.write(() => this.ensureHost().apply([{ op: 'paste', value: text, parent: m.parent }])));
					break;
				}
				case 'duplicate': this.show(await this.write(() => this.ensureHost().apply([{ op: 'duplicate', ids: m.ids }]))); break;
				case 'openAsText': await vscode.commands.executeCommand('vscode.openWith', this.document.uri, 'default'); break;
			}
		} catch (err) {
			this.reportError(err);
		}
	}

	private async write(action: () => Promise<DesignerView>): Promise<DesignerView> {
		this.writing++;
		try { return await action(); } finally {
			this.writing--;
			this.rememberOwnText();
			if (this.writing === 0 && this.recheck) {
				this.recheck = false;
				this.changed();
			}
		}
	}

	private rememberOwnText() {
		let text: string;
		try { text = fs.readFileSync(this.file, 'utf8'); } catch { return; }
		text = normalize(text);
		if (this.ownTexts.includes(text)) return;
		this.ownTexts.push(text);
		if (this.ownTexts.length > 8) this.ownTexts.shift();
	}

	/** The document changed on disk or in a text editor: reload the form unless the change is our own write. */
	private changed() {
		if (this.writing > 0) { this.recheck = true; return; }
		if (this.document.isDirty) return;
		if (this.ownTexts.includes(normalize(this.document.getText()))) return;
		void this.reopen();
	}

	private show(view: DesignerView, select?: string[]) {
		this.post({ type: 'view', view, select });
		if (view.handler?.created) void this.gotoHandler(view.handler.method, view.handler.line);
	}

	private reportError(err: unknown) {
		if (err instanceof HostRequestError) {
			const e = err.error;
			if (e.kind === 'code') {
				this.post({ type: 'parseError', message: e.message, line: e.line, column: e.column });
				return;
			}
			this.post({ type: 'error', message: e.message });
			if (e.detail) this.log.appendLine(e.detail);
			return;
		}
		const message = err instanceof Error ? err.message : String(err);
		this.log.appendLine(message);
		this.post({ type: 'error', message });
	}

	async reopen() {
		// The selection stays where the components it names are still there.
		try { this.post({ type: 'view', view: await this.ensureHost().open(this.file) }); }
		catch (err) { this.reportError(err); }
	}

	/** F7: the form's code, where the handlers live. */
	async viewCode() {
		const code = companionOf(this.file);
		if (!fs.existsSync(code)) throw new Error(vscode.l10n.t('{0} does not exist next to the designer file.', path.basename(code)));
		await vscode.window.showTextDocument(vscode.Uri.file(code), { viewColumn: vscode.ViewColumn.Active });
	}

	async gotoHandler(method: string, line?: number) {
		const code = companionOf(this.file);
		if (!fs.existsSync(code)) return;
		const doc = await vscode.workspace.openTextDocument(code);
		let target = line ? line - 1 : -1;
		if (target < 0) {
			const re = new RegExp(`\\b${method}\\s*\\(`);
			for (let i = 0; i < doc.lineCount; i++) if (re.test(doc.lineAt(i).text)) { target = i; break; }
		}
		const editor = await vscode.window.showTextDocument(doc, { viewColumn: vscode.ViewColumn.Active });
		if (target >= 0) {
			// Inside the braces, on the empty line of a fresh stub.
			const body = Math.min(target + 2, doc.lineCount - 1);
			const pos = new vscode.Position(body, doc.lineAt(body).firstNonWhitespaceCharacterIndex || 12);
			editor.selection = new vscode.Selection(pos, pos);
			editor.revealRange(new vscode.Range(pos, pos), vscode.TextEditorRevealType.InCenter);
		}
	}

	/** Tidy = read and write back: the writer's canonical form. */
	async tidy() {
		this.show(await this.write(() => this.ensureHost().apply([])));
	}

	/** The host of this designer, for the library commands (scan before adding). */
	hostClient(): HostClient { return this.ensureHost(); }

	dispose() {
		if (this.libraryTimer) clearTimeout(this.libraryTimer);
		for (const s of this.subscriptions) s.dispose();
		this.host?.dispose();
	}
}

/** The text as VS Code holds it: no BOM, one kind of line break. */
function normalize(text: string): string {
	return text.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
}

function html(webview: vscode.Webview, media: vscode.Uri): string {
	const nonce = Array.from({ length: 32 }, () => Math.floor(Math.random() * 36).toString(36)).join('');
	const uri = (f: string) => webview.asWebviewUri(vscode.Uri.joinPath(media, f));
	return `<!DOCTYPE html>
<html lang="${vscode.env.language}">
<head>
<meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src ${webview.cspSource} data:; style-src ${webview.cspSource} 'unsafe-inline'; script-src 'nonce-${nonce}';">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<link href="${uri('designer.css')}" rel="stylesheet">
<title>NetForms Designer</title>
</head>
<body>
<div id="app">
  <div id="toolbar"></div>
  <div id="main">
    <aside id="toolbox"></aside>
    <section id="workspace"><div id="stage"></div><div id="tray"></div></section>
    <aside id="inspector"></aside>
  </div>
  <div id="status"></div>
</div>
<script nonce="${nonce}" src="${uri('geometry.js')}"></script>
<script nonce="${nonce}" src="${uri('designer.js')}"></script>
</body>
</html>`;
}
