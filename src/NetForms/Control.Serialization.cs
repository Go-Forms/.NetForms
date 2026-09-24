using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// Design-time serialization: the <c>ShouldSerializeXxx</c>/<c>ResetXxx</c> pairs that
/// <see cref="System.ComponentModel.PropertyDescriptor.ShouldSerializeValue"/> and
/// <see cref="System.ComponentModel.PropertyDescriptor.ResetValue"/> find by name, exactly as in
/// WinForms. They decide which lines the designer code writer emits, so they follow the reference
/// implementation, checked by <c>SerializationDiffTests</c> and the fresh-instance column of
/// <c>AttributeDiffTests</c> (Ф5.2, docs/PLAN.md).
/// </summary>
/// <remarks>
/// The <c>ShouldSerialize</c> methods are <c>internal</c>, not private: TypeDescriptor looks them up
/// on the type that declares the property, and many controls re-declare an inherited property with
/// <c>new</c> to change its attributes (decision 53). An internal method is found from there, a
/// private one on the base class would not be.
/// </remarks>
public partial class Control
{
    /// <summary>Ambient: written only when set on this control rather than inherited from the parent.</summary>
    internal virtual bool ShouldSerializeBackColor() => !_backColor.IsEmpty;

    internal virtual bool ShouldSerializeForeColor() => !_foreColor.IsEmpty;

    internal virtual bool ShouldSerializeFont() => _font != null;

    internal virtual bool ShouldSerializeCursor() => _cursor != null;

    public virtual void ResetCursor() => Cursor = null!;

    internal virtual bool ShouldSerializeRightToLeft() => RightToLeft != RightToLeft.No;

    public virtual void ResetRightToLeft() => RightToLeft = RightToLeft.No;

    internal virtual bool ShouldSerializeImeMode() => ImeMode != ImeMode.Inherit;

    public void ResetImeMode() => ImeMode = ImeMode.Inherit;

    /// <summary>The control's own flag: a control disabled only through its parent is not written as disabled.</summary>
    internal bool ShouldSerializeEnabled() => !_enabled;

    internal bool ShouldSerializeVisible() => !_visible;

    internal virtual bool ShouldSerializeText() => Text.Length != 0;

    public virtual void ResetText() => Text = string.Empty;

    internal virtual bool ShouldSerializeSize() => Size != DefaultSize;

    /// <summary>
    /// Control.Location has neither a default value nor a ShouldSerialize method in WinForms, so it is
    /// always written; the hook exists for the derived controls that decide otherwise (Form, TabPage).
    /// It is virtual because TypeDescriptor finds the method on the type that declares the property.
    /// </summary>
    internal virtual bool ShouldSerializeLocation() => true;

    internal virtual bool ShouldSerializeMargin() => Margin != DefaultMargin;

    internal void ResetMargin() => Margin = DefaultMargin;

    internal virtual bool ShouldSerializePadding() => Padding != DefaultPadding;

    internal void ResetPadding() => Padding = DefaultPadding;

    internal virtual bool ShouldSerializeMinimumSize() => MinimumSize != DefaultMinimumSize;

    internal virtual bool ShouldSerializeMaximumSize() => MaximumSize != DefaultMaximumSize;
}
