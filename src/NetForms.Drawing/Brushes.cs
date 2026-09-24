namespace System.Drawing;

/// <summary>Generated: one cached Brush per colour of <see cref="Color"/>.</summary>
public static class Brushes
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

    public static Brush Transparent => Get(nameof(Transparent), Color.Transparent);
    public static Brush AliceBlue => Get(nameof(AliceBlue), Color.AliceBlue);
    public static Brush AntiqueWhite => Get(nameof(AntiqueWhite), Color.AntiqueWhite);
    public static Brush Aqua => Get(nameof(Aqua), Color.Aqua);
    public static Brush Aquamarine => Get(nameof(Aquamarine), Color.Aquamarine);
    public static Brush Azure => Get(nameof(Azure), Color.Azure);
    public static Brush Beige => Get(nameof(Beige), Color.Beige);
    public static Brush Bisque => Get(nameof(Bisque), Color.Bisque);
    public static Brush Black => Get(nameof(Black), Color.Black);
    public static Brush BlanchedAlmond => Get(nameof(BlanchedAlmond), Color.BlanchedAlmond);
    public static Brush Blue => Get(nameof(Blue), Color.Blue);
    public static Brush BlueViolet => Get(nameof(BlueViolet), Color.BlueViolet);
    public static Brush Brown => Get(nameof(Brown), Color.Brown);
    public static Brush BurlyWood => Get(nameof(BurlyWood), Color.BurlyWood);
    public static Brush CadetBlue => Get(nameof(CadetBlue), Color.CadetBlue);
    public static Brush Chartreuse => Get(nameof(Chartreuse), Color.Chartreuse);
    public static Brush Chocolate => Get(nameof(Chocolate), Color.Chocolate);
    public static Brush Coral => Get(nameof(Coral), Color.Coral);
    public static Brush CornflowerBlue => Get(nameof(CornflowerBlue), Color.CornflowerBlue);
    public static Brush Cornsilk => Get(nameof(Cornsilk), Color.Cornsilk);
    public static Brush Crimson => Get(nameof(Crimson), Color.Crimson);
    public static Brush Cyan => Get(nameof(Cyan), Color.Cyan);
    public static Brush DarkBlue => Get(nameof(DarkBlue), Color.DarkBlue);
    public static Brush DarkCyan => Get(nameof(DarkCyan), Color.DarkCyan);
    public static Brush DarkGoldenrod => Get(nameof(DarkGoldenrod), Color.DarkGoldenrod);
    public static Brush DarkGray => Get(nameof(DarkGray), Color.DarkGray);
    public static Brush DarkGreen => Get(nameof(DarkGreen), Color.DarkGreen);
    public static Brush DarkKhaki => Get(nameof(DarkKhaki), Color.DarkKhaki);
    public static Brush DarkMagenta => Get(nameof(DarkMagenta), Color.DarkMagenta);
    public static Brush DarkOliveGreen => Get(nameof(DarkOliveGreen), Color.DarkOliveGreen);
    public static Brush DarkOrange => Get(nameof(DarkOrange), Color.DarkOrange);
    public static Brush DarkOrchid => Get(nameof(DarkOrchid), Color.DarkOrchid);
    public static Brush DarkRed => Get(nameof(DarkRed), Color.DarkRed);
    public static Brush DarkSalmon => Get(nameof(DarkSalmon), Color.DarkSalmon);
    public static Brush DarkSeaGreen => Get(nameof(DarkSeaGreen), Color.DarkSeaGreen);
    public static Brush DarkSlateBlue => Get(nameof(DarkSlateBlue), Color.DarkSlateBlue);
    public static Brush DarkSlateGray => Get(nameof(DarkSlateGray), Color.DarkSlateGray);
    public static Brush DarkTurquoise => Get(nameof(DarkTurquoise), Color.DarkTurquoise);
    public static Brush DarkViolet => Get(nameof(DarkViolet), Color.DarkViolet);
    public static Brush DeepPink => Get(nameof(DeepPink), Color.DeepPink);
    public static Brush DeepSkyBlue => Get(nameof(DeepSkyBlue), Color.DeepSkyBlue);
    public static Brush DimGray => Get(nameof(DimGray), Color.DimGray);
    public static Brush DodgerBlue => Get(nameof(DodgerBlue), Color.DodgerBlue);
    public static Brush Firebrick => Get(nameof(Firebrick), Color.Firebrick);
    public static Brush FloralWhite => Get(nameof(FloralWhite), Color.FloralWhite);
    public static Brush ForestGreen => Get(nameof(ForestGreen), Color.ForestGreen);
    public static Brush Fuchsia => Get(nameof(Fuchsia), Color.Fuchsia);
    public static Brush Gainsboro => Get(nameof(Gainsboro), Color.Gainsboro);
    public static Brush GhostWhite => Get(nameof(GhostWhite), Color.GhostWhite);
    public static Brush Gold => Get(nameof(Gold), Color.Gold);
    public static Brush Goldenrod => Get(nameof(Goldenrod), Color.Goldenrod);
    public static Brush Gray => Get(nameof(Gray), Color.Gray);
    public static Brush Green => Get(nameof(Green), Color.Green);
    public static Brush GreenYellow => Get(nameof(GreenYellow), Color.GreenYellow);
    public static Brush Honeydew => Get(nameof(Honeydew), Color.Honeydew);
    public static Brush HotPink => Get(nameof(HotPink), Color.HotPink);
    public static Brush IndianRed => Get(nameof(IndianRed), Color.IndianRed);
    public static Brush Indigo => Get(nameof(Indigo), Color.Indigo);
    public static Brush Ivory => Get(nameof(Ivory), Color.Ivory);
    public static Brush Khaki => Get(nameof(Khaki), Color.Khaki);
    public static Brush Lavender => Get(nameof(Lavender), Color.Lavender);
    public static Brush LavenderBlush => Get(nameof(LavenderBlush), Color.LavenderBlush);
    public static Brush LawnGreen => Get(nameof(LawnGreen), Color.LawnGreen);
    public static Brush LemonChiffon => Get(nameof(LemonChiffon), Color.LemonChiffon);
    public static Brush LightBlue => Get(nameof(LightBlue), Color.LightBlue);
    public static Brush LightCoral => Get(nameof(LightCoral), Color.LightCoral);
    public static Brush LightCyan => Get(nameof(LightCyan), Color.LightCyan);
    public static Brush LightGoldenrodYellow => Get(nameof(LightGoldenrodYellow), Color.LightGoldenrodYellow);
    public static Brush LightGray => Get(nameof(LightGray), Color.LightGray);
    public static Brush LightGreen => Get(nameof(LightGreen), Color.LightGreen);
    public static Brush LightPink => Get(nameof(LightPink), Color.LightPink);
    public static Brush LightSalmon => Get(nameof(LightSalmon), Color.LightSalmon);
    public static Brush LightSeaGreen => Get(nameof(LightSeaGreen), Color.LightSeaGreen);
    public static Brush LightSkyBlue => Get(nameof(LightSkyBlue), Color.LightSkyBlue);
    public static Brush LightSlateGray => Get(nameof(LightSlateGray), Color.LightSlateGray);
    public static Brush LightSteelBlue => Get(nameof(LightSteelBlue), Color.LightSteelBlue);
    public static Brush LightYellow => Get(nameof(LightYellow), Color.LightYellow);
    public static Brush Lime => Get(nameof(Lime), Color.Lime);
    public static Brush LimeGreen => Get(nameof(LimeGreen), Color.LimeGreen);
    public static Brush Linen => Get(nameof(Linen), Color.Linen);
    public static Brush Magenta => Get(nameof(Magenta), Color.Magenta);
    public static Brush Maroon => Get(nameof(Maroon), Color.Maroon);
    public static Brush MediumAquamarine => Get(nameof(MediumAquamarine), Color.MediumAquamarine);
    public static Brush MediumBlue => Get(nameof(MediumBlue), Color.MediumBlue);
    public static Brush MediumOrchid => Get(nameof(MediumOrchid), Color.MediumOrchid);
    public static Brush MediumPurple => Get(nameof(MediumPurple), Color.MediumPurple);
    public static Brush MediumSeaGreen => Get(nameof(MediumSeaGreen), Color.MediumSeaGreen);
    public static Brush MediumSlateBlue => Get(nameof(MediumSlateBlue), Color.MediumSlateBlue);
    public static Brush MediumSpringGreen => Get(nameof(MediumSpringGreen), Color.MediumSpringGreen);
    public static Brush MediumTurquoise => Get(nameof(MediumTurquoise), Color.MediumTurquoise);
    public static Brush MediumVioletRed => Get(nameof(MediumVioletRed), Color.MediumVioletRed);
    public static Brush MidnightBlue => Get(nameof(MidnightBlue), Color.MidnightBlue);
    public static Brush MintCream => Get(nameof(MintCream), Color.MintCream);
    public static Brush MistyRose => Get(nameof(MistyRose), Color.MistyRose);
    public static Brush Moccasin => Get(nameof(Moccasin), Color.Moccasin);
    public static Brush NavajoWhite => Get(nameof(NavajoWhite), Color.NavajoWhite);
    public static Brush Navy => Get(nameof(Navy), Color.Navy);
    public static Brush OldLace => Get(nameof(OldLace), Color.OldLace);
    public static Brush Olive => Get(nameof(Olive), Color.Olive);
    public static Brush OliveDrab => Get(nameof(OliveDrab), Color.OliveDrab);
    public static Brush Orange => Get(nameof(Orange), Color.Orange);
    public static Brush OrangeRed => Get(nameof(OrangeRed), Color.OrangeRed);
    public static Brush Orchid => Get(nameof(Orchid), Color.Orchid);
    public static Brush PaleGoldenrod => Get(nameof(PaleGoldenrod), Color.PaleGoldenrod);
    public static Brush PaleGreen => Get(nameof(PaleGreen), Color.PaleGreen);
    public static Brush PaleTurquoise => Get(nameof(PaleTurquoise), Color.PaleTurquoise);
    public static Brush PaleVioletRed => Get(nameof(PaleVioletRed), Color.PaleVioletRed);
    public static Brush PapayaWhip => Get(nameof(PapayaWhip), Color.PapayaWhip);
    public static Brush PeachPuff => Get(nameof(PeachPuff), Color.PeachPuff);
    public static Brush Peru => Get(nameof(Peru), Color.Peru);
    public static Brush Pink => Get(nameof(Pink), Color.Pink);
    public static Brush Plum => Get(nameof(Plum), Color.Plum);
    public static Brush PowderBlue => Get(nameof(PowderBlue), Color.PowderBlue);
    public static Brush Purple => Get(nameof(Purple), Color.Purple);
    public static Brush RebeccaPurple => Get(nameof(RebeccaPurple), Color.RebeccaPurple);
    public static Brush Red => Get(nameof(Red), Color.Red);
    public static Brush RosyBrown => Get(nameof(RosyBrown), Color.RosyBrown);
    public static Brush RoyalBlue => Get(nameof(RoyalBlue), Color.RoyalBlue);
    public static Brush SaddleBrown => Get(nameof(SaddleBrown), Color.SaddleBrown);
    public static Brush Salmon => Get(nameof(Salmon), Color.Salmon);
    public static Brush SandyBrown => Get(nameof(SandyBrown), Color.SandyBrown);
    public static Brush SeaGreen => Get(nameof(SeaGreen), Color.SeaGreen);
    public static Brush SeaShell => Get(nameof(SeaShell), Color.SeaShell);
    public static Brush Sienna => Get(nameof(Sienna), Color.Sienna);
    public static Brush Silver => Get(nameof(Silver), Color.Silver);
    public static Brush SkyBlue => Get(nameof(SkyBlue), Color.SkyBlue);
    public static Brush SlateBlue => Get(nameof(SlateBlue), Color.SlateBlue);
    public static Brush SlateGray => Get(nameof(SlateGray), Color.SlateGray);
    public static Brush Snow => Get(nameof(Snow), Color.Snow);
    public static Brush SpringGreen => Get(nameof(SpringGreen), Color.SpringGreen);
    public static Brush SteelBlue => Get(nameof(SteelBlue), Color.SteelBlue);
    public static Brush Tan => Get(nameof(Tan), Color.Tan);
    public static Brush Teal => Get(nameof(Teal), Color.Teal);
    public static Brush Thistle => Get(nameof(Thistle), Color.Thistle);
    public static Brush Tomato => Get(nameof(Tomato), Color.Tomato);
    public static Brush Turquoise => Get(nameof(Turquoise), Color.Turquoise);
    public static Brush Violet => Get(nameof(Violet), Color.Violet);
    public static Brush Wheat => Get(nameof(Wheat), Color.Wheat);
    public static Brush White => Get(nameof(White), Color.White);
    public static Brush WhiteSmoke => Get(nameof(WhiteSmoke), Color.WhiteSmoke);
    public static Brush Yellow => Get(nameof(Yellow), Color.Yellow);
    public static Brush YellowGreen => Get(nameof(YellowGreen), Color.YellowGreen);
}
