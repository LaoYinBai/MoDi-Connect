# 墨堤互联

墨堤互联是一个面向多品牌设备的跨端音频互联项目。当前已实现 Android 采集端、Windows 接收端、四种手动选择链路，以及两端真实产品 UI。

> 当前工程状态核对日期：2026 年 8 月 13 日。

## 当前可用范围

| 领域 | 状态 | 说明 |
|---|---|---|
| Android 真实 UI | 已接入 | 生产入口为 `MainActivity → MoDiApp()`，不是测试 UI |
| Windows 真实 UI | 已接入并验收 | `MoDi.Desktop` 与 TestUI 共用模块化 `MoDi.Presentation` |
| 四条音频管线 | 已实现 | 扬声器、监听、虚拟麦克风、系统音频转虚拟麦克风 |
| 四种链路 | 已实现 | Wi-Fi LAN、Wi-Fi Direct、蓝牙、USB；由用户手动选择，不自动降级 |
| 自动化测试 | .NET 五组与 Android 单元测试通过 | 四链路双端回归、跨语言金样、二进制来源与制品许可门禁通过 |
| 协议二进制边界 | `0.1.1` 技术迁移完成，发布阻塞 | 应用只引用 JAR/NuGet 二进制；合格法律复核未完成前，候选包不得分发 |
| 正式发布 | 尚未完成 | 安装器、签名、合格法律复核、干净环境和发布回归仍是发布前门槛 |

## 功能概览

### 四条音频管线

| 模式 | Android 采集 | Windows 输出 | 典型场景 |
|---|---|---|---|
| 扬声器 | 系统音频 | 扬声器 | 手机媒体在电脑播放 |
| 监听 | 系统音频 + 麦克风 | 扬声器 | 直播或录屏解说 |
| 虚拟麦克风 | 麦克风 | 虚拟音频设备 | 手机作为无线麦克风 |
| 系统音频转虚拟麦克风 | 系统音频 | 虚拟音频设备 | 将手机媒体送入会议或游戏语音 |

### 链路策略

四种链路保持独立，由用户根据环境选择：

- Wi-Fi LAN：同一局域网内发现和连接。
- Wi-Fi Direct：没有共同路由器时点对点连接。
- 蓝牙：RFCOMM 备用链路。
- USB：有线环境下的独立链路。

项目明确不实现跨链路自动降级，以免系统在用户不知情时改变链路。UI 同时展示环境状态和当前激活链路。

## 快速构建

### Windows

要求：.NET 10 SDK、Windows 10 19041 或更高版本。

```powershell
dotnet build windows/MoDi.Desktop/MoDi.Desktop.csproj --configuration Debug
dotnet run --project windows/MoDi.Desktop/MoDi.Desktop.csproj
```

Windows 真实应用入口为 `windows/MoDi.Desktop`。测试 UI 位于 `test-ui/UITest`，只用于视觉定稿和演示，不连接真实音频、网络或系统功能。

### Android

要求：JDK 17、Android SDK 36。

```powershell
cd android
.\gradlew.bat :app:testDebugUnitTest :app:assembleDebug --no-daemon
```

Gradle Wrapper 已改为官方 HTTPS 地址并固定 SHA-256；Release 配置引用的 `proguard-rules.pro` 仍不存在。当前只验证 Debug 测试与 APK 构建，不得据此写成 Release 可发布。

## 测试

从仓库根目录串行运行：

```powershell
dotnet test windows/MoDi.App.Contracts.Tests/MoDi.App.Contracts.Tests.csproj --no-restore
dotnet test windows/MoDi.Desktop.Tests/MoDi.Desktop.Tests.csproj --no-restore
dotnet test windows/MoDi.Presentation.Tests/MoDi.Presentation.Tests.csproj --no-restore
dotnet test windows/MoDi.Architecture.Tests/MoDi.Architecture.Tests.csproj --no-restore
dotnet test test-ui/UITest.Tests/UITest.Tests.csproj --no-restore
```

多个测试项目共享编译输出，建议串行执行，避免并发写入同一 `obj/bin` 造成增量构建缓存冲突。

## 架构边界

```text
MoDi.App.Contracts                 平台无关接口与不可变状态
        ↑
MoDi.Presentation                  共享 AXAML、样式、字体、资产与模块 ViewModel
        ↑                                      ↑
test-ui/UITest                               MoDi.Desktop
仅内存假实现                                  真实 Windows 适配器与服务

Android MainActivity → MoDiApp → MoDiRuntime → 现有链路/音频能力
```

- 测试 UI 不引用 `MoDi.Desktop`，也不访问真实网络、音频、注册表、进程或更新器。
- Windows 宿主只负责组合真实实现；视觉模块保持一个模块一个 AXAML 文件。
- Android 入口、页面、舞台、管线卡、按钮、导航、设置和运行时适配器按文件拆分。
- 当前协议帧头为 15 字节；公开应用以冻结 API 和带提交指纹的二进制包为依据。
- 协议实现源码由所有者按专有许可管理，不包含在本仓库；应用仅固定引用本地 `0.1.1` JAR/NuGet。合格法律复核完成前，不宣称具备对外分发条件。

## 项目目录

```text
MoDi-Connect/
├─ android/                       Android 应用
├─ windows/                       Windows 应用、共享 UI 与测试
├─ third_party/modi-protocol/     固定版本 0.1.1 协议二进制候选包（专有许可，仅二进制）
├─ assets/                        图标与字体制品
├─ content/                       双端共享 Markdown 单一内容源
├─ scripts/                       构建、发布、许可与协议制品校验脚本
├─ test-ui/                       UI 设计语言与隔离 TestUI
└─ docs/                          架构与发布文档
```

## 文档入口

- [UI 设计语言](test-ui/UI设计语言v1.0.md)
- [Windows UI 设计书](test-ui/墨堤Win端UI设计书.md)

## 许可证说明

仓库根目录应用自有代码按 [GNU GPL v3 或更高版本](LICENSE) 发布。`license-map.v1.json` 以最长路径前缀记录全部仓库文件的唯一许可归属；Gradle Wrapper、Concentus 与五套字体分别遵守各自许可证。

协议层为专有许可的闭源组件：两端应用只消费固定版本 `0.1.1` JAR 与 NuGet 二进制（`third_party/modi-protocol/`），随附协议专有许可、二进制再分发授权、GPLv3 第 7 节附加许可和第三方通知。上述法律文件仍明确标记“对外分发被阻止，等待合格法律复核”，因此当前候选包不得对外分发。

Copyright © 2026 Silvite
