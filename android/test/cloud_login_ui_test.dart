import 'package:flutter/material.dart';
import 'package:tomatotodo/core/account/cloud_account_ui.dart';

import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/core/account/cloud_account.dart';
import 'package:tomatotodo/core/account/local_profile.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';
import 'package:tomatotodo/features/general/general_state.dart';

void main() {
  testWidgets('remembered login restores securely and unchecking clears it', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    final prefs = (await tester.runAsync(
      () => SharedPreferences.getInstance(),
    ))!;
    final settings = AppSettings(prefs), general = GeneralState(prefs);
    final dashboard = DashboardController(prefs), profile = LocalProfile(prefs);

    String? saved = jsonEncode({
      'email': 'test@example.com',
      'password': 'test-only-password',
    });
    final messenger =
        TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger;
    messenger.setMockMethodCallHandler(
      const MethodChannel('com.tomatotodo/cloud_vault'),
      (call) async {
        if (call.method == 'readLogin') return saved;
        if (call.method == 'writeLogin') saved = call.arguments as String?;
        return null;
      },
    );
    final cloud = CloudAccount(settings, dashboard, general, profile);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              onPressed: () => showCloudLogin(context, cloud),
              child: const Text('open'),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
    final fields = tester
        .widgetList<TextField>(find.byType(TextField))
        .toList();
    expect(fields[0].controller!.text, 'test@example.com');
    expect(fields[1].controller!.text, 'test-only-password');
    expect(fields[1].obscureText, isTrue);
    expect(
      tester.widget<CheckboxListTile>(find.byType(CheckboxListTile)).value,
      isTrue,
    );
    await tester.tap(find.text('记住密码'));
    await tester.pumpAndSettle();
    expect(saved, isNull);
    await tester.tap(find.text('取消'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
    expect(
      tester
          .widgetList<TextField>(find.byType(TextField))
          .last
          .controller!
          .text,
      isEmpty,
    );
    await tester.tap(find.text('取消'));
    await tester.pumpAndSettle();
    cloud.dispose();
    dashboard.dispose();
    general.dispose();
    settings.dispose();
    profile.dispose();
    messenger.setMockMethodCallHandler(
      const MethodChannel('com.tomatotodo/cloud_vault'),
      null,
    );
  });
}
