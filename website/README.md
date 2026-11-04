# Tomatotodo 官网

静态官网，介绍 Windows 和 Android 客户端，包含功能说明、下载入口和常见问题。无需安装前端依赖。

## 本地预览

在本目录运行：

```bash
python -m http.server 4190
```

访问 `http://localhost:4190`。也可用任何静态服务器托管本目录。

## 部署

站点入口为 `index.html`，资源使用相对路径，可部署到子目录。GitHub Pages 可通过仓库的 Pages 设置启用 GitHub Actions，并配置 `.github/workflows/website.yml` 中的工作流。

下载按钮固定指向已发布的 Windows 1.6.2 和 Android 1.0.3 安装包；发布新版本后需更新页面版本号、下载链接、更新说明链接和系统要求。

首页、功能区和图库使用实际运行截图：Windows 为已安装客户端，Android 为 Android Studio 手机和平板模拟器中的运行画面。仅去除模拟器外壳，软件界面不经过 AI 重绘。

`poster.html` 是 1600×1000 的宣传海报源文件，`assets/cover.png` 为导出的封面。环境背景由 ImageGen 生成，软件画面仍使用原始截图。图库支持三端切换和原图放大。网站不收集账户信息，不使用分析追踪服务。
