# Tomatotodo Windows 1.6.2

原生 WinUI 3 客户端，使用 C#、.NET 10 和 Windows App SDK。主界面不是 WebView2。

## 开发环境

- Windows 开发机器、.NET 10 SDK。
- Visual Studio 及 WinUI / Windows SDK 开发工具；Windows SDK 版本以项目文件为准。
- NuGet 联网恢复依赖。推荐 x64 配置。

打开 `Tomatotodo.Windows.slnx`，选择客户端启动项目。MSIX 调试身份需通过 Visual Studio 或项目配置启动，不要直接双击调试目录 exe。

```powershell
dotnet restore Tomatotodo.Windows.csproj
dotnet build Tomatotodo.Windows.csproj -p:Platform=x64
dotnet run --project Tomatotodo.Windows.csproj -p:Platform=x64
```

## 测试

`tests/` 包含账户、课程、语言和关于页面逻辑测试，`tools/Sync.Tests/` 包含共享 JSON 数据测试。例如：

```powershell
dotnet run --project tests/CourseSchedule.Tests.csproj
dotnet run --project tests/Language.Tests.csproj
dotnet run --project tools/Sync.Tests/Sync.Tests.csproj
```

## 打包

安装 Inno Setup 6，并显式传入编译器路径：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-Setup.ps1 -CompilerPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

生成安装程序位于 `packaging/output/`；二进制、安装包和签名证书不提交仓库。

## 数据与服务

账户和同步实现位于 `Accounts/`，共享数据说明位于 `docs/cross-platform-sync-v2.md`。云端服务端不在本目录中。不要将真实用户数据、凭据或服务端密钥写入测试配置。

应用图标资源位于 `Assets/`，源图为 `Assets/icon-source.png`；`scripts/Generate-Assets.ps1` 用于生成平台图标。
