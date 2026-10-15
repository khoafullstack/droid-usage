<#
.SYNOPSIS
    Builds Velopack installers (Setup.exe + update packages) for Factory Droid Usage.

.PARAMETER Version
    SemVer to build (X.Y.Z). Defaults to the vX.Y.Z git tag on HEAD.

.PARAMETER Arch
    Architectures to build. Default: x64 and arm64.

.PARAMETER Publish
    Upload the result to GitHub Releases. Requires GITHUB_TOKEN.

.PARAMETER SkipTagCheck
    Allow building without a tag and with a dirty working tree (local testing only).
#>
[CmdletBinding()]
param(
    [string]$Version,
    [ValidateSet('x64', 'arm64')]
    [string[]]$Arch = @('x64', 'arm64'),
    [switch]$Publish,
    [switch]$SkipTagCheck
)

$ErrorActionPreference = 'Stop'

$PackId = 'FactoryDroidUsage'
$PackTitle = 'Factory Droid Usage'
$MainExe = 'DroidUsage.exe'

$WindowsDir = Split-Path -Parent $PSScriptRoot
$RepoRoot = Split-Path -Parent $WindowsDir
$Project = Join-Path $WindowsDir 'DroidUsage\DroidUsage.csproj'
$Icon = Join-Path $WindowsDir 'DroidUsage\Assets\factory.ico'
$ArtifactsDir = Join-Path $WindowsDir 'artifacts'
$ReleasesDir = Join-Path $WindowsDir 'releases'

# Single source of truth: the same URL the app uses to look for updates.
$RepoUrl = $null
if ($Publish) {
    $endpoints = Get-Content (Join-Path $WindowsDir 'DroidUsage\Configuration\FactoryEndpoints.cs') -Raw
    if ($endpoints -match 'ReleasesRepoUrl\s*=\s*"([^"]+)"') {
        $RepoUrl = $Matches[1]
    }
    if (-not $RepoUrl -or $RepoUrl -match '<owner>') {
        throw 'Set FactoryEndpoints.ReleasesRepoUrl to the real GitHub repository before publishing.'
    }
}

function Invoke-Checked {
    param([string]$Description, [scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE"
    }
}

Push-Location $RepoRoot
try {
    if (-not $Version) {
        $tag = (git describe --tags --exact-match HEAD 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $tag) {
            if (-not $SkipTagCheck) {
                throw 'HEAD has no tag. Create one (git tag v1.2.3) or pass -Version with -SkipTagCheck.'
            }
            throw 'No tag on HEAD and no -Version given.'
        }
        $Version = $tag.Trim().TrimStart('v')
    }

    if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
        throw "Version '$Version' is not valid SemVer (expected X.Y.Z)."
    }

    if (-not $SkipTagCheck) {
        $dirty = git status --porcelain
        if ($dirty) {
            throw 'Working tree is not clean. Commit or stash changes, or pass -SkipTagCheck.'
        }
    }

    Write-Host "Building $PackTitle $Version for: $($Arch -join ', ')" -ForegroundColor Cyan

    Invoke-Checked 'dotnet tool restore' { dotnet tool restore }

    New-Item -ItemType Directory -Force -Path $ReleasesDir | Out-Null

    foreach ($a in $Arch) {
        $rid = "win-$a"
        $publishDir = Join-Path $ArtifactsDir "publish\$a"

        if (Test-Path $publishDir) {
            Remove-Item -Recurse -Force $publishDir
        }

        Write-Host "`n[$a] dotnet publish" -ForegroundColor Cyan
        Invoke-Checked "dotnet publish ($a)" {
            dotnet publish $Project -c Release -r $rid --self-contained `
                -p:Platform=$a -p:Version=$Version -o $publishDir
        }

        if ($Publish) {
            if (-not $env:GITHUB_TOKEN) {
                throw 'GITHUB_TOKEN is not set.'
            }
            Write-Host "[$a] fetching previous release for delta packages" -ForegroundColor Cyan
            dotnet vpk download github --repoUrl $RepoUrl --channel $rid --outputDir $ReleasesDir --token $env:GITHUB_TOKEN
        }

        Write-Host "[$a] vpk pack" -ForegroundColor Cyan
        Invoke-Checked "vpk pack ($a)" {
            dotnet vpk pack `
                --packId $PackId `
                --packTitle $PackTitle `
                --packVersion $Version `
                --packDir $publishDir `
                --mainExe $MainExe `
                --icon $Icon `
                --runtime $rid `
                --channel $rid `
                --outputDir $ReleasesDir
        }
    }

    if ($Publish) {
        foreach ($a in $Arch) {
            $rid = "win-$a"
            Write-Host "[$a] uploading to GitHub Releases" -ForegroundColor Cyan
            Invoke-Checked "vpk upload ($a)" {
                dotnet vpk upload github `
                    --repoUrl $RepoUrl `
                    --token $env:GITHUB_TOKEN `
                    --tag "v$Version" `
                    --releaseName "$PackTitle $Version" `
                    --channel $rid `
                    --outputDir $ReleasesDir `
                    --publish
            }
        }
    }

    Write-Host "`nInstallers:" -ForegroundColor Green
    Get-ChildItem $ReleasesDir -Filter '*Setup.exe' | ForEach-Object { Write-Host "  $($_.FullName)" }
}
finally {
    Pop-Location
}
