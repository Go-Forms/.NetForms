using System.Drawing;
using NetForms.Platform;

namespace System.Windows.Forms;

public class Screen
{
    private readonly ScreenInfo _info;

    private Screen(ScreenInfo info) => _info = info;

    public static Screen[] AllScreens
    {
        get
        {
            var infos = Application.Platform.GetScreens();
            var result = new Screen[infos.Length];
            for (int i = 0; i < infos.Length; i++) result[i] = new Screen(infos[i]);
            return result;
        }
    }

    public static Screen PrimaryScreen
    {
        get
        {
            foreach (var s in AllScreens)
            {
                if (s.Primary) return s;
            }
            return AllScreens[0];
        }
    }

    public Rectangle Bounds => _info.Bounds;
    public Rectangle WorkingArea => _info.WorkingArea;
    public string DeviceName => _info.DeviceName;
    public bool Primary => _info.Primary;
    public int BitsPerPixel => _info.BitsPerPixel;

    public static Screen FromPoint(Point point)
    {
        foreach (var s in AllScreens)
        {
            if (s.Bounds.Contains(point)) return s;
        }
        return PrimaryScreen;
    }

    public static Screen FromRectangle(Rectangle rect)
    {
        Screen? best = null;
        long bestArea = -1;
        foreach (var s in AllScreens)
        {
            var i = Rectangle.Intersect(s.Bounds, rect);
            long area = (long)i.Width * i.Height;
            if (area > bestArea)
            {
                best = s;
                bestArea = area;
            }
        }
        return best ?? PrimaryScreen;
    }

    public static Screen FromControl(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return FromRectangle(control.RectangleToScreen(control.ClientRectangle));
    }

    public static Rectangle GetBounds(Point pt) => FromPoint(pt).Bounds;
    public static Rectangle GetBounds(Rectangle rect) => FromRectangle(rect).Bounds;
    public static Rectangle GetBounds(Control ctl) => FromControl(ctl).Bounds;
    public static Rectangle GetWorkingArea(Point pt) => FromPoint(pt).WorkingArea;
    public static Rectangle GetWorkingArea(Rectangle rect) => FromRectangle(rect).WorkingArea;
    public static Rectangle GetWorkingArea(Control ctl) => FromControl(ctl).WorkingArea;

    public override bool Equals(object? obj) => obj is Screen s && s._info == _info;

    public override int GetHashCode() => _info.GetHashCode();

    public override string ToString() => $"Screen[Bounds={Bounds} WorkingArea={WorkingArea} Primary={Primary} DeviceName={DeviceName}]";
}
