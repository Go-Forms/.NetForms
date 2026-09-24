using System.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms;

public class ToolStripItemCollection : IList, IList<ToolStripItem>
{
    private readonly ToolStrip _owner;
    private readonly List<ToolStripItem> _items = new();
    private readonly bool _readOnly;

    public ToolStripItemCollection(ToolStrip owner, ToolStripItem[]? value)
    {
        _owner = owner;
        if (value != null) AddRange(value);
    }

    /// <summary>
    /// A read-only view (DisplayedItems, as in WinForms): the items are listed, not adopted. Adding them
    /// the normal way would take each one out of the strip's real Items first.
    /// </summary>
    internal ToolStripItemCollection(ToolStrip owner, ToolStripItem[] value, bool isReadOnly)
    {
        _owner = owner;
        _items.AddRange(value);
        _readOnly = isReadOnly;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => _readOnly;

    private void CheckWritable()
    {
        if (_readOnly) throw new NotSupportedException("The collection is read-only.");
    }

    public virtual ToolStripItem this[int index] => _items[index];

    ToolStripItem IList<ToolStripItem>.this[int index]
    {
        get => _items[index];
        set => throw new NotSupportedException();
    }

    public virtual ToolStripItem? this[string? key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var i in _items)
            {
                if (string.Equals(i.Name, key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return null;
        }
    }

    public ToolStripItem Add(string? text) => Add(text, null, null);

    public ToolStripItem Add(Image? image) => Add(null, image, null);

    public ToolStripItem Add(string? text, Image? image) => Add(text, image, null);

    public ToolStripItem Add(string? text, Image? image, EventHandler? onClick)
    {
        var item = _owner.CreateDefaultItem(text, image, onClick);
        Add(item);
        return item;
    }

    public int Add(ToolStripItem value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Insert(_items.Count, value);
        return _items.Count - 1;
    }

    void ICollection<ToolStripItem>.Add(ToolStripItem item) => Add(item);

    public void AddRange(ToolStripItem[] toolStripItems)
    {
        ArgumentNullException.ThrowIfNull(toolStripItems);
        foreach (var i in toolStripItems) Add(i);
    }

    public void AddRange(ToolStripItemCollection toolStripItems)
    {
        ArgumentNullException.ThrowIfNull(toolStripItems);
        foreach (var i in toolStripItems.ToArray()) Add(i);
    }

    public void Insert(int index, ToolStripItem value)
    {
        CheckWritable();
        ArgumentNullException.ThrowIfNull(value);
        value.Owner?.Items.Remove(value);
        _items.Insert(Math.Clamp(index, 0, _items.Count), value);
        value.SetOwner(_owner);
        _owner.OnItemAdded(new ToolStripItemEventArgs(value));
        _owner.ItemsChanged();
    }

    public bool Remove(ToolStripItem value)
    {
        CheckWritable();
        if (value == null || !_items.Remove(value)) return false;
        value.SetOwner(null);
        _owner.OnItemRemoved(new ToolStripItemEventArgs(value));
        _owner.ItemsChanged();
        return true;
    }

    public void RemoveAt(int index) => Remove(_items[index]);

    public virtual void RemoveByKey(string? key)
    {
        var item = this[key];
        if (item != null) Remove(item);
    }

    public virtual void Clear()
    {
        foreach (var i in _items.ToArray()) Remove(i);
    }

    public bool Contains(ToolStripItem value) => _items.Contains(value);
    public virtual bool ContainsKey(string? key) => this[key] != null;
    public int IndexOf(ToolStripItem value) => _items.IndexOf(value);

    public virtual int IndexOfKey(string? key)
    {
        var item = this[key];
        return item == null ? -1 : IndexOf(item);
    }

    public ToolStripItem[] Find(string key, bool searchAllChildren)
    {
        var result = new List<ToolStripItem>();
        FindInto(this, key, searchAllChildren, result);
        return result.ToArray();
    }

    private static void FindInto(ToolStripItemCollection items, string key, bool deep, List<ToolStripItem> into)
    {
        foreach (var i in items._items)
        {
            if (string.Equals(i.Name, key, StringComparison.OrdinalIgnoreCase)) into.Add(i);
            if (deep && i is ToolStripDropDownItem d && d.HasDropDownItems) FindInto(d.DropDownItems, key, true, into);
        }
    }

    public void CopyTo(ToolStripItem[] array, int index) => _items.CopyTo(array, index);
    public ToolStripItem[] ToArray() => _items.ToArray();
    public IEnumerator<ToolStripItem> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _items[index]; set => throw new NotSupportedException(); }
    int IList.Add(object? value) => Add((ToolStripItem)value!);
    bool IList.Contains(object? value) => value is ToolStripItem i && Contains(i);
    int IList.IndexOf(object? value) => value is ToolStripItem i ? IndexOf(i) : -1;
    void IList.Insert(int index, object? value) => Insert(index, (ToolStripItem)value!);
    void IList.Remove(object? value) { if (value is ToolStripItem i) Remove(i); }
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
}

public class ToolStripButton : ToolStripItem
{
    private bool _checked;
    private CheckState _checkState = CheckState.Unchecked;

