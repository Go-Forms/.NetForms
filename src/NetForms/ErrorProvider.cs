using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace System.Windows.Forms;

public enum ErrorBlinkStyle
{
    BlinkIfDifferentError = 0,
    AlwaysBlink = 1,
    NeverBlink = 2,
}

public enum ErrorIconAlignment
{
    TopLeft = 0,
    TopRight = 1,
    MiddleLeft = 2,
    MiddleRight = 3,
    BottomLeft = 4,
    BottomRight = 5,
}

/// <summary>
/// Shows an error icon next to a control, with the error text as its tool tip. An extender provider, as in
/// WinForms: "Error on errorProvider1" is a property it gives every control.
/// </summary>
/// <remarks>
/// Semantics follow dotnet/winforms (MIT) <c>ErrorProvider</c>: icon placement, blinking, data-bound errors
/// from <see cref="IDataErrorInfo"/>. WinForms draws the icons in a child window on top of the control's
/// siblings; here each icon is an adornment of the parent (<see cref="Control.AddAdornment"/>), painted above
/// the children and hit before them, and not part of <see cref="Control.Controls"/>.
/// </remarks>
[ProvideProperty("IconPadding", typeof(Control))]
[ProvideProperty("IconAlignment", typeof(Control))]
[ProvideProperty("Error", typeof(Control))]
[ToolboxItemFilter("System.Windows.Forms")]
[ComplexBindingProperties(nameof(DataSource), nameof(DataMember))]
[Description("Provides a user interface to indicate to the user that a control on a form has an error associated with it.")]
public class ErrorProvider : Component, IExtenderProvider, ISupportInitialize
{
    private const int DefaultBlinkRate = 250;
    private const ErrorBlinkStyle DefaultBlinkStyle = ErrorBlinkStyle.BlinkIfDifferentError;
    private const ErrorIconAlignment DefaultIconAlignment = ErrorIconAlignment.MiddleRight;

    /// <summary>The small-icon size every error icon is drawn at (ScaleSmallIconToDpi at 96 DPI).</summary>
    private static readonly Size s_iconSize = new(16, 16);

    private static Icon? s_defaultIcon;

    private readonly Dictionary<Control, ControlItem> _items = new();
    private Icon? _icon;
    private Icon? _scaledIcon;
    private int _blinkRate = DefaultBlinkRate;
    private ErrorBlinkStyle _blinkStyle = DefaultBlinkStyle;
    private bool _showIcon = true;
    private bool _rightToLeft;
    private int _errorCount;
    private Timer? _timer;
    private ToolTip? _tip;

    private ContainerControl? _parentControl;
    private object? _dataSource;
    private string? _dataMember;
    private bool _initializing;
    private bool _setErrorManagerOnEndInit;
    private bool _inSetErrorManager;
    private BindingManagerBase? _errorManager;

    private EventHandler? _onRightToLeftChanged;

    public ErrorProvider()
    {
    }

    public ErrorProvider(ContainerControl parentControl)
        : this()
    {
        ArgumentNullException.ThrowIfNull(parentControl);
        _parentControl = parentControl;
    }

    public ErrorProvider(IContainer container)
        : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    public override ISite? Site
    {
        set
        {
            base.Site = value;
            if (value?.GetService(typeof(IDesignerHost)) is IDesignerHost { RootComponent: ContainerControl root })
            {
                ContainerControl = root;
            }
        }
    }

