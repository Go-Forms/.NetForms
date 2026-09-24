using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum PropertySort
{
    NoSort = 0,
    Alphabetical = 1,
    Categorized = 2,
    CategorizedAlphabetical = 3,
}

public enum GridItemType
{
    Property = 0,
    Category = 1,
    ArrayValue = 2,
    Root = 3,
}

/// <summary>One row of a <see cref="PropertyGrid"/>: a category band, a property, or the root.</summary>
public abstract class GridItem
{
    public abstract GridItemCollection GridItems { get; }

    public abstract GridItemType GridItemType { get; }

    public abstract string? Label { get; }

    public abstract GridItem? Parent { get; }

    public abstract PropertyDescriptor? PropertyDescriptor { get; }

    public abstract object? Value { get; }

    public virtual bool Expandable => false;

    public virtual bool Expanded { get; set; }

    public object? Tag { get; set; }

    public abstract bool Select();
}

public class GridItemCollection : IEnumerable<GridItem>
{
    public static readonly GridItemCollection Empty = new(Array.Empty<GridItem>());

    private readonly List<GridItem> _items;

    internal GridItemCollection(IEnumerable<GridItem> items) => _items = new List<GridItem>(items);

    public int Count => _items.Count;

    public GridItem this[int index] => _items[index];

    public GridItem? this[string? label]
    {
        get
        {
            foreach (var item in _items)
            {
                if (string.Equals(item.Label, label, StringComparison.Ordinal)) return item;
            }
            return null;
        }
    }

    internal void Add(GridItem item) => _items.Add(item);

    public IEnumerator<GridItem> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

// --- event args --------------------------------------------------------------------------

public delegate void PropertyValueChangedEventHandler(object? s, PropertyValueChangedEventArgs e);
public delegate void SelectedGridItemChangedEventHandler(object? sender, SelectedGridItemChangedEventArgs e);

public class PropertyValueChangedEventArgs : EventArgs
{
    public PropertyValueChangedEventArgs(GridItem? changedItem, object? oldValue)
    {
        ChangedItem = changedItem;
        OldValue = oldValue;
    }

    public GridItem? ChangedItem { get; }
    public object? OldValue { get; }
}

public class SelectedGridItemChangedEventArgs : EventArgs
{
    public SelectedGridItemChangedEventArgs(GridItem? oldSelection, GridItem? newSelection)
    {
        OldSelection = oldSelection;
        NewSelection = newSelection;
    }

    public GridItem? OldSelection { get; }
    public GridItem? NewSelection { get; }
}
