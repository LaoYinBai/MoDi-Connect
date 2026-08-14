#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$mapPath = Join-Path $repositoryRoot 'license-map.v1.json'

if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.git'))) {
    & git -C $repositoryRoot rev-parse --git-dir *> $null
    if ($LASTEXITCODE -ne 0) { throw "Repository root is not a Git worktree: $repositoryRoot" }
}
if (-not (Test-Path -LiteralPath $mapPath -PathType Leaf)) {
    throw "Repository license map is missing: $mapPath"
}

if (-not ('MoDi.RepositoryLicenseStrictJson' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MoDi
{
    public static class RepositoryLicenseStrictJson
    {
        public static void RejectDuplicateProperties(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            Visit(document.RootElement, "$");
        }

        private static void Visit(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                        throw new FormatException($"Duplicate JSON property at {path}: {property.Name}");
                    Visit(property.Value, path + "." + property.Name);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                    Visit(item, path + "[" + index++ + "]");
            }
        }
    }
}
'@
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $LiteralPath).Hash.ToLowerInvariant()
}

function Test-PathPrefixMatch {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Prefix
    )
    if ($Prefix.EndsWith('/', [StringComparison]::Ordinal)) {
        return $Path.StartsWith($Prefix, [StringComparison]::Ordinal)
    }
    return $Path.Equals($Prefix, [StringComparison]::Ordinal)
}

function Get-LicenseRecord {
    param([Parameter(Mandatory = $true)][string]$Path)
    $matches = @($map.overrides | Where-Object { Test-PathPrefixMatch -Path $Path -Prefix ([string]$_.pathPrefix) })
    if ($matches.Count -eq 0) { return $map.default }
    $longest = ($matches | ForEach-Object { ([string]$_.pathPrefix).Length } | Measure-Object -Maximum).Maximum
    $winners = @($matches | Where-Object { ([string]$_.pathPrefix).Length -eq $longest })
    if ($winners.Count -ne 1) { return $null }
    return $winners[0]
}

$rawMap = Get-Content -Raw -Encoding UTF8 -LiteralPath $mapPath
[MoDi.RepositoryLicenseStrictJson]::RejectDuplicateProperties($rawMap)
$map = $rawMap | ConvertFrom-Json -Depth 20 -DateKind String

if ($map.schemaVersion -ne 1) { throw 'Repository license map schemaVersion must be 1.' }
if ($map.default.spdxExpression -cne 'GPL-3.0-or-later') { throw 'The application default license must be GPL-3.0-or-later.' }
if ($map.default.copyright -cne 'Copyright (C) 2026 Silvite') { throw 'The application default copyright holder is invalid.' }

$records = @($map.default) + @($map.overrides)
foreach ($record in $records) {
    foreach ($field in @('spdxExpression', 'copyright', 'provenance')) {
        if ([string]::IsNullOrWhiteSpace([string]$record.$field)) { throw "License record is missing $field." }
    }
    $licenseFiles = @($record.licenseFiles)
    if ($licenseFiles.Count -eq 0) { throw "License record has no licenseFiles: $($record.spdxExpression)" }
    foreach ($relative in $licenseFiles) {
        $relative = ([string]$relative).Replace('\', '/')
        if ($relative -match '(^|/)\.\.(/|$)' -or $relative.StartsWith('/') -or $relative -match '^[A-Za-z]:') {
            throw "Unsafe license file path: $relative"
        }
        $licensePath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)))
        if (-not $licensePath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "License file escaped the repository root: $relative"
        }
        if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) { throw "Declared license file is missing: $relative" }
    }
}

