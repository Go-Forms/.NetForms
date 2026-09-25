using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Globalization;
using System.Runtime.Serialization;

namespace System.Windows.Forms;

public class TreeNodeMouseHoverEventArgs : EventArgs
{
    public TreeNodeMouseHoverEventArgs(TreeNode node) => Node = node;

    public TreeNode Node { get; }
}

public delegate void TreeNodeMouseHoverEventHandler(object? sender, TreeNodeMouseHoverEventArgs e);

/// <summary>The font and colours of one item of an owner-drawn control (TreeView.GetItemRenderStyles).</summary>
[Serializable]
public class OwnerDrawPropertyBag : MarshalByRefObject, ISerializable
{
    internal OwnerDrawPropertyBag()
    {
    }

    protected OwnerDrawPropertyBag(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info);
        foreach (SerializationEntry entry in info)
        {
            switch (entry.Name)
            {
                case "Font": Font = entry.Value as Font; break;
                case "ForeColor": ForeColor = (Color)entry.Value!; break;
                case "BackColor": BackColor = (Color)entry.Value!; break;
            }
        }
    }

    public Font? Font { get; set; }

    public Color ForeColor { get; set; } = Color.Empty;

    public Color BackColor { get; set; } = Color.Empty;

    public virtual bool IsEmpty() => Font == null && ForeColor.IsEmpty && BackColor.IsEmpty;

    public static OwnerDrawPropertyBag Copy(OwnerDrawPropertyBag? value)
    {
        var result = new OwnerDrawPropertyBag();
        if (value == null) return result;
        result.BackColor = value.BackColor;
        result.ForeColor = value.ForeColor;
        result.Font = value.Font;
        return result;
    }

    void ISerializable.GetObjectData(SerializationInfo si, StreamingContext context)
    {
        si.AddValue("BackColor", BackColor);
        si.AddValue("ForeColor", ForeColor);
        si.AddValue("Font", Font);
    }
}

/// <summary>Spells a TreeNode as the constructor call the designer writes: new TreeNode("text", children) and its image overloads.</summary>
public class TreeNodeConverter : TypeConverter
{
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(destinationType);
        if (destinationType == typeof(InstanceDescriptor) && value is TreeNode node)
        {
            var children = new TreeNode[node.Nodes.Count];
            node.Nodes.CopyTo(children, 0);
            if (node.ImageIndex != -1 || node.SelectedImageIndex != -1)
            {
                if (node.Nodes.Count == 0)
                {
                    var ctor = typeof(TreeNode).GetConstructor([typeof(string), typeof(int), typeof(int)])!;
                    return new InstanceDescriptor(ctor, new object[] { node.Text, node.ImageIndex, node.SelectedImageIndex }, false);
                }
                var ctorWithChildren = typeof(TreeNode).GetConstructor([typeof(string), typeof(int), typeof(int), typeof(TreeNode[])])!;
                return new InstanceDescriptor(ctorWithChildren, new object[] { node.Text, node.ImageIndex, node.SelectedImageIndex, children }, false);
            }
            if (node.Nodes.Count == 0)
            {
                return new InstanceDescriptor(typeof(TreeNode).GetConstructor([typeof(string)])!, new object[] { node.Text }, false);
            }
            return new InstanceDescriptor(typeof(TreeNode).GetConstructor([typeof(string), typeof(TreeNode[])])!, new object[] { node.Text, children }, false);
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}

/// <summary>A node's ImageIndex in the property grid: -1 reads "(default)" (the tree's image), -2 "(none)".</summary>
public class TreeViewImageIndexConverter : ImageIndexConverter
{
    protected override bool IncludeNoneAsStandardValue => false;

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            if (string.Compare(s, "(default)", true, culture) == 0) return -1;
            if (string.Compare(s, "(none)", true, culture) == 0) return -2;
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(destinationType);
        if (destinationType == typeof(string) && value is int i)
        {
            if (i == -1) return "(default)";
            if (i == -2) return "(none)";
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var values = new System.Collections.Generic.List<object>();
        foreach (var v in base.GetStandardValues(context)) values.Add(v!);
        values.Add(-1);
        values.Add(-2);
        return new StandardValuesCollection(values);
    }
}

/// <summary>A node's ImageKey in the property grid: the empty key reads "(default)".</summary>
public class TreeViewImageKeyConverter : ImageKeyConverter
{
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(destinationType);
        if (destinationType == typeof(string) && value is null or "") return "(default)";
        return base.ConvertTo(context, culture, value, destinationType);
    }
}

public class ListViewItemMouseHoverEventArgs : EventArgs
{
    public ListViewItemMouseHoverEventArgs(ListViewItem item) => Item = item;

    public ListViewItem Item { get; }
}

public delegate void ListViewItemMouseHoverEventHandler(object? sender, ListViewItemMouseHoverEventArgs e);
