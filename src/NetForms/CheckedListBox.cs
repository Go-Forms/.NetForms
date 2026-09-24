using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace System.Windows.Forms;

/// <summary>
/// A ListBox with a check box in front of every item. As in WinForms: the check state belongs to the
/// item (it follows it through inserts, removals and sorting), a click on the box - or anywhere on the
/// item with <see cref="CheckOnClick"/> - and the Space key toggle it, <see cref="ItemCheck"/> comes
/// before the change and may alter it, only SelectionMode None and One are allowed and the items are
/// always drawn by the control (DrawMode.Normal).
/// </summary>
[DefaultEvent("SelectedIndexChanged")]
[DefaultProperty("Items")]
public class CheckedListBox : ListBox
{
    private readonly List<CheckState> _states = new();
    private bool _checkOnClick;
    private bool _threeDCheckBoxes;
    private int _lastSelected = -1;
    private readonly CheckedIndexCollection _checkedIndices;
    private readonly CheckedItemCollection _checkedItems;

    public CheckedListBox()
    {
        _checkedIndices = new CheckedIndexCollection(this);
        _checkedItems = new CheckedItemCollection(this);
    }

    [Category("Behavior")]
    [Description("Indicates if the check box should be toggled with the first click on an item.")]
    [DefaultValue(false)]
    public bool CheckOnClick
    {
        get => _checkOnClick;
        set => _checkOnClick = value;
    }

