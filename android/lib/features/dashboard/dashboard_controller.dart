import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../configuration/preset_schedule.dart';

enum TimerPhase { focus, shortBreak }

enum DashboardTile {
  analogTimer,
  digitalTimer,
  tasks,
  mode,
  countUp,
  countdown,
  quote,
  nextCourse,
  todayCourses,
  calendar,
}

extension DashboardTileDetails on DashboardTile {
  String get label => switch (this) {
    DashboardTile.analogTimer => UiText.t("圆形计时器"),
    DashboardTile.digitalTimer => UiText.t("数字计时器"),
    DashboardTile.tasks => UiText.t("任务清单"),
    DashboardTile.mode => UiText.t("专注与短休"),
    DashboardTile.countUp => UiText.t("正向计时"),
    DashboardTile.countdown => UiText.t("倒数日"),
    DashboardTile.quote => UiText.t("名言警句"),
    DashboardTile.nextCourse => UiText.t("接下来课程"),
    DashboardTile.todayCourses => UiText.t("今日课程"),
    DashboardTile.calendar => UiText.t("日历"),
  };
  int get columns =>
      this == DashboardTile.tasks ||
          this == DashboardTile.mode ||
          this == DashboardTile.countUp ||
          this == DashboardTile.countdown ||
          this == DashboardTile.todayCourses ||
          this == DashboardTile.calendar
      ? 1
      : 2;
  int get rows => this == DashboardTile.analogTimer ? 2 : 1;
}

/// Supported dashboard footprints, in columns and rows.
typedef TileSize = ({int columns, int rows});

List<TileSize> dashboardTileSizes(
  DashboardTile tile, {
  required bool tablet,
  required int columns,
}) {
  final sizes = switch (tile) {
    DashboardTile.analogTimer => [
      (columns: 2, rows: 2),
      if (tablet) (columns: 3, rows: 2),
    ],
    DashboardTile.nextCourse => [(columns: 2, rows: 1), (columns: 1, rows: 1)],
    DashboardTile.digitalTimer => [
      (columns: 2, rows: 1),
      if (tablet) (columns: 3, rows: 2),
      if (tablet) (columns: 4, rows: 2),
    ],
    DashboardTile.tasks || DashboardTile.todayCourses => [
      (columns: 1, rows: 1),
      (columns: 2, rows: 1),
      (columns: 2, rows: 2),
      (columns: 1, rows: 2),
      if (tablet) (columns: 3, rows: 2),
    ],
    DashboardTile.mode ||
    DashboardTile.countdown => [(columns: 1, rows: 1), (columns: 2, rows: 1)],
    DashboardTile.calendar => [
      (columns: 1, rows: 1),
      (columns: 2, rows: 1),
      (columns: 2, rows: 2),
      if (tablet) (columns: 3, rows: 2),
      if (tablet) (columns: 4, rows: 2),
    ],
    _ => [(columns: tile.columns, rows: tile.rows)],
  };
  return sizes.where((size) => size.columns <= columns).toList();
}

List<DashboardTile> reorderedDashboardTiles(
  List<DashboardTile> order,
  DashboardTile moved,
  DashboardTile target, {
  bool after = false,
}) {
  final result = List<DashboardTile>.of(order);
  if (moved == target || !result.contains(moved) || !result.contains(target)) {
    return result;
  }
  result.remove(moved);
  final insertion = result.indexOf(target) + (after ? 1 : 0);
  result.insert(insertion, moved);
  return result;
}

class FocusTask {
  const FocusTask({
    required this.id,
    required this.title,
    required this.presetId,
    this.subtitle = '',
    this.estimate = 0,
    this.done = false,
    this.completedBaseline = 0,
  });
  final String id;
  final String title;
  final String presetId;
  final String subtitle;
  final int estimate;
  final bool done;
  final int completedBaseline;
  FocusTask copyWith({
    String? title,
    String? subtitle,
    int? estimate,
    bool? done,
  }) => FocusTask(
    id: id,
    title: title ?? this.title,
    presetId: presetId,
    subtitle: subtitle ?? this.subtitle,
    estimate: estimate ?? this.estimate,
    done: done ?? this.done,
    completedBaseline: completedBaseline,
  );
  Map<String, Object?> toJson() => {
    'id': id,
    'title': title,
    'presetId': presetId,
    'subtitle': subtitle,
    'estimate': estimate,
    'done': done,
    'completedBaseline': completedBaseline,
  };
  static FocusTask? fromJson(Object? value) {
    if (value is! Map || value['id'] is! String || value['title'] is! String) {
      return null;
    }
    final title = (value['title'] as String).trim();
    if (title.isEmpty) return null;
    return FocusTask(
      id: value['id'] as String,
      title: title,
      presetId: value['presetId'] is String
          ? value['presetId'] as String
          : 'default',
      subtitle: value['subtitle'] is String ? value['subtitle'] as String : '',
      estimate: value['estimate'] is int
          ? (value['estimate'] as int).clamp(0, 99)
          : 0,
      done: value['done'] == true,
      completedBaseline: value['completedBaseline'] is int
          ? value['completedBaseline'] as int
          : 0,
    );
  }
}

