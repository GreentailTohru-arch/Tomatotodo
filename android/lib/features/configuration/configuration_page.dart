import 'package:tomatotodo/core/localization/ui_text.dart';

import '../../core/widgets/adaptive_card_list.dart';
import '../../core/widgets/app_fab_location.dart';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/account/account_drawer.dart';
import '../../core/account/local_profile.dart';

import '../dashboard/dashboard_controller.dart';

class ConfigurationPage extends StatelessWidget {
  const ConfigurationPage({
    super.key,
    required this.controller,
    this.profile,
    this.onAccountPressed,
    this.onRequestNotificationAccess,
  });
  final DashboardController controller;
  final LocalProfile? profile;
  final VoidCallback? onAccountPressed;
  final Future<bool> Function()? onRequestNotificationAccess;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => AnimatedBuilder(
      animation: controller,
      builder: (context, _) => Scaffold(
        appBar: AppBar(
          title: Text(UiText.t("配置")),
          actions: [
            IconButton(
              tooltip: UiText.t("历史任务"),
              icon: Icon(Icons.history),
              onPressed: () => _push(context, _HistoryPage(controller)),
            ),
            IconButton(
              tooltip: UiText.t("配置排序"),
              icon: Icon(Icons.swap_vert),
              onPressed: () => _push(context, _SortPage(controller)),
            ),
            if (profile != null && onAccountPressed != null)
              AccountAvatarButton(
                profile: profile!,
                onPressed: onAccountPressed!,
              ),
          ],
        ),
        body: AdaptiveCardList(
          padding: EdgeInsets.fromLTRB(16, 12, 16, 112),
          children: [
            for (var i = 0; i < controller.presets.length; i++) ...[
              _PresetCard(
                controller,
                controller.presets[i],
                onRequestNotificationAccess,
                key: ValueKey(controller.presets[i].id),
              ),
              if (i < controller.presets.length - 1) SizedBox(height: 8),
            ],
          ],
        ),
        floatingActionButtonLocation: MediaQuery.sizeOf(context).width >= 600
            ? AppFabLocation()
            : FloatingActionButtonLocation.endFloat,
        floatingActionButton: FloatingActionButton.extended(
          onPressed: () => _presetDialog(context, controller),
          icon: Icon(Icons.add),
          label: Text(UiText.t("添加预设")),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
        ),
      ),
    ),
  );
}

void _push(BuildContext context, Widget page) =>
    Navigator.push(context, MaterialPageRoute<void>(builder: (_) => page));
Future<void> _presetDialog(
  BuildContext context,
  DashboardController controller, [
  TaskPreset? preset,
]) async {
  final value = await showDialog<String>(
    context: context,
    builder: (context) => _PresetNameDialog(
      title: preset == null ? UiText.t("添加预设") : UiText.t("重命名清单"),
      initialName: preset?.name ?? '',
    ),
  );
  if (value == null || value.trim().isEmpty) return;
  if (preset == null) {
    controller.addPreset(value);
  } else {
    controller.renamePreset(preset.id, value);
  }
}

class _PresetNameDialog extends StatefulWidget {
  const _PresetNameDialog({required this.title, required this.initialName});
  final String title;
  final String initialName;

  @override
  State<_PresetNameDialog> createState() => _PresetNameDialogState();
}

class _PresetNameDialogState extends State<_PresetNameDialog> {
  late final TextEditingController field = TextEditingController(
    text: widget.initialName,
  );

  @override
  void dispose() {
    field.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => AlertDialog(
      title: Text(widget.title),
      content: TextField(
        controller: field,
        autofocus: true,
        maxLength: 40,
        decoration: InputDecoration(
          labelText: UiText.t("清单名称"),
          border: OutlineInputBorder(),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: Text(UiText.t("取消")),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(context, field.text),
          child: Text(UiText.t("保存")),
        ),
      ],
    ),
  );
}

class _PresetCard extends StatefulWidget {
  const _PresetCard(
    this.controller,
    this.preset,
    this.requestAccess, {
    super.key,
  });
  final DashboardController controller;
  final TaskPreset preset;
  final Future<bool> Function()? requestAccess;
  @override
  State<_PresetCard> createState() => _PresetCardState();
}

