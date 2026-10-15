using System;
using System.Collections;
using System.Collections.Generic;

namespace System.Windows.Forms.Layout;

/// <summary>
/// A read-only list of the elements a layout arranges (WinForms API). WinForms derives
/// Control.ControlCollection from it too; here only <see cref="ToolStripPanel.ToolStripPanelRowCollection"/> does,
/// the control collection keeps its own list.
/// </summary>
public class ArrangedElementCollection : IList
{
    private protected readonly List<object> InnerList;

    internal ArrangedElementCollection() => InnerList = new List<object>();

    internal ArrangedElementCollection(int capacity) => InnerList = new List<object>(capacity);

    public virtual int Count => InnerList.Count;

    public virtual bool IsReadOnly => true;

    bool IList.IsFixedSize => false;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    object? IList.this[int index]
    {
        get => InnerList[index];
        set => throw new NotSupportedException();
    }

    public override bool Equals(object? obj) =>
        obj is ArrangedElementCollection other && other.InnerList.Count == InnerList.Count && System.Linq.Enumerable.SequenceEqual(other.InnerList, InnerList);

    public override int GetHashCode() => base.GetHashCode();

    public void CopyTo(Array array, int index) => ((ICollection)InnerList).CopyTo(array, index);

    public virtual IEnumerator GetEnumerator() => InnerList.GetEnumerator();

    int IList.Add(object? value) => throw new NotSupportedException();

    void IList.Clear() => throw new NotSupportedException();

    bool IList.Contains(object? value) => value != null && InnerList.Contains(value);

    int IList.IndexOf(object? value) => value == null ? -1 : InnerList.IndexOf(value);

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();

    void IList.RemoveAt(int index) => throw new NotSupportedException();
}
