using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>
/// The indirection between a control and its data: it wraps a list (or a type to build one from),
/// tracks the current item, forwards the list's change notifications and offers sorting and
/// filtering when the underlying list supports them.
/// </summary>
[DefaultProperty(nameof(DataSource))]
[DefaultEvent(nameof(CurrentChanged))]
public class BindingSource : Component, IBindingListView, ITypedList, ICancelAddNew, ISupportInitializeNotification
{
    private object? _dataSource;
    private string _dataMember = string.Empty;
    private IList _list = new List<object>();
    private int _position = -1;
    private bool _initializing;
    // Null until set, as in WinForms - "no sort" and "sorted by nothing" are different states.
    private string? _sort;
    private string? _filter;

    public BindingSource() { }

    public BindingSource(IContainer container) : this()
    {
        container?.Add(this);
    }

    public BindingSource(object? dataSource, string? dataMember)
    {
        _dataSource = dataSource;
        _dataMember = dataMember ?? string.Empty;
        ResetList();
    }

    // --- events -----------------------------------------------------------------------------

    [Category("Data")]
    [Description("Event raised when the value of Current changes.")]
    public event EventHandler? CurrentChanged;

    [Category("Data")]
    [Description("Event raised when the value of Current changes, or a property of the current item changes.")]
    public event EventHandler? CurrentItemChanged;

    [Category("Data")]
    [Description("Event raised when a change occurs in the BindingSource's list.")]
    public event ListChangedEventHandler? ListChanged;

    [Category("Data")]
    [Description("Event raised when the DataSource changes.")]
    public event EventHandler? DataSourceChanged;

    [Category("Data")]
    [Description("Event raised when the DataMember changes.")]
    public event EventHandler? DataMemberChanged;

    [Category("Data")]
    [Description("Event raised when the value of Position changes.")]
    public event EventHandler? PositionChanged;

    [Category("Data")]
    [Description("Event raised when the user calls AddNew on the BindingSource")]
    public event AddingNewEventHandler? AddingNew;

    [Category("Data")]
    [Description("Event raised after data has been exchanged between the data source and a control property bound to that data source.")]
    public event BindingCompleteEventHandler? BindingComplete;
    public event EventHandler? Initialized;

    protected virtual void OnCurrentChanged(EventArgs e) => CurrentChanged?.Invoke(this, e);
    protected virtual void OnCurrentItemChanged(EventArgs e) => CurrentItemChanged?.Invoke(this, e);
    protected virtual void OnListChanged(ListChangedEventArgs e) => ListChanged?.Invoke(this, e);
    protected virtual void OnDataSourceChanged(EventArgs e) => DataSourceChanged?.Invoke(this, e);
    protected virtual void OnDataMemberChanged(EventArgs e) => DataMemberChanged?.Invoke(this, e);
    protected virtual void OnPositionChanged(EventArgs e) => PositionChanged?.Invoke(this, e);
    protected virtual void OnAddingNew(AddingNewEventArgs e) => AddingNew?.Invoke(this, e);
    protected virtual void OnBindingComplete(BindingCompleteEventArgs e) => BindingComplete?.Invoke(this, e);

    // --- source -------------------------------------------------------------------------------

