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

& (Join-Path $root 'android\gradlew.bat') -p (Join-Path $root 'android') `
    testReleaseUnitTest --tests 'com.modi.connect.core.connectivity.ConnectivityModelTest' --no-daemon
exit $LASTEXITCODE
