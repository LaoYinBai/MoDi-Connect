# Windows Package A 真实应用验收说明

本目录保存首次将已验收 Windows TestUI 接入真实应用的复核证据。真实应用和 TestUI 当前加载相同的 `MoDi.Presentation` AXAML、样式、字体、舞台资产和模块 ViewModel；差异只在组合根：TestUI 提供内存假实现，`MoDi.Desktop` 提供真实 Windows 适配器。

## 重现截图

从仓库根目录关闭正在运行的仓库内 `MoDi.Desktop.exe`，执行：

```powershell
dotnet build windows/MoDi.Desktop/MoDi.Desktop.csproj --configuration Debug --verbosity minimal
powershell -ExecutionPolicy Bypass -File windows/MoDi.Desktop/Validation/capture-production-evidence.ps1
```

脚本启动准确的 Debug 可执行文件，通过具名 UI Automation 控件切换页面和主题，在 150% DPI 下捕获原生 1920×1080 窗口，再高质量缩放为 1280×720。脚本在 `finally` 中关闭自己启动的进程。

## 证据文件

| 真实应用证据 | 测试 UI 参考 | 核对内容 |
|---|---|---|
| `package-a-production-main-dark.png` | `test-ui/UITest/Validation/10-shell-main-dark.png` | 深色资源、五字体、功能栏、舞台和 Shell 模块 |
| `package-a-production-main-light.png` | `test-ui/UITest/Validation/11-shell-main-light.png` | 浅色资源和完整字体角色 |
| `package-a-production-settings.png` | `test-ui/UITest/Validation/14-shell-settings.png` | 设置页释放功能栏空间，当前两套预设 + 完整自定义外观 |
| `package-a-production-about.png` | `test-ui/UITest/Validation/15-shell-about.png` | 关于页读取白名单内置 Markdown |

历史 TestUI 图片早于“全窗口导航居中、V1 两预设 + 完整 Custom、About 内置 Markdown”三个最终决策，因此不覆盖原图。当前两个宿主加载同一 Presentation，架构测试阻止复制生产页面。

## 五字体

`FontFamilyTitle`、`FontFamilyFunction`、`FontFamilyBody`、`FontFamilyAnnotation`、`FontFamilyDefault` 依次只解析到项目内阿里妈妈东方大楷、霞鹜文楷、朱雀仿宋、源樣明體和思源宋体；真实应用与 TestUI 共用同一组资源，不使用系统默认字体表达设计层级。

## 项目边界

```text
MoDi.App.Contracts                 平台无关接口与不可变快照
        ↑
MoDi.Presentation                 共享 AXAML、资源、资产和模块 ViewModel
        ↑                                      ↑
test-ui/UITest                               MoDi.Desktop
仅内存假实现                                  真实适配器和 Windows 服务
```

- `MoDi.App.Contracts` 无项目或第三方包依赖。
- `MoDi.Presentation` 只引用 Contracts 和 UI 包。
- TestUI 不含生产、网络、音频、注册表、进程、Git、更新器或插件加载器实现。
- `MoDi.Desktop` 当前仍直接项目引用协议源码，该临时边界必须由 Package B 删除。
- `ProductionComposition` 是唯一生产组合根；可选模块故障不会阻止接收主路径，接收器故障也不阻止设置、关于和内置内容。

## 自动化结果

2026 年 8 月 11 日 Package A 门槛：Contracts 6、Presentation 106、TestUI 29、Desktop 83、Architecture 8，合计 232 项全部通过。

Debug 构建成功，生产程序正常启动和关闭；UI Automation 完成主界面、设置、关于和主题切换。详细汇总见 [Windows 真实 UI 验收记录](../../../docs/验收/Windows真实UI验收记录.md)。

## 延后事项

- Package B：协议 JAR 与 DLL/NuGet 二进制边界。
- Package C：外部 DLL 与独立 EXE 插件宿主。
- Package D：稳定/测试更新、内置私有 Git/.NET、固定 Commit SHA 与 Gitee 凭据处理。
