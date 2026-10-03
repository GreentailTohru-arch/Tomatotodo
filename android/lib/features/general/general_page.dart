import 'package:tomatotodo/core/localization/ui_text.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/account/account_drawer.dart';
import '../../core/account/local_profile.dart';
import '../../core/account/cloud_account.dart';
import '../../core/account/cloud_account_ui.dart';
import '../../core/account/unified_user_data.dart';

import '../../core/settings/app_settings.dart';
import '../../core/settings/app_language.dart';
import '../../core/updates/app_updates.dart';
import '../../core/updates/version_announcements.dart';
import '../../core/home_widgets/home_widgets_bridge.dart';
import '../dashboard/dashboard_controller.dart';
import '../personalization/personalization_page.dart';
import 'course_schedule.dart';
import 'data_portability.dart';
import 'general_state.dart';
import 'timer_sound_page.dart';
import 'about_credits.dart';

enum _GeneralCategory {
  common,
  dashboard,
  course,
  personalization,
  account,
  about,
}

extension on _GeneralCategory {
  String get label => switch (this) {
    _GeneralCategory.about => UiText.t("关于与更新"),
    _GeneralCategory.common => UiText.t("通用设置"),
    _GeneralCategory.dashboard => UiText.t("仪表盘"),
    _GeneralCategory.course => UiText.t("课程表"),
    _GeneralCategory.personalization => UiText.t("个性化"),
    _GeneralCategory.account => UiText.t("账户与用户数据"),
  };

  String get subtitle => switch (this) {
    _GeneralCategory.about => UiText.t("App 版本、检查更新、当前公告"),
    _GeneralCategory.common => UiText.t("计时节奏、通知音效、系统消息窗常驻、桌面小组件"),
    _GeneralCategory.dashboard => UiText.t("圆表、进度、倒数日、课程提醒"),
    _GeneralCategory.course => UiText.t("课表入口、课程表导入与导出"),
    _GeneralCategory.personalization => UiText.t("外观模式、主题色、字体缩放"),
    _GeneralCategory.account => UiText.t("本地账户、数据备份与恢复"),
  };

  String get keywords => switch (this) {
    _GeneralCategory.about => UiText.t("软件版本 下载 更新 升级 跳过 当前公告"),
    _GeneralCategory.common => UiText.t("专注 短休 系统消息窗常驻"),
    _GeneralCategory.dashboard => UiText.t("显示计时刻度 进度指示器 平滑 波浪"),
    _GeneralCategory.course => UiText.t("课表开关 导入课程表 导出课程表"),
    _GeneralCategory.personalization => UiText.t("浅色 深色 自动 主题色彩"),
    _GeneralCategory.account => UiText.t("退出登录 切换账户 云端 迁移 上传 导出用户数据 导入用户数据 恢复出厂数据"),
  };

  IconData get icon => switch (this) {
    _GeneralCategory.about => Icons.info_outline,
    _GeneralCategory.common => Icons.tune_outlined,
    _GeneralCategory.dashboard => Icons.dashboard_outlined,
    _GeneralCategory.course => Icons.calendar_view_week_outlined,
    _GeneralCategory.personalization => Icons.palette_outlined,
    _GeneralCategory.account => Icons.manage_accounts_outlined,
  };
}

