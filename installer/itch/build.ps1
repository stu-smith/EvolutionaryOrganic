<#
.SYNOPSIS
    Builds the Morphogen payload for itch.io, and optionally pushes it.

.DESCRIPTION
    Produces two things in installer\itch\out:

      payload\                                  The folder butler uploads. Self-contained, so it
                                                runs on a clean machine with no .NET runtime.
      Morphogen-itch-win-x64.zip     The same thing zipped, for uploading by hand
                                                through the itch.io web form if butler is not
                                                available.

    Self-contained on purpose. itch.io downloads are expected to run straight out of the folder,
    and neither the itch app nor a plain unzip will install a prerequisite for you, so a
    framework-dependent build would simply fail on a machine without the .NET Desktop Runtime.

    butler uploads a FOLDER, not an archive. It diffs against the previous build and uploads only
    what changed, which is what makes a self-contained WPF app reasonable to ship repeatedly.
    Pushing the zip instead would throw that away and re-upload everything every time.

.PARAMETER Version
    Three-part version. Stamped into the binaries, and sent to itch as the build's user version.
    Defaults to the most recent git tag, so the common case needs no argument. Pass it explicitly
    only when you mean to stamp something the tags do not say.

.PARAMETER SkipPublish
    Reuse the existing publish output instead of rebuilding the app.

.PARAMETER Push
    Upload to itch.io after building. Needs butler on PATH, or -ButlerPath, or -FetchButler.

.PARAMETER Target
    The itch.io game, as user/game. Defaults to $env:ITCH_TARGET, or whitelightrising/morphogen.

.PARAMETER Channel
    Channel to push to. butler infers the platform from the name, so it wants 'windows' in it.

.PARAMETER ButlerPath
    Path to butler.exe, if it is not on PATH.

.PARAMETER FetchButler
    Download butler into obj\butler rather than expecting it to be installed.

.EXAMPLE
    .\build.ps1
    Builds at the version from the most recent git tag.

.EXAMPLE
    .\build.ps1 -Push -Target whitelightrising/morphogen -FetchButler
    Same version, and publishes it.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

    [switch] $SkipPublish,

    [switch] $Push,

    [string] $Target = $(if ($env:ITCH_TARGET) { $env:ITCH_TARGET } else { 'whitelightrising/morphogen' }),

    [ValidatePattern('^[a-z0-9][a-z0-9-]*$')]
    [string] $Channel = 'windows',

    [string] $ButlerPath,

    [switch] $FetchButler
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$itchDir    = $PSScriptRoot
$repoRoot   = Resolve-Path (Join-Path $itchDir '..\..')
$objDir     = Join-Path $itchDir 'obj'
$outDir     = Join-Path $itchDir 'out'
$publishDir = Join-Path $objDir 'publish'
$payloadDir = Join-Path $outDir 'payload'
$zipPath    = Join-Path $outDir 'Morphogen-itch-win-x64.zip'
$exeName    = 'Morphogen.exe'

$butlerUrl = 'https://broth.itch.zone/butler/windows-amd64/LATEST/archive/default'

foreach ($dir in @($objDir, $outDir)) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
}

function Invoke-Checked {
    param([string] $What, [scriptblock] $Action)
    Write-Host "==> $What" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE" }
}

# --- 0. The version -----------------------------------------------------------------------------
# Derived from the tags rather than hardcoded, because a hand-passed version is checked against
# nothing and goes straight onto the storefront.
if (-not $Version) {
    $tag = $null
    try {
        $ErrorActionPreference = 'Continue'
        $tag = & git -C $repoRoot describe --tags --abbrev=0 2>$null
    }
    catch {
        $tag = $null    # No git on PATH, or not a repository. Fall through to the default.
    }
    finally {
        $ErrorActionPreference = 'Stop'
    }

    if ($tag -and $LASTEXITCODE -eq 0 -and $tag -match '^v?(\d+\.\d+\.\d+)$') {
        $Version = $Matches[1]
        Write-Host "==> Version $Version, from git tag $tag" -ForegroundColor Cyan
    }
    else {
        # 0.0.0 rather than a plausible-looking number: if this ends up on a storefront it should
        # be obviously wrong at a glance, not quietly wrong.
        $Version = '0.0.0'
        Write-Host '==> Version 0.0.0 - no three-part git tag found. Pass -Version to override.' -ForegroundColor Yellow
    }
}

