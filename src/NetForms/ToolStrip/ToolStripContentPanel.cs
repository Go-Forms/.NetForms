using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The middle of a <see cref="ToolStripContainer"/>: where the window's controls go. It fills what the
/// container's tool strip panels leave and raises <see cref="Load"/> once it is created.
/// </summary>
[DefaultEvent("Load")]
[InitializationEvent("Load")]
[ToolboxItem(false)]
public class ToolStripContentPanel : Panel
{
    private ToolStripRenderer? _renderer;
    private ToolStripRenderMode _renderMode = ToolStripRenderMode.System;

    public ToolStripContentPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    [Localizable(false)]
    public override AutoSizeMode AutoSizeMode { get => AutoSizeMode.GrowOnly; set { } }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override AnchorStyles Anchor { get => base.Anchor; set => base.Anchor = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool AutoScroll { get => base.AutoScroll; set => base.AutoScroll = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size AutoScrollMargin { get => base.AutoScrollMargin; set => base.AutoScrollMargin = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size AutoScrollMinSize { get => base.AutoScrollMinSize; set => base.AutoScrollMinSize = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new event EventHandler? AutoSizeChanged
    {
        add => base.AutoSizeChanged += value;
        remove => base.AutoSizeChanged -= value;
    }

    /// <summary>A transparent content panel makes its container transparent too, so what is behind shows through (WinForms).</summary>
    public override Color BackColor
    {
        get => base.BackColor;
        set
        {
            if (Parent is ToolStripContainer container && value == Color.Transparent) container.BackColor = Color.Transparent;
            base.BackColor = value;
        }
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool CausesValidation { get => base.CausesValidation; set => base.CausesValidation = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? CausesValidationChanged
    {
        add => base.CausesValidationChanged += value;
        remove => base.CausesValidationChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override DockStyle Dock { get => base.Dock; set => base.Dock = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new event EventHandler? DockChanged
    {
        add => base.DockChanged += value;
        remove => base.DockChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Point Location { get => base.Location; set => base.Location = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new event EventHandler? LocationChanged
    {
        add => base.LocationChanged += value;
        remove => base.LocationChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Size MinimumSize { get => base.MinimumSize; set => base.MinimumSize = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Size MaximumSize { get => base.MaximumSize; set => base.MaximumSize = value; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    public new string Name { get => base.Name; set => base.Name = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new int TabIndex { get => base.TabIndex; set => base.TabIndex = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TabIndexChanged
    {
        add => base.TabIndexChanged += value;
        remove => base.TabIndexChanged -= value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToolStripRenderer Renderer
    {
        get
        {
            if (_renderMode == ToolStripRenderMode.ManagerRenderMode) return ToolStripManager.Renderer;
            return _renderer ??= _renderMode == ToolStripRenderMode.Professional ? new ToolStripProfessionalRenderer() : new ToolStripSystemRenderer();
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_renderer, value) && _renderMode == ToolStripRenderMode.Custom) return;
            _renderMode = ToolStripRenderMode.Custom;
            _renderer = value;
            OnRendererChanged(EventArgs.Empty);
        }
    }

    /// <summary>System by default: the content panel looks like any panel unless told otherwise (WinForms).</summary>
    [Description("The painting styles applied to the control.")]
    [Category("Appearance")]
    public ToolStripRenderMode RenderMode
    {
        get => _renderMode;
        set
        {
            if (value == ToolStripRenderMode.Custom && _renderer == null)
                throw new NotSupportedException("Set the Renderer property to use ToolStripRenderMode.Custom.");
            if (_renderMode == value) return;
            _renderMode = value;
            if (value != ToolStripRenderMode.Custom) _renderer = null;
            OnRendererChanged(EventArgs.Empty);
        }
    }

    private bool ShouldSerializeRenderMode() => _renderMode != ToolStripRenderMode.System && _renderMode != ToolStripRenderMode.Custom;

    private void ResetRenderMode() => RenderMode = ToolStripRenderMode.System;

    [Category("Behavior")]
    [Description("Occurs whenever the content panel loads.")]
    public event EventHandler? Load;

    [Category("Appearance")]
    [Description("Occurs when the value of the Renderer property changes.")]
    public event EventHandler? RendererChanged;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RecreatingHandle) OnLoad(EventArgs.Empty);
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnLoad(EventArgs e) => Load?.Invoke(this, e);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var args = new ToolStripContentPanelRenderEventArgs(e.Graphics, this);
        Renderer.DrawToolStripContentPanelBackground(args);
        if (!args.Handled) base.OnPaintBackground(e);
    }

    protected virtual void OnRendererChanged(EventArgs e)
    {
        Invalidate();
        RendererChanged?.Invoke(this, e);
    }
}
