using System.ComponentModel;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>ErrorProvider: icon placement, blinking and data-bound errors, as dotnet/winforms does them.</summary>
public class ErrorProviderTests
{
    private static (TestPlatform platform, Form form, TestWindow window) ShowForm(params Control[] controls)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        foreach (var c in controls) form.Controls.Add(c);
        form.Show();
        return (platform, form, platform.Windows.Last());
    }

    private static Color IconRed => Color.FromArgb(0xE0, 0x1B, 0x24);

    private static bool IsIconRed(Color c) => Math.Abs(c.R - IconRed.R) < 8 && Math.Abs(c.G - IconRed.G) < 8 && Math.Abs(c.B - IconRed.B) < 8;

    [Fact]
    public void TheIconSitsBesideTheControlAsWinFormsPlacesIt()
    {
        var box = new TextBox { Bounds = new Rectangle(50, 40, 100, 23) };
        var (_, form, window) = ShowForm(box);
        using (form)
        using (var provider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink })
        {
            provider.SetError(box, "Required");
            Assert.Equal("Required", provider.GetError(box));
            Assert.True(provider.HasErrors);

            // MiddleRight, no padding: x = Right, y centred on the control (ControlItem.GetIconBounds).
            using (var bmp = window.Paint())
            {
                Assert.True(IsIconRed(bmp.GetPixel(150 + 3, 40 + 3 + 8)), "icon at (150, 43)");
                Assert.False(IsIconRed(bmp.GetPixel(150 + 20, 40 + 11)));
            }

            // The icon is not a child control: layout, tab order and user code never see it.
            Assert.Single(form.Controls);

            provider.SetIconAlignment(box, ErrorIconAlignment.TopLeft);
            provider.SetIconPadding(box, 4);
            using (var bmp = window.Paint())
            {
                // x = Left - 16 - padding, y = Top.
                Assert.True(IsIconRed(bmp.GetPixel(50 - 20 + 3, 40 + 8)));
                Assert.False(IsIconRed(bmp.GetPixel(150 + 3, 40 + 11)));
            }

            // Moving the control moves its icon.
            box.Location = new Point(200, 100);
            using (var bmp = window.Paint())
            {
                Assert.True(IsIconRed(bmp.GetPixel(200 - 20 + 3, 100 + 8)));
            }

            provider.SetError(box, "");
            Assert.False(provider.HasErrors);
            using (var bmp = window.Paint())
            {
                Assert.False(IsIconRed(bmp.GetPixel(200 - 20 + 3, 100 + 8)));
            }
        }
    }

    [Fact]
    public void TheIconIsOnTopOfSiblingsAndTakesTheMouseOnlyWhereItIsOpaque()
    {
        var box = new TextBox { Bounds = new Rectangle(50, 40, 100, 23) };
        var neighbour = new Panel { Bounds = new Rectangle(150, 30, 100, 60), BackColor = Color.Blue };
        var (platform, form, window) = ShowForm(box, neighbour);
        using (form)
        using (var provider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink })
        {
            provider.SetError(box, "Too long");
            using (var bmp = window.Paint())
            {
                Assert.True(IsIconRed(bmp.GetPixel(150 + 3, 43 + 8)), "painted above the panel it overlaps");
            }

            // The middle of the disc is the icon; its transparent corner is the panel below.
            var middle = form.HitTest(new Point(150 + 3, 43 + 8));
            Assert.NotSame(neighbour, middle);
            Assert.NotSame(form, middle);
            Assert.Same(neighbour, form.HitTest(new Point(150, 43)));

            // Hovering the icon shows the error text as a tool tip, at once.
            int windows = platform.Windows.Count;
            window.Host.MouseMove(new Point(150 + 3, 43 + 8), MouseButton.None, InputModifiers.None);
            platform.Timers.First(t => t.IsRunning && t.Interval == TimeSpan.FromMilliseconds(1)).Fire();
            Assert.Equal(windows + 1, platform.Windows.Count);
            Assert.True(platform.Windows.Last().IsVisible);
        }
    }

    [Fact]
    public void ANewErrorBlinksFiveTimesAndThenStays()
    {
        var box = new TextBox { Bounds = new Rectangle(50, 40, 100, 23) };
        var (platform, form, window) = ShowForm(box);
        using (form)
        using (var provider = new ErrorProvider())
        {
            provider.SetError(box, "First");
            var timer = platform.Timers.Single(t => t.IsRunning && t.Interval == TimeSpan.FromMilliseconds(250));
            var shown = new List<bool>();
            for (int i = 0; i < 12 && timer.IsRunning; i++)
            {
                timer.Fire();
                using var bmp = window.Paint();
                shown.Add(IsIconRed(bmp.GetPixel(153, 51)));
            }
            // Phase 10 counts down; odd phases hide the icon: five dark frames, then it stays up.
            Assert.Equal(new[] { true, false, true, false, true, false, true, false, true, false, true }, shown);
            Assert.False(timer.IsRunning);

            // The same text again does not blink; a different one does.
            provider.SetError(box, "First");
            Assert.False(timer.IsRunning);
            provider.SetError(box, "Second");
            Assert.True(timer.IsRunning);
        }
    }

    [Fact]
    public void BlinkRateZeroMeansNeverBlink()
    {
        using var provider = new ErrorProvider();
        Assert.Equal(ErrorBlinkStyle.BlinkIfDifferentError, provider.BlinkStyle);
        provider.BlinkRate = 0;
        Assert.Equal(ErrorBlinkStyle.NeverBlink, provider.BlinkStyle);
        provider.BlinkStyle = ErrorBlinkStyle.AlwaysBlink;
        Assert.Equal(ErrorBlinkStyle.NeverBlink, provider.BlinkStyle);
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.BlinkRate = -1);
    }

    [Fact]
    public void TheIconFollowsTheControlsVisibilityAndWaitsForTheWindow()
    {
        var page = new Panel { Bounds = new Rectangle(0, 0, 300, 200) };
        var box = new TextBox { Bounds = new Rectangle(50, 40, 100, 23) };
        page.Controls.Add(box);
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(400, 300) };
        form.Controls.Add(page);
        using var provider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

        // Before the form has a window there is nothing to draw on; the icon comes with HandleCreated.
        provider.SetError(box, "Set in the constructor");
        form.Show();
        var window = platform.Windows.Last();
        using (var bmp = window.Paint()) Assert.True(IsIconRed(bmp.GetPixel(153, 51)));

        page.Visible = false;
        page.BackColor = Color.White;
        using (var bmp = window.Paint()) Assert.False(IsIconRed(bmp.GetPixel(153, 51)));
        page.Visible = true;
        using (var bmp = window.Paint()) Assert.True(IsIconRed(bmp.GetPixel(153, 51)));

        provider.Clear();
        Assert.False(provider.HasErrors);
        Assert.Equal("", provider.GetError(box));
        using (var bmp = window.Paint()) Assert.False(IsIconRed(bmp.GetPixel(153, 51)));
    }

    [Fact]
    public void ExtenderPropertiesMatchWinForms()
    {
        using var provider = new ErrorProvider();
        Assert.True(provider.CanExtend(new TextBox()));
        Assert.False(provider.CanExtend(new Form()));
        Assert.False(provider.CanExtend("text"));
        var box = new TextBox();
        Assert.Equal(ErrorIconAlignment.MiddleRight, provider.GetIconAlignment(box));
        Assert.Equal(0, provider.GetIconPadding(box));
        Assert.Equal(new Size(16, 16), provider.Icon.Size);
        Assert.Throws<ArgumentNullException>(() => provider.Icon = null!);

        var provides = TypeDescriptor.GetAttributes(typeof(ErrorProvider)).OfType<ProvidePropertyAttribute>().Select(a => a.PropertyName).OrderBy(n => n);
        Assert.Equal(new[] { "Error", "IconAlignment", "IconPadding" }, provides);
    }

    private sealed class Person : IDataErrorInfo, INotifyPropertyChanged
    {
        private string _name = "";

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public int Age { get; set; }

        public string Error => "";

        public string this[string columnName] => columnName switch
        {
            nameof(Name) when string.IsNullOrWhiteSpace(Name) => "Name is required",
            nameof(Age) when Age < 0 => "Age cannot be negative",
            _ => "",
        };

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void DataBoundErrorsComeFromIDataErrorInfo()
    {
        var people = new BindingSource { DataSource = new BindingList<Person> { new() { Name = "", Age = 30 }, new() { Name = "Ann", Age = -1 } } };
        var name = new TextBox { Bounds = new Rectangle(10, 10, 100, 23) };
        var age = new TextBox { Bounds = new Rectangle(10, 50, 100, 23) };
        name.DataBindings.Add("Text", people, "Name");
        age.DataBindings.Add("Text", people, "Age");
        var (_, form, _) = ShowForm(name, age);
        using (form)
        using (var provider = new ErrorProvider { ContainerControl = form, DataSource = people })
        {
            Assert.Equal("Name is required", provider.GetError(name));
            Assert.Equal("", provider.GetError(age));

            people.Position = 1;
            Assert.Equal("", provider.GetError(name));
            Assert.Equal("Age cannot be negative", provider.GetError(age));

            // Editing the control writes the value; the binding reports the item's error text again.
            people.Position = 0;
            name.Text = "Bob";
            Assert.Equal("", provider.GetError(name));
            name.Text = " ";
            Assert.Equal("Name is required", provider.GetError(name));
        }
    }

    [Fact]
    public void BindingCompleteCarriesTheDataErrorText()
    {
        var person = new Person { Name = "Ann" };
        var box = new TextBox();
        var binding = box.DataBindings.Add("Text", person, "Name");
        var states = new List<(BindingCompleteState, string)>();
        binding.BindingComplete += (_, e) => states.Add((e.BindingCompleteState, e.ErrorText));
        box.Text = "";
        Assert.Equal((BindingCompleteState.DataError, "Name is required"), states.Last());
        box.Text = "Bob";
        Assert.Equal((BindingCompleteState.Success, ""), states.Last());
    }
}
