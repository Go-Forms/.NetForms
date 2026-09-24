using System.ComponentModel;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Ф6.К, decision 117: MaskedTextBox - typing, rejection, deletion and the text formats, through the BCL's MaskedTextProvider.</summary>
public class MaskedTextBoxTests
{
    private static (Form form, TestWindow window, MaskedTextBox box) Show(string mask)
    {
        var platform = TestPlatform.Install();
        // "/" in a mask is the culture's date separator (as in WinForms): pinned, so the test reads the same everywhere.
        var box = new MaskedTextBox(mask) { Bounds = new Rectangle(10, 10, 150, 23), Culture = System.Globalization.CultureInfo.InvariantCulture };
        var form = new Form { ClientSize = new Size(300, 100) };
        form.Controls.Add(box);
        form.Show();
        box.Focus();
        return (form, platform.Windows.Last(), box);
    }

    private static void Type(TestWindow window, string text)
    {
        foreach (char c in text) window.Host.TextInput(c.ToString());
    }

    [Fact]
    public void TypedDigitsFillTheMaskAroundItsLiterals()
    {
        var (form, window, box) = Show("00/00/0000");
        using (form)
        {
            Assert.Equal("__/__/____", box.TextBoxDisplay());
            Assert.Equal("  /  /", box.Text);
            Type(window, "12052026");
            Assert.Equal("12/05/2026", box.Text);
            Assert.True(box.MaskCompleted);
            Assert.True(box.MaskFull);
            box.TextMaskFormat = MaskFormat.ExcludePromptAndLiterals;
            Assert.Equal("12052026", box.Text);
        }
    }

    [Fact]
    public void AnInvalidCharacterIsRejectedAndReported()
    {
        var (form, window, box) = Show("(999) 000-0000");
        using (form)
        {
            var rejected = new List<MaskedTextResultHint>();
            box.MaskInputRejected += (_, e) => rejected.Add(e.RejectionHint);
            Type(window, "a");
            Assert.Equal(new[] { MaskedTextResultHint.DigitExpected }, rejected);
            Type(window, "4955551234");
            Assert.Equal("(495) 555-1234", box.Text);
            // Backspace removes the digit before the caret and the rest shifts left.
            window.Host.KeyDown((int)Keys.Back, InputModifiers.None);
            Assert.Equal("(495) 555-123", box.Text);
            Assert.False(box.MaskCompleted);
        }
    }

    [Fact]
    public void TextSetInCodeGoesThroughTheMask()
    {
        TestPlatform.Install();
        var box = new MaskedTextBox("00-00") { Culture = System.Globalization.CultureInfo.InvariantCulture };
        var rejected = 0;
        box.MaskInputRejected += (_, _) => rejected++;
        box.Text = "1x234";
        Assert.Equal("12-34", box.Text); // 'x' rejected, the rest one by one
        Assert.Equal(1, rejected);
        box.Mask = "";
        Assert.Equal("12-34", box.Text); // without a mask, the text as it was shown, literals included
        box.Text = "free text";
        Assert.Equal("free text", box.Text);
    }

    [Fact]
    public void ValidatingTypeParsesTheText()
    {
        TestPlatform.Install();
        var box = new MaskedTextBox("00/00/0000") { Culture = new System.Globalization.CultureInfo("ru-RU"), ValidatingType = typeof(DateTime) };
        box.Text = "12052026";
        var args = new List<TypeValidationEventArgs>();
        box.TypeValidationCompleted += (_, e) => args.Add(e);
        Assert.Equal(new DateTime(2026, 5, 12), box.ValidateText());
        Assert.True(args.Single().IsValidInput);
        box.Text = "99999999";
        Assert.Null(box.ValidateText());
        Assert.False(args.Last().IsValidInput);
    }
}

internal static class MaskedTextBoxTestExtensions
{
    /// <summary>What the box shows: the lines of its display (the base keeps the display string).</summary>
    public static string TextBoxDisplay(this MaskedTextBox box) => string.Join("\n", ((TextBoxBase)box).Lines);
}
