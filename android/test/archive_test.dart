import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/app/tomatotodo_app.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';
import 'package:tomatotodo/features/archive/archive_page.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';

void main() {
  test('archive greeting follows seven day periods', () {
    final cases = <int, String>{
      2: '夜深了，记得休息',
      6: '早安，从容开始',
      10: '上午好，稳稳向前',
      12: '中午好，稍作休息',
      16: '下午好，保持节奏',
      19: '傍晚好，收好今天',
      22: '晚上好，辛苦了',
    };
    for (final entry in cases.entries) {
      expect(archiveGreeting(DateTime(2026, 9, 30, entry.key)), entry.value);
    }
  });

  testWidgets('archive cards reorder in edit mode and save on done', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    Widget page() => MaterialApp(
      home: ArchivePage(controller: controller, preferences: preferences),
    );
    await tester.pumpWidget(page());
    await tester.longPressAt(
      tester.getCenter(find.byKey(const Key('archive-edit-space'))),
    );
    await tester.pumpAndSettle();
    expect(find.byTooltip('完成排序'), findsOneWidget);
    final calendar = find.text('专注日历');
    final gesture = await tester.startGesture(tester.getCenter(calendar));
    await tester.pump(const Duration(milliseconds: 600));
    await gesture.moveTo(const Offset(195, 760));
    for (var i = 0; i < 20; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
    await gesture.up();
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('完成排序'));
    await tester.pumpAndSettle();
    expect(preferences.getStringList('tomatotodo-archive-card-order-v1'), [
      'log',
      'statistics',
      'heatmap',
      'calendar',
    ]);
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pumpWidget(page());
    expect(find.textContaining('日志'), findsWidgets);
    expect(tester.takeException(), isNull);
    controller.dispose();
  });

  testWidgets(
    'archive shows zero values, calendar and share preview on a phone',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        TomatotodoApp(settings: AppSettings(preferences)),
      );
      await tester.tap(find.text('档案').last);
      await tester.pumpAndSettle();
      expect(find.text('今日专注'), findsOneWidget);
      expect(find.text('0 秒'), findsWidgets);
      expect(find.text('专注日历'), findsOneWidget);
      final carousel = find.byKey(const Key('archive-metric-carousel'));
      final calendarCard = find.byKey(const ValueKey('calendar'));
      expect(
        tester.getTopLeft(calendarCard).dy - tester.getBottomLeft(carousel).dy,
        closeTo(12, 0.1),
      );
      await tester.tap(find.byTooltip('保存与分享'));
      await tester.pumpAndSettle();
      expect(find.text('系统分享'), findsOneWidget);
      expect(find.text('保存相册'), findsOneWidget);
      expect(find.text('Tomatotodo'), findsOneWidget);
      expect(find.text('本地用户'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('metric carousel reveals all four summaries by swiping', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: ArchivePage(controller: controller)),
      ),
    );
    final carousel = find.byKey(const Key('archive-metric-carousel'));
    expect(carousel, findsOneWidget);
    expect(tester.getSize(carousel).height, 168);
    expect(find.text('今日专注'), findsOneWidget);
    await tester.drag(carousel, const Offset(-650, 0));
    await tester.pumpAndSettle();
    expect(find.text('专注次数'), findsOneWidget);
    expect(tester.takeException(), isNull);
    controller.dispose();
  });

  testWidgets('long daily logs stay inside a 320px scrolling region', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    final now = DateTime.now();
    for (var i = 0; i < 20; i++) {
      controller.logs.add(
        FocusLog(
          at: now,
          seconds: 60,
          taskTitle: '日志 $i',
          completedSession: false,
        ),
      );
    }
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: ArchivePage(controller: controller)),
      ),
    );
    await tester.ensureVisible(find.textContaining('日志').first);
    await tester.pumpAndSettle();
    final list = find.byKey(const Key('archive-log-list'));
    expect(list, findsOneWidget);
    expect(tester.getSize(list).height, lessThanOrEqualTo(320));
    await tester.scrollUntilVisible(
      find.text('日志 19'),
      250,
      scrollable: find.descendant(of: list, matching: find.byType(Scrollable)),
    );
    expect(find.text('日志 19'), findsOneWidget);
    expect(tester.takeException(), isNull);
    controller.dispose();
  });

  testWidgets('tablet logs fill the tall card from the top and scroll', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    final now = DateTime.now();
    for (var i = 0; i < 20; i++) {
      controller.logs.add(
        FocusLog(
          at: now,
          seconds: 60,
          taskTitle: '日志 $i',
          completedSession: false,
        ),
      );
    }
    tester.view.physicalSize = const Size(1280, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: ArchivePage(controller: controller)),
      ),
    );
    await tester.ensureVisible(find.textContaining('日志').first);
    await tester.pumpAndSettle();
    final list = find.byKey(const Key('archive-log-list'));
    expect(list, findsOneWidget);
    expect(tester.getSize(list).height, greaterThan(320));
    final header = find.textContaining(' 日志');
    expect(
      tester.getTopLeft(list).dy - tester.getBottomLeft(header).dy,
      lessThan(24),
    );
    expect(
      tester.getTopLeft(find.text('日志 0')).dy,
      greaterThanOrEqualTo(tester.getTopLeft(list).dy),
    );
    await tester.scrollUntilVisible(
      find.text('日志 19'),
      250,
      scrollable: find.descendant(of: list, matching: find.byType(Scrollable)),
    );
    expect(find.text('日志 19'), findsOneWidget);
    expect(tester.takeException(), isNull);
    controller.dispose();
  });

  testWidgets('calendar month pages slide in opposite directions', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    final now = DateTime.now();
    final next = DateTime(now.year, now.month + 1);
    final currentKey = ValueKey('${now.year}-${now.month}');
    final nextKey = ValueKey('${next.year}-${next.month}');
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: ArchivePage(controller: controller)),
      ),
    );
    expect(find.text('🍅'), findsOneWidget);
    final nextButton = find.byTooltip('下一页').first;
    await tester.ensureVisible(nextButton);
    await tester.tap(nextButton);
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 150));
    expect(find.byKey(currentKey), findsOneWidget);
    expect(find.byKey(nextKey), findsOneWidget);
    final oldSlide = tester.widget<SlideTransition>(
      find
          .ancestor(
            of: find.byKey(currentKey),
            matching: find.byType(SlideTransition),
          )
          .first,
    );
    final newSlide = tester.widget<SlideTransition>(
      find
          .ancestor(
            of: find.byKey(nextKey),
            matching: find.byType(SlideTransition),
          )
          .first,
    );
    expect(oldSlide.position.value.dx, lessThan(0));
    expect(newSlide.position.value.dx, greaterThan(0));
    await tester.pumpAndSettle();
    expect(find.byKey(currentKey), findsNothing);
    expect(find.byKey(nextKey), findsOneWidget);
    await tester.ensureVisible(find.byKey(nextKey));
    await tester.fling(
      find.byKey(const Key('archive-calendar-gesture')),
      const Offset(280, 0),
      900,
    );
    await tester.pumpAndSettle();
    expect(find.byKey(currentKey), findsOneWidget);
    controller.dispose();
  });
}
