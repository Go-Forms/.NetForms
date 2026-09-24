using System;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The colours the professional renderer paints with. WinForms computes these from the Windows
/// theme; we take them from <see cref="Theme"/> so a strip looks identical on both platforms.
/// Deriving and overriding a property is still the supported way to recolour a strip.
/// </summary>
public class ProfessionalColorTable
{
    public bool UseSystemColors { get; set; }

    public virtual Color ButtonSelectedHighlight => Theme.StripItemHot;
    public virtual Color ButtonSelectedHighlightBorder => Theme.StripItemBorder;
    public virtual Color ButtonPressedHighlight => Theme.StripItemPressed;
    public virtual Color ButtonPressedHighlightBorder => Theme.StripItemBorder;
    public virtual Color ButtonCheckedHighlight => Theme.StripItemChecked;
    public virtual Color ButtonCheckedHighlightBorder => Theme.StripItemBorder;
    public virtual Color ButtonPressedBorder => Theme.StripItemBorder;
    public virtual Color ButtonSelectedBorder => Theme.StripItemBorder;
    public virtual Color ButtonCheckedGradientBegin => Theme.StripItemChecked;
    public virtual Color ButtonCheckedGradientMiddle => Theme.StripItemChecked;
    public virtual Color ButtonCheckedGradientEnd => Theme.StripItemChecked;
    public virtual Color ButtonSelectedGradientBegin => Theme.StripItemHot;
    public virtual Color ButtonSelectedGradientMiddle => Theme.StripItemHot;
    public virtual Color ButtonSelectedGradientEnd => Theme.StripItemHot;
    public virtual Color ButtonPressedGradientBegin => Theme.StripItemPressed;
    public virtual Color ButtonPressedGradientMiddle => Theme.StripItemPressed;
    public virtual Color ButtonPressedGradientEnd => Theme.StripItemPressed;

    public virtual Color CheckBackground => Theme.MenuCheckBackground;
    public virtual Color CheckSelectedBackground => Theme.MenuCheckBackground;
    public virtual Color CheckPressedBackground => Theme.MenuCheckBackground;

    public virtual Color GripDark => Theme.StripGrip;
    public virtual Color GripLight => Color.White;

    public virtual Color ImageMarginGradientBegin => Theme.MenuImageMargin;
    public virtual Color ImageMarginGradientMiddle => Theme.MenuImageMargin;
    public virtual Color ImageMarginGradientEnd => Theme.MenuImageMargin;
    public virtual Color ImageMarginRevealedGradientBegin => Theme.MenuImageMargin;
    public virtual Color ImageMarginRevealedGradientMiddle => Theme.MenuImageMargin;
    public virtual Color ImageMarginRevealedGradientEnd => Theme.MenuImageMargin;

    public virtual Color MenuStripGradientBegin => Theme.StripBackground;
    public virtual Color MenuStripGradientEnd => Theme.StripBackground;
    public virtual Color MenuItemSelected => Theme.MenuItemSelected;
    public virtual Color MenuItemSelectedGradientBegin => Theme.MenuItemSelected;
    public virtual Color MenuItemSelectedGradientEnd => Theme.MenuItemSelected;
    public virtual Color MenuItemPressedGradientBegin => Theme.StripBackground;
    public virtual Color MenuItemPressedGradientMiddle => Theme.StripBackground;
    public virtual Color MenuItemPressedGradientEnd => Theme.StripBackground;
    public virtual Color MenuItemBorder => Theme.MenuItemSelectedBorder;
    public virtual Color MenuBorder => Theme.MenuBorder;

    public virtual Color SeparatorDark => Theme.StripSeparator;
    public virtual Color SeparatorLight => Color.White;

    public virtual Color StatusStripGradientBegin => Theme.StatusStripBackground;
    public virtual Color StatusStripGradientEnd => Theme.StatusStripBackground;

    public virtual Color ToolStripBorder => Theme.StripBorder;
    public virtual Color ToolStripDropDownBackground => Theme.MenuBackground;
    public virtual Color ToolStripGradientBegin => Theme.StripBackground;
    public virtual Color ToolStripGradientMiddle => Theme.StripBackground;
    public virtual Color ToolStripGradientEnd => Theme.StripBackground;
    public virtual Color ToolStripContentPanelGradientBegin => Theme.StripBackground;
    public virtual Color ToolStripContentPanelGradientEnd => Theme.StripBackground;
    public virtual Color ToolStripPanelGradientBegin => Theme.StripBackground;
    public virtual Color ToolStripPanelGradientEnd => Theme.StripBackground;

    public virtual Color OverflowButtonGradientBegin => Theme.StripBackground;
    public virtual Color OverflowButtonGradientMiddle => Theme.StripBackground;
    public virtual Color OverflowButtonGradientEnd => Theme.StripBackground;

    public virtual Color RaftingContainerGradientBegin => Theme.StripBackground;
    public virtual Color RaftingContainerGradientEnd => Theme.StripBackground;
}