class TaskPreset {
  const TaskPreset({
    required this.id,
    required this.name,
    this.dueAt,
    this.remindAt,
    this.repeat = 'none',
    this.repeatInterval = 1,
    this.repeatUnit = 'week',
    this.repeatDays = const [],
  });
  final String id;
  final String name;
  final DateTime? dueAt, remindAt;
  final String repeat, repeatUnit;
  final int repeatInterval;
  final List<int> repeatDays;
  static const Object _unchanged = Object();
  TaskPreset copyWith({
    String? name,
    Object? dueAt = _unchanged,
    Object? remindAt = _unchanged,
    String? repeat,
    int? repeatInterval,
    String? repeatUnit,
    List<int>? repeatDays,
  }) => TaskPreset(
    id: id,
    name: name ?? this.name,
    dueAt: identical(dueAt, _unchanged) ? this.dueAt : dueAt as DateTime?,
    remindAt: identical(remindAt, _unchanged)
        ? this.remindAt
        : remindAt as DateTime?,
    repeat: repeat ?? this.repeat,
    repeatInterval: repeatInterval ?? this.repeatInterval,
    repeatUnit: repeatUnit ?? this.repeatUnit,
    repeatDays: repeatDays ?? this.repeatDays,
  );
  Map<String, Object?> toJson() => {
    'id': id,
    'name': name,
    'dueAt': dueAt?.toIso8601String(),
    'remindAt': remindAt?.toIso8601String(),
    'repeat': repeat,
    'repeatInterval': repeatInterval,
    'repeatUnit': repeatUnit,
    'repeatDays': repeatDays,
  };
  static TaskPreset? fromJson(Object? value) =>
      value is Map && value['id'] is String && value['name'] is String
      ? TaskPreset(
          id: value['id'] as String,
          name: value['name'] as String,
          dueAt: DateTime.tryParse(value['dueAt']?.toString() ?? ''),
          remindAt: DateTime.tryParse(value['remindAt']?.toString() ?? ''),
          repeat:
              [
                'none',
                'daily',
                'workday',
                'weekly',
                'monthly',
                'yearly',
                'custom',
              ].contains(value['repeat'])
              ? value['repeat'] as String
              : 'none',
          repeatInterval: value['repeatInterval'] is int
              ? (value['repeatInterval'] as int).clamp(1, 365)
              : 1,
          repeatUnit:
              [
                'day',
                'week',
                'month',
                'year',
              ].contains(value['repeatUnit'])
              ? value['repeatUnit'] as String
              : 'week',
          repeatDays: value['repeatDays'] is List
              ? (value['repeatDays'] as List)
                    .whereType<int>()
                    .where((d) => d >= 1 && d <= 7)
                    .toSet()
                    .toList()
              : [],
        )
      : null;
}

class TaskHistoryEntry {
  const TaskHistoryEntry({
    required this.id,
    required this.title,
    required this.at,
    required this.completed,
    this.status = 'deleted',
  });
  final String id;
  final String title;
  final DateTime at;
  final bool completed;
  final String status;
  Map<String, Object?> toJson() => {
    'id': id,
    'title': title,
    'at': at.toIso8601String(),
    'completed': completed,
    'status': status,
  };
  static TaskHistoryEntry? fromJson(Object? value) {
    if (value is! Map || value['id'] is! String || value['title'] is! String) {
      return null;
    }
    final at = DateTime.tryParse(value['at']?.toString() ?? '');
    return at == null
        ? null
        : TaskHistoryEntry(
            id: value['id'] as String,
            title: value['title'] as String,
            at: at,
            completed: value['completed'] == true,
            status: value['status'] == 'expired'
                ? 'expired'
                : value['completed'] == true
                ? 'completed'
                : 'deleted',
          );
  }
}

class FocusLog {
  const FocusLog({
    required this.at,
    required this.seconds,
    required this.taskTitle,
    required this.completedSession,
    this.taskId,
    this.id,
  });
  final DateTime at;
  final int seconds;
  final String taskTitle;
  final bool completedSession;
  final String? taskId;
  final String? id;
  Map<String, Object?> toJson() => {
    'at': at.toIso8601String(),
    'seconds': seconds,
    'taskTitle': taskTitle,
    'completedSession': completedSession,
    'taskId': taskId,
    if (id != null) 'id': id,
  };
  static FocusLog? fromJson(Object? value) {
    if (value is! Map) return null;
    final at = DateTime.tryParse(value['at']?.toString() ?? '');
    final seconds = value['seconds'];
    if (at == null || seconds is! int || seconds <= 0) return null;
    return FocusLog(
      id: value['id'] as String?,
      at: at,
      seconds: seconds,
      taskTitle: value['taskTitle'] is String
          ? value['taskTitle'] as String
          : UiText.t("未指定任务"),
      completedSession: value['completedSession'] == true,
      taskId: value['taskId'] is String ? value['taskId'] as String : null,
    );
  }
}

