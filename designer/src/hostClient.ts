// hostClient.ts is the ONLY file that talks to the designer host process
// (tools/NetFormsDesigner.Host, protocol in src/NetForms.Design/DesignerProtocol.cs): one JSON
// object per line each way, answered in order, matched by id. It replaces GoFormsDesigner's
// goTool.ts - but the host is long-lived, so a canvas edit costs a message, not a process start.
import * as cp from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as readline from 'readline';
import * as vscode from 'vscode';

export interface ViewItem {
	id: string;
	type: string;
	parent: string;
	kind: 'control' | 'nested' | 'item';
	x: number; y: number; w: number; h: number;
	left: number; top: number;
	clientX: number; clientY: number;
	visible: boolean;
	container: boolean;
	movable: boolean;
	resizable: boolean;
	dock: string;
	anchor: string;
	margin: number[];
	padding: number[];
	/** A TabControl's tab headers (x, y, w, h) and its selected page. */
	tabs?: number[][];
	selectedIndex?: number;
}

export interface HandlerLocation { file: string; method: string; line: number; created: boolean; }

export interface DesignerView {
	className: string;
	rootType: string;
	text: string;
	width: number;
	height: number;
	png: string;
	items: ViewItem[];
	tray: { id: string; type: string }[];
	canUndo: boolean;
	canRedo: boolean;
	handler?: HandlerLocation;
}

export interface Op {
	op: 'setBounds' | 'setForm' | 'setProp' | 'resetProp' | 'setItems' | 'setEvent' | 'add' | 'remove' | 'setParent' | 'rename' | 'bringToFront' | 'sendToBack';
	id?: string; type?: string; parent?: string;
	x?: number; y?: number; w?: number; h?: number;
	prop?: string; value?: string; values?: string[]; ids?: string[];
	event?: string; handler?: string;
}

export interface HostError { kind: 'edit' | 'code' | 'internal'; message: string; file?: string; line?: number; column?: number; detail?: string; }

export class HostRequestError extends Error {
	constructor(readonly error: HostError) { super(error.message); }
}

/** Where the host is: the setting, the copy bundled with the extension, or a NetForms checkout above the file. */
export function findHost(extensionPath: string, near?: string): string | undefined {
	const configured = vscode.workspace.getConfiguration('netforms').get<string>('designerHostPath');
	if (configured && fs.existsSync(configured)) return configured;
	const bundled = path.join(extensionPath, 'host', 'NetFormsDesigner.Host.dll');
	if (fs.existsSync(bundled)) return bundled;
	const starts = [near, extensionPath].filter((p): p is string => !!p);
	for (const start of starts) {
		for (let dir = path.dirname(start); ; dir = path.dirname(dir)) {
			for (const config of ['Release', 'Debug']) {
				const candidate = path.join(dir, 'tools', 'NetFormsDesigner.Host', 'bin', config, 'net10.0', 'NetFormsDesigner.Host.dll');
				if (fs.existsSync(candidate)) return candidate;
			}
			if (path.dirname(dir) === dir) break;
		}
	}
	return undefined;
}

/** One running host process. Requests are queued on the process's stdin and answered in order. */
export class HostClient implements vscode.Disposable {
	private readonly process: cp.ChildProcess;
	private readonly pending = new Map<number, { resolve: (v: any) => void; reject: (e: Error) => void }>();
	private nextId = 1;
	private exited = false;
	private stderr = '';

	constructor(hostPath: string, private readonly log: vscode.OutputChannel) {
		const dotnet = vscode.workspace.getConfiguration('netforms').get<string>('dotnetPath') || 'dotnet';
		const [cmd, args] = hostPath.toLowerCase().endsWith('.dll') ? [dotnet, [hostPath]] : [hostPath, []];
		this.process = cp.spawn(cmd, args, { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
		const lines = readline.createInterface({ input: this.process.stdout! });
		lines.on('line', (line) => this.onLine(line));
		this.process.stderr!.on('data', (d: Buffer) => {
			const text = d.toString('utf8');
			this.stderr = (this.stderr + text).slice(-4000);
			this.log.append(text);
		});
		this.process.on('exit', (code) => {
			this.exited = true;
			const error = new Error(`The designer host exited (code ${code}).${this.stderr ? '\n' + this.stderr : ''}`);
			for (const p of this.pending.values()) p.reject(error);
			this.pending.clear();
		});
		this.process.on('error', (err) => {
			this.exited = true;
			for (const p of this.pending.values()) p.reject(err);
			this.pending.clear();
		});
	}

	get alive(): boolean { return !this.exited; }

	private onLine(line: string) {
		let message: any;
		try { message = JSON.parse(line); } catch { this.log.appendLine('host: ' + line); return; }
		const waiter = this.pending.get(message.id);
		if (!waiter) return;
		this.pending.delete(message.id);
		if (message.error) waiter.reject(new HostRequestError(message.error));
		else waiter.resolve(message.result);
	}

	request<T>(method: string, params?: object): Promise<T> {
		if (this.exited) return Promise.reject(new Error('The designer host is not running.'));
		const id = this.nextId++;
		return new Promise<T>((resolve, reject) => {
			this.pending.set(id, { resolve, reject });
			this.process.stdin!.write(JSON.stringify({ id, method, params }) + '\n', 'utf8');
		});
	}

	open(file: string, autoSave = true) { return this.request<DesignerView>('open', { path: file, autoSave }); }
	render() { return this.request<DesignerView>('render'); }
	apply(ops: Op[]) { return this.request<DesignerView>('apply', { ops }); }
	undo() { return this.request<DesignerView>('undo'); }
	redo() { return this.request<DesignerView>('redo'); }
	properties(id: string) { return this.request<any[]>('properties', { id }); }
	events(id: string) { return this.request<any[]>('events', { id }); }
	toolbox() { return this.request<any[]>('toolbox'); }

	dispose() {
		if (this.exited) return;
		this.request('close').catch(() => undefined).finally(() => setTimeout(() => this.process.kill(), 2000));
	}
}
