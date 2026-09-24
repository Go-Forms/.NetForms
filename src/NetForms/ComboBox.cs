using System.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms;

public enum ComboBoxStyle
{
    Simple = 0,
    DropDown = 1,
    DropDownList = 2,
}

/// <summary>
/// An editable (DropDown) or fixed (DropDownList) selection box whose list opens in a
/// non-activating popup window, so it can extend past the form like the Win32 one.
/// </summary>
[DefaultEvent("SelectedIndexChanged")]
[DefaultProperty("Items")]
public class ComboBox : ListControl
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

    private const int ButtonWidth = 17;

    private readonly ObjectCollection _items;
    private readonly TextBox _edit;
    private ComboBoxStyle _style = ComboBoxStyle.DropDown;
    private int _selectedIndex = -1;
    private int _dropDownHeight = 106;
    private int _maxDropDownItems = 8;
    private int _dropDownWidth;
    private bool _sorted;
    private bool _droppedDown;
    private bool _buttonHot;
    private bool _buttonPressed;
    private DropDownPopup? _popup;
    private FlatStyle _flatStyle = FlatStyle.Standard;
    private DrawMode _drawMode = DrawMode.Normal;
    private int _itemHeight;
    private bool _syncingText;
    private int _maxLength;

    public ComboBox()
    {
        _items = new ObjectCollection(this);
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        _edit = new TextBox { BorderStyle = BorderStyle.None, TabStop = false, Name = "comboEdit", MaxLength = 0 };
        _edit.TextChanged += EditTextChanged;
        _edit.KeyDown += EditKeyDown;
        _edit.GotFocus += (_, _) => Invalidate();
        _edit.LostFocus += (_, _) => Invalidate();
        Controls.Add(_edit);
        LayoutEdit();
    }

    protected override Size DefaultSize => new Size(121, PreferredHeight);

    [Category("Appearance")]
    [Description("The background color of the component.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : SystemColors.Window;
        set
        {
            base.BackColor = value;
            _edit.BackColor = value;
        }
    }

    // --- events --------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Occurs when the value of the SelectedIndex property changes.")]
    public event EventHandler? SelectedIndexChanged;

    [Category("Behavior")]
    [Description("Occurs when an item is chosen from the drop-down list and the drop-down list is closed.")]
    public event EventHandler? SelectionChangeCommitted;

    [Category("Behavior")]
    [Description("Occurs when the drop-down portion of the combo box is shown.")]
    public event EventHandler? DropDown;

    [Category("Behavior")]
    [Description("Indicates that the drop-down portion of the combo box has closed.")]
    public event EventHandler? DropDownClosed;

    [Category("Behavior")]
    [Description("Occurs when the value of the DropDownStyle property changes.")]
    public event EventHandler? DropDownStyleChanged;

    [Category("Behavior")]
    [Description("Occurs when the combo box text has changed.")]
    public event EventHandler? TextUpdate;

    [Category("Behavior")]
    [Description("Occurs whenever a particular item/area needs to be painted.")]
    public event DrawItemEventHandler? DrawItem;

    [Category("Behavior")]
    [Description("Occurs whenever a particular item's height needs to be calculated.")]
    public event MeasureItemEventHandler? MeasureItem;

    protected virtual void OnSelectionChangeCommitted(EventArgs e) => SelectionChangeCommitted?.Invoke(this, e);
    protected virtual void OnDropDown(EventArgs e) => DropDown?.Invoke(this, e);
    protected virtual void OnDropDownClosed(EventArgs e) => DropDownClosed?.Invoke(this, e);
    protected virtual void OnDropDownStyleChanged(EventArgs e) => DropDownStyleChanged?.Invoke(this, e);
    protected virtual void OnTextUpdate(EventArgs e) => TextUpdate?.Invoke(this, e);
    protected virtual void OnDrawItem(DrawItemEventArgs e) => DrawItem?.Invoke(this, e);
    protected virtual void OnMeasureItem(MeasureItemEventArgs e) => MeasureItem?.Invoke(this, e);

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        SelectedIndexChanged?.Invoke(this, e);
    }

    // --- properties ----------------------------------------------------------------

    [Category("Data")]
    [Description("The items in the combo box.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ObjectCollection Items => _items;

    [Category("Appearance")]
    [Description("Controls the appearance and functionality of the combo box.")]
    [DefaultValue(ComboBoxStyle.DropDown)]
    public ComboBoxStyle DropDownStyle
    {
        get => _style;
        set
        {
            if (_style == value) return;
            _style = value;
            _edit.Visible = value == ComboBoxStyle.DropDown || value == ComboBoxStyle.Simple;
            OnDropDownStyleChanged(EventArgs.Empty);
            LayoutEdit();
            Invalidate();
        }
    }

    [Description("The index of the currently selected item of the combo box.")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= _items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            SyncTextFromSelection();
            Invalidate();
            OnSelectedIndexChanged(EventArgs.Empty);
        }
    }

    [Description("The currently selected item in the combo box, or null.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get => _selectedIndex >= 0 ? _items[_selectedIndex] : null;
        set => SelectedIndex = value == null ? -1 : _items.IndexOf(value);
    }

    [Category("Appearance")]
    [Description("The text associated with the control.")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => _style == ComboBoxStyle.DropDownList ? (SelectedItem != null ? GetItemText(SelectedItem) : string.Empty) : _edit.Text;
        set
        {
            value ??= string.Empty;
            int i = FindStringExact(value);
            if (i >= 0)
            {
                SelectedIndex = i;
            }
            else if (_style != ComboBoxStyle.DropDownList)
            {
                _syncingText = true;
                _edit.Text = value;
                _syncingText = false;
                _selectedIndex = -1;
                Invalidate();
            }
            OnTextChanged(EventArgs.Empty);
        }
    }

    [Description("The selected text in the edit component of the combo box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedText
    {
        get => _style == ComboBoxStyle.DropDownList ? string.Empty : _edit.SelectedText;
        set { if (_style != ComboBoxStyle.DropDownList) _edit.SelectedText = value; }
    }

    [Description("The index of the first character in the selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionStart
    {
        get => _edit.SelectionStart;
        set => _edit.SelectionStart = value;
    }

    [Description("The length of the selected text in the edit component of the combo box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionLength
    {
        get => _edit.SelectionLength;
        set => _edit.SelectionLength = value;
    }

    public void SelectAll() => _edit.SelectAll();

    public void Select(int start, int length) => _edit.Select(start, length);

    [Description("Indicates if the combo box is currently dropped down.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool DroppedDown
    {
        get => _droppedDown;
        set
        {
            if (value) ShowDropDown();
            else CloseDropDown(commit: false);
        }
    }

    [Category("Behavior")]
    [Description("The height, in pixels, of the drop-down box in a combo box.")]
    [DefaultValue(106)]
    public int DropDownHeight
    {
        get => _dropDownHeight;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            _dropDownHeight = value;
        }
    }

    [Category("Behavior")]
    [Description("The width, in pixels, of the drop-down box in a combo box.")]
    public int DropDownWidth
    {
        get => _dropDownWidth > 0 ? _dropDownWidth : Width;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            _dropDownWidth = value;
        }
    }

    internal bool ShouldSerializeDropDownWidth() => _dropDownWidth > 0;

    [Category("Behavior")]
    [Description("The maximum number of entries to display in the drop-down list.")]
    [DefaultValue(8)]
    [Localizable(true)]
    public int MaxDropDownItems
    {
        get => _maxDropDownItems;
        set
        {
            if (value < 1 || value > 100) throw new ArgumentOutOfRangeException(nameof(value));
            _maxDropDownItems = value;
        }
    }

    [Category("Behavior")]
    [Description("Specifies the maximum number of characters that can be entered into the combo box.")]
    [DefaultValue(0)]
    [Localizable(true)]
    public int MaxLength
    {
        // A combo box starts unlimited (0), unlike a text box, which starts at 32767; the inner
        // edit takes 0 to mean "no limit" too, so the two agree.
        get => _maxLength;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _maxLength = value;
            _edit.MaxLength = value;
        }
    }

    [Category("Behavior")]
    [Description("Specifies whether items in the list portion of the combo box are sorted.")]
    [DefaultValue(false)]
    public bool Sorted
    {
        get => _sorted;
        set
        {
            if (_sorted == value) return;
            _sorted = value;
            if (value) _items.SortInternal();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Determines the display of the control.")]
    [DefaultValue(FlatStyle.Standard)]
    [Localizable(true)]
    public FlatStyle FlatStyle
    {
        get => _flatStyle;
        set { _flatStyle = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Indicates whether the code or the operating system will handle drawing of elements in the list.")]
    [DefaultValue(DrawMode.Normal)]
    public DrawMode DrawMode
    {
        get => _drawMode;
        set { _drawMode = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("The height, in pixels, of items in an owner-draw combo box.")]
    [Localizable(true)]
    public int ItemHeight
    {
        get => _itemHeight > 0 ? _itemHeight : Font.Height;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            _itemHeight = value;
        }
    }

    /// <summary>Only an owner-drawn list keeps an item height of its own; otherwise it follows the font.</summary>
    internal bool ShouldSerializeItemHeight() => _itemHeight > 0 && DrawMode != DrawMode.Normal;

    [Description("The preferred height of this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PreferredHeight => Font.Height + 7;

    [Category("Behavior")]
    [Description("Indicates whether the combo box should resize to avoid showing partial items.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public bool IntegralHeight { get; set; } = true;

    [Description("Indicates the text completion behavior of the combo box.")]
    [DefaultValue(AutoCompleteMode.None)]
    public AutoCompleteMode AutoCompleteMode { get; set; }

    [Description("The source of complete strings used for automatic completion.")]
    [DefaultValue(AutoCompleteSource.None)]
    public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

    private AutoCompleteStringCollection? _autoCompleteCustomSource;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    [Localizable(true)]
    [Description("The autocomplete custom source, which is a custom StringCollection used when the AutoCompleteSource is CustomSource.")]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public AutoCompleteStringCollection AutoCompleteCustomSource
    {
        get => _autoCompleteCustomSource ??= new AutoCompleteStringCollection();
        set => _autoCompleteCustomSource = value;
    }

    public void BeginUpdate() { }

    public void EndUpdate() => Invalidate();

    // --- items ---------------------------------------------------------------------

    internal void ItemsChanged()
    {
        if (_selectedIndex >= _items.Count)
        {
            _selectedIndex = -1;
            SyncTextFromSelection();
        }
        Invalidate();
    }

    protected override void SetItemsCore(IList items)
    {
        _items.SetFromDataSource(items);
        SelectedIndex = _items.Count > 0 ? 0 : -1;
    }

    protected override object? GetItemCore(int index) => _items[index];

    protected override int GetItemCountCore() => _items.Count;

    protected override void RefreshItems() => Invalidate();

    public int FindString(string s) => FindString(s, -1);

    public int FindString(string s, int startIndex)
    {
        if (string.IsNullOrEmpty(s)) return -1;
        for (int n = 0; n < _items.Count; n++)
        {
            int i = (startIndex + 1 + n) % _items.Count;
            if (GetItemText(_items[i]).StartsWith(s, StringComparison.CurrentCultureIgnoreCase)) return i;
        }
        return -1;
    }

    public int FindStringExact(string s) => FindStringExact(s, -1);

    public int FindStringExact(string s, int startIndex)
    {
        if (s == null) return -1;
        for (int n = 0; n < _items.Count; n++)
        {
            int i = (startIndex + 1 + n) % _items.Count;
            if (string.Equals(GetItemText(_items[i]), s, StringComparison.CurrentCultureIgnoreCase)) return i;
        }
        return -1;
    }

    private void SyncTextFromSelection()
    {
        _syncingText = true;
        _edit.Text = SelectedItem != null ? GetItemText(SelectedItem) : string.Empty;
        _edit.SelectAll();
        _syncingText = false;
        OnTextChanged(EventArgs.Empty);
    }

    private void EditTextChanged(object? sender, EventArgs e)
    {
        if (_syncingText) return;
        // Typing de-selects unless it matches an item exactly.
        int i = FindStringExact(_edit.Text);
        if (i != _selectedIndex)
        {
            _selectedIndex = i;
            Invalidate();
            OnSelectedIndexChanged(EventArgs.Empty);
        }
        OnTextUpdate(EventArgs.Empty);
        OnTextChanged(EventArgs.Empty);
    }

    private void EditKeyDown(object? sender, KeyEventArgs e) => HandleNavigationKey(e);

    // --- geometry ------------------------------------------------------------------

    private Rectangle ButtonRectangle => new Rectangle(Width - 1 - ButtonWidth, 1, ButtonWidth, Math.Max(0, Height - 2));

    private Rectangle TextRectangle => new Rectangle(1, 1, Math.Max(0, Width - 2 - ButtonWidth), Math.Max(0, Height - 2));

    private void LayoutEdit()
    {
        var r = TextRectangle;
        // Vertically centre the single-line editor in the box.
        int h = _edit.PreferredHeight;
        _edit.Bounds = new Rectangle(r.X + 2, r.Y + Math.Max(0, (r.Height - h) / 2), Math.Max(0, r.Width - 3), h);
        _edit.Visible = _style != ComboBoxStyle.DropDownList;
    }

    protected override void OnResize(EventArgs e)
    {
        LayoutEdit();
        base.OnResize(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _edit.Font = Font;
        if (Height != PreferredHeight && _style != ComboBoxStyle.Simple) Height = PreferredHeight;
        LayoutEdit();
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if (_style != ComboBoxStyle.Simple) height = PreferredHeight;
        base.SetBoundsCore(x, y, width, height, specified);
    }

    // --- drop-down -----------------------------------------------------------------

    private void ShowDropDown()
    {
        if (_droppedDown) return;
        var form = FindForm();
        if (form == null || !form.IsWindowCreated) return;
        OnDropDown(EventArgs.Empty);
        _popup ??= new DropDownPopup(this);
        _popup.Populate();
        int rows = Math.Max(1, Math.Min(_items.Count, _maxDropDownItems));
        int height = IntegralHeight ? rows * ItemHeight + 2 : Math.Min(_dropDownHeight, rows * ItemHeight + 2);
        var screen = PointToScreen(new Point(0, Height));
        _droppedDown = true;
        _popup.ShowAt(this, screen, new Size(DropDownWidth, height));
        Invalidate();
    }

    private void CloseDropDown(bool commit)
    {
        if (!_droppedDown) return;
        _droppedDown = false;
        _popup?.Dismiss();
        Invalidate();
        OnDropDownClosed(EventArgs.Empty);
    }

    internal void PopupDismissed()
    {
        if (!_droppedDown) return;
        _droppedDown = false;
        Invalidate();
        OnDropDownClosed(EventArgs.Empty);
    }

    internal void PopupCommitted(int index)
    {
        bool changed = index != _selectedIndex;
        if (index >= 0) SelectedIndex = index;
        CloseDropDown(commit: true);
        if (changed) OnSelectionChangeCommitted(EventArgs.Empty);
        if (CanFocus) Focus();
    }

    // --- input ---------------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (CanFocus) Focus();
            bool onButton = ButtonRectangle.Contains(e.Location) || _style == ComboBoxStyle.DropDownList;
            if (onButton)
            {
                _buttonPressed = true;
                if (_droppedDown) CloseDropDown(commit: false);
                else ShowDropDown();
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _buttonPressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool hot = ButtonRectangle.Contains(e.Location);
        if (hot != _buttonHot)
        {
            _buttonHot = hot;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _buttonHot = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!_droppedDown && _items.Count > 0)
        {
            int delta = e.Delta > 0 ? -1 : 1;
            int next = Math.Clamp(_selectedIndex + delta, 0, _items.Count - 1);
            if (next != _selectedIndex)
            {
                SelectedIndex = next;
                OnSelectionChangeCommitted(EventArgs.Empty);
            }
        }
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End
        || ((keyData & Keys.KeyCode) is Keys.Return or Keys.Escape && _droppedDown)
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        HandleNavigationKey(e);
        base.OnKeyDown(e);
    }

    private void HandleNavigationKey(KeyEventArgs e)
    {
        if (e.Handled) return;
        if (_droppedDown && _popup != null)
        {
            switch (e.KeyCode)
            {
                case Keys.Return:
                    PopupCommitted(_popup.HighlightedIndex);
                    e.Handled = true;
                    return;
                case Keys.Escape:
                    CloseDropDown(commit: false);
                    e.Handled = true;
                    return;
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    _popup.Navigate(e.KeyCode);
                    e.Handled = true;
                    return;
            }
            return;
        }

        if (_items.Count == 0) return;
        int target = _selectedIndex;
        switch (e.KeyCode)
        {
            case Keys.Up: target = Math.Max(0, _selectedIndex - 1); break;
            case Keys.Down when e.Alt: ShowDropDown(); e.Handled = true; return;
            case Keys.Down: target = Math.Min(_items.Count - 1, _selectedIndex + 1); break;
            case Keys.PageUp: target = Math.Max(0, _selectedIndex - _maxDropDownItems + 1); break;
            case Keys.PageDown: target = Math.Min(_items.Count - 1, _selectedIndex + _maxDropDownItems - 1); break;
            case Keys.Home when _style == ComboBoxStyle.DropDownList: target = 0; break;
            case Keys.End when _style == ComboBoxStyle.DropDownList: target = _items.Count - 1; break;
            case Keys.F4: ShowDropDown(); e.Handled = true; return;
            default: return;
        }
        e.Handled = true;
        if (target != _selectedIndex)
        {
            SelectedIndex = target;
            OnSelectionChangeCommitted(EventArgs.Empty);
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        // Type-ahead in a DropDownList: jump to the first item starting with the character.
        if (_style == ComboBoxStyle.DropDownList && !char.IsControl(e.KeyChar))
        {
            int i = FindString(e.KeyChar.ToString(), _selectedIndex);
            if (i >= 0)
            {
                SelectedIndex = i;
                OnSelectionChangeCommitted(EventArgs.Empty);
            }
            e.Handled = true;
        }
        base.OnKeyPress(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        if (_edit.Visible && _edit.CanFocus)
        {
            _edit.Focus();
            _edit.SelectAll();
        }
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _edit.Enabled = Enabled;
        base.OnEnabledChanged(e);
    }

    // --- painting ------------------------------------------------------------------

    private bool HasFocusWithin => Focused || _edit.Focused;

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(Enabled ? BackColor : SystemColors.Control);
        pevent.Graphics.FillRectangle(brush, pevent.ClipRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        bool enabled = Enabled;
        bool focusWithin = HasFocusWithin;

        if (_style == ComboBoxStyle.DropDownList)
        {
            // The whole box acts as a button.
            var face = !enabled ? Theme.ButtonFaceDisabled : _droppedDown || _buttonPressed ? Theme.ButtonFacePressed : MouseIsOverBox ? Theme.ButtonFaceHot : Theme.ButtonFace;
            using var faceBrush = new SolidBrush(face);
            g.FillRectangle(faceBrush, rect);
            var textRect = TextRectangle;
            textRect.Inflate(-3, 0);
            var color = enabled ? ForeColor : Theme.DisabledText;
            if (_drawMode == DrawMode.Normal || _selectedIndex < 0)
            {
                TextRenderer.DrawText(g, Text, Font, textRect, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            else
            {
                OnDrawItem(new DrawItemEventArgs(g, Font, textRect, _selectedIndex, DrawItemState.ComboBoxEdit | (focusWithin ? DrawItemState.Focus : DrawItemState.None), color, face));
            }
            if (focusWithin && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(g, new Rectangle(textRect.X - 1, textRect.Y + 2, textRect.Width + 2, textRect.Height - 4), ForeColor, face);
            }
        }

        PaintButton(g, ButtonRectangle, enabled);

        var border = !enabled ? Theme.ButtonBorderDisabled : focusWithin || _droppedDown ? Theme.WindowBorderFocused : MouseIsOverBox ? Theme.CheckBorder : Theme.WindowBorder;
        using (var pen = new Pen(border)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        base.OnPaint(e);
    }

    private bool MouseIsOverBox => _buttonHot || ClientRectangle.Contains(PointToClient(Control.MousePosition));

    private void PaintButton(Graphics g, Rectangle button, bool enabled)
    {
        if (_style != ComboBoxStyle.DropDownList)
        {
            var face = !enabled ? Theme.ButtonFaceDisabled : _droppedDown || _buttonPressed ? Theme.ButtonFacePressed : _buttonHot ? Theme.ButtonFaceHot : BackColor;
            using var b = new SolidBrush(face);
            g.FillRectangle(b, button);
        }
        // Chevron.
        int cx = button.X + button.Width / 2, cy = button.Y + button.Height / 2;
        using var pen = new Pen(enabled ? Theme.ScrollArrow : Theme.ScrollThumbDisabled, 1.5f);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _popup?.Dispose();
            _popup = null;
        }
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + ", Items.Count: " + _items.Count;

    // --- popup ---------------------------------------------------------------------

    private sealed class DropDownPopup : PopupForm
    {
        private readonly ComboBox _owner;
        private readonly ListBox _list;

        public DropDownPopup(ComboBox owner)
        {
            _owner = owner;
            _list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, TabStop = false };
            _list.MouseMove += (_, e) =>
            {
                int i = _list.IndexFromPoint(e.Location);
                if (i >= 0 && i != _list.SelectedIndex) _list.SelectedIndex = i;
            };
            _list.MouseUp += (_, e) =>
            {
                int i = _list.IndexFromPoint(e.Location);
                if (i >= 0) _owner.PopupCommitted(i);
            };
            Controls.Add(_list);
            Dismissed += (_, _) => _owner.PopupDismissed();
        }

        public int HighlightedIndex => _list.SelectedIndex;

        public void Populate()
        {
            _list.Font = _owner.Font;
            _list.ItemHeight = _owner.ItemHeight;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var item in _owner._items) _list.Items.Add(item);
            _list.EndUpdate();
            _list.DisplayMember = _owner.DisplayMember;
            if (_owner._selectedIndex >= 0 && _owner._selectedIndex < _list.Items.Count) _list.SelectedIndex = _owner._selectedIndex;
            else _list.SelectedIndex = -1;
        }

        public void Navigate(Keys key)
        {
            int count = _list.Items.Count;
            if (count == 0) return;
            int current = _list.SelectedIndex;
            int target = key switch
            {
                Keys.Up => Math.Max(0, current - 1),
                Keys.Down => Math.Min(count - 1, current + 1),
                Keys.PageUp => Math.Max(0, current - _owner._maxDropDownItems + 1),
                Keys.PageDown => Math.Min(count - 1, current + _owner._maxDropDownItems - 1),
                Keys.Home => 0,
                Keys.End => count - 1,
                _ => current,
            };
            _list.SelectedIndex = Math.Max(0, target);
        }

        protected override bool IsClickInsideAnchor(Point ownerClientPoint)
        {
            // A click on the combo itself is handled by the combo (it toggles); anything else dismisses.
            var inCombo = _owner.PointFromForm(ownerClientPoint);
            return _owner.ClientRectangle.Contains(inCombo);
        }
    }

    // --- items collection ----------------------------------------------------------

    public class ObjectCollection : IList, IList<object>
    {
        private readonly ComboBox _owner;
        private readonly List<object> _items = new();

        public ObjectCollection(ComboBox owner) => _owner = owner;

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public object this[int index]
        {
            get => _items[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _items[index] = value;
                _owner.ItemsChanged();
            }
        }

        public int Add(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            int index;
            if (_owner._sorted)
            {
                index = 0;
                var text = _owner.GetItemText(item);
                while (index < _items.Count && string.Compare(_owner.GetItemText(_items[index]), text, StringComparison.CurrentCultureIgnoreCase) <= 0) index++;
                _items.Insert(index, item);
            }
            else
            {
                _items.Add(item);
                index = _items.Count - 1;
            }
            _owner.ItemsChanged();
            return index;
        }

        void ICollection<object>.Add(object item) => Add(item);

        public void AddRange(object[] items)
        {
            ArgumentNullException.ThrowIfNull(items);
            foreach (var i in items) Add(i);
        }

        public void AddRange(IEnumerable<object> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            foreach (var i in items) Add(i);
        }

        public void Insert(int index, object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (_owner._sorted) { Add(item); return; }
            _items.Insert(index, item);
            _owner.ItemsChanged();
        }

        public void Clear()
        {
            _items.Clear();
            _owner._selectedIndex = -1;
            _owner.SyncTextFromSelection();
            _owner.ItemsChanged();
        }

        public bool Contains(object value) => _items.Contains(value);
        public int IndexOf(object value) => _items.IndexOf(value);

        public bool Remove(object value)
        {
            int i = _items.IndexOf(value);
            if (i < 0) return false;
            RemoveAt(i);
            return true;
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            if (_owner._selectedIndex == index) { _owner._selectedIndex = -1; _owner.SyncTextFromSelection(); }
            else if (_owner._selectedIndex > index) _owner._selectedIndex--;
            _owner.ItemsChanged();
        }

        public void CopyTo(object[] destination, int arrayIndex) => _items.CopyTo(destination, arrayIndex);
        public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        internal void SortInternal() => _items.Sort((a, b) => string.Compare(_owner.GetItemText(a), _owner.GetItemText(b), StringComparison.CurrentCultureIgnoreCase));

        internal void SetFromDataSource(IList source)
        {
            _items.Clear();
            foreach (var o in source) if (o != null) _items.Add(o);
            _owner.ItemsChanged();
        }

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => this[index]; set => this[index] = value!; }
        int IList.Add(object? value) => Add(value!);
        bool IList.Contains(object? value) => value != null && Contains(value);
        int IList.IndexOf(object? value) => value == null ? -1 : IndexOf(value);
        void IList.Insert(int index, object? value) => Insert(index, value!);
        void IList.Remove(object? value) { if (value != null) Remove(value); }
        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

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
}
