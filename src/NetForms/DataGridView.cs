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
    private CurrencyManager? _currencyManager;
    private PropertyDescriptorCollection? _boundProperties;
    private bool _inBindingUpdate;
    private bool _syncingPosition;

    private DataGridViewCell? _currentCell;
    private (int Column, int Row) _anchor = (-1, -1);
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

    /// <summary>The number of rows; setting it adds rows from the template or removes them from the end (VirtualMode grids size themselves this way).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [DefaultValue(0)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public int RowCount
    {
        get => _rows.Count;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (value == _rows.Count) return;
            if (IsBound) throw new InvalidOperationException("RowCount property cannot be set on a data-bound DataGridView control.");
            if (value > 0 && _columns.Count == 0) ColumnCount = 1;
            if (value > _rows.Count) _rows.Add(value - _rows.Count);
            else while (_rows.Count > value) _rows.RemoveAt(_rows.Count - 1);
        }
    }

    /// <summary>The number of columns; setting it adds text box columns or removes them from the end.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [DefaultValue(0)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public int ColumnCount
    {
        get => _columns.Count;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (value == _columns.Count) return;
            if (IsBound) throw new InvalidOperationException("ColumnCount property cannot be set on a data-bound DataGridView control.");
            while (_columns.Count < value) _columns.Add(new DataGridViewTextBoxColumn());
            while (_columns.Count > value) _columns.RemoveAt(_columns.Count - 1);
        }
    }

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
    public DataGridViewEditMode EditMode
    {
        get => _editMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(DataGridViewEditMode));
            if (_editMode == value) return;
            _editMode = value;
            OnEditModeChanged(EventArgs.Empty);
        }
    }

    private DataGridViewEditMode _editMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

    [Category("Property Changed")]
    [Description("Occurs when the value of the DataGridView.EditMode property changes.")]
    public event EventHandler? EditModeChanged;

    protected virtual void OnEditModeChanged(EventArgs e)
    {
        // EditOnEnter edits the current cell at once; leaving it ends a pending edit.
        if (Focused && _editMode == DataGridViewEditMode.EditOnEnter && _currentCell != null && !IsCurrentCellInEditMode) BeginEditInternal(selectAll: true);
        else if (_editMode != DataGridViewEditMode.EditOnEnter && _editingControl != null && !IsCurrentCellDirty) EndEdit();
        EditModeChanged?.Invoke(this, e);
    }

    /// <summary>Whether the row header shows the pencil while the current row is being edited.</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    [Description("Indicates whether or not the editing glyph is visible in the row header of the cell being edited.")]
    public bool ShowEditingIcon { get; set; } = true;

    public void InvalidateCell(DataGridViewCell dataGridViewCell)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCell);
        if (dataGridViewCell.DataGridView != this) throw new ArgumentException("The cell does not belong to this DataGridView.");
        Invalidate();
    }

    public void InvalidateCell(int columnIndex, int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(columnIndex, _columns.Count);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowIndex, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(rowIndex, _rows.Count);
        Invalidate();
    }

    public void InvalidateColumn(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(columnIndex, _columns.Count);
        Invalidate();
    }

    public void InvalidateRow(int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(rowIndex, _rows.Count);
        Invalidate();
    }

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

    /// <summary>
    /// The bound list is <c>BindingContext[DataSource, DataMember]</c>, as in WinForms: so a DataSet with a table
    /// name, a relation as the DataMember (master-detail) and a BindingSource all resolve the way they do there,
    /// and the grid shares the current item with every other control bound to the same source. Without a
    /// binding context (a grid not yet in a form) there is nothing to show until it gets one.
    /// </summary>
    private void DetachDataSource()
    {
        if (_currencyManager != null)
        {
            _currencyManager.ListChanged -= OnBoundListChanged;
            _currencyManager.PositionChanged -= OnBoundPositionChanged;
            _currencyManager.MetaDataChanged -= OnBoundMetaDataChanged;
        }
        _currencyManager = null;
        _boundList = null;
        _boundProperties = null;
    }

    private void AttachDataSource()
    {
        var context = BindingContext;
        _currencyManager = _dataSource != null && _dataSource != DBNull.Value && context != null
            ? context[_dataSource, _dataMember] as CurrencyManager
            : null;
        _boundList = _currencyManager?.List;
        if (_currencyManager == null || _boundList == null)
        {
            _currencyManager = null;
            _boundList = null;
            if (_dataSource == null) _rows.Clear();
            InvalidateGridLayout();
            return;
        }

        _boundProperties = _currencyManager.GetItemProperties();
        if (AutoGenerateColumns) GenerateColumns();
        _currencyManager.ListChanged += OnBoundListChanged;
        _currencyManager.PositionChanged += OnBoundPositionChanged;
        _currencyManager.MetaDataChanged += OnBoundMetaDataChanged;
        RebuildBoundRows();
        OnBoundPositionChanged(_currencyManager, EventArgs.Empty);
    }

    protected override void OnBindingContextChanged(EventArgs e)
    {
        if (_dataSource != null)
        {
            DetachDataSource();
            AttachDataSource();
        }
        base.OnBindingContextChanged(e);
    }

    private void OnBoundMetaDataChanged(object? sender, EventArgs e)
    {
        if (_currencyManager == null) return;
        _boundProperties = _currencyManager.GetItemProperties();
        if (AutoGenerateColumns) GenerateColumns();
        RebuildBoundRows();
    }

    /// <summary>The data source's current item moved (another control, or code): the current row follows.</summary>
    private void OnBoundPositionChanged(object? sender, EventArgs e)
    {
        if (_currencyManager == null || _syncingPosition) return;
        int row = _currencyManager.Position;
        if (row < 0 || row >= _rows.Count || _currentCell?.RowIndex == row) return;
        int column = _currentCell?.ColumnIndex ?? -1;
        if (column < 0 || column >= _columns.Count || !_columns[column].Visible) column = FirstVisibleColumnIndex();
        if (column < 0) return;
        _syncingPosition = true;
        try
        {
            SetCurrentCell(column, row, validate: false);
        }
        finally
        {
            _syncingPosition = false;
        }
    }

    private int FirstVisibleColumnIndex()
    {
        for (int i = 0; i < _columns.Count; i++)
        {
            if (_columns[i].Visible) return i;
        }
        return -1;
    }

    private void GenerateColumns()
    {
        if (_boundProperties == null) return;
        _columns.Clear();
        foreach (PropertyDescriptor property in _boundProperties)
        {
            if (!property.IsBrowsable) continue;
            // A child list (a DataRelation of a DataTable, a collection property) is not a column (WinForms).
            if (typeof(IList).IsAssignableFrom(property.PropertyType)
                && !TypeDescriptor.GetConverter(typeof(Image)).CanConvertFrom(property.PropertyType)) continue;
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
            _columns.Add(new DataGridViewTextBoxColumn
            {
                Name = property.Name,
                HeaderText = property.DisplayName,
                DataPropertyName = property.Name,
                ValueType = property.PropertyType,
                ReadOnly = property.IsReadOnly,
            });
        }
    }

    private void OnBoundListChanged(object? sender, ListChangedEventArgs e)
    {
        // A related manager (a relation as the DataMember) swaps its list when the parent moves.
        if (_currencyManager != null) _boundList = _currencyManager.List;
        if (_inBindingUpdate) return;
        RebuildBoundRows();
    }

    private void RebuildBoundRows()
    {
        if (_boundList == null) return;
        var current = CurrentCellAddress;
        var rows = new List<DataGridViewRow>(_boundList.Count);
        for (int i = 0; i < _boundList.Count; i++) rows.Add(CreateRowFromTemplate());
        _rows.ResetTo(rows);
        _selection.RemoveWhere(c => c.Row >= rows.Count || c.Column >= _columns.Count);
        // The current cell stays at its address in the new rows (or goes, when the list got shorter).
        if (_currentCell != null && !IsCurrentCellInEditMode)
        {
            var moved = current.Y >= 0 && current.Y < rows.Count && current.X >= 0 && current.X < _columns.Count
                ? _rows[current.Y].Cells[current.X]
                : null;
            _currentCell = moved;
            if (moved == null) OnCurrentCellChanged(EventArgs.Empty);
        }
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

    [Category("Focus")]
    [Description("Occurs when the cell receives input focus, becoming the current cell in the DataGridView.")]
    public event DataGridViewCellEventHandler? CellEnter;

    [Category("Focus")]
    [Description("Occurs when a cell loses input focus and is no longer the current cell.")]
    public event DataGridViewCellEventHandler? CellLeave;

    [Category("Focus")]
    [Description("Occurs when a row receives input focus and becomes the current row.")]
    public event DataGridViewCellEventHandler? RowEnter;

    [Category("Focus")]
    [Description("Occurs when a row loses input focus and is no longer the current row.")]
    public event DataGridViewCellEventHandler? RowLeave;

    [Category("Focus")]
    [Description("Occurs when the cell is validating.")]
    public event DataGridViewCellValidatingEventHandler? CellValidating;

    [Category("Focus")]
    [Description("Occurs after the cell has finished validating.")]
    public event DataGridViewCellEventHandler? CellValidated;

    [Category("Focus")]
    [Description("Occurs when a row is validating.")]
    public event DataGridViewCellCancelEventHandler? RowValidating;

    [Category("Focus")]
    [Description("Occurs after a row has finished validating.")]
    public event DataGridViewCellEventHandler? RowValidated;

    [Category("Display")]
    [Description("Occurs when the user leaves edit mode, regardless of whether the value of the current cell has been modified.")]
    public event DataGridViewCellParsingEventHandler? CellParsing;

    [Category("Action")]
    [Description("Occurs when a control for editing a cell is showing.")]
    public event DataGridViewEditingControlShowingEventHandler? EditingControlShowing;

    [Category("Behavior")]
    [Description("Occurs when the state of a cell changes in relation to a change in its contents.")]
    public event EventHandler? CurrentCellDirtyStateChanged;

    [Category("Data")]
    [Description("Occurs when the DataGridView.VirtualMode property of the DataGridView control is true and the DataGridView needs to determine whether the current row has uncommitted changes.")]
    public event QuestionEventHandler? RowDirtyStateNeeded;

    [Category("Action")]
    [Description("Occurs when the DataGridView.VirtualMode property of a DataGridView control is true and a row edit should be canceled.")]
    public event QuestionEventHandler? CancelRowEdit;

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
    protected virtual void OnCellEnter(DataGridViewCellEventArgs e) => CellEnter?.Invoke(this, e);
    protected virtual void OnCellLeave(DataGridViewCellEventArgs e) => CellLeave?.Invoke(this, e);
    protected virtual void OnRowEnter(DataGridViewCellEventArgs e) => RowEnter?.Invoke(this, e);
    protected virtual void OnRowLeave(DataGridViewCellEventArgs e) => RowLeave?.Invoke(this, e);
    protected virtual void OnCellValidating(DataGridViewCellValidatingEventArgs e) => CellValidating?.Invoke(this, e);
    protected virtual void OnCellValidated(DataGridViewCellEventArgs e) => CellValidated?.Invoke(this, e);
    protected virtual void OnRowValidating(DataGridViewCellCancelEventArgs e) => RowValidating?.Invoke(this, e);
    protected virtual void OnRowValidated(DataGridViewCellEventArgs e) => RowValidated?.Invoke(this, e);
    protected virtual void OnCellParsing(DataGridViewCellParsingEventArgs e) => CellParsing?.Invoke(this, e);
    protected virtual void OnEditingControlShowing(DataGridViewEditingControlShowingEventArgs e) => EditingControlShowing?.Invoke(this, e);
    protected virtual void OnCurrentCellDirtyStateChanged(EventArgs e) => CurrentCellDirtyStateChanged?.Invoke(this, e);
    protected virtual void OnRowDirtyStateNeeded(QuestionEventArgs e) => RowDirtyStateNeeded?.Invoke(this, e);
    protected virtual void OnCancelRowEdit(QuestionEventArgs e) => CancelRowEdit?.Invoke(this, e);

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
        if (_editingControl != null) PositionEditingControl(setLocation: true, setSize: true, setFocus: false);
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
        PositionEditingControl(setLocation: true, setSize: true, setFocus: false);
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
        set
        {
            if (ReferenceEquals(value, _currentCell)) return;
            if (value != null && value.DataGridView != this) throw new ArgumentException("The cell does not belong to this DataGridView.");
            // WinForms validates here too: ScrollIntoView commits the edit through CommitEditForOperation (CellLeave,
            // CellValidating, CellValidated, RowValidating when the row changes) before the cell moves.
            if (!SetCurrentCell(value))
            {
                throw new InvalidOperationException("Operation did not succeed because the program cannot commit or quit a cell value change.");
            }
        }
    }

    [Browsable(false)]
    public DataGridViewRow? CurrentRow => _currentCell?.OwningRow;

    [Browsable(false)]
    public Point CurrentCellAddress => _currentCell != null ? new Point(_currentCell.ColumnIndex, _currentCell.RowIndex) : new Point(-1, -1);

    /// <summary>
    /// Moves the current cell as WinForms' SetCurrentCellAddressCore: the edit of the old cell is committed (with
    /// <paramref name="validate"/>: CellLeave, RowLeave, CellValidating/CellValidated, then RowValidating/
    /// RowValidated when the row changes), then RowEnter, CurrentCellChanged, CellEnter on the new one. A cancelled
    /// validation (or a failed commit) leaves the current cell where it was and returns false.
    /// </summary>
    private bool SetCurrentCell(DataGridViewCell? cell, bool validate = true)
    {
        if (ReferenceEquals(_currentCell, cell))
        {
            BeginEditOnEnter();
            return true;
        }
        var old = _currentCell;
        if (old != null && old.RowIndex >= 0 && old.ColumnIndex >= 0)
        {
            bool rowChange = cell == null || cell.RowIndex != old.RowIndex;
            if (!EndEditCore(DataGridViewDataErrorContexts.Parsing | DataGridViewDataErrorContexts.Commit | DataGridViewDataErrorContexts.CurrentCellChange,
                    validate ? ValidateCell.Always : ValidateCell.Never, keepFocus: EditMode != DataGridViewEditMode.EditOnEnter,
                    fireLeave: validate, rowChange: rowChange))
            {
                return false;
            }
            if (!ReferenceEquals(_currentCell, old)) return false; // a handler moved it
            if (rowChange && validate)
            {
                var rowValidating = new DataGridViewCellCancelEventArgs(old.ColumnIndex, old.RowIndex);
                OnRowValidating(rowValidating);
                if (rowValidating.Cancel)
                {
                    OnRowEnter(new DataGridViewCellEventArgs(old.ColumnIndex, old.RowIndex));
                    OnCellEnter(new DataGridViewCellEventArgs(old.ColumnIndex, old.RowIndex));
                    return false;
                }
                OnRowValidated(new DataGridViewCellEventArgs(old.ColumnIndex, old.RowIndex));
            }
            if (rowChange) _currentRowDirty = false;
        }
        if (cell != null && (old == null || old.RowIndex != cell.RowIndex)) OnRowEnter(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));

        _currentCell = cell;
        if (cell != null) FirstDisplayedCell(cell.ColumnIndex, cell.RowIndex);
        // The bound source's current item follows the current row (and so does every control bound to it).
        if (cell != null && !_syncingPosition && _currencyManager != null && cell.RowIndex >= 0
            && cell.RowIndex < _currencyManager.Count && _currencyManager.Position != cell.RowIndex)
        {
            _syncingPosition = true;
            try
            {
                _currencyManager.Position = cell.RowIndex;
            }
            finally
            {
                _syncingPosition = false;
            }
        }
        OnCurrentCellChanged(EventArgs.Empty);
        if (cell != null && ReferenceEquals(_currentCell, cell))
        {
            OnCellEnter(new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex));
            BeginEditOnEnter();
        }
        Invalidate();
        return true;
    }

    /// <summary>EditOnEnter edits every cell that becomes current; a cell that edits itself (the check box) is always in edit mode.</summary>
    private void BeginEditOnEnter()
    {
        var cell = _currentCell;
        if (cell == null || IsCurrentCellInEditMode || !ContainsFocus || cell.ReadOnly) return;
        if (EditMode == DataGridViewEditMode.EditOnEnter || (EditMode != DataGridViewEditMode.EditProgrammatically && cell.EditType == null))
        {
            BeginEditInternal(selectAll: true);
        }
    }

    private bool SetCurrentCell(int column, int row, bool validate = true)
    {
        if (column < 0 || column >= _columns.Count || row < 0 || row >= _rows.Count) return SetCurrentCell(null, validate);
        return SetCurrentCell(_rows[row].Cells[column], validate);
    }

    // --- editing ------------------------------------------------------------------------------------------------
    // The WinForms editing model (DataGridView.Methods.cs in dotnet/winforms): a cell edits through a control of
    // its EditType (an IDataGridViewEditingControl hosted in EditingPanel) or edits itself (IDataGridViewEditingCell,
    // the check box). Typing makes the current cell dirty; the edited value reaches the cell on commit - leaving the
    // cell, EndEdit or CommitEdit - through CellValidating, CellParsing, ParseFormattedValue and CellValidated, and a
    // failed parse raises DataError.

    private Control? _editingControl;
    private Control? _latestEditingControl;
    private Panel? _editingPanel;
    private bool _editingCellInEditMode;
    private bool _currentCellDirty;
    private bool _currentRowDirty;
    private bool _ignoringEditingChanges;
    private bool _inBeginEdit;
    private bool _inCellValidating;
    private object? _uneditedFormattedValue;

    /// <summary>The control editing the current cell, or null.</summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Control? EditingControl => _editingControl;

    /// <summary>The panel that holds the editing control over the cell.</summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Panel EditingPanel => _editingPanel ??= new Panel { Visible = false, TabStop = false };

    internal bool IsCellInEditMode(int columnIndex, int rowIndex) =>
        IsCurrentCellInEditMode && _currentCell != null && _currentCell.ColumnIndex == columnIndex && _currentCell.RowIndex == rowIndex;

    [Browsable(false)]
    public bool IsCurrentCellInEditMode => _editingControl != null || _editingCellInEditMode;

    [Browsable(false)]
    public bool IsCurrentCellDirty => _currentCellDirty;

    /// <summary>Whether the current row has changes not committed yet; VirtualMode asks through RowDirtyStateNeeded.</summary>
    [Browsable(false)]
    public bool IsCurrentRowDirty
    {
        get
        {
            if (!VirtualMode) return _currentRowDirty || _currentCellDirty;
            var e = new QuestionEventArgs(_currentRowDirty || _currentCellDirty);
            OnRowDirtyStateNeeded(e);
            return e.Response;
        }
    }

    private bool IsCurrentCellDirtyInternal
    {
        set
        {
            if (_currentCellDirty == value) return;
            _currentCellDirty = value;
            OnCurrentCellDirtyStateChanged(EventArgs.Empty);
        }
    }

    /// <summary>The editing control (or editing cell) reports a change: the current cell becomes dirty.</summary>
    public virtual void NotifyCurrentCellDirty(bool dirty)
    {
        if (_ignoringEditingChanges) return;
        IsCurrentCellDirtyInternal = dirty;
        if (dirty && _editingControl is IDataGridViewEditingControl { RepositionEditingControlOnValueChange: true })
        {
            PositionEditingControl(setLocation: true, setSize: true, setFocus: false);
        }
    }

    public virtual bool BeginEdit(bool selectAll)
    {
        if (_currentCell == null) throw new InvalidOperationException("Operation cannot be performed because there is no current cell.");
        return IsCurrentCellInEditMode || BeginEditInternal(selectAll);
    }

    private bool BeginEditInternal(bool selectAll)
    {
        if (_inBeginEdit) throw new InvalidOperationException("BeginEdit cannot be called while in the CellBeginEdit event.");
        var cell = _currentCell;
        if (cell == null || cell.ReadOnly) return false;
        var editType = cell.EditType;
        if (editType == null && cell is not IDataGridViewEditingCell) return false;

        _inBeginEdit = true;
        try
        {
            var args = new DataGridViewCellCancelEventArgs(cell.ColumnIndex, cell.RowIndex);
            OnCellBeginEdit(args);
            if (args.Cancel) return false;
            if (_currentCell == null) return false;
            if (!ReferenceEquals(_currentCell, cell))
            {
                // The handler moved the current cell: everything checked above is about another cell.
                cell = _currentCell;
                editType = cell.EditType;
                if (cell.ReadOnly || (editType == null && cell is not IDataGridViewEditingCell)) return false;
            }

            var style = cell.InheritedStyle;
            if (editType == null)
            {
                _editingCellInEditMode = true;
                InitializeEditingCellValue(cell);
                ((IDataGridViewEditingCell)cell).PrepareEditingCellForEdit(selectAll);
                Invalidate();
                return true;
            }

            if (!typeof(Control).IsAssignableFrom(editType) || !typeof(IDataGridViewEditingControl).IsAssignableFrom(editType))
            {
                throw new InvalidCastException("The editing control must derive from Control and implement IDataGridViewEditingControl.");
            }
            if (_latestEditingControl != null && editType.IsInstanceOfType(_latestEditingControl) && !_latestEditingControl.GetType().IsSubclassOf(editType))
            {
                _editingControl = _latestEditingControl;
            }
            else
            {
                _editingControl = (Control)Activator.CreateInstance(editType)!;
                ((IDataGridViewEditingControl)_editingControl).EditingControlDataGridView = this;
                _latestEditingControl?.Dispose();
                _latestEditingControl = null;
            }
            var editing = (IDataGridViewEditingControl)_editingControl;
            editing.EditingControlRowIndex = cell.RowIndex;
            if (!InitializeEditingControlValue(ref style, cell)) return false;

            var showing = new DataGridViewEditingControlShowingEventArgs(_editingControl, style);
            OnEditingControlShowing(showing);
            if (_editingControl == null) return false;
            EditingPanel.BackColor = showing.CellStyle.BackColor;
            editing.ApplyCellStyleToEditingControl(showing.CellStyle);
            PositionEditingControl(setLocation: true, setSize: true, setFocus: true);
            if (_editingControl == null) return false;
            editing.PrepareEditingControlForEdit(selectAll);
            Invalidate();
            return true;
        }
        finally
        {
            _inBeginEdit = false;
        }
    }

    /// <summary>The value the edit starts from, remembered to restore it on cancel.</summary>
    private bool InitializeEditingControlValue(ref DataGridViewCellStyle style, DataGridViewCell cell)
    {
        object? initial = FormattedValueOf(cell, cell.RowIndex, ref style, DataGridViewDataErrorContexts.Formatting);
        _ignoringEditingChanges = true;
        try
        {
            cell.InitializeEditingControl(cell.RowIndex, initial, style);
            ((IDataGridViewEditingControl)_editingControl!).EditingControlValueChanged = false;
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            var error = new DataGridViewDataErrorEventArgs(ex, cell.ColumnIndex, cell.RowIndex, DataGridViewDataErrorContexts.InitialValueRestoration);
            OnDataErrorInternal(error);
            if (error.ThrowException) throw error.Exception!;
            return !error.Cancel;
        }
        finally
        {
            _ignoringEditingChanges = false;
        }
        _uneditedFormattedValue = initial;
        return true;
    }

    private void InitializeEditingCellValue(DataGridViewCell cell)
    {
        var style = cell.InheritedStyle;
        _uneditedFormattedValue = FormattedValueOf(cell, cell.RowIndex, ref style, DataGridViewDataErrorContexts.Formatting);
        _ignoringEditingChanges = true;
        try
        {
            var editingCell = (IDataGridViewEditingCell)cell;
            if (!Equals(editingCell.GetEditingCellFormattedValue(DataGridViewDataErrorContexts.Formatting), _uneditedFormattedValue))
            {
                editingCell.EditingCellFormattedValue = _uneditedFormattedValue;
            }
            editingCell.EditingCellValueChanged = false;
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            var error = new DataGridViewDataErrorEventArgs(ex, cell.ColumnIndex, cell.RowIndex, DataGridViewDataErrorContexts.InitialValueRestoration);
            OnDataErrorInternal(error);
            if (error.ThrowException) throw error.Exception!;
        }
        finally
        {
            _ignoringEditingChanges = false;
        }
    }

    /// <summary>A cell's value: the cell's own, or in VirtualMode what CellValueNeeded supplies.</summary>
    internal object? ValueOf(DataGridViewCell cell, int rowIndex)
    {
        if (VirtualMode && rowIndex >= 0 && cell.ColumnIndex >= 0 && !IsBound)
        {
            var needed = new DataGridViewCellValueEventArgs(cell.ColumnIndex, rowIndex);
            OnCellValueNeeded(needed);
            return needed.Value;
        }
        return cell.Value;
    }

    /// <summary>
    /// A cell's formatted value as WinForms makes it: CellFormatting gets the raw value; unless it applied its own
    /// formatting, the cell formats what the handler left (and a string stays as it is).
    /// </summary>
    internal object? FormattedValueOf(DataGridViewCell cell, int rowIndex, ref DataGridViewCellStyle style, DataGridViewDataErrorContexts context) =>
        FormattedValueOf(cell, ValueOf(cell, rowIndex), rowIndex, ref style, context);

    internal object? FormattedValueOf(DataGridViewCell cell, object? value, int rowIndex, ref DataGridViewCellStyle style, DataGridViewDataErrorContexts context) =>
        cell.GetFormattedValueInternal(value, rowIndex, ref style, context);

    internal DataGridViewCellFormattingEventArgs RaiseCellFormatting(int columnIndex, int rowIndex, object? value, Type? desiredType, DataGridViewCellStyle style)
    {
        var e = new DataGridViewCellFormattingEventArgs(columnIndex, rowIndex, value, desiredType, style);
        OnCellFormatting(e);
        return e;
    }

    internal void RaiseDataError(DataGridViewDataErrorEventArgs e) => OnDataErrorInternal(e);

    internal void RaiseCellValuePushed(int columnIndex, int rowIndex, object? value)
    {
        var e = new DataGridViewCellValueEventArgs(columnIndex, rowIndex) { Value = value };
        OnCellValuePushed(e);
    }

    /// <summary>Puts the editing control over the current cell (and hides it while the cell is scrolled away).</summary>
    private void PositionEditingControl(bool setLocation, bool setSize, bool setFocus)
    {
        if (_editingControl == null || _currentCell == null) return;
        var cell = _currentCell;
        var cellBounds = GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, false);
        var clip = Rectangle.Intersect(cellBounds, CellsRectangle);
        if (clip.Width <= 0 || clip.Height <= 0)
        {
            // Scrolled out of sight: the panel stays (it may hold the focus), out of the way.
            EditingPanel.Bounds = new Rectangle(-10000, -10000, Math.Max(1, cellBounds.Width), Math.Max(1, cellBounds.Height));
            return;
        }
        cell.PositionEditingControl(setLocation, setSize, cellBounds, clip, cell.InheritedStyle,
            singleVerticalBorderAdded: false, singleHorizontalBorderAdded: false,
            isFirstDisplayedColumn: cell.ColumnIndex == FirstVisibleColumnIndex(), isFirstDisplayedRow: cell.RowIndex == 0);
        EditingPanel.Visible = true;
        _editingControl.Visible = true;
        if (setFocus && _editingControl.CanFocus) _editingControl.Focus();
    }

    public bool EndEdit() => EndEdit(DataGridViewDataErrorContexts.Parsing | DataGridViewDataErrorContexts.Commit);

    public bool EndEdit(DataGridViewDataErrorContexts context) => EditMode == DataGridViewEditMode.EditOnEnter
        ? CommitEdit(context)
        : EndEditCore(context, ValidateCell.Never, keepFocus: true);

    /// <summary>Pushes the edited value into the cell and stays in edit mode.</summary>
    public bool CommitEdit(DataGridViewDataErrorContexts context)
    {
        if (!IsCurrentCellInEditMode) return true;
        var error = CommitEditCore(context, ValidateCell.Never, fireLeave: false, rowChange: false);
        if (error != null)
        {
            if (error.ThrowException) throw error.Exception!;
            if (error.Cancel) return false;
        }
        return true;
    }

    private enum ValidateCell { Never, Always, WhenChanged }

    /// <summary>Commits the edit (see CommitEditCore) and leaves edit mode; false keeps the cell in edit mode.</summary>
    private bool EndEditCore(DataGridViewDataErrorContexts context, ValidateCell validate, bool keepFocus, bool fireLeave = false, bool rowChange = false)
    {
        var cell = _currentCell;
        if (cell == null) return true;
        var error = CommitEditCore(context, validate, fireLeave, rowChange);
        if (error != null)
        {
            if (error.ThrowException) throw error.Exception!;
            if (error.Cancel) return false;
            // The DataError handler said: give up the edit and restore the old value.
            CancelEditPrivate();
        }
        if (!IsCurrentCellInEditMode || !ReferenceEquals(_currentCell, cell)) return true;

        int column = cell.ColumnIndex, row = cell.RowIndex;
        if (_editingControl != null)
        {
            bool hadFocus = _editingControl.ContainsFocus;
            _ignoringEditingChanges = true;
            try
            {
                cell.DetachEditingControl();
            }
            finally
            {
                _ignoringEditingChanges = false;
            }
            _latestEditingControl = _editingControl;
            _editingControl = null;
            if (keepFocus && hadFocus && CanFocus) Focus();
        }
        else
        {
            _editingCellInEditMode = false;
        }
        Invalidate();
        if (column >= 0 && column < _columns.Count && row >= 0 && row < _rows.Count)
        {
            OnCellEndEdit(new DataGridViewCellEventArgs(column, row));
        }
        return true;
    }

    /// <summary>
    /// WinForms' CommitEdit: with <paramref name="validate"/> Always, CellLeave (and RowLeave), then CellValidating
    /// - cancelled, the cell stays and gets CellEnter again. A dirty cell then gets its edited formatted value parsed
    /// (CellParsing, ParseFormattedValue) and stored; a failure raises DataError. CellValidated closes a validation.
    /// </summary>
    private DataGridViewDataErrorEventArgs? CommitEditCore(DataGridViewDataErrorContexts context, ValidateCell validate, bool fireLeave, bool rowChange)
    {
        var cell = _currentCell;
        if (cell == null) return null;
        int column = cell.ColumnIndex, row = cell.RowIndex;
        if (validate == ValidateCell.Always)
        {
            if (fireLeave)
            {
                OnCellLeave(new DataGridViewCellEventArgs(column, row));
                if (rowChange) OnRowLeave(new DataGridViewCellEventArgs(column, row));
            }
            if (_currentCell == null) return null;
            if (RaiseCellValidating(cell, context))
            {
                if (fireLeave)
                {
                    if (rowChange) OnRowEnter(new DataGridViewCellEventArgs(column, row));
                    OnCellEnter(new DataGridViewCellEventArgs(column, row));
                }
                return new DataGridViewDataErrorEventArgs(null, column, row, context) { Cancel = true };
            }
            if (!IsCurrentCellInEditMode || !IsCurrentCellDirty) OnCellValidated(new DataGridViewCellEventArgs(column, row));
        }

        if (_currentCell == null || !IsCurrentCellInEditMode) return null;
        if (!IsCurrentCellDirty) return null;

        if (validate == ValidateCell.WhenChanged && RaiseCellValidating(cell, context))
        {
            return new DataGridViewDataErrorEventArgs(null, column, row, context) { Cancel = true };
        }

        object? formattedValue = _editingControl is IDataGridViewEditingControl editing
            ? editing.GetEditingControlFormattedValue(context)
            : ((IDataGridViewEditingCell)cell).GetEditingCellFormattedValue(context);
        if (!PushFormattedValue(cell, formattedValue, out var exception))
        {
            var error = new DataGridViewDataErrorEventArgs(exception, column, row, context) { Cancel = true };
            OnDataErrorInternal(error);
            return error;
        }
        if (!IsCurrentCellInEditMode) return null;
        _uneditedFormattedValue = formattedValue;
        if (_editingControl is IDataGridViewEditingControl ec) ec.EditingControlValueChanged = false;
        else if (cell is IDataGridViewEditingCell editingCell) editingCell.EditingCellValueChanged = false;
        IsCurrentCellDirtyInternal = false;
        _currentRowDirty = true;
        if (validate is ValidateCell.Always or ValidateCell.WhenChanged) OnCellValidated(new DataGridViewCellEventArgs(column, row));
        return null;
    }

    /// <summary>CellValidating with the edited formatted value; true when a handler cancelled.</summary>
    private bool RaiseCellValidating(DataGridViewCell cell, DataGridViewDataErrorContexts context)
    {
        if (_inCellValidating) return false;
        var e = new DataGridViewCellValidatingEventArgs(cell.ColumnIndex, cell.RowIndex, cell.GetEditedFormattedValue(cell.RowIndex, context));
        _inCellValidating = true;
        try
        {
            OnCellValidating(e);
        }
        finally
        {
            _inCellValidating = false;
        }
        return e.Cancel;
    }

    /// <summary>CellParsing first; unless it parsed the value, the cell's ParseFormattedValue. Then the value is stored.</summary>
    private bool PushFormattedValue(DataGridViewCell cell, object? formattedValue, out Exception? exception)
    {
        exception = null;
        var style = cell.InheritedStyle;
        var parsing = new DataGridViewCellParsingEventArgs(cell.RowIndex, cell.ColumnIndex, formattedValue, cell.ValueType, style);
        OnCellParsing(parsing);
        object? value;
        if (parsing.ParsingApplied && parsing.Value != null && cell.ValueType != null && cell.ValueType.IsInstanceOfType(parsing.Value))
        {
            value = parsing.Value;
        }
        else
        {
            try
            {
                value = cell.ParseFormattedValue(formattedValue, parsing.InheritedCellStyle ?? style, null, null);
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
                exception = ex;
                return false;
            }
        }
        return cell.SetValueInternal(cell.RowIndex, value);
    }

    /// <summary>Discards the edit: the editing control (or cell) gets the value it started from, and stays in edit mode.</summary>
    public bool CancelEdit() => CancelEdit(endEdit: false);

    private bool CancelEdit(bool endEdit)
    {
        if (_currentCell == null) return true;
        CancelEditPrivate();
        if (!IsCurrentCellInEditMode) return true;
        if (endEdit && EditMode != DataGridViewEditMode.EditOnEnter && _editingControl != null)
        {
            return EndEditCore(DataGridViewDataErrorContexts.Parsing | DataGridViewDataErrorContexts.InitialValueRestoration, ValidateCell.Never, keepFocus: true);
        }
        _ignoringEditingChanges = true;
        try
        {
            if (_editingControl is IDataGridViewEditingControl editing)
            {
                editing.EditingControlFormattedValue = _uneditedFormattedValue;
                editing.EditingControlValueChanged = false;
            }
            else if (_currentCell is IDataGridViewEditingCell editingCell)
            {
                editingCell.EditingCellFormattedValue = _uneditedFormattedValue;
                editingCell.EditingCellValueChanged = false;
            }
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            var error = new DataGridViewDataErrorEventArgs(ex, _currentCell.ColumnIndex, _currentCell.RowIndex, DataGridViewDataErrorContexts.InitialValueRestoration);
            OnDataErrorInternal(error);
            if (error.ThrowException) throw error.Exception!;
        }
        finally
        {
            _ignoringEditingChanges = false;
        }
        if (_editingControl is IDataGridViewEditingControl prepared) prepared.PrepareEditingControlForEdit(selectAll: true);
        else if (_currentCell is IDataGridViewEditingCell preparedCell) preparedCell.PrepareEditingCellForEdit(selectAll: true);
        Invalidate();
        return true;
    }

    private void CancelEditPrivate()
    {
        if (VirtualMode && _currentRowDirty && !_currentCellDirty)
        {
            // Escape on a row whose cells were committed: VirtualMode gives the row's edits back (WinForms' CancelRowEdit).
            _currentRowDirty = false;
            OnCancelRowEdit(new QuestionEventArgs(false));
        }
        if (!IsCurrentCellInEditMode) return;
        if (_editingControl is IDataGridViewEditingControl editing) editing.EditingControlValueChanged = false;
        else if (_currentCell is IDataGridViewEditingCell editingCell) editingCell.EditingCellValueChanged = false;
        IsCurrentCellDirtyInternal = false;
    }

    /// <summary>Reloads the current cell's value into the editing control, discarding what was typed.</summary>
    public bool RefreshEdit()
    {
        var cell = _currentCell;
        if (cell == null || !IsCurrentCellInEditMode) return true;
        var style = cell.InheritedStyle;
        if (_editingControl is IDataGridViewEditingControl editing)
        {
            if (!InitializeEditingControlValue(ref style, cell)) return false;
            if (editing.RepositionEditingControlOnValueChange) PositionEditingControl(setLocation: true, setSize: true, setFocus: false);
            editing.PrepareEditingControlForEdit(selectAll: true);
            editing.EditingControlValueChanged = false;
        }
        else
        {
            InitializeEditingCellValue(cell);
            ((IDataGridViewEditingCell)cell).PrepareEditingCellForEdit(selectAll: true);
        }
        IsCurrentCellDirtyInternal = false;
        Invalidate();
        return true;
    }

    /// <summary>
    /// The focus leaves the grid (or its editing control) for another control: the edit is committed with validation
    /// (CellValidating, CellValidated), then the row validates; a cancel keeps the focus here.
    /// </summary>
    protected override void OnValidating(CancelEventArgs e)
    {
        var cell = _currentCell;
        if (cell != null && cell.RowIndex >= 0)
        {
            if (!EndEditCore(DataGridViewDataErrorContexts.Parsing | DataGridViewDataErrorContexts.Commit | DataGridViewDataErrorContexts.LeaveControl,
                    ValidateCell.Always, keepFocus: false))
            {
                e.Cancel = true;
                return;
            }
            if (_currentCell != null)
            {
                var rowValidating = new DataGridViewCellCancelEventArgs(_currentCell.ColumnIndex, _currentCell.RowIndex);
                OnRowValidating(rowValidating);
                if (rowValidating.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                OnRowValidated(new DataGridViewCellEventArgs(_currentCell.ColumnIndex, _currentCell.RowIndex));
            }
        }
        base.OnValidating(e);
    }

    /// <summary>DataError; WinForms shows a message box when nobody handles it - NetForms leaves it to the handler.</summary>
    private void OnDataErrorInternal(DataGridViewDataErrorEventArgs e) => OnDataError(e);

    internal void OnMouseWheelInternal(MouseEventArgs e) => OnMouseWheel(e);

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
                    if (!SetCurrentCell(0, hit.RowIndex)) break;
                }
                break;

            case DataGridViewHitTestType.Cell:
                {
                    var cell = _rows[hit.RowIndex].Cells[hit.ColumnIndex];
                    bool control = (ModifierKeys & Keys.Control) != 0;
                    bool shift = (ModifierKeys & Keys.Shift) != 0;
                    bool wasCurrent = ReferenceEquals(_currentCell, cell);
                    // The old cell's edit is committed and validated first; a cancelled validation keeps everything.
                    if (!SetCurrentCell(cell)) break;

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

                    var args = new DataGridViewCellEventArgs(hit.ColumnIndex, hit.RowIndex);
                    OnCellClick(args);
                    OnCellMouseClick(new DataGridViewCellMouseEventArgs(hit.ColumnIndex, hit.RowIndex,
                        e.X - GetCellDisplayRectangle(hit.ColumnIndex, hit.RowIndex, false).X,
                        e.Y - GetCellDisplayRectangle(hit.ColumnIndex, hit.RowIndex, false).Y, e));

                    if (IsContentClick(cell, e.Location))
                    {
                        cell.OnContentClickInternal(args);
                        OnCellContentClick(args);
                    }
                    else if (wasCurrent && !IsCurrentCellInEditMode && EditMode != DataGridViewEditMode.EditProgrammatically && cell.EditType != null)
                    {
                        // A click on the cell that already was current edits it (WinForms' DataGridViewTextBoxCell.OnMouseClick).
                        BeginEditInternal(selectAll: true);
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
            if (_currentCell != null && !IsCurrentCellInEditMode && EditMode != DataGridViewEditMode.EditProgrammatically) BeginEditInternal(selectAll: true);
        }
        base.OnDoubleClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (ProcessDataGridViewKey(e))
        {
            e.Handled = true;
            return;
        }
        // EditOnKeystroke: a key that the current cell takes as input starts the edit; the character that follows
        // goes to the editing control, which has the focus by then.
        var cell = _currentCell;
        if (cell != null && !IsCurrentCellInEditMode && !cell.ReadOnly
            && EditMode is DataGridViewEditMode.EditOnKeystroke or DataGridViewEditMode.EditOnKeystrokeOrF2
            && cell.KeyEntersEditMode(e))
        {
            if (BeginEditInternal(selectAll: true)) e.Handled = _editingControl != null;
        }
    }

    /// <summary>The Enter, Escape and Tab keys of the grid - and of its editing control, whose dialog keys come here.</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Enter:
                if (ProcessEnterKey(keyData)) return true;
                break;
            case Keys.Escape:
                if (ProcessEscapeKey(keyData)) return true;
                break;
            case Keys.Tab:
                if (!StandardTab && ProcessTabKey(keyData)) return true;
                break;
        }
        return base.ProcessDialogKey(keyData);
    }

    /// <summary>
    /// The keys typed into the editing control: the ones the grid navigates with (arrows, Home/End, Page Up/Down,
    /// Enter, Escape, Tab, F2, Delete, Space) go to the grid unless the control wants them (EditingControlWantsInputKey).
    /// </summary>
    protected override bool ProcessKeyPreview(ref Message m)
    {
        if (m.Msg == Message.WM_KEYDOWN && _editingControl is IDataGridViewEditingControl editing && _editingControl.ContainsFocus)
        {
            var e = new KeyEventArgs((Keys)(int)m.WParam | ModifierKeys);
            bool gridWantsInputKey = e.KeyCode is Keys.Delete or Keys.Down or Keys.End or Keys.Enter or Keys.Escape or Keys.F2 or Keys.F3
                or Keys.Home or Keys.Left or Keys.Next or Keys.Prior or Keys.Right or Keys.Space or Keys.Tab or Keys.Up;
            if (gridWantsInputKey && !editing.EditingControlWantsInputKey(e.KeyData, gridWantsInputKey) && ProcessDataGridViewKey(e))
            {
                return true;
            }
        }
        return base.ProcessKeyPreview(ref m);
    }

    /// <summary>The grid's keyboard: moving the current cell, F2, Space and Ctrl+A. True when the key was used.</summary>
    protected bool ProcessDataGridViewKey(KeyEventArgs e)
    {
        EnsureLayout();
        switch (e.KeyCode)
        {
            case Keys.Up:
            case Keys.Down:
            case Keys.Left:
            case Keys.Right:
            case Keys.Home:
            case Keys.End:
            case Keys.PageUp:
            case Keys.PageDown:
                return MoveCurrentCell(e.KeyCode, e.Modifiers);
            case Keys.Enter:
                return ProcessEnterKey(e.KeyData);
            case Keys.Escape:
                return ProcessEscapeKey(e.KeyData);
            case Keys.Tab:
                return ProcessTabKey(e.KeyData);
            case Keys.F2:
                return ProcessF2Key(e.KeyData);
            case Keys.Space when (e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift)) == 0
                && _currentCell is DataGridViewCheckBoxCell or DataGridViewButtonCell or DataGridViewLinkCell:
                {
                    // WinForms raises these from the cell's OnKeyUp; the check box toggles its edited value.
                    var args = new DataGridViewCellEventArgs(_currentCell.ColumnIndex, _currentCell.RowIndex);
                    OnCellClick(args);
                    _currentCell.OnContentClickInternal(args);
                    OnCellContentClick(args);
                    return true;
                }
            case Keys.A when (e.Modifiers & Keys.Control) != 0 && MultiSelect && !IsCurrentCellInEditMode:
                SelectAll();
                return true;
            case Keys.Delete:
                if (!IsCurrentCellInEditMode && AllowUserToDeleteRows && !IsBound && SelectedRows.Count > 0)
                {
                    DeleteSelectedRows();
                    return true;
                }
                return false;
        }
        return false;
    }

    /// <summary>F2 starts editing with the caret at the end of the text.</summary>
    protected bool ProcessF2Key(Keys keyData)
    {
        if (_currentCell == null || IsCurrentCellInEditMode || (keyData & Keys.Modifiers) != 0) return false;
        if (EditMode is not (DataGridViewEditMode.EditOnF2 or DataGridViewEditMode.EditOnKeystrokeOrF2)) return false;
        BeginEditInternal(selectAll: false);
        return true;
    }

    /// <summary>Enter commits the edit and moves to the cell below (Ctrl+Enter commits and stays).</summary>
    protected bool ProcessEnterKey(Keys keyData)
    {
        if (_currentCell == null) return false;
        if (IsCurrentCellInEditMode
            && !EndEditCore(DataGridViewDataErrorContexts.Parsing | DataGridViewDataErrorContexts.Commit, ValidateCell.WhenChanged, keepFocus: true))
        {
            return true; // the value did not commit: the cell stays in edit mode
        }
        if ((keyData & Keys.Control) == 0 && _currentCell != null && _currentCell.RowIndex < _rows.Count - 1) MoveCurrentCell(Keys.Down, Keys.None);
        return true;
    }

    /// <summary>Escape gives up the edit: the old value comes back and edit mode ends.</summary>
    protected bool ProcessEscapeKey(Keys keyData)
    {
        if (!IsCurrentCellInEditMode) return false;
        CancelEdit(endEdit: true);
        if (CanFocus && !Focused) Focus();
        return true;
    }

    /// <summary>Tab moves to the next cell (Shift+Tab to the previous), wrapping to the next row.</summary>
    protected bool ProcessTabKey(Keys keyData)
    {
        if (_columns.Count == 0 || _rows.Count == 0 || (keyData & Keys.Control) != 0) return false;
        var address = CurrentCellAddress;
        int column = Math.Max(0, address.X), row = Math.Max(0, address.Y);
        column += (keyData & Keys.Shift) != 0 ? -1 : 1;
        if (column >= _columns.Count)
        {
            if (row >= _rows.Count - 1) return false; // past the last cell: the focus leaves the grid
            column = 0;
            row++;
        }
        else if (column < 0)
        {
            if (row == 0) return false;
            column = _columns.Count - 1;
            row--;
        }
        SelectAndMove(column, row, Keys.None);
        return true;
    }

    private bool MoveCurrentCell(Keys key, Keys modifiers)
    {
        if (_columns.Count == 0 || _rows.Count == 0) return false;
        var address = CurrentCellAddress;
        int column = address.X, row = address.Y;
        if (column < 0) column = 0;
        if (row < 0) row = 0;
        bool ctrl = (modifiers & Keys.Control) != 0;
        switch (key)
        {
            case Keys.Up: row--; break;
            case Keys.Down: row++; break;
            case Keys.Left: column--; break;
            case Keys.Right: column++; break;
            case Keys.Home: column = 0; if (ctrl) row = 0; break;
            case Keys.End: column = _columns.Count - 1; if (ctrl) row = _rows.Count - 1; break;
            case Keys.PageUp: row -= Math.Max(1, CellsRectangle.Height / Math.Max(1, RowTemplate.Height)); break;
            case Keys.PageDown: row += Math.Max(1, CellsRectangle.Height / Math.Max(1, RowTemplate.Height)); break;
        }
        SelectAndMove(Math.Clamp(column, 0, _columns.Count - 1), Math.Clamp(row, 0, _rows.Count - 1), modifiers);
        return true;
    }

    private void SelectAndMove(int column, int row, Keys modifiers)
    {
        if (!SetCurrentCell(column, row)) return;
        if (MultiSelect && (modifiers & Keys.Shift) != 0) SelectRangeTo(column, row);
        else
        {
            SelectOnlyCell(column, row);
            _anchor = (column, row);
        }
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

        object? value = ValueOf(cell, rowIndex);
        object? formatted = FormattedValueOf(cell, value, rowIndex, ref style, DataGridViewDataErrorContexts.Display);
        // A cell editing itself (the check box) shows the edited value until it is committed.
        if (cell is IDataGridViewEditingCell editingCell && _editingCellInEditMode && ReferenceEquals(cell, _currentCell))
        {
            formatted = editingCell.GetEditingCellFormattedValue(DataGridViewDataErrorContexts.Display);
        }

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
            // The editing controls live outside Controls between edits (WinForms keeps the last one for reuse).
            _latestEditingControl?.Dispose();
            _latestEditingControl = null;
            _editingControl?.Dispose();
            _editingControl = null;
            _editingPanel?.Dispose();
            _editingPanel = null;
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
