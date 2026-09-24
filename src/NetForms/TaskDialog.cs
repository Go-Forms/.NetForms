using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace System.Windows.Forms;

/// <summary>
/// The task dialog (comctl32's TaskDialogIndirect in WinForms): heading, text, icon, buttons, command links,
/// radio buttons, a verification check box, an expander, a footnote and a progress bar, all of which can
/// change while it is shown.
/// </summary>
/// <remarks>
/// Semantics follow dotnet/winforms (MIT): which button is the result, when the dialog may be cancelled, what a
/// shown page lets you change, navigation between pages. The dialog itself is NetForms' own form in the style of
/// <see cref="MessageBox"/> (decision 120): comctl32 has no counterpart on Linux, and one look on both systems is
/// the point of NetForms.
/// </remarks>
public class TaskDialog : IWin32Window
{
    private TaskDialogPage _page;
    private TaskDialogForm? _form;
    private TaskDialogButton? _result;

    private TaskDialog(TaskDialogPage page) => _page = page;

    /// <summary>There is no native window behind a NetForms task dialog.</summary>
    public IntPtr Handle => IntPtr.Zero;

    public static TaskDialogButton ShowDialog(TaskDialogPage page, TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner) =>
        ShowDialogCore(Form.ActiveForm, page, startupLocation);

    public static TaskDialogButton ShowDialog(IWin32Window owner, TaskDialogPage page, TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return ShowDialogCore(owner, page, startupLocation);
    }

    /// <summary>A native owner handle: NetForms has none, so the active form owns the dialog.</summary>
    public static TaskDialogButton ShowDialog(IntPtr hwndOwner, TaskDialogPage page, TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner) =>
        ShowDialogCore(Form.ActiveForm, page, startupLocation);

    public static Task<TaskDialogButton> ShowDialogAsync(TaskDialogPage page, TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner) =>
        ShowDialogAsyncCore(Form.ActiveForm, page, startupLocation);

    public static Task<TaskDialogButton> ShowDialogAsync(IWin32Window owner, TaskDialogPage page, TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return ShowDialogAsyncCore(owner, page, startupLocation);
    }

    public static Task<TaskDialogButton> ShowDialogAsync(IntPtr hwndOwner, TaskDialogPage page, TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner) =>
        ShowDialogAsyncCore(Form.ActiveForm, page, startupLocation);

    /// <summary>Closes the shown dialog with <see cref="TaskDialogButton.Cancel"/> as the result, without raising a Click.</summary>
    public void Close()
    {
        if (_form == null) return;
        _result ??= BoundButton(TaskDialogResult.Cancel) ?? Placeholder(TaskDialogResult.Cancel);
        _form.CloseFromDialog();
    }

