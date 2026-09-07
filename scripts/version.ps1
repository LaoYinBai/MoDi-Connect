#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('show', 'verify', 'bump-build', 'set-version')]
    [string] $Command = 'show',

    [Parameter(Position = 1)]
    [string] $Value,

    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch] $Json,
    [switch] $RequireGit
)

$ErrorActionPreference = 'Stop'
$versionPath = Join-Path $RepositoryRoot 'version.json'
. (Join-Path $PSScriptRoot 'version\SemVer.Common.ps1')

function Read-VersionSource {
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        throw "Version source not found: $versionPath"
    }
    try { $source = Get-Content -LiteralPath $versionPath -Raw | ConvertFrom-Json }
    catch { throw "Invalid version.json: $($_.Exception.Message)" }

    if ([int]$source.schemaVersion -ne 2) { throw 'version.json schemaVersion must be 2.' }
    $semanticVersion = ConvertTo-MoDiSemanticVersion ([string]$source.version)
    if ($semanticVersion.Value -cne [string]$source.version) { throw 'version.json version must be normalized without a v prefix.' }
    if ([long]$source.build -lt 1 -or [long]$source.build -gt [int]::MaxValue) {
        throw 'version.json build must be between 1 and 2147483647.'
    }
    return $source
}

function Resolve-GitCommit([switch] $Required) {
    try {
        $inside = (& git -C $RepositoryRoot rev-parse --is-inside-work-tree 2>$null | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $inside -ne 'true') { throw 'not a Git work tree' }
        $commit = (& git -C $RepositoryRoot rev-parse --short=7 HEAD 2>$null | Out-String).Trim().ToLowerInvariant()
        if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{7}$') { throw 'commit unavailable' }
        if ($Required) {
            $changes = @(& git -C $RepositoryRoot status --porcelain --untracked-files=all)
            if ($LASTEXITCODE -ne 0) { throw 'unable to inspect Git status' }
            if ($changes.Count -gt 0) { throw '[E_DIRTY_RELEASE_SOURCE] Formal builds require a clean Git source tree.' }
            $parentSourceText = (& git -C $RepositoryRoot show 'HEAD^:version.json' 2>$null | Out-String).Trim()
            if ($LASTEXITCODE -eq 0 -and $parentSourceText) {
                try { $parentBuild = [long](($parentSourceText | ConvertFrom-Json).build) }
                catch { throw '[E_PARENT_VERSION_SOURCE] Parent version.json is invalid.' }
                $currentBuild = [long](Read-VersionSource).build
                if ($currentBuild -le $parentBuild) {
                    throw "[E_BUILD_NOT_INCREMENTED] Build $currentBuild reuses or precedes parent build $parentBuild. Run scripts/version.ps1 bump-build and commit it last."
                }
            }
        }
        return $commit
    } catch {
        if ($Required) { throw "[E_RELEASE_GIT_REQUIRED] $($_.Exception.Message)" }
        return 'unknown'
    }
}

function Write-VersionSource($Source) {
    $normalized = [ordered]@{
        schemaVersion = 2
        version = [string]$Source.version
        build = [long]$Source.build
    }
    $temporary = "$versionPath.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        [IO.File]::WriteAllText($temporary, (($normalized | ConvertTo-Json) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary, $versionPath, $true)
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

$source = Read-VersionSource
switch ($Command) {
    'bump-build' {
        if ([long]$source.build -ge [int]::MaxValue) { throw 'Build cannot be incremented beyond Android versionCode limit.' }
        $source.build = [long]$source.build + 1
        Write-VersionSource $source
        $source = Read-VersionSource
    }
    'set-version' {
        $source.version = (ConvertTo-MoDiSemanticVersion $Value -AllowVPrefix).Value
        Write-VersionSource $source
        $source = Read-VersionSource
    }
}

$semanticVersion = ConvertTo-MoDiSemanticVersion ([string]$source.version)
$identity = [ordered]@{
    version = [string]$source.version
    build = [long]$source.build
    releaseIdentity = [string]$semanticVersion.ReleaseIdentity
    channel = [string]$semanticVersion.UpdateFeed
    numericVersion = [string]$semanticVersion.NumericVersion
    commit = Resolve-GitCommit -Required:$RequireGit
    informationalVersion = $null
}
$identity.informationalVersion = "$($identity.version)+build.$($identity.build).sha.$($identity.commit)"

if ($Json) {
    $identity | ConvertTo-Json -Compress
} else {
    "Version : $($identity.version)"
    "Build   : $($identity.build)"
    "Identity: $($identity.releaseIdentity)"
    "Feed    : $($identity.channel)"
    "Commit  : $($identity.commit)"
}
