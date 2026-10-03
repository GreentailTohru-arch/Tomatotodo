import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tomatotodo/core/account/avatar_crop_page.dart';

void main() {
  testWidgets('Avatar crop keeps zoomed and dragged selection within image', (
    tester,
  ) async {
    final recorder = ui.PictureRecorder();
    Canvas(recorder).drawRect(
      const Rect.fromLTWH(0, 0, 800, 400),
      Paint()..color = Colors.green,
    );
    final picture = recorder.endRecording();
    final image = await picture.toImage(800, 400);
    Rect? result;
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: TextButton(
              onPressed: () async {
                result = await Navigator.of(context).push<Rect>(
                  MaterialPageRoute(
                    builder: (_) => AvatarCropPage(image: image),
                  ),
                );
              },
              child: const Text('选择'),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('选择'));
    await tester.pumpAndSettle();
    final slider = tester.widget<Slider>(find.byType(Slider));
    slider.onChanged!(2);
    await tester.pump();
    await tester.drag(find.byType(CustomPaint).last, const Offset(900, 0));
    await tester.pump();
    await tester.tap(find.text('完成'));
    await tester.pumpAndSettle();
    expect(result, isNotNull);
    expect(result!.width, closeTo(200, .01));
    expect(result!.height, result!.width);
    expect(result!.left, greaterThanOrEqualTo(0));
    expect(result!.right, lessThanOrEqualTo(800));
    expect(result!.top, greaterThanOrEqualTo(0));
    expect(result!.bottom, lessThanOrEqualTo(400));
    await tester.pumpWidget(const SizedBox());
    image.dispose();
    picture.dispose();
  });
}
