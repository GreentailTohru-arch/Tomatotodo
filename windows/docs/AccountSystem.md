# 本地 / 云端双模账户

## 已实现的使用流程

左侧导航栏底部、常规上方的圆形头像入口打开独立 `AccountWindow`。窗口有可拖动标题栏，不阻断主窗口操作；打开和关闭使用短暂的位移/透明度动画，遵循 Windows 的关闭动画设置，并跟随应用浅色/深色主题。

本地账户支持邮箱注册、常用邮箱后缀、密码规则提示、登录、记住邮箱、30 天自动登录、退出登录、昵称与多行简介、JPEG/PNG 头像选择。密码要求为本项目指定的 8–128 位及四类字符，采用 16 字节随机盐、PBKDF2-SHA256 600,000 次迭代，固定时间比较。自动登录保存随机令牌，退出时撤销；不保存明文密码。

云端选项显示明确的“云端模拟”说明。25 位激活码自动大写并分为五组，只校验格式。注册创建 `mock-...` 标识及本机占位文件；没有真实网络请求、邮箱验证或白名单校验。

本机注册记录中的激活码只允许使用一次，重复注册不会创建账户；注册过程使用跨进程互斥锁串行处理。真实白名单成员资格和跨设备单次兑换仍必须由未来服务器提供唯一约束与事务，不能把本地模拟当作服务器授权。

设备在线会话使用 Global Windows 命名互斥锁，同设备的另一程序实例不能同时登录。关闭程序或退出登录释放锁，进程异常退出由操作系统回收；换账号必须先退出。此约束适用于升级后的程序，不是防篡改或服务器在线状态。

登录后先展示头像、昵称、账户信息与简介；点击“修改个人信息”进入编辑，保存返回资料页，取消恢复原来的昵称/简介。更换头像仍是立即保存操作。所有密码框使用原生 Peek 眼睛，在密码输入焦点内提示 Caps Lock；注册确认密码不一致时即时显示红色提醒。

未登录时沿用原来的 `Native/state.json`。新账户从默认配置开始，登录时先保存旧工作区和专注记录，再加载新账户。退出登录恢复未登录工作区。原有未登录数据不会自动迁移或覆盖。

本地音乐目录的授权令牌也按账户生成；切换账户后，旧目录扫描或封面读取的异步结果不会写入新账户界面。迁移仍只复制设置中的引用，不复制音频文件。

## 文件与职责

| 层 | 实现 | 职责 |
| --- | --- | --- |
| View | `Views/AccountWindow.xaml` | Fluent 控件、两种来源的 Pivot、登录/注册表单、资料与迁移区域 |
| View 适配 | `Views/AccountWindow.xaml.cs` | AppWindow、窗口动画、PasswordBox 输入桥接、图片选择器、覆盖确认 ContentDialog |
| ViewModel | `Accounts/AccountViewModel.cs` | ObservableObject 属性、AsyncRelayCommand、校验反馈、忙碌状态、服务编排 |
| 账户 | `Accounts/AccountServices.cs` | IAccountService 本地实现、MockCloudAccountService、会话与迁移服务 |
| 存储 | `Accounts/UserDataStorageService.cs` | 独立目录、三个 JSON 和头像、目录交换与失败回滚 |
| 凭据 | `Accounts/WindowsCredentialStore.cs` | `DataProtectionProvider("LOCAL=user")` 加密凭据和会话 |
| 图片 | `Accounts/WindowsImageProcessingService.cs` | 检查格式/大小、EXIF 方向修正、居中裁剪、PNG 编码 |
| 主程序桥接 | `MainPage.Accounts.cs` | 导航入口、窗口实例、存储路由、账户切换时提交计时与刷新页面 |

ViewModel 不引用 WinUI 控件。通过 `IAccountInteractionService` 请求图片选择和确认弹窗，通过 `IAccountWorkspace` 切换工作区。接口集中在 `Accounts/Contracts.cs`，可以分别替换实现。

## 持久化结构

```text
%LOCALAPPDATA%/Tomatotodo/Native/
  state.json                         # 原有未登录数据
  Accounts/
    Security.dat                     # DPAPI 加密验证值、记住的邮箱、自动登录令牌
    Users/<账户 Guid>/
      UserProfile.json               # 身份、邮箱、类型、昵称、简介、激活码、模拟云标识
      AppSettings.json               # 设置、任务、仪表盘、专注日志等；不重复存课程表
      Timetable.json                 # CourseScheduleData，未导入为初始空课程表
      avatar.png                     # 始终存在，256×256；未选头像时生成默认头像
    Staging/                         # 写入暂存，位于用户目录之外
    Recovery/                        # 目录交换期间的恢复副本，成功后清理
```

用户目录由 Guid 生成，不使用邮箱或昵称作为路径。更新先写完整暂存目录，再交换目标目录；失败恢复原目录。构造存储服务时恢复中断的目录交换。原始头像不会留在用户目录中。

## 迁移语义与后续云端接口

当前登录账户为来源。目标列表只列出另一种类型的账户，必须输入目标账户密码。通过目标认证后，再显示“确认覆盖 / 取消”的警告，取消为默认按钮。仅确认后提交来源最新数据并执行模拟延迟，然后覆盖目标的设置、专注档案、任务、课程表、昵称、简介和头像。目标 Guid、邮箱、账户类型、激活码、云端标识及密码保持不变，以免复制身份或破坏后续登录。

未来接入后端时，用真实服务替换 `MockCloudAccountService : IAccountService` 与 `MockAccountMigrationService : IAccountMigrationService`。接口已包含 CancellationToken；需要由真实服务实现服务器身份校验、激活码/白名单验证、远端版本和覆盖事务。当前没有伪造的 API 地址或成功上传提示。

## 验证

```powershell
dotnet build Tomatotodo.Windows.csproj -c Debug -p:Platform=x64
dotnet run --project tests/Accounts.Tests.csproj -c Debug
dotnet run --project tests/CourseSchedule.Tests.csproj -c Debug
```

账户测试在唯一临时目录中使用 Windows 真正的 DPAPI 和图像编解码器，覆盖注册/重复/错误密码、自动登录与撤销、四文件约定、设置隔离、双向迁移、取消不修改目标、目标身份保持、ViewModel 流程，以及 256px 实际像素居中裁剪。测试结束移除自身临时目录，不使用产品账户或用户数据。

当前项目的账户及既有状态 JSON 仍使用反射序列化，因此 Debug / Release 均禁用裁剪，以保留模型元数据；后续整体改为源生成 JsonSerializerContext 后才可重新启用。DPAPI 防止直接读取凭据文件，但本地账户不是对同一 Windows 用户或管理员的操作系统级隔离边界；个人资料、课程表和设置 JSON 按需求保持明文可迁移。

参考：[MVVM Toolkit](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)、[Windows 数据保护](https://learn.microsoft.com/windows/uwp/security/data-protection)、[BitmapTransform](https://learn.microsoft.com/uwp/api/windows.graphics.imaging.bitmaptransform)。
