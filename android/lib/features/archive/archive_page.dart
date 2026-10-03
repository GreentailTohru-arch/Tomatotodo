import 'package:tomatotodo/core/localization/ui_text.dart';

import '../../core/widgets/equal_height_row.dart';

import 'dart:ui' as ui;

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:gal/gal.dart';
import 'package:share_plus/share_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../core/account/account_drawer.dart';
import '../../core/account/local_profile.dart';

import '../dashboard/dashboard_controller.dart';
import 'focus_postcard.dart';

DateTime _day(DateTime date) => DateTime(date.year, date.month, date.day);
bool _same(DateTime a, DateTime b) =>
    a.year == b.year && a.month == b.month && a.day == b.day;
String _two(int n) => n.toString().padLeft(2, '0');
String _date(DateTime d) => '${d.year}-${_two(d.month)}-${_two(d.day)}';
int _seconds(Iterable<FocusLog> logs, DateTime date) => logs
    .where((log) => _same(log.at, date))
    .fold(0, (int sum, log) => sum + log.seconds);
String _duration(int seconds) => seconds >= 3600
    ? UiText.f("{0} 小时 {1} 分钟", [seconds ~/ 3600, (seconds % 3600) ~/ 60])
    : seconds >= 60
    ? UiText.f("{0} 分 {1} 秒", [seconds ~/ 60, seconds % 60])
    : UiText.f("{0} 秒", [seconds]);

String archiveGreeting(DateTime now) => switch (now.hour) {
  < 5 => UiText.t("夜深了，记得休息"),
  < 8 => UiText.t("早安，从容开始"),
  < 12 => UiText.t("上午好，稳稳向前"),
  < 14 => UiText.t("中午好，稍作休息"),
  < 18 => UiText.t("下午好，保持节奏"),
  < 21 => UiText.t("傍晚好，收好今天"),
  _ => UiText.t("晚上好，辛苦了"),
};

enum ArchivePeriod { day, week, month, year }

extension ArchivePeriodText on ArchivePeriod {
  String get label => switch (this) {
    ArchivePeriod.day => UiText.t("日度"),
    ArchivePeriod.week => UiText.t("周度"),
    ArchivePeriod.month => UiText.t("月度"),
    ArchivePeriod.year => UiText.t("年度"),
  };
}

class ArchivePage extends StatefulWidget {
  const ArchivePage({
    super.key,
    required this.controller,
    this.preferences,
    this.profile,
    this.onAccountPressed,
  });
  final DashboardController controller;
  final SharedPreferences? preferences;
  final LocalProfile? profile;
  final VoidCallback? onAccountPressed;
  @override
  State<ArchivePage> createState() => _ArchivePageState();
}