/// A single clock drives every dashboard timer view and the fixed controls.
class DashboardController extends ChangeNotifier {
  DashboardController(this._preferences, {DateTime Function()? now})
    : _now = now ?? DateTime.now {
    _restore();
    checkPresetDeadlines();
    _ticker = Timer.periodic(Duration(milliseconds: 250), (_) => _tick());
  }
  static final _storageKey = 'mobile_dashboard_v1';
  final SharedPreferences _preferences;
  final DateTime Function() _now;
  Timer? _ticker;
  DateTime? _lastDeadlineCheck;
  void Function()? onCompletedFocus;
  void Function()? onCompletedBreak;
  TimerPhase phase = TimerPhase.focus;
  bool countUp = false;
  bool isRunning = false;
  bool shortBreakEnabled = true;
  bool automaticCycle = true;
  int focusMinutes = 25;
  int shortMinutes = 5;
  int _elapsedBase = 0;
  int _loggedElapsed = 0;
  DateTime? _startedAt;
  String? activeTaskId;
  String activePresetId = 'default';
  final List<TaskPreset> presets = [
    TaskPreset(id: 'default', name: UiText.t("我的一天")),
    TaskPreset(id: 'important', name: UiText.t("重要")),
    TaskPreset(id: 'planned', name: UiText.t("计划内")),
  ];
  final List<TaskHistoryEntry> taskHistory = [];
  String countdownName = '';
  DateTime? countdownDate;
  final List<FocusTask> tasks = [];
  final List<FocusLog> logs = [];
  final Map<DashboardTile, TileSize> tileSizes = {};

  TileSize tileSize(
    DashboardTile tile, {
    required bool tablet,
    required int columns,
  }) {
    final allowed = dashboardTileSizes(tile, tablet: tablet, columns: columns);
    final saved = tileSizes[tile];
    if (saved == null) return allowed.first;
    if (allowed.contains(saved)) return saved;
    final smaller =
        allowed
            .where((s) => s.columns <= saved.columns && s.rows <= saved.rows)
            .toList()
          ..sort((a, b) => (b.columns * b.rows).compareTo(a.columns * a.rows));
    return smaller.isEmpty ? allowed.first : smaller.first;
  }

  void setTileSize(DashboardTile tile, TileSize size) {
    if (!dashboardTileSizes(tile, tablet: true, columns: 6).contains(size) ||
        tileSizes[tile] == size) {
      return;
    }
    tileSizes[tile] = size;
    _changed();
  }

  Map<String, Object?> _exportTileSizes() => {
    for (final entry in tileSizes.entries)
      entry.key.name: {
        'columns': entry.value.columns,
        'rows': entry.value.rows,
      },
  };

  final List<DashboardTile> visibleTiles = [
    DashboardTile.analogTimer,
    DashboardTile.tasks,
    DashboardTile.countUp,
    DashboardTile.mode,
    DashboardTile.countdown,
    DashboardTile.quote,
    DashboardTile.nextCourse,
    DashboardTile.todayCourses,
  ];

  int get durationSeconds =>
      (phase == TimerPhase.focus ? focusMinutes : shortMinutes) * 60;
  int get elapsedSeconds {
    final live = isRunning && _startedAt != null
        ? _clockNow().difference(_startedAt!).inSeconds
        : 0;
    return (_elapsedBase + live).clamp(0, countUp ? 8640000 : durationSeconds);
  }

  int get displaySeconds => countUp
      ? elapsedSeconds
      : (durationSeconds - elapsedSeconds).clamp(0, durationSeconds);
  double get progress => progressAt(smooth: false);

  double progressAt({required bool smooth}) {
    final live = isRunning && _startedAt != null
        ? _clockNow().difference(_startedAt!).inMilliseconds / 1000
        : 0.0;
    final elapsed = (_elapsedBase + (smooth ? live : live.floorToDouble()))
        .clamp(0.0, countUp ? 8640000.0 : durationSeconds.toDouble());
    if (countUp) {
      final cycle = focusMinutes * 60;
      return (elapsed % cycle) / cycle;
    }
    return (elapsed / durationSeconds).clamp(0.0, 1.0);
  }

  FocusTask? get activeTask {
    for (final task in tasks) {
      if (task.id == activeTaskId) return task;
    }
    return null;
  }

  int get totalFocusSeconds => logs.fold(0, (sum, log) => sum + log.seconds);
  int get completedSessions => logs.where((log) => log.completedSession).length;
  int tomatoesForTask(FocusTask task) {
    final uniqueTitle =
        tasks.where((item) => item.title == task.title).length == 1;
    return task.completedBaseline +
        logs
            .where(
              (log) =>
                  log.completedSession &&
                  (log.taskId == task.id ||
                      (log.taskId == null &&
                          uniqueTitle &&
                          log.taskTitle == task.title)),
            )
            .length;
  }

  List<FocusTask> get activeTasks =>
      tasks.where((task) => task.presetId == activePresetId).toList();
  List<FocusTask> tasksFor(String presetId) =>
      tasks.where((task) => task.presetId == presetId).toList();
  int get completedTasks => activeTasks.where((task) => task.done).length;
  int? get countdownDays {
    final target = countdownDate;
    if (target == null) return null;
    final now = _clockNow();
    return DateTime.utc(
      target.year,
      target.month,
      target.day,
    ).difference(DateTime.utc(now.year, now.month, now.day)).inDays;
  }

