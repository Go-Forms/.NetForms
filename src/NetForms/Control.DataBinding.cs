using System;
using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>
/// Data binding on the control, as in WinForms: <see cref="DataBindings"/> and the <see cref="BindingContext"/>
/// they are bound through. A control without a context of its own uses its parent's; a container
/// (<see cref="ContainerControl"/>) makes one on demand. Bindings bind once the control is created and has a
/// context, and rebind when either changes (the vendored <see cref="Binding"/> and <see cref="Forms.BindingContext"/>
/// of dotnet/winforms do the rest).
/// </summary>
public partial class Control : IBindableComponent
{
    private ControlBindingsCollection? _dataBindings;
    private BindingContext? _bindingContext;
    private bool _hasOwnBindingContext;

    [Category("Data")]
    [Description("The data bindings for the control.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    [RefreshProperties(RefreshProperties.All)]
    [ParenthesizePropertyName(true)]
    public ControlBindingsCollection DataBindings => _dataBindings ??= new ControlBindingsCollection(this);

    /// <summary>What <see cref="BindingContext"/> returns before a container's override makes one (SplitContainer uses it).</summary>
    internal BindingContext? BindingContextInternal
    {
        get => _hasOwnBindingContext ? _bindingContext : _parent?.BindingContext;
        set
        {
            var old = _hasOwnBindingContext ? _bindingContext : null;
            _bindingContext = value;
            _hasOwnBindingContext = value != null;
            if (!ReferenceEquals(old, value)) OnBindingContextChanged(EventArgs.Empty);
        }
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The binding manager for the control.")]
    public virtual BindingContext? BindingContext
    {
        get => BindingContextInternal;
        set => BindingContextInternal = value;
    }

    [Category("Property Changed")]
    [Description("Event raised when the value of the BindingContext property is changed on Control.")]
    public event EventHandler? BindingContextChanged;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnBindingContextChanged(EventArgs e)
    {
        if (_dataBindings != null) UpdateBindings();
        BindingContextChanged?.Invoke(this, e);
        if (_controls != null)
        {
            foreach (var c in _controls.ToArray()) c.OnParentBindingContextChanged(e);
        }
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnParentBindingContextChanged(EventArgs e)
    {
        if (!_hasOwnBindingContext) OnBindingContextChanged(e);
    }

    /// <summary>Removes every binding of the control.</summary>
    public void ResetBindings() => _dataBindings?.Clear();

    private void UpdateBindings()
    {
        for (int i = 0; i < DataBindings.Count; i++)
        {
            BindingContext.UpdateBinding(BindingContext, DataBindings[i]);
        }
    }
}
