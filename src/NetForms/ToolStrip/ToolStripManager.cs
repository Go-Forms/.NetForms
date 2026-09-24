using System;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms;

public enum ToolStripManagerRenderMode
{
    Custom = 0,
    System = 1,
    Professional = 2,
}

/// <summary>
/// Application-wide state shared by every strip: the default renderer, the list of live strips
/// (so menu shortcuts can be found), and the stack of drop-downs that is currently open, which is
/// what makes Escape, the arrow keys and "click outside to dismiss" work across windows.
/// </summary>
public static class ToolStripManager
{
    private static readonly List<WeakReference<ToolStrip>> s_toolStrips = new();
    private static readonly List<ToolStripDropDown> s_openDropDowns = new();
    private static ToolStripRenderer? s_renderer;
    private static ToolStripManagerRenderMode s_renderMode = ToolStripManagerRenderMode.Professional;

    public static ToolStripRenderer Renderer
    {
        get => s_renderer ??= new ToolStripProfessionalRenderer();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(s_renderer, value)) return;
            s_renderer = value;
            s_renderMode = ToolStripManagerRenderMode.Custom;
            OnRendererChanged();
        }
    }

    public static ToolStripManagerRenderMode RenderMode
    {
        get => s_renderMode;
        set
        {
            if (value == ToolStripManagerRenderMode.Custom && s_renderer == null)
                throw new NotSupportedException("Set the Renderer property to use ToolStripManagerRenderMode.Custom.");
            if (s_renderMode == value) return;
            s_renderMode = value;
            s_renderer = value switch
            {
                ToolStripManagerRenderMode.System => new ToolStripSystemRenderer(),
                ToolStripManagerRenderMode.Professional => new ToolStripProfessionalRenderer(),
                _ => s_renderer,
            };
            OnRendererChanged();
        }
    }

    public static bool VisualStylesEnabled { get; set; } = true;

    public static event EventHandler? RendererChanged;

    private static void OnRendererChanged()
    {
        RendererChanged?.Invoke(null, EventArgs.Empty);
        foreach (var strip in LiveToolStrips()) strip.Invalidate();
    }

    // --- registry of live strips ------------------------------------------------------

    internal static void Register(ToolStrip strip)
    {
        lock (s_toolStrips) s_toolStrips.Add(new WeakReference<ToolStrip>(strip));
    }

    internal static void Unregister(ToolStrip strip)
    {
        lock (s_toolStrips)
        {
            s_toolStrips.RemoveAll(r => !r.TryGetTarget(out var t) || ReferenceEquals(t, strip));
        }
    }

    private static List<ToolStrip> LiveToolStrips()
    {
        var result = new List<ToolStrip>();
        lock (s_toolStrips)
        {
            s_toolStrips.RemoveAll(r => !r.TryGetTarget(out _));
            foreach (var reference in s_toolStrips)
            {
                if (reference.TryGetTarget(out var strip)) result.Add(strip);
            }
        }
        return result;
    }

    public static ToolStrip? FindToolStrip(string toolStripName)
    {
        foreach (var strip in LiveToolStrips())
        {
            if (string.Equals(strip.Name, toolStripName, StringComparison.Ordinal)) return strip;
        }
        return null;
    }

    // --- open drop-downs ---------------------------------------------------------------

    internal static void RegisterOpenDropDown(ToolStripDropDown dropDown)
    {
        if (!s_openDropDowns.Contains(dropDown)) s_openDropDowns.Add(dropDown);
    }

    internal static void UnregisterOpenDropDown(ToolStripDropDown dropDown) => s_openDropDowns.Remove(dropDown);

    /// <summary>
    /// The innermost drop-down currently open, or null when no menu is up. Drop-downs that went
    /// away without telling us (their form was disposed) are dropped here rather than being left
    /// to swallow the application's keys.
    /// </summary>
    internal static ToolStripDropDown? ActiveDropDown
    {
        get
        {
            for (int i = s_openDropDowns.Count - 1; i >= 0; i--)
            {
                if (s_openDropDowns[i].Visible) return s_openDropDowns[i];
                s_openDropDowns.RemoveAt(i);
            }
            return null;
        }
    }

    internal static bool IsMenuOpen => ActiveDropDown != null;

    internal static void CloseAllDropDowns(ToolStripDropDownCloseReason reason)
    {
        for (int i = s_openDropDowns.Count - 1; i >= 0; i--)
        {
            s_openDropDowns[i].Close(reason);
        }
        s_openDropDowns.Clear();
    }

    // --- shortcuts and menu keys ---------------------------------------------------------

    public static bool IsValidShortcut(Keys shortcut)
    {
        if (shortcut == Keys.None) return false;
        var code = shortcut & Keys.KeyCode;
        if (code is >= Keys.F1 and <= Keys.F24) return true;
        return (shortcut & (Keys.Control | Keys.Alt)) != Keys.None;
    }

    public static bool IsShortcutDefined(Keys shortcut) => FindShortcutItem(shortcut, null) != null;

    private static ToolStripMenuItem? FindShortcutItem(Keys shortcut, Form? scope)
    {
        foreach (var strip in LiveToolStrips())
        {
            if (strip is ToolStripDropDown) continue;
            if (scope != null && strip.FindForm() != scope) continue;
            var hit = FindShortcutItem(strip.Items, shortcut);
            if (hit != null) return hit;
        }
        return null;
    }

    private static ToolStripMenuItem? FindShortcutItem(ToolStripItemCollection items, Keys shortcut)
    {
        foreach (var item in items)
        {
            if (item is ToolStripMenuItem menuItem)
            {
                if (menuItem.ShortcutKeys == shortcut) return menuItem;
                if (menuItem.HasDropDownItems)
                {
                    var nested = FindShortcutItem(menuItem.DropDownItems, shortcut);
                    if (nested != null) return nested;
                }
            }
            else if (item is ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems)
            {
                var nested = FindShortcutItem(dropDownItem.DropDownItems, shortcut);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    /// <summary>
    /// Called by <see cref="Form"/> before the focused control sees a key: fires a menu shortcut, or
    /// drives the open menu with the arrow keys, Enter and Escape. Returns true when the key is used up.
    /// </summary>
    internal static bool ProcessCmdKey(Keys keyData, Form form)
    {
        if (IsMenuOpen && ProcessMenuKey(keyData)) return true;

        if (IsValidShortcut(keyData))
        {
            var item = FindShortcutItem(keyData, form);
            if (item is { Enabled: true })
            {
                item.PerformClick();
                return true;
            }
        }
        return false;
    }

    private static bool ProcessMenuKey(Keys keyData)
    {
        var dropDown = ActiveDropDown;
        if (dropDown == null) return false;
        var selected = dropDown.SelectedItemInternal;

        switch (keyData & Keys.KeyCode)
        {
            case Keys.Escape:
                if (dropDown.OwnerItem?.Owner is ToolStripDropDown) dropDown.Close(ToolStripDropDownCloseReason.Keyboard);
                else CloseAllDropDowns(ToolStripDropDownCloseReason.Keyboard);
                return true;

            case Keys.Down:
                dropDown.SelectItem(dropDown.GetNextItem(selected, ArrowDirection.Down));
                return true;

            case Keys.Up:
                dropDown.SelectItem(dropDown.GetNextItem(selected, ArrowDirection.Up));
                return true;

            case Keys.Right:
                if (selected is ToolStripDropDownItem { HasDropDownItems: true } expandable)
                {
                    expandable.ShowDropDown(fromKeyboard: true);
                    return true;
                }
                return MoveAlongMenuBar(dropDown, forward: true);

            case Keys.Left:
                if (dropDown.OwnerItem?.Owner is ToolStripDropDown)
                {
                    dropDown.Close(ToolStripDropDownCloseReason.Keyboard);
                    return true;
                }
                return MoveAlongMenuBar(dropDown, forward: false);

            case Keys.Return:
                if (selected != null)
                {
                    selected.PerformClick();
                    if (selected is not ToolStripDropDownItem { HasDropDownItems: true })
                    {
                        dropDown.CloseChain(ToolStripDropDownCloseReason.Keyboard);
                    }
                    return true;
                }
                return true;
        }
        return false;
    }

    /// <summary>Left/Right at the edge of a menu walks to the neighbouring top-level menu.</summary>
    private static bool MoveAlongMenuBar(ToolStripDropDown dropDown, bool forward)
    {
        var root = dropDown;
        while (root.OwnerItem?.Owner is ToolStripDropDown parent) root = parent;
        if (root.OwnerItem?.Owner is not ToolStrip bar || bar is ToolStripDropDown) return false;

        var next = bar.GetNextItem(root.OwnerItem, forward ? ArrowDirection.Right : ArrowDirection.Left);
        if (next is not ToolStripDropDownItem { HasDropDownItems: true } menu) return false;

        CloseAllDropDowns(ToolStripDropDownCloseReason.Keyboard);
        bar.SelectItem(menu);
        menu.ShowDropDown(fromKeyboard: true);
        return true;
    }

    // --- merging -------------------------------------------------------------------------

    /// <summary>
    /// Appends the source strip's items to the target, honouring each item's MergeAction. Only the
    /// Append/Insert/Remove actions are implemented; MatchOnly and Replace fall back to Append.
    /// </summary>
    public static bool Merge(ToolStrip sourceToolStrip, ToolStrip targetToolStrip)
    {
        ArgumentNullException.ThrowIfNull(sourceToolStrip);
        ArgumentNullException.ThrowIfNull(targetToolStrip);
        if (ReferenceEquals(sourceToolStrip, targetToolStrip)) throw new ArgumentException("A strip cannot be merged into itself.");

        bool merged = false;
        foreach (var item in sourceToolStrip.Items.ToArray())
        {
            switch (item.MergeAction)
            {
                case MergeAction.Remove:
                    sourceToolStrip.Items.Remove(item);
                    merged = true;
                    break;
                case MergeAction.Insert when item.MergeIndex >= 0:
                    targetToolStrip.Items.Insert(Math.Min(item.MergeIndex, targetToolStrip.Items.Count), item);
                    merged = true;
                    break;
                default:
                    targetToolStrip.Items.Add(item);
                    merged = true;
                    break;
            }
        }
        return merged;
    }

    public static bool Merge(ToolStrip sourceToolStrip, string targetName)
    {
        var target = FindToolStrip(targetName);
        return target != null && Merge(sourceToolStrip, target);
    }
}
