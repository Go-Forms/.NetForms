using System.Globalization;

namespace Sklad
{
    public partial class MainForm : Form
    {
        internal static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public MainForm()
        {
            InitializeComponent();
            cmbWarehouse.SelectedIndex = 0;
            colPrice.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colQty.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            FillItems();
            FillJournal(gridPrihod, new[] { "№ док.", "Дата", "Поставщик", "Сумма, руб.", "Статус" }, new[] { 80, 100, 330, 130, 140 }, new[]
            {
                new object[] { "1481", "21.09.2026", "ООО «Канцторг»", "48 212,40", "Проведён" },
                new object[] { "1480", "18.09.2026", "ИП Сафонов А. В.", "12 950,00", "Проведён" },
                new object[] { "1479", "15.09.2026", "ООО «Упаковка-Сервис»", "31 406,00", "Проведён" },
                new object[] { "1478", "11.09.2026", "ООО «Канцторг»", "9 870,00", "Проведён" },
                new object[] { "1477", "08.09.2026", "АО «ОфисСнаб»", "64 118,90", "Проведён" },
            });
            FillJournal(gridRashod, new[] { "№ док.", "Дата", "Получатель", "Сумма, руб.", "Статус" }, new[] { 80, 100, 330, 130, 140 }, new[]
            {
                new object[] { "2209", "21.09.2026", "Бухгалтерия", "3 412,00", "Проведён" },
                new object[] { "2208", "21.09.2026", "Отдел продаж", "1 290,00", "Проведён" },
                new object[] { "2207", "19.09.2026", "Склад №2 (Промзона)", "22 610,00", "Проведён" },
                new object[] { "2206", "17.09.2026", "Приёмная", "744,80", "Проведён" },
            });
            FillJournal(gridOstatki, new[] { "Наименование", "На начало", "Приход", "Расход", "На конец" }, new[] { 330, 110, 110, 110, 110 },
                Stock.Items.Take(12).Select(i => new object[] { i.Name, (i.Qty + 20).ToString("N0", Ru), "40", "60", i.Qty.ToString("N0", Ru) }).ToArray());
            FillJournal(gridPartners, new[] { "Наименование", "ИНН", "Телефон", "Город" }, new[] { 300, 130, 160, 150 }, new[]
            {
                new object[] { "ООО «Канцторг»", "7701234567", "+7 495 123-45-67", "Москва" },
                new object[] { "ИП Сафонов А. В.", "503012345678", "+7 916 555-01-02", "Подольск" },
                new object[] { "ООО «Упаковка-Сервис»", "5024098765", "+7 495 987-65-43", "Красногорск" },
                new object[] { "АО «ОфисСнаб»", "7719876543", "+7 495 600-70-80", "Москва" },
            });
            FillJournal(gridReports, new[] { "Отчёт", "Период", "Сформирован" }, new[] { 360, 200, 160 }, new[]
            {
                new object[] { "Оборотная ведомость", "Сентябрь 2026", "21.09.2026" },
                new object[] { "Инвентаризационная опись (ИНВ-3)", "III квартал 2026", "01.07.2026" },
                new object[] { "Товарный отчёт (ТОРГ-29)", "Август 2026", "31.08.2026" },
            });
        }

        private void FillItems()
        {
            gridItems.Rows.Clear();
            foreach (var i in Stock.Items)
            {
                gridItems.Rows.Add(i.Code, i.Name, i.Unit, i.Price.ToString("N2", Ru), i.Qty.ToString("N0", Ru), i.Place);
            }
        }

        private static void FillJournal(DataGridView grid, string[] headers, int[] widths, object[][] rows)
        {
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            for (int c = 0; c < headers.Length; c++)
            {
                var column = new DataGridViewTextBoxColumn { HeaderText = headers[c], Name = "col" + c, Width = widths[c] };
                if (!(c == 0 || headers[c] is "Поставщик" or "Получатель")) column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                grid.Columns.Add(column);
            }
            foreach (var row in rows)
            {
                grid.Rows.Add(row);
            }
        }

        private void btnPrihod_Click(object? sender, EventArgs e)
        {
            using var form = new PrihodForm(Stock.NextPrihod);
            if (form.ShowDialog(this) != DialogResult.OK || form.SelectedItem is not { } item)
            {
                return;
            }

            int index = Stock.Items.IndexOf(item);
            Stock.Items[index] = item with { Qty = item.Qty + form.Quantity };
            FillItems();
            gridItems.ClearSelection();
            gridItems.Rows[index].Selected = true;
            gridItems.CurrentCell = gridItems.Rows[index].Cells[0];
            gridPrihod.Rows.Insert(0, Stock.NextPrihod.ToString(), "22.09.2026", form.Supplier,
                (item.Price * form.Quantity).ToString("N2", Ru), "Проведён");
            statusLabel.Text = $"Приход № {Stock.NextPrihod} от 22.09.2026 проведён: {item.Name} +{form.Quantity} {item.Unit}";
            Stock.NextPrihod++;
            tabControl1.SelectedTab = tabItems;
        }

        private void btnRashod_Click(object? sender, EventArgs e) => tabControl1.SelectedTab = tabRashod;

        private void btnOstatki_Click(object? sender, EventArgs e) => tabControl1.SelectedTab = tabOstatki;
    }
}