/// <summary>The colour table the default renderer uses.</summary>
public static class ProfessionalColors
{
    private static readonly ProfessionalColorTable s_table = new();

    internal static ProfessionalColorTable ColorTable => s_table;

    public static Color ButtonSelectedHighlight => s_table.ButtonSelectedHighlight;
    public static Color ButtonPressedHighlight => s_table.ButtonPressedHighlight;
    public static Color ButtonCheckedHighlight => s_table.ButtonCheckedHighlight;
    public static Color CheckBackground => s_table.CheckBackground;
    public static Color GripDark => s_table.GripDark;
    public static Color GripLight => s_table.GripLight;
    public static Color MenuBorder => s_table.MenuBorder;
    public static Color MenuItemBorder => s_table.MenuItemBorder;
    public static Color MenuItemSelected => s_table.MenuItemSelected;
    public static Color SeparatorDark => s_table.SeparatorDark;
    public static Color SeparatorLight => s_table.SeparatorLight;
    public static Color ToolStripBorder => s_table.ToolStripBorder;
    public static Color ToolStripDropDownBackground => s_table.ToolStripDropDownBackground;
    public static Color ToolStripGradientBegin => s_table.ToolStripGradientBegin;
    public static Color ToolStripGradientEnd => s_table.ToolStripGradientEnd;
}

/// <summary>
/// The default look: flat fills from <see cref="ProfessionalColorTable"/>, a hairline border at
/// the strip's docked edge, white drop-downs with an image margin column.
/// </summary>
public class ToolStripProfessionalRenderer : ToolStripRenderer
{
    public ToolStripProfessionalRenderer() : this(new ProfessionalColorTable()) { }

    public ToolStripProfessionalRenderer(ProfessionalColorTable professionalColorTable)
    {
        ColorTable = professionalColorTable ?? throw new ArgumentNullException(nameof(professionalColorTable));
    }

    public ProfessionalColorTable ColorTable { get; }

