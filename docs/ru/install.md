# Установка и настройка

Всё, что нужно NetForms, берётся из NuGet: библиотека (`NetForms`), шаблоны проектов
(`NetForms.Templates`) и конвертер (`NetForms.Convert`). Вы ставите .NET SDK, на Linux — несколько
системных библиотек и, по желанию, VS Code с дизайнером.

## 1. .NET 10 SDK

NetForms работает только на `net10.0` (см. [Совместимость](compatibility.md#1-на-чём-работает-netforms)).
Проверить, что установлено:

```sh
dotnet --list-sdks     # нужна строка, начинающаяся с 10.0
```

### Windows 10 / 11

```powershell
winget install Microsoft.DotNet.SDK.10
```

или установщик с <https://dotnet.microsoft.com/download/dotnet/10.0>.

### Ubuntu 24.04 и новее

.NET 10 есть в собственном архиве Ubuntu:

```sh
sudo apt update
sudo apt install -y dotnet-sdk-10.0
```

### Debian 12 / 13

Из репозитория пакетов Microsoft:

```sh
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-10.0
```

(Для Debian 13 замените `12` на `13` в адресе.)

### Fedora

```sh
sudo dnf install -y dotnet-sdk-10.0
```

Если в вашем выпуске Fedora такого пакета ещё нет, используйте скрипт ниже.

### Astra Linux, РЕД ОС, ALT Linux и любой другой дистрибутив

Скрипт установки от Microsoft работает на любом Linux с glibc и не требует прав root:

```sh
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 10.0
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc
source ~/.bashrc
```

Если ваш дистрибутив сам собирает пакет .NET 10, подойдёт и он.

## 2. Системные библиотеки (Linux)

В настольной установке они обычно уже есть. В минимальной системе, на сервере или в контейнере
поставьте:

| Семейство | Команда |
|---|---|
| Debian, Ubuntu, Astra Linux | `sudo apt install -y libfontconfig1 libx11-6 libice6 libsm6 fonts-dejavu-core` и библиотеку ICU вашего выпуска (`apt-cache search '^libicu[0-9]'`, например `libicu74` в Ubuntu 24.04, `libicu72` в Debian 12) |
| Fedora, РЕД ОС | `sudo dnf install -y libicu fontconfig libX11 libICE libSM dejavu-sans-fonts` |
| ALT Linux | `sudo apt-get install libicu fontconfig libX11 libICE libSM fonts-ttf-dejavu` (у имён пакетов может быть суффикс версии: `apt-cache search icu`) |

Зачем они: ICU — это глобализация .NET (без неё задайте `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, и
форматы культур станут инвариантными); fontconfig и TrueType-шрифт — то, чем рисуется текст; `libX11`,
`libICE`, `libSM` — X11-бэкенд Avalonia. На рабочем столе Wayland окна NetForms работают через XWayland,
который GNOME и KDE ставят по умолчанию.

Шрифт WinForms по умолчанию — Segoe UI. Там, где его нет, берётся ближайший шрифт без засечек, и текст
может быть на пиксель-два шире или уже, чем в Windows. Контролы с `AutoSize` подстраиваются; надписи
фиксированного размера, подогнанные в Windows впритык, могут потребовать чуть больше места.

## 3. Шаблоны и конвертер

```sh
# шаблоны проекта и элементов: dotnet new netforms, netforms-form, netforms-usercontrol
dotnet new install NetForms.Templates::0.1.0-preview.5

# конвертер готовых WinForms-проектов
dotnet tool install -g NetForms.Convert --prerelease
```

Обновить позже — `dotnet new update` и `dotnet tool update -g NetForms.Convert --prerelease`;
удалить — `dotnet new uninstall NetForms.Templates` и `dotnet tool uninstall -g NetForms.Convert`.
Глобальные инструменты лежат в `~/.dotnet/tools` (Linux) или `%USERPROFILE%\.dotnet\tools` (Windows),
эта папка должна быть в `PATH`.

## 4. VS Code и дизайнер

1. **VS Code.** Windows: `winget install Microsoft.VisualStudioCode`. Linux: пакет `.deb` или `.rpm` с
   <https://code.visualstudio.com/download> или `sudo snap install code --classic`. Сборку Flatpak лучше не
   брать: её песочница без дополнительной настройки не видит системный `dotnet`.
2. **C#.** Расширение **C#** (`ms-dotnettools.csharp`) — подсказки, сборка, отладка. В VSCodium и других
   редакторах с Open VSX: `muhammad-sammy.csharp`.
3. **NetForms Designer.** Панель расширений → поиск *NetForms* → Install, или
   `code --install-extension netforms.netforms-designer`. Расширение одно для всех систем: в Marketplace под
   одним именем лежит сборка для каждой платформы (Windows и Linux, x64 и arm64; есть и непроверенные сборки
   для macOS), и VS Code сам скачивает ту, что подходит машине. Без доступа к Marketplace возьмите из GitHub
   Releases `.vsix` своей платформы (`netforms-designer-linux-x64.vsix`, `-win32-x64`, …, около 15 МБ) или
   универсальный `netforms-designer-<версия>.vsix`, который работает везде (около 40 МБ), и выполните
   `code --install-extension <файл>.vsix`.
4. **Проверка.** Палитра команд → **NetForms: Check Setup** покажет найденный `dotnet`, хост дизайнера и
   установленные шаблоны.

Если `dotnet` не виден в `PATH`, который видит VS Code (установка скриптом выше, своё расположение),
задайте **Settings → NetForms → Dotnet Path** (`netforms.dotnetPath`), например `/home/me/.dotnet/dotnet`.

Хост дизайнера работает на среде .NET 10, установленной вместе с SDK. Для правки форм сеть не нужна;
при первом создании проекта или формы расширение ставит `NetForms.Templates` из NuGet.

## 5. Без доступа в интернет

NuGet нужен NetForms только для восстановления пакетов. В закрытой сети положите пакеты в общую папку или
внутренний фид (Nexus, Artifactory, ProGet, BaGet) и укажите его NuGet:

```sh
dotnet nuget add source /srv/nuget -n internal          # папка с файлами .nupkg
dotnet new install /srv/nuget/NetForms.Templates.0.1.0-preview.5.nupkg
dotnet tool install -g NetForms.Convert --prerelease --add-source /srv/nuget
```

В папке должны быть пакеты NetForms и их зависимости (Avalonia, SkiaSharp, HarfBuzzSharp,
System.Resources.Extensions, System.Configuration.ConfigurationManager и то, от чего они зависят). Проще всего
собрать их так: один раз восстановить проект NetForms на машине с интернетом и скопировать файлы `.nupkg`
из её кэша NuGet (`dotnet nuget locals global-packages --list`).

## 6. Поставка приложения на Linux

```sh
# на целевой машине нужна среда выполнения .NET 10
dotnet publish -c Release -r linux-x64 --self-contained false -o publish/linux-x64

# среда внутри: кроме системных библиотек из раздела 2 ставить ничего не нужно
dotnet publish -c Release -r linux-x64 --self-contained true -o publish/linux-x64
```

Для ARM — `linux-arm64`, для Windows — `win-x64`. Скопируйте папку и запустите исполняемый файл
(`./MyApp`). Чтобы приложение появилось в меню рабочего стола, добавьте файл `.desktop`:

```ini
# ~/.local/share/applications/myapp.desktop  (или /usr/share/applications/ для всех пользователей)
[Desktop Entry]
Type=Application
Name=Моё приложение
Exec=/opt/myapp/MyApp
Icon=/opt/myapp/myapp.png
Categories=Office;
```

Публикация одним файлом и Native AOT пока не поддерживаются.

## 7. Проблемы

| Признак | Что делать |
|---|---|
| `NU1202 … not compatible with net8.0` | Проект должен быть на `net10.0`. |
| `NETSDK1100` | В проекте остался `UseWindowsForms` или цель с `-windows`: см. [Перевод WinForms-проекта](migrating.md). |
| `Couldn't find a valid ICU package` | Поставьте ICU (раздел 2) или задайте `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`. |
| `DllNotFoundException: libSkiaSharp` или `libX11` | Нет системных библиотек (раздел 2). |
| Окно не открывается по SSH | NetForms нужен дисплей: запускайте в сеансе рабочего стола, через `ssh -X` или под `xvfb-run` для тестов. |
| VS Code: *designer host not found* / *dotnet not found* | **NetForms: Check Setup**, затем задайте `netforms.dotnetPath`. |
