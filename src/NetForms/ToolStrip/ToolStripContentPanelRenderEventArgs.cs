using System;
using System.Drawing;

namespace System.Windows.Forms;

public delegate void ToolStripContentPanelRenderEventHandler(object? sender, ToolStripContentPanelRenderEventArgs e);

/// <summary>What a renderer gets to paint a ToolStripContentPanel's background; <see cref="Handled"/> skips the panel's own.</summary>
public class ToolStripContentPanelRenderEventArgs : EventArgs
{
    public ToolStripContentPanelRenderEventArgs(Graphics g, ToolStripContentPanel contentPanel)
    {
        Graphics = g;
        ToolStripContentPanel = contentPanel;
    }

    public Graphics Graphics { get; }

    public ToolStripContentPanel ToolStripContentPanel { get; }

    public bool Handled { get; set; }
}