    private static Task<TaskDialogButton> ShowDialogAsyncCore(IWin32Window? owner, TaskDialogPage page, TaskDialogStartupLocation startupLocation)
    {
        ArgumentNullException.ThrowIfNull(page);
        // As in WinForms: the dialog is shown from the message loop, the task completes when it closes.
        var tcs = new TaskCompletionSource<TaskDialogButton>();
        Application.Post(() =>
        {
            try
            {
                tcs.SetResult(ShowDialogCore(owner, page, startupLocation));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private static TaskDialogButton ShowDialogCore(IWin32Window? owner, TaskDialogPage page, TaskDialogStartupLocation startupLocation)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Validate();
        var dialog = new TaskDialog(page);
        page.Bind(dialog);
        var ownerForm = owner as Form ?? (owner as Control)?.FindForm();
        using var form = new TaskDialogForm(dialog, ownerForm == null);
        dialog._form = form;
        try
        {
            form.StartPosition = startupLocation == TaskDialogStartupLocation.CenterOwner && ownerForm != null
                ? FormStartPosition.CenterParent
                : FormStartPosition.CenterScreen;
            form.Build(page);
            form.ShowDialog(ownerForm);
        }
        finally
        {
            dialog._form = null;
            dialog._page.Unbind();
        }
        // Closed some other way (the owner went away): Cancel, as TaskDialogIndirect returns IDCANCEL.
        return dialog._result ?? Placeholder(TaskDialogResult.Cancel);
    }

    /// <summary>A result that is no button of the page (Cancel from the close box, OK of a page without buttons).</summary>
    private static TaskDialogButton Placeholder(TaskDialogResult result) => new(result) { Visible = false };

    internal TaskDialogPage Page => _page;

    private TaskDialogButton? BoundButton(TaskDialogResult result) =>
        _page.Buttons.FirstOrDefault(b => b.IsCreatable && b.IsStandardButton && b.StandardButtonResult == result);

    /// <summary>Escape, Alt+F4 and the close box work when the page allows cancelling or has a Cancel button.</summary>
    internal bool CanCancel => _page.AllowCancel || BoundButton(TaskDialogResult.Cancel) != null;

    /// <summary>A button was pressed (by the user or PerformClick).</summary>
    internal void ClickButton(TaskDialogButton button)
    {
        if (_form == null || _result != null) return;
        var page = _page;
        bool close = button.HandleButtonClicked();
        if (button.IsStandardButton && button.StandardButtonResult == TaskDialogResult.Help)
        {
            // Help does not close the dialog; it asks the page for help (TDN_HELP).
            page.OnHelpRequest(EventArgs.Empty);
            return;
        }
        // A Click handler that navigated to another page keeps the dialog open.
        if (!close || !ReferenceEquals(page, _page) || _form == null) return;
        _result = button;
        _form.CloseFromDialog();
    }

    /// <summary>The user asked to cancel (Escape, the close box): the Cancel button if there is one, else a Cancel result.</summary>
    internal void Cancel()
    {
        if (!CanCancel) return;
        if (BoundButton(TaskDialogResult.Cancel) is { } cancel)
        {
            ClickButton(cancel);
            return;
        }
        _result = Placeholder(TaskDialogResult.Cancel);
        _form?.CloseFromDialog();
    }

    /// <summary>The implicit OK of a page without visible buttons.</summary>
    internal void ClickImplicitOk()
    {
        _result = Placeholder(TaskDialogResult.OK);
        _form?.CloseFromDialog();
    }

    internal TaskDialogButton? Result => _result;

    internal void Navigate(TaskDialogPage page)
    {
        if (_form == null) throw new InvalidOperationException("Cannot navigate the dialog when it has already closed.");
        page.Validate();
        var old = _page;
        old.OnDestroyed(EventArgs.Empty);
        old.Unbind();
        _page = page;
        page.Bind(this);
        _form.Build(page);
        page.OnCreated(EventArgs.Empty);
    }

    /// <summary>Something shown changed (text, icon, progress, a state): lay the dialog out again.</summary>
    internal void OnPageChanged() => _form?.UpdateContent();

    // --- the window ---------------------------------------------------------------------------

    private sealed class TaskDialogForm : Form
    {
        private const int Margin_ = 12;
        private const int IconSize = 32;
        private const int IconGap = 10;
        private const int BlockGap = 10;
        private const int ButtonHeight = 26;
        private const int MinButtonWidth = 88;
        private const int ButtonGap = 8;
        private const int FixedClientWidth = 400;
        private const int MaxClientWidth = 640;

        private static readonly Color HeadingColor = Color.FromArgb(0x00, 0x33, 0x99);
        private static readonly Color FooterColor = Color.FromArgb(0xF0, 0xF0, 0xF0);
        private static readonly Color FooterLine = Color.FromArgb(0xDF, 0xDF, 0xDF);

        private readonly TaskDialog _dialog;
        private TaskDialogPage? _page;
        private bool _closingFromDialog;

        private Font? _headingFont;
        private Font? _commandLinkFont;
        private readonly Label _heading = new() { AutoSize = false, UseMnemonic = false, BackColor = Color.Transparent };
        private readonly LinkLabel _text = NewLinkLabel();
        private readonly LinkLabel _expandedText = NewLinkLabel();
        private readonly ProgressBar _progress = new();
        private readonly List<(RadioButton Control, TaskDialogRadioButton Radio)> _radios = new();
        private readonly List<(ButtonBase Control, TaskDialogButton Button)> _buttons = new();
        private readonly Panel _footer = new() { BackColor = FooterColor };
        private readonly ExpandoButton _expando = new();
        private readonly CheckBox _verification = new() { AutoSize = true, UseMnemonic = false };
        private readonly Panel _footnotePanel = new() { BackColor = FooterColor };
        private readonly LinkLabel _footnote = NewLinkLabel();
        private int _footnoteIconTop;
        private Rectangle _iconBounds;
        private Rectangle _barBounds;

        public TaskDialogForm(TaskDialog dialog, bool showInTaskbar)
        {
            _dialog = dialog;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ShowInTaskbar = showInTaskbar;
            BackColor = SystemColors.Window;
            KeyPreview = false;

            _text.LinkClicked += OnLinkClicked;
            _expandedText.LinkClicked += OnLinkClicked;
            _footnote.LinkClicked += OnLinkClicked;
            _expando.Click += (_, _) =>
            {
                _page?.Expander?.Toggle();
                UpdateContent();
            };
            _verification.CheckedChanged += (_, _) => _page?.Verification?.SetChecked(_verification.Checked);
            _footnotePanel.Paint += PaintFootnoteIcon;
        }

        private static LinkLabel NewLinkLabel() => new()
        {
            AutoSize = false,
            UseMnemonic = false,
            BackColor = Color.Transparent,
            LinkBehavior = LinkBehavior.HoverUnderline,
        };

        private Font HeadingFont => _headingFont ??= new Font(Font.FontFamily, 12f);

        private Font CommandLinkFont => _commandLinkFont ??= new Font(Font.FontFamily, 11f);

        /// <summary>Creates the controls of a page (a new one after navigation) and lays them out.</summary>
        public void Build(TaskDialogPage page)
        {
            _page = page;
            SuspendLayout();
            foreach (var (control, _) in _radios) control.Dispose();
            foreach (var (control, _) in _buttons) control.Dispose();
            _radios.Clear();
            _buttons.Clear();
            Controls.Clear();
            _footer.Controls.Clear();
            _footnotePanel.Controls.Clear();

            Controls.Add(_heading);
            Controls.Add(_text);
            Controls.Add(_expandedText);
            Controls.Add(_progress);
            foreach (var radio in page.RadioButtons)
            {
                var control = new RadioButton { AutoSize = true, Text = radio.Text, UseMnemonic = false };
                control.CheckedChanged += (_, _) =>
                {
                    if (control.Checked && !radio.Checked) page.CheckRadioButton(radio);
                };
                _radios.Add((control, radio));
                Controls.Add(control);
            }

            // Command links sit in the content; the other buttons in the footer, custom ones first, then the
            // standard ones in the system's order. A page with no visible button gets an OK button.
            var visible = page.Buttons.Where(b => b.IsCreatable).ToList();
            foreach (var link in visible.OfType<TaskDialogCommandLinkButton>())
            {
                var control = new CommandLinkControl(link.Text ?? string.Empty, link.DescriptionText, CommandLinkFont);
                control.Click += (_, _) => _dialog.ClickButton(link);
                _buttons.Add((control, link));
                Controls.Add(control);
            }
            var footerButtons = visible.Where(b => b is not TaskDialogCommandLinkButton && !b.IsStandardButton)
                .Concat(visible.Where(b => b.IsStandardButton).OrderBy(b => StandardOrder(b.StandardButtonResult)))
                .ToList();
            if (visible.Count == 0)
            {
                var ok = new Button { Text = SystemStrings.Get("OK"), UseVisualStyleBackColor = true };
                ok.Click += (_, _) => _dialog.ClickImplicitOk();
                _footer.Controls.Add(ok);
                AcceptButton = ok;
            }
            foreach (var b in footerButtons)
            {
                var control = new Button { Text = b.DisplayText, UseVisualStyleBackColor = true };
                control.Click += (_, _) => _dialog.ClickButton(b);
                _buttons.Add((control, b));
                _footer.Controls.Add(control);
            }

            _footer.Controls.Add(_expando);
            _footer.Controls.Add(_verification);
            _footnotePanel.Controls.Add(_footnote);
            Controls.Add(_footer);
            Controls.Add(_footnotePanel);

            if (visible.Count > 0)
            {
                var def = page.DefaultButton is { } d ? _buttons.FirstOrDefault(x => x.Button.Equals(d)).Control : null;
                def ??= _buttons.Count > 0 ? _buttons.OrderBy(x => x.Button is TaskDialogCommandLinkButton ? 0 : 1).First().Control : null;
                AcceptButton = def as IButtonControl;
                if (def != null) ActiveControl = def;
            }

            MinimizeBox = page.AllowMinimize;
            RightToLeft = page.RightToLeftLayout ? RightToLeft.Yes : RightToLeft.No;
            _verification.Checked = page.Verification?.Checked == true;
            foreach (var (control, radio) in _radios) control.Checked = radio.Checked;
            ResumeLayout(false);
            UpdateContent();
        }

        /// <summary>comctl32's order of the common buttons.</summary>
        private static int StandardOrder(TaskDialogResult result) => result switch
        {
            TaskDialogResult.OK => 0,
            TaskDialogResult.Yes => 1,
            TaskDialogResult.No => 2,
            TaskDialogResult.Abort => 3,
            TaskDialogResult.Retry => 4,
            TaskDialogResult.Ignore => 5,
            TaskDialogResult.TryAgain => 6,
            TaskDialogResult.Continue => 7,
            TaskDialogResult.Cancel => 8,
            TaskDialogResult.Close => 9,
            _ => 10,
        };

        /// <summary>Applies the page's current texts and states and lays everything out; the window follows the content.</summary>
        public void UpdateContent()
        {
            var page = _page;
            if (page == null) return;
            Text = page.Caption ?? Application.ProductName;

            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            var icon = page.Icon;
            var bar = icon?.BarColor;
            var image = icon?.GetImage();
            bool iconBeside = image != null && bar == null;

            var (text, textLinks) = ParseLinks(page.Text, page.EnableLinks);
            var expander = page.Expander is { IsCreated: true } e ? e : null;
            var (expanded, expandedLinks) = ParseLinks(expander?.Text, page.EnableLinks);
            var footnote = page.Footnote is { IsCreated: true } f ? f : null;
            var (note, noteLinks) = ParseLinks(footnote?.Text, page.EnableLinks);
            string heading = page.Heading ?? string.Empty;

            // Width: fixed, or (SizeToContent) what the longest line wants, within limits; never narrower than the footer.
            int footerNeed = FooterWidth(page);
            int clientWidth = FixedClientWidth;
            int textLeft = Margin_ + (iconBeside ? IconSize + IconGap : 0);
            if (page.SizeToContent)
            {
                int natural = new[] { Measure(heading, HeadingFont, 0).Width, Measure(text, Font, 0).Width }
                    .Concat(_radios.Select(r => r.Control.PreferredSize.Width))
                    .Concat(_buttons.Where(x => x.Control is CommandLinkControl).Select(x => x.Control.PreferredSize.Width))
                    .DefaultIfEmpty(0).Max();
                clientWidth = Math.Clamp(textLeft + natural + Margin_, FixedClientWidth, MaxClientWidth);
            }
            clientWidth = Math.Max(clientWidth, footerNeed);
            int textWidth = clientWidth - textLeft - Margin_;

            int y = Margin_;
            _barBounds = Rectangle.Empty;
            _heading.Font = HeadingFont;
            if (bar != null)
            {
                // The coloured header bar: shield and heading on the bar, across the whole width.
                int headingHeight = heading.Length > 0 ? Measure(heading, HeadingFont, clientWidth - 2 * Margin_ - IconSize - IconGap).Height : 0;
                int barHeight = Math.Max(IconSize, headingHeight) + 2 * 8;
                _barBounds = new Rectangle(0, 0, clientWidth, barHeight);
                _iconBounds = new Rectangle(Margin_, (barHeight - IconSize) / 2, IconSize, IconSize);
                _heading.ForeColor = icon!.BarTextColor;
                _heading.Visible = heading.Length > 0;
                _heading.Text = heading;
                _heading.Bounds = new Rectangle(Margin_ + IconSize + IconGap, (barHeight - headingHeight) / 2, clientWidth - 2 * Margin_ - IconSize - IconGap, headingHeight);
                y = barHeight + BlockGap;
            }
            else
            {
                _iconBounds = iconBeside ? new Rectangle(Margin_, Margin_, IconSize, IconSize) : Rectangle.Empty;
                _heading.ForeColor = HeadingColor;
                _heading.Visible = heading.Length > 0;
                _heading.Text = heading;
                if (heading.Length > 0)
                {
                    int h = Measure(heading, HeadingFont, textWidth).Height;
                    _heading.Bounds = new Rectangle(textLeft, y, textWidth, h);
                    y += h + BlockGap;
                }
            }

            y = PlaceText(_text, text, textLinks, textLeft, y, textWidth);
            bool expandedAfterText = expander != null && expander.Expanded && expander.Position == TaskDialogExpanderPosition.AfterText;
            _expandedText.Visible = false;
            if (expandedAfterText) y = PlaceText(_expandedText, expanded, expandedLinks, textLeft, y, textWidth);

            var progressModel = page.ProgressBar is { IsCreated: true } p ? p : null;
            _progress.Visible = progressModel != null;
            if (progressModel != null)
            {
                ApplyProgress(progressModel);
                _progress.Bounds = new Rectangle(textLeft, y, textWidth, 15);
                y += 15 + BlockGap;
            }

            foreach (var (control, radio) in _radios)
            {
                control.Enabled = radio.Enabled;
                if (control.Checked != radio.Checked) control.Checked = radio.Checked;
                control.Location = new Point(textLeft, y);
                y += control.PreferredSize.Height + 4;
            }
            if (_radios.Count > 0) y += BlockGap - 4;

            foreach (var (control, button) in _buttons)
            {
                control.Enabled = button.Enabled;
                if (control is CommandLinkControl link)
                {
                    link.ShowShield = button.ShowShieldIcon;
                    int h = link.GetPreferredSize(new Size(textWidth, 0)).Height;
                    link.Bounds = new Rectangle(textLeft, y, textWidth, h);
                    y += h + 4;
                }
                else
                {
                    control.Image = button.ShowShieldIcon ? ShieldImage : null;
                    control.TextImageRelation = TextImageRelation.ImageBeforeText;
                }
            }
            if (_buttons.Any(x => x.Control is CommandLinkControl)) y += BlockGap - 4;

            if (iconBeside) y = Math.Max(y, Margin_ + IconSize + BlockGap);
            int contentHeight = y - BlockGap + Margin_;

            // Footer: the expando button and the verification box on the left, buttons on the right.
            // No row at all when it would be empty (a page of command links only).
            bool footer = FooterButtons().Any() || expander != null || page.Verification is { IsCreated: true };
            int footerHeight = footer ? LayoutFooter(page, clientWidth, expander) : 0;
            _footer.Visible = footer;
            _footer.Bounds = new Rectangle(0, contentHeight, clientWidth, footerHeight);
            y = contentHeight + footerHeight;

            bool expandedAfterFootnote = expander != null && expander.Expanded && expander.Position == TaskDialogExpanderPosition.AfterFootnote;
            // (Visible reads the whole chain, and the window is not shown yet on the first layout.)
            bool footnotePanel = footnote != null || expandedAfterFootnote;
            _footnotePanel.Visible = footnotePanel;
            if (footnotePanel)
            {
                int py = 8;
                bool footnoteIcon = footnote?.Icon?.GetImage() != null;
                int noteLeft = Margin_ + (footnoteIcon ? 16 + 6 : 0);
                _footnote.Visible = footnote != null;
                if (footnote != null)
                {
                    _footnoteIconTop = py;
                    py = PlaceText(_footnote, note, noteLinks, noteLeft, py, clientWidth - noteLeft - Margin_) - BlockGap + 8;
                }
                if (expandedAfterFootnote)
                {
                    _footnotePanel.Controls.Add(_expandedText);
                    py = PlaceText(_expandedText, expanded, expandedLinks, Margin_, py, clientWidth - 2 * Margin_) - BlockGap + 8;
                }
                else if (_expandedText.Parent == _footnotePanel)
                {
                    Controls.Add(_expandedText);
                    if (expandedAfterText) PlaceText(_expandedText, expanded, expandedLinks, textLeft, _expandedText.Top, textWidth);
                }
                _footnotePanel.Bounds = new Rectangle(0, y, clientWidth, py);
                y += py;
            }
            ClientSize = new Size(clientWidth, y);
            Invalidate();
            _footnotePanel.Invalidate();
        }

        private static Image? s_shield;

        private static Image ShieldImage => s_shield ??= new Bitmap(SystemIcons.Shield.ToBitmap(), 16, 16);

        private int FooterWidth(TaskDialogPage page)
        {
            int buttons = FooterButtons().Sum(b => ButtonWidth(b) + ButtonGap);
            int left = page.Expander is { IsCreated: true } ? _expando.GetPreferredSize(Size.Empty).Width + 2 * ButtonGap : 0;
            return Margin_ + left + buttons - ButtonGap + Margin_;
        }

        private IEnumerable<Button> FooterButtons() => _footer.Controls.OfType<Button>();

        private int ButtonWidth(Button b) => Math.Max(MinButtonWidth, TextRenderer.MeasureText(b.Text, b.Font).Width + 16 + (b.Image != null ? 20 : 0));

        private int LayoutFooter(TaskDialogPage page, int width, TaskDialogExpander? expander)
        {
            var buttons = FooterButtons().ToList();
            int y = 10;
            int x = width - Margin_;
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                int w = ButtonWidth(buttons[i]);
                x -= w;
                buttons[i].Bounds = new Rectangle(x, y, w, ButtonHeight);
                x -= ButtonGap;
            }
            int rowBottom = buttons.Count > 0 ? y + ButtonHeight : y;

            _expando.Visible = expander != null;
            if (expander != null)
            {
                _expando.Text = expander.Expanded
                    ? expander.ExpandedButtonText ?? SystemStrings.Get("Hide details")
                    : expander.CollapsedButtonText ?? SystemStrings.Get("See details");
                _expando.Expanded = expander.Expanded;
                var size = _expando.GetPreferredSize(Size.Empty);
                _expando.Bounds = new Rectangle(Margin_, y + (ButtonHeight - size.Height) / 2, size.Width, size.Height);
            }

            var verification = page.Verification is { IsCreated: true } v ? v : null;
            _verification.Visible = verification != null;
            if (verification != null)
            {
                _verification.Text = verification.Text;
                if (_verification.Checked != verification.Checked) _verification.Checked = verification.Checked;
                var size = _verification.PreferredSize;
                // Beside the buttons when the row has room, else on a row of its own below.
                int buttonsLeft = buttons.Count > 0 ? buttons[0].Left : width - Margin_;
                bool fits = expander == null && Margin_ + size.Width + ButtonGap <= buttonsLeft;
                int vy = fits ? y + (ButtonHeight - size.Height) / 2 : rowBottom + 8;
                _verification.Location = new Point(Margin_, vy);
                rowBottom = Math.Max(rowBottom, vy + size.Height);
            }
            return rowBottom + 10;
        }

        private int PlaceText(LinkLabel label, string text, List<(int Start, int Length, string Href)> links, int x, int y, int width)
        {
            label.Visible = text.Length > 0;
            if (text.Length == 0) return y;
            if (label.Text != text || label.Links.Count != links.Count)
            {
                label.Text = text;
                label.Links.Clear();
                foreach (var (start, length, href) in links) label.Links.Add(start, length, href);
                if (links.Count == 0) label.LinkArea = new LinkArea(0, 0);
            }
            int h = Measure(text, label.Font, width).Height;
            label.Bounds = new Rectangle(x, y, width, h);
            return y + h + BlockGap;
        }

        private static Size Measure(string text, Font font, int width) =>
            text.Length == 0 ? Size.Empty : TextRenderer.MeasureText(text, font, new Size(width <= 0 ? int.MaxValue : width, int.MaxValue),
                (width > 0 ? TextFormatFlags.WordBreak : 0) | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

        private void ApplyProgress(TaskDialogProgressBar model)
        {
            bool marquee = model.State is TaskDialogProgressBarState.Marquee or TaskDialogProgressBarState.MarqueePaused;
            _progress.Style = marquee ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            _progress.MarqueeAnimationSpeed = model.State == TaskDialogProgressBarState.Marquee ? Math.Max(1, model.MarqueeSpeed == 0 ? 30 : model.MarqueeSpeed) : 0;
            int min = Math.Min(model.Minimum, model.Maximum), max = Math.Max(model.Minimum, model.Maximum);
            _progress.Minimum = 0;
            _progress.Maximum = max;
            _progress.Minimum = min;
            _progress.Value = Math.Clamp(model.Value, min, max);
            _progress.BarState = model.State switch
            {
                TaskDialogProgressBarState.Error => ProgressBarBarState.Error,
                TaskDialogProgressBarState.Paused => ProgressBarBarState.Paused,
                _ => ProgressBarBarState.Normal,
            };
        }

        /// <summary>&lt;a href="x"&gt;text&lt;/a&gt; becomes a link when links are enabled; the markup is text otherwise.</summary>
        internal static (string Text, List<(int Start, int Length, string Href)> Links) ParseLinks(string? markup, bool enabled)
        {
            var links = new List<(int, int, string)>();
            markup ??= string.Empty;
            if (!enabled || markup.IndexOf("<a", StringComparison.OrdinalIgnoreCase) < 0) return (markup, links);
            var sb = new System.Text.StringBuilder();
            int last = 0;
            foreach (Match m in s_anchor.Matches(markup))
            {
                sb.Append(markup, last, m.Index - last);
                string inner = m.Groups["text"].Value;
                string href = m.Groups["href"].Success ? m.Groups["href"].Value : inner;
                links.Add((sb.Length, inner.Length, href));
                sb.Append(inner);
                last = m.Index + m.Length;
            }
            sb.Append(markup, last, markup.Length - last);
            return (sb.ToString(), links);
        }

        private static readonly Regex s_anchor = new(@"<a(?:\s+href\s*=\s*""(?<href>[^""]*)"")?\s*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private void OnLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_page != null && e.Link?.LinkData is string href) _page.OnLinkClicked(new TaskDialogLinkClickedEventArgs(href));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _page?.OnCreated(EventArgs.Empty);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // The close box and Alt+F4 cancel, when the page allows it; nothing else may close the dialog.
            if (!_closingFromDialog && _dialog.Result == null && e.CloseReason == CloseReason.UserClosing)
            {
                _closingFromDialog = true;
                try
                {
                    _dialog.Cancel();
                }
                finally
                {
                    _closingFromDialog = false;
                }
                if (_dialog.Result == null) e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _page?.OnDestroyed(EventArgs.Empty);
        }

        public void CloseFromDialog()
        {
            if (_closingFromDialog) return;
            _closingFromDialog = true;
            try
            {
                // Setting DialogResult ends a modal form (and clears Modal); a modeless one is closed.
                bool modal = Modal;
                DialogResult = DialogResult.OK;
                if (!modal) Close();
            }
            finally
            {
                _closingFromDialog = false;
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    _dialog.Cancel();
                    return true;
                case Keys.F1:
                    _page?.OnHelpRequest(EventArgs.Empty);
                    return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            var g = e.Graphics;
            var icon = _page?.Icon;
            if (!_barBounds.IsEmpty && icon?.BarColor is { } barColor)
            {
                using var brush = new SolidBrush(barColor);
                g.FillRectangle(brush, _barBounds);
            }
            if (!_iconBounds.IsEmpty && icon?.GetImage() is { } image) g.DrawImage(image, _iconBounds);
            if (_footer.Height > 0)
            {
                using var pen = new Pen(FooterLine);
                g.DrawLine(pen, 0, _footer.Top - 1, ClientSize.Width, _footer.Top - 1);
            }
        }

        private void PaintFootnoteIcon(object? sender, PaintEventArgs e)
        {
            using (var pen = new Pen(FooterLine)) e.Graphics.DrawLine(pen, Margin_, 0, _footnotePanel.Width - Margin_, 0);
            if (_page?.Footnote is { IsCreated: true } footnote && footnote.Icon?.GetImage() is { } image)
                e.Graphics.DrawImage(image, new Rectangle(Margin_, _footnoteIconTop, 16, 16));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _headingFont?.Dispose();
                _commandLinkFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>The "See details" toggle: a chevron in a circle and the text, like comctl32's expando button.</summary>
    private sealed class ExpandoButton : Control
    {
        private bool _hot;

        public ExpandoButton()
        {
            SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            BackColor = Color.Transparent;
        }

        public bool Expanded { get; set; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font);
            return new Size(20 + 6 + text.Width, Math.Max(20, text.Height));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hot = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Space or Keys.Enter)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int cy = Height / 2;
            var circle = new Rectangle(1, cy - 9, 18, 18);
            using (var pen = new Pen(_hot ? Theme.Accent : Color.FromArgb(0x9A, 0x9A, 0x9A))) g.DrawEllipse(pen, circle);
            using (var chevron = new Pen(Color.FromArgb(0x40, 0x40, 0x40), 1.6f))
            {
                // Down when collapsed (there is more below), up when expanded.
                int d = Expanded ? -1 : 1;
                g.DrawLines(chevron, new[] { new Point(6, cy - 2 * d), new Point(10, cy + 2 * d), new Point(14, cy - 2 * d) });
            }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(26, 0, Width - 26, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, new Rectangle(24, 1, Width - 25, Height - 2));
        }
    }

    /// <summary>A command link: a flat button with an arrow, a large caption and a description line.</summary>
    private sealed class CommandLinkControl : Button
    {
        private readonly string _description;
        private readonly Font _titleFont;
        private bool _hot;

        public CommandLinkControl(string title, string? description, Font titleFont)
        {
            Text = title;
            _description = description ?? string.Empty;
            _titleFont = titleFont;
            UseMnemonic = true;
        }

        public bool ShowShield { get; set; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width > 0 ? proposedSize.Width : 400;
            int textWidth = Math.Max(1, width - 40);
            int h = TextRenderer.MeasureText(Text, _titleFont, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
            if (_description.Length > 0) h += 2 + TextRenderer.MeasureText(_description, Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            int natural = 40 + Math.Max(TextRenderer.MeasureText(Text, _titleFont).Width, TextRenderer.MeasureText(_description, Font).Width);
            return new Size(proposedSize.Width > 0 ? width : natural, h + 18);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hot = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Parent?.BackColor ?? SystemColors.Window;
            using (var b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);
            if (Enabled && (_hot || Focused))
            {
                using var fill = new SolidBrush(Color.FromArgb(0xF0, 0xF6, 0xFD));
                using var border = new Pen(Color.FromArgb(0xC5, 0xDD, 0xF6));
                g.FillRectangle(fill, 0, 0, Width - 1, Height - 1);
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            }
            var textColor = Enabled ? Color.FromArgb(0x15, 0x1C, 0x55) : SystemColors.GrayText;
            if (ShowShield) g.DrawImage(ShieldSmall, new Rectangle(10, 10, 16, 16));
            else
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var arrow = new Pen(Enabled ? Color.FromArgb(0x1E, 0x6F, 0xC4) : SystemColors.GrayText, 2f);
                g.DrawLine(arrow, 10, 18, 24, 18);
                g.DrawLines(arrow, new[] { new PointF(18, 12), new PointF(24, 18), new PointF(18, 24) });
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            }
            int textWidth = Math.Max(1, Width - 40);
            var titleSize = TextRenderer.MeasureText(Text, _titleFont, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak);
            TextRenderer.DrawText(g, Text, _titleFont, new Rectangle(32, 8, textWidth, titleSize.Height), textColor, TextFormatFlags.WordBreak);
            if (_description.Length > 0)
                TextRenderer.DrawText(g, _description, Font, new Rectangle(32, 8 + titleSize.Height + 2, textWidth, Height), Enabled ? textColor : SystemColors.GrayText, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, new Rectangle(2, 2, Width - 4, Height - 4));
        }

        private static Image? s_shield;

        private static Image ShieldSmall => s_shield ??= new Bitmap(SystemIcons.Shield.ToBitmap(), 16, 16);
    }
}
