using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>A row or a column: the part of the grid model that carries size, state and a default style.</summary>
public class DataGridViewBand : DataGridViewElement, ICloneable, IDisposable
{
    private DataGridViewCellStyle? _defaultCellStyle;
    private bool _visible = true;
    private bool _readOnly;
    private bool _frozen;

    internal DataGridViewBand() { }

    public int Index { get; internal set; } = -1;

    public object? Tag { get; set; }

    public DataGridViewCellStyle DefaultCellStyle
    {
        get => _defaultCellStyle ??= new DataGridViewCellStyle();
        set
        {
            _defaultCellStyle = value;
            DataGridView?.Invalidate();
        }
    }

    public bool HasDefaultCellStyle => _defaultCellStyle != null;

    /// <summary>As in WinForms: a style is written only once created and set to something.</summary>
    internal bool ShouldSerializeDefaultCellStyle() =>
        _defaultCellStyle != null && !_defaultCellStyle.IsEquivalentTo(new DataGridViewCellStyle());

    public virtual bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            State = value ? State | DataGridViewElementStates.Visible : State & ~DataGridViewElementStates.Visible;
            DataGridView?.InvalidateGridLayout();
        }
    }

    public virtual bool ReadOnly
    {
        get => _readOnly;
        set
        {
            if (_readOnly == value) return;
            _readOnly = value;
            State = value ? State | DataGridViewElementStates.ReadOnly : State & ~DataGridViewElementStates.ReadOnly;
        }
    }

    public virtual bool Frozen
    {
        get => _frozen;
        set
        {
            if (_frozen == value) return;
            _frozen = value;
            DataGridView?.InvalidateGridLayout();
        }
    }

    public virtual DataGridViewTriState Resizable { get; set; } = DataGridViewTriState.NotSet;

    public virtual bool Selected
    {
        get => (State & DataGridViewElementStates.Selected) != 0;
        set => State = value ? State | DataGridViewElementStates.Selected : State & ~DataGridViewElementStates.Selected;
    }

    public virtual object Clone()
    {
        var copy = (DataGridViewBand)Activator.CreateInstance(GetType())!;
        copy._visible = _visible;
        copy._readOnly = _readOnly;
        copy._frozen = _frozen;
        copy.Tag = Tag;
        if (_defaultCellStyle != null) copy._defaultCellStyle = (DataGridViewCellStyle)_defaultCellStyle.Clone();
        return copy;
    }

    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing) { }
}

/// <summary>One column: its header, its width policy and the cell template its rows are built from.</summary>
/// <remarks>A component, as in WinForms: the designer gives each column a field and a block of its own.</remarks>
[DesignTimeVisible(false)]
[ToolboxItem(false)]
public class DataGridViewColumn : DataGridViewBand, IComponent
{
    private ISite? _site;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ISite? Site
    {
        get => _site;
        set => _site = value;
    }

    [Browsable(false)]
    public event EventHandler? Disposed;

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                _site?.Container?.Remove(this);
                Disposed?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private string _headerText = string.Empty;
    private int _width = 100;
    private int _minimumWidth = 5;
    private float _fillWeight = 100f;
    private DataGridViewAutoSizeColumnMode _autoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
    private DataGridViewCell? _template;

    public DataGridViewColumn() : this(new DataGridViewTextBoxCell()) { }

    public DataGridViewColumn(DataGridViewCell? cellTemplate)
    {
        _template = cellTemplate;
        HeaderCell = new DataGridViewColumnHeaderCell { OwningColumn = this };
    }

    [Browsable(false)]
    public string Name { get; set; } = string.Empty;

