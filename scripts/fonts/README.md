# 双端 UI 字体构建

本目录维护墨堤 Windows、Android、Windows TestUI 与 Android Debug TestUI 共用的五字体设计语言。完整源字体不进入 Git，也不依赖开发机已安装字体；默认保存在仓库外的 `D:\MoDi-Local-Font-Library`。

## 字体角色

| 角色 | 字体 | 处理方式 |
|---|---|---|
| 主标题 | 阿里妈妈东方大楷 | 官方 TTF 原样打包，禁止转换、拆分、子集化和改名 |
| 功能 / 设备列表 | 霞鹜文楷 | 从官方 Regular 字重生成应用子集 |
| 正文 | 朱雀仿宋 | 从官方 Regular 字重生成应用子集 |
| 小字补充 | 源樣明體 | 从官方 TC Regular 字重生成应用子集 |
| 默认 / 未覆盖 | 思源宋体 | 从官方 SC Regular 字重生成应用子集 |

四个开放字体子集固定包含 GB2312 的 6763 个汉字、ASCII、常用标点、仓库静态 UI 文案和 `extra-characters.txt` 的人工增量字符。阿里妈妈东方大楷只做来源哈希及覆盖校验，应用制品必须与官方 TTF 逐字节一致。

生成后的字体位于 `assets/fonts/android-res/font`，五份上游完整许可逐字节复制到 `assets/fonts/android-res/raw`。Android 通过共享资源目录直接打包两类文件；Windows 将字体作为 Avalonia 资源嵌入，并把许可复制到发布目录的 `FontLicenses`。制品锁同时固定字体与许可的文件名、长度和 SHA-256。

## 来源锁

`font-sources.lock.json` 固定每个字体的官方来源、版本或上游 revision、源文件大小、SHA-256、内部家族名、许可文件及允许的处理方式。生成器会先验证本地源文件和许可文件；任一哈希不符即停止，不会更新应用制品。

默认字库可通过环境变量覆盖：

```powershell
$env:MODI_FONT_LIBRARY = 'E:\Fonts\MoDi'
```

本地字库只需保存锁文件列出的源文件和许可文件。上游 Git sparse checkout 可用于取回文件，但不属于可分发应用内容，也不要复制进仓库。

## 后续补字

新增静态 UI 文案后，Android `preBuild` 与 Windows `CoreCompile` 会运行只读验证器；字符集合变化时构建会关闭失败并提示重建。若动态设备名、插件名或外部文档需要基础集合之外的字符，把字符加入 `extra-characters.txt`，运行 `Build-Fonts.ps1`，再执行 Android、Windows 和 TestUI 测试。普通构建只验证已提交字体、许可、字符集合和制品锁，不要求协作者拥有完整本地字库。

上游字体升级必须单独更新来源锁、重新审查许可并进行视觉验收；普通补字不得静默升级字体版本。
