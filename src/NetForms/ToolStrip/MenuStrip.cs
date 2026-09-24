using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The menu bar at the top of a form. Its items are <see cref="ToolStripMenuItem"/>s whose
/// drop-downs open on click and then follow the pointer from one top-level item to the next,
/// the way Windows menus behave.
/// </summary>
public class MenuStrip : ToolStrip
{
    private ToolStripMenuItem? _mdiWindowListItem;

    public MenuStrip()
    {
        GripStyle = ToolStripGripStyle.Hidden;
        CanOverflow = false;
        Dock = DockStyle.Top;
        Stretch = true;
    }

    protected override Size DefaultSize => new Size(200, 24);

    protected override Padding DefaultPadding => new Padding(6, 2, 0, 2);

    protected override Padding DefaultGripMargin => Padding.Empty;

    protected override bool DefaultShowItemToolTips => false;

    /// <summary>A menu bar always spans its dock edge; WinForms exposes this on ToolStrip.</summary>
    [Category("Layout")]
    [Description("Specifies whether the ToolStrip stretches from end to end in the rafting container.")]
    [DefaultValue(true)]
    public bool Stretch { get; set; }

    [Category("Behavior")]
    [Description("Specifies the item whose DropDown will show the list of MDI windows.")]
    [DefaultValue(null)]
    public ToolStripMenuItem? MdiWindowListItem
    {
        get => _mdiWindowListItem;
        set => _mdiWindowListItem = value;
    }

    protected internal override ToolStripItem CreateDefaultItem(string? text, Image? image, EventHandler? onClick)
    {
        if (text == "-") return new ToolStripSeparator();
        return new ToolStripMenuItem(text, image, onClick);
    }

    /// <summary>Once a menu is open, sliding onto another top-level item switches to its drop-down.</summary>
    internal override void SelectItem(ToolStripItem? item)
    {
        var previous = SelectedItemInternal;
        base.SelectItem(item);
        if (ReferenceEquals(previous, item)) return;

        if (previous is ToolStripDropDownItem { DropDownVisible: true } open && !ReferenceEquals(open, item))
        {
            if (item is ToolStripDropDownItem { HasDropDownItems: true } next && next.Enabled)
            {
                open.HideDropDown();
                next.ShowDropDown();
            }
        }
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool CanOverflow { get => base.CanOverflow; set => base.CanOverflow = value; }

    [Category("Appearance")]
    [DefaultValue(ToolStripGripStyle.Hidden)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new ToolStripGripStyle GripStyle { get => base.GripStyle; set => base.GripStyle = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool ShowItemToolTips { get => base.ShowItemToolTips; set => base.ShowItemToolTips = value; }
}

/// <summary>
/// The bar along the bottom of a form. Items sit in one row; a <see cref="ToolStripStatusLabel"/>
/// with Spring set takes the space the others leave, and the sizing grip sits in the corner.
/// </summary>
public class StatusStrip : ToolStrip
{
    /// <summary>Grip metrics from dotnet/winforms' StatusStrip (GripWidth = 15).</summary>
    private const int GripWidth = 15;

    private bool _sizingGrip = true;

    public StatusStrip()
    {
        GripStyle = ToolStripGripStyle.Hidden;
        CanOverflow = false;
        LayoutStyle = ToolStripLayoutStyle.Table;
        Dock = DockStyle.Bottom;
        ShowItemToolTips = false;
    }

    protected override Size DefaultSize => new Size(200, 22);

    protected override Padding DefaultPadding => new Padding(1, 0, GripWidth - 1, 0);

    protected override DockStyle DefaultDock => DockStyle.Bottom;

    protected override bool DefaultShowItemToolTips => false;

    [Category("Appearance")]
    [Description("Determines whether a StatusStrip has a sizing grip.")]
    [DefaultValue(true)]
    public bool SizingGrip
    {
        get => _sizingGrip;
        set
        {
            if (_sizingGrip == value) return;
            _sizingGrip = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    public Rectangle SizeGripBounds => _sizingGrip
        ? new Rectangle(Math.Max(0, Width - GripWidth), 0, GripWidth, Height)
        : Rectangle.Empty;

    [Category("Appearance")]
    [Description("Specifies the background color of the ToolStrip.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : Theme.StatusStripBackground;
        set => base.BackColor = value;
    }

    protected internal override ToolStripItem CreateDefaultItem(string? text, Image? image, EventHandler? onClick)
    {
        if (text == "-") return new ToolStripSeparator();
        return new ToolStripStatusLabel(text, image, onClick);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_sizingGrip && FindForm() is { WindowState: FormWindowState.Normal })
        {
            Renderer.DrawStatusStripSizingGrip(new ToolStripRenderEventArgs(e.Graphics, this, ClientRectangle, BackColor));
        }
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool CanOverflow { get => base.CanOverflow; set => base.CanOverflow = value; }

    [Category("Layout")]
    [DefaultValue(DockStyle.Bottom)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override DockStyle Dock { get => base.Dock; set => base.Dock = value; }

    [Category("Appearance")]
    [DefaultValue(ToolStripGripStyle.Hidden)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new ToolStripGripStyle GripStyle { get => base.GripStyle; set => base.GripStyle = value; }

    [Category("Layout")]
    [DefaultValue(ToolStripLayoutStyle.Table)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override ToolStripLayoutStyle LayoutStyle { get => base.LayoutStyle; set => base.LayoutStyle = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool ShowItemToolTips { get => base.ShowItemToolTips; set => base.ShowItemToolTips = value; }

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? PaddingChanged
    {
        add => base.PaddingChanged += value;
        remove => base.PaddingChanged -= value;
    }
}