    [Category("Behavior")]
    [DefaultValue(DefaultBlinkStyle)]
    [Description("Controls whether the error icon blinks when an error is set.")]
    public ErrorBlinkStyle BlinkStyle
    {
        get => _blinkRate == 0 ? ErrorBlinkStyle.NeverBlink : _blinkStyle;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ErrorBlinkStyle));
            if (_blinkRate == 0) value = ErrorBlinkStyle.NeverBlink;
            if (_blinkStyle == value) return;

            if (value == ErrorBlinkStyle.AlwaysBlink)
            {
                _showIcon = true;
                _blinkStyle = ErrorBlinkStyle.AlwaysBlink;
                StartBlinking();
            }
            else if (_blinkStyle == ErrorBlinkStyle.AlwaysBlink)
            {
                _blinkStyle = value;
                _timer?.Stop();
                UpdateWindows(timerCaused: false);
            }
            else
            {
                _blinkStyle = value;
            }
        }
    }

    [Category("Behavior")]
    [DefaultValue(DefaultBlinkRate)]
    [Description("The rate in milliseconds at which the error icon blinks.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    public int BlinkRate
    {
        get => _blinkRate;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "BlinkRate must be zero or greater. Negative values are not permitted.");
            _blinkRate = value;
            if (_blinkRate == 0) BlinkStyle = ErrorBlinkStyle.NeverBlink;
        }
    }

    [DefaultValue(null)]
    [Category("Data")]
    [Description("The parent control, usually the form, that contains the data-bound controls on which the ErrorProvider can display error icons.")]
    public ContainerControl? ContainerControl
    {
        get => _parentControl;
        set
        {
            if (_parentControl == value) return;
            if (_parentControl != null) _parentControl.BindingContextChanged -= ParentControl_BindingContextChanged;
            _parentControl = value;
            if (_parentControl != null) _parentControl.BindingContextChanged += ParentControl_BindingContextChanged;
            SetErrorManager(DataSource, DataMember, force: true);
        }
    }

    public bool HasErrors => _errorCount > 0;

    [Category("Appearance")]
    [Localizable(true)]
    [DefaultValue(false)]
    [Description("Indicates whether the component should draw right-to-left for RTL languages.")]
    public virtual bool RightToLeft
    {
        get => _rightToLeft;
        set
        {
            if (value == _rightToLeft) return;
            _rightToLeft = value;
            OnRightToLeftChanged(EventArgs.Empty);
        }
    }

    [Category("Property Changed")]
    [Description("Occurs when the value of the RightToLeft property changes.")]
    public event EventHandler? RightToLeftChanged
    {
        add => _onRightToLeftChanged += value;
        remove => _onRightToLeftChanged -= value;
    }

    [Category("Data")]
    [Localizable(false)]
    [Bindable(true)]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    [TypeConverter(typeof(StringConverter))]
    public object? Tag { get; set; }

    [DefaultValue(null)]
    [Category("Data")]
    [AttributeProvider(typeof(IListSource))]
    [Description("Indicates the source of data to bind errors against.")]
    public object? DataSource
    {
        get => _dataSource;
        set => SetErrorManager(value, DataMember, force: false);
    }

    internal bool ShouldSerializeDataSource() => _dataSource is not null;

    [DefaultValue(null)]
    [Category("Data")]
    [Description("Indicates the sub-list of data from the DataSource to bind errors against.")]
    public string? DataMember
    {
        get => _dataMember;
        set => SetErrorManager(DataSource, value ?? string.Empty, force: false);
    }

    internal bool ShouldSerializeDataMember() => !string.IsNullOrEmpty(_dataMember);

    [Localizable(true)]
    [Category("Appearance")]
    [Description("The icon used to indicate an error.")]
    public Icon Icon
    {
        get => _icon ??= DefaultIcon;
        set
        {
            _icon = value ?? throw new ArgumentNullException(nameof(value));
            _scaledIcon = null;
            UpdateWindows(timerCaused: false);
        }
    }

    internal bool ShouldSerializeIcon() => _icon is not null && _icon != DefaultIcon;

    internal void ResetIcon() => Icon = DefaultIcon;

    /// <summary>The icon as drawn: scaled to the small-icon size, like WinForms' IconRegion.</summary>
    internal Icon DrawnIcon => _scaledIcon ??= Icon.Size == s_iconSize ? Icon : new Icon(Icon, s_iconSize);

    /// <summary>The ErrorProvider resource icon: a red disc with a white exclamation mark.</summary>
    private static Icon DefaultIcon => s_defaultIcon ??= DrawDefaultIcon();

    private static Icon DrawDefaultIcon()
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var red = new SolidBrush(Color.FromArgb(0xE0, 0x1B, 0x24))) g.FillEllipse(red, 0.5f, 0.5f, 15f, 15f);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            using var white = new SolidBrush(Color.White);
            g.FillRectangle(white, 7, 3, 2, 7);
            g.FillRectangle(white, 7, 11, 2, 2);
        }
        return new Icon(bmp);
    }

    void ISupportInitialize.BeginInit() => _initializing = true;

    void ISupportInitialize.EndInit()
    {
        if (DataSource is ISupportInitializeNotification { IsInitialized: false } notifying)
        {
            notifying.Initialized += DataSource_Initialized;
        }
        else
        {
            EndInitCore();
        }
    }

    private void DataSource_Initialized(object? sender, EventArgs e)
    {
        if (DataSource is ISupportInitializeNotification notifying) notifying.Initialized -= DataSource_Initialized;
        EndInitCore();
    }

    private void EndInitCore()
    {
        _initializing = false;
        if (_setErrorManagerOnEndInit)
        {
            _setErrorManagerOnEndInit = false;
            SetErrorManager(DataSource, DataMember, force: true);
        }
    }

    public bool CanExtend(object? extendee) => extendee is Control and not Form;

    [DefaultValue("")]
    [Localizable(true)]
    [Category("Appearance")]
    [Description("The error description for a control.")]
    public string GetError(Control control) => EnsureControlItem(control).Error;

    [DefaultValue(DefaultIconAlignment)]
    [Localizable(true)]
    [Category("Appearance")]
    [Description("The location of the error icon relative to the control.")]
    public ErrorIconAlignment GetIconAlignment(Control control) => EnsureControlItem(control).IconAlignment;

    [DefaultValue(0)]
    [Localizable(true)]
    [Category("Appearance")]
    [Description("The number of pixels to leave between the error icon and the control.")]
    public int GetIconPadding(Control control) => EnsureControlItem(control).IconPadding;

    public void SetError(Control control, string? value)
    {
        var item = EnsureControlItem(control);
        bool countChanged = item.Error != value && string.IsNullOrEmpty(item.Error) != string.IsNullOrEmpty(value);
        item.Error = value;
        if (countChanged) _errorCount += string.IsNullOrEmpty(value) ? -1 : 1;
    }

    public void SetIconAlignment(Control control, ErrorIconAlignment value) => EnsureControlItem(control).IconAlignment = value;

    public void SetIconPadding(Control control, int padding) => EnsureControlItem(control).IconPadding = padding;

    /// <summary>Removes every error (and every icon) this provider shows.</summary>
    public void Clear()
    {
        foreach (var item in _items.Values) item.Dispose();
        _items.Clear();
        _errorCount = 0;
        _timer?.Stop();
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnRightToLeftChanged(EventArgs e)
    {
        UpdateWindows(timerCaused: false);
        _onRightToLeftChanged?.Invoke(this, e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Clear();
            UnwireEvents(_errorManager);
            _timer?.Dispose();
            _timer = null;
            _tip?.Dispose();
            _tip = null;
        }
        base.Dispose(disposing);
    }

    private ControlItem EnsureControlItem(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!_items.TryGetValue(control, out var item))
        {
            item = new ControlItem(this, control);
            _items[control] = item;
        }
        return item;
    }

    // --- data-bound errors -------------------------------------------------------------

    public void BindToDataAndErrors(object? newDataSource, string? newDataMember) => SetErrorManager(newDataSource, newDataMember, force: false);

    /// <summary>Re-reads the errors of the current item of the bound data source.</summary>
    public void UpdateBinding() => ErrorManager_CurrentChanged(_errorManager, EventArgs.Empty);

    /// <summary>
    /// The errors come from the current item of <c>ContainerControl.BindingContext[DataSource, DataMember]</c> and
    /// are shown on the controls whose bindings that manager holds (ported from WinForms' ErrorProvider).
    /// </summary>
    private void SetErrorManager(object? newDataSource, string? newDataMember, bool force)
    {
        if (_inSetErrorManager) return;
        _inSetErrorManager = true;
        try
        {
            if (DataSource == newDataSource && DataMember == newDataMember && !force) return;
            _dataSource = newDataSource;
            _dataMember = newDataMember;

            if (_initializing)
            {
                _setErrorManagerOnEndInit = true;
                return;
            }

            UnwireEvents(_errorManager);
            _errorManager = _parentControl is { BindingContext: { } context } && _dataSource != null
                ? context[_dataSource, _dataMember]
                : null;
            WireEvents(_errorManager);
            if (_errorManager != null) UpdateBinding();
        }
        finally
        {
            _inSetErrorManager = false;
        }
    }

    private void WireEvents(BindingManagerBase? listManager)
    {
        if (listManager == null) return;
        listManager.CurrentChanged += ErrorManager_CurrentChanged;
        listManager.BindingComplete += ErrorManager_BindingComplete;
        if (listManager is CurrencyManager currencyManager)
        {
            currencyManager.ItemChanged += ErrorManager_ItemChanged;
            currencyManager.Bindings.CollectionChanged += ErrorManager_BindingsChanged;
        }
    }

    private void UnwireEvents(BindingManagerBase? listManager)
    {
        if (listManager == null) return;
        listManager.CurrentChanged -= ErrorManager_CurrentChanged;
        listManager.BindingComplete -= ErrorManager_BindingComplete;
        if (listManager is CurrencyManager currencyManager)
        {
            currencyManager.ItemChanged -= ErrorManager_ItemChanged;
            currencyManager.Bindings.CollectionChanged -= ErrorManager_BindingsChanged;
        }
    }

    private void ErrorManager_BindingComplete(object? sender, BindingCompleteEventArgs e)
    {
        if (e.Binding?.Control is { } control) SetError(control, e.ErrorText ?? string.Empty);
    }

    private void ErrorManager_BindingsChanged(object? sender, CollectionChangeEventArgs e) => ErrorManager_CurrentChanged(_errorManager, e);

    private void ParentControl_BindingContextChanged(object? sender, EventArgs e) => SetErrorManager(DataSource, DataMember, force: true);

    private void ErrorManager_ItemChanged(object? sender, ItemChangedEventArgs e)
    {
        if (_errorManager == null) return;
        var bindings = _errorManager.Bindings;
        if (e.Index == -1 && _errorManager.Count == 0)
        {
            // The list became empty: no errors.
            for (int j = 0; j < bindings.Count; j++)
            {
                if (bindings[j].Control is { } control) SetError(control, string.Empty);
            }
        }
        else
        {
            ErrorManager_CurrentChanged(sender, e);
        }
    }

    private void ErrorManager_CurrentChanged(object? sender, EventArgs e)
    {
        if (_errorManager == null || _errorManager.Count == 0) return;
        if (_errorManager.Current is not IDataErrorInfo dataErrorInfo) return;

        foreach (var item in _items.Values) item.BlinkPhase = 0;

        // Several bindings on one control: their errors are joined line by line.
        var bindings = _errorManager.Bindings;
        var controlError = new Dictionary<Control, string>();
        for (int j = 0; j < bindings.Count; j++)
        {
            if (bindings[j].Control is not { } control) continue;
            string error = dataErrorInfo[bindings[j].BindingMemberInfo.BindingField] ?? string.Empty;
            controlError.TryGetValue(control, out var output);
            controlError[control] = string.IsNullOrEmpty(output) ? error : output + "\r\n" + error;
        }
        foreach (var entry in controlError) SetError(entry.Key, entry.Value);
    }

    // --- icons and blinking ---------------------------------------------------------------

    private ToolTip Tip => _tip ??= new ToolTip { ShowAlways = true, InitialDelay = 1 };

    /// <summary>Starts the blink timer (it stops by itself once every blinking icon has blinked out).</summary>
    private void StartBlinking()
    {
        if (_timer == null)
        {
            _timer = new Timer();
            _timer.Tick += OnTimer;
        }
        _timer.Interval = Math.Max(1, _blinkRate);
        _timer.Start();
        UpdateWindows(timerCaused: false);
    }

    private void OnTimer(object? sender, EventArgs e)
    {
        int blinkPhase = 0;
        foreach (var item in _items.Values) blinkPhase += item.BlinkPhase;
        if (blinkPhase == 0 && BlinkStyle != ErrorBlinkStyle.AlwaysBlink) _timer!.Stop();
        UpdateWindows(timerCaused: true);
    }

    private void UpdateWindows(bool timerCaused)
    {
        foreach (var item in _items.Values) item.UpdateWindow(timerCaused);
        if (timerCaused) _showIcon = !_showIcon;
    }

    /// <summary>
    /// The error state of one control. WinForms groups the icons of all controls with one parent into one
    /// window; here every icon is its own adornment, which is the same on screen. One difference follows
    /// from it: WinForms blinks the odd-numbered icons of a window in counter-phase with the even ones,
    /// here all blinking icons are in phase.
    /// </summary>
    private sealed class ControlItem
    {
        private const int StartingBlinkPhase = 10; // five blinks

        private readonly ErrorProvider _provider;
        private readonly Control _control;
        private string _error = string.Empty;
        private int _iconPadding;
        private ErrorIconAlignment _iconAlignment = DefaultIconAlignment;
        private ErrorWindow? _window;

        public ControlItem(ErrorProvider provider, Control control)
        {
            _provider = provider;
            _control = control;
            _control.HandleCreated += OnCreateHandle;
            _control.HandleDestroyed += OnDestroyHandle;
            _control.LocationChanged += OnBoundsChanged;
            _control.SizeChanged += OnBoundsChanged;
            _control.VisibleChanged += OnParentVisibleChanged;
            _control.ParentChanged += OnParentVisibleChanged;
        }

        public int BlinkPhase { get; set; }

        /// <summary>True while the pointer is on the icon: the tip is up and the icon does not blink away under it.</summary>
        public bool ToolTipShown { get; set; }

        [AllowNull]
        public string Error
        {
            get => _error;
            set
            {
                value ??= string.Empty;
                if (_error == value && _provider.BlinkStyle != ErrorBlinkStyle.AlwaysBlink) return;
                bool adding = _error.Length == 0;
                _error = value;
                if (value.Length == 0)
                {
                    RemoveFromWindow();
                }
                else if (adding)
                {
                    AddToWindow();
                }
                else if (_provider.BlinkStyle != ErrorBlinkStyle.NeverBlink)
                {
                    StartBlinking();
                }
                else
                {
                    UpdateWindow(timerCaused: false);
                }
            }
        }

        public int IconPadding
        {
            get => _iconPadding;
            set
            {
                if (_iconPadding == value) return;
                _iconPadding = value;
                UpdateWindow(timerCaused: false);
            }
        }

        public ErrorIconAlignment IconAlignment
        {
            get => _iconAlignment;
            set
            {
                if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ErrorIconAlignment));
                if (_iconAlignment == value) return;
                _iconAlignment = value;
                UpdateWindow(timerCaused: false);
            }
        }

        public void Dispose()
        {
            _control.HandleCreated -= OnCreateHandle;
            _control.HandleDestroyed -= OnDestroyHandle;
            _control.LocationChanged -= OnBoundsChanged;
            _control.SizeChanged -= OnBoundsChanged;
            _control.VisibleChanged -= OnParentVisibleChanged;
            _control.ParentChanged -= OnParentVisibleChanged;
            RemoveFromWindow();
            _error = string.Empty;
        }

        private ErrorIconAlignment RtlTranslate(ErrorIconAlignment align) => !_provider.RightToLeft ? align : align switch
        {
            ErrorIconAlignment.TopLeft => ErrorIconAlignment.TopRight,
            ErrorIconAlignment.MiddleLeft => ErrorIconAlignment.MiddleRight,
            ErrorIconAlignment.BottomLeft => ErrorIconAlignment.BottomRight,
            ErrorIconAlignment.TopRight => ErrorIconAlignment.TopLeft,
            ErrorIconAlignment.MiddleRight => ErrorIconAlignment.MiddleLeft,
            _ => ErrorIconAlignment.BottomLeft,
        };

        /// <summary>The icon's bounds in the parent's client coordinates.</summary>
        public Rectangle GetIconBounds(Size size)
        {
            int x = RtlTranslate(_iconAlignment) switch
            {
                ErrorIconAlignment.TopLeft or ErrorIconAlignment.MiddleLeft or ErrorIconAlignment.BottomLeft => _control.Left - size.Width - _iconPadding,
                _ => _control.Right + _iconPadding,
            };
            int y = _iconAlignment switch
            {
                ErrorIconAlignment.TopLeft or ErrorIconAlignment.TopRight => _control.Top,
                ErrorIconAlignment.MiddleLeft or ErrorIconAlignment.MiddleRight => _control.Top + (_control.Height - size.Height) / 2,
                _ => _control.Bottom - size.Height,
            };
            return new Rectangle(x, y, size.Width, size.Height);
        }

        public void UpdateWindow(bool timerCaused)
        {
            if (_window == null) return;
            _window.SetBoundsFromLayout(GetIconBounds(_provider.DrawnIcon.Size));

            bool showIcon = true;
            if (!ToolTipShown)
            {
                showIcon = _provider.BlinkStyle switch
                {
                    ErrorBlinkStyle.BlinkIfDifferentError => BlinkPhase == 0 || (BlinkPhase & 1) == 0,
                    ErrorBlinkStyle.AlwaysBlink => _provider._showIcon,
                    _ => true,
                };
            }
            _window.IconShown = showIcon;
            _provider.Tip.SetToolTip(_window, _error);
            if (timerCaused && BlinkPhase > 0) BlinkPhase--;
        }

        private void StartBlinking()
        {
            if (_window == null) return;
            BlinkPhase = StartingBlinkPhase;
            _provider.StartBlinking();
        }

        private void AddToWindow()
        {
            if (_window != null || !_control.Created || !_control.Visible || _control.Parent is not { } parent || _error.Length == 0) return;
            _window = new ErrorWindow(this);
            parent.AddAdornment(_window);
            UpdateWindow(timerCaused: false);
            if (_provider.BlinkStyle != ErrorBlinkStyle.NeverBlink) StartBlinking();
        }

        private void RemoveFromWindow()
        {
            if (_window == null) return;
            _provider._tip?.SetToolTip(_window, null);
            _window.Parent?.RemoveAdornment(_window);
            _window.Dispose();
            _window = null;
            ToolTipShown = false;
        }

        private void OnBoundsChanged(object? sender, EventArgs e) => UpdateWindow(timerCaused: false);

        private void OnParentVisibleChanged(object? sender, EventArgs e)
        {
            BlinkPhase = 0;
            RemoveFromWindow();
            AddToWindow();
        }

        private void OnCreateHandle(object? sender, EventArgs e) => AddToWindow();

        private void OnDestroyHandle(object? sender, EventArgs e) => RemoveFromWindow();

        public void SetToolTipShown(bool shown)
        {
            ToolTipShown = shown;
            UpdateWindow(timerCaused: false);
        }

        public ErrorProvider Provider => _provider;
    }

    /// <summary>The icon of one control: an adornment of the control's parent.</summary>
    private sealed class ErrorWindow : Control
    {
        private readonly ControlItem _item;
        private bool _iconShown = true;

        public ErrorWindow(ControlItem item)
        {
            _item = item;
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public bool IconShown
        {
            get => _iconShown;
            set
            {
                if (_iconShown == value) return;
                _iconShown = value;
                Invalidate();
            }
        }

        /// <summary>Only the icon's opaque pixels are the window (WinForms sets a window region from the icon mask).</summary>
        internal override bool AdornmentContains(Point p)
        {
            var bitmap = _item.Provider.DrawnIcon.Bitmap;
            return p.X >= 0 && p.Y >= 0 && p.X < bitmap.Width && p.Y < bitmap.Height && bitmap.GetPixel(p.X, p.Y).A != 0;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Transparent: only the icon is painted.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_iconShown) e.Graphics.DrawIcon(_item.Provider.DrawnIcon, 0, 0);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _item.SetToolTipShown(true);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _item.SetToolTipShown(false);
        }
    }
}
