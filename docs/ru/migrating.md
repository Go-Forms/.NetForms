# Перевод WinForms-проекта на NetForms

WinForms-проект на Linux не собирается:

```text
$ dotnet build
error NETSDK1100: To build a project targeting Windows on this operating system,
set the EnableWindowsTargeting property to true.
```

а если заставить (`-p:EnableWindowsTargeting=true`), собирается, но не запускается:

```text
$ dotnet run -p:EnableWindowsTargeting=true
You must install or update .NET to run this application.
Framework: 'Microsoft.WindowsDesktop.App', version '10.0.0' (x64)
No frameworks were found.
```

`Microsoft.WindowsDesktop.App` — настоящий WinForms — существует только для Windows. NetForms заменяет его
пакетом NuGet. В большинстве проектов меняется только файл `.csproj`.

## Конвертер

```sh
dotnet tool install -g NetForms.Convert --prerelease

netforms-convert MyApp.csproj              # анализ: что изменится и что не будет работать
netforms-convert MyApp.csproj --apply      # переписать проект
dotnet run
```

В VS Code: правый щелчок по `.csproj` → **NetForms: Convert WinForms Project…** — тот же анализ в виде отчёта
со ссылками на файл и строку, затем та же правка.

Что он делает с проектом в SDK-формате:

```diff
 <Project Sdk="Microsoft.NET.Sdk">

   <PropertyGroup>
     <OutputType>WinExe</OutputType>
-    <TargetFramework>net8.0-windows</TargetFramework>
+    <TargetFramework>net10.0</TargetFramework>
     <Nullable>enable</Nullable>
-    <UseWindowsForms>true</UseWindowsForms>
     <ImplicitUsings>enable</ImplicitUsings>
   </PropertyGroup>

+  <ItemGroup>
+    <PackageReference Include="NetForms" Version="0.1.0-preview.3" />
+    <!-- What <UseWindowsForms>true</UseWindowsForms> added: the WinForms namespaces as implicit usings. -->
+    <Using Include="System.Drawing" />
+    <Using Include="System.Windows.Forms" />
+  </ItemGroup>
+
 </Project>
```

Исходный файл сохраняется как `MyApp.csproj.winforms.bak`. Повторный запуск ничего не меняет.

Параметры:

| Параметр | |
|---|---|
| `--apply` | Записать изменения (без него — только отчёт). |
| `--target cross` (по умолчанию) | `net10.0`: собирается и работает на Windows и Linux. |
| `--target windows` | `net10.0-windows`: только Windows, но с NetForms вместо среды Windows Desktop. API только для Windows тогда отмечается как справка, а не предупреждение. |
| `--framework-path <папка>` | Только для работы над самим NetForms: ссылка на копию NetForms (`ProjectReference`) вместо пакета. |
| `--json` | Отчёт в JSON. |

Аргументом может быть проект, решение (`.sln`, `.slnx`) или папка.

### Проекты .NET Framework

Проекты старого формата (`<Project ToolsVersion="15.0" …>`, `packages.config`, список `<Compile Include>`)
конвертер сам переводит в SDK-формат:

- список файлов → шаблоны (globs), с `Remove` для файлов на диске, которые старый проект не компилировал,
  и `Update` для `DependentUpon` и генераторов;
- переносятся `OutputType`, `RootNamespace`, `AssemblyName`, `ApplicationIcon`, `StartupObject`, `LangVersion`,
  `AllowUnsafeBlocks`, подпись `.snk`; `Properties\AssemblyInfo.cs` остаётся (`GenerateAssemblyInfo=false`);
- ссылки на сборки фреймворка убираются (в .NET они неявны), ссылки с `HintPath` остаются,
  `packages.config` → `PackageReference`, ссылки на проекты сохраняются;
- файлы, которые программа читала из закоммиченной папки `bin\Debug`, получают `CopyToOutputDirectory`;
- библиотеки того же решения без WinForms становятся библиотеками `net10.0`.

