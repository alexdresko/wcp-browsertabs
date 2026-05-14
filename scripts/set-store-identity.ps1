param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceFolder,

    [Parameter(Mandatory = $true)]
    [string]$IdentityName,

    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [Parameter(Mandatory = $true)]
    [string]$PublisherDisplayName,

    [string]$DisplayName = "Browser Tabs",

    [string]$Version = "0.0.1.0"
)

$ErrorActionPreference = "Stop"

$projectRoot = Join-Path $WorkspaceFolder "src/extension/WcpBrowserTabs/WcpBrowserTabs"
$projectPath = Join-Path $projectRoot "WcpBrowserTabs.csproj"
$manifestPath = Join-Path $projectRoot "Package.appxmanifest"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not (Test-Path $manifestPath)) {
    throw "Manifest file not found: $manifestPath"
}

function Set-XmlElementText {
    param(
        [xml]$Xml,
        [string]$ElementName,
        [string]$Value
    )

    $node = $Xml.Project.PropertyGroup |
        Where-Object { $null -ne $_.$ElementName } |
        Select-Object -First 1

    if ($null -eq $node) {
        $node = $Xml.Project.PropertyGroup |
            Where-Object { -not $_.Condition } |
            Select-Object -First 1

        if ($null -eq $node) {
            throw "Could not find an unconditional PropertyGroup in $projectPath"
        }

        $child = $Xml.CreateElement($ElementName)
        [void]$node.AppendChild($child)
    }

    $node.$ElementName = $Value
}

[xml]$projectXml = Get-Content -Path $projectPath -Raw
Set-XmlElementText -Xml $projectXml -ElementName "AppxPackageIdentityName" -Value $IdentityName
Set-XmlElementText -Xml $projectXml -ElementName "AppxPackagePublisher" -Value $Publisher
Set-XmlElementText -Xml $projectXml -ElementName "AppxPackageVersion" -Value $Version
$projectXml.Save($projectPath)

[xml]$manifestXml = Get-Content -Path $manifestPath -Raw
$manifestXml.Package.Identity.Name = $IdentityName
$manifestXml.Package.Identity.Publisher = $Publisher
$manifestXml.Package.Identity.Version = $Version
$manifestXml.Package.Properties.DisplayName = $DisplayName
$manifestXml.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$manifestXml.Save($manifestPath)

Write-Host "Updated Store identity:"
Write-Host "  Identity Name: $IdentityName"
Write-Host "  Publisher: $Publisher"
Write-Host "  Publisher Display Name: $PublisherDisplayName"
Write-Host "  Display Name: $DisplayName"
Write-Host "  Version: $Version"
