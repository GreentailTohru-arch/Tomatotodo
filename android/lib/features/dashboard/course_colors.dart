import 'package:flutter/material.dart';

/// A stable Material color scheme for the same course across the app.
ColorScheme courseColors(BuildContext context, String name) {
  final base = Theme.of(context).colorScheme;
  final hash = name.runes.fold<int>(
    0,
    (value, rune) => (value * 31 + rune) & 0x7fffffff,
  );
  final seed = HSLColor.fromColor(base.primary);
  return ColorScheme.fromSeed(
    seedColor: seed
        .withHue((seed.hue + hash % 360) % 360)
        .withSaturation(.5)
        .toColor(),
    brightness: base.brightness,
  );
}