    public bool RoundedEdges { get; set; } = true;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var strip = e.ToolStrip;
        var back = strip.IsBackColorSet ? strip.BackColor
            : strip is ToolStripDropDown ? ColorTable.ToolStripDropDownBackground
            : strip is StatusStrip ? ColorTable.StatusStripGradientBegin
            : strip is MenuStrip ? ColorTable.MenuStripGradientBegin
            : ColorTable.ToolStripGradientBegin;
        using var brush = new SolidBrush(back);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var strip = e.ToolStrip;
        var bounds = new Rectangle(Point.Empty, strip.Size);
        if (strip is ToolStripDropDown)
        {
            using var pen = new Pen(ColorTable.MenuBorder);
            e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            return;
        }
        if (strip is StatusStrip)
        {
            using var top = new Pen(ColorTable.ToolStripBorder);
            e.Graphics.DrawLine(top, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
            return;
        }

        // A single line on the edge the strip is docked against, as WinForms draws it.
        using var border = new Pen(ColorTable.ToolStripBorder);
        switch (strip.Dock)
        {
            case DockStyle.Left:
                e.Graphics.DrawLine(border, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
                break;
            case DockStyle.Right:
                e.Graphics.DrawLine(border, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);
                break;
            case DockStyle.Bottom:
                e.Graphics.DrawLine(border, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
                break;
            default:
                e.Graphics.DrawLine(border, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                break;
        }
    }

    protected override void OnRenderGrip(ToolStripGripRenderEventArgs e)
    {
        var bounds = e.GripBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var dark = new SolidBrush(ColorTable.GripDark);
        bool vertical = e.GripDisplayStyle == ToolStripGripDisplayStyle.Vertical;

        // Two-pixel dots every four pixels along the grip, inset two pixels from the ends.
        if (vertical)
        {
            int x = bounds.X + bounds.Width / 2 - 1;
            for (int y = bounds.Y + 2; y < bounds.Bottom - 3; y += 4)
            {
                e.Graphics.FillRectangle(dark, new Rectangle(x, y, 2, 2));
            }
        }
        else
        {
            int y = bounds.Y + bounds.Height / 2 - 1;
            for (int x = bounds.X + 2; x < bounds.Right - 3; x += 4)
            {
                e.Graphics.FillRectangle(dark, new Rectangle(x, y, 2, 2));
            }
        }
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        var bounds = e.AffectedBounds;
        if (bounds.Width <= 0) return;
        using var brush = new SolidBrush(ColorTable.ImageMarginGradientBegin);
        e.Graphics.FillRectangle(brush, bounds);
        using var pen = new Pen(ColorTable.SeparatorDark);
        e.Graphics.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
    }

    protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e)
    {
        var bounds = e.AffectedBounds;
        using var brush = new SolidBrush(Theme.SizingGrip);
        // Three diagonal rows of 2x2 dots in the bottom-right corner.
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3 - row; col++)
            {
                int x = bounds.Right - 4 - col * 4;
                int y = bounds.Bottom - 4 - row * 4;
                e.Graphics.FillRectangle(brush, new Rectangle(x, y, 2, 2));
            }
        }
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var bounds = new Rectangle(Point.Empty, item.Size);
        bool pressed = item.Pressed;
        bool selected = item.Selected;
        bool @checked = item is ToolStripButton { Checked: true };

        if (!pressed && !selected && !@checked) return;

        Color fill = pressed ? ColorTable.ButtonPressedHighlight
            : @checked && !selected ? ColorTable.ButtonCheckedHighlight
            : @checked ? ColorTable.ButtonPressedHighlight
            : ColorTable.ButtonSelectedHighlight;
        Color border = pressed ? ColorTable.ButtonPressedBorder
            : @checked ? ColorTable.ButtonCheckedHighlightBorder
            : ColorTable.ButtonSelectedBorder;

        using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, bounds);
        using (var pen = new Pen(border)) e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) => OnRenderButtonBackground(e);

    protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
    {
        OnRenderButtonBackground(e);
        if (e.Item is not ToolStripSplitButton split) return;
        if (!split.Selected && !split.Pressed) return;
        using var pen = new Pen(ColorTable.ButtonSelectedBorder);
        int x = split.DropDownButtonBounds.Left;
        e.Graphics.DrawLine(pen, x, 2, x, split.Height - 3);
    }

    protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e) => OnRenderButtonBackground(e);

    protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.IsBackColorSet)
        {
            using var brush = new SolidBrush(e.Item.BackColor);
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var bounds = new Rectangle(Point.Empty, item.Size);
        bool onDropDown = item.IsOnDropDown;
        bool open = item is ToolStripDropDownItem { DropDownVisible: true };

        if (!item.Selected && !item.Pressed && !open) return;
        if (!item.Enabled)
        {
            // Windows still highlights a disabled menu item that the keyboard lands on, but faintly.
            using var faint = new SolidBrush(Theme.StripItemHot);
            e.Graphics.FillRectangle(faint, bounds);
            return;
        }

        if (!onDropDown && open)
        {
            // An open top-level menu keeps the drop-down's colour so the two read as one surface.
            using var brush = new SolidBrush(ColorTable.ToolStripDropDownBackground);
            e.Graphics.FillRectangle(brush, bounds);
            using var pen = new Pen(ColorTable.MenuBorder);
            e.Graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);
            e.Graphics.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
            e.Graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
            return;
        }

        using (var brush = new SolidBrush(ColorTable.MenuItemSelected)) e.Graphics.FillRectangle(brush, bounds);
        using (var pen = new Pen(ColorTable.MenuItemBorder)) e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        var box = Rectangle.Inflate(r, 2, 2);
        using (var brush = new SolidBrush(ColorTable.CheckBackground)) e.Graphics.FillRectangle(brush, box);
        using (var pen = new Pen(Theme.MenuCheckBorder)) e.Graphics.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
        base.OnRenderItemCheck(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        using var pen = new Pen(ColorTable.SeparatorDark);
        if (e.Vertical)
        {
            int x = bounds.Left + bounds.Width / 2;
            e.Graphics.DrawLine(pen, x, bounds.Top + 3, x, bounds.Bottom - 4);
        }
        else
        {
            int y = bounds.Top + bounds.Height / 2;
            // On a drop-down the line starts after the image margin, as Windows draws it.
            int left = e.Item.Owner is ToolStripDropDownMenu menu ? menu.ImageMarginWidth + 2 : bounds.Left + 2;
            e.Graphics.DrawLine(pen, left, y, bounds.Right - 3, y);
        }
    }

    protected override void OnRenderToolStripStatusLabelBackground(ToolStripItemRenderEventArgs e)
    {
        OnRenderLabelBackground(e);
        if (e.Item is not ToolStripStatusLabel label || label.BorderSides == ToolStripStatusLabelBorderSides.None) return;
        var bounds = new Rectangle(Point.Empty, label.Size);
        using var pen = new Pen(ColorTable.SeparatorDark);
        if ((label.BorderSides & ToolStripStatusLabelBorderSides.Left) != 0) e.Graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);
        if ((label.BorderSides & ToolStripStatusLabelBorderSides.Top) != 0) e.Graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
        if ((label.BorderSides & ToolStripStatusLabelBorderSides.Right) != 0) e.Graphics.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
        if ((label.BorderSides & ToolStripStatusLabelBorderSides.Bottom) != 0) e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
    }
}

/// <summary>
/// The "system" render mode. WinForms draws through the visual-style engine here; we have one look,
/// so this is the professional renderer without the drop-down image margin - a documented difference.
/// </summary>
public class ToolStripSystemRenderer : ToolStripProfessionalRenderer
{
    public ToolStripSystemRenderer() { }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(ColorTable.ToolStripDropDownBackground);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }
}
