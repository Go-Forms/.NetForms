using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NetForms.Compat;

/// <summary>
/// Scenarios written against the public WinForms API only. The same file is compiled into
/// tests/NetForms.Compat (real System.Windows.Forms, Windows only) and into the NetForms test
/// suite; the two outputs are diffed on Windows CI. Every entry is "key = value" with a
/// stable key; values are formatted with invariant culture.
///
/// Key prefixes: "exact/" must match; "text/" may differ by a few pixels (GDI vs Skia
/// metrics); "info/" is reported, never asserted (known differences such as Form.Size).
/// </summary>
public static class CompatScenarios
{
    public static SortedDictionary<string, string> Run()
    {
        var r = new SortedDictionary<string, string>(StringComparer.Ordinal);
        Defaults(r);
        DesignerForm(r);
        Docking(r);
        Anchoring(r);
        TextMetrics(r);
        FocusOrder(r);
        Controls(r);
        LayoutPanels(r);
        Strips(r);
        Mdi(r);
        RichText(r);
        GridHeaders(r);
        return r;
    }

    /// <summary>DataGridView.ColumnHeadersHeightSizeMode and ScrollBars (the corpus' SuperAdventure uses both).</summary>
    private static void GridHeaders(SortedDictionary<string, string> r)
    {
        using var grid = new DataGridView { Size = new Size(300, 200) };
        grid.Columns.Add("a", "Name");
        grid.Columns.Add("b", "Quantity");
        grid.CreateControl();
        r["exact/dgv/header-defaults"] = $"{grid.ColumnHeadersHeightSizeMode} {grid.ScrollBars} {grid.ColumnHeadersHeight}";
        grid.ColumnHeadersHeight = 40;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        r["text/dgv/header-autosize"] = grid.ColumnHeadersHeight.ToString();
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 16);
        grid.AutoResizeColumnHeadersHeight();
        r["text/dgv/header-autosize-big-font"] = grid.ColumnHeadersHeight.ToString();
        grid.ColumnHeadersHeight = 50; // cached while auto-sized
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        r["exact/dgv/header-after-autosize"] = grid.ColumnHeadersHeight.ToString();
        grid.ScrollBars = ScrollBars.Vertical;
        r["exact/dgv/scrollbars"] = grid.ScrollBars.ToString();
    }

    /// <summary>
    /// RichTextBox (decision 124): RichEdit's text model, how it reads mixed formatting, what edits do to the
    /// paragraph formats, Find, protection, RTF out (without the generator and language tags, which name the
    /// RichEdit build and the machine's language) and in, files.
    /// </summary>
    private static void RichText(SortedDictionary<string, string> r)
    {
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n");
        static string Rtf(string rtf)
        {
            // What differs between machines and builds of RichEdit: the generator, the ANSI code page, languages.
            rtf = System.Text.RegularExpressions.Regex.Replace(rtf, @"\{\\\*\\generator [^}]*\}", "");
            rtf = System.Text.RegularExpressions.Regex.Replace(rtf, @"\\(ansicpg|deflang|lang)\d+", "");
            // A font's charset is the system's (Arial is RUSSIAN_CHARSET on a Russian Windows); Symbol's is its own.
            rtf = System.Text.RegularExpressions.Regex.Replace(rtf, @"\\fcharset(?!2 )\d+", @"\fcharset");
            return Esc(rtf);
        }
        static string C(Color c) => c.IsEmpty ? "Empty" : c.ToArgb().ToString("X8") + (c.IsNamedColor ? " named" : "");
        static string F(Font? f) => f == null ? "null" : f.Name + " " + f.Size.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + f.Style;
        RichTextBox New(string text = "")
        {
            var box = new RichTextBox { Font = new Font("Arial", 10), Size = new Size(200, 100) };
            box.CreateControl();
            if (text.Length > 0) box.Text = text;
            return box;
        }

        using (var box = new RichTextBox())
        {
            r["exact/rtb/defaults"] = $"{box.Multiline} {box.MaxLength} {box.ScrollBars} {S(box.Size)} {box.AutoSize} {box.DetectUrls} {box.SelectionType} {box.ZoomFactor} {box.LanguageOption}";
            r["exact/rtb/selectedrtf-no-handle"] = Rtf(box.SelectedRtf);
        }

        using (var box = New())
        {
            r["exact/rtb/empty-rtf"] = Rtf(box.Rtf);
            r["exact/rtb/empty-selectedrtf"] = Rtf(box.SelectedRtf);
            box.Text = "one\r\ntwo\rthree\nfour";
            r["exact/rtb/newlines"] = $"{Esc(box.Text)} {box.TextLength} {box.Lines.Length} {box.GetFirstCharIndexFromLine(1)} {box.GetLineFromCharIndex(5)}";
            r["exact/rtb/modified-after-text"] = box.Modified.ToString();
            r["exact/rtb/rtf-plain"] = Rtf(box.Rtf);
        }

        using (var box = New("Hello world"))
        {
            box.Select(0, 5);
            box.SelectionColor = Color.Red;
            box.SelectionFont = new Font("Arial", 10, FontStyle.Bold);
            r["exact/rtb/color-back"] = C(box.SelectionColor);
            r["exact/rtb/modified-after-format"] = box.Modified.ToString();
            box.Select(0, 11);
            r["exact/rtb/mixed-color"] = C(box.SelectionColor);
            r["exact/rtb/mixed-bold"] = F(box.SelectionFont);
            r["exact/rtb/rtf-bold-red"] = Rtf(box.Rtf);
            box.Select(1, 2);
            r["exact/rtb/selectedrtf"] = Rtf(box.SelectedRtf);
            box.Select(4, 3);
            box.SelectionFont = new Font("Courier New", 12);
            box.Select(0, 11);
            r["exact/rtb/mixed-faces"] = F(box.SelectionFont);
            box.Select(4, 3);
            box.SelectionFont = new Font("Arial", 12);
            box.Select(0, 11);
            r["exact/rtb/mixed-sizes"] = F(box.SelectionFont);
            r["exact/rtb/rtf-mixed"] = Rtf(box.Rtf);
            // Text replacing formatted text: which format it takes.
            box.Text = "new text";
            box.Select(0, 3);
            r["exact/rtb/text-after-format"] = C(box.SelectionColor) + " " + F(box.SelectionFont);
            r["exact/rtb/back-default"] = C(box.SelectionBackColor);
            r["exact/rtb/offset-default"] = box.SelectionCharOffset.ToString();
        }

        using (var box = New("log: "))
        {
            box.SelectionStart = box.TextLength;
            box.SelectionColor = Color.Red;
            box.AppendText("error");
            box.Select(5, 5);
            r["exact/rtb/append-after-colour"] = C(box.SelectionColor);
            box.Select(box.TextLength, 0);
            box.SelectedText = "!";
            box.Select(10, 1);
            r["exact/rtb/selectedtext-after-colour"] = C(box.SelectionColor);
        }

        using (var box = New("one\ntwo\nthree"))
        {
            string Aligns()
            {
                var parts = new List<string>();
                int start = 0;
                foreach (var line in box.Text.Split('\n'))
                {
                    box.Select(start, 0);
                    parts.Add(box.SelectionAlignment.ToString());
                    start += line.Length + 1;
                }
                return string.Join(",", parts);
            }
            box.Select(4, 0);
            box.SelectionAlignment = HorizontalAlignment.Center;
            r["exact/rtb/para-center"] = Aligns();
            box.Select(5, 0);
            box.SelectedText = "\n";
            r["exact/rtb/para-split"] = Esc(box.Text) + " " + Aligns();
            box.Select(3, 1);
            box.SelectedText = "";
            r["exact/rtb/para-join-next-centered"] = Esc(box.Text) + " " + Aligns();
            box.Select(4, 1);
            box.SelectedText = "";
            r["exact/rtb/para-join-last"] = Esc(box.Text) + " " + Aligns();
            box.SelectAll();
            r["exact/rtb/para-mixed"] = box.SelectionAlignment.ToString();
            box.Select(0, 0);
            box.SelectionIndent = 30;
            box.SelectionHangingIndent = 15;
            box.SelectionRightIndent = 10;
            box.SelectionTabs = new[] { 40, 80 };
            box.SelectionBullet = true;
            r["exact/rtb/para-props"] = $"{box.SelectionIndent} {box.SelectionHangingIndent} {box.SelectionRightIndent} {string.Join(",", box.SelectionTabs)} {box.SelectionBullet}";
            r["exact/rtb/rtf-para"] = Rtf(box.Rtf);
        }

        using (var box = New("The cat sat on the Cat mat; category"))
        {
            r["exact/rtb/find"] = string.Join(",", new[]
            {
                box.Find("cat"), box.Find("Cat", RichTextBoxFinds.MatchCase), box.Find("cat", 20, RichTextBoxFinds.None),
                box.Find("cat", 20, RichTextBoxFinds.WholeWord), box.Find("cat", RichTextBoxFinds.Reverse),
                box.Find("cat", 0, 27, RichTextBoxFinds.Reverse | RichTextBoxFinds.WholeWord), box.Find("cat", 5, 5, RichTextBoxFinds.None),
                box.Find(""), box.Find(new[] { 'n', 'm' }), box.Find(new[] { 'm' }, 10), box.Find(new[] { 'm' }, 0, 10),
            });
            box.Select(0, 0);
            box.Find("sat", RichTextBoxFinds.NoHighlight);
            r["exact/rtb/find-nohighlight"] = box.SelectionStart + "," + box.SelectionLength;
            box.Find("mat");
            r["exact/rtb/find-selects"] = box.SelectionStart + "," + box.SelectionLength;
        }

        using (var box = New("keep this; edit"))
        {
            int raised = 0;
            box.Protected += (_, _) => raised++;
            box.Select(0, 9);
            box.SelectionProtected = true;
            box.Select(0, 4);
            box.SelectedText = "lose";
            string afterReplace = box.Text + " " + raised;
            box.SelectionColor = Color.Blue;
            string afterColour = C(box.SelectionColor) + " " + raised;
            box.Select(9, 0);
            box.SelectedText = ",";
            box.Select(box.TextLength, 0);
            box.AppendText("!");
            box.Text = "replaced";
            r["exact/rtb/protected"] = $"{afterReplace} | {afterColour} | {box.Text} {raised}";
        }

        using (var box = New("abc"))
        {
            // The action names are WinForms' resources, in the UI language: reported, not compared.
            r["exact/rtb/undo-after-text"] = $"{box.CanUndo} {box.UndoActionName}";
            box.Select(3, 0);
            box.SelectedText = "d";
            r["exact/rtb/undo-after-selectedtext"] = box.CanUndo.ToString();
            r["info/rtb/undo-name-selectedtext"] = box.UndoActionName;
            box.Select(0, 1);
            box.SelectionColor = Color.Red;
            r["exact/rtb/undo-after-format"] = box.CanUndo.ToString();
            r["info/rtb/undo-name-format"] = box.UndoActionName;
            box.Undo();
            box.Select(0, 1);
            r["exact/rtb/undo-format"] = C(box.SelectionColor) + " " + box.Text + " " + box.CanRedo;
            r["info/rtb/redo-name-format"] = box.RedoActionName;
        }

        using (var box = New("[ ]"))
        {
            box.Select(1, 1);
            box.SelectedRtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}{\colortbl ;\red255\green0\blue0;}\pard\cf1\f0 red}";
            box.Select(0, 0);
            box.SelectedRtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\pard\qr\f0 first\par}";
            string text = Esc(box.Text);
            box.Select(0, 0);
            string first = box.SelectionAlignment.ToString();
            box.Select(8, 1);
            r["exact/rtb/selectedrtf-insert"] = $"{text} {first} {C(box.SelectionColor)}";
            box.Rtf = @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil\fcharset0 Calibri;}{\f1\fnil\fcharset204 Calibri;}{\f2\fnil\fcharset2 Symbol;}}"
                + "\r\n" + @"{\colortbl ;\red0\green77\blue187;}\viewkind4\uc1 \pard\qc\cf1\b\f0\fs28 Report\cf0\b0\fs22\par"
                + "\r\n" + @"\pard{\pntext\f2\'B7\tab}{\*\pn\pnlvlblt\pnf2\pnindent0{\pntxtb\'B7}}\fi-360\li720\f1\'cf\'f0\'e8\'e2\'e5\'f2\f0\par"
                + "\r\n" + @"{\pntext\f2\'B7\tab}" + "\\" + @"u8364?\~5 \{x\}\line next\par" + "\r\n" + @"\pard\i end\i0\par" + "\r\n}";
            string read = Esc(box.Text);
            box.Select(8, 0);
            r["exact/rtb/rtf-read"] = $"{read} {box.Modified} {box.SelectionBullet} {box.SelectionIndent} {box.SelectionHangingIndent}";
            box.Select(0, 6);
            r["exact/rtb/rtf-read-title"] = $"{box.SelectionAlignment} {C(box.SelectionColor)} {F(box.SelectionFont)}";
            // Characters outside ASCII: RichEdit writes \'hh in the font's code page, NetForms \uN? (decision 124).
            r["info/rtb/rtf-rewritten"] = Rtf(box.Rtf);
            box.Rtf = "";
            r["exact/rtb/rtf-cleared"] = Esc(box.Text) + " " + box.Modified;
            string error;
            try
            {
                box.Rtf = "plain";
                error = "none";
            }
            catch (Exception e)
            {
                error = e.GetType().Name;
            }
            r["exact/rtb/rtf-not-rtf"] = error;
        }

        using (var box = New("abc"))
        {
            r["exact/rtb/positions"] = $"{box.GetPositionFromCharIndex(-1)} {box.GetPositionFromCharIndex(4)} {box.GetCharIndexFromPosition(new Point(190, 5))} {box.GetCharIndexFromPosition(new Point(190, 90))}";
            box.ZoomFactor = 1.2345f;
            r["exact/rtb/zoom"] = box.ZoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture);
            r["text/rtb/position-a"] = box.GetPositionFromCharIndex(0).X + "," + box.GetPositionFromCharIndex(0).Y;
        }

        using (var box = New())
        {
            box.SelectedText = "typed";
            box.SelectAll();
            r["exact/rtb/default-format"] = F(box.SelectionFont) + " " + F(box.Font);
            box.Font = new Font("Arial", 12);
            r["exact/rtb/font-with-text"] = F(box.SelectionFont) + " " + F(box.Font);
            box.Text = "";
            box.Font = new Font("Arial", 11);
            box.SelectedText = "again";
            box.SelectAll();
            r["exact/rtb/font-without-text"] = F(box.SelectionFont) + " " + F(box.Font);
            box.ForeColor = Color.Green;
            r["exact/rtb/forecolor-all"] = C(box.SelectionColor);
        }

        using (var box = New("one\ntwo"))
        {
            box.Select(0, 0);
            box.SelectionAlignment = HorizontalAlignment.Center;
            box.SelectionIndent = 20;
            box.SelectionHangingIndent = -10;
            box.SelectionRightIndent = 5;
            box.SelectionTabs = new[] { 30 };
            box.Select(4, 0);
            box.SelectionAlignment = HorizontalAlignment.Right;
            box.SelectionBullet = true;
            box.Select(0, 3);
            box.SelectionFont = new Font("Arial", 10, FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout | FontStyle.Bold);
            box.SelectionBackColor = Color.Yellow;
            box.SelectionCharOffset = 4;
            box.SelectionProtected = true;
            r["exact/rtb/rtf-props-order"] = Rtf(box.Rtf);
            box.Select(4, 0);
            r["exact/rtb/bullet-hanging"] = box.SelectionHangingIndent + " " + box.SelectionIndent;
            box.Text = "reset";
            box.Select(0, 0);
            r["exact/rtb/text-resets-para"] = $"{box.SelectionAlignment} {box.SelectionIndent} {box.SelectionBullet} {box.SelectionProtected}";
        }

        using (var box = New("x"))
        {
            box.Rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\pard\f0\fs20 a\line b\par}";
            r["exact/rtb/line-is-soft-break"] = Esc(box.Text) + " " + box.Lines.Length + " " + box.GetLineFromCharIndex(3);
            box.Rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\pard\f0\fs20 a\par b\par\par}";
            r["exact/rtb/final-par"] = Esc(box.Text);
            box.Rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\pard\f0 no size}";
            box.SelectAll();
            r["exact/rtb/rtf-without-fs"] = F(box.SelectionFont);
            box.Rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}{\f1 Courier New;}}\pard\f1\fs30 x\plain y\par}";
            box.Select(1, 1);
            r["exact/rtb/rtf-plain-resets"] = F(box.SelectionFont);
        }

        using (var box = New("Строка\nline"))
        {
            using var plain = new System.IO.MemoryStream();
            box.SaveFile(plain, RichTextBoxStreamType.UnicodePlainText);
            r["exact/rtb/save-unicode"] = Convert.ToHexString(plain.ToArray());
            var utf8 = new System.IO.MemoryStream(new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0x0D, 0x0A, 0x42 });
            box.LoadFile(utf8, RichTextBoxStreamType.PlainText);
            r["exact/rtb/load-utf8-bom"] = Esc(box.Text) + " " + box.Modified;
        }
    }

    /// <summary>MDI: a child is a control of the parent's client area, not a window of its own.</summary>
    private static void Mdi(SortedDictionary<string, string> r)
    {
        using var parent = new Form { ClientSize = new Size(600, 400), IsMdiContainer = true };
        r["exact/mdi/is-container"] = parent.IsMdiContainer.ToString();
        r["exact/mdi/children-empty"] = parent.MdiChildren.Length.ToString();
        r["exact/mdi/active-null"] = (parent.ActiveMdiChild == null).ToString();

        var child = new Form { MdiParent = parent, Text = "Child" };
        r["exact/mdi/is-child"] = child.IsMdiChild.ToString();
        r["exact/mdi/child-toplevel"] = (child.TopLevelControl == child).ToString();
        r["exact/mdi/children-one"] = parent.MdiChildren.Length.ToString();
        r["exact/mdi/child-parent-is-parent"] = ReferenceEquals(child.MdiParent, parent).ToString();
        r["exact/mdi/child-findform"] = ReferenceEquals(child.FindForm(), child).ToString();

        child.Bounds = new Rectangle(10, 20, 300, 200);
        r["exact/mdi/child-bounds"] = B(child.Bounds);
        r["info/mdi/child-clientsize"] = S(child.ClientSize);
        r["info/mdi/child-frame"] = (child.Width - child.ClientSize.Width) + "," + (child.Height - child.ClientSize.Height);

        child.MdiParent = null;
        r["exact/mdi/detached"] = child.IsMdiChild.ToString();
        r["exact/mdi/children-after-detach"] = parent.MdiChildren.Length.ToString();
        child.Dispose();
    }

    private static string B(Rectangle b) => $"{b.X},{b.Y},{b.Width},{b.Height}";
    private static string S(Size s) => $"{s.Width},{s.Height}";

    private static void Defaults(SortedDictionary<string, string> r)
    {
        r["exact/defaults/font"] = Control.DefaultFont.Name + " " + Control.DefaultFont.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);
        r["exact/defaults/button-size"] = S(new Button().Size);
        r["exact/defaults/label-size"] = S(new Label().Size);
        r["exact/defaults/label-tabstop"] = new Label().TabStop.ToString();
        r["exact/defaults/control-margin"] = new Control().Margin.ToString();
        r["exact/defaults/label-margin"] = new Label().Margin.ToString();
        r["exact/defaults/button-textalign"] = new Button().TextAlign.ToString();
        r["exact/defaults/anchor"] = new Control().Anchor.ToString();
        r["info/defaults/form-clientsize"] = S(new Form().ClientSize);
        r["info/defaults/backcolor"] = Control.DefaultBackColor.ToArgb().ToString("X8");
        r["text/defaults/font-height"] = Control.DefaultFont.Height.ToString();
        r["text/defaults/measure-height"] = TextRenderer.MeasureText("Ag", Control.DefaultFont).Height.ToString();
        r["text/defaults/measure-nopadding"] = S(TextRenderer.MeasureText("Ag", Control.DefaultFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding));
    }

    private static void DesignerForm(SortedDictionary<string, string> r)
    {
        using var form = new HelloForms.MainForm();
        var button = form.Controls["button1"]!;
        var label = form.Controls["label1"]!;
        r["exact/designer/clientsize"] = S(form.ClientSize);
        r["exact/designer/button"] = B(button.Bounds);
        r["exact/designer/label-location"] = label.Location.X + "," + label.Location.Y;
        r["text/designer/label-size"] = S(label.Size);
        r["exact/designer/zorder"] = form.Controls[0].Name + "," + form.Controls[1].Name;
        r["exact/designer/tabindex"] = button.TabIndex + "," + label.TabIndex;

        form.ClientSize = new Size(484, 240);
        r["exact/designer/button-after-resize"] = B(button.Bounds);
        r["exact/designer/label-after-resize"] = B(new Rectangle(label.Location, new Size(0, 0)));

        form.ClientSize = new Size(284, 140);
        r["exact/designer/button-after-restore"] = B(button.Bounds);
    }

    private static Control Child(int x, int y, int w, int h, DockStyle dock) =>
        new Control { Bounds = new Rectangle(x, y, w, h), Dock = dock };

    private static void Docking(SortedDictionary<string, string> r)
    {
        // Same matrix as tests/NetForms.Tests/DockLayoutGoldenTests.cs; children added in z-order.
        void Case(string name, int cw, int ch, params (string n, Control c)[] children)
        {
            using var parent = new Control { Size = new Size(cw, ch) };
            parent.SuspendLayout();
            foreach (var (_, c) in children) parent.Controls.Add(c);
            parent.ResumeLayout(false);
            parent.PerformLayout();
            foreach (var (n, c) in children) r[$"exact/dock/{name}/{n}"] = B(c.Bounds);
        }

        Case("top-bottom-fill", 600, 400, ("body", Child(5, 5, 10, 10, DockStyle.Fill)), ("status", Child(0, 0, 100, 24, DockStyle.Bottom)), ("toolbar", Child(0, 0, 100, 34, DockStyle.Top)));
        Case("left-right-fill", 600, 400, ("body", Child(0, 0, 10, 10, DockStyle.Fill)), ("aside", Child(0, 0, 100, 999, DockStyle.Right)), ("nav", Child(0, 0, 150, 999, DockStyle.Left)));
        Case("corner-left-first", 500, 300, ("head", Child(0, 0, 0, 40, DockStyle.Top)), ("side", Child(0, 0, 120, 0, DockStyle.Left)));
        Case("corner-top-first", 500, 300, ("side", Child(0, 0, 120, 0, DockStyle.Left)), ("head", Child(0, 0, 0, 40, DockStyle.Top)));
        Case("two-fills", 400, 200, ("second", Child(3, 4, 20, 20, DockStyle.Fill)), ("first", Child(0, 0, 10, 10, DockStyle.Fill)));
        Case("dock-and-loose", 400, 300, ("loose", Child(20, 20, 80, 25, DockStyle.None)), ("top", Child(0, 0, 0, 50, DockStyle.Top)));
        Case("huge-left", 200, 100, ("body", Child(0, 0, 10, 10, DockStyle.Fill)), ("huge", Child(0, 0, 300, 0, DockStyle.Left)));

        using (var parent = new Control { Size = new Size(600, 400) })
        {
            var top = new Control { Height = 34, Dock = DockStyle.Top, Visible = false };
            var body = new Control { Dock = DockStyle.Fill };
            parent.Controls.Add(body);
            parent.Controls.Add(top);
            parent.PerformLayout();
            r["exact/dock/invisible-top/body-before"] = B(body.Bounds);
            top.Visible = true;
            r["exact/dock/invisible-top/body-after"] = B(body.Bounds);
        }
    }

    private static void Anchoring(SortedDictionary<string, string> r)
    {
        static Control Column(int x, int y, int w, int h, AnchorStyles a) => new Control { Bounds = new Rectangle(x, y, w, h), Anchor = a };

        using (var parent = new Control { Size = new Size(1076, 800) })
        {
            var left = Column(12, 40, 520, 260, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            var right = Column(544, 40, 520, 260, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            parent.Controls.Add(left);
            parent.Controls.Add(right);
            parent.Size = new Size(1376, 800);
            r["exact/anchor/both-lr/left"] = B(left.Bounds);
            r["exact/anchor/both-lr/right"] = B(right.Bounds);
        }

        using (var parent = new Control { Size = new Size(1076, 760) })
        {
            var strip = new Control { Bounds = new Rectangle(12, 544, 1052, 200), Dock = DockStyle.Bottom };
            var box = Column(12, 40, 520, 260, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom);
            parent.Controls.Add(box);
            parent.Controls.Add(strip);
            parent.Size = new Size(1076, 1000);
            r["exact/anchor/strip-and-box/strip"] = B(strip.Bounds);
            r["exact/anchor/strip-and-box/box"] = B(box.Bounds);
        }

        using (var parent = new Control { Size = new Size(300, 200) })
        {
            var none = Column(100, 50, 40, 20, AnchorStyles.None);
            var br = Column(197, 105, 75, 23, AnchorStyles.Bottom | AnchorStyles.Right);
            var all = Column(10, 10, 100, 50, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right);
            parent.Controls.Add(none);
            parent.Controls.Add(br);
            parent.Controls.Add(all);
            parent.Size = new Size(401, 301);
            r["exact/anchor/none-odd/none"] = B(none.Bounds);
            r["exact/anchor/none-odd/br"] = B(br.Bounds);
            r["exact/anchor/none-odd/all"] = B(all.Bounds);
            parent.Size = new Size(300, 200);
            r["exact/anchor/none-odd/none-restored"] = B(none.Bounds);
            parent.Size = new Size(50, 30);
            r["exact/anchor/none-odd/all-shrunk"] = B(all.Bounds);
        }

        using (var parent = new Control())
        {
            parent.SuspendLayout();
            var button = Column(197, 105, 75, 23, AnchorStyles.Bottom | AnchorStyles.Right);
            parent.Controls.Add(button);
            parent.ClientSize = new Size(284, 140);
            parent.ResumeLayout(false);
            parent.PerformLayout();
            parent.ClientSize = new Size(384, 240);
            r["exact/anchor/resume-false/button"] = B(button.Bounds);
        }

        using (var parent = new Control { Size = new Size(300, 200), Padding = new Padding(10) })
        {
            var fill = new Control { Dock = DockStyle.Fill };
            var br = Column(200, 150, 50, 20, AnchorStyles.Bottom | AnchorStyles.Right);
            parent.Controls.Add(br);
            parent.Controls.Add(fill);
            parent.Size = new Size(400, 300);
            r["exact/anchor/padding/fill"] = B(fill.Bounds);
            r["exact/anchor/padding/br"] = B(br.Bounds);
        }
    }

    private static void TextMetrics(SortedDictionary<string, string> r)
    {
        var font = Control.DefaultFont;
        r["text/font/height"] = font.Height.ToString();
        foreach (var s in new[] { "label1", "Click me", "Hello, World!", "The quick brown fox jumps over the lazy dog" })
        {
            var key = s.Replace(' ', '_').Replace(',', '_').Replace('!', '_');
            r[$"text/measure/{key}"] = S(TextRenderer.MeasureText(s, font));
            r[$"text/measure-nopad/{key}"] = S(TextRenderer.MeasureText(s, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding));
        }
        var label = new Label { AutoSize = true, Text = "label1" };
        r["text/label/preferred"] = S(label.PreferredSize);
        var button = new Button { Text = "OK" };
        r["text/button/preferred"] = S(button.GetPreferredSize(Size.Empty));
    }

    private static void Controls(SortedDictionary<string, string> r)
    {
        r["exact/controls/checkbox-size"] = S(new CheckBox().Size);
        r["exact/controls/radiobutton-size"] = S(new RadioButton().Size);
        r["exact/controls/groupbox-size"] = S(new GroupBox().Size);
        r["exact/controls/panel-size"] = S(new Panel().Size);
        r["exact/controls/listbox-size"] = S(new ListBox().Size);
        r["exact/controls/combobox-size"] = S(new ComboBox().Size);
        r["exact/controls/numericupdown-size"] = S(new NumericUpDown().Size);
        r["exact/controls/trackbar-size"] = S(new TrackBar().Size);
        r["exact/controls/progressbar-size"] = S(new ProgressBar().Size);
        r["exact/controls/tabcontrol-size"] = S(new TabControl().Size);
        r["exact/controls/splitcontainer-size"] = S(new SplitContainer().Size);
        r["exact/controls/picturebox-size"] = S(new PictureBox().Size);
        r["exact/controls/vscrollbar-width"] = new VScrollBar().Width.ToString();
        r["exact/controls/hscrollbar-height"] = new HScrollBar().Height.ToString();
        r["text/controls/textbox-size"] = S(new TextBox().Size);
        r["text/controls/textbox-preferred-height"] = new TextBox().PreferredHeight.ToString();
        r["text/controls/combobox-preferred-height"] = new ComboBox().PreferredHeight.ToString();
        r["text/controls/listbox-itemheight"] = new ListBox().ItemHeight.ToString();
        r["text/controls/checkbox-preferred"] = S(new CheckBox { Text = "Check me" }.GetPreferredSize(Size.Empty));
        r["text/controls/radiobutton-preferred"] = S(new RadioButton { Text = "Pick me" }.GetPreferredSize(Size.Empty));
        // Probes for the CheckBox/RadioButton preferred-size formula (open question since decision 42).
        r["info/check/font"] = new CheckBox().Font.Name + " " + new CheckBox().Font.SizeInPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) + " h" + new CheckBox().Font.Height;
        foreach (var text in new[] { "", "X", "Check me", "A much longer check box text" })
        {
            r["info/check/pref-" + text.Length] = S(new CheckBox { Text = text }.GetPreferredSize(Size.Empty));
            r["info/radio/pref-" + text.Length] = S(new RadioButton { Text = text }.GetPreferredSize(Size.Empty));
            r["info/check/measure-" + text.Length] = S(TextRenderer.MeasureText(text, new CheckBox().Font));
        }
        using (var segoe = new Font("Segoe UI", 9f))
        {
            r["info/check/segoe-pref"] = S(new CheckBox { Text = "Check me", Font = segoe }.GetPreferredSize(Size.Empty));
            r["info/check/segoe-measure"] = S(TextRenderer.MeasureText("Check me", segoe));
            r["info/check/segoe-label"] = S(new Label { Text = "Check me", Font = segoe }.GetPreferredSize(Size.Empty));
        }
        r["info/check/label-pref"] = S(new Label { Text = "Check me" }.GetPreferredSize(Size.Empty));
        foreach (var text in new[] { "", "X", "Check me", "A much longer check box text" })
        {
            r["info/check/gdi-pref-" + text.Length] = S(new CheckBox { Text = text, UseCompatibleTextRendering = false }.GetPreferredSize(Size.Empty));
            r["info/radio/gdi-pref-" + text.Length] = S(new RadioButton { Text = text, UseCompatibleTextRendering = false }.GetPreferredSize(Size.Empty));
        }
        r["info/check/gdi-label"] = S(new Label { Text = "Check me", UseCompatibleTextRendering = false }.GetPreferredSize(Size.Empty));
        foreach (var text in new[] { "X", "Check me", "A much longer check box text" })
        {
            var f = new CheckBox().Font;
            r["info/check/nopad-" + text.Length] = S(TextRenderer.MeasureText(text, f, Size.Empty, TextFormatFlags.NoPadding));
            r["info/check/wb-" + text.Length] = S(TextRenderer.MeasureText(text, f, Size.Empty, TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl));
            r["info/check/wbnp-" + text.Length] = S(TextRenderer.MeasureText(text, f, Size.Empty, TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding));
            r["info/check/wbmax-" + text.Length] = S(TextRenderer.MeasureText(text, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl));
        }
        r["info/check/pref-flat"] = S(new CheckBox { Text = "Check me", FlatStyle = FlatStyle.Flat }.GetPreferredSize(Size.Empty));
        r["info/check/pref-autosize"] = S(new CheckBox { Text = "Check me", AutoSize = true }.Size);

        using (var group = new GroupBox { Size = new Size(200, 100), Padding = new Padding(3) })
        {
            r["text/controls/groupbox-display"] = B(group.DisplayRectangle);
        }
        using (var tabs = new TabControl { Size = new Size(200, 100) })
        {
            tabs.TabPages.Add("One");
            r["text/controls/tabcontrol-display"] = B(tabs.DisplayRectangle);
            r["text/controls/tabcontrol-page"] = B(tabs.TabPages[0].Bounds);
        }
        using (var split = new SplitContainer { Size = new Size(300, 100), SplitterDistance = 100 })
        {
            r["exact/controls/split-panel1"] = B(split.Panel1.Bounds);
            r["exact/controls/split-panel2"] = B(split.Panel2.Bounds);
            split.FixedPanel = FixedPanel.Panel2;
            split.Width = 400;
            r["exact/controls/split-fixed2-distance"] = split.SplitterDistance.ToString();
            split.FixedPanel = FixedPanel.Panel1;
            split.Width = 500;
            r["exact/controls/split-fixed1-distance"] = split.SplitterDistance.ToString();
            split.SplitterDistance = 1000;
            r["exact/controls/split-clamped"] = split.SplitterDistance.ToString();
        }
        using (var list = new ListBox())
        {
            list.Items.AddRange(new object[] { "b", "a", "c" });
            list.Sorted = true;
            r["exact/controls/listbox-sorted"] = string.Join(",", list.Items.Cast<object>());
            list.SelectedIndex = 1;
            r["exact/controls/listbox-text"] = list.Text;
        }
        using (var text = new TextBox { Text = "line" })
        {
            text.AppendText("\r\nmore");
            r["exact/controls/textbox-size"] = S(text.Size);
            r["exact/controls/textbox-preferred"] = text.PreferredHeight.ToString();
            r["exact/controls/textbox-lines"] = text.Lines.Length.ToString();
            r["exact/controls/textbox-text"] = text.Text.Replace("\r\n", "|").Replace("\n", "|");
            text.Multiline = true;
            text.Text = "a\r\nb";
            r["exact/controls/textbox-multiline-lines"] = string.Join("/", text.Lines);
            text.SelectionStart = 1;
            text.SelectionLength = 3;
            r["exact/controls/textbox-selected"] = text.SelectedText.Replace("\r\n", "|");
        }
        using (var spin = new NumericUpDown { Maximum = 5, Increment = 2 })
        {
            spin.UpButton();
            spin.UpButton();
            spin.UpButton();
            r["exact/controls/numeric-clamped"] = spin.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        using (var bar = new TrackBar { Orientation = Orientation.Vertical })
        {
            r["exact/controls/trackbar-vertical-size"] = S(bar.Size);
        }
    }

    /// <summary>
    /// ToolStrip, MenuStrip, StatusStrip and drop-down menus: the defaults and the item geometry.
    /// Sizes that come out of text measurement are under "text/"; the rest must match exactly.
    /// </summary>
    private static void Strips(SortedDictionary<string, string> r)
    {
        using (var strip = new ToolStrip())
        {
            r["exact/toolstrip/size"] = S(strip.Size);
            r["exact/toolstrip/dock"] = strip.Dock.ToString();
            r["exact/toolstrip/padding"] = strip.Padding.ToString();
            r["exact/toolstrip/gripstyle"] = strip.GripStyle.ToString();
            r["exact/toolstrip/gripmargin"] = strip.GripMargin.ToString();
            r["exact/toolstrip/imagescalingsize"] = S(strip.ImageScalingSize);
            r["exact/toolstrip/canoverflow"] = strip.CanOverflow.ToString();
            r["exact/toolstrip/tabstop"] = strip.TabStop.ToString();
            r["exact/toolstrip/layoutstyle"] = strip.LayoutStyle.ToString();
            r["exact/toolstrip/orientation"] = strip.Orientation.ToString();

            var first = new ToolStripButton("New");
            var second = new ToolStripButton("Open");
            var separator = new ToolStripSeparator();
            strip.Items.AddRange(new ToolStripItem[] { first, separator, second });
            strip.Size = new Size(400, 25);
            strip.PerformLayout();

            r["exact/toolstrip/item-margin"] = first.Margin.ToString();
            r["exact/toolstrip/grip-rect"] = B(strip.GripRectangle);
            r["exact/toolstrip/first-left"] = first.Bounds.X.ToString();
            r["exact/toolstrip/first-top"] = first.Bounds.Y.ToString();
            r["exact/toolstrip/first-height"] = first.Bounds.Height.ToString();
            r["text/toolstrip/first-width"] = first.Bounds.Width.ToString();
            r["exact/toolstrip/separator-size"] = S(separator.Size);
            r["text/toolstrip/second-left"] = second.Bounds.X.ToString();
        }

        using (var menu = new MenuStrip())
        {
            r["exact/menustrip/size"] = S(menu.Size);
            r["exact/menustrip/dock"] = menu.Dock.ToString();
            r["exact/menustrip/padding"] = menu.Padding.ToString();
            r["exact/menustrip/gripstyle"] = menu.GripStyle.ToString();
            r["exact/menustrip/canoverflow"] = menu.CanOverflow.ToString();

            var file = new ToolStripMenuItem("&File");
            var edit = new ToolStripMenuItem("&Edit");
            menu.Items.AddRange(new ToolStripItem[] { file, edit });
            menu.Size = new Size(400, 24);
            menu.PerformLayout();

            r["exact/menustrip/item-margin"] = file.Margin.ToString();
            r["exact/menustrip/item-padding"] = file.Padding.ToString();
            r["exact/menustrip/item-displaystyle"] = file.DisplayStyle.ToString();
            r["exact/menustrip/file-left"] = file.Bounds.X.ToString();
            r["exact/menustrip/file-top"] = file.Bounds.Y.ToString();
            r["exact/menustrip/file-height"] = file.Bounds.Height.ToString();
            r["text/menustrip/file-width"] = file.Bounds.Width.ToString();
            r["text/menustrip/edit-left"] = edit.Bounds.X.ToString();
            r["text/menustrip/measure-file"] = TextRenderer.MeasureText("File", menu.Font).Width.ToString();
            r["text/menustrip/preferred-file"] = S(file.GetPreferredSize(Size.Empty));

            // A drop-down item takes its defaults from where it lives, not from where it was made.
            var open = new ToolStripMenuItem("Open") { ShortcutKeys = Keys.Control | Keys.O };
            var sub = new ToolStripMenuItem("Recent");
            sub.DropDownItems.Add(new ToolStripMenuItem("a.txt"));
            var separator = new ToolStripSeparator();
            file.DropDownItems.AddRange(new ToolStripItem[] { open, separator, sub });

            r["exact/menu/dropdown-item-padding"] = open.Padding.ToString();
            r["exact/menu/dropdown-item-margin"] = open.Margin.ToString();
            r["exact/menu/dropdown-item-displaystyle"] = open.DisplayStyle.ToString();
            r["exact/menu/shortcut-text"] = open.ShortcutKeyDisplayString ?? open.ShortcutKeys.ToString();
            r["exact/menu/has-dropdown-items"] = sub.HasDropDownItems.ToString();
            r["info/menu/dropdown-padding"] = file.DropDown.Padding.ToString();
            r["text/menu/item-height"] = open.GetPreferredSize(Size.Empty).Height.ToString();
            r["info/menu/item-preferred"] = S(open.GetPreferredSize(Size.Empty));
            r["info/menu/dropdown-preferred"] = S(file.DropDown.GetPreferredSize(Size.Empty));
            r["text/menu/separator-height"] = separator.GetPreferredSize(Size.Empty).Height.ToString();
        }

        using (var list = new ListView())
        {
            r["exact/listview/size"] = S(list.Size);
            r["exact/listview/view"] = list.View.ToString();
            r["exact/listview/borderstyle"] = list.BorderStyle.ToString();
            r["exact/listview/headerstyle"] = list.HeaderStyle.ToString();
            r["exact/listview/multiselect"] = list.MultiSelect.ToString();
            r["exact/listview/hideselection"] = list.HideSelection.ToString();
            r["exact/listview/labelwrap"] = list.LabelWrap.ToString();
            r["exact/listview/scrollable"] = list.Scrollable.ToString();
            r["exact/listview/showgroups"] = list.ShowGroups.ToString();
            r["exact/listview/sorting"] = list.Sorting.ToString();
            r["exact/listview/alignment"] = list.Alignment.ToString();
            r["exact/listview/backcolor"] = list.BackColor.ToArgb().ToString("X8");
            r["info/listview/tilesize"] = S(list.TileSize);

            var column = list.Columns.Add("Name");
            r["exact/listview/column-width"] = column.Width.ToString();
            r["exact/listview/column-align"] = column.TextAlign.ToString();
            r["exact/listview/column-index"] = column.Index.ToString();

            var item = list.Items.Add("Item");
            item.SubItems.Add("Sub");
            r["exact/listview/item-subitems"] = item.SubItems.Count.ToString();
            r["exact/listview/item-imageindex"] = item.ImageIndex.ToString();
            r["exact/listview/item-usestyle"] = item.UseItemStyleForSubItems.ToString();
            r["exact/listview/item-index"] = item.Index.ToString();
            r["exact/listview/selected-count"] = list.SelectedItems.Count.ToString();
            r["exact/listview/checked-count"] = list.CheckedItems.Count.ToString();
        }

        using (var propertyGrid = new PropertyGrid())
        {
            r["exact/propertygrid/size"] = S(propertyGrid.Size);
            r["exact/propertygrid/sort"] = propertyGrid.PropertySort.ToString();
            r["exact/propertygrid/helpvisible"] = propertyGrid.HelpVisible.ToString();
            r["exact/propertygrid/toolbarvisible"] = propertyGrid.ToolbarVisible.ToString();
            r["exact/propertygrid/selected-null"] = (propertyGrid.SelectedObject == null).ToString();
            r["exact/propertygrid/selected-count"] = propertyGrid.SelectedObjects.Length.ToString();

            propertyGrid.SelectedObject = new Button { Text = "OK" };
            r["exact/propertygrid/selected-after-set"] = (propertyGrid.SelectedObject != null).ToString();

            // WinForms selects the first property once an object is set; the root is reached
            // through its Parent chain (there is no public root accessor).
            var selected = propertyGrid.SelectedGridItem;
            r["exact/propertygrid/has-selected-item"] = (selected != null).ToString();
            if (selected != null)
            {
                r["exact/propertygrid/selected-type"] = selected.GridItemType.ToString();
                var root = selected;
                while (root.Parent != null) root = root.Parent;
                r["exact/propertygrid/root-type"] = root.GridItemType.ToString();
                r["info/propertygrid/root-children"] = root.GridItems.Count.ToString();
            }
        }

        using (var tree = new TreeView())
        {
            r["exact/treeview/size"] = S(tree.Size);
            r["exact/treeview/borderstyle"] = tree.BorderStyle.ToString();
            r["exact/treeview/indent"] = tree.Indent.ToString();
            r["exact/treeview/showlines"] = tree.ShowLines.ToString();
            r["exact/treeview/showplusminus"] = tree.ShowPlusMinus.ToString();
            r["exact/treeview/showrootlines"] = tree.ShowRootLines.ToString();
            r["exact/treeview/hideselection"] = tree.HideSelection.ToString();
            r["exact/treeview/fullrowselect"] = tree.FullRowSelect.ToString();
            r["exact/treeview/scrollable"] = tree.Scrollable.ToString();
            r["exact/treeview/pathseparator"] = tree.PathSeparator;
            r["exact/treeview/drawmode"] = tree.DrawMode.ToString();
            r["exact/treeview/backcolor"] = tree.BackColor.ToArgb().ToString("X8");
            r["text/treeview/itemheight"] = tree.ItemHeight.ToString();

            var root = tree.Nodes.Add("Root");
            var child = root.Nodes.Add("Child");
            var leaf = child.Nodes.Add("Leaf");
            r["exact/treeview/node-level"] = leaf.Level.ToString();
            r["exact/treeview/node-index"] = child.Index.ToString();
            r["exact/treeview/node-fullpath"] = leaf.FullPath;
            r["exact/treeview/node-count-deep"] = tree.GetNodeCount(true).ToString();
            r["exact/treeview/node-count-shallow"] = tree.GetNodeCount(false).ToString();
            r["exact/treeview/node-expanded"] = root.IsExpanded.ToString();
            root.Expand();
            r["exact/treeview/node-expanded-after"] = root.IsExpanded.ToString();
            r["exact/treeview/node-imageindex"] = root.ImageIndex.ToString();
        }

        using (var grid = new DataGridView())
        {
            r["exact/grid/size"] = S(grid.Size);
            r["exact/grid/selectionmode"] = grid.SelectionMode.ToString();
            r["exact/grid/editmode"] = grid.EditMode.ToString();
            r["exact/grid/autosizecolumnsmode"] = grid.AutoSizeColumnsMode.ToString();
            r["exact/grid/allowadd"] = grid.AllowUserToAddRows.ToString();
            r["exact/grid/allowdelete"] = grid.AllowUserToDeleteRows.ToString();
            r["exact/grid/multiselect"] = grid.MultiSelect.ToString();
            r["exact/grid/readonly"] = grid.ReadOnly.ToString();
            r["exact/grid/rowheadersvisible"] = grid.RowHeadersVisible.ToString();
            r["exact/grid/columnheadersvisible"] = grid.ColumnHeadersVisible.ToString();
            r["exact/grid/rowheaderswidth"] = grid.RowHeadersWidth.ToString();
            r["exact/grid/autogeneratecolumns"] = grid.AutoGenerateColumns.ToString();
            r["info/grid/columnheadersheight"] = grid.ColumnHeadersHeight.ToString();
            r["info/grid/rowtemplate-height"] = grid.RowTemplate.Height.ToString();

            grid.AllowUserToAddRows = false;
            grid.Columns.Add("Name", "Name");
            grid.Columns.Add("Age", "Age");
            r["exact/grid/column-width"] = grid.Columns[0].Width.ToString();
            r["exact/grid/column-minwidth"] = grid.Columns[0].MinimumWidth.ToString();
            r["exact/grid/column-fillweight"] = grid.Columns[0].FillWeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
            r["exact/grid/column-sortmode"] = grid.Columns[0].SortMode.ToString();
            r["exact/grid/column-index"] = grid.Columns[1].Index.ToString();
            r["exact/grid/column-celltype"] = grid.Columns[0].CellType?.Name ?? "<null>";

            grid.Rows.Add("Ada", 36);
            r["exact/grid/rows"] = grid.Rows.Count.ToString();
            r["exact/grid/cells-per-row"] = grid.Rows[0].Cells.Count.ToString();
            r["exact/grid/cell-value"] = grid.Rows[0].Cells[0].Value?.ToString() ?? "<null>";
            r["exact/grid/cell-formatted"] = grid.Rows[0].Cells[1].FormattedValue?.ToString() ?? "<null>";
            r["exact/grid/selected-cells"] = grid.SelectedCells.Count.ToString();
        }

        using (var status = new StatusStrip())
        {
            r["exact/statusstrip/size"] = S(status.Size);
            r["exact/statusstrip/dock"] = status.Dock.ToString();
            r["exact/statusstrip/padding"] = status.Padding.ToString();
            r["exact/statusstrip/layoutstyle"] = status.LayoutStyle.ToString();
            r["exact/statusstrip/sizinggrip"] = status.SizingGrip.ToString();

            var ready = new ToolStripStatusLabel("Ready");
            var spring = new ToolStripStatusLabel("Spring") { Spring = true };
            status.Items.AddRange(new ToolStripItem[] { ready, spring });
            status.Size = new Size(400, 22);
            status.PerformLayout();

            r["exact/statusstrip/label-margin"] = ready.Margin.ToString();
            r["exact/statusstrip/label-textalign"] = ready.TextAlign.ToString();
            r["exact/statusstrip/ready-left"] = ready.Bounds.X.ToString();
            r["text/statusstrip/spring-right"] = spring.Bounds.Right.ToString();
        }

        StripsAutoSize(r);
    }

    /// <summary>
    /// Strips start with AutoSize on and a docked strip takes its preferred size across the dock only.
    /// The form's font is set explicitly so both sides measure Segoe UI 9pt.
    /// </summary>
    private static void StripsAutoSize(SortedDictionary<string, string> r)
    {
        using var form = new Form { Font = new Font("Segoe UI", 9F), ClientSize = new Size(400, 300) };
        var tool = new ToolStrip();
        var button = new ToolStripButton("New");
        tool.Items.Add(button);
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        menu.Items.Add(file);
        var status = new StatusStrip();
        var ready = new ToolStripStatusLabel("Ready");
        status.Items.Add(ready);
        var empty = new ToolStrip();
        var floating = new ToolStrip { Dock = DockStyle.None, Location = new Point(10, 100) };
        floating.Items.Add(new ToolStripButton("Floating"));
        var comboStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        var combo = new ToolStripComboBox();
        var textItem = new ToolStripTextBox();
        comboStrip.Items.AddRange(new ToolStripItem[] { new ToolStripButton("New"), combo, textItem });
        form.Controls.AddRange(new Control[] { empty, floating, comboStrip, tool, menu, status });
        form.PerformLayout();
        comboStrip.PerformLayout();

        // The tab strip at Segoe UI 9pt: where a page goes (VS writes tabPage1.Location = (4, 24)).
        var tabs = new TabControl { Location = new Point(10, 150), Size = new Size(300, 120) };
        tabs.TabPages.Add(new TabPage("Controls"));
        tabs.TabPages.Add(new TabPage("Layout"));
        form.Controls.Add(tabs);
        form.PerformLayout();
        r["text/tabs/display-rect"] = B(tabs.DisplayRectangle);
        r["text/tabs/page-bounds"] = B(tabs.TabPages[0].Bounds);
        r["text/tabs/tab-rect-0"] = B(tabs.GetTabRect(0));
        r["text/tabs/tab-rect-1"] = B(tabs.GetTabRect(1));
        r["exact/tabs/itemsize"] = S(tabs.ItemSize);
        r["text/tabs/font-height"] = tabs.Font.Height.ToString();

        r["exact/stripautosize/combo-margin"] = combo.Margin.ToString();
        r["exact/stripautosize/textbox-margin"] = textItem.Margin.ToString();
        r["text/stripautosize/combostrip-size"] = S(comboStrip.Size);
        r["text/stripautosize/combo-bounds"] = B(combo.Bounds);
        r["text/stripautosize/combo-control-bounds"] = B(combo.Control.Bounds);
        r["text/stripautosize/textbox-bounds"] = B(textItem.Bounds);
        r["text/stripautosize/textbox-control-bounds"] = B(textItem.Control.Bounds);

        r["exact/stripautosize/toolstrip-autosize"] = tool.AutoSize.ToString();
        r["exact/stripautosize/statusstrip-autosize"] = status.AutoSize.ToString();
        r["exact/stripautosize/dropdown-autosize"] = new ContextMenuStrip().AutoSize.ToString();
        r["text/stripautosize/toolstrip-size"] = S(tool.Size);
        r["text/stripautosize/toolstrip-preferred"] = S(tool.GetPreferredSize(new Size(400, 1)));
        r["text/stripautosize/button-preferred"] = S(button.GetPreferredSize(Size.Empty));
        r["text/stripautosize/menustrip-size"] = S(menu.Size);
        r["text/stripautosize/menuitem-preferred"] = S(file.GetPreferredSize(Size.Empty));
        r["text/stripautosize/statusstrip-size"] = S(status.Size);
        r["text/stripautosize/statusstrip-preferred"] = S(status.GetPreferredSize(new Size(400, 1)));
        r["text/stripautosize/statuslabel-preferred"] = S(ready.GetPreferredSize(Size.Empty));
        r["text/stripautosize/statuslabel-bounds"] = B(ready.Bounds);
        r["text/stripautosize/empty-size"] = S(empty.Size);
        r["text/stripautosize/floating-size"] = S(floating.Size);
        r["text/stripautosize/toolstrip-top"] = tool.Top.ToString();
        r["text/stripautosize/menustrip-top"] = menu.Top.ToString();
        r["text/stripautosize/statusstrip-top"] = status.Top.ToString();
    }

    private static void LayoutPanels(SortedDictionary<string, string> r)
    {
        static Control Box(int w, int h) => new Control { Size = new Size(w, h), Margin = new Padding(3) };

        using (var flow = new FlowLayoutPanel { Size = new Size(100, 200) })
        {
            var a = Box(40, 20);
            var b = Box(40, 30);
            var c = Box(40, 20);
            var bottom = Box(20, 10);
            bottom.Anchor = AnchorStyles.Bottom;
            flow.Controls.AddRange(new[] { a, b, c, bottom });
            flow.PerformLayout();
            r["exact/flow/a"] = B(a.Bounds);
            r["exact/flow/b"] = B(b.Bounds);
            r["exact/flow/c"] = B(c.Bounds);
            r["exact/flow/bottom-anchored"] = B(bottom.Bounds);
            flow.FlowDirection = FlowDirection.TopDown;
            flow.PerformLayout();
            r["exact/flow/topdown-c"] = B(c.Bounds);
            flow.FlowDirection = FlowDirection.RightToLeft;
            flow.PerformLayout();
            r["exact/flow/rtl-a"] = B(a.Bounds);
            flow.SetFlowBreak(a, true);
            flow.FlowDirection = FlowDirection.LeftToRight;
            flow.PerformLayout();
            r["exact/flow/break-b"] = B(b.Bounds);
            r["exact/flow/break-a"] = B(a.Bounds);
            r["exact/flow/break-c"] = B(c.Bounds);
            r["exact/flow/break-bottom"] = B(bottom.Bounds);
        }

        // The row a flow break ends: is its height its own, or does the next control count? Here the
        // control after the break is shorter than the one before it, and a third row follows.
        using (var flow = new FlowLayoutPanel { Size = new Size(200, 300) })
        {
            var tall = Box(40, 50);
            var small = Box(40, 10);
            var wide = Box(180, 20);
            var last = Box(40, 15);
            flow.Controls.AddRange(new[] { tall, small, wide, last });
            flow.SetFlowBreak(tall, true);
            flow.PerformLayout();
            r["exact/flow/break2-tall"] = B(tall.Bounds);
            r["exact/flow/break2-small"] = B(small.Bounds);
            r["exact/flow/break2-wide"] = B(wide.Bounds);
            r["exact/flow/break2-last"] = B(last.Bounds);
            flow.SetFlowBreak(tall, false);
            flow.SetFlowBreak(small, true);
            flow.PerformLayout();
            r["exact/flow/break3-small"] = B(small.Bounds);
            r["exact/flow/break3-wide"] = B(wide.Bounds);
        }

        using (var table = new TableLayoutPanel { Size = new Size(300, 100), ColumnCount = 3, RowCount = 1 })
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
            var a = Box(10, 10);
            var b = Box(10, 10);
            var c = Box(10, 10);
            c.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            table.Controls.Add(a, 0, 0);
            table.Controls.Add(b, 1, 0);
            table.Controls.Add(c, 2, 0);
            table.PerformLayout();
            r["exact/table/widths"] = string.Join(",", table.GetColumnWidths());
            r["exact/table/heights"] = string.Join(",", table.GetRowHeights());
            r["exact/table/a"] = B(a.Bounds);
            r["exact/table/b"] = B(b.Bounds);
            r["exact/table/c-bottom-right"] = B(c.Bounds);

            table.ColumnStyles.Clear();
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            b.Width = 60;
            table.PerformLayout();
            r["exact/table/autosize-widths"] = string.Join(",", table.GetColumnWidths());
        }

        using (var table = new TableLayoutPanel { Size = new Size(200, 200), ColumnCount = 2, RowCount = 2 })
        {
            var a = Box(10, 10);
            var b = Box(10, 10);
            var c = Box(10, 10);
            var d = Box(10, 10);
            table.Controls.AddRange(new[] { a, b, c, d });
            table.SetColumnSpan(c, 2);
            table.PerformLayout();
            r["exact/table/flow-c"] = table.GetPositionFromControl(c).ToString();
            r["exact/table/flow-d"] = table.GetPositionFromControl(d).ToString();
            r["exact/table/rows-after-grow"] = table.GetRowHeights().Length.ToString();
        }

        using (var panel = new Panel { Size = new Size(100, 100), AutoScroll = true })
        {
            var far = new Control { Bounds = new Rectangle(10, 250, 50, 30) };
            var near = new Control { Bounds = new Rectangle(10, 10, 50, 30) };
            panel.Controls.Add(far);
            panel.Controls.Add(near);
            panel.PerformLayout();
            r["exact/autoscroll/vertical-visible"] = panel.VerticalScroll.Visible.ToString();
            r["exact/autoscroll/horizontal-visible"] = panel.HorizontalScroll.Visible.ToString();
            r["text/autoscroll/display"] = B(panel.DisplayRectangle);
            panel.AutoScrollPosition = new Point(0, 100);
            r["exact/autoscroll/position"] = panel.AutoScrollPosition.X + "," + panel.AutoScrollPosition.Y;
            r["exact/autoscroll/near"] = B(near.Bounds);
            r["exact/autoscroll/far"] = B(far.Bounds);
        }

        using (var panel = new Panel { Size = new Size(50, 50), AutoSize = true, Padding = new Padding(5) })
        {
            var child = new Control { Bounds = new Rectangle(10, 10, 100, 40), Margin = new Padding(3) };
            panel.Controls.Add(child);
            r["exact/autosize/panel-grow"] = S(panel.Size);
            child.Width = 20;
            r["exact/autosize/panel-growonly"] = S(panel.Size);
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            r["exact/autosize/panel-shrink"] = S(panel.Size);
        }

        // The same AutoSize panel inside a parent: WinForms applies a container's AutoSize from the
        // parent's layout, so a parentless panel (above) never grows and this one does.
        using (var host = new Control { Size = new Size(300, 300) })
        {
            var panel = new Panel { Location = new Point(0, 0), Size = new Size(50, 50), AutoSize = true, Padding = new Padding(5) };
            host.Controls.Add(panel);
            var child = new Control { Bounds = new Rectangle(10, 10, 100, 40), Margin = new Padding(3) };
            panel.Controls.Add(child);
            r["exact/autosize/parented-grow"] = S(panel.Size);
            child.Width = 20;
            r["exact/autosize/parented-growonly"] = S(panel.Size);
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            r["exact/autosize/parented-shrink"] = S(panel.Size);
        }

        // PreferredSize of a container with docked children (DefaultLayout measureOnly): a Fill child
        // that is not AutoSize adds nothing and its Margin never counts; Top/Left children add only
        // their height/width; anchored children count with their margins. Found by the Ф5.1 reader gate.
        using (var panel = new Panel { Size = new Size(200, 100), Padding = new Padding(4) })
        {
            panel.Controls.Add(new Control { Dock = DockStyle.Fill, Size = new Size(150, 80), Margin = new Padding(3) });
            r["exact/autosize/pref-fill"] = S(panel.PreferredSize);
            panel.Controls.Add(new Control { Dock = DockStyle.Top, Size = new Size(170, 20) });
            panel.Controls.Add(new Control { Dock = DockStyle.Left, Size = new Size(30, 60) });
            r["exact/autosize/pref-docked"] = S(panel.PreferredSize);
            panel.Controls.Add(new Control { Bounds = new Rectangle(10, 10, 300, 40), Margin = new Padding(3) });
            r["exact/autosize/pref-docked-anchored"] = S(panel.PreferredSize);
            panel.Controls.Add(new Control { Bounds = new Rectangle(20, 50, 40, 20), Anchor = AnchorStyles.Bottom | AnchorStyles.Right });
            r["exact/autosize/pref-bottom-right"] = S(panel.PreferredSize);
        }
    }

    private static void FocusOrder(SortedDictionary<string, string> r)
    {
        try
        {
            using var form = new Form { ClientSize = new Size(200, 100), StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
            var a = new Button { Name = "a", Bounds = new Rectangle(10, 10, 50, 20), TabIndex = 0 };
            var b = new Button { Name = "b", Bounds = new Rectangle(10, 40, 50, 20), TabIndex = 1 };
            form.Controls.Add(a);
            form.Controls.Add(b);
            var events = new List<string>();
            foreach (var c in new[] { a, b })
            {
                c.Enter += (s, _) => events.Add(((Control)s!).Name + ".Enter");
                c.Leave += (s, _) => events.Add(((Control)s!).Name + ".Leave");
                c.GotFocus += (s, _) => events.Add(((Control)s!).Name + ".GotFocus");
                c.LostFocus += (s, _) => events.Add(((Control)s!).Name + ".LostFocus");
                c.Validating += (s, _) => events.Add(((Control)s!).Name + ".Validating");
                c.Validated += (s, _) => events.Add(((Control)s!).Name + ".Validated");
            }
            form.Show();
            Application.DoEvents();
            r["exact/focus/initial"] = string.Join(" ", events);
            events.Clear();
            b.Focus();
            Application.DoEvents();
            r["exact/focus/b-focus"] = string.Join(" ", events);
            events.Clear();
            form.SelectNextControl(b, true, true, true, true);
            Application.DoEvents();
            r["exact/focus/select-next"] = string.Join(" ", events);
            r["exact/focus/active"] = form.ActiveControl?.Name ?? "<null>";
            form.Close();
        }
        catch (Exception ex)
        {
            r["info/focus/error"] = ex.GetType().Name + ": " + ex.Message;
        }
    }
}
