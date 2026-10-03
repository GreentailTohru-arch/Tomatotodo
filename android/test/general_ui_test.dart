import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/app/tomatotodo_app.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';

void main() {
  testWidgets(
    'tablet sound detail preserves category navigation and returns inline',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      debugDefaultTargetPlatformOverride = TargetPlatform.android;
      addTearDown(() => debugDefaultTargetPlatformOverride = null);
      const channel = MethodChannel('com.tomatotodo/notification_sounds');
      tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        channel,
        (call) async => call.method == 'list' ? [] : null,
      );
      addTearDown(
        () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
          channel,
          null,
        ),
      );
      tester.view.physicalSize = const Size(1280, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(TomatotodoApp(settings: AppSettings(prefs)));
      await tester.tap(find.text('常规').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('番茄时钟通知音效'));
      await tester.pumpAndSettle();
      expect(find.byType(NavigationRail), findsOneWidget);
      expect(find.byType(SearchBar), findsOneWidget);
      expect(find.widgetWithText(ListTile, '个性化'), findsOneWidget);
      expect(find.text('开启音效'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.tap(find.byTooltip('返回通用设置'));
      await tester.pumpAndSettle();
      expect(find.text('计时节奏'), findsOneWidget);
      expect(find.text('开启音效'), findsNothing);
      await tester.pumpWidget(const SizedBox());
      debugDefaultTargetPlatformOverride = null;
    },
  );

  testWidgets('general settings navigate through Pixel-style category pages', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(TomatotodoApp(settings: AppSettings(preferences)));
    await tester.tap(find.widgetWithText(NavigationDestination, '常规'));
    await tester.pumpAndSettle();
    expect(find.text('常规'), findsWidgets);
    expect(find.byType(SearchBar), findsOneWidget);
    expect(find.byType(ExpansionTile), findsNothing);
    expect(find.widgetWithText(ListTile, '通用设置'), findsOneWidget);
    expect(find.widgetWithText(ListTile, '账户与用户数据'), findsOneWidget);

    await tester.tap(find.byType(SearchBar));
    await tester.pumpAndSettle();
    expect(find.text('搜索建议'), findsOneWidget);
    expect(find.text('最近搜索'), findsNothing);
    await tester.enterText(find.byType(SearchBar).last, '进度指示器');
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, '仪表盘').last);
    await tester.pumpAndSettle();
    expect(find.text('进度指示器'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    await tester.tap(find.byType(SearchBar));
    await tester.pumpAndSettle();
    expect(find.text('最近搜索'), findsOneWidget);
    expect(find.text('进度指示器'), findsOneWidget);
    await tester.tap(find.text('清除'));
    await tester.pumpAndSettle();
    expect(find.text('最近搜索'), findsNothing);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(ListTile, '通用设置'));
    await tester.pumpAndSettle();
    expect(find.text('计时节奏'), findsOneWidget);
    expect(find.text('系统消息窗常驻'), findsOneWidget);
    await tester.tap(find.text('计时节奏'));
    await tester.pumpAndSettle();
    expect(find.text('专注分钟数（1–180）'), findsOneWidget);
    await tester.tap(find.text('取消'));
    await tester.pumpAndSettle();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(ListTile, '仪表盘').last);
    await tester.pumpAndSettle();
    final dialTicks = find.widgetWithText(SwitchListTile, '显示计时刻度');
    expect(tester.widget<SwitchListTile>(dialTicks).value, isFalse);
    await tester.tap(dialTicks);
    await tester.pumpAndSettle();
    expect(tester.widget<SwitchListTile>(dialTicks).value, isTrue);
    expect(find.text('进度指示器'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(ListTile, '课程表'));
    await tester.pumpAndSettle();
    expect(find.text('导入课程表'), findsOneWidget);
    await tester.tap(find.widgetWithText(SwitchListTile, '课表开关'));
    await tester.pumpAndSettle();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(
      tester
          .widgetList<NavigationDestination>(find.byType(NavigationDestination))
          .map((item) => item.label),
      ['仪表盘', '配置', '课表', '档案', '常规'],
    );
    await tester.tap(find.widgetWithText(NavigationDestination, '课表'));
    await tester.pumpAndSettle();
    expect(find.text('课程表'), findsOneWidget);
    expect(find.text('无课程'), findsWidgets);
    expect(tester.takeException(), isNull);
  });
}
