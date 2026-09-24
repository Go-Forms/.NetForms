using System.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms;

public enum LinkBehavior
{
    SystemDefault = 0,
    AlwaysUnderline = 1,
    HoverUnderline = 2,
    NeverUnderline = 3,
}

public delegate void LinkLabelLinkClickedEventHandler(object? sender, LinkLabelLinkClickedEventArgs e);

public class LinkLabelLinkClickedEventArgs : EventArgs
{
    public LinkLabelLinkClickedEventArgs(LinkLabel.Link? link) : this(link, MouseButtons.Left) { }

    public LinkLabelLinkClickedEventArgs(LinkLabel.Link? link, MouseButtons button)
    {
        Link = link;
        Button = button;
    }

    public LinkLabel.Link? Link { get; }
    public MouseButtons Button { get; }
}

/// <summary>A label whose whole text (by default) or given ranges of it act as hyperlinks.</summary>
[DefaultEvent("LinkClicked")]
public class LinkLabel : Label
{
    private readonly LinkCollection _links;
    private Color _linkColor = Theme.LinkText;
    private Color _activeLinkColor = Theme.LinkActive;
    private Color _visitedLinkColor = Theme.LinkVisited;
    private Color _disabledLinkColor = Theme.DisabledText;
    private LinkBehavior _linkBehavior = LinkBehavior.SystemDefault;
    private Link? _hoverLink;
    private Link? _pressedLink;
    private Link? _wholeTextLink;

    public LinkLabel()
    {
        _links = new LinkCollection(this);
        SetStyle(ControlStyles.Selectable, true);
        // A link label with no text has nothing to tab to; WinForms turns TabStop on as soon as
        // there is a link to visit (see OnTextChanged / the LinkCollection).
        TabStop = false;
    }

    protected override Cursor DefaultCursor => Cursors.Hand;

    [Category("Action")]
    [Description("Occurs when the link is clicked.")]
    public event LinkLabelLinkClickedEventHandler? LinkClicked;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LinkCollection Links => _links;

    [Category("Appearance")]
    [Description("Determines the color of the hyperlink in its default state.")]
    public Color LinkColor { get => _linkColor; set { _linkColor = value; Invalidate(); } }

    [Category("Appearance")]
    [Description("Determines the color of the hyperlink when the user clicks the link.")]
    public Color ActiveLinkColor { get => _activeLinkColor; set { _activeLinkColor = value; Invalidate(); } }

    [Category("Appearance")]
    [Description("Determines the color of the hyperlink when the LinkVisited property is set to true.")]
    public Color VisitedLinkColor { get => _visitedLinkColor; set { _visitedLinkColor = value; Invalidate(); } }

    [Category("Appearance")]
    [Description("Determines the color of the hyperlink when disabled.")]
    public Color DisabledLinkColor { get => _disabledLinkColor; set { _disabledLinkColor = value; Invalidate(); } }

    internal bool ShouldSerializeLinkColor() => _linkColor != Theme.LinkText;

    internal bool ShouldSerializeActiveLinkColor() => _activeLinkColor != Theme.LinkActive;

    internal bool ShouldSerializeVisitedLinkColor() => _visitedLinkColor != Theme.LinkVisited;

    internal bool ShouldSerializeDisabledLinkColor() => _disabledLinkColor != Theme.DisabledText;

    [Category("Behavior")]
    [Description("Determines the underline behavior of the hyperlink.")]
    [DefaultValue(LinkBehavior.SystemDefault)]
    public LinkBehavior LinkBehavior { get => _linkBehavior; set { _linkBehavior = value; Invalidate(); } }

    [Category("Behavior")]
    [Description("Portion of the text in the label to render as a hyperlink.")]
    [Localizable(true)]
    public LinkArea LinkArea
    {
        get => _links.Count == 0 ? new LinkArea(0, Text.Length) : new LinkArea(_links[0].Start, _links[0].Length);
        set
        {
            _links.Clear();
            _links.Add(value.Start, value.Length);
        }
    }

    /// <summary>The implicit single link over the whole text is not written.</summary>
    internal bool ShouldSerializeLinkArea() =>
        _links.Count > 0 && !(_links.Count == 1 && _links[0].Start == 0 && _links[0].Length == Text.Length);

    [Category("Appearance")]
    [Description("Determines if the hyperlink should be rendered as visited.")]
    [DefaultValue(false)]
    public bool LinkVisited
    {
        get => _links.Count > 0 && _links[0].Visited;
        set
        {
            if (_links.Count == 0) _links.Add(0, Text.Length);
            _links[0].Visited = value;
        }
    }

    protected virtual void OnLinkClicked(LinkLabelLinkClickedEventArgs e) => LinkClicked?.Invoke(this, e);

