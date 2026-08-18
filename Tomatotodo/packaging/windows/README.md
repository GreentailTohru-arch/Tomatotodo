# Windows 打包

此目录只保存可维护的源码和脚本。`deps/`、`build/`、WebView2 DLL 与安装包均由脚本生成并被 `.gitignore` 排除。

先在项目根目录生成前端：

```powershell
pnpm run build
```

生成可运行的 Windows WebView2 应用：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/windows/build.ps1
```

同时生成单文件安装包：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/windows/build.ps1 -Setup
```

输出位置：`packaging/windows/build/`。

该外壳启用 Per-Monitor V2 高 DPI，并在 WebView2 获得焦点时可靠处理 F12 无边框全屏。
