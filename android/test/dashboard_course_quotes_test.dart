import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/dashboard/course_dashboard_data.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';
import 'package:tomatotodo/features/dashboard/dashboard_quotes.dart';
import 'package:tomatotodo/features/dashboard/dashboard_page.dart';
import 'package:tomatotodo/features/general/course_schedule.dart';
import 'package:tomatotodo/features/general/general_state.dart';

void main() {
  testWidgets('course tiles show shared schedule data on a phone', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final dashboard = DashboardController(preferences);
    final general = GeneralState(preferences);
    final quotes = DashboardQuotes(
      preferences,
      fetch: (_) => Future.error(const FormatException('offline')),
    );
    final now = DateTime.now();
    general.setSchedule(
      CourseSchedule(
        termName: '测试',
        timezone: 'Asia/Shanghai',
        weeks: [
          CourseWeek(
            monday: mondayOf(now),
            events: [
              CourseEvent(
                day: now.weekday - 1,
                start: 1,
                end: 2,
                startTime: '00:00',
                endTime: '23:59',
                name: '成长课程',
                room: '教室 A',
              ),
            ],
          ),
        ],
      ),
    );
    general.setCourseEnabled(false);
    dashboard.visibleTiles
      ..clear()
      ..addAll([DashboardTile.nextCourse, DashboardTile.todayCourses]);
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DashboardPage(
            controller: dashboard,
            general: general,
            quotes: quotes,
            onConfigure: () {},
          ),
        ),
      ),
    );
    expect(find.text('成长课程'), findsNWidgets(2));
    expect(find.textContaining('教室 A'), findsWidgets);
    expect(general.courseEnabled, isFalse);
    expect(find.text('课表未开启'), findsNothing);
    expect(tester.takeException(), isNull);
    dashboard.dispose();
    general.dispose();
    quotes.dispose();
  });

  test(
    'next course includes an ongoing class and today shows the full day',
    () {
      final schedule = CourseSchedule(
        termName: '测试学期',
        timezone: 'Asia/Shanghai',
        weeks: [
          CourseWeek(
            monday: DateTime(2026, 9, 28),
            events: const [
              CourseEvent(
                day: 0,
                start: 1,
                end: 2,
                startTime: '08:00',
                endTime: '09:30',
                name: '数学',
              ),
              CourseEvent(
                day: 0,
                start: 3,
                end: 4,
                startTime: '10:00',
                endTime: '11:30',
                name: '英语',
              ),
            ],
          ),
        ],
      );
      final duringFirst = DateTime(2026, 9, 28, 8, 30);
      expect(CourseDashboardData.today(schedule, duringFirst).length, 2);
      expect(CourseDashboardData.next(schedule, duringFirst)?.event.name, '数学');
      expect(
        CourseDashboardData.next(schedule, duringFirst)?.isOngoing(duringFirst),
        isTrue,
      );
      expect(
        CourseDashboardData.next(
          schedule,
          DateTime(2026, 9, 28, 9, 31),
        )?.event.name,
        '英语',
      );
      expect(
        CourseDashboardData.next(schedule, DateTime(2026, 9, 28, 12)),
        isNull,
      );
    },
  );

  test('quote falls back offline, caches a relevant network quote', () async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final quotes = DashboardQuotes(
      preferences,
      now: () => DateTime(2026, 9, 28),
      fetch: (_) async => http.Response.bytes(
        utf8.encode(
          jsonEncode({
            'hitokoto': '坚持走下去，未来会给你答案。',
            'from': '测试来源',
            'uuid': 'sample-id',
          }),
        ),
        200,
      ),
    );
    expect(quotes.current.source, contains('离线'));
    await quotes.refresh();
    expect(quotes.current.text, '坚持走下去，未来会给你答案。');
    expect(quotes.current.source, '一言 · 测试来源');
    quotes.dispose();
    final offline = DashboardQuotes(
      preferences,
      fetch: (_) => Future.error(const FormatException('offline')),
    );
    expect(offline.current.text, '坚持走下去，未来会给你答案。');
    offline.dispose();
  });

  test(
    'existing dashboards receive the three new movable modules once',
    () async {
      SharedPreferences.setMockInitialValues({
        'mobile_dashboard_v1': jsonEncode({
          'visibleTiles': ['analogTimer', 'tasks'],
        }),
      });
      final preferences = await SharedPreferences.getInstance();
      final controller = DashboardController(preferences);
      expect(
        controller.visibleTiles,
        containsAll([
          DashboardTile.quote,
          DashboardTile.nextCourse,
          DashboardTile.todayCourses,
        ]),
      );
      await Future<void>.delayed(Duration.zero);
      controller.setTileVisible(DashboardTile.quote, false);
      await Future<void>.delayed(Duration.zero);
      controller.dispose();
      final restored = DashboardController(preferences);
      expect(restored.visibleTiles, isNot(contains(DashboardTile.quote)));
      restored.dispose();
    },
  );
}
