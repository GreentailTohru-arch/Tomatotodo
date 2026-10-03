import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/material.dart';

/// The square crop is stored intact; the circular guide matches account avatars.
class AvatarCropPage extends StatefulWidget {
  const AvatarCropPage({super.key, required this.image});
  final ui.Image image;

  @override
  State<AvatarCropPage> createState() => _AvatarCropPageState();
}

class _AvatarCropPageState extends State<AvatarCropPage> {
  double zoom = 1;
  double startZoom = 1;
  Offset center = Offset.zero;
  Offset startCenter = Offset.zero;
  Offset startFocal = Offset.zero;
  double frameSide = 1;

  double get sourceSide =>
      math.min(widget.image.width, widget.image.height).toDouble();

  Rect get crop {
    final side = sourceSide / zoom;
    final cx = (widget.image.width / 2 + center.dx).clamp(
      side / 2,
      widget.image.width - side / 2,
    );
    final cy = (widget.image.height / 2 + center.dy).clamp(
      side / 2,
      widget.image.height - side / 2,
    );
    return Rect.fromCenter(center: Offset(cx, cy), width: side, height: side);
  }

  void clampCenter() => center =
      crop.center - Offset(widget.image.width / 2, widget.image.height / 2);

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final colors = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(
        title: Text(UiText.t("裁剪头像")),
        actions: [
          IconButton(
            tooltip: UiText.t("重置裁剪"),
            icon: Icon(Icons.restart_alt),
            onPressed: () => setState(() {
              zoom = 1;
              center = Offset.zero;
            }),
          ),
          Padding(
            padding: EdgeInsets.only(right: 12),
            child: FilledButton(
              onPressed: () => Navigator.pop(context, crop),
              child: Text(UiText.t("完成")),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: LayoutBuilder(
                builder: (context, constraints) {
                  frameSide = math
                      .min(
                        constraints.maxWidth - 48,
                        constraints.maxHeight - 48,
                      )
                      .clamp(1, 480)
                      .toDouble();
                  return GestureDetector(
                    behavior: HitTestBehavior.opaque,
                    onScaleStart: (details) {
                      startZoom = zoom;
                      startCenter = center;
                      startFocal = details.localFocalPoint;
                    },
                    onScaleUpdate: (details) => setState(() {
                      zoom = (startZoom * details.scale).clamp(1, 5);
                      center =
                          startCenter -
                          (details.localFocalPoint - startFocal) *
                              (sourceSide / zoom / frameSide);
                      clampCenter();
                    }),
                    child: CustomPaint(
                      size: Size(constraints.maxWidth, constraints.maxHeight),
                      painter: _CropPainter(
                        widget.image,
                        crop,
                        frameSide,
                        colors.primary,
                      ),
                    ),
                  );
                },
              ),
            ),
            Text(UiText.t("拖动照片调整位置，双指缩放")),
            Padding(
              padding: EdgeInsets.fromLTRB(24, 12, 24, 24),
              child: Row(
                children: [
                  Icon(Icons.zoom_out),
                  Expanded(
                    child: Slider(
                      value: zoom,
                      min: 1,
                      max: 5,
                      label: UiText.f("{0} 倍", [zoom.toStringAsFixed(1)]),
                      onChanged: (value) => setState(() {
                        zoom = value;
                        clampCenter();
                      }),
                    ),
                  ),
                  Icon(Icons.zoom_in),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CropPainter extends CustomPainter {
  _CropPainter(this.image, this.crop, this.side, this.accent);
  final ui.Image image;
  final Rect crop;
  final double side;
  final Color accent;

  @override
  void paint(Canvas canvas, Size size) {
    final frame = Rect.fromCenter(
      center: size.center(Offset.zero),
      width: side,
      height: side,
    );
    final scale = side / crop.width;
    final destination = Rect.fromLTWH(
      frame.left - crop.left * scale,
      frame.top - crop.top * scale,
      image.width * scale,
      image.height * scale,
    );
    canvas.drawImageRect(
      image,
      Rect.fromLTWH(0, 0, image.width.toDouble(), image.height.toDouble()),
      destination,
      Paint()..filterQuality = FilterQuality.high,
    );
    final mask = Path()
      ..fillType = PathFillType.evenOdd
      ..addRect(Offset.zero & size)
      ..addOval(frame);
    canvas.drawPath(mask, Paint()..color = Colors.black.withValues(alpha: .6));
    canvas.drawOval(
      frame,
      Paint()
        ..color = accent
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2,
    );
    // Corner brackets make the underlying square output boundary explicit.
    final brackets = Path();
    for (final x in [frame.left, frame.right]) {
      for (final y in [frame.top, frame.bottom]) {
        final dx = x == frame.left ? 20.0 : -20.0;
        final dy = y == frame.top ? 20.0 : -20.0;
        brackets
          ..moveTo(x, y + dy)
          ..lineTo(x, y)
          ..lineTo(x + dx, y);
      }
    }
    canvas.drawPath(
      brackets,
      Paint()
        ..color = accent
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3
        ..strokeCap = StrokeCap.round,
    );
  }

  @override
  bool shouldRepaint(_CropPainter oldDelegate) =>
      oldDelegate.crop != crop ||
      oldDelegate.side != side ||
      oldDelegate.accent != accent ||
      oldDelegate.image != image;
}
