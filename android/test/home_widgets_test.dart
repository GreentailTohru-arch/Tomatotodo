import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';

void main() {
  test(
    'widget pause journal logs once across replay and restores the live clock',
    () async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      final now = DateTime(2026, 9, 29, 10);
      final controller = DashboardController(preferences, now: () => now);
      controller.addTask('桌面专注');
      final before = Map<String, Object?>.from(controller.widgetRuntime)
        ..addAll({'running': true, 'elapsed': 73, 'startedAt': null});
      final paused = Map<String, Object?>.from(before)
        ..addAll({'running': false, 'loggedElapsed': 73});
      final event = {
        'id': 1,
        'at': now.millisecondsSinceEpoch,
        'before': before,
        'action': 'toggle',
      };
      await controller.applyWidgetEvents([
        event,
      ], Map<String, dynamic>.from(paused));
      expect(controller.totalFocusSeconds, 73);
      expect(controller.isRunning, false);
      expect(controller.displaySeconds, 1500 - 73);
      controller.dispose();
      final restored = DashboardController(
        preferences,
        now: () => now.add(const Duration(seconds: 20)),
      );
      await restored.applyWidgetEvents([
        event,
      ], Map<String, dynamic>.from(paused));
      expect(restored.totalFocusSeconds, 73);
      final running = Map<String, dynamic>.from(paused)
        ..addAll({'running': true, 'startedAt': now.millisecondsSinceEpoch});
      restored.restoreWidgetRuntime(running);
      expect(restored.elapsedSeconds, 93);
      restored.dispose();
    },
  );

  test('background completion preserves completion time and alternates short break', () async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final ended = DateTime(2026, 9, 29, 10);
    final controller = DashboardController(
      preferences,
      now: () => ended.add(const Duration(minutes: 1)),
    );
    controller.setDurations(focus: 1, shortBreak: 5, enableShortBreak: true);
    final before = Map<String, Object?>.from(controller.widgetRuntime)
      ..addAll({'elapsed': 60, 'running': true});
    final after = Map<String, dynamic>.from(before)
      ..addAll({
        'phase': 'shortBreak',
        'elapsed': 0,
        'loggedElapsed': 0,
        'startedAt': ended.millisecondsSinceEpoch,
      });
    await controller.applyWidgetEvents([
      {
        'id': 1,
        'at': ended.millisecondsSinceEpoch,
        'before': before,
        'action': 'complete',
      },
    ], after);
    expect(controller.completedSessions, 1);
    expect(controller.logs.single.at, ended);
    expect(controller.phase, TimerPhase.shortBreak);
    expect(controller.displaySeconds, 240);
    expect(controller.exportData().containsKey('widgetEventId'), false);
    controller.dispose();
  });
}
