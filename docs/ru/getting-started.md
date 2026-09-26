# Первое приложение

NetForms — это Windows Forms для .NET 10 на Windows и Linux. Если вы писали приложение на WinForms, вы уже
умеете писать на NetForms: код тот же. Здесь — только то, что отличается: как поставить инструменты и
создать проект.

## 1. Установка

- **.NET 10 SDK** — <https://dotnet.microsoft.com/download>. В Ubuntu 24.04+: `sudo apt install dotnet-sdk-10.0`.
- **Только Linux**: несколько системных библиотек (в настольном дистрибутиве они уже есть).
- По желанию, для визуального дизайнера: **VS Code** с расширением **NetForms Designer**
  (Marketplace / Open VSX: `netforms.netforms-designer`) и расширением C#.

Пошагово для Windows и каждого семейства Linux: [Установка и настройка](install.md).

## 2. Создание проекта

Из командной строки:

```sh
dotnet new install NetForms.Templates::0.1.0-preview.7
dotnet new netforms -n HelloForms
cd HelloForms
dotnet run
```

Или в VS Code: **NetForms: Create New Project…** — расширение само поставит шаблоны из NuGet.

Получаются те же три файла, что Visual Studio создаёт для WinForms-приложения, и файл проекта, который
отличается от созданного Visual Studio в одном месте — ссылке на NetForms:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>HelloForms</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NetForms" Version="0.1.0-preview.7" />
  </ItemGroup>

  <ItemGroup>
    <!-- То, что добавляет <UseWindowsForms>true</UseWindowsForms>: пространства имён WinForms как неявные using. -->
    <Using Include="System.Drawing" />
    <Using Include="System.Windows.Forms" />
  </ItemGroup>

</Project>
```

`Program.cs`:

```csharp
namespace HelloForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
```

Формы и пользовательские контролы добавляются командами `dotnet new netforms-form -n SettingsForm` и
`dotnet new netforms-usercontrol -n ColorPicker` (в папке проекта, после первого `dotnet build` или
`dotnet restore`; в подпапке передайте пространство имён: `--Namespace HelloForms.Views`) или командой
**NetForms: New Form…** в VS Code.

## 3. Код — как в WinForms

Всё из [документации Microsoft по WinForms](https://learn.microsoft.com/ru-ru/dotnet/desktop/winforms/)
применимо: контролы, события, раскладка через `Anchor`/`Dock`, `TableLayoutPanel`, свои контролы с
`OnPaint`, привязка данных к `DataGridView`, диалоги. Например, свой контрол:

```csharp
public class Gauge : Control
{
    private int _value;

    public int Value
    {
        get => _value;
        set { _value = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.FillRectangle(Brushes.SteelBlue, 0, 0, Width * Value / 100, Height);
        TextRenderer.DrawText(e.Graphics, $"{Value}%", Font, ClientRectangle, ForeColor);
    }
}
```

Прежде чем опираться на что-то редкое, загляните в [Совместимость](compatibility.md) и
[покрытие API](../api/README.md).

## 4. Проектирование форм

Откройте `MainForm.Designer.cs` в VS Code с установленным расширением — он откроется холстом. См.
[Визуальный дизайнер](designer.md). Файл, который он записывает, — такой же, как у Visual Studio, поэтому
тот же проект можно править и в Visual Studio на Windows.

## 5. Поставка

```sh
# на целевой машине нужна среда выполнения .NET 10
dotnet publish -c Release -r linux-x64 --self-contained false

# всё в одной папке, .NET ставить не нужно
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r win-x64 --self-contained true
```

Сборка, сделанная на Windows, запускается на Linux, и наоборот (без `-r`, с установленной средой .NET):
нативные библиотеки SkiaSharp для обеих систем есть в пакете. Подробнее, включая ярлык в меню рабочего
стола, — в [Установке и настройке](install.md#6-поставка-приложения-на-linux).
