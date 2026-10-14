using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NetForms.Design;

/// <summary>
/// Menus on the design surface, as in VS: a click on a menu item opens its drop-down under it (a sub-menu to the
/// right), and a ContextMenuStrip selected in the tray opens at the top left of the form - so every item, however
/// deep, can be selected, given a Click handler, ShortcutKeys, Checked and the rest. The drop-downs are painted by
/// NetForms like the form; their items are on the canvas as items of the view.
/// </summary>
public sealed partial class DesignSurface
{
    /// <summary>
    /// The menu item or ContextMenuStrip the canvas shows open (with every drop-down above it); a leaf item keeps
    /// the drop-down it is in open. Null: no menu is open.
    /// </summary>
    public string? OpenMenu { get; set; }

    /// <summary>The drop-downs from the outermost in: what <see cref="OpenMenu"/> needs open.</summary>
    private List<ToolStripDropDown> OpenDropDowns()
    {
        var chain = new List<ToolStripDropDown>();
        if (string.IsNullOrEmpty(OpenMenu)) return chain;
        object target;
        try { target = Resolve(OpenMenu); }
        catch (DesignerEditException) { return chain; }

        if (target is ToolStripDropDownItem { HasDropDownItems: true } owner) chain.Add(owner.DropDown);
        else if (target is ToolStripDropDown own) chain.Add(own);
        for (var item = target as ToolStripItem ?? (target as ToolStripDropDown)?.OwnerItem; item?.Owner is ToolStripDropDown dropDown; item = dropDown.OwnerItem)
            if (!chain.Contains(dropDown)) chain.Add(dropDown);
        chain.Reverse();
        return chain;
    }

    /// <summary>Paints the open drop-downs and puts them and their items on the view.</summary>
    private void AddDropDowns(DesignerView view, Dictionary<object, string> ids)
    {
        Rectangle? previous = null;
        foreach (var dropDown in OpenDropDowns())
        {
            var ownerItem = dropDown.OwnerItem;
            string? ownerId = ownerItem != null ? ids.GetValueOrDefault(ownerItem) : ids.GetValueOrDefault(dropDown);
            if (ownerId == null) break;

            Point at;
            if (ownerItem == null) at = new Point(0, 0); // a ContextMenuStrip of the form: at its top left, as VS shows it
            else if (ownerItem.Owner is ToolStripDropDown && previous is { } parent) at = new Point(parent.Right, parent.Y + ownerItem.Bounds.Y);
            else if (ownerItem.Owner is { } strip && strip.Parent != null && ids.ContainsKey(strip))
            {
                var origin = strip.Parent.PointToForm(strip.Location);
                var b = ownerItem.Bounds;
                bool vertical = strip.LayoutStyle is ToolStripLayoutStyle.VerticalStackWithOverflow;
                at = vertical ? new Point(origin.X + b.Right, origin.Y + b.Y) : new Point(origin.X + b.X, origin.Y + b.Bottom);
            }
            else break;

            var picture = Paint(dropDown, out var size, out var bounds);
            var rect = new Rectangle(at, size);
            (view.DropDowns ??= new()).Add(new DesignerDropDown { Owner = ownerId, X = rect.X, Y = rect.Y, W = rect.Width, H = rect.Height, Png = picture });
            // The drop-down itself, under its items: a click on its margin selects what owns it.
            view.Items.Add(new DesignerViewItem { Id = ownerId, Type = dropDown.GetType().FullName!, Kind = "dropdown", X = rect.X, Y = rect.Y, W = rect.Width, H = rect.Height, Visible = true });
            foreach (var (item, b) in bounds)
            {
                if (!ids.TryGetValue(item, out var itemId)) continue;
                view.Items.Add(new DesignerViewItem
                {
                    Id = itemId,
                    Type = item.GetType().FullName ?? item.GetType().Name,
                    Parent = ownerId,
                    Kind = "item",
                    X = rect.X + b.X,
                    Y = rect.Y + b.Y,
                    W = b.Width,
                    H = b.Height,
                    Left = b.X,
                    Top = b.Y,
                    Visible = true,
                    HasDropDown = item is ToolStripDropDownItem { HasDropDownItems: true },
                });
            }
            previous = rect;
        }
        view.OpenMenu = view.DropDowns != null ? OpenMenu : null;
    }

    /// <summary>
    /// A drop-down laid out at its preferred size and painted, with where each item is on it. Its own Size is put
    /// back afterwards: a picture for the canvas is not an edit of the form.
    /// </summary>
    private static string Paint(ToolStripDropDown dropDown, out Size size, out List<(ToolStripItem Item, Rectangle Bounds)> bounds)
    {
        var old = dropDown.Size;
        dropDown.EnsureItemLayout();
        size = dropDown.GetPreferredSize(Size.Empty);
        size = new Size(Math.Max(1, Math.Max(size.Width, dropDown.MinimumSize.Width)), Math.Max(1, Math.Max(size.Height, dropDown.MinimumSize.Height)));
        try
        {
            dropDown.Size = size;
            dropDown.PerformLayout();
            dropDown.EnsureItemLayout();
            bounds = dropDown.Items.Cast<ToolStripItem>().Where(i => i.Available).Select(i => (i, i.Bounds)).ToList();
            using var bitmap = new Bitmap(size.Width, size.Height);
            dropDown.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            return System.Convert.ToBase64String(png.ToArray());
        }
        finally
        {
            if (dropDown.Size != old) dropDown.Size = old;
        }
    }
}

/// <summary>An open drop-down menu on the canvas: its picture and where it is.</summary>
public sealed class DesignerDropDown
{
    /// <summary>The menu item it drops from, or the ContextMenuStrip.</summary>
    public string Owner { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public string Png { get; set; } = "";
}