class GeneralPage extends StatelessWidget {
  const GeneralPage({
    super.key,
    required this.settings,
    required this.dashboard,
    required this.general,
    this.profile,
    this.onAccountPressed,
    this.cloud,
    this.updates,
    this.announcements,
  });
  final AppSettings settings;
  final DashboardController dashboard;
  final GeneralState general;
  final LocalProfile? profile;
  final VoidCallback? onAccountPressed;
  final CloudAccount? cloud;
  final AppUpdates? updates;
  final VersionAnnouncements? announcements;
  void _message(BuildContext context, String text) {
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));
    }
  }

  Future<bool> _confirm(
    BuildContext context,
    String title,
    String description,
    String action,
  ) async =>
      await showDialog<bool>(
        context: context,
        builder: (dialog) => AlertDialog(
          title: Text(title),
          content: Text(description),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialog, false),
              child: Text(UiText.t("取消")),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(dialog, true),
              child: Text(action),
            ),
          ],
        ),
      ) ??
      false;
  @override
  Widget build(BuildContext context) => UiText.watch(context, () => LayoutBuilder(
    builder: (context, box) =>
        box.maxWidth >= 840 ? _TabletSettings(page: this) : _compact(context),
  ));

  Widget _compact(BuildContext context) => Material(
    color: Theme.of(context).colorScheme.surfaceContainerLow,
    child: CustomScrollView(
      key: PageStorageKey('general-settings-scroll'),
      slivers: [
        SliverToBoxAdapter(
          child: SafeArea(
            bottom: false,
            child: Center(
              child: ConstrainedBox(
                constraints: BoxConstraints(maxWidth: 720),
                child: Padding(
                  padding: EdgeInsets.fromLTRB(20, 24, 20, 32),
                  child: Column(
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              UiText.t("常规"),
                              style: Theme.of(context).textTheme.headlineMedium,
                            ),
                          ),
                          if (profile != null && onAccountPressed != null)
                            AccountAvatarButton(
                              profile: profile!,
                              onPressed: onAccountPressed!,
                            ),
                        ],
                      ),
                      SizedBox(height: 20),
                      _GeneralSettingsSearch(
                        settings: settings,
                        onCategorySelected: (item) =>
                            _openCategory(context, item),
                      ),
                      SizedBox(height: 24),
                      for (final item in _GeneralCategory.values)
                        ListTile(
                          contentPadding: EdgeInsets.symmetric(
                            horizontal: 20,
                            vertical: 8,
                          ),
                          leading: Icon(item.icon, size: 26),
                          title: Text(
                            item.label,
                            style: Theme.of(context).textTheme.titleMedium
                                ?.copyWith(fontWeight: FontWeight.w400),
                          ),
                          subtitle: Text(item.subtitle),
                          onTap: () => _openCategory(context, item),
                        ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    ),
  );

  void _openCategory(BuildContext context, _GeneralCategory category) {
    if (category == _GeneralCategory.personalization) {
      Navigator.push(
        context,
        MaterialPageRoute<void>(
          builder: (_) => PersonalizationPage(settings: settings),
        ),
      );
      return;
    }
    Navigator.push(
      context,
      MaterialPageRoute<void>(
        builder: (_) => AnimatedBuilder(
          animation: Listenable.merge([general, dashboard, ?cloud, ?updates]),
          builder: (pageContext, _) => Scaffold(
            backgroundColor: Theme.of(pageContext)
                .colorScheme
                .surfaceContainerLow,
            appBar: AppBar(title: Text(category.label)),
            body: Center(
              child: ConstrainedBox(
                constraints: BoxConstraints(maxWidth: 720),
                child: ListView(
                  padding: EdgeInsets.fromLTRB(16, 8, 16, 32),
                  children: _categoryItems(pageContext, category),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _sectionLabel(BuildContext context, String label) => Padding(
    padding: EdgeInsets.fromLTRB(16, 22, 16, 6),
    child: Text(
      label,
      style: Theme.of(context).textTheme.titleSmall
          ?.copyWith(color: Theme.of(context).colorScheme.primary),
    ),
  );

  List<Widget> _categoryItems(
    BuildContext context,
    _GeneralCategory category, {
    VoidCallback? onTimerSound,
  }) => switch (category) {
    _GeneralCategory.about => [
      ListTile(
        leading: Icon(Icons.apps_outlined),
        title: Text('Tomatotodo'),
        subtitle: Text(UiText.f("App 版本 {0}", [updates?.versionLabel ?? UiText.t("暂不可用")])),
        trailing: Icon(Icons.chevron_right),
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute<void>(
            builder: (_) => Scaffold(
              appBar: AppBar(title: Text('Tomatotodo')),
              body: SafeArea(
                child: SingleChildScrollView(child: AboutCredits()),
              ),
            ),
          ),
        ),
      ),
      ListTile(
        leading: Icon(Icons.system_update_outlined),
        title: Text(updates?.checking == true ? UiText.t("正在检查更新…") : UiText.t("检查更新")),
        subtitle: Text(UiText.t("手动检查也会显示已跳过的版本")),
        trailing: updates?.checking == true
            ? SizedBox.square(
                dimension: 24,
                child: CircularProgressIndicator(strokeWidth: 2),
              )
            : Icon(Icons.chevron_right),
        onTap: updates == null || updates!.checking || !AppUpdates.supported
            ? null
            : () => updates!.check(context, manual: true),
      ),
      ListTile(
        leading: Icon(Icons.campaign_outlined),
        title: Text(UiText.t("当前公告")),
        subtitle: Text(UiText.t("查看当前版本公告，包含已读公告")),
        trailing: Icon(Icons.chevron_right),
        onTap: announcements == null || updates == null
            ? null
            : () async {
                try {
                  if (updates!.version == null) await updates!.loadVersion();
                  if (context.mounted) {
                    await announcements!.showStartup(
                      context,
                      updates!.version!,
                      manual: true,
                    );
                  }
                } catch (_) {
                  if (context.mounted) _message(context, UiText.t("无法读取版本，请稍后重试"));
                }
              },
      ),
    ],
    _GeneralCategory.common => [
      ListTile(
        leading: Icon(Icons.language_outlined),
        title: Text(AppLanguage.title(Localizations.localeOf(context))),
        subtitle: Text(AppLanguage.label(Localizations.localeOf(context), settings.language)),
        onTap: () async {
          final locale = Localizations.localeOf(context);
          final selected = await showDialog<String>(
            context: context,
            builder: (dialog) => SimpleDialog(
              title: Text(AppLanguage.title(locale)),
              children: [
                for (final code in AppLanguage.codes)
                  ListTile(
                    title: Text(AppLanguage.label(locale, code)),
                    trailing: settings.language == code ? Icon(Icons.check) : null,
                    onTap: () => Navigator.pop(dialog, code),
                  ),
              ],
            ),
          );
          if (selected != null) await settings.setLanguage(selected);
        },
      ),
      ListTile(
        leading: Icon(Icons.timer_outlined),
        title: Text(UiText.t("计时节奏")),
        subtitle: Text(
          UiText.f("专注 {0} 分钟 · 短休 {1} 分钟", [dashboard.focusMinutes, dashboard.shortMinutes]),
        ),
        onTap: () => _rhythm(context),
      ),
      SwitchListTile(
        secondary: Icon(Icons.free_breakfast_outlined),
        title: Text(UiText.t("启用短休")),
        subtitle: Text(UiText.t("完成专注后切换到短休，是否自动开始由循环方式决定")),
        value: dashboard.shortBreakEnabled,
        onChanged: dashboard.setShortBreakEnabled,
      ),
      ListTile(
        leading: Icon(Icons.repeat_rounded),
        title: Text(UiText.t("循环计时")),
        subtitle: Text(
          UiText.t("手动：每阶段结束后等待开始。自动：连续运行，任务番茄达标后完成短休再停止。关闭短休则连续专注；正向计时不参与循环。"),
        ),
        trailing: Text(dashboard.automaticCycle ? UiText.t("自动") : UiText.t("手动")),
        onTap: () async {
          final value = await showDialog<bool>(
            context: context,
            builder: (dialog) => SimpleDialog(
              title: Text(UiText.t("循环计时")),
              children: [
                for (final auto in [false, true])
                  ListTile(
                    title: Text(auto ? UiText.t("自动循环计时") : UiText.t("手动循环计时")),
                    trailing: dashboard.automaticCycle == auto
                        ? Icon(Icons.check)
                        : null,
                    onTap: () => Navigator.pop(dialog, auto),
                  ),
              ],
            ),
          );
          if (value != null) dashboard.setAutomaticCycle(value);
        },
      ),
      SwitchListTile(
        secondary: Icon(Icons.arrow_upward_rounded),
        title: Text(UiText.t("正向计时")),
        subtitle: Text(UiText.t("从 00:00 向上累计专注时间，不自动结束或奖励番茄")),
        value: dashboard.countUp,
        onChanged: dashboard.setCountUp,
      ),
      if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android)
        ListTile(
          leading: Icon(Icons.music_note_outlined),
          title: Text(UiText.t("番茄时钟通知音效")),
          subtitle: Text(UiText.t("专注与短休结束 · 系统、自定义和试听")),
          onTap:
              onTimerSound ??
              () => Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) =>
                      TimerSoundPage(preferences: general.preferences),
                ),
              ),
        ),
      SwitchListTile(
        secondary: Icon(Icons.notifications_active_outlined),
        title: Text(UiText.t("系统消息窗常驻")),
        subtitle: Text(
          defaultTargetPlatform == TargetPlatform.iOS
              ? UiText.t("iOS 不支持通知常驻")
              : UiText.t("在通知栏保留 Tomatotodo 入口"),
        ),
        value: general.pinnedMessage,
        onChanged: defaultTargetPlatform == TargetPlatform.iOS
            ? null
            : general.setPinnedMessage,
      ),
      if (general.notificationIssue != null)
        ListTile(
          leading: Icon(Icons.info_outline),
          title: Text(general.notificationIssue!),
        ),
      if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android)
        ListTile(
          leading: Icon(Icons.widgets_outlined),
          title: Text(UiText.t("桌面小组件")),
          subtitle: Text(UiText.t("计时、倒数日、名言、清单与课程")),
          onTap: () => _homeWidgets(context),
        ),
    ],
    _GeneralCategory.dashboard => [
      _sectionLabel(context, UiText.t("圆表设置")),
      SwitchListTile(
        secondary: Icon(Icons.watch_later_outlined),
        title: Text(UiText.t("显示计时刻度")),
        subtitle: Text(UiText.t("在大计时器圆环内显示刻度")),
        value: general.timerDialTicks,
        onChanged: general.setTimerDialTicks,
      ),
      _sectionLabel(context, UiText.t("进度指示器")),
      _progressChoice(
        context,
        UiText.t("进度变化"),
        ButtonSegment(value: false, label: Text(UiText.t("卡点式"))),
        ButtonSegment(value: true, label: Text(UiText.t("平滑式"))),
        general.progressSmooth,
        (value) => general.setProgressStyle(smooth: value),
      ),
      _progressChoice(
        context,
        UiText.t("线条粗细"),
        ButtonSegment(value: false, label: Text(UiText.t("细线"))),
        ButtonSegment(value: true, label: Text(UiText.t("粗线"))),
        general.progressThick,
        (value) => general.setProgressStyle(thick: value),
      ),
      _progressChoice(
        context,
        UiText.t("线条形态"),
        ButtonSegment(value: false, label: Text(UiText.t("平直"))),
        ButtonSegment(value: true, label: Text(UiText.t("波浪"))),
        general.progressWavy,
        (value) => general.setProgressStyle(wavy: value),
      ),
      _sectionLabel(context, UiText.t("倒数日与课程")),
      ListTile(
        leading: Icon(Icons.event_outlined),
        title: Text(UiText.t("倒数日设置")),
        subtitle: Text(
          dashboard.countdownDate == null
              ? UiText.t("还没有目标日期")
              : '${dashboard.countdownName} · ${courseDate(dashboard.countdownDate!)}',
        ),
        onTap: () => _countdown(context),
      ),
      ListTile(
        leading: Icon(Icons.alarm_outlined),
        title: Text(UiText.t("课程提醒")),
        subtitle: Text(
          general.reminderEnabled ? UiText.f("提前 {0} 分钟", [general.leadMinutes]) : UiText.t("已关闭"),
        ),
        onTap: () => _reminders(context),
      ),
    ],
    _GeneralCategory.course => [
      SwitchListTile(
        secondary: Icon(Icons.calendar_view_week_outlined),
        title: Text(UiText.t("课表开关")),
        subtitle: Text(UiText.t("只控制底栏入口，不影响课程组件与提醒")),
        value: general.courseEnabled,
        onChanged: general.setCourseEnabled,
      ),
      _sectionLabel(context, UiText.t("课程表导入与导出")),
      ListTile(
        leading: Icon(Icons.file_upload_outlined),
        title: Text(UiText.t("导入课程表")),
        subtitle: Text(UiText.t("读取 tomatotodo-course-schedule JSON")),
        onTap: () => _importSchedule(context),
      ),
      ListTile(
        leading: Icon(Icons.file_download_outlined),
        title: Text(UiText.t("导出课程表")),
        onTap: () => _exportSchedule(context, false),
      ),
      ListTile(
        leading: Icon(Icons.description_outlined),
        title: Text(UiText.t("导出空白模板")),
        onTap: () => _exportSchedule(context, true),
      ),
      ListTile(
        leading: Icon(Icons.content_copy_outlined),
        title: Text(UiText.t("复制 AI 整理提示词")),
        onTap: () async {
          await Clipboard.setData(
            ClipboardData(
              text: UiText.t("请查看桌面课表的全部可查询日期和周次，收集每门课程的周一日期、星期、节次、24 小时制开始与结束时间、教室及教师。严格按 Tomatotodo 导出的课程表 JSON 模板返回纯 JSON，不要 Markdown 或说明。day 为 0 到 6，对应周一到周日；没有课程的周保留空 events。"),
            ),
          );
          if (context.mounted) _message(context, UiText.t("提示词已复制"));
        },
      ),
    ],
    _GeneralCategory.account => [
      ListTile(
        leading: Icon(Icons.switch_account_outlined),
        title: Text(UiText.t("退出登录与切换账户")),
        subtitle: Text(
          cloud?.isCloud == true
              ? '${cloud!.email}\n${cloud!.status}'
              : UiText.t("本地账户 · 可注册或登录云端账户"),
        ),
        onTap: onAccountPressed,
      ),
      ListTile(
        enabled: cloud != null && !cloud!.busy,
        leading: Icon(Icons.cloud_sync_outlined),
        title: Text(UiText.t("本地与云端数据迁移")),
        subtitle: Text(UiText.t("选择迁移方向，保留迁移前备份")),
        onTap: cloud == null ? null : () => showDataMigration(context, cloud!),
      ),
      ListTile(
        enabled: cloud?.isCloud == true && !cloud!.busy,
        leading: Icon(Icons.cloud_upload_outlined),
        title: Text(UiText.t("手动上传云端数据")),
        subtitle: Text(cloud?.isCloud == true ? cloud!.status : UiText.t("登录云端账户后可用")),
        onTap: () async {
          try {
            await cloud?.synchronize(manual: true);
            if (context.mounted) _message(context, cloud!.status);
          } catch (error) {
            if (context.mounted) _message(context, UiText.f("同步未完成：{0}", [error]));
          }
        },
      ),
      _sectionLabel(context, UiText.t("用户数据导入与导出")),
      ListTile(
        leading: Icon(Icons.backup_outlined),
        title: Text(UiText.t("导出用户数据")),
        subtitle: Text(UiText.t("任务、档案、仪表盘和常规设置")),
        onTap: () => _exportUser(context),
      ),
      ListTile(
        leading: Icon(Icons.settings_backup_restore_outlined),
        title: Text(UiText.t("导入用户数据")),
        subtitle: Text(UiText.t("验证后覆盖当前本机数据")),
        onTap: () => _importUser(context),
      ),
      _sectionLabel(context, UiText.t("重置")),
      ListTile(
        leading: Icon(Icons.delete_forever_outlined),
        title: Text(UiText.t("恢复出厂数据")),
        subtitle: Text(UiText.t("清除任务、专注日志和本地设置")),
        onTap: () => _factoryReset(context),
      ),
    ],
    _GeneralCategory.personalization => [],
  };

  Widget _progressChoice(
    BuildContext context,
    String label,
    ButtonSegment<bool> first,
    ButtonSegment<bool> second,
    bool selected,
    ValueChanged<bool> onChanged,
  ) => Padding(
    padding: EdgeInsets.fromLTRB(16, 4, 16, 16),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: Theme.of(context).textTheme.bodyMedium),
        SizedBox(height: 8),
        SegmentedButton<bool>(
          segments: [first, second],
          selected: {selected},
          onSelectionChanged: (values) => onChanged(values.first),
        ),
      ],
    ),
  );

  void _homeWidgets(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (sheet) => SafeArea(
        child: FractionallySizedBox(
          heightFactor: .68,
          child: ListView(
            children: [
              for (final item in [
                (
                  'timer',
                  UiText.t("计时器"),
                  UiText.t("2×2 · 3×2 · 4×2，可直接开始和暂停"),
                  Icons.timer_outlined,
                ),
                ('countdown', UiText.t("倒数日"), '2×1 · 2×2', Icons.event_outlined),
                ('quote', UiText.t("名言警句"), UiText.t("4×2，支持离线显示"), Icons.format_quote),
                ('tasks', UiText.t("任务清单"), '2×2 · 3×2 · 4×2', Icons.checklist),
                ('courses', UiText.t("今日课程"), '2×2 · 3×2 · 4×2', Icons.school_outlined),
              ])
                ListTile(
                  leading: Icon(item.$4),
                  title: Text(item.$2),
                  subtitle: Text(item.$3),
                  onTap: () async {
                    Navigator.pop(sheet);
                    try {
                      final added = await HomeWidgetsBridge.pin(item.$1);
                      if (!added && context.mounted) {
                        _message(context, UiText.t("请长按手机桌面，在小组件列表中选择 Tomatotodo"));
                      }
                    } on PlatformException {
                      if (context.mounted) _message(context, UiText.t("请从手机桌面的小组件列表添加"));
                    }
                  },
                ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _rhythm(BuildContext context) async {
    final focus = TextEditingController(
      text: dashboard.focusMinutes.toString(),
    );
    final short = TextEditingController(
      text: dashboard.shortMinutes.toString(),
    );
    var enabled = dashboard.shortBreakEnabled;
    final result = await showDialog<(int, int, bool)>(
      context: context,
      builder: (dialog) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: Text(UiText.t("计时节奏")),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: focus,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  labelText: UiText.t("专注分钟数（1–180）"),
                  border: OutlineInputBorder(),
                ),
              ),
              SizedBox(height: 12),
              TextField(
                controller: short,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  labelText: UiText.t("短休分钟数（1–60）"),
                  border: OutlineInputBorder(),
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialog),
              child: Text(UiText.t("取消")),
            ),
            FilledButton(
              onPressed: () {
                final a = int.tryParse(focus.text),
                    b = int.tryParse(short.text);
                if (a == null ||
                    a < 1 ||
                    a > 180 ||
                    b == null ||
                    b < 1 ||
                    b > 60) {
                  _message(context, UiText.t("请输入有效的分钟数"));
                  return;
                }
                Navigator.pop(dialog, (a, b, enabled));
              },
              child: Text(UiText.t("保存")),
            ),
          ],
        ),
      ),
    );
    focus.dispose();
    short.dispose();
    if (result != null) {
      dashboard.setDurations(
        focus: result.$1,
        shortBreak: result.$2,
        enableShortBreak: result.$3,
      );
    }
  }

  Future<void> _countdown(BuildContext context) async {
    final name = TextEditingController(text: dashboard.countdownName);
    var date = dashboard.countdownDate;
    final result = await showDialog<(String, DateTime)?>(
      context: context,
      builder: (dialog) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: Text(UiText.t("倒数日设置")),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: name,
                maxLength: 40,
                decoration: InputDecoration(
                  labelText: UiText.t("目标名称"),
                  border: OutlineInputBorder(),
                ),
              ),
              OutlinedButton.icon(
                onPressed: () async {
                  final picked = await showDatePicker(
                    context: context,
                    firstDate: DateTime(2000),
                    lastDate: DateTime(2100),
                    initialDate: date ?? DateTime.now(),
                  );
                  if (picked != null) setDialog(() => date = picked);
                },
                icon: Icon(Icons.event),
                label: Text(date == null ? UiText.t("选择日期") : courseDate(date!)),
              ),
            ],
          ),
          actions: [
            if (dashboard.countdownDate != null)
              TextButton(
                onPressed: () {
                  dashboard.clearCountdown();
                  Navigator.pop(dialog);
                },
                child: Text(UiText.t("清除")),
              ),
            TextButton(
              onPressed: () => Navigator.pop(dialog),
              child: Text(UiText.t("取消")),
            ),
            FilledButton(
              onPressed: () {
                if (name.text.trim().isEmpty || date == null) {
                  _message(context, UiText.t("请输入名称并选择日期"));
                  return;
                }
                Navigator.pop(dialog, (name.text.trim(), date!));
              },
              child: Text(UiText.t("保存")),
            ),
          ],
        ),
      ),
    );
    name.dispose();
    if (result != null) dashboard.setCountdown(result.$1, result.$2);
  }

  Future<void> _reminders(BuildContext context) async {
    final field = TextEditingController(text: general.leadMinutes.toString());
    var enabled = general.reminderEnabled,
        system = general.reminderSystem,
        inApp = general.reminderInApp,
        sound = general.reminderSound,
        vibration = general.reminderVibration;
    final result = await showDialog<(bool, int, bool, bool, bool, bool)?>(
      context: context,
      builder: (dialog) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: Text(UiText.t("课程提醒")),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(UiText.t("启用课程提醒")),
                  value: enabled,
                  onChanged: (value) => setDialog(() => enabled = value),
                ),
                TextField(
                  controller: field,
                  keyboardType: TextInputType.number,
                  decoration: InputDecoration(
                    labelText: UiText.t("提前分钟数（0–120）"),
                    border: OutlineInputBorder(),
                  ),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(UiText.t("系统弹窗")),
                  value: system,
                  onChanged: (value) => setDialog(() => system = value),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(UiText.t("软件消息")),
                  value: inApp,
                  onChanged: (value) => setDialog(() => inApp = value),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(UiText.t("音效")),
                  value: sound,
                  onChanged: (value) => setDialog(() => sound = value),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(UiText.t("震动")),
                  value: vibration,
                  onChanged: (value) => setDialog(() => vibration = value),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialog),
              child: Text(UiText.t("取消")),
            ),
            FilledButton(
              onPressed: () {
                final lead = int.tryParse(field.text);
                if (lead == null || lead < 0 || lead > 120) {
                  _message(context, UiText.t("提前量需为 0–120 分钟"));
                  return;
                }
                Navigator.pop(dialog, (
                  enabled,
                  lead,
                  system,
                  inApp,
                  sound,
                  vibration,
                ));
              },
              child: Text(UiText.t("保存")),
            ),
          ],
        ),
      ),
    );
    field.dispose();
    if (result != null) {
      general.setReminder(
        enabled: result.$1,
        lead: result.$2,
        system: result.$3,
        inApp: result.$4,
        sound: result.$5,
        vibration: result.$6,
      );
    }
  }

  Future<void> _importSchedule(BuildContext context) async {
    try {
      final raw = await pickJsonFile();
      if (raw == null) return;
      final schedule = CourseSchedule.fromJson(raw);
      final count = schedule.weeks.fold<int>(
        0,
        (sum, week) => sum + week.events.length,
      );
      if (!context.mounted) return;
      final yes = await _confirm(
        context,
        UiText.t("导入课程表？"),
        UiText.f("将导入 {0} 周、{1} 门课程，并覆盖当前课表。", [schedule.weeks.length, count]),
        UiText.t("导入"),
      );
      if (yes) {
        general.setSchedule(schedule);
        if (context.mounted) _message(context, UiText.t("课程表已导入"));
      }
    } catch (error) {
      if (context.mounted) _message(context, UiText.f("导入失败：{0}", [error]));
    }
  }

  Future<void> _exportSchedule(BuildContext context, bool template) async {
    try {
      final data = template
          ? general.schedule.template()
          : {
              ...general.schedule.toJson(),
              'app': 'Tomatotodo',
              'exportedAt': DateTime.now().toIso8601String(),
            };
      final saved = await saveJsonFile(
        template
            ? 'Tomatotodo-course-template.json'
            : 'Tomatotodo-course-schedule.json',
        data,
      );
      if (saved && context.mounted) _message(context, UiText.t("JSON 已导出"));
    } catch (error) {
      if (context.mounted) _message(context, UiText.f("导出失败：{0}", [error]));
    }
  }

  Future<void> _exportUser(BuildContext context) async {
    try {
      final data =
          cloud?.capture() ??
          UnifiedUserData.capture(
            dashboard: dashboard.exportData(),
            general: general.exportData(),
            appearance: settings.exportData(),
            profile:
                profile?.exportData() ??
                {'Nickname': UiText.t("本地用户"), 'Biography': '', 'AvatarBase64': null},
          );
      final saved = await saveJsonFile(
        'Tomatotodo-backup-${courseDate(DateTime.now())}.json',
        data,
      );
      if (saved && context.mounted) _message(context, UiText.t("用户数据已导出"));
    } catch (error) {
      if (context.mounted) _message(context, UiText.f("导出失败：{0}", [error]));
    }
  }

  Future<void> _importUser(BuildContext context) async {
    try {
      final raw = await pickJsonFile();
      if (raw == null) return;
      if (raw is Map &&
          raw['format'] == 'tomatotodo-user-data' &&
          raw['version'] == 2) {
        final document = Map<String, dynamic>.from(raw);
        UnifiedUserData.validate(document);
        if (!context.mounted ||
            !await _confirm(
              context,
              UiText.t("覆盖当前用户数据？"),
              UiText.t("将导入共用的任务、课程表和档案，并保留文件中的电脑与手机专属设置。登录云端时，此修改也会同步。"),
              UiText.t("覆盖导入"),
            )) {
          return;
        }
        if (cloud == null) throw StateError(UiText.t("账户数据服务未初始化"));
        await cloud!.importDocument(document);
        await cloud!.synchronize();
        if (context.mounted) _message(context, UiText.t("跨平台用户数据已导入"));
        return;
      }
      if (raw is! Map ||
          raw['format'] != 'tomatotodo-user-data' ||
          raw['version'] != 1 ||
          raw['platform'] != 'flutter' ||
          raw['data'] is! Map) {
        throw FormatException(UiText.t("仅支持 Tomatotodo Flutter 用户数据 v1"));
      }
      final data = Map<String, dynamic>.from(raw['data'] as Map);
      if (data['dashboard'] is! Map ||
          data['general'] is! Map ||
          data['personalization'] is! Map) {
        throw FormatException(UiText.t("备份缺少必要数据"));
      }
      final dashboardData = Map<String, dynamic>.from(data['dashboard'] as Map);
      final generalData = Map<String, dynamic>.from(data['general'] as Map);
      final appearanceData = Map<String, dynamic>.from(
        data['personalization'] as Map,
      );
      DashboardController.validateExportData(dashboardData);
      GeneralState.validateExportData(generalData);
      AppSettings.validateExportData(appearanceData);
      if (!context.mounted) return;
      final yes = await _confirm(
        context,
        UiText.t("覆盖当前用户数据？"),
        UiText.t("导入将替换本机任务、档案、仪表盘、课程表和设置。请先导出备份。"),
        UiText.t("覆盖导入"),
      );
      if (!yes) return;
      await dashboard.replaceData(dashboardData);
      await general.replaceData(generalData);
      await settings.replaceData(appearanceData);
      if (context.mounted) _message(context, UiText.t("用户数据已导入"));
    } catch (error) {
      if (context.mounted) _message(context, UiText.f("导入失败：{0}", [error]));
    }
  }

  Future<void> _factoryReset(BuildContext context) async {
    if (!await _confirm(context, UiText.t("恢复出厂数据？"), UiText.t("任务、专注日志、课程表和设置将被清除。"), UiText.t("继续"))) {
      return;
    }
    if (!context.mounted ||
        !await _confirm(context, UiText.t("最后确认"), UiText.t("此操作无法撤销。建议先导出用户数据。"), UiText.t("清除并恢复"))) {
      return;
    }
    await dashboard.factoryReset();
    await general.factoryReset();
    await settings.factoryReset();
    if (context.mounted) _message(context, UiText.t("已恢复出厂数据"));
  }
}

