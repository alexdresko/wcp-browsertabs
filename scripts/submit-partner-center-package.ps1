param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [Parameter(Mandatory = $true)]
    [string]$ApplicationId,

    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$GithubToken = $env:GITHUB_TOKEN,
    [string]$TenantId = $env:PARTNER_CENTER_TENANT_ID,
    [string]$ClientId = $env:PARTNER_CENTER_CLIENT_ID,
    [string]$ClientSecret = $env:PARTNER_CENTER_CLIENT_SECRET,
    [ValidateSet("draft", "submit")]
    [string]$Mode = "draft",
    [string]$ConfirmSubmit,
    [string]$OutputFolder,
    [int]$PollIntervalSeconds = 60,
    [int]$MaxPollMinutes = 60,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$storeApiBaseUrl = "https://manage.devcenter.microsoft.com/v1.0/my"
$storeResource = "https://manage.devcenter.microsoft.com"

function Assert-NotBlank {
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name is required."
    }
}

function ConvertTo-JsonBody {
    param(
        [Parameter(Mandatory = $true)]
        $Value
    )

    return $Value | ConvertTo-Json -Depth 100 -Compress
}

function Invoke-WithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Operation,
        [string]$Description = "request",
        [int]$MaxAttempts = 6
    )

    $attempt = 1
    $delaySeconds = 5

    while ($true) {
        try {
            return & $Operation
        }
        catch {
            $response = $_.Exception.Response
            $statusCode = $null

            if ($null -ne $response) {
                $statusCode = [int]$response.StatusCode
            }

            if ($statusCode -ne 429 -or $attempt -ge $MaxAttempts) {
                throw
            }

            $retryAfter = $null
            if ($null -ne $response.Headers) {
                $retryAfter = $response.Headers["Retry-After"]
            }

            if (-not [int]::TryParse($retryAfter, [ref]$delaySeconds)) {
                $delaySeconds = [Math]::Min(300, [Math]::Pow(2, $attempt) * 5)
            }

            Write-Warning ("Partner Center throttled {0}. Waiting {1} seconds before retry {2}/{3}." -f $Description, $delaySeconds, ($attempt + 1), $MaxAttempts)
            Start-Sleep -Seconds $delaySeconds
            $attempt++
        }
    }
}

function Invoke-GitHubApi {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri
    )

    $headers = @{
        Accept = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent" = "wcp-browsertabs-partner-center-submit"
    }

    if (-not [string]::IsNullOrWhiteSpace($GithubToken)) {
        $headers.Authorization = "Bearer $GithubToken"
    }

    return Invoke-RestMethod -Method Get -Uri $Uri -Headers $headers
}

function Get-ReleaseBundleAsset {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repo,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseTag
    )

    $release = Invoke-GitHubApi -Uri "https://api.github.com/repos/$Repo/releases/tags/$ReleaseTag"
    $bundleAssets = @($release.assets | Where-Object { $_.name -like "*.msixbundle" })

    if ($bundleAssets.Count -eq 0) {
        throw "Release $ReleaseTag has no .msixbundle asset."
    }

    if ($bundleAssets.Count -gt 1) {
        $names = ($bundleAssets | ForEach-Object { $_.name }) -join ", "
        throw "Release $ReleaseTag has multiple .msixbundle assets. Expected exactly one. Found: $names"
    }

    return $bundleAssets[0]
}

