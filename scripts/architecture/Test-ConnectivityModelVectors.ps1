[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$vectors = Join-Path $PSScriptRoot 'connectivity-model-vectors.json'

$document = Get-Content -LiteralPath $vectors -Raw | ConvertFrom-Json
if ($document.schemaVersion -ne 1) {
    throw "Unsupported connectivity vector schema: $($document.schemaVersion)"
}

& dotnet test (Join-Path $root 'windows\MoDi.App.Contracts.Tests\MoDi.App.Contracts.Tests.csproj') `
    -c Release --nologo --filter 'FullyQualifiedName~ConnectivityModelTests'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$gradle = Join-Path $root 'android\gradlew.bat'
$androidRoot = Join-Path $root 'android'
$taskListing = @(& $gradle -p $androidRoot ':app:tasks' '--all' '--console=plain' '--no-daemon')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$releaseTasks = if ($taskListing -match '(?m)^testReleaseUnitTest\s+-') {
    @('testReleaseUnitTest')
} elseif (
    $taskListing -match '(?m)^testCommunityReleaseUnitTest\s+-' -and
    $taskListing -match '(?m)^testOfficialReleaseUnitTest\s+-'
) {
    @('testCommunityReleaseUnitTest', 'testOfficialReleaseUnitTest')
} else {
    throw 'Unable to resolve Android release unit-test tasks for the current edition topology.'
}

& $gradle -p $androidRoot $releaseTasks `
    --tests 'com.modi.connect.core.connectivity.ConnectivityModelTest' --no-daemon
exit $LASTEXITCODE
