using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>A control that manages focus among its children (Form, UserControl, ...).</summary>
[Flags]
public enum ValidationConstraints
{
    None = 0,
    Selectable = 1,
    Enabled = 2,
    Visible = 4,
    TabStop = 8,
    ImmediateChildren = 16,
}

public class ContainerControl : ScrollableControl, IContainerControl
{
    private Control? _activeControl;

    public ContainerControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint, false);
    }

    /// <summary>A container has a binding context of its own, made on first use (WinForms).</summary>
    [Browsable(false)]
    [Description("The binding manager for the container control.  This manages all bindings of child controls to lists.")]
    public override BindingContext? BindingContext
    {
        get
        {
            var context = base.BindingContext;
            if (context == null)
            {
                context = new BindingContext();
                BindingContext = context;
            }
            return context;
        }
        set => base.BindingContext = value;
    }

    /// <summary>Once created, the container binds the data bindings of everything in it (WinForms).</summary>
    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        OnBindingContextChanged(EventArgs.Empty);
    }

    /// <summary>The child that has (or last had) the focus inside this container.</summary>
    [Category("Behavior")]
    [Description("The currently active control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? ActiveControl
    {
        get => _activeControl;
        set
        {
            if (value != null && !Contains(value)) throw new ArgumentException("The control is not a child of this container.");
            var form = FindForm();
            if (form != null && value != null)
            {
                form.SetFocusedControl(value);
            }
            else
            {
                _activeControl = value;
            }
        }
    }

    internal void SetActiveControlInternal(Control? value) => _activeControl = value;

    /// <summary>The form this container is on (not itself: a form's ParentForm is its MDI parent or null).</summary>
    [Browsable(false)]
    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The form that the container control is assigned to.")]
    public Form? ParentForm
    {
        get
        {
            if (this is Form { IsMdiChild: true } child) return child.MdiParent;
            for (var c = Parent; c != null; c = c.Parent)
                if (c is Form form) return form;
            return null;
        }
    }

    bool IContainerControl.ActivateControl(Control active)
    {
        ActiveControl = active;
        return ActiveControl == active;
    }

    private AutoValidate _autoValidate = AutoValidate.Inherit;

    /// <summary>
    /// Whether the control losing the focus is validated, and whether a failed validation keeps the
    /// focus. <see cref="AutoValidate.Inherit"/> reads the nearest container above; a top-level
    /// container resolves it to <see cref="AutoValidate.EnablePreventFocusChange"/>, as in WinForms.
    /// </summary>
    [Category("Behavior")]
    [AmbientValue(AutoValidate.Inherit)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual AutoValidate AutoValidate
    {
        get => _autoValidate == AutoValidate.Inherit ? GetAutoValidateForControl(this) : _autoValidate;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(AutoValidate));
            if (_autoValidate == value) return;
            _autoValidate = value;
            OnAutoValidateChanged(EventArgs.Empty);
        }
    }

    internal virtual bool ShouldSerializeAutoValidate() => _autoValidate != AutoValidate.Inherit;

    [Category("Property Changed")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public event EventHandler? AutoValidateChanged;

    protected virtual void OnAutoValidateChanged(EventArgs e) => AutoValidateChanged?.Invoke(this, e);

    /// <summary>The AutoValidate that applies to <paramref name="control"/>: its nearest container's.</summary>
    internal static AutoValidate GetAutoValidateForControl(Control control)
    {
        for (var p = control.Parent; p != null; p = p.Parent)
            if (p is ContainerControl container) return container.AutoValidate;
        return AutoValidate.EnablePreventFocusChange;
    }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SizeF AutoScaleDimensions { get; set; }

    [Category("Layout")]
    [Description("Determines how the form or control will scale when screen resolution or fonts change.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Inherit;

    [Category("Layout")]
    [Browsable(false)]
    public SizeF CurrentAutoScaleDimensions => new SizeF(7f, 15f);

    public SizeF AutoScaleFactor => new SizeF(1f, 1f);

    /// <summary>Validate the active control (Validating/Validated) without moving the focus.</summary>
    public bool Validate() => Validate(false);

    public bool Validate(bool checkAutoValidate)
    {
        var active = _activeControl;
        if (active == null || !active.CausesValidation) return true;
        var e = new CancelEventArgs();
        active.RaiseValidating(e);
        if (e.Cancel) return false;
        active.RaiseValidated();
        return true;
    }

    public virtual bool ValidateChildren() => ValidateChildren(ValidationConstraints.Selectable);

    public virtual bool ValidateChildren(ValidationConstraints validationConstraints)
    {
        bool ok = true;
        foreach (var child in Controls)
        {
            if (!child.CausesValidation) continue;
            if ((validationConstraints & ValidationConstraints.Enabled) != 0 && !child.Enabled) continue;
            if ((validationConstraints & ValidationConstraints.Visible) != 0 && !child.Visible) continue;
            if ((validationConstraints & ValidationConstraints.TabStop) != 0 && !child.TabStop) continue;
            if ((validationConstraints & ValidationConstraints.Selectable) != 0 && !child.CanSelect) continue;
            var e = new CancelEventArgs();
            child.RaiseValidating(e);
            if (e.Cancel)
            {
                ok = false;
                break;
            }
            child.RaiseValidated();
            if (child is ContainerControl cc && !cc.ValidateChildren(validationConstraints))
            {
                ok = false;
                break;
            }
        }
        return ok;
    }

    protected virtual void UpdateDefaultButton() { }

    protected virtual bool ProcessTabKey(bool forward)
    {
        // Focus cues appear once the keyboard is used for navigation, as with Win32's UISF_HIDEFOCUS.
        FindForm()?.ShowFocusCuesFromKeyboard();
        return SelectNextControl(ActiveControl, forward, true, true, true);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & (Keys.Alt | Keys.Control)) == Keys.None)
        {
            var keyCode = keyData & Keys.KeyCode;
            switch (keyCode)
            {
                case Keys.Tab:
                    if (ProcessTabKey((keyData & Keys.Shift) == Keys.None)) return true;
                    break;
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                    FindForm()?.ShowFocusCuesFromKeyboard();
                    if (SelectNextControl(ActiveControl, keyCode is Keys.Right or Keys.Down, false, false, true)) return true;
                    break;
            }
        }
        return base.ProcessDialogKey(keyData);
    }
}
