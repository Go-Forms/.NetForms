using System.Reflection;
using System.Text.Json;
using NetForms.Platform;
using SkiaSharp;

namespace Sklad
{
    /// <summary>
    /// Съёмка «Склада» для ролика без окна. Платформа ниже — такая же, как у Avalonia: NetForms рисует
    /// форму через <see cref="IWindowHost.Paint"/> в холст с масштабом 2 (как на HiDPI-экране), а клики,
    /// ввод текста и F2 приходят через тот же <see cref="IWindowHost"/>, что и от настоящей мыши и клавиатуры.
    /// Каждый кадр — PNG клиентской области; рамку окна дорисовывает ролик.
    /// </summary>
    internal static class Film
    {
        private const float Scale = 2f;

        private static string s_dir = ".";
        private static readonly Dictionary<string, object> s_meta = new();

        public static void Shoot(string dir)
        {
            s_dir = Directory.CreateDirectory(dir).FullName;
            // Офис русский: LANG=ru_RU.UTF-8, даты в DateTimePicker — «22.09.2026».
            System.Globalization.CultureInfo.CurrentCulture = MainForm.Ru;
            System.Globalization.CultureInfo.CurrentUICulture = MainForm.Ru;
            var platform = new FilmPlatform();
            // Application.Platform — точка подмены бэкенда (так делают тесты и дизайнер).
            typeof(Application).GetProperty("Platform", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, platform);

            var main = new MainForm();
            main.Show();
            var mw = platform.Windows[^1];
            Snap(mw, "main-0");

            // Лёша кликает по вкладкам: все открываются.
            var tabs = Find<TabControl>(main, "tabControl1");
            var tabRects = new List<int[]>();
            for (int i = 0; i < tabs.TabCount; i++)
            {
                tabRects.Add(Rect(ClientRect(main, tabs, tabs.GetTabRect(i))));
            }
            s_meta["tabs"] = tabRects;
            for (int i = 1; i < tabs.TabCount; i++)
            {
                mw.Click(Center(ClientRect(main, tabs, tabs.GetTabRect(i))));
                Snap(mw, $"main-tab{i}");
            }
            mw.Click(Center(ClientRect(main, tabs, tabs.GetTabRect(0))));
            Snap(mw, "main-back");

            // Марина Петровна: «Приход», поиск, количество, F2.
            var btnPrihod = Find<Button>(main, "btnPrihod");
            var prihodCenter = Center(ClientRect(main, btnPrihod));
            s_meta["btnPrihod"] = Rect(ClientRect(main, btnPrihod));
            mw.Host.MouseMove(prihodCenter, MouseButton.None, InputModifiers.None);
            Snap(mw, "main-hover-prihod");

            platform.OnModalLoop = () =>
            {
                var dw = platform.Windows[^1];
                var dialog = Application.OpenForms.OfType<PrihodForm>().Single();
                Snap(dw, "prihod-0");

                var search = Find<TextBox>(dialog, "txtSearch");
                s_meta["txtSearch"] = Rect(ClientRect(dialog, search));
                dw.Click(Center(ClientRect(dialog, search)));
                string query = "бумага";
                for (int i = 0; i < query.Length; i++)
                {
                    dw.Host.TextInput(query[i].ToString());
                    Snap(dw, $"prihod-s{i + 1}");
                }

                var grid = Find<DataGridView>(dialog, "gridGoods");
                var row = grid.GetRowDisplayRectangle(0, true);
                var rowRect = ClientRect(dialog, grid, row);
                s_meta["goodsRow0"] = Rect(rowRect);
                dw.Click(new Point(rowRect.X + 140, rowRect.Y + rowRect.Height / 2));
                Snap(dw, "prihod-row");

                var qty = Find<NumericUpDown>(dialog, "numQty");
                var qtyRect = ClientRect(dialog, qty);
                s_meta["numQty"] = Rect(qtyRect);
                dw.Click(new Point(qtyRect.X + 30, qtyRect.Y + qtyRect.Height / 2));
                dw.Host.KeyDown((int)Keys.A, InputModifiers.Control);
                dw.Host.KeyUp((int)Keys.A, InputModifiers.Control);
                dw.Host.TextInput("4");
                dw.Host.TextInput("0");
                Snap(dw, "prihod-qty");
                s_meta["btnSave"] = Rect(ClientRect(dialog, Find<Button>(dialog, "btnSave")));
                s_meta["dialogClient"] = new[] { dialog.ClientSize.Width, dialog.ClientSize.Height };
                s_meta["dialogTitle"] = dialog.Text;

                // F2 — как всегда. Кнопка там же, где была четырнадцать лет.
                dw.Host.KeyDown((int)Keys.F2, InputModifiers.None);
                dw.Host.KeyUp((int)Keys.F2, InputModifiers.None);
            };
            mw.Click(prihodCenter);
            mw.Host.MouseMove(new Point(prihodCenter.X + 400, prihodCenter.Y + 300), MouseButton.None, InputModifiers.None);
            Snap(mw, "main-saved");

            s_meta["mainClient"] = new[] { main.ClientSize.Width, main.ClientSize.Height };
            s_meta["mainTitle"] = main.Text;
            s_meta["scale"] = Scale;
            File.WriteAllText(Path.Combine(s_dir, "film.json"), JsonSerializer.Serialize(s_meta, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{Directory.GetFiles(s_dir, "*.png").Length} кадров → {s_dir}");
        }

        private static T Find<T>(Control root, string name) where T : Control =>
            (T)root.Controls.Find(name, searchAllChildren: true).Single();

        /// <summary>Прямоугольник <paramref name="local"/> (в координатах <paramref name="c"/>) в клиентских координатах формы.</summary>
        private static Rectangle ClientRect(Form form, Control c, Rectangle? local = null)
        {
            var r = local ?? new Rectangle(Point.Empty, c.Size);
            var p = form.PointToClient(c.PointToScreen(r.Location));
            return new Rectangle(p, r.Size);
        }

        private static Point Center(Rectangle r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

        private static int[] Rect(Rectangle r) => new[] { r.X, r.Y, r.Width, r.Height };

        private static void Snap(FilmWindow w, string name)
        {
            var size = w.ClientSize;
            using var bitmap = new SKBitmap(new SKImageInfo((int)(size.Width * Scale), (int)(size.Height * Scale), SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Scale(Scale);
                w.Host.Paint(canvas, size, new Rectangle(Point.Empty, size));
                canvas.Flush();
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(s_dir, name + ".png"));
            data.SaveTo(file);
        }
    }

    /// <summary>Платформа без окон: окна помнят свой хост, модальный цикл выполняет сценарий съёмки.</summary>
    internal sealed class FilmPlatform : IPlatform
    {
        public List<FilmWindow> Windows { get; } = new();

        /// <summary>Что делает «пользователь», пока открыт модальный диалог.</summary>
        public Action? OnModalLoop { get; set; }

        public void Initialize() { }

        public IPlatformWindow CreateWindow(IWindowHost host)
        {
            var w = new FilmWindow(host);
            Windows.Add(w);
            return w;
        }

        public void RunMessageLoop()
        {
            var action = OnModalLoop;
            OnModalLoop = null;
            action?.Invoke();
        }

        public void ExitMessageLoop() { }
        public void DoEvents() { }
        public bool IsUIThread => true;
        public void Post(Action action) => action();
        public IPlatformTimer CreateTimer(Action tick) => new FilmTimer();
        public ScreenInfo[] GetScreens() => new[] { new ScreenInfo("film", new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1040), true, 32) };
        public string? GetClipboardText() => null;
        public void SetClipboardText(string text) { }
        public string[]? ShowOpenFileDialog(IPlatformWindow? owner, FileDialogOptions options) => null;
        public string? ShowSaveFileDialog(IPlatformWindow? owner, FileDialogOptions options) => null;
        public string? ShowFolderDialog(IPlatformWindow? owner, FolderDialogOptions options) => null;
    }

    internal sealed class FilmTimer : IPlatformTimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsRunning { get; private set; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() { }
    }

    internal sealed class FilmWindow : IPlatformWindow
    {
        public FilmWindow(IWindowHost host) => Host = host;

        public IWindowHost Host { get; }
        public string Title { get; set; } = string.Empty;
        public Size ClientSize { get; set; }
        public Point Location { get; set; }
        public bool Resizable { get; set; } = true;
        public bool ShowInTaskbar { get; set; } = true;
        public Size MinimumClientSize { get; set; }
        public Size MaximumClientSize { get; set; }
        public WindowStartPosition StartPosition { get; set; }
        public IPlatformWindow? Owner { get; set; }
        public PlatformWindowState State { get; set; }
        public bool Decorations { get; set; } = true;
        public bool CanMinimize { get; set; } = true;
        public bool CanMaximize { get; set; } = true;
        public bool TopMost { get; set; }
        public bool ShowActivated { get; set; } = true;
        public bool IsVisible { get; private set; }
        public bool IsActive { get; private set; }
        public double Scaling => 2.0;

        public void Show()
        {
            IsVisible = true;
            Host.Shown();
            if (ShowActivated)
            {
                IsActive = true;
                Host.Activated();
            }
        }

        public void ShowModal(IPlatformWindow? owner) => Show();
        public void Hide() => IsVisible = false;

        public void Close()
        {
            if (Host.Closing(userRequested: false)) return;
            IsVisible = false;
            Host.Closed();
        }

        public void Activate() { }
        public void Invalidate(Rectangle? area) { }
        public void SetCapture(bool capture) { }
        public void SetCursor(string cursorName) { }
        public void Dispose() { }

        public void Click(Point p)
        {
            Host.MouseMove(p, MouseButton.None, InputModifiers.None);
            Host.MouseDown(MouseButton.Left, p, 1, InputModifiers.None);
            Host.MouseUp(MouseButton.Left, p, InputModifiers.None);
        }
    }
}
