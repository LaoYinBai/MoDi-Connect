param(
    [string]$ExePath = "$PSScriptRoot\..\bin\Debug\net10.0\UITest.exe",
    [switch]$MainDarkOnly,
    [switch]$DeviceListOnly,
    [switch]$SkipExpiryProbe
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

public static class UITestValidationCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
'@

[UITestValidationCapture]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

function Get-WindowRectangle($process) {
    $rect = New-Object UITestValidationCapture+RECT
    if (-not [UITestValidationCapture]::GetWindowRect($process.MainWindowHandle, [ref]$rect)) {
        throw 'GetWindowRect failed.'
    }

    return $rect
}

function Save-WindowCapture($process, [string]$fileName) {
    $rect = Get-WindowRectangle $process
    $sourceWidth = $rect.Right - $rect.Left
    $sourceHeight = $rect.Bottom - $rect.Top
    if ($sourceWidth -le 0 -or $sourceHeight -le 0) {
        throw "Invalid window rectangle: ${sourceWidth}x${sourceHeight}."
    }

    $nativeBitmap = New-Object System.Drawing.Bitmap $sourceWidth, $sourceHeight
    $nativeGraphics = [System.Drawing.Graphics]::FromImage($nativeBitmap)
    $deviceContext = $nativeGraphics.GetHdc()
    try {
        if (-not [UITestValidationCapture]::PrintWindow($process.MainWindowHandle, $deviceContext, 2)) {
            throw 'PrintWindow failed.'
        }
    }
    finally {
        $nativeGraphics.ReleaseHdc($deviceContext)
        $nativeGraphics.Dispose()
    }

    $path = Join-Path $PSScriptRoot $fileName
    if ($sourceWidth -eq $targetWidth -and $sourceHeight -eq $targetHeight) {
        $nativeBitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $nativeBitmap.Dispose()
    }
    else {
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
    }

    $saved = [System.Drawing.Image]::FromFile($path)
    try {
        if ($saved.Width -ne $targetWidth -or $saved.Height -ne $targetHeight) {
            throw "Capture has unexpected dimensions: $($saved.Width)x$($saved.Height)."
        }
    }
    finally {
        $saved.Dispose()
    }

    Write-Host "CAPTURED $fileName ${targetWidth}x${targetHeight} from ${sourceWidth}x${sourceHeight}"
}

function Find-ElementById($root, [string]$automationId, [int]$timeoutSeconds = 10) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $automationId)
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    do {
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Missing automation element: $automationId"
}

function Find-ElementByName($root, [string]$name, [int]$timeoutSeconds = 10) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $name)
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    do {
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Missing named automation element: $name"
}

function Find-ButtonByName($root, [string]$name, [int]$timeoutSeconds = 10) {
    $condition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $name)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Button)))
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    do {
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Missing named automation button: $name"
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

    throw 'No actionable ancestor found.'
}

function Invoke-Element($element, [int]$settleMilliseconds = 300) {
    $actionable = Get-ActionableAncestor $element
    try {
        $pattern = $actionable.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $pattern.Invoke()
    }
    catch {
        $pattern = $actionable.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        $pattern.Toggle()
    }
    Start-Sleep -Milliseconds $settleMilliseconds
}

function Invoke-ById($root, [string]$automationId, [int]$settleMilliseconds = 300) {
    Invoke-Element (Find-ElementById $root $automationId) $settleMilliseconds
}

function Invoke-ByName($root, [string]$name, [int]$settleMilliseconds = 300) {
    Invoke-Element (Find-ElementByName $root $name) $settleMilliseconds
}

function Move-PointerToElement($element, [int]$settleMilliseconds = 450) {
    $bounds = $element.Current.BoundingRectangle
    if ([double]::IsInfinity($bounds.X) -or $bounds.Width -le 0 -or $bounds.Height -le 0) {
        throw "Element '$($element.Current.Name)' has no usable bounds."
    }

    $x = [int][Math]::Round($bounds.X + ($bounds.Width / 2))
    $y = [int][Math]::Round($bounds.Y + ($bounds.Height / 2))
    if (-not [UITestValidationCapture]::SetCursorPos($x, $y)) {
        throw "SetCursorPos failed for $x,$y."
    }
    Start-Sleep -Milliseconds $settleMilliseconds
}

function Click-AtElement($element, [int]$settleMilliseconds = 450) {
    Move-PointerToElement $element 100
    [UITestValidationCapture]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [UITestValidationCapture]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $settleMilliseconds
}

function Move-PointerToStageCenter($root) {
    $bounds = $root.Current.BoundingRectangle
    [UITestValidationCapture]::SetCursorPos(
        [int][Math]::Round($bounds.X + ($bounds.Width * 0.58)),
        [int][Math]::Round($bounds.Y + ($bounds.Height * 0.53))) | Out-Null
    Start-Sleep -Milliseconds 250
}

function Set-SidebarCompact($root) {
    $bounds = $root.Current.BoundingRectangle
    $scale = $bounds.Width / 1280.0
    # The 16px grip straddles the rail edge, so its center is the x=200 boundary.
    $startX = [int][Math]::Round($bounds.X + (200 * $scale))
    $endX = [int][Math]::Round($bounds.X + (58 * $scale))
    $y = [int][Math]::Round($bounds.Y + (330 * $scale))

    [UITestValidationCapture]::SetCursorPos($startX, $y) | Out-Null
    [UITestValidationCapture]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    foreach ($step in 1..8) {
        $x = [int][Math]::Round($startX + (($endX - $startX) * $step / 8.0))
        [UITestValidationCapture]::SetCursorPos($x, $y) | Out-Null
        Start-Sleep -Milliseconds 30
    }
    [UITestValidationCapture]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 450

    $audioButton = Find-ElementById $root 'AudioPluginButton'
    $expectedRailWidth = 56 * $scale
    $actualRailWidth =
        ($audioButton.Current.BoundingRectangle.Right - $bounds.X) + (8 * $scale)
    if ([Math]::Abs($actualRailWidth - $expectedRailWidth) -gt 2) {
        throw "Sidebar width is $actualRailWidth px; expected $expectedRailWidth px."
    }

    $audioLabelCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        'AudioPluginLabel')
    $audioLabel = $root.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $audioLabelCondition)
    if ($audioLabel -and -not $audioLabel.Current.IsOffscreen) {
        throw 'Sidebar did not snap to its compact state.'
    }
}

