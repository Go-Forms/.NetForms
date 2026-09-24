# NetForms.Markup

Generates the `System.ComponentModel` markup of the NetForms controls out of a metadata dump taken
from the real `System.Windows.Forms`. It is the tool that produced the phase 5.0 markup; it is kept
so the markup can be regenerated rather than patched by hand when controls are added.

It is a build-time tool, not part of the product, and deliberately not in `NetForms.slnx`.

## The two passes

```
dotnet run --project tools/NetForms.Markup -- mark   <winforms-attrs-text.json> src/NetForms [--apply] [--only Button,Label]
dotnet run --project tools/NetForms.Markup -- shadow <winforms-attrs-text.json> <netforms-attrs.json> src/NetForms [--apply]
```

* **mark** puts `[Category]`, `[Description]`, `[DefaultValue]`, `[Localizable]`, `[Browsable]` and
  `[DesignerSerializationVisibility]` on the members we declare ourselves.
* **shadow** re-declares the *inherited* members whose advertised metadata has to differ on a derived
  control - WinForms hides `Text` on a `ProgressBar` by re-declaring it, and there is no other way,
  because `TypeDescriptor` reads attributes off the member. It needs our own dump to know what still
  differs, so it has to be run repeatedly: each round changes what we advertise.

Without `--apply` both passes only report.

## The inputs

```powershell
# reference: the real WinForms (Windows only)
dotnet run --project tests/NetForms.Compat -- --attrs winforms-attrs.json            # for the diff test
dotnet run --project tests/NetForms.Compat -- --attrs winforms-attrs-text.json --text # + descriptions, for the generator

# ours: written by the test suite on every run
tests/NetForms.Tests/bin/Debug/net10.0/render-out/netforms-attrs.json
```

## The loop

Regenerating from scratch means restoring the un-marked sources, running `mark` once, then running
`shadow` until it reports zero (three rounds, in practice), rebuilding between rounds so our dump is
fresh. The result is verified by `AttributeDiffTests.DesignTimeMetadataMatchesRealWinForms`:

```powershell
$env:NETFORMS_WINFORMS_ATTRS = "...\winforms-attrs.json"
dotnet test tests/NetForms.Tests
```
