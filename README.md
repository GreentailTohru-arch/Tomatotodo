<p align="center">
  <img src="./icon-192.png" width="112" height="112" alt="Tomatotodo Logo" />
</p>

<h1 align="center">Tomatotodo</h1>

<p align="center">
  一款融合番茄计时、任务管理与专注数据分析的桌面效率应用。
</p>

<p align="center">
  <strong>Material Design 3 · Material You · React · Vite · WebView2</strong>
</p>

## 项目简介

Tomatotodo 希望把“计划任务、开始专注、回顾时间”放在同一个安静、清晰的工作空间中。

应用采用 Google Material Design 3 设计语言与 Material You 动态配色，提供可自由编排的仪表盘、任务清单预设、多种计时方式、专注档案和轻量实用工具。Windows 版本使用原生 WebView2 外壳，不捆绑 Electron、Node.js 或完整浏览器运行库。

当前版本：**1.2.0**

## 主要功能

### 可编辑仪表盘

- 按标准网格添加、关闭和拖拽组件。
- 点击保存后记录组件顺序、显示状态和自定义布局。
- 包含大计时器、任务清单、当前任务、日历、天气、日期时间、倒数日、圆表、媒体、名言警句等组件。
- 响应式安全边界保证窗口缩放时文字和控件保持完整。

### 专注计时

- 专注与短休计时，可按设置自动轮换。
- 正向计时适合碎片时间记录，并正常计入专注档案。
- 沉浸模式提供全屏翻页时钟与状态提示。
- 小窗模式可在其他窗口上方显示状态、倒计时和暂停控制。
- 完成一轮专注会获得番茄，并记录任务、时间与时长。

### 任务清单

- 创建、编辑、删除和排序多套任务清单预设。
- 任务支持小标题、预计番茄数和完成状态。
- 支持长按拖拽调整任务顺序。
- 可选择当前任务，并按预计番茄数连续执行专注轮次。

### 专注档案

- 查看今日专注时间、完成番茄、任务进度和专注次数。
- 专注日历支持查看每天的专注时间与番茄数量。
- 提供日度、周度、月度和年度专注统计折线图。
- GitHub 风格年度专注热力图。
- 单日专注日志记录时间、任务与持续时长。
- 一键导出固定比例的专注明信片 PNG，包含今日摘要、日志、日期、Logo 与水印。

### Material You 个性化

- 自动、日间和夜间主题模式。
- 多套 Material You 主题色预设及自定义种子颜色。
- 支持纯黑模式、字体缩放和多种配色方案。
- Windows 原生标题栏跟随应用内日夜模式切换。

### 工具与媒体

- 自动保存、可拖拽的快捷便笺。
- 翻译、基础计算器和支持分段记录的秒表。
- Spotify 当前媒体展示。
- 本地音乐文件夹导入、播放进度、音量、静音与播放顺序控制。
- 实时天气、倒数日与可配置圆表。

### 数据管理

- 任务、设置、便笺和专注档案默认保存在本机。
- 支持导入或导出版本化 JSON 用户数据备份。
- Spotify 登录令牌、本地音乐文件和文件夹授权不会写入备份。
- 恢复出厂数据采用两次确认，并恢复保存的 1.2 默认配置模板。

## 安装 Windows 版本

### 系统要求

- Windows 10 或 Windows 11，x64。
- Microsoft Edge WebView2 Runtime。多数现代 Windows 系统已预装。
- 建议使用 1920 × 1080 或更高分辨率。

### 安装步骤

1. 从 GitHub Releases 下载 `Tomatotodo-Setup-1.2.0.exe`。
2. 运行安装程序并确认安装。
3. 安装程序会在当前用户目录安装应用，并创建桌面与开始菜单快捷方式。
4. 升级安装会保留现有任务、设置、便笺和专注记录。

> 如果安装包尚未进行代码签名，Windows SmartScreen 可能显示安全提示。请确认文件来自本仓库的正式 Release。

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `F12` | 进入或退出无边框全屏 |
| `F5` | 刷新天气数据，不重新加载应用 |
| `Esc` | 关闭当前便笺或工具窗口 |

## 本地开发

项目使用 **pnpm** 管理依赖。

```bash
pnpm install --frozen-lockfile
pnpm dev
```

Vite 开发服务器默认运行在 `http://localhost:5173/`。

### 构建与测试

```bash
pnpm run build
pnpm run test:sites
```

也可以一次执行完整检查：

```bash
pnpm run check
```

前端构建结果位于 `dist/client/`。

## 构建 Windows 安装包

Windows 打包需要 Windows、.NET Framework 4.8 编译环境及 WebView2 Runtime。首次构建会从 NuGet 下载 WebView2 SDK。

```powershell
pnpm run build
powershell -ExecutionPolicy Bypass -File packaging/windows/build.ps1 -Setup
```

输出文件：

```text
packaging/windows/build/Tomatotodo/Tomatotodo.exe
packaging/windows/build/Tomatotodo-Setup-1.2.0.exe
```

Windows 外壳支持 Per-Monitor V2 DPI、100% WebView 基础缩放、应用主题标题栏以及 F12 无边框全屏。

## 项目结构

```text
src/                         React 页面、状态与组件样式
src/components/              玻璃与工具窗口组件
public/                      PWA 清单、离线缓存与应用图标
packaging/windows/           WebView2 原生外壳与安装程序源码
scripts/                     构建准备脚本
tests/                       自动化测试
worker/                      Web Worker / Sites 入口
dist/client/                 前端构建产物（不提交 Git）
```

## 技术栈

- React 19
- Vite 6
- Material Color Utilities
- Phosphor Icons
- Microsoft Edge WebView2
- .NET Framework WinForms Windows 外壳

## 数据与隐私

- 应用数据默认保存在浏览器本地存储或 Windows WebView2 本地配置目录。
- 本地音乐仅在当前设备读取，不会上传到 Tomatotodo 服务器。
- 启用天气、在线名言或 Spotify 功能时，应用会向相应第三方服务发送必要请求。
- Windows 卸载程序会清理本地应用数据；请在卸载前导出 JSON 备份。

## 1.2.0 更新日志

- 全面升级为 Material Design 3 界面与固定左侧导航。
- 新增可添加、关闭、拖拽和保存的组件化仪表盘。
- 重构任务清单配置，支持小标题、预计番茄数与预设排序。
- 重构专注档案，新增统计图、热力图和专注明信片导出。
- 新增沉浸翻页时钟、圆表、秒表与本地音乐控制。
- 完善 Material You 配色、日夜模式、数据备份和响应式布局。
- Windows 版本改善 DPI 清晰度，并让原生标题栏跟随应用主题。

## 参与开发

欢迎通过 Issue 报告问题或提出建议。提交 Pull Request 前，请先运行：

```bash
pnpm run check
```

## 致谢

感谢 React、Vite、Material Design、Material Color Utilities、Phosphor Icons 与 Microsoft WebView2 等项目提供的优秀工具与设计基础。

---

<p align="center">把今天安排清楚，也把每一段专注看得见。</p>
