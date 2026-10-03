import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/general/course_schedule.dart';
import 'package:tomatotodo/features/general/course_schedule_page.dart';
import 'package:tomatotodo/features/general/general_state.dart';

void main() {
  testWidgets(
    'tablet weekday cards share row heights only in multi-column layout',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final general = GeneralState(await SharedPreferences.getInstance());
      general.schedule = CourseSchedule(
        termName: 'Test',
        timezone: 'Asia/Shanghai',
        weeks: [
          CourseWeek(
            monday: mondayOf(DateTime.now()),
            events: [
              for (var i = 0; i < 3; i++)
                CourseEvent(
                  day: 1,
                  start: i + 1,
                  end: i + 1,
                  startTime: '08:00',
                  endTime: '09:00',
                  name: 'Course $i',
                ),
              const CourseEvent(
                day: 3,
                start: 1,
                end: 1,
                startTime: '08:00',
                endTime: '09:00',
                name: 'Other',
              ),
            ],
          ),
        ],
      );
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      for (final width in [1280.0, 800.0, 390.0]) {
        tester.view.physicalSize = Size(width, 1000);
        await tester.pumpWidget(
          MaterialApp(home: CourseSchedulePage(general: general)),
        );
        await tester.pumpAndSettle();
        Rect card(String day) => tester.getRect(
          find.ancestor(of: find.text(day), matching: find.byType(Card)).first,
        );
        if (width >= 1200) {
          expect(card('周一').height, card('周二').height);
          expect(card('周三').height, card('周二').height);
          expect(card('周四').height, card('周五').height);
          expect(card('周六').height, card('周五').height);
          expect(card('周四').height, lessThan(card('周一').height));
          expect(card('周日').width, card('周一').width);
          final courseList = find
              .ancestor(
                of: find.text('Course 0'),
                matching: find.byType(ListView),
              )
              .first;
          final state = tester.state<ScrollableState>(
            find.descendant(of: courseList, matching: find.byType(Scrollable)),
          );
          expect(state.position.maxScrollExtent, greaterThan(0));
          await tester.drag(courseList, const Offset(0, -150));
          await tester.pumpAndSettle();
          expect(state.position.pixels, greaterThan(0));
          expect(find.text('Course 2').hitTestable(), findsOneWidget);
        } else if (width >= 720) {
          expect(card('周一').height, card('周二').height);
          expect(card('周三').height, card('周四').height);
        } else {
          expect(card('周一').height, lessThan(card('周二').height));
        }
        expect(tester.takeException(), isNull);
      }
      await tester.pumpWidget(const SizedBox());
      general.dispose();
    },
  );

  testWidgets(
    'search jumps to a course in another week and week menu switches back',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final general = GeneralState(await SharedPreferences.getInstance());
      general.schedule = CourseSchedule(
        termName: '秋季',
        timezone: 'Asia/Shanghai',
        weeks: [
          CourseWeek(monday: DateTime(2026, 9, 28), events: const []),
          CourseWeek(
            monday: DateTime(2026, 10, 5),
            events: const [
              CourseEvent(
                day: 2,
                start: 1,
                end: 2,
                startTime: '08:00',
                endTime: '09:40',
                name: '高等数学',
                room: 'A101',
                teacher: '王老师',
              ),
            ],
          ),
        ],
      );
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(useMaterial3: true),
          home: CourseSchedulePage(general: general),
        ),
      );
      expect(find.textContaining('第 1 周 ·'), findsOneWidget);
      await tester.tap(find.byTooltip('搜索课程'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(SearchBar), '高等数学');
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ListTile, '高等数学'));
      await tester.pumpAndSettle();
      expect(find.textContaining('第 2 周 ·'), findsOneWidget);
      expect(find.widgetWithText(Card, '高等数学'), findsWidgets);
      await tester.tap(find.byTooltip('切换周次'));
      await tester.pumpAndSettle();
      await tester.tap(find.textContaining('第 1 周 ·').last);
      await tester.pumpAndSettle();
      expect(find.textContaining('第 1 周 ·'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
      general.dispose();
    },
  );
}