    [Category("Data")]
    [Description("Indicates the source of data for the BindingSource.")]
    [DefaultValue(null)]
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (ReferenceEquals(_dataSource, value)) return;
            _dataSource = value;
            ResetList();
            OnDataSourceChanged(EventArgs.Empty);
        }
    }

    [Category("Data")]
    [Description("Indicates a sub-list of the DataSource that the BindingSource is bound to.")]
    [DefaultValue("")]
    public string DataMember
    {
        get => _dataMember;
        set
        {
            value ??= string.Empty;
            if (_dataMember == value) return;
            _dataMember = value;
            ResetList();
            OnDataMemberChanged(EventArgs.Empty);
        }
    }

    [Browsable(false)]
    public IList List => _list;

    private void ResetList()
    {
        if (_list is IBindingList old) old.ListChanged -= ForwardListChanged;

        object? resolved = _dataSource switch
        {
            null => null,
            IListSource source => source.GetList(),
            _ => _dataSource,
        };
        if (resolved != null && _dataMember.Length > 0)
        {
            var property = TypeDescriptor.GetProperties(resolved).Find(_dataMember, true);
            resolved = property?.GetValue(resolved);
            if (resolved is IListSource inner) resolved = inner.GetList();
        }

        _list = resolved switch
        {
            IList list => list,
            // A bare type means "an empty, typed list I can add to".
            Type type => CreateTypedList(type),
            null => new List<object>(),
            IEnumerable enumerable => ToList(enumerable),
            _ => new List<object> { resolved },
        };

        if (_list is IBindingList bindingList) bindingList.ListChanged += ForwardListChanged;
        _position = _list.Count > 0 ? 0 : -1;
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        OnCurrentChanged(EventArgs.Empty);
    }

    private static IList CreateTypedList(Type itemType)
    {
        var listType = typeof(BindingList<>).MakeGenericType(itemType);
        return (IList)Activator.CreateInstance(listType)!;
    }

    private static IList ToList(IEnumerable source)
    {
        var list = new List<object>();
        foreach (var item in source) list.Add(item);
        return list;
    }

    private void ForwardListChanged(object? sender, ListChangedEventArgs e)
    {
        OnListChanged(e);
        if (_position >= _list.Count) Position = _list.Count - 1;
        OnCurrentItemChanged(EventArgs.Empty);
    }

    // --- current item ---------------------------------------------------------------------------

    [Browsable(false)]
    public object? Current => _position >= 0 && _position < _list.Count ? _list[_position] : null;

    [DefaultValue(-1)]
    [Browsable(false)]
    public int Position
    {
        get => _position;
        set
        {
            int next = _list.Count == 0 ? -1 : Math.Clamp(value, 0, _list.Count - 1);
            if (_position == next) return;
            _position = next;
            OnPositionChanged(EventArgs.Empty);
            OnCurrentChanged(EventArgs.Empty);
        }
    }

    public void MoveFirst() => Position = 0;
    public void MoveLast() => Position = _list.Count - 1;
    public void MoveNext() => Position = _position + 1;
    public void MovePrevious() => Position = _position - 1;

    // --- list operations -------------------------------------------------------------------------

    [Browsable(false)]
    public int Count => _list.Count;

    [Browsable(false)]
    public bool IsReadOnly => _list.IsReadOnly;

    [Browsable(false)]
    public bool IsFixedSize => _list.IsFixedSize;

    [Browsable(false)]
    public bool IsSynchronized => _list.IsSynchronized;

    [Browsable(false)]
    public object SyncRoot => _list.SyncRoot;

    public object? this[int index]
    {
        get => _list[index];
        set
        {
            _list[index] = value;
            OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, index));
        }
    }

    public int Add(object? value)
    {
        int index = _list.Add(value);
        if (_list is not IBindingList) OnListChanged(new ListChangedEventArgs(ListChangedType.ItemAdded, index));
        return index;
    }

    public object AddNew()
    {
        var e = new AddingNewEventArgs();
        OnAddingNew(e);
        if (e.NewObject != null)
        {
            Add(e.NewObject);
            Position = _list.Count - 1;
            return e.NewObject;
        }
        if (_list is IBindingList { AllowNew: true } bindingList)
        {
            var added = bindingList.AddNew();
            Position = _list.Count - 1;
            return added!;
        }
        throw new InvalidOperationException("The list does not support adding new items; handle AddingNew.");
    }

    public void Clear()
    {
        _list.Clear();
        _position = -1;
        if (_list is not IBindingList) OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        OnCurrentChanged(EventArgs.Empty);
    }

    public bool Contains(object? value) => _list.Contains(value);
    public void CopyTo(Array array, int index) => _list.CopyTo(array, index);
    public IEnumerator GetEnumerator() => _list.GetEnumerator();
    public int IndexOf(object? value) => _list.IndexOf(value);

    public void Insert(int index, object? value)
    {
        _list.Insert(index, value);
        if (_list is not IBindingList) OnListChanged(new ListChangedEventArgs(ListChangedType.ItemAdded, index));
    }

    public void Remove(object? value)
    {
        int index = _list.IndexOf(value);
        if (index < 0) return;
        RemoveAt(index);
    }

    public void RemoveAt(int index)
    {
        _list.RemoveAt(index);
        if (_position >= _list.Count) _position = _list.Count - 1;
        if (_list is not IBindingList) OnListChanged(new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
        OnCurrentChanged(EventArgs.Empty);
    }

    public void RemoveCurrent()
    {
        if (_position < 0) throw new InvalidOperationException("There is no current item to remove.");
        RemoveAt(_position);
    }

    public void EndEdit()
    {
        if (Current is IEditableObject editable) editable.EndEdit();
    }

    public void CancelEdit()
    {
        if (Current is IEditableObject editable) editable.CancelEdit();
    }

    public void ResetBindings(bool metadataChanged)
    {
        OnListChanged(new ListChangedEventArgs(metadataChanged ? ListChangedType.PropertyDescriptorChanged : ListChangedType.Reset, -1));
    }

    public void ResetCurrentItem() => OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, _position));

    public void ResetItem(int itemIndex) => OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, itemIndex));

    // --- sorting and filtering ---------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Determines whether the BindingSource allows new items to be added to the list.")]
    public bool AllowNew => _list is IBindingList { AllowNew: true } || _list is not IBindingList && !_list.IsFixedSize;

    [Browsable(false)]
    public bool AllowEdit => _list is not IBindingList bindingList || bindingList.AllowEdit;

    [Browsable(false)]
    public bool AllowRemove => _list is IBindingList bindingList ? bindingList.AllowRemove : !_list.IsFixedSize;

    [Browsable(false)]
    public bool SupportsChangeNotification => _list is IBindingList;

    [Browsable(false)]
    public bool SupportsSearching => _list is IBindingList { SupportsSearching: true };

    [Browsable(false)]
    public bool SupportsSorting => _list is IBindingList { SupportsSorting: true };

    [Browsable(false)]
    public bool SupportsAdvancedSorting => _list is IBindingListView { SupportsAdvancedSorting: true };

    [Browsable(false)]
    public bool SupportsFiltering => _list is IBindingListView { SupportsFiltering: true };

    [Browsable(false)]
    public bool IsSorted => _list is IBindingList { IsSorted: true };

    [Category("Data")]
    [Description("Indicates names of database columns used to sort the set of rows returned by the data source.")]
    [DefaultValue(null)]
    public string? Sort
    {
        get => _sort;
        set
        {
            _sort = value;
            if (_list is IBindingListView view) view.Filter = view.Filter;
            ApplySort();
        }
    }

    [Category("Data")]
    [Description("Indicates a database column expression used to filter the set of rows returned by the data source.")]
    [DefaultValue(null)]
    public string? Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            if (_list is IBindingListView view) view.Filter = _filter;
        }
    }

    private void ApplySort()
    {
        if (string.IsNullOrEmpty(_sort))
        {
            if (_list is IBindingList { SupportsSorting: true } sortable) sortable.RemoveSort();
            return;
        }
        if (_list is not IBindingList { SupportsSorting: true } list) return;

        var parts = _sort.Split(',')[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var direction = parts.Length > 1 && parts[1].StartsWith("DESC", StringComparison.OrdinalIgnoreCase)
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        var property = GetItemProperties(null).Find(parts[0], true);
        if (property != null) list.ApplySort(property, direction);
    }

    [Browsable(false)]
    public PropertyDescriptor? SortProperty => (_list as IBindingList)?.SortProperty;

    [Browsable(false)]
    public ListSortDirection SortDirection => (_list as IBindingList)?.SortDirection ?? ListSortDirection.Ascending;

    [Browsable(false)]
    public ListSortDescriptionCollection? SortDescriptions => (_list as IBindingListView)?.SortDescriptions;

    public int Find(PropertyDescriptor property, object key)
    {
        if (_list is IBindingList { SupportsSearching: true } list) return list.Find(property, key);
        for (int i = 0; i < _list.Count; i++)
        {
            if (Equals(property.GetValue(_list[i]), key)) return i;
        }
        return -1;
    }

    public int Find(string propertyName, object key)
    {
        var property = GetItemProperties(null).Find(propertyName, true)
            ?? throw new ArgumentException("No property named '" + propertyName + "'.", nameof(propertyName));
        return Find(property, key);
    }

    void IBindingList.AddIndex(PropertyDescriptor property) => (_list as IBindingList)?.AddIndex(property);
    void IBindingList.RemoveIndex(PropertyDescriptor property) => (_list as IBindingList)?.RemoveIndex(property);
    void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction) => (_list as IBindingList)?.ApplySort(property, direction);
    void IBindingListView.ApplySort(ListSortDescriptionCollection sorts) => (_list as IBindingListView)?.ApplySort(sorts);
    public void RemoveSort()
    {
        _sort = null;
        (_list as IBindingList)?.RemoveSort();
    }
    public void RemoveFilter()
    {
        _filter = null;
        (_list as IBindingListView)?.RemoveFilter();
    }

    // --- ITypedList ---------------------------------------------------------------------------------

    public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[]? listAccessors)
    {
        if (_list is ITypedList typed) return typed.GetItemProperties(listAccessors);
        var itemType = ItemType;
        return itemType != null ? TypeDescriptor.GetProperties(itemType) : new PropertyDescriptorCollection(null);
    }

    public string GetListName(PropertyDescriptor[]? listAccessors)
    {
        if (_list is ITypedList typed) return typed.GetListName(listAccessors);
        return ItemType?.Name ?? string.Empty;
    }

    private Type? ItemType
    {
        get
        {
            if (_dataSource is Type type) return type;
            var generic = _list.GetType().GetInterface("IList`1")?.GetGenericArguments()[0];
            if (generic != null && generic != typeof(object)) return generic;
            return _list.Count > 0 ? _list[0]?.GetType() : null;
        }
    }

    // --- ICancelAddNew / ISupportInitializeNotification ------------------------------------------------

    void ICancelAddNew.CancelNew(int itemIndex) => (_list as ICancelAddNew)?.CancelNew(itemIndex);

    void ICancelAddNew.EndNew(int itemIndex) => (_list as ICancelAddNew)?.EndNew(itemIndex);

    public bool IsInitialized => !_initializing;

    public void BeginInit() => _initializing = true;

    public void EndInit()
    {
        _initializing = false;
        ResetList();
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    public void SuspendBinding() { }

    public void ResumeBinding() { }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _list is IBindingList bindingList) bindingList.ListChanged -= ForwardListChanged;
        base.Dispose(disposing);
    }
}

