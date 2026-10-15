using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NetForms.Design;

/// <summary>
/// Pictures and icons set in the designer, as in VS: Image, BackgroundImage, a form's Icon, a menu item's Image are
/// picked from a file and kept in the form's .resx (<see cref="NetForms.Design.Serialization.ResxWriter"/>); an
/// ImageList's Images are added, named, reordered and removed, and its ImageStream written the same way. The grid
/// shows a picture's thumbnail.
/// </summary>
public sealed partial class DesignSurface
{
    /// <summary>The largest side of a thumbnail in the grid.</summary>
    private const int ThumbnailSize = 48;

    /// <summary>A property that holds a picture or an icon (what the image editor of the grid edits).</summary>
    private static bool IsImageProperty(Type type) => type == typeof(Image) || type == typeof(Bitmap) || type == typeof(Icon);

    /// <summary>setImage: the picture (or icon) in a file on the property; no path clears it.</summary>
    private void SetImage(object target, string name, string? path)
    {
        var pd = FindProperty(target, name) ?? throw new DesignerEditException($"'{target.GetType().Name}' has no property '{name}'.");
        if (pd.IsReadOnly || !IsImageProperty(pd.PropertyType)) throw new DesignerEditException($"{name} is not a picture.");
        object? value = null;
        if (!string.IsNullOrEmpty(path)) value = LoadPicture(path, pd.PropertyType == typeof(Icon));
        try { pd.SetValue(target, value); }
        catch (Exception ex) { throw new DesignerEditException($"{name} cannot be set: {Unwrap(ex).Message}", ex); }
    }

    /// <summary>A picture file read whole (the file is not held open): an icon for an Icon property, else a bitmap (an .ico too).</summary>
    private static object LoadPicture(string path, bool icon)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new DesignerEditException($"{Path.GetFileName(path)} cannot be read: {ex.Message}", ex); }
        try
        {
            bool isIcon = bytes.Length > 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1 && bytes[3] == 0;
            if (icon)
            {
                if (!isIcon) throw new DesignerEditException($"{Path.GetFileName(path)} is not an icon (.ico).");
                return new Icon(new MemoryStream(bytes));
            }
            if (isIcon) return new Icon(new MemoryStream(bytes)).ToBitmap();
            return new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception ex) when (ex is not DesignerEditException)
        {
            throw new DesignerEditException($"{Path.GetFileName(path)} is not a picture NetForms can read: {Unwrap(ex).Message}", ex);
        }
    }

    /// <summary>
    /// setImages: an ImageList's pictures in a new order - <paramref name="ids"/> names each one (<c>#2</c>: the
    /// third it had; a path: a file to add), <paramref name="keys"/> its name (Images.SetKeyName).
    /// </summary>
    private static void SetImages(object target, string[] keys, string?[]? ids)
    {
        var list = target as ImageList ?? throw new DesignerEditException($"'{target.GetType().Name}' is not an ImageList.");
        if (ids == null || ids.Length != keys.Length) throw new DesignerEditException("The edit needs one id for each name.");
        var old = Enumerable.Range(0, list.Images.Count).Select(i => list.Images[i]).ToList();
        var pictures = new List<(string Key, Image Image)>();
        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i] ?? "";
            Image image;
            if (id.StartsWith('#') && int.TryParse(id.AsSpan(1), out int at))
            {
                if (at < 0 || at >= old.Count) throw new DesignerEditException($"'{id}' is not a picture of the list.");
                image = old[at];
            }
            else
            {
                image = (Image)LoadPicture(id, icon: false);
            }
            pictures.Add((keys[i] ?? "", image));
        }
        list.Images.Clear();
        foreach (var (key, image) in pictures) list.Images.Add(key, image);
    }

    /// <summary>The grid's row for a picture: its size as the value, a thumbnail, the image editor.</summary>
    private static void ImageRow(DesignerPropertyRow row, object? value)
    {
        row.Editor = "image";
        row.Value = value switch
        {
            null => "(none)",
            Icon icon => $"(Icon) {icon.Width} × {icon.Height}",
            Image image => $"({image.GetType().Name}) {image.Width} × {image.Height}",
            _ => value.ToString(),
        };
        row.Preview = value switch
        {
            Icon icon => Thumbnail(icon.ToBitmap()),
            Image image => Thumbnail(image),
            _ => null,
        };
    }

    /// <summary>An ImageList's Images: the names and thumbnails of its pictures, for the images editor.</summary>
    private static void ImagesRow(DesignerPropertyRow row, ImageList list)
    {
        row.Editor = "images";
        row.Value = $"({list.Images.Count} images)";
        row.Items = list.Images.Keys.Cast<string?>().Select(k => k ?? "").ToList();
        while (row.Items.Count < list.Images.Count) row.Items.Add("");
        row.ItemIds = Enumerable.Range(0, list.Images.Count).Select(i => "#" + i).ToList();
        row.Previews = Enumerable.Range(0, list.Images.Count).Select(i => Thumbnail(list.Images[i]) ?? "").ToList();
        row.Modified = list.Images.Count > 0;
    }

    /// <summary>A PNG of the picture scaled to fit <see cref="ThumbnailSize"/>, base64; null when it cannot be drawn.</summary>
    private static string? Thumbnail(Image image)
    {
        try
        {
            double scale = Math.Min(1, (double)ThumbnailSize / Math.Max(1, Math.Max(image.Width, image.Height)));
            int w = Math.Max(1, (int)Math.Round(image.Width * scale)), h = Math.Max(1, (int)Math.Round(image.Height * scale));
            using var bitmap = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bitmap)) g.DrawImage(image, new Rectangle(0, 0, w, h));
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            return System.Convert.ToBase64String(png.ToArray());
        }
        catch (Exception) { return null; }
    }
}
