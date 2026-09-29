// Adapted from dotnet/winforms (src/System.Windows.Forms/System/Windows/Forms/Components/HelpProvider.cs).
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>
/// Gives controls F1 help: a pop-up with the control's <c>HelpString</c>, or the help file or URL in
/// <see cref="HelpNamespace"/> opened at the control's <c>HelpKeyword</c>. An extender provider, as in WinForms:
/// the designer shows HelpString, HelpKeyword, HelpNavigator and ShowHelp on every control of the form.
/// </summary>
[ProvideProperty("HelpString", typeof(Control))]
[ProvideProperty("HelpKeyword", typeof(Control))]
[ProvideProperty("HelpNavigator", typeof(Control))]
[ProvideProperty("ShowHelp", typeof(Control))]
[ToolboxItemFilter("System.Windows.Forms")]
[Description("Provides pop-up or online Help for controls.")]
public class HelpProvider : Component, IExtenderProvider
{
    private readonly Dictionary<Control, string?> _helpStrings = [];
    private readonly Dictionary<Control, bool> _showHelp = [];
    private readonly List<Control> _boundControls = [];
    private readonly Dictionary<Control, string?> _keywords = [];
    private readonly Dictionary<Control, HelpNavigator> _navigators = [];

    public HelpProvider()
    {
    }

    /// <summary>The help file (a .chm on Windows) or URL the keywords are looked up in.</summary>
    [Localizable(true)]
    [DefaultValue(null)]
    [Editor("System.Windows.Forms.Design.HelpNamespaceEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    [Description("The name of the Help file associated with this HelpProvider object.")]
    public virtual string? HelpNamespace { get; set; }

    [Category("Data")]
    [Localizable(false)]
    [Bindable(true)]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    [TypeConverter(typeof(StringConverter))]
    public object? Tag { get; set; }

    public virtual bool CanExtend(object? target) => target is Control;

    [DefaultValue(null)]
    [Localizable(true)]
    [Description("The Help keyword displayed when the user invokes Help for the specified control.")]
    public virtual string? GetHelpKeyword(Control ctl)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        return _keywords.TryGetValue(ctl, out string? value) ? value : null;
    }

    [DefaultValue(HelpNavigator.AssociateIndex)]
    [Localizable(true)]
    [Description("The Help command to use when retrieving Help from the Help file for the specified control.")]
    public virtual HelpNavigator GetHelpNavigator(Control ctl)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        return _navigators.TryGetValue(ctl, out HelpNavigator value) ? value : HelpNavigator.AssociateIndex;
    }

    [DefaultValue(null)]
    [Localizable(true)]
    [Description("The Help string displayed when the user invokes Help for the specified control.")]
    public virtual string? GetHelpString(Control ctl)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        return _helpStrings.TryGetValue(ctl, out string? value) ? value : null;
    }

    [Localizable(true)]
    [Description("Determines if Help will be displayed for the specified control.")]
    public virtual bool GetShowHelp(Control ctl)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        return _showHelp.TryGetValue(ctl, out bool value) && value;
    }

    /// <summary>F1 on a control (HelpRequested): the pop-up for a mouse request, else the help file, else the pop-up.</summary>
    private void OnControlHelp(object? sender, HelpEventArgs hevent)
    {
        if (sender is not Control ctl || hevent is null || !GetShowHelp(ctl)) return;
        string? helpString = GetHelpString(ctl);
        string? keyword = GetHelpKeyword(ctl);
        HelpNavigator navigator = GetHelpNavigator(ctl);

        if (Control.MouseButtons != MouseButtons.None && !string.IsNullOrEmpty(helpString))
        {
            Help.ShowPopup(ctl, helpString, hevent.MousePos);
            hevent.Handled = true;
            return;
        }

        if (HelpNamespace is not null)
        {
            if (!string.IsNullOrEmpty(keyword)) Help.ShowHelp(ctl, HelpNamespace, navigator, keyword);
            else Help.ShowHelp(ctl, HelpNamespace, navigator);
            hevent.Handled = true;
            return;
        }

        if (!string.IsNullOrEmpty(helpString))
        {
            Help.ShowPopup(ctl, helpString, hevent.MousePos);
            hevent.Handled = true;
        }
    }

    private void OnQueryAccessibilityHelp(object? sender, QueryAccessibilityHelpEventArgs e)
    {
        if (sender is not Control ctl) return;
        e.HelpString = GetHelpString(ctl);
        e.HelpKeyword = GetHelpKeyword(ctl);
        e.HelpNamespace = HelpNamespace;
    }

    public virtual void SetHelpString(Control ctl, string? helpString)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        _helpStrings[ctl] = helpString;
        if (!string.IsNullOrEmpty(helpString)) SetShowHelp(ctl, true);
        UpdateEventBinding(ctl);
    }

    public virtual void SetHelpKeyword(Control ctl, string? keyword)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        _keywords[ctl] = keyword;
        if (!string.IsNullOrEmpty(keyword)) SetShowHelp(ctl, true);
        UpdateEventBinding(ctl);
    }

    public virtual void SetHelpNavigator(Control ctl, HelpNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        if (!Enum.IsDefined(navigator)) throw new InvalidEnumArgumentException(nameof(navigator), (int)navigator, typeof(HelpNavigator));
        _navigators[ctl] = navigator;
        SetShowHelp(ctl, true);
        UpdateEventBinding(ctl);
    }

    public virtual void SetShowHelp(Control ctl, bool value)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        _showHelp[ctl] = value;
        UpdateEventBinding(ctl);
    }

    internal bool ShouldSerializeShowHelp(Control ctl)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        return _showHelp.ContainsKey(ctl);
    }

    public virtual void ResetShowHelp(Control ctl)
    {
        ArgumentNullException.ThrowIfNull(ctl);
        _showHelp.Remove(ctl);
    }

    private void UpdateEventBinding(Control ctl)
    {
        bool showHelp = GetShowHelp(ctl);
        bool isBound = _boundControls.Contains(ctl);
        if (showHelp && !isBound)
        {
            ctl.HelpRequested += OnControlHelp;
            ctl.QueryAccessibilityHelp += OnQueryAccessibilityHelp;
            _boundControls.Add(ctl);
        }
        else if (!showHelp && isBound)
        {
            ctl.HelpRequested -= OnControlHelp;
            ctl.QueryAccessibilityHelp -= OnQueryAccessibilityHelp;
            _boundControls.Remove(ctl);
        }
    }

    public override string ToString() => $"{base.ToString()}, HelpNamespace: {HelpNamespace}";
}
