import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/app/tomatotodo_app.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';
import 'package:tomatotodo/features/dashboard/dashboard_page.dart';
import 'package:tomatotodo/features/dashboard/dashboard_quotes.dart';
import 'package:tomatotodo/features/general/general_state.dart';

void main() {
  testWidgets('calendar tile opens a Material date picker at phone size', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    controller.visibleTiles
      ..clear()
      ..add(DashboardTile.calendar);
    final general = GeneralState(preferences);
    final quotes = DashboardQuotes(
      preferences,
      fetch: (_) => Future.error(const FormatException('offline')),
    );
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DashboardPage(
            controller: controller,
            general: general,
            quotes: quotes,
            onConfigure: () {},
          ),
        ),
      ),
    );
    await tester.tap(find.text('日历'));
    await tester.pumpAndSettle();
    expect(find.byType(DatePickerDialog), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    controller.dispose();
    general.dispose();
    quotes.dispose();
  });

  test('countdown progress offers stepped and smooth timing', () async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    var now = DateTime(2026, 9, 29, 10);
    final controller = DashboardController(preferences, now: () => now);
    controller.setDurations(focus: 1, shortBreak: 1, enableShortBreak: true);
    expect(controller.progress, 0);
    controller.toggleRunning();
    now = now.add(const Duration(milliseconds: 750));
    expect(controller.progressAt(smooth: false), 0);
    expect(controller.progressAt(smooth: true), closeTo(0.0125, 0.00001));
    now = now.add(const Duration(milliseconds: 500));
    expect(controller.progressAt(smooth: false), closeTo(1 / 60, 0.00001));
    expect(controller.progressAt(smooth: true), closeTo(1.25 / 60, 0.00001));
    controller.toggleRunning();
    final paused = controller.progressAt(smooth: true);
    now = now.add(const Duration(seconds: 5));
    expect(controller.progressAt(smooth: true), paused);
    controller.dispose();
  });

  test(
    'launcher task event changes completion once without resetting timer',
    () async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      final controller = DashboardController(preferences);
      controller.addTask('读一本书', estimate: 3);
      final taskId = controller.activeTask!.id;
      controller.toggleRunning();
      final runtime = Map<String, dynamic>.from(controller.widgetRuntime);
      final event = {
        'id': 1,
        'at': DateTime.now().millisecondsSinceEpoch,
        'action': 'task',
        'taskId': taskId,
        'before': runtime,
      };
      await controller.applyWidgetEvents([event], runtime);
      expect(controller.activeTask!.done, isTrue);
      expect(controller.isRunning, isTrue);
      await controller.applyWidgetEvents([event], runtime);
      expect(controller.activeTask!.done, isTrue);
      controller.dispose();
    },
  );

  testWidgets(
    'preset ordering, task metadata and active list survive restore',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      final controller = DashboardController(preferences);
      controller.addPreset('学习');
      final study = controller.presets.last;
      controller.selectPreset(study.id);
      controller.addTask('背单词', subtitle: '第二课', estimate: 3);
      controller.addTask('复习');
      controller.reorderTasks(study.id, 1, 0);
      expect(controller.tasksFor(study.id).first.title, '复习');
      controller.reorderPresets(3, 0);
      expect(controller.presets.first.name, '学习');
      await tester.pump();
      controller.dispose();
      final restored = DashboardController(preferences);
      expect(restored.activePresetId, study.id);
      expect(restored.tasksFor(study.id).last.subtitle, '第二课');
      expect(restored.tasksFor(study.id).last.estimate, 3);
      expect(restored.presets.first.name, '学习');
      restored.dispose();
    },
  );

  testWidgets(
    'timer records actual focus time and alternates into short break',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      var now = DateTime(2026, 9, 27, 10);
      final controller = DashboardController(preferences, now: () => now);
      controller.addTask('写报告', estimate: 3);
      controller.setDurations(focus: 1, shortBreak: 1, enableShortBreak: true);
      controller.toggleRunning();
      now = now.add(const Duration(seconds: 12));
      controller.toggleRunning();
      expect(controller.totalFocusSeconds, 12);
      expect(controller.logs.single.taskTitle, '写报告');
      controller.toggleRunning();
      now = now.add(const Duration(seconds: 48));
      await tester.pump(const Duration(milliseconds: 300));
      expect(controller.completedSessions, 1);
      expect(controller.tomatoesForTask(controller.activeTask!), 1);
      expect(controller.totalFocusSeconds, 60);
      expect(controller.phase, TimerPhase.shortBreak);
      expect(controller.isRunning, isTrue);
      controller.reset();
      expect(controller.totalFocusSeconds, 60);
      controller.dispose();
    },
  );

  testWidgets('dashboard tile drag order survives restore', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    controller.moveTile(DashboardTile.mode, DashboardTile.analogTimer);
    expect(controller.visibleTiles.first, DashboardTile.mode);
    controller.moveTile(
      DashboardTile.mode,
      DashboardTile.analogTimer,
      after: true,
    );
    expect(controller.visibleTiles[1], DashboardTile.mode);
    await tester.pump();
    controller.dispose();
    final restored = DashboardController(preferences);
    expect(restored.visibleTiles[1], DashboardTile.mode);
    restored.dispose();
  });

  testWidgets('compact tile footprints and blank-space edit mode', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    final general = GeneralState(preferences);
    final quotes = DashboardQuotes(
      preferences,
      fetch: (_) => Future.error(const FormatException('offline')),
    );
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    expect(DashboardTile.tasks.columns, 1);
    expect(DashboardTile.tasks.rows, 1);
    expect(DashboardTile.mode.columns, 1);
    expect(DashboardTile.mode.rows, 1);
    expect(DashboardTile.countdown.columns, 1);
    expect(DashboardTile.countdown.rows, 1);
    expect(DashboardTile.calendar.columns, 1);
    expect(DashboardTile.calendar.rows, 1);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DashboardPage(
            controller: controller,
            general: general,
            quotes: quotes,
            onConfigure: () {},
          ),
        ),
      ),
    );
    expect(find.text('添加模块'), findsNothing);
    await tester.longPressAt(const Offset(8, 135));
    await tester.pumpAndSettle();
    expect(find.text('调整仪表盘'), findsOneWidget);
    await tester.tap(find.byTooltip('完成编辑'));
    await tester.pumpAndSettle();
    expect(find.text('仪表盘'), findsOneWidget);
    expect(tester.takeException(), isNull);
    controller.dispose();
    general.dispose();
    quotes.dispose();
  });

  testWidgets('task dialog sets tomato goal and task tile starts at the top', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    final general = GeneralState(preferences);
    final quotes = DashboardQuotes(
      preferences,
      fetch: (_) => Future.error(const FormatException('offline')),
    );
    tester.view.physicalSize = const Size(390, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DashboardPage(
            controller: controller,
            general: general,
            quotes: quotes,
            onConfigure: () {},
          ),
        ),
      ),
    );
    await tester.ensureVisible(find.byTooltip('添加任务'));
    await tester.tap(find.byTooltip('添加任务'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, '任务名称'), '背单词');
    await tester.enterText(find.widgetWithText(TextFormField, '目标番茄数'), '3');
    await tester.tap(find.text('保存'));
    await tester.pumpAndSettle();
    expect(controller.activeTasks.single.estimate, 3);
    expect(find.text('🍅 0/3'), findsOneWidget);
    expect(
      tester.getTopLeft(find.text('🍅 0/3')).dy,
      closeTo(tester.getTopLeft(find.text('背单词').last).dy, 10),
    );
    expect(
      tester.getTopLeft(find.text('背单词').last).dy,
      lessThan(
        tester
                .getBottomLeft(
                  find.text(
                    controller.presets
                        .firstWhere(
                          (preset) => preset.id == controller.activePresetId,
                        )
                        .name,
                  ),
                )
                .dy +
            90,
      ),
    );
    controller.dispose();
    general.dispose();
    quotes.dispose();
  });

  testWidgets('only blank-space long press enters edit, then tiles drag', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    final general = GeneralState(preferences);
    final quotes = DashboardQuotes(
      preferences,
      fetch: (_) => Future.error(const FormatException('offline')),
    );
    tester.view.physicalSize = const Size(390, 2000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DashboardPage(
            controller: controller,
            general: general,
            quotes: quotes,
            onConfigure: () {},
          ),
        ),
      ),
    );
    await tester.ensureVisible(find.text('计划时段'));
    await tester.pumpAndSettle();
    final start = tester.getCenter(find.text('计划时段'));
    final gesture = await tester.startGesture(start);
    await tester.pump(const Duration(milliseconds: 600));
    await gesture.up();
    await tester.pumpAndSettle();
    expect(find.text('调整仪表盘'), findsNothing);
    await tester.longPressAt(const Offset(8, 135));
    await tester.pumpAndSettle();
    expect(find.text('调整仪表盘'), findsOneWidget);
    final mode = tester.getCenter(find.text('计划时段'));
    final target = tester.getTopLeft(find.text('正向计时')) + const Offset(8, 8);
    final beforePreview = tester.getTopLeft(
      find.byKey(const ValueKey(DashboardTile.mode)),
    );
    final drag = await tester.startGesture(mode);
    await tester.pump(const Duration(milliseconds: 400));
    await drag.moveTo(target);
    await tester.pump(const Duration(milliseconds: 200));
    expect(
      controller.visibleTiles.indexOf(DashboardTile.mode),
      greaterThan(controller.visibleTiles.indexOf(DashboardTile.countUp)),
    );
    expect(
      tester.getTopLeft(find.byKey(const ValueKey(DashboardTile.mode))).dy,
      lessThan(beforePreview.dy),
    );
    await drag.up();
    await tester.pumpAndSettle();
    expect(
      controller.visibleTiles.indexOf(DashboardTile.mode),
      lessThan(controller.visibleTiles.indexOf(DashboardTile.countUp)),
    );
    controller.dispose();
    general.dispose();
    quotes.dispose();
  });

  testWidgets(
    'dragging a dashboard tile near the lower edge scrolls the page',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      final controller = DashboardController(preferences);
      final general = GeneralState(preferences);
      final quotes = DashboardQuotes(
        preferences,
        fetch: (_) => Future.error(const FormatException('offline')),
      );
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: DashboardPage(
              controller: controller,
              general: general,
              quotes: quotes,
              onConfigure: () {},
            ),
          ),
        ),
      );
      await tester.longPressAt(const Offset(8, 135));
      await tester.pumpAndSettle();
      final page = tester.widget<CustomScrollView>(
        find.byType(CustomScrollView).first,
      );
      final before = page.controller!.offset;
      final drag = await tester.startGesture(
        tester.getCenter(find.text('计划时段')),
      );
      await tester.pump(const Duration(milliseconds: 400));
      await drag.moveTo(const Offset(195, 710));
      for (var i = 0; i < 25; i++) {
        await tester.pump(const Duration(milliseconds: 20));
      }
      expect(page.controller!.offset, greaterThan(before));
      await drag.up();
      await tester.pumpAndSettle();
      await tester.pumpWidget(const SizedBox.shrink());
      controller.dispose();
      general.dispose();
      quotes.dispose();
    },
  );

  testWidgets('edit mode remove action hides a tile', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    final general = GeneralState(preferences);
    final quotes = DashboardQuotes(
      preferences,
      fetch: (_) => Future.error(const FormatException('offline')),
    );
    tester.view.physicalSize = const Size(390, 2000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DashboardPage(
            controller: controller,
            general: general,
            quotes: quotes,
            onConfigure: () {},
          ),
        ),
      ),
    );
    await tester.longPressAt(const Offset(8, 135));
    await tester.pumpAndSettle();
    final tile = find.byKey(const ValueKey(DashboardTile.tasks));
    await tester.ensureVisible(tile);
    final drag = await tester.startGesture(tester.getCenter(tile));
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();
    final remove = find.byKey(const ValueKey('remove-tile-target'));
    expect(remove, findsOneWidget);
    await drag.moveTo(tester.getCenter(remove));
    await tester.pump(const Duration(milliseconds: 250));
    await drag.up();
    await tester.pumpAndSettle();
    expect(controller.visibleTiles, isNot(contains(DashboardTile.tasks)));
    controller.dispose();
    general.dispose();
    quotes.dispose();
  });

  testWidgets(
    'compact MD3 dashboard has fixed controls and optional digital tile',
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
      expect(find.text('25:00'), findsOneWidget);
      expect(find.text('大计时器'), findsOneWidget);
      expect(find.text('准备就绪'), findsOneWidget);
      final statusPill = find.byTooltip('准备就绪');
      final expandedWidth = tester.getSize(statusPill).width;
      await tester.pumpAndSettle();
      expect(tester.getSize(statusPill).width, lessThan(expandedWidth));
      await tester.tap(statusPill);
      await tester.pumpAndSettle();
      expect(tester.getSize(statusPill).width, expandedWidth);
      await tester.tapAt(const Offset(8, 135));
      await tester.pumpAndSettle();
      expect(tester.getSize(statusPill).width, lessThan(expandedWidth));
      expect(
        tester.getTopLeft(find.text('大计时器')).dy,
        lessThan(tester.getTopLeft(find.text('25:00')).dy),
      );
      expect(
        tester.getTopLeft(find.text('准备就绪')).dx,
        greaterThan(tester.getTopRight(find.text('大计时器')).dx),
      );
      expect(find.text('开始'), findsOneWidget);
      expect(find.byTooltip('重置'), findsOneWidget);
      final start = tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, '开始'),
      );
      final reset = tester.widget<IconButton>(
        find.byWidgetPredicate(
          (widget) => widget is IconButton && widget.tooltip == '重置',
        ),
      );
      expect(start.style!.shape!.resolve({}), isA<RoundedRectangleBorder>());
      expect(start.style!.elevation!.resolve({}), 8);
      expect(find.byIcon(Icons.play_arrow_rounded), findsOneWidget);
      expect(
        start.style!.shape!.resolve({WidgetState.pressed}),
        isA<RoundedRectangleBorder>(),
      );
      expect(reset.style!.shape!.resolve({}), isA<CircleBorder>());
      await tester.tap(find.text('开始'));
      await tester.pump(const Duration(milliseconds: 350));
      expect(find.text('暂停'), findsOneWidget);
      expect(find.byIcon(Icons.pause_rounded), findsOneWidget);
      await tester.tap(find.byTooltip('重置'));
      await tester.pumpAndSettle();
      expect(find.text('开始'), findsOneWidget);
      expect(find.byIcon(Icons.play_arrow_rounded), findsOneWidget);
      expect(find.byTooltip('添加模块'), findsNothing);
      await tester.longPressAt(const Offset(8, 135));
      await tester.pumpAndSettle();
      expect(find.widgetWithText(FilledButton, '开始'), findsNothing);
      final resetButton = find.byWidgetPredicate(
        (widget) => widget is IconButton && widget.tooltip == '重置',
      );
      expect(resetButton, findsNothing);
      await tester.tap(find.byTooltip('添加模块'));
      await tester.pumpAndSettle();
      expect(find.text('数字计时器'), findsOneWidget);
      await tester.tap(find.widgetWithText(SwitchListTile, '数字计时器'));
      await tester.pumpAndSettle();
      await tester.tapAt(const Offset(10, 120));
      await tester.pumpAndSettle();
      expect(find.text('数字计时器'), findsOneWidget);
      expect(find.text('准备就绪'), findsWidgets);
      expect(find.text('当前任务：未选择任务'), findsOneWidget);
      await tester.tap(find.byTooltip('完成编辑'));
      await tester.pumpAndSettle();
      expect(find.byTooltip('添加模块'), findsNothing);
      expect(tester.widget<IconButton>(resetButton).onPressed, isNotNull);
      expect(
        tester
            .widget<FilledButton>(find.widgetWithText(FilledButton, '开始'))
            .onPressed,
        isNotNull,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