class _PresetCardState extends State<_PresetCard> {
  bool _expanded = true;
  DashboardController get controller => widget.controller;
  TaskPreset get preset => widget.preset;
  Future<bool> Function()? get requestAccess => widget.requestAccess;
  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    final tasks = controller.tasksFor(preset.id);
    final active = preset.id == controller.activePresetId;
    final estimate = tasks.fold<int>(0, (sum, task) => sum + task.estimate);
    return Card.outlined(
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(
          color: active ? scheme.primary : scheme.outlineVariant,
          width: active ? 2 : 1,
        ),
      ),
      child: Padding(
        padding: EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  active ? Icons.today_outlined : Icons.list_alt_outlined,
                  color: scheme.primary,
                ),
                SizedBox(width: 10),
                Expanded(
                  child: Wrap(
                    spacing: 6,
                    runSpacing: 4,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(
                        preset.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      if (active) ...[
                        DecoratedBox(
                          decoration: BoxDecoration(
                            color: scheme.primaryContainer,
                            borderRadius: BorderRadius.circular(99),
                          ),
                          child: Padding(
                            padding: EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 4,
                            ),
                            child: Text(
                              UiText.t("当前清单"),
                              style: Theme.of(context).textTheme.labelSmall
                                  ?.copyWith(color: scheme.onPrimaryContainer),
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                IconButton(
                  tooltip: _expanded ? UiText.t("收起任务") : UiText.t("展开任务"),
                  onPressed: () => setState(() => _expanded = !_expanded),
                  icon: AnimatedRotation(
                    turns: _expanded ? .5 : 0,
                    duration: MediaQuery.disableAnimationsOf(context)
                        ? Duration.zero
                        : Duration(milliseconds: 250),
                    child: Icon(Icons.expand_more),
                  ),
                ),
                PopupMenuButton<String>(
                  tooltip: UiText.t("更多"),
                  onSelected: (value) async {
                    switch (value) {
                      case 'select':
                        controller.selectPreset(preset.id);
                      case 'edit':
                        _push(context, _EditorPage(controller, preset.id));
                      case 'due':
                      case 'remind':
                        await _scheduleDialog(
                          context,
                          controller,
                          preset,
                          reminder: value == 'remind',
                          requestAccess: requestAccess,
                        );
                      case 'repeat':
                        await _repeatDialog(context, controller, preset);
                      case 'delete':
                        _deleteDialog(context, controller, preset);
                    }
                  },
                  itemBuilder: (_) => [
                    PopupMenuItem(
                      value: 'select',
                      child: _MenuLabel(Icons.check, UiText.t("设为当前")),
                    ),
                    PopupMenuItem(
                      value: 'edit',
                      child: _MenuLabel(Icons.edit_outlined, UiText.t("编辑")),
                    ),
                    PopupMenuItem(
                      value: 'due',
                      child: _MenuLabel(Icons.event_outlined, UiText.t("截止日期")),
                    ),
                    PopupMenuItem(
                      value: 'remind',
                      child: _MenuLabel(
                        Icons.notifications_outlined,
                        UiText.t("提醒我"),
                      ),
                    ),
                    PopupMenuItem(
                      value: 'repeat',
                      child: _MenuLabel(Icons.repeat, UiText.t("重复")),
                    ),
                    PopupMenuDivider(),
                    if (controller.presets.length > 1)
                      PopupMenuItem(
                        value: 'delete',
                        child: _MenuLabel(Icons.delete_outline, UiText.t("删除")),
                      ),
                  ],
                ),
              ],
            ),
            if (preset.dueAt != null ||
                preset.remindAt != null ||
                preset.repeat != 'none') ...[
              SizedBox(height: 6),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                physics: BouncingScrollPhysics(),
                child: Row(
                  children: [
                    if (preset.dueAt != null)
                      Chip(
                        avatar: Icon(Icons.event_outlined, size: 18),
                        label: Text(
                          UiText.f("截止 {0}/{1}", [
                            preset.dueAt!.month,
                            preset.dueAt!.day,
                          ]),
                        ),
                      ),
                    if (preset.dueAt != null &&
                        (preset.remindAt != null || preset.repeat != 'none'))
                      SizedBox(width: 6),
                    if (preset.remindAt != null)
                      Chip(
                        avatar: Icon(Icons.notifications_outlined, size: 18),
                        label: Text(
                          UiText.f("提醒 {0}/{1} {2}:{3}", [
                            preset.remindAt!.month,
                            preset.remindAt!.day,
                            preset.remindAt!.hour.toString().padLeft(2, '0'),
                            preset.remindAt!.minute.toString().padLeft(2, '0'),
                          ]),
                        ),
                      ),
                    if (preset.remindAt != null && preset.repeat != 'none')
                      SizedBox(width: 6),
                    if (preset.repeat != 'none')
                      Chip(
                        avatar: Icon(Icons.repeat, size: 18),
                        label: Text(
                          UiText.f("重复 {0}", [
                            _repeatLabels[preset.repeat] ?? UiText.t("自定义"),
                          ]),
                        ),
                      ),
                  ],
                ),
              ),
            ],
            AnimatedCrossFade(
              duration: MediaQuery.disableAnimationsOf(context)
                  ? Duration.zero
                  : Duration(milliseconds: 250),
              sizeCurve: Curves.easeInOutCubic,
              crossFadeState: _expanded
                  ? CrossFadeState.showFirst
                  : CrossFadeState.showSecond,
              secondChild: SizedBox(width: double.infinity),
              firstChild: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Divider(height: 20),
                  if (tasks.isEmpty)
                    Padding(
                      padding: EdgeInsets.symmetric(vertical: 8),
                      child: Text(
                        UiText.t("还没有任务。使用“更多”菜单开始编辑。"),
                        style: TextStyle(color: scheme.onSurfaceVariant),
                      ),
                    )
                  else
                    SizedBox(
                      height: tasks
                          .take(3)
                          .fold<double>(
                            0,
                            (height, task) =>
                                height + (task.subtitle.isEmpty ? 48.0 : 64.0),
                          ),
                      child: ListView.builder(
                        primary: false,
                        padding: EdgeInsets.zero,
                        physics: tasks.length > 3
                            ? BouncingScrollPhysics()
                            : NeverScrollableScrollPhysics(),
                        itemCount: tasks.length,
                        itemBuilder: (context, i) => SizedBox(
                          height: tasks[i].subtitle.isEmpty ? 48 : 64,
                          child: Padding(
                            padding: EdgeInsets.only(bottom: 2),
                            child: Material(
                              color:
                                  active &&
                                      tasks[i].id == controller.activeTaskId
                                  ? scheme.secondaryContainer
                                  : scheme.surfaceContainerLow,
                              borderRadius:
                                  active &&
                                      tasks[i].id == controller.activeTaskId
                                  ? BorderRadius.circular(24)
                                  : BorderRadius.vertical(
                                      top: Radius.circular(i == 0 ? 16 : 4),
                                      bottom: Radius.circular(
                                        i == tasks.length - 1 ? 16 : 4,
                                      ),
                                    ),
                              clipBehavior: Clip.antiAlias,
                              child: InkWell(
                                onTap: () {
                                  controller.selectPreset(preset.id);
                                  controller.selectTask(tasks[i].id);
                                },
                                child: Padding(
                                  padding: EdgeInsets.symmetric(horizontal: 12),
                                  child: Row(
                                    children: [
                                      SizedBox(
                                        width: 38,
                                        child: Center(
                                          child: Text(
                                            '${i + 1}'.padLeft(2, '0'),
                                          ),
                                        ),
                                      ),
                                      SizedBox(width: 8),
                                      Expanded(
                                        child: Column(
                                          mainAxisAlignment:
                                              MainAxisAlignment.center,
                                          crossAxisAlignment:
                                              CrossAxisAlignment.start,
                                          children: [
                                            Text(
                                              tasks[i].title,
                                              maxLines:
                                                  tasks[i].subtitle.isEmpty
                                                  ? 2
                                                  : 1,
                                              overflow: TextOverflow.ellipsis,
                                            ),
                                            if (tasks[i].subtitle.isNotEmpty)
                                              Text(
                                                tasks[i].subtitle,
                                                maxLines: 1,
                                                overflow: TextOverflow.ellipsis,
                                                style: Theme.of(context)
                                                    .textTheme
                                                    .bodySmall
                                                    ?.copyWith(
                                                      color: scheme
                                                          .onSurfaceVariant,
                                                    ),
                                              ),
                                          ],
                                        ),
                                      ),
                                      SizedBox(width: 8),
                                      Center(
                                        child: Text(
                                          tasks[i].estimate == 0
                                              ? '—'
                                              : UiText.f("{0} 番茄", [
                                                  tasks[i].estimate,
                                                ]),
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
            Divider(height: 20),
            Row(
              children: [
                Expanded(
                  child: Text(
                    UiText.f("{0}/{1} 已完成", [
                      tasks.where((task) => task.done).length,
                      tasks.length,
                    ]),
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Text(
                    estimate == 0
                        ? UiText.t("未设置番茄预算")
                        : UiText.f("预计 {0} 个番茄", [estimate]),
                    textAlign: TextAlign.end,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _MenuLabel extends StatelessWidget {
  const _MenuLabel(this.icon, this.label);
  final IconData icon;
  final String label;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => Row(
      children: [
        Icon(
          icon,
          size: 20,
          color: Theme.of(context).colorScheme.onSurfaceVariant,
        ),
        SizedBox(width: 12),
        Flexible(child: Text(label)),
      ],
    ),
  );
}

Map<String, String> get _repeatLabels => {
  'none': UiText.t("不重复"),
  'daily': UiText.t("每天"),
  'workday': UiText.t("工作日"),
  'weekly': UiText.t("每周"),
  'monthly': UiText.t("每月"),
  'yearly': UiText.t("每年"),
  'custom': UiText.t("自定义"),
};

Future<void> _scheduleDialog(
  BuildContext context,
  DashboardController controller,
  TaskPreset preset, {
  required bool reminder,
  Future<bool> Function()? requestAccess,
}) async {
  final options = reminder
      ? [
          UiText.t("不提醒"),
          UiText.t("今日晚些时候"),
          UiText.t("明天"),
          UiText.t("下周"),
          UiText.t("选择日期和时间"),
        ]
      : [
          UiText.t("不设置"),
          UiText.t("今天"),
          UiText.t("明天"),
          UiText.t("下周"),
          UiText.t("选择日期"),
        ];
  final choice = await showModalBottomSheet<int>(
    context: context,
    showDragHandle: true,
    useSafeArea: true,
    builder: (sheet) => ListView(
      shrinkWrap: true,
      children: [
        Padding(
          padding: EdgeInsets.fromLTRB(24, 8, 24, 12),
          child: Text(
            reminder ? UiText.t("提醒我") : UiText.t("截止日期"),
            style: Theme.of(sheet).textTheme.titleLarge,
          ),
        ),
        for (var i = 0; i < options.length; i++)
          ListTile(
            leading: Icon(
              i == 0 ? Icons.remove_circle_outline : Icons.event_outlined,
            ),
            title: Text(options[i]),
            onTap: () => Navigator.pop(sheet, i),
          ),
        SizedBox(height: 16),
      ],
    ),
  );
  if (choice == null || !context.mounted) return;
  final now = DateTime.now();
  DateTime? value;
  if (choice == 1) {
    value = reminder
        ? DateTime(now.year, now.month, now.day, 18)
        : DateTime(now.year, now.month, now.day, 23, 59);
    if (!value.isAfter(now)) value = now.add(Duration(hours: 1));
  } else if (choice == 2 || choice == 3) {
    final day = now.add(Duration(days: choice == 2 ? 1 : 7));
    value = DateTime(
      day.year,
      day.month,
      day.day,
      reminder ? 9 : 23,
      reminder ? 0 : 59,
    );
  } else if (choice == 4) {
    final date = await showDatePicker(
      context: context,
      initialDate: now,
      firstDate: DateTime(now.year),
      lastDate: DateTime(now.year + 10),
    );
    if (date == null || !context.mounted) return;
    if (reminder) {
      final time = await showTimePicker(
        context: context,
        initialTime: preset.remindAt == null
            ? TimeOfDay(hour: 9, minute: 0)
            : TimeOfDay.fromDateTime(preset.remindAt!),
      );
      if (time == null) return;
      value = DateTime(date.year, date.month, date.day, time.hour, time.minute);
    } else {
      value = DateTime(date.year, date.month, date.day, 23, 59);
    }
    if (!value.isAfter(DateTime.now())) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(UiText.t("请选择未来的日期和时间"))));
      }
      return;
    }
  }
  if (value != null && requestAccess != null && context.mounted) {
    final allowed = await requestAccess();
    if (!allowed && context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(UiText.t("请允许系统通知，清单提醒才能弹出"))));
    }
  }
  if (reminder) {
    controller.updatePresetSchedule(preset.id, remindAt: value);
  } else {
    controller.updatePresetSchedule(preset.id, dueAt: value);
  }
}

Future<void> _repeatDialog(
  BuildContext context,
  DashboardController controller,
  TaskPreset preset,
) async {
  final selected = await showModalBottomSheet<String>(
    context: context,
    showDragHandle: true,
    useSafeArea: true,
    builder: (sheet) => ListView(
      shrinkWrap: true,
      children: [
        Padding(
          padding: EdgeInsets.fromLTRB(24, 8, 24, 12),
          child: Text(
            UiText.t("重复"),
            style: Theme.of(sheet).textTheme.titleLarge,
          ),
        ),
        for (final item in _repeatLabels.entries)
          ListTile(
            leading: Icon(
              item.key == preset.repeat
                  ? Icons.radio_button_checked
                  : Icons.radio_button_unchecked,
            ),
            title: Text(item.value),
            onTap: () => Navigator.pop(sheet, item.key),
          ),
        SizedBox(height: 16),
      ],
    ),
  );
  if (selected == null || !context.mounted) return;
  if (selected != 'custom') {
    controller.updatePresetSchedule(preset.id, repeat: selected);
    return;
  }
  final field = TextEditingController(text: '${preset.repeatInterval}');
  var unit = preset.repeatUnit;
  final days = preset.repeatDays.toSet();
  final custom = await showDialog<bool>(
    context: context,
    builder: (dialog) => StatefulBuilder(
      builder: (context, update) => AlertDialog(
        title: Text(UiText.t("自定义重复")),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: field,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  labelText: UiText.t("重复周期"),
                  border: OutlineInputBorder(),
                ),
              ),
              SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: unit,
                decoration: InputDecoration(
                  labelText: UiText.t("单位"),
                  border: OutlineInputBorder(),
                ),
                items: [
                  DropdownMenuItem(value: 'day', child: Text(UiText.t("天"))),
                  DropdownMenuItem(value: 'week', child: Text(UiText.t("周"))),
                  DropdownMenuItem(value: 'month', child: Text(UiText.t("月"))),
                  DropdownMenuItem(value: 'year', child: Text(UiText.t("年"))),
                ],
                onChanged: (value) {
                  if (value != null) update(() => unit = value);
                },
              ),
              if (unit == 'week') ...[
                SizedBox(height: 12),
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: Text(UiText.t("重复日期")),
                ),
                Wrap(
                  spacing: 4,
                  children: [
                    for (var day = 1; day <= 7; day++)
                      FilterChip(
                        label: Text(
                          [
                            UiText.t("一"),
                            UiText.t("二"),
                            UiText.t("三"),
                            UiText.t("四"),
                            UiText.t("五"),
                            UiText.t("六"),
                            UiText.t("日"),
                          ][day - 1],
                        ),
                        selected: days.contains(day),
                        onSelected: (selected) => update(() {
                          if (selected) {
                            days.add(day);
                          } else {
                            days.remove(day);
                          }
                        }),
                      ),
                  ],
                ),
              ],
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialog, false),
            child: Text(UiText.t("取消")),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialog, true),
            child: Text(UiText.t("确定")),
          ),
        ],
      ),
    ),
  );
  final interval = int.tryParse(field.text);
  WidgetsBinding.instance.addPostFrameCallback((_) => field.dispose());
  if (custom != true || interval == null || interval < 1 || interval > 365) {
    if (custom == true && context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(UiText.t("重复周期应为 1 到 365"))));
    }
    return;
  }
  controller.updatePresetSchedule(
    preset.id,
    repeat: 'custom',
    repeatInterval: interval,
    repeatUnit: unit,
    repeatDays: days.toList()..sort(),
  );
}

Future<void> _deleteDialog(
  BuildContext context,
  DashboardController controller,
  TaskPreset preset,
) async {
  final yes = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(UiText.t("删除清单？")),
      content: Text(UiText.f("“{0}”及其中任务将移除。", [preset.name])),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: Text(UiText.t("取消")),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(context, true),
          child: Text(UiText.t("删除")),
        ),
      ],
    ),
  );
  if (yes == true) controller.deletePreset(preset.id);
}

