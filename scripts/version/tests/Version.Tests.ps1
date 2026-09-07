$root = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$script = Join-Path $root 'scripts/version.ps1'

Describe 'Community unified version identity' {
    It 'reads the repository single source' {
        $source = Get-Content (Join-Path $root 'version.json') -Raw | ConvertFrom-Json
        $identity = & $script show -RepositoryRoot $root -Json | ConvertFrom-Json
        $identity.version | Should Be $source.version
        $identity.build | Should Be $source.build
        $identity.channel | Should Be 'stable'
        $identity.releaseIdentity | Should Be 'stable'
        $identity.commit | Should Match '^(unknown|[0-9a-f]{7})$'
    }

    It 'increments a fixture build without changing product version' {
        $fixture = Join-Path ([IO.Path]::GetTempPath()) ('modi-community-version-' + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $fixture | Out-Null
        try {
            Set-Content (Join-Path $fixture 'version.json') '{"schemaVersion":2,"version":"1.0.0","build":4}'
            & $script bump-build -RepositoryRoot $fixture | Out-Null
            $source = Get-Content (Join-Path $fixture 'version.json') -Raw | ConvertFrom-Json
            $source.version | Should Be '1.0.0'
            $source.build | Should Be 5
        } finally { Remove-Item -LiteralPath $fixture -Recurse -Force }
    }

    It 'derives Android and Windows from version.json' {
        (Get-Content (Join-Path $root 'android/app/build.gradle.kts') -Raw) | Should Match 'version\.json'
        (Get-Content (Join-Path $root 'windows/MoDi.Desktop/MoDi.Desktop.csproj') -Raw) | Should Match 'Generate-VersionAssemblyInfo\.ps1'
    }
}

Describe 'Shared semantic version vectors' {
    . (Join-Path $root 'scripts/version/SemVer.Common.ps1')
    $vectors = Get-Content (Join-Path $root 'scripts/version/semver-test-vectors.json') -Raw | ConvertFrom-Json
    foreach ($pair in $vectors.orderedPairs) {
        It "orders $($pair[0]) before $($pair[1])" {
            Compare-MoDiSemanticVersion $pair[0] $pair[1] -AllowVPrefix | Should BeLessThan 0
        }
    }
    foreach ($pair in $vectors.equivalentPairs) {
        It "normalizes $($pair[0]) and $($pair[1]) equally" {
            Compare-MoDiSemanticVersion $pair[0] $pair[1] -AllowVPrefix | Should Be 0
        }
    }
}