    /// <summary>
    /// WinForms' UpdateSelectability: a link label is a tab stop exactly while there is something
    /// to visit, which is why a freshly constructed one reports TabStop false.
    /// </summary>
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        TabStop = Text.Length > 0;
    }

    /// <summary>The effective links: explicit ones, or the whole text when none were defined.</summary>
    private IEnumerable<Link> EffectiveLinks()
    {
        if (_links.Count > 0) return _links;
        if (_wholeTextLink == null || _wholeTextLink.Length != Text.Length) _wholeTextLink = new Link(0, Text.Length) { Owner = this };
        return new[] { _wholeTextLink };
    }

    private Link? LinkAt(Point p)
    {
        int index = CharIndexAt(p);
        if (index < 0) return null;
        foreach (var link in EffectiveLinks())
        {
            if (link.Enabled && index >= link.Start && index < link.Start + link.Length) return link;
        }
        return null;
    }

    /// <summary>Character index under a point, for the single-line case (links wrap with the text as a whole).</summary>
    private int CharIndexAt(Point p)
    {
        var text = Text;
        if (text.Length == 0) return -1;
        var flags = CreateTextFormatFlags() | TextFormatFlags.NoPadding;
        var pad = Padding;
        var rect = new Rectangle(pad.Left, pad.Top, Math.Max(0, Width - pad.Horizontal), Math.Max(0, Height - pad.Vertical));
        var size = TextRenderer.MeasureText(text, Font, rect.Size, flags);
        if (!new Rectangle(rect.Location, size).Contains(p)) return -1;
        // Walk the string for the widest prefix that ends before p.X.
        int lineHeight = Font.Height;
        int line = Math.Max(0, (p.Y - rect.Y) / Math.Max(1, lineHeight));
        var lines = TextLayout.Break(text, Font, (flags & TextFormatFlags.WordBreak) != 0 ? rect.Width : 0, (flags & TextFormatFlags.WordBreak) != 0);
        if (line >= lines.Count) return -1;
        int offset = 0;
        for (int i = 0; i < line; i++) offset += lines[i].Text.Length + 1;
        var lineText = lines[line].Text;
        for (int n = 1; n <= lineText.Length; n++)
        {
            if (TextLayout.MeasureWidth(Font, lineText.Substring(0, n)) >= p.X - rect.X) return offset + n - 1;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var link = LinkAt(e.Location);
        if (!ReferenceEquals(link, _hoverLink))
        {
            _hoverLink = link;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hoverLink != null)
        {
            _hoverLink = null;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressedLink = LinkAt(e.Location);
        if (_pressedLink != null && CanFocus) Focus();
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        var pressed = _pressedLink;
        _pressedLink = null;
        Invalidate();
        if (pressed != null && ReferenceEquals(LinkAt(e.Location), pressed))
        {
            OnLinkClicked(new LinkLabelLinkClickedEventArgs(pressed, e.Button));
        }
        base.OnMouseUp(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Return or Keys.Space)
        {
            foreach (var link in EffectiveLinks())
            {
                if (link.Enabled)
                {
                    OnLinkClicked(new LinkLabelLinkClickedEventArgs(link, MouseButtons.None));
                    e.Handled = true;
                    break;
                }
            }
        }
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Links are painted per range: a single range covering the whole text is the common case.
        var g = e.Graphics;
        var pad = Padding;
        var rect = new Rectangle(pad.Left, pad.Top, Math.Max(0, Width - pad.Horizontal), Math.Max(0, Height - pad.Vertical));
        var flags = CreateTextFormatFlags();
        var text = Text;
        var links = new List<Link>(EffectiveLinks());
        bool underline = _linkBehavior != LinkBehavior.NeverUnderline && (_linkBehavior != LinkBehavior.HoverUnderline || _hoverLink != null);

        if (links.Count == 1 && links[0].Start == 0 && links[0].Length >= text.Length)
        {
            var link = links[0];
            using var font = underline ? new Font(Font, Font.Style | FontStyle.Underline) : new Font(Font, Font.Style);
            TextRenderer.DrawText(g, text, font, rect, LinkColorFor(link), flags);
        }
        else
        {
            // Mixed text: draw plain text, then re-draw the link ranges in link colour on top (single-line layout).
            var color = Enabled ? ForeColor : Theme.DisabledText;
            TextRenderer.DrawText(g, text, Font, rect, color, flags);
            foreach (var link in links)
            {
                int start = Math.Clamp(link.Start, 0, text.Length);
                int end = Math.Clamp(link.Start + link.Length, 0, text.Length);
                if (end <= start) continue;
                float x0 = TextLayout.MeasureWidth(Font, text.Substring(0, start));
                float x1 = TextLayout.MeasureWidth(Font, text.Substring(0, end));
                var linkRect = new Rectangle(rect.X + (int)Math.Floor(x0) + PaddingLeft(), rect.Y, (int)Math.Ceiling(x1 - x0) + 1, rect.Height);
                using var bg = new SolidBrush(BackColor);
                g.FillRectangle(bg, linkRect.X, linkRect.Y, linkRect.Width, Math.Min(rect.Height, Font.Height));
                using var font = underline ? new Font(Font, Font.Style | FontStyle.Underline) : new Font(Font, Font.Style);
                TextRenderer.DrawText(g, text.Substring(start, end - start), font, linkRect, LinkColorFor(link), (flags & ~TextFormatFlags.HorizontalCenter & ~TextFormatFlags.Right) | TextFormatFlags.NoPadding | TextFormatFlags.NoClipping);
            }
        }

        if (Focused && ShowFocusCues)
        {
            var size = TextRenderer.MeasureText(text, Font, rect.Size, flags);
            ControlPaint.DrawFocusRectangle(g, new Rectangle(rect.X, rect.Y, Math.Min(rect.Width, size.Width), Math.Min(rect.Height, size.Height)), ForeColor, BackColor);
        }
    }

    private int PaddingLeft() => (int)Math.Ceiling(Font.SizeInPixels / 6f);

    private Color LinkColorFor(Link link)
    {
        if (!Enabled || !link.Enabled) return _disabledLinkColor;
        if (ReferenceEquals(link, _pressedLink)) return _activeLinkColor;
        if (link.Visited) return _visitedLinkColor;
        return _linkColor;
    }

    public class Link
    {
        public Link() { }

        public Link(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public Link(int start, int length, object? linkData) : this(start, length) => LinkData = linkData;

        internal LinkLabel? Owner { get; set; }

        public int Start { get; set; }
        public int Length { get; set; }
        public object? LinkData { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Visited { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public object? Tag { get; set; }
    }

    public class LinkCollection : IList<Link>, IList
    {
        private readonly LinkLabel _owner;
        private readonly List<Link> _items = new();

        public LinkCollection(LinkLabel owner) => _owner = owner;

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public Link this[int index]
        {
            get => _items[index];
            set
            {
                _items[index] = value;
                _owner.Invalidate();
            }
        }

        public Link Add(int start, int length) => Add(start, length, null);

        public Link Add(int start, int length, object? linkData)
        {
            var link = new Link(start, length, linkData) { Owner = _owner };
            Add(link);
            return link;
        }

        public int Add(Link value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Owner = _owner;
            _items.Add(value);
            _owner.Invalidate();
            return _items.Count - 1;
        }

        void ICollection<Link>.Add(Link item) => Add(item);

        public void Clear()
        {
            _items.Clear();
            _owner.Invalidate();
        }

        public bool Contains(Link link) => _items.Contains(link);
        public int IndexOf(Link link) => _items.IndexOf(link);
        public void Insert(int index, Link value) { _items.Insert(index, value); _owner.Invalidate(); }
        public bool Remove(Link value) { bool r = _items.Remove(value); _owner.Invalidate(); return r; }
        public void RemoveAt(int index) { _items.RemoveAt(index); _owner.Invalidate(); }
        public void CopyTo(Link[] array, int index) => _items.CopyTo(array, index);
        public IEnumerator<Link> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _items[index]; set => this[index] = (Link)value!; }
        int IList.Add(object? value) => Add((Link)value!);
        bool IList.Contains(object? value) => value is Link l && Contains(l);
        int IList.IndexOf(object? value) => value is Link l ? IndexOf(l) : -1;
        void IList.Insert(int index, object? value) => Insert(index, (Link)value!);
        void IList.Remove(object? value) { if (value is Link l) Remove(l); }
        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [DefaultValue(FlatStyle.Standard)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new FlatStyle FlatStyle { get => base.FlatStyle; set => base.FlatStyle = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }
}

public struct LinkArea : IEquatable<LinkArea>
{
    public LinkArea(int start, int length)
    {
        Start = start;
        Length = length;
    }

    public int Start { get; set; }
    public int Length { get; set; }
    public readonly bool IsEmpty => Length == 0 && Start == 0;

    public readonly bool Equals(LinkArea other) => Start == other.Start && Length == other.Length;
    public override readonly bool Equals(object? obj) => obj is LinkArea a && Equals(a);
    public override readonly int GetHashCode() => HashCode.Combine(Start, Length);
    public static bool operator ==(LinkArea a, LinkArea b) => a.Equals(b);
    public static bool operator !=(LinkArea a, LinkArea b) => !a.Equals(b);
    public override readonly string ToString() => $"{{Start={Start}, Length={Length}}}";
}
