namespace System.Drawing;

/// <summary>Generated: one cached Brush per colour of <see cref="SystemColors"/>.</summary>
public static class SystemBrushes
{
    private static readonly System.Collections.Generic.Dictionary<string, Brush> s_cache = new();

    private static Brush Get(string name, Color color)
    {
        lock (s_cache)
        {
            if (!s_cache.TryGetValue(name, out var v))
            {
                v = new SolidBrush(color);
                v.IsSystemOwned = true;
                s_cache[name] = v;
            }
            return v;
        }
    }

    public static Brush ActiveBorder => Get(nameof(ActiveBorder), SystemColors.ActiveBorder);
    public static Brush ActiveCaption => Get(nameof(ActiveCaption), SystemColors.ActiveCaption);
    public static Brush ActiveCaptionText => Get(nameof(ActiveCaptionText), SystemColors.ActiveCaptionText);
    public static Brush AppWorkspace => Get(nameof(AppWorkspace), SystemColors.AppWorkspace);
    public static Brush ButtonFace => Get(nameof(ButtonFace), SystemColors.ButtonFace);
    public static Brush ButtonHighlight => Get(nameof(ButtonHighlight), SystemColors.ButtonHighlight);
    public static Brush ButtonShadow => Get(nameof(ButtonShadow), SystemColors.ButtonShadow);
    public static Brush Control => Get(nameof(Control), SystemColors.Control);
    public static Brush ControlDark => Get(nameof(ControlDark), SystemColors.ControlDark);
    public static Brush ControlDarkDark => Get(nameof(ControlDarkDark), SystemColors.ControlDarkDark);
    public static Brush ControlLight => Get(nameof(ControlLight), SystemColors.ControlLight);
    public static Brush ControlLightLight => Get(nameof(ControlLightLight), SystemColors.ControlLightLight);
    public static Brush ControlText => Get(nameof(ControlText), SystemColors.ControlText);
    public static Brush Desktop => Get(nameof(Desktop), SystemColors.Desktop);
    public static Brush GradientActiveCaption => Get(nameof(GradientActiveCaption), SystemColors.GradientActiveCaption);
    public static Brush GradientInactiveCaption => Get(nameof(GradientInactiveCaption), SystemColors.GradientInactiveCaption);
    public static Brush GrayText => Get(nameof(GrayText), SystemColors.GrayText);
    public static Brush Highlight => Get(nameof(Highlight), SystemColors.Highlight);
    public static Brush HighlightText => Get(nameof(HighlightText), SystemColors.HighlightText);
    public static Brush HotTrack => Get(nameof(HotTrack), SystemColors.HotTrack);
    public static Brush InactiveBorder => Get(nameof(InactiveBorder), SystemColors.InactiveBorder);
    public static Brush InactiveCaption => Get(nameof(InactiveCaption), SystemColors.InactiveCaption);
    public static Brush InactiveCaptionText => Get(nameof(InactiveCaptionText), SystemColors.InactiveCaptionText);
    public static Brush Info => Get(nameof(Info), SystemColors.Info);
    public static Brush InfoText => Get(nameof(InfoText), SystemColors.InfoText);
    public static Brush Menu => Get(nameof(Menu), SystemColors.Menu);
    public static Brush MenuBar => Get(nameof(MenuBar), SystemColors.MenuBar);
    public static Brush MenuHighlight => Get(nameof(MenuHighlight), SystemColors.MenuHighlight);
    public static Brush MenuText => Get(nameof(MenuText), SystemColors.MenuText);
    public static Brush ScrollBar => Get(nameof(ScrollBar), SystemColors.ScrollBar);
    public static Brush Window => Get(nameof(Window), SystemColors.Window);
    public static Brush WindowFrame => Get(nameof(WindowFrame), SystemColors.WindowFrame);
    public static Brush WindowText => Get(nameof(WindowText), SystemColors.WindowText);
}
