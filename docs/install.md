# Install and set up

Everything NetForms needs comes from NuGet: the library (`NetForms`), the project templates
(`NetForms.Templates`) and the converter (`NetForms.Convert`). You install the .NET SDK, a few system
libraries on Linux, and optionally VS Code with the designer.

## 1. The .NET 10 SDK

NetForms targets `net10.0` only (see [Compatibility](compatibility.md#1-what-runs-netforms)).
Check what you have:

```sh
dotnet --list-sdks     # a line starting with 10.0 is needed
```

### Windows 10 / 11

```powershell
winget install Microsoft.DotNet.SDK.10
```

or the installer from <https://dotnet.microsoft.com/download/dotnet/10.0>.

### Ubuntu 24.04 and later

.NET 10 is in Ubuntu's own archive:

```sh
sudo apt update
sudo apt install -y dotnet-sdk-10.0
```

### Debian 12 / 13

From Microsoft's package repository:

```sh
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-10.0
```

(Debian 13: replace `12` with `13` in the URL.)

### Fedora

```sh
sudo dnf install -y dotnet-sdk-10.0
```

If your Fedora release does not have the package yet, use the script below.

### Astra Linux, RED OS, ALT Linux and any other distribution

Microsoft's install script works on every glibc-based Linux and needs no root:

```sh
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 10.0
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc
source ~/.bashrc
```

If your distribution packages .NET 10 itself, its package works as well.

## 2. System libraries (Linux)

A desktop installation usually has all of them. On a minimal system, a server or in a container, install:

| Distribution family | Command |
|---|---|
| Debian, Ubuntu, Astra Linux | `sudo apt install -y libfontconfig1 libx11-6 libice6 libsm6 fonts-dejavu-core` plus the ICU library of your release (`apt-cache search '^libicu[0-9]'`, e.g. `libicu74` on Ubuntu 24.04, `libicu72` on Debian 12) |
| Fedora, RED OS | `sudo dnf install -y libicu fontconfig libX11 libICE libSM dejavu-sans-fonts` |
| ALT Linux | `sudo apt-get install libicu fontconfig libX11 libICE libSM fonts-ttf-dejavu` (package names may carry a version suffix: `apt-cache search icu`) |

What they are for: ICU is .NET's globalization (without it set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`,
and cultures fall back to invariant formatting); fontconfig and a TrueType font are what text is drawn with;
`libX11`, `libICE`, `libSM` are Avalonia's X11 backend. On Wayland desktops NetForms windows run through
XWayland, which GNOME and KDE install by default.

WinForms' default font is Segoe UI. Where it is not installed, the closest sans-serif family is used and text
can be a pixel or two wider or narrower than on Windows. Controls with `AutoSize` adapt; fixed-size labels
that were cut to the pixel on Windows may need a little more room.

## 3. Templates and the converter

```sh
# project and item templates: dotnet new netforms, netforms-form, netforms-usercontrol
dotnet new install NetForms.Templates::0.1.0-preview.5

# the converter for existing WinForms projects
dotnet tool install -g NetForms.Convert --prerelease
```

Update them later with `dotnet new update` and `dotnet tool update -g NetForms.Convert --prerelease`;
remove with `dotnet new uninstall NetForms.Templates` and `dotnet tool uninstall -g NetForms.Convert`.
Global tools live in `~/.dotnet/tools` (Linux) or `%USERPROFILE%\.dotnet\tools` (Windows), which must be on
`PATH`.

## 4. VS Code and the designer

1. **VS Code.** Windows: `winget install Microsoft.VisualStudioCode`. Linux: the `.deb` or `.rpm` from
   <https://code.visualstudio.com/download>, or `sudo snap install code --classic`. Prefer those to the
   Flatpak build: its sandbox does not see the system `dotnet` without extra setup.
2. **C#.** The **C#** extension (`ms-dotnettools.csharp`) for IntelliSense, build and debugging. In VSCodium
   and other editors that use Open VSX: `muhammad-sammy.csharp`.
3. **NetForms Designer.** Extensions view → search *NetForms* → Install, or
   `code --install-extension netforms.netforms-designer`. It is one extension for every system: the
   Marketplace keeps a build per platform (Windows and Linux, x64 and arm64; macOS builds too, untested)
   under the same name, and VS Code downloads the one for your machine by itself. Without access to the
   Marketplace, take the `.vsix` for your platform from GitHub Releases (`netforms-designer-linux-x64.vsix`,
   `-win32-x64`, …, about 15 MB) or the universal `netforms-designer-<version>.vsix` that works everywhere
   (about 40 MB), and run `code --install-extension <file>.vsix`.
4. **Check.** Command palette → **NetForms: Check Setup** shows the `dotnet` found, the designer host and the
   installed templates.

If `dotnet` is not on the `PATH` VS Code sees (the install script above, a custom location), set
**Settings → NetForms → Dotnet Path** (`netforms.dotnetPath`), for example `/home/me/.dotnet/dotnet`.

The designer host runs on the .NET 10 runtime you installed with the SDK. Editing forms needs no network;
creating a project or a form the first time installs `NetForms.Templates` from NuGet.

## 5. Without internet access

NetForms needs NuGet only to restore packages. In a closed network, put the packages on a file share or an
internal feed (Nexus, Artifactory, ProGet, BaGet) and point NuGet at it:

```sh
dotnet nuget add source /srv/nuget -n internal          # a folder with the .nupkg files
dotnet new install /srv/nuget/NetForms.Templates.0.1.0-preview.5.nupkg
dotnet tool install -g NetForms.Convert --prerelease --add-source /srv/nuget
```

The folder needs the NetForms packages and their dependencies (Avalonia, SkiaSharp, HarfBuzzSharp,
System.Resources.Extensions, System.Configuration.ConfigurationManager and what they depend on).
The simplest way to collect them is to restore a NetForms project once on a machine with internet access and copy
the `.nupkg` files from its NuGet cache (`dotnet nuget locals global-packages --list`).

## 6. Shipping an application to Linux

```sh
# needs the .NET 10 runtime on the target machine
dotnet publish -c Release -r linux-x64 --self-contained false -o publish/linux-x64

# carries the runtime with it: nothing to install besides the system libraries of section 2
dotnet publish -c Release -r linux-x64 --self-contained true -o publish/linux-x64
```

Use `linux-arm64` for ARM machines and `win-x64` for Windows. Copy the folder and run the executable
(`./MyApp`). To show the application in the desktop menu, add a `.desktop` file:

```ini
# ~/.local/share/applications/myapp.desktop  (or /usr/share/applications/ for all users)
[Desktop Entry]
Type=Application
Name=My App
Exec=/opt/myapp/MyApp
Icon=/opt/myapp/myapp.png
Categories=Office;
```

Single-file publishing and Native AOT are not supported yet.

## 7. Problems

| Symptom | Fix |
|---|---|
| `NU1202 … not compatible with net8.0` | The project must target `net10.0`. |
| `NETSDK1100` | The project still has `UseWindowsForms` or a `-windows` target: see [Moving a WinForms project](migrating.md). |
| `Couldn't find a valid ICU package` | Install ICU (section 2) or set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`. |
| `DllNotFoundException: libSkiaSharp` or `libX11` | System libraries missing (section 2). |
| The window does not open over SSH | NetForms needs a display: run it on the desktop session, or with `ssh -X`, or under `xvfb-run` for tests. |
| VS Code: *designer host not found* / *dotnet not found* | **NetForms: Check Setup**, then set `netforms.dotnetPath`. |
