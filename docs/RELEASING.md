# Выпуск версии

Что публикуется, откуда и что нужно сделать руками один раз. Всё остальное делает
`.github/workflows/release.yml` по тегу `v<версия>`.

## Что выходит

| Куда | Что | Собирается |
|---|---|---|
| nuget.org | `NetForms`, `NetForms.Drawing`, `NetForms.Drawing.Common`, `NetForms.Platform`, `NetForms.Platform.Avalonia` (+ `.snupkg` с символами и Source Link) | `dotnet pack NetForms.slnx -c Release -o artifacts/pkg` |
| nuget.org | `NetForms.Templates` — `dotnet new netforms`, `netforms-form`, `netforms-usercontrol` | `dotnet pack templates/NetForms.Templates.csproj -c Release -o artifacts/pkg` |
| nuget.org | `NetForms.Convert` — .NET tool `netforms-convert` (~42 МБ: натив Skia только для настольных платформ) | входит в `dotnet pack NetForms.slnx` |
| VS Code Marketplace, Open VSX | `netforms.netforms-designer`, по пакету на платформу (`win32-x64`, `win32-arm64`, `linux-x64`, `linux-arm64`, `darwin-x64`, `darwin-arm64`, ~13 МБ каждый) | `cd designer && npm run package:targets` |
| GitHub Releases | всё перечисленное + универсальный `.vsix` (41 МБ) | задача `github-release` |
| GitHub Pages | сайт `site/` | `.github/workflows/pages.yml` при изменении `site/` в `main` |

Не публикуются (`IsPackable=false` по умолчанию в `Directory.Build.props`): `NetForms.Design`,
`NetForms.Design.Serialization` (едут внутри расширения), тесты, сэмплы, `ApiDiff`, `Markup`, хост дизайнера.

## Один раз, руками (владелец репозитория)

1. **nuget.org.** Войти, проверить, что id свободны (на 2026-09-24 `NetForms` свободен), создать API-ключ
   с правом *Push new packages and package versions* и шаблоном `NetForms*`. В GitHub: *Settings → Secrets and
   variables → Actions* → секрет `NUGET_API_KEY`. После первой публикации — зарезервировать префикс `NetForms.`
   (ID prefix reservation, заявка на account@nuget.org), чтобы чужие пакеты не выглядели нашими.
2. **VS Code Marketplace.** Создать publisher `netforms` на <https://marketplace.visualstudio.com/manage>
   (id должен совпасть с `"publisher"` в `designer/package.json`; если занят — поменять там). Personal Access
   Token в Azure DevOps: *Organization: All accessible organizations*, scope *Marketplace → Manage*. Секрет
   `VSCE_PAT`.
3. **Open VSX** (VSCodium, Cursor, Gitpod и др.): аккаунт на <https://open-vsx.org> через GitHub, подписать
   Publisher Agreement, `npx ovsx create-namespace netforms -p <token>`, секрет `OVSX_PAT`. Без секрета шаг
   пропускается.
4. **GitHub Pages.** *Settings → Pages → Source: GitHub Actions*. Адрес сайта:
   `https://go-forms.github.io/.NetForms/`. Имя репозитория с точкой в начале Pages обслуживает, но если
   адрес не откроется — переименовать репозиторий (например, `NetForms`) или подключить свой домен
   (файл `site/CNAME`).
5. **Лицензия.** `LICENSE` — MIT, правообладатель «NetForms contributors» (как `Authors` в
   `Directory.Build.props`). Если правообладатель другой — поменять в `LICENSE` и `Copyright` там же.

## Каждый выпуск

1. Версия — одна на всё: `<Version>` в `Directory.Build.props` и `Version` пакета NetForms в
   `templates/netforms-app/NetFormsApp1.csproj` (их равенство держит `TemplateTests`; конвертер берёт версию
   из своей сборки). Версия расширения — `designer/package.json` (Marketplace понимает только `x.y.z`; превью
   помечается флагом `--pre-release`, его ставит workflow для тегов с дефисом).
2. `CHANGELOG.md` (корень) и `designer/CHANGELOG.md` — что вошло. Текст корневого идёт в GitHub Release.
3. `dotnet run --project tools/NetForms.ApiDiff -- --markdown docs/api` — обновить таблицы покрытия; цифры в
   `docs/compatibility.md` и `README.md` — по ним.
4. Прогон: `dotnet test NetForms.slnx`, `cd designer && npm test`; на Windows — с оракулами (см. PLAN.md).
5. Сухой прогон: *Actions → Release → Run workflow* на ветке — соберёт все пакеты как артефакты, ничего не
   опубликует.
6. Тег: `git tag v0.1.0-preview.1 && git push origin v0.1.0-preview.1`. Workflow проверит, что тег равен
   версии, соберёт, прогонит тесты, опубликует и создаст GitHub Release (превью — как pre-release).

## Проверить пакеты локально

```sh
dotnet pack NetForms.slnx -c Release -o artifacts/pkg
dotnet pack templates/NetForms.Templates.csproj -c Release -o artifacts/pkg

# шаблон → проект → сборка из локального фида (nuget.config с <add key="local" value=".../artifacts/pkg" />)
dotnet new install artifacts/pkg/NetForms.Templates.0.1.0-preview.1.nupkg
dotnet new netforms -n Hello && cd Hello && dotnet build

# конвертер как tool
dotnet tool install -g NetForms.Convert --prerelease --add-source artifacts/pkg
netforms-convert path/to/WinFormsApp.csproj --apply

# расширение: пакет под платформу и его хост против protocol-теста
cd designer && node scripts/package-targets.js linux-x64
unzip -q netforms-designer-linux-x64.vsix -d /tmp/vsix
NETFORMS_TEST_HOST=/tmp/vsix/extension/host/NetFormsDesigner.Host.dll node --test test/protocol.test.js
```

Так проверен 0.1.0-preview.1 (2026-09-24, Linux): все семь пакетов собираются; проект из шаблона и
проект «как из Visual Studio» (`net10.0-windows` + `UseWindowsForms`) после `netforms-convert --apply`
собираются из пакетов и открывают окно под X11; хост из `linux-x64.vsix` проходит protocol-тест.
