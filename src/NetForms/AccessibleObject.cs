using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms.Automation;

namespace System.Windows.Forms;

/// <summary>
/// What an accessibility client (a screen reader, UI automation, a test driver) sees of a user interface element.
/// WinForms serves it to MSAA/UI Automation through COM; here it is the managed model - the same members and the
/// same answers - that a platform bridge (Avalonia's automation peers, decision 154) reads.
/// The base object describes nothing; <see cref="Control.ControlAccessibleObject"/> describes a control.
/// </summary>
public class AccessibleObject : MarshalByRefObject
{
    public AccessibleObject()
    {
    }

    /// <summary>The element's rectangle in screen coordinates.</summary>
    public virtual Rectangle Bounds => Rectangle.Empty;

    public virtual string? DefaultAction => null;

    public virtual string? Description => null;

    public virtual string? Help => null;

    public virtual string? KeyboardShortcut => null;

    public virtual string? Name
    {
        get => null;
        set { }
    }

    public virtual AccessibleObject? Parent => null;

    public virtual AccessibleRole Role => AccessibleRole.None;

    public virtual AccessibleStates State => AccessibleStates.None;

    public virtual string? Value
    {
        get => string.Empty;
        set { }
    }

    public virtual void DoDefaultAction()
    {
    }

    /// <summary>The child at <paramref name="index"/>, or null. -1 from <see cref="GetChildCount"/> means "no children of this object's own".</summary>
    public virtual AccessibleObject? GetChild(int index) => null;

    public virtual int GetChildCount() => -1;

    public virtual AccessibleObject? GetFocused()
    {
        int count = GetChildCount();
        if (count < 0) return (State & AccessibleStates.Focused) != 0 ? this : null;
        for (int i = 0; i < count; i++)
        {
            var child = GetChild(i);
            if (child != null && (child.State & AccessibleStates.Focused) != 0) return child;
        }
        return (State & AccessibleStates.Focused) != 0 ? this : null;
    }

    public virtual int GetHelpTopic(out string? fileName)
    {
        fileName = null;
        return -1;
    }

    public virtual AccessibleObject? GetSelected()
    {
        int count = GetChildCount();
        if (count < 0) return (State & AccessibleStates.Selected) != 0 ? this : null;
        for (int i = 0; i < count; i++)
        {
            var child = GetChild(i);
            if (child != null && (child.State & AccessibleStates.Selected) != 0) return child;
        }
        return (State & AccessibleStates.Selected) != 0 ? this : null;
    }

    /// <summary>The child under the screen point, this object when the point is on it but on no child, else null.</summary>
    public virtual AccessibleObject? HitTest(int x, int y)
    {
        int count = GetChildCount();
        for (int i = 0; i < count; i++)
        {
            var child = GetChild(i);
            if (child != null && child.Bounds.Contains(x, y)) return child;
        }
        return Bounds.Contains(x, y) ? this : null;
    }

    public virtual AccessibleObject? Navigate(AccessibleNavigation navdir)
    {
        int count = GetChildCount();
        switch (navdir)
        {
            case AccessibleNavigation.FirstChild:
                return count > 0 ? GetChild(0) : null;
            case AccessibleNavigation.LastChild:
                return count > 0 ? GetChild(count - 1) : null;
        }
        var parent = Parent;
        if (parent == null) return null;
        int siblings = parent.GetChildCount();
        int index = -1;
        for (int i = 0; i < siblings; i++)
        {
            if (Equals(parent.GetChild(i), this)) { index = i; break; }
        }
        if (index < 0) return null;
        return navdir switch
        {
            AccessibleNavigation.Next or AccessibleNavigation.Down or AccessibleNavigation.Right => index + 1 < siblings ? parent.GetChild(index + 1) : null,
            AccessibleNavigation.Previous or AccessibleNavigation.Up or AccessibleNavigation.Left => index > 0 ? parent.GetChild(index - 1) : null,
            _ => null,
        };
    }

    public virtual void Select(AccessibleSelection flags)
    {
    }

    /// <summary>A UI Automation notification (a screen reader announces <paramref name="notificationText"/>).</summary>
    public bool RaiseAutomationNotification(AutomationNotificationKind notificationKind, AutomationNotificationProcessing notificationProcessing, string notificationText)
    {
        AutomationNotificationRaised?.Invoke(this, notificationText);
        return AutomationNotificationRaised != null;
    }

