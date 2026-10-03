# Tomatotodo Android 1.0.3+9

Flutter / Material Design 3 客户端，支持 Android 手机、平板、桌面小组件和多语言布局。

## 环境与运行

安装满足 `pubspec.yaml` Dart SDK 约束的 Flutter SDK、Android SDK 和 Java 17。Android Studio 打开本目录，配置本机 SDK，不提交 `android/local.properties`。

```bash
flutter pub get
flutter devices
flutter run
flutter analyze
flutter test
flutter build apk --release
```

APK 输出到 `build/app/outputs/flutter-apk/`。当前 release 配置使用调试签名；正式分发应自行配置签名，不提交 keystore 或密码。

## 目录

- `lib/app/`：入口、导航和主题。
- `lib/features/`：仪表盘、清单、档案、课程与设置。
- `lib/core/`：账户、同步、语言、更新与小组件桥接。
- `android/`：Kotlin 平台代码、通知及系统桌面小组件。
- `localization/`：语言目录和翻译资料。
- `test/`：布局、计时、同步及其他功能测试。
- `docs/cross-platform-sync-v2.md`：跨平台数据结构说明。

`ios/` 和 `web/` 为保留的平台工程，不表示已发布或完成验证的 iOS / Web 产品。云端服务端不包含在本目录中。

## 图标

`assets/branding/tomatotodo-logo.png` 为客户端图标源。生成平台图标：

```bash
dart run flutter_launcher_icons
```

请勿提交构建缓存、安装包、账户数据、认证令牌、本机 SDK 路径或签名私钥。
