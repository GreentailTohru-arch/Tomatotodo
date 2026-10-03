import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/app/tomatotodo_app.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';

void main() {
  testWidgets(
    'tablet landscape, portrait and compact preserve navigation and timer',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      tester.view.devicePixelRatio = 1;
      tester.view.physicalSize = const Size(1280, 800);
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(TomatotodoApp(settings: AppSettings(prefs)));
      await tester.pumpAndSettle();
      expect(find.byType(NavigationRail), findsOneWidget);
      expect(find.byType(NavigationBar), findsNothing);
      final timer = tester.getRect(
        find.byKey(const ValueKey('tile-analogTimer')),
      );
      final tasks = tester.getRect(find.byKey(const ValueKey('tile-tasks')));
      expect(tasks.left, greaterThan(timer.right));
      expect(tasks.top, timer.top);
    await tester.tap(find.text('档案').last);
    await tester.pumpAndSettle();
    final cards = find.byWidgetPredicate((widget) => widget is KeyedSubtree && widget.key.toString().contains('tablet-archive-'));
    expect(cards, findsNWidgets(4));
    expect(tester.getSize(cards.at(0)).height, tester.getSize(cards.at(1)).height);
    expect(tester.getSize(cards.at(2)).height, tester.getSize(cards.at(3)).height);
    expect(tester.takeException(), isNull);
    await tester.tap(find.text('常规').last);
      await tester.pumpAndSettle();
      expect(find.byType(VerticalDivider), findsOneWidget);
      expect(find.text('系统消息窗常驻'), findsOneWidget);
      tester.view.physicalSize = const Size(800, 1280);
      await tester.pumpAndSettle();
      expect(find.byType(NavigationRail), findsOneWidget);
      expect(tester.takeException(), isNull);
      tester.view.physicalSize = const Size(390, 844);
      await tester.pumpAndSettle();
      expect(find.byType(NavigationBar), findsOneWidget);
      await tester.tap(find.widgetWithText(NavigationDestination, '仪表盘'));
      await tester.pumpAndSettle();
      expect(find.text('25:00'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );
}
