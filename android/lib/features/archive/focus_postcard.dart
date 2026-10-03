import 'package:tomatotodo/core/localization/ui_text.dart';
import 'package:flutter/material.dart';

import '../../core/account/account_drawer.dart';
import '../../core/account/local_profile.dart';
import '../dashboard/dashboard_controller.dart';

class FocusPostcard extends StatelessWidget {
  const FocusPostcard({
    super.key,
    required this.date,
    required this.logs,
    required this.tasks,
    this.profile,
  });

  final DateTime date;
  final List<FocusLog> logs;
  final List<FocusTask> tasks;
  final LocalProfile? profile;

  String get _date =>
      '${date.year} / ${date.month.toString().padLeft(2, '0')} / ${date.day.toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final colors = Theme.of(context).colorScheme;
    final seconds = logs.fold<int>(0, (sum, log) => sum + log.seconds);
    final hours = seconds ~/ 3600;
    final minutes = (seconds % 3600) ~/ 60;
    final duration = hours > 0
        ? UiText.f("{0}小时 {1}分钟", [hours, minutes])
        : minutes > 0
        ? UiText.f("{0}分钟", [minutes])
        : UiText.f("{0}秒", [seconds]);
    final tomatoes = logs.where((log) => log.completedSession).length;
    final completed = tasks.where((task) => task.done).length;
    final recent = logs.reversed.take(2).toList();

    return SizedBox(
      width: 800,
      height: 500,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(32),
        child: DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [colors.primaryContainer, colors.surfaceContainerHigh],
            ),
          ),
          child: Stack(
            children: [
              Positioned(
                right: -100,
                top: -120,
                child: Container(
                  width: 370,
                  height: 370,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: colors.primary.withValues(alpha: .10),
                  ),
                ),
              ),
              Padding(
                padding: EdgeInsets.all(40),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        ClipRRect(
                          borderRadius: BorderRadius.circular(16),
                          child: Image.asset(
                            'assets/branding/tomatotodo-logo.png',
                            width: 54,
                            height: 54,
                          ),
                        ),
                        SizedBox(width: 16),
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Tomatotodo',
                              style: TextStyle(
                                color: colors.onSurface,
                                fontSize: 27,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            Text(
                              UiText.t("专注记录"),
                              style: TextStyle(
                                color: colors.onSurfaceVariant,
                                fontSize: 15,
                              ),
                            ),
                          ],
                        ),
                        Spacer(),
                        Text(
                          _date,
                          style: TextStyle(
                            color: colors.onSurfaceVariant,
                            fontSize: 17,
                          ),
                        ),
                      ],
                    ),
                    SizedBox(height: 34),
                    Text(
                      UiText.t("今天，专注了"),
                      style: TextStyle(
                        color: colors.onSurfaceVariant,
                        fontSize: 19,
                      ),
                    ),
                    Text(
                      duration,
                      style: TextStyle(
                        color: colors.onPrimaryContainer,
                        fontSize: 55,
                        fontWeight: FontWeight.w700,
                        letterSpacing: -2,
                      ),
                    ),
                    SizedBox(height: 18),
                    Row(
                      children: [
                        _Stat(
                          label: UiText.t("完成番茄"),
                          value: '$tomatoes',
                          icon: Icons.check_circle_outline,
                          colors: colors,
                        ),
                        SizedBox(width: 12),
                        _Stat(
                          label: UiText.t("完成任务"),
                          value: '$completed/${tasks.length}',
                          icon: Icons.task_alt,
                          colors: colors,
                        ),
                        SizedBox(width: 12),
                        _Stat(
                          label: UiText.t("专注记录"),
                          value: '${logs.length}',
                          icon: Icons.timer_outlined,
                          colors: colors,
                        ),
                      ],
                    ),
                    SizedBox(height: 20),
                    Divider(color: colors.outlineVariant),
                    SizedBox(height: 8),
                    Row(
                      children: [
                        if (profile != null)
                          ProfileAvatar(profile: profile!, radius: 19)
                        else
                          CircleAvatar(
                            radius: 19,
                            child: Icon(
                              Icons.person_outline,
                              color: colors.onPrimaryContainer,
                            ),
                          ),
                        SizedBox(width: 10),
                        Text(
                          profile?.name ?? UiText.t("本地用户"),
                          style: TextStyle(
                            color: colors.onSurface,
                            fontSize: 16,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        Spacer(),
                        Flexible(
                          child: Text(
                            recent.isEmpty
                                ? UiText.t("每一段专注，都值得记录")
                                : recent
                                      .map((log) => log.taskTitle)
                                      .join(' · '),
                            overflow: TextOverflow.ellipsis,
                            maxLines: 1,
                            textAlign: TextAlign.end,
                            style: TextStyle(
                              color: colors.onSurfaceVariant,
                              fontSize: 15,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({
    required this.label,
    required this.value,
    required this.icon,
    required this.colors,
  });
  final String label;
  final String value;
  final IconData icon;
  final ColorScheme colors;

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => Expanded(
    child: Container(
      height: 80,
      padding: EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      decoration: BoxDecoration(
        color: colors.surface.withValues(alpha: .65),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Row(
        children: [
          Icon(icon, color: colors.primary, size: 24),
          SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(
                value,
                style: TextStyle(
                  color: colors.onSurface,
                  fontSize: 23,
                  fontWeight: FontWeight.w700,
                ),
              ),
              Text(
                label,
                style: TextStyle(color: colors.onSurfaceVariant, fontSize: 13),
              ),
            ],
          ),
        ],
      ),
    ),
  ));
}