  bool externalTimerDriver = false;
  int widgetEventId = 0;
  DateTime? _widgetEventTime;
  DateTime _clockNow() => _widgetEventTime ?? _now();

  Map<String, Object?> get widgetRuntime => {
    'phase': phase.name,
    'countUp': countUp,
    'running': isRunning,
    'focusMinutes': focusMinutes,
    'shortMinutes': shortMinutes,
    'shortBreakEnabled': shortBreakEnabled,
    'automaticCycle': automaticCycle,
    'targetRemaining': activeTask != null && activeTask!.estimate > 0
        ? (activeTask!.estimate - tomatoesForTask(activeTask!)).clamp(0, 99)
        : -1,
    'elapsed': _elapsedBase,
    'loggedElapsed': _loggedElapsed,
    'startedAt': _startedAt?.millisecondsSinceEpoch,
    'activeTaskId': activeTaskId,
    'activePresetId': activePresetId,
  };

  void restoreWidgetRuntime(Map<String, dynamic> runtime) {
    phase = runtime['phase'] == 'shortBreak'
        ? TimerPhase.shortBreak
        : TimerPhase.focus;
    countUp = runtime['countUp'] == true;
    isRunning = runtime['running'] == true;
    focusMinutes = _intInRange(runtime['focusMinutes'], focusMinutes, 1, 180);
    shortMinutes = _intInRange(runtime['shortMinutes'], shortMinutes, 1, 60);
    shortBreakEnabled = runtime['shortBreakEnabled'] != false;
    automaticCycle = runtime['automaticCycle'] != false;
    _elapsedBase = _intInRange(runtime['elapsed'], 0, 0, 8640000);
    _loggedElapsed = _intInRange(runtime['loggedElapsed'], 0, 0, 8640000);
    _startedAt = runtime['startedAt'] is int
        ? DateTime.fromMillisecondsSinceEpoch(runtime['startedAt'] as int)
        : null;
    final id = runtime['activeTaskId'];
    if (id is String && tasks.any((task) => task.id == id)) activeTaskId = id;
    final preset = runtime['activePresetId'];
    if (preset is String && presets.any((item) => item.id == preset)) {
      activePresetId = preset;
    }
    notifyListeners();
  }

  Future<void> applyWidgetEvents(
    List<dynamic> events,
    Map<String, dynamic>? runtime,
  ) async {
    for (final raw in events) {
      final event = Map<String, dynamic>.from(raw as Map);
      final id = event['id'] as int;
      if (id <= widgetEventId) continue;
      _widgetEventTime = DateTime.fromMillisecondsSinceEpoch(
        event['at'] as int,
      );
      try {
        if (event['action'] != 'task') {
          restoreWidgetRuntime(
            Map<String, dynamic>.from(event['before'] as Map),
          );
        }
        switch (event['action']) {
          case 'complete':
            _completePhase();
          case 'toggle':
            toggleRunning();
          case 'reset':
            reset();
          case 'task':
            final taskId = event['taskId'];
            if (taskId is String) toggleTaskDone(taskId);
        }
        widgetEventId = id;
      } finally {
        _widgetEventTime = null;
      }
    }
    if (runtime != null) restoreWidgetRuntime(runtime);
    await _persist();
  }

  void _tick() {
    final now = _clockNow();
    if (_lastDeadlineCheck == null ||
        now.difference(_lastDeadlineCheck!).inSeconds >= 15) {
      _lastDeadlineCheck = now;
      checkPresetDeadlines();
    }
    if (!isRunning) return;
    if (!countUp && elapsedSeconds >= durationSeconds) {
      if (!externalTimerDriver) _completePhase();
    } else {
      notifyListeners();
    }
  }

  void toggleRunning() {
    if (isRunning) {
      _freeze();
      _commitFocus();
      isRunning = false;
    } else {
      if (!countUp && _elapsedBase >= durationSeconds) _elapsedBase = 0;
      _startedAt = _clockNow();
      isRunning = true;
    }
    _changed();
  }

  void reset() {
    _freeze();
    _commitFocus();
    isRunning = false;
    _startedAt = null;
    _elapsedBase = 0;
    _loggedElapsed = 0;
    _changed();
  }

  void choosePhase(TimerPhase next) {
    if (countUp || (next == TimerPhase.shortBreak && !shortBreakEnabled)) {
      return;
    }
    _freeze();
    _commitFocus();
    isRunning = false;
    phase = next;
    _elapsedBase = 0;
    _loggedElapsed = 0;
    _startedAt = null;
    _changed();
  }

  void setCountUp(bool enabled) {
    if (countUp == enabled) return;
    _freeze();
    _commitFocus();
    isRunning = false;
    countUp = enabled;
    phase = TimerPhase.focus;
    _elapsedBase = 0;
    _loggedElapsed = 0;
    _startedAt = null;
    _changed();
  }

  void setAutomaticCycle(bool enabled) {
    automaticCycle = enabled;
    _changed();
  }

  void setShortBreakEnabled(bool enabled) {
    shortBreakEnabled = enabled;
    if (!enabled && phase == TimerPhase.shortBreak) {
      reset();
      phase = TimerPhase.focus;
    }
    _changed();
  }

