# 第三方声明

墨堤 Windows 客户端当前使用或引用以下主要第三方组件：

- Avalonia UI 12.1.0：桌面 UI、主题与字体支持。
- Material.Icons.Avalonia 3.0.2：界面图标。
- NAudio 2.3.0：Windows 音频访问。
- Concentus 2.2.2：Opus 音频编解码。
- QRCoder 1.6.0：配对二维码生成。
- Makaretu.Dns.Multicast 0.27.0：局域网服务发现。
- InTheHand.Net.Bluetooth 4.2.4：蓝牙能力。
- Android SDK Platform-Tools 37.0.1：Windows USB 链路使用的应用私有 ADB 运行时；发行包在 `tools/adb/NOTICE.txt` 保留 Google 随该版本提供的完整声明。
- Microsoft SysVAD：虚拟音频驱动研发基础，原始示例和 MoDi 修改按 Microsoft Public License（MS-PL）管理。
- Gradle Wrapper 8.11.1：Android 构建启动组件，使用 Apache License 2.0。
- Concentus Java 1.0.1：Android 本地 Opus 编解码 JAR，SHA-256 固定为 `288f4f1e646943d9a616188e8fd82d6e8f4f475d7f024409c5fdb7fa8fc12618`，使用 BSD-3-Clause。
- 阿里妈妈东方大楷 1.006 beta：主标题字体；按淘宝（中国）软件有限公司随字体提供的厂商许可，将官方 TTF 原样嵌入，不转换、不拆分、不子集化、不改名。
- 霞鹜文楷 1.522：功能与设备列表字体；基于官方 Regular 字重生成并重新命名的应用子集，使用 SIL Open Font License 1.1。
- 朱雀仿宋 0.212：正文字体；基于官方 Regular 字重生成并重新命名的应用子集，使用 SIL Open Font License 1.1。
- 源樣明體 2.100：小字补充字体；基于官方 TC Regular 字重生成并重新命名的应用子集，使用 SIL Open Font License 1.1。
- 思源宋体 2.003R：未覆盖节点的默认字体；基于官方 SC Regular 字重生成并重新命名的应用子集，使用 SIL Open Font License 1.1。

本页是随应用提供的摘要，不替代各组件自己的许可证文本。五份完整字体许可随 Windows 输出置于 `FontLicenses`，Android 包内置于 `res/raw`；MS-PL、Apache-2.0 和 Concentus BSD-3-Clause 正文保存在仓库 `LICENSES`，Concentus 许可另与本地 JAR 同目录保存。正式发布包还必须按实际解析依赖生成完整组件清单，并保留所需许可证与版权声明。

墨堤协议组件已通过固定版本 `MoDi.Protocol 0.1.1` 二进制接入，协议实现源码不在应用仓库中。发行制品随附协议专有许可、二进制再分发授权、GPLv3 第 7 节附加许可和协议第三方通知；这些文件及 owner 发布许可复核记录已完成归档。