Исходники правятся только там, где правка не может изменить смысл: удаляется `using` пространства имён,
которого в .NET нет (`System.Runtime.Remoting.Messaging`); для типа, переехавшего в .NET в NuGet-пакет
(`System.Data.SqlClient`, `System.IO.Ports`), добавляется `PackageReference`; ссылки на файлы в `.resx`
приводятся к реальному регистру букв.

## Вручную

1. `TargetFramework`: `net10.0` (или `net10.0-windows` — только для Windows).
2. Удалите `<UseWindowsForms>true</UseWindowsForms>` (и `<EnableWindowsTargeting>`, если добавляли).
3. Добавьте `<PackageReference Include="NetForms" Version="0.1.0-preview.3" />`.
4. Если включён `ImplicitUsings`, добавьте `<Using Include="System.Drawing" />` и `<Using Include="System.Windows.Forms" />`.

## Что может сказать отчёт

| Категория в отчёте | Что значит | Что делать |
|---|---|---|
| `Missing in NetForms (type)` / `(member)` | Компилируется с WinForms, но пока не с NetForms. | Посмотрите [покрытие API](../api/README.md); замените или заведите задачу — очерёдность определяет корпус. |
| `Compile error` | Не скомпилировалось бы и с WinForms, или не хватает ссылки. | Прочитайте сообщение; обычно это пакет, который старый проект брал из GAC. |
| `Windows API`, `Windows only` | P/Invoke в `user32`/`gdi32`/…; `Microsoft.Win32.Registry` или другой API/пакет только для Windows. | Спрячьте за `OperatingSystem.IsWindows()` или замените управляемым API (`Properties.Settings` вместо реестра). |
| `Win32 messages` | Переопределён `WndProc` или `CreateParams`. | Компилируется, но не вызывается — см. [Совместимость § 5](compatibility.md#5-api-только-для-windows). |
| `Windows path` | `@"C:\data"` или `@"\img\x.png"` передаётся в `File.*`. | `Path.Combine`. (`Image.FromFile` из NetForms такие пути понимает — это только справка.) |
| `Resources` | Запись `.resx`, которую может прочитать только `BinaryFormatter`. | Добавьте ресурс заново файлом (`ResXFileRef`) или строкой. |
| `COM`, `WPF` | COM-ссылка, `UseWPF`. | Не переносится. Оставьте сборку для Windows (`--target windows`) или замените компонент. |
| `Project format`, `Signing`, `Output files` | Что-то из старого проекта, что не перенесено (отсутствующий файл, ключ PFX, свой import). | Прочитайте строку; сборка работает и без этого или подскажет, что вернуть. |
| `form FAIL MainForm.Designer.cs` | Дизайнер не может открыть эту форму. | Она всё равно компилируется и работает; сообщение объясняет причину. |

## Неполадки

| Ошибка | Причина |
|---|---|
| `NU1202: Package NetForms … is not compatible with net8.0` | Проект всё ещё на старом .NET. NetForms нужен `net10.0`. |
| `NETSDK1100` | Остался `UseWindowsForms` или TFM с `-windows`. |
| `NETSDK1136` / пакет тянет `Microsoft.WindowsDesktop.App` | Зависимость собрана под среду Windows Desktop. Найдите кроссплатформенную версию. |
| `CS0012: The type 'Control' is defined in an assembly that is not referenced … System.Windows.Forms, Version=4.0.0.0, PublicKeyToken=b77a5c561934e089` | Библиотека контролов, собранная против настоящего WinForms (ZedGraph, OxyPlot, ScottPlot, FastColoredTextBox…). Пока не поддерживается: фасад `System.Windows.Forms` в NetForms не имеет строгого имени Microsoft, и компилятор не принимает его вместо настоящей сборки. Подключите исходники библиотеки вместо пакета или дождитесь изменения фасада (см. «Совместимость»). |
| `DllNotFoundException: libSkiaSharp` / `libX11` на Linux | Нет системных библиотек — см. [Установку и настройку](install.md#2-системные-библиотеки-linux). |
| Окно открывается, но текст выглядит иначе | На Linux нет Segoe UI, используется замена. Установите шрифт или задайте `Font` явно. |
