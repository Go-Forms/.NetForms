namespace System.Drawing;

/// <summary>Generated: one cached Pen per colour of <see cref="SystemColors"/>.</summary>
public static class SystemPens
{
    private static readonly System.Collections.Generic.Dictionary<string, Pen> s_cache = new();

    private static Pen Get(string name, Color color)
    {
        lock (s_cache)
        {
            if (!s_cache.TryGetValue(name, out var v))
            {
                v = new Pen(color);
                v.IsSystemOwned = true;
                s_cache[name] = v;
            }
            return v;
        }
    }

    public static Pen ActiveBorder => Get(nameof(ActiveBorder), SystemColors.ActiveBorder);
    public static Pen ActiveCaption => Get(nameof(ActiveCaption), SystemColors.ActiveCaption);
    public static Pen ActiveCaptionText => Get(nameof(ActiveCaptionText), SystemColors.ActiveCaptionText);
    public static Pen AppWorkspace => Get(nameof(AppWorkspace), SystemColors.AppWorkspace);
    public static Pen ButtonFace => Get(nameof(ButtonFace), SystemColors.ButtonFace);
    public static Pen ButtonHighlight => Get(nameof(ButtonHighlight), SystemColors.ButtonHighlight);
    public static Pen ButtonShadow => Get(nameof(ButtonShadow), SystemColors.ButtonShadow);
    public static Pen Control => Get(nameof(Control), SystemColors.Control);
    public static Pen ControlDark => Get(nameof(ControlDark), SystemColors.ControlDark);
    public static Pen ControlDarkDark => Get(nameof(ControlDarkDark), SystemColors.ControlDarkDark);
    public static Pen ControlLight => Get(nameof(ControlLight), SystemColors.ControlLight);
    public static Pen ControlLightLight => Get(nameof(ControlLightLight), SystemColors.ControlLightLight);
    public static Pen ControlText => Get(nameof(ControlText), SystemColors.ControlText);
    public static Pen Desktop => Get(nameof(Desktop), SystemColors.Desktop);
    public static Pen GradientActiveCaption => Get(nameof(GradientActiveCaption), SystemColors.GradientActiveCaption);
    public static Pen GradientInactiveCaption => Get(nameof(GradientInactiveCaption), SystemColors.GradientInactiveCaption);
    public static Pen GrayText => Get(nameof(GrayText), SystemColors.GrayText);
    public static Pen Highlight => Get(nameof(Highlight), SystemColors.Highlight);
    public static Pen HighlightText => Get(nameof(HighlightText), SystemColors.HighlightText);
    public static Pen HotTrack => Get(nameof(HotTrack), SystemColors.HotTrack);
    public static Pen InactiveBorder => Get(nameof(InactiveBorder), SystemColors.InactiveBorder);
    public static Pen InactiveCaption => Get(nameof(InactiveCaption), SystemColors.InactiveCaption);
    public static Pen InactiveCaptionText => Get(nameof(InactiveCaptionText), SystemColors.InactiveCaptionText);
    public static Pen Info => Get(nameof(Info), SystemColors.Info);
    public static Pen InfoText => Get(nameof(InfoText), SystemColors.InfoText);
    public static Pen Menu => Get(nameof(Menu), SystemColors.Menu);
    public static Pen MenuBar => Get(nameof(MenuBar), SystemColors.MenuBar);
    public static Pen MenuHighlight => Get(nameof(MenuHighlight), SystemColors.MenuHighlight);
    public static Pen MenuText => Get(nameof(MenuText), SystemColors.MenuText);
    public static Pen ScrollBar => Get(nameof(ScrollBar), SystemColors.ScrollBar);
    public static Pen Window => Get(nameof(Window), SystemColors.Window);
    public static Pen WindowFrame => Get(nameof(WindowFrame), SystemColors.WindowFrame);
    public static Pen WindowText => Get(nameof(WindowText), SystemColors.WindowText);
}