function Open-Simulator($root) {
    $label = Find-ElementByName $root '演示控制'
    Invoke-Element $label 350
    $null = Find-ElementByName $root '连接'
}

function Close-Simulator($root) {
    Invoke-Element (Find-ElementByName $root '演示控制') 350
}

$resolvedExe = (Resolve-Path $ExePath).Path
$captureProcess = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 200
        $captureProcess.Refresh()
    } while ($captureProcess.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
    if ($captureProcess.MainWindowHandle -eq 0) { throw 'UITest capture window did not appear.' }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($captureProcess.MainWindowHandle)
    $windowBounds = $root.Current.BoundingRectangle
    Write-Host "WINDOW logical target 1280x720; native UIA bounds $($windowBounds.Width)x$($windowBounds.Height)"

    if ($DeviceListOnly) {
        Move-PointerToStageCenter $root
        Move-PointerToElement (Find-ElementById $root 'PairedDevicesButton')
        $null = Find-ElementByName $root '工作室 Mac'
        Save-WindowCapture $captureProcess '13-shell-device-list.png'
        return
    }

    # Main dark: exercise the drawer, settle the accepted connected stage, then
    # close the drawer so the product shell itself is the visual subject.
    Open-Simulator $root
    Invoke-ByName $root '连接' 100
    Start-Sleep -Seconds 6
    Close-Simulator $root
    Move-PointerToStageCenter $root
    Save-WindowCapture $captureProcess '10-shell-main-dark.png'

    if ($MainDarkOnly) { return }

    # Top chrome theme command and light main state.
    Invoke-Element (Find-ElementByName $root '切换浅色') 1000
    Save-WindowCapture $captureProcess '11-shell-main-light.png'
    Invoke-Element (Find-ElementByName $root '切换深色') 1000

    # QR hover is pointer-driven. Refresh by real click and verify the revision.
    $qrButton = Find-ElementById $root 'QrButton'
    Move-PointerToElement $qrButton
    $null = Find-ElementByName $root '扫描二维码进行本地配对'
    Click-AtElement $qrButton
    $null = Find-ElementByName $root '版本 1'
    Save-WindowCapture $captureProcess '12-shell-qr.png'

    if (-not $SkipExpiryProbe) {
        Write-Host 'Waiting for the deterministic two-minute QR UI expiry...'
        Start-Sleep -Seconds 121
        $null = Find-ElementByName $root '二维码已过期'
        Click-AtElement $qrButton
        $null = Find-ElementByName $root '版本 2'
        Write-Host 'QR expiry and click-to-refresh states verified.'
    }

    Move-PointerToStageCenter $root

    # Paired-device overlay, then row selection -> handshaking.
    $deviceButton = Find-ElementById $root 'PairedDevicesButton'
    Move-PointerToElement $deviceButton
    $null = Find-ElementByName $root '工作室 Mac'
    Save-WindowCapture $captureProcess '13-shell-device-list.png'
    Click-AtElement (Get-ActionableAncestor (Find-ElementByName $root '工作室 Mac')) 500
    $handshakeStatus = Find-ElementById $root 'ConnectionStatusText'
    if ($handshakeStatus.Current.Name -ne '握手中') {
        throw "Device selection did not request handshaking; status is '$($handshakeStatus.Current.Name)'."
    }

    # Settings: capture the top of the page, then exercise local-only controls
    # and actions before reset restores the default theme/state.
    Invoke-ById $root 'SettingsNavigationButton' 600
    Save-WindowCapture $captureProcess '14-shell-settings.png'
    $settingsChecks = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::CheckBox)))
    foreach ($check in $settingsChecks) {
        if (-not $check.Current.IsOffscreen) { Invoke-Element $check 150 }
    }
    foreach ($action in @('选择图片', '导入插件', '开发者入口', '检查更新', '导出日志', '重置设置')) {
        Write-Host "INVOKING settings action: $action"
        Invoke-Element (Find-ButtonByName $root $action) 120
    }

    # About navigation and its deterministic local actions.
    Invoke-ById $root 'AboutNavigationButton' 600
    Save-WindowCapture $captureProcess '15-shell-about.png'
    foreach ($action in @('联系', '日志', '复制信息')) {
        Invoke-ByName $root $action 120
    }

    # Back to main, demonstrate the drawer once more, then drag/release the rail
    # through native mouse input and verify the compact label is off-screen.
    Invoke-ById $root 'MainNavigationButton' 600
    Open-Simulator $root
    Close-Simulator $root
    Set-SidebarCompact $root
    Move-PointerToStageCenter $root
    Save-WindowCapture $captureProcess '16-shell-sidebar-compact.png'
}
finally {
    if ($captureProcess -and -not $captureProcess.HasExited) {
        $captureProcess.CloseMainWindow() | Out-Null
        if (-not $captureProcess.WaitForExit(10000)) {
            Stop-Process -Id $captureProcess.Id -Force
            $captureProcess.WaitForExit()
        }
    }
}
