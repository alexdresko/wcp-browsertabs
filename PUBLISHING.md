# Publishing Browser Tabs

This app is a Command Palette extension distributed as MSIX.

## Partner Center values

In Partner Center, create a new **MSIX or PWA app** and reserve the product name. In the product, open **Product management > Product identity** and copy these exact values:

- `Package/Identity/Name`
- `Package/Identity/Publisher`
- `Package/Properties/PublisherDisplayName`

Apply them locally:

```powershell
.\scripts\set-store-identity.ps1 `
  -WorkspaceFolder . `
  -IdentityName "<Package/Identity/Name>" `
  -Publisher "<Package/Identity/Publisher>" `
  -PublisherDisplayName "<Package/Properties/PublisherDisplayName>" `
  -DisplayName "wcp-browsertabs" `
  -Version "0.0.1.0"
```

## Build the Store bundle

`version.txt` is the source of truth for the app version. Store/MSIX package
versions use the same version with a trailing `.0` revision. For example,
`version.txt` value `0.0.3` packages as `0.0.3.0`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\package-store-bundle.ps1 `
  -WorkspaceFolder .
```

For a new Store submission, bump the MSIX package version while packaging:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\package-store-bundle.ps1 `
  -WorkspaceFolder . `
  -BumpVersion
```

Release Please owns normal release version bumps. Store submissions should come
from GitHub Release artifacts. Pull request bundles are validation-only and
should not be uploaded to Partner Center.

Upload the generated `.msixbundle` from `artifacts\store\<timestamp>\` to the Partner Center **Packages** page.

Published v0.0.2 bundle:

```text
artifacts\store\20260514-183636\WcpBrowserTabs_0.0.2.0_Bundle.msixbundle
```

## Partner Center submission checklist

The bundle only completes the **Packages** page. Partner Center also requires these sections before **Submit for certification** is enabled.

### Pricing and availability

- Markets: all possible markets
- Audience: public audience
- Discoverability: available and discoverable in the Microsoft Store
- Schedule: release as soon as possible; stop acquisition never
- Base price: Free
- Free trial: none
- Sale pricing: none
- Organizational licensing: leave default unless you want to opt out

### Properties

- Category: Productivity
- Subcategory: Utilities & tools, if Partner Center offers it
- Privacy policy URL: recommended even if Partner Center does not force it
- Website: `https://www.alexdresko.com/`
- Support contact info: use your preferred support email or website
- Product declarations: leave unchecked unless one applies
- System requirements: no special hardware

### Age ratings

Answer the age-rating questionnaire truthfully for a productivity utility:

- Not a game
- No violence, sexual content, controlled substances, gambling, or user-generated content
- No account sign-in
- No purchases
- No location, camera, microphone, contacts, or library access

### Packages

Upload:

```text
artifacts\store\<timestamp>\WcpBrowserTabs_<version>.0_Bundle.msixbundle
```

After upload, confirm the package validates and is offered for Windows Desktop. Do not enable Xbox, Holographic, or Team device families.

### Store listing

Use this draft listing copy.

Short description:

```text
Switch to open Chrome and Edge tabs directly from Windows Command Palette.
```

Description:

```text
Browser Tabs integrates with Windows Command Palette to make open browser tabs searchable from one launcher. It discovers open Chrome and Microsoft Edge tabs on your Windows desktop, shows matching tabs in Command Palette, and switches directly to the selected tab.

This extension requires Microsoft PowerToys Command Palette to be installed and enabled. Browser tab discovery runs locally on your device using Windows UI Automation. Browser Tabs does not send your browser tab data to a remote service.
```

What's new in this version:

```text
Initial Microsoft Store release.
```

Features:

```text
Search open Chrome tabs
Search open Microsoft Edge tabs
Switch directly to a selected tab
Highlights currently active tabs
Runs locally as a Command Palette extension
```

Keywords:

```text
command palette, powertoys, browser tabs, chrome, edge, tab switcher, productivity
```

Screenshots:

- Required: at least one Desktop screenshot.
- Recommended: four or more Desktop screenshots.
- Format: PNG.
- Size: 1366 x 768 pixels or larger.
- Limit: 50 MB per image.
- Capture Command Palette showing Browser Tabs search results, a filtered search, an active tab tag, and the extension entry point.

Uploadable Store art prepared locally:

```text
store-assets\store-logo-300x300.png
store-assets\super-hero-1920x1080.png
```

Use `store-assets\store-logo-300x300.png` for the 1:1 app tile icon. It is recommended because the Store prioritizes the uploaded 300 x 300 image over the icon included in the package.

The package also contains these MSIX visual assets:

```text
Assets\StoreLogo.png                         50 x 50
Assets\Square44x44Logo.png                   44 x 44
Assets\Square44x44Logo.scale-200.png         88 x 88
Assets\Square150x150Logo.png                150 x 150
Assets\Square150x150Logo.scale-200.png      300 x 300
Assets\SmallTile.png                         71 x 71
Assets\Wide310x150Logo.png                  310 x 150
Assets\Wide310x150Logo.scale-200.png        620 x 300
Assets\LargeTile.png                        310 x 310
Assets\SplashScreen.png                     620 x 300
Assets\SplashScreen.scale-200.png          1240 x 600
```

Recommended screenshot set:

```text
01-command-palette-browser-tabs-results.png
02-filter-open-tabs-by-title.png
03-switch-to-selected-tab.png
04-empty-state-or-extension-entry.png
```

Suggested captions:

```text
Search open Chrome and Edge tabs from Windows Command Palette.
Filter browser tabs by title, browser, or window text.
Switch directly to the selected browser tab.
Runs locally as a PowerToys Command Palette extension.
```

### Submission options

- Publishing hold: publish as soon as certification passes
- Notes for certification: paste the certification note below
- Restricted capabilities: provide the `runFullTrust` explanation below

Certification note:

```text
Browser Tabs is a Windows Command Palette extension, not a standalone app. Its app-list entry is disabled intentionally because users launch it through Microsoft PowerToys Command Palette. If the package executable is launched directly, it shows an informational dialog explaining that Browser Tabs must be opened from Command Palette.

To test it, install and enable Microsoft PowerToys Command Palette, open Chrome or Microsoft Edge with several tabs, launch Command Palette, open Browser Tabs, search for a tab title, and select a result. The extension should bring the browser window forward and activate the selected tab. No account, network service, or test credentials are required.
```

Restricted capability explanation for `runFullTrust`:

```text
Browser Tabs declares runFullTrust because Command Palette extensions use an out-of-process COM server and packaged COM registration. The extension runs as a desktop Command Palette extension, enumerates local Chrome and Microsoft Edge window/tab UI Automation elements, and activates the selected local browser tab. It does not use runFullTrust to elevate privileges, install services, modify system settings, or access user files.
```

Useful Microsoft docs:

- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension-store
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/create-app-submission
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/reserve-your-apps-name
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/upload-app-packages
