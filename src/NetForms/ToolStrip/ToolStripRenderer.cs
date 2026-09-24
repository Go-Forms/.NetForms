using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace System.Windows.Forms;

public enum ToolStripGripStyle
{
    Hidden = 0,
    Visible = 1,
}

public enum ToolStripGripDisplayStyle
{
    Vertical = 0,
    Horizontal = 1,
}

public enum ToolStripLayoutStyle
{
    StackWithOverflow = 0,
    HorizontalStackWithOverflow = 1,
    VerticalStackWithOverflow = 2,
    Flow = 3,
    Table = 4,
}

public enum ToolStripRenderMode
{
    Custom = 0,
    System = 1,
    Professional = 2,
    ManagerRenderMode = 3,
}

public enum ToolStripTextDirection
{
    Inherit = 0,
    Horizontal = 1,
    Vertical90 = 2,
    Vertical270 = 3,
}

/// <summary>Direction of a navigation step or of a drawn arrow; the low bit is the orientation, as in WinForms.</summary>
public enum ArrowDirection
{
    Left = 0,
    Up = 1,
    Right = 16,
    Down = 17,
}

// --- render event args ---------------------------------------------------------------

public class ToolStripRenderEventArgs : EventArgs
{
    public ToolStripRenderEventArgs(Graphics g, ToolStrip toolStrip)
        : this(g, toolStrip, new Rectangle(Point.Empty, toolStrip?.Size ?? Size.Empty), Color.Empty) { }

    public ToolStripRenderEventArgs(Graphics g, ToolStrip toolStrip, Rectangle affectedBounds, Color backColor)
    {
        Graphics = g;
        ToolStrip = toolStrip;
        AffectedBounds = affectedBounds;
        BackColor = backColor;
    }

    public Graphics Graphics { get; }
    public ToolStrip ToolStrip { get; }
    public Rectangle AffectedBounds { get; }

    public Color BackColor { get; }

    /// <summary>The area a drop-down shares with the item that opened it; empty unless a menu is open.</summary>
    public Rectangle ConnectedArea { get; internal set; }
}

public class ToolStripItemRenderEventArgs : EventArgs
{
    public ToolStripItemRenderEventArgs(Graphics g, ToolStripItem item)
    {
        Graphics = g;
        Item = item;
    }

    public Graphics Graphics { get; }
    public ToolStripItem Item { get; }
    public ToolStrip? ToolStrip => Item.Owner;
}

public class ToolStripItemTextRenderEventArgs : ToolStripItemRenderEventArgs
{
    public ToolStripItemTextRenderEventArgs(Graphics g, ToolStripItem item, string? text, Rectangle textRectangle, Color textColor, Font textFont, ContentAlignment textAlign)
        : this(g, item, text, textRectangle, textColor, textFont, TranslateAlignment(textAlign))
    {
        TextAlign = textAlign;
    }

    public ToolStripItemTextRenderEventArgs(Graphics g, ToolStripItem item, string? text, Rectangle textRectangle, Color textColor, Font textFont, TextFormatFlags format)
        : base(g, item)
    {
        Text = text;
        TextRectangle = textRectangle;
        TextColor = textColor;
        TextFont = textFont;
        TextFormat = format;
        TextAlign = ContentAlignment.MiddleCenter;
    }

    public string? Text { get; set; }
    public Rectangle TextRectangle { get; set; }
    public Color TextColor { get; set; }
    public Font TextFont { get; set; }
    public TextFormatFlags TextFormat { get; set; }
    public ContentAlignment TextAlign { get; set; }
    public ToolStripTextDirection TextDirection { get; set; } = ToolStripTextDirection.Horizontal;

    internal static TextFormatFlags TranslateAlignment(ContentAlignment align)
    {
        var flags = TextFormatFlags.Default;
        flags |= align switch
        {
            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => TextFormatFlags.HorizontalCenter,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
            _ => TextFormatFlags.Left,
        };
        flags |= align switch
        {
            ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => TextFormatFlags.Bottom,
            _ => TextFormatFlags.Top,
        };
        return flags;
    }
}

public class ToolStripItemImageRenderEventArgs : ToolStripItemRenderEventArgs
{
    public ToolStripItemImageRenderEventArgs(Graphics g, ToolStripItem item, Rectangle imageRectangle)
        : this(g, item, item.Image, imageRectangle) { }

