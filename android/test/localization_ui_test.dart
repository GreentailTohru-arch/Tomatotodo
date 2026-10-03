import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/app/tomatotodo_app.dart';
import 'package:tomatotodo/core/settings/app_language.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';
import 'package:tomatotodo/core/localization/ui_text.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  testWidgets(
    'all locale variants load native controls and correct direction',
    (tester) async {
      await tester.runAsync(UiText.initialize);
      for (final code in AppLanguage.codes.skip(1)) {
        UiText.language = code;
        await tester.pumpWidget(
          MaterialApp(
            key: ValueKey(code),
            locale: AppLanguage.localeFor(code),
            supportedLocales: AppLanguage.locales,
            localizationsDelegates: GlobalMaterialLocalizations.delegates,
            home: Builder(
              builder: (context) => Scaffold(
                body: Column(
                  children: [
                    Text(UiText.t('语言')),
                    TextButton(
                      onPressed: () {},
                      child: Text(
                        MaterialLocalizations.of(context).okButtonLabel,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull, reason: code);
        expect(
          Directionality.of(tester.element(find.byType(Column))),
          ['ar', 'he'].contains(code) ? TextDirection.rtl : TextDirection.ltr,
          reason: code,
        );
      }
      UiText.language = 'zh-CN';
    },
  );
  testWidgets('phone navigation and preset footers fit long and RTL locales', (
    tester,
  ) async {
    await tester.runAsync(UiText.initialize);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    for (final code in AppLanguage.codes.skip(1)) {
      SharedPreferences.setMockInitialValues({'ui.language': code});
      final settings = AppSettings(await SharedPreferences.getInstance());
      tester.view.physicalSize = const Size(360, 800);
      await tester.pumpWidget(
        TomatotodoApp(key: ValueKey(code), settings: settings),
      );
      await tester.pumpAndSettle();
      final initialError = tester.takeException();
      expect(initialError, isNull, reason: '$code dashboard');
      await tester.tap(
        find.widgetWithText(NavigationDestination, UiText.t('配置')),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull, reason: '$code preset page');
      final icons = find.descendant(
        of: find.byType(NavigationBar),
        matching: find.byType(Icon),
      );
      final tops = icons
          .evaluate()
          .map((e) => tester.getTopLeft(find.byWidget(e.widget)).dy)
          .toList();
      expect(
        tops.reduce((a, b) => a > b ? a : b) -
            tops.reduce((a, b) => a < b ? a : b),
        lessThan(1),
        reason: '$code icon baselines',
      );
      for (final key in ['档案', '常规']) {
        await tester.tap(
          find.widgetWithText(NavigationDestination, UiText.t(key)),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull, reason: '$code/$key');
      }
      await tester.pumpWidget(const SizedBox());
    }
    UiText.language = 'zh-CN';
  });
  testWidgets('catalogs preserve every source key and numeric placeholder', (
    tester,
  ) async {
    final catalog = await tester.runAsync(
      () async => jsonDecode(
        await rootBundle.loadString('assets/localization/catalog.json'),
      ),
    ) as Map<String, dynamic>;
    expect(catalog.length, 41);
    final source = catalog['zh-CN'] as Map<String, dynamic>;
    final token = RegExp(r'\{\d+\}');
    for (final code in AppLanguage.codes.skip(1)) {
      final entries = catalog[code] as Map<String, dynamic>;
      expect(entries.keys.toSet(), source.keys.toSet(), reason: code);
      for (final key in source.keys) {
        final value = entries[key] as String;
        expect(value.trim(), isNotEmpty, reason: '$code/$key');
        final expected = token.allMatches(key).map((m) => m[0]).toList()
          ..sort();
        final actual = token.allMatches(value).map((m) => m[0]).toList()
          ..sort();
        expect(actual, expected, reason: '$code/$key');
      }
    }
  });
  testWidgets('changing locale keeps the open settings route and its content', (
    tester,
  ) async {
    await tester.runAsync(UiText.initialize);
    SharedPreferences.setMockInitialValues({'ui.language': 'zh-CN'});
    final settings = AppSettings(await SharedPreferences.getInstance());
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(TomatotodoApp(settings: settings));
    await tester.tap(find.text('常规').last);
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, '通用设置'));
    await tester.pumpAndSettle();
    expect(find.text('语言'), findsOneWidget);
    await settings.setLanguage('en-US');
    await tester.pumpAndSettle();
    expect(find.text('Language'), findsOneWidget);
    expect(find.text('Timer durations'), findsOneWidget);
    expect(find.text('语言'), findsNothing);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    UiText.language = 'zh-CN';
  });
}