class _EditorPage extends StatefulWidget {
  const _EditorPage(this.controller, this.presetId);
  final DashboardController controller;
  final String presetId;
  @override
  State<_EditorPage> createState() => _EditorPageState();
}

class _EditorPageState extends State<_EditorPage> {
  final name = TextEditingController();
  final subtitle = TextEditingController();
  final estimate = TextEditingController();
  @override
  void dispose() {
    name.dispose();
    subtitle.dispose();
    estimate.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => AnimatedBuilder(
      animation: widget.controller,
      builder: (context, _) {
        final controller = widget.controller;
        final preset = controller.presets
            .where((item) => item.id == widget.presetId)
            .firstOrNull;
        if (preset == null) {
          return Scaffold(body: Center(child: Text(UiText.t("清单已删除"))));
        }
        final tasks = controller.tasksFor(preset.id);
        return Scaffold(
          appBar: AppBar(
            title: Text(UiText.t("编辑预设")),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: Text(UiText.t("完成")),
              ),
            ],
          ),
          body: Column(
            children: [
              Padding(
                padding: EdgeInsets.all(16),
                child: ListTile(
                  title: Text(preset.name),
                  subtitle: Text(UiText.t("清单名称")),
                  trailing: Icon(Icons.edit_outlined),
                  onTap: () => _presetDialog(context, controller, preset),
                ),
              ),
              Padding(
                padding: EdgeInsets.fromLTRB(16, 0, 16, 8),
                child: Row(
                  children: [
                    Text(
                      UiText.t("任务内容"),
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    Spacer(),
                    Text(UiText.f("{0} 项 · 左右滑动显示操作", [tasks.length])),
                  ],
                ),
              ),
              Expanded(
                child: ReorderableListView.builder(
                  buildDefaultDragHandles: false,
                  padding: EdgeInsets.symmetric(horizontal: 12),
                  itemCount: tasks.length,
                  onReorderItem: (oldIndex, newIndex) =>
                      controller.reorderTasks(preset.id, oldIndex, newIndex),
                  itemBuilder: (context, index) {
                    final task = tasks[index];
                    return _SwipeActionsTaskRow(
                      key: ValueKey(task.id),
                      task: task,
                      index: index,
                      controller: controller,
                    );
                  },
                ),
              ),
              SafeArea(
                top: false,
                child: Padding(
                  padding: EdgeInsets.all(16),
                  child: Column(
                    children: [
                      TextField(
                        controller: name,
                        decoration: InputDecoration(
                          labelText: UiText.t("任务名称"),
                          border: OutlineInputBorder(),
                        ),
                      ),
                      SizedBox(height: 8),
                      Row(
                        children: [
                          Expanded(
                            child: TextField(
                              controller: subtitle,
                              decoration: InputDecoration(
                                labelText: UiText.t("小标题（可选）"),
                                border: OutlineInputBorder(),
                              ),
                            ),
                          ),
                          SizedBox(width: 8),
                          SizedBox(
                            width: 112,
                            child: TextField(
                              controller: estimate,
                              keyboardType: TextInputType.number,
                              decoration: InputDecoration(
                                labelText: UiText.t("番茄数"),
                                border: OutlineInputBorder(),
                              ),
                            ),
                          ),
                        ],
                      ),
                      SizedBox(height: 8),
                      SizedBox(
                        width: double.infinity,
                        child: FilledButton.icon(
                          onPressed: () {
                            if (name.text.trim().isEmpty) return;
                            controller.addTask(
                              name.text,
                              presetId: preset.id,
                              subtitle: subtitle.text,
                              estimate: int.tryParse(estimate.text) ?? 0,
                            );
                            name.clear();
                            subtitle.clear();
                            estimate.clear();
                          },
                          icon: Icon(Icons.add),
                          label: Text(UiText.t("添加任务")),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        );
      },
    ),
  );
}

class _SwipeActionsTaskRow extends StatefulWidget {
  const _SwipeActionsTaskRow({
    super.key,
    required this.task,
    required this.index,
    required this.controller,
  });
  final FocusTask task;
  final int index;
  final DashboardController controller;

  @override
  State<_SwipeActionsTaskRow> createState() => _SwipeActionsTaskRowState();
}

class _SwipeActionsTaskRowState extends State<_SwipeActionsTaskRow> {
  static final _revealWidth = 128.0;
  double _direction = 1;
  double _drag = 0;
  bool _revealed = false;

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    final task = widget.task;
    return ClipRect(
      child: Stack(
        children: [
          Positioned.fill(
            child: Align(
              alignment: (_revealed ? _direction : _drag) < 0
                  ? AlignmentDirectional.centerEnd
                  : AlignmentDirectional.centerStart,
              child: SizedBox(
                width: _revealWidth,
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                  children: [
                    IconButton.filledTonal(
                      tooltip: UiText.t("编辑任务"),
                      onPressed: _revealed
                          ? () {
                              setState(() => _revealed = false);
                              _taskDialog(context, widget.controller, task);
                            }
                          : null,
                      style: IconButton.styleFrom(minimumSize: Size(56, 56)),
                      icon: Icon(Icons.edit_outlined),
                    ),
                    IconButton.filledTonal(
                      tooltip: UiText.t("删除任务"),
                      onPressed: _revealed
                          ? () => widget.controller.deleteTask(task.id)
                          : null,
                      style: IconButton.styleFrom(
                        minimumSize: Size(56, 56),
                        backgroundColor: scheme.errorContainer,
                        foregroundColor: scheme.onErrorContainer,
                      ),
                      icon: Icon(Icons.delete_outline),
                    ),
                  ],
                ),
              ),
            ),
          ),
          TweenAnimationBuilder<double>(
            tween: Tween(end: _revealed ? _direction * _revealWidth : 0),
            duration: MediaQuery.disableAnimationsOf(context)
                ? Duration.zero
                : Duration(milliseconds: 220),
            curve: Curves.easeOutCubic,
            builder: (context, offset, child) => Transform.translate(
              offset: Offset(offset + _drag, 0),
              child: child,
            ),
            child: GestureDetector(
              onHorizontalDragUpdate: (details) => setState(() {
                final base = _revealed ? _direction * _revealWidth : 0.0;
                _drag =
                    (base + _drag + details.delta.dx).clamp(
                      -_revealWidth,
                      _revealWidth,
                    ) -
                    base;
              }),
              onHorizontalDragCancel: () => setState(() => _drag = 0),
              onHorizontalDragEnd: (_) => setState(() {
                final total =
                    (_revealed ? _direction * _revealWidth : 0) + _drag;
                _revealed = total.abs() > 32;
                _direction = total < 0 ? -1 : 1;
                _drag = 0;
              }),
              child: Card.filled(
                child: ListTile(
                  leading: ReorderableDragStartListener(
                    index: widget.index,
                    child: Icon(Icons.drag_handle),
                  ),
                  title: Text(task.title),
                  subtitle: task.subtitle.trim().isEmpty
                      ? null
                      : Text(task.subtitle, maxLines: 2),
                  trailing: task.estimate > 0
                      ? Text(UiText.f("{0} 个番茄", [task.estimate]))
                      : null,
                  onTap: () {
                    if (_revealed) {
                      setState(() => _revealed = false);
                    } else {
                      _taskDialog(context, widget.controller, task);
                    }
                  },
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

Future<void> _taskDialog(
  BuildContext context,
  DashboardController controller,
  FocusTask task,
) async {
  final name = TextEditingController(text: task.title);
  final subtitle = TextEditingController(text: task.subtitle);
  final estimate = TextEditingController(
    text: task.estimate == 0 ? '' : '${task.estimate}',
  );
  final yes = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(UiText.t("编辑任务")),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextField(
            controller: name,
            decoration: InputDecoration(labelText: UiText.t("任务名称")),
          ),
          TextField(
            controller: subtitle,
            decoration: InputDecoration(labelText: UiText.t("小标题（可选）")),
          ),
          TextField(
            controller: estimate,
            keyboardType: TextInputType.number,
            decoration: InputDecoration(labelText: UiText.t("番茄数")),
          ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: Text(UiText.t("取消")),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(context, true),
          child: Text(UiText.t("保存")),
        ),
      ],
    ),
  );
  if (yes == true) {
    controller.updateTask(
      task.id,
      name.text,
      subtitle.text,
      int.tryParse(estimate.text) ?? 0,
    );
  }
  name.dispose();
  subtitle.dispose();
  estimate.dispose();
}

class _HistoryPage extends StatefulWidget {
  const _HistoryPage(this.controller);
  final DashboardController controller;
  @override
  State<_HistoryPage> createState() => _HistoryPageState();
}

class _HistoryPageState extends State<_HistoryPage> {
  final Set<String> selected = {};
  final Map<int, GlobalKey> _rowKeys = {};
  Set<String> _selectionBeforeDrag = {};
  int? _dragAnchor;

  Future<void> _restoreSelected() async {
    if (selected.isEmpty) return;
    final controller = widget.controller;
    final presetId = await showModalBottomSheet<String>(
      context: context,
      showDragHandle: true,
      useSafeArea: true,
      builder: (sheetContext) => ListView(
        shrinkWrap: true,
        children: [
          Padding(
            padding: EdgeInsets.fromLTRB(24, 8, 24, 12),
            child: Text(
              UiText.t("恢复到哪个清单？"),
              style: Theme.of(sheetContext).textTheme.titleLarge,
            ),
          ),
          for (final preset in controller.presets)
            ListTile(
              leading: Icon(
                preset.id == controller.activePresetId
                    ? Icons.today_outlined
                    : Icons.list_alt_outlined,
              ),
              title: Text(preset.name),
              subtitle: Text(
                UiText.f("{0} 项任务", [controller.tasksFor(preset.id).length]),
              ),
              trailing: preset.id == controller.activePresetId
                  ? Icon(Icons.check)
                  : null,
              onTap: () => Navigator.pop(sheetContext, preset.id),
            ),
          SizedBox(height: 16),
        ],
      ),
    );
    if (!mounted || presetId == null) return;
    controller.restoreHistory(selected, presetId: presetId);
    setState(selected.clear);
  }

  int? _rowAt(Offset globalPosition) {
    final history = widget.controller.taskHistory;
    for (var i = 0; i < history.length; i++) {
      final box = _rowKeys[i]?.currentContext?.findRenderObject();
      if (box is! RenderBox || !box.hasSize) continue;
      final rect = box.localToGlobal(Offset.zero) & box.size;
      if (globalPosition.dy >= rect.top && globalPosition.dy <= rect.bottom) {
        return i;
      }
    }
    return null;
  }

  void _selectThrough(int index) {
    final anchor = _dragAnchor;
    if (anchor == null) return;
    final history = widget.controller.taskHistory;
    setState(() {
      selected
        ..clear()
        ..addAll(_selectionBeforeDrag);
      for (
        var i = anchor < index ? anchor : index;
        i <= (anchor > index ? anchor : index);
        i++
      ) {
        selected.add(history[i].id);
      }
    });
  }

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => AnimatedBuilder(
      animation: widget.controller,
      builder: (context, _) => Scaffold(
        appBar: AppBar(
          title: Text(UiText.t("历史任务")),
          actions: [
            if (widget.controller.taskHistory.isNotEmpty)
              TextButton(
                onPressed: () => setState(() {
                  final ids = widget.controller.taskHistory.map(
                    (entry) => entry.id,
                  );
                  if (selected.length == widget.controller.taskHistory.length) {
                    selected.clear();
                  } else {
                    selected
                      ..clear()
                      ..addAll(ids);
                  }
                }),
                child: Text(
                  selected.length == widget.controller.taskHistory.length
                      ? UiText.t("取消全选")
                      : UiText.t("全选"),
                ),
              ),
          ],
        ),
        body: widget.controller.taskHistory.isEmpty
            ? Center(child: Text(UiText.t("暂无历史任务")))
            : Column(
                children: [
                  Padding(
                    padding: EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Align(
                      alignment: AlignmentDirectional.centerStart,
                      child: Text(
                        UiText.t("长按任务并拖动，可连续选择"),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ),
                  ),
                  Expanded(
                    child: GestureDetector(
                      behavior: HitTestBehavior.translucent,
                      onLongPressStart: (details) {
                        final index = _rowAt(details.globalPosition);
                        if (index == null) return;
                        HapticFeedback.selectionClick();
                        _selectionBeforeDrag = Set.of(selected);
                        _dragAnchor = index;
                        _selectThrough(index);
                      },
                      onLongPressMoveUpdate: (details) {
                        final index = _rowAt(details.globalPosition);
                        if (index != null) _selectThrough(index);
                      },
                      onLongPressEnd: (_) => _dragAnchor = null,
                      child: ListView.builder(
                        itemCount: widget.controller.taskHistory.length,
                        itemBuilder: (context, index) {
                          final entry = widget.controller.taskHistory[index];
                          return CheckboxListTile(
                            key: _rowKeys.putIfAbsent(index, () => GlobalKey()),
                            value: selected.contains(entry.id),
                            onChanged: (value) => setState(() {
                              if (value == true) {
                                selected.add(entry.id);
                              } else {
                                selected.remove(entry.id);
                              }
                            }),
                            title: Text(entry.title),
                            subtitle: Text(
                              UiText.f("{0} · {1}-{2} {3}:{4}", [
                                entry.status == 'expired'
                                    ? UiText.t("已逾期")
                                    : entry.completed
                                    ? UiText.t("已完成")
                                    : UiText.t("已删除"),
                                entry.at.month,
                                entry.at.day,
                                entry.at.hour.toString().padLeft(2, '0'),
                                entry.at.minute.toString().padLeft(2, '0'),
                              ]),
                            ),
                          );
                        },
                      ),
                    ),
                  ),
                ],
              ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: EdgeInsets.all(16),
            child: Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: selected.isEmpty
                        ? null
                        : () {
                            widget.controller.deleteHistory(selected);
                            setState(selected.clear);
                          },
                    child: Text(UiText.t("删除")),
                  ),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: FilledButton(
                    onPressed: selected.isEmpty ? null : _restoreSelected,
                    child: Text(UiText.t("恢复")),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
}

class _SortPage extends StatelessWidget {
  const _SortPage(this.controller);
  final DashboardController controller;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => AnimatedBuilder(
      animation: controller,
      builder: (context, _) => Scaffold(
        appBar: AppBar(title: Text(UiText.t("配置排序"))),
        body: ReorderableListView.builder(
          buildDefaultDragHandles: false,
          padding: EdgeInsets.all(16),
          itemCount: controller.presets.length,
          onReorderItem: controller.reorderPresets,
          itemBuilder: (context, index) {
            final preset = controller.presets[index];
            return Card.filled(
              key: ValueKey(preset.id),
              child: ListTile(
                leading: ReorderableDragStartListener(
                  index: index,
                  child: Icon(Icons.drag_handle),
                ),
                title: Text(preset.name),
                subtitle: Text(
                  UiText.f("{0} 项任务{1}", [
                    controller.tasksFor(preset.id).length,
                    preset.id == controller.activePresetId
                        ? UiText.t(" · 当前清单")
                        : '',
                  ]),
                ),
                trailing: Text('${index + 1}'.padLeft(2, '0')),
              ),
            );
          },
        ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: EdgeInsets.all(16),
            child: FilledButton(
              onPressed: () => Navigator.pop(context),
              child: Text(UiText.t("保存排序")),
            ),
          ),
        ),
      ),
    ),
  );
}