    public ToolStripButton() { }
    public ToolStripButton(string? text) : base(text, null, null) { }
    public ToolStripButton(Image? image) : base(null, image, null) { }
    public ToolStripButton(string? text, Image? image) : base(text, image, null) { }
    public ToolStripButton(string? text, Image? image, EventHandler? onClick) : base(text, image, onClick) { }
    public ToolStripButton(string? text, Image? image, EventHandler? onClick, string? name) : base(text, image, onClick, name) { }

    protected override bool DefaultAutoToolTip => true;

    [Description("Occurs whenever the Check property is changed.")]
    public event EventHandler? CheckedChanged;

    [Description("Occurs whenever the CheckState property is changed.")]
    public event EventHandler? CheckStateChanged;

    [Category("Behavior")]
    [Description("Indicates whether the item should toggle its selected state when clicked.")]
    [DefaultValue(false)]
    public bool CheckOnClick { get; set; }

    [Category("Appearance")]
    [Description("Indicates whether the ToolStripButton is pressed in or not pressed in.")]
    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    [Category("Appearance")]
    [Description("Indicates the state of the component.")]
    [DefaultValue(CheckState.Unchecked)]
    public CheckState CheckState
    {
        get => _checkState;
        set
        {
            if (_checkState == value) return;
            bool old = _checked;
            _checkState = value;
            _checked = value != CheckState.Unchecked;
            OnCheckStateChanged(EventArgs.Empty);
            if (old != _checked) OnCheckedChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool CanSelect => true;

    protected override Padding DefaultPadding => new Padding(2, 0, 2, 0);

    /// <summary>An image-only button is the default square (23x23), as WinForms sizes tool bar buttons.</summary>
    public override Size GetPreferredSize(Size constrainingSize)
    {
        var size = base.GetPreferredSize(constrainingSize);
        if (AutoSize && Text.Length == 0) size.Width = Math.Max(size.Width, DefaultSize.Width);
        return size;
    }

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);
    protected virtual void OnCheckStateChanged(EventArgs e) => CheckStateChanged?.Invoke(this, e);

    protected override void OnClick(EventArgs e)
    {
        if (CheckOnClick) Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Renderer.DrawButtonBackground(new ToolStripItemRenderEventArgs(e.Graphics, this));
        PaintImageAndText(e);
        base.OnPaint(e);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool AutoToolTip { get => base.AutoToolTip; set => base.AutoToolTip = value; }
}

public class ToolStripLabel : ToolStripItem
{
    public ToolStripLabel() { }
    public ToolStripLabel(string? text) : base(text, null, null) { }
    public ToolStripLabel(Image? image) : base(null, image, null) { }
    public ToolStripLabel(string? text, Image? image) : base(text, image, null) { }
    public ToolStripLabel(string? text, Image? image, bool isLink) : base(text, image, null) => IsLink = isLink;
    public ToolStripLabel(string? text, Image? image, bool isLink, EventHandler? onClick) : base(text, image, onClick) => IsLink = isLink;
    public ToolStripLabel(string? text, Image? image, bool isLink, EventHandler? onClick, string? name) : base(text, image, onClick, name) => IsLink = isLink;

    [Category("Behavior")]
    [Description("Specifies whether the label acts as a link.")]
    [DefaultValue(false)]
    public bool IsLink { get; set; }

    [Category("Appearance")]
    [Description("Specifies the color of the link.")]
    public Color LinkColor { get; set; } = Theme.LinkText;

    [Category("Appearance")]
    [Description("Specifies the color of the link when the link is active.")]
    public Color ActiveLinkColor { get; set; } = Theme.LinkActive;

    [Category("Appearance")]
    [Description("Specifies the color of the link when the link has been visited.")]
    public Color VisitedLinkColor { get; set; } = Theme.LinkVisited;

    /// <summary>dotnet/winforms' ToolStripLabelLayout sets borderSize = 0: a label is exactly its text.</summary>
    internal override int ContentBorderSize => 0;

    internal bool ShouldSerializeLinkColor() => LinkColor != Theme.LinkText;

    internal bool ShouldSerializeActiveLinkColor() => ActiveLinkColor != Theme.LinkActive;

    internal bool ShouldSerializeVisitedLinkColor() => VisitedLinkColor != Theme.LinkVisited;

    [Category("Appearance")]
    [Description("Determines if the hyperlink should be rendered as visited.")]
    [DefaultValue(false)]
    public bool LinkVisited { get; set; }

    [Category("Behavior")]
    [Description("The underlining behavior of the link.")]
    [DefaultValue(LinkBehavior.SystemDefault)]
    public LinkBehavior LinkBehavior { get; set; } = LinkBehavior.SystemDefault;

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool CanSelect => IsLink;

    [Category("Appearance")]
    [Description("The foreground color used to display text and graphics in the item.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color ForeColor
    {
        get
        {
            if (!IsForeColorSet && IsLink) return LinkVisited ? VisitedLinkColor : Pressed ? ActiveLinkColor : LinkColor;
            return base.ForeColor;
        }
        set => base.ForeColor = value;
    }

    private Font? _underlined;
    private Font? _underlinedFor;

    [Category("Appearance")]
    [Description("The font used to display text in the item.")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Font Font
    {
        get
        {
            var font = base.Font;
            if (!IsLink) return font;
            bool underline = LinkBehavior is LinkBehavior.AlwaysUnderline or LinkBehavior.SystemDefault
                || (LinkBehavior == LinkBehavior.HoverUnderline && Selected);
            if (!underline) return font;
            // Derived fonts are cached: this getter runs on every measure and paint.
            if (!ReferenceEquals(_underlinedFor, font))
            {
                _underlinedFor = font;
                _underlined = new Font(font, font.Style | FontStyle.Underline);
            }
            return _underlined!;
        }
        set => base.Font = value;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Renderer.DrawLabelBackground(new ToolStripItemRenderEventArgs(e.Graphics, this));
        PaintImageAndText(e);
        base.OnPaint(e);
    }
}

public class ToolStripSeparator : ToolStripItem
{
    public ToolStripSeparator() { }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool CanSelect => false;

    protected override Size DefaultSize => new Size(6, 6);

    protected internal override Padding DefaultMargin => Padding.Empty;

    /// <summary>Six pixels along the flow; the strip stretches it across (full height on a tool bar, full width in a menu).</summary>
    public override Size GetPreferredSize(Size constrainingSize) => new Size(6, 6);

    protected override void OnPaint(PaintEventArgs e)
    {
        bool vertical = !IsOnDropDown && Owner?.Orientation != Orientation.Vertical;
        Renderer.DrawSeparator(new ToolStripSeparatorRenderEventArgs(e.Graphics, this, vertical));
        base.OnPaint(e);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool AutoToolTip { get => base.AutoToolTip; set => base.AutoToolTip = value; }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override ToolStripItemDisplayStyle DisplayStyle { get => base.DisplayStyle; set => base.DisplayStyle = value; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool Enabled { get => base.Enabled; set => base.Enabled = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Font Font { get => base.Font; set => base.Font = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Image? Image { get => base.Image; set => base.Image = value; }

    [Category("Appearance")]
    [DefaultValue(typeof(ContentAlignment), "MiddleCenter")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ContentAlignment ImageAlign { get => base.ImageAlign; set => base.ImageAlign = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new int ImageIndex { get => base.ImageIndex; set => base.ImageIndex = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string ImageKey { get => base.ImageKey; set => base.ImageKey = value; }

    [Category("Appearance")]
    [DefaultValue(ToolStripItemImageScaling.SizeToFit)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ToolStripItemImageScaling ImageScaling { get => base.ImageScaling; set => base.ImageScaling = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Color ImageTransparentColor { get => base.ImageTransparentColor; set => base.ImageTransparentColor = value; }

    [Category("Appearance")]
    [DefaultValue("")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Appearance")]
    [DefaultValue(typeof(ContentAlignment), "MiddleCenter")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override ContentAlignment TextAlign { get => base.TextAlign; set => base.TextAlign = value; }

    [Category("Appearance")]
    [DefaultValue(TextImageRelation.ImageBeforeText)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new TextImageRelation TextImageRelation { get => base.TextImageRelation; set => base.TextImageRelation = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string? ToolTipText { get => base.ToolTipText; set => base.ToolTipText = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DisplayStyleChanged
    {
        add => base.DisplayStyleChanged += value;
        remove => base.DisplayStyleChanged -= value;
    }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? EnabledChanged
    {
        add => base.EnabledChanged += value;
        remove => base.EnabledChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }
}

public class ToolStripStatusLabel : ToolStripLabel
{
    public ToolStripStatusLabel() { }
    public ToolStripStatusLabel(string? text) : base(text) { }
    public ToolStripStatusLabel(Image? image) : base(image) { }
    public ToolStripStatusLabel(string? text, Image? image) : base(text, image) { }
    public ToolStripStatusLabel(string? text, Image? image, EventHandler? onClick) : base(text, image, false, onClick) { }
    public ToolStripStatusLabel(string? text, Image? image, EventHandler? onClick, string? name) : base(text, image, false, onClick, name) { }

    private bool _spring;

    /// <summary>Takes up the room the other items leave.</summary>
    [Category("Appearance")]
    [Description("Specifies whether the item fills up the remaining space.")]
    [DefaultValue(false)]
    public bool Spring
    {
        get => _spring;
        set
        {
            if (_spring == value) return;
            _spring = value;
            InvalidateLayout();
        }
    }

    [Category("Appearance")]
    [Description("Specifies the border style for the panel.")]
    [DefaultValue(Border3DStyle.Flat)]
    public Border3DStyle BorderStyle { get; set; } = Border3DStyle.Flat;

    [Category("Appearance")]
    [Description("Specifies the sides of the panel that should display borders.")]
    [DefaultValue(ToolStripStatusLabelBorderSides.None)]
    public ToolStripStatusLabelBorderSides BorderSides { get; set; } = ToolStripStatusLabelBorderSides.None;

    protected internal override Padding DefaultMargin => new Padding(0, 3, 0, 2);

    protected override void OnPaint(PaintEventArgs e)
    {
        Renderer.DrawToolStripStatusLabelBackground(new ToolStripItemRenderEventArgs(e.Graphics, this));
        PaintImageAndText(e);
        // Skip ToolStripLabel's painting: a status label draws its own borders through the renderer.
        RaisePaint(e);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(ToolStripItemAlignment.Left)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new ToolStripItemAlignment Alignment { get => base.Alignment; set => base.Alignment = value; }
}

public enum Border3DStyle
{
    Adjust = 0x2000,
    Bump = 9,
    Etched = 6,
    Flat = 0x400A,
    Raised = 5,
    RaisedInner = 4,
    RaisedOuter = 1,
    Sunken = 10,
    SunkenInner = 8,
    SunkenOuter = 2,
}

[Flags]
public enum ToolStripStatusLabelBorderSides
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
    All = 15,
}

/// <summary>Hosts an ordinary Control inside a strip (ToolStripComboBox, ToolStripTextBox, ToolStripProgressBar).</summary>
public class ToolStripControlHost : ToolStripItem
{
    public ToolStripControlHost(Control c) : this(c, null) { }

    public ToolStripControlHost(Control c, string? name)
    {
        Control = c ?? throw new ArgumentNullException(nameof(c));
        Name = name ?? string.Empty;
        Control.TabStop = false;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control Control { get; }

    [Category("Appearance")]
    [Description("The text to display on the item.")]
    [DefaultValue("")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => Control.Text;
        set => Control.Text = value;
    }

    [Category("Behavior")]
    [Description("Indicates whether the control is enabled.")]
    [DefaultValue(true)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool Enabled
    {
        get => Control.Enabled;
        set => Control.Enabled = value;
    }

    [Category("Appearance")]
    [Description("The font used to display text in the item.")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Font Font
    {
        get => Control.Font;
        set => Control.Font = value;
    }

    [Category("Appearance")]
    [Description("The foreground color used to display text and graphics in the item.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color ForeColor
    {
        get => Control.ForeColor;
        set => Control.ForeColor = value;
    }

    [Category("Appearance")]
    [Description("The background color used to display text and graphics in the control.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => Control.BackColor;
        set => Control.BackColor = value;
    }

    // The hosted control holds these values, so it decides whether they were set.
    internal override bool ShouldSerializeBackColor() => Control.ShouldSerializeBackColor();

    internal override bool ShouldSerializeForeColor() => Control.ShouldSerializeForeColor();

    internal override bool ShouldSerializeFont() => Control.ShouldSerializeFont();

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool CanSelect => Control.CanSelect;

    public override Size GetPreferredSize(Size constrainingSize)
    {
        if (!AutoSize) return ExplicitSize;
        var pref = Control.AutoSize ? Control.GetPreferredSize(constrainingSize) : Control.Size;
        return new Size(pref.Width + Padding.Horizontal, pref.Height + Padding.Vertical);
    }

    protected internal override void SetBounds(Rectangle bounds)
    {
        base.SetBounds(bounds);
        var content = ContentRectangle;
        var owner = Owner;
        if (owner != null)
        {
            if (Control.Parent != owner) owner.Controls.Add(Control);
            // The control gets the content height; one that keeps its own (a combo box, a single-line
            // text box) is centred instead - a 23px combo sits at y=1 in a 25px strip, as in WinForms.
            int x = bounds.X + content.X, y = bounds.Y + content.Y;
            int h = Control.AutoSize && Control is TextBoxBase ? Control.Height : content.Height;
            Control.SetBoundsFromLayout(new Rectangle(x, y + (content.Height - h) / 2, content.Width, h));
            if (Control.Height != h)
                Control.SetBoundsFromLayout(new Rectangle(x, y + (content.Height - Control.Height) / 2, content.Width, Control.Height));
        }
        Control.Visible = Available;
    }

    internal void Detach()
    {
        Control.Parent?.Controls.Remove(Control);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // The hosted control paints itself as a child of the strip.
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override ToolStripItemDisplayStyle DisplayStyle { get => base.DisplayStyle; set => base.DisplayStyle = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Image? Image { get => base.Image; set => base.Image = value; }

    [Category("Appearance")]
    [DefaultValue(typeof(ContentAlignment), "MiddleCenter")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ContentAlignment ImageAlign { get => base.ImageAlign; set => base.ImageAlign = value; }

    [Category("Appearance")]
    [DefaultValue(ToolStripItemImageScaling.SizeToFit)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ToolStripItemImageScaling ImageScaling { get => base.ImageScaling; set => base.ImageScaling = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Color ImageTransparentColor { get => base.ImageTransparentColor; set => base.ImageTransparentColor = value; }

    [Category("Appearance")]
    [DefaultValue(typeof(ContentAlignment), "MiddleCenter")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override ContentAlignment TextAlign { get => base.TextAlign; set => base.TextAlign = value; }

    [Category("Appearance")]
    [DefaultValue(TextImageRelation.ImageBeforeText)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new TextImageRelation TextImageRelation { get => base.TextImageRelation; set => base.TextImageRelation = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DisplayStyleChanged
    {
        add => base.DisplayStyleChanged += value;
        remove => base.DisplayStyleChanged -= value;
    }
}

[DefaultProperty("Items")]
public class ToolStripComboBox : ToolStripControlHost
{
    public ToolStripComboBox() : base(new ComboBox { Width = 121 }) { }
    public ToolStripComboBox(string? name) : this() => Name = name ?? string.Empty;

    /// <summary>dotnet/winforms: (1,0,1,0) on a strip, 2 all round on a drop-down.</summary>
    protected internal override Padding DefaultMargin => IsOnDropDown ? new Padding(2) : new Padding(1, 0, 1, 0);

    [Description("Displays an editable text box with a drop-down list of permitted values.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ComboBox ComboBox => (ComboBox)Control;

    [Category("Data")]
    [Description("The items in the combo box.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ComboBox.ObjectCollection Items => ComboBox.Items;

    [Description("The index of the currently selected item of the combo box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex { get => ComboBox.SelectedIndex; set => ComboBox.SelectedIndex = value; }

    [Description("The currently selected item in the combo box, or null.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem { get => ComboBox.SelectedItem; set => ComboBox.SelectedItem = value; }

    [Category("Appearance")]
    [Description("Controls the appearance and functionality of the combo box.")]
    [DefaultValue(ComboBoxStyle.DropDown)]
    public ComboBoxStyle DropDownStyle { get => ComboBox.DropDownStyle; set => ComboBox.DropDownStyle = value; }

    [Category("Behavior")]
    [Description("Occurs when the value of the SelectedIndex property changes.")]
    public event EventHandler? SelectedIndexChanged
    {
        add => ComboBox.SelectedIndexChanged += value;
        remove => ComboBox.SelectedIndexChanged -= value;
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DoubleClick
    {
        add => base.DoubleClick += value;
        remove => base.DoubleClick -= value;
    }
}

public class ToolStripTextBox : ToolStripControlHost
{
    public ToolStripTextBox() : base(new TextBox { Width = 100 }) { }
    public ToolStripTextBox(string? name) : this() => Name = name ?? string.Empty;

    /// <summary>dotnet/winforms: (1,0,1,0) on a strip, 2 all round on a drop-down.</summary>
    protected internal override Padding DefaultMargin => IsOnDropDown ? new Padding(2) : new Padding(1, 0, 1, 0);

    [Description("Enables the user to enter text, and provides multiline editing and password character masking.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextBox TextBox => (TextBox)Control;

    [Category("Behavior")]
    [Description("Controls whether the text in the edit control can be changed or not.")]
    [DefaultValue(false)]
    public bool ReadOnly { get => TextBox.ReadOnly; set => TextBox.ReadOnly = value; }

    [Category("Behavior")]
    [Description("Specifies the maximum number of characters that can be entered into the edit control.")]
    [DefaultValue(32767)]
    [Localizable(true)]
    public int MaxLength { get => TextBox.MaxLength; set => TextBox.MaxLength = value; }

    [Category("Appearance")]
    [Description("The currently selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedText { get => TextBox.SelectedText; set => TextBox.SelectedText = value; }
    public void SelectAll() => TextBox.SelectAll();
}

[DefaultProperty("Value")]
public class ToolStripProgressBar : ToolStripControlHost
{
    public ToolStripProgressBar() : base(new ProgressBar { Size = new Size(100, 16) }) { }
    public ToolStripProgressBar(string? name) : this() => Name = name ?? string.Empty;

    [Description("Displays a bar that fills to indicate to the user the progress of an operation.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ProgressBar ProgressBar => (ProgressBar)Control;

    [Category("Behavior")]
    [Description("The current value for the ProgressBar, in the range specified by the minimum and maximum properties.")]
    [DefaultValue(0)]
    public int Value { get => ProgressBar.Value; set => ProgressBar.Value = value; }

    [Category("Behavior")]
    [Description("The lower bound of the range this ProgressBar is working with.")]
    [DefaultValue(0)]
    public int Minimum { get => ProgressBar.Minimum; set => ProgressBar.Minimum = value; }

    [Category("Behavior")]
    [Description("The upper bound of the range this ProgressBar is working with.")]
    [DefaultValue(100)]
    public int Maximum { get => ProgressBar.Maximum; set => ProgressBar.Maximum = value; }

    [Category("Behavior")]
    [Description("The amount to increment the current value of the control by when the PerformStep() method is called.")]
    [DefaultValue(10)]
    public int Step { get => ProgressBar.Step; set => ProgressBar.Step = value; }

    [Category("Behavior")]
    [Description("This property allows the user to set the style of the ProgressBar.")]
    [DefaultValue(ProgressBarStyle.Blocks)]
    public ProgressBarStyle Style { get => ProgressBar.Style; set => ProgressBar.Style = value; }
    public void PerformStep() => ProgressBar.PerformStep();
    public void Increment(int value) => ProgressBar.Increment(value);

    protected internal override Padding DefaultMargin => new Padding(1, 2, 1, 1);

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [DefaultValue("")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? LocationChanged
    {
        add => base.LocationChanged += value;
        remove => base.LocationChanged -= value;
    }

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? OwnerChanged
    {
        add => base.OwnerChanged += value;
        remove => base.OwnerChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }
}
