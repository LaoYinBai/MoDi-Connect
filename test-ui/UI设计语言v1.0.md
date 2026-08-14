# UI 设计语言 v1.0

> 通用设计系统 · 令牌驱动 · Dark-first
> 适用范围：本人所有客户端产品；各产品以"实例化设计书"方式引用本语言
> 最后更新：2026-07-28

---

## 0. 总则

1. **一切样式皆令牌**，代码中禁止出现硬编码色值 / 时长 / 圆角。
2. **Dark 为默认主题**；Light 主题通过 `Light` 后缀令牌镜像，运行时整体换装。
3. **品牌色（Accent 族）深浅共用同一值**，不随主题变化——品牌不换衣服。
4. **语义命名，非色相命名**：用 `Success` 不用 `Green`；色值可改，语义不变。
5. **一套手感**：全软件共用同一张动效参数表，不允许单个组件"特殊处理"。

---

## 1. 令牌命名规则

| 规则 | 说明 | 示例 |
|------|------|------|
| 类别前缀 | 按用途分族 | `Accent*` / `Surface*` / `Text*` / `Meter*` / `Border*` / `Radius*` / `Duration*` / `Easing*` |
| 深色为默认 | 深色令牌无后缀 | `SurfaceBg` |
| 浅色镜像 | 浅色令牌加 `Light` 后缀 | `SurfaceBgLight` |
| 层级后缀 | 同类分级用 `Secondary` / `Muted` | `SurfaceCardSecondary` / `TextMuted` |
| 状态后缀 | 组件状态用 `Hover` / `Pressed` / `Disabled` | `AccentPrimaryHover` |

---

## 2. 色彩令牌（基准实现）

### 2.1 品牌色（深浅共用，不随主题变）

| 令牌 | 色值 | 用途 |
|------|------|------|
| `AccentPrimary` | `#00C389` | 主绿、激活态、主按钮 |
| `AccentBlue` | `#00A3FF` | 辅助蓝、连接 / 提示 |
| `Success` | `#22C55E` | 成功、绿点 |
| `Warning` | `#F59E0B` | 警告 |
| `Error` | `#EF4444` | 错误 |

### 2.2 表面色（深色）

| 令牌 | 色值 | 用途 |
|------|------|------|
| `SurfaceBg` | `#111618` | 窗口背景 |
| `SurfaceCard` | `#171D20` | 一级卡片 |
| `SurfaceCardSecondary` | `#20272B` | 侧边栏 / 二级面板 |
| `BorderDefault` | `#2A3438` | 边框 |
| `AccentBg` | `#182B27` | 选中态背景 |

### 2.3 文字色

| 令牌 | 色值 | 用途 |
|------|------|------|
| `TextPrimary` | `#FFFFFF` | 主文字 |
| `TextSecondary` | `#A7B0B5` | 次文字 |
| `TextMuted` | `#6B7280` | 辅助文字 |

### 2.4 电平条

| 令牌 | 色值 | 用途 |
|------|------|------|
| `MeterNormal` | `#22C55E` | 正常段 |
| `MeterWarning` | `#F59E0B` | 警告段 |
| `MeterDanger` | `#EF4444` | 危险段 |
| `MeterTrack` | `#2A3438` | 轨道背景 |

### 2.5 浅色主题（镜像表）

| 令牌 | 色值 | 对照深色 |
|------|------|---------|
| `SurfaceBgLight` | `#F7F9FA` | ← `SurfaceBg` |
| `SurfaceCardLight` | `#FFFFFF` | ← `SurfaceCard` |
| `TextPrimaryLight` | `#111827` | ← `TextPrimary` |
| `TextSecondaryLight` | `#6B7280` | ← `TextSecondary` |
| `BorderLight` | `#E5E7EB` | ← `BorderDefault` |
| `AccentBgLight` | `#F0FFFA` | ← `AccentBg` |
| `MeterTrackLight` | `#E5E7EB` | ← `MeterTrack` |

> 品牌色（2.1）与状态色深浅共用，不出现在镜像表中。

---

## 3. 字体令牌

| 令牌 | 值 | 用途 |
|------|----|------|
| `FontSizeDisplay` | 28px Bold | 品牌展示、空状态大字 |
| `FontSizePageTitle` | 20px SemiBold | 页面标题 |
| `FontSizeCardTitle` | 16px Medium | 卡片 / 板块标题 |
| `FontSizeBody` | 14px Regular | 正文、按钮、表单 |
| `FontSizeCaption` | 12px Regular | 图注、时间戳、次要说明 |
| `LineHeightTight` | 1.3 | 标题 |
| `LineHeightBody` | 1.6 | 正文 |

---

## 4. 布局令牌

| 令牌 | 值 |
|------|----|
| `WindowWidth` | 1280px |
| `WindowHeight` | 720px |
| `TopBarHeight` | 48px |
| `StatusBarHeight` | 32px |
| `SidebarWidth` | 220px（固定侧栏产品）/ 实例化时可改为弹性区间 |
| `SpacingXS` | 4px |
| `SpacingS` | 8px |
| `SpacingM` | 12px |
| `SpacingL` | 16px |
| `SpacingXL` | 24px |
| `SpacingXXL` | 32px |

