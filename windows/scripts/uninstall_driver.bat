@echo off
chcp 65001 >nul
title LAN Audio Bridge — 卸载 VB-Audio Virtual Cable 驱动

echo ============================================
echo   LAN Audio Bridge — 驱动卸载
echo   正在卸载 VB-Audio Virtual Cable
echo ============================================
echo.

set "UNINSTALL_EXE=%PROGRAMFILES%\VB-Audio\Virtual Cable\Uninstall.exe"

if not exist "%UNINSTALL_EXE%" (
    if exist "%PROGRAMFILES(x86)%\VB-Audio\Virtual Cable\Uninstall.exe" (
        set "UNINSTALL_EXE=%PROGRAMFILES(x86)%\VB-Audio\Virtual Cable\Uninstall.exe"
    ) else (
        echo [错误] 找不到卸载程序
        echo 请在"控制面板 → 程序和功能"中手动卸载 "VB-Audio Virtual Cable"
        pause
        exit /b 1
    )
)

echo [1/2] 正在卸载 VB-Audio Virtual Cable...
start /wait "" "%UNINSTALL_EXE%" /S

echo [2/2] 清理残留...
timeout /t 2 /nobreak >nul

echo [完成] VB-Audio Virtual Cable 已卸载
echo LAN Audio Bridge 的虚拟麦克风模式将不可用
echo.
pause
