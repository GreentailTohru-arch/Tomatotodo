import 'package:tomatotodo/core/localization/ui_text.dart';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:crypto/crypto.dart';

import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:timezone/data/latest_all.dart' as tz_data;
import 'package:timezone/timezone.dart' as tz;

import 'course_schedule.dart';
import '../configuration/preset_schedule.dart';
import '../dashboard/dashboard_controller.dart';

class CourseNotifications {
  CourseNotifications();
  static final _timezoneChannel = MethodChannel('com.tomatotodo/timezone');
  final _plugin = FlutterLocalNotificationsPlugin();
  bool _ready = false;
  static int pinnedId = 9000;
  static int timerId = 9001;
  static int tomatoId = 9002;
  static int presetBaseId = 20000;
  Future<void> initialize() async {
    if (_ready ||
        kIsWeb ||
        (defaultTargetPlatform != TargetPlatform.android &&
            defaultTargetPlatform != TargetPlatform.iOS)) {
      return;
    }
    await _plugin.initialize(
      settings: InitializationSettings(
        android: AndroidInitializationSettings('ic_stat_tomatotodo'),
        iOS: DarwinInitializationSettings(
          requestAlertPermission: false,
          requestSoundPermission: false,
          requestBadgePermission: false,
        ),
      ),
    );
    tz_data.initializeTimeZones();
    final zone = await _timezoneChannel.invokeMethod<String>(
      'getLocalTimezone',
    );
    if (zone == null) throw StateError(UiText.t("无法获取系统时区"));
    tz.setLocalLocation(tz.getLocation(zone));
    _ready = true;
  }

  Future<bool> requestAccess({bool exact = false}) async {
    await initialize();
    if (!_ready) return false;
    if (defaultTargetPlatform == TargetPlatform.android) {
      final android = _plugin
          .resolvePlatformSpecificImplementation<
            AndroidFlutterLocalNotificationsPlugin
          >();
      final allowed = await android?.requestNotificationsPermission() ?? false;
      if (!allowed) return false;
      if (exact) return await android?.requestExactAlarmsPermission() ?? false;
      return true;
    }
    final ios = _plugin
        .resolvePlatformSpecificImplementation<
          IOSFlutterLocalNotificationsPlugin
        >();
    return await ios?.requestPermissions(
          alert: true,
          sound: true,
          badge: true,
        ) ??
        false;
  }

