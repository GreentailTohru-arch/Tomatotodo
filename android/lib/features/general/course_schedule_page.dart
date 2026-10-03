import 'package:tomatotodo/core/localization/ui_text.dart';
import '../../core/widgets/adaptive_card_list.dart';

import 'package:flutter/material.dart';

import '../../core/account/account_drawer.dart';
import '../../core/account/local_profile.dart';
import '../dashboard/course_colors.dart';
import 'course_schedule.dart';
import 'general_state.dart';

class CourseSchedulePage extends StatefulWidget {
  const CourseSchedulePage({
    super.key,
    required this.general,
    this.profile,
    this.onAccountPressed,
  });
  final GeneralState general;
  final LocalProfile? profile;
  final VoidCallback? onAccountPressed;
  @override
  State<CourseSchedulePage> createState() => _CourseSchedulePageState();
}

class _CourseSchedulePageState extends State<CourseSchedulePage> {
  static List<String> get days => [UiText.t("周一"), UiText.t("周二"), UiText.t("周三"), UiText.t("周四"), UiText.t("周五"), UiText.t("周六"), UiText.t("周日")];
  final search = SearchController();
  final scroll = ScrollController();
  final dayKeys = List.generate(7, (_) => GlobalKey());
  int selectedWeek = 0;
  CourseSchedule? _displayedSchedule;
  CourseEvent? selectedEvent;

  @override
  void dispose() {
    search.dispose();
    scroll.dispose();
    super.dispose();
  }

  void chooseWeek(int index) {
    setState(() {
      selectedWeek = index;
      selectedEvent = null;
    });
    if (scroll.hasClients) scroll.jumpTo(0);
  }