public delegate void BindingCompleteEventHandler(object? sender, BindingCompleteEventArgs e);

/// <summary>Handler for a binding's Format/Parse hooks (ConvertEventArgs itself comes from the BCL).</summary>
public delegate void ConvertEventHandler(object? sender, ConvertEventArgs e);

public enum BindingCompleteState
{
    Success = 0,
    DataError = 1,
    Exception = 2,
}

public enum BindingCompleteContext
{
    ControlUpdate = 0,
    DataSourceUpdate = 1,
}

public class BindingCompleteEventArgs : CancelEventArgs
{
    public BindingCompleteEventArgs(Binding? binding, BindingCompleteState state, BindingCompleteContext context)
        : this(binding, state, context, string.Empty, null, false) { }

    public BindingCompleteEventArgs(Binding? binding, BindingCompleteState state, BindingCompleteContext context, string? errorText)
        : this(binding, state, context, errorText, null, true) { }

    public BindingCompleteEventArgs(Binding? binding, BindingCompleteState state, BindingCompleteContext context, string? errorText, Exception? exception)
        : this(binding, state, context, errorText, exception, true) { }

    public BindingCompleteEventArgs(Binding? binding, BindingCompleteState state, BindingCompleteContext context, string? errorText, Exception? exception, bool cancel)
        : base(cancel)
    {
        Binding = binding;
        BindingCompleteState = state;
        BindingCompleteContext = context;
        ErrorText = errorText ?? string.Empty;
        Exception = exception;
    }