  Future<void> pin(bool enabled) async {
    if (defaultTargetPlatform != TargetPlatform.android) return;
    await initialize();
    if (!_ready) return;
    if (!enabled) {
      await _plugin.cancel(id: pinnedId);
      return;
    }
    if (!await requestAccess()) throw StateError(UiText.t("系统通知权限未开启"));
    await _plugin.show(
      id: pinnedId,
      title: 'Tomatotodo',
      body: UiText.t("专注当下，点击返回应用"),
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(
          'tomatotodo_ongoing',
          UiText.t("常驻消息"),
          importance: Importance.low,
          priority: Priority.low,
          ongoing: true,
          autoCancel: false,
        ),
        iOS: DarwinNotificationDetails(presentSound: false),
      ),
    );
  }

  Future<void> showTimer({
    required bool active,
    required bool running,
    required bool shortBreak,
    required bool countUp,
    required int elapsed,
    required int total,
  }) async {
    await initialize();
    if (!_ready) return;
    if (!active) {
      await _plugin.cancel(id: timerId);
      return;
    }
    final remaining = (total - elapsed).clamp(0, total);
    final phase = shortBreak ? UiText.t("短休") : UiText.t("专注");
    final time = countUp ? elapsed : remaining;
    await _plugin.show(
      id: timerId,
      title: running ? UiText.f("{0}进行中", [phase]) : UiText.f("{0}已暂停", [phase]),
      body:
          UiText.f("{0} {1}:{2}", [countUp ? UiText.t("已专注") : UiText.t("剩余"), (time ~/ 60).toString().padLeft(2, '0'), (time % 60).toString().padLeft(2, '0')]),
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(
          'tomatotodo_timer_progress',
          UiText.t("专注进度"),
          channelDescription: UiText.t("专注和短休的常驻进度"),
          importance: Importance.low,
          priority: Priority.low,
          ongoing: true,
          autoCancel: false,
          onlyAlertOnce: true,
          showProgress: true,
          maxProgress: total,
          progress: countUp ? elapsed % total : elapsed.clamp(0, total),
        ),
        iOS: DarwinNotificationDetails(presentSound: false),
      ),
    );
  }

  Future<void> showTomato() => showPhaseEvent(false);

  Future<void> showPhaseEvent(bool breakEnded) async {
    await initialize();
    if (!_ready) return;
    final prefs = await SharedPreferences.getInstance();
    final key = 'timer_sound_${breakEnded ? 'break' : 'focus'}';
    final enabled = prefs.getBool('${key}_enabled') ?? true;
    final vibration = prefs.getBool('${key}_vibration') ?? true;
    final uri = prefs.getString('${key}_uri') ?? '';
    final identity = sha256
        .convert(utf8.encode('$enabled:$uri:$vibration'))
        .toString()
        .substring(0, 16);
    await _plugin.show(
      id: breakEnded ? tomatoId + 1 : tomatoId,
      title: breakEnded ? UiText.t("短休结束，开始专注") : UiText.t("🍅 获得一个番茄"),
      body: breakEnded ? UiText.t("休息好了，开始下一轮专注。") : UiText.t("一轮专注已完成，继续保持。"),
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(
          'tomatotodo_${breakEnded ? 'break' : 'focus'}_$identity',
          breakEnded ? UiText.t("短休结束") : UiText.t("番茄奖励"),
          importance: Importance.high,
          priority: Priority.high,
          playSound: enabled,
          enableVibration: vibration,
          sound: enabled && uri.isNotEmpty
              ? UriAndroidNotificationSound(uri)
              : null,
        ),
        iOS: DarwinNotificationDetails(presentSound: enabled),
      ),
    );
  }

  Future<({bool allowed, bool exact, int count})> schedulePresets(
    List<TaskPreset> presets, {
    required int previousCount,
  }) async {
    await initialize();
    if (!_ready) return (allowed: false, exact: false, count: 0);
    for (var i = 0; i < previousCount; i++) {
      await _plugin.cancel(id: presetBaseId + i);
    }
    final now = DateTime.now();
    if (!presets.any(
      (p) =>
          (p.dueAt?.isAfter(now) ?? false) ||
          (p.remindAt?.isAfter(now) ?? false),
    )) {
      return (allowed: true, exact: true, count: 0);
    }
    if (!await requestAccess()) {
      return (allowed: false, exact: false, count: 0);
    }
    final exact = await requestAccess(exact: true);
    final events = <({DateTime at, String title, String body})>[];
    for (final preset in presets) {
      var due = preset.dueAt;
      var remind = preset.remindAt;
      for (var occurrence = 0; occurrence < 32; occurrence++) {
        if (due != null && due.isAfter(now)) {
          events.add((at: due, title: UiText.t("清单截止"), body: UiText.f("{0} · 截止时间已到", [preset.name])));
        }
        if (remind != null && remind.isAfter(now)) {
          events.add((
            at: remind,
            title: UiText.t("任务提醒"),
            body: UiText.f("{0} · 计划时间到了", [preset.name]),
          ));
        }
        if (due == null) break;
        final next = nextPresetDue(
          due,
          repeat: preset.repeat,
          interval: preset.repeatInterval,
          unit: preset.repeatUnit,
          weekdays: preset.repeatDays,
        );
        if (next == null || next.difference(now).inDays > 90) break;
        remind = remind == null ? null : next.add(remind.difference(due));
        due = next;
      }
    }
    events.sort((a, b) => a.at.compareTo(b.at));
    final count = events.length.clamp(0, 64);
    for (var i = 0; i < count; i++) {
      final event = events[i];
      await _plugin.zonedSchedule(
        id: presetBaseId + i,
        scheduledDate: tz.TZDateTime.from(event.at, tz.local),
        title: event.title,
        body: event.body,
        notificationDetails: NotificationDetails(
          android: AndroidNotificationDetails(
            'tomatotodo_preset_alerts',
            UiText.t("任务与截止提醒"),
            importance: Importance.high,
            priority: Priority.high,
          ),
          iOS: DarwinNotificationDetails(),
        ),
        androidScheduleMode: exact
            ? AndroidScheduleMode.exactAllowWhileIdle
            : AndroidScheduleMode.inexactAllowWhileIdle,
      );
    }
    return (allowed: true, exact: exact, count: count);
  }

  Future<void> schedule({
    required CourseSchedule schedule,
    required bool enabled,
    required int leadMinutes,
    required bool system,
    required bool sound,
    bool vibration = true,
    required int previousCount,
  }) async {
    await initialize();
    if (!_ready) return;
    for (var i = 0; i < previousCount; i++) {
      await _plugin.cancel(id: 10000 + i);
    }
    if (!enabled || !system) return;
    if (!await requestAccess()) throw StateError(UiText.t("系统通知权限未开启"));
    final exact = await requestAccess(exact: true);
    final now = DateTime.now();
    final entries =
        schedule
            .futureEvents(now)
            .where(
              (item) => item.start
                  .subtract(Duration(minutes: leadMinutes))
                  .isAfter(now),
            )
            .toList()
          ..sort((a, b) => a.start.compareTo(b.start));
    for (var i = 0; i < entries.length && i < 60; i++) {
      final item = entries[i];
      final when = item.start.subtract(Duration(minutes: leadMinutes));
      await _plugin.zonedSchedule(
        id: 10000 + i,
        scheduledDate: tz.TZDateTime.from(when, tz.local),
        title: UiText.t("课程提醒"),
        body:
            '${item.event.name} · ${item.event.startTime}${item.event.room.isEmpty ? '' : ' · ${item.event.room}'}',
        notificationDetails: NotificationDetails(
          android: AndroidNotificationDetails(
            'tomatotodo_courses_sound_${sound}_vibration_$vibration',
            sound ? UiText.t("课程提醒") : UiText.t("课程提醒（静音）"),
            importance: Importance.high,
            priority: Priority.high,
            playSound: sound,
            enableVibration: vibration,
          ),
          iOS: DarwinNotificationDetails(presentSound: sound),
        ),
        androidScheduleMode: exact
            ? AndroidScheduleMode.exactAllowWhileIdle
            : AndroidScheduleMode.inexactAllowWhileIdle,
      );
    }
  }
}
