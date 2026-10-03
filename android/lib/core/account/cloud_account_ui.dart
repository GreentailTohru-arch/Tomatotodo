import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter/material.dart';

import 'cloud_account.dart';
import 'unified_user_data.dart';

Future<void> showCloudLogin(BuildContext context, CloudAccount cloud) =>
    showDialog<void>(
      context: context,
      builder: (_) => _CloudLogin(cloud: cloud),
    );

class _CloudLogin extends StatefulWidget {
  const _CloudLogin({required this.cloud});
  final CloudAccount cloud;
  @override
  State<_CloudLogin> createState() => _CloudLoginState();
}

class _CloudLoginState extends State<_CloudLogin> {
  final email = TextEditingController(),
      password = TextEditingController(),
      name = TextEditingController(),
      code = TextEditingController(),
      confirm = TextEditingController();
  bool register = false, busy = false, obscure = true;
  String? error;
  bool remember = false, loadingCredentials = true;
  static final vault = MethodChannel('com.tomatotodo/cloud_vault');
  @override
  void initState() {
    super.initState();
    restoreLogin();
  }

  Future<void> restoreLogin() async {
    try {
      final raw = await vault.invokeMethod<String>('readLogin');
      if (!mounted) return;
      if (raw != null) {
        final saved = jsonDecode(raw) as Map<String, dynamic>;
        email.text = saved['email'] as String;
        password.text = saved['password'] as String;
        remember = true;
      }
    } catch (_) {
      if (mounted) error = UiText.t("无法读取已记住的密码，请重新输入");
    } finally {
      if (mounted) setState(() => loadingCredentials = false);
    }
  }

  Future<void> setRemember(bool value) async {
    setState(() => remember = value);
    if (!value) {
      try {
        await vault.invokeMethod<void>('writeLogin', null);
      } catch (_) {
        if (mounted) setState(() => error = UiText.t("清除已记住的密码失败，请重试"));
      }
    }
  }

