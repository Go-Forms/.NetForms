using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>The border and the state colors of a button whose FlatStyle is Flat.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public class FlatButtonAppearance
{
    private readonly ButtonBase _owner;
    private int _borderSize = 1;
    private Color _borderColor = Color.Empty;
    private Color _checkedBackColor = Color.Empty;
    private Color _mouseDownBackColor = Color.Empty;
    private Color _mouseOverBackColor = Color.Empty;

    internal FlatButtonAppearance(ButtonBase owner) => _owner = owner;

    [Browsable(true)]
    [NotifyParentProperty(true)]
    [Category("Appearance")]
    [Description("For buttons whose FlatStyle is FlatStyle.Flat, this property specifies the size, in pixels, of the border around the button.")]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DefaultValue(1)]
    public int BorderSize
    {
        get => _borderSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_borderSize == value) return;
            _borderSize = value;
            _owner.Invalidate();
        }
    }

    [Browsable(true)]
    [NotifyParentProperty(true)]
    [Category("Appearance")]
    [Description("For buttons whose FlatStyle is FlatStyle.Flat, this property specifies the color of the border around the button.")]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DefaultValue(typeof(Color), "")]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            if (value.Equals(Color.Transparent)) throw new NotSupportedException("The ButtonBase border color cannot be transparent.");
            if (_borderColor == value) return;
            _borderColor = value;
            _owner.Invalidate();
        }
    }

    [Browsable(true)]
    [NotifyParentProperty(true)]
    [Category("Appearance")]
    [Description("For buttons whose FlatStyle is FlatStyle.Flat, this property specifies the color of the client area of the button when the button is checked and the mouse pointer is outside the bounds of the control.")]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DefaultValue(typeof(Color), "")]
    public Color CheckedBackColor
    {
        get => _checkedBackColor;
        set
        {
            if (_checkedBackColor == value) return;
            _checkedBackColor = value;
            _owner.Invalidate();
        }
    }

    [Browsable(true)]
    [NotifyParentProperty(true)]
    [Category("Appearance")]
    [Description("For buttons whose FlatStyle is FlatStyle.Flat, this property specifies the color of the client area of the button when the mouse is pressed within the bounds of the control.")]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DefaultValue(typeof(Color), "")]
    public Color MouseDownBackColor
    {
        get => _mouseDownBackColor;
        set
        {
            if (_mouseDownBackColor == value) return;
            _mouseDownBackColor = value;
            _owner.Invalidate();
        }
    }

    [Browsable(true)]
    [NotifyParentProperty(true)]
    [Category("Appearance")]
    [Description("For buttons whose FlatStyle is FlatStyle.Flat, this property specifies the color of the client area of the button when the mouse pointer is within the bounds of the control.")]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DefaultValue(typeof(Color), "")]
    public Color MouseOverBackColor
    {
        get => _mouseOverBackColor;
        set
        {
            if (_mouseOverBackColor == value) return;
            _mouseOverBackColor = value;
            _owner.Invalidate();
        }
    }
}