    [Category("Appearance")]
    [Description("The caption text on the column's header cell.")]
    [Localizable(true)]
    public string HeaderText
    {
        get => _headerText;
        set
        {
            _headerText = value ?? string.Empty;
            DataGridView?.Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DataGridViewColumnHeaderCell HeaderCell { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual DataGridViewCell? CellTemplate
    {
        get => _template;
        set => _template = value;
    }

    [Browsable(false)]
    public Type CellType => _template?.GetType() ?? typeof(DataGridViewTextBoxCell);

    [DefaultValue(null)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    /// <summary>Null until set, as in WinForms: the cells then use their own type (string, bool, Image...).</summary>
    public Type? ValueType { get; set; }

    /// <summary>Name of the bound object's property this column shows.</summary>
    [Category("Data")]
    [Description("The name of the data source property or database column to which the DataGridViewColumn is bound.")]
    [DefaultValue("")]
    public string DataPropertyName { get; set; } = string.Empty;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsDataBound => DataGridView?.IsBound == true && !string.IsNullOrEmpty(DataPropertyName);

    [Category("Layout")]
    [Description("The current width of the column.")]
    [Localizable(true)]
    public int Width
    {
        get => _width;
        set
        {
            value = Math.Max(value, _minimumWidth);
            if (_width == value) return;
            _width = value;
            DataGridView?.InvalidateGridLayout();
        }
    }

    [Category("Layout")]
    [Description("The minimum width of the column in pixels.")]
    [DefaultValue(5)]
    [Localizable(true)]
    public int MinimumWidth
    {
        get => _minimumWidth;
        set
        {
            if (value < 2 || value > 65536) throw new ArgumentOutOfRangeException(nameof(value));
            _minimumWidth = value;
            if (_width < value) Width = value;
        }
    }

    [Category("Layout")]
    [Description("The weight that is used when sizing this column in the Fill auto size mode.")]
    [DefaultValue(100F)]
    public float FillWeight
    {
        get => _fillWeight;
        set
        {
            if (value <= 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(value));
            _fillWeight = value;
            DataGridView?.InvalidateGridLayout();
        }
    }

    [Category("Layout")]
    [Description("Determines the auto size mode for this column.")]
    [DefaultValue(DataGridViewAutoSizeColumnMode.NotSet)]
    public DataGridViewAutoSizeColumnMode AutoSizeMode
    {
        get => _autoSizeMode;
        set
        {
            if (_autoSizeMode == value) return;
            _autoSizeMode = value;
            DataGridView?.InvalidateGridLayout();
        }
    }

    /// <summary>The mode actually in force: the column's own, or the grid's when the column says NotSet.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DataGridViewAutoSizeColumnMode InheritedAutoSizeMode =>
        _autoSizeMode != DataGridViewAutoSizeColumnMode.NotSet
            ? _autoSizeMode
            : (DataGridViewAutoSizeColumnMode)(DataGridView?.AutoSizeColumnsMode ?? DataGridViewAutoSizeColumnsMode.None);

    [Category("Behavior")]
    [Description("The sort mode for the column.")]
    [DefaultValue(DataGridViewColumnSortMode.NotSortable)]
    public DataGridViewColumnSortMode SortMode { get; set; } = DataGridViewColumnSortMode.NotSortable;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int DisplayIndex
    {
        get => Index;
        set { }
    }

    [Category("Appearance")]
    [Description("The text used for ToolTips.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string ToolTipText { get; set; } = string.Empty;

    [Category("Behavior")]
    [Description("Indicates whether the user can edit the column's cells.")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool ReadOnly
    {
        get => base.ReadOnly;
        set => base.ReadOnly = value;
    }

    [Category("Behavior")]
    [Description("Indicates whether the column is resizable.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    /// <summary>
    /// As in WinForms: what was set, else what the grid allows (AllowUserToResizeColumns), and outside a
    /// grid NotSet. Written only when set.
    /// </summary>
    public override DataGridViewTriState Resizable
    {
        get => base.Resizable != DataGridViewTriState.NotSet ? base.Resizable
            : DataGridView == null ? DataGridViewTriState.NotSet
            : DataGridView.AllowUserToResizeColumns ? DataGridViewTriState.True : DataGridViewTriState.False;
        set => base.Resizable = value;
    }

    internal bool ShouldSerializeResizable() => base.Resizable != DataGridViewTriState.NotSet;

    internal bool ShouldSerializeHeaderText() => _headerText.Length != 0;

    /// <summary>Makes the cell a row of this column starts life with.</summary>
    internal DataGridViewCell CreateCell()
    {
        var cell = (DataGridViewCell)(_template ?? new DataGridViewTextBoxCell()).Clone();
        cell.OwningColumn = this;
        return cell;
    }

    public override object Clone()
    {
        var copy = (DataGridViewColumn)base.Clone();
        copy._headerText = _headerText;
        copy._width = _width;
        copy._minimumWidth = _minimumWidth;
        copy._fillWeight = _fillWeight;
        copy._autoSizeMode = _autoSizeMode;
        copy._template = _template != null ? (DataGridViewCell)_template.Clone() : null;
        copy.Name = Name;
        copy.ValueType = ValueType;
        copy.DataPropertyName = DataPropertyName;
        copy.SortMode = SortMode;
        return copy;
    }

    public override string ToString() => $"DataGridViewColumn {{ Name={Name}, Index={Index} }}";

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new DataGridView? DataGridView => base.DataGridView;

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new DataGridViewCellStyle DefaultCellStyle { get => base.DefaultCellStyle; set => base.DefaultCellStyle = value; }

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool Frozen { get => base.Frozen; set => base.Frozen = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool HasDefaultCellStyle => base.HasDefaultCellStyle;

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new int Index => base.Index;

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool Selected { get => base.Selected; set => base.Selected = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override DataGridViewElementStates State => base.State;

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new object? Tag { get => base.Tag; set => base.Tag = value; }

    [Category("Appearance")]
    [DefaultValue(true)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool Visible { get => base.Visible; set => base.Visible = value; }
}

public class DataGridViewTextBoxColumn : DataGridViewColumn
{
    public DataGridViewTextBoxColumn() : base(new DataGridViewTextBoxCell())
    {
        // The one column type that sorts by itself (WinForms: DataGridViewColumnSortMode.Automatic).
        base.SortMode = DataGridViewColumnSortMode.Automatic;
    }

    [Category("Behavior")]
    [Description("Specifies the maximum number of characters that can be entered into the text box.")]
    [DefaultValue(32767)]
    public int MaxInputLength
    {
        get => (CellTemplate as DataGridViewTextBoxCell)?.MaxInputLength ?? 32767;
        set { if (CellTemplate is DataGridViewTextBoxCell cell) cell.MaxInputLength = value; }
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(DataGridViewColumnSortMode.Automatic)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new DataGridViewColumnSortMode SortMode { get => base.SortMode; set => base.SortMode = value; }
}

public class DataGridViewCheckBoxColumn : DataGridViewColumn
{
    public DataGridViewCheckBoxColumn() : this(false) { }

    public DataGridViewCheckBoxColumn(bool threeState) : base(new DataGridViewCheckBoxCell(threeState))
    {
    }

    [Category("Behavior")]
    [Description("Indicates whether the hosted check box cells will allow three check states rather than two.")]
    [DefaultValue(false)]
    public bool ThreeState
    {
        get => (CellTemplate as DataGridViewCheckBoxCell)?.ThreeState ?? false;
        set { if (CellTemplate is DataGridViewCheckBoxCell cell) cell.ThreeState = value; }
    }

    [Category("Data")]
    [Description("The underlying value corresponding to a cell value of true, which appears as a checked box.")]
    [DefaultValue(null)]
    public object? TrueValue
    {
        get => (CellTemplate as DataGridViewCheckBoxCell)?.TrueValue;
        set { if (CellTemplate is DataGridViewCheckBoxCell cell) cell.TrueValue = value; }
    }

    [Category("Data")]
    [Description("The underlying value corresponding to a cell value of false, which appears as an unchecked box.")]
    [DefaultValue(null)]
    public object? FalseValue
    {
        get => (CellTemplate as DataGridViewCheckBoxCell)?.FalseValue;
        set { if (CellTemplate is DataGridViewCheckBoxCell cell) cell.FalseValue = value; }
    }
}

public class DataGridViewButtonColumn : DataGridViewColumn
{
    public DataGridViewButtonColumn() : base(new DataGridViewButtonCell()) { }

    [Category("Appearance")]
    [Description("The default text displayed on the button cell.")]
    [DefaultValue(null)]
    public string? Text { get; set; }

    [Category("Appearance")]
    [Description("Indicates whether the DataGridViewButtonColumn.Text property value is displayed as the button text for cells in this column.")]
    [DefaultValue(false)]
    public bool UseColumnTextForButtonValue
    {
        get => (CellTemplate as DataGridViewButtonCell)?.UseColumnTextForButtonValue ?? false;
        set { if (CellTemplate is DataGridViewButtonCell cell) cell.UseColumnTextForButtonValue = value; }
    }
}

public class DataGridViewLinkColumn : DataGridViewColumn
{
    public DataGridViewLinkColumn() : base(new DataGridViewLinkCell()) { }

    [Category("Appearance")]
    [Description("The link text displayed in all of the column's cells.")]
    [DefaultValue(null)]
    public string? Text { get; set; }

    [Category("Appearance")]
    [Description("The color used to display an unselected link within cells in the column.")]
    public Color LinkColor
    {
        get => (CellTemplate as DataGridViewLinkCell)?.LinkColor ?? Theme.LinkText;
        set { if (CellTemplate is DataGridViewLinkCell cell) cell.LinkColor = value; }
    }

    internal bool ShouldSerializeLinkColor() => LinkColor != Theme.LinkText;

    [Category("Appearance")]
    [Description("Indicates whether the DataGridViewLinkColumn.Text property value is displayed as the link text.")]
    [DefaultValue(false)]
    public bool UseColumnTextForLinkValue
    {
        get => (CellTemplate as DataGridViewLinkCell)?.UseColumnTextForLinkValue ?? false;
        set { if (CellTemplate is DataGridViewLinkCell cell) cell.UseColumnTextForLinkValue = value; }
    }
}

public class DataGridViewImageColumn : DataGridViewColumn
{
    public DataGridViewImageColumn() : this(false) { }

    public DataGridViewImageColumn(bool valuesAreIcons) : base(new DataGridViewImageCell(valuesAreIcons))
    {
    }

    [Category("Appearance")]
    [Description("The image displayed in the cells of this column when the cell's DataGridViewCell.Value property is not set and the cell's DataGridViewImageCell.ValueIsIcon property is set to false.")]
    [DefaultValue(null)]
    public Image? Image { get; set; }

    [Category("Appearance")]
    [Description("User-defined text associated with the image.")]
    [DefaultValue("")]
    public string Description
    {
        get => (CellTemplate as DataGridViewImageCell)?.Description ?? string.Empty;
        set { if (CellTemplate is DataGridViewImageCell cell) cell.Description = value; }
    }
}

public class DataGridViewComboBoxColumn : DataGridViewColumn
{
    public DataGridViewComboBoxColumn() : base(new DataGridViewComboBoxCell()) { }

    private DataGridViewComboBoxCell Template => (DataGridViewComboBoxCell)CellTemplate!;

    [Category("Data")]
    [Description("The collection of objects used as selections in the combo boxes.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public DataGridViewComboBoxCell.ObjectCollection Items => Template.Items;

    [Category("Data")]
    [Description("The data source that populates the selections for the combo boxes.")]
    [DefaultValue(null)]
    public object? DataSource
    {
        get => Template.DataSource;
        set => Template.DataSource = value;
    }

    [Category("Data")]
    [Description("A string that specifies the property or column from which to retrieve strings for display in the combo boxes.")]
    [DefaultValue("")]
    public string DisplayMember
    {
        get => Template.DisplayMember;
        set => Template.DisplayMember = value;
    }

    [Category("Data")]
    [Description("A string that specifies the property or column from which to get values that correspond to the selections in the drop-down list.")]
    [DefaultValue("")]
    public string ValueMember
    {
        get => Template.ValueMember;
        set => Template.ValueMember = value;
    }

    [Category("Behavior")]
    [Description("The maximum number of items in the drop-down list of the cells in the column.")]
    [DefaultValue(8)]
    public int MaxDropDownItems
    {
        get => Template.MaxDropDownItems;
        set => Template.MaxDropDownItems = value;
    }
}

/// <summary>One row: its cells, its height and its header.</summary>
public class DataGridViewRow : DataGridViewBand
{
    private readonly DataGridViewCellCollection _cells;
    private int _height = -1;

    public DataGridViewRow()
    {
        _cells = new DataGridViewCellCollection(this);
        HeaderCell = new DataGridViewRowHeaderCell { OwningRow = this };
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public DataGridViewCellCollection Cells => _cells;

    private string _errorText = string.Empty;

    /// <summary>The row's error message (WinForms shows it as an icon in the row header; a CellValidating handler sets it).</summary>
    [DefaultValue("")]
    [NotifyParentProperty(true)]
    [Category("Appearance")]
    [Description("The error message text for row-level errors.")]
    [AllowNull]
    public string ErrorText
    {
        get => _errorText;
        set
        {
            _errorText = value ?? string.Empty;
            DataGridView?.Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DataGridViewRowHeaderCell HeaderCell { get; }

    internal object? HeaderCellValue { get; set; }

    [Category("Appearance")]
    [Description("The current height of the row.")]
    public int Height
    {
        get => _height >= 0 ? _height : DataGridView?.RowTemplate?._height ?? DefaultHeight;
        set
        {
            if (value < 2) throw new ArgumentOutOfRangeException(nameof(value));
            if (_height == value) return;
            _height = value;
            DataGridView?.InvalidateGridLayout();
        }
    }

    /// <summary>A row's height before anyone sets it, as in WinForms: the default font's height + 9.</summary>
    internal static int DefaultHeight => Control.DefaultFont.Height + 9;

    internal bool ShouldSerializeHeight() => Height != DefaultHeight;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MinimumHeight { get; set; } = 3;

    /// <summary>True for the blank row at the bottom that AllowUserToAddRows offers.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsNewRow => DataGridView != null && DataGridView.NewRowIndex == Index;

    [Browsable(false)]
    public DataGridViewCellStyle InheritedStyle
    {
        get
        {
            var style = new DataGridViewCellStyle();
            var grid = DataGridView;
            if (grid != null)
            {
                style.ApplyStyle(grid.DefaultCellStyle);
                style.ApplyStyle(grid.RowsDefaultCellStyle);
                if (Index >= 0 && Index % 2 == 1) style.ApplyStyle(grid.AlternatingRowsDefaultCellStyle);
            }
            if (HasDefaultCellStyle) style.ApplyStyle(DefaultCellStyle);
            return style;
        }
    }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool Selected
    {
        get => DataGridView != null ? DataGridView.IsRowSelected(Index) : base.Selected;
        set
        {
            if (DataGridView != null) DataGridView.SetRowSelected(Index, value);
            else base.Selected = value;
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Displayed => Visible;

    public object?[] SetValues(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        for (int i = 0; i < values.Length && i < _cells.Count; i++) _cells[i].Value = values[i];
        return values;
    }

    public DataGridViewCell? GetCell(string columnName)
    {
        int index = DataGridView?.Columns.IndexOf(columnName) ?? -1;
        return index >= 0 && index < _cells.Count ? _cells[index] : null;
    }

    public Rectangle GetRowBounds() => DataGridView?.GetRowDisplayRectangle(Index, false) ?? Rectangle.Empty;

    public override object Clone()
    {
        var copy = (DataGridViewRow)base.Clone();
        copy._height = _height;
        foreach (DataGridViewCell cell in _cells) copy.Cells.Add((DataGridViewCell)cell.Clone());
        return copy;
    }

    public override string ToString() => $"DataGridViewRow {{ Index={Index} }}";

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new DataGridView? DataGridView => base.DataGridView;

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public new DataGridViewCellStyle DefaultCellStyle { get => base.DefaultCellStyle; set => base.DefaultCellStyle = value; }

    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool Frozen { get => base.Frozen; set => base.Frozen = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool HasDefaultCellStyle => base.HasDefaultCellStyle;

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new int Index => base.Index;

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool ReadOnly { get => base.ReadOnly; set => base.ReadOnly = value; }

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override DataGridViewTriState Resizable { get => base.Resizable; set => base.Resizable = value; }

    internal bool ShouldSerializeResizable() => base.Resizable != DataGridViewTriState.NotSet;

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override DataGridViewElementStates State => base.State;

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new object? Tag { get => base.Tag; set => base.Tag = value; }

    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool Visible { get => base.Visible; set => base.Visible = value; }
}

public class DataGridViewCellCollection : IList, IList<DataGridViewCell>
{
    private readonly DataGridViewRow _owner;
    private readonly List<DataGridViewCell> _cells = new();

    public DataGridViewCellCollection(DataGridViewRow dataGridViewRow) => _owner = dataGridViewRow;

    public int Count => _cells.Count;
    public bool IsReadOnly => false;

    public DataGridViewCell this[int index]
    {
        get => _cells[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Attach(value, index);
            _cells[index] = value;
        }
    }

    public DataGridViewCell this[string columnName]
    {
        get
        {
            int index = _owner.DataGridView?.Columns.IndexOf(columnName) ?? -1;
            if (index < 0) throw new ArgumentException("No column named '" + columnName + "'.", nameof(columnName));
            return _cells[index];
        }
        set
        {
            int index = _owner.DataGridView?.Columns.IndexOf(columnName) ?? -1;
            if (index < 0) throw new ArgumentException("No column named '" + columnName + "'.", nameof(columnName));
            this[index] = value;
        }
    }

    public int Add(DataGridViewCell dataGridViewCell)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCell);
        Attach(dataGridViewCell, _cells.Count);
        _cells.Add(dataGridViewCell);
        return _cells.Count - 1;
    }

    void ICollection<DataGridViewCell>.Add(DataGridViewCell item) => Add(item);

    public void AddRange(params DataGridViewCell[] dataGridViewCells)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCells);
        foreach (var cell in dataGridViewCells) Add(cell);
    }

    private void Attach(DataGridViewCell cell, int index)
    {
        cell.OwningRow = _owner;
        cell.SetDataGridView(_owner.DataGridView);
        var grid = _owner.DataGridView;
        if (grid != null && index < grid.Columns.Count) cell.OwningColumn = grid.Columns[index];
    }

    internal void Rebind()
    {
        for (int i = 0; i < _cells.Count; i++) Attach(_cells[i], i);
    }

    public void Clear() => _cells.Clear();
    public bool Contains(DataGridViewCell cell) => _cells.Contains(cell);
    public int IndexOf(DataGridViewCell cell) => _cells.IndexOf(cell);
    public void Insert(int index, DataGridViewCell cell) { Attach(cell, index); _cells.Insert(index, cell); }
    public bool Remove(DataGridViewCell cell) => _cells.Remove(cell);
    public void RemoveAt(int index) => _cells.RemoveAt(index);
    public void CopyTo(DataGridViewCell[] array, int index) => _cells.CopyTo(array, index);
    public IEnumerator<DataGridViewCell> GetEnumerator() => _cells.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _cells.GetEnumerator();

    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _cells[index]; set => this[index] = (DataGridViewCell)value!; }
    int IList.Add(object? value) => Add((DataGridViewCell)value!);
    bool IList.Contains(object? value) => value is DataGridViewCell c && Contains(c);
    int IList.IndexOf(object? value) => value is DataGridViewCell c ? IndexOf(c) : -1;
    void IList.Insert(int index, object? value) => Insert(index, (DataGridViewCell)value!);
    void IList.Remove(object? value) { if (value is DataGridViewCell c) Remove(c); }
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_cells).CopyTo(array, index);
}

public class DataGridViewColumnCollection : IList, IList<DataGridViewColumn>
{
    private readonly DataGridView _owner;
    private readonly List<DataGridViewColumn> _columns = new();

    public DataGridViewColumnCollection(DataGridView dataGridView) => _owner = dataGridView;

    public int Count => _columns.Count;
    public bool IsReadOnly => false;

    public DataGridViewColumn this[int index] => _columns[index];

    DataGridViewColumn IList<DataGridViewColumn>.this[int index]
    {
        get => _columns[index];
        set => throw new NotSupportedException();
    }

    public DataGridViewColumn this[string columnName]
    {
        get
        {
            int i = IndexOf(columnName);
            if (i < 0) throw new ArgumentException("No column named '" + columnName + "'.", nameof(columnName));
            return _columns[i];
        }
    }

    public virtual int Add(DataGridViewColumn dataGridViewColumn)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewColumn);
        dataGridViewColumn.SetDataGridView(_owner);
        _columns.Add(dataGridViewColumn);
        Reindex();
        _owner.ColumnsChanged(added: dataGridViewColumn);
        return _columns.Count - 1;
    }

    public virtual int Add(string? columnName, string? headerText)
    {
        var column = new DataGridViewTextBoxColumn { Name = columnName ?? string.Empty, HeaderText = headerText ?? string.Empty };
        return Add(column);
    }

    void ICollection<DataGridViewColumn>.Add(DataGridViewColumn item) => Add(item);

    public virtual void AddRange(params DataGridViewColumn[] dataGridViewColumns)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewColumns);
        foreach (var column in dataGridViewColumns) Add(column);
    }

    public virtual void Clear()
    {
        foreach (var column in _columns) column.SetDataGridView(null);
        _columns.Clear();
        _owner.ColumnsChanged(added: null);
    }

    public virtual bool Contains(DataGridViewColumn dataGridViewColumn) => _columns.Contains(dataGridViewColumn);

    public virtual bool Contains(string columnName) => IndexOf(columnName) >= 0;

    public int IndexOf(DataGridViewColumn dataGridViewColumn) => _columns.IndexOf(dataGridViewColumn);

    public int IndexOf(string? columnName)
    {
        if (string.IsNullOrEmpty(columnName)) return -1;
        for (int i = 0; i < _columns.Count; i++)
        {
            if (string.Equals(_columns[i].Name, columnName, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    public virtual void Insert(int index, DataGridViewColumn dataGridViewColumn)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewColumn);
        dataGridViewColumn.SetDataGridView(_owner);
        _columns.Insert(Math.Clamp(index, 0, _columns.Count), dataGridViewColumn);
        Reindex();
        _owner.ColumnsChanged(added: dataGridViewColumn);
    }

    public virtual bool Remove(DataGridViewColumn dataGridViewColumn)
    {
        if (dataGridViewColumn == null || !_columns.Remove(dataGridViewColumn)) return false;
        dataGridViewColumn.SetDataGridView(null);
        Reindex();
        _owner.ColumnsChanged(added: null);
        return true;
    }

    public virtual void Remove(string columnName)
    {
        int i = IndexOf(columnName);
        if (i >= 0) RemoveAt(i);
    }

    public virtual void RemoveAt(int index)
    {
        _columns[index].SetDataGridView(null);
        _columns.RemoveAt(index);
        Reindex();
        _owner.ColumnsChanged(added: null);
    }

    public int GetColumnCount(DataGridViewElementStates includeFilter)
    {
        int count = 0;
        foreach (var column in _columns)
        {
            if ((column.State & includeFilter) == includeFilter) count++;
        }
        return count;
    }

    private void Reindex()
    {
        for (int i = 0; i < _columns.Count; i++) ((DataGridViewBand)_columns[i]).Index = i;
    }

    public void CopyTo(DataGridViewColumn[] array, int index) => _columns.CopyTo(array, index);
    public IEnumerator<DataGridViewColumn> GetEnumerator() => _columns.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _columns.GetEnumerator();

    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _columns[index]; set => throw new NotSupportedException(); }
    int IList.Add(object? value) => Add((DataGridViewColumn)value!);
    void IList.Clear() => Clear();
    bool IList.Contains(object? value) => value is DataGridViewColumn c && Contains(c);
    int IList.IndexOf(object? value) => value is DataGridViewColumn c ? IndexOf(c) : -1;
    void IList.Insert(int index, object? value) => Insert(index, (DataGridViewColumn)value!);
    void IList.Remove(object? value) { if (value is DataGridViewColumn c) Remove(c); }
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_columns).CopyTo(array, index);
}

public class DataGridViewRowCollection : IList, IList<DataGridViewRow>
{
    private readonly DataGridView _owner;
    private readonly List<DataGridViewRow> _rows = new();

    public DataGridViewRowCollection(DataGridView dataGridView) => _owner = dataGridView;

    public int Count => _rows.Count;
    public bool IsReadOnly => false;

    public DataGridViewRow this[int index] => _rows[index];

    DataGridViewRow IList<DataGridViewRow>.this[int index]
    {
        get => _rows[index];
        set => throw new NotSupportedException();
    }

    /// <summary>Adds an empty row built from the column templates.</summary>
    public virtual int Add() => Add(_owner.CreateRowFromTemplate());

    public virtual int Add(int count)
    {
        int first = _rows.Count;
        for (int i = 0; i < count; i++) Add();
        return first;
    }

    public virtual int Add(DataGridViewRow dataGridViewRow)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewRow);
        if (_owner.IsBound) throw new InvalidOperationException("Rows cannot be added to a data-bound DataGridView.");
        return AddCore(dataGridViewRow);
    }

    public virtual int Add(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var row = _owner.CreateRowFromTemplate();
        int index = Add(row);
        row.SetValues(values);
        return index;
    }

    internal int AddCore(DataGridViewRow row)
    {
        row.SetDataGridView(_owner);
        _rows.Add(row);
        Reindex();
        row.Cells.Rebind();
        foreach (DataGridViewCell cell in row.Cells) cell.SetDataGridView(_owner);
        _owner.RowsChanged(new DataGridViewRowsAddedEventArgs(_rows.Count - 1, 1));
        return _rows.Count - 1;
    }

    void ICollection<DataGridViewRow>.Add(DataGridViewRow item) => Add(item);

    public virtual void AddRange(params DataGridViewRow[] dataGridViewRows)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewRows);
        foreach (var row in dataGridViewRows) Add(row);
    }

    public virtual void Clear()
    {
        int count = _rows.Count;
        foreach (var row in _rows) row.SetDataGridView(null);
        _rows.Clear();
        _owner.NotifyRowsRemoved(new DataGridViewRowsRemovedEventArgs(0, count));
    }

    /// <summary>Replaces every row; used when the grid rebuilds itself from a data source.</summary>
    internal void ResetTo(IEnumerable<DataGridViewRow> rows)
    {
        _rows.Clear();
        foreach (var row in rows)
        {
            row.SetDataGridView(_owner);
            _rows.Add(row);
        }
        Reindex();
        foreach (var row in _rows)
        {
            row.Cells.Rebind();
            foreach (DataGridViewCell cell in row.Cells) cell.SetDataGridView(_owner);
        }
    }

    public virtual bool Contains(DataGridViewRow dataGridViewRow) => _rows.Contains(dataGridViewRow);

    public int IndexOf(DataGridViewRow dataGridViewRow) => _rows.IndexOf(dataGridViewRow);

    public virtual void Insert(int rowIndex, DataGridViewRow dataGridViewRow)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewRow);
        dataGridViewRow.SetDataGridView(_owner);
        _rows.Insert(Math.Clamp(rowIndex, 0, _rows.Count), dataGridViewRow);
        Reindex();
        dataGridViewRow.Cells.Rebind();
        _owner.RowsChanged(new DataGridViewRowsAddedEventArgs(rowIndex, 1));
    }

    public virtual void Insert(int rowIndex, params object?[] values)
    {
        var row = _owner.CreateRowFromTemplate();
        Insert(rowIndex, row);
        row.SetValues(values);
    }

    public virtual bool Remove(DataGridViewRow dataGridViewRow)
    {
        int index = _rows.IndexOf(dataGridViewRow);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    public virtual void RemoveAt(int index)
    {
        _rows[index].SetDataGridView(null);
        _rows.RemoveAt(index);
        Reindex();
        _owner.NotifyRowsRemoved(new DataGridViewRowsRemovedEventArgs(index, 1));
    }

    public int GetRowCount(DataGridViewElementStates includeFilter)
    {
        int count = 0;
        foreach (var row in _rows)
        {
            if ((row.State & includeFilter) == includeFilter) count++;
        }
        return count;
    }

    internal void SortCore(Comparison<DataGridViewRow> comparison)
    {
        _rows.Sort(comparison);
        Reindex();
    }

    private void Reindex()
    {
        for (int i = 0; i < _rows.Count; i++) ((DataGridViewBand)_rows[i]).Index = i;
    }

    public void CopyTo(DataGridViewRow[] array, int index) => _rows.CopyTo(array, index);
    public IEnumerator<DataGridViewRow> GetEnumerator() => _rows.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _rows.GetEnumerator();

    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _rows[index]; set => throw new NotSupportedException(); }
    int IList.Add(object? value) => Add((DataGridViewRow)value!);
    void IList.Clear() => Clear();
    bool IList.Contains(object? value) => value is DataGridViewRow r && Contains(r);
    int IList.IndexOf(object? value) => value is DataGridViewRow r ? IndexOf(r) : -1;
    void IList.Insert(int index, object? value) => Insert(index, (DataGridViewRow)value!);
    void IList.Remove(object? value) { if (value is DataGridViewRow r) Remove(r); }
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_rows).CopyTo(array, index);
}

/// <summary>A read-only snapshot of selected cells, rows or columns.</summary>
public class DataGridViewSelectedCellCollection : IList, IReadOnlyList<DataGridViewCell>
{
    private readonly List<DataGridViewCell> _cells;

    internal DataGridViewSelectedCellCollection(List<DataGridViewCell> cells) => _cells = cells;

    public int Count => _cells.Count;
    public DataGridViewCell this[int index] => _cells[index];
    public bool Contains(DataGridViewCell dataGridViewCell) => _cells.Contains(dataGridViewCell);
    public int IndexOf(DataGridViewCell dataGridViewCell) => _cells.IndexOf(dataGridViewCell);
    public void CopyTo(Array array, int index) => ((ICollection)_cells).CopyTo(array, index);
    public IEnumerator<DataGridViewCell> GetEnumerator() => _cells.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _cells.GetEnumerator();

    bool IList.IsFixedSize => true;
    public bool IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _cells[index]; set => throw new NotSupportedException(); }
    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object? value) => value is DataGridViewCell c && Contains(c);
    int IList.IndexOf(object? value) => value is DataGridViewCell c ? IndexOf(c) : -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
}

public class DataGridViewSelectedRowCollection : IList, IReadOnlyList<DataGridViewRow>
{
    private readonly List<DataGridViewRow> _rows;

    internal DataGridViewSelectedRowCollection(List<DataGridViewRow> rows) => _rows = rows;

    public int Count => _rows.Count;
    public DataGridViewRow this[int index] => _rows[index];
    public bool Contains(DataGridViewRow dataGridViewRow) => _rows.Contains(dataGridViewRow);
    public int IndexOf(DataGridViewRow dataGridViewRow) => _rows.IndexOf(dataGridViewRow);
    public void CopyTo(Array array, int index) => ((ICollection)_rows).CopyTo(array, index);
    public IEnumerator<DataGridViewRow> GetEnumerator() => _rows.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _rows.GetEnumerator();

    bool IList.IsFixedSize => true;
    public bool IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _rows[index]; set => throw new NotSupportedException(); }
    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object? value) => value is DataGridViewRow r && Contains(r);
    int IList.IndexOf(object? value) => value is DataGridViewRow r ? IndexOf(r) : -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
}

public class DataGridViewSelectedColumnCollection : IList, IReadOnlyList<DataGridViewColumn>
{
    private readonly List<DataGridViewColumn> _columns;

    internal DataGridViewSelectedColumnCollection(List<DataGridViewColumn> columns) => _columns = columns;

    public int Count => _columns.Count;
    public DataGridViewColumn this[int index] => _columns[index];
    public bool Contains(DataGridViewColumn dataGridViewColumn) => _columns.Contains(dataGridViewColumn);
    public int IndexOf(DataGridViewColumn dataGridViewColumn) => _columns.IndexOf(dataGridViewColumn);
    public void CopyTo(Array array, int index) => ((ICollection)_columns).CopyTo(array, index);
    public IEnumerator<DataGridViewColumn> GetEnumerator() => _columns.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _columns.GetEnumerator();

    bool IList.IsFixedSize => true;
    public bool IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _columns[index]; set => throw new NotSupportedException(); }
    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object? value) => value is DataGridViewColumn c && Contains(c);
    int IList.IndexOf(object? value) => value is DataGridViewColumn c ? IndexOf(c) : -1;
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
}
