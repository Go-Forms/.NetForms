# NetForms.ExtraControls

Extra controls for [NetForms](https://go-forms.github.io/.NetForms/) — Windows Forms for .NET 10 on Windows and Linux.
They are ordinary WinForms controls (`class ToggleSwitch : Control` with its own `OnPaint`), so they work in any NetForms
application and show up in the **NetForms Designer** toolbox with their icons, properties and events.

| Control | What it is |
|---|---|
| `ToggleSwitch` | An on/off switch. `Checked`, `CheckedChanged`, `OnColor`, `OffColor`, `ThumbColor`. |
| `RatingStars` | A row of stars for a rating. `Value`, `Maximum`, `ReadOnly`, `ValueChanged`; mouse and arrow keys. |
| `CircularProgressBar` | A round progress indicator. `Value`, `Minimum`, `Maximum`, `LineWidth`, `ShowPercentage`, `Increment()`. |
| `GradientPanel` | A `Panel` with a gradient background. `StartColor`, `EndColor`, `GradientMode`. A container. |
| `ColorPickerButton` | A button with a colour swatch that opens the `ColorDialog`. `SelectedColor`, `SelectedColorChanged`. |
| `CountdownTimer` | A component (in the designer's tray): counts `Duration` down. `Start()`, `Stop()`, `Reset()`, `Tick`, `Finished`. |

## Install

```sh
dotnet add package NetForms.ExtraControls --prerelease
```

or, in VS Code with the NetForms Designer: **NetForms: Add Control Library… → NuGet package…**, type
`NetForms.ExtraControls`. The toolbox gets a *NetForms.ExtraControls* group; drag a control onto the form.

## Use it in code

```csharp
using NetForms.ExtraControls;

var wifi = new ToggleSwitch { Text = "Wi-Fi", Checked = true, Location = new Point(12, 12), Size = new Size(120, 24) };
wifi.CheckedChanged += (_, _) => statusLabel.Text = wifi.Checked ? "On" : "Off";
Controls.Add(wifi);
```

The source is in the NetForms repository, `src/NetForms.ExtraControls` — also a worked example of writing a control
library for NetForms: [Control libraries](https://go-forms.github.io/.NetForms/docs/control-libraries.html).
