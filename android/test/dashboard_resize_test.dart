import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';
import 'package:tomatotodo/features/dashboard/dashboard_page.dart';
import 'package:tomatotodo/features/dashboard/dashboard_quotes.dart';
import 'package:tomatotodo/features/general/general_state.dart';

void main() {
  test('sizes persist, validate, export and fall back without losing tablet preference', () async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final controller = DashboardController(prefs);
    controller.setTileSize(DashboardTile.digitalTimer, (columns: 4, rows: 2));
    controller.setTileSize(DashboardTile.mode, (columns: 4, rows: 2));
    expect(controller.tileSizes.containsKey(DashboardTile.mode), isFalse);
    expect(
      controller.tileSize(
        DashboardTile.digitalTimer,
        tablet: false,
        columns: 2,
      ),
      (columns: 2, rows: 1),
    );
    expect(
      controller.tileSize(DashboardTile.digitalTimer, tablet: true, columns: 3),
      (columns: 3, rows: 2),
    );
    await Future<void>.delayed(Duration.zero);
    final restored = DashboardController(prefs);
    expect(
      restored.tileSize(DashboardTile.digitalTimer, tablet: true, columns: 6),
      (columns: 4, rows: 2),
    );
    await restored.replaceData(
      Map<String, dynamic>.from(controller.exportData()),
    );
    expect(restored.tileSizes[DashboardTile.digitalTimer], (
      columns: 4,
      rows: 2,
    ));
    await restored.factoryReset();
    expect(restored.tileSizes, isEmpty);
    restored.dispose();
    controller.dispose();
  });

  testWidgets(
    'long press corner resizes, moves neighbors, and persists on release',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      final controller = DashboardController(prefs);
      controller.visibleTiles
        ..clear()
        ..addAll([DashboardTile.tasks, DashboardTile.mode]);
      final general = GeneralState(prefs);
      final quotes = DashboardQuotes(prefs);
      tester.view.devicePixelRatio = 1;
      tester.view.physicalSize = const Size(800, 1000);
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
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('resize-tasks')), findsNothing);
      final tile = find.byKey(const ValueKey('tile-tasks'));
      final original = tester.getRect(tile);
      // Grid whitespace between the first two cells.
      await tester.longPressAt(Offset(original.right + 6, original.top + 30));
      await tester.pumpAndSettle();
      expect(find.byTooltip('完成编辑'), findsOneWidget);
      final modeBefore = tester.getRect(
        find.byKey(const ValueKey('tile-mode')),
      );
      final gesture = await tester.startGesture(
        tester.getCenter(find.byKey(const ValueKey('resize-tasks'))),
      );
      await tester.pump(const Duration(milliseconds: 600));
      expect(find.byType(Chip), findsOneWidget);
      await gesture.moveBy(Offset(original.width + 12, original.height + 12));
      await tester.pumpAndSettle();
      expect(controller.tileSizes[DashboardTile.tasks], isNull);
      expect(tester.getSize(tile).width, closeTo(original.width * 2 + 12, 1));
      expect(tester.getSize(tile).height, closeTo(original.height * 2 + 12, 1));
      expect(
        tester.getRect(find.byKey(const ValueKey('tile-mode'))),
        isNot(modeBefore),
      );
      await gesture.up();
      await tester.pumpAndSettle();
      expect(controller.tileSizes[DashboardTile.tasks], (columns: 2, rows: 2));
      expect(tester.takeException(), isNull);
      await tester.tap(find.byTooltip('完成编辑'));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('resize-tasks')), findsNothing);
      await tester.pumpWidget(const SizedBox());
      controller.dispose();
      general.dispose();
      quotes.dispose();
    },
  );

  testWidgets('all requested footprints fit phone and tablet constraints', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final controller = DashboardController(prefs);
    final general = GeneralState(prefs);
    final quotes = DashboardQuotes(prefs);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    for (final width in [390.0, 800.0, 1280.0]) {
      tester.view.physicalSize = Size(width, 1000);
      for (final tile in [
        DashboardTile.analogTimer,
        DashboardTile.digitalTimer,
        DashboardTile.tasks,
        DashboardTile.todayCourses,
        DashboardTile.nextCourse,
        DashboardTile.mode,
        DashboardTile.countdown,
        DashboardTile.calendar,
      ]) {
        for (final size in dashboardTileSizes(
          tile,
          tablet: width >= 600,
          columns: width < 600 ? 2 : 4,
        )) {
          controller.visibleTiles
            ..clear()
            ..add(tile);
          controller.setTileSize(tile, size);
          await tester.pumpWidget(
            MaterialApp(
              home: Scaffold(
                body: DashboardPage(
                  key: UniqueKey(),
                  controller: controller,
                  general: general,
                  quotes: quotes,
                  onConfigure: () {},
                ),
              ),
            ),
          );
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull, reason: '$width $tile $size');
        }
      }
    }
    await tester.pumpWidget(const SizedBox());
    controller.dispose();
    general.dispose();
    quotes.dispose();
  });
}

