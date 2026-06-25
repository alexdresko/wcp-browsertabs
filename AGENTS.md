# Project Guidelines

## Application Overview

This repository contains a Windows Command Palette extension named **Browser Tabs**.

- It discovers open browser tabs from supported browsers (currently Chrome, Edge, and Firefox).
- It shows those tabs in Command Palette so users can search and switch quickly.
- It is implemented as a COM-based Windows app extension.

## Documentation

- [Command Palette Overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview)
- [Command Palette Extensibility](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)

## Code Location

- Main extension solution: `src/extension/WcpBrowserTabs/WcpBrowserTabs.sln`
- Extension project (core feature code): `src/extension/WcpBrowserTabs/WcpBrowserTabs/`
- Browser tab discovery and activation:
  - `src/extension/WcpBrowserTabs/WcpBrowserTabs/BrowserTabDiscoveryService.cs`
  - `src/extension/WcpBrowserTabs/WcpBrowserTabs/BrowserWindowActivator.cs`
  - `src/extension/WcpBrowserTabs/WcpBrowserTabs/ActivateBrowserTabCommand.cs`
- Command Palette provider/page wiring:
  - `src/extension/WcpBrowserTabs/WcpBrowserTabs/WcpBrowserTabsCommandsProvider.cs`
  - `src/extension/WcpBrowserTabs/WcpBrowserTabs/Pages/WcpBrowserTabsPage.cs`
- Prototype API project: `src/Poc/`

## Commit Conventions

All commits MUST follow the [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Types

- **feat**: New feature
- **fix**: Bug fix
- **docs**: Documentation changes
- **style**: Code style changes (formatting, no logic change)
- **refactor**: Code refactoring (no feature or bug fix)
- **perf**: Performance improvements
- **test**: Adding or updating tests
- **build**: Build system or dependency changes
- **ci**: CI/CD configuration changes
- **chore**: Maintenance tasks

### Examples

```
feat(browser-tabs): add edge browser support
fix(activation): resolve tab focus issue on multi-monitor setup
docs: update installation instructions
refactor(discovery): simplify tab enumeration logic
```

### Rules

- Use lowercase for type and description
- Keep description under 72 characters
- Use imperative mood ("add" not "added" or "adds")
- Breaking changes MUST include `BREAKING CHANGE:` in footer or `!` after type
