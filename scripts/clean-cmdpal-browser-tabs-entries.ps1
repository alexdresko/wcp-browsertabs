param(
    [string]$CmdPalPackageFamily = "Microsoft.CommandPalette_8wekyb3d8bbwe",
    [string]$ExtensionPackageName = "WcpBrowserTabs",
    [string]$ExtensionDisplayName = "Browser Tabs",
    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"

$localStateRoot = Join-Path $env:LOCALAPPDATA ("Packages\" + $CmdPalPackageFamily + "\LocalState")
$settingsPath = Join-Path $localStateRoot "settings.json"
$statePath = Join-Path $localStateRoot "state.json"

if (-not (Test-Path $settingsPath)) {
    throw "CmdPal settings file not found: $settingsPath"
}

if (-not (Test-Path $statePath)) {
    throw "CmdPal state file not found: $statePath"
}

$cmdPalProcessName = "Microsoft.CmdPal.UI"
$cmdPalWasRunning = $null -ne (Get-Process -Name $cmdPalProcessName -ErrorAction SilentlyContinue)
if ($cmdPalWasRunning) {
    Get-Process -Name $cmdPalProcessName -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$settingsBackupPath = "$settingsPath.bak.$stamp"
$stateBackupPath = "$statePath.bak.$stamp"
Copy-Item $settingsPath $settingsBackupPath
Copy-Item $statePath $stateBackupPath

$removedProviderKeys = @()
$removedHistoryCount = 0

$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
if ($null -ne $settings.ProviderSettings) {
    $providerProperties = @($settings.ProviderSettings.PSObject.Properties)
    foreach ($property in $providerProperties) {
        $name = $property.Name
        if ($name -like ($ExtensionPackageName + "_*") -or $name -like ("*" + $ExtensionDisplayName + "*")) {
            $removedProviderKeys += $name
        }
    }

    foreach ($key in $removedProviderKeys) {
        [void]$settings.ProviderSettings.PSObject.Properties.Remove($key)
    }
}

$settings | ConvertTo-Json -Depth 50 | Set-Content -Path $settingsPath -Encoding utf8

$state = Get-Content $statePath -Raw | ConvertFrom-Json
if ($null -ne $state.RecentCommands -and $null -ne $state.RecentCommands.History) {
    $history = @($state.RecentCommands.History)
    $filteredHistory = @(
        $history | Where-Object {
            $_.CommandId -notlike ($ExtensionPackageName + "_*") -and
            $_.CommandId -notlike ($ExtensionDisplayName + "*")
        }
    )

    $removedHistoryCount = $history.Count - $filteredHistory.Count
    $state.RecentCommands.History = $filteredHistory
}

$state | ConvertTo-Json -Depth 50 | Set-Content -Path $statePath -Encoding utf8

$restartedCmdPal = $false
if (-not $NoRestart -and $cmdPalWasRunning) {
    Start-Process explorer.exe ("shell:AppsFolder\" + $CmdPalPackageFamily + "!App")
    $restartedCmdPal = $true
}

[pscustomobject]@{
    CmdPalLocalState = $localStateRoot
    SettingsBackup = $settingsBackupPath
    StateBackup = $stateBackupPath
    RemovedProviderCount = $removedProviderKeys.Count
    RemovedProviderKeys = ($removedProviderKeys -join "; ")
    RemovedHistoryCount = $removedHistoryCount
    RestartedCmdPal = $restartedCmdPal
} | Format-List
