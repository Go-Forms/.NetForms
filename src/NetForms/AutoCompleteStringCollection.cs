using System.Collections;
using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>The strings a TextBox or ComboBox completes from when AutoCompleteSource is CustomSource.</summary>
public class AutoCompleteStringCollection : IList
{
    private readonly ArrayList _data = new();

    public event CollectionChangeEventHandler? CollectionChanged;

    protected void OnCollectionChanged(CollectionChangeEventArgs e) => CollectionChanged?.Invoke(this, e);

    public string this[int index]
    {
        get => (string)_data[index]!;
        set
        {
            OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Remove, _data[index]));
            _data[index] = value;
            OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Add, value));
        }
    }

    public int Count => _data.Count;

    bool IList.IsReadOnly => false;

    bool IList.IsFixedSize => false;

    public bool IsReadOnly => false;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = (string)value!;
    }

    public int Add(string value)
    {
        int index = _data.Add(value);
        OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Add, value));
        return index;
    }

    public void AddRange(string[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _data.AddRange(value);
        OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Refresh, null));
    }

    public void Clear()
    {
        _data.Clear();
        OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Refresh, null));
    }

    public bool Contains(string value) => _data.Contains(value);

    public void CopyTo(string[] array, int index) => _data.CopyTo(array, index);

    public int IndexOf(string value) => _data.IndexOf(value);

    public void Insert(int index, string value)
    {
        _data.Insert(index, value);
        OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Add, value));
    }

    public void Remove(string value)
    {
        _data.Remove(value);
        OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Remove, value));
    }

    public void RemoveAt(int index)
    {
        var value = _data[index];
        _data.RemoveAt(index);
        OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Remove, value));
    }

    public IEnumerator GetEnumerator() => _data.GetEnumerator();

    int IList.Add(object? value) => Add((string)value!);

    bool IList.Contains(object? value) => Contains((string)value!);

    int IList.IndexOf(object? value) => IndexOf((string)value!);

    void IList.Insert(int index, object? value) => Insert(index, (string)value!);

    void IList.Remove(object? value) => Remove((string)value!);

    void ICollection.CopyTo(Array array, int index) => _data.CopyTo(array, index);
}
