using System;
using System.Collections;
using System.Collections.Generic;

namespace System.Windows.Forms;

public partial class Control
{
    /// <summary>
    /// The children of a control, in z-order: index 0 is the front-most control, the last
    /// one is at the back. <see cref="Add"/> appends, i.e. puts the new control at the back
    /// - exactly as WinForms, which is why designer code adds the top-most control first.
    /// </summary>
    public class ControlCollection : IList, IList<Control>, IReadOnlyList<Control>
    {
        private readonly List<Control> _items = new();

        public ControlCollection(Control owner)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public Control Owner { get; }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public virtual Control this[int index]
        {
            get => _items[index];
        }

        Control IList<Control>.this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException();
        }

        public virtual Control? this[string? key]
        {
            get
            {
                if (string.IsNullOrEmpty(key)) return null;
                int i = IndexOfKey(key);
                return i >= 0 ? _items[i] : null;
            }
        }

        public virtual void Add(Control? value)
        {
            if (value == null) return;
            if (value.GetTopLevel()) throw new ArgumentException("Top-level controls cannot be added to a control.");
            if (value == Owner || value.Contains(Owner)) throw new ArgumentException("A control cannot be added to itself or its own children.");

            if (value._parent == Owner)
            {
                // Already ours: bring it to the back, as WinForms does on re-Add.
                _items.Remove(value);
                _items.Add(value);
                return;
            }

            value._parent?.Controls.Remove(value);

            _items.Add(value);
            if (value._tabIndex == -1)
            {
                // WinForms hands out the next free tab index.
                int next = 0;
                foreach (var c in _items)
                {
                    if (c != value && c._tabIndex >= next) next = c._tabIndex + 1;
                }
                value._tabIndex = next;
            }

            value.AssignParent(Owner);
            value.RaiseInitLayout();
            Owner.LayoutEngine.InitLayout(value, BoundsSpecified.All);
            Owner.OnControlAdded(new ControlEventArgs(value));
            Owner.PerformLayout(value, "Parent");
            Owner.Invalidate();
        }

        public virtual void AddRange(Control[] controls)
        {
            ArgumentNullException.ThrowIfNull(controls);
            Owner.SuspendLayout();
            try
            {
                foreach (var c in controls) Add(c);
            }
            finally
            {
                Owner.ResumeLayout(true);
            }
        }

        public virtual void Remove(Control? value)
        {
            if (value == null || value._parent != Owner) return;
            var form = Owner.FindForm();
            form?.ChildRemoved(value);
            _items.Remove(value);
            value.AssignParent(null);
            Owner.OnControlRemoved(new ControlEventArgs(value));
            Owner.PerformLayout(value, "Parent");
            Owner.Invalidate();
        }

        public void RemoveAt(int index) => Remove(_items[index]);

        public virtual void RemoveByKey(string? key)
        {
            int i = IndexOfKey(key);
            if (i >= 0) RemoveAt(i);
        }

        public virtual void Clear()
        {
            Owner.SuspendLayout();
            try
            {
                for (int i = _items.Count - 1; i >= 0; i--) Remove(_items[i]);
            }
            finally
            {
                Owner.ResumeLayout(true);
            }
        }

        public bool Contains(Control? control) => control != null && _items.Contains(control);

        public virtual bool ContainsKey(string? key) => IndexOfKey(key) >= 0;

        public int IndexOf(Control? control) => control == null ? -1 : _items.IndexOf(control);

        public virtual int IndexOfKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Name, key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        public Control[] Find(string key, bool searchAllChildren)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            var result = new List<Control>();
            FindInto(this, key, searchAllChildren, result);
            return result.ToArray();
        }

        private static void FindInto(ControlCollection collection, string key, bool deep, List<Control> result)
        {
            foreach (var c in collection._items)
            {
                if (string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase)) result.Add(c);
                if (deep && c._controls != null) FindInto(c._controls, key, true, result);
            }
        }

        public int GetChildIndex(Control child) => GetChildIndex(child, true);

        public virtual int GetChildIndex(Control child, bool throwException)
        {
            int i = IndexOf(child);
            if (i < 0 && throwException) throw new ArgumentException("The control is not a child of this control.");
            return i;
        }

        public virtual void SetChildIndex(Control child, int newIndex)
        {
            int old = GetChildIndex(child, true);
            if (newIndex < 0 || newIndex >= _items.Count) newIndex = _items.Count - 1;
            if (old == newIndex) return;
            _items.RemoveAt(old);
            _items.Insert(newIndex, child);
            Owner.PerformLayout(child, "ChildIndex");
            Owner.Invalidate();
        }

        public void CopyTo(Array dest, int index) => ((ICollection)_items).CopyTo(dest, index);

        public void CopyTo(Control[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public Control[] ToArray() => _items.ToArray();

        public IEnumerator<Control> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        // --- IList plumbing ---------------------------------------------------------

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;

        object? IList.this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException();
        }

        int IList.Add(object? value)
        {
            Add(value as Control);
            return _items.Count - 1;
        }

        bool IList.Contains(object? value) => Contains(value as Control);
        int IList.IndexOf(object? value) => IndexOf(value as Control);
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => Remove(value as Control);

        void IList<Control>.Insert(int index, Control item) => throw new NotSupportedException();
        bool ICollection<Control>.Remove(Control item)
        {
            bool had = Contains(item);
            Remove(item);
            return had;
        }
    }
}
