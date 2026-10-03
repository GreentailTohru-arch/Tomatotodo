import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/configuration/preset_schedule.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';

void main() {
  test(
    'preset schedule survives restore and expires unfinished tasks',
    () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      var now = DateTime(2026, 9, 28, 12);
      final controller = DashboardController(prefs, now: () => now);
      controller.addTask('单词复习');
      controller.updatePresetSchedule(
        'default',
        dueAt: DateTime(2026, 9, 29, 18),
        remindAt: DateTime(2026, 9, 29, 9),
        repeat: 'daily',
      );
      await Future<void>.delayed(Duration.zero);
      controller.dispose();
      final restored = DashboardController(prefs, now: () => now);
      expect(restored.presets.first.dueAt, DateTime(2026, 9, 29, 18));
      expect(restored.presets.first.remindAt, DateTime(2026, 9, 29, 9));
      expect(restored.presets.first.repeat, 'daily');
      now = DateTime(2026, 9, 29, 18, 1);
      restored.checkPresetDeadlines();
      expect(restored.tasks, isEmpty);
      expect(restored.taskHistory.first.status, 'expired');
      expect(restored.presets.first.dueAt, DateTime(2026, 9, 30, 18));
      expect(restored.presets.first.remindAt, DateTime(2026, 9, 30, 9));
      restored.dispose();
    },
  );

  test('repeat schedule handles workdays, month ends and custom weekdays', () {
    expect(
      nextPresetDue(
        DateTime(2026, 10, 2, 9),
        repeat: 'workday',
        interval: 1,
        unit: 'day',
        weekdays: const [],
      ),
      DateTime(2026, 10, 5, 9),
    );
    expect(
      nextPresetDue(
        DateTime(2026, 1, 31, 9),
        repeat: 'monthly',
        interval: 1,
        unit: 'month',
        weekdays: const [],
      ),
      DateTime(2026, 2, 28, 9),
    );
    expect(
      nextPresetDue(
        DateTime(2026, 9, 28, 9),
        repeat: 'custom',
        interval: 2,
        unit: 'week',
        weekdays: const [1, 3],
      ),
      DateTime(2026, 9, 30, 9),
    );
  });
}
