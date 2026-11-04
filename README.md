<div align="center">
  <img src="./icon.png" alt="Tomatotodo 图标" width="96">
  <h1>Tomatotodo</h1>
  <p>适用于 Windows 与 Android 的番茄计时、任务管理与专注记录应用。</p>
  <p>
    <a href="https://github.com/GreentailTohru-arch/Tomatotodo/releases/tag/v1.6.2"><img src="./.github/assets/windows.svg" alt="Windows 1.6.2"></a>
    <a href="https://github.com/GreentailTohru-arch/Tomatotodo/releases/tag/android-v1.0.3"><img src="./.github/assets/android.svg" alt="Android 1.0.3"></a>
    <a href="./LICENSE"><img src="./.github/assets/license.svg" alt="MIT 许可证"></a>
  </p>
  <p>
    <a href="#下载安装">下载安装</a> ·
    <a href="#界面预览">界面预览</a> ·
    <a href="#快速开始">快速开始</a> ·
    <a href="#从源码构建">从源码构建</a> ·
    <a href="https://greentailtohru-arch.github.io/Tomatotodo/">官网</a>
  </p>
  <a href="https://greentailtohru-arch.github.io/Tomatotodo/"><img src="./Website/assets/cover.png" alt="Tomatotodo 的 Windows、Android 手机与平板界面预览" width="880"></a>
</div>

## 下载安装