    public Binding? Binding { get; }
    public BindingCompleteState BindingCompleteState { get; }
    public BindingCompleteContext BindingCompleteContext { get; }
    public string ErrorText { get; }
    public Exception? Exception { get; }
}

/// <summary>
/// A one-way-plus-commit link between a control property and a data-source property - enough for
/// <c>textBox.DataBindings.Add("Text", source, "Name")</c> to work in both directions.
/// </summary>
public class Binding
{
    private Control? _control;
    private BindingSource? _source;
    private PropertyDescriptor? _controlProperty;
    private PropertyDescriptor? _dataProperty;
    private bool _updating;

    public Binding(string propertyName, object? dataSource, string? dataMember)
        : this(propertyName, dataSource, dataMember, false) { }

    public Binding(string propertyName, object? dataSource, string? dataMember, bool formattingEnabled)
    {
        PropertyName = propertyName ?? string.Empty;
        DataSource = dataSource;
        BindingMemberInfo = new BindingMemberInfo(dataMember);
        FormattingEnabled = formattingEnabled;
    }

    public string PropertyName { get; }
    public object? DataSource { get; }
    public BindingMemberInfo BindingMemberInfo { get; }
    public bool FormattingEnabled { get; set; }
    public string FormatString { get; set; } = string.Empty;
    public object? NullValue { get; set; }
    public DataSourceUpdateMode DataSourceUpdateMode { get; set; } = DataSourceUpdateMode.OnValidation;
    public ControlUpdateMode ControlUpdateMode { get; set; } = ControlUpdateMode.OnPropertyChanged;

