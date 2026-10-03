import 'package:tomatotodo/core/localization/ui_text.dart';
import 'package:flutter/material.dart';

import 'local_profile.dart';
import 'cloud_account.dart';
import 'cloud_account_ui.dart';

class ProfileAvatar extends StatelessWidget {
  const ProfileAvatar({super.key, required this.profile, this.radius = 19});
  final LocalProfile profile;
  final double radius;

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => CircleAvatar(
    radius: radius,
    backgroundColor: Theme.of(context).colorScheme.primaryContainer,
    foregroundColor: Theme.of(context).colorScheme.onPrimaryContainer,
    backgroundImage: profile.avatarBytes == null
        ? null
        : MemoryImage(profile.avatarBytes!),
    child: profile.avatarBytes == null
        ? Text(
            profile.name.characters.first,
            style: TextStyle(fontSize: radius * .9),
          )
        : null,
  ));
}

class AccountAvatarButton extends StatelessWidget {
  const AccountAvatarButton({
    super.key,
    required this.profile,
    required this.onPressed,
  });
  final LocalProfile profile;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => ListenableBuilder(
    listenable: profile,
    builder: (context, _) => IconButton(
      tooltip: UiText.t("账户"),
      onPressed: onPressed,
      icon: ProfileAvatar(profile: profile),
    ),
  ));
}

class AccountDrawer extends StatelessWidget {
  const AccountDrawer({super.key, required this.profile, this.cloud});
  final LocalProfile profile;
  final CloudAccount? cloud;

  Future<void> _editName(BuildContext context) async {
    final controller = TextEditingController(text: profile.name);
    final value = await showDialog<String>(
      context: context,
      builder: (dialog) => AlertDialog(
        title: Text(UiText.t("编辑个人资料")),
        content: TextField(
          controller: controller,
          autofocus: true,
          maxLength: 24,
          textInputAction: TextInputAction.done,
          decoration: InputDecoration(
            labelText: UiText.t("显示名称"),
            border: OutlineInputBorder(),
          ),
          onSubmitted: (text) => Navigator.pop(dialog, text),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialog),
            child: Text(UiText.t("取消")),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialog, controller.text),
            child: Text(UiText.t("保存")),
          ),
        ],
      ),
    );
    // The route transition may still reference the field briefly.
    WidgetsBinding.instance.addPostFrameCallback((_) => controller.dispose());
    if (value != null) await profile.rename(value);
  }

  Future<void> _editAvatar(BuildContext context) async {
    try {
      await profile.chooseAvatar(context);
    } on FormatException catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.message)));
      }
    } catch (_) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(UiText.t("头像读取失败，请重试"))));
      }
    }
  }

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => ListenableBuilder(
    listenable: Listenable.merge([profile, ?cloud]),
    builder: (context, _) {
      final colors = Theme.of(context).colorScheme;
      return Drawer(
        width: 340,
        child: SafeArea(
          child: ListView(
            padding: EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: [
              Row(
                children: [
                  Image.asset(
                    'assets/branding/tomatotodo-logo.png',
                    width: 32,
                    height: 32,
                  ),
                  SizedBox(width: 10),
                  Text(
                    'Tomatotodo',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Spacer(),
                  IconButton(
                    tooltip: UiText.t("关闭"),
                    onPressed: () => Navigator.pop(context),
                    icon: Icon(Icons.close),
                  ),
                ],
              ),
              SizedBox(height: 16),
              Card.filled(
                margin: EdgeInsets.zero,
                child: Padding(
                  padding: EdgeInsets.all(20),
                  child: Column(
                    children: [
                      ProfileAvatar(profile: profile, radius: 36),
                      SizedBox(height: 12),
                      Text(
                        profile.name,
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      SizedBox(height: 4),
                      Text(
                        cloud?.isCloud == true
                            ? cloud!.email
                            : UiText.t("本地账户 · 仅保存在此设备"),
                        style: Theme.of(context).textTheme.bodySmall
                            ?.copyWith(color: colors.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
              ),
              SizedBox(height: 20),
              Text(
                UiText.t("账户"),
                style: Theme.of(context).textTheme.labelLarge
                    ?.copyWith(color: colors.primary),
              ),
              SizedBox(height: 8),
              ListTile(
                selected: true,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(28),
                ),
                leading: ProfileAvatar(profile: profile, radius: 18),
                title: Text(profile.name),
                subtitle: Text(
                  cloud?.isCloud == true ? cloud!.status : UiText.t("当前使用 · 本地账户"),
                ),
                trailing: Icon(Icons.check),
              ),
              ListTile(
                enabled: cloud != null && !cloud!.busy,
                leading: Icon(Icons.cloud_outlined),
                title: Text(UiText.t("云端账户")),
                subtitle: Text(
                  cloud?.isCloud == true
                      ? UiText.t("退出后返回本地账户，未同步修改会保留")
                      : UiText.t("注册或登录，同步电脑与手机的数据"),
                ),
                onTap: cloud == null
                    ? null
                    : () async {
                        if (cloud!.isCloud) {
                          await cloud!.signOut();
                        } else {
                          await showCloudLogin(context, cloud!);
                        }
                      },
              ),
              Divider(height: 32),
              ListTile(
                leading: Icon(Icons.edit_outlined),
                title: Text(UiText.t("修改名称")),
                onTap: () => _editName(context),
              ),
              ListTile(
                leading: Icon(Icons.add_photo_alternate_outlined),
                title: Text(UiText.t("更换头像")),
                subtitle: Text(UiText.t("从设备选择图片")),
                onTap: () => _editAvatar(context),
              ),
            ],
          ),
        ),
      );
    },
  ));
}
