# 墨堤互联协议第三方组件通知

External distribution status: BLOCKED pending qualified legal review.

本清单描述协议 `0.1.x` 构建图中的第三方组件，不改变各组件原始许可证。协议发布验证器必须确认 JAR 和 NuGet/DLL 没有内嵌第三方实现类、应用类、源码或调试符号。

## 运行依赖

- Kotlin 标准库 `2.1.0`：Apache License 2.0，来源 `https://github.com/JetBrains/kotlin`。
- kotlinx.coroutines core `1.10.1`：Apache License 2.0，来源 `https://github.com/Kotlin/kotlinx.coroutines`。
- .NET 实现仅使用目标框架 `.NET 10` 的基础类库，不声明额外 NuGet 运行依赖。

JVM JAR 不是 fat JAR；Kotlin 标准库和 kotlinx.coroutines 由 Maven/Gradle 依赖解析，不复制进协议 JAR。NuGet 包只包含 `MoDi.Protocol.dll` 和法律材料。

## 构建与测试依赖

Gradle、Kotlin Gradle Plugin、JUnit 5、kotlinx.serialization、xUnit、Pester 和 .NET SDK 仅用于构建或测试，不进入协议运行二进制。版本与制品哈希由 `toolchain.lock.json`、Gradle dependency verification/lock 和 NuGet lock 文件固定。

正式外部分发前仍须由合格法律审核确认本通知、最终依赖图和随包许可证正文完整；当前清单不得被解释为解除外部分发停止门。
