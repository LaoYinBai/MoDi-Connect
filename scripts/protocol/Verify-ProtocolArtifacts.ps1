#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$ResolvedNuGetPackageRoot
)

$ErrorActionPreference = 'Stop'
$candidateRoot = Join-Path $RepositoryRoot 'third_party/modi-protocol'
$manifestPath = Join-Path $candidateRoot 'protocol-artifacts.v1.json'

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Protocol artifact manifest is missing: $manifestPath"
}

if (-not ('MoDi.StrictJson' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MoDi
{
    public static class StrictJson
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

$rawManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath
[MoDi.StrictJson]::RejectDuplicateProperties($rawManifest)
$manifest = $rawManifest | ConvertFrom-Json -Depth 10 -DateKind String

if ($manifest.schemaVersion -ne 1 -or $manifest.protocolVersion -cne '0.1.1') { throw 'Protocol manifest version is invalid.' }
if ($manifest.sourceLicenseStatus -cne 'PROPRIETARY_SOURCE_OWNER_ISSUED') { throw 'Protocol source license status is invalid.' }
if ($manifest.externalDistributionStatus -cne 'EXTERNAL_DISTRIBUTION_APPROVED_BY_OWNER') { throw 'Protocol candidate lacks the required owner-approved external-distribution status.' }
if ([string]$manifest.sourceCommit -cnotmatch '^[0-9a-f]{40}$') { throw 'Protocol source commit is invalid.' }
if ([string]$manifest.vectorSet.id -cne 'modi-protocol-v0.1' -or [string]$manifest.vectorSet.sha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Protocol vector identity is invalid.' }
if ($manifest.sourceTreeClean -ne $true) { throw 'Protocol candidate was not built from a clean source tree.' }

$allowedFiles = @(
    'BINARY-REDISTRIBUTION-GRANT.txt',
    'LICENSE-PROTOCOL-BINARY.txt',
    'MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt',
    'THIRD-PARTY-NOTICES.md',
    'maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.jar',
    'maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.module',
    'maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.pom',
    'nuget/MoDi.Protocol.0.1.1.nupkg',
    'protocol-artifacts.v1.json'
) | Sort-Object

$candidateRoot = [IO.Path]::GetFullPath($candidateRoot)
$actualFiles = @(Get-ChildItem -LiteralPath $candidateRoot -Recurse -File | ForEach-Object {
    if (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Protocol candidate contains a reparse point: $($_.FullName)" }
    [IO.Path]::GetRelativePath($candidateRoot, $_.FullName).Replace([IO.Path]::DirectorySeparatorChar, '/')
} | Sort-Object)
if ([string]::Join("`n", $allowedFiles) -cne [string]::Join("`n", $actualFiles)) {
    throw "Protocol candidate file allow-list mismatch. Actual: $($actualFiles -join ', ')"
}

$expectedArtifactPaths = @(
    'maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.jar',
    'maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.pom',
    'maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.module',
    'nuget/MoDi.Protocol.0.1.1.nupkg'
) | Sort-Object
$artifactPaths = @($manifest.artifacts | ForEach-Object { [string]$_.path })
if ($artifactPaths.Count -ne $expectedArtifactPaths.Count -or ($artifactPaths | Select-Object -Unique).Count -ne $artifactPaths.Count) { throw 'Protocol artifact paths are missing or duplicated.' }
if ([string]::Join("`n", @($artifactPaths | Sort-Object)) -cne [string]::Join("`n", $expectedArtifactPaths)) { throw 'Protocol artifact path allow-list mismatch.' }

foreach ($artifact in $manifest.artifacts) {
    $relative = [string]$artifact.path
    if ($relative -match '(^|/)\.\.(/|$)' -or $relative.StartsWith('/') -or $relative -match '^[A-Za-z]:') { throw "Unsafe protocol artifact path: $relative" }
    if ([string]$artifact.embeddedCommit -cne [string]$manifest.sourceCommit -or [string]$artifact.embeddedVectorSha256 -cne [string]$manifest.vectorSet.sha256) { throw "Protocol artifact embedded identity mismatch: $relative" }
    $path = [IO.Path]::GetFullPath((Join-Path $candidateRoot $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)))
    if (-not $path.StartsWith($candidateRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Protocol artifact escaped the candidate root: $relative" }
    $file = Get-Item -LiteralPath $path
    if ([long]$file.Length -ne [long]$artifact.size -or (Get-Sha256 -LiteralPath $path) -cne [string]$artifact.sha256) { throw "Protocol artifact hash/size mismatch: $relative" }
}

$legal = [ordered]@{
    licenseSha256 = 'LICENSE-PROTOCOL-BINARY.txt'
    redistributionGrantSha256 = 'BINARY-REDISTRIBUTION-GRANT.txt'
    linkingExceptionSha256 = 'MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt'
    thirdPartyNoticesSha256 = 'THIRD-PARTY-NOTICES.md'
}
foreach ($entry in $legal.GetEnumerator()) {
    $path = Join-Path $candidateRoot $entry.Value
    $legalText = Get-Content -Raw -Encoding UTF8 -LiteralPath $path
    if ($legalText -notmatch '(?m)^External distribution status: APPROVED by the owner \(Silvite\), review completed 2026-08-15\.$') { throw "Protocol legal external-distribution approval marker is missing: $($entry.Value)" }
    if ($legalText -match '(?m)^DRAFT\b') { throw "Protocol legal file still contains a draft marker: $($entry.Value)" }
    if ((Get-Sha256 -LiteralPath $path) -cne [string]$manifest.legal.($entry.Key)) { throw "Protocol legal hash mismatch: $($entry.Value)" }
}

$jarRecord = $manifest.artifacts | Where-Object path -Like '*.jar'
$jarPath = Join-Path $candidateRoot ([string]$jarRecord.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
$jar = [IO.Compression.ZipFile]::OpenRead($jarPath)
try {
    $entry = $jar.GetEntry('META-INF/MANIFEST.MF')
    if ($null -eq $entry) { throw 'Protocol JAR manifest is missing.' }
    $reader = [IO.StreamReader]::new($entry.Open(), [Text.Encoding]::UTF8)
    try { $jarManifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $jarManifest = $jarManifest -replace "\r?\n ", ''
    if (-not $jarManifest.Contains("Implementation-Version: 0.1.1") -or -not $jarManifest.Contains("MoDi-Protocol-Commit: $($manifest.sourceCommit)") -or -not $jarManifest.Contains("MoDi-Protocol-Vector-SHA256: $($manifest.vectorSet.sha256)")) { throw 'Protocol JAR manifest identity mismatch.' }
    if ($jar.Entries | Where-Object { $_.FullName -match '\.(kt|java)$' -or $_.FullName -match '(^|/)android(x)?/' -or $_.FullName -like 'com/modi/connect/*' }) { throw 'Protocol JAR contains forbidden source, Android, or application entries.' }
} finally { $jar.Dispose() }

$modulePath = Join-Path $candidateRoot 'maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.module'
$rawModule = Get-Content -Raw -Encoding UTF8 -LiteralPath $modulePath
[MoDi.StrictJson]::RejectDuplicateProperties($rawModule)
$module = $rawModule | ConvertFrom-Json -Depth 10
if ($module.component.group -cne 'com.silvite.modi' -or $module.component.module -cne 'modi-protocol-jvm' -or $module.component.version -cne '0.1.1') { throw 'Protocol Gradle module identity mismatch.' }
foreach ($variant in $module.variants) {
    $file = $variant.files | Where-Object name -EQ 'modi-protocol-jvm-0.1.1.jar'
    if ($null -eq $file -or $file.sha256 -cne $jarRecord.sha256) { throw "Protocol Gradle module JAR hash mismatch: $($variant.name)" }
}

$pomPath = Join-Path $candidateRoot 'maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.pom'
[xml]$pom = Get-Content -Raw -Encoding UTF8 -LiteralPath $pomPath
if ($pom.project.groupId -cne 'com.silvite.modi' -or $pom.project.artifactId -cne 'modi-protocol-jvm' -or $pom.project.version -cne '0.1.1') { throw 'Protocol Maven POM identity mismatch.' }

$nupkgRecord = $manifest.artifacts | Where-Object path -Like '*.nupkg'
$nupkgPath = Join-Path $candidateRoot ([string]$nupkgRecord.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
$nupkg = [IO.Compression.ZipFile]::OpenRead($nupkgPath)
try {
    $names = @($nupkg.Entries | ForEach-Object FullName)
    if ('lib/net10.0/MoDi.Protocol.dll' -cnotin $names) { throw 'Protocol NuGet DLL is missing.' }
    if ($names | Where-Object { $_ -match '\.(cs|pdb)$' -or $_ -match 'sourcelink' -or $_ -match 'MoDi\.(Desktop|Connect)' }) { throw 'Protocol NuGet contains forbidden source, symbols, SourceLink, or application assemblies.' }
    $dll = $nupkg.GetEntry('lib/net10.0/MoDi.Protocol.dll')
    $memory = [IO.MemoryStream]::new()
    try {
        $stream = $dll.Open()
        try { $stream.CopyTo($memory) } finally { $stream.Dispose() }
        $packagedDllBytes = $memory.ToArray()
        $packagedDllSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($packagedDllBytes)).ToLowerInvariant()
        $text = [Text.Encoding]::Latin1.GetString($packagedDllBytes).ToLowerInvariant()
        foreach ($marker in @('gitee.com','access_token','private_token','tokenfragment','modi.desktop','com/modi/connect')) { if ($text.Contains($marker)) { throw "Protocol DLL contains forbidden marker: $marker" } }
        if (-not $text.Contains(([string]$manifest.sourceCommit).ToLowerInvariant()) -or -not $text.Contains(([string]$manifest.vectorSet.sha256).ToLowerInvariant())) { throw 'Protocol DLL embedded release identity mismatch.' }
    } finally { $memory.Dispose() }
} finally { $nupkg.Dispose() }

if (-not [string]::IsNullOrWhiteSpace($ResolvedNuGetPackageRoot)) {
    $resolvedPackageRoot = [IO.Path]::GetFullPath($ResolvedNuGetPackageRoot)
    $resolvedDllPath = Join-Path $resolvedPackageRoot 'lib/net10.0/MoDi.Protocol.dll'
    if (-not (Test-Path -LiteralPath $resolvedDllPath -PathType Leaf)) {
        throw "Resolved MoDi.Protocol DLL is missing: $resolvedDllPath"
    }
    if ((Get-Sha256 -LiteralPath $resolvedDllPath) -cne $packagedDllSha256) {
        throw "Resolved MoDi.Protocol DLL differs from the vendored NuGet package: $resolvedDllPath"
    }
}

Write-Output "Protocol artifact verification passed: version $($manifest.protocolVersion), commit $($manifest.sourceCommit)"
