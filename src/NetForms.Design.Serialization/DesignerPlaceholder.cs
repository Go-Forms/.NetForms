using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetForms.Design.Serialization;

/// <summary>
/// Stands in for a control whose type the designer cannot load — typically a user control defined
/// in the user's own project, which the designer does not compile (a known limitation of Ф5, see
/// docs/PLAN.md). It takes the geometry and the common <see cref="Control"/> properties, and paints
/// the missing type's name so the form still reads correctly.
/// </summary>
public class DesignerPlaceholder : Control
{
    public DesignerPlaceholder(string typeName)
    {
        TypeName = typeName;
    }

    /// <summary>The type as written in the designer file.</summary>
    public string TypeName { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        using (var pen = new Pen(SystemColors.ControlDark) { DashStyle = DashStyle.Dash })
            e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
        TextRenderer.DrawText(e.Graphics, TypeName, Font, r, SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }
}
