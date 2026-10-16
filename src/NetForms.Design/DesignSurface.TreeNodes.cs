using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace NetForms.Design;

/// <summary>
/// The TreeNode Editor (decision 184), as Visual Studio has it: the nodes as a tree, each with its own Name, Text,
/// ImageKey/ImageIndex and SelectedImageKey/SelectedImageIndex (the tree's ImageList), StateImageIndex, Tag,
/// ToolTipText and Checked. The client sends the whole tree back (<c>setTreeNodes</c>); a node it kept ("from")
/// keeps what the editor does not show (its font, colors, its other properties).
/// </summary>
public sealed partial class DesignSurface
{
    /// <summary>The nodes of a tree, in order (a child after its parent), with their depth.</summary>
    private static List<DesignerTreeNode> TreeNodesOf(TreeNodeCollection nodes)
    {
        var list = new List<DesignerTreeNode>();
        void Walk(TreeNodeCollection level, int depth)
        {
            foreach (TreeNode node in level)
            {
                list.Add(new DesignerTreeNode
                {
                    From = list.Count,
                    Depth = depth,
                    Text = node.Text,
                    Name = node.Name,
                    Image = ImageChoice(node.ImageKey, node.ImageIndex),
                    SelectedImage = ImageChoice(node.SelectedImageKey, node.SelectedImageIndex),
                    StateImage = ImageChoice(node.StateImageKey, node.StateImageIndex),
                    Tag = node.Tag == null ? "" : System.Convert.ToString(node.Tag, CultureInfo.InvariantCulture) ?? "",
                    ToolTipText = node.ToolTipText,
                    Checked = node.Checked,
                });
                Walk(node.Nodes, depth + 1);
            }
        }
        Walk(nodes, 0);
        return list;
    }

    /// <summary>A node's picture as the editor shows it: "" (the tree's own), a key, or "#3" for an index.</summary>
    private static string ImageChoice(string key, int index) =>
        !string.IsNullOrEmpty(key) ? key : index >= 0 ? "#" + index.ToString(CultureInfo.InvariantCulture) : "";

    /// <summary>The pictures a node can take: the tree's ImageList, by key where they have keys, else by index; with thumbnails.</summary>
    private static void TreeImages(DesignerPropertyRow row, TreeView tree)
    {
        var images = tree.ImageList;
        if (images == null || images.Images.Count == 0) return;
        row.TreeImages = new List<string>();
        row.Previews = new List<string>();
        for (int i = 0; i < images.Images.Count; i++)
        {
            var key = i < images.Images.Keys.Count ? images.Images.Keys[i] : null;
            row.TreeImages.Add(string.IsNullOrEmpty(key) ? "#" + i.ToString(CultureInfo.InvariantCulture) : key);
            row.Previews.Add(Thumbnail(images.Images[i]) ?? "");
        }
    }

    /// <summary>The whole tree from the editor (JSON: the nodes in order, with their depth).</summary>
    private void SetTreeNodes(object target, string? json)
    {
        var tree = target as TreeView ?? throw new DesignerEditException("Only a TreeView's nodes are edited this way.");
        List<DesignerTreeNode>? edited;
        try { edited = JsonSerializer.Deserialize<List<DesignerTreeNode>>(json ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException ex) { throw new DesignerEditException("The nodes could not be read: " + ex.Message, ex); }
        edited ??= new List<DesignerTreeNode>();

        var old = new List<TreeNode>();
        void Collect(TreeNodeCollection level)
        {
            foreach (TreeNode n in level)
            {
                old.Add(n);
                Collect(n.Nodes);
            }
        }
        Collect(tree.Nodes);
        var oldTags = old.Select(n => n.Tag == null ? "" : System.Convert.ToString(n.Tag, CultureInfo.InvariantCulture) ?? "").ToList();

        // Taken apart first: a kept node may move under another parent.
        foreach (var n in old) n.Nodes.Clear();
        tree.Nodes.Clear();

        var parents = new List<TreeNodeCollection> { tree.Nodes };
        var used = new HashSet<int>();
        int counter = 0;
        foreach (var e in edited)
        {
            if (e.Depth < 0 || e.Depth >= parents.Count)
                throw new DesignerEditException($"'{e.Text}' is deeper than a child of the node above it.");
            TreeNode node;
            if (e.From is int from && from >= 0 && from < old.Count && used.Add(from))
            {
                node = old[from];
                if (e.Tag != oldTags[from]) node.Tag = e.Tag.Length == 0 ? null : e.Tag;
            }
            else
            {
                node = new TreeNode();
                if (e.Tag.Length > 0) node.Tag = e.Tag;
            }
            node.Text = e.Text;
            // A new node without a name is named as VS names them: Node0, Node1, ...
            if (string.IsNullOrEmpty(e.Name) && node.Name.Length == 0)
            {
                while (edited.Any(x => x.Name == "Node" + counter.ToString(CultureInfo.InvariantCulture))) counter++;
                e.Name = "Node" + (counter++).ToString(CultureInfo.InvariantCulture);
            }
            node.Name = e.Name ?? "";
            SetImage(e.Image, k => node.ImageKey = k, i => node.ImageIndex = i);
            SetImage(e.SelectedImage, k => node.SelectedImageKey = k, i => node.SelectedImageIndex = i);
            SetImage(e.StateImage, k => node.StateImageKey = k, i => node.StateImageIndex = i);
            node.ToolTipText = e.ToolTipText ?? "";
            node.Checked = e.Checked;
            parents[e.Depth].Add(node);
            parents.RemoveRange(e.Depth + 1, parents.Count - e.Depth - 1);
            parents.Add(node.Nodes);
        }
    }

    /// <summary>"" leaves the tree's picture (index -1, no key); "#3" is an index, anything else a key.</summary>
    private static void SetImage(string? choice, Action<string> key, Action<int> index)
    {
        if (string.IsNullOrEmpty(choice))
        {
            index(-1);
            key("");
        }
        else if (choice.StartsWith('#') && int.TryParse(choice.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var i)) index(i);
        else key(choice);
    }
}

/// <summary>One node in the TreeNode Editor.</summary>
public sealed class DesignerTreeNode
{
    /// <summary>Its place in the tree as it was (the order of <see cref="DesignerPropertyRow.TreeNodes"/>); null for a new node.</summary>
    public int? From { get; set; }

    /// <summary>0 for a root node, its parent's plus one for a child.</summary>
    public int Depth { get; set; }

    public string Text { get; set; } = "";
    public string? Name { get; set; }

    /// <summary>"" (the tree's), an image key, or "#3" for ImageIndex 3.</summary>
    public string Image { get; set; } = "";
    public string SelectedImage { get; set; } = "";
    public string StateImage { get; set; } = "";

    /// <summary>The Tag as text (the editor sets a string).</summary>
    public string Tag { get; set; } = "";
    public string ToolTipText { get; set; } = "";
    public bool Checked { get; set; }
}
