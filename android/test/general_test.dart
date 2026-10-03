import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';
import 'package:tomatotodo/features/general/course_schedule.dart';
import 'package:tomatotodo/features/general/general_state.dart';

void main() {
  testWidgets('progress timing preference persists and resets', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final general = GeneralState(preferences);
    expect(general.progressSmooth, isFalse);
    general.setProgressStyle(smooth: true);
    await tester.pump();
    final restored = GeneralState(preferences);
    expect(restored.progressSmooth, isTrue);
    await restored.factoryReset();
    expect(restored.progressSmooth, isFalse);
    general.dispose();
    restored.dispose();
  });

  test(
    'course schedule validates the desktop JSON envelope and exact times',
    () {
      final monday = mondayOf(DateTime(2026, 9, 27));
      final source = {
        'format': 'tomatotodo-course-schedule',
        'version': 1,
        'term': {'name': '测试学期', 'timezone': 'Asia/Shanghai'},
        'weeks': [
          {
            'date': courseDate(monday),
            'events': [
              {
                'day': 0,
                'start': 1,
                'end': 2,
                'startTime': '08:30',
                'endTime': '10:05',
                'name': '设计课',
                'room': 'A101',
              },
            ],
          },
        ],
      };
      final schedule = CourseSchedule.fromJson(source);
      expect(schedule.weeks.single.events.single.name, '设计课');
      expect(schedule.toJson()['format'], 'tomatotodo-course-schedule');
      expect(
        schedule
            .futureEvents(monday.subtract(const Duration(days: 1)))
            .single
            .start
            .hour,
        8,
      );
      final invalid = Map<String, Object?>.from(source)..['version'] = 2;
      expect(() => CourseSchedule.fromJson(invalid), throwsFormatException);
    },
  );

  testWidgets('factory reset clears real logs, tasks and course settings', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final dashboard = DashboardController(preferences);
    final general = GeneralState(preferences);
    dashboard.addTask('写报告');
    general.setCourseEnabled(true);
    general.setReminder(
      enabled: true,
      lead: 15,
      system: false,
      inApp: true,
      sound: false,
    );
    await dashboard.factoryReset();
    await general.factoryReset();
    expect(dashboard.tasks, isEmpty);
    expect(dashboard.logs, isEmpty);
    expect(dashboard.focusMinutes, 25);
    expect(general.courseEnabled, isFalse);
    expect(general.leadMinutes, 10);
    dashboard.dispose();
    general.dispose();
  });
}
