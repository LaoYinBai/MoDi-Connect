param(
    [string]$ExePath = "$PSScriptRoot\..\bin\Debug\net10.0\UITest.exe",
    [int]$DurationMinutes = 30
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$resultPath = Join-Path $PSScriptRoot 'soak-result.csv'
$summaryPath = Join-Path $PSScriptRoot 'soak-summary.txt'
Remove-Item -LiteralPath $summaryPath -Force -ErrorAction SilentlyContinue
"minute,working_set_mb,tcp_connections,udp_endpoints" | Set-Content -LiteralPath $resultPath -Encoding utf8

$appProcess = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path $ExePath) -PassThru -WindowStyle Minimized
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 200
        $appProcess.Refresh()
    } while ($appProcess.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)

    if ($appProcess.MainWindowHandle -eq 0) { throw 'UITest window did not appear.' }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($appProcess.MainWindowHandle)
    $disconnectName = -join @([char]0x65AD, [char]0x5F00)
    $handshakeName = -join @([char]0x63E1, [char]0x624B)
    $connectName = -join @([char]0x8FDE, [char]0x63A5)
    $reconnectName = -join @([char]0x91CD, [char]0x8FDE)
    $autoPulseName = -join @([char]0x81EA, [char]0x52A8, [char]0x8109, [char]0x51B2)
    $buttonNames = @($disconnectName, $handshakeName, $connectName, $reconnectName)
    $buttons = @{}
    foreach ($name in $buttonNames) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $name)
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if (-not $element) { throw "Missing simulator button: $name" }
        $buttons[$name] = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    }

    $autoCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $autoPulseName)
    $autoToggle = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $autoCondition)
    if ($autoToggle) {
        $autoToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    }

    function Add-MemorySample([int]$minute) {
        $appProcess.Refresh()
        $tcpCount = @(Get-NetTCPConnection -OwningProcess $appProcess.Id -ErrorAction SilentlyContinue).Count
        $udpCount = @(Get-NetUDPEndpoint -OwningProcess $appProcess.Id -ErrorAction SilentlyContinue).Count
        $workingSet = [Math]::Round($appProcess.WorkingSet64 / 1MB, 2)
        "$minute,$workingSet,$tcpCount,$udpCount" | Add-Content -LiteralPath $resultPath -Encoding utf8
    }

    Add-MemorySample 0
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $sequence = @($connectName, $reconnectName, $connectName, $disconnectName, $handshakeName, $connectName)
    $sequenceIndex = 0
    $sampled15 = $false
    $targetSeconds = $DurationMinutes * 60

    while ($watch.Elapsed.TotalSeconds -lt $targetSeconds) {
        $buttons[$sequence[$sequenceIndex % $sequence.Count]].Invoke()
        $sequenceIndex++
        Start-Sleep -Seconds 4

        if (-not $sampled15 -and $watch.Elapsed.TotalMinutes -ge 15) {
            Add-MemorySample 15
            $sampled15 = $true
        }
    }

    Add-MemorySample $DurationMinutes
    $watch.Stop()
    $appProcess.CloseMainWindow() | Out-Null
    if (-not $appProcess.WaitForExit(10000)) {
        Stop-Process -Id $appProcess.Id -Force
        throw 'UITest did not exit within 10 seconds after CloseMainWindow.'
    }

    @(
        'SOAK_OK'
        "duration_minutes=$DurationMinutes"
        "state_requests=$sequenceIndex"
        'window_closed=True'
        "result_file=$resultPath"
    ) | Set-Content -LiteralPath $summaryPath -Encoding utf8
}
catch {
    "SOAK_FAILED`n$($_.Exception.Message)" | Set-Content -LiteralPath $summaryPath -Encoding utf8
    if (-not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force }
    throw
}
