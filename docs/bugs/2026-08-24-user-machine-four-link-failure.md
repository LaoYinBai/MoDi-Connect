# 用户机器四链路失效与 Windows 退出

## 现场证据

- Windows 日志包含 5 次 `GlobalExceptionBoundary`、`AppDomain.UnhandledException`、`IsTerminating=True`。
- 异常均为 Avalonia 跨线程访问：Wi-Fi Direct 的进度定时器上报状态后，最终在 `Button.CanExecuteChanged` 读取 UI 对象时终止进程。
- Android 诊断显示 Wi-Fi Direct Group Owner 已建立，但 60 秒内没有收到 Windows HELLO。
- Windows 日志持续显示发现 0 个 P2P 设备。
- USB 检测累计 696 次无法启动 `adb`；用户安装目录中不存在该程序。

## 根因

`PairedDevicesViewModel` 可能在异步生产组合创建期间于线程池构造，因而捕获到空的 `SynchronizationContext`。之后 Wi-Fi Direct 的 `System.Threading.Timer` 每五秒从后台线程发布配对快照；原实现把“上下文为空”误当作可以直接执行，最终从后台线程触发绑定按钮的命令状态刷新并终止进程。

四条链路看似同时失效，是因为 Wi-Fi Direct 常驻任务即使不是当前选中链路也会启动；它触发的进程级退出会同时销毁 LAN、蓝牙和 USB。USB 另有独立的发行缺陷：应用仅从系统 PATH 查找 ADB，而 Setup 未携带 ADB。

## 排除项

VB-CABLE 缺失只会使虚拟麦克风路线返回失败。`CableRenderer.Prepare` 和 `AudioRouter.StartCable` 都把设备缺失转换为错误状态，没有抛出本次日志中的致命异常，因此不是四链路共同失效的根因。

## 修复边界

1. 所有配对 UI 快照通过 Avalonia UI Dispatcher 应用。
2. Windows 发行包携带应用私有 ADB，不依赖开发机 PATH。
3. Setup 为应用程序配置有限范围的 UDP 入站规则，并在卸载时清理。
4. VB-CABLE 引导使用系统自带 Windows PowerShell 5.1，保持用户主动选择，并在执行前校验下载程序签名。
5. Wi-Fi Direct 硬件与驱动能力继续由诊断模块报告；Setup 不安装厂商无线或蓝牙驱动。
