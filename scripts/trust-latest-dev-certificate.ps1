param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceFolder
)

$ErrorActionPreference = "Stop"

$projectRoot = Join-Path $WorkspaceFolder "src/extension/WcpBrowserTabs/WcpBrowserTabs"
$packageRoot = Join-Path $projectRoot "AppPackages"
$cert = $null

if (Test-Path $packageRoot) {
    $cert = Get-ChildItem -Path $packageRoot -Recurse -File -Filter *.cer |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

if ($null -eq $cert) {
    $devCertRoot = Join-Path $projectRoot "DevCert"
    if (Test-Path $devCertRoot) {
        $cert = Get-ChildItem -Path $devCertRoot -Recurse -File -Filter *.cer |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
    }
}

if ($null -eq $cert) {
    Write-Host "No .cer file found under $packageRoot or $devCertRoot. Skipping trust step."
    exit 0
}

$thumbprint = (Get-PfxCertificate -FilePath $cert.FullName).Thumbprint
$lmTrusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $thumbprint } |
    Select-Object -First 1
$lmRoot = Get-ChildItem Cert:\LocalMachine\Root -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $thumbprint } |
    Select-Object -First 1

if ($null -eq $lmTrusted -or $null -eq $lmRoot) {
    $escapedCertPath = $cert.FullName.Replace("'", "''")
    $elevatedCommand = "Import-Certificate -FilePath '$escapedCertPath' -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null; Import-Certificate -FilePath '$escapedCertPath' -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null"
    $p = Start-Process -FilePath powershell -Verb RunAs -PassThru -Wait -ArgumentList @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-Command',
        $elevatedCommand
    )
    if ($p.ExitCode -ne 0) {
        throw "Failed to import certificate into LocalMachine stores (exit code $($p.ExitCode))."
    }
}

Import-Certificate -FilePath $cert.FullName -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
Write-Host "Trusted certificate: $($cert.FullName) (thumbprint: $thumbprint)"