  void showResult(int index, CourseEvent event) {
    search.closeView(event.name);
    setState(() {
      selectedWeek = index;
      selectedEvent = event;
    });
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final target = dayKeys[event.day].currentContext;
      if (target != null) {
        Scrollable.ensureVisible(
          target,
          duration: Duration(milliseconds: 350),
          curve: Curves.easeInOutCubicEmphasized,
          alignment: .12,
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => AnimatedBuilder(
    animation: widget.general,
    builder: (context, _) {
      final weeks = widget.general.schedule.weeks;
      if (!identical(_displayedSchedule, widget.general.schedule)) {
        _displayedSchedule = widget.general.schedule;
        final currentMonday = mondayOf(DateTime.now());
        final current = weeks.indexWhere(
          (week) => week.monday == currentMonday,
        );
        selectedWeek = current >= 0 ? current : 0;
        selectedEvent = null;
      }
      if (weeks.isEmpty) {
        return Scaffold(body: Center(child: Text(UiText.t("暂无课程表"))));
      }
      if (selectedWeek >= weeks.length) selectedWeek = 0;
      final week = weeks[selectedWeek];
      final colors = Theme.of(context).colorScheme;
      return Scaffold(
        appBar: AppBar(
          title: Text(UiText.t("课程表")),
          actions: [
            SearchAnchor(
              searchController: search,
              builder: (context, controller) => IconButton(
                tooltip: UiText.t("搜索课程"),
                icon: Icon(Icons.search),
                onPressed: controller.openView,
              ),
              viewHintText: UiText.t("搜索课程、教室或教师"),
              suggestionsBuilder: (context, controller) {
                final query = controller.text.trim().toLowerCase();
                if (query.isEmpty) {
                  return [
                    ListTile(
                      leading: Icon(Icons.search),
                      title: Text(UiText.t("输入课程、教室或教师名称")),
                    ),
                  ];
                }
                final matches = <(int, CourseEvent)>[];
                for (var i = 0; i < weeks.length; i++) {
                  for (final event in weeks[i].events) {
                    if ('${event.name} ${event.room} ${event.teacher}'
                        .toLowerCase()
                        .contains(query)) {
                      matches.add((i, event));
                    }
                  }
                }
                if (matches.isEmpty) {
                  return [ListTile(title: Text(UiText.t("没有找到课程")))];
                }
                return matches.map((match) {
                  final (index, event) = match;
                  return ListTile(
                    leading: Icon(
                      Icons.school_outlined,
                      color: courseColors(context, event.name).primary,
                    ),
                    title: Text(event.name),
                    subtitle: Text(
                      UiText.f("第 {0} 周 · {1} · {2}–{3}{4}", [index + 1, days[event.day], event.startTime, event.endTime, event.room.isEmpty ? '' : ' · ${event.room}']),
                    ),
                    onTap: () => showResult(index, event),
                  );
                });
              },
            ),
            PopupMenuButton<int>(
              tooltip: UiText.t("切换周次"),
              icon: Icon(Icons.calendar_view_week_outlined),
              onSelected: chooseWeek,
              itemBuilder: (context) => [
                for (var i = 0; i < weeks.length; i++)
                  PopupMenuItem(
                    value: i,
                    child: Row(
                      children: [
                        Icon(
                          i == selectedWeek
                              ? Icons.check
                              : Icons.calendar_today_outlined,
                          size: 20,
                          color: i == selectedWeek
                              ? colors.primary
                              : colors.onSurfaceVariant,
                        ),
                        SizedBox(width: 12),
                        Flexible(
                          child: Text(
                            UiText.f("第 {0} 周 · {1}", [i + 1, courseDate(weeks[i].monday)]),
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
            if (widget.profile != null && widget.onAccountPressed != null)
              AccountAvatarButton(
                profile: widget.profile!,
                onPressed: widget.onAccountPressed!,
              ),
          ],
        ),
        body: AdaptiveCardList(
          equalRowHeights: true,
          controller: scroll,
          padding: EdgeInsets.fromLTRB(16, 8, 16, 24),
          header: Padding(
            padding: EdgeInsets.fromLTRB(4, 4, 4, 12),
            child: Text(
              UiText.f("第 {0} 周 · {1}—{2}", [selectedWeek + 1, courseDate(week.monday), courseDate(week.monday.add(Duration(days: 6)))]),
              style: Theme.of(context).textTheme.titleSmall
                  ?.copyWith(color: colors.onSurfaceVariant),
            ),
          ),
          children: [
            for (var day = 0; day < 7; day++) ...[
              Card.outlined(
                key: dayKeys[day],
                margin: EdgeInsets.zero,
                child: Padding(
                  padding: EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Padding(
                        padding: EdgeInsets.fromLTRB(4, 2, 4, 8),
                        child: Text(
                          days[day],
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      ),
                      if (!week.events.any((event) => event.day == day))
                        Padding(
                          padding: EdgeInsets.fromLTRB(4, 4, 4, 8),
                          child: Text(
                            UiText.t("无课程"),
                            style: TextStyle(color: colors.onSurfaceVariant),
                          ),
                        ),
                      if (week.events.any((event) => event.day == day))
                        _DayCourseList(
                          key: ValueKey('${week.monday}-$day'),
                          events: week.events
                              .where((event) => event.day == day)
                              .toList(),
                          selected: selectedEvent,
                        ),
                    ],
                  ),
                ),
              ),
              SizedBox(height: 8),
            ],
          ],
        ),
      );
    },
  ));
}

class _DayCourseList extends StatefulWidget {
  const _DayCourseList({super.key, required this.events, this.selected});
  final List<CourseEvent> events;
  final CourseEvent? selected;
  @override
  State<_DayCourseList> createState() => _DayCourseListState();
}

class _DayCourseListState extends State<_DayCourseList> {
  final _scroll = ScrollController();
  final _selectedKey = GlobalKey();
  @override
  void initState() {
    super.initState();
    _revealSelected();
  }

  @override
  void didUpdateWidget(covariant _DayCourseList oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.selected != widget.selected) _revealSelected();
  }

  void _revealSelected() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || widget.selected == null) return;
      final target = _selectedKey.currentContext;
      if (target != null) {
        Scrollable.ensureVisible(
          target,
          duration: Duration(milliseconds: 250),
          alignment: .5,
        );
      }
    });
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => LayoutBuilder(
    builder: (context, box) {
      final theme = Theme.of(context);
      final textWidth = (box.maxWidth - 56).clamp(1.0, double.infinity);
      double textHeight(String text, TextStyle? style) {
        final painter = TextPainter(
          text: TextSpan(text: text, style: style),
          textDirection: Directionality.of(context),
          textScaler: MediaQuery.textScalerOf(context),
        )..layout(maxWidth: textWidth);
        final height = painter.height;
        painter.dispose();
        return height;
      }

      double eventHeight(CourseEvent event) {
        final title = textHeight(
          event.name,
          theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w600),
        );
        final time = textHeight(
          UiText.f("{0}–{1} · 第 {2}–{3} 节", [event.startTime, event.endTime, event.start, event.end]),
          theme.textTheme.bodySmall,
        );
        final details = [
          event.room,
          event.teacher,
        ].where((s) => s.isNotEmpty).join(' · ');
        final content =
            title +
            4 +
            time +
            (details.isEmpty
                ? 0
                : 2 + textHeight(details, theme.textTheme.bodySmall));
        return 30 + (content < 22 ? 22 : content);
      }

      final visibleHeight = widget.events
          .take(2)
          .fold<double>(0, (sum, event) => sum + eventHeight(event));
      return SizedBox(
        height: visibleHeight,
        child: Scrollbar(
          controller: _scroll,
          child: ListView(
            controller: _scroll,
            primary: false,
            padding: EdgeInsets.zero,
            children: [
              for (final event in widget.events)
                _CourseTile(
                  key: identical(widget.selected, event) ? _selectedKey : null,
                  event: event,
                  highlighted: identical(widget.selected, event),
                ),
            ],
          ),
        ),
      );
    },
  ));
}

class _CourseTile extends StatelessWidget {
  const _CourseTile({
    super.key,
    required this.event,
    required this.highlighted,
  });
  final CourseEvent event;
  final bool highlighted;
  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = courseColors(context, event.name);
    final text = scheme.onPrimaryContainer;
    return Card.filled(
      margin: EdgeInsets.only(bottom: 6),
      color: scheme.primaryContainer,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: highlighted
            ? BorderSide(color: scheme.primary, width: 2)
            : BorderSide.none,
      ),
      child: Padding(
        padding: EdgeInsets.all(12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(Icons.school_outlined, color: text, size: 22),
            SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    event.name,
                    style: Theme.of(context).textTheme.titleSmall
                        ?.copyWith(color: text, fontWeight: FontWeight.w600),
                  ),
                  SizedBox(height: 4),
                  Text(
                    UiText.f("{0}–{1} · 第 {2}–{3} 节", [event.startTime, event.endTime, event.start, event.end]),
                    style: Theme.of(context).textTheme.bodySmall
                        ?.copyWith(color: text),
                  ),
                  if (event.room.isNotEmpty || event.teacher.isNotEmpty) ...[
                    SizedBox(height: 2),
                    Text(
                      [
                        event.room,
                        event.teacher,
                      ].where((s) => s.isNotEmpty).join(' · '),
                      style: Theme.of(context).textTheme.bodySmall
                          ?.copyWith(color: text),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
