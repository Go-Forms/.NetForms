// The visual designer: a custom editor for *.Designer.cs. The webview (media/designer.js) is the
// editing chrome - selection, handles, drag, snap lines, property grid, toolbox - and everything it
// shows comes from the host process, which holds the live form and paints it with NetForms itself.
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { DesignerView, findHost, HostClient, HostRequestError, Op } from './hostClient';

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
	| { type: 'run' };

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
	}

	get file(): string { return this.document.uri.fsPath; }

	private post(message: object) { void this.panel.webview.postMessage(message); }

	private ensureHost(): HostClient {
		if (this.host?.alive) return this.host;
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
					const [view, toolbox] = await Promise.all([host.open(this.file), host.toolbox()]);
					const snap = vscode.workspace.getConfiguration('netforms').get<boolean>('snapToLines', true);
					this.post({ type: 'init', view, toolbox, snap, strings: vscode.l10n.bundle ?? {} });
					break;
				}
				case 'apply': this.show(await this.write(() => this.ensureHost().apply(m.ops)), m.select); break;
				case 'undo': this.show(await this.write(() => this.ensureHost().undo())); break;
				case 'redo': this.show(await this.write(() => this.ensureHost().redo())); break;
				case 'properties': this.post({ type: 'properties', id: m.id, rows: await this.ensureHost().properties(m.id) }); break;
				case 'events': this.post({ type: 'events', id: m.id, rows: await this.ensureHost().events(m.id) }); break;
				case 'gotoHandler': await this.gotoHandler(m.handler); break;
				case 'viewCode': await this.viewCode(); break;
				case 'run': await vscode.commands.executeCommand('netforms.run', this.document.uri); break;
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

	dispose() {
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
