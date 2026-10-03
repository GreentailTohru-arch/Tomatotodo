import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'course_notifications.dart';
import 'course_schedule.dart';

class GeneralState extends ChangeNotifier {
  GeneralState(this.preferences) {
    _restore();
    _tickTimer = Timer.periodic(
      Duration(seconds: 20),
      (_) => _checkForegroundReminders(),
    );
    unawaited(_syncNotifications());
  }
  final SharedPreferences preferences;
  final CourseNotifications notifications = CourseNotifications();
  Timer? _tickTimer;
  bool _disposed = false;
  void Function(String)? onInAppReminder;
  final Set<String> _fired = {};
  bool courseEnabled = false;
  bool pinnedMessage = false;
  bool timerDialTicks = false;
  bool progressWavy = true;
  bool progressThick = true;
  bool progressSmooth = false;
  bool reminderEnabled = false;
  int leadMinutes = 10;
  bool reminderSystem = true;
  bool reminderInApp = true;
  bool reminderSound = true;
  bool reminderVibration = true;
  String? notificationIssue;
  CourseSchedule schedule = CourseSchedule.blank();
  static final storageKey = 'tomatotodo-general-v1';
  Map<String, Object?> exportData() => {
    'courseEnabled': courseEnabled,
    'pinnedMessage': pinnedMessage,
    'timerDialTicks': timerDialTicks,
    'progressWavy': progressWavy,
    'progressThick': progressThick,
    'progressSmooth': progressSmooth,
    'reminderEnabled': reminderEnabled,
    'leadMinutes': leadMinutes,
    'reminderSystem': reminderSystem,
    'reminderInApp': reminderInApp,
    'reminderSound': reminderSound,
    'reminderVibration': reminderVibration,
    'schedule': schedule.toJson(),
  };
  void _restore() {
    final raw = preferences.getString(storageKey);
    if (raw == null) return;
    try {
      final data = jsonDecode(raw);
      if (data is! Map) return;
      courseEnabled = data['courseEnabled'] == true;
      pinnedMessage = data['pinnedMessage'] == true;
      timerDialTicks = data['timerDialTicks'] == true;
      progressWavy = data['progressWavy'] != false;
      progressThick = data['progressThick'] != false;
      progressSmooth = data['progressSmooth'] == true;
      reminderEnabled = data['reminderEnabled'] == true;
      leadMinutes = data['leadMinutes'] is int
          ? (data['leadMinutes'] as int).clamp(0, 120)
          : 10;
      reminderSystem = data['reminderSystem'] != false;
      reminderInApp = data['reminderInApp'] != false;
      reminderSound = data['reminderSound'] != false;
      reminderVibration = data['reminderVibration'] != false;
      if (data['schedule'] != null) {
        schedule = CourseSchedule.fromJson(data['schedule']);
      }
    } catch (_) {
      /* Invalid local data falls back to defaults. */
    }
  }

  void _changed() {
    notifyListeners();
    unawaited(preferences.setString(storageKey, jsonEncode(exportData())));
    unawaited(_syncNotifications());
  }

  void setCourseEnabled(bool value) {
    courseEnabled = value;
    _changed();
  }

  void setPinnedMessage(bool value) {
    pinnedMessage = value;
    _changed();
  }

  void setTimerDialTicks(bool value) {
    timerDialTicks = value;
    _changed();
  }

  void setProgressStyle({bool? wavy, bool? thick, bool? smooth}) {
    progressWavy = wavy ?? progressWavy;
    progressThick = thick ?? progressThick;
    progressSmooth = smooth ?? progressSmooth;
    notifyListeners();
    unawaited(preferences.setString(storageKey, jsonEncode(exportData())));
  }

  void setReminder({
    required bool enabled,
    required int lead,
    required bool system,
    required bool inApp,
    required bool sound,
    bool vibration = true,
  }) {
    if (lead < 0 || lead > 120) {
      throw FormatException(UiText.t("提醒提前量必须是 0 到 120 分钟"));
    }
    reminderEnabled = enabled;
    leadMinutes = lead;
    reminderSystem = system;
    reminderInApp = inApp;
    reminderSound = sound;
    reminderVibration = vibration;
    _changed();
  }

  void setSchedule(CourseSchedule value) {
    schedule = value;
    _changed();
  }

