#requires -Version 7.0

[CmdletBinding()]
param(
    [string]$FontLibrary = $(if ($env:MODI_FONT_LIBRARY) { $env:MODI_FONT_LIBRARY } else { 'D:\MoDi-Local-Font-Library' }),
    [string]$PythonExecutable = 'python'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$libraryRoot = [System.IO.Path]::GetFullPath($FontLibrary)
$toolRoot = Join-Path $libraryRoot '.tools\fonttools-4.63.0'
$toolPython = Join-Path $toolRoot 'Scripts\python.exe'
$requirements = Join-Path $PSScriptRoot 'requirements.lock.txt'

if (-not (Test-Path -LiteralPath $libraryRoot -PathType Container)) {
    throw "完整字体库不存在：$libraryRoot"
}

if (-not (Test-Path -LiteralPath $toolPython -PathType Leaf)) {
    & $PythonExecutable -m venv $toolRoot
    if ($LASTEXITCODE -ne 0) {
        throw "创建隔离 Python 环境失败，退出码：$LASTEXITCODE"
    }
}

$installedVersion = & $toolPython -c "import fontTools; print(fontTools.__version__)" 2>$null
if ($LASTEXITCODE -ne 0 -or $installedVersion.Trim() -ne '4.63.0') {
    & $toolPython -m pip install `
        --index-url https://pypi.org/simple `
        --only-binary=:all: `
        --require-hashes `
        --no-deps `
        --requirement $requirements
    if ($LASTEXITCODE -ne 0) {
        throw "安装锁定的 fontTools 失败，退出码：$LASTEXITCODE"
    }
}

Push-Location $repoRoot
try {
    & $toolPython -m scripts.fonts.build_fonts --repo-root $repoRoot --font-library $libraryRoot
    if ($LASTEXITCODE -ne 0) {
        throw "字体生成失败，退出码：$LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
