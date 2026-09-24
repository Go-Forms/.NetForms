using System.ComponentModel;
using System.Text;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Decision 124: RichTextBox - RichEdit's text model ("\n" paragraphs), character and paragraph formatting that
/// edits carry along, RTF in and out, Find, protection, undo, the events, files, and the layout of mixed fonts.
/// </summary>
public class RichTextBoxTests
{
    // Colours come back through a COLORREF and ColorTranslator.FromOle, which names the known ones - as in WinForms.
    private static readonly Color Red = Color.Red;
    private static readonly Color Blue = Color.Blue;

    private static (Form form, TestWindow window, RichTextBox box) Show(int width = 200, int height = 100)
    {
        var platform = TestPlatform.Install();
        var box = new RichTextBox { Bounds = new Rectangle(10, 10, width, height), Font = new Font("Arial", 10) };
        var form = new Form { ClientSize = new Size(width + 40, height + 40) };
        form.Controls.Add(box);
        form.Show();
        box.Focus();
        return (form, platform.Windows.Last(), box);
    }

    private static RichTextBox Box(string text = "")
    {
        TestPlatform.Install();
        return new RichTextBox { Font = new Font("Arial", 10), Text = text };
    }

    private static void Type(TestWindow window, string text)
    {
        foreach (char c in text) window.Host.TextInput(c.ToString());
    }

    [Fact]
    public void ParagraphBreaksAreOneCharacter()
    {
        var box = Box();
        box.Text = "one\r\ntwo\rthree\nfour";
        Assert.Equal("one\ntwo\nthree\nfour", box.Text);
        Assert.Equal(18, box.TextLength);
        Assert.Equal(new[] { "one", "two", "three", "four" }, box.Lines);
        Assert.Equal(4, box.GetFirstCharIndexFromLine(1));
        Assert.Equal(1, box.GetLineFromCharIndex(5));

        // SuperAdventure's message log: append, then scroll to the end.
        box.Text = "";
        box.Text += "You see a rat." + Environment.NewLine;
        box.Text += "You hit it." + Environment.NewLine;
        box.SelectionStart = box.Text.Length;
        box.ScrollToCaret();
        Assert.Equal("You see a rat.\nYou hit it.\n", box.Text);
        Assert.Equal(box.TextLength, box.SelectionStart);
    }

    [Fact]
    public void DefaultsAreThoseOfWinForms()
    {
        var box = Box();
        Assert.True(box.Multiline);
        Assert.Equal(int.MaxValue, box.MaxLength);
        Assert.Equal(RichTextBoxScrollBars.Both, box.ScrollBars);
        Assert.True(box.DetectUrls);
        Assert.False(box.AutoSize);
        Assert.Equal(new Size(100, 96), box.Size);
        Assert.Equal(1f, box.ZoomFactor);
        Assert.Equal(RichTextBoxLanguageOptions.AutoFont | RichTextBoxLanguageOptions.DualFont, box.LanguageOption);
        Assert.Equal("Arial", box.SelectionFont!.Name);
        // The box's font reaches RichEdit through WM_SETFONT as whole pixels: Arial 10 reads back as 9.75 points.
        Assert.Equal(9.75f, box.SelectionFont.Size);
        Assert.Equal(box.ForeColor.ToArgb(), box.SelectionColor.ToArgb());
        Assert.Equal(box.BackColor, box.SelectionBackColor);
        Assert.Equal(RichTextBoxSelectionTypes.Empty, box.SelectionType);
        Assert.Equal(HorizontalAlignment.Left, box.SelectionAlignment);
        Assert.Empty(box.SelectionTabs);
    }

    [Fact]
    public void SelectionFormattingAppliesToTheRangeAndReadsMixedAsWinFormsDoes()
    {
        var box = Box("Hello world");
        box.Select(0, 5);
        box.SelectionColor = Color.Red;
        box.SelectionFont = new Font("Arial", 10, FontStyle.Bold);
        Assert.Equal(Color.Red, box.SelectionColor);
        Assert.True(box.SelectionColor.IsNamedColor);
        Assert.True(box.SelectionFont!.Bold);

        box.Select(6, 5);
        Assert.Equal(box.ForeColor.ToArgb(), box.SelectionColor.ToArgb());
        Assert.False(box.SelectionFont!.Bold);

        box.Select(0, 11);
        Assert.Equal(Color.Empty, box.SelectionColor);
        var mixed = box.SelectionFont!;
        Assert.Equal("Arial", mixed.Name);
        Assert.False(mixed.Bold); // a style only when all of the selection has it
        box.Select(4, 3);
        box.SelectionFont = new Font("Courier New", 12);
        box.Select(0, 11);
        Assert.Null(box.SelectionFont); // faces differ
        box.Select(4, 3);
        box.SelectionFont = new Font("Arial", 12);
        box.Select(0, 11);
        Assert.Equal(13f, box.SelectionFont!.Size); // sizes differ: 13, as GetCharFormatFont
        Assert.True(box.Modified);
    }