| 平台 | 系统要求 | 安装包 | 版本说明 |
| --- | --- | --- | --- |
| Windows | Windows 10 2004 或更新版本，x64 | [下载 EXE](https://github.com/GreentailTohru-arch/Tomatotodo/releases/download/v1.6.2/Tomatotodo-Setup-1.6.2.exe) | [1.6.2](https://github.com/GreentailTohru-arch/Tomatotodo/releases/tag/v1.6.2) |
| Android | Android 7.0 或更新版本，支持手机和平板 | [下载 APK](https://github.com/GreentailTohru-arch/Tomatotodo/releases/download/android-v1.0.3/Tomatotodo-1.0.3.apk) | [1.0.3](https://github.com/GreentailTohru-arch/Tomatotodo/releases/tag/android-v1.0.3) |

Windows 下载后运行安装向导；Android 打开 APK，按系统提示安装。当前公开 APK 使用调试证书签名。升级前建议在应用内导出 JSON 备份。历史版本见 [GitHub Releases](https://github.com/GreentailTohru-arch/Tomatotodo/releases)。

## 界面预览

### Windows

<table>
  <tr>
    <td width="50%" align="center"><img src="./Website/assets/windows-dashboard.png" width="420" alt="Windows 仪表盘"><br><sub><b>仪表盘</b> · 选择当前任务、控制计时并编排组件</sub></td>
    <td width="50%" align="center"><img src="./Website/assets/windows-archive.png" width="420" alt="Windows 专注档案"><br><sub><b>专注档案</b> · 查看日期日志、统计图和年度热力图</sub></td>
  </tr>
</table>

### Android

<table>
  <tr>
    <td width="30%" align="center"><img src="./Website/assets/phone-dashboard.png" width="190" alt="Android 手机仪表盘"><br><sub><b>手机</b> · 底部导航与触控计时</sub></td>
    <td width="70%" align="center"><img src="./Website/assets/tablet-dashboard.png" width="560" alt="Android 平板仪表盘"><br><sub><b>平板</b> · 侧边导航与多列仪表盘</sub></td>
  </tr>
</table>

<details>
<summary>查看更多界面</summary>

<table>
  <tr>
    <td width="50%" align="center"><img src="./Website/assets/windows-presets.png" width="420" alt="Windows 任务清单"><br><sub>Windows · 配置清单</sub></td>
    <td width="50%" align="center"><img src="./Website/assets/tablet-courses.png" width="420" alt="Android 平板课程表"><br><sub>Android 平板 · 课程表</sub></td>
  </tr>
  <tr>
    <td align="center"><img src="./Website/assets/windows-dashboard-dark.png" width="420" alt="Windows 深色仪表盘"><br><sub>Windows · 深色外观</sub></td>
    <td align="center"><img src="./Website/assets/windows-general.png" width="420" alt="Windows 常规设置"><br><sub>Windows · 常规设置</sub></td>
  </tr>
</table>

</details>

截图来自 Windows 已安装客户端及 Android 手机、平板模拟器中的实际运行界面。更多页面操作见[官网图文指南](https://greentailtohru-arch.github.io/Tomatotodo/#desktop-guide)。

## 快速开始

1. 在「配置」创建清单并添加任务。可为任务填写副标题和预计番茄数。
2. 回到「仪表盘」，选择当前任务并开始计时。专注时长、短休与循环方式可在「常规」调整。
3. 打开「档案」，按日期查看日志，或切换统计周期回顾专注时长。

## 主要功能

| 功能 | 说明 |
| --- | --- |
| 专注计时 | 自定义专注与短休时长，支持手动循环、自动循环和正向计时。手动循环在阶段结束后等待操作；正向计时从零累计，不自动结束或发放番茄。 |
| 任务管理 | 管理多份清单，调整任务顺序，设置预计番茄数并跟踪完成状态。 |
| 仪表盘 | 添加、移除和重排计时器、任务、日历、课程和倒数日等组件。Android 可调整部分组件的尺寸。 |
| 专注档案 | 按日查看日志，按日、周、月或年查看统计，并通过年度热力图回顾专注记录。 |
| 课程表 | 按周查看课程时间、教室与教师，设置课前提醒，独立导入或导出课表 JSON。 |
| 外观与语言 | 支持浅色、深色和自动模式，以及自定义主题色；提供多语言和跟随系统语言选项。 |

Windows 提供计时小窗和系统托盘；Android 适配手机和平板，并支持桌面小组件。通知是否显示取决于应用设置与系统权限。

## 数据与同步

- **本地使用**：计时、任务和专注档案无需登录即可使用。可在「常规」导出用户数据 JSON；课表有独立的导入导出入口。
- **跨设备同步**：两端登录同一个云端账户后，可同步清单、课程表、计时设置与专注日志。需要把原有本地数据带到云端时，使用应用内的数据迁移入口。
- **数据范围**：当前计时进度留在本机。Windows 与 Android 的仪表盘布局和外观分别保存；同时修改同一数据分区时，按应用提示处理冲突。

覆盖导入、迁移或清除应用数据前，请先备份。字段与合并规则见[跨平台同步协议](./windows/docs/cross-platform-sync-v2.md)。

## 从源码构建

| 路径 | 内容 | 文档 |
| --- | --- | --- |
| [`windows/`](./windows/) | 原生 Windows 客户端：C#、.NET 10、WinUI 3 | [开发、测试与打包](./windows/README.md) |
| [`android/`](./android/) | Android 客户端：Flutter、Dart、Kotlin | [运行、测试与打包](./android/README.md) |
| [`Website/`](./Website/) | 静态官网、截图与图文指南 | [预览与部署](./Website/README.md) |

仓库包含客户端和官网源码；云端服务端单独维护。

<details>
<summary>展开构建与测试命令</summary>

### Windows

准备 .NET 10 SDK、Windows SDK 和 Visual Studio 的 WinUI 开发工具。可打开 [`Tomatotodo.Windows.slnx`](./windows/Tomatotodo.Windows.slnx)，或从仓库根目录执行：

```powershell
cd windows
dotnet restore Tomatotodo.Windows.csproj
dotnet build Tomatotodo.Windows.csproj -p:Platform=x64
dotnet run --project tests/CourseSchedule.Tests.csproj
dotnet run --project tools/Sync.Tests/Sync.Tests.csproj
```

### Android

准备 Flutter SDK、Android SDK 和 JDK 17。Dart SDK 约束见 [`pubspec.yaml`](./android/pubspec.yaml)。

```bash
cd android
flutter pub get
flutter analyze
flutter test
flutter build apk --release
```

</details>

## 反馈与许可

通过 [GitHub Issues](https://github.com/GreentailTohru-arch/Tomatotodo/issues) 提交问题或建议。报告问题时请注明平台、应用和系统版本，提供复现步骤、预期结果及实际结果；附截图或日志前请移除个人信息。

项目采用 [MIT 许可证](./LICENSE)。
