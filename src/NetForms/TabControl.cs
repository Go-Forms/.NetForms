using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum TabAlignment
{
    Top = 0,
    Bottom = 1,
    Left = 2,
    Right = 3,
}

public enum TabAppearance
{
    Normal = 0,
    Buttons = 1,
    FlatButtons = 2,
}

public enum TabSizeMode
{
    Normal = 0,
    FillToRight = 1,
    Fixed = 2,
}

public enum TabControlAction
{
    Selecting = 0,
    Selected = 1,
    Deselecting = 2,
    Deselected = 3,
}

public delegate void TabControlEventHandler(object? sender, TabControlEventArgs e);
public delegate void TabControlCancelEventHandler(object? sender, TabControlCancelEventArgs e);

public class TabControlEventArgs : EventArgs
{
    public TabControlEventArgs(TabPage? tabPage, int tabPageIndex, TabControlAction action)
    {
        TabPage = tabPage;
        TabPageIndex = tabPageIndex;
        Action = action;
    }

    public TabPage? TabPage { get; }
    public int TabPageIndex { get; }
    public TabControlAction Action { get; }
}

public class TabControlCancelEventArgs : CancelEventArgs
{
    public TabControlCancelEventArgs(TabPage? tabPage, int tabPageIndex, bool cancel, TabControlAction action) : base(cancel)
    {
        TabPage = tabPage;
        TabPageIndex = tabPageIndex;
        Action = action;
    }

    public TabPage? TabPage { get; }
    public int TabPageIndex { get; }
    public TabControlAction Action { get; }
}

[DefaultEvent("Click")]
[DefaultProperty("Text")]
public class TabPage : Panel
{
    internal override bool ShouldSerializeLocation() => Location != Point.Empty;

    private string? _toolTipText;

    public TabPage() : this(string.Empty) { }

    public TabPage(string? text)
    {
        Text = text ?? string.Empty;
        SetStyle(ControlStyles.Selectable, false);
        Dock = DockStyle.None;
    }