# --- 1. The app ---------------------------------------------------------------------------------
if (-not $SkipPublish) {
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    Invoke-Checked 'Publishing Morphogen (self-contained)' {
        dotnet publish (Join-Path $repoRoot 'src\Morphogen.App\Morphogen.App.csproj') `
            -c Release -r win-x64 --self-contained true `
            -p:DebugType=none -p:Version=$Version `
            -o $publishDir
    }
}

if (-not (Test-Path (Join-Path $publishDir $exeName))) {
    throw "No $exeName in $publishDir - publish first, or drop -SkipPublish."
}

# --- 2. The payload -----------------------------------------------------------------------------
# Rebuilt from scratch every time. butler diffs against what is already on the channel, so a
# stale file left behind here would be uploaded and would then persist in every later build.
Write-Host '==> Staging the payload' -ForegroundColor Cyan
if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
New-Item -ItemType Directory -Path $payloadDir | Out-Null

Copy-Item (Join-Path $publishDir '*') $payloadDir -Recurse -Force
Copy-Item (Join-Path $itchDir 'readme.txt') $payloadDir
Copy-Item (Join-Path $itchDir 'itch.toml') (Join-Path $payloadDir '.itch.toml')

# --- 3. The zip ---------------------------------------------------------------------------------
# Only for uploading by hand. butler gets the folder.
Write-Host '==> Zipping (for manual upload)' -ForegroundColor Cyan
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $payloadDir '*') -CompressionLevel Optimal -DestinationPath $zipPath

$payloadSize = (Get-ChildItem $payloadDir -Recurse -File | Measure-Object -Property Length -Sum).Sum

Write-Host ''
Write-Host 'Built:' -ForegroundColor Green
'{0,-40} {1,8:N0} MB' -f 'payload\', ($payloadSize / 1MB) | Write-Host
'{0,-40} {1,8:N0} MB' -f (Split-Path $zipPath -Leaf), ((Get-Item $zipPath).Length / 1MB) | Write-Host

if (-not $Push) {
    Write-Host ''
    Write-Host 'Not pushed. Add -Push to upload, or drop the zip on the itch.io web form.' -ForegroundColor DarkGray
    return
}

# --- 4. The push --------------------------------------------------------------------------------
# Deliberately last and deliberately opt-in: a push is live on the storefront the moment it
# finishes. There is no draft state for a build the way there is for a GitHub release.
if (-not $Target) {
    throw 'No itch.io target. Pass -Target user/game, or set ITCH_TARGET.'
}

if ($Target -notmatch '^[A-Za-z0-9_-]+/[A-Za-z0-9_-]+$') {
    throw "'$Target' is not a user/game target, e.g. whitelightrising/morphogen."
}

# butler authenticates from BUTLER_API_KEY, or from the credentials `butler login` saved. Check for
# one or the other up front rather than letting butler stop halfway to prompt for a login.
# Where it saves them has moved between butler versions, so look in both places.
$butlerCreds = @(
    (Join-Path $env:USERPROFILE '.config\itch\butler_creds'),
    (Join-Path $env:APPDATA 'itch\butler_creds')
) | Where-Object { Test-Path $_ }
if (-not $env:BUTLER_API_KEY -and -not $butlerCreds) {
    throw 'butler is not logged in. Run `butler login`, or set BUTLER_API_KEY from https://itch.io/user/settings/api-keys.'
}

if ($FetchButler) {
    $butlerDir = Join-Path $objDir 'butler'
    $butler = Join-Path $butlerDir 'butler.exe'

    if (-not (Test-Path $butler)) {
        Write-Host '==> Downloading butler' -ForegroundColor Cyan
        $archive = Join-Path $objDir 'butler.zip'
        Invoke-WebRequest -Uri $butlerUrl -OutFile $archive
        if (Test-Path $butlerDir) { Remove-Item $butlerDir -Recurse -Force }
        Expand-Archive -Path $archive -DestinationPath $butlerDir -Force
        Remove-Item $archive -Force
    }
}
elseif ($ButlerPath) {
    $butler = $ButlerPath
    if (-not (Test-Path $butler)) { throw "No butler at $butler" }
}
else {
    $found = Get-Command butler -ErrorAction SilentlyContinue
    if (-not $found) {
        throw 'butler is not on PATH. Install it, pass -ButlerPath, or use -FetchButler.'
    }
    $butler = $found.Source
}

Invoke-Checked "Pushing to $Target`:$Channel" {
    & $butler push $payloadDir "$Target`:$Channel" --userversion $Version
}

Write-Host ''
Invoke-Checked 'Channel status' {
    & $butler status "$Target`:$Channel"
}

Write-Host ''
Write-Host "Live at https://$($Target.Split('/')[0]).itch.io/$($Target.Split('/')[1])" -ForegroundColor Green