class _GeneralSettingsSearch extends StatefulWidget {
  const _GeneralSettingsSearch({
    required this.settings,
    required this.onCategorySelected,
  });

  final AppSettings settings;
  final ValueChanged<_GeneralCategory> onCategorySelected;

  @override
  State<_GeneralSettingsSearch> createState() => _GeneralSettingsSearchState();
}

class _GeneralSettingsSearchState extends State<_GeneralSettingsSearch> {
  static final _historyKey = 'generalSearchHistory';
  static List<(String, _GeneralCategory)> get _suggested => [
    (UiText.t("计时节奏"), _GeneralCategory.common),
    (UiText.t("课程提醒"), _GeneralCategory.dashboard),
    (UiText.t("主题色"), _GeneralCategory.personalization),
  ];

  final SearchController _controller = SearchController();
  late final ValueNotifier<List<String>> _history;

  @override
  void initState() {
    super.initState();
    _history = ValueNotifier(
      widget.settings.preferences.getStringList(_historyKey) ?? [],
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    _history.dispose();
    super.dispose();
  }

  void _select(_GeneralCategory category, String term) {
    final value = term.trim();
    if (value.isNotEmpty) {
      _history.value = [
        value,
        ..._history.value.where((item) => item != value),
      ].take(5).toList();
      widget.settings.preferences.setStringList(_historyKey, _history.value);
    }
    _controller.closeView('');
    widget.onCategorySelected(category);
  }

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => SearchAnchor.bar(
    searchController: _controller,
    barHintText: UiText.t("搜索设置"),
    viewHintText: UiText.t("搜索设置"),
    barLeading: Icon(Icons.search),
    barElevation: WidgetStatePropertyAll(0),
    barBackgroundColor: WidgetStatePropertyAll(
      Theme.of(context).colorScheme.surfaceContainerLowest,
    ),
    suggestionsBuilder: (searchContext, controller) {
      final query = controller.text.trim().toLowerCase();
      if (query.isNotEmpty) {
        final matches = _GeneralCategory.values
            .where(
              (item) => '${item.label} ${item.subtitle} ${item.keywords}'
                  .toLowerCase()
                  .contains(query),
            )
            .take(3);
        if (matches.isEmpty) {
          return [ListTile(title: Text(UiText.t("没有找到相关设置")))];
        }
        return matches.map(
          (item) => ListTile(
            leading: Icon(item.icon),
            title: Text(item.label),
            subtitle: Text(item.subtitle),
            onTap: () => _select(item, controller.text),
          ),
        );
      }

      return [
        ValueListenableBuilder<List<String>>(
          valueListenable: _history,
          builder: (context, history, _) => Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (history.isNotEmpty) ...[
                Padding(
                  padding: EdgeInsets.fromLTRB(16, 12, 8, 4),
                  child: Row(
                    children: [
                      Expanded(child: Text(UiText.t("最近搜索"))),
                      TextButton(
                        onPressed: () {
                          _history.value = [];
                          widget.settings.preferences.remove(_historyKey);
                        },
                        child: Text(UiText.t("清除")),
                      ),
                    ],
                  ),
                ),
                for (final term in history.take(3))
                  ListTile(
                    leading: Icon(Icons.history),
                    title: Text(term),
                    onTap: () => controller.text = term,
                  ),
              ],
              Padding(
                padding: EdgeInsets.fromLTRB(16, 16, 16, 4),
                child: Text(UiText.t("搜索建议")),
              ),
              for (final (term, category) in _suggested)
                ListTile(
                  leading: Icon(Icons.search),
                  title: Text(term),
                  onTap: () => _select(category, term),
                ),
            ],
          ),
        ),
      ];
    },
  ));
}