  void setDurations({
    required int focus,
    required int shortBreak,
    required bool enableShortBreak,
  }) {
    if (focus < 1 || focus > 180 || shortBreak < 1 || shortBreak > 60) return;
    reset();
    focusMinutes = focus;
    shortMinutes = shortBreak;
    shortBreakEnabled = enableShortBreak;
    if (!enableShortBreak && phase == TimerPhase.shortBreak) {
      phase = TimerPhase.focus;
    }
    _changed();
  }

  void _freeze() {
    if (!isRunning) return;
    _elapsedBase = elapsedSeconds;
    _startedAt = null;
  }

  void _commitFocus({bool completed = false}) {
    if (phase != TimerPhase.focus) return;
    final delta = _elapsedBase - _loggedElapsed;
    if (delta <= 0) return;
    logs.insert(
      0,
      FocusLog(
        id: '${DateTime.now().microsecondsSinceEpoch}-${Random.secure().nextInt(1 << 32)}',
        at: _clockNow(),
        seconds: delta,
        taskTitle: activeTask?.title ?? UiText.t("未指定任务"),
        completedSession: completed,
        taskId: activeTask?.id,
      ),
    );
    _loggedElapsed = _elapsedBase;
  }

  void _completePhase() {
    _elapsedBase = durationSeconds;
    _startedAt = null;
    final wasFocus = phase == TimerPhase.focus;
    if (wasFocus) _commitFocus(completed: true);
    if (wasFocus && _widgetEventTime == null) onCompletedFocus?.call();
    if (!wasFocus && _widgetEventTime == null) onCompletedBreak?.call();
    if (wasFocus && shortBreakEnabled) {
      phase = TimerPhase.shortBreak;
    } else {
      phase = TimerPhase.focus;
    }
    _elapsedBase = 0;
    _loggedElapsed = 0;
    final task = activeTask;
    final targetReached =
        task != null &&
        task.estimate > 0 &&
        tomatoesForTask(task) >= task.estimate;
    isRunning =
        automaticCycle &&
        (wasFocus ? (shortBreakEnabled || !targetReached) : !targetReached);
    _startedAt = isRunning ? _clockNow() : null;
    _changed();
  }

  void selectTask(String id) {
    if (id == activeTaskId || !tasks.any((task) => task.id == id)) return;
    _freeze();
    _commitFocus();
    if (isRunning) _startedAt = _clockNow();
    activeTaskId = id;
    activePresetId = tasks.firstWhere((task) => task.id == id).presetId;
    _changed();
  }

  void addTask(
    String title, {
    String? presetId,
    String subtitle = '',
    int estimate = 0,
  }) {
    final trimmed = title.trim();
    if (trimmed.isEmpty) return;
    var taskId = _clockNow().microsecondsSinceEpoch;
    while (tasks.any((task) => task.id == '$taskId')) {
      taskId++;
    }
    final task = FocusTask(
      id: '$taskId',
      title: trimmed,
      presetId: presetId ?? activePresetId,
      subtitle: subtitle.trim(),
      estimate: estimate.clamp(0, 99),
    );
    tasks.add(task);
    if (task.presetId == activePresetId) activeTaskId ??= task.id;
    _changed();
  }

  void renameTask(String id, String title) {
    final trimmed = title.trim();
    final index = tasks.indexWhere((task) => task.id == id);
    if (index < 0 || trimmed.isEmpty) return;
    tasks[index] = tasks[index].copyWith(title: trimmed);
    _changed();
  }

  void updateTask(String id, String title, String subtitle, int estimate) {
    final index = tasks.indexWhere((task) => task.id == id);
    if (index < 0 || title.trim().isEmpty) return;
    tasks[index] = tasks[index].copyWith(
      title: title.trim(),
      subtitle: subtitle.trim(),
      estimate: estimate.clamp(0, 99),
    );
    _changed();
  }

  void reorderTasks(String presetId, int oldIndex, int newIndex) {
    final ordered = tasksFor(presetId);
    if (oldIndex < 0 || oldIndex >= ordered.length) return;
    final moved = ordered.removeAt(oldIndex);
    ordered.insert(newIndex.clamp(0, ordered.length), moved);
    tasks.removeWhere((task) => task.presetId == presetId);
    tasks.addAll(ordered);
    _changed();
  }

  void addPreset(String name) {
    if (name.trim().isEmpty) return;
    presets.add(
      TaskPreset(
        id: 'preset_${_clockNow().microsecondsSinceEpoch}_${presets.length}',
        name: name.trim(),
      ),
    );
    _changed();
  }

  void renamePreset(String id, String name) {
    final index = presets.indexWhere((preset) => preset.id == id);
    if (index < 0 || name.trim().isEmpty) return;
    presets[index] = presets[index].copyWith(name: name.trim());
    _changed();
  }

