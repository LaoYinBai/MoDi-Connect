#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$WindowsOutput,
    [string]$AndroidApk
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
if ([string]::IsNullOrWhiteSpace($WindowsOutput)) {
    $WindowsOutput = Join-Path $repositoryRoot 'windows/MoDi.Desktop/bin/Release/net10.0-windows10.0.19041.0'
}
if ([string]::IsNullOrWhiteSpace($AndroidApk)) {
    $AndroidApk = Join-Path $repositoryRoot 'android/app/build/outputs/apk/debug/app-debug.apk'
}
$windowsOutput = [IO.Path]::GetFullPath($WindowsOutput)
$androidApk = [IO.Path]::GetFullPath($AndroidApk)
$candidateRoot = Join-Path $repositoryRoot 'third_party/modi-protocol'
$manifestPath = Join-Path $candidateRoot 'protocol-artifacts.v1.json'

foreach ($requiredPath in @($windowsOutput, $androidApk, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required package audit input is missing: $requiredPath" }
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $LiteralPath).Hash.ToLowerInvariant()
}

function Get-BytesSha256 {
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function Get-ZipEntryBytes {
    param(
        [Parameter(Mandatory = $true)][IO.Compression.ZipArchive]$Archive,
        [Parameter(Mandatory = $true)][string]$EntryName
    )
    $entries = @($Archive.Entries | Where-Object FullName -CEQ $EntryName)
    if ($entries.Count -ne 1) { throw "Expected exactly one package entry '$EntryName', found $($entries.Count)." }
    $memory = [IO.MemoryStream]::new()
    try {
        $stream = $entries[0].Open()
        try { $stream.CopyTo($memory) } finally { $stream.Dispose() }
        return $memory.ToArray()
    }
    finally { $memory.Dispose() }
}

function Assert-FileMatchesSource {
    param(
        [Parameter(Mandatory = $true)][string]$PackagedPath,
        [Parameter(Mandatory = $true)][string]$SourcePath
    )
    if (-not (Test-Path -LiteralPath $PackagedPath -PathType Leaf)) { throw "Packaged license file is missing: $PackagedPath" }
    if ((Get-FileSha256 -LiteralPath $PackagedPath) -cne (Get-FileSha256 -LiteralPath $SourcePath)) {
        throw "Packaged license differs from its repository source: $PackagedPath"
    }
}

function Assert-ZipEntryMatchesSource {
    param(
        [Parameter(Mandatory = $true)][IO.Compression.ZipArchive]$Archive,
        [Parameter(Mandatory = $true)][string]$EntryName,
        [Parameter(Mandatory = $true)][string]$SourcePath
    )
    $bytes = Get-ZipEntryBytes -Archive $Archive -EntryName $EntryName
    if ((Get-BytesSha256 -Bytes $bytes) -cne (Get-FileSha256 -LiteralPath $SourcePath)) {
        throw "Package entry differs from its repository source: $EntryName"
    }
}

function Get-DexClassDefinitionCount {
    param(
        [Parameter(Mandatory = $true)][byte[]]$Bytes,
        [Parameter(Mandatory = $true)][string]$Descriptor
    )

    if ($Bytes.Length -lt 112 -or [Text.Encoding]::ASCII.GetString($Bytes, 0, 4) -cne "dex`n") {
        throw 'Invalid DEX file encountered during package audit.'
    }
    $stringIdsSize = [BitConverter]::ToUInt32($Bytes, 56)
    $stringIdsOffset = [BitConverter]::ToUInt32($Bytes, 60)
    $typeIdsSize = [BitConverter]::ToUInt32($Bytes, 64)
    $typeIdsOffset = [BitConverter]::ToUInt32($Bytes, 68)
    $classDefinitionsSize = [BitConverter]::ToUInt32($Bytes, 96)
    $classDefinitionsOffset = [BitConverter]::ToUInt32($Bytes, 100)
    $count = 0

    for ($index = 0; $index -lt $classDefinitionsSize; $index++) {
        $classItemOffset = [long]$classDefinitionsOffset + ([long]$index * 32)
        if ($classItemOffset + 4 -gt $Bytes.Length) { throw 'DEX class_defs table escaped the file.' }
        $classIndex = [BitConverter]::ToUInt32($Bytes, [int]$classItemOffset)
        if ($classIndex -ge $typeIdsSize) { throw 'DEX class_idx escaped the type_ids table.' }
        $typeItemOffset = [long]$typeIdsOffset + ([long]$classIndex * 4)
        if ($typeItemOffset + 4 -gt $Bytes.Length) { throw 'DEX type_ids table escaped the file.' }
        $descriptorIndex = [BitConverter]::ToUInt32($Bytes, [int]$typeItemOffset)
        if ($descriptorIndex -ge $stringIdsSize) { throw 'DEX descriptor_idx escaped the string_ids table.' }
        $stringItemOffset = [long]$stringIdsOffset + ([long]$descriptorIndex * 4)
        if ($stringItemOffset + 4 -gt $Bytes.Length) { throw 'DEX string_ids table escaped the file.' }
        $stringDataOffset = [BitConverter]::ToUInt32($Bytes, [int]$stringItemOffset)
        if ($stringDataOffset -ge $Bytes.Length) { throw 'DEX string_data offset escaped the file.' }

        $position = [int]$stringDataOffset
        do {
            if ($position -ge $Bytes.Length) { throw 'Truncated DEX string length.' }
            $lengthByte = $Bytes[$position++]
        } while (($lengthByte -band 0x80) -ne 0)
        $stringStart = $position
        while ($position -lt $Bytes.Length -and $Bytes[$position] -ne 0) { $position++ }
        if ($position -ge $Bytes.Length) { throw 'Unterminated DEX string data.' }
        $value = [Text.Encoding]::UTF8.GetString($Bytes, $stringStart, $position - $stringStart)
        if ($value -ceq $Descriptor) { $count++ }
    }
    return $count
}

$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json -Depth 10 -DateKind String
if ($manifest.protocolVersion -cne '0.1.1' -or $manifest.externalDistributionStatus -cne 'EXTERNAL_DISTRIBUTION_APPROVED_BY_OWNER') {
    throw 'Package audit requires the owner-approved 0.1.1 internal protocol candidate.'
}

$windowsLicenseSources = [ordered]@{
    'Licenses/GPL-3.0-or-later.txt' = 'LICENSE'
    'Licenses/MoDi.Protocol/LICENSE-PROTOCOL-BINARY.txt' = 'third_party/modi-protocol/LICENSE-PROTOCOL-BINARY.txt'
    'Licenses/MoDi.Protocol/BINARY-REDISTRIBUTION-GRANT.txt' = 'third_party/modi-protocol/BINARY-REDISTRIBUTION-GRANT.txt'
    'Licenses/MoDi.Protocol/MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt' = 'third_party/modi-protocol/MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt'
    'Licenses/MoDi.Protocol/THIRD-PARTY-NOTICES.md' = 'third_party/modi-protocol/THIRD-PARTY-NOTICES.md'
    'Licenses/ThirdParty/Apache-2.0.txt' = 'LICENSES/Apache-2.0.txt'
    'Licenses/ThirdParty/BSD-3-Clause-Concentus.txt' = 'LICENSES/BSD-3-Clause-Concentus.txt'
    'FontLicenses/alimama_dongfang_dakai_license.txt' = 'assets/fonts/android-res/raw/alimama_dongfang_dakai_license.txt'
    'FontLicenses/genyo_mincho_ofl.txt' = 'assets/fonts/android-res/raw/genyo_mincho_ofl.txt'
    'FontLicenses/lxgw_wenkai_ofl.txt' = 'assets/fonts/android-res/raw/lxgw_wenkai_ofl.txt'
    'FontLicenses/source_han_serif_ofl.txt' = 'assets/fonts/android-res/raw/source_han_serif_ofl.txt'
    'FontLicenses/zhuque_fangsong_ofl.txt' = 'assets/fonts/android-res/raw/zhuque_fangsong_ofl.txt'
}
foreach ($entry in $windowsLicenseSources.GetEnumerator()) {
    $packaged = Join-Path $windowsOutput ([string]$entry.Key).Replace('/', [IO.Path]::DirectorySeparatorChar)
    $source = Join-Path $repositoryRoot ([string]$entry.Value).Replace('/', [IO.Path]::DirectorySeparatorChar)
    Assert-FileMatchesSource -PackagedPath $packaged -SourcePath $source
}

$protocolDlls = @(Get-ChildItem -LiteralPath $windowsOutput -Recurse -File -Filter 'MoDi.Protocol.dll')
if ($protocolDlls.Count -ne 1) { throw "Windows output must contain exactly one MoDi.Protocol.dll, found $($protocolDlls.Count)." }
if (Get-ChildItem -LiteralPath $windowsOutput -Recurse -File | Where-Object { $_.Name -match '^MoDi\.Protocol\.(pdb|xml)$' -or $_.Extension -in @('.kt', '.java', '.cs') }) {
    throw 'Windows output contains protocol symbols, documentation, or source files.'
}
if (Get-ChildItem -LiteralPath $windowsOutput -Recurse -File | Where-Object { $_.Extension -in @('.jar', '.nupkg') }) {
    throw 'Windows output must not carry protocol package containers.'
}

$nupkgRecord = $manifest.artifacts | Where-Object path -Like '*.nupkg'
$nupkgPath = Join-Path $candidateRoot ([string]$nupkgRecord.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
$nupkg = [IO.Compression.ZipFile]::OpenRead($nupkgPath)
try {
    $packagedDllBytes = Get-ZipEntryBytes -Archive $nupkg -EntryName 'lib/net10.0/MoDi.Protocol.dll'
    if ((Get-FileSha256 -LiteralPath $protocolDlls[0].FullName) -cne (Get-BytesSha256 -Bytes $packagedDllBytes)) {
        throw 'Windows output protocol DLL differs from the vendored NuGet candidate.'
    }
}
finally { $nupkg.Dispose() }

$forbiddenMarkers = @(
    'gitee.com/DSGYDS',
    'MoDi-Connect-Protocol.git',
    'private_token',
    'access_token',
    'tokenfragment'
)
foreach ($file in Get-ChildItem -LiteralPath $windowsOutput -Recurse -File | Where-Object { $_.Extension -in @('.dll', '.exe', '.json') }) {
    $text = [Text.Encoding]::Latin1.GetString([IO.File]::ReadAllBytes($file.FullName))
    foreach ($marker in $forbiddenMarkers) {
        if ($text.Contains($marker, [StringComparison]::OrdinalIgnoreCase)) { throw "Windows output contains forbidden marker '$marker': $($file.FullName)" }
    }
}

$apk = [IO.Compression.ZipFile]::OpenRead($androidApk)
try {
    $apkLicenseSources = [ordered]@{
        'META-INF/PROPRIETARY-PROTOCOL-LICENSE-1.0.txt' = 'third_party/modi-protocol/LICENSE-PROTOCOL-BINARY.txt'
        'META-INF/BINARY-REDISTRIBUTION-GRANT-1.0.txt' = 'third_party/modi-protocol/BINARY-REDISTRIBUTION-GRANT.txt'
        'META-INF/MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt' = 'third_party/modi-protocol/MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt'
        'META-INF/THIRD-PARTY-NOTICES.md' = 'third_party/modi-protocol/THIRD-PARTY-NOTICES.md'
        'META-INF/CONCENTUS-1.0.1-BSD-3-CLAUSE.txt' = 'android/app/libs/concentus-1.0.1.LICENSE.txt'
        'res/raw/alimama_dongfang_dakai_license.txt' = 'assets/fonts/android-res/raw/alimama_dongfang_dakai_license.txt'
        'res/raw/genyo_mincho_ofl.txt' = 'assets/fonts/android-res/raw/genyo_mincho_ofl.txt'
        'res/raw/lxgw_wenkai_ofl.txt' = 'assets/fonts/android-res/raw/lxgw_wenkai_ofl.txt'
        'res/raw/source_han_serif_ofl.txt' = 'assets/fonts/android-res/raw/source_han_serif_ofl.txt'
        'res/raw/zhuque_fangsong_ofl.txt' = 'assets/fonts/android-res/raw/zhuque_fangsong_ofl.txt'
    }
    foreach ($entry in $apkLicenseSources.GetEnumerator()) {
        $source = Join-Path $repositoryRoot ([string]$entry.Value).Replace('/', [IO.Path]::DirectorySeparatorChar)
        Assert-ZipEntryMatchesSource -Archive $apk -EntryName ([string]$entry.Key) -SourcePath $source
    }

    $forbiddenEntries = @($apk.Entries | Where-Object {
        $_.FullName -match '\.(kt|java|cs|pdb)$' -or
        $_.FullName -match '(^|/)third_party/modi-protocol/' -or
        $_.FullName -match '\.(jar|nupkg)$'
    })
    if ($forbiddenEntries.Count -ne 0) { throw "APK contains forbidden source, symbol, or protocol-container entries: $($forbiddenEntries.FullName -join ', ')" }

    $descriptorCount = 0
    foreach ($dexEntry in $apk.Entries | Where-Object { $_.FullName -match '^classes\d*\.dex$' }) {
        $dexBytes = Get-ZipEntryBytes -Archive $apk -EntryName $dexEntry.FullName
        $dexText = [Text.Encoding]::Latin1.GetString($dexBytes)
        $descriptorCount += Get-DexClassDefinitionCount -Bytes $dexBytes -Descriptor 'Lcom/modi/protocol/PacketHeaderCodec;'
        foreach ($marker in $forbiddenMarkers) {
            if ($dexText.Contains($marker, [StringComparison]::OrdinalIgnoreCase)) { throw "APK DEX contains forbidden marker: $marker" }
        }
    }
    if ($descriptorCount -ne 1) { throw "APK must contain exactly one PacketHeaderCodec class descriptor, found $descriptorCount." }
}
finally { $apk.Dispose() }

Write-Output "Application package license verification passed: Windows licenses=$($windowsLicenseSources.Count), protocol DLLs=1, APK licenses=$($apkLicenseSources.Count), protocol class descriptors=1"