class _TabletSettings extends StatefulWidget {
  const _TabletSettings({required this.page});
  final GeneralPage page;
  @override
  State<_TabletSettings> createState() => _TabletSettingsState();
}

class _TabletSettingsState extends State<_TabletSettings> {
  _GeneralCategory selected = _GeneralCategory.common;
  bool soundDetail = false;
  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final page = widget.page;
    return SafeArea(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            width: 300,
            child: ListView(
              padding: EdgeInsets.all(20),
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        UiText.t("常规"),
                        style: Theme.of(context).textTheme.headlineMedium,
                      ),
                    ),
                    if (page.profile != null && page.onAccountPressed != null)
                      AccountAvatarButton(
                        profile: page.profile!,
                        onPressed: page.onAccountPressed!,
                      ),
                  ],
                ),
                SizedBox(height: 20),
                _GeneralSettingsSearch(
                  settings: page.settings,
                  onCategorySelected: (value) => setState(() {
                    selected = value;
                    soundDetail = false;
                  }),
                ),
                SizedBox(height: 16),
                for (final category in _GeneralCategory.values)
                  Padding(
                    padding: EdgeInsets.only(bottom: 4),
                    child: ListTile(
                      shape: StadiumBorder(),
                      selected: selected == category,
                      selectedTileColor: Theme.of(context)
                          .colorScheme
                          .secondaryContainer,
                      leading: Icon(category.icon),
                      title: Text(category.label),
                      onTap: () => setState(() {
                        selected = category;
                        soundDetail = false;
                      }),
                    ),
                  ),
              ],
            ),
          ),
          VerticalDivider(width: 1),
          Expanded(
            child: soundDetail
                ? TimerSoundPage(
                    preferences: page.general.preferences,
                    onBack: () => setState(() => soundDetail = false),
                  )
                : selected == _GeneralCategory.personalization
                ? PersonalizationPage(settings: page.settings, embedded: true)
                : AnimatedBuilder(
                    animation: Listenable.merge([
                      page.general,
                      page.dashboard,
                      ?page.cloud,
                      ?page.updates,
                    ]),
                    builder: (context, _) => ListView(
                      key: ValueKey(selected),
                      padding: EdgeInsets.all(24),
                      children: [
                        Text(
                          selected.label,
                          style: Theme.of(context).textTheme.headlineSmall,
                        ),
                        SizedBox(height: 24),
                        ...page._categoryItems(
                          context,
                          selected,
                          onTimerSound: () =>
                              setState(() => soundDetail = true),
                        ),
                      ],
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}