    public Control? Control => _control;

    public event ConvertEventHandler? Format;
    public event ConvertEventHandler? Parse;
    public event BindingCompleteEventHandler? BindingComplete;

    internal void SetControl(Control control)
    {
        _control = control;
        _controlProperty = TypeDescriptor.GetProperties(control).Find(PropertyName, true);
        _source = DataSource as BindingSource ?? (DataSource != null ? new BindingSource(DataSource, null) : null);
        if (_source != null)
        {
            _dataProperty = _source.GetItemProperties(null).Find(BindingMemberInfo.BindingField, true);
            _source.CurrentChanged += (_, _) => ReadValue();
            _source.ListChanged += (_, _) => ReadValue();
        }
        if (_controlProperty != null) _controlProperty.AddValueChanged(control, (_, _) => WriteValue());
        ReadValue();
    }

    public void ReadValue()
    {
        if (_updating || _control == null || _controlProperty == null || _source?.Current == null || _dataProperty == null) return;
        _updating = true;
        try
        {
            object? value = _dataProperty.GetValue(_source.Current);
            if (Format != null)
            {
                var e = new ConvertEventArgs(value, _controlProperty.PropertyType);
                Format(this, e);
                value = e.Value;
            }
            else if (FormattingEnabled && FormatString.Length > 0 && value is IFormattable formattable)
            {
                value = formattable.ToString(FormatString, null);
            }
            _controlProperty.SetValue(_control, Convert(value, _controlProperty.PropertyType));
            OnBindingComplete(CreateBindingCompleteEventArgs(BindingCompleteContext.ControlUpdate, null));
        }
        catch (Exception ex)
        {
            OnBindingComplete(CreateBindingCompleteEventArgs(BindingCompleteContext.ControlUpdate, ex));
        }
        finally
        {
            _updating = false;
        }
    }

