using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

// The rest of WinForms' ScrollableControl surface: the scroll-state bits, HScroll/VScroll, DockPadding.
public partial class ScrollableControl
{
    protected const int ScrollStateAutoScrolling = 0x0001;
    protected const int ScrollStateHScrollVisible = 0x0002;
    protected const int ScrollStateVScrollVisible = 0x0004;
    protected const int ScrollStateUserHasScrolled = 0x0008;
    protected const int ScrollStateFullDrag = 0x0010;

    private int _scrollStateBits;
    private DockPaddingEdges? _dockPadding;

    /// <summary>The bits of the scroll state; the auto-scroll and bar bits are the control's live state.</summary>
    protected bool GetScrollState(int bit)
    {
        int state = _scrollStateBits;
        if (_autoScroll) state |= ScrollStateAutoScrolling;
        if (_hVisible) state |= ScrollStateHScrollVisible;
        if (_vVisible) state |= ScrollStateVScrollVisible;
        return (bit & state) == bit;
    }

    protected void SetScrollState(int bit, bool value)
    {
        if ((bit & ScrollStateAutoScrolling) != 0) _autoScroll = value;
        if ((bit & ScrollStateHScrollVisible) != 0) _hVisible = value;
        if ((bit & ScrollStateVScrollVisible) != 0) _vVisible = value;
        int stored = bit & ~(ScrollStateAutoScrolling | ScrollStateHScrollVisible | ScrollStateVScrollVisible);
        _scrollStateBits = value ? _scrollStateBits | stored : _scrollStateBits & ~stored;
    }

    /// <summary>Whether the horizontal scroll bar is shown (WS_HSCROLL in WinForms).</summary>
    protected bool HScroll
    {
        get => GetScrollState(ScrollStateHScrollVisible);
        set
        {
            SetScrollState(ScrollStateHScrollVisible, value);
            Invalidate();
        }
    }

    /// <summary>Whether the vertical scroll bar is shown (WS_VSCROLL in WinForms).</summary>
    protected bool VScroll
    {
        get => GetScrollState(ScrollStateVScrollVisible);
        set
        {
            SetScrollState(ScrollStateVScrollVisible, value);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public DockPaddingEdges DockPadding => _dockPadding ??= new DockPaddingEdges(this);

    /// <summary>Show or hide the scroll bars as AutoScroll decides: hiding them scrolls back to the origin.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void AdjustFormScrollbars(bool displayScrollbars)
    {
        if (!displayScrollbars)
        {
            SetDisplayRectLocation(0, 0);
            if (_hVisible || _vVisible)
            {
                _hVisible = _vVisible = false;
                PerformLayout(this, "Scrollbars");
                Invalidate();
            }
            return;
        }
        PerformLayout(this, "Scrollbars");
    }

    public void SetAutoScrollMargin(int x, int y) => AutoScrollMargin = new Size(Math.Max(0, x), Math.Max(0, y));

    /// <summary>The padding of a container, edge by edge - the pre-2.0 name of <see cref="Control.Padding"/>, which it reads and writes.</summary>
    [TypeConverter(typeof(DockPaddingEdgesConverter))]
    public class DockPaddingEdges : ICloneable
    {
        private readonly ScrollableControl? _owner;
        private Padding _padding;

        internal DockPaddingEdges(ScrollableControl owner) => _owner = owner;

        internal DockPaddingEdges(int left, int right, int top, int bottom) => _padding = new Padding(left, top, right, bottom);

        private Padding Value
        {
            get => _owner?.Padding ?? _padding;
            set
            {
                if (_owner != null) _owner.Padding = value;
                else _padding = value;
            }
        }

        [RefreshProperties(RefreshProperties.All)]
        public int All
        {
            get => Value.All == -1 ? 0 : Value.All;
            set => Value = new Padding(value);
        }

        [RefreshProperties(RefreshProperties.All)]
        public int Bottom
        {
            get => Value.Bottom;
            set { var p = Value; p.Bottom = value; Value = p; }
        }

        [RefreshProperties(RefreshProperties.All)]
        public int Left
        {
            get => Value.Left;
            set { var p = Value; p.Left = value; Value = p; }
        }

        [RefreshProperties(RefreshProperties.All)]
        public int Right
        {
            get => Value.Right;
            set { var p = Value; p.Right = value; Value = p; }
        }

        [RefreshProperties(RefreshProperties.All)]
        public int Top
        {
            get => Value.Top;
            set { var p = Value; p.Top = value; Value = p; }
        }

        public override bool Equals(object? other) => other is DockPaddingEdges dpe && Value == dpe.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"[DockPaddingEdges: All={All}, Top={Top}, Left={Left}, Bottom={Bottom}, Right={Right}]";

        object ICloneable.Clone() => new DockPaddingEdges(Left, Right, Top, Bottom);
    }

    public class DockPaddingEdgesConverter : TypeConverter
    {
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value, Attribute[]? attributes)
        {
            var props = TypeDescriptor.GetProperties(typeof(DockPaddingEdges), attributes);
            return props.Sort(["All", "Left", "Top", "Right", "Bottom"]);
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;
    }
}
