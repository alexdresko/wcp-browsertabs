param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceFolder
)

$ErrorActionPreference = "Stop"

$projectRoot = Join-Path $WorkspaceFolder "src/extension/WcpBrowserTabs/WcpBrowserTabs"
$project = Join-Path $projectRoot "WcpBrowserTabs.csproj"
$manifestPath = Join-Path $projectRoot "Package.appxmanifest"
$certPath = Join-Path $projectRoot "DevCert\WcpBrowserTabsDev.cer"

if (-not (Test-Path $project)) { throw "Project file not found: $project" }
if (-not (Test-Path $manifestPath)) { throw "Manifest file not found: $manifestPath" }
if (-not (Test-Path $certPath)) { throw "Signing certificate not found: $certPath" }

$thumbprint = (Get-PfxCertificate -FilePath $certPath).Thumbprint
$signingCert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Thumbprint -eq $thumbprint -and $_.HasPrivateKey } |
    Select-Object -First 1

if ($null -eq $signingCert) {
    throw "Signing certificate private key not found in Cert:\CurrentUser\My for thumbprint $thumbprint. Import DevCert\WcpBrowserTabsDev.pfx into CurrentUser\Personal, then retry."
}

$utcNow = [DateTime]::UtcNow
$build = [int]($utcNow.Date - [DateTime]"2024-01-01").TotalDays
$revision = [int]($utcNow.TimeOfDay.TotalSeconds / 2)
$appxVersion = "0.0.$build.$revision"
Write-Host "Using package version: $appxVersion"

$manifestOriginal = Get-Content -Path $manifestPath -Raw
$manifestUpdated = $manifestOriginal -replace '(?s)(<Identity\s+[^>]*?Version=")[^"]+(")', ('${1}' + $appxVersion + '${2}')

if ($manifestUpdated -eq $manifestOriginal) {
    throw "Failed to update package version in $manifestPath"
}

try {
    Set-Content -Path $manifestPath -Value $manifestUpdated -Encoding utf8

    dotnet msbuild $project `
        /t:Restore,Build `
        /p:Configuration=Debug `
        /p:Platform=x64 `
        /p:GenerateAppxPackageOnBuild=true `
        /p:UapAppxPackageBuildMode=SideloadOnly `
        /p:AppxBundle=Never `
        /p:PublishSingleFile=false `
        /p:AppxPackageDir=AppPackages\ `
        /p:AppxPackageSigningEnabled=true `
        "/p:PackageCertificateThumbprint=$thumbprint" `
        /p:PackageCertificateStoreName=My

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Set-Content -Path $manifestPath -Value $manifestOriginal -Encoding utf8
}