    public void WriteValue()
    {
        if (_updating || _control == null || _controlProperty == null || _source?.Current == null || _dataProperty == null) return;
        if (_dataProperty.IsReadOnly) return;
        _updating = true;
        try
        {
            object? value = _controlProperty.GetValue(_control);
            if (Parse != null)
            {
                var e = new ConvertEventArgs(value, _dataProperty.PropertyType);
                Parse(this, e);
                value = e.Value;
            }
            _dataProperty.SetValue(_source.Current, Convert(value, _dataProperty.PropertyType));
            OnBindingComplete(CreateBindingCompleteEventArgs(BindingCompleteContext.DataSourceUpdate, null));
        }
        catch (Exception ex)
        {
            OnBindingComplete(CreateBindingCompleteEventArgs(BindingCompleteContext.DataSourceUpdate, ex));
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>
    /// As in WinForms: an exception is reported (and cancels); otherwise the data source's
    /// <see cref="IDataErrorInfo"/> text for the bound field makes it a data error - that is what
    /// ErrorProvider shows next to the control.
    /// </summary>
    private BindingCompleteEventArgs CreateBindingCompleteEventArgs(BindingCompleteContext context, Exception? ex)
    {
        if (ex != null) return new BindingCompleteEventArgs(this, BindingCompleteState.Exception, context, ex.Message, ex, true);
        string errorText = _source?.Current is IDataErrorInfo info ? info[BindingMemberInfo.BindingField] ?? string.Empty : string.Empty;
        var state = errorText.Length > 0 ? BindingCompleteState.DataError : BindingCompleteState.Success;
        return new BindingCompleteEventArgs(this, state, context, errorText, null, false);
    }

    protected virtual void OnBindingComplete(BindingCompleteEventArgs e) => BindingComplete?.Invoke(this, e);

    private object? Convert(object? value, Type target)
    {
        if (value == null) return NullValue;
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (underlying.IsInstanceOfType(value)) return value;
        return System.Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.CurrentCulture);
    }
}

public readonly struct BindingMemberInfo : IEquatable<BindingMemberInfo>
{
    public BindingMemberInfo(string? dataMember)
    {
        dataMember ??= string.Empty;
        int dot = dataMember.LastIndexOf('.');
        BindingPath = dot >= 0 ? dataMember[..dot] : string.Empty;
        BindingField = dot >= 0 ? dataMember[(dot + 1)..] : dataMember;
    }

    public string BindingPath { get; }
    public string BindingField { get; }
    public string BindingMember => BindingPath.Length > 0 ? BindingPath + "." + BindingField : BindingField;

    public bool Equals(BindingMemberInfo other) => BindingMember == other.BindingMember;
    public override bool Equals(object? obj) => obj is BindingMemberInfo other && Equals(other);
    public override int GetHashCode() => BindingMember.GetHashCode();
    public static bool operator ==(BindingMemberInfo a, BindingMemberInfo b) => a.Equals(b);
    public static bool operator !=(BindingMemberInfo a, BindingMemberInfo b) => !a.Equals(b);
}

public enum DataSourceUpdateMode
{
    OnValidation = 0,
    OnPropertyChanged = 1,
    Never = 2,
}

public enum ControlUpdateMode
{
    OnPropertyChanged = 0,
    Never = 1,
}

/// <summary>The bindings attached to one control (WinForms' Control.DataBindings).</summary>
public class ControlBindingsCollection : IEnumerable<Binding>
{
    private readonly Control _owner;
    private readonly List<Binding> _bindings = new();

    internal ControlBindingsCollection(Control control) => _owner = control;

    public Control Control => _owner;

    public int Count => _bindings.Count;

    public Binding this[int index] => _bindings[index];

    public Binding Add(string propertyName, object dataSource, string dataMember)
    {
        var binding = new Binding(propertyName, dataSource, dataMember);
        Add(binding);
        return binding;
    }

    public Binding Add(string propertyName, object dataSource, string dataMember, bool formattingEnabled)
    {
        var binding = new Binding(propertyName, dataSource, dataMember, formattingEnabled);
        Add(binding);
        return binding;
    }

    public void Add(Binding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _bindings.Add(binding);
        binding.SetControl(_owner);
    }

    public void Clear() => _bindings.Clear();

    public void Remove(Binding binding) => _bindings.Remove(binding);

    public void RemoveAt(int index) => _bindings.RemoveAt(index);

    public IEnumerator<Binding> GetEnumerator() => _bindings.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _bindings.GetEnumerator();
}
