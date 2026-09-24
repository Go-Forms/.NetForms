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
/// <item><term>toolbox</term><description>→ categories.</description></item>
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
                _surface = DesignSurface.Open(path);
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
            case "properties":
                return Current().GetProperties((string?)p["id"]);
            case "events":
                return Current().GetEvents((string?)p["id"]);
            case "toolbox":
                return DesignerToolbox.Categories();
            case "save":
                Current().Save();
                return new { saved = true };
            case "close":
                _surface?.Dispose();
                _surface = null;
                Closed = true;
                return new { closed = true };
            default:
                throw new DesignerEditException($"Unknown method '{method}'.");
        }
    }

    private DesignSurface Current() => _surface ?? throw new DesignerEditException("No form is open.");

    private static object Error(Exception ex) => ex switch
    {
        DesignerCodeException code => new { kind = "code", message = code.Reason, file = code.FilePath, line = code.Line, column = code.Column },
        DesignerEditException edit => new { kind = "edit", message = edit.Message },
        _ => new { kind = "internal", message = ex.Message, detail = ex.ToString() },
    };

    public void Dispose() => _surface?.Dispose();
}