    [Category("Layout")]
    [Description("Defines the edges of the container to which a certain control is bound. When a control is anchored to an edge, the distance between the control's closest edge and the specified edge will remain constant.")]
    [DefaultValue(AnchorStyles.Top | AnchorStyles.Left)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override AnchorStyles Anchor
    {
        get => base.Anchor;
        set { }
    }

    [Category("Layout")]
    [Description("Defines which borders of the control are bound to the container.")]
    [DefaultValue(DockStyle.None)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override DockStyle Dock
    {
        get => base.Dock;
        set { }
    }

    [Description("The text that is shown when the mouse hovers over this tab.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string ToolTipText
    {
        get => _toolTipText ?? string.Empty;
        set => _toolTipText = value;
    }

    [Description("Identifies the image displayed on the tab.")]
    [DefaultValue(-1)]
    [Localizable(true)]
    public int ImageIndex { get; set; } = -1;

    [Description("Identifies the image displayed on the tab.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string ImageKey { get; set; } = string.Empty;

    private bool _useVisualStyleBackColor;

    /// <summary>With visual styles a page is window-coloured (white), not control-coloured.</summary>
    [Category("Appearance")]
    [Description("Determines if the background is drawn using visual styles, if supported.")]
    [DefaultValue(false)]
    public bool UseVisualStyleBackColor
    {
        get => _useVisualStyleBackColor;
        set
        {
            _useVisualStyleBackColor = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("The background color of the component.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => !IsBackColorSet && _useVisualStyleBackColor ? SystemColors.Window : base.BackColor;
        set => base.BackColor = value;
    }

    public TabControl? TabControl => Parent as TabControl;

    public static TabPage? GetTabPageOfComponent(object? comp)
    {
        var c = comp as Control;
        while (c != null && c is not TabPage) c = c.Parent;
        return c as TabPage;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        TabControl?.Invalidate();
    }

    public override string ToString() => "TabPage: {" + Text + "}";

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(AutoSizeMode.GrowOnly)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override AutoSizeMode AutoSizeMode { get => base.AutoSizeMode; set => base.AutoSizeMode = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool Enabled { get => base.Enabled; set => base.Enabled = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new Point Location { get => base.Location; set => base.Location = value; }

    [Category("Layout")]
    [DefaultValue(typeof(Size), "0, 0")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Size MaximumSize { get => base.MaximumSize; set => base.MaximumSize = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Size MinimumSize { get => base.MinimumSize; set => base.MinimumSize = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new int TabIndex { get => base.TabIndex; set => base.TabIndex = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool Visible { get => base.Visible; set => base.Visible = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DockChanged
    {
        add => base.DockChanged += value;
        remove => base.DockChanged -= value;
    }

    [Category("Property Changed")]
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
    public new event EventHandler? LocationChanged
    {
        add => base.LocationChanged += value;
        remove => base.LocationChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TabIndexChanged
    {
        add => base.TabIndexChanged += value;
        remove => base.TabIndexChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? VisibleChanged
    {
        add => base.VisibleChanged += value;
        remove => base.VisibleChanged -= value;
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? AutoSizeChanged
    {
        add => base.AutoSizeChanged += value;
        remove => base.AutoSizeChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }
}

/// <summary>A strip of tabs over a stack of TabPages; only the selected page is visible.</summary>
[DefaultEvent("SelectedIndexChanged")]
[DefaultProperty("TabPages")]
public class TabControl : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageChanged
    {
        add => base.BackgroundImageChanged += value;
        remove => base.BackgroundImageChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }


    private readonly TabPageCollection _pages;
    private int _selectedIndex = -1;
    private TabAlignment _alignment = TabAlignment.Top;
    private TabAppearance _appearance = TabAppearance.Normal;
    private TabSizeMode _sizeMode = TabSizeMode.Normal;
    /// <summary>Empty until set: the tabs then size themselves to their text, as the native control does.</summary>
    private Size _itemSize = Size.Empty;
    private Point _padding = new Point(6, 3);
    private int _hotTab = -1;
    private bool _stripFocused;

    public TabControl()
    {
        _pages = new TabPageCollection(this);
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override Size DefaultSize => new Size(200, 100);

    [Category("Action")]
    [Description("Occurs after a tab page is selected as the topmost tab page.")]
    public event TabControlEventHandler? Selected;

    [Category("Action")]
    [Description("Occurs after a tab page is deselected as the topmost tab page.")]
    public event TabControlEventHandler? Deselected;

    [Category("Action")]
    [Description("Occurs when a tab page is being selected.")]
    public event TabControlCancelEventHandler? Selecting;

    [Category("Action")]
    [Description("Occurs when a tab page is being deselected.")]
    public event TabControlCancelEventHandler? Deselecting;

    [Category("Behavior")]
    [Description("Occurs when the value of the SelectedIndex property changes.")]
    public event EventHandler? SelectedIndexChanged;

    [Category("Behavior")]
    [Description("Occurs whenever a particular item/area needs to be painted.")]
    public event DrawItemEventHandler? DrawItem;

    [Category("Behavior")]
    [Description("The TabPages in the TabControl.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TabPageCollection TabPages => _pages;

    [Category("Appearance")]
    [Description("The number of tabs in the tab strip.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TabCount => _pages.Count;

    [Category("Behavior")]
    [Description("The index of the currently selected item.")]
    [DefaultValue(-1)]
    [Browsable(false)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1) throw new ArgumentOutOfRangeException(nameof(value));
            if (value >= _pages.Count) value = _pages.Count - 1;
            if (_selectedIndex == value) return;
            SelectCore(value, fromUser: false);
        }
    }

    [Category("Appearance")]
    [Description("The currently selected tab page.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TabPage? SelectedTab
    {
        get => _selectedIndex >= 0 && _selectedIndex < _pages.Count ? _pages[_selectedIndex] : null;
        set => SelectedIndex = value == null ? -1 : _pages.IndexOf(value);
    }

    [Category("Behavior")]
    [Description("Determines whether the tabs appear on the top, bottom, left, or right side of the Control (left or right are implicitly multilined).")]
    [DefaultValue(TabAlignment.Top)]
    [Localizable(true)]
    public TabAlignment Alignment
    {
        get => _alignment;
        set { _alignment = value; PerformLayout(); Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Indicates whether the tabs are painted as buttons or regular tabs.")]
    [DefaultValue(TabAppearance.Normal)]
    [Localizable(true)]
    public TabAppearance Appearance
    {
        get => _appearance;
        set { _appearance = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Indicates how tabs are sized.")]
    [DefaultValue(TabSizeMode.Normal)]
    public TabSizeMode SizeMode
    {
        get => _sizeMode;
        set { _sizeMode = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Determines the width of fixed-width or owner-draw tabs and the height of all tabs.")]
    [Localizable(true)]
    public Size ItemSize
    {
        // Not set, it reports the first tab's actual size (57x20 for "Controls" at Segoe UI 9pt), as WinForms does.
        get => !_itemSize.IsEmpty ? _itemSize : _pages.Count > 0 ? GetTabRect(0).Size : Size.Empty;
        set
        {
            if (value.Width < 0 || value.Height < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _itemSize = value;
            PerformLayout();
            Invalidate();
        }
    }

    internal bool ShouldSerializeItemSize() => !_itemSize.IsEmpty;

    public void ResetItemSize() => ItemSize = Size.Empty;

    [Category("Behavior")]
    [Description("Indicates how much extra space should be added around the text/image in the tab.")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new Point Padding
    {
        get => _padding;
        set { _padding = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Indicates if more than one row of tabs is allowed.")]
    [DefaultValue(false)]
    public bool Multiline { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the tabs visually change when the mouse passes over them.")]
    [DefaultValue(false)]
    public bool HotTrack { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether ToolTips should be shown for tabs that have their ToolTips set.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public bool ShowToolTips { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the user or the system paints the captions.")]
    [DefaultValue(TabDrawMode.Normal)]
    public TabDrawMode DrawMode { get; set; } = TabDrawMode.Normal;

    [Category("Appearance")]
    [Description("The number of rows currently being displayed in the tab strip.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int RowCount => 1;

    /// <summary>
    /// The native tab control's geometry, measured against WinForms (keys <c>tabs/*</c>): a tab is the
    /// font height + 4 tall (20 at Segoe UI 9pt) unless ItemSize says otherwise, the row starts 2px from
    /// the edge, and the page area begins 2px after it - (4, 24) for the default font, which is what the
    /// VS designer writes into <c>tabPage1.Location</c>.
    /// </summary>
    private int TabHeight => _itemSize.Height > 0 ? _itemSize.Height : Font.Height + 4;

    private int StripHeight => TabHeight + 2;

    private Color PageColor => SelectedTab?.BackColor ?? SystemColors.Window;

    /// <summary>The page area: 4px inside the page frame, as the Win32 tab control reports it (page at (4, 24) for the default font).</summary>
    [Description("Retrieves the display rectangle of this control.")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Rectangle DisplayRectangle
    {
        get
        {
            int strip = StripHeight;
            var r = ClientRectangle;
            return _alignment switch
            {
                TabAlignment.Bottom => new Rectangle(r.X + 4, r.Y + 4, Math.Max(0, r.Width - 8), Math.Max(0, r.Height - strip - 6)),
                _ => new Rectangle(r.X + 4, r.Y + strip + 2, Math.Max(0, r.Width - 8), Math.Max(0, r.Height - strip - 6)),
            };
        }
    }

    public Rectangle GetTabRect(int index)
    {
        if (index < 0 || index >= _pages.Count) throw new ArgumentOutOfRangeException(nameof(index));
        int x = 2;
        int h = TabHeight;
        int y = _alignment == TabAlignment.Bottom ? Height - h - 2 : 2;
        for (int i = 0; i < _pages.Count; i++)
        {
            int w = TabWidth(i);
            if (i == index) return new Rectangle(x, y, w, h);
            x += w;
        }
        return Rectangle.Empty;
    }

    private int TabWidth(int index)
    {
        if (_sizeMode == TabSizeMode.Fixed) return _itemSize.Width > 0 ? _itemSize.Width : 42;
        if (_sizeMode == TabSizeMode.FillToRight && _pages.Count > 0) return Math.Max(1, (Width - 4) / _pages.Count);
        // The text's own extent (no GDI padding) plus Padding.X on each side: 45 + 12 = 57 for "Controls".
        var size = TextRenderer.MeasureText(_pages[index].Text, Font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        return Math.Max(_itemSize.Width, size.Width + 2 * _padding.X);
    }

    private int TabAt(Point p)
    {
        for (int i = 0; i < _pages.Count; i++)
        {
            if (GetTabRect(i).Contains(p)) return i;
        }
        return -1;
    }

    protected override ControlCollection CreateControlsInstance() => new ControlCollection(this);

    internal void PageAdded(TabPage page)
    {
        page.Visible = false;
        LayoutPages();
        if (_selectedIndex < 0 && _pages.Count > 0) SelectCore(0, fromUser: false);
        Invalidate();
    }

    internal void PageRemoved(TabPage page, int index)
    {
        if (_pages.Count == 0)
        {
            _selectedIndex = -1;
        }
        else if (_selectedIndex >= _pages.Count)
        {
            SelectCore(_pages.Count - 1, fromUser: false);
        }
        else if (index <= _selectedIndex && _selectedIndex > 0 && index != _selectedIndex)
        {
            _selectedIndex--;
        }
        else if (index == _selectedIndex)
        {
            int keep = _selectedIndex;
            _selectedIndex = -1;
            SelectCore(Math.Min(keep, _pages.Count - 1), fromUser: false);
        }
        LayoutPages();
        Invalidate();
    }

    private void LayoutPages()
    {
        var display = DisplayRectangle;
        for (int i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            page.SetBoundsFromLayout(display);
            page.Visible = i == _selectedIndex;
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        LayoutPages();
        base.OnLayout(levent);
    }

    private void SelectCore(int index, bool fromUser)
    {
        var current = SelectedTab;
        if (current != null)
        {
            var deselecting = new TabControlCancelEventArgs(current, _selectedIndex, false, TabControlAction.Deselecting);
            OnDeselecting(deselecting);
            if (deselecting.Cancel) return;
        }
        var target = index >= 0 && index < _pages.Count ? _pages[index] : null;
        if (target != null)
        {
            var selecting = new TabControlCancelEventArgs(target, index, false, TabControlAction.Selecting);
            OnSelecting(selecting);
            if (selecting.Cancel) return;
        }

        int oldIndex = _selectedIndex;
        _selectedIndex = index;
        LayoutPages();
        Invalidate();
        if (current != null) OnDeselected(new TabControlEventArgs(current, oldIndex, TabControlAction.Deselected));
        if (target != null) OnSelected(new TabControlEventArgs(target, index, TabControlAction.Selected));
        OnSelectedIndexChanged(EventArgs.Empty);
    }

    public void SelectTab(int index) => SelectedIndex = index;

    public void SelectTab(TabPage tabPage) => SelectedTab = tabPage;

    public void SelectTab(string tabPageName)
    {
        var page = _pages[tabPageName];
        if (page != null) SelectedTab = page;
    }

    public void DeselectTab(int index)
    {
        if (index == _selectedIndex) SelectedIndex = (index + 1) % Math.Max(1, _pages.Count);
    }

    protected virtual void OnSelecting(TabControlCancelEventArgs e) => Selecting?.Invoke(this, e);
    protected virtual void OnSelected(TabControlEventArgs e) => Selected?.Invoke(this, e);
    protected virtual void OnDeselecting(TabControlCancelEventArgs e) => Deselecting?.Invoke(this, e);
    protected virtual void OnDeselected(TabControlEventArgs e) => Deselected?.Invoke(this, e);
    protected virtual void OnSelectedIndexChanged(EventArgs e) => SelectedIndexChanged?.Invoke(this, e);
    protected virtual void OnDrawItem(DrawItemEventArgs e) => DrawItem?.Invoke(this, e);

    // --- input ---------------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int tab = TabAt(e.Location);
            if (tab >= 0)
            {
                if (CanFocus) Focus();
                if (tab != _selectedIndex) SelectCore(tab, fromUser: true);
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int hot = TabAt(e.Location);
        if (hot != _hotTab)
        {
            _hotTab = hot;
            Invalidate(new Rectangle(0, _alignment == TabAlignment.Bottom ? Height - StripHeight : 0, Width, StripHeight));
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hotTab = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_pages.Count > 0)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: SelectCore(Math.Max(0, _selectedIndex - 1), true); e.Handled = true; break;
                case Keys.Right: SelectCore(Math.Min(_pages.Count - 1, _selectedIndex + 1), true); e.Handled = true; break;
                case Keys.Home: SelectCore(0, true); e.Handled = true; break;
                case Keys.End: SelectCore(_pages.Count - 1, true); e.Handled = true; break;
            }
        }
        base.OnKeyDown(e);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        // Ctrl+Tab / Ctrl+Shift+Tab cycle the pages from anywhere inside the control.
        if ((keyData & Keys.KeyCode) == Keys.Tab && (keyData & Keys.Control) != 0 && _pages.Count > 0)
        {
            int next = (keyData & Keys.Shift) != 0 ? (_selectedIndex - 1 + _pages.Count) % _pages.Count : (_selectedIndex + 1) % _pages.Count;
            SelectCore(next, true);
            return true;
        }
        return base.ProcessDialogKey(keyData);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        _stripFocused = true;
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _stripFocused = false;
        Invalidate();
        base.OnLostFocus(e);
    }

    // --- painting ------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int strip = StripHeight;
        bool bottom = _alignment == TabAlignment.Bottom;
        var pageArea = bottom
            ? new Rectangle(0, 0, Width, Math.Max(0, Height - strip))
            : new Rectangle(0, strip, Width, Math.Max(0, Height - strip));

        using (var pageBrush = new SolidBrush(PageColor)) g.FillRectangle(pageBrush, pageArea);
        using var border = new Pen(Theme.ScrollThumb);
        g.DrawRectangle(border, pageArea.X, pageArea.Y, pageArea.Width - 1, pageArea.Height - 1);

        for (int i = 0; i < _pages.Count; i++)
        {
            if (i == _selectedIndex) continue;
            PaintTab(g, i, false);
        }
        if (_selectedIndex >= 0 && _selectedIndex < _pages.Count) PaintTab(g, _selectedIndex, true);
        base.OnPaint(e);
    }

    private void PaintTab(Graphics g, int index, bool selected)
    {
        var rect = GetTabRect(index);
        bool bottom = _alignment == TabAlignment.Bottom;
        var page = _pages[index];

        if (DrawMode == TabDrawMode.OwnerDrawFixed)
        {
            var state = selected ? DrawItemState.Selected : DrawItemState.None;
            if (selected && _stripFocused) state |= DrawItemState.Focus;
            OnDrawItem(new DrawItemEventArgs(g, Font, rect, index, state, ForeColor, BackColor));
            return;
        }

        if (selected)
        {
            // The selected tab merges with the page: grows 2px and covers the page border.
            var r = bottom ? new Rectangle(rect.X - 2, rect.Y - 1, rect.Width + 4, rect.Height + 1) : new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 3);
            using var fill = new SolidBrush(PageColor);
            g.FillRectangle(fill, r);
            using var pen = new Pen(Theme.ScrollThumb);
            if (bottom)
            {
                g.DrawLine(pen, r.X, r.Y, r.X, r.Bottom - 1);
                g.DrawLine(pen, r.Right - 1, r.Y, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pen, r.X, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
            }
            else
            {
                g.DrawLine(pen, r.X, r.Y, r.X, r.Bottom - 1);
                g.DrawLine(pen, r.Right - 1, r.Y, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pen, r.X, r.Y, r.Right - 1, r.Y);
            }
            rect = r;
        }
        else
        {
            var inset = bottom ? new Rectangle(rect.X, rect.Y, rect.Width, rect.Height - 2) : new Rectangle(rect.X, rect.Y + 2, rect.Width, rect.Height - 2);
            var face = index == _hotTab ? Theme.ButtonFaceHot : Theme.ButtonFace;
            using var fill = new SolidBrush(face);
            g.FillRectangle(fill, inset);
            using var pen = new Pen(Theme.ButtonBorder);
            g.DrawRectangle(pen, inset.X, inset.Y, inset.Width - 1, inset.Height - 1);
        }

        var color = Enabled ? ForeColor : Theme.DisabledText;
        TextRenderer.DrawText(g, page.Text, Font, rect, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        if (selected && _stripFocused && ShowFocusCues)
        {
            var focus = rect;
            focus.Inflate(-3, -3);
            ControlPaint.DrawFocusRectangle(g, focus, ForeColor, PageColor);
        }
    }

    public override string ToString() => base.ToString() + ", TabPages.Count: " + _pages.Count;

    // --- collections ---------------------------------------------------------------

    public new class ControlCollection : Control.ControlCollection
    {
        private readonly TabControl _owner;

        public ControlCollection(TabControl owner) : base(owner) => _owner = owner;

        public override void Add(Control? value)
        {
            if (value is not TabPage page) throw new ArgumentException("Only TabPage controls can be added to a TabControl.", nameof(value));
            base.Add(page);
            _owner.PageAdded(page);
        }

        public override void Remove(Control? value)
        {
            int index = IndexOf(value);
            base.Remove(value);
            if (value is TabPage page && index >= 0) _owner.PageRemoved(page, index);
        }
    }

    public class TabPageCollection : IList, IList<TabPage>
    {
        private readonly TabControl _owner;

        public TabPageCollection(TabControl owner) => _owner = owner;

        private Control.ControlCollection Controls => _owner.Controls;

        public int Count => Controls.Count;
        public bool IsReadOnly => false;

        public virtual TabPage this[int index]
        {
            get => (TabPage)Controls[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var old = Controls[index];
                Controls.Remove(old);
                Controls.Add(value);
                Controls.SetChildIndex(value, index);
            }
        }

        public virtual TabPage? this[string? key] => Controls[key] as TabPage;

        public void Add(TabPage value) => Controls.Add(value);

        public void Add(string? text) => Add(new TabPage(text));

        public void Add(string? key, string? text) => Add(new TabPage(text) { Name = key ?? string.Empty });

        public void AddRange(TabPage[] pages)
        {
            ArgumentNullException.ThrowIfNull(pages);
            foreach (var p in pages) Add(p);
        }

        public bool Contains(TabPage page) => Controls.Contains(page);
        public virtual bool ContainsKey(string? key) => Controls.ContainsKey(key);
        public int IndexOf(TabPage page) => Controls.IndexOf(page);
        public virtual int IndexOfKey(string? key) => Controls.IndexOfKey(key);

        public void Insert(int index, TabPage tabPage)
        {
            Controls.Add(tabPage);
            Controls.SetChildIndex(tabPage, index);
        }

        public void Insert(int index, string? text) => Insert(index, new TabPage(text));

        public bool Remove(TabPage value)
        {
            bool had = Contains(value);
            Controls.Remove(value);
            return had;
        }

        public void RemoveAt(int index) => Controls.RemoveAt(index);
        public virtual void RemoveByKey(string? key) => Controls.RemoveByKey(key);
        public void Clear() => Controls.Clear();

        public void CopyTo(TabPage[] array, int index)
        {
            for (int i = 0; i < Count; i++) array[index + i] = this[i];
        }

        public IEnumerator<TabPage> GetEnumerator()
        {
            for (int i = 0; i < Count; i++) yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        TabPage IList<TabPage>.this[int index] { get => this[index]; set => this[index] = value; }
        void ICollection<TabPage>.Add(TabPage item) => Add(item);
        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => this[index]; set => this[index] = (TabPage)value!; }
        int IList.Add(object? value) { Add((TabPage)value!); return Count - 1; }
        bool IList.Contains(object? value) => value is TabPage p && Contains(p);
        int IList.IndexOf(object? value) => value is TabPage p ? IndexOf(p) : -1;
        void IList.Insert(int index, object? value) => Insert(index, (TabPage)value!);
        void IList.Remove(object? value) { if (value is TabPage p) Remove(p); }
        void ICollection.CopyTo(Array array, int index) { for (int i = 0; i < Count; i++) array.SetValue(this[i], index + i); }
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor { get => base.BackColor; set => base.BackColor = value; }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color ForeColor { get => base.ForeColor; set => base.ForeColor = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? BackColorChanged
    {
        add => base.BackColorChanged += value;
        remove => base.BackColorChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? ForeColorChanged
    {
        add => base.ForeColorChanged += value;
        remove => base.ForeColorChanged -= value;
    }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event PaintEventHandler? Paint
    {
        add => base.Paint += value;
        remove => base.Paint -= value;
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