    public virtual bool RaiseLiveRegionChanged() => throw new NotSupportedException("The object does not implement IAutomationLiveRegion.");

    /// <summary>MSAA's standard proxies of a window; NetForms' objects answer for themselves.</summary>
    protected void UseStdAccessibleObjects(IntPtr handle)
    {
    }

    protected void UseStdAccessibleObjects(IntPtr handle, int objid)
    {
    }

    /// <summary>What the platform bridge listens to: notifications and events raised through this object.</summary>
    internal static event Action<AccessibleObject, string>? AutomationNotificationRaised;

    internal static event Action<AccessibleObject, AccessibleEvents, int>? ClientsNotified;

    internal static void RaiseClientsNotified(AccessibleObject source, AccessibleEvents accEvent, int childID) =>
        ClientsNotified?.Invoke(source, accEvent, childID);
}

public class QueryAccessibilityHelpEventArgs : EventArgs
{
    public QueryAccessibilityHelpEventArgs()
    {
    }

    public QueryAccessibilityHelpEventArgs(string? helpNamespace, string? helpString, string? helpKeyword)
    {
        HelpNamespace = helpNamespace;
        HelpString = helpString;
        HelpKeyword = helpKeyword;
    }

    public string? HelpNamespace { get; set; }

    public string? HelpString { get; set; }

    public string? HelpKeyword { get; set; }
}

public delegate void QueryAccessibilityHelpEventHandler(object? sender, QueryAccessibilityHelpEventArgs e);

public partial class Control
{
    private AccessibleObject? _accessibilityObject;
    private AccessibleRole _accessibleRole = AccessibleRole.Default;

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The accessible object that is used to describe this control to accessibility clients.")]
    public AccessibleObject AccessibilityObject => _accessibilityObject ??= CreateAccessibilityInstance();

    internal bool IsAccessibilityObjectCreated => _accessibilityObject != null;

