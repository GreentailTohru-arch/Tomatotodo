import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/general/timer_sound_page.dart';

void main() {
  testWidgets(
    'sound preferences remain independent through adaptive layout and preview',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      const channel = MethodChannel('com.tomatotodo/notification_sounds');
      final calls = <MethodCall>[];
      tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(channel, (
        call,
      ) async {
        calls.add(call);
        if (call.method == 'list') {
          return [
            {'uri': 'content://sound/1', 'name': '轻铃'},
          ];
        }
        return null;
      });
      addTearDown(
        () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
          channel,
          null,
        ),
      );
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      tester.view.physicalSize = const Size(1280, 900);
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(useMaterial3: true),
          home: TimerSoundPage(preferences: prefs),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byType(Card), findsNothing);
      expect(find.byType(RadioListTile<String>), findsNothing);
      await tester.tap(find.text('选择音效'));
      await tester.pumpAndSettle();
      expect(find.byType(AlertDialog), findsOneWidget);
      await tester.tap(find.text('轻铃'));
      await tester.pumpAndSettle();
      expect(prefs.getString('timer_sound_focus_uri'), isNull);
      await tester.tap(find.text('确定'));
      await tester.pumpAndSettle();
      expect(prefs.getString('timer_sound_focus_uri'), 'content://sound/1');
      await tester.tap(find.text('试听音效'));
      await tester.pumpAndSettle();
      expect(calls.last.arguments, 'content://sound/1');
      await tester.tap(find.text('短休结束'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(SwitchListTile, '震动'));
      await tester.pumpAndSettle();
      expect(prefs.getBool('timer_sound_break_vibration'), false);
      expect(prefs.getBool('timer_sound_focus_vibration'), isNull);
      for (final width in [800.0, 390.0]) {
        tester.view.physicalSize = Size(width, 1000);
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
      }
      await tester.pumpWidget(const SizedBox());
      expect(calls.last.method, 'stop');
    },
  );
}
