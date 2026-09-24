namespace System.Drawing;

/// <summary>Generated: one cached Pen per colour of <see cref="Color"/>.</summary>
public static class Pens
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

    public static Pen Transparent => Get(nameof(Transparent), Color.Transparent);
    public static Pen AliceBlue => Get(nameof(AliceBlue), Color.AliceBlue);
    public static Pen AntiqueWhite => Get(nameof(AntiqueWhite), Color.AntiqueWhite);
    public static Pen Aqua => Get(nameof(Aqua), Color.Aqua);
    public static Pen Aquamarine => Get(nameof(Aquamarine), Color.Aquamarine);
    public static Pen Azure => Get(nameof(Azure), Color.Azure);
    public static Pen Beige => Get(nameof(Beige), Color.Beige);
    public static Pen Bisque => Get(nameof(Bisque), Color.Bisque);
    public static Pen Black => Get(nameof(Black), Color.Black);
    public static Pen BlanchedAlmond => Get(nameof(BlanchedAlmond), Color.BlanchedAlmond);
    public static Pen Blue => Get(nameof(Blue), Color.Blue);
    public static Pen BlueViolet => Get(nameof(BlueViolet), Color.BlueViolet);
    public static Pen Brown => Get(nameof(Brown), Color.Brown);
    public static Pen BurlyWood => Get(nameof(BurlyWood), Color.BurlyWood);
    public static Pen CadetBlue => Get(nameof(CadetBlue), Color.CadetBlue);
    public static Pen Chartreuse => Get(nameof(Chartreuse), Color.Chartreuse);
    public static Pen Chocolate => Get(nameof(Chocolate), Color.Chocolate);
    public static Pen Coral => Get(nameof(Coral), Color.Coral);
    public static Pen CornflowerBlue => Get(nameof(CornflowerBlue), Color.CornflowerBlue);
    public static Pen Cornsilk => Get(nameof(Cornsilk), Color.Cornsilk);
    public static Pen Crimson => Get(nameof(Crimson), Color.Crimson);
    public static Pen Cyan => Get(nameof(Cyan), Color.Cyan);
    public static Pen DarkBlue => Get(nameof(DarkBlue), Color.DarkBlue);
    public static Pen DarkCyan => Get(nameof(DarkCyan), Color.DarkCyan);
    public static Pen DarkGoldenrod => Get(nameof(DarkGoldenrod), Color.DarkGoldenrod);
    public static Pen DarkGray => Get(nameof(DarkGray), Color.DarkGray);
    public static Pen DarkGreen => Get(nameof(DarkGreen), Color.DarkGreen);
    public static Pen DarkKhaki => Get(nameof(DarkKhaki), Color.DarkKhaki);
    public static Pen DarkMagenta => Get(nameof(DarkMagenta), Color.DarkMagenta);
    public static Pen DarkOliveGreen => Get(nameof(DarkOliveGreen), Color.DarkOliveGreen);
    public static Pen DarkOrange => Get(nameof(DarkOrange), Color.DarkOrange);
    public static Pen DarkOrchid => Get(nameof(DarkOrchid), Color.DarkOrchid);
    public static Pen DarkRed => Get(nameof(DarkRed), Color.DarkRed);
    public static Pen DarkSalmon => Get(nameof(DarkSalmon), Color.DarkSalmon);
    public static Pen DarkSeaGreen => Get(nameof(DarkSeaGreen), Color.DarkSeaGreen);
    public static Pen DarkSlateBlue => Get(nameof(DarkSlateBlue), Color.DarkSlateBlue);
    public static Pen DarkSlateGray => Get(nameof(DarkSlateGray), Color.DarkSlateGray);
    public static Pen DarkTurquoise => Get(nameof(DarkTurquoise), Color.DarkTurquoise);
    public static Pen DarkViolet => Get(nameof(DarkViolet), Color.DarkViolet);
    public static Pen DeepPink => Get(nameof(DeepPink), Color.DeepPink);
    public static Pen DeepSkyBlue => Get(nameof(DeepSkyBlue), Color.DeepSkyBlue);
    public static Pen DimGray => Get(nameof(DimGray), Color.DimGray);
    public static Pen DodgerBlue => Get(nameof(DodgerBlue), Color.DodgerBlue);
    public static Pen Firebrick => Get(nameof(Firebrick), Color.Firebrick);
    public static Pen FloralWhite => Get(nameof(FloralWhite), Color.FloralWhite);
    public static Pen ForestGreen => Get(nameof(ForestGreen), Color.ForestGreen);
    public static Pen Fuchsia => Get(nameof(Fuchsia), Color.Fuchsia);
    public static Pen Gainsboro => Get(nameof(Gainsboro), Color.Gainsboro);
    public static Pen GhostWhite => Get(nameof(GhostWhite), Color.GhostWhite);
    public static Pen Gold => Get(nameof(Gold), Color.Gold);
    public static Pen Goldenrod => Get(nameof(Goldenrod), Color.Goldenrod);
    public static Pen Gray => Get(nameof(Gray), Color.Gray);
    public static Pen Green => Get(nameof(Green), Color.Green);
    public static Pen GreenYellow => Get(nameof(GreenYellow), Color.GreenYellow);
    public static Pen Honeydew => Get(nameof(Honeydew), Color.Honeydew);
    public static Pen HotPink => Get(nameof(HotPink), Color.HotPink);
    public static Pen IndianRed => Get(nameof(IndianRed), Color.IndianRed);
    public static Pen Indigo => Get(nameof(Indigo), Color.Indigo);
    public static Pen Ivory => Get(nameof(Ivory), Color.Ivory);
    public static Pen Khaki => Get(nameof(Khaki), Color.Khaki);
    public static Pen Lavender => Get(nameof(Lavender), Color.Lavender);
    public static Pen LavenderBlush => Get(nameof(LavenderBlush), Color.LavenderBlush);
    public static Pen LawnGreen => Get(nameof(LawnGreen), Color.LawnGreen);
    public static Pen LemonChiffon => Get(nameof(LemonChiffon), Color.LemonChiffon);
    public static Pen LightBlue => Get(nameof(LightBlue), Color.LightBlue);
    public static Pen LightCoral => Get(nameof(LightCoral), Color.LightCoral);
    public static Pen LightCyan => Get(nameof(LightCyan), Color.LightCyan);
    public static Pen LightGoldenrodYellow => Get(nameof(LightGoldenrodYellow), Color.LightGoldenrodYellow);
    public static Pen LightGray => Get(nameof(LightGray), Color.LightGray);
    public static Pen LightGreen => Get(nameof(LightGreen), Color.LightGreen);
    public static Pen LightPink => Get(nameof(LightPink), Color.LightPink);
    public static Pen LightSalmon => Get(nameof(LightSalmon), Color.LightSalmon);
    public static Pen LightSeaGreen => Get(nameof(LightSeaGreen), Color.LightSeaGreen);
    public static Pen LightSkyBlue => Get(nameof(LightSkyBlue), Color.LightSkyBlue);
    public static Pen LightSlateGray => Get(nameof(LightSlateGray), Color.LightSlateGray);
    public static Pen LightSteelBlue => Get(nameof(LightSteelBlue), Color.LightSteelBlue);
    public static Pen LightYellow => Get(nameof(LightYellow), Color.LightYellow);
    public static Pen Lime => Get(nameof(Lime), Color.Lime);
    public static Pen LimeGreen => Get(nameof(LimeGreen), Color.LimeGreen);
    public static Pen Linen => Get(nameof(Linen), Color.Linen);
    public static Pen Magenta => Get(nameof(Magenta), Color.Magenta);
    public static Pen Maroon => Get(nameof(Maroon), Color.Maroon);
    public static Pen MediumAquamarine => Get(nameof(MediumAquamarine), Color.MediumAquamarine);
    public static Pen MediumBlue => Get(nameof(MediumBlue), Color.MediumBlue);
    public static Pen MediumOrchid => Get(nameof(MediumOrchid), Color.MediumOrchid);
    public static Pen MediumPurple => Get(nameof(MediumPurple), Color.MediumPurple);
    public static Pen MediumSeaGreen => Get(nameof(MediumSeaGreen), Color.MediumSeaGreen);
    public static Pen MediumSlateBlue => Get(nameof(MediumSlateBlue), Color.MediumSlateBlue);
    public static Pen MediumSpringGreen => Get(nameof(MediumSpringGreen), Color.MediumSpringGreen);
    public static Pen MediumTurquoise => Get(nameof(MediumTurquoise), Color.MediumTurquoise);
    public static Pen MediumVioletRed => Get(nameof(MediumVioletRed), Color.MediumVioletRed);
    public static Pen MidnightBlue => Get(nameof(MidnightBlue), Color.MidnightBlue);
    public static Pen MintCream => Get(nameof(MintCream), Color.MintCream);
    public static Pen MistyRose => Get(nameof(MistyRose), Color.MistyRose);
    public static Pen Moccasin => Get(nameof(Moccasin), Color.Moccasin);
    public static Pen NavajoWhite => Get(nameof(NavajoWhite), Color.NavajoWhite);
    public static Pen Navy => Get(nameof(Navy), Color.Navy);
    public static Pen OldLace => Get(nameof(OldLace), Color.OldLace);
    public static Pen Olive => Get(nameof(Olive), Color.Olive);
    public static Pen OliveDrab => Get(nameof(OliveDrab), Color.OliveDrab);
    public static Pen Orange => Get(nameof(Orange), Color.Orange);
    public static Pen OrangeRed => Get(nameof(OrangeRed), Color.OrangeRed);
    public static Pen Orchid => Get(nameof(Orchid), Color.Orchid);
    public static Pen PaleGoldenrod => Get(nameof(PaleGoldenrod), Color.PaleGoldenrod);
    public static Pen PaleGreen => Get(nameof(PaleGreen), Color.PaleGreen);
    public static Pen PaleTurquoise => Get(nameof(PaleTurquoise), Color.PaleTurquoise);
    public static Pen PaleVioletRed => Get(nameof(PaleVioletRed), Color.PaleVioletRed);
    public static Pen PapayaWhip => Get(nameof(PapayaWhip), Color.PapayaWhip);
    public static Pen PeachPuff => Get(nameof(PeachPuff), Color.PeachPuff);
    public static Pen Peru => Get(nameof(Peru), Color.Peru);
    public static Pen Pink => Get(nameof(Pink), Color.Pink);
    public static Pen Plum => Get(nameof(Plum), Color.Plum);
    public static Pen PowderBlue => Get(nameof(PowderBlue), Color.PowderBlue);
    public static Pen Purple => Get(nameof(Purple), Color.Purple);
    public static Pen RebeccaPurple => Get(nameof(RebeccaPurple), Color.RebeccaPurple);
    public static Pen Red => Get(nameof(Red), Color.Red);
    public static Pen RosyBrown => Get(nameof(RosyBrown), Color.RosyBrown);
    public static Pen RoyalBlue => Get(nameof(RoyalBlue), Color.RoyalBlue);
    public static Pen SaddleBrown => Get(nameof(SaddleBrown), Color.SaddleBrown);
    public static Pen Salmon => Get(nameof(Salmon), Color.Salmon);
    public static Pen SandyBrown => Get(nameof(SandyBrown), Color.SandyBrown);
    public static Pen SeaGreen => Get(nameof(SeaGreen), Color.SeaGreen);
    public static Pen SeaShell => Get(nameof(SeaShell), Color.SeaShell);
    public static Pen Sienna => Get(nameof(Sienna), Color.Sienna);
    public static Pen Silver => Get(nameof(Silver), Color.Silver);
    public static Pen SkyBlue => Get(nameof(SkyBlue), Color.SkyBlue);
    public static Pen SlateBlue => Get(nameof(SlateBlue), Color.SlateBlue);
    public static Pen SlateGray => Get(nameof(SlateGray), Color.SlateGray);
    public static Pen Snow => Get(nameof(Snow), Color.Snow);
    public static Pen SpringGreen => Get(nameof(SpringGreen), Color.SpringGreen);
    public static Pen SteelBlue => Get(nameof(SteelBlue), Color.SteelBlue);
    public static Pen Tan => Get(nameof(Tan), Color.Tan);
    public static Pen Teal => Get(nameof(Teal), Color.Teal);
    public static Pen Thistle => Get(nameof(Thistle), Color.Thistle);
    public static Pen Tomato => Get(nameof(Tomato), Color.Tomato);
    public static Pen Turquoise => Get(nameof(Turquoise), Color.Turquoise);
    public static Pen Violet => Get(nameof(Violet), Color.Violet);
    public static Pen Wheat => Get(nameof(Wheat), Color.Wheat);
    public static Pen White => Get(nameof(White), Color.White);
    public static Pen WhiteSmoke => Get(nameof(WhiteSmoke), Color.WhiteSmoke);
    public static Pen Yellow => Get(nameof(Yellow), Color.Yellow);
    public static Pen YellowGreen => Get(nameof(YellowGreen), Color.YellowGreen);
}
