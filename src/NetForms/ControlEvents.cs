using System.Drawing;

namespace System.Windows.Forms;

/// <summary>Raised before KeyDown; setting <see cref="IsInputKey"/> makes the key an input key for the control.</summary>
public class PreviewKeyDownEventArgs : EventArgs
{
    public PreviewKeyDownEventArgs(Keys keyData) => KeyData = keyData;

    public bool Alt => (KeyData & Keys.Alt) == Keys.Alt;
    public bool Control => (KeyData & Keys.Control) == Keys.Control;
    public Keys KeyCode => KeyData & Keys.KeyCode;
    public int KeyValue => (int)(KeyData & Keys.KeyCode);
    public Keys KeyData { get; }
    public Keys Modifiers => KeyData & Keys.Modifiers;
    public bool Shift => (KeyData & Keys.Shift) == Keys.Shift;
    public bool IsInputKey { get; set; }
}

public delegate void PreviewKeyDownEventHandler(object? sender, PreviewKeyDownEventArgs e);

public class HelpEventArgs : EventArgs
{
    public HelpEventArgs(Point mousePos) => MousePos = mousePos;

    public Point MousePos { get; }
    public bool Handled { get; set; }
}

public delegate void HelpEventHandler(object? sender, HelpEventArgs hlpevent);

[Flags]
public enum UICues
{
    ShowFocus = 0x01,
    ShowKeyboard = 0x02,
    Shown = ShowFocus | ShowKeyboard,
    ChangeFocus = 0x04,
    ChangeKeyboard = 0x08,
    Changed = ChangeFocus | ChangeKeyboard,
    None = 0x00,
}

public class UICuesEventArgs : EventArgs
{
    private readonly UICues _uicues;

    public UICuesEventArgs(UICues uicues) => _uicues = uicues;

    public bool ShowFocus => (_uicues & UICues.ShowFocus) != 0;
    public bool ShowKeyboard => (_uicues & UICues.ShowKeyboard) != 0;
    public bool ChangeFocus => (_uicues & UICues.ChangeFocus) != 0;
    public bool ChangeKeyboard => (_uicues & UICues.ChangeKeyboard) != 0;
    public UICues Changed => _uicues & UICues.Changed;
}

public delegate void UICuesEventHandler(object? sender, UICuesEventArgs e);

/// <summary>A method with no parameters and no result, for <see cref="Control.Invoke(Delegate)"/>.</summary>
public delegate void MethodInvoker();

[Flags]
public enum GetChildAtPointSkip
{
    None = 0x0000,
    Invisible = 0x0001,
    Disabled = 0x0002,
    Transparent = 0x0004,
}

public enum ImageLayout
{
    None,
    Tile,
    Center,
    Stretch,
    Zoom,
}

public interface IContainerControl
{
    Control? ActiveControl { get; set; }

    bool ActivateControl(Control active);
}

/// <summary>
/// What a Win32 control would be created with. NetForms controls have no native window (decision 114):
/// <see cref="Control.CreateParams"/> is there so overrides compile; the values are not applied.
/// </summary>
public class CreateParams
{
    public string? ClassName { get; set; }
    public string? Caption { get; set; }
    public int Style { get; set; }
    public int ExStyle { get; set; }
    public int ClassStyle { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public IntPtr Parent { get; set; }
    public object? Param { get; set; }

    public override string ToString() =>
        $"CreateParams {{'{ClassName}', '{Caption}', 0x{Style:x}, 0x{ExStyle:x}, {{{X}, {Y}, {Width}, {Height}}}}}";
}
