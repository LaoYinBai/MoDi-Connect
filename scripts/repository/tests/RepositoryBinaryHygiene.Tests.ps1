$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$gate = Join-Path $repositoryRoot 'scripts/repository/Test-RepositoryBinaryHygiene.ps1'

function New-TestRepository([string]$name) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ("modi-hygiene-$name-" + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $root | Out-Null
    & git -C $root init --quiet
    & git -C $root config user.name 'MoDi Test'
    & git -C $root config user.email 'test@invalid.local'
    return $root
}

Describe 'Repository binary hygiene gate' {
    It 'accepts the current GitHub source tree and its reviewed binary allow-list' {
        $output = & pwsh -NoProfile -File $gate -RepositoryRoot $repositoryRoot 2>&1 | Out-String

        $LASTEXITCODE | Should Be 0
        $output | Should Match 'Repository binary hygiene passed'
    }

    It 'rejects an accidentally tracked release package' {
        $temp = New-TestRepository 'release'
        try {
            [IO.File]::WriteAllBytes((Join-Path $temp 'MoDi-Android-test.apk'), [byte[]](1, 2, 3))
            & git -C $temp add .
            & git -C $temp commit --quiet -m fixture

            $output = & pwsh -NoProfile -File $gate -RepositoryRoot $temp 2>&1 | Out-String

            $LASTEXITCODE | Should Be 1
            $output | Should Match 'E_FORBIDDEN_TRACKED_ARTIFACT'
        } finally {
            Remove-Item -LiteralPath $temp -Recurse -Force
        }
    }

    It 'rejects an oversized tracked file even when its extension is not forbidden' {
        $temp = New-TestRepository 'oversized'
        try {
            $stream = [IO.File]::OpenWrite((Join-Path $temp 'oversized.dat'))
            try { $stream.SetLength(10MB + 1) } finally { $stream.Dispose() }
            & git -C $temp add .
            & git -C $temp commit --quiet -m fixture

            $output = & pwsh -NoProfile -File $gate -RepositoryRoot $temp 2>&1 | Out-String

            $LASTEXITCODE | Should Be 1
            $output | Should Match 'E_TRACKED_FILE_TOO_LARGE'
        } finally {
            Remove-Item -LiteralPath $temp -Recurse -Force
        }
    }

    It 'rejects generated build output that was force-added' {
        $temp = New-TestRepository 'generated'
        try {
            New-Item -ItemType Directory -Path (Join-Path $temp 'windows/App/bin/Release') -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $temp 'windows/App/bin/Release/app.dat') -Value fixture
            & git -C $temp add -f .
            & git -C $temp commit --quiet -m fixture

            $output = & pwsh -NoProfile -File $gate -RepositoryRoot $temp 2>&1 | Out-String

            $LASTEXITCODE | Should Be 1
            $output | Should Match 'E_TRACKED_BUILD_OUTPUT'
        } finally {
            Remove-Item -LiteralPath $temp -Recurse -Force
        }
    }
}
