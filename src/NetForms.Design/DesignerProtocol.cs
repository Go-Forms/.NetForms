using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NetForms.Design.Serialization;

namespace NetForms.Design;

/// <summary>
/// The protocol between the designer client (the VS Code extension) and the host process: one JSON
/// object per line in each direction (Ф5.3, docs/PLAN.md).
/// </summary>
/// <remarks>
/// <para>Request: <c>{"id": 1, "method": "apply", "params": {...}}</c>. Response: <c>{"id": 1, "result": ...}</c>
/// or <c>{"id": 1, "error": {"kind": "edit"|"code"|"internal", "message": ..., "file", "line", "column"}}</c>
/// (<c>code</c> - the designer file cannot be read, with its position; <c>edit</c> - an edit was refused and
/// nothing changed).</para>
/// <list type="table">
/// <item><term>hello</term><description>→ <c>{name, protocol}</c>.</description></item>
/// <item><term>open</term><description><c>{path, autoSave = true}</c> → view.</description></item>
/// <item><term>render</term><description>→ view (base64 PNG of the client area, every component's rectangle, the tray).</description></item>
/// <item><term>apply</term><description><c>{ops: [DesignerOp…]}</c> → view; one undoable step, all or nothing.</description></item>
/// <item><term>undo / redo</term><description>→ view.</description></item>
/// <item><term>properties / events</term><description><c>{id}</c> → the property grid / the Events tab of a component.</description></item>
/// <item><term>toolbox</term><description><c>{libraries?: [DesignerToolboxLibrary…]}</c> → categories: a group per library of the project first, then NetForms's.</description></item>
/// <item><term>libraries</term><description><c>{assemblies: [path…]}</c>: load the build output of the project (its own assembly; what is beside it
/// comes along) for the forms to use; the open form is read again with it → <c>{assemblies, errors, view?}</c>. An empty list unloads.</description></item>
/// <item><term>scan</term><description><c>{paths: [path…]}</c> → what each assembly offers the toolbox and whether it can be used; no code of it runs.</description></item>
/// <item><term>scanPackage</term><description><c>{path}</c>: a <c>.nupkg</c> or an extracted package → the same, for the lib/ folder a net10.0 project takes.</description></item>
/// <item><term>save</term><description>→ <c>{saved}</c> (only needed with autoSave off).</description></item>
/// <item><term>close</term><description>closes the form; the host exits after answering.</description></item>
/// </list>
/// </remarks>
public sealed class DesignerProtocol : IDisposable
{
    public const int Version = 1;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private DesignSurface? _surface;
    private DesignerLibraries _libraries = DesignerLibraries.None;

    /// <summary>Set once "close" has been answered; the host loop stops then.</summary>
    public bool Closed { get; private set; }

    public DesignSurface? Surface => _surface;

    /// <summary>Answers one request line with one response line.</summary>
    public string Handle(string requestLine)
    {
        JsonNode? id = null;
        try
        {
            var request = JsonNode.Parse(requestLine)?.AsObject() ?? throw new DesignerEditException("The request is empty.");
            id = request["id"]?.DeepClone();
            var method = (string?)request["method"] ?? throw new DesignerEditException("The request has no method.");
            var p = request["params"] as JsonObject ?? new JsonObject();
            var result = Dispatch(method, p);
            return new JsonObject { ["id"] = id, ["result"] = JsonSerializer.SerializeToNode(result, s_json) }.ToJsonString(s_json);
        }
        catch (Exception ex)
        {
            return new JsonObject { ["id"] = id, ["error"] = JsonSerializer.SerializeToNode(Error(ex), s_json) }.ToJsonString(s_json);
        }
    }

    private object? Dispatch(string method, JsonObject p)
    {
        switch (method)
        {
            case "hello":
                return new { name = "NetForms designer host", protocol = Version };
            case "open":
            {
                var path = (string?)p["path"] ?? throw new DesignerEditException("open needs a path.");
                _surface?.Dispose();
                _surface = null;
                _surface = DesignSurface.Open(path, _libraries);
                _surface.AutoSave = (bool?)p["autoSave"] ?? true;
                return _surface.Render();
            }
            case "render":
                return Current().Render();
            case "apply":
            {
                var ops = p["ops"]?.Deserialize<List<DesignerOp>>(s_json) ?? new List<DesignerOp>();
                Current().Apply(ops);
                return Current().Render();
            }
            case "undo":
                Current().Undo();
                return Current().Render();
            case "redo":
                Current().Redo();
                return Current().Render();
            case "copy":
            {
                var ids = p["ids"]?.Deserialize<List<string>>(s_json) ?? new List<string>();
                return new { text = Current().Copy(ids) };
            }
            case "properties":
                return Current().GetProperties((string?)p["id"]);
            case "events":
                return Current().GetEvents((string?)p["id"]);
            case "toolbox":
                return DesignerToolbox.Categories(p["libraries"]?.Deserialize<List<DesignerToolboxLibrary>>(s_json), _libraries);
            case "libraries":
                return LoadLibraries(p["assemblies"]?.Deserialize<List<string>>(s_json) ?? new List<string>());
            case "scan":
                return ControlLibraryScanner.Scan(p["paths"]?.Deserialize<List<string>>(s_json) ?? new List<string>());
            case "scanPackage":
                return ControlLibraryScanner.ScanPackage((string?)p["path"] ?? throw new DesignerEditException("scanPackage needs a path."));
            case "save":
                Current().Save();
                return new { saved = true };
            case "close":
                _surface?.Dispose();
                _surface = null;
                _libraries.Dispose();
                _libraries = DesignerLibraries.None;
                Closed = true;
                return new { closed = true };
            default:
                throw new DesignerEditException($"Unknown method '{method}'.");
        }
    }

    /// <summary>
    /// Replaces the project's assemblies. The open form is read again with the new ones (its file is the
    /// truth, so nothing is lost; undo history starts over), and only then are the old ones unloaded - no
    /// object of theirs is left on the canvas.
    /// </summary>
    private object LoadLibraries(List<string> assemblies)
    {
        var next = DesignerLibraries.Load(assemblies);
        var old = _libraries;
        var oldSurface = _surface;
        _libraries = next;
        DesignerView? view = null;
        object? error = null;
        if (oldSurface?.FilePath != null)
        {
            oldSurface.Save();
            _surface = null;
            oldSurface.Dispose();
            try
            {
                _surface = DesignSurface.Open(oldSurface.FilePath, _libraries);
                _surface.AutoSave = oldSurface.AutoSave;
                view = _surface.Render();
            }
            catch (Exception ex) { error = Error(ex); }
        }
        if (!ReferenceEquals(old, next)) old.Dispose();
        return new
        {
            assemblies = next.Assemblies.Select(a => a.GetName().Name).ToList(),
            errors = next.Errors,
            view,
            error,
        };
    }

    private DesignSurface Current() => _surface ?? throw new DesignerEditException("No form is open.");

    private static object Error(Exception ex) => ex switch
    {
        DesignerCodeException code => new { kind = "code", message = code.Reason, file = code.FilePath, line = code.Line, column = code.Column },
        DesignerEditException edit => new { kind = "edit", message = edit.Message },
        _ => new { kind = "internal", message = ex.Message, detail = ex.ToString() },
    };

    public void Dispose()
    {
        _surface?.Dispose();
        _libraries.Dispose();
    }
}
