// Adapted from dotnet/winforms (src/System.Windows.Forms/System/Windows/Forms/DataBinding/BindingNavigator.cs).
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace System.Windows.Forms;

/// <summary>
/// The tool strip that moves through the records of a <see cref="BindingSource"/>: first, previous, the position
/// and the count, next, last, add and delete. WinForms' implementation; the standard items come from
/// <see cref="AddStandardItems"/>, which the designer calls when a navigator is dropped on a form (as VS does), and
/// their pictures are drawn by NetForms.
/// </summary>
[DefaultProperty(nameof(BindingSource))]
[DefaultEvent(nameof(RefreshItems))]
[Description("Provides a user interface for navigating and manipulating data bound to controls on a form.")]
public class BindingNavigator : ToolStrip, ISupportInitialize
{
    private BindingSource? _bindingSource;

    private ToolStripItem? _moveFirstItem;
    private ToolStripItem? _movePreviousItem;
    private ToolStripItem? _moveNextItem;
    private ToolStripItem? _moveLastItem;
    private ToolStripItem? _addNewItem;
    private ToolStripItem? _deleteItem;
    private ToolStripItem? _positionItem;
    private ToolStripItem? _countItem;

    private string _countItemFormat = DefaultCountItemFormat;
    private bool _initializing;
    private bool _addNewItemUserEnabled = true;
    private bool _deleteItemUserEnabled = true;
    private bool _refreshing;

    private static string DefaultCountItemFormat => SystemStrings.Get("of {0}");

    /// <summary>An empty navigator; call <see cref="AddStandardItems"/> for the standard items.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public BindingNavigator()
        : this(addStandardItems: false)
    {
    }

    /// <summary>A navigator with the standard items, bound to <paramref name="bindingSource"/>.</summary>
    public BindingNavigator(BindingSource? bindingSource)
        : this(addStandardItems: true)
    {
        BindingSource = bindingSource;
    }

    /// <summary>An empty navigator in <paramref name="container"/> (what the designer writes).</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public BindingNavigator(IContainer container)
        : this(addStandardItems: false)
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    public BindingNavigator(bool addStandardItems)
    {
        if (addStandardItems) AddStandardItems();
    }

    /// <summary>ISupportInitialize: the items are not refreshed while the designer code sets the navigator up.</summary>
    public void BeginInit() => _initializing = true;

