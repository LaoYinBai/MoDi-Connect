#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [long]$MaximumTrackedFileSizeBytes = 10MB
)

$ErrorActionPreference = 'Stop'

function Fail([string]$code, [string]$message) {
    Write-Error "[$code] $message"
    exit 1
}

$RepositoryRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepositoryRoot).Path)
& git -C $RepositoryRoot rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) { Fail 'E_NOT_GIT_REPOSITORY' $RepositoryRoot }

$reviewedBinaryPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
@(
    'android/app/libs/concentus-1.0.1.jar',
    'android/gradle/wrapper/gradle-wrapper.jar',
    'third_party/modi-protocol/maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.jar',
    'third_party/modi-protocol/nuget/MoDi.Protocol.0.1.1.nupkg'
) | ForEach-Object { [void]$reviewedBinaryPaths.Add($_) }

$forbiddenExtensions = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
@('.aab', '.apk', '.appx', '.exe', '.jar', '.msi', '.msix', '.nupkg', '.pdb', '.rar', '.zip', '.7z') |
    ForEach-Object { [void]$forbiddenExtensions.Add($_) }

$violations = [Collections.Generic.List[string]]::new()
$trackedFiles = @(& git -C $RepositoryRoot ls-files)
if ($LASTEXITCODE -ne 0) { Fail 'E_TRACKED_FILE_LIST' 'Unable to enumerate tracked files.' }

$totalBytes = 0L
foreach ($relativePath in $trackedFiles) {
    $normalized = $relativePath.Replace('\', '/')
    $absolutePath = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) { continue }

    $file = Get-Item -LiteralPath $absolutePath
    $totalBytes += $file.Length
    if ($file.Length -gt $MaximumTrackedFileSizeBytes) {
        $violations.Add("[E_TRACKED_FILE_TOO_LARGE] $normalized ($($file.Length) bytes)")
    }

    if ($normalized -match '(^|/)(artifacts|bin|obj|build|\.gradle|\.idea|\.vs)(/|$)') {
        $violations.Add("[E_TRACKED_BUILD_OUTPUT] $normalized")
    }

    $extension = [IO.Path]::GetExtension($normalized)
    if ($forbiddenExtensions.Contains($extension) -and -not $reviewedBinaryPaths.Contains($normalized)) {
        $violations.Add("[E_FORBIDDEN_TRACKED_ARTIFACT] $normalized")
    }
}

if ($violations.Count -gt 0) {
    $violations | Sort-Object -Unique | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "Repository binary hygiene passed: tracked=$($trackedFiles.Count), bytes=$totalBytes, maxFileBytes=$MaximumTrackedFileSizeBytes, reviewedBinaryExceptions=$($reviewedBinaryPaths.Count)."
