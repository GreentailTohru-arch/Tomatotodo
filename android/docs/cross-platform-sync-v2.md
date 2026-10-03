# Tomatotodo 跨平台同步 v2

## 使用方式

1. 两端使用本次更新后的版本，登录同一个云端账户。手机从右上角头像进入注册/登录；注册仍需管理员发放的 25 位一次性激活码。
2. 首次登录会打开云端数据，本地账户的数据会单独保留。需要上传原来的本地内容时，在“常规 → 账户与用户数据 → 本地与云端数据迁移”选择本地迁移到云端。
3. 修改后约 1 秒自动同步；软件运行时每 20 秒检查另一端更新，手机回到前台也会同步。手动上传会先拉取、合并、处理冲突，再上传，避免盲目覆盖。
4. 无网络时保留本机修改，下次联网/启动重试。已失效的会话需要重新登录，未上传修改仍保留在该账户的本机缓存里。
5. 同一分区两端都改过时，展示“桌面版/移动版”和各自修改时间，用户选择本机或云端版本；不按手机时钟大小自动覆盖。取消选择时两份数据均保留。
6. 退出云端返回之前的本地账户；“云端复制到本地”会先同步，再替换本地快照。迁移、导入和冲突解决前保留备份。

## 一份文件，共用协议

导入/导出采用 `format: tomatotodo-user-data`、`version: 2`：

| 分区 | 内容 | 同步规则 |
| --- | --- | --- |
| shared.tasks | 清单、任务、副标题、预算、完成状态、截止/提醒/重复、历史任务、当前选择 | 三方比较；同分区同时编辑时询问用户 |
| shared.timetable | 原有 tomatotodo-course-schedule v1 课程文件 | 三方比较；冲突询问用户 |
| shared.timer | 专注/短休长度、自动短休、正向计时开关 | 三方比较 |
| shared.countdown | 倒数日名称和日期 | 三方比较 |
| shared.profile | 昵称、简介、256px 头像 | 三方比较；不包含账户授权身份 |
| shared.archive | 带稳定 UUID 的专注日志及删除标记 | 按 ID 合并去重；删除标记防止旧设备把已清理日志带回来 |
| platforms.windows | WinUI 外观、仪表盘和 Windows 专属设置 | 由 Windows 适配器解释；手机原样保留 |
| platforms.mobile | MD3 外观、仪表盘和 Android 专属设置 | 由 Flutter 适配器解释；Windows 原样保留 |
| changes | 分区最近编辑平台与 UTC 时间 | 用于冲突说明；不用于决定谁获胜 |

共享的是协议、字段含义、测试夹具和合并规则。C# 与 Dart 分别提供适配器，避免把 Windows 页面状态套到 Android。两端导出同一个 v2 文件，保留另一平台的专属分区。

## 一致性与安全

- 服务器整数 `version` + 行锁实现乐观并发：旧版本上传返回 409，客户端重新拉取；不会使用最终写入者无条件覆盖。
- 自动同步仅上传持久数据，不上传当前计时进度、音频文件、文件夹授权、设备权限、密码、JWT。
- 计时进度留在本机。专注时间在现有暂停/结束/重置等提交点成为日志后同步；两个设备分别计时产生不同日志，可累加。
- 任务 CompletedPomodoros 保存历史基数，显示时再叠加已完成日志，防止跨端反复导入导致番茄翻倍。
- Windows 会话使用 DPAPI；Android 使用 Android Keystore AES-GCM，令牌文件位于 noBackupFilesDir。HTTPS 保持证书验证。
- 手机应用数据前完整校验，并保存恢复日志；Windows 使用原有账户目录原子替换。
- 手机处于后台/被系统终止时不承诺实时上传；桌面小组件离线事件由现有事件队列接收，在手机 App 恢复后同步。
- 同步颗粒度是分区；例如同一时间修改不同清单仍可能提示一次“配置清单”冲突。档案日志始终合并。
- 旧桌面云端快照首次读取时升级。升级后的账户拒绝旧客户端原生格式回写（426），因此需要同时更新两端。

## 实现入口

- Flutter：`lib/core/account/unified_user_data.dart`、`cloud_account.dart`、`cloud_account_ui.dart`。
- WinUI：`Accounts/UnifiedUserData.cs`、`CloudAccountService.cs`、`MainPage.Accounts.cs`。
- 后端：`backend/app/sync.py`、`backend/app/main.py`。
- 兼容夹具：`unified-v2.json`；Windows → Flutter → Windows 往返检查验证共享字段不会凭空改变。

## 验证

- Flutter `flutter analyze` 与 `flutter test`。
- Windows `dotnet build Tomatotodo.Windows.csproj -p:Platform=x64`、`dotnet run --project tests/Accounts.Tests.csproj`。
- 协议测试 `dotnet run --project tools/Sync.Tests/Sync.Tests.csproj -- fixture <desktop.json>`；Flutter 测试通过 SYNC_FIXTURE/SYNC_OUTPUT 读写夹具；Windows roundtrip 模式检查返回文件。
- 后端使用随机隔离 PostgreSQL 数据库：`uv run python tests/run_isolated.py`，不读取或清空生产用户数据。

## 本次后端部署

2026-09-30 已将两个修改文件备份到 `/opt/tomatotodo/backups/sync-v2-20260930-102627`，核对原始 SHA256 后更新并重启 HTTP/HTTPS 服务。公网 HTTPS `/health` 返回 ok，OpenAPI 包含 `/api/auth/refresh`。未修改生产用户记录、安装包或已发布版本指针。
