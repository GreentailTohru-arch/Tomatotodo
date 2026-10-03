import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/general/about_credits.dart';

void main() {
  testWidgets(
    'multiple people stack, sponsors wrap, only first developer has working sponsor action',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      tester.view.physicalSize = const Size(390, 1200);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      Map<String, dynamic> person(String name) => {
        'name': name,
        'description': '',
        'url': 'https://example.com/profile',
        'avatar': '',
      };
      final data = {
        'developers': [person('Ren'), person('Dev Two')],
        'thanks': [person('Thanks A'), person('Thanks B')],
        'sponsors': List.generate(10, (i) => person('Support $i')),
        'sponsor_url': 'https://example.com/support',
      };
      final client = MockClient(
        (request) async => http.Response(jsonEncode(data), 200),
      );
      Object? opened;
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(
            const MethodChannel('com.tomatotodo/app_updates'),
            (call) async {
              opened = call.arguments;
              return null;
            },
          );
      addTearDown(
        () => TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
            .setMockMethodCallHandler(
              const MethodChannel('com.tomatotodo/app_updates'),
              null,
            ),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(child: AboutCredits(client: client)),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('赞助 Tomatotodo'), findsOneWidget);
      expect(
        tester.getTopLeft(find.text('Dev Two')).dy,
        greaterThan(tester.getTopLeft(find.text('Ren')).dy),
      );
      expect(
        tester.getTopLeft(find.byTooltip('Support 9')).dy,
        greaterThan(tester.getTopLeft(find.byTooltip('Support 0')).dy),
      );
      await tester.tap(find.text('赞助 Tomatotodo'));
      await tester.pump();
      expect(opened, 'https://example.com/support');
      await tester.tap(find.byTooltip('Ren'));
      await tester.pump();
      expect(opened, 'https://example.com/profile');
      expect(tester.takeException(), isNull);
    },
  );
}