    [Fact]
    public void TypingContinuesTheFormatBeforeTheCaretOrTheOneSetOnAnEmptySelection()
    {
        var (form, window, box) = Show();
        using (form)
        {
            Type(window, "ab");
            box.Select(0, 2);
            box.SelectionColor = Color.Red;
            box.Select(2, 0);
            Type(window, "c");
            box.Select(2, 1);
            Assert.Equal(Red, box.SelectionColor);

            // Set on the empty selection, taken by what is typed there.
            box.Select(3, 0);
            box.SelectionColor = Color.Blue;
            Assert.Equal(Blue, box.SelectionColor);
            Type(window, "d");
            box.Select(3, 1);
            Assert.Equal(Blue, box.SelectionColor);

            // Moving the caret forgets it.
            box.Select(0, 0);
            box.SelectionColor = Color.Green;
            box.Select(4, 0);
            Type(window, "e");
            box.Select(4, 1);
            Assert.Equal(Blue, box.SelectionColor);
        }
    }

    [Fact]
    public void AppendTextAfterColouringTheEndIsColoured()
    {
        var box = Box("log: ");
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectionColor = Color.Red;
        box.AppendText("error");
        box.SelectionColor = box.ForeColor;
        box.Select(5, 5);
        Assert.Equal(Red, box.SelectionColor);
        box.Select(0, 4);
        Assert.Equal(box.ForeColor.ToArgb(), box.SelectionColor.ToArgb());
    }

    [Fact]
    public void RtfIsWrittenAsRichEditWritesIt()
    {
        var box = Box("Hello");
        var rtf = box.Rtf;
        Assert.StartsWith(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Arial;}}", rtf);
        Assert.Contains(@"\pard\f0\fs20 Hello\par", rtf);
        Assert.DoesNotContain(@"\colortbl", rtf); // the box's own text colour is the automatic one
        Assert.EndsWith("}\r\n", rtf);