  @override
  void dispose() {
    for (final c in [email, password, name, code, confirm]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> submit() async {
    if (!email.text.contains('@') || password.text.isEmpty) {
      setState(() => error = UiText.t("请输入邮箱和密码"));
      return;
    }
    if (register &&
        (password.text != confirm.text ||
            name.text.trim().isEmpty ||
            code.text.replaceAll(RegExp(r'[^a-zA-Z0-9]'), '').length != 25)) {
      setState(() => error = UiText.t("请检查昵称、25 位激活码和两次密码"));
      return;
    }
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.cloud.signIn(
        email: email.text,
        password: password.text,
        nickname: register ? name.text : null,
        activationCode: register ? code.text.trim() : null,
      );
      if (!register) {
        try {
          await vault.invokeMethod<void>(
            'writeLogin',
            remember
                ? jsonEncode({
                    'email': email.text.trim(),
                    'password': password.text,
                  })
                : null,
          );
        } catch (_) {
          if (mounted) {
            ScaffoldMessenger.of(context)
                .showSnackBar(SnackBar(content: Text(UiText.t("已登录，但记住密码设置未能保存"))));
          }
        }
      }
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) {
        setState(
          () => error = e is CloudApiException ? e.message : UiText.t("连接失败，请检查网络后重试"),
        );
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => PopScope(
    canPop: !busy,
    child: AlertDialog(
      title: Text(register ? UiText.t("创建云端账户") : UiText.t("登录云端账户")),
      content: SizedBox(
        width: 360,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              SegmentedButton<bool>(
                segments: [
                  ButtonSegment(value: false, label: Text(UiText.t("登录"))),
                  ButtonSegment(value: true, label: Text(UiText.t("注册"))),
                ],
                selected: {register},
                onSelectionChanged: busy
                    ? null
                    : (v) => setState(() {
                        register = v.first;
                        error = null;
                      }),
              ),
              SizedBox(height: 20),
              TextField(
                controller: email,
                enabled: !busy && !loadingCredentials,
                keyboardType: TextInputType.emailAddress,
                decoration: InputDecoration(
                  labelText: UiText.t("邮箱"),
                  border: OutlineInputBorder(),
                ),
              ),
              if (register) ...[
                SizedBox(height: 12),
                TextField(
                  controller: name,
                  enabled: !busy && !loadingCredentials,
                  maxLength: 24,
                  decoration: InputDecoration(
                    labelText: UiText.t("昵称"),
                    border: OutlineInputBorder(),
                  ),
                ),
              ],
              SizedBox(height: 12),
              TextField(
                controller: password,
                enabled: !busy && !loadingCredentials,
                obscureText: obscure,
                decoration: InputDecoration(
                  labelText: UiText.t("密码"),
                  border: OutlineInputBorder(),
                  suffixIcon: IconButton(
                    tooltip: obscure ? UiText.t("显示密码") : UiText.t("隐藏密码"),
                    onPressed: () => setState(() => obscure = !obscure),
                    icon: Icon(
                      obscure
                          ? Icons.visibility_outlined
                          : Icons.visibility_off_outlined,
                    ),
                  ),
                ),
              ),
              if (!register)
                CheckboxListTile(
                  contentPadding: EdgeInsets.zero,
                  controlAffinity: ListTileControlAffinity.leading,
                  title: Text(UiText.t("记住密码")),
                  value: remember,
                  onChanged: busy || loadingCredentials
                      ? null
                      : (value) => setRemember(value ?? false),
                ),
              if (register) ...[
                SizedBox(height: 12),
                TextField(
                  controller: confirm,
                  enabled: !busy && !loadingCredentials,
                  obscureText: obscure,
                  decoration: InputDecoration(
                    labelText: UiText.t("确认密码"),
                    border: OutlineInputBorder(),
                  ),
                ),
                SizedBox(height: 12),
                TextField(
                  controller: code,
                  enabled: !busy && !loadingCredentials,
                  maxLength: 29,
                  decoration: InputDecoration(
                    labelText: UiText.t("激活码"),
                    border: OutlineInputBorder(),
                  ),
                ),
              ],
              if (error != null)
                Padding(
                  padding: EdgeInsets.only(top: 12),
                  child: Text(
                    error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
              if (busy)
                Padding(
                  padding: EdgeInsets.only(top: 16),
                  child: LinearProgressIndicator(),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: busy ? null : () => Navigator.pop(context),
          child: Text(UiText.t("取消")),
        ),
        FilledButton(
          onPressed: busy || loadingCredentials ? null : submit,
          child: Text(register ? UiText.t("注册并登录") : UiText.t("登录")),
        ),
      ],
    ),
  ));
}

Future<bool?> showSyncConflict(BuildContext context, List<JsonMap> conflicts) =>
    showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (dialog) {
        String label(String key) =>
            {
              'tasks': UiText.t("配置清单"),
              'timer': UiText.t("计时设置"),
              'timetable': UiText.t("课程表"),
              'countdown': UiText.t("倒数日"),
              'profile': UiText.t("个人资料"),
              'windows': UiText.t("桌面专属设置"),
              'mobile': UiText.t("手机专属设置"),
            }[key] ??
            key;
        String source(dynamic data) {
          final platform = data?['platform'] == 'windows'
              ? UiText.t("桌面版")
              : data?['platform'] == 'mobile'
              ? UiText.t("移动版")
              : UiText.t("另一设备");
          final date = DateTime.tryParse(data?['at'] ?? '')?.toLocal();
          return UiText.f("{0} · {1}", [platform, date?.toString().split('.').first ?? UiText.t("未知时间")]);
        }

        return AlertDialog(
          title: Text(UiText.t("选择同步数据")),
          content: SizedBox(
            width: 380,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(UiText.t("这些内容在两端都被修改。请选择冲突部分使用哪一份；专注日志会自动合并。")),
                  for (final c in conflicts)
                    Padding(
                      padding: EdgeInsets.only(top: 18),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            label(c['section']),
                            style: Theme.of(context).textTheme.titleSmall,
                          ),
                          Text(UiText.f("本机：{0}", [source(c['local'])])),
                          Text(UiText.f("云端：{0}", [source(c['remote'])])),
                        ],
                      ),
                    ),
                  Padding(
                    padding: EdgeInsets.only(top: 16),
                    child: Text(UiText.t("选择前的两份数据会保留为本机备份。")),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialog),
              child: Text(UiText.t("稍后决定")),
            ),
            TextButton(
              onPressed: () => Navigator.pop(dialog, false),
              child: Text(UiText.t("使用云端版本")),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(dialog, true),
              child: Text(UiText.t("保留本机版本")),
            ),
          ],
        );
      },
    );

Future<void> showDataMigration(BuildContext context, CloudAccount cloud) async {
  if (!cloud.isCloud) {
    await showCloudLogin(context, cloud);
    if (!context.mounted || !cloud.isCloud) return;
  }
  final direction = await showDialog<bool>(
    context: context,
    builder: (dialog) => AlertDialog(
      title: Text(UiText.t("本地与云端数据迁移")),
      content: Text(UiText.t("选择迁移方向。目标数据会先在本机备份；本地上传到云端时，清单和设置使用本地版本，专注日志合并保留。")),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(dialog),
          child: Text(UiText.t("取消")),
        ),
        TextButton(
          onPressed: () => Navigator.pop(dialog, false),
          child: Text(UiText.t("云端复制到本地")),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(dialog, true),
          child: Text(UiText.t("本地迁移到云端")),
        ),
      ],
    ),
  );
  if (direction == null) return;
  try {
    if (direction) {
      await cloud.migrateLocalToCloud();
    } else {
      await cloud.copyCloudToLocal();
    }
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(direction ? cloud.status : UiText.t("已复制到本地账户，退出云端后可查看"))),
      );
    }
  } catch (e) {
    if (context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(UiText.f("迁移未完成：{0}", [e]))));
    }
  }
}
