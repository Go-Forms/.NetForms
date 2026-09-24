using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;

namespace System.Windows.Forms;

/// <summary>Shared base of ListBox and ComboBox: DataSource/DisplayMember/ValueMember and item text.</summary>
public abstract class ListControl : Control
{
    private object? _dataSource;
    private string _displayMember = string.Empty;
    private string _valueMember = string.Empty;
    private string _formatString = string.Empty;
    private bool _formattingEnabled;

    [Category("Property Changed")]
    [Description("Event raised when the value of the DataSource property is changed on ListControl.")]
    public event EventHandler? DataSourceChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the DisplayMember property is changed on ListControl.")]
    public event EventHandler? DisplayMemberChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the ValueMember property is changed on ListControl.")]
    public event EventHandler? ValueMemberChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the SelectedValue property is changed on ListControl.")]
    public event EventHandler? SelectedValueChanged;

    [Category("Property Changed")]
    [Description("Event raised to allow you to convert the value to a value suitable for display.")]
    public event ListControlConvertEventHandler? Format;

    [Category("Property Changed")]
    [Description("Event raised when the value of the FormatInfo property changed.")]
    [Browsable(false)]
    public event EventHandler? FormatInfoChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the FormatString property is changed.")]
    public event EventHandler? FormatStringChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the FormattingEnabled property is changed.")]
    public event EventHandler? FormattingEnabledChanged;

    [Category("Data")]
    [Description("Indicates the list that this control will use to get its items.")]
    [DefaultValue(null)]
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (value != null && value is not IList && value is not IListSource)
                throw new ArgumentException("DataSource must be an IList or IListSource.", nameof(value));
            if (ReferenceEquals(_dataSource, value)) return;
            _dataSource = value;
            OnDataSourceChanged(EventArgs.Empty);
            SetItemsCore(ResolveList(value));
        }
    }

    [Category("Data")]
    [Description("Indicates the property to display for the items in this control.")]
    [DefaultValue("")]
    public string DisplayMember
    {
        get => _displayMember;
        set
        {
            value ??= string.Empty;
            if (_displayMember == value) return;
            _displayMember = value;
            OnDisplayMemberChanged(EventArgs.Empty);
            RefreshItems();
        }
    }

    [Category("Data")]
    [Description("Indicates the property to use as the actual value for the items in the control.")]
    [DefaultValue("")]
    public string ValueMember
    {
        get => _valueMember;
        set
        {
            value ??= string.Empty;
            if (_valueMember == value) return;
            _valueMember = value;
            OnValueMemberChanged(EventArgs.Empty);
        }
    }

    [Description("The format specifier characters that indicate how a value is to be displayed.")]
    [DefaultValue("")]
    public string FormatString
    {
        get => _formatString;
        set
        {
            value ??= string.Empty;
            if (_formatString == value) return;
            _formatString = value;
            OnFormatStringChanged(EventArgs.Empty);
            RefreshItems();
        }
    }

    [Description("If this property is true, the value of FormatString is used to convert the value of DisplayMember into a value that can be displayed.")]
    [DefaultValue(false)]
    public bool FormattingEnabled
    {
        get => _formattingEnabled;
        set
        {
            if (_formattingEnabled == value) return;
            _formattingEnabled = value;
            OnFormattingEnabledChanged(EventArgs.Empty);
            RefreshItems();
        }
    }

    [DefaultValue(null)]
    [Browsable(false)]
    public IFormatProvider? FormatInfo { get; set; }

    public abstract int SelectedIndex { get; set; }

    [Category("Data")]
    [Description("Indicates the actual value of the currently selected item. Setting it will cause the item whose actual value is equal to become selected.")]
    [DefaultValue(null)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedValue
    {
        get
        {
            int i = SelectedIndex;
            if (i < 0) return null;
            var item = GetItemCore(i);
            return string.IsNullOrEmpty(_valueMember) ? item : GetMember(item, _valueMember);
        }
        set
        {
            if (string.IsNullOrEmpty(_valueMember)) return;
            for (int i = 0; i < GetItemCountCore(); i++)
            {
                if (Equals(GetMember(GetItemCore(i), _valueMember), value))
                {
                    SelectedIndex = i;
                    return;
                }
            }
            SelectedIndex = -1;
        }
    }

    protected virtual void OnDataSourceChanged(EventArgs e) => DataSourceChanged?.Invoke(this, e);
    protected virtual void OnDisplayMemberChanged(EventArgs e) => DisplayMemberChanged?.Invoke(this, e);
    protected virtual void OnValueMemberChanged(EventArgs e) => ValueMemberChanged?.Invoke(this, e);
    protected virtual void OnSelectedValueChanged(EventArgs e) => SelectedValueChanged?.Invoke(this, e);
    protected virtual void OnFormat(ListControlConvertEventArgs e) => Format?.Invoke(this, e);
    protected virtual void OnFormatInfoChanged(EventArgs e) => FormatInfoChanged?.Invoke(this, e);
    protected virtual void OnFormatStringChanged(EventArgs e) => FormatStringChanged?.Invoke(this, e);
    protected virtual void OnFormattingEnabledChanged(EventArgs e) => FormattingEnabledChanged?.Invoke(this, e);

    protected virtual void OnSelectedIndexChanged(EventArgs e) => OnSelectedValueChanged(EventArgs.Empty);

    /// <summary>Replace the items from a data source.</summary>
    protected abstract void SetItemsCore(IList items);

    protected abstract object? GetItemCore(int index);

    protected abstract int GetItemCountCore();

    protected abstract void RefreshItems();

    public string GetItemText(object? item)
    {
        if (item == null) return string.Empty;
        object? value = item;
        if (!string.IsNullOrEmpty(_displayMember))
        {
            value = GetMember(item, _displayMember) ?? string.Empty;
        }

        if (_formattingEnabled)
        {
            var e = new ListControlConvertEventArgs(value, typeof(string), item);
            OnFormat(e);
            if (e.Value is string formatted) return formatted;
            value = e.Value;
            if (!string.IsNullOrEmpty(_formatString) && value is IFormattable f) return f.ToString(_formatString, FormatInfo);
        }
        return value?.ToString() ?? string.Empty;
    }

    private static object? GetMember(object? item, string member)
    {
        if (item == null) return null;
        var type = item.GetType();
        var prop = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop != null) return prop.GetValue(item);
        var field = type.GetField(member, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return field?.GetValue(item);
    }

    private static IList ResolveList(object? source)
    {
        if (source == null) return Array.Empty<object>();
        if (source is IListSource ls) return ls.GetList();
        return (IList)source;
    }
}

public delegate void ListControlConvertEventHandler(object? sender, ListControlConvertEventArgs e);

public class ListControlConvertEventArgs : ConvertEventArgs
{
    public ListControlConvertEventArgs(object? value, Type desiredType, object? listItem) : base(value, desiredType) => ListItem = listItem;

    public object? ListItem { get; }
}

public class ConvertEventArgs : EventArgs
{
    public ConvertEventArgs(object? value, Type desiredType)
    {
        Value = value;
        DesiredType = desiredType;
    }

    public object? Value { get; set; }
    public Type DesiredType { get; }
}
