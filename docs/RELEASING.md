# Выпуск версии

Для разработчиков NetForms: что публикуется, как это запускается и что владелец репозитория настраивает
один раз. Всё остальное делают workflow `.github/workflows/ci.yml` и `release.yml`.

## Как выходит версия

**Выпуск — это смена версии в `main`.** Поднимите `<Version>` в `Directory.Build.props`, влейте в `main`, и
после зелёных тестов CI сам:

1. видит, что тега `v<Version>` ещё нет (задача `release-check`);
2. вызывает `release.yml`: собирает все пакеты, публикует NuGet-пакеты в nuget.org, расширение — в VS Code
   Marketplace и Open VSX;
3. создаёт GitHub Release с файлами пакетов и текстом `CHANGELOG.md` — вместе с ним появляется тег `v<Version>`.

Следующие пуши в `main` с той же версией ничего не публикуют: тег уже есть.

Если учётных данных NuGet нет, `release-check` пишет предупреждение и ничего не выпускает (и тег не
создаётся). Добавьте их — и следующий пуш в `main` (или ручной запуск CI) выпустит эту версию.

Другие способы запустить `release.yml`:

- **вручную**: *Actions → Release → Run workflow*. Без галочки *publish* — сухой прогон: собирает все пакеты
  как артефакты и ничего не публикует. С галочкой — настоящий выпуск текущей версии;
- **тегом**: `git tag v0.1.0-preview.1 && git push origin v0.1.0-preview.1` (тег обязан совпадать с версией).

Повторная публикация безопасна: `dotnet nuget push --skip-duplicate`, `vsce publish --skip-duplicate`,
`ovsx publish --skip-duplicate`, существующий GitHub Release пропускается.

## Что выходит

| Куда | Что | Собирается |
|---|---|---|
| nuget.org | `NetForms`, `NetForms.Drawing`, `NetForms.Drawing.Common`, `NetForms.Platform`, `NetForms.Platform.Avalonia` (+ `.snupkg` с символами и Source Link) | `dotnet pack NetForms.slnx -c Release -o artifacts/pkg` |
| nuget.org | `NetForms.Templates` — `dotnet new netforms`, `netforms-form`, `netforms-usercontrol` | `dotnet pack templates/NetForms.Templates.csproj -c Release -o artifacts/pkg` |
| nuget.org | `NetForms.Convert` — .NET tool `netforms-convert` (~42 МБ: натив Skia только для настольных платформ) | входит в `dotnet pack NetForms.slnx` |
| VS Code Marketplace, Open VSX | `netforms.netforms-designer`, по пакету на платформу (`win32-x64`, `win32-arm64`, `linux-x64`, `linux-arm64`, `darwin-x64`, `darwin-arm64`, ~13 МБ каждый); версия NetForms с дефисом → pre-release | `cd designer && npm run package:targets` |
| GitHub Releases | всё перечисленное + универсальный `.vsix` (41 МБ) | задача `github-release` |
| GitHub Pages | сайт: `site/` + документация из `docs/` | `.github/workflows/pages.yml` при изменении `site/` или `docs/` в `main` |

Не публикуются (`IsPackable=false` по умолчанию в `Directory.Build.props`): `NetForms.Design`,
`NetForms.Design.Serialization` (едут внутри расширения), тесты, сэмплы, `ApiDiff`, `Markup`, хост дизайнера.

## Один раз, руками (владелец репозитория)

### nuget.org — один из двух способов

**A. Trusted Publishing, без хранимого ключа (рекомендуется).**

1. Войдите на nuget.org под учётной записью, которая будет владельцем пакетов.
2. *Username → Trusted Publishing → Create*: Repository Owner `Go-Forms`, Repository `.NetForms`,
   Workflow File `release.yml` (если публикация из CI упадёт с ошибкой политики, добавьте вторую политику с
   `ci.yml` — CI вызывает `release.yml` как reusable workflow). **Scopes: «Push new packages and package
   versions»** (не только новых версий: первые публикации — это новые пакеты), **Glob Pattern: `NetForms*`**
   (или `*`). Environment оставьте пустым.
3. В GitHub: *Settings → Secrets and variables → Actions → Variables* → переменная `NUGET_USER` = имя
   пользователя nuget.org (не e-mail).

Workflow получает от GitHub OIDC-токен, обменивает его на ключ, живущий час (`NuGet/login@v1`), и публикует.
Самой первой публикацией нового id на nuget.org владельцем становится эта учётная запись.

Если в логе «Successfully exchanged OIDC token for NuGet API key», а затем `403 … does not have permission to
access the specified package`, — политика найдена, но не разрешает эту публикацию: проверьте Scopes и Glob
Pattern (выше) и что e-mail учётной записи nuget.org подтверждён. Если в репозитории есть и секрет
`NUGET_API_KEY`, workflow после такого отказа повторяет публикацию с ним.

**B. API-ключ.**

1. nuget.org → *API Keys → Create*: scope *Push new packages and package versions*, Glob Pattern `NetForms*`,
   срок — до года.
2. В GitHub: *Settings → Secrets and variables → Actions → Secrets* → `NUGET_API_KEY`.
3. Поставьте напоминание продлить ключ до истечения срока.

После первой публикации зарезервируйте префикс `NetForms.` (*ID prefix reservation*, заявка на
account@nuget.org): чужие пакеты с этим префиксом не смогут выглядеть нашими.

