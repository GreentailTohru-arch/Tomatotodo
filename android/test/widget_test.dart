import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/app/tomatotodo_app.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';
import 'package:tomatotodo/features/configuration/configuration_page.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';

void main() {
  setUp(() {
    final view = TestWidgetsFlutterBinding.ensureInitialized()
        .platformDispatcher
        .views
        .first;
    view.physicalSize = const Size(390, 900);
    view.devicePixelRatio = 1;
    addTearDown(view.resetPhysicalSize);
    addTearDown(view.resetDevicePixelRatio);
  });
  testWidgets('history supports select all and long-press range selection', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    tester.view.physicalSize = const Size(390, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    var now = DateTime(2026, 9, 29);
    final controller = DashboardController(preferences, now: () => now);
    for (final title in ['甲任务', '乙任务', '丙任务']) {
      now = now.add(const Duration(milliseconds: 1));
      controller.addTask(title);
      controller.deleteTask(controller.tasks.last.id);
    }
    await tester.pumpWidget(
      MaterialApp(home: ConfigurationPage(controller: controller)),
    );
    await tester.tap(find.byTooltip('历史任务'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('全选'));
    await tester.pumpAndSettle();
    expect(find.text('取消全选'), findsOneWidget);
    expect(
      tester
          .widgetList<CheckboxListTile>(find.byType(CheckboxListTile))
          .every((tile) => tile.value == true),
      isTrue,
    );
    await tester.tap(find.text('取消全选'));
    await tester.pumpAndSettle();
    final first = tester.getCenter(find.text('丙任务'));
    final second = tester.getCenter(find.text('乙任务'));
    final gesture = await tester.startGesture(first);
    await tester.pump(const Duration(milliseconds: 600));
    await gesture.moveTo(second);
    await tester.pumpAndSettle();
    await gesture.up();
    await tester.pumpAndSettle();
    final values = tester
        .widgetList<CheckboxListTile>(find.byType(CheckboxListTile))
        .map((tile) => tile.value)
        .toList();
    expect(values, [true, true, false]);
    await tester.tap(find.text('恢复'));
    await tester.pumpAndSettle();
    expect(find.text('恢复到哪个清单？'), findsOneWidget);
    final destination = find.widgetWithText(ListTile, '重要').last;
    await tester.ensureVisible(destination);
    await tester.pumpAndSettle();
    await tester.tap(destination);
    await tester.pumpAndSettle();
    final target = controller.presets.firstWhere(
      (preset) => preset.name == '重要',
    );
    expect(controller.taskHistory.length, 1);
    expect(controller.tasksFor(target.id).map((task) => task.title).toList(), [
      '丙任务',
      '乙任务',
    ]);
    controller.dispose();
  });

  testWidgets('configuration row aligns number, title and tomatoes', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    controller.addTask('仅标题', estimate: 3);
    controller.addTask('带副标题', subtitle: '补充说明', estimate: 2);
    tester.view.physicalSize = const Size(390, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(home: ConfigurationPage(controller: controller)),
    );
    await tester.pumpAndSettle();
    final titleY = tester.getCenter(find.text('仅标题')).dy;
    expect((tester.getCenter(find.text('01')).dy - titleY).abs(), lessThan(1));
    expect(
      (tester.getCenter(find.text('3 番茄')).dy - titleY).abs(),
      lessThan(1),
    );
    final subtitleRowCenter =
        (tester.getTopLeft(find.text('带副标题')).dy +
            tester.getBottomLeft(find.text('补充说明')).dy) /
        2;
    expect(
      (tester.getCenter(find.text('02')).dy - subtitleRowCenter).abs(),
      lessThan(3),
    );
    expect(
      (tester.getCenter(find.text('2 番茄')).dy - subtitleRowCenter).abs(),
      lessThan(3),
    );
    controller.dispose();
  });

  testWidgets('preset chips stay on one line and task rows scroll locally', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(
      preferences,
      now: () => DateTime(2026, 9, 29),
    );
    final presetId = controller.activePresetId;
    controller.updatePresetSchedule(
      presetId,
      dueAt: DateTime(2026, 10, 1),
      remindAt: DateTime(2026, 9, 30, 18),
      repeat: 'weekly',
    );
    for (var i = 1; i <= 5; i++) {
      controller.addTask('任务 $i');
    }
    tester.view.physicalSize = const Size(390, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(home: ConfigurationPage(controller: controller)),
    );
    await tester.pumpAndSettle();
    expect(
      tester.getTopLeft(find.text('截止 10/1')).dy,
      tester.getTopLeft(find.text('提醒 9/30 18:00')).dy,
    );
    final taskList = find.byType(ListView).last;
    final expandedHeight = tester.getSize(find.byType(Card).first).height;
    await tester.tap(find.byTooltip('收起任务').first);
    await tester.pumpAndSettle();
    expect(
      tester.getSize(find.byType(Card).first).height,
      lessThan(expandedHeight),
    );
    await tester.tap(find.byTooltip('展开任务').first);
    await tester.pumpAndSettle();
    expect(tester.getSize(find.byType(Card).first).height, expandedHeight);
    final scrollable = find
        .descendant(of: taskList, matching: find.byType(Scrollable))
        .first;
    expect(
      tester.state<ScrollableState>(scrollable).position.maxScrollExtent,
      greaterThan(0),
    );
    await tester.drag(taskList, const Offset(0, -180));
    await tester.pumpAndSettle();
    expect(
      tester.state<ScrollableState>(scrollable).position.pixels,
      greaterThan(0),
    );
    controller.dispose();
  });

  testWidgets('editor reveals delete only after right swipe', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final controller = DashboardController(preferences);
    controller.addTask('无副标题', estimate: 3);
    await tester.pumpWidget(
      MaterialApp(home: ConfigurationPage(controller: controller)),
    );
    await tester.tap(find.byTooltip('更多').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('编辑').last);
    await tester.pumpAndSettle();
    final tile = tester.widget<ListTile>(find.widgetWithText(ListTile, '无副标题'));
    expect(tile.subtitle, isNull);
    final row = find
        .ancestor(of: find.text('无副标题'), matching: find.byType(Card))
        .first;
    await tester.drag(row, const Offset(110, 0));
    await tester.pumpAndSettle();
    expect(controller.activeTasks.single.title, '无副标题');
    await tester.tap(find.byTooltip('编辑任务'));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(AlertDialog, '编辑任务'), findsOneWidget);
    await tester.tap(find.text('取消').last);
    await tester.pumpAndSettle();
    await tester.drag(row, const Offset(-150, 0));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.delete_outline).last);
    await tester.pumpAndSettle();
    expect(controller.activeTasks, isEmpty);
    controller.dispose();
  });

  testWidgets('Preset menu exposes schedule and repeat controls', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    await tester.pumpWidget(TomatotodoApp(settings: AppSettings(preferences)));
    await tester.tap(find.widgetWithText(NavigationDestination, '配置'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('更多').first);
    await tester.pumpAndSettle();
    for (final label in ['设为当前', '编辑', '截止日期', '提醒我', '重复', '删除']) {
      expect(find.text(label), findsOneWidget);
    }
    await tester.tap(find.text('重复'));
    await tester.pumpAndSettle();
    expect(find.text('每天'), findsOneWidget);
    expect(find.text('工作日'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('自定义'),
      200,
      scrollable: find
          .descendant(
            of: find.byType(BottomSheet),
            matching: find.byType(Scrollable),
          )
          .first,
    );
    expect(find.text('自定义'), findsOneWidget);
  });

  testWidgets('Account avatar opens the local account drawer', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    await tester.pumpWidget(TomatotodoApp(settings: AppSettings(preferences)));
    await tester.tap(find.byTooltip('账户').first);
    await tester.pumpAndSettle();
    expect(find.text('本地账户 · 仅保存在此设备'), findsOneWidget);
    expect(find.text('云端账户'), findsOneWidget);
    expect(find.text('注册或登录，同步电脑与手机的数据'), findsOneWidget);
    await tester.tap(find.text('云端账户'));
    await tester.pumpAndSettle();
    expect(find.text('登录云端账户'), findsOneWidget);
    await tester.tap(find.text('注册'));
    await tester.pumpAndSettle();
    expect(find.text('创建云端账户'), findsOneWidget);
    expect(find.text('激活码'), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.tap(find.text('取消'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('修改名称'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).last, '小番茄');
    await tester.tap(find.text('保存'));
    await tester.pumpAndSettle();
    expect(find.text('小番茄'), findsWidgets);
    expect(preferences.getString('local_profile_name'), '小番茄');
    expect(tester.takeException(), isNull);
  });

  testWidgets('Configuration add preset stays floating and opens editor', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    await tester.pumpWidget(TomatotodoApp(settings: AppSettings(preferences)));
    await tester.tap(find.widgetWithText(NavigationDestination, '配置'));
    await tester.pumpAndSettle();
    expect(find.byType(FloatingActionButton), findsOneWidget);
    expect(find.widgetWithText(FloatingActionButton, '添加预设'), findsOneWidget);
    await tester.tap(find.widgetWithText(FloatingActionButton, '添加预设'));
    await tester.pumpAndSettle();
    expect(find.byType(AlertDialog), findsOneWidget);
    expect(find.text('清单名称'), findsOneWidget);
    await tester.tap(find.text('取消'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(NavigationDestination, '仪表盘'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(NavigationDestination, '配置'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });

  testWidgets('Four destinations and nested persistent personalization', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final settings = AppSettings(preferences);
    await tester.pumpWidget(TomatotodoApp(settings: settings));
    expect(find.byType(NavigationDestination), findsNWidgets(4));
    expect(find.text('25:00'), findsOneWidget);
    expect(find.byTooltip('添加模块'), findsNothing);
    expect(find.text('开始'), findsOneWidget);
    await tester.tap(find.widgetWithText(NavigationDestination, '常规'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.widgetWithText(ListTile, '个性化'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, '个性化'));
    await tester.pumpAndSettle();
    expect(find.text('主题色彩'), findsOneWidget);
    await tester.tap(find.text('深色'));
    await tester.pumpAndSettle();
    expect(settings.themeMode, ThemeMode.dark);
    expect(preferences.getString('themeMode'), 'dark');
    await tester.tap(find.bySemanticsLabel('蓝海主题色'));
    await tester.pumpAndSettle();
    expect(preferences.getInt('seedColor'), 0xFF386A9B);
    await tester.tap(find.text('调性点缀'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('高保真'));
    await tester.pumpAndSettle();
    expect(settings.schemeVariant, DynamicSchemeVariant.fidelity);
    expect(preferences.getString('schemeVariant'), 'fidelity');
    await tester.scrollUntilVisible(find.text('纯黑模式'), 200);
    await tester.tap(find.text('纯黑模式'));
    await tester.pumpAndSettle();
    expect(settings.pureBlack, isTrue);
    expect(
      Theme.of(tester.element(find.text('纯黑模式'))).colorScheme.surface,
      Colors.black,
    );
    expect(settings.exportData()['schemeVariant'], 'fidelity');
    final theme = Theme.of(tester.element(find.text('纯黑模式')));
    expect(theme.useMaterial3, isTrue);
    expect(theme.brightness, Brightness.dark);
  });
}
