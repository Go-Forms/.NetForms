using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ControlLibrary;

/// <summary>A component without a place on the form: the component tray.</summary>
public class Ticker : Component
{
    public Ticker() { }

    public Ticker(IContainer container) : this() => container.Add(this);

    [DefaultValue(1000)]
    public int Interval { get; set; } = 1000;
}

/// <summary>Its painting throws: the designer draws a red cross instead, and stays up.</summary>
public class FaultyPainter : Control
{
    protected override void OnPaint(PaintEventArgs e) => throw new InvalidOperationException("The painter is broken.");
}

/// <summary>Its constructor throws: the designer shows a red cross with the error, and keeps the statements.</summary>
public class FaultyConstructor : Control
{
    public FaultyConstructor() => throw new InvalidOperationException("No license for design time.");

    public string Caption { get; set; } = "";
}

/// <summary>Not for the toolbox.</summary>
[ToolboxItem(false)]
public class HiddenControl : Control { }

/// <summary>Inherits [DesignTimeVisible(false)]: not for the toolbox either.</summary>
[DesignTimeVisible(false)]
public class InvisibleBase : Control { }

public class InvisibleDerived : InvisibleBase { }

/// <summary>Abstract: never in the toolbox.</summary>
public abstract class GaugeBase : Control { }

/// <summary>No parameterless constructor: never in the toolbox.</summary>
public class NeedsArguments : Control
{
    public NeedsArguments(int value) => Tag = value;
}

/// <summary>A form of the library: designed, not dropped.</summary>
public class SettingsForm : Form { }

/// <summary>A user control: in the toolbox, as in Visual Studio's project components.</summary>
public class AddressBox : UserControl
{
    public AddressBox()
    {
        var label = new Label { Text = "Address", Location = new System.Drawing.Point(3, 3), AutoSize = true };
        Controls.Add(label);
        Size = new System.Drawing.Size(200, 60);
    }
}