---

## 5. 圆角 / 边框 / 滚动条

| 令牌 | 值 |
|------|----|
| `RadiusLargeCard` | 16px |
| `RadiusSmallCard` | 12px |
| `RadiusButton` | 8px |
| `RadiusPill` | 999px |
| `BorderWidth` | 1px |
| `ScrollbarWidth` | 6px（细滚动条，hover 显现，圆角） |

---

## 6. 动效令牌

| 令牌 | 值 | 用途 |
|------|----|------|
| `DurationInstant` | 100ms | 按下反馈 |
| `DurationFast` | 150ms | hover、弹层收起 |
| `DurationBase` | 200ms | 胶囊滑动、拨杆、弹层展开 |
| `DurationSlow` | 300ms | 降级淡变、置灰 |
| `DurationPage` | 240ms | 页面元素显现 |
| `EasingStandard` | `cubic-bezier(0.4, 0, 0.2, 1)` | 快起柔落，默认缓动 |
| `EasingOvershoot` | `cubic-bezier(0.34, 1.56, 0.64, 1)` | 弹层展开微过冲 |
| `EasingOut` | `ease-out` | 页面元素上浮 |
| `HoverLift` | 1px | 按钮 hover 上浮量 |
| `PressScale` | 0.97 | 按下缩放 |
| `PageRevealRise` | 12px | 页面切换元素上浮量 |
| `PageRevealStagger` | 40ms | 分组错落间隔 |

---

## 7. 组件语法

### 7.1 按钮（四级）

| 级别 | 样式 | 使用场景 |
|------|------|---------|
| 主按钮 | `AccentPrimary` 底 + 白字 | 每页唯一主操作 |
| 次按钮 | 透明底 + `BorderDefault` 描边 + `TextPrimary` 字 | 常规操作 |
| 幽灵按钮 | 无边框无底色，hover 浮现底色 | 弱操作 |
| 危险按钮 | `Error` 底（或描边）| 删除、重置等不可逆操作 |

**五态参数（全级别统一）：**

| 状态 | 表现 | 时长 |
|------|------|------|
| Default | 基准样式 | — |
| Hover | 上浮 `HoverLift` + 色深一档 | `DurationFast` |
| Pressed | 缩放 `PressScale` | `DurationInstant` |
| Disabled | 不透明度 40%，不响应 hover | — |
| Loading | 文字替换为居中 spinner，尺寸不塌缩 | — |

### 7.2 卡片

- 圆角按层级取 `RadiusLargeCard` / `RadiusSmallCard`
- `BorderWidth` 描边，`BorderDefault`
- 无阴影或极轻阴影（深色主题优先用描边分层，不用投影）

### 7.3 开关（胶囊拨杆）

- 轨道 = 胶囊形，宽 40px 高 22px
- 滑块 18px 圆点，开启态轨道染 `AccentPrimary`
- 切换 200ms，滑块弹性到位

### 7.4 弹层

- 以触发点为**顶角**做 scale 展开（transform-origin 对准触发按钮）
- 展开 200ms `EasingOvershoot`（微过冲）；收起 150ms `EasingStandard`（无过冲）
- 移出触发区自动收起的类型，收起不抢焦点

### 7.5 Toast（三档）

| 档位 | 形式 | 场景 |
|------|------|------|
| 静默 | 不弹 | 用户预期内的状态变化 |
| 轻提示 | 应用内右下角，4s 自逝 | 可忽略的告知 |
| 系统级 | 调用 OS 原生通知（Windows 右下角）| **必须处理**的事件 |

### 7.6 列表行

- 整行可点的行：hover 浮现 `AccentBg` 底色，150ms
- 行高 44–48px，左侧图标 + 主文案 + 右侧次要信息

---

## 8. 页面切换通则（PCL2 式）

1. 旧页面整体淡出 120ms
2. 新页面元素**按组**（标题组 / 内容组 / 操作组）上浮 `PageRevealRise` + 淡入 `DurationPage` `EasingOut`
3. 组间错落 `PageRevealStagger`——内容"自己浮上来"，不是一整页砸下来

---

## 9. 主题化机制

1. 换主题 = 整体替换令牌表，组件代码零感知
2. 品牌色 / 状态色不参与换装
3. 主题切换的过渡动效由**实例化设计书**定义（本语言只保证令牌可换）

---

## 10. 无障碍与细节

| 项 | 标准 |
|----|------|
| 正文对比度 | ≥ 4.5:1（AA） |
| 焦点可见 | 键盘焦点 2px `AccentBlue` 描边环 |
| 动效降级 | 系统开启"减少动态效果"时，位移类动效降级为纯淡变 |
| 选中色 | `AccentBg` / `AccentBgLight` |
