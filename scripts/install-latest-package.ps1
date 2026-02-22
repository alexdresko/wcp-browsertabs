param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceFolder
)

$ErrorActionPreference = "Stop"

$packageRoot = Join-Path $WorkspaceFolder "src/extension/WcpBrowserTabs/WcpBrowserTabs/AppPackages"

if (-not (Test-Path $packageRoot)) {
    throw "Package output folder not found: $packageRoot. Run package task first."
}

$package = Get-ChildItem -Path $packageRoot -Recurse -File |
    Where-Object {
        $_.Extension -in ".msix", ".appx", ".msixbundle", ".appxbundle" -and
        $_.FullName -notlike "*\Dependencies\*"
    } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $package) {
    throw "No main package found under $packageRoot. Run package task first."
}

$sig = Get-AuthenticodeSignature -FilePath $package.FullName
if ($sig.Status -ne "Valid") {
    throw "Package signature is not valid (status: $($sig.Status)): $($package.FullName)"
}

$install = {
    Add-AppxPackage -Path $package.FullName -ForceApplicationShutdown -ErrorAction Stop
}

try {
    & $install
}
catch {
    $message = $_.Exception.Message
    $isSameVersionConflict = $message -like "*0x80073CFB*"
    $isHigherVersionInstalled = $message -like "*0x80073D06*"

    if (-not ($isSameVersionConflict -or $isHigherVersionInstalled)) { throw }

    if ($isSameVersionConflict) {
        Write-Host "Detected same-version package conflict. Removing existing WcpBrowserTabs package(s) for current user and retrying."
    }
    else {
        Write-Host "Detected higher installed package version. Removing existing WcpBrowserTabs package(s) for current user and retrying."
    }

    $existing = Get-AppxPackage -Name WcpBrowserTabs -ErrorAction SilentlyContinue
    foreach ($app in $existing) {
        Remove-AppxPackage -Package $app.PackageFullName -ErrorAction Stop
    }

    & $install
}

Write-Host "Installed package: $($package.FullName)"

$cleanupScript = Join-Path $WorkspaceFolder "scripts\clean-cmdpal-browser-tabs-entries.ps1"
if (Test-Path $cleanupScript) {
    try {
        & $cleanupScript | Out-Host
    }
    catch {
        Write-Warning "CmdPal Browser Tabs cleanup failed (non-fatal): $($_.Exception.Message)"
    }
}
