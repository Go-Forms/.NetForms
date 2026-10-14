using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NetForms.Design.Serialization;

namespace NetForms.Design;

/// <summary>
/// The columns of a DataGridView and a ListView on the design surface: their headers are items of the canvas, so a
/// click selects a column and the grid edits it - Width, DataPropertyName, ReadOnly, its cell style, and for a
/// DataGridView column its type (text box, check box, combo box, button, image, link), as VS's Edit Columns dialog
/// does. Before, the collection editor changed captions only, and every new column was a text box column.
/// </summary>
public sealed partial class DesignSurface
{
    /// <summary>The column types the designer offers, as VS's Add Column dialog lists them.</summary>
    private static readonly Type[] s_columnTypes =
    {
        typeof(DataGridViewTextBoxColumn), typeof(DataGridViewCheckBoxColumn), typeof(DataGridViewComboBoxColumn),
        typeof(DataGridViewButtonColumn), typeof(DataGridViewImageColumn), typeof(DataGridViewLinkColumn),
    };

    /// <summary>The headers of a grid's or a list view's columns, as items over the control on the canvas.</summary>
    private void AddColumnHeaders(Control control, string controlId, Dictionary<object, string> ids, List<DesignerViewItem> into)
    {
        var client = control.PointToForm(Point.Empty);
        var headers = new List<(object Column, Rectangle Bounds)>();
        switch (control)
        {
            case DataGridView grid when grid.ColumnHeadersVisible:
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (!column.Visible) continue;
                    var r = grid.GetColumnDisplayRectangle(column.Index, true);
                    headers.Add((column, new Rectangle(r.X, r.Y, r.Width, Math.Min(r.Height, grid.ColumnHeadersHeight))));
                }
                break;
            case ListView list:
                for (int i = 0; i < list.Columns.Count; i++) headers.Add((list.Columns[i], list.GetColumnHeaderBounds(i)));
                break;
            default:
                return;
        }
        bool visible = VisibleOnCanvas(control);
        foreach (var (column, r) in headers)
        {
            if (r.Width <= 0 || r.Height <= 0 || !ids.TryGetValue(column, out var id)) continue;
            into.Add(new DesignerViewItem
            {
                Id = id,
                Type = column.GetType().FullName ?? column.GetType().Name,
                Parent = controlId,
                Kind = "item",
                X = client.X + r.X,
                Y = client.Y + r.Y,
                W = r.Width,
                H = r.Height,
                Left = r.X,
                Top = r.Y,
                Visible = visible,
            });
        }
    }

    /// <summary>The ColumnType row of a DataGridView column: its type, to pick another.</summary>
    private static DesignerPropertyRow ColumnTypeRow(DataGridViewColumn column) => new()
    {
        Name = "ColumnType",
        Category = "Design",
        Description = "The type of the column: what its cells show and edit (text box, check box, combo box, button, image, link).",
        Type = "Type",
        Value = column.GetType().Name,
        Editor = "enum",
        Options = s_columnTypes.Select(t => t.Name).ToList(),
        Modified = false, // a type has no default to reset to
    };

    /// <summary>
    /// A column of another type in place of <paramref name="old"/>: the same name, place and everything the two
    /// types share that the old one had set (HeaderText, Width, DataPropertyName, ReadOnly, its cell style, …), as
    /// VS's Edit Columns dialog changes ColumnType.
    /// </summary>
    private void SetColumnType(DataGridViewColumn old, string? typeName)
    {
        var type = s_columnTypes.FirstOrDefault(t => t.Name == typeName || t.FullName == typeName)
            ?? throw new DesignerEditException($"'{typeName}' is not a column type: pick one of {string.Join(", ", s_columnTypes.Select(t => t.Name))}.");
        if (old.GetType() == type) return;
        var grid = old.DataGridView ?? throw new DesignerEditException("The column is not in a DataGridView.");
        var component = Model.FindByInstance(old) ?? throw new DesignerEditException("The column is not a component of the form.");
        var name = component.Name;

        DataGridViewColumn replacement;
        try { replacement = (DataGridViewColumn)Activator.CreateInstance(type)!; }
        catch (Exception ex) { throw new DesignerEditException($"Creating a {type.Name} failed: {Unwrap(ex).Message}", ex); }
        var target = TypeDescriptor.GetProperties(replacement);
        foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(old))
        {
            if (pd.Name is "Name" or "CellTemplate" or "CellType" or "DisplayIndex" || pd.IsReadOnly || DesignerCodeWriter.IsExtenderProperty(pd)) continue;
            if (pd.SerializationVisibility == DesignerSerializationVisibility.Hidden || !ShouldSerializeSafe(pd, old)) continue;
            var into = target[pd.Name];
            if (into == null || into.IsReadOnly || !into.PropertyType.IsAssignableFrom(pd.PropertyType)) continue;
            try
            {
                var value = pd.GetValue(old);
                into.SetValue(replacement, value is DataGridViewCellStyle style ? new DataGridViewCellStyle(style) : value);
            }
            catch (Exception) { } // a value the new type does not take (SortMode on an image column)
        }

        replacement.Name = old.Name; // the column's own Name (what Columns["..."] finds), kept as VS keeps it
        int index = old.Index;
        grid.Columns.RemoveAt(index);
        Model.RemoveComponent(component);
        grid.Columns.Insert(index, replacement);
        try { Model.AddComponent(replacement, name); }
        catch (ArgumentException ex) { throw new DesignerEditException(ex.Message, ex); }
    }

    private static bool ShouldSerializeSafe(PropertyDescriptor pd, object instance)
    {
        try { return pd.ShouldSerializeValue(instance); }
        catch (Exception) { return false; }
    }
}