        box.Select(0, 5);
        box.SelectionColor = Color.Red;
        box.Select(1, 2);
        box.SelectionFont = new Font("Arial", 10, FontStyle.Bold | FontStyle.Underline);
        rtf = box.Rtf;
        Assert.Contains(@"{\colortbl ;\red255\green0\blue0;}", rtf);
        Assert.Contains(@"\cf1", rtf);
        Assert.Contains(@"\ul\b", rtf);
        Assert.Contains(@"\ulnone\b0", rtf);
    }

    [Fact]
    public void RtfRoundTripsTextAndFormatting()
    {
        var box = Box("Title\nплан: bullet one\nbullet two\ncentered");
        box.Select(0, 5);
        box.SelectionFont = new Font("Times New Roman", 16, FontStyle.Bold);
        box.SelectionColor = Color.Blue;
        box.Select(6, 1);
        box.SelectionBackColor = Color.Yellow;
        box.Select(8, 20);
        box.SelectionBullet = true;
        box.Select(box.TextLength - 3, 0);
        box.SelectionAlignment = HorizontalAlignment.Center;
        box.SelectionIndent = 20;
        box.SelectionRightIndent = 10;
        box.SelectionHangingIndent = 15;
        box.SelectionTabs = new[] { 40, 80 };

        var copy = Box();
        copy.Rtf = box.Rtf;
        Assert.Equal(box.Text, copy.Text);
        Assert.Equal(box.Rtf, copy.Rtf);

        copy.Select(0, 5);
        Assert.Equal("Times New Roman", copy.SelectionFont!.Name);
        Assert.Equal(16f, copy.SelectionFont.Size);
        Assert.True(copy.SelectionFont.Bold);
        Assert.Equal(Blue, copy.SelectionColor);
        copy.Select(6, 1);
        Assert.Equal(Color.Yellow, copy.SelectionBackColor);
        copy.Select(10, 0);
        Assert.True(copy.SelectionBullet);
        copy.Select(0, 0);
        Assert.False(copy.SelectionBullet);
        copy.Select(copy.TextLength - 1, 0);
        Assert.Equal(HorizontalAlignment.Center, copy.SelectionAlignment);
        Assert.Equal(20, copy.SelectionIndent);
        Assert.Equal(10, copy.SelectionRightIndent);
        Assert.Equal(15, copy.SelectionHangingIndent);
        Assert.Equal(new[] { 40, 80 }, copy.SelectionTabs);
        Assert.True(copy.Modified); // StreamIn sets EM_SETMODIFY
    }

    [Fact]
    public void RtfFromOtherWritersIsRead()
    {
        // WordPad-style: Cyrillic through the font's charset, \u with fallback, a bullet list, alignment, colours.
        const string rtf = @"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Calibri;}{\f1\fnil\fcharset204 Calibri;}{\f2\fnil\fcharset2 Symbol;}}
{\colortbl ;\red0\green77\blue187;}
{\*\generator Riched20 10.0.19041}\viewkind4\uc1
\pard\sa200\sl276\slmult1\qc\cf1\b\f0\fs28\lang9 Report\cf0\b0\fs22\par
\pard{\pntext\f2\'B7\tab}{\*\pn\pnlvlblt\pnf2\pnindent0{\pntxtb\'B7}}\fi-360\li720\sa200\sl276\slmult1\f1\lang1049\'cf\'f0\'e8\'e2\'e5\'f2\f0\lang9\par
{\pntext\f2\'B7\tab}\" + @"u8364?\~5 \{x\}\par
\pard\sa200\sl276\slmult1\i end\i0\par
}
";
        var box = Box();
        box.Rtf = rtf;
        Assert.Equal("Report\nПривет\n\u20AC\u00A05 {x}\nend", box.Text);
        box.Select(0, 6);
        Assert.Equal(HorizontalAlignment.Center, box.SelectionAlignment);
        Assert.True(box.SelectionFont!.Bold);
        Assert.Equal(14f, box.SelectionFont.Size);
        Assert.Equal(Color.FromArgb(255, 0, 77, 187), box.SelectionColor);
        box.Select(8, 0);
        Assert.True(box.SelectionBullet);
        Assert.Equal(24, box.SelectionIndent); // \li720 \fi-360: the first line (the bullet) at 360 twips
        Assert.Equal(24, box.SelectionHangingIndent);
        box.Select(14, 0);
        Assert.True(box.SelectionBullet);
        box.Select(box.TextLength - 1, 0);
        Assert.False(box.SelectionBullet);
        box.Select(box.TextLength - 3, 3);
        Assert.True(box.SelectionFont!.Italic);
    }

    [Fact]
    public void NotRtfIsRejectedAndEmptyClears()
    {
        var box = Box("text");
        Assert.Throws<ArgumentException>(() => box.Rtf = "plain");
        box.Rtf = "";
        Assert.Equal("", box.Text);
        Assert.Throws<ArgumentException>(() => box.SelectedRtf = "plain");
    }

    [Fact]
    public void SelectedRtfReplacesTheSelectionWithItsFormatting()
    {
        var source = Box("red");
        source.SelectAll();
        source.SelectionColor = Color.Red;
        var fragment = source.SelectedRtf;
        Assert.DoesNotContain(@"\par" + "\r\n", fragment); // a selection does not end in the final paragraph mark

        var box = Box("[ ]");
        box.Select(1, 1);
        box.SelectedRtf = fragment;
        Assert.Equal("[red]", box.Text);
        box.Select(1, 3);
        Assert.Equal(Red, box.SelectionColor);
        box.Select(4, 1);
        Assert.Equal(box.ForeColor.ToArgb(), box.SelectionColor.ToArgb());

        // A "\par" inside it is a paragraph break, as RichEdit inserts it.
        box.Select(0, 0);
        box.SelectedRtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\pard\qr\f0 first\par}";
        Assert.Equal("first\n[red]", box.Text);
        box.Select(0, 0);
        Assert.Equal(HorizontalAlignment.Right, box.SelectionAlignment);
        box.Select(7, 0);
        Assert.Equal(HorizontalAlignment.Left, box.SelectionAlignment);
    }

    [Fact]
    public void ParagraphFormatsFollowEditsAsInRichEdit()
    {
        var box = Box("one\ntwo\nthree");
        box.Select(4, 0);
        box.SelectionAlignment = HorizontalAlignment.Center;
        Assert.Equal(HorizontalAlignment.Left, Alignment(box, 0));
        Assert.Equal(HorizontalAlignment.Center, Alignment(box, 5));
        Assert.Equal(HorizontalAlignment.Left, Alignment(box, 9));

        // A break typed inside the centred paragraph: both halves centred.
        box.Select(5, 0);
        box.SelectedText = "\n";
        Assert.Equal("one\nt\nwo\nthree", box.Text);
        Assert.Equal(HorizontalAlignment.Center, Alignment(box, 4));
        Assert.Equal(HorizontalAlignment.Center, Alignment(box, 6));

        // Deleting the break after "one": the joined paragraph keeps the first one's format.
        box.Select(3, 1);
        box.SelectedText = "";
        Assert.Equal("onet\nwo\nthree", box.Text);
        Assert.Equal(HorizontalAlignment.Left, Alignment(box, 0));
        Assert.Equal(HorizontalAlignment.Center, Alignment(box, 5));

        // Mixed paragraphs read as Left.
        box.SelectAll();
        Assert.Equal(HorizontalAlignment.Left, box.SelectionAlignment);
    }

    private static HorizontalAlignment Alignment(RichTextBox box, int at)
    {
        box.Select(at, 0);
        return box.SelectionAlignment;
    }

    [Fact]
    public void FindSearchesAsEmFindText()
    {
        var box = Box("The cat sat on the Cat mat; category");
        Assert.Equal(4, box.Find("cat"));
        Assert.Equal(4, box.SelectionStart);
        Assert.Equal(3, box.SelectionLength);
        Assert.Equal(19, box.Find("Cat", RichTextBoxFinds.MatchCase));
        Assert.Equal(28, box.Find("cat", 20, RichTextBoxFinds.None));
        Assert.Equal(-1, box.Find("cat", 20, RichTextBoxFinds.WholeWord));
        Assert.Equal(28, box.Find("cat", RichTextBoxFinds.Reverse));
        Assert.Equal(19, box.Find("cat", 0, 27, RichTextBoxFinds.Reverse | RichTextBoxFinds.WholeWord));
        box.Select(0, 0);
        Assert.Equal(8, box.Find("sat", RichTextBoxFinds.NoHighlight));
        Assert.Equal(0, box.SelectionStart);
        Assert.Equal(-1, box.Find("dog"));
        Assert.Equal(4, box.Find("cat", 5, 5, RichTextBoxFinds.None)); // an empty range: the whole text
        Assert.Throws<ArgumentOutOfRangeException>(() => box.Find("cat", -1, RichTextBoxFinds.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => box.Find("cat", 0, -2, RichTextBoxFinds.None));
        Assert.Throws<ArgumentException>(() => box.Find("cat", 10, 5, RichTextBoxFinds.None));
        Assert.Throws<ArgumentNullException>(() => box.Find((string)null!));

        Assert.Equal(13, box.Find(new[] { 'n', 'm' }));
        Assert.Equal(23, box.Find(new[] { 'm' }, 10));
        Assert.Equal(-1, box.Find(new[] { 'm' }, 0, 10));
        Assert.Equal(-1, box.Find(Array.Empty<char>()));
    }

    [Fact]
    public void ProtectedTextRefusesEditsAndRaisesProtected()
    {
        var (form, window, box) = Show();
        using (form)
        {
            box.Text = "keep this; edit";
            box.Select(0, 9);
            box.SelectionProtected = true;
            Assert.True(box.SelectionProtected);
            int raised = 0;
            box.Protected += (_, _) => raised++;

            box.Select(2, 0);
            Type(window, "x");
            Assert.Equal("keep this; edit", box.Text);
            Assert.Equal(1, raised);

            box.Select(5, 6);
            window.Host.KeyDown((int)Keys.Delete, InputModifiers.None);
            Assert.Equal("keep this; edit", box.Text);
            Assert.Equal(2, raised);

            box.Select(0, 4);
            box.SelectedText = "lose";
            Assert.Equal("keep this; edit", box.Text);
            Assert.Equal(3, raised);

            box.Select(0, 4);
            box.SelectionColor = Color.Red; // formatting protected text is refused too
            Assert.Equal(4, raised);
            Assert.NotEqual(Red, box.SelectionColor);

            // Outside it, and at its end, editing works.
            box.Select(box.TextLength, 0);
            Type(window, "!");
            Assert.Equal("keep this; edit!", box.Text);
            box.Select(9, 0);
            Type(window, ",");
            Assert.Equal("keep this,; edit!", box.Text);
            Assert.Equal(4, raised);
        }
    }

    [Fact]
    public void UndoGroupsTypingAndRestoresFormatting()
    {
        var (form, window, box) = Show();
        using (form)
        {
            Type(window, "abc");
            Assert.Equal("Typing", box.UndoActionName);
            box.Undo();
            Assert.Equal("", box.Text);
            Assert.Equal("Typing", box.RedoActionName);
            box.Redo();
            Assert.Equal("abc", box.Text);

            box.Select(0, 3);
            box.SelectionColor = Color.Red;
            Assert.Equal(Red, box.SelectionColor);
            Assert.Equal("Unknown", box.UndoActionName);
            box.Undo();
            box.Select(0, 3);
            Assert.Equal(box.ForeColor.ToArgb(), box.SelectionColor.ToArgb());

            box.Select(3, 0);
            window.Host.KeyDown((int)Keys.Back, InputModifiers.None);
            Assert.Equal("ab", box.Text);
            Assert.Equal("Delete", box.UndoActionName);
        }
    }

    [Fact]
    public void EventsFollowSelectionContentsAndLinks()
    {
        var (form, window, box) = Show(200, 60);
        using (form)
        {
            var selections = new List<(int, int)>();
            box.SelectionChanged += (_, _) => selections.Add((box.SelectionStart, box.SelectionLength));
            var sizes = new List<int>();
            box.ContentsResized += (_, e) => sizes.Add(e.NewRectangle.Height);
            var links = new List<LinkClickedEventArgs>();
            box.LinkClicked += (_, e) => links.Add(e);
            int vscroll = 0;
            box.VScroll += (_, _) => vscroll++;

            Type(window, "ab");
            Assert.Equal(new[] { (1, 0), (2, 0) }, selections);
            box.Select(0, 2);
            Assert.Equal((0, 2), selections[^1]);
            int before = sizes.Count;
            box.AppendText("\nsecond line");
            Assert.True(sizes.Count > before);
            Assert.True(sizes[^1] > 25, "two lines of Arial 10: " + sizes[^1]);

            box.Text = "see https://example.org/page, then";
            var at = box.GetPositionFromCharIndex(10);
            window.Host.MouseDown(MouseButton.Left, new Point(box.Left + at.X + 2, box.Top + at.Y + 3), 1, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Left, new Point(box.Left + at.X + 2, box.Top + at.Y + 3), InputModifiers.None);
            var link = Assert.Single(links);
            Assert.Equal("https://example.org/page", link.LinkText);
            Assert.Equal(4, link.LinkStart);
            Assert.Equal(24, link.LinkLength);

            box.Text = string.Join("\n", Enumerable.Range(1, 30).Select(i => "line " + i));
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
            Assert.True(vscroll > 0);
        }
    }

    [Fact]
    public void ScrollBarsAppearOnlyWhenTheTextOverflows()
    {
        var (form, _, box) = Show(200, 60);
        using (form)
        {
            box.Text = "short";
            Assert.Null(box.VerticalBar);
            box.Text = string.Join("\n", Enumerable.Range(1, 30).Select(i => "line " + i));
            Assert.NotNull(box.VerticalBar);
            Assert.Null(box.HorizontalBar); // word wrap: never a horizontal one
            box.ScrollBars = RichTextBoxScrollBars.None;
            Assert.Null(box.VerticalBar);
            box.ScrollBars = RichTextBoxScrollBars.ForcedVertical;
            box.Text = "short";
            Assert.NotNull(box.VerticalBar);
            Assert.False(box.VerticalBar!.Enabled);
            box.ScrollBars = RichTextBoxScrollBars.Both;
            box.WordWrap = false;
            box.Text = new string('w', 200);
            Assert.NotNull(box.HorizontalBar);
        }
    }

    [Fact]
    public void LinesTakeTheHeightOfTheirTallestRun()
    {
        var box = Box("a\nb\nc");
        box.Size = new Size(200, 200);
        int plain = box.GetPositionFromCharIndex(2).Y - box.GetPositionFromCharIndex(0).Y;
        box.Select(2, 1);
        box.SelectionFont = new Font("Arial", 20);
        int tall = box.GetPositionFromCharIndex(4).Y - box.GetPositionFromCharIndex(2).Y;
        Assert.Equal(plain, box.GetPositionFromCharIndex(2).Y - box.GetPositionFromCharIndex(0).Y);
        Assert.True(tall > plain * 1.5, $"{tall} vs {plain}");

        // Indent moves the text; a centred paragraph starts further right.
        int x0 = box.GetPositionFromCharIndex(0).X;
        box.Select(0, 0);
        box.SelectionIndent = 30;
        Assert.Equal(x0 + 30, box.GetPositionFromCharIndex(0).X);
        box.Select(4, 0);
        box.SelectionAlignment = HorizontalAlignment.Center;
        Assert.True(box.GetPositionFromCharIndex(4).X > 80);

        // A tab goes to the next half inch (48 px), or to the paragraph's own stop.
        box.Text = "a\tb";
        box.Select(0, 0);
        box.SelectionIndent = 0;
        int start = box.GetPositionFromCharIndex(0).X;
        Assert.Equal(start + 48, box.GetPositionFromCharIndex(2).X);
        box.SelectionTabs = new[] { 100 };
        Assert.Equal(start + 100, box.GetPositionFromCharIndex(2).X);
    }

    [Fact]
    public void PositionsAndZoomAsRichEdit()
    {
        var box = Box("abc");
        Assert.Equal(Point.Empty, box.GetPositionFromCharIndex(-1));
        Assert.Equal(Point.Empty, box.GetPositionFromCharIndex(4));
        Assert.Equal(2, box.GetCharIndexFromPosition(new Point(90, 5))); // past the end: the last character
        int width = box.GetPositionFromCharIndex(3).X - box.GetPositionFromCharIndex(0).X;
        box.ZoomFactor = 2.0f;
        Assert.InRange(box.GetPositionFromCharIndex(3).X - box.GetPositionFromCharIndex(0).X, width * 2 - 2, width * 2 + 2);
        box.ZoomFactor = 1.2345f;
        Assert.Equal(1.235f, box.ZoomFactor); // thousandths, rounded up (EM_SETZOOM)
        Assert.Throws<ArgumentOutOfRangeException>(() => box.ZoomFactor = 64f);
        Assert.Throws<ArgumentOutOfRangeException>(() => box.ZoomFactor = 0.01f);
    }

    [Fact]
    public void FilesLoadAndSaveInEveryStreamType()
    {
        var box = Box("Строка\nline");
        box.Select(0, 6);
        box.SelectionFont = new Font("Arial", 10, FontStyle.Italic);

        using (var rtf = new MemoryStream())
        {
            box.SaveFile(rtf, RichTextBoxStreamType.RichText);
            Assert.All(rtf.ToArray(), b => Assert.True(b < 0x80)); // ASCII: other characters as \uN?
            rtf.Position = 0;
            var loaded = Box();
            loaded.LoadFile(rtf, RichTextBoxStreamType.RichText);
            Assert.Equal(box.Text, loaded.Text);
            loaded.Select(0, 6);
            Assert.True(loaded.SelectionFont!.Italic);
        }

        using (var unicode = new MemoryStream())
        {
            box.SaveFile(unicode, RichTextBoxStreamType.UnicodePlainText);
            Assert.Equal("Строка\r\nline", Encoding.Unicode.GetString(unicode.ToArray()));
            unicode.Position = 0;
            var loaded = Box();
            loaded.LoadFile(unicode, RichTextBoxStreamType.UnicodePlainText);
            Assert.Equal("Строка\nline", loaded.Text);
        }

        var utf8 = new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Привет\r\nмир")).ToArray());
        var text = Box();
        text.LoadFile(utf8, RichTextBoxStreamType.PlainText);
        Assert.Equal("Привет\nмир", text.Text);
        Assert.True(text.Modified);

        Assert.Throws<ArgumentException>(() => Box().LoadFile(new MemoryStream(Encoding.ASCII.GetBytes("not rtf")), RichTextBoxStreamType.RichText));
        Assert.Throws<InvalidEnumArgumentException>(() => Box().LoadFile(new MemoryStream(), (RichTextBoxStreamType)9));

        var path = Path.Combine(Path.GetTempPath(), "netforms-rtb-" + Guid.NewGuid() + ".rtf");
        try
        {
            box.SaveFile(path);
            var loaded = Box();
            loaded.LoadFile(path);
            Assert.Equal(box.Text, loaded.Text);
            loaded.Select(0, 6);
            Assert.True(loaded.SelectionFont!.Italic);
            // RTF has half points: the 9.75 of the default format comes back as 10, as it does in RichEdit.
            loaded.Select(7, 4);
            Assert.Equal(10f, loaded.SelectionFont!.Size);
            // What is read writes itself back unchanged.
            var again = Box();
            again.Rtf = loaded.Rtf;
            Assert.Equal(loaded.Rtf, again.Rtf);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CopyAndPasteKeepTheFormatting()
    {
        var (form, window, box) = Show();
        using (form)
        {
            box.Text = "bold plain";
            box.Select(0, 4);
            box.SelectionFont = new Font("Arial", 10, FontStyle.Bold);
            box.Copy();
            Assert.Equal("bold", Clipboard.GetText());
            Assert.True(box.CanPaste(DataFormats.GetFormat(DataFormats.Rtf)));
            box.Select(box.TextLength, 0);
            box.Paste();
            Assert.Equal("bold plainbold", box.Text);
            box.Select(10, 4);
            Assert.True(box.SelectionFont!.Bold);
            Assert.Equal("Paste", box.UndoActionName);

            // Text from elsewhere pastes as text in the format at the caret.
            Clipboard.SetText("x\r\ny");
            box.Select(0, 0);
            box.Paste();
            Assert.Equal("x\nybold plainbold", box.Text);
        }
    }

    [Fact]
    public void RichEditShortcutsAlignParagraphs()
    {
        var (form, window, box) = Show();
        using (form)
        {
            Type(window, "text");
            window.Host.KeyDown((int)Keys.E, InputModifiers.Control);
            Assert.Equal(HorizontalAlignment.Center, box.SelectionAlignment);
            window.Host.KeyDown((int)Keys.R, InputModifiers.Control);
            Assert.Equal(HorizontalAlignment.Right, box.SelectionAlignment);
            box.RichTextShortcutsEnabled = false;
            window.Host.KeyDown((int)Keys.L, InputModifiers.Control);
            Assert.Equal(HorizontalAlignment.Right, box.SelectionAlignment);
        }
    }

    [Fact]
    public void DataFormatsGetFormatRegistersNames()
    {
        Assert.Equal(1, DataFormats.GetFormat(DataFormats.Text).Id);
        Assert.Equal(13, DataFormats.GetFormat(DataFormats.UnicodeText).Id);
        var rtf = DataFormats.GetFormat(DataFormats.Rtf);
        Assert.True(rtf.Id >= 0xC000);
        Assert.Same(rtf, DataFormats.GetFormat(DataFormats.Rtf));
        Assert.Same(rtf, DataFormats.GetFormat(rtf.Id));
    }

    [Fact]
    public void FormattedTextGolden()
    {
        TestPlatform.Install();
        // A family no machine has: the hermetic fallback (DejaVu) draws it the same everywhere.
        const string rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0 NetForms Hermetic;}}{\colortbl ;\red200\green0\blue0;\red0\green0\blue200;\red255\green255\blue0;}
\pard\qc\b\fs28 Rich text\b0\fs18\par
\pard plain, \cf1 red\cf0 , \b bold\b0 , \i italic\i0 , \ul under\ulnone , \highlight3 marked\highlight0 , x\up6 2\up0  and \fs30 big\fs18\par
\pard{\pntext\'B7\tab}{\*\pn\pnlvlblt{\pntxtb\'B7}}\fi-200\li400 first bullet\par
{\pntext\'B7\tab}second bullet, long enough to wrap onto the next line\par
\pard\qr\cf2 right: www.example.org\par
}";
        var box = new RichTextBox { Bounds = new Rectangle(0, 0, 260, 170), BorderStyle = BorderStyle.FixedSingle, Font = new Font("NetForms Hermetic", 9) };
        box.Rtf = rtf;
        using var bmp = new Bitmap(260, 170);
        box.DrawToBitmap(bmp, new Rectangle(0, 0, 260, 170));
        Golden.Assert(bmp, "richtextbox-formatted");
    }
}
