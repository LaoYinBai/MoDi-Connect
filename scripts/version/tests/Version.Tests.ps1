$root = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$script = Join-Path $root 'scripts/version.ps1'

Describe 'Community unified version identity' {
    It 'reads the repository single source' {
        $identity = & $script show -RepositoryRoot $root -Json | ConvertFrom-Json
        $identity.version | Should Be '1.0.0'
        $identity.build | Should Be 4
        $identity.channel | Should Be 'stable'
        $identity.commit | Should Match '^(unknown|[0-9a-f]{7})$'
    }

    It 'increments a fixture build without changing product version' {
        $fixture = Join-Path ([IO.Path]::GetTempPath()) ('modi-community-version-' + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $fixture | Out-Null
        try {
            Set-Content (Join-Path $fixture 'version.json') '{"schemaVersion":1,"version":"1.0.0","build":4,"channel":"stable"}'
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