  Future<void> replaceData(Map<String, dynamic> data) async {
    validateExportData(data);
    final parsedSchedule = CourseSchedule.fromJson(data['schedule']);
    final lead = data['leadMinutes'];
    if (lead is! int || lead < 0 || lead > 120) {
      throw FormatException(UiText.t("提醒设置格式无效"));
    }
    courseEnabled = data['courseEnabled'] == true;
    pinnedMessage = data['pinnedMessage'] == true;
    timerDialTicks = data['timerDialTicks'] == true;
    progressWavy = data['progressWavy'] != false;
    progressThick = data['progressThick'] != false;
    progressSmooth = data['progressSmooth'] == true;
    reminderEnabled = data['reminderEnabled'] == true;
    leadMinutes = lead;
    reminderSystem = data['reminderSystem'] != false;
    reminderInApp = data['reminderInApp'] != false;
    reminderSound = data['reminderSound'] != false;
    reminderVibration = data['reminderVibration'] != false;
    schedule = parsedSchedule;
    await preferences.setString(storageKey, jsonEncode(exportData()));
    notifyListeners();
    await _syncNotifications();
  }

  static void validateExportData(Map<String, dynamic> data) {
    CourseSchedule.fromJson(data['schedule']);
    final lead = data['leadMinutes'];
    if (lead is! int || lead < 0 || lead > 120) {
      throw FormatException(UiText.t("提醒设置格式无效"));
    }
  }

  Future<void> factoryReset() async {
    courseEnabled = false;
    pinnedMessage = false;
    timerDialTicks = false;
    progressWavy = true;
    progressThick = true;
    progressSmooth = false;
    reminderEnabled = false;
    leadMinutes = 10;
    reminderSystem = true;
    reminderInApp = true;
    reminderSound = true;
    reminderVibration = true;
    schedule = CourseSchedule.blank();
    await preferences.remove(storageKey);
    notifyListeners();
    await _syncNotifications();
  }

  Future<void> refreshNotificationLanguage() => _syncNotifications();

  Future<void> _syncNotifications() async {
    try {
      await notifications.pin(pinnedMessage);
      final previous =
          preferences.getInt('tomatotodo-scheduled-course-count') ?? 0;
      final upcoming = schedule
          .futureEvents(DateTime.now())
          .where(
            (item) => item.start
                .subtract(Duration(minutes: leadMinutes))
                .isAfter(DateTime.now()),
          )
          .length
          .clamp(0, 60);
      await notifications.schedule(
        schedule: schedule,
        enabled: reminderEnabled,
        leadMinutes: leadMinutes,
        system: reminderSystem,
        sound: reminderSound,
        vibration: reminderVibration,
        previousCount: previous,
      );
      await preferences.setInt(
        'tomatotodo-scheduled-course-count',
        reminderEnabled && reminderSystem ? upcoming : 0,
      );
      notificationIssue = null;
      if (!_disposed) notifyListeners();
    } catch (_) {
      notificationIssue = UiText.t("系统通知未启用，请检查通知与精确提醒权限");
      if (!_disposed) notifyListeners();
    }
  }

  void _checkForegroundReminders() {
    if (!reminderEnabled || !reminderInApp) return;
    final now = DateTime.now();
    for (final week in schedule.weeks) {
      for (final event in week.events) {
        final date = week.monday.add(Duration(days: event.day));
        final parts = event.startTime.split(':').map(int.parse).toList();
        final start = DateTime(
          date.year,
          date.month,
          date.day,
          parts[0],
          parts[1],
        );
        final reminderAt = start.subtract(Duration(minutes: leadMinutes));
        final key =
            '${courseDate(date)}:${event.day}:${event.startTime}:${event.name}';
        if (!now.isBefore(reminderAt) &&
            now.isBefore(start) &&
            _fired.add(key)) {
          onInAppReminder?.call(UiText.f("课程提醒：{0} · {1}", [event.name, event.startTime]));
          if (reminderSound) SystemSound.play(SystemSoundType.alert);
        }
      }
    }
    _fired.removeWhere(
      (key) => key.substring(0, 10).compareTo(courseDate(now)) < 0,
    );
  }

  @override
  void dispose() {
    _disposed = true;
    _tickTimer?.cancel();
    super.dispose();
  }
}