### VS Code Marketplace

Публикует задача `vscode-publish` в `release.yml`: шесть платформенных `.vsix` под одним id
`netforms.netforms-designer`, в окружении GitHub `marketplace` (оно создаётся само при первом запуске).

1. **Publisher.** <https://marketplace.visualstudio.com/manage> → войти учётной записью Microsoft → *Create
   publisher*, ID `netforms` (должен совпадать с `"publisher"` в `designer/package.json`; если занят — поменяйте
   там и в документации: `netforms.netforms-designer`).
2. **Вход для публикации** — один из двух способов (заданы оба — сначала B, при отказе A).

**A. Токен `VSCE_PAT` — пять минут, но только до 30 ноября 2026.** 1 декабря 2026 Microsoft отключает
глобальные токены Azure DevOps, а Marketplace принимает только их.

1. <https://dev.azure.com> той же учётной записью Microsoft (попросит создать организацию — любое имя) →
   *User settings → Personal access tokens → New Token*: **Organization: All accessible organizations** (с одной
   организацией будет 403), *Scopes: Custom defined → Show all scopes →* **Marketplace: Manage**, срок — до
   30.11.2026.
2. GitHub: *Settings → Secrets and variables → Actions → Secrets* → `VSCE_PAT`.

**B. Managed identity в Microsoft Entra ID — без хранимого ключа, и после 1 декабря 2026.** Нужна подписка
Azure (сама identity бесплатна). Вход через GitHub OIDC, как Trusted Publishing у nuget.org; `vsce publish
--oidc` Marketplace пока не поддерживает.

1. Azure Portal → *Managed Identities → Create*: любая resource group, регион и имя (например
   `netforms-marketplace`). В *Properties* — **Client ID** и **Tenant ID**.
2. На identity: *Settings → Federated credentials → Add credential*, сценарий *GitHub Actions deploying Azure
   resources*: Organization `Go-Forms`, Repository `.NetForms` (регистр важен), Entity type **Environment**,
   Environment name `marketplace`.
3. GitHub: секреты `AZURE_CLIENT_ID` и `AZURE_TENANT_ID`.
4. *Actions → Marketplace identity → Run workflow*: он входит как identity и печатает её id Marketplace
   (профиль Azure DevOps, не Object ID из Entra). Marketplace → publisher `netforms` → *Members → Add* → этот id,
   роль **Contributor**.

Именно managed identity: с app registration вход проходит, а публикация, по опыту других проектов, падает с
`InvalidAccessException`.

Без обоих способов шаг публикации в Marketplace пропускается с предупреждением, остальной выпуск идёт.

**Расширение для уже вышедшей версии** (секреты добавлены после выпуска): *Actions → Release → Run workflow* с
галочкой *publish*. NuGet-пакеты и GitHub Release уже есть — они пропускаются, уйдёт только расширение.

### Open VSX (VSCodium, Cursor и др.) — по желанию

Аккаунт на <https://open-vsx.org> через GitHub, подписать Publisher Agreement,
`npx ovsx create-namespace netforms -p <token>`, секрет `OVSX_PAT`. Без секрета шаг пропускается.

### GitHub Pages — сделано

*Settings → Pages → Source: GitHub Actions* включено 2026-09-24, сайт — <https://go-forms.github.io/.NetForms/>.
Свой домен задаётся там же (*Custom domain*) плюс запись `CNAME` у регистратора на `go-forms.github.io`; файл
`CNAME` в репозитории при публикации через Actions не нужен.

### Лицензия

`LICENSE` — MIT, правообладатель «NetForms contributors» (как `Authors` в `Directory.Build.props`). Если
правообладатель другой — поменяйте в `LICENSE` и `Copyright` там же.

## Каждый выпуск

1. **Версия.** `<Version>` в `Directory.Build.props`, `Version` пакета NetForms в
   `templates/netforms-app/NetFormsApp1.csproj` и `netformsVersion` в `designer/package.json` — одинаковые
   (их равенство держат `TemplateTests` и `designer/test/version.test.js`; конвертер берёт версию из своей
   сборки). Версия самого расширения — `version` в `designer/package.json` (Marketplace понимает только `x.y.z`),
   поднимайте её, когда меняется расширение.
2. **Что вошло**: `CHANGELOG.md` (его текст идёт в GitHub Release) и `designer/CHANGELOG.md`.
3. **Покрытие API**: `dotnet run --project tools/NetForms.ApiDiff -- --markdown docs/api`; цифры в
   `docs/compatibility.md`, `docs/ru/compatibility.md`, `README.md`, `README.ru.md` и на главных страницах сайта — по нему.
4. **Прогон**: `dotnet test NetForms.slnx`, `cd designer && npm test`; на Windows — с оракулами (см. PLAN.md).
5. **Сухой прогон** (по желанию): *Actions → Release → Run workflow* без *publish*.
6. **PR в `main`** со сменой версии → после зелёного CI выпуск идёт сам.

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

# сайт с документацией
cd site && npm ci && npm run build && npm run serve
```

Тот же путь «пакеты → шаблоны из `.nupkg` → новый проект собирается из пакетов» каждый раз проходит тест
`TemplateTests.ANewProjectFromTheTemplatePackageBuildsFromTheNetFormsPackages`.