    [Category("Accessibility")]
    [DefaultValue(AccessibleRole.Default)]
    [Description("The role that will be reported to accessibility clients.")]
    public AccessibleRole AccessibleRole
    {
        get => _accessibleRole;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(AccessibleRole));
            _accessibleRole = value;
        }
    }

    [Category("Accessibility")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The default action description that will be reported to accessibility clients.")]
    public string? AccessibleDefaultActionDescription { get; set; }

    [Category("Behavior")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("Indicates if the control is visible to accessibility applications.")]
    public bool IsAccessible { get; set; }

    [Category("Behavior")]
    [Description("Occurs when AccessibleObject is providing help to accessibility applications.")]
    public event QueryAccessibilityHelpEventHandler? QueryAccessibilityHelp;

    internal void RaiseQueryAccessibilityHelp(QueryAccessibilityHelpEventArgs e) => QueryAccessibilityHelp?.Invoke(this, e);

    /// <summary>The accessible object of this control; override to describe a custom control to accessibility clients.</summary>
    protected virtual AccessibleObject CreateAccessibilityInstance() => new ControlAccessibleObject(this);

    protected virtual AccessibleObject? GetAccessibilityObjectById(int objectId) =>
        this is IAutomationLiveRegion ? AccessibilityObject : null;

    protected internal void AccessibilityNotifyClients(AccessibleEvents accEvent, int childID) =>
        AccessibilityNotifyClients(accEvent, -4 /* OBJID_CLIENT */, childID);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected void AccessibilityNotifyClients(AccessibleEvents accEvent, int objectID, int childID)
    {
        if (_accessibilityObject != null) AccessibleObject.RaiseClientsNotified(_accessibilityObject, accEvent, childID);
    }

    /// <summary>The role a control of this kind reports when <see cref="AccessibleRole"/> is Default (the system proxy's answer).</summary>
    internal virtual AccessibleRole DefaultAccessibleRole => this switch
    {
        CheckBox => AccessibleRole.CheckButton,
        RadioButton => AccessibleRole.RadioButton,
        ButtonBase => AccessibleRole.PushButton,
        LinkLabel => AccessibleRole.Link,
        Label => AccessibleRole.StaticText,
        TextBoxBase => AccessibleRole.Text,
        ComboBox => AccessibleRole.ComboBox,
        ListBox or ListView => AccessibleRole.List,
        TreeView => AccessibleRole.Outline,
        GroupBox => AccessibleRole.Grouping,
        ProgressBar => AccessibleRole.ProgressBar,
        TrackBar => AccessibleRole.Slider,
        ScrollBar => AccessibleRole.ScrollBar,
        TabControl => AccessibleRole.PageTabList,
        TabPage => AccessibleRole.Client,
        DataGridView => AccessibleRole.Table,
        MenuStrip => AccessibleRole.MenuBar,
        StatusStrip => AccessibleRole.StatusBar,
        ToolStripDropDown => AccessibleRole.MenuPopup,
        ToolStrip => AccessibleRole.ToolBar,
        PictureBox => AccessibleRole.Graphic,
        UpDownBase => AccessibleRole.SpinButton,
        DateTimePicker => AccessibleRole.DropList,
        MonthCalendar => AccessibleRole.Table,
        Form => AccessibleRole.Client,
        _ => AccessibleRole.Client,
    };

    /// <summary>Describes a control to accessibility clients: name, role, state, bounds, children, default action.</summary>
    public class ControlAccessibleObject : AccessibleObject
    {
        public ControlAccessibleObject(Control ownerControl)
        {
            Owner = ownerControl ?? throw new ArgumentNullException(nameof(ownerControl));
        }

        public Control Owner { get; }

        public IntPtr Handle
        {
            get => Owner.Handle;
            set { }
        }

        public override Rectangle Bounds
        {
            get
            {
                if (!Owner.Visible) return Rectangle.Empty;
                return Owner.Parent != null ? Owner.Parent.RectangleToScreen(Owner.Bounds) : Owner.RectangleToScreen(new Rectangle(Point.Empty, Owner.ClientSize));
            }
        }

        public override string? DefaultAction
        {
            get
            {
                if (Owner.AccessibleDefaultActionDescription != null) return Owner.AccessibleDefaultActionDescription;
                return Role switch
                {
                    AccessibleRole.PushButton => "Press",
                    AccessibleRole.CheckButton => (State & AccessibleStates.Checked) != 0 ? "Uncheck" : "Check",
                    AccessibleRole.RadioButton => "Check",
                    AccessibleRole.Link => "Jump",
                    _ => null,
                };
            }
        }

        public override string? Description => Owner.AccessibleDescription;

        public override string? Help
        {
            get
            {
                var e = new QueryAccessibilityHelpEventArgs();
                Owner.RaiseQueryAccessibilityHelp(e);
                return e.HelpString;
            }
        }

        public override int GetHelpTopic(out string? fileName)
        {
            var e = new QueryAccessibilityHelpEventArgs();
            Owner.RaiseQueryAccessibilityHelp(e);
            fileName = e.HelpNamespace;
            return int.TryParse(e.HelpKeyword, out int topic) ? topic : -1;
        }

        public override string? KeyboardShortcut
        {
            get
            {
                // The mnemonic of the control's text, or of the label before it (a text box after "&Name:").
                char mnemonic = Mnemonic(Owner.Text);
                if (mnemonic == '\0' && PreviousLabel() is { } label) mnemonic = Mnemonic(label.Text);
                return mnemonic == '\0' ? null : "Alt+" + char.ToUpperInvariant(mnemonic);
            }
        }

        public override string? Name
        {
            get
            {
                if (Owner.AccessibleName != null) return Owner.AccessibleName;
                // What the system proxy reads: the window text, or for a control that shows none (a text box,
                // a list) the text of the label just before it in tab order.
                if (UsesOwnText && Owner.Text.Length > 0) return StripMnemonic(Owner.Text);
                return PreviousLabel() is { } label ? StripMnemonic(label.Text) : (Owner.Text.Length > 0 ? StripMnemonic(Owner.Text) : null);
            }
            set => Owner.AccessibleName = value;
        }

        public override AccessibleObject? Parent => Owner.Parent?.AccessibilityObject;

        public override AccessibleRole Role => Owner.AccessibleRole != AccessibleRole.Default ? Owner.AccessibleRole : Owner.DefaultAccessibleRole;

        public override AccessibleStates State
        {
            get
            {
                var state = AccessibleStates.None;
                if (!Owner.Enabled) state |= AccessibleStates.Unavailable;
                if (!Owner.Visible) state |= AccessibleStates.Invisible;
                if (Owner.CanFocus) state |= AccessibleStates.Focusable;
                if (Owner.Focused) state |= AccessibleStates.Focused;
                if (Owner is CheckBox { CheckState: CheckState.Checked } or RadioButton { Checked: true }) state |= AccessibleStates.Checked;
                if (Owner is CheckBox { CheckState: CheckState.Indeterminate }) state |= AccessibleStates.Mixed;
                if (Owner is TextBoxBase { ReadOnly: true }) state |= AccessibleStates.ReadOnly;
                return state;
            }
        }

        public override string? Value
        {
            get => Owner is TextBoxBase or ComboBox or UpDownBase ? Owner.Text : base.Value;
            set
            {
                if (Owner is TextBoxBase or ComboBox or UpDownBase) Owner.Text = value ?? string.Empty;
            }
        }

        public override int GetChildCount()
        {
            int count = 0;
            foreach (Control child in Owner.Controls) if (child.Visible) count++;
            return count;
        }

        public override AccessibleObject? GetChild(int index)
        {
            if (index < 0) return null;
            foreach (Control child in Owner.Controls)
            {
                if (!child.Visible) continue;
                if (index-- == 0) return child.AccessibilityObject;
            }
            return null;
        }

        public override AccessibleObject? GetFocused()
        {
            if (Owner.Focused) return this;
            foreach (Control child in Owner.Controls)
            {
                if (child.ContainsFocus) return child.AccessibilityObject.GetFocused() ?? child.AccessibilityObject;
            }
            return null;
        }

        public override void DoDefaultAction()
        {
            if (!Owner.Enabled) return;
            switch (Owner)
            {
                case IButtonControl button:
                    button.PerformClick();
                    break;
                case CheckBox checkBox:
                    checkBox.Checked = !checkBox.Checked;
                    break;
                case RadioButton radio:
                    radio.Checked = true;
                    break;
            }
        }

        public override void Select(AccessibleSelection flags)
        {
            if ((flags & AccessibleSelection.TakeFocus) != 0 && Owner.CanFocus) Owner.Focus();
        }

        public void NotifyClients(AccessibleEvents accEvent) => RaiseClientsNotified(this, accEvent, 0);

        public void NotifyClients(AccessibleEvents accEvent, int childID) => RaiseClientsNotified(this, accEvent, childID);

        public void NotifyClients(AccessibleEvents accEvent, int objectID, int childID) => RaiseClientsNotified(this, accEvent, childID);

        public override bool RaiseLiveRegionChanged()
        {
            if (Owner is not IAutomationLiveRegion) throw new InvalidOperationException("The owner of the accessible object does not implement IAutomationLiveRegion.");
            RaiseClientsNotified(this, (AccessibleEvents)0x8019 /* EVENT_OBJECT_LIVEREGIONCHANGED */, 0);
            return true;
        }

        public override string ToString() => $"ControlAccessibleObject: Owner = {Owner}";

        private bool UsesOwnText => Owner is not (TextBoxBase or ListControl or UpDownBase or TreeView or ListView or DataGridView);

        private Label? PreviousLabel()
        {
            var parent = Owner.Parent;
            if (parent == null) return null;
            Label? best = null;
            foreach (Control sibling in parent.Controls)
            {
                if (sibling is Label label && sibling.TabIndex < Owner.TabIndex && (best == null || sibling.TabIndex > best.TabIndex)) best = label;
            }
            // Only the label directly before the control in tab order names it.
            if (best == null) return null;
            foreach (Control sibling in parent.Controls)
            {
                if (sibling != Owner && sibling is not Label && sibling.TabIndex > best.TabIndex && sibling.TabIndex < Owner.TabIndex) return null;
            }
            return best;
        }

        private static char Mnemonic(string text)
        {
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] != '&') continue;
                if (text[i + 1] == '&') { i++; continue; }
                return text[i + 1];
            }
            return '\0';
        }

        private static string StripMnemonic(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '&' && i + 1 < text.Length)
                {
                    i++;
                }
                sb.Append(text[i]);
            }
            return sb.ToString();
        }
    }
}