    public ToolStripItemImageRenderEventArgs(Graphics g, ToolStripItem item, Image? image, Rectangle imageRectangle)
        : base(g, item)
    {
        Image = image;
        ImageRectangle = imageRectangle;
    }

    public Image? Image { get; }
    public Rectangle ImageRectangle { get; }

    /// <summary>Set when the item is drawing its check mark rather than its own image.</summary>
    internal bool IsCheckMark { get; set; }
}

public class ToolStripArrowRenderEventArgs : EventArgs
{
    public ToolStripArrowRenderEventArgs(Graphics g, ToolStripItem toolStripItem, Rectangle arrowRectangle, Color arrowColor, ArrowDirection arrowDirection)
    {
        Graphics = g;
        Item = toolStripItem;
        ArrowRectangle = arrowRectangle;
        ArrowColor = arrowColor;
        Direction = arrowDirection;
    }

    public Graphics Graphics { get; }
    public ToolStripItem Item { get; }
    public Rectangle ArrowRectangle { get; set; }
    public Color ArrowColor { get; set; }
    public ArrowDirection Direction { get; set; }
}

public class ToolStripGripRenderEventArgs : ToolStripRenderEventArgs
{
    public ToolStripGripRenderEventArgs(Graphics g, ToolStrip toolStrip) : base(g, toolStrip) { }

    public Rectangle GripBounds => ToolStrip.GripRectangle;
    public ToolStripGripDisplayStyle GripDisplayStyle => ToolStrip.GripDisplayStyle;
    public ToolStripGripStyle GripStyle => ToolStrip.GripStyle;
}

public class ToolStripSeparatorRenderEventArgs : ToolStripItemRenderEventArgs
{
    public ToolStripSeparatorRenderEventArgs(Graphics g, ToolStripSeparator separator, bool vertical) : base(g, separator)
    {
        Vertical = vertical;
    }

    public bool Vertical { get; }
}

// --- the renderer --------------------------------------------------------------------

/// <summary>
/// Draws a ToolStrip and its items. Every item paints through the owning strip's renderer, so a
/// custom look is one subclass away - the same contract as WinForms. All item-level drawing is in
/// item coordinates: the strip translates the Graphics to the item's origin before calling.
/// </summary>
public abstract class ToolStripRenderer
{
    protected ToolStripRenderer() { }

    // --- strip -----------------------------------------------------------------------

    public void DrawToolStripBackground(ToolStripRenderEventArgs e) => OnRenderToolStripBackground(e);
    public void DrawToolStripBorder(ToolStripRenderEventArgs e) => OnRenderToolStripBorder(e);
    public void DrawGrip(ToolStripGripRenderEventArgs e) => OnRenderGrip(e);
    public void DrawImageMargin(ToolStripRenderEventArgs e) => OnRenderImageMargin(e);
    public void DrawStatusStripSizingGrip(ToolStripRenderEventArgs e) => OnRenderStatusStripSizingGrip(e);

    // --- item backgrounds -------------------------------------------------------------

    public void DrawButtonBackground(ToolStripItemRenderEventArgs e) => OnRenderButtonBackground(e);
    public void DrawDropDownButtonBackground(ToolStripItemRenderEventArgs e) => OnRenderDropDownButtonBackground(e);
    public void DrawSplitButton(ToolStripItemRenderEventArgs e) => OnRenderSplitButtonBackground(e);
    public void DrawLabelBackground(ToolStripItemRenderEventArgs e) => OnRenderLabelBackground(e);
    public void DrawMenuItemBackground(ToolStripItemRenderEventArgs e) => OnRenderMenuItemBackground(e);
    public void DrawOverflowButtonBackground(ToolStripItemRenderEventArgs e) => OnRenderOverflowButtonBackground(e);
    public void DrawItemBackground(ToolStripItemRenderEventArgs e) => OnRenderItemBackground(e);
    public void DrawToolStripStatusLabelBackground(ToolStripItemRenderEventArgs e) => OnRenderToolStripStatusLabelBackground(e);
    public void DrawSeparator(ToolStripSeparatorRenderEventArgs e) => OnRenderSeparator(e);

    // --- item content -----------------------------------------------------------------

    public void DrawItemText(ToolStripItemTextRenderEventArgs e) => OnRenderItemText(e);
    public void DrawItemImage(ToolStripItemImageRenderEventArgs e) => OnRenderItemImage(e);
    public void DrawItemCheck(ToolStripItemImageRenderEventArgs e) => OnRenderItemCheck(e);
    public void DrawArrow(ToolStripArrowRenderEventArgs e) => OnRenderArrow(e);

