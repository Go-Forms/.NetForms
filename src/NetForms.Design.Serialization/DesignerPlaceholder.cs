using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetForms.Design.Serialization;

/// <summary>
/// Stands in for a control whose type the designer cannot load — typically a user control defined
/// in the user's own project before it is built, or a type of a library the project does not reference
/// (docs/designer-control-libraries.md). It takes the geometry and the common <see cref="Control"/>
/// properties, and paints the missing type's name so the form still reads correctly. A control whose
/// constructor threw is a placeholder too, drawn as a red cross with the error (decision 157).
/// </summary>
public class DesignerPlaceholder : Control
{
    public DesignerPlaceholder(string typeName) : this(typeName, null) { }

    public DesignerPlaceholder(string typeName, string? error)
    {
        TypeName = typeName;
        Error = error;
    }

    /// <summary>The type as written in the designer file.</summary>
    public string TypeName { get; }

    /// <summary>Why the type is known but could not be created (its constructor threw); null for an unknown type.</summary>
    public string? Error { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        if (Error != null)
        {
            DrawErrorPlate(e.Graphics, r, TypeName + ": " + Error);
            return;
        }
        using (var pen = new Pen(SystemColors.ControlDark) { DashStyle = DashStyle.Dash })
            e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
        TextRenderer.DrawText(e.Graphics, TypeName, Font, r, SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }
}
