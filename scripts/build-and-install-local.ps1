param(
    [string]$WorkspaceFolder = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$UseManifestVersion
)

$ErrorActionPreference = "Stop"

$workspace = (Resolve-Path $WorkspaceFolder).Path

$packageScript = Join-Path $PSScriptRoot "package-extension-x64.ps1"
$trustScript = Join-Path $PSScriptRoot "trust-latest-dev-certificate.ps1"
$installScript = Join-Path $PSScriptRoot "install-latest-package.ps1"

Write-Host "Building local x64 package..."
& $packageScript -WorkspaceFolder $workspace -UseManifestVersion:$UseManifestVersion

Write-Host "Trusting latest development certificate..."
& $trustScript -WorkspaceFolder $workspace

Write-Host "Installing latest local package..."
& $installScript -WorkspaceFolder $workspace
