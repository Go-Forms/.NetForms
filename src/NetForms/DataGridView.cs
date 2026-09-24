using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>
/// The grid. Columns own the width policy, rows own the cells, and the control owns the viewport:
/// headers are fixed, the cell area scrolls under them in pixels. Values live in the cells for an
/// unbound grid and in the bound objects otherwise, so binding never duplicates the data.
/// </summary>
[DefaultEvent(nameof(CellContentClick))]
public class DataGridView : Control, ISupportInitialize
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

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? StyleChanged
    {
        add => base.StyleChanged += value;
        remove => base.StyleChanged -= value;
    }

    private readonly DataGridViewColumnCollection _columns;
    private readonly DataGridViewRowCollection _rows;
    private readonly HashSet<(int Column, int Row)> _selection = new();
    private readonly ScrollBarCore _vscroll;
    private readonly ScrollBarCore _hscroll;

    private Point _scroll;
    private Size _content;
    private bool _vVisible;
    private bool _hVisible;
    private bool _layoutDirty = true;

    private object? _dataSource;
    private string _dataMember = string.Empty;
    private IList? _boundList;
    private PropertyDescriptorCollection? _boundProperties;
    private bool _inBindingUpdate;

    private DataGridViewCell? _currentCell;
    private (int Column, int Row) _anchor = (-1, -1);
    private Control? _editor;
    private DataGridViewCell? _editingCell;
    private int _pressedColumnHeader = -1;
    private int _hotColumnHeader = -1;
    private DataGridViewColumn? _sortedColumn;
    private SortOrder _sortOrder = SortOrder.None;
    private int _rowHeadersWidth = 41;
    private int _columnHeadersHeight = 23;
    private int _cachedColumnHeadersHeight = 23;
    private DataGridViewColumnHeadersHeightSizeMode _columnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
    private ScrollBars _scrollBars = ScrollBars.Both;

    public DataGridView()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.StandardClick | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        _columns = new DataGridViewColumnCollection(this);
        _rows = new DataGridViewRowCollection(this);
        _vscroll = new ScrollBarCore(this, vertical: true, (v, _) => ScrollTo(_scroll.X, v));
        _hscroll = new ScrollBarCore(this, vertical: false, (v, _) => ScrollTo(v, _scroll.Y));

        RowTemplate = new DataGridViewRow();
        // WinForms sizes the default row from the font: Font.Height + 9 (25 at 9pt Segoe UI).
        RowTemplate.Height = Font.Height + 9;
        BackgroundColor = SystemColors.AppWorkspace;
        BackColor = SystemColors.Window;

        _defaultCellStyle = DefaultDefaultCellStyle();
        _ambientFont = _defaultCellStyle.Font;
        _columnHeadersDefaultCellStyle = DefaultColumnHeadersDefaultCellStyle();
        _ambientColumnHeadersFont = _columnHeadersDefaultCellStyle.Font;
        _rowHeadersDefaultCellStyle = DefaultRowHeadersDefaultCellStyle();
        _ambientRowHeadersFont = _rowHeadersDefaultCellStyle.Font;
        RowsDefaultCellStyle = new DataGridViewCellStyle();
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle();
    }

    // --- design-time serialization ---------------------------------------------------------------
    // A style is written only when it differs from what a new grid starts with (WinForms compares
    // against its DefaultXxxCellStyle the same way).

    private static readonly DataGridViewCellStyle s_emptyStyle = new();

    internal bool ShouldSerializeDefaultCellStyle() => !DefaultCellStyle.IsEquivalentTo(DefaultDefaultCellStyle());

    internal bool ShouldSerializeColumnHeadersDefaultCellStyle() => !ColumnHeadersDefaultCellStyle.IsEquivalentTo(DefaultColumnHeadersDefaultCellStyle());

    internal bool ShouldSerializeRowHeadersDefaultCellStyle() => !RowHeadersDefaultCellStyle.IsEquivalentTo(DefaultRowHeadersDefaultCellStyle());

    internal bool ShouldSerializeRowsDefaultCellStyle() => !RowsDefaultCellStyle.IsEquivalentTo(s_emptyStyle);

    internal bool ShouldSerializeAlternatingRowsDefaultCellStyle() => !AlternatingRowsDefaultCellStyle.IsEquivalentTo(s_emptyStyle);

    internal bool ShouldSerializeBackgroundColor() => BackgroundColor != SystemColors.AppWorkspace;

    internal bool ShouldSerializeGridColor() => GridColor != Color.FromArgb(0xD0, 0xD0, 0xD0);

    internal bool ShouldSerializeColumnHeadersHeight() =>
        _columnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize && _columnHeadersHeight != 23;

    internal bool ShouldSerializeRowHeadersWidth() => _rowHeadersWidth != 41;

    protected override Size DefaultSize => new Size(240, 150);

    // --- model ---------------------------------------------------------------------------------

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public DataGridViewColumnCollection Columns => _columns;

    [Browsable(false)]
    public DataGridViewRowCollection Rows => _rows;

    [Category("Appearance")]
    [Description("Identifies the template row whose characteristics are used as the basis for all new implicitly added rows.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public DataGridViewRow RowTemplate { get; set; }

    /// <summary>Index of the blank "new row" at the bottom, or -1 when the grid does not offer one.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int NewRowIndex => AllowUserToAddRows && !IsBound && _rows.Count > 0 ? _rows.Count - 1 : -1;

    internal DataGridViewRow CreateRowFromTemplate()
    {
        var row = (DataGridViewRow)RowTemplate.Clone();
        row.Cells.Clear();
        foreach (DataGridViewColumn column in _columns) row.Cells.Add(column.CreateCell());
        return row;
    }

    // --- appearance ------------------------------------------------------------------------------

    // The three default styles start with the grid's font, as in WinForms (DefaultDefaultCellStyle): a cell's
    // InheritedStyle, and so the cellStyle a custom cell's Paint gets, always has a Font. The font is "ambient" -
    // it follows the grid's Font - until the style is given a font of its own.
    private DataGridViewCellStyle _defaultCellStyle;
    private DataGridViewCellStyle _columnHeadersDefaultCellStyle;
    private DataGridViewCellStyle _rowHeadersDefaultCellStyle;
    private Font? _ambientFont, _ambientColumnHeadersFont, _ambientRowHeadersFont;

    private DataGridViewCellStyle DefaultDefaultCellStyle() => new()
    {
        BackColor = SystemColors.Window,
        ForeColor = SystemColors.ControlText,
        SelectionBackColor = Theme.Highlight,
        SelectionForeColor = Theme.HighlightText,
        Font = Font,
        Alignment = DataGridViewContentAlignment.MiddleLeft,
        WrapMode = DataGridViewTriState.False,
    };

    private DataGridViewCellStyle DefaultColumnHeadersDefaultCellStyle() => new()
    {
        BackColor = Theme.StripBackground,
        ForeColor = SystemColors.ControlText,
        SelectionBackColor = Theme.StripItemPressed,
        SelectionForeColor = SystemColors.ControlText,
        Font = Font,
        Alignment = DataGridViewContentAlignment.MiddleLeft,
        WrapMode = DataGridViewTriState.True,
    };

    private DataGridViewCellStyle DefaultRowHeadersDefaultCellStyle() => new()
    {
        BackColor = Theme.StripBackground,
        ForeColor = SystemColors.ControlText,
        SelectionBackColor = Theme.StripItemPressed,
        SelectionForeColor = SystemColors.ControlText,
        Font = Font,
        Alignment = DataGridViewContentAlignment.MiddleLeft,
        WrapMode = DataGridViewTriState.True,
    };

    /// <summary>
    /// The style every cell inherits from. A style assigned with members unset gets them from the defaults
    /// (WinForms returns a filled copy from the getter instead - a deliberate difference, so that editing the
    /// returned style is never lost); null restores the default style.
    /// </summary>
    [Category("Appearance")]
    [Description("The DataGridViewBand.DefaultCellStyle to be applied to the DataGridView if no other style is set.")]
    [AmbientValue(null)]
    [AllowNull]
    public DataGridViewCellStyle DefaultCellStyle
    {
        get => _defaultCellStyle;
        set
        {
            var defaults = DefaultDefaultCellStyle();
            if (value == null) value = defaults;
            else
            {
                if (value.BackColor.IsEmpty) value.BackColor = defaults.BackColor;
                if (value.ForeColor.IsEmpty) value.ForeColor = defaults.ForeColor;
                if (value.SelectionBackColor.IsEmpty) value.SelectionBackColor = defaults.SelectionBackColor;
                if (value.SelectionForeColor.IsEmpty) value.SelectionForeColor = defaults.SelectionForeColor;
                value.Font ??= defaults.Font;
                if (value.Alignment == DataGridViewContentAlignment.NotSet) value.Alignment = defaults.Alignment;
                if (value.WrapMode == DataGridViewTriState.NotSet) value.WrapMode = defaults.WrapMode;
            }
            _defaultCellStyle = value;
            _ambientFont = ReferenceEquals(value.Font, Font) ? Font : null;
            InvalidateGridLayout();
        }
    }

    [Category("Appearance")]
    [Description("The default column header style.")]
    [AmbientValue(null)]
    [AllowNull]
    public DataGridViewCellStyle ColumnHeadersDefaultCellStyle
    {
        get => _columnHeadersDefaultCellStyle;
        set
        {
            _columnHeadersDefaultCellStyle = value ?? DefaultColumnHeadersDefaultCellStyle();
            _ambientColumnHeadersFont = ReferenceEquals(_columnHeadersDefaultCellStyle.Font, Font) ? Font : null;
            InvalidateGridLayout();
        }
    }

    [Category("Appearance")]
    [Description("The default style applied to the row header cells.")]
    [AmbientValue(null)]
    [AllowNull]
    public DataGridViewCellStyle RowHeadersDefaultCellStyle
    {
        get => _rowHeadersDefaultCellStyle;
        set
        {
            _rowHeadersDefaultCellStyle = value ?? DefaultRowHeadersDefaultCellStyle();
            _ambientRowHeadersFont = ReferenceEquals(_rowHeadersDefaultCellStyle.Font, Font) ? Font : null;
            InvalidateGridLayout();
        }
    }

    [Category("Appearance")]
    [Description("The default style applied to the row cells of the DataGridView.")]
    public DataGridViewCellStyle RowsDefaultCellStyle { get; set; }

    [Category("Appearance")]
    [Description("The default cell style applied to odd-numbered rows.")]
    public DataGridViewCellStyle AlternatingRowsDefaultCellStyle { get; set; }

    [Category("Appearance")]
    [Description("The color of the grid lines separating the cells of the DataGridView.")]
    public Color GridColor { get; set; } = Color.FromArgb(0xD0, 0xD0, 0xD0);

    [Category("Appearance")]
    [Description("The background color of the DataGridView.")]
    public Color BackgroundColor { get; set; }

    [Category("Appearance")]
    [Description("The border style for the DataGridView.")]
    [DefaultValue(BorderStyle.FixedSingle)]
    public BorderStyle BorderStyle { get; set; } = BorderStyle.FixedSingle;

    [Category("Appearance")]
    [Description("The cell border style for the DataGridView.")]
    [DefaultValue(DataGridViewCellBorderStyle.Single)]
    public DataGridViewCellBorderStyle CellBorderStyle { get; set; } = DataGridViewCellBorderStyle.Single;

    [Category("Appearance")]
    [Description("The border style applied to the column headers.")]
    [DefaultValue(DataGridViewHeaderBorderStyle.Raised)]
    public DataGridViewHeaderBorderStyle ColumnHeadersBorderStyle { get; set; } = DataGridViewHeaderBorderStyle.Raised;

    [Category("Appearance")]
    [Description("The border style of the row header cells.")]
    [DefaultValue(DataGridViewHeaderBorderStyle.Raised)]
    public DataGridViewHeaderBorderStyle RowHeadersBorderStyle { get; set; } = DataGridViewHeaderBorderStyle.Raised;

    [Category("Appearance")]
    [Description("Indicates whether the column headers row is displayed.")]
    [DefaultValue(true)]
    public bool ColumnHeadersVisible { get; set; } = true;

    [Category("Appearance")]
    [Description("Indicates whether the column that contains row headers is displayed.")]
    [DefaultValue(true)]
    public bool RowHeadersVisible { get; set; } = true;

    [Category("Appearance")]
    [Description("The height, in pixels, of the column headers row.")]
    [Localizable(true)]
    public int ColumnHeadersHeight
    {
        get => ColumnHeadersVisible ? _columnHeadersHeight : 0;
        set
        {
            if (value < 4 || value > 32768) throw new ArgumentOutOfRangeException(nameof(value));
            // While the headers size themselves the value is kept for when they stop (as WinForms).
            if (_columnHeadersHeightSizeMode == DataGridViewColumnHeadersHeightSizeMode.AutoSize)
            {
                _cachedColumnHeadersHeight = value;
                return;
            }
            if (_columnHeadersHeight == value) return;
            _columnHeadersHeight = value;
            InvalidateGridLayout();
            OnColumnHeadersHeightChanged(EventArgs.Empty);
        }
    }

    [Category("Property Changed")]
    [Description("Event raised when the value of the ColumnHeadersHeight property changes.")]
    public event EventHandler? ColumnHeadersHeightChanged;

    protected virtual void OnColumnHeadersHeightChanged(EventArgs e) => ColumnHeadersHeightChanged?.Invoke(this, e);

    [Category("Behavior")]
    [Description("Determines the behavior for adjusting the column headers height.")]
    [DefaultValue(DataGridViewColumnHeadersHeightSizeMode.EnableResizing)]
    [RefreshProperties(RefreshProperties.All)]
    public DataGridViewColumnHeadersHeightSizeMode ColumnHeadersHeightSizeMode
    {
        get => _columnHeadersHeightSizeMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(DataGridViewColumnHeadersHeightSizeMode));
            if (_columnHeadersHeightSizeMode == value) return;
            var e = new DataGridViewAutoSizeModeEventArgs(_columnHeadersHeightSizeMode == DataGridViewColumnHeadersHeightSizeMode.AutoSize);
            _columnHeadersHeightSizeMode = value;
            OnColumnHeadersHeightSizeModeChanged(e);
        }
    }

    [Category("Property Changed")]
    [Description("Occurs when the value of the ColumnHeadersHeightSizeMode property changes.")]
    public event DataGridViewAutoSizeModeEventHandler? ColumnHeadersHeightSizeModeChanged;

    protected virtual void OnColumnHeadersHeightSizeModeChanged(DataGridViewAutoSizeModeEventArgs e)
    {
        if (_columnHeadersHeightSizeMode == DataGridViewColumnHeadersHeightSizeMode.AutoSize)
        {
            if (!e.PreviousModeAutoSized) _cachedColumnHeadersHeight = _columnHeadersHeight;
            AutoResizeColumnHeadersHeight();
        }
        else if (e.PreviousModeAutoSized)
        {
            ColumnHeadersHeight = _cachedColumnHeadersHeight;
        }
        ColumnHeadersHeightSizeModeChanged?.Invoke(this, e);
    }

    /// <summary>Fits the column headers row to the tallest header text (one line, as the headers are drawn).</summary>
    public void AutoResizeColumnHeadersHeight()
    {
        if (!ColumnHeadersVisible) return;
        int height = 0;
        var style = ColumnHeadersDefaultCellStyle;
        var font = style.Font ?? Font;
        foreach (DataGridViewColumn column in _columns)
        {
            if (!column.Visible) continue;
            var text = string.IsNullOrEmpty(column.HeaderText) ? "Ag" : column.HeaderText;
            height = Math.Max(height, TextRenderer.MeasureText(text, font).Height + style.Padding.Vertical + ColumnHeaderTextMargin);
        }
        if (height == 0) height = TextRenderer.MeasureText("Ag", font).Height + style.Padding.Vertical + ColumnHeaderTextMargin;
        height = Math.Clamp(height, 4, 32768);
        if (height == _columnHeadersHeight) return;
        _columnHeadersHeight = height;
        InvalidateGridLayout();
        OnColumnHeadersHeightChanged(EventArgs.Empty);
    }

    /// <summary>What a header row adds to its text: the default 23 over Segoe UI 9's 15.</summary>
    private const int ColumnHeaderTextMargin = 8;

    [Category("Layout")]
    [Description("The scroll bars the DataGridView shows when its content does not fit.")]
    [DefaultValue(ScrollBars.Both)]
    [Localizable(true)]
    public ScrollBars ScrollBars
    {
        get => _scrollBars;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ScrollBars));
            if (_scrollBars == value) return;
            // WinForms scrolls to the top-left first, so no scroll offset outlives its bar.
            _scroll = Point.Empty;
            _scrollBars = value;
            InvalidateGridLayout();
        }
    }

    [Category("Layout")]
    [Description("The width, in pixels, of the column that contains the row headers.")]
    [Localizable(true)]
    public int RowHeadersWidth
    {
        get => RowHeadersVisible ? _rowHeadersWidth : 0;
        set
        {
            if (value < 4 || value > 32768) throw new ArgumentOutOfRangeException(nameof(value));
            _rowHeadersWidth = value;
            InvalidateGridLayout();
        }
    }

    [Category("Layout")]
    [Description("Determines the auto size mode for the visible columns.")]
    [DefaultValue(DataGridViewAutoSizeColumnsMode.None)]
    public DataGridViewAutoSizeColumnsMode AutoSizeColumnsMode { get; set; } = DataGridViewAutoSizeColumnsMode.None;

    [Category("Behavior")]
    [Description("Indicates how the cells of the DataGridView can be selected.")]
    [DefaultValue(DataGridViewSelectionMode.RowHeaderSelect)]
    public DataGridViewSelectionMode SelectionMode { get; set; } = DataGridViewSelectionMode.RowHeaderSelect;

    [Category("Behavior")]
    [Description("Identifies the mode that determines how cell editing is started.")]
    [DefaultValue(DataGridViewEditMode.EditOnKeystrokeOrF2)]
    public DataGridViewEditMode EditMode { get; set; } = DataGridViewEditMode.EditOnKeystrokeOrF2;

    [Category("Behavior")]
    [Description("Indicates whether the option to add rows is displayed to the user.")]
    [DefaultValue(true)]
    public bool AllowUserToAddRows { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether the user is allowed to delete rows from the DataGridView.")]
    [DefaultValue(true)]
    public bool AllowUserToDeleteRows { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether users can resize columns.")]
    [DefaultValue(true)]
    public bool AllowUserToResizeColumns { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether users can resize rows.")]
    [DefaultValue(true)]
    public bool AllowUserToResizeRows { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether manual column repositioning is enabled.")]
    [DefaultValue(false)]
    public bool AllowUserToOrderColumns { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the user is allowed to select more than one cell, row, or column of the DataGridView at a time.")]
    [DefaultValue(true)]
    public bool MultiSelect { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether the user can edit the cells of the DataGridView control.")]
    [DefaultValue(false)]
    public bool ReadOnly { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether you have provided your own data-management operations for the DataGridView control.")]
    [DefaultValue(false)]
    public bool VirtualMode { get; set; }

    [Category("Appearance")]
    [Description("Indicates whether or not ToolTips will show when the mouse pointer pauses on a cell.")]
    [DefaultValue(true)]
    public bool ShowCellToolTips { get; set; } = true;

    [Category("Appearance")]
    [Description("Indicates whether row and column headers use the visual styles of the user's current theme if visual styles are enabled for the application.")]
    [DefaultValue(true)]
    public bool EnableHeadersVisualStyles { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether the TAB key moves the focus to the next control in the tab order rather than moving focus to the next cell in the control.")]
    [DefaultValue(false)]
    public bool StandardTab { get; set; }

    [Browsable(false)]
    public DataGridViewColumn? SortedColumn => _sortedColumn;

    [Browsable(false)]
    public SortOrder SortOrder => _sortOrder;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DataGridViewTopLeftHeaderCell TopLeftHeaderCell { get; } = new();

    // --- data binding --------------------------------------------------------------------------------

    [Category("Data")]
    [Description("Indicates the source of data for the DataGridView control.")]
    [DefaultValue(null)]
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (ReferenceEquals(_dataSource, value)) return;
            DetachDataSource();
            _dataSource = value;
            AttachDataSource();
        }
    }

    [Category("Data")]
    [Description("Indicates a sub-list of the DataSource to show in the DataGridView control.")]
    [DefaultValue("")]
    public string DataMember
    {
        get => _dataMember;
        set
        {
            if (_dataMember == value) return;
            _dataMember = value ?? string.Empty;
            DetachDataSource();
            AttachDataSource();
        }
    }

    [DefaultValue(true)]
    [Browsable(false)]
    public bool AutoGenerateColumns { get; set; } = true;

    internal bool IsBound => _boundList != null;

    private void DetachDataSource()
    {
        if (_boundList is IBindingList bindingList) bindingList.ListChanged -= OnBoundListChanged;
        if (_dataSource is BindingSource source) source.ListChanged -= OnBoundListChanged;
        _boundList = null;
        _boundProperties = null;
    }

    private void AttachDataSource()
    {
        var list = ResolveList(_dataSource, _dataMember);
        _boundList = list;
        if (list == null)
        {
            _rows.Clear();
            InvalidateGridLayout();
            return;
        }

        _boundProperties = GetItemProperties(list);
        if (AutoGenerateColumns) GenerateColumns();
        if (list is IBindingList bindingList) bindingList.ListChanged += OnBoundListChanged;
        if (_dataSource is BindingSource source) source.ListChanged += OnBoundListChanged;
        RebuildBoundRows();
    }

    private static IList? ResolveList(object? source, string member)
    {
        object? resolved = source switch
        {
            null => null,
            BindingSource bindingSource => bindingSource.List,
            IListSource listSource => listSource.GetList(),
            _ => source,
        };
        if (resolved != null && !string.IsNullOrEmpty(member))
        {
            var property = TypeDescriptor.GetProperties(resolved).Find(member, true);
            resolved = property?.GetValue(resolved);
            if (resolved is IListSource inner) resolved = inner.GetList();
        }
        return resolved as IList;
    }

    private static PropertyDescriptorCollection GetItemProperties(IList list)
    {
        if (list is ITypedList typed) return typed.GetItemProperties(null);
        var itemType = list.GetType().GetInterface("IList`1")?.GetGenericArguments()[0];
        if (itemType == null && list.Count > 0 && list[0] != null) itemType = list[0]!.GetType();
        return itemType != null ? TypeDescriptor.GetProperties(itemType) : new PropertyDescriptorCollection(null);
    }

    private void GenerateColumns()
    {
        if (_boundProperties == null) return;
        _columns.Clear();
        foreach (PropertyDescriptor property in _boundProperties)
        {
            if (!property.IsBrowsable) continue;
            var column = new DataGridViewTextBoxColumn
            {
                Name = property.Name,
                HeaderText = property.DisplayName,
                DataPropertyName = property.Name,
                ValueType = property.PropertyType,
                ReadOnly = property.IsReadOnly,
            };
            if (property.PropertyType == typeof(bool))
            {
                _columns.Add(new DataGridViewCheckBoxColumn
                {
                    Name = property.Name,
                    HeaderText = property.DisplayName,
                    DataPropertyName = property.Name,
                    ValueType = property.PropertyType,
                    ReadOnly = property.IsReadOnly,
                });
                continue;
            }
            _columns.Add(column);
        }
    }

    private void OnBoundListChanged(object? sender, ListChangedEventArgs e)
    {
        if (_inBindingUpdate) return;
        RebuildBoundRows();
    }

    private void RebuildBoundRows()
    {
        if (_boundList == null) return;
        var rows = new List<DataGridViewRow>(_boundList.Count);
        for (int i = 0; i < _boundList.Count; i++) rows.Add(CreateRowFromTemplate());
        _rows.ResetTo(rows);
        _selection.RemoveWhere(c => c.Row >= rows.Count || c.Column >= _columns.Count);
        InvalidateGridLayout();
    }

    private PropertyDescriptor? PropertyFor(int columnIndex)
    {
        if (_boundProperties == null || columnIndex < 0 || columnIndex >= _columns.Count) return null;
        var name = _columns[columnIndex].DataPropertyName;
        if (string.IsNullOrEmpty(name)) name = _columns[columnIndex].Name;
        return string.IsNullOrEmpty(name) ? null : _boundProperties.Find(name, true);
    }

    internal object? GetBoundValue(int rowIndex, int columnIndex)
    {
        if (_boundList == null || rowIndex < 0 || rowIndex >= _boundList.Count) return null;
        var property = PropertyFor(columnIndex);
        var item = _boundList[rowIndex];
        return item == null || property == null ? null : property.GetValue(item);
    }

    internal bool SetBoundValue(int rowIndex, int columnIndex, object? value)
    {
        if (_boundList == null || rowIndex < 0 || rowIndex >= _boundList.Count) return false;
        var property = PropertyFor(columnIndex);
        var item = _boundList[rowIndex];
        if (item == null || property == null || property.IsReadOnly) return false;
        try
        {
            _inBindingUpdate = true;
            property.SetValue(item, ConvertForProperty(value, property.PropertyType));
            return true;
        }
        catch (Exception ex)
        {
            var error = new DataGridViewDataErrorEventArgs(ex, columnIndex, rowIndex, DataGridViewDataErrorContexts.Commit);
            OnDataError(error);
            if (error.ThrowException) throw;
            return false;
        }
        finally
        {
            _inBindingUpdate = false;
        }
    }

    private static object? ConvertForProperty(object? value, Type target)
    {
        if (value == null) return null;
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (underlying.IsInstanceOfType(value)) return value;
        if (value is CheckState state && underlying == typeof(bool)) return state == CheckState.Checked;
        return Convert.ChangeType(value, underlying, CultureInfo.CurrentCulture);
    }

    // --- events -------------------------------------------------------------------------------------

    [Category("Mouse")]
    [Description("Occurs when any part of the cell is clicked.")]
    public event DataGridViewCellEventHandler? CellClick;

    [Category("Mouse")]
    [Description("Occurs when the content within a cell is clicked.")]
    public event DataGridViewCellEventHandler? CellContentClick;

    [Category("Mouse")]
    [Description("Occurs when the user double-clicks anywhere in a cell.")]
    public event DataGridViewCellEventHandler? CellDoubleClick;

    [Category("Action")]
    [Description("Occurs when the value of a cell changes.")]
    public event DataGridViewCellEventHandler? CellValueChanged;

    [Category("Data")]
    [Description("Occurs when edit mode stops for the currently selected cell.")]
    public event DataGridViewCellEventHandler? CellEndEdit;

    [Category("Data")]
    [Description("Occurs when edit mode starts for the selected cell.")]
    public event DataGridViewCellCancelEventHandler? CellBeginEdit;

    [Category("Display")]
    [Description("Occurs when the contents of a cell need to be formatted for display.")]
    public event DataGridViewCellFormattingEventHandler? CellFormatting;

    [Category("Display")]
    [Description("Occurs when a cell needs to be drawn.")]
    public event DataGridViewCellPaintingEventHandler? CellPainting;

    [Category("Mouse")]
    [Description("Occurs whenever a mouse clicks anywhere on a cell.")]
    public event DataGridViewCellMouseEventHandler? CellMouseClick;

    [Category("Data")]
    [Description("Occurs when the DataGridView.VirtualMode property of the DataGridView control is true and the DataGridView requires a value for a cell in order to format and display the cell.")]
    public event DataGridViewCellValueEventHandler? CellValueNeeded;

    [Category("Data")]
    [Description("Occurs when the DataGridView.VirtualMode property of the DataGridView control is true, and a cell value has changed and requires storage in the underlying data source.")]
    public event DataGridViewCellValueEventHandler? CellValuePushed;

    [Category("Action")]
    [Description("Occurs when the current selection changes.")]
    public event EventHandler? SelectionChanged;

    [Category("Action")]
    [Description("Occurs when DataGridView.CurrentCell changes.")]
    public event EventHandler? CurrentCellChanged;

    [Category("Mouse")]
    [Description("Occurs when the user clicks a column header.")]
    public event DataGridViewCellMouseEventHandler? ColumnHeaderMouseClick;

    [Category("Action")]
    [Description("Event raised when one or more rows are added to the rows collection.")]
    public event DataGridViewRowsAddedEventHandler? RowsAdded;

    [Category("Action")]
    [Description("Event raised when one or more rows are removed from the rows collection.")]
    public event DataGridViewRowsRemovedEventHandler? RowsRemoved;

    [Category("Data")]
    [Description("Occurs when the DataGridView compares two cell values to perform a sort operation.")]
    public event DataGridViewSortCompareEventHandler? SortCompare;

    [Category("Behavior")]
    [Description("Occurs when an external data-parsing or validation operation throws an exception, or when an attempt to commit data to a data source does not succeed.")]
    public event DataGridViewDataErrorEventHandler? DataError;

    [Category("Action")]
    [Description("Occurs when the user deletes a row from the DataGridView control.")]
    public event DataGridViewRowCancelEventHandler? UserDeletingRow;

    [Category("Action")]
    [Description("Occurs when the user has finished deleting a row from the DataGridView control.")]
    public event DataGridViewRowEventHandler? UserDeletedRow;

    [Category("Data")]
    [Description("Occurs when the DataGridView control completes a sorting operation.")]
    public event EventHandler? Sorted;

    protected virtual void OnCellClick(DataGridViewCellEventArgs e) => CellClick?.Invoke(this, e);
    protected virtual void OnCellContentClick(DataGridViewCellEventArgs e) => CellContentClick?.Invoke(this, e);
    protected virtual void OnCellDoubleClick(DataGridViewCellEventArgs e) => CellDoubleClick?.Invoke(this, e);
    protected virtual void OnCellValueChanged(DataGridViewCellEventArgs e) => CellValueChanged?.Invoke(this, e);
    protected virtual void OnCellEndEdit(DataGridViewCellEventArgs e) => CellEndEdit?.Invoke(this, e);
    protected virtual void OnCellBeginEdit(DataGridViewCellCancelEventArgs e) => CellBeginEdit?.Invoke(this, e);
    protected virtual void OnCellFormatting(DataGridViewCellFormattingEventArgs e) => CellFormatting?.Invoke(this, e);
    protected virtual void OnCellPainting(DataGridViewCellPaintingEventArgs e) => CellPainting?.Invoke(this, e);
    protected virtual void OnCellMouseClick(DataGridViewCellMouseEventArgs e) => CellMouseClick?.Invoke(this, e);
    protected virtual void OnCellValueNeeded(DataGridViewCellValueEventArgs e) => CellValueNeeded?.Invoke(this, e);
    protected virtual void OnCellValuePushed(DataGridViewCellValueEventArgs e) => CellValuePushed?.Invoke(this, e);
    protected virtual void OnSelectionChanged(EventArgs e) => SelectionChanged?.Invoke(this, e);
    protected virtual void OnCurrentCellChanged(EventArgs e) => CurrentCellChanged?.Invoke(this, e);
    protected virtual void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e) => ColumnHeaderMouseClick?.Invoke(this, e);
    protected virtual void OnRowsAdded(DataGridViewRowsAddedEventArgs e) => RowsAdded?.Invoke(this, e);
    protected virtual void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e) => RowsRemoved?.Invoke(this, e);
    protected virtual void OnSortCompare(DataGridViewSortCompareEventArgs e) => SortCompare?.Invoke(this, e);
    protected virtual void OnDataError(DataGridViewDataErrorEventArgs e) => DataError?.Invoke(this, e);
    protected virtual void OnUserDeletingRow(DataGridViewRowCancelEventArgs e) => UserDeletingRow?.Invoke(this, e);
    protected virtual void OnUserDeletedRow(DataGridViewRowEventArgs e) => UserDeletedRow?.Invoke(this, e);
    protected virtual void OnSorted(EventArgs e) => Sorted?.Invoke(this, e);

    internal void NotifyCellValueChanged(int columnIndex, int rowIndex)
    {
        OnCellValueChanged(new DataGridViewCellEventArgs(columnIndex, rowIndex));
        Invalidate();
    }

    internal void ColumnsChanged(DataGridViewColumn? added)
    {
        // Every row needs a cell for the new column.
        foreach (DataGridViewRow row in _rows)
        {
            while (row.Cells.Count < _columns.Count) row.Cells.Add(_columns[row.Cells.Count].CreateCell());
            while (row.Cells.Count > _columns.Count) row.Cells.RemoveAt(row.Cells.Count - 1);
            row.Cells.Rebind();
        }
        InvalidateGridLayout();
        if (_columnHeadersHeightSizeMode == DataGridViewColumnHeadersHeightSizeMode.AutoSize) AutoResizeColumnHeadersHeight();
    }

    internal void RowsChanged(DataGridViewRowsAddedEventArgs e)
    {
        OnRowsAdded(e);
        InvalidateGridLayout();
    }

    internal void NotifyRowsRemoved(DataGridViewRowsRemovedEventArgs e)
    {
        _selection.RemoveWhere(c => c.Row >= _rows.Count);
        if (_currentCell != null && _currentCell.RowIndex < 0) SetCurrentCell(null);
        OnRowsRemoved(e);
        InvalidateGridLayout();
    }

    // --- layout -----------------------------------------------------------------------------------------

    internal void InvalidateGridLayout()
    {
        _layoutDirty = true;
        Invalidate();
    }

    private int BorderSize => BorderStyle switch { BorderStyle.None => 0, BorderStyle.FixedSingle => 1, _ => 2 };

    private Rectangle InnerRectangle
    {
        get
        {
            int b = BorderSize;
            return new Rectangle(b, b, Math.Max(0, Width - 2 * b), Math.Max(0, Height - 2 * b));
        }
    }

    private Rectangle ViewportRectangle
    {
        get
        {
            var r = InnerRectangle;
            if (_vVisible) r.Width = Math.Max(0, r.Width - ScrollBarCore.Thickness);
            if (_hVisible) r.Height = Math.Max(0, r.Height - ScrollBarCore.Thickness);
            return r;
        }
    }

    /// <summary>The scrolling part: everything right of the row headers and below the column headers.</summary>
    private Rectangle CellsRectangle
    {
        get
        {
            var v = ViewportRectangle;
            return new Rectangle(v.X + RowHeadersWidth, v.Y + ColumnHeadersHeight,
                Math.Max(0, v.Width - RowHeadersWidth), Math.Max(0, v.Height - ColumnHeadersHeight));
        }
    }

    private void EnsureLayout()
    {
        if (!_layoutDirty) return;
        _layoutDirty = false;
        ApplyAutoSizeColumns();

        int width = 0;
        foreach (DataGridViewColumn column in _columns)
        {
            if (column.Visible) width += column.Width;
        }
        int height = 0;
        foreach (DataGridViewRow row in _rows)
        {
            if (row.Visible) height += row.Height;
        }
        _content = new Size(width, height);
        UpdateScrollBars();
    }

    private void ApplyAutoSizeColumns()
    {
        if (_columns.Count == 0) return;
        var mode = AutoSizeColumnsMode;
        if (mode == DataGridViewAutoSizeColumnsMode.None)
        {
            // A column may still opt in on its own.
            foreach (DataGridViewColumn column in _columns)
            {
                if (column.AutoSizeMode is DataGridViewAutoSizeColumnMode.NotSet or DataGridViewAutoSizeColumnMode.None) continue;
                if (column.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) continue;
                column.Width = MeasureColumn(column, column.AutoSizeMode);
            }
            return;
        }

        if (mode == DataGridViewAutoSizeColumnsMode.Fill)
        {
            DistributeFillWidth();
            return;
        }

        foreach (DataGridViewColumn column in _columns)
        {
            if (!column.Visible) continue;
            var columnMode = column.AutoSizeMode != DataGridViewAutoSizeColumnMode.NotSet
                ? column.AutoSizeMode
                : (DataGridViewAutoSizeColumnMode)mode;
            if (columnMode is DataGridViewAutoSizeColumnMode.None or DataGridViewAutoSizeColumnMode.NotSet) continue;
            if (columnMode == DataGridViewAutoSizeColumnMode.Fill) continue;
            column.Width = MeasureColumn(column, columnMode);
        }
    }

    /// <summary>
    /// Fill mode: fixed columns keep their width, Fill columns share what is left in proportion to
    /// their FillWeight - the policy GoForms' DataGridView used, and the one WinForms documents.
    /// </summary>
    private void DistributeFillWidth()
    {
        var fill = new List<DataGridViewColumn>();
        int fixedWidth = 0;
        float totalWeight = 0;
        foreach (DataGridViewColumn column in _columns)
        {
            if (!column.Visible) continue;
            var columnMode = column.AutoSizeMode;
            if (columnMode is DataGridViewAutoSizeColumnMode.None)
            {
                fixedWidth += column.Width;
                continue;
            }
            if (columnMode is not DataGridViewAutoSizeColumnMode.NotSet and not DataGridViewAutoSizeColumnMode.Fill)
            {
                column.Width = MeasureColumn(column, columnMode);
                fixedWidth += column.Width;
                continue;
            }
            fill.Add(column);
            totalWeight += column.FillWeight;
        }
        if (fill.Count == 0 || totalWeight <= 0) return;

        var viewport = InnerRectangle;
        int available = Math.Max(0, viewport.Width - RowHeadersWidth - fixedWidth - (_vVisible ? ScrollBarCore.Thickness : 0));
        int used = 0;
        for (int i = 0; i < fill.Count; i++)
        {
            int share = i == fill.Count - 1
                ? Math.Max(fill[i].MinimumWidth, available - used)
                : Math.Max(fill[i].MinimumWidth, (int)(available * (fill[i].FillWeight / totalWeight)));
            fill[i].Width = share;
            used += share;
        }
    }

    private int MeasureColumn(DataGridViewColumn column, DataGridViewAutoSizeColumnMode mode)
    {
        int width = column.MinimumWidth;
        bool header = mode is DataGridViewAutoSizeColumnMode.ColumnHeader or DataGridViewAutoSizeColumnMode.AllCells
            or DataGridViewAutoSizeColumnMode.DisplayedCells;
        bool cells = mode is not DataGridViewAutoSizeColumnMode.ColumnHeader;

        if (header && ColumnHeadersVisible)
        {
            width = Math.Max(width, TextRenderer.MeasureText(column.HeaderText, Font).Width + 10);
        }
        if (cells)
        {
            int index = column.Index;
            foreach (DataGridViewRow row in _rows)
            {
                if (index >= row.Cells.Count) continue;
                var cell = row.Cells[index];
                width = Math.Max(width, cell.GetPreferredSize(null, cell.InheritedStyle, row.Index, Size.Empty).Width);
            }
        }
        return width;
    }

    private void UpdateScrollBars()
    {
        var inner = InnerRectangle;
        int headerWidth = RowHeadersWidth;
        int headerHeight = ColumnHeadersHeight;

        bool allowV = (_scrollBars & ScrollBars.Vertical) != 0, allowH = (_scrollBars & ScrollBars.Horizontal) != 0;
        bool v = allowV && _content.Height > inner.Height - headerHeight;
        bool h = allowH && _content.Width > inner.Width - headerWidth - (v ? ScrollBarCore.Thickness : 0);
        if (h && !v) v = allowV && _content.Height > inner.Height - headerHeight - ScrollBarCore.Thickness;
        _vVisible = v;
        _hVisible = h;

        var cells = CellsRectangle;
        _vscroll.Minimum = 0;
        _vscroll.Maximum = Math.Max(0, _content.Height - 1);
        _vscroll.LargeChange = Math.Max(1, cells.Height);
        _vscroll.SmallChange = Math.Max(1, RowTemplate.Height);
        _vscroll.Bounds = new Rectangle(inner.Right - ScrollBarCore.Thickness, inner.Y, ScrollBarCore.Thickness, Math.Max(0, ViewportRectangle.Height));
        _vscroll.Enabled = Enabled;

        _hscroll.Minimum = 0;
        _hscroll.Maximum = Math.Max(0, _content.Width - 1);
        _hscroll.LargeChange = Math.Max(1, cells.Width);
        _hscroll.SmallChange = 20;
        _hscroll.Bounds = new Rectangle(inner.X, inner.Bottom - ScrollBarCore.Thickness, Math.Max(0, ViewportRectangle.Width), ScrollBarCore.Thickness);
        _hscroll.Enabled = Enabled;

        int maxX = Math.Max(0, _content.Width - cells.Width);
        int maxY = Math.Max(0, _content.Height - cells.Height);
        _scroll = new Point(Math.Clamp(_scroll.X, 0, maxX), Math.Clamp(_scroll.Y, 0, maxY));
        _vscroll.Value = _scroll.Y;
        _hscroll.Value = _scroll.X;
    }

    private void ScrollTo(int x, int y)
    {
        var cells = CellsRectangle;
        int maxX = Math.Max(0, _content.Width - cells.Width);
        int maxY = Math.Max(0, _content.Height - cells.Height);
        var next = new Point(Math.Clamp(x, 0, maxX), Math.Clamp(y, 0, maxY));
        if (next == _scroll) return;
        _scroll = next;
        _vscroll.Value = next.Y;
        _hscroll.Value = next.X;
        PositionEditor();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        InvalidateGridLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        // An ambient font follows the grid's; one the style was given (a different Font object) stays.
        if (_ambientFont != null && ReferenceEquals(_defaultCellStyle.Font, _ambientFont)) _defaultCellStyle.Font = _ambientFont = Font;
        if (_ambientColumnHeadersFont != null && ReferenceEquals(_columnHeadersDefaultCellStyle.Font, _ambientColumnHeadersFont))
            _columnHeadersDefaultCellStyle.Font = _ambientColumnHeadersFont = Font;
        if (_ambientRowHeadersFont != null && ReferenceEquals(_rowHeadersDefaultCellStyle.Font, _ambientRowHeadersFont))
            _rowHeadersDefaultCellStyle.Font = _ambientRowHeadersFont = Font;
        InvalidateGridLayout();
    }

    // --- cell geometry ---------------------------------------------------------------------------------

    private int ColumnLeft(int columnIndex)
    {
        int x = 0;
        for (int i = 0; i < columnIndex && i < _columns.Count; i++)
        {
            if (_columns[i].Visible) x += _columns[i].Width;
        }
        return x;
    }

    private int RowTop(int rowIndex)
    {
        int y = 0;
        for (int i = 0; i < rowIndex && i < _rows.Count; i++)
        {
            if (_rows[i].Visible) y += _rows[i].Height;
        }
        return y;
    }

    public Rectangle GetCellDisplayRectangle(int columnIndex, int rowIndex, bool cutOverflow)
    {
        EnsureLayout();
        if (columnIndex < 0 || columnIndex >= _columns.Count || rowIndex < 0 || rowIndex >= _rows.Count) return Rectangle.Empty;
        var cells = CellsRectangle;
        var bounds = new Rectangle(
            cells.X + ColumnLeft(columnIndex) - _scroll.X,
            cells.Y + RowTop(rowIndex) - _scroll.Y,
            _columns[columnIndex].Width,
            _rows[rowIndex].Height);
        return cutOverflow ? Rectangle.Intersect(bounds, cells) : bounds;
    }

    public Rectangle GetRowDisplayRectangle(int rowIndex, bool cutOverflow)
    {
        EnsureLayout();
        if (rowIndex < 0 || rowIndex >= _rows.Count) return Rectangle.Empty;
        var viewport = ViewportRectangle;
        var bounds = new Rectangle(viewport.X, CellsRectangle.Y + RowTop(rowIndex) - _scroll.Y, viewport.Width, _rows[rowIndex].Height);
        return cutOverflow ? Rectangle.Intersect(bounds, viewport) : bounds;
    }

    public Rectangle GetColumnDisplayRectangle(int columnIndex, bool cutOverflow)
    {
        EnsureLayout();
        if (columnIndex < 0 || columnIndex >= _columns.Count) return Rectangle.Empty;
        var viewport = ViewportRectangle;
        var bounds = new Rectangle(CellsRectangle.X + ColumnLeft(columnIndex) - _scroll.X, viewport.Y, _columns[columnIndex].Width, viewport.Height);
        return cutOverflow ? Rectangle.Intersect(bounds, viewport) : bounds;
    }

    internal Size GetCellSize(int columnIndex, int rowIndex)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count || rowIndex < 0 || rowIndex >= _rows.Count) return Size.Empty;
        return new Size(_columns[columnIndex].Width, _rows[rowIndex].Height);
    }

    public DataGridViewHitTestInfo HitTest(int x, int y)
    {
        EnsureLayout();
        var viewport = ViewportRectangle;
        if (!viewport.Contains(x, y)) return DataGridViewHitTestInfo.Nowhere;

        bool inHeaderRow = ColumnHeadersVisible && y < viewport.Y + ColumnHeadersHeight;
        bool inHeaderColumn = RowHeadersVisible && x < viewport.X + RowHeadersWidth;
        if (inHeaderRow && inHeaderColumn) return new DataGridViewHitTestInfo(DataGridViewHitTestType.TopLeftHeader, -1, -1);

        int column = ColumnAt(x);
        int row = RowAt(y);
        if (inHeaderRow) return new DataGridViewHitTestInfo(DataGridViewHitTestType.ColumnHeader, column, -1);
        if (inHeaderColumn) return new DataGridViewHitTestInfo(DataGridViewHitTestType.RowHeader, -1, row);
        if (column >= 0 && row >= 0) return new DataGridViewHitTestInfo(DataGridViewHitTestType.Cell, column, row);
        return DataGridViewHitTestInfo.Nowhere;
    }

    private int ColumnAt(int x)
    {
        var cells = CellsRectangle;
        int position = x - cells.X + _scroll.X;
        if (position < 0) return -1;
        int left = 0;
        for (int i = 0; i < _columns.Count; i++)
        {
            if (!_columns[i].Visible) continue;
            int right = left + _columns[i].Width;
            if (position >= left && position < right) return i;
            left = right;
        }
        return -1;
    }

    private int RowAt(int y)
    {
        var cells = CellsRectangle;
        int position = y - cells.Y + _scroll.Y;
        if (position < 0) return -1;
        int top = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (!_rows[i].Visible) continue;
            int bottom = top + _rows[i].Height;
            if (position >= top && position < bottom) return i;
            top = bottom;
        }
        return -1;
    }

    /// <summary>Scrolls until the cell is fully inside the cell area.</summary>
    public bool FirstDisplayedCell(int columnIndex, int rowIndex)
    {
        EnsureLayout();
        var cells = CellsRectangle;
        int x = _scroll.X, y = _scroll.Y;
        if (rowIndex >= 0 && rowIndex < _rows.Count)
        {
            int top = RowTop(rowIndex);
            int bottom = top + _rows[rowIndex].Height;
            if (top < y) y = top;
            else if (bottom > y + cells.Height) y = bottom - cells.Height;
        }
        if (columnIndex >= 0 && columnIndex < _columns.Count)
        {
            int left = ColumnLeft(columnIndex);
            int right = left + _columns[columnIndex].Width;
            if (left < x) x = left;
            else if (right > x + cells.Width) x = right - cells.Width;
        }
        ScrollTo(x, y);
        return true;
    }

    // --- selection -----------------------------------------------------------------------------------------

    internal bool IsCellSelected(int columnIndex, int rowIndex) => _selection.Contains((columnIndex, rowIndex));

    internal void SetCellSelected(int columnIndex, int rowIndex, bool selected)
    {
        if (columnIndex < 0 || rowIndex < 0) return;
        bool changed = selected ? _selection.Add((columnIndex, rowIndex)) : _selection.Remove((columnIndex, rowIndex));
        if (!changed) return;
        OnSelectionChanged(EventArgs.Empty);
        Invalidate();
    }

    internal bool IsRowSelected(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count || _columns.Count == 0) return false;
        for (int c = 0; c < _columns.Count; c++)
        {
            if (_columns[c].Visible && !_selection.Contains((c, rowIndex))) return false;
        }
        return true;
    }

    internal void SetRowSelected(int rowIndex, bool selected)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count) return;
        bool changed = false;
        for (int c = 0; c < _columns.Count; c++)
        {
            if (!_columns[c].Visible) continue;
            changed |= selected ? _selection.Add((c, rowIndex)) : _selection.Remove((c, rowIndex));
        }
        if (!changed) return;
        OnSelectionChanged(EventArgs.Empty);
        Invalidate();
    }

    private bool IsColumnSelected(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count || _rows.Count == 0) return false;
        for (int r = 0; r < _rows.Count; r++)
        {
            if (_rows[r].Visible && !_selection.Contains((columnIndex, r))) return false;
        }
        return true;
    }

    private void SetColumnSelected(int columnIndex, bool selected)
    {
        for (int r = 0; r < _rows.Count; r++)
        {
            if (!_rows[r].Visible) continue;
            if (selected) _selection.Add((columnIndex, r));
            else _selection.Remove((columnIndex, r));
        }
        OnSelectionChanged(EventArgs.Empty);
        Invalidate();
    }

    [Browsable(false)]
    public DataGridViewSelectedCellCollection SelectedCells
    {
        get
        {
            var cells = new List<DataGridViewCell>();
            foreach (var (column, row) in OrderedSelection())
            {
                if (row < _rows.Count && column < _rows[row].Cells.Count) cells.Add(_rows[row].Cells[column]);
            }
            return new DataGridViewSelectedCellCollection(cells);
        }
    }

    [Browsable(false)]
    public DataGridViewSelectedRowCollection SelectedRows
    {
        get
        {
            var rows = new List<DataGridViewRow>();
            for (int r = 0; r < _rows.Count; r++)
            {
                if (IsRowSelected(r)) rows.Add(_rows[r]);
            }
            return new DataGridViewSelectedRowCollection(rows);
        }
    }

    [Browsable(false)]
    public DataGridViewSelectedColumnCollection SelectedColumns
    {
        get
        {
            var columns = new List<DataGridViewColumn>();
            for (int c = 0; c < _columns.Count; c++)
            {
                if (IsColumnSelected(c)) columns.Add(_columns[c]);
            }
            return new DataGridViewSelectedColumnCollection(columns);
        }
    }

    private IEnumerable<(int Column, int Row)> OrderedSelection()
    {
        var list = new List<(int Column, int Row)>(_selection);
        list.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column));
        return list;
    }

    public void ClearSelection()
    {
        if (_selection.Count == 0) return;
        _selection.Clear();
        OnSelectionChanged(EventArgs.Empty);
        Invalidate();
    }

    public void SelectAll()
    {
        for (int r = 0; r < _rows.Count; r++)
        {
            for (int c = 0; c < _columns.Count; c++)
            {
                if (_rows[r].Visible && _columns[c].Visible) _selection.Add((c, r));
            }
        }
        OnSelectionChanged(EventArgs.Empty);
        Invalidate();
    }

    private void SelectOnlyCell(int column, int row)
    {
        _selection.Clear();
        switch (SelectionMode)
        {
            case DataGridViewSelectionMode.FullRowSelect:
            case DataGridViewSelectionMode.RowHeaderSelect when column < 0:
                for (int c = 0; c < _columns.Count; c++)
                {
                    if (_columns[c].Visible) _selection.Add((c, row));
                }
                break;
            case DataGridViewSelectionMode.FullColumnSelect:
            case DataGridViewSelectionMode.ColumnHeaderSelect when row < 0:
                for (int r = 0; r < _rows.Count; r++)
                {
                    if (_rows[r].Visible) _selection.Add((column, r));
                }
                break;
            default:
                if (column >= 0 && row >= 0) _selection.Add((column, row));
                break;
        }
        OnSelectionChanged(EventArgs.Empty);
        Invalidate();
    }

    private void SelectRangeTo(int column, int row)
    {
        if (_anchor.Column < 0 || _anchor.Row < 0)
        {
            SelectOnlyCell(column, row);
            return;
        }
        _selection.Clear();
        int c1 = Math.Min(_anchor.Column, column), c2 = Math.Max(_anchor.Column, column);
        int r1 = Math.Min(_anchor.Row, row), r2 = Math.Max(_anchor.Row, row);
        if (SelectionMode is DataGridViewSelectionMode.FullRowSelect)
        {
            c1 = 0;
            c2 = _columns.Count - 1;
        }
        for (int r = r1; r <= r2 && r < _rows.Count; r++)
        {
            for (int c = c1; c <= c2 && c < _columns.Count; c++)
            {
                if (_rows[r].Visible && _columns[c].Visible) _selection.Add((c, r));
            }
        }
        OnSelectionChanged(EventArgs.Empty);
        Invalidate();
    }

    // --- current cell ----------------------------------------------------------------------------------------

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DataGridViewCell? CurrentCell
    {
        get => _currentCell;
        set => SetCurrentCell(value);
    }

    [Browsable(false)]
    public DataGridViewRow? CurrentRow => _currentCell?.OwningRow;

    [Browsable(false)]
    public Point CurrentCellAddress => _currentCell != null ? new Point(_currentCell.ColumnIndex, _currentCell.RowIndex) : new Point(-1, -1);

    private void SetCurrentCell(DataGridViewCell? cell)
    {
        if (ReferenceEquals(_currentCell, cell)) return;
        EndEdit();
        _currentCell = cell;
        if (cell != null) FirstDisplayedCell(cell.ColumnIndex, cell.RowIndex);
        OnCurrentCellChanged(EventArgs.Empty);
        Invalidate();
    }

    private void SetCurrentCell(int column, int row)
    {
        if (column < 0 || column >= _columns.Count || row < 0 || row >= _rows.Count)
        {
            SetCurrentCell(null);
            return;
        }
        SetCurrentCell(_rows[row].Cells[column]);
    }

    // --- editing ------------------------------------------------------------------------------------------------

    internal bool IsCellInEditMode(int columnIndex, int rowIndex) =>
        _editingCell != null && _editingCell.ColumnIndex == columnIndex && _editingCell.RowIndex == rowIndex;

    [Browsable(false)]
    public bool IsCurrentCellInEditMode => _editingCell != null;

    public bool BeginEdit(bool selectAll)
    {
        var cell = _currentCell;
        if (cell == null || cell.ReadOnly || cell.EditType == null) return false;
        if (_editingCell == cell) return true;
        EndEdit();

        var e = new DataGridViewCellCancelEventArgs(cell.ColumnIndex, cell.RowIndex);
        OnCellBeginEdit(e);
        if (e.Cancel) return false;

        _editingCell = cell;
        _editor = CreateEditor(cell);
        PositionEditor();
        _editor.Visible = true;
        _editor.Focus();
        if (_editor is TextBox box)
        {
            box.Text = cell.FormattedValue as string ?? string.Empty;
            if (selectAll) box.SelectAll();
        }
        else if (_editor is ComboBox combo && cell is DataGridViewComboBoxCell comboCell)
        {
            combo.Items.Clear();
            foreach (var item in comboCell.EditItems) combo.Items.Add(item!);
            combo.SelectedItem = cell.Value;
        }
        return true;
    }

    private Control CreateEditor(DataGridViewCell cell)
    {
        if (cell.EditType == typeof(ComboBox))
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            combo.SelectedIndexChanged += (_, _) => { };
            Controls.Add(combo);
            return combo;
        }
        var box = new TextBox { BorderStyle = BorderStyle.None, Visible = false };
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Return) { EndEdit(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { CancelEdit(); e.Handled = true; }
        };
        Controls.Add(box);
        return box;
    }

    private void PositionEditor()
    {
        if (_editor == null || _editingCell == null) return;
        var bounds = GetCellDisplayRectangle(_editingCell.ColumnIndex, _editingCell.RowIndex, false);
        _editor.Bounds = new Rectangle(bounds.X + 1, bounds.Y + 1, Math.Max(0, bounds.Width - 2), Math.Max(0, bounds.Height - 2));
    }

    public bool EndEdit()
    {
        if (_editingCell == null || _editor == null) return true;
        var cell = _editingCell;
        _editingCell = null;

        object? value = _editor switch
        {
            TextBox box => cell.ParseFormattedValue(box.Text, cell.InheritedStyle, null, null),
            ComboBox combo => combo.SelectedItem,
            _ => null,
        };
        DisposeEditor();
        cell.Value = value;
        OnCellEndEdit(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));
        if (CanFocus) Focus();
        return true;
    }

    public void CancelEdit()
    {
        if (_editingCell == null) return;
        var cell = _editingCell;
        _editingCell = null;
        DisposeEditor();
        OnCellEndEdit(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));
        if (CanFocus) Focus();
    }

    private void DisposeEditor()
    {
        if (_editor == null) return;
        var editor = _editor;
        _editor = null;
        Controls.Remove(editor);
        editor.Dispose();
    }

    // --- sorting ---------------------------------------------------------------------------------------------------

    public void Sort(DataGridViewColumn dataGridViewColumn, ListSortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewColumn);
        if (IsBound && _dataSource is BindingSource source && source.SupportsSorting)
        {
            source.Sort = dataGridViewColumn.DataPropertyName + (direction == ListSortDirection.Descending ? " DESC" : " ASC");
        }
        else
        {
            int index = dataGridViewColumn.Index;
            int sign = direction == ListSortDirection.Ascending ? 1 : -1;
            _rows.SortCore((a, b) =>
            {
                object? x = index < a.Cells.Count ? a.Cells[index].Value : null;
                object? y = index < b.Cells.Count ? b.Cells[index].Value : null;
                var e = new DataGridViewSortCompareEventArgs(dataGridViewColumn, x, y, a.Index, b.Index);
                OnSortCompare(e);
                if (e.Handled) return sign * e.SortResult;
                return sign * CompareValues(x, y);
            });
        }
        _sortedColumn = dataGridViewColumn;
        _sortOrder = direction == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending;
        dataGridViewColumn.HeaderCell.SortGlyphDirection = _sortOrder;
        foreach (DataGridViewColumn column in _columns)
        {
            if (column != dataGridViewColumn) column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }
        OnSorted(EventArgs.Empty);
        InvalidateGridLayout();
    }

    public virtual void Sort(IComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        _rows.SortCore((a, b) => comparer.Compare(a, b));
        InvalidateGridLayout();
    }

    private static int CompareValues(object? x, object? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        if (x is IComparable comparable && x.GetType() == y.GetType()) return comparable.CompareTo(y);
        return string.Compare(x.ToString(), y.ToString(), StringComparison.CurrentCulture);
    }

    // --- input --------------------------------------------------------------------------------------------------------

    internal override bool IsOverlayPoint(Point p) =>
        (_vVisible && _vscroll.Bounds.Contains(p)) || (_hVisible && _hscroll.Bounds.Contains(p));

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
    {
        Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown => true,
        _ => base.IsInputKey(keyData),
    };

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (CanFocus) Focus();

        if (e.Button == MouseButtons.Left)
        {
            if (_vVisible && _vscroll.Bounds.Contains(e.Location)) { _vscroll.MouseDown(e.Location); base.OnMouseDown(e); return; }
            if (_hVisible && _hscroll.Bounds.Contains(e.Location)) { _hscroll.MouseDown(e.Location); base.OnMouseDown(e); return; }
        }

        var hit = HitTest(e.X, e.Y);
        switch (hit.Type)
        {
            case DataGridViewHitTestType.ColumnHeader:
                _pressedColumnHeader = hit.ColumnIndex;
                Invalidate();
                break;

            case DataGridViewHitTestType.RowHeader:
                if (SelectionMode is DataGridViewSelectionMode.RowHeaderSelect or DataGridViewSelectionMode.FullRowSelect)
                {
                    if (MultiSelect && (ModifierKeys & Keys.Shift) != 0) SelectRangeTo(_columns.Count - 1, hit.RowIndex);
                    else
                    {
                        SelectOnlyCell(-1, hit.RowIndex);
                        _anchor = (0, hit.RowIndex);
                    }
                    SetCurrentCell(0, hit.RowIndex);
                }
                break;

            case DataGridViewHitTestType.Cell:
                {
                    var cell = _rows[hit.RowIndex].Cells[hit.ColumnIndex];
                    bool control = (ModifierKeys & Keys.Control) != 0;
                    bool shift = (ModifierKeys & Keys.Shift) != 0;

                    if (MultiSelect && shift) SelectRangeTo(hit.ColumnIndex, hit.RowIndex);
                    else if (MultiSelect && control)
                    {
                        SetCellSelected(hit.ColumnIndex, hit.RowIndex, !cell.Selected);
                        _anchor = (hit.ColumnIndex, hit.RowIndex);
                    }
                    else
                    {
                        SelectOnlyCell(hit.ColumnIndex, hit.RowIndex);
                        _anchor = (hit.ColumnIndex, hit.RowIndex);
                    }
                    SetCurrentCell(cell);

                    var args = new DataGridViewCellEventArgs(hit.ColumnIndex, hit.RowIndex);
                    OnCellClick(args);
                    OnCellMouseClick(new DataGridViewCellMouseEventArgs(hit.ColumnIndex, hit.RowIndex,
                        e.X - GetCellDisplayRectangle(hit.ColumnIndex, hit.RowIndex, false).X,
                        e.Y - GetCellDisplayRectangle(hit.ColumnIndex, hit.RowIndex, false).Y, e));

                    if (IsContentClick(cell, e.Location))
                    {
                        cell.OnCellContentClick(args);
                        OnCellContentClick(args);
                    }
                    else if (EditMode == DataGridViewEditMode.EditOnEnter)
                    {
                        BeginEdit(true);
                    }
                    break;
                }
        }
        base.OnMouseDown(e);
    }

    /// <summary>True when the click landed on the interactive part of the cell (a check box, a button, a link).</summary>
    private bool IsContentClick(DataGridViewCell cell, Point point)
    {
        var bounds = GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, false);
        return cell switch
        {
            DataGridViewCheckBoxCell checkBox => checkBox.BoxBounds(bounds).Contains(point),
            DataGridViewButtonCell or DataGridViewLinkCell => bounds.Contains(point),
            _ => false,
        };
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool left = (e.Button & MouseButtons.Left) != 0;
        if (_vVisible) _vscroll.MouseMove(e.Location, left);
        if (_hVisible) _hscroll.MouseMove(e.Location, left);

        var hit = HitTest(e.X, e.Y);
        int hot = hit.Type == DataGridViewHitTestType.ColumnHeader ? hit.ColumnIndex : -1;
        if (hot != _hotColumnHeader)
        {
            _hotColumnHeader = hot;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_vVisible) _vscroll.MouseUp(e.Location);
            if (_hVisible) _hscroll.MouseUp(e.Location);

            if (_pressedColumnHeader >= 0)
            {
                int pressed = _pressedColumnHeader;
                _pressedColumnHeader = -1;
                var hit = HitTest(e.X, e.Y);
                if (hit.Type == DataGridViewHitTestType.ColumnHeader && hit.ColumnIndex == pressed && pressed < _columns.Count)
                {
                    var column = _columns[pressed];
                    OnColumnHeaderMouseClick(new DataGridViewCellMouseEventArgs(pressed, -1, e.X, e.Y, e));
                    if (column.SortMode == DataGridViewColumnSortMode.Automatic)
                    {
                        var direction = _sortedColumn == column && _sortOrder == SortOrder.Ascending
                            ? ListSortDirection.Descending
                            : ListSortDirection.Ascending;
                        Sort(column, direction);
                    }
                    else if (SelectionMode is DataGridViewSelectionMode.ColumnHeaderSelect or DataGridViewSelectionMode.FullColumnSelect)
                    {
                        SelectOnlyCell(pressed, -1);
                    }
                }
                Invalidate();
            }
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _vscroll.MouseLeave();
        _hscroll.MouseLeave();
        if (_hotColumnHeader != -1)
        {
            _hotColumnHeader = -1;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int notches = e.Delta / 120;
        if (notches == 0) notches = Math.Sign(e.Delta);
        if (_vVisible) ScrollTo(_scroll.X, _scroll.Y - notches * RowTemplate.Height * 3);
        base.OnMouseWheel(e);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        var point = PointToClient(MousePosition);
        var hit = HitTest(point.X, point.Y);
        if (hit.Type == DataGridViewHitTestType.Cell)
        {
            OnCellDoubleClick(new DataGridViewCellEventArgs(hit.ColumnIndex, hit.RowIndex));
            BeginEdit(true);
        }
        base.OnDoubleClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        EnsureLayout();
        var address = CurrentCellAddress;
        int column = address.X, row = address.Y;
        if (column < 0 && _columns.Count > 0) column = 0;
        if (row < 0 && _rows.Count > 0) row = 0;

        switch (e.KeyCode)
        {
            case Keys.Up: row--; break;
            case Keys.Down: row++; break;
            case Keys.Left: column--; break;
            case Keys.Right: column++; break;
            case Keys.Home: column = 0; if ((e.Modifiers & Keys.Control) != 0) row = 0; break;
            case Keys.End: column = _columns.Count - 1; if ((e.Modifiers & Keys.Control) != 0) row = _rows.Count - 1; break;
            case Keys.PageUp: row -= Math.Max(1, CellsRectangle.Height / Math.Max(1, RowTemplate.Height)); break;
            case Keys.PageDown: row += Math.Max(1, CellsRectangle.Height / Math.Max(1, RowTemplate.Height)); break;
            case Keys.Tab:
                column += (e.Modifiers & Keys.Shift) != 0 ? -1 : 1;
                if (column >= _columns.Count) { column = 0; row++; }
                else if (column < 0) { column = _columns.Count - 1; row--; }
                break;
            case Keys.F2:
                if (EditMode is DataGridViewEditMode.EditOnF2 or DataGridViewEditMode.EditOnKeystrokeOrF2)
                {
                    BeginEdit(true);
                    e.Handled = true;
                }
                return;
            case Keys.Space:
                if (_currentCell != null)
                {
                    var args = new DataGridViewCellEventArgs(_currentCell.ColumnIndex, _currentCell.RowIndex);
                    _currentCell.OnCellContentClick(args);
                    OnCellContentClick(args);
                    e.Handled = true;
                }
                return;
            case Keys.A when (e.Modifiers & Keys.Control) != 0 && MultiSelect:
                SelectAll();
                e.Handled = true;
                return;
            case Keys.Delete:
                if (AllowUserToDeleteRows && !IsBound && SelectedRows.Count > 0)
                {
                    DeleteSelectedRows();
                    e.Handled = true;
                }
                return;
            default:
                base.OnKeyDown(e);
                return;
        }

        if (_columns.Count == 0 || _rows.Count == 0)
        {
            base.OnKeyDown(e);
            return;
        }

        column = Math.Clamp(column, 0, _columns.Count - 1);
        row = Math.Clamp(row, 0, _rows.Count - 1);
        if (MultiSelect && (e.Modifiers & Keys.Shift) != 0) SelectRangeTo(column, row);
        else { SelectOnlyCell(column, row); _anchor = (column, row); }
        SetCurrentCell(column, row);
        e.Handled = true;
        base.OnKeyDown(e);
    }

    private void DeleteSelectedRows()
    {
        var rows = new List<DataGridViewRow>(SelectedRows);
        foreach (var row in rows)
        {
            var e = new DataGridViewRowCancelEventArgs(row);
            OnUserDeletingRow(e);
            if (e.Cancel) continue;
            _rows.Remove(row);
            OnUserDeletedRow(new DataGridViewRowEventArgs(row));
        }
    }

    // --- painting ---------------------------------------------------------------------------------------------------------

    internal TextFormatFlags TranslateAlignment(DataGridViewCellStyle style)
    {
        var alignment = style.Alignment == DataGridViewContentAlignment.NotSet
            ? DataGridViewContentAlignment.MiddleLeft
            : style.Alignment;
        var flags = alignment switch
        {
            DataGridViewContentAlignment.TopCenter or DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.BottomCenter => TextFormatFlags.HorizontalCenter,
            DataGridViewContentAlignment.TopRight or DataGridViewContentAlignment.MiddleRight or DataGridViewContentAlignment.BottomRight => TextFormatFlags.Right,
            _ => TextFormatFlags.Left,
        };
        flags |= alignment switch
        {
            DataGridViewContentAlignment.MiddleLeft or DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter,
            DataGridViewContentAlignment.BottomLeft or DataGridViewContentAlignment.BottomCenter or DataGridViewContentAlignment.BottomRight => TextFormatFlags.Bottom,
            _ => TextFormatFlags.Top,
        };
        return flags;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(BackgroundColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureLayout();
        var g = e.Graphics;
        var viewport = ViewportRectangle;
        var cells = CellsRectangle;

        var state = g.Save();
        g.IntersectClip(cells);
        PaintCells(g, cells);
        g.Restore(state);

        if (ColumnHeadersVisible)
        {
            state = g.Save();
            g.IntersectClip(new Rectangle(cells.X, viewport.Y, cells.Width, ColumnHeadersHeight));
            PaintColumnHeaders(g, cells);
            g.Restore(state);
        }
        if (RowHeadersVisible)
        {
            state = g.Save();
            g.IntersectClip(new Rectangle(viewport.X, cells.Y, RowHeadersWidth, cells.Height));
            PaintRowHeaders(g, cells);
            g.Restore(state);
        }
        if (ColumnHeadersVisible && RowHeadersVisible) PaintTopLeftHeader(g, viewport);

        if (BorderStyle != BorderStyle.None)
        {
            using var pen = new Pen(BorderStyle == BorderStyle.Fixed3D ? Theme.WindowBorder : Theme.ButtonBorder);
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
        base.OnPaint(e);
    }

    internal override void OnPaintOverlay(Graphics g)
    {
        if (_vVisible) _vscroll.Paint(g);
        if (_hVisible) _hscroll.Paint(g);
        base.OnPaintOverlay(g);
    }

    private void PaintCells(Graphics g, Rectangle clip)
    {
        for (int r = 0; r < _rows.Count; r++)
        {
            if (!_rows[r].Visible) continue;
            var rowBounds = GetRowDisplayRectangle(r, false);
            if (rowBounds.Bottom < clip.Top || rowBounds.Top > clip.Bottom) continue;

            for (int c = 0; c < _columns.Count; c++)
            {
                if (!_columns[c].Visible) continue;
                var bounds = GetCellDisplayRectangle(c, r, false);
                if (bounds.Right < clip.Left || bounds.Left > clip.Right) continue;
                PaintCell(g, clip, c, r, bounds);
            }
        }
    }

    private void PaintCell(Graphics g, Rectangle clip, int columnIndex, int rowIndex, Rectangle bounds)
    {
        var cell = _rows[rowIndex].Cells[columnIndex];
        var style = cell.InheritedStyle;
        var cellState = DataGridViewElementStates.Visible;
        if (IsCellSelected(columnIndex, rowIndex)) cellState |= DataGridViewElementStates.Selected;
        if (cell.ReadOnly) cellState |= DataGridViewElementStates.ReadOnly;

        object? value = cell.Value;
        if (VirtualMode)
        {
            var needed = new DataGridViewCellValueEventArgs(columnIndex, rowIndex);
            OnCellValueNeeded(needed);
            if (needed.Value != null) value = needed.Value;
        }

        object? formatted = cell.GetFormattedValue(value, rowIndex, style, DataGridViewDataErrorContexts.Display);
        var formatting = new DataGridViewCellFormattingEventArgs(columnIndex, rowIndex, formatted, typeof(string), style);
        OnCellFormatting(formatting);
        if (formatting.FormattingApplied || !Equals(formatting.Value, formatted)) formatted = formatting.Value;
        style = formatting.CellStyle ?? style;

        var painting = new DataGridViewCellPaintingEventArgs(this, g, clip, bounds, rowIndex, columnIndex, cellState,
            value, formatted, cell.ErrorText, style, new DataGridViewAdvancedBorderStyle(), DataGridViewPaintParts.All);
        OnCellPainting(painting);
        if (painting.Handled) return;

        cell.PaintCell(g, clip, bounds, rowIndex, cellState, value, formatted, cell.ErrorText, style,
            new DataGridViewAdvancedBorderStyle(), DataGridViewPaintParts.All);

        if (_currentCell == cell && Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(bounds, -1, -1));
        }
    }

    /// <summary>Used by DataGridViewCellPaintingEventArgs.PaintBackground.</summary>
    internal void PaintCellBackground(DataGridViewCellPaintingEventArgs e, Rectangle clipBounds, bool cellsPaintSelectionBackground)
    {
        bool selected = cellsPaintSelectionBackground && (e.State & DataGridViewElementStates.Selected) != 0;
        var color = selected ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor;
        if (color.IsEmpty) return;
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, e.CellBounds);
    }

    /// <summary>Used by DataGridViewCellPaintingEventArgs.PaintContent.</summary>
    internal void PaintCellContent(DataGridViewCellPaintingEventArgs e, Rectangle clipBounds)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _rows.Count || e.ColumnIndex < 0 || e.ColumnIndex >= _columns.Count) return;
        var cell = _rows[e.RowIndex].Cells[e.ColumnIndex];
        cell.PaintCell(e.Graphics, clipBounds, e.CellBounds, e.RowIndex, e.State, e.Value, e.FormattedValue, e.ErrorText,
            e.CellStyle, e.AdvancedBorderStyle, DataGridViewPaintParts.ContentForeground);
    }

    private void PaintColumnHeaders(Graphics g, Rectangle cells)
    {
        var viewport = ViewportRectangle;
        var style = ColumnHeadersDefaultCellStyle;
        for (int c = 0; c < _columns.Count; c++)
        {
            if (!_columns[c].Visible) continue;
            var column = _columns[c];
            var bounds = new Rectangle(cells.X + ColumnLeft(c) - _scroll.X, viewport.Y, column.Width, ColumnHeadersHeight);
            if (bounds.Right < cells.Left || bounds.Left > cells.Right) continue;

            bool selected = IsColumnSelected(c);
            var back = c == _pressedColumnHeader ? Theme.StripItemPressed
                : c == _hotColumnHeader ? Theme.StripItemHot
                : selected ? style.SelectionBackColor
                : style.BackColor;
            using (var brush = new SolidBrush(back))
            {
                g.FillRectangle(brush, bounds);
            }
            using (var pen = new Pen(GridColor))
            {
                g.DrawLine(pen, bounds.Right - 1, bounds.Top + 2, bounds.Right - 1, bounds.Bottom - 3);
                g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }

            var text = new Rectangle(bounds.X + 4, bounds.Y, Math.Max(0, bounds.Width - 8 - (column.HeaderCell.SortGlyphDirection != SortOrder.None ? 12 : 0)), bounds.Height);
            TextRenderer.DrawText(g, column.HeaderText, Font, text, style.ForeColor.IsEmpty ? ForeColor : style.ForeColor,
                TranslateAlignment(style) | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (column.HeaderCell.SortGlyphDirection != SortOrder.None)
            {
                PaintSortGlyph(g, new Rectangle(bounds.Right - 14, bounds.Y, 10, bounds.Height), column.HeaderCell.SortGlyphDirection);
            }
        }
    }

    private void PaintSortGlyph(Graphics g, Rectangle bounds, SortOrder order)
    {
        var middle = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        using var brush = new SolidBrush(ForeColor);
        var points = order == SortOrder.Ascending
            ? new[] { new Point(middle.X - 4, middle.Y + 2), new Point(middle.X + 4, middle.Y + 2), new Point(middle.X, middle.Y - 3) }
            : new[] { new Point(middle.X - 4, middle.Y - 2), new Point(middle.X + 4, middle.Y - 2), new Point(middle.X, middle.Y + 3) };
        g.FillPolygon(brush, points);
    }

    private void PaintRowHeaders(Graphics g, Rectangle cells)
    {
        var viewport = ViewportRectangle;
        var style = RowHeadersDefaultCellStyle;
        for (int r = 0; r < _rows.Count; r++)
        {
            if (!_rows[r].Visible) continue;
            var bounds = new Rectangle(viewport.X, cells.Y + RowTop(r) - _scroll.Y, RowHeadersWidth, _rows[r].Height);
            if (bounds.Bottom < cells.Top || bounds.Top > cells.Bottom) continue;

            bool selected = IsRowSelected(r);
            using (var brush = new SolidBrush(selected ? style.SelectionBackColor : style.BackColor))
            {
                g.FillRectangle(brush, bounds);
            }
            using (var pen = new Pen(GridColor))
            {
                g.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
                g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
            }

            // The arrow that marks the current row, as WinForms draws in the row header.
            if (_currentCell != null && _currentCell.RowIndex == r)
            {
                var middle = new Point(bounds.Left + 10, bounds.Top + bounds.Height / 2);
                using var arrow = new SolidBrush(ForeColor);
                g.FillPolygon(arrow, new[]
                {
                    new Point(middle.X - 3, middle.Y - 4), new Point(middle.X + 3, middle.Y), new Point(middle.X - 3, middle.Y + 4),
                });
            }

            if (_rows[r].HeaderCellValue != null)
            {
                TextRenderer.DrawText(g, _rows[r].HeaderCellValue!.ToString(), Font,
                    new Rectangle(bounds.X + 18, bounds.Y, Math.Max(0, bounds.Width - 20), bounds.Height),
                    style.ForeColor.IsEmpty ? ForeColor : style.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }
    }

    private void PaintTopLeftHeader(Graphics g, Rectangle viewport)
    {
        var bounds = new Rectangle(viewport.X, viewport.Y, RowHeadersWidth, ColumnHeadersHeight);
        using (var brush = new SolidBrush(ColumnHeadersDefaultCellStyle.BackColor))
        {
            g.FillRectangle(brush, bounds);
        }
        using var pen = new Pen(GridColor);
        g.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
        g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachDataSource();
            _vscroll.Dispose();
            _hscroll.Dispose();
        }
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + ", Rows.Count: " + _rows.Count;

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Color BackColor { get => base.BackColor; set => base.BackColor = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Font Font { get => base.Font; set => base.Font = value; }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Color ForeColor { get => base.ForeColor; set => base.ForeColor = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

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
    public new event EventHandler? FontChanged
    {
        add => base.FontChanged += value;
        remove => base.FontChanged -= value;
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

    // WinForms implements these explicitly; the designer writes ((ISupportInitialize)x).BeginInit().
    void ISupportInitialize.BeginInit() { }

    void ISupportInitialize.EndInit() { }
}
