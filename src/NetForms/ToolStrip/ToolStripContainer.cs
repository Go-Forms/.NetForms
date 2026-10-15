using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// A <see cref="ToolStripPanel"/> on each side and a <see cref="ToolStripContentPanel"/> in the middle: tool
/// bars, menus and status bars go into the side panels, where the user can drag them about, and the rest of
/// the window's controls into the content panel. Its own Controls hold just those five and take no others.
/// </summary>
[Description("Provides panels on each side of the form that can contain one or more ToolStrip, MenuStrip, or StatusStrip controls.")]
public class ToolStripContainer : ContainerControl
{
    private readonly ToolStripPanel _topPanel;
    private readonly ToolStripPanel _bottomPanel;
    private readonly ToolStripPanel _leftPanel;
    private readonly ToolStripPanel _rightPanel;
    private readonly ToolStripContentPanel _contentPanel;

    public ToolStripContainer()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SuspendLayout();
        try
        {
            _topPanel = new ToolStripPanel(this);
            _bottomPanel = new ToolStripPanel(this);
            _leftPanel = new ToolStripPanel(this);
            _rightPanel = new ToolStripPanel(this);
            _contentPanel = new ToolStripContentPanel { Dock = DockStyle.Fill };
            _topPanel.Dock = DockStyle.Top;
            _bottomPanel.Dock = DockStyle.Bottom;
            _rightPanel.Dock = DockStyle.Right;
            _leftPanel.Dock = DockStyle.Left;

            // In this order (WinForms): the content first, so it docks last and fills what the panels leave.
            var controls = (ToolStripContainerTypedControlCollection)Controls;
            controls.AddInternal(_contentPanel);
            controls.AddInternal(_leftPanel);
            controls.AddInternal(_rightPanel);
            controls.AddInternal(_topPanel);
            controls.AddInternal(_bottomPanel);
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    internal ToolStripPanel[] Panels => new[] { _topPanel, _bottomPanel, _leftPanel, _rightPanel };

    protected override Size DefaultSize => new Size(150, 175);

    // --- the panels ---------------------------------------------------------------------------------

    [Category("Appearance")]
    [Description("The bottom panel of the ToolStripContainer.  This area is used for moving ToolStrips, MenuStrips and StatusStrips.")]
    [Localizable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ToolStripPanel BottomToolStripPanel => _bottomPanel;

    [Category("Appearance")]
    [Description("Specifies the visibility of the bottom panel of the ToolStripContainer.")]
    [DefaultValue(true)]
    public bool BottomToolStripPanelVisible { get => _bottomPanel.VisibleOwn; set => _bottomPanel.Visible = value; }

    [Category("Appearance")]
    [Description("The center panel of the ToolStripContainer.")]
    [Localizable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ToolStripContentPanel ContentPanel => _contentPanel;

    [Category("Appearance")]
    [Description("The left panel of the ToolStripContainer.  This area is used for moving ToolStrips, MenuStrips and StatusStrips.")]
    [Localizable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ToolStripPanel LeftToolStripPanel => _leftPanel;

    [Category("Appearance")]
    [Description("Specifies the visibility of the left panel of the ToolStripContainer.")]
    [DefaultValue(true)]
    public bool LeftToolStripPanelVisible { get => _leftPanel.VisibleOwn; set => _leftPanel.Visible = value; }

    [Category("Appearance")]
    [Description("The right panel of the ToolStripContainer.  This area is used for moving ToolStrips, MenuStrips and StatusStrips.")]
    [Localizable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ToolStripPanel RightToolStripPanel => _rightPanel;

    [Category("Appearance")]
    [Description("Specifies the visibility of the right panel of the ToolStripContainer.")]
    [DefaultValue(true)]
    public bool RightToolStripPanelVisible { get => _rightPanel.VisibleOwn; set => _rightPanel.Visible = value; }

    [Category("Appearance")]
    [Description("The top panel of the ToolStripContainer.  This area is used for moving ToolStrips, MenuStrips and StatusStrips.")]
    [Localizable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ToolStripPanel TopToolStripPanel => _topPanel;

    [Category("Appearance")]
    [Description("Specifies the visibility of the top panel of the ToolStripContainer.")]
    [DefaultValue(true)]
    public bool TopToolStripPanelVisible { get => _topPanel.VisibleOwn; set => _topPanel.Visible = value; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ControlCollection Controls => base.Controls;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected override ControlCollection CreateControlsInstance() => new ToolStripContainerTypedControlCollection(this);

    protected override AccessibleObject CreateAccessibilityInstance() => base.CreateAccessibilityInstance();

    /// <summary>Right to left, the left and right panels change sides (WinForms).</summary>
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        bool rtl = RightToLeft == RightToLeft.Yes;
        _leftPanel.Dock = rtl ? DockStyle.Right : DockStyle.Left;
        _rightPanel.Dock = rtl ? DockStyle.Left : DockStyle.Right;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        foreach (Control control in Controls) control.SuspendLayout();
        base.OnSizeChanged(e);
        foreach (Control control in Controls) control.ResumeLayout();
    }

    // --- members WinForms hides on this control (checked by AttributeDiffTests) ----------------------

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    public new Color BackColor { get => base.BackColor; set => base.BackColor = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new event EventHandler? BackColorChanged
    {
        add => base.BackColorChanged += value;
        remove => base.BackColorChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Image? BackgroundImage { get => base.BackgroundImage; set => base.BackgroundImage = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new event EventHandler? BackgroundImageChanged
    {
        add => base.BackgroundImageChanged += value;
        remove => base.BackgroundImageChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override ImageLayout BackgroundImageLayout { get => base.BackgroundImageLayout; set => base.BackgroundImageLayout = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
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
    public new ContextMenuStrip? ContextMenuStrip { get => base.ContextMenuStrip; set => base.ContextMenuStrip = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ContextMenuStripChanged
    {
        add => base.ContextMenuStripChanged += value;
        remove => base.ContextMenuStripChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Cursor Cursor { get => base.Cursor; set => base.Cursor = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new event EventHandler? CursorChanged
    {
        add => base.CursorChanged += value;
        remove => base.CursorChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Color ForeColor { get => base.ForeColor; set => base.ForeColor = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new event EventHandler? ForeColorChanged
    {
        add => base.ForeColorChanged += value;
        remove => base.ForeColorChanged -= value;
    }

    /// <summary>The five panels, and nothing else: other controls go into <see cref="ContentPanel"/>.</summary>
    private sealed class ToolStripContainerTypedControlCollection : ControlCollection
    {
        private readonly ToolStripContainer _owner;
        private bool _adding;

        public ToolStripContainerTypedControlCollection(ToolStripContainer owner) : base(owner) => _owner = owner;

        internal void AddInternal(Control control)
        {
            _adding = true;
            try { base.Add(control); }
            finally { _adding = false; }
        }

        public override void Add(Control? value)
        {
            if (value == null) return;
            if (!_adding) throw new NotSupportedException("Adding controls directly to the ToolStripContainer is not supported.  Use the 'ContentPanel.Controls' property instead.");
            base.Add(value);
        }

        public override void Remove(Control? value)
        {
            if (value is ToolStripPanel or ToolStripContentPanel && !value.Disposing && !_owner.Disposing && !_owner.DesignMode && !_owner.IsDisposed)
                throw new NotSupportedException("The control collection is read-only.");
            base.Remove(value);
        }

        public override void SetChildIndex(Control child, int newIndex)
        {
            if (child is ToolStripPanel or ToolStripContentPanel) return;
            base.SetChildIndex(child, newIndex);
        }
    }
}
