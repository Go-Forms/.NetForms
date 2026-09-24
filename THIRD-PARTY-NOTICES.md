# Third-party notices

NetForms includes material from the projects below. Their licences apply to that material.

---

## dotnet/winforms

<https://github.com/dotnet/winforms> — MIT License, Copyright (c) .NET Foundation and Contributors.

**What is used.** The design-time metadata of the Windows Forms controls: the `[Description]` help
text of every property and event, together with their categories, default values, browsability and
serialization visibility. These are the strings and values `System.Windows.Forms` itself carries, read
out of the reference implementation with `tests/NetForms.Compat --attrs --text` and written onto the
matching members of our controls by `tools/NetForms.Markup`. They are what a designer shows in its
property grid, so reproducing them is what makes our property grid read like the original.
A few runtime strings of the same resources - RichTextBox's undo action names and error messages - are
used in English and in the repository's Russian translation (`src/NetForms/SystemStrings.cs`).

**Vendored source.** `src/NetForms/VisualStyles/*.cs` - the `System.Windows.Forms.VisualStyles` enumerations
and the `VisualStyleElement` class tree - are copied from `src/System.Windows.Forms/System/Windows/Forms/VisualStyles`
(commit 751b3b8), with the Win32 constants they referred to (`TMT_*`, `HT*`, `BF_*`, `EDGE_*`) replaced by their
numeric values. Each file keeps the .NET Foundation header.

Layout semantics and control metrics elsewhere in the code were derived from the same repository by
reading it as a specification; where whole algorithms were ported the file says so in a comment.

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## Surviving-WinForms

<https://github.com/grantwinney/Surviving-WinForms> — MIT License, Copyright (c) 2021 Grant.

**What is used.** One `ImageList.ImageStream` resource written by the Visual Studio designer (four 16×16 icons,
from `Debugging/Misc/MessageBoxForDevs/MessageBoxForDevs/ExceptionalBox.resx`, commit 3275914): as the test
fixture `tests/Fixtures/ImageStream/ExceptionalBox.ImageStream.b64` and in `samples/Gallery/ResourcesForm.resx`.
It is the reference that NetForms reads the comctl32 image list format correctly (decision 123).

---

## DejaVu fonts

`tests/NetForms.Tests/Fonts/DejaVuSans*.ttf` — Bitstream Vera / DejaVu licence, see
`tests/NetForms.Tests/Fonts/LICENSE-DejaVu.txt`. Used only by the test suite, to make golden renders
reproducible; not shipped with the library.

---

## The short film `docs/story/angry-csproj.mp4`

Not part of the library. The video carries material produced with the tools below; everything else in it
(drawings, music, sound effects, the «Склад» app) comes from this repository — see `docs/story/video/README.md`.

- **Voices** — synthesized with [RHVoice](https://github.com/RHVoice/RHVoice): `aleksandr-hq` — CC BY-SA 4.0,
  Copyright Artem Plaksin, Sergey Parshakov, Beka Gozalishvili, Aleksander Karlov; `aleksandr`, `irina`, `elena` —
  LGPL-2.1+, Copyright Olga Yakovleva (as packaged in Ubuntu's `rhvoice-russian`).
- **Fonts** rendered into the frames — Onest, Unbounded, JetBrains Mono, Caveat, PT Serif, Noto Sans — SIL Open Font
  License 1.1. They are installed at build time (npm `@fontsource/*`, `fonts-noto-core`) and not stored in the repository.
