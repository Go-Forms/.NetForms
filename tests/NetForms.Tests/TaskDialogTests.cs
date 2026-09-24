using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>TaskDialog: results, cancelling, live updates and navigation as dotnet/winforms defines them.</summary>
public class TaskDialogTests
{
    private static Form Shown() => Application.OpenForms[Application.OpenForms.Count - 1]!;

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var d in All(c)) yield return d;
        }
    }

    private static Button FooterButton(string text) => All(Shown()).OfType<Button>().Single(b => b.Text == text && b.Visible);

    private static void Key(Keys key) =>
        TestPlatform.Install().Windows.Last().Host.KeyDown((int)key, InputModifiers.None);

    [Fact]
    public void TheDefaultButtonAnswersEnterAndStandardButtonsCompareEqual()
    {
        var platform = TestPlatform.Install();
        TaskDialogPage? shownPage = null;
        platform.OnMessageLoop = () =>
        {
            Assert.Equal("Continue?", platform.Windows.Last().Title);
            Assert.True(platform.Windows.Last().IsModal);
            shownPage!.BoundDialog!.ToString();
            Key(Keys.Return);
        };
        shownPage = new TaskDialogPage
        {
            Caption = "Continue?",
            Text = "You're about to delete the selected files! Continue?",
            Icon = TaskDialogIcon.Warning,
            Buttons = { TaskDialogButton.Yes, TaskDialogButton.No },
            DefaultButton = TaskDialogButton.No,
            SizeToContent = true,
        };
        var result = TaskDialog.ShowDialog(shownPage);
        Assert.True(result == TaskDialogButton.No);
        Assert.False(result == TaskDialogButton.Yes);
        Assert.Same(shownPage.Buttons[1], result);
        Assert.Null(shownPage.BoundDialog); // unbound again once closed
    }

    [Fact]
    public void ACustomButtonIsTheResultUnlessItKeepsTheDialogOpen()
    {
        var platform = TestPlatform.Install();
        var page = new TaskDialogPage { Text = "Pick one" };
        var keep = page.Buttons.Add("Keep open", allowCloseDialog: false);
        var go = page.Buttons.Add("Go");
        int keepClicks = 0;
        keep.Click += (_, _) => keepClicks++;
        platform.OnMessageLoop = () =>
        {
            FooterButton("Keep open").PerformClick();
            Assert.Equal(1, keepClicks);
            Assert.True(platform.Windows.Last().IsVisible);
            go.PerformClick();
        };
        Assert.Same(go, TaskDialog.ShowDialog(page));
    }

    [Fact]
    public void EscapeCancelsOnlyWhenThePageAllowsIt()
    {
        var platform = TestPlatform.Install();
        var page = new TaskDialogPage { Text = "No way out", Buttons = { TaskDialogButton.OK } };
        platform.OnMessageLoop = () =>
        {
            Key(Keys.Escape);
            Assert.True(platform.Windows.Last().IsVisible);
            // The close box is refused too.
            platform.Windows.Last().Host.Closing(userRequested: true);
            Assert.True(platform.Windows.Last().IsVisible);
            FooterButton(SystemStringsOk).PerformClick();
        };
        Assert.True(TaskDialog.ShowDialog(page) == TaskDialogButton.OK);

        var cancellable = new TaskDialogPage { Text = "Leave any time", AllowCancel = true, Buttons = { TaskDialogButton.OK } };
        platform.OnMessageLoop = () => Key(Keys.Escape);
        var result = TaskDialog.ShowDialog(cancellable);
        Assert.True(result == TaskDialogButton.Cancel);
        Assert.False(result.Visible); // a placeholder: Cancel is not one of the page's buttons

        // A Cancel button makes the dialog cancellable, and Escape clicks it.
        var withCancel = new TaskDialogPage { Text = "Sure?", Buttons = { TaskDialogButton.OK, TaskDialogButton.Cancel } };
        int cancelClicks = 0;
        withCancel.Buttons[1].Click += (_, _) => cancelClicks++;
        platform.OnMessageLoop = () => Key(Keys.Escape);
        Assert.Same(withCancel.Buttons[1], TaskDialog.ShowDialog(withCancel));
        Assert.Equal(1, cancelClicks);
    }

    private static string SystemStringsOk => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ОК" : "OK";

    [Fact]
    public void APageWithoutButtonsHasAnOkButton()
    {
        var platform = TestPlatform.Install();
        platform.OnMessageLoop = () => Key(Keys.Return);
        var result = TaskDialog.ShowDialog(new TaskDialogPage { Heading = "Done", Text = "All files were copied." });
        Assert.True(result == TaskDialogButton.OK);
    }

    [Fact]
    public void TextAndProgressUpdateWhileShownButTheStructureDoesNot()
    {
        var platform = TestPlatform.Install();
        var bar = new TaskDialogProgressBar { Value = 5, Minimum = 0, Maximum = 5 };
        var page = new TaskDialogPage { Caption = "Final Countdown", Text = "5 seconds", ProgressBar = bar, Icon = TaskDialogIcon.Information };
        platform.OnMessageLoop = () =>
        {
            var form = Shown();
            var progress = All(form).OfType<ProgressBar>().Single();
            Assert.True(progress.Visible);
            Assert.Equal(5, progress.Value);

            bar.Value = 2;
            page.Text = "2 seconds";
            Assert.Equal(2, progress.Value);
            Assert.Contains(All(form).OfType<LinkLabel>(), l => l.Visible && l.Text == "2 seconds");

            int height = form.ClientSize.Height;
            page.Text = "A much longer text that certainly does not fit on one line of the dialog, so that it has to wrap onto a second and a third line.";
            Assert.True(form.ClientSize.Height > height, "the window grows with its text");

            page.Caption = "Almost";
            Assert.Equal("Almost", platform.Windows.Last().Title);

            Assert.Throws<InvalidOperationException>(() => page.Buttons.Add("Late"));
            Assert.Throws<InvalidOperationException>(() => page.AllowCancel = true);
            Assert.Throws<InvalidOperationException>(() => bar.State = TaskDialogProgressBarState.None);
            bar.State = TaskDialogProgressBarState.Error;
            Assert.Equal(ProgressBarBarState.Error, progress.BarState);
            Key(Keys.Return);
        };
        TaskDialog.ShowDialog(page);
    }

    [Fact]
    public void TheVerificationBoxAndTheExpanderReportTheirState()
    {
        var platform = TestPlatform.Install();
        var verification = new TaskDialogVerificationCheckBox("Accept EULA");
        var expander = new TaskDialogExpander { Text = "By using our product...", CollapsedButtonText = "Nothing to see here..." };
        var page = new TaskDialogPage
        {
            Caption = "Accept EULA",
            Heading = "EULA",
            Text = "Check the box and press OK.",
            Icon = TaskDialogIcon.ShieldWarningYellowBar,
            AllowCancel = true,
            Expander = expander,
            Buttons = { new TaskDialogButton("OK", false) },
            Verification = verification,
        };
        verification.CheckedChanged += (_, _) => page.Buttons[0].Enabled = verification.Checked;
        int expandedChanges = 0;
        expander.ExpandedChanged += (_, _) => expandedChanges++;

        platform.OnMessageLoop = () =>
        {
            var form = Shown();
            var ok = FooterButton("OK");
            Assert.False(ok.Enabled);
            var box = All(form).OfType<CheckBox>().Single();
            Assert.Equal("Accept EULA", box.Text);
            box.Checked = true;
            Assert.True(verification.Checked);
            Assert.True(ok.Enabled);

            Assert.DoesNotContain(All(form).OfType<LinkLabel>(), l => l.Visible && l.Text == "By using our product...");
            var expando = All(form).Single(c => c.GetType().Name == "ExpandoButton");
            Assert.Equal("Nothing to see here...", expando.Text);
            expando.GetType().GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(expando, new object[] { EventArgs.Empty });
            Assert.True(expander.Expanded);
            Assert.Equal(1, expandedChanges);
            Assert.Contains(All(form).OfType<LinkLabel>(), l => l.Visible && l.Text == "By using our product...");
            Assert.Equal("Hide details", expando.Text);
            ok.PerformClick();
        };
        Assert.Same(page.Buttons[0], TaskDialog.ShowDialog(page));
    }

    [Fact]
    public void LinksInTextAndFootnoteRaiseLinkClicked()
    {
        var platform = TestPlatform.Install();
        var page = new TaskDialogPage
        {
            Text = "Read <a href=\"https://example.org/terms\">the terms</a> first.",
            Footnote = "© <a href=\"https://example.org/\">ShadyBiz LLC</a>, 2013",
            EnableLinks = true,
        };
        var clicked = new List<string>();
        page.LinkClicked += (_, e) => clicked.Add(e.LinkHref);
        platform.OnMessageLoop = () =>
        {
            var labels = All(Shown()).OfType<LinkLabel>().Where(l => l.Visible).ToList();
            var text = labels.Single(l => l.Text == "Read the terms first.");
            Assert.Equal(new[] { (5, 9) }, text.Links.Cast<LinkLabel.Link>().Select(l => (l.Start, l.Length)));
            var footnote = labels.Single(l => l.Text == "© ShadyBiz LLC, 2013");
            text.GetType().GetMethod("OnLinkClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(text, new object[] { new LinkLabelLinkClickedEventArgs(text.Links[0]) });
            footnote.GetType().GetMethod("OnLinkClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(footnote, new object[] { new LinkLabelLinkClickedEventArgs(footnote.Links[0]) });
            Key(Keys.Return);
        };
        TaskDialog.ShowDialog(page);
        Assert.Equal(new[] { "https://example.org/terms", "https://example.org/" }, clicked);

        // Without EnableLinks the markup is plain text.
        platform.OnMessageLoop = () =>
        {
            Assert.Contains(All(Shown()).OfType<LinkLabel>(), l => l.Visible && l.Text.Contains("<a href="));
            Key(Keys.Return);
        };
        TaskDialog.ShowDialog(new TaskDialogPage { Text = "See <a href=\"x\">this</a>." });
    }

    [Fact]
    public void RadioButtonsCheckOneAtATime()
    {
        var platform = TestPlatform.Install();
        var page = new TaskDialogPage { Text = "Which?" };
        var first = page.RadioButtons.Add("First");
        var second = page.RadioButtons.Add("Second");
        first.Checked = true;
        var log = new List<string>();
        first.CheckedChanged += (_, _) => log.Add("first " + first.Checked);
        second.CheckedChanged += (_, _) => log.Add("second " + second.Checked);
        platform.OnMessageLoop = () =>
        {
            var radios = All(Shown()).OfType<RadioButton>().ToList();
            Assert.Equal(new[] { true, false }, radios.Select(r => r.Checked));
            radios[1].Checked = true;
            Assert.Throws<InvalidOperationException>(() => second.Checked = false);
            first.Checked = true; // programmatic, while shown
            Key(Keys.Return);
        };
        TaskDialog.ShowDialog(page);
        Assert.Equal(new[] { "first False", "second True", "second False", "first True" }, log);

        page = new TaskDialogPage();
        page.RadioButtons.Add("A").Checked = true;
        page.RadioButtons.Add("B").Checked = true;
        Assert.Throws<InvalidOperationException>(() => TaskDialog.ShowDialog(page));
    }

    [Fact]
    public void NavigationReplacesThePageInTheSameWindow()
    {
        var platform = TestPlatform.Install();
        var log = new List<string>();
        var second = new TaskDialogPage { Heading = "Working...", Buttons = { TaskDialogButton.Close } };
        var first = new TaskDialogPage { Heading = "Start?", Buttons = { TaskDialogButton.Yes, TaskDialogButton.No } };
        first.Created += (_, _) => log.Add("first created");
        first.Destroyed += (_, _) => log.Add("first destroyed");
        second.Created += (_, _) => log.Add("second created");
        second.Destroyed += (_, _) => log.Add("second destroyed");
        first.Buttons[0].Click += (_, _) => first.Navigate(second);

        platform.OnMessageLoop = () =>
        {
            int windows = platform.Windows.Count;
            first.Buttons[0].PerformClick();
            Assert.Equal(windows, platform.Windows.Count); // the same window shows the new page
            Assert.True(platform.Windows.Last().IsVisible);
            Assert.Same(second, second.Buttons[0].BoundPage);
            second.Buttons[0].PerformClick();
        };
        var result = TaskDialog.ShowDialog(first);
        Assert.True(result == TaskDialogButton.Close);
        Assert.Equal(new[] { "first created", "first destroyed", "second created", "second destroyed" }, log);
    }

    [Fact]
    public void CommandLinksAndHelp()
    {
        var platform = TestPlatform.Install();
        var install = new TaskDialogCommandLinkButton("Install now", "Recommended for most people");
        var page = new TaskDialogPage { Heading = "Ready", Buttons = { install, new TaskDialogCommandLinkButton("Later"), TaskDialogButton.Help } };
        int help = 0;
        page.HelpRequest += (_, _) => help++;
        platform.OnMessageLoop = () =>
        {
            FooterButton(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "Справка" : "Help").PerformClick();
            Assert.Equal(1, help);
            Assert.True(platform.Windows.Last().IsVisible);
            Key(Keys.F1);
            Assert.Equal(2, help);
            var link = All(Shown()).OfType<Button>().First(b => b.Text == "Install now");
            link.PerformClick();
        };
        Assert.Same(install, TaskDialog.ShowDialog(page));
    }

    [Fact]
    public void PagesAreCheckedBeforeTheyAreShown()
    {
        TestPlatform.Install();
        var mixed = new TaskDialogPage { Buttons = { new TaskDialogButton("Custom"), new TaskDialogCommandLinkButton("Link") } };
        Assert.Throws<InvalidOperationException>(() => TaskDialog.ShowDialog(mixed));
        var noDefault = new TaskDialogPage { Buttons = { TaskDialogButton.OK }, DefaultButton = TaskDialogButton.Cancel };
        Assert.Throws<InvalidOperationException>(() => TaskDialog.ShowDialog(noDefault));
        Assert.Throws<InvalidOperationException>(() => TaskDialogButton.OK.Text = "Okay");
        var twice = new TaskDialogPage { Buttons = { TaskDialogButton.OK } };
        Assert.Throws<InvalidOperationException>(() => twice.Buttons.Add(TaskDialogButton.OK));
        TaskDialogFootnote note = "implicit";
        Assert.Equal("implicit", note.Text);
    }
}
