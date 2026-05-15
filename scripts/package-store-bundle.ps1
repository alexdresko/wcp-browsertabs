param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceFolder,

    [string]$Configuration = "Release",
    [string]$Version,
    [string]$IdentityName,
    [string]$Publisher,
    [string]$MakeAppxPath,
    [string]$OutputFolder,
    [switch]$BumpVersion,
    [switch]$UpdateVersion,
    [switch]$SkipBuild,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-MakeAppxExecutable {
    param(
        [string]$ExplicitPath
    )

    if ($ExplicitPath) {
        if (-not (Test-Path $ExplicitPath)) {
            throw "makeappx.exe not found at explicit path: $ExplicitPath"
        }

        return (Resolve-Path $ExplicitPath).Path
    }

    $arch = switch ($env:PROCESSOR_ARCHITECTURE) {
        "AMD64" { "x64" }
        "x86" { "x86" }
        "ARM64" { "arm64" }
        default { "x64" }
    }

    $found = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\$arch\makeappx.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if ($null -eq $found) {
        throw "Unable to locate makeappx.exe. Install the Windows SDK or pass -MakeAppxPath."
    }

    return $found.FullName
}

function Invoke-DotnetMsbuild {
    param(
        [string]$ProjectPath,
        [string[]]$Arguments,
        [switch]$DryRunMode
    )

    $display = @("dotnet", "msbuild", $ProjectPath) + $Arguments
    Write-Host ($display -join " ")

    if ($DryRunMode) {
        return
    }

    & dotnet msbuild $ProjectPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Get-LatestPackageForArch {
    param(
        [string]$PackageRoot,
        [string]$Architecture
    )

    $suffix = "_$Architecture.msix"

    return Get-ChildItem -Path $PackageRoot -Recurse -File -Filter *.msix |
        Where-Object {
            $_.FullName -notlike "*\Dependencies\*" -and
            $_.Name.EndsWith($suffix, [System.StringComparison]::OrdinalIgnoreCase)
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Read-ProjectProperty {
    param(
        [xml]$ProjectXml,
        [string]$Name
    )

    $values = @()
    foreach ($group in $ProjectXml.Project.PropertyGroup) {
        $candidate = $group.$Name
        if ($null -eq $candidate) {
            continue
        }

        if ($candidate -is [System.Array]) {
            foreach ($item in $candidate) {
                if ($item.InnerText) {
                    $values += $item.InnerText
                }
            }
        }
        elseif ($candidate.InnerText) {
            $values += $candidate.InnerText
        }
        elseif ([string]$candidate) {
            $values += [string]$candidate
        }
    }

    return ($values | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
}

function ConvertTo-MSBuildPropertyValue {
    param(
        [string]$Value
    )

    return $Value.
        Replace("%", "%25").
        Replace(";", "%3B").
        Replace(",", "%2C")
}

function Get-NextAppxPackageVersion {
    param(
        [string]$CurrentVersion
    )

    $parts = $CurrentVersion -split "\."
    if ($parts.Length -ne 4) {
        throw "Appx package version must have four numeric parts: $CurrentVersion"
    }

    $numbers = @()
    foreach ($part in $parts) {
        $value = 0
        if (-not [int]::TryParse($part, [ref]$value) -or $value -lt 0 -or $value -gt 65535) {
            throw "Appx package version parts must be numbers from 0 through 65535: $CurrentVersion"
        }

        $numbers += $value
    }

    if ($numbers[2] -ge 65535) {
        throw "Cannot bump Appx package build version because it is already 65535: $CurrentVersion"
    }

    $numbers[2] += 1
    $numbers[3] = 0

    return ($numbers -join ".")
}

function Set-ProjectProperty {
    param(
        [xml]$ProjectXml,
        [string]$Name,
        [string]$Value
    )

    foreach ($group in $ProjectXml.Project.PropertyGroup) {
        $candidate = $group.$Name
        if ($null -ne $candidate) {
            $group.$Name = $Value
            return
        }
    }

    $targetGroup = $ProjectXml.Project.PropertyGroup |
        Where-Object { -not $_.Condition } |
        Select-Object -First 1

    if ($null -eq $targetGroup) {
        throw "Could not find an unconditional PropertyGroup in project file."
    }

    $child = $ProjectXml.CreateElement($Name)
    $child.InnerText = $Value
    [void]$targetGroup.AppendChild($child)
}

function Set-PackageManifestVersion {
    param(
        [xml]$ManifestXml,
        [string]$Version
    )

    if ($null -eq $ManifestXml.Package.Identity) {
        throw "Could not find Package/Identity in manifest."
    }

    $ManifestXml.Package.Identity.Version = $Version
}

$projectRoot = Join-Path $WorkspaceFolder "src/extension/WcpBrowserTabs/WcpBrowserTabs"
$projectPath = Join-Path $projectRoot "WcpBrowserTabs.csproj"
$manifestPath = Join-Path $projectRoot "Package.appxmanifest"
$packageRoot = Join-Path $projectRoot "AppPackages"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not (Test-Path $manifestPath)) {
    throw "Manifest file not found: $manifestPath"
}

[xml]$projectXml = Get-Content -Path $projectPath -Raw
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)

$projectVersion = Read-ProjectProperty -ProjectXml $projectXml -Name "AppxPackageVersion"

if ($BumpVersion -and $Version) {
    throw "Pass either -BumpVersion or -Version, not both."
}

$resolvedVersion = if ($BumpVersion) {
    Get-NextAppxPackageVersion -CurrentVersion $projectVersion
}
elseif ($Version) {
    $Version
}
else {
    $projectVersion
}

$resolvedIdentityName = if ($IdentityName) { $IdentityName } else { Read-ProjectProperty -ProjectXml $projectXml -Name "AppxPackageIdentityName" }
$resolvedPublisher = if ($Publisher) { $Publisher } else { Read-ProjectProperty -ProjectXml $projectXml -Name "AppxPackagePublisher" }

if ([string]::IsNullOrWhiteSpace($resolvedVersion)) {
    throw "No Appx package version found. Set <AppxPackageVersion> in $projectPath or pass -Version."
}

if ([string]::IsNullOrWhiteSpace($resolvedIdentityName)) {
    throw "No Appx package identity name found. Set <AppxPackageIdentityName> in $projectPath or pass -IdentityName."
}

if ([string]::IsNullOrWhiteSpace($resolvedPublisher)) {
    throw "No Appx package publisher found. Set <AppxPackagePublisher> in $projectPath or pass -Publisher."
}

if ($resolvedPublisher -eq "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US") {
    Write-Warning "AppxPackagePublisher still matches the sample/template publisher. Replace it with your Partner Center publisher before Store submission."
}

if ($BumpVersion -or $UpdateVersion) {
    [xml]$manifestXml = Get-Content -Path $manifestPath -Raw

    Set-ProjectProperty -ProjectXml $projectXml -Name "AppxPackageVersion" -Value $resolvedVersion
    Set-PackageManifestVersion -ManifestXml $manifestXml -Version $resolvedVersion

    if (-not $DryRun) {
        $projectXml.Save($projectPath)
        $manifestXml.Save($manifestPath)
    }

    Write-Host "Updated Appx package version: $resolvedVersion"
}

if (-not $OutputFolder) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputFolder = Join-Path $WorkspaceFolder "artifacts\store\$timestamp"
}

New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

$commonMsbuildArgs = @(
    "/t:Restore,Build",
    "/p:Configuration=$Configuration",
    "/p:GenerateAppxPackageOnBuild=true",
    "/p:UapAppxPackageBuildMode=StoreUpload",
    "/p:AppxBundle=Never",
    "/p:PublishSingleFile=false",
    "/p:PublishTrimmed=false",
    "/p:AppxPackageDir=AppPackages\",
    "/p:AppxPackageSigningEnabled=false",
    "/p:AppxPackageVersion=$(ConvertTo-MSBuildPropertyValue $resolvedVersion)",
    "/p:AppxPackageIdentityName=$(ConvertTo-MSBuildPropertyValue $resolvedIdentityName)",
    "/p:AppxPackagePublisher=$(ConvertTo-MSBuildPropertyValue $resolvedPublisher)"
)

if (-not $SkipBuild) {
    Invoke-DotnetMsbuild -ProjectPath $projectPath -Arguments ($commonMsbuildArgs + "/p:Platform=x64") -DryRunMode:$DryRun
    Invoke-DotnetMsbuild -ProjectPath $projectPath -Arguments ($commonMsbuildArgs + "/p:Platform=ARM64") -DryRunMode:$DryRun
}

if ($DryRun) {
    Write-Host "Dry run completed. Skipped package discovery and bundling."
    exit 0
}

if (-not (Test-Path $packageRoot)) {
    throw "Package output folder not found: $packageRoot"
}

$x64Package = Get-LatestPackageForArch -PackageRoot $packageRoot -Architecture "x64"
$arm64Package = Get-LatestPackageForArch -PackageRoot $packageRoot -Architecture "arm64"

if ($null -eq $x64Package) {
    throw "Unable to find the generated x64 MSIX package under $packageRoot"
}

if ($null -eq $arm64Package) {
    throw "Unable to find the generated arm64 MSIX package under $packageRoot"
}

$bundleBaseName = "{0}_{1}_Bundle" -f $projectName, $resolvedVersion
$bundlePath = Join-Path $OutputFolder ($bundleBaseName + ".msixbundle")
$mappingPath = Join-Path $OutputFolder "bundle_mapping.txt"

$mappingContent = @(
    "[Files]",
    ('"{0}" "{1}"' -f $x64Package.FullName, $x64Package.Name),
    ('"{0}" "{1}"' -f $arm64Package.FullName, $arm64Package.Name)
) -join [Environment]::NewLine

Set-Content -Path $mappingPath -Value $mappingContent -Encoding ascii

$resolvedMakeAppx = Get-MakeAppxExecutable -ExplicitPath $MakeAppxPath
Write-Host "Using makeappx: $resolvedMakeAppx"

Write-Host ("{0} (x64): {1}" -f $x64Package.Name, $x64Package.FullName)
Write-Host ("{0} (arm64): {1}" -f $arm64Package.Name, $arm64Package.FullName)

& $resolvedMakeAppx bundle /f $mappingPath /p $bundlePath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path $bundlePath)) {
    throw "Bundle file was not created: $bundlePath"
}

Write-Host "Created Store bundle: $bundlePath"
Write-Host "Bundle mapping: $mappingPath"
