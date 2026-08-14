# MoDi Connect — ProGuard/R8 规则
#
# 当前 buildTypes.release.isMinifyEnabled = false（混淆未启用）。
# 本文件为构建引用基线 + 未来开启混淆时的保守规则。
# 开启混淆前必须逐条复核并在真机回归验证。

# ── 协议二进制（com.silvite.modi:modi-protocol-jvm:0.1.1）──
# 纯 Kotlin/JVM 库，无反射、无序列化；保持公开 API 稳定。
-keep class com.modi.protocol.** { *; }

# ── Concentus（Opus 编解码，libs/concentus-1.0.1.jar）──
# 纯 Java 库，无反射；保守保留，避免未来混淆后行为变化。
-keep class Concentus.** { *; }

# ── MLKit 条形码扫描（com.google.mlkit:barcode-scanning）──
# MLKit 内部依赖反射与序列化，必须整体保留。
-keep class com.google.mlkit.** { *; }
-keep class com.google.android.gms.** { *; }

# ── Compose ──
# Compose 编译器插件已自动处理 @Composable 相关保留，无需额外规则。

# ── Kotlin 协程 ──
# 无反射需求，默认规则即可；保留挂起函数桥接以防内联优化误伤。
-keepclassmembers class kotlinx.coroutines.** { volatile <fields>; }

# ── 保留行号与源文件（发布崩溃日志可读）──
-keepattributes SourceFile, LineNumberTable
-renamesourcefileattribute SourceFile
