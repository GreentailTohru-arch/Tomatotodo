import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/core/updates/app_updates.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  final calls = <MethodCall>[];
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    calls.clear();
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(AppUpdates.channel, (call) async {
          calls.add(call);
          if (call.method == 'version') {
            return {'version': '0.1.0', 'build': '1'};
          }
          return null;
        });
  });
  Future<AppUpdates> service({
    String? version = '0.2.0',
    int status = 200,
    String url = 'https://example.com/app.apk',
  }) async => AppUpdates(
    await SharedPreferences.getInstance(),
    client: MockClient((request) async {
      expect(request.url.queryParameters['platform'], 'android');
      return http.Response(
        jsonEncode(
          version == null ? null : {'version': version, 'download_url': url},
        ),
        status,
      );
    }),
  );

  test('numeric comparison pads segments and rejects malformed versions', () {
    expect(compareVersions('1.10', '1.9.9'), greaterThan(0));
    expect(compareVersions('1.2', '1.2.0.0'), 0);
    expect(compareVersions('0.1.0', '1.0'), lessThan(0));
    for (final invalid in ['1', '01.2', '1.2-beta', '1.65536', '1.2.3.4.5']) {
      expect(() => versionParts(invalid), throwsFormatException);
    }
  });
  test(
    'skip survives restart, manual bypasses skip, newer release prompts',
    () async {
      final updates = await service();
      expect(await updates.fetch(manual: false), UpdateResult.available);
      expect(updates.versionLabel, '0.1.0（1）');
      await updates.skip(updates.latest!);
      updates.dispose();
      final restarted = await service(version: '0.2');
      expect(await restarted.fetch(manual: false), UpdateResult.skipped);
      expect(await restarted.fetch(manual: true), UpdateResult.available);
      restarted.dispose();
      final newer = await service(version: '0.3');
      expect(await newer.fetch(manual: false), UpdateResult.available);
      newer.dispose();
    },
  );
  test('equal, older and withdrawn releases do not offer an update', () async {
    for (final version in ['0.1', '0.0.9', null]) {
      final updates = await service(version: version);
      expect(
        await updates.fetch(manual: true),
        version == null ? UpdateResult.unpublished : UpdateResult.current,
      );
      expect(updates.latest, isNull);
      updates.dispose();
    }
  });
  test('invalid responses and unsafe URLs fail closed', () async {
    for (final url in [
      'http://example.com/app.apk',
      'https://user:pass@example.com/a',
      'file:///a.apk',
    ]) {
      final updates = await service(url: url);
      await expectLater(updates.fetch(manual: true), throwsFormatException);
      updates.dispose();
    }
    final offline = await service(status: 503);
    await expectLater(offline.fetch(manual: true), throwsFormatException);
    offline.dispose();
  });
  testWidgets('three update actions; skip then manual download opens HTTPS', (
    tester,
  ) async {
    final updates = await service();
    await tester.pumpWidget(
      MaterialApp(
        theme: ThemeData(useMaterial3: true),
        home: Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              onPressed: () => updates.check(context, manual: true),
              child: const Text('检查'),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('检查'));
    await tester.pumpAndSettle();
    expect(find.text('暂不更新'), findsOneWidget);
    expect(find.text('下载更新'), findsOneWidget);
    await tester.tap(find.text('跳过此版本'));
    await tester.pumpAndSettle();
    expect(updates.preferences.getString(AppUpdates.skippedKey), '0.2.0');
    await tester.tap(find.text('检查'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('下载更新'));
    await tester.pumpAndSettle();
    expect(
      calls.where((c) => c.method == 'openDownload').single.arguments,
      'https://example.com/app.apk',
    );
    updates.dispose();
  });
}
