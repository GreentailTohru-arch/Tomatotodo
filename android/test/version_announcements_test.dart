import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/core/updates/version_announcements.dart';

void main() {
  testWidgets(
    'normal is remembered, urgent returns next launch; acknowledge is the only visible dismiss action',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      VersionAnnouncements create() => VersionAnnouncements(
        preferences,
        client: MockClient((request) async {
          expect(request.url.queryParameters, {
            'platform': 'android',
            'version': '1.0.0',
          });
          return http.Response(
            jsonEncode([
              for (final severity in ['normal', 'urgent'])
                {
                  'id': severity,
                  'platform': 'android',
                  'version': '1.0.0.0',
                  'active': true,
                  'title': '$severity title',
                  'subtitle': 'subtitle',
                  'severity': severity,
                },
            ]),
            200,
          );
        }),
      );
      var announcements = create();
      Future<void> launch({bool manual = false}) async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: Builder(
                builder: (context) => TextButton(
                  onPressed: () => announcements.showStartup(context, '1.0.0', manual: manual),
                  child: const Text('启动'),
                ),
              ),
            ),
          ),
        );
        await tester.tap(find.text('启动'));
        await tester.pumpAndSettle();
      }

      await launch();
      expect(find.text('normal title'), findsOneWidget);
      expect(find.byIcon(Icons.close), findsNothing);
      await tester.tap(find.text('我知道了'));
      await tester.pumpAndSettle();
      expect(find.text('urgent title'), findsOneWidget);
      await tester.tap(find.text('我知道了'));
      await tester.pumpAndSettle();
      expect(preferences.getStringList(VersionAnnouncements.readKey), [
        'normal',
      ]);
      announcements.dispose();
      announcements = create();
      await launch();
      expect(find.text('normal title'), findsNothing);
      expect(find.text('urgent title'), findsOneWidget);
      expect(find.byIcon(Icons.close), findsNothing);
      await tester.tap(find.text('我知道了'));
      await tester.pumpAndSettle();
      await launch(manual: true);
      expect(find.text('normal title'), findsOneWidget);
      await tester.tap(find.text('我知道了'));
      await tester.pumpAndSettle();
      expect(find.text('urgent title'), findsOneWidget);
      expect(find.byIcon(Icons.close), findsNothing);
      await tester.tap(find.text('我知道了'));
      await tester.pumpAndSettle();
      announcements.dispose();
    },
  );
}
