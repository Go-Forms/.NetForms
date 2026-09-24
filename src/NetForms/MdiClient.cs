using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum MdiLayout
{
    Cascade = 0,
    TileHorizontal = 1,
    TileVertical = 2,
    ArrangeIcons = 3,
}

/// <summary>
/// The sunken area an MDI parent keeps its children in. It is an ordinary control docked to fill
/// the form, and each MDI child is one of its children - which is why a child form can be moved,
/// sized and clipped like any other control instead of needing a window of its own.
/// </summary>
[DesignerCategory("")]
public sealed class MdiClient : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
    }

    internal MdiClient()
    {
        SetStyle(ControlStyles.ContainerControl | ControlStyles.Selectable, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = SystemColors.AppWorkspace;
        Dock = DockStyle.Fill;
    }

    [Description("The collection of child controls within this control.")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public new ControlCollection Controls => base.Controls;

    /// <summary>The child forms, front-most first (their z-order).</summary>
    internal List<Form> Children
    {
        get
        {
            var children = new List<Form>();
            foreach (Control control in Controls)
            {
                if (control is Form form) children.Add(form);
            }
            return children;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(BackColor);
        pevent.Graphics.FillRectangle(brush, pevent.ClipRectangle);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // A maximised child fills whatever the client area has become.
        foreach (var child in Children) child.OnMdiClientResized();
    }

    /// <summary>Arranges the children the way <see cref="Form.LayoutMdi"/> was asked to.</summary>
    internal void LayoutMdi(MdiLayout value)
    {
        var children = new List<Form>();
        foreach (var child in Children)
        {
            if (child.VisibleOwn && child.WindowState != FormWindowState.Minimized) children.Add(child);
        }
        if (children.Count == 0) return;
        // Children are stored front-most first; arrange them back to front so the active one ends up on top.
        children.Reverse();

        var area = ClientRectangle;
        switch (value)
        {
            case MdiLayout.TileHorizontal:
                {
                    int height = Math.Max(40, area.Height / children.Count);
                    for (int i = 0; i < children.Count; i++)
                    {
                        children[i].RestoreForLayout();
                        children[i].Bounds = new Rectangle(area.X, area.Y + i * height, area.Width,
                            i == children.Count - 1 ? area.Height - i * height : height);
                    }
                    break;
                }
            case MdiLayout.TileVertical:
                {
                    int width = Math.Max(60, area.Width / children.Count);
                    for (int i = 0; i < children.Count; i++)
                    {
                        children[i].RestoreForLayout();
                        children[i].Bounds = new Rectangle(area.X + i * width, area.Y,
                            i == children.Count - 1 ? area.Width - i * width : width, area.Height);
                    }
                    break;
                }
            case MdiLayout.ArrangeIcons:
                {
                    int x = area.X;
                    int y = area.Bottom - Form.MdiChildCaptionHeight - 4;
                    foreach (var child in children)
                    {
                        child.RestoreForLayout();
                        child.Bounds = new Rectangle(x, y, 160, Form.MdiChildCaptionHeight + 4);
                        x += 164;
                        if (x + 160 > area.Right)
                        {
                            x = area.X;
                            y -= Form.MdiChildCaptionHeight + 8;
                        }
                    }
                    break;
                }
            default:
                {
                    int step = Form.MdiChildCaptionHeight + 2;
                    int width = Math.Max(160, area.Width - step * Math.Min(children.Count, 8) - 8);
                    int height = Math.Max(100, area.Height - step * Math.Min(children.Count, 8) - 8);
                    for (int i = 0; i < children.Count; i++)
                    {
                        children[i].RestoreForLayout();
                        int offset = i % 8 * step;
                        children[i].Bounds = new Rectangle(area.X + offset + 2, area.Y + offset + 2, width, height);
                    }
                    break;
                }
        }
        Invalidate();
    }
}