  void updatePresetSchedule(
    String id, {
    Object? dueAt = TaskPreset._unchanged,
    Object? remindAt = TaskPreset._unchanged,
    String? repeat,
    int? repeatInterval,
    String? repeatUnit,
    List<int>? repeatDays,
  }) {
    final index = presets.indexWhere((preset) => preset.id == id);
    if (index < 0) return;
    presets[index] = presets[index].copyWith(
      dueAt: dueAt,
      remindAt: remindAt,
      repeat: repeat,
      repeatInterval: repeatInterval,
      repeatUnit: repeatUnit,
      repeatDays: repeatDays,
    );
    _changed();
  }

  void checkPresetDeadlines() {
    final now = _clockNow();
    var changed = false;
    for (var i = 0; i < presets.length; i++) {
      final preset = presets[i];
      final due = preset.dueAt;
      if (due == null || due.isAfter(now)) continue;
      for (final task in tasksFor(
        preset.id,
      ).where((task) => !task.done).toList()) {
        if (activeTaskId == task.id) {
          _freeze();
          _commitFocus();
          if (isRunning) _startedAt = now;
          activeTaskId = null;
        }
        tasks.removeWhere((item) => item.id == task.id);
        taskHistory.insert(
          0,
          TaskHistoryEntry(
            id: '${task.id}_expired_${now.microsecondsSinceEpoch}',
            title: task.title,
            at: now,
            completed: false,
            status: 'expired',
          ),
        );
      }
      var next = nextPresetDue(
        due,
        repeat: preset.repeat,
        interval: preset.repeatInterval,
        unit: preset.repeatUnit,
        weekdays: preset.repeatDays,
      );
      var attempts = 0;
      while (next != null && !next.isAfter(now) && attempts++ < 4000) {
        next = nextPresetDue(
          next,
          repeat: preset.repeat,
          interval: preset.repeatInterval,
          unit: preset.repeatUnit,
          weekdays: preset.repeatDays,
        );
      }
      if (next != null && !next.isAfter(now)) next = null;
      final remind = next == null || preset.remindAt == null
          ? null
          : next.add(preset.remindAt!.difference(due));
      presets[i] = preset.copyWith(dueAt: next, remindAt: remind);
      if (next != null) {
        for (var j = 0; j < tasks.length; j++) {
          if (tasks[j].presetId == preset.id && tasks[j].done) {
            tasks[j] = tasks[j].copyWith(done: false);
          }
        }
      }
      if (activePresetId == preset.id) {
        activeTaskId ??= tasksFor(preset.id).firstOrNull?.id;
      }
      changed = true;
    }
    if (changed) _changed();
  }

  void selectPreset(String id) {
    if (!presets.any((preset) => preset.id == id) || activePresetId == id) {
      return;
    }
    _freeze();
    _commitFocus();
    if (isRunning) _startedAt = _clockNow();
    activePresetId = id;
    activeTaskId = tasksFor(id).firstOrNull?.id;
    _changed();
  }

  void deletePreset(String id) {
    if (presets.length <= 1) return;
    for (final task in tasksFor(id)) {
      deleteTask(task.id);
    }
    presets.removeWhere((preset) => preset.id == id);
    if (activePresetId == id) {
      activePresetId = presets.first.id;
      activeTaskId = tasksFor(activePresetId).firstOrNull?.id;
    }
    _changed();
  }

  void reorderPresets(int oldIndex, int newIndex) {
    if (oldIndex < 0 || oldIndex >= presets.length) return;
    presets.insert(
      newIndex.clamp(0, presets.length - 1),
      presets.removeAt(oldIndex),
    );
    _changed();
  }

  void restoreHistory(Set<String> ids, {required String presetId}) {
    if (!presets.any((preset) => preset.id == presetId)) return;
    for (final entry
        in taskHistory.where((entry) => ids.contains(entry.id)).toList()) {
      addTask(entry.title, presetId: presetId);
    }
    taskHistory.removeWhere((entry) => ids.contains(entry.id));
    _changed();
  }

  void deleteHistory(Set<String> ids) {
    taskHistory.removeWhere((entry) => ids.contains(entry.id));
    _changed();
  }

  void toggleTaskDone(String id) {
    final index = tasks.indexWhere((task) => task.id == id);
    if (index < 0) return;
    tasks[index] = tasks[index].copyWith(done: !tasks[index].done);
    if (tasks[index].done) {
      taskHistory.insert(
        0,
        TaskHistoryEntry(
          id: '${id}_${_clockNow().microsecondsSinceEpoch}',
          title: tasks[index].title,
          at: _clockNow(),
          completed: true,
          status: 'completed',
        ),
      );
    }
    _changed();
  }

  void deleteTask(String id) {
    final removed = tasks.where((task) => task.id == id).firstOrNull;
    if (removed != null) {
      taskHistory.insert(
        0,
        TaskHistoryEntry(
          id: '${id}_${_clockNow().microsecondsSinceEpoch}',
          title: removed.title,
          at: _clockNow(),
          completed: removed.done,
          status: removed.done ? 'completed' : 'deleted',
        ),
      );
    }
    if (activeTaskId == id) {
      _freeze();
      _commitFocus();
      if (isRunning) _startedAt = _clockNow();
    }
    tasks.removeWhere((task) => task.id == id);
    if (activeTaskId == id) {
      activeTaskId = tasksFor(activePresetId).firstOrNull?.id;
    }
    _changed();
  }