    public void EndInit()
    {
        _initializing = false;
        RefreshItemsInternal();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) BindingSource = null;
        base.Dispose(disposing);
    }

    /// <summary>
    /// The standard items: move first/previous, the position box and the count, move next/last, add new and delete,
    /// with the names Visual Studio gives them (bindingNavigatorMoveFirstItem, ...). It does not remove any items.
    /// </summary>
    public virtual void AddStandardItems()
    {
        MoveFirstItem = new ToolStripButton();
        MovePreviousItem = new ToolStripButton();
        MoveNextItem = new ToolStripButton();
        MoveLastItem = new ToolStripButton();
        PositionItem = new ToolStripTextBox();
        CountItem = new ToolStripLabel();
        AddNewItem = new ToolStripButton();
        DeleteItem = new ToolStripButton();

        var separator1 = new ToolStripSeparator();
        var separator2 = new ToolStripSeparator();
        var separator3 = new ToolStripSeparator();

        // A name in VS style, with the case of the navigator's own name ("bindingNavigator..." or "BindingNavigator...").
        char ch = string.IsNullOrEmpty(Name) || char.IsLower(Name[0]) ? 'b' : 'B';
        MoveFirstItem.Name = $"{ch}indingNavigatorMoveFirstItem";
        MovePreviousItem.Name = $"{ch}indingNavigatorMovePreviousItem";
        MoveNextItem.Name = $"{ch}indingNavigatorMoveNextItem";
        MoveLastItem.Name = $"{ch}indingNavigatorMoveLastItem";
        PositionItem.Name = $"{ch}indingNavigatorPositionItem";
        CountItem.Name = $"{ch}indingNavigatorCountItem";
        AddNewItem.Name = $"{ch}indingNavigatorAddNewItem";
        DeleteItem.Name = $"{ch}indingNavigatorDeleteItem";
        separator1.Name = $"{ch}indingNavigatorSeparator1";
        separator2.Name = $"{ch}indingNavigatorSeparator2";
        separator3.Name = $"{ch}indingNavigatorSeparator3";

        MoveFirstItem.Text = SystemStrings.Get("Move first");
        MovePreviousItem.Text = SystemStrings.Get("Move previous");
        MoveNextItem.Text = SystemStrings.Get("Move next");
        MoveLastItem.Text = SystemStrings.Get("Move last");
        AddNewItem.Text = SystemStrings.Get("Add new");
        DeleteItem.Text = SystemStrings.Get("Delete");
        CountItem.ToolTipText = SystemStrings.Get("Total number of items");
        PositionItem.ToolTipText = SystemStrings.Get("Current position");
        CountItem.AutoToolTip = false;
        PositionItem.AutoToolTip = false;
        PositionItem.AccessibleName = SystemStrings.Get("Position");

        MoveFirstItem.Image = NavigatorIcon(0);
        MovePreviousItem.Image = NavigatorIcon(1);
        MoveNextItem.Image = NavigatorIcon(2);
        MoveLastItem.Image = NavigatorIcon(3);
        AddNewItem.Image = NavigatorIcon(4);
        DeleteItem.Image = NavigatorIcon(5);
        foreach (var item in new[] { MoveFirstItem, MovePreviousItem, MoveNextItem, MoveLastItem, AddNewItem, DeleteItem })
        {
            item.RightToLeftAutoMirrorImage = true;
            item.DisplayStyle = ToolStripItemDisplayStyle.Image;
        }

        PositionItem.AutoSize = false;
        PositionItem.Width = 50;

        Items.AddRange(new[]
        {
            MoveFirstItem, MovePreviousItem, separator1, PositionItem, CountItem, separator2,
            MoveNextItem, MoveLastItem, separator3, AddNewItem, DeleteItem,
        });
    }

    [DefaultValue(null)]
    [Category("Data")]
    [Description("The BindingSource that the BindingNavigator navigates.")]
    [TypeConverter(typeof(ReferenceConverter))]
    public BindingSource? BindingSource
    {
        get => _bindingSource;
        set => WireUpBindingSource(ref _bindingSource, value);
    }

    [TypeConverter(typeof(ReferenceConverter))]
    [Category("Items")]
    [Description("The ToolStripItem on the BindingNavigator that raises the 'Move first' action.")]
    public ToolStripItem? MoveFirstItem
    {
        get => Live(ref _moveFirstItem);
        set => WireUpButton(ref _moveFirstItem, value, OnMoveFirst);
    }

    [TypeConverter(typeof(ReferenceConverter))]
    [Category("Items")]
    [Description("The ToolStripItem on the BindingNavigator that raises the 'Move previous' action.")]
    public ToolStripItem? MovePreviousItem
    {
        get => Live(ref _movePreviousItem);
        set => WireUpButton(ref _movePreviousItem, value, OnMovePrevious);
    }

    [TypeConverter(typeof(ReferenceConverter))]
    [Category("Items")]
    [Description("The ToolStripItem on the BindingNavigator that raises the 'Move next' action.")]
    public ToolStripItem? MoveNextItem
    {
        get => Live(ref _moveNextItem);
        set => WireUpButton(ref _moveNextItem, value, OnMoveNext);
    }

    [TypeConverter(typeof(ReferenceConverter))]
    [Category("Items")]
    [Description("The ToolStripItem on the BindingNavigator that raises the 'Move last' action.")]
    public ToolStripItem? MoveLastItem
    {
        get => Live(ref _moveLastItem);
        set => WireUpButton(ref _moveLastItem, value, OnMoveLast);
    }

    [TypeConverter(typeof(ReferenceConverter))]
    [Category("Items")]
    [Description("The ToolStripItem on the BindingNavigator that raises the 'Add new' action.")]
    public ToolStripItem? AddNewItem
    {
        get => Live(ref _addNewItem);
        set
        {
            if (_addNewItem != value && value is not null)
            {
                value.EnabledChanged += OnAddNewItemEnabledChanged;
                _addNewItemUserEnabled = value.Enabled;
            }
            WireUpButton(ref _addNewItem, value, OnAddNew);
        }
    }

    [TypeConverter(typeof(ReferenceConverter))]
    [Category("Items")]
    [Description("The ToolStripItem on the BindingNavigator that raises the 'Delete' action.")]
    public ToolStripItem? DeleteItem
    {
        get => Live(ref _deleteItem);
        set
        {
            if (_deleteItem != value && value is not null)
            {
                value.EnabledChanged += OnDeleteItemEnabledChanged;
                _deleteItemUserEnabled = value.Enabled;
            }
            WireUpButton(ref _deleteItem, value, OnDelete);
        }
    }

    [TypeConverter(typeof(ReferenceConverter))]
    [Category("Items")]
    [Description("The ToolStripItem on the BindingNavigator that displays the current position.")]
    public ToolStripItem? PositionItem
    {
        get => Live(ref _positionItem);
        set => WireUpTextBox(ref _positionItem, value);
    }

    [TypeConverter(typeof(ReferenceConverter))]
    [Category("Items")]
    [Description("The ToolStripItem on the BindingNavigator that displays the total number of items.")]
    public ToolStripItem? CountItem
    {
        get => Live(ref _countItem);
        set => WireUpLabel(ref _countItem, value);
    }

    /// <summary>The count text: "of {0}" by default (the {0} is the number of records).</summary>
    [Category("Appearance")]
    [Description("Formatting to apply to count displayed in the CountItem ToolStrip item.")]
    public string CountItemFormat
    {
        get => _countItemFormat;
        set
        {
            if (_countItemFormat == value) return;
            _countItemFormat = value;
            RefreshItemsInternal();
        }
    }

    [Category("Behavior")]
    [Description("Event raised when BindingNavigator ToolStrip items need to be refreshed to reflect current state of data.")]
    public event EventHandler? RefreshItems;

    /// <summary>Enables the items for the current position and count and writes the position and the count.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void RefreshItemsCore()
    {
        int count, position;
        bool allowNew, allowRemove;
        if (_bindingSource is null)
        {
            count = 0;
            position = 0;
            allowNew = false;
            allowRemove = false;
        }
        else
        {
            count = _bindingSource.Count;
            position = _bindingSource.Position + 1;
            allowNew = ((IBindingList)_bindingSource).AllowNew;
            allowRemove = ((IBindingList)_bindingSource).AllowRemove;
        }

        if (!DesignMode)
        {
            if (MoveFirstItem is not null) _moveFirstItem!.Enabled = position > 1;
            if (MovePreviousItem is not null) _movePreviousItem!.Enabled = position > 1;
            if (MoveNextItem is not null) _moveNextItem!.Enabled = position < count;
            if (MoveLastItem is not null) _moveLastItem!.Enabled = position < count;
            if (AddNewItem is not null)
            {
                _addNewItem!.EnabledChanged -= OnAddNewItemEnabledChanged;
                _addNewItem.Enabled = _addNewItemUserEnabled && allowNew;
                _addNewItem.EnabledChanged += OnAddNewItemEnabledChanged;
            }
            if (DeleteItem is not null)
            {
                _deleteItem!.EnabledChanged -= OnDeleteItemEnabledChanged;
                _deleteItem.Enabled = _deleteItemUserEnabled && allowRemove && count > 0;
                _deleteItem.EnabledChanged += OnDeleteItemEnabledChanged;
            }
            if (PositionItem is not null) _positionItem!.Enabled = position > 0 && count > 0;
            if (CountItem is not null) _countItem!.Enabled = count > 0;
        }

        RestoreStandardImages();

        if (_positionItem is not null) _positionItem.Text = position.ToString(CultureInfo.CurrentCulture);
        if (_countItem is not null) _countItem.Text = DesignMode ? CountItemFormat : string.Format(CultureInfo.CurrentCulture, CountItemFormat, count);
    }

    protected virtual void OnRefreshItems()
    {
        RefreshItemsCore();
        RefreshItems?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Validates the control that has the focus on the form before the navigator moves (WinForms: ValidateActiveControl).</summary>
    public bool Validate()
    {
        var container = FindForm() as ContainerControl;
        return container?.Validate() ?? true;
    }

    private void AcceptNewPosition()
    {
        if (_positionItem is null || _bindingSource is null) return;
        int newPosition = _bindingSource.Position;
        try
        {
            newPosition = Convert.ToInt32(_positionItem.Text, CultureInfo.CurrentCulture) - 1;
        }
        catch (FormatException)
        {
        }
        catch (OverflowException)
        {
        }
        // The BindingSource keeps the position in range; a bad number puts the old position back in the box.
        if (newPosition != _bindingSource.Position) _bindingSource.Position = newPosition;
        RefreshItemsInternal();
    }

    private void CancelNewPosition() => RefreshItemsInternal();

    private void OnMoveFirst(object? sender, EventArgs e)
    {
        if (Validate() && _bindingSource is not null)
        {
            _bindingSource.MoveFirst();
            RefreshItemsInternal();
        }
    }

    private void OnMovePrevious(object? sender, EventArgs e)
    {
        if (Validate() && _bindingSource is not null)
        {
            _bindingSource.MovePrevious();
            RefreshItemsInternal();
        }
    }

    private void OnMoveNext(object? sender, EventArgs e)
    {
        if (Validate() && _bindingSource is not null)
        {
            _bindingSource.MoveNext();
            RefreshItemsInternal();
        }
    }

    private void OnMoveLast(object? sender, EventArgs e)
    {
        if (Validate() && _bindingSource is not null)
        {
            _bindingSource.MoveLast();
            RefreshItemsInternal();
        }
    }

    private void OnAddNew(object? sender, EventArgs e)
    {
        if (Validate() && _bindingSource is not null)
        {
            _bindingSource.AddNew();
            RefreshItemsInternal();
        }
    }

    private void OnDelete(object? sender, EventArgs e)
    {
        if (Validate() && _bindingSource is not null)
        {
            _bindingSource.RemoveCurrent();
            RefreshItemsInternal();
        }
    }

    private void OnPositionKey(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                AcceptNewPosition();
                break;
            case Keys.Escape:
                CancelNewPosition();
                break;
        }
    }

    private void OnPositionLostFocus(object? sender, EventArgs e) => AcceptNewPosition();

    private void OnBindingSourceStateChanged(object? sender, EventArgs e) => RefreshItemsInternal();

    private void OnBindingSourceDisposed(object? sender, EventArgs e) => BindingSource = null;

    private void OnBindingSourceListChanged(object? sender, ListChangedEventArgs e) => RefreshItemsInternal();

    private void RefreshItemsInternal()
    {
        // Nothing during the designer code; and one refresh at a time (Enabled and Text raise events of their own).
        if (_initializing || _refreshing) return;
        _refreshing = true;
        try
        {
            OnRefreshItems();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ResetCountItemFormat() => _countItemFormat = DefaultCountItemFormat;

    private bool ShouldSerializeCountItemFormat() => _countItemFormat != DefaultCountItemFormat;

    private void OnAddNewItemEnabledChanged(object? sender, EventArgs e)
    {
        if (AddNewItem is not null && !_refreshing) _addNewItemUserEnabled = _addNewItem!.Enabled;
    }

    private void OnDeleteItemEnabledChanged(object? sender, EventArgs e)
    {
        if (DeleteItem is not null && !_refreshing) _deleteItemUserEnabled = _deleteItem!.Enabled;
    }

    private static ToolStripItem? Live(ref ToolStripItem? item)
    {
        if (item is not null && item.IsDisposed) item = null;
        return item;
    }

    private void WireUpButton(ref ToolStripItem? oldButton, ToolStripItem? newButton, EventHandler clickHandler)
    {
        if (oldButton == newButton) return;
        if (oldButton is not null) oldButton.Click -= clickHandler;
        if (newButton is not null) newButton.Click += clickHandler;
        oldButton = newButton;
        RefreshItemsInternal();
    }

    private void WireUpTextBox(ref ToolStripItem? oldTextBox, ToolStripItem? newTextBox)
    {
        if (oldTextBox == newTextBox) return;
        if (oldTextBox is ToolStripControlHost oldHost)
        {
            oldHost.Control.KeyUp -= OnPositionKey;
            oldHost.Control.LostFocus -= OnPositionLostFocus;
        }
        if (newTextBox is ToolStripControlHost newHost)
        {
            newHost.Control.KeyUp += OnPositionKey;
            newHost.Control.LostFocus += OnPositionLostFocus;
        }
        oldTextBox = newTextBox;
        RefreshItemsInternal();
    }

    private void WireUpLabel(ref ToolStripItem? oldLabel, ToolStripItem? newLabel)
    {
        if (oldLabel == newLabel) return;
        oldLabel = newLabel;
        RefreshItemsInternal();
    }

    private void WireUpBindingSource(ref BindingSource? oldBindingSource, BindingSource? newBindingSource)
    {
        if (oldBindingSource == newBindingSource) return;
        if (oldBindingSource is not null)
        {
            oldBindingSource.PositionChanged -= OnBindingSourceStateChanged;
            oldBindingSource.CurrentChanged -= OnBindingSourceStateChanged;
            oldBindingSource.CurrentItemChanged -= OnBindingSourceStateChanged;
            oldBindingSource.DataSourceChanged -= OnBindingSourceStateChanged;
            oldBindingSource.DataMemberChanged -= OnBindingSourceStateChanged;
            oldBindingSource.ListChanged -= OnBindingSourceListChanged;
            oldBindingSource.Disposed -= OnBindingSourceDisposed;
        }
        if (newBindingSource is not null)
        {
            newBindingSource.PositionChanged += OnBindingSourceStateChanged;
            newBindingSource.CurrentChanged += OnBindingSourceStateChanged;
            newBindingSource.CurrentItemChanged += OnBindingSourceStateChanged;
            newBindingSource.DataSourceChanged += OnBindingSourceStateChanged;
            newBindingSource.DataMemberChanged += OnBindingSourceStateChanged;
            newBindingSource.ListChanged += OnBindingSourceListChanged;
            newBindingSource.Disposed += OnBindingSourceDisposed;
        }
        oldBindingSource = newBindingSource;
        RefreshItemsInternal();
    }

    private static readonly ConditionalWeakTable<Image, object> s_standardImages = new();

    /// <summary>
    /// One of the pictures <see cref="AddStandardItems"/> gives the items. The designer does not write these to the
    /// form's code (VS puts them in the .resx, which NetForms' designer does not write): the navigator puts them back,
    /// see <see cref="RestoreStandardImages"/>.
    /// </summary>
    internal static bool IsStandardImage(object? image) => image is Image i && s_standardImages.TryGetValue(i, out _);

    /// <summary>
    /// A standard item that shows only its image but has none (the form's code was written by NetForms' designer)
    /// gets the standard picture back.
    /// </summary>
    private void RestoreStandardImages()
    {
        Restore(_moveFirstItem, 0);
        Restore(_movePreviousItem, 1);
        Restore(_moveNextItem, 2);
        Restore(_moveLastItem, 3);
        Restore(_addNewItem, 4);
        Restore(_deleteItem, 5);

        static void Restore(ToolStripItem? item, int index)
        {
            if (item is { Image: null, DisplayStyle: ToolStripItemDisplayStyle.Image, IsDisposed: false }) item.Image = NavigatorIcon(index);
        }
    }

    /// <summary>The pictures of the standard items (WinForms ships them as icons): first, previous, next, last, add, delete.</summary>
    internal static Bitmap NavigatorIcon(int index)
    {
        var bitmap = new Bitmap(16, 16);
        s_standardImages.AddOrUpdate(bitmap, index);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var blue = Color.FromArgb(0x1E, 0x5A, 0xA8);
        using var fill = new SolidBrush(blue);
        switch (index)
        {
            case 0: // |<
                g.FillRectangle(fill, 3, 3, 2, 10);
                g.FillPolygon(fill, new PointF[] { new(12, 3), new(12, 13), new(6, 8) });
                break;
            case 1: // <
                g.FillPolygon(fill, new PointF[] { new(11, 3), new(11, 13), new(5, 8) });
                break;
            case 2: // >
                g.FillPolygon(fill, new PointF[] { new(5, 3), new(5, 13), new(11, 8) });
                break;
            case 3: // >|
                g.FillPolygon(fill, new PointF[] { new(4, 3), new(4, 13), new(10, 8) });
                g.FillRectangle(fill, 11, 3, 2, 10);
                break;
            case 4: // + (add)
                using (var green = new SolidBrush(Color.FromArgb(0x2E, 0x8B, 0x3A)))
                {
                    g.FillRectangle(green, 7, 2, 3, 12);
                    g.FillRectangle(green, 2, 7, 12, 3);
                }
                break;
            default: // x (delete)
                using (var red = new Pen(Color.FromArgb(0xC4, 0x2B, 0x1C), 2.5f))
                {
                    g.DrawLine(red, 3, 3, 13, 13);
                    g.DrawLine(red, 13, 3, 3, 13);
                }
                break;
        }
        return bitmap;
    }
}
