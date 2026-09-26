using System.Windows.Forms;

namespace ControlLibrary.Extras;

/// <summary>A control in a namespace of its own: a folder of the project can be a toolbox group of its own.</summary>
public class Badge : Label
{
    public Badge()
    {
        AutoSize = true;
        Text = "NEW";
    }
}