$prefixes = @($map.overrides | ForEach-Object { [string]$_.pathPrefix })
if ($prefixes.Count -ne ($prefixes | Select-Object -Unique).Count) { throw 'Repository license map contains duplicate pathPrefix values.' }
foreach ($prefix in $prefixes) {
    if ([string]::IsNullOrWhiteSpace($prefix) -or $prefix.Contains('\') -or $prefix.StartsWith('/') -or $prefix -match '(^|/)\.\.(/|$)') {
        throw "Invalid license pathPrefix: $prefix"
    }
}

$requiredOverrides = [ordered]@{
    'third_party/modi-protocol/' = 'LicenseRef-MoDi-Proprietary-1.0 AND LicenseRef-MoDi-Binary-Redistribution-Grant-1.0 AND LicenseRef-MoDi-GPL-Linking-Exception-1.0'
    'sysvad-dev/' = 'MS-PL'
    'android/gradle/wrapper/' = 'Apache-2.0'
    'android/gradlew' = 'Apache-2.0'
    'android/gradlew.bat' = 'Apache-2.0'
    'android/app/libs/concentus-1.0.1.jar' = 'BSD-3-Clause'
}
foreach ($required in $requiredOverrides.GetEnumerator()) {
    $record = @($map.overrides | Where-Object { [string]$_.pathPrefix -ceq [string]$required.Key })
    if ($record.Count -ne 1 -or [string]$record[0].spdxExpression -cne [string]$required.Value) {
        throw "Required license override is missing or invalid: $($required.Key)"
    }
}

$git = [Diagnostics.Process]::new()
$git.StartInfo = [Diagnostics.ProcessStartInfo]::new()
$git.StartInfo.FileName = 'git'
$git.StartInfo.UseShellExecute = $false
$git.StartInfo.RedirectStandardOutput = $true
$git.StartInfo.RedirectStandardError = $true
foreach ($argument in @('-C', $repositoryRoot, 'ls-files', '--cached', '--others', '--exclude-standard', '-z')) {
    [void]$git.StartInfo.ArgumentList.Add($argument)
}
if (-not $git.Start()) { throw 'Failed to start git ls-files.' }
$gitOutput = $git.StandardOutput.ReadToEnd()
$gitError = $git.StandardError.ReadToEnd()
$git.WaitForExit()
if ($git.ExitCode -ne 0) { throw "git ls-files failed: $gitError" }
$files = @($gitOutput.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique)
if ($files.Count -eq 0) { throw 'Repository file inventory is empty.' }

$unmapped = [Collections.Generic.List[string]]::new()
$ambiguous = [Collections.Generic.List[string]]::new()
$counts = @{}
foreach ($path in $files) {
    $matches = @($map.overrides | Where-Object { Test-PathPrefixMatch -Path $path -Prefix ([string]$_.pathPrefix) })
    if ($matches.Count -eq 0) {
        $record = $map.default
    }
    else {
        $longest = ($matches | ForEach-Object { ([string]$_.pathPrefix).Length } | Measure-Object -Maximum).Maximum
        $winners = @($matches | Where-Object { ([string]$_.pathPrefix).Length -eq $longest })
        if ($winners.Count -ne 1) {
            $ambiguous.Add($path)
            continue
        }
        $record = $winners[0]
    }
    if ($null -eq $record -or [string]::IsNullOrWhiteSpace([string]$record.spdxExpression)) {
        $unmapped.Add($path)
        continue
    }
    $expression = [string]$record.spdxExpression
    $counts[$expression] = 1 + [int]($counts[$expression] ?? 0)
}
if ($unmapped.Count -ne 0 -or $ambiguous.Count -ne 0) {
    throw "Repository license coverage failed: unmapped=$($unmapped.Count) [$($unmapped -join ', ')]; ambiguous=$($ambiguous.Count) [$($ambiguous -join ', ')]"
}

$concentusPath = Join-Path $repositoryRoot 'android/app/libs/concentus-1.0.1.jar'
if ((Get-Sha256 -LiteralPath $concentusPath) -cne '288f4f1e646943d9a616188e8fd82d6e8f4f475d7f024409c5fdb7fa8fc12618') {
    throw 'Pinned Concentus 1.0.1 JAR hash mismatch.'
}

$fontLockPath = Join-Path $repositoryRoot 'assets/fonts/font-artifacts.lock.json'
$fontLock = Get-Content -Raw -Encoding UTF8 -LiteralPath $fontLockPath | ConvertFrom-Json -Depth 10
foreach ($font in $fontLock.artifacts) {
    $fontPath = "assets/fonts/android-res/font/$($font.fileName)"
    $licensePath = "assets/fonts/android-res/raw/$($font.licenseFileName)"
    $expectedExpression = if ([string]$font.id -ceq 'alimama-dongfang-dakai') { 'LicenseRef-Alimama-DongFangDaKai-Font' } else { 'OFL-1.1' }
    $fontRecord = Get-LicenseRecord -Path $fontPath
    $fontLicenseRecord = Get-LicenseRecord -Path $licensePath
    if ($null -eq $fontRecord -or [string]$fontRecord.spdxExpression -cne $expectedExpression -or $licensePath -cnotin @($fontRecord.licenseFiles)) {
        throw "Font artifact license mapping mismatch: $fontPath"
    }
    if ($null -eq $fontLicenseRecord -or [string]$fontLicenseRecord.spdxExpression -cne $expectedExpression) {
        throw "Font license text mapping mismatch: $licensePath"
    }
    $absoluteLicensePath = Join-Path $repositoryRoot $licensePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    if ((Get-Sha256 -LiteralPath $absoluteLicensePath) -cne ([string]$font.licenseSha256).ToLowerInvariant()) {
        throw "Font license text hash mismatch: $licensePath"
    }
}

$mislicensedSysvad = @()
foreach ($path in $files | Where-Object { $_.StartsWith('sysvad-dev/', [StringComparison]::Ordinal) -and $_ -match '\.(c|cc|cpp|h|hpp|inf|inx|md|txt)$' }) {
    $absolute = Join-Path $repositoryRoot $path.Replace('/', [IO.Path]::DirectorySeparatorChar)
    if ((Get-Content -Raw -ErrorAction SilentlyContinue -LiteralPath $absolute) -match 'SPDX-License-Identifier:\s*GPL') {
        $mislicensedSysvad += $path
    }
}
if ($mislicensedSysvad.Count -ne 0) { throw "SysVAD files must not declare GPL: $($mislicensedSysvad -join ', ')" }

$summary = $counts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }
Write-Output "Repository license verification passed: files=$($files.Count), unmapped=0, ambiguous=0"
Write-Output ($summary -join '; ')