    [Category("Appearance")]
    [Description("Indicates if the CheckBoxes should show up as flat or 3D in appearance.")]
    [DefaultValue(false)]
    public bool ThreeDCheckBoxes
    {
        get => _threeDCheckBoxes;
        set
        {
            if (_threeDCheckBoxes == value) return;
            _threeDCheckBoxes = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Specifies whether the UseCompatibleTextRendering property is used.")]
    [DefaultValue(false)]
    public bool UseCompatibleTextRendering { get; set; }

    /// <summary>The items; <c>Items.Add(item, true)</c> adds one checked.</summary>
    [Category("Data")]
    [Description("The items in the checked list box.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public new ObjectCollection Items => (ObjectCollection)base.Items;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CheckedIndexCollection CheckedIndices => _checkedIndices;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CheckedItemCollection CheckedItems => _checkedItems;

    /// <summary>Only None and One: a checked list box selects one item at most.</summary>
    [Category("Behavior")]
    [Description("Indicates if the list box is to be single-select, multi-select, or not selectable.")]
    [DefaultValue(SelectionMode.One)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override SelectionMode SelectionMode
    {
        get => base.SelectionMode;
        set
        {
            if (value is SelectionMode.MultiSimple or SelectionMode.MultiExtended)
                throw new ArgumentException("Multi selection is not supported on CheckedListBox.", nameof(value));
            base.SelectionMode = value;
        }
    }

    /// <summary>The font's height plus room for the box's border (WinForms: 18 at Segoe UI 9pt); not settable.</summary>
    [Category("Behavior")]
    [Description("The height, in pixels, of items in a fixed-height owner-draw list box.")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override int ItemHeight
    {
        get => Font.Height + 2;
        set { }
    }

    /// <summary>Always Normal: the control draws its items (and their boxes) itself.</summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override DrawMode DrawMode
    {
        get => DrawMode.Normal;
        set { }
    }

    [Category("Behavior")]
    [Description("Occurs when the checked state of an item changes.")]
    public event ItemCheckEventHandler? ItemCheck;

    protected override ListBox.ObjectCollection CreateItemCollection() => new ObjectCollection(this);

    // --- the state of each item --------------------------------------------------------------

    public bool GetItemChecked(int index) => GetItemCheckState(index) != CheckState.Unchecked;

    public CheckState GetItemCheckState(int index)
    {
        if (index < 0 || index >= Items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return _states[index];
    }

    public void SetItemChecked(int index, bool value) => SetItemCheckState(index, value ? CheckState.Checked : CheckState.Unchecked);

    public void SetItemCheckState(int index, CheckState value)
    {
        if (index < 0 || index >= Items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(CheckState));
        var current = _states[index];
        if (value == current) return;
        var e = new ItemCheckEventArgs(index, value, current);
        OnItemCheck(e);
        if (e.NewValue == current) return;
        _states[index] = e.NewValue;
        Invalidate(GetItemRectangle(index));
    }

    protected virtual void OnItemCheck(ItemCheckEventArgs ice) => ItemCheck?.Invoke(this, ice);

    internal override void ItemInserted(int index) => _states.Insert(index, CheckState.Unchecked);

    internal override void ItemRemoved(int index) => _states.RemoveAt(index);

    internal override void ItemsCleared() => _states.Clear();

    internal override void ItemsReordered(int[] oldIndices)
    {
        var old = _states.ToArray();
        for (int i = 0; i < oldIndices.Length; i++) _states[i] = old[oldIndices[i]];
    }

    /// <summary>Checked becomes Unchecked, anything else Checked - as a click does in WinForms.</summary>
    private void Toggle(int index) =>
        SetItemCheckState(index, _states[index] == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked);

    // --- input ---------------------------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int before = SelectedIndex;
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        int index = IndexFromPoint(e.Location);
        if (index < 0 || index >= Items.Count) return;
        // The box toggles on any click; the text only with CheckOnClick or when the item was
        // already selected (a second click), as in WinForms.
        var box = CheckBoxBounds(GetItemRectangle(index));
        if (box.Contains(e.Location) || _checkOnClick || (index == before && index == _lastSelected)) Toggle(index);
        _lastSelected = index;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (e.KeyChar == ' ' && SelectedIndex >= 0 && SelectionMode != SelectionMode.None)
        {
            Toggle(SelectedIndex);
            e.Handled = true;
        }
        base.OnKeyPress(e);
    }

    // --- painting ------------------------------------------------------------------------------

    private static Rectangle CheckBoxBounds(Rectangle item) =>
        new Rectangle(item.X + 1, item.Y + (item.Height - CheckBox.BoxSize) / 2, CheckBox.BoxSize, CheckBox.BoxSize);

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var g = e.Graphics;
        var box = CheckBoxBounds(e.Bounds);
        using (var back = new SolidBrush(BackColor)) g.FillRectangle(back, new Rectangle(e.Bounds.X, e.Bounds.Y, box.Right + 2 - e.Bounds.X, e.Bounds.Height));
        CheckBox.PaintBox(g, box, _states[e.Index], Enabled, hot: false, pressed: false, flat: !_threeDCheckBoxes, ForeColor);

        // The text part is an ordinary list item: highlighted when selected, with the focus rectangle.
        var text = new Rectangle(box.Right + 3, e.Bounds.Y, Math.Max(0, e.Bounds.Right - box.Right - 3), e.Bounds.Height);
        using (var back = new SolidBrush(e.BackColor)) g.FillRectangle(back, text);
        var color = Enabled ? e.ForeColor : Theme.DisabledText;
        TextRenderer.DrawText(g, GetItemText(Items[e.Index]), e.Font, text, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0) ControlPaint.DrawFocusRectangle(g, text, e.ForeColor, e.BackColor);
    }

    // --- collections ---------------------------------------------------------------------------

    /// <summary>The items of a CheckedListBox: the ListBox collection plus adding with a check state.</summary>
    public new class ObjectCollection : ListBox.ObjectCollection
    {
        private readonly CheckedListBox _owner;

        public ObjectCollection(CheckedListBox owner) : base(owner) => _owner = owner;

        public int Add(object item, bool isChecked) => Add(item, isChecked ? CheckState.Checked : CheckState.Unchecked);

        public int Add(object item, CheckState check)
        {
            if (!Enum.IsDefined(check)) throw new InvalidEnumArgumentException(nameof(check), (int)check, typeof(CheckState));
            int index = Add(item);
            // A new item's state is set without ItemCheck, as WinForms does.
            _owner._states[index] = check;
            _owner.Invalidate();
            return index;
        }
    }

    /// <summary>The indices of the checked (and indeterminate) items, in item order.</summary>
    public class CheckedIndexCollection : IList
    {
        private readonly CheckedListBox _owner;

        internal CheckedIndexCollection(CheckedListBox owner) => _owner = owner;

        private List<int> Indices() => Enumerable.Range(0, _owner._states.Count).Where(i => _owner._states[i] != CheckState.Unchecked).ToList();

        public int Count => Indices().Count;
        public int this[int index] => Indices()[index];
        object? IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
        public bool IsReadOnly => true;
        bool IList.IsFixedSize => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        public bool Contains(int index) => index >= 0 && index < _owner._states.Count && _owner._states[index] != CheckState.Unchecked;
        bool IList.Contains(object? index) => index is int i && Contains(i);
        public int IndexOf(int index) => Indices().IndexOf(index);
        int IList.IndexOf(object? index) => index is int i ? IndexOf(i) : -1;
        public void CopyTo(Array dest, int index) => ((ICollection)Indices().ToArray()).CopyTo(dest, index);
        public IEnumerator GetEnumerator() => Indices().GetEnumerator();
        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

    /// <summary>The checked (and indeterminate) items themselves, in item order.</summary>
    public class CheckedItemCollection : IList
    {
        private readonly CheckedListBox _owner;

        internal CheckedItemCollection(CheckedListBox owner) => _owner = owner;

        private List<object> Items() => _owner.CheckedIndices.Cast<int>().Select(i => _owner.Items[i]).ToList();

        public int Count => Items().Count;
        public object this[int index] { get => Items()[index]; set => throw new NotSupportedException(); }
        object? IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
        public bool IsReadOnly => true;
        bool IList.IsFixedSize => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        public bool Contains(object? item) => item != null && Items().Contains(item);
        public int IndexOf(object? item) => item == null ? -1 : Items().IndexOf(item);
        public void CopyTo(Array dest, int index) => ((ICollection)Items().ToArray()).CopyTo(dest, index);
        public IEnumerator GetEnumerator() => Items().GetEnumerator();
        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Data")]
    [DefaultValue(null)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new object? DataSource { get => base.DataSource; set => base.DataSource = value; }

    [Category("Data")]
    [DefaultValue("")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new string DisplayMember { get => base.DisplayMember; set => base.DisplayMember = value; }

    [Category("Data")]
    [DefaultValue("")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new string ValueMember { get => base.ValueMember; set => base.ValueMember = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DataSourceChanged
    {
        add => base.DataSourceChanged += value;
        remove => base.DataSourceChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DisplayMemberChanged
    {
        add => base.DisplayMemberChanged += value;
        remove => base.DisplayMemberChanged -= value;
    }

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event DrawItemEventHandler? DrawItem
    {
        add => base.DrawItem += value;
        remove => base.DrawItem -= value;
    }

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MeasureItemEventHandler? MeasureItem
    {
        add => base.MeasureItem += value;
        remove => base.MeasureItem -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? ValueMemberChanged
    {
        add => base.ValueMemberChanged += value;
        remove => base.ValueMemberChanged -= value;
    }
}