  void setCountdown(String name, DateTime date) {
    countdownName = name.trim();
    countdownDate = date;
    _changed();
  }

  void clearCountdown() {
    countdownName = '';
    countdownDate = null;
    _changed();
  }

  void setTileVisible(DashboardTile tile, bool visible) {
    if (visible && !visibleTiles.contains(tile)) visibleTiles.add(tile);
    if (!visible) visibleTiles.remove(tile);
    _changed();
  }

  void moveTile(
    DashboardTile tile,
    DashboardTile target, {
    bool after = false,
  }) {
    final next = reorderedDashboardTiles(
      visibleTiles,
      tile,
      target,
      after: after,
    );
    setTileOrder(next);
  }

  void setTileOrder(List<DashboardTile> order) {
    if (order.length != visibleTiles.length ||
        order.toSet().length != order.length ||
        !order.toSet().containsAll(visibleTiles) ||
        listEquals(order, visibleTiles)) {
      return;
    }
    visibleTiles
      ..clear()
      ..addAll(order);
    _changed();
  }

  void _changed() {
    notifyListeners();
    unawaited(_persist());
  }

  Map<String, Object?> exportData() => {
    'phase': phase.name,
    'countUp': countUp,
    'focusMinutes': focusMinutes,
    'shortMinutes': shortMinutes,
    'shortBreakEnabled': shortBreakEnabled,
    'automaticCycle': automaticCycle,
    'activeTaskId': activeTaskId,
    'activePresetId': activePresetId,
    'presets': presets.map((preset) => preset.toJson()).toList(),
    'tasks': tasks.map((task) => task.toJson()).toList(),
    'taskHistory': taskHistory.map((entry) => entry.toJson()).toList(),
    'logs': logs.map((log) => log.toJson()).toList(),
    'visibleTiles': visibleTiles.map((tile) => tile.name).toList(),
    'tileSizes': _exportTileSizes(),
    'courseQuoteTilesV1': true,
    'countdownName': countdownName,
    'countdownDate': countdownDate?.toIso8601String(),
  };

  static void validateExportData(Map<String, dynamic> data) {
    if (data['presets'] is! List ||
        data['tasks'] is! List ||
        data['logs'] is! List) {
      throw FormatException(UiText.t("用户数据中的任务或档案格式无效"));
    }
    final parsedPresets = (data['presets'] as List)
        .map(TaskPreset.fromJson)
        .toList();
    if (parsedPresets.isEmpty || parsedPresets.any((item) => item == null)) {
      throw FormatException(UiText.t("用户数据中缺少有效清单"));
    }
    if ((data['tasks'] as List).any(
          (item) => FocusTask.fromJson(item) == null,
        ) ||
        (data['logs'] as List).any((item) => FocusLog.fromJson(item) == null)) {
      throw FormatException(UiText.t("用户数据中的任务或专注日志无效"));
    }
    final ids = parsedPresets
        .whereType<TaskPreset>()
        .map((item) => item.id)
        .toSet();
    if ((data['tasks'] as List)
        .map(FocusTask.fromJson)
        .whereType<FocusTask>()
        .any((item) => !ids.contains(item.presetId))) {
      throw FormatException(UiText.t("任务关联了不存在的清单"));
    }
  }

  Future<void> replaceData(
    Map<String, dynamic> data, {
    bool preserveRuntime = false,
  }) async {
    validateExportData(data);
    final runtime = preserveRuntime ? widgetRuntime : null;
    isRunning = false;
    _startedAt = null;
    await _preferences.setString(_storageKey, jsonEncode(data));
    _restore();
    if (runtime != null) restoreWidgetRuntime(runtime);
    notifyListeners();
  }

  Future<void> factoryReset() async {
    isRunning = false;
    _startedAt = null;
    await _preferences.remove(_storageKey);
    _restoreDefaults();
    notifyListeners();
  }

  void _restoreDefaults() {
    phase = TimerPhase.focus;
    countUp = false;
    isRunning = false;
    shortBreakEnabled = true;
    automaticCycle = true;
    focusMinutes = 25;
    shortMinutes = 5;
    _elapsedBase = 0;
    _loggedElapsed = 0;
    _startedAt = null;
    activeTaskId = null;
    activePresetId = 'default';
    presets
      ..clear()
      ..addAll([
        TaskPreset(id: 'default', name: UiText.t("我的一天")),
        TaskPreset(id: 'important', name: UiText.t("重要")),
        TaskPreset(id: 'planned', name: UiText.t("计划内")),
      ]);
    tileSizes.clear();
    tasks.clear();
    taskHistory.clear();
    logs.clear();
    countdownName = '';
    countdownDate = null;
    visibleTiles
      ..clear()
      ..addAll([
        DashboardTile.analogTimer,
        DashboardTile.tasks,
        DashboardTile.countUp,
        DashboardTile.mode,
        DashboardTile.countdown,
        DashboardTile.quote,
        DashboardTile.nextCourse,
        DashboardTile.todayCourses,
      ]);
  }

