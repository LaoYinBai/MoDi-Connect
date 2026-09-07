Set-StrictMode -Version Latest

function ConvertTo-MoDiSemanticVersion {
    param(
        [Parameter(Mandatory)][string] $Value,
        [switch] $AllowVPrefix
    )

    $pattern = if ($AllowVPrefix) {
        '^(?:v)?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(beta|rc)\.(0|[1-9]\d*))?$'
    } else {
        '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(beta|rc)\.(0|[1-9]\d*))?$'
    }
    $match = [regex]::Match($Value, $pattern, [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw "Invalid MoDi product version '$Value'. Expected major.minor.patch, optionally followed by -beta.N or -rc.N."
    }

    $prerelease = if ($match.Groups[4].Success) { $match.Groups[4].Value } else { $null }
    $number = if ($match.Groups[5].Success) { [long]$match.Groups[5].Value } else { $null }
    $normalized = "$([long]$match.Groups[1].Value).$([long]$match.Groups[2].Value).$([long]$match.Groups[3].Value)"
    if ($prerelease) { $normalized += "-$prerelease.$number" }

    [pscustomobject]@{
        Value = $normalized
        Major = [long]$match.Groups[1].Value
        Minor = [long]$match.Groups[2].Value
        Patch = [long]$match.Groups[3].Value
        Prerelease = $prerelease
        PrereleaseNumber = $number
        IsPrerelease = [bool]$prerelease
        ReleaseIdentity = if ($prerelease) { $prerelease } else { 'stable' }
        UpdateFeed = if ($prerelease) { 'beta' } else { 'stable' }
        NumericVersion = "$([long]$match.Groups[1].Value).$([long]$match.Groups[2].Value).$([long]$match.Groups[3].Value).0"
    }
}

function Compare-MoDiSemanticVersion {
    param(
        [Parameter(Mandatory)][string] $Left,
        [Parameter(Mandatory)][string] $Right,
        [switch] $AllowVPrefix
    )
    $a = ConvertTo-MoDiSemanticVersion $Left -AllowVPrefix:$AllowVPrefix
    $b = ConvertTo-MoDiSemanticVersion $Right -AllowVPrefix:$AllowVPrefix
    foreach ($part in @('Major', 'Minor', 'Patch')) {
        if ($a.$part -lt $b.$part) { return -1 }
        if ($a.$part -gt $b.$part) { return 1 }
    }
    if (-not $a.IsPrerelease -and -not $b.IsPrerelease) { return 0 }
    if (-not $a.IsPrerelease) { return 1 }
    if (-not $b.IsPrerelease) { return -1 }
    $rank = @{ beta = 0; rc = 1 }
    if ($rank[$a.Prerelease] -lt $rank[$b.Prerelease]) { return -1 }
    if ($rank[$a.Prerelease] -gt $rank[$b.Prerelease]) { return 1 }
    if ($a.PrereleaseNumber -lt $b.PrereleaseNumber) { return -1 }
    if ($a.PrereleaseNumber -gt $b.PrereleaseNumber) { return 1 }
    return 0
}

