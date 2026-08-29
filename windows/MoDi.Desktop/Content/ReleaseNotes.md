# 版本说明

## V1.0（协议 0.1.1）

核对日期：2026 年 8 月 29 日。

- Windows 真实应用已接入验收后的现代水墨 UI，与隔离 TestUI 共用模块化 `MoDi.Presentation`。
- Android 生产入口已使用模块化 Compose `MoDiApp`。
- Windows 与 Android 统一四个品牌色和五种字体语义层级。
- Windows 顶部导航按完整窗口居中，主界面功能栏在设置/关于页自动收起。
- LAN、Wi-Fi Direct、蓝牙、USB 四种链路由用户手动选择，不做自动降级。
- 二维码配对、最近设备、音量、输出路线、网络、外观、启动、日志和内置 Markdown 已连接真实 Windows 服务。
- 个性化重置只影响主题、背景、动效和功能栏宽度。
- 协议二进制边界已落地：协议实现以 `MoDi.Protocol 0.1.1` 二进制（JAR / DLL-NuGet）随包分发，应用构建图不再编译协议源码。
- 应用自有代码按 GPL-3.0-or-later 及《MoDi Protocol Binary Linking Exception 1.0》发布；协议二进制按专有许可与再分发授权随包提供。
- 社区版保持无内置下载更新逻辑；更新入口打开官网或 GitHub Release。
- 外部 DLL/EXE 插件和脚本插件尚未开放；当前只注册内置音频模块。