    // --- overridables ------------------------------------------------------------------

    protected virtual void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { }
    protected virtual void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    protected virtual void OnRenderGrip(ToolStripGripRenderEventArgs e) { }
    protected virtual void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
    protected virtual void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e) { }
    protected virtual void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) { }
    protected virtual void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) { }
    protected virtual void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e) { }
    protected virtual void OnRenderLabelBackground(ToolStripItemRenderEventArgs e) { }
    protected virtual void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) { }
    protected virtual void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e) { }
    protected virtual void OnRenderItemBackground(ToolStripItemRenderEventArgs e) { }
    protected virtual void OnRenderToolStripStatusLabelBackground(ToolStripItemRenderEventArgs e) => OnRenderLabelBackground(e);
    protected virtual void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { }

    /// <summary>Text drawing is shared by every renderer: the look lives in the colours the item passes in.</summary>
    protected virtual void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        var flags = e.TextFormat;
        if (e.Item.Owner is ToolStrip strip && !strip.ShowKeyboardCuesInternal) flags |= TextFormatFlags.HidePrefix;
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, e.TextColor, flags);
    }

    protected virtual void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        var image = e.Image;
        if (image == null || e.ImageRectangle.Width <= 0 || e.ImageRectangle.Height <= 0) return;
        if (!e.Item.Enabled)
        {
            using var disabled = CreateDisabledImage(image);
            e.Graphics.DrawImage(disabled, e.ImageRectangle);
            return;
        }
        e.Graphics.DrawImage(image, e.ImageRectangle);
    }

    protected virtual void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image != null)
        {
            OnRenderItemImage(e);
            return;
        }
        DrawCheckMark(e.Graphics, e.ImageRectangle, e.Item.Enabled ? Theme.CheckMark : Theme.DisabledText);
    }

    protected virtual void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var r = e.ArrowRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        var middle = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);

        // A 5x3 (or 3x5) solid triangle, the size Win32 draws menu and combo arrows at.
        Point[] points = e.Direction switch
        {
            ArrowDirection.Up => new[] { new Point(middle.X - 2, middle.Y + 1), new Point(middle.X + 2, middle.Y + 1), new Point(middle.X, middle.Y - 1) },
            ArrowDirection.Left => new[] { new Point(middle.X + 1, middle.Y - 2), new Point(middle.X + 1, middle.Y + 2), new Point(middle.X - 1, middle.Y) },
            ArrowDirection.Right => new[] { new Point(middle.X - 1, middle.Y - 2), new Point(middle.X - 1, middle.Y + 2), new Point(middle.X + 1, middle.Y) },
            _ => new[] { new Point(middle.X - 2, middle.Y - 1), new Point(middle.X + 2, middle.Y - 1), new Point(middle.X, middle.Y + 1) },
        };
        using var brush = new SolidBrush(e.ArrowColor);
        e.Graphics.FillPolygon(brush, points);
    }

    // --- helpers ------------------------------------------------------------------------

    /// <summary>The grey, half-transparent copy WinForms shows for a disabled item's image.</summary>
    public static Image CreateDisabledImage(Image normalImage)
    {
        ArgumentNullException.ThrowIfNull(normalImage);
        var source = new Bitmap(normalImage);
        var result = new Bitmap(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                int grey = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                grey = grey + (255 - grey) / 2;
                result.SetPixel(x, y, Color.FromArgb(c.A / 2, grey, grey, grey));
            }
        }
        source.Dispose();
        return result;
    }

    /// <summary>The two-stroke tick used for checked menu items and checked ToolStripButtons.</summary>
    private protected static void DrawCheckMark(Graphics g, Rectangle bounds, Color color)
    {
        if (bounds.Width < 6 || bounds.Height < 6) return;
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float x = bounds.X + bounds.Width * 0.22f;
        float y = bounds.Y + bounds.Height * 0.52f;
        g.DrawLines(pen, new[]
        {
            new PointF(x, y),
            new PointF(bounds.X + bounds.Width * 0.42f, bounds.Y + bounds.Height * 0.74f),
            new PointF(bounds.X + bounds.Width * 0.79f, bounds.Y + bounds.Height * 0.28f),
        });
        g.SmoothingMode = old;
    }
}
