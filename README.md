# wcp-browsertabs

Browser Tabs is a Windows Command Palette extension for searching and switching between open Chrome, Microsoft Edge, and Firefox tabs.

Install it from the Microsoft Store:

https://apps.microsoft.com/detail/9N7BMMLTGFWX

## Requirements

- Windows 10 version 2004 or later
- Microsoft PowerToys with Command Palette enabled
- Chrome, Microsoft Edge, or Firefox

## Usage

1. Install `wcp-browsertabs` from the Microsoft Store.
2. Open Chrome, Microsoft Edge, or Firefox with a few tabs.
3. Open PowerToys Command Palette.
4. Open **Browser Tabs**.
5. Search by tab title, browser, or window title.
6. Select a result to switch directly to that browser tab.

Browser Tabs is not a standalone app. It is intentionally hidden from the normal app list and is launched through PowerToys Command Palette.

## Privacy

Browser tab discovery runs locally on your device through Windows UI Automation. Browser Tabs does not send browser tab titles, browsing activity, account information, or other personal data to a remote service.

See [PRIVACY.md](PRIVACY.md).

## Development

Main extension solution:

```powershell
src\extension\WcpBrowserTabs\WcpBrowserTabs.sln
```

Build:

```powershell
dotnet build src\extension\WcpBrowserTabs\WcpBrowserTabs.sln
```

Run unit tests:

```powershell
dotnet test src\extension\WcpBrowserTabs\WcpBrowserTabs.Tests\WcpBrowserTabs.Tests.csproj /p:Platform=x64
```

Run unit tests with coverage:

```powershell
dotnet test src\extension\WcpBrowserTabs\WcpBrowserTabs.Tests\WcpBrowserTabs.Tests.csproj --settings src\extension\WcpBrowserTabs\WcpBrowserTabs.Tests\coverage.runsettings --collect "XPlat Code Coverage" --results-directory artifacts\unit-tests /p:Platform=x64
```

Run browser UI integration tests:

```powershell
dotnet test src\extension\WcpBrowserTabs\WcpBrowserTabs.UiTests\WcpBrowserTabs.UiTests.csproj --filter Category=Ui /p:Platform=x64
```

Run browser UI integration tests with coverage:

```powershell
dotnet test src\extension\WcpBrowserTabs\WcpBrowserTabs.UiTests\WcpBrowserTabs.UiTests.csproj --filter Category=Ui --settings src\extension\WcpBrowserTabs\WcpBrowserTabs.UiTests\coverage.runsettings --collect "XPlat Code Coverage" --results-directory artifacts\ui-tests /p:Platform=x64
```

The UI tests launch Chrome, Microsoft Edge, and Firefox with temporary profiles
and local test pages. Tests for browsers that are not installed are skipped.
Coverage output is written as Cobertura XML under `artifacts\unit-tests\` or
`artifacts\ui-tests\`.

Create a Store bundle using the current package version:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\package-store-bundle.ps1 `
  -WorkspaceFolder .
```

Bump the MSIX package version and create a Store bundle:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\package-store-bundle.ps1 `
  -WorkspaceFolder . `
  -BumpVersion
```

The package version is stored in:

- `src\extension\WcpBrowserTabs\WcpBrowserTabs\WcpBrowserTabs.csproj`
- `src\extension\WcpBrowserTabs\WcpBrowserTabs\Package.appxmanifest`

Store bundles are written to `artifacts\store\<timestamp>\`.

## Versioning

The app version is stored in `version.txt` as SemVer, for example `0.0.3`.
Release Please owns release version bumps and creates tags such as `v0.0.4`.

MSIX packages require four version parts, so Store package versions append a
zero revision:

```text
App version:          0.0.3
MSIX package version: 0.0.3.0
```

Pull request builds create validation-only Store bundles with CI run-number
versions. Do not submit PR artifacts to Partner Center; submit Store packages
from GitHub Release artifacts instead.