function Save-GitHubReleaseAsset {
    param(
        [Parameter(Mandatory = $true)]
        $Asset,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    $headers = @{
        Accept = "application/octet-stream"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent" = "wcp-browsertabs-partner-center-submit"
    }

    if (-not [string]::IsNullOrWhiteSpace($GithubToken)) {
        $headers.Authorization = "Bearer $GithubToken"
    }

    Invoke-WebRequest -Method Get -Uri $Asset.url -Headers $headers -OutFile $DestinationPath
}

function Get-PartnerCenterAccessToken {
    Assert-NotBlank -Name "PARTNER_CENTER_TENANT_ID" -Value $TenantId
    Assert-NotBlank -Name "PARTNER_CENTER_CLIENT_ID" -Value $ClientId
    Assert-NotBlank -Name "PARTNER_CENTER_CLIENT_SECRET" -Value $ClientSecret

    $body = @{
        grant_type = "client_credentials"
        client_id = $ClientId
        client_secret = $ClientSecret
        resource = $storeResource
    }

    $tokenResponse = Invoke-WithRetry -Description "Partner Center token request" -Operation {
        Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$TenantId/oauth2/token" -Body $body -ContentType "application/x-www-form-urlencoded"
    }

    return $tokenResponse.access_token
}

function Invoke-PartnerCenterApi {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Get", "Post", "Put")]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [string]$AccessToken,
        $Body
    )

    $headers = @{
        Authorization = "Bearer $AccessToken"
        Accept = "application/json"
    }

    $operation = {
        if ($null -eq $Body) {
            Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers
        }
        else {
            Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers -Body (ConvertTo-JsonBody -Value $Body) -ContentType "application/json"
        }
    }

    return Invoke-WithRetry -Description "$Method $Uri" -Operation $operation
}

function New-PackageUploadZip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath,
        [Parameter(Mandatory = $true)]
        [string]$ZipPath
    )

    if (Test-Path $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    Compress-Archive -LiteralPath $PackagePath -DestinationPath $ZipPath -CompressionLevel Optimal
}

function New-SubmittedPackageResource {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    return [ordered]@{
        fileName = $FileName
        fileStatus = "PendingUpload"
        minimumDirectXVersion = "None"
        minimumSystemRam = "None"
    }
}

function Set-SubmissionPackage {
    param(
        [Parameter(Mandatory = $true)]
        $Submission,
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $existingPackages = @($Submission.applicationPackages | Where-Object {
        $_.fileName -ne $FileName -and $_.fileStatus -ne "PendingDelete"
    })

    $Submission.applicationPackages = @($existingPackages + (New-SubmittedPackageResource -FileName $FileName))
    return $Submission
}

function Upload-SubmissionZip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SasUri,
        [Parameter(Mandatory = $true)]
        [string]$ZipPath
    )

    $bytes = [System.IO.File]::ReadAllBytes($ZipPath)
    $headers = @{
        "x-ms-blob-type" = "BlockBlob"
    }

    Invoke-WithRetry -Description "upload package ZIP" -Operation {
        Invoke-RestMethod -Method Put -Uri $SasUri -Headers $headers -Body $bytes -ContentType "application/zip"
    } | Out-Null
}

function Assert-NoPendingSubmission {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AccessToken
    )

    $appUrl = "$storeApiBaseUrl/applications/$ApplicationId"
    $app = Invoke-PartnerCenterApi -Method Get -Uri $appUrl -AccessToken $AccessToken
    $pendingSubmission = $app.pendingApplicationSubmission

    if ($null -ne $pendingSubmission -and -not [string]::IsNullOrWhiteSpace($pendingSubmission.id)) {
        throw "Partner Center already has a pending submission for ${ApplicationId}: $($pendingSubmission.id). Delete or finish that submission before creating another one."
    }
}

function Wait-SubmissionStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AccessToken,
        [Parameter(Mandatory = $true)]
        [string]$SubmissionId
    )

    $deadline = (Get-Date).AddMinutes($MaxPollMinutes)
    $statusUrl = "$storeApiBaseUrl/applications/$ApplicationId/submissions/$SubmissionId/status"
    $lastStatus = $null

    while ((Get-Date) -lt $deadline) {
        $status = Invoke-PartnerCenterApi -Method Get -Uri $statusUrl -AccessToken $AccessToken
        $lastStatus = $status
        $statusValue = $status.status
        Write-Host "Partner Center submission status: $statusValue"

        if ($statusValue -in @("Published", "PublishFailed", "CertificationFailed", "CommitFailed", "PreProcessingFailed", "ReleaseFailed")) {
            return $status
        }

        Start-Sleep -Seconds $PollIntervalSeconds
    }

    Write-Warning "Partner Center submission $SubmissionId was still in progress after $MaxPollMinutes minutes."
    return $lastStatus
}

