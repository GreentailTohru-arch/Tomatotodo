<p align="center">
  <img src="./icon.png" width="80" height="80" alt="Tomatotodo 图标">
</p>

<h1 align="center">Tomatotodo</h1>

<p align="center">适用于 Windows 和 Android 的番茄计时、任务管理与专注记录应用。</p>

<p align="center">
  <a href="https://greentailtohru-arch.github.io/Tomatotodo/">官网</a> ·
  <a href="https://github.com/GreentailTohru-arch/Tomatotodo/releases">发布记录</a> ·
  <a href="https://greentailtohru-arch.github.io/Tomatotodo/#desktop-guide">图文指南</a>
</p>

## 下载与安装

| 平台 | 系统要求 | 安装文件 | 更新说明 |
| --- | --- | --- | --- |
| Windows | Windows 10 2004 或更新版本，x64 | [下载 EXE](https://github.com/GreentailTohru-arch/Tomatotodo/releases/download/v1.6.2/Tomatotodo-Setup-1.6.2.exe) | [1.6.2](https://github.com/GreentailTohru-arch/Tomatotodo/releases/tag/v1.6.2) |
| Android | Android 7.0 或更新版本，支持手机和平板 | [下载 APK](https://github.com/GreentailTohru-arch/Tomatotodo/releases/download/android-v1.0.3/Tomatotodo-1.0.3.apk) | [1.0.3](https://github.com/GreentailTohru-arch/Tomatotodo/releases/tag/android-v1.0.3) |

Windows 下载后运行安装程序。Android 下载后打开 APK，按系统提示完成安装。当前公开 APK 使用调试证书签名。升级或迁移前，建议在应用内导出 JSON 备份。

## 使用说明

1. 在「配置」创建清单并添加任务，可为任务设置预计番茄数。
2. 在「仪表盘」选择任务并开始计时。可在「常规」调整专注时长、短休和循环方式，或启用正向计时。
3. 在「档案」查看专注日志、日历、统计和热力图。

仪表盘支持增删和重排组件。课程表支持按周查看、课前提醒和独立的 JSON 导入导出。应用可本地使用；跨设备同步需要登录云端账户。

## 源码结构

| 目录 | 内容 | 开发文档 |
| --- | --- | --- |
| [`windows/`](./windows/) | Windows 客户端，C#、.NET 10、WinUI 3 | [构建、测试与打包](./windows/README.md) |
| [`android/`](./android/) | Android 客户端，Flutter、Dart、Kotlin | [运行、测试与打包](./android/README.md) |
| [`Website/`](./Website/) | 官网、实机截图和图文指南 | [预览与部署](./Website/README.md) |

仓库包含两个客户端的源码。云端服务端未包含在本仓库中。

## 反馈与许可

请通过 [GitHub Issues](https://github.com/GreentailTohru-arch/Tomatotodo/issues) 提交反馈，并附上平台、应用版本、复现步骤和实际结果。

本项目采用 [MIT License](./LICENSE)。