class _ArchivePageState extends State<ArchivePage>
    with SingleTickerProviderStateMixin {
  static final _orderKey = 'tomatotodo-archive-card-order-v1';
  static List<String> get _defaultOrder => [
    'calendar',
    'log',
    'statistics',
    'heatmap',
  ];
  late List<String> _cardOrder;
  bool _editing = false;
  final _postcardKey = GlobalKey();
  late final AnimationController _chartAnimation = AnimationController(
    vsync: this,
    duration: Duration(milliseconds: 520),
  )..forward();
  late DateTime month, selected, periodDate;
  int _monthDirection = 1;
  late int year;
  ArchivePeriod period = ArchivePeriod.month;
  bool exporting = false;
  @override
  void initState() {
    super.initState();
    final saved = widget.preferences?.getStringList(_orderKey);
    _cardOrder =
        saved != null &&
            saved.length == _defaultOrder.length &&
            saved.toSet().containsAll(_defaultOrder)
        ? List.of(saved)
        : List.of(_defaultOrder);
    final now = DateTime.now();
    month = DateTime(now.year, now.month);
    selected = _day(now);
    periodDate = _day(now);
    year = now.year;
  }

  @override
  void dispose() {
    _chartAnimation.dispose();
    super.dispose();
  }

  void _growChart() {
    if (MediaQuery.disableAnimationsOf(context)) {
      _chartAnimation.value = 1;
    } else {
      _chartAnimation.forward(from: 0);
    }
  }

  void _enterEditMode() {
    if (_editing) return;
    setState(() => _editing = true);
  }

  void _finishEditing() {
    widget.preferences?.setStringList(_orderKey, _cardOrder);
    setState(() => _editing = false);
  }

  void _reorderCards(int oldIndex, int newIndex) {
    setState(() {
      final moved = _cardOrder.removeAt(oldIndex);
      _cardOrder.insert(newIndex, moved);
    });
  }

  Widget _archiveCard(String id, BuildContext context, List<FocusLog> logs) =>
      switch (id) {
        'calendar' => _calendar(context, logs),
        'log' => _dailyLog(context, logs),
        'statistics' => _statistics(context, logs),
        _ => _heatmap(context, logs),
      };

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => AnimatedBuilder(
      animation: widget.controller,
      builder: (context, _) {
        final logs = widget.controller.logs;
        final today = _day(DateTime.now());
        final todays = logs.where((log) => _same(log.at, today)).toList();
        final tasks = widget.controller.activeTasks;
        return GestureDetector(
          behavior: HitTestBehavior.translucent,
          onLongPress: _editing ? null : _enterEditMode,
          child: CustomScrollView(
            slivers: [
              SliverAppBar(
                pinned: true,
                title: Text(
                  _editing ? UiText.t("调整档案") : archiveGreeting(DateTime.now()),
                ),
                actions: [
                  if (_editing)
                    IconButton.filledTonal(
                      tooltip: UiText.t("完成排序"),
                      icon: Icon(Icons.check),
                      onPressed: _finishEditing,
                    ),
                  if (!_editing)
                    IconButton(
                      tooltip: UiText.t("保存与分享"),
                      icon: Icon(Icons.ios_share_outlined),
                      onPressed: () => _postcard(context),
                    ),
                  if (widget.profile != null && widget.onAccountPressed != null)
                    AccountAvatarButton(
                      profile: widget.profile!,
                      onPressed: widget.onAccountPressed!,
                    ),
                ],
              ),
              SliverToBoxAdapter(
                child: Center(
                  child: ConstrainedBox(
                    constraints: BoxConstraints(maxWidth: double.infinity),
                    child: Padding(
                      padding: EdgeInsets.fromLTRB(16, 8, 16, 0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          LayoutBuilder(
                            builder: (context, box) => SizedBox(
                              height: 168,
                              child: CarouselView(
                                key: Key('archive-metric-carousel'),
                                itemExtent: box.maxWidth >= 1000
                                    ? box.maxWidth / 4
                                    : box.maxWidth < 340
                                    ? box.maxWidth * .72
                                    : box.maxWidth < 650
                                    ? 252
                                    : 276,
                                shrinkExtent: 72,
                                itemSnapping: true,
                                scrollDirection: Axis.horizontal,
                                padding: EdgeInsets.symmetric(horizontal: 4),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(28),
                                ),
                                children: [
                                  _Metric(
                                    UiText.t("今日专注"),
                                    _duration(_seconds(logs, today)),
                                    Icons.timer_outlined,
                                    0,
                                  ),
                                  _Metric(
                                    UiText.t("完成番茄"),
                                    UiText.f("{0} 个", [
                                      todays
                                          .where((log) => log.completedSession)
                                          .length,
                                    ]),
                                    Icons.check_circle_outline,
                                    1,
                                  ),
                                  _Metric(
                                    UiText.t("今日任务"),
                                    '${tasks.where((task) => task.done).length} / ${tasks.length}',
                                    Icons.checklist_outlined,
                                    2,
                                  ),
                                  _Metric(
                                    UiText.t("专注次数"),
                                    UiText.f("{0} 次", [todays.length]),
                                    Icons.insights_outlined,
                                    3,
                                  ),
                                ],
                              ),
                            ),
                          ),
                          SizedBox(key: Key('archive-edit-space'), height: 12),
                          if (_editing)
                            Text(
                              UiText.t("长按卡片调整顺序"),
                              style: Theme.of(context).textTheme.bodyMedium,
                            ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
              if (!_editing && MediaQuery.sizeOf(context).width >= 840)
                SliverToBoxAdapter(
                  child: LayoutBuilder(
                    builder: (context, box) => Padding(
                      padding: EdgeInsets.fromLTRB(24, 0, 24, 32),
                      child: Column(
                        children: [
                          for (var i = 0; i < _cardOrder.length; i += 2) ...[
                            if (i > 0) SizedBox(height: 16),
                            EqualHeightRow(
                              children: [
                                for (final id in _cardOrder.skip(i).take(2))
                                  KeyedSubtree(
                                    key: ValueKey('tablet-archive-$id'),
                                    child: _archiveCard(id, context, logs),
                                  ),
                              ],
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                )
              else
                SliverPadding(
                  padding: EdgeInsets.fromLTRB(16, 0, 16, 32),
                  sliver: SliverReorderableList(
                    itemCount: _cardOrder.length,
                    onReorderItem: _reorderCards,
                    itemBuilder: (context, index) {
                      final id = _cardOrder[index];
                      final card = Center(
                        child: ConstrainedBox(
                          constraints: BoxConstraints(maxWidth: 868),
                          child: Padding(
                            padding: EdgeInsets.only(bottom: 12),
                            child: AbsorbPointer(
                              absorbing: _editing,
                              child: _archiveCard(id, context, logs),
                            ),
                          ),
                        ),
                      );
                      return _editing
                          ? ReorderableDelayedDragStartListener(
                              key: ValueKey(id),
                              index: index,
                              child: card,
                            )
                          : GestureDetector(
                              key: ValueKey(id),
                              onLongPress: () {},
                              child: card,
                            );
                    },
                  ),
                ),
            ],
          ),
        );
      },
    ),
  );
  Widget _calendar(BuildContext context, List<FocusLog> logs) {
    final first = DateTime(month.year, month.month);
    final start = first.subtract(Duration(days: first.weekday - 1));
    final scheme = Theme.of(context).colorScheme;
    final tomatoCount = logs
        .where(
          (log) =>
              log.completedSession &&
              log.at.year == month.year &&
              log.at.month == month.month,
        )
        .length;
    return _Panel(
      UiText.t("专注日历"),
      Icons.calendar_month_outlined,
      _Pager(
        '${month.year} / ${_two(month.month)}',
        () => _changeMonth(-1),
        () => _changeMonth(1),
      ),
      GestureDetector(
        key: Key('archive-calendar-gesture'),
        behavior: HitTestBehavior.translucent,
        onHorizontalDragEnd: (details) {
          final velocity = details.primaryVelocity ?? 0;
          if (velocity.abs() > 120) _changeMonth(velocity < 0 ? 1 : -1);
        },
        child: ClipRect(
          child: AnimatedSwitcher(
            duration: MediaQuery.disableAnimationsOf(context)
                ? Duration.zero
                : Duration(milliseconds: 300),
            switchInCurve: Curves.easeOutCubic,
            switchOutCurve: Curves.easeInCubic,
            transitionBuilder: (child, animation) {
              final entering =
                  child.key == ValueKey('${month.year}-${month.month}');
              return SlideTransition(
                position: Tween<Offset>(
                  begin: Offset(
                    entering
                        ? _monthDirection.toDouble()
                        : -_monthDirection.toDouble(),
                    0,
                  ),
                  end: Offset.zero,
                ).animate(animation),
                child: child,
              );
            },
            child: Column(
              key: ValueKey('${month.year}-${month.month}'),
              children: [
                Card.filled(
                  child: Padding(
                    padding: EdgeInsets.all(12),
                    child: Row(
                      children: [
                        Text(
                          '🍅',
                          semanticsLabel: UiText.t("番茄"),
                          style: TextStyle(fontSize: 22),
                        ),
                        SizedBox(width: 8),
                        Expanded(child: Text(UiText.t("本月番茄"))),
                        const SizedBox(width: 8),
                        Flexible(
                          child: Text(
                            UiText.f("{0} 个", [tomatoCount]),
                            textAlign: TextAlign.end,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                SizedBox(height: 8),
                Row(
                  children: [
                    for (var weekday = 0; weekday < 7; weekday++)
                      Expanded(
                        child: Center(
                          child: Text(
                            MaterialLocalizations.of(context)
                                .narrowWeekdays[(weekday + 1) % 7],
                          ),
                        ),
                      ),
                  ],
                ),
                SizedBox(height: 8),
                for (var row = 0; row < 6; row++)
                  Padding(
                    padding: EdgeInsets.only(bottom: 4),
                    child: Row(
                      children: [
                        for (var col = 0; col < 7; col++)
                          Expanded(
                            child: Padding(
                              padding: EdgeInsets.symmetric(horizontal: 2),
                              child: _dayCell(
                                context,
                                logs,
                                start.add(Duration(days: row * 7 + col)),
                                scheme,
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _changeMonth(int direction) {
    setState(() {
      _monthDirection = direction;
      month = DateTime(month.year, month.month + direction);
    });
  }

  Widget _dayCell(
    BuildContext context,
    List<FocusLog> logs,
    DateTime date,
    ColorScheme scheme,
  ) {
    final picked = _same(date, selected);
    final today = _same(date, DateTime.now());
    final inside = date.month == month.month;
    final minutes = _seconds(logs, date) ~/ 60;
    final tomatoes = logs
        .where((log) => log.completedSession && _same(log.at, date))
        .length;
    return Tooltip(
      message: UiText.f("{0} · {1} 分钟 · {2} 个番茄", [
        _date(date),
        minutes,
        tomatoes,
      ]),
      child: AspectRatio(
        aspectRatio: 0.95,
        child: Material(
          color: today
              ? scheme.primaryContainer
              : inside
              ? scheme.surfaceContainerLow
              : scheme.surfaceContainer,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(8),
            side: BorderSide(
              color: picked ? scheme.primary : scheme.outlineVariant,
              width: picked ? 2 : 1,
            ),
          ),
          child: InkWell(
            onTap: () => setState(() => selected = date),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text(
                  date.day.toString(),
                  style: TextStyle(
                    color: inside ? scheme.onSurface : scheme.onSurfaceVariant,
                    fontWeight: picked || today ? FontWeight.bold : null,
                  ),
                ),
                if (minutes > 0)
                  Text(
                    minutes.toString(),
                    style: Theme.of(context).textTheme.labelSmall
                        ?.copyWith(color: scheme.primary),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _dailyLog(BuildContext context, List<FocusLog> logs) {
    final entries = logs.where((log) => _same(log.at, selected)).toList();
    return _Panel(
      UiText.f("{0} 日志", [_date(selected)]),
      Icons.description_outlined,
      Text(
        _duration(_seconds(logs, selected)),
        style: Theme.of(context).textTheme.titleMedium,
      ),
      entries.isEmpty
          ? Padding(
              padding: EdgeInsets.symmetric(vertical: 24),
              child: Center(child: Text(UiText.t("这一天还没有专注记录"))),
            )
          : LayoutBuilder(
              builder: (context, constraints) => ConstrainedBox(
                constraints: BoxConstraints(
                  maxHeight: constraints.hasBoundedHeight
                      ? constraints.maxHeight
                      : 320,
                ),
                child: Scrollbar(
                  child: ListView.builder(
                    key: Key('archive-log-list'),
                    primary: false,
                    padding: EdgeInsets.zero,
                    shrinkWrap: true,
                    itemCount: entries.length,
                    itemBuilder: (context, index) {
                      final log = entries[index];
                      return Card.filled(
                        child: ListTile(
                          title: Text(log.taskTitle),
                          subtitle: Text(
                            UiText.f("{0}:{1}{2}", [
                              _two(log.at.hour),
                              _two(log.at.minute),
                              log.completedSession ? UiText.t(" · 完成番茄") : '',
                            ]),
                          ),
                          trailing: Text(_duration(log.seconds)),
                        ),
                      );
                    },
                  ),
                ),
              ),
            ),
      fillBody: entries.isNotEmpty,
    );
  }

  Widget _statistics(BuildContext context, List<FocusLog> logs) {
    final start = switch (period) {
      ArchivePeriod.day => _day(periodDate),
      ArchivePeriod.week => _day(
        periodDate,
      ).subtract(Duration(days: periodDate.weekday - 1)),
      ArchivePeriod.month => DateTime(periodDate.year, periodDate.month),
      ArchivePeriod.year => DateTime(periodDate.year),
    };
    final count = switch (period) {
      ArchivePeriod.day => 24,
      ArchivePeriod.week => 7,
      ArchivePeriod.month => DateTime(start.year, start.month + 1, 0).day,
      ArchivePeriod.year => 12,
    };
    final values = List<int>.filled(count, 0);
    for (final log in logs) {
      final index = switch (period) {
        ArchivePeriod.day => _same(log.at, start) ? log.at.hour : -1,
        ArchivePeriod.week => _day(log.at).difference(start).inDays,
        ArchivePeriod.month =>
          log.at.year == start.year && log.at.month == start.month
              ? log.at.day - 1
              : -1,
        ArchivePeriod.year => log.at.year == start.year ? log.at.month - 1 : -1,
      };
      if (index >= 0 && index < count) values[index] += log.seconds;
    }
    final label = switch (period) {
      ArchivePeriod.day => _date(start),
      ArchivePeriod.week => UiText.f("{0} 起", [_date(start)]),
      ArchivePeriod.month => UiText.f("{0} 年 {1} 月", [start.year, start.month]),
      ArchivePeriod.year => UiText.f("{0} 年", [start.year]),
    };
    return _Panel(
      UiText.t("专注统计"),
      Icons.show_chart,
      _Pager(label, () => _shift(-1), () => _shift(1)),
      Column(
        children: [
          SegmentedButton<ArchivePeriod>(
            showSelectedIcon: false,
            segments: [
              for (final p in ArchivePeriod.values)
                ButtonSegment(value: p, label: Text(p.label)),
            ],
            selected: {period},
            onSelectionChanged: (value) {
              setState(() {
                period = value.first;
                periodDate = _day(DateTime.now());
              });
              _growChart();
            },
          ),
          SizedBox(height: 16),
          SizedBox(
            height: 200,
            child: AnimatedBuilder(
              animation: _chartAnimation,
              builder: (context, child) => CustomPaint(
                painter: _Chart(
                  values,
                  Theme.of(context).colorScheme.primary,
                  Theme.of(context).colorScheme.outlineVariant,
                  Curves.easeOutCubic.transform(_chartAnimation.value),
                ),
                child: child,
              ),
              child: Padding(
                padding: EdgeInsets.only(left: 24),
                child: Row(
                  children: [
                    for (var i = 0; i < values.length; i++)
                      Expanded(
                        child: Tooltip(
                          message:
                              '${_point(start, i)} · ${_duration(values[i])}',
                          child: SizedBox.expand(),
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),
          SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(_point(start, 0)),
              Text(_point(start, count ~/ 2)),
              Text(_point(start, count - 1)),
            ],
          ),
          SizedBox(height: 8),
          Text(
            UiText.t("专注时间 / 分钟"),
            style: Theme.of(context).textTheme.labelMedium,
          ),
        ],
      ),
    );
  }

  String _point(DateTime start, int index) => switch (period) {
    ArchivePeriod.day => '${_two(index)}:00',
    ArchivePeriod.week =>
      '${start.add(Duration(days: index)).month}/${start.add(Duration(days: index)).day}',
    ArchivePeriod.month => UiText.f("{0} 日", [index + 1]),
    ArchivePeriod.year => UiText.f("{0} 月", [index + 1]),
  };
  void _shift(int amount) {
    setState(() {
      periodDate = switch (period) {
        ArchivePeriod.day => periodDate.add(Duration(days: amount)),
        ArchivePeriod.week => periodDate.add(Duration(days: amount * 7)),
        ArchivePeriod.month => DateTime(
          periodDate.year,
          periodDate.month + amount,
        ),
        ArchivePeriod.year => DateTime(
          periodDate.year + amount,
          periodDate.month,
        ),
      };
    });
    _growChart();
  }

  Widget _heatmap(BuildContext context, List<FocusLog> logs) {
    final start = DateTime(year);
    final end = DateTime(year + 1);
    final offset = start.weekday - 1;
    final weeks = ((end.difference(start).inDays + offset) / 7).ceil();
    final scheme = Theme.of(context).colorScheme;
    final dailySeconds = <DateTime, int>{};
    for (final log in logs) {
      if (log.at.year == year) {
        final day = _day(log.at);
        dailySeconds.update(
          day,
          (value) => value + log.seconds,
          ifAbsent: () => log.seconds,
        );
      }
    }
    final total = logs
        .where((log) => log.at.year == year)
        .fold<int>(0, (sum, log) => sum + log.seconds);
    Color shade(int seconds) => seconds == 0
        ? scheme.surfaceContainerHighest
        : Color.lerp(
            scheme.surfaceContainerHighest,
            scheme.primary,
            seconds >= 7200
                ? 1
                : seconds >= 3600
                ? .8
                : seconds >= 1500
                ? .6
                : .38,
          )!;
    return _Panel(
      UiText.t("专注热力图"),
      Icons.grid_view_outlined,
      _Pager(
        UiText.f("{0} 年", [year]),
        () => setState(() => year--),
        () => setState(() => year++),
      ),
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: _HeatmapSpotlight(
              color: scheme.primary,
              child: Row(
                children: [
                  for (var week = 0; week < weeks; week++)
                    Padding(
                      padding: EdgeInsets.only(right: 3),
                      child: Column(
                        children: [
                          for (var weekday = 0; weekday < 7; weekday++)
                            Padding(
                              padding: EdgeInsets.only(bottom: 3),
                              child: Builder(
                                builder: (context) {
                                  final date = start.add(
                                    Duration(days: week * 7 + weekday - offset),
                                  );
                                  final inside = date.year == year;
                                  final seconds = inside
                                      ? (dailySeconds[date] ?? 0)
                                      : 0;
                                  return ExcludeSemantics(
                                    child: Tooltip(
                                      message: inside
                                          ? '${_date(date)} · ${_duration(seconds)}'
                                          : '',
                                      child: Container(
                                        width: 11,
                                        height: 11,
                                        decoration: BoxDecoration(
                                          color: inside
                                              ? shade(seconds)
                                              : scheme.surfaceContainerLow,
                                          borderRadius: BorderRadius.circular(
                                            3,
                                          ),
                                        ),
                                      ),
                                    ),
                                  );
                                },
                              ),
                            ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ),
          SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: Text(
                  UiText.f("全年累计 {0}", [_duration(total)]),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              Text(UiText.t("少")),
              SizedBox(width: 6),
              for (final seconds in [0, 600, 1500, 3600, 7200])
                Padding(
                  padding: EdgeInsets.only(right: 3),
                  child: Container(
                    width: 13,
                    height: 13,
                    decoration: BoxDecoration(
                      color: shade(seconds),
                      borderRadius: BorderRadius.circular(3),
                    ),
                  ),
                ),
              Text(UiText.t("多")),
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _postcard(BuildContext context) async {
    await showDialog<void>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, update) {
          final now = DateTime.now();
          final todays = widget.controller.logs
              .where((log) => _same(log.at, now))
              .toList();
          return AlertDialog(
            title: Text(UiText.t("保存与分享")),
            content: SizedBox(
              width: 340,
              height: 213,
              child: FittedBox(
                fit: BoxFit.contain,
                child: RepaintBoundary(
                  key: _postcardKey,
                  child: FocusPostcard(
                    date: now,
                    logs: todays,
                    tasks: widget.controller.activeTasks,
                    profile: widget.profile,
                  ),
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(dialogContext),
                child: Text(UiText.t("关闭")),
              ),
              OutlinedButton.icon(
                onPressed: exporting
                    ? null
                    : () async {
                        update(() => exporting = true);
                        try {
                          await _save(context);
                        } finally {
                          update(() => exporting = false);
                        }
                      },
                icon: Icon(Icons.download_outlined),
                label: Text(UiText.t("保存相册")),
              ),
              FilledButton.icon(
                onPressed: exporting
                    ? null
                    : () async {
                        update(() => exporting = true);
                        try {
                          await _share(context);
                        } finally {
                          update(() => exporting = false);
                        }
                      },
                icon: Icon(Icons.share_outlined),
                label: Text(UiText.t("系统分享")),
              ),
            ],
          );
        },
      ),
    );
  }

  Future<Uint8List> _bytes() async {
    await WidgetsBinding.instance.endOfFrame;
    final boundary =
        _postcardKey.currentContext!.findRenderObject()
            as RenderRepaintBoundary;
    final image = await boundary.toImage(pixelRatio: 2);
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    image.dispose();
    return data!.buffer.asUint8List();
  }

  Future<void> _share(BuildContext context) async {
    try {
      final bytes = await _bytes();
      await SharePlus.instance.share(
        ShareParams(
          files: [XFile.fromData(bytes, mimeType: 'image/png')],
          fileNameOverrides: ['Tomatotodo-${_date(DateTime.now())}.png'],
          title: UiText.t("Tomatotodo 专注记录"),
        ),
      );
    } catch (_) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(UiText.t("分享未完成，请重试"))));
      }
    }
  }

  Future<void> _save(BuildContext context) async {
    try {
      final bytes = await _bytes();
      if (kIsWeb) {
        await SharePlus.instance.share(
          ShareParams(
            files: [XFile.fromData(bytes, mimeType: 'image/png')],
            fileNameOverrides: ['Tomatotodo-${_date(DateTime.now())}.png'],
          ),
        );
      } else {
        await Gal.putImageBytes(
          bytes,
          name: 'Tomatotodo-${_date(DateTime.now())}',
        );
      }
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(kIsWeb ? UiText.t("图片已交给浏览器保存") : UiText.t("已保存到相册")),
          ),
        );
      }
    } catch (_) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(UiText.t("保存失败，请检查相册权限"))));
      }
    }
  }
}

class _Metric extends StatelessWidget {
  const _Metric(this.label, this.value, this.icon, this.motif);
  final String label, value;
  final IconData icon;
  final int motif;
  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    final backgrounds = [
      scheme.primaryContainer,
      scheme.tertiaryContainer,
      scheme.secondaryContainer,
      scheme.surfaceContainerHigh,
    ];
    final foregrounds = [
      scheme.onPrimaryContainer,
      scheme.onTertiaryContainer,
      scheme.onSecondaryContainer,
      scheme.onSurface,
    ];
    final foreground = foregrounds[motif];
    return Semantics(
      label: '$label，$value',
      child: DecoratedBox(
        decoration: BoxDecoration(color: backgrounds[motif]),
        child: Stack(
          fit: StackFit.expand,
          children: [
            CustomPaint(painter: _MetricMotifPainter(motif, foreground)),
            Padding(
              padding: EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(icon, size: 20, color: foreground),
                      SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          label,
                          maxLines: 1,
                          overflow: TextOverflow.fade,
                          style: Theme.of(context).textTheme.labelLarge
                              ?.copyWith(color: foreground),
                        ),
                      ),
                    ],
                  ),
                  Spacer(),
                  Text(
                    value,
                    maxLines: 2,
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      color: foreground,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MetricMotifPainter extends CustomPainter {
  const _MetricMotifPainter(this.motif, this.color);
  final int motif;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    // A fixed illustration coordinate space keeps the artwork undistorted
    // while the carousel masks and reveals each card.
    final soft = Paint()..color = color.withValues(alpha: .10);
    final medium = Paint()..color = color.withValues(alpha: .18);
    final strong = Paint()..color = color.withValues(alpha: .29);
    final line = Paint()
      ..color = color.withValues(alpha: .30)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    final scale = size.height / 168;
    canvas.save();
    canvas.translate(size.width - 132 * scale, size.height - 136 * scale);
    canvas.scale(scale);
    switch (motif) {
      case 0:
        canvas.drawCircle(Offset(93, 35), 22, medium);
        canvas.drawPath(
          Path()
            ..moveTo(-10, 136)
            ..cubicTo(18, 106, 31, 38, 67, 67)
            ..cubicTo(88, 86, 106, 58, 149, 99)
            ..lineTo(149, 145)
            ..close(),
          soft,
        );
        canvas.drawPath(
          Path()
            ..moveTo(-5, 141)
            ..cubicTo(34, 118, 60, 75, 91, 95)
            ..cubicTo(112, 109, 129, 96, 150, 86)
            ..lineTo(150, 145)
            ..close(),
          medium,
        );
        canvas.drawPath(
          Path()
            ..moveTo(30, 133)
            ..quadraticBezierTo(73, 113, 106, 120),
          line..strokeWidth = 2,
        );
      case 1:
        canvas.drawCircle(Offset(75, 74), 55, soft);
        // Soft shoulders, a small stem notch and a broad rounded base.
        final fruit = Path()
          ..moveTo(72, 49)
          ..cubicTo(53, 34, 22, 45, 24, 77)
          ..cubicTo(26, 106, 45, 119, 72, 119)
          ..cubicTo(101, 119, 119, 102, 120, 77)
          ..cubicTo(121, 46, 92, 34, 72, 49)
          ..close();
        canvas.drawPath(fruit, medium);
        canvas.drawPath(
          Path()
            ..moveTo(73, 46)
            ..cubicTo(65, 36, 54, 33, 45, 35)
            ..quadraticBezierTo(50, 47, 61, 51)
            ..quadraticBezierTo(49, 53, 44, 62)
            ..quadraticBezierTo(61, 64, 72, 54)
            ..quadraticBezierTo(82, 66, 100, 63)
            ..quadraticBezierTo(96, 52, 83, 50)
            ..quadraticBezierTo(98, 45, 103, 35)
            ..quadraticBezierTo(84, 32, 73, 46)
            ..close(),
          strong,
        );
        canvas.drawPath(
          Path()
            ..moveTo(73, 48)
            ..quadraticBezierTo(70, 33, 79, 26),
          line..strokeWidth = 5,
        );
        canvas.drawPath(
          Path()
            ..moveTo(41, 72)
            ..quadraticBezierTo(39, 84, 46, 92),
          line..strokeWidth = 3,
        );
      case 2:
        canvas.drawCircle(Offset(75, 72), 53, soft);
        canvas.drawPath(
          Path()
            ..moveTo(61, 62)
            ..lineTo(29, 117)
            ..lineTo(103, 117)
            ..lineTo(90, 64)
            ..close(),
          soft,
        );
        canvas.drawPath(
          Path()
            ..moveTo(91, 113)
            ..lineTo(101, 76)
            ..lineTo(79, 43),
          line..strokeWidth = 5,
        );
        canvas.drawPath(
          Path()
            ..moveTo(52, 39)
            ..quadraticBezierTo(65, 29, 79, 39)
            ..lineTo(93, 61)
            ..quadraticBezierTo(72, 72, 45, 62)
            ..close(),
          strong,
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromLTWH(66, 113, 49, 8),
            Radius.circular(4),
          ),
          strong,
        );
        canvas.drawLine(
          Offset(21, 123),
          Offset(123, 123),
          line..strokeWidth = 2,
        );
      case 3:
        canvas.drawCircle(Offset(77, 72), 53, soft);
        for (var i = 0; i < 3; i++) {
          final left = i.isOdd ? 35.0 : 25.0;
          final top = 58.0 + i * 21;
          canvas.drawRRect(
            RRect.fromRectAndRadius(
              Rect.fromLTWH(left, top, 86, 18),
              Radius.circular(5),
            ),
            i.isOdd ? medium : strong,
          );
          canvas.drawLine(
            Offset(left + 15, top + 9),
            Offset(left + 75, top + 9),
            Paint()
              ..color = color.withValues(alpha: .13)
              ..strokeWidth = 2
              ..strokeCap = StrokeCap.round,
          );
        }
        canvas.drawPath(
          Path()
            ..moveTo(92, 58)
            ..lineTo(92, 76)
            ..lineTo(97, 72)
            ..lineTo(102, 76)
            ..lineTo(102, 58)
            ..close(),
          strong,
        );
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _MetricMotifPainter oldDelegate) =>
      oldDelegate.motif != motif || oldDelegate.color != color;
}

class _Panel extends StatelessWidget {
  const _Panel(
    this.title,
    this.icon,
    this.trailing,
    this.child, {
    this.fillBody = false,
  });
  final bool fillBody;
  final String title;
  final IconData icon;
  final Widget trailing, child;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => Card.outlined(
      child: Padding(
        padding: EdgeInsets.all(14),
        child: LayoutBuilder(
          builder: (context, box) => Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(icon, color: Theme.of(context).colorScheme.primary),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      title,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                  trailing,
                ],
              ),
              SizedBox(height: 12),
              if (box.hasBoundedHeight)
                Expanded(
                  child: fillBody
                      ? SizedBox.expand(child: child)
                      : Center(child: child),
                )
              else
                child,
            ],
          ),
        ),
      ),
    ),
  );
}

class _Pager extends StatelessWidget {
  const _Pager(this.label, this.back, this.next);
  final String label;
  final VoidCallback back, next;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        IconButton(
          tooltip: UiText.t("上一页"),
          onPressed: back,
          icon: Icon(Icons.chevron_left),
        ),
        Text(label, style: Theme.of(context).textTheme.labelMedium),
        IconButton(
          tooltip: UiText.t("下一页"),
          onPressed: next,
          icon: Icon(Icons.chevron_right),
        ),
      ],
    ),
  );
}

class _HeatmapSpotlight extends StatefulWidget {
  const _HeatmapSpotlight({required this.color, required this.child});
  final Color color;
  final Widget child;

  @override
  State<_HeatmapSpotlight> createState() => _HeatmapSpotlightState();
}

class _HeatmapSpotlightState extends State<_HeatmapSpotlight> {
  Offset? point;

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => MouseRegion(
      onHover: (event) => setState(() => point = event.localPosition),
      onExit: (_) => setState(() => point = null),
      child: ClipRect(
        child: Stack(
          children: [
            widget.child,
            if (point != null)
              Positioned(
                left: point!.dx - 76,
                top: point!.dy - 76,
                child: IgnorePointer(
                  child: Container(
                    width: 152,
                    height: 152,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      gradient: RadialGradient(
                        colors: [
                          widget.color.withValues(alpha: .24),
                          widget.color.withValues(alpha: .08),
                          widget.color.withValues(alpha: 0),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    ),
  );
}

class _Chart extends CustomPainter {
  const _Chart(this.values, this.accent, this.grid, this.progress);
  final List<int> values;
  final Color accent, grid;
  final double progress;
  @override
  void paint(Canvas canvas, Size size) {
    const left = 24.0;
    final plotWidth = size.width - left - 4;
    final plotHeight = size.height - 10;
    final maxMinutes =
        values.fold<int>(0, (max, value) => value > max ? value : max) / 60;
    var ceiling = 40.0;
    while (ceiling < maxMinutes) {
      ceiling *= 2;
    }
    final gridPaint = Paint()
      ..color = grid
      ..strokeWidth = 1;
    for (var i = 0; i <= 4; i++) {
      final y = plotHeight * (1 - i / 4);
      canvas.drawLine(Offset(left, y), Offset(size.width, y), gridPaint);
      final text = TextPainter(
        text: TextSpan(
          text: (ceiling * i / 4).round().toString(),
          style: TextStyle(color: grid, fontSize: 9),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      text.paint(canvas, Offset(0, y - 6));
    }
    final path = Path();
    final area = Path();
    final line = Paint()
      ..color = accent
      ..strokeWidth = 2.5
      ..style = PaintingStyle.stroke;
    final dot = Paint()..color = accent.withValues(alpha: progress);
    final points = <Offset>[];
    for (var i = 0; i < values.length; i++) {
      final x =
          left + (values.length == 1 ? 0 : plotWidth * i / (values.length - 1));
      final y = plotHeight * (1 - (values[i] / 60) / ceiling * progress);
      if (i == 0) {
        path.moveTo(x, y);
        area.moveTo(x, plotHeight);
        area.lineTo(x, y);
      } else {
        path.lineTo(x, y);
        area.lineTo(x, y);
      }
      points.add(Offset(x, y));
    }
    area.lineTo(left + plotWidth, plotHeight);
    area.close();
    canvas.drawPath(
      area,
      Paint()
        ..shader = LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [
            accent.withValues(alpha: .20 * progress),
            accent.withValues(alpha: .02 * progress),
          ],
        ).createShader(Rect.fromLTWH(left, 0, plotWidth, plotHeight))
        ..style = PaintingStyle.fill,
    );
    canvas.drawPath(path, line);
    if (values.length <= 31) {
      for (final point in points) {
        canvas.drawCircle(point, 2.5, dot);
      }
    }
  }

  @override
  bool shouldRepaint(covariant _Chart old) =>
      old.values != values ||
      old.accent != accent ||
      old.grid != grid ||
      old.progress != progress;
}