  Future<void> _persist() => _preferences.setString(
    _storageKey,
    jsonEncode({
      'phase': phase.name,
      'countUp': countUp,
      'isRunning': isRunning,
      'focusMinutes': focusMinutes,
      'shortMinutes': shortMinutes,
      'shortBreakEnabled': shortBreakEnabled,
      'automaticCycle': automaticCycle,
      'widgetEventId': widgetEventId,
      'elapsed': elapsedSeconds,
      'loggedElapsed': _loggedElapsed,
      'activeTaskId': activeTaskId,
      'activePresetId': activePresetId,
      'presets': presets.map((preset) => preset.toJson()).toList(),
      'taskHistory': taskHistory.map((entry) => entry.toJson()).toList(),
      'tasks': tasks.map((task) => task.toJson()).toList(),
      'logs': logs.map((log) => log.toJson()).toList(),
      'visibleTiles': visibleTiles.map((tile) => tile.name).toList(),
      'tileSizes': _exportTileSizes(),
      'courseQuoteTilesV1': true,
      'countdownName': countdownName,
      'countdownDate': countdownDate?.toIso8601String(),
    }),
  );
  void _restore() {
    _restoreDefaults();
    final raw = _preferences.getString(_storageKey);
    if (raw == null) return;
    try {
      final data = jsonDecode(raw);
      if (data is! Map) return;
      final sizes = data['tileSizes'];
      if (sizes is Map) {
        for (final tile in DashboardTile.values) {
          final value = sizes[tile.name];
          if (value is Map && value['columns'] is int && value['rows'] is int) {
            final size = (
              columns: value['columns'] as int,
              rows: value['rows'] as int,
            );
            if (dashboardTileSizes(
              tile,
              tablet: true,
              columns: 6,
            ).contains(size)) {
              tileSizes[tile] = size;
            }
          }
        }
      }
      widgetEventId = data['widgetEventId'] is int
          ? data['widgetEventId'] as int
          : 0;
      phase = data['phase'] == 'shortBreak'
          ? TimerPhase.shortBreak
          : TimerPhase.focus;
      countUp = data['countUp'] == true;
      focusMinutes = _intInRange(data['focusMinutes'], 25, 1, 180);
      shortMinutes = _intInRange(data['shortMinutes'], 5, 1, 60);
      shortBreakEnabled = data['shortBreakEnabled'] != false;
      automaticCycle = data['automaticCycle'] != false;
      _elapsedBase = _intInRange(data['elapsed'], 0, 0, 8640000);
      _loggedElapsed = _intInRange(data['loggedElapsed'], 0, 0, _elapsedBase);
      activeTaskId = data['activeTaskId'] is String
          ? data['activeTaskId'] as String
          : null;
      if (data['presets'] is List) {
        final restored = (data['presets'] as List)
            .map(TaskPreset.fromJson)
            .whereType<TaskPreset>()
            .toList();
        if (restored.isNotEmpty) {
          presets
            ..clear()
            ..addAll(restored);
        }
      }
      activePresetId =
          data['activePresetId'] is String &&
              presets.any((preset) => preset.id == data['activePresetId'])
          ? data['activePresetId'] as String
          : presets.first.id;
      if (data['taskHistory'] is List) {
        taskHistory.addAll(
          (data['taskHistory'] as List)
              .map(TaskHistoryEntry.fromJson)
              .whereType<TaskHistoryEntry>(),
        );
      }
      countdownName = data['countdownName'] is String
          ? data['countdownName'] as String
          : '';
      countdownDate = DateTime.tryParse(
        data['countdownDate']?.toString() ?? '',
      );
      if (data['tasks'] is List) {
        tasks.addAll(
          (data['tasks'] as List)
              .map(FocusTask.fromJson)
              .whereType<FocusTask>(),
        );
      }
      if (data['logs'] is List) {
        logs.addAll(
          (data['logs'] as List).map(FocusLog.fromJson).whereType<FocusLog>(),
        );
      }
      if (data['visibleTiles'] is List) {
        visibleTiles
          ..clear()
          ..addAll(
            (data['visibleTiles'] as List)
                .whereType<String>()
                .map(
                  (name) => DashboardTile.values
                      .where((tile) => tile.name == name)
                      .firstOrNull,
                )
                .whereType<DashboardTile>()
                .toSet(),
          );
        if (data['courseQuoteTilesV1'] != true) {
          for (final tile in [
            DashboardTile.quote,
            DashboardTile.nextCourse,
            DashboardTile.todayCourses,
          ]) {
            if (!visibleTiles.contains(tile)) visibleTiles.add(tile);
          }
        }
      }
      if (!tasksFor(activePresetId).any((task) => task.id == activeTaskId)) {
        activeTaskId = tasksFor(activePresetId).firstOrNull?.id;
      }
      // Only foreground time counts as focus time after an app relaunch.
      isRunning = false;
      _startedAt = null;
      if (!countUp && _elapsedBase >= durationSeconds) {
        _elapsedBase = durationSeconds;
      }
      if (data['courseQuoteTilesV1'] != true) unawaited(_persist());
    } catch (_) {
      // Damaged local preferences must not block launch.
    }
  }

  static int _intInRange(Object? value, int fallback, int min, int max) =>
      value is int ? value.clamp(min, max) : fallback;
  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }
}
