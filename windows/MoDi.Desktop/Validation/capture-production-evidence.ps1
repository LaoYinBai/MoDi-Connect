param(
    [string]$ExePath = "$PSScriptRoot\..\bin\Debug\net10.0-windows10.0.19041.0\MoDi.Desktop.exe"
)

$ErrorActionPreference = 'Stop'
$targetWidth = 1280
$targetHeight = 720

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MoDiProductionCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);
}
'@

[MoDiProductionCapture]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

function Find-ById($root, [string]$automationId) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $automationId)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Missing automation element: $automationId"
}

function Find-ByName($root, [string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $name)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Missing named automation element: $name"
}

function Get-ActionableAncestor($element) {
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    while ($element) {
        try {
            $null = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            return $element
        }
        catch {}
        try {
            $null = $element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            return $element
        }
        catch {}
        $element = $walker.GetParent($element)
    }
    throw 'No actionable automation ancestor found.'
}

function Invoke-Element($element, [int]$settleMilliseconds = 600) {
    $element = Get-ActionableAncestor $element
    try {
        $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $pattern.Invoke()
    }
    catch {
        $pattern = $element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        $pattern.Toggle()
    }
    Start-Sleep -Milliseconds $settleMilliseconds
}

function Save-Capture($process, [string]$fileName) {
    $rect = New-Object MoDiProductionCapture+RECT
    if (-not [MoDiProductionCapture]::GetWindowRect($process.MainWindowHandle, [ref]$rect)) {
        throw 'GetWindowRect failed.'
    }
    $sourceWidth = $rect.Right - $rect.Left
    $sourceHeight = $rect.Bottom - $rect.Top
    $nativeBitmap = New-Object System.Drawing.Bitmap $sourceWidth, $sourceHeight
    $nativeGraphics = [System.Drawing.Graphics]::FromImage($nativeBitmap)
    $deviceContext = $nativeGraphics.GetHdc()
    try {
        if (-not [MoDiProductionCapture]::PrintWindow($process.MainWindowHandle, $deviceContext, 2)) {
            throw 'PrintWindow failed.'
        }
    }
    finally {
        $nativeGraphics.ReleaseHdc($deviceContext)
        $nativeGraphics.Dispose()
    }

    $path = Join-Path $PSScriptRoot $fileName
    $targetBitmap = New-Object System.Drawing.Bitmap $targetWidth, $targetHeight
    $targetGraphics = [System.Drawing.Graphics]::FromImage($targetBitmap)
    try {
        $targetGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $targetGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $targetGraphics.DrawImage($nativeBitmap, 0, 0, $targetWidth, $targetHeight)
        $targetBitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $targetGraphics.Dispose()
        $targetBitmap.Dispose()
        $nativeBitmap.Dispose()
    }
    Write-Host "CAPTURED $fileName ${targetWidth}x${targetHeight} from ${sourceWidth}x${sourceHeight}"
}

$resolvedExe = (Resolve-Path $ExePath).Path
$captureProcess = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 200
        $captureProcess.Refresh()
    } while ($captureProcess.MainWindowHandle -eq 0 -and -not $captureProcess.HasExited -and [DateTime]::UtcNow -lt $deadline)
    if ($captureProcess.HasExited) { throw "Production app exited early: $($captureProcess.ExitCode)" }
    if ($captureProcess.MainWindowHandle -eq 0) { throw 'Production capture window did not appear.' }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($captureProcess.MainWindowHandle)
    Start-Sleep -Seconds 3
    Save-Capture $captureProcess 'package-a-production-main-dark.png'

    # Windows PowerShell 5.1 treats UTF-8 scripts without a BOM as the local ANSI
    # code page. Construct the one localized lookup value from code points so this
    # evidence script remains portable between powershell.exe and pwsh.exe.
    $switchToLightName = -join @(
        [char]0x5207,
        [char]0x6362,
        [char]0x6D45,
        [char]0x8272)
    $themeToggle = Get-ActionableAncestor (Find-ByName $root $switchToLightName)
    Invoke-Element $themeToggle 1000
    Save-Capture $captureProcess 'package-a-production-main-light.png'
    Invoke-Element $themeToggle 1000

    Invoke-Element (Find-ById $root 'SettingsNavigationButton')
    Save-Capture $captureProcess 'package-a-production-settings.png'

    Invoke-Element (Find-ById $root 'AboutNavigationButton')
    Save-Capture $captureProcess 'package-a-production-about.png'
}
finally {
    if ($captureProcess -and -not $captureProcess.HasExited) {
        $captureProcess.CloseMainWindow() | Out-Null
        if (-not $captureProcess.WaitForExit(10000)) {
            Stop-Process -Id $captureProcess.Id
            $captureProcess.WaitForExit()
        }
    }
}
