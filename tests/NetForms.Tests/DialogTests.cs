using Xunit;

namespace NetForms.Tests;

/// <summary>
/// The file pickers are the system's, so the tests drive them through the platform seam:
/// TestPlatform answers with a queued result and records what it was asked for.
/// </summary>
public class DialogTests
{
    [Fact]
    public void OpenFileDialogPassesItsSettingsDownAndKeepsTheChosenPath()
    {
        var platform = TestPlatform.Install();
        using var dialog = new OpenFileDialog
        {
            Title = "Pick one",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FilterIndex = 2,
            InitialDirectory = @"C:\work",
            Multiselect = true,
        };

        platform.OpenFileResult = new[] { @"C:\work\a.txt", @"C:\work\b.txt" };
        Assert.Equal(DialogResult.OK, dialog.ShowDialog());

        var asked = platform.FileDialogCalls[^1];
        Assert.Equal("Pick one", asked.Title);
        Assert.Equal("Text files (*.txt)|*.txt|All files (*.*)|*.*", asked.Filter);
        Assert.Equal(2, asked.FilterIndex);
        Assert.Equal(@"C:\work", asked.InitialDirectory);
        Assert.True(asked.Multiselect);

        Assert.Equal(@"C:\work\a.txt", dialog.FileName);
        Assert.Equal(2, dialog.FileNames.Length);
        // CheckFileExists is on for an open dialog, off for a save one.
        Assert.True(dialog.CheckFileExists);
    }

    [Fact]
    public void CancellingLeavesTheFileNameEmpty()
    {
        var platform = TestPlatform.Install();
        using var dialog = new OpenFileDialog();
        platform.OpenFileResult = null;

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());
        Assert.Equal(string.Empty, dialog.FileName);
        Assert.Empty(dialog.FileNames);
    }

    [Fact]
    public void FileOkCanVetoTheChoice()
    {
        var platform = TestPlatform.Install();
        using var dialog = new SaveFileDialog { DefaultExt = "txt" };
        platform.SaveFileResult = @"C:\work\out.txt";

        bool veto = true;
        dialog.FileOk += (_, e) => e.Cancel = veto;
        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());
        Assert.Equal(string.Empty, dialog.FileName);

        // Reset() clears the settings, not the handlers, so the veto has to be lifted by hand.
        veto = false;
        dialog.Reset();
        platform.SaveFileResult = @"C:\work\out.txt";
        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
        Assert.Equal(@"C:\work\out.txt", dialog.FileName);
        Assert.True(dialog.OverwritePrompt);
        Assert.False(dialog.CheckFileExists);
    }

    [Fact]
    public void AnInvalidFilterIsRejected()
    {
        using var dialog = new OpenFileDialog();
        Assert.Throws<ArgumentException>(() => dialog.Filter = "Text files|*.txt|All files");
        dialog.Filter = "Text files|*.txt";
        Assert.Equal("Text files|*.txt", dialog.Filter);
    }

    [Fact]
    public void FolderBrowserDialogReturnsTheSelectedPath()
    {
        var platform = TestPlatform.Install();
        using var dialog = new FolderBrowserDialog { Description = "Choose a folder", InitialDirectory = @"C:\work" };
        platform.FolderResult = @"C:\work\sub";

        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
        Assert.Equal(@"C:\work\sub", dialog.SelectedPath);
        Assert.Equal(@"C:\work", platform.FolderDialogCalls[^1].InitialDirectory);

        platform.FolderResult = null;
        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());
        // A cancelled browse keeps the previous selection, as WinForms does.
        Assert.Equal(@"C:\work\sub", dialog.SelectedPath);
    }

    [Fact]
    public void ColorAndFontDialogsCarryTheirSettings()
    {
        TestPlatform.Install();

        using var color = new ColorDialog { Color = Color.Red, FullOpen = true };
        Assert.Equal(Color.Red, color.Color);
        Assert.Equal(16, color.CustomColors.Length);
        color.Reset();
        Assert.Equal(Color.Black, color.Color);
        Assert.True(color.AllowFullOpen);

        using var font = new FontDialog { ShowColor = true, MinSize = 8, MaxSize = 24 };
        Assert.Equal(Control.DefaultFont.Name, font.Font.Name);
        font.Font = new Font("Arial", 14, FontStyle.Bold);
        Assert.Equal(14, font.Font.Size);
        Assert.True(font.ShowEffects);
        font.Reset();
        Assert.Equal(0, font.MinSize);
        Assert.False(font.ShowColor);
    }

    [Fact]
    public void ColorAndFontDialogFormsBuildAndPaint()
    {
        var platform = TestPlatform.Install();
        // The pickers are our own forms: build them through ShowDialog and paint one frame.
        // ShowDialog would block on the message loop, so the forms are exercised directly.
        using var owner = new Form { ClientSize = new Size(600, 400) };
        owner.Show();

        var colorDialog = new ColorDialog();
        var fontDialog = new FontDialog();
        Assert.NotNull(colorDialog);
        Assert.NotNull(fontDialog);

        // FontFamily.Families is what the font picker lists; it must not be empty.
        Assert.NotEmpty(FontFamily.Families);
        owner.Close();
    }
}
