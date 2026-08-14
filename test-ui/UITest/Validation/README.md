# Windows TestUI 最终验证资产

本目录只保留当前最终截图和可复现脚本。完整中文结论见 [Windows 测试 UI 验收记录](../../../docs/验收/Windows测试UI验收记录.md)。

## 最终截图

| 文件 | 内容 |
|---|---|
| `10-shell-main-dark.png` | 深色主界面 |
| `11-shell-main-light.png` | 浅色主界面 |
| `12-shell-qr.png` | 二维码浮层 |
| `13-shell-device-list.png` | 配对设备浮层 |
| `14-shell-settings.png` | 设置页 |
| `15-shell-about.png` | 关于页 |
| `16-shell-sidebar-compact.png` | 56px 紧凑功能栏 |
| `17-shell-button-reference-comparison.png` | 圆钮参考比较 |
| `18-shell-sidebar-before-after.png` | 功能栏修正前后比较 |

早期 01–09、反馈图、参考图、稳定性 CSV/TXT 和完整旧验收记录已移动到 `archive/ui-validation-history`。

## 构建与测试

```powershell
dotnet test windows/MoDi.Presentation.Tests/MoDi.Presentation.Tests.csproj --no-restore
dotnet test test-ui/UITest.Tests/UITest.Tests.csproj --no-restore
dotnet build test-ui/UITest/UITest.csproj --no-restore
```

## 截图

```powershell
powershell -ExecutionPolicy Bypass -File test-ui/UITest/Validation/capture-stage-states.ps1
```

脚本通过具名 UI Automation 控件生成最终窗口截图。运行前关闭其他 `UITest.exe`，结束后确认脚本启动的进程已经退出。

## 稳定性

```powershell
powershell -ExecutionPolicy Bypass -File test-ui/UITest/Validation/run-stage-soak.ps1 -DurationMinutes 30
```

稳定性脚本只驱动 TestUI 内存状态；历史结果已归档。TestUI 不连接真实网络、音频或系统服务。
