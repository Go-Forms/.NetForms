// Adapted from dotnet/winforms (src/System.Windows.Forms/System/Windows/Forms/Controls/UpDown/DomainUpDown*.cs).
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>
/// A spin box over a list of strings (or objects shown by their <see cref="object.ToString"/>): the arrows step
/// through <see cref="Items"/>, typing picks the item that starts with the text. WinForms' own implementation
/// over NetForms' <see cref="UpDownBase"/>.
/// </summary>
[DefaultProperty(nameof(Items))]
[DefaultEvent(nameof(SelectedItemChanged))]
[DefaultBindingProperty(nameof(SelectedItem))]
[Description("Represents a Windows spin box (also known as an up-down control) that displays string values.")]
public class DomainUpDown : UpDownBase
{
    private DomainUpDownItemCollection? _domainItems;
    private string _stringValue = string.Empty;
    private int _domainIndex = -1;
    private bool _sorted;
    private bool _inSort;

    public DomainUpDown()
    {
        Text = string.Empty;
    }

    [Category("Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    [Description("The allowable values of the DomainUpDown.")]
    [Localizable(true)]
    [Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public DomainUpDownItemCollection Items => _domainItems ??= new DomainUpDownItemCollection(this);

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding
    {
        get => base.Padding;
        set => base.Padding = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? PaddingChanged
    {
        add => base.PaddingChanged += value;
        remove => base.PaddingChanged -= value;
    }

    [Browsable(false)]
    [DefaultValue(-1)]
    [Category("Appearance")]
    [Description("The index of the selected domain value.")]
    public int SelectedIndex
    {
        get => UserEdit ? -1 : _domainIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, Items.Count);
            if (value != SelectedIndex) SelectIndex(value);
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The currently selected domain value.")]
    public object? SelectedItem
    {
        get
        {
            int index = SelectedIndex;
            return index == -1 ? null : Items[index];
        }
        set
        {
            if (value is null)
            {
                SelectedIndex = -1;
                return;
            }
            for (int i = 0; i < Items.Count; i++)
            {
                if (value.Equals(Items[i]))
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Controls whether items in the domain list are sorted.")]
    public bool Sorted
    {
        get => _sorted;
        set
        {
            _sorted = value;
            if (_sorted) SortDomainItems();
        }
    }

    [Category("Behavior")]
    [Localizable(true)]
    [DefaultValue(false)]
    [Description("Indicates whether values wrap around at either end of the item list.")]
    public bool Wrap { get; set; }

    [Category("Behavior")]
    [Description("Occurs when the selected item in the control changes.")]
    public event EventHandler? SelectedItemChanged;

    /// <summary>The next item (the arrow down), or the item the typed text starts with.</summary>
    public override void DownButton()
    {
        if (_domainItems is null || _domainItems.Count <= 0) return;
        int matchIndex = UserEdit ? MatchIndex(Text, false, _domainIndex) : -1;
        if (matchIndex != -1)
        {
            _domainIndex = matchIndex;
            SelectIndex(matchIndex);
        }
        else if (_domainIndex < _domainItems.Count - 1)
        {
            SelectIndex(_domainIndex + 1);
        }
        else if (Wrap)
        {
            SelectIndex(0);
        }
    }

    /// <summary>The previous item (the arrow up), or the item the typed text starts with.</summary>
    public override void UpButton()
    {
        if (_domainItems is null || _domainItems.Count <= 0) return;
        int matchIndex = UserEdit ? MatchIndex(Text, false, _domainIndex) : -1;
        if (matchIndex != -1)
        {
            _domainIndex = matchIndex;
            SelectIndex(matchIndex);
        }
        else if (_domainIndex > 0)
        {
            SelectIndex(_domainIndex - 1);
        }
        else if (Wrap)
        {
            SelectIndex(_domainItems.Count - 1);
        }
    }

    internal int MatchIndex(string text, bool complete) => MatchIndex(text, complete, _domainIndex);

    /// <summary>The first item from <paramref name="startPosition"/> on (wrapping) equal to or starting with <paramref name="text"/>.</summary>
    internal int MatchIndex(string text, bool complete, int startPosition)
    {
        if (_domainItems is null || text.Length < 1 || _domainItems.Count <= 0) return -1;
        if (startPosition < 0) startPosition = _domainItems.Count - 1;
        if (startPosition >= _domainItems.Count) startPosition = 0;

        int index = startPosition;
        int matchIndex = -1;
        bool found;
        if (!complete) text = text.ToUpper(CultureInfo.InvariantCulture);
        do
        {
            string item = Items[index]?.ToString() ?? string.Empty;
            found = complete
                ? item.Equals(text)
                : item.ToUpper(CultureInfo.InvariantCulture).StartsWith(text, StringComparison.Ordinal);
            if (found) matchIndex = index;
            index++;
            if (index >= _domainItems.Count) index = 0;
        }
        while (!found && index != startPosition);
        return matchIndex;
    }

    protected override void OnChanged(object? source, EventArgs e) => OnSelectedItemChanged(source, e);

    /// <summary>A read-only DomainUpDown jumps to the next item that starts with the typed character.</summary>
    protected override void OnTextBoxKeyPress(object? source, KeyPressEventArgs e)
    {
        if (ReadOnly)
        {
            UnicodeCategory uc = char.GetUnicodeCategory(e.KeyChar);
            if (uc is UnicodeCategory.LetterNumber
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.MathSymbol
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.OtherNumber
                or UnicodeCategory.UppercaseLetter)
            {
                int matchIndex = MatchIndex(e.KeyChar.ToString(), false, _domainIndex + 1);
                if (matchIndex != -1) SelectIndex(matchIndex);
                e.Handled = true;
            }
        }
        base.OnTextBoxKeyPress(source, e);
    }

    protected void OnSelectedItemChanged(object? source, EventArgs e) => SelectedItemChanged?.Invoke(this, e);

    private void SelectIndex(int index)
    {
        if (_domainItems is null || index < -1 || index >= _domainItems.Count) return;
        _domainIndex = index;
        if (_domainIndex >= 0)
        {
            _stringValue = _domainItems[_domainIndex]?.ToString() ?? string.Empty;
            UserEdit = false;
            UpdateEditText();
        }
        else
        {
            UserEdit = true;
        }
    }

    private void SortDomainItems()
    {
        if (_inSort) return;
        _inSort = true;
        try
        {
            if (_domainItems is not null)
            {
                ArrayList.Adapter(_domainItems).Sort(new DomainUpDownItemCompare());
                if (!UserEdit)
                {
                    int newIndex = MatchIndex(_stringValue, true);
                    if (newIndex != -1) SelectIndex(newIndex);
                }
            }
        }
        finally
        {
            _inSort = false;
        }
    }

    public override string ToString()
    {
        string s = base.ToString();
        return $"{s}, Items.Count: {Items.Count}, SelectedIndex: {SelectedIndex}";
    }

    protected override void UpdateEditText()
    {
        UserEdit = false;
        ChangingText = true;
        Text = _stringValue;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = 0;
        if (_domainItems != null)
        {
            foreach (var item in _domainItems)
            {
                width = Math.Max(width, TextRenderer.MeasureText(item?.ToString() ?? string.Empty, Font).Width);
            }
        }
        // The text box plus the buttons and the border.
        return new Size(width + ButtonsWidth + 8, PreferredHeight) + Padding.Size;
    }

    /// <summary>The items: an <see cref="ArrayList"/> that keeps the owner's selection and sort order up to date.</summary>
    public class DomainUpDownItemCollection : ArrayList
    {
        private readonly DomainUpDown _owner;

        internal DomainUpDownItemCollection(DomainUpDown owner)
        {
            _owner = owner;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override object? this[int index]
        {
            get => base[index];
            set
            {
                base[index] = value;
                if (_owner.SelectedIndex == index) _owner.SelectIndex(index);
                if (_owner.Sorted) _owner.SortDomainItems();
            }
        }

        public override int Add(object? item)
        {
            int ret = base.Add(item);
            if (_owner.Sorted) _owner.SortDomainItems();
            return ret;
        }

        public override void Remove(object? item)
        {
            int index = IndexOf(item);
            if (index == -1)
                throw new ArgumentOutOfRangeException(nameof(item), item, $"Invalid value '{item}' for parameter 'item'.");
            RemoveAt(index);
        }

        public override void RemoveAt(int item)
        {
            base.RemoveAt(item);
            if (item < _owner._domainIndex) _owner.SelectIndex(_owner._domainIndex - 1);
            else if (item == _owner._domainIndex) _owner.SelectIndex(-1);
        }

        public override void Insert(int index, object? item)
        {
            base.Insert(index, item);
            if (_owner.Sorted) _owner.SortDomainItems();
        }
    }

    private sealed class DomainUpDownItemCompare : IComparer
    {
        public int Compare(object? p, object? q)
        {
            if (p == q) return 0;
            if (p is null || q is null) return 0;
            return string.Compare(p.ToString(), q.ToString(), false, CultureInfo.CurrentCulture);
        }
    }
}
