using Xunit;

// Application.Platform is process-wide state; keep the tests sequential.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NetForms.Tests
{
    internal static class HermeticModule
    {
        /// <summary>
        /// Pixels independent of the machine for every test, whatever runs first: the Windows default
        /// palette for system colors and text as glyph outlines (docs/PLAN.md, Ф6), so the goldens hold
        /// on Windows and Linux alike.
        /// </summary>
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
            System.Drawing.HermeticRendering.Palette = System.Drawing.HermeticRendering.WindowsDefaultPalette;
            System.Drawing.HermeticRendering.OutlineText = true;
            // The strings NetForms takes "from the OS" follow the UI language (decision 118): pinned for the tests.
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo("en-US");
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            System.Drawing.HermeticRendering.FallbackFontFiles = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Fonts", "DejaVuSans.ttf"),
                Path.Combine(AppContext.BaseDirectory, "Fonts", "DejaVuSans-Bold.ttf"),
            };
        }
    }
}
