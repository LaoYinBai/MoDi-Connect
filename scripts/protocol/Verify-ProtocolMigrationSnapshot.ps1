#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$inventoryPath = Join-Path $repositoryRoot 'docs\protocol\package-b\current-source-hashes-0.1.0.json'
$inventory = Get-Content -Raw -Encoding UTF8 -LiteralPath $inventoryPath | ConvertFrom-Json
$snapshotCommit = [string]$inventory.applicationSnapshotCommit

if ($snapshotCommit -cnotmatch '^[0-9a-f]{40}$') { throw "Invalid application snapshot commit: $snapshotCommit" }
if (@($inventory.files).Count -ne 42) { throw "Expected 42 source records, found $(@($inventory.files).Count)." }
if (Test-Path -LiteralPath (Join-Path $repositoryRoot 'MoDi-Connect-Protocol-zh\src')) { throw 'Chinese protocol source returned to the application tree.' }
if (Test-Path -LiteralPath (Join-Path $repositoryRoot 'MoDi-Connect-Protocol-en\src')) { throw 'English protocol source returned to the application tree.' }

git -C $repositoryRoot cat-file -e "$snapshotCommit^{commit}"
if ($LASTEXITCODE -ne 0) { throw "Snapshot commit is not available locally: $snapshotCommit" }

$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$archivePath = [IO.Path]::GetFullPath((Join-Path $temporaryRoot "modi-protocol-snapshot-$PID-$([Guid]::NewGuid().ToString('N')).zip"))
if (-not $archivePath.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Temporary archive escaped the system temporary directory.' }

try {
    git -C $repositoryRoot archive --format=zip "--output=$archivePath" $snapshotCommit -- MoDi-Connect-Protocol-zh/src MoDi-Connect-Protocol-en/src
    if ($LASTEXITCODE -ne 0) { throw 'Could not export the historical protocol source snapshot.' }

    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $stream = $entry.Open()
            try {
                $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
            } finally { $stream.Dispose() }
            $entries[$entry.FullName] = [pscustomobject]@{ Length = $entry.Length; Sha256 = $hash }
        }

        $expectedPaths = @($inventory.files.path | Sort-Object)
        $actualPaths = @($entries.Keys | Sort-Object)
        if (@(Compare-Object $expectedPaths $actualPaths).Count -ne 0) { throw 'Historical snapshot path list differs from the frozen inventory.' }
        foreach ($file in $inventory.files) {
            $actual = $entries[[string]$file.path]
            if ($actual.Length -ne [long]$file.length) { throw "Length mismatch: $($file.path)" }
            if ($actual.Sha256 -cne [string]$file.sha256) { throw "SHA-256 mismatch: $($file.path)" }
        }
    } finally { $archive.Dispose() }
} finally {
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
}

Write-Output "Protocol migration snapshot verified: 42 files at $snapshotCommit"
