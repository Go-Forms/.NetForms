using System;
using System.Drawing;

namespace System.Windows.Forms;

public delegate void ToolStripPanelRenderEventHandler(object? sender, ToolStripPanelRenderEventArgs e);

/// <summary>What a renderer gets to paint a ToolStripPanel's background; <see cref="Handled"/> skips the panel's own.</summary>
public class ToolStripPanelRenderEventArgs : EventArgs
{
    public ToolStripPanelRenderEventArgs(Graphics g, ToolStripPanel toolStripPanel)
    {
        Graphics = g;
        ToolStripPanel = toolStripPanel;
    }

    public Graphics Graphics { get; }

    public ToolStripPanel ToolStripPanel { get; }

    public bool Handled { get; set; }
}
