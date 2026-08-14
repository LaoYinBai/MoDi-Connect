@echo off
setlocal
chcp 65001 >nul
title MoDi — 检查 VB-Audio Virtual Cable 驱动

set "OFFICIAL_URL=https://vb-audio.com/Cable/"

echo ============================================
echo   MoDi — 虚拟音频驱动检查
echo ============================================
echo.

call :is_installed
if not errorlevel 1 (
    echo [已就绪] 已检测到 VB-Audio Virtual Cable。
    echo 无需重复安装，可直接启用虚拟麦克风相关功能。
    echo.
    pause
    exit /b 0
)

echo [未安装] 当前系统未检测到 VB-Audio Virtual Cable。
echo MoDi 不在源码仓库或安装包内分发第三方驱动。
echo 请从官方页面下载并按官方说明完成安装：
echo %OFFICIAL_URL%
echo.
choice /C YN /N /M "是否现在打开 VB-Audio 官方下载页面？[Y/N] "
if errorlevel 2 (
    echo 已取消。驱动安装完成前，虚拟麦克风相关功能不可用。
    exit /b 2
)

start "" "%OFFICIAL_URL%"
echo.
echo 官方页面已打开。完成安装后，请重新运行本脚本或重启 MoDi 检查状态。
pause
exit /b 2

:is_installed
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB-Audio_Virtual_Cable" >nul 2>&1 && exit /b 0
reg query "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB-Audio_Virtual_Cable" >nul 2>&1 && exit /b 0
powershell.exe -NoProfile -NonInteractive -Command "$devices = Get-CimInstance Win32_SoundDevice -ErrorAction SilentlyContinue; if ($devices.Name -match 'VB-Audio|CABLE (Input|Output)') { exit 0 } else { exit 1 }" >nul 2>&1
exit /b %errorlevel%