if ($Tag -notmatch '^v?\d+\.\d+\.\d+$') {
    throw "Tag must look like v1.2.3 or 1.2.3. Received: $Tag"
}

Assert-NotBlank -Name "Repository" -Value $Repository
Assert-NotBlank -Name "ApplicationId" -Value $ApplicationId

if ($Mode -eq "submit" -and $ConfirmSubmit -ne "SUBMIT") {
    throw "Submitting for certification requires -ConfirmSubmit SUBMIT."
}

if (-not $OutputFolder) {
    $safeTag = $Tag -replace '[^A-Za-z0-9_.-]', '-'
    $OutputFolder = Join-Path (Join-Path (Get-Location) "artifacts\partner-center") $safeTag
}

New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

$asset = Get-ReleaseBundleAsset -Repo $Repository -ReleaseTag $Tag
$packagePath = Join-Path $OutputFolder $asset.name
$zipPath = Join-Path $OutputFolder ("partner-center-upload-$Tag.zip")

Write-Host "Resolved release asset: $($asset.name)"
Write-Host "Release asset URL: $($asset.browser_download_url)"
Write-Host "Output folder: $OutputFolder"

if ($DryRun) {
    Write-Host "Dry run: skipping release asset download, Partner Center authentication, upload, update, and commit."
    Write-Host "Would create Partner Center app submission for application: $ApplicationId"
    Write-Host "Would add package resource with fileName: $($asset.name)"
    Write-Host "Would upload ZIP: $zipPath"
    Write-Host "Would leave draft uncommitted." -NoNewline
    if ($Mode -eq "submit") {
        Write-Host " Would commit because mode is submit and confirmation was provided."
    }
    else {
        Write-Host
    }
    exit 0
}

Save-GitHubReleaseAsset -Asset $asset -DestinationPath $packagePath
New-PackageUploadZip -PackagePath $packagePath -ZipPath $zipPath

Write-Host "Downloaded package: $packagePath"
Write-Host "Created Partner Center upload ZIP: $zipPath"

$accessToken = Get-PartnerCenterAccessToken
Assert-NoPendingSubmission -AccessToken $accessToken

$createUrl = "$storeApiBaseUrl/applications/$ApplicationId/submissions"
$submission = Invoke-PartnerCenterApi -Method Post -Uri $createUrl -AccessToken $accessToken

if ([string]::IsNullOrWhiteSpace($submission.id)) {
    throw "Partner Center did not return a submission id."
}

if ([string]::IsNullOrWhiteSpace($submission.fileUploadUrl)) {
    throw "Partner Center did not return a fileUploadUrl for submission $($submission.id)."
}

Write-Host "Created Partner Center submission: $($submission.id)"

$submission = Set-SubmissionPackage -Submission $submission -FileName $asset.name
$updateUrl = "$storeApiBaseUrl/applications/$ApplicationId/submissions/$($submission.id)"
Invoke-PartnerCenterApi -Method Put -Uri $updateUrl -AccessToken $accessToken -Body $submission | Out-Null
Upload-SubmissionZip -SasUri $submission.fileUploadUrl -ZipPath $zipPath

Write-Host "Updated Partner Center draft and uploaded package archive."

if ($Mode -eq "draft") {
    Write-Host "Draft mode complete. Certification was not requested."
    Write-Host "Submission id: $($submission.id)"
    exit 0
}

$commitUrl = "$storeApiBaseUrl/applications/$ApplicationId/submissions/$($submission.id)/commit"
$commit = Invoke-PartnerCenterApi -Method Post -Uri $commitUrl -AccessToken $accessToken
Write-Host "Commit requested. Initial status: $($commit.status)"

$finalStatus = Wait-SubmissionStatus -AccessToken $accessToken -SubmissionId $submission.id
Write-Host "Final observed status: $($finalStatus.status)"

if ($finalStatus.status -in @("PublishFailed", "CertificationFailed", "CommitFailed", "PreProcessingFailed", "ReleaseFailed")) {
    throw "Partner Center submission failed with status: $($finalStatus.status)"
}
