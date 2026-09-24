using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;

namespace NetForms.Platform.Avalonia;

/// <summary>
/// NotifyIcon on Avalonia's <see cref="TrayIcon"/>: Shell_NotifyIcon on Windows, a StatusNotifierItem over
/// D-Bus on Linux. The menu is a <see cref="NativeMenu"/> the shell draws itself; it is rebuilt from the host
/// each time it opens, so a ContextMenuStrip filled in its Opening handler shows what the handler put there.
/// </summary>
internal sealed class AvaloniaTrayIcon : IPlatformTrayIcon
{
    private readonly ITrayIconHost _host;
    private readonly TrayIcon _icon = new();
    private IReadOnlyList<TrayMenuItem>? _items;
    private NativeMenu? _menu;

    public AvaloniaTrayIcon(ITrayIconHost host)
    {
        _host = host;
        _icon.Clicked += (_, _) => _host.Clicked();
    }

    public byte[]? IconPng
    {
        set => _icon.Icon = value == null ? null : new WindowIcon(new MemoryStream(value));
    }

    public string ToolTip
    {
        set => _icon.ToolTipText = value;
    }

    public bool Visible
    {
        set => _icon.IsVisible = value;
    }

    public IReadOnlyList<TrayMenuItem>? Menu
    {
        set
        {
            _items = value;
            if (value == null)
            {
                _icon.Menu = null;
                _menu = null;
                return;
            }
            if (_menu == null)
            {
                _menu = new NativeMenu();
                _menu.Opening += (_, _) => _host.MenuOpening();
                _menu.Closed += (_, _) => _host.MenuClosed();
                _icon.Menu = _menu;
            }
            Fill(_menu, value);
        }
    }

    private static void Fill(NativeMenu menu, IReadOnlyList<TrayMenuItem> items)
    {
        menu.Items.Clear();
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                menu.Items.Add(new NativeMenuItemSeparator());
                continue;
            }
            var native = new NativeMenuItem(item.Text) { IsEnabled = item.Enabled };
            if (item.Checked)
            {
                native.ToggleType = MenuItemToggleType.CheckBox;
                native.IsChecked = true;
            }
            if (item.Click is { } click) native.Click += (_, _) => click();
            if (item.Items is { Count: > 0 } children)
            {
                var sub = new NativeMenu();
                Fill(sub, children);
                native.Menu = sub;
            }
            menu.Items.Add(native);
        }
    }

    public void Dispose()
    {
        _icon.IsVisible = false;
        _icon.Dispose();
    }
}
