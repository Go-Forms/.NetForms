using System.Drawing.Printing;

namespace Printing
{
    /// <summary>
    /// Prints the text of the editor, page by page, with the standard WinForms printing components: page setup,
    /// print preview and the print dialog with a page range. Nothing in it is NetForms-specific.
    /// </summary>
    public partial class MainForm : Form
    {
        private string[] _lines = Array.Empty<string>();
        private int _nextLine;
        private int _page;

        public MainForm()
        {
            InitializeComponent();
            editor.Text = string.Join(Environment.NewLine, Enumerable.Range(1, 150).Select(i =>
                $"{i,3}. The quick brown fox jumps over the lazy dog. Съешь же ещё этих мягких французских булок."));
        }

        private void pageSetupMenuItem_Click(object sender, EventArgs e) => pageSetupDialog1.ShowDialog(this);

        private void printPreviewMenuItem_Click(object sender, EventArgs e) => printPreviewDialog1.ShowDialog(this);

        private void printMenuItem_Click(object sender, EventArgs e)
        {
            printDocument1.PrinterSettings.MinimumPage = 1;
            printDocument1.PrinterSettings.MaximumPage = 9999;
            printDocument1.PrinterSettings.FromPage = 1;
            printDocument1.PrinterSettings.ToPage = 9999;
            if (printDialog1.ShowDialog(this) == DialogResult.OK) printDocument1.Print();
        }

        private void exitMenuItem_Click(object sender, EventArgs e) => Close();

        private void printDocument1_BeginPrint(object sender, PrintEventArgs e)
        {
            _lines = editor.Lines;
            _nextLine = 0;
            _page = 0;
        }

        private void printDocument1_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics!;
            var area = e.MarginBounds;
            var settings = printDocument1.PrinterSettings;
            bool someRange = settings.PrintRange == PrintRange.SomePages && e.PageSettings.PrinterSettings == settings;

            // Pages before FromPage are laid out but not drawn, so page numbers stay right.
            do
            {
                _page++;
                bool draw = !someRange || _page >= settings.FromPage;
                float y = area.Top;
                using var header = new Font(editor.Font, FontStyle.Bold);
                if (draw) g.DrawString($"{printDocument1.DocumentName} - {_page}", header, Brushes.Black, area.Left, y);
                y += header.GetHeight(g) * 1.5f;
                float lineHeight = editor.Font.GetHeight(g);
                while (_nextLine < _lines.Length && y + lineHeight <= area.Bottom)
                {
                    if (draw) g.DrawString(_lines[_nextLine], editor.Font, Brushes.Black, new RectangleF(area.Left, y, area.Width, lineHeight));
                    _nextLine++;
                    y += lineHeight;
                }
                if (draw) break;
            }
            while (_nextLine < _lines.Length);

            e.HasMorePages = _nextLine < _lines.Length && (!someRange || _page < settings.ToPage);
        }
    }
}
