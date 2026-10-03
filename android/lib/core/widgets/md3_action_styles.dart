import 'package:flutter/material.dart';

/// Material 3 button variants with animated shape changes on press.
class Md3ActionStyles {
  const Md3ActionStyles._();

  static ButtonStyle pill({double height = 64}) =>
      FilledButton.styleFrom(
        minimumSize: Size(0, height),
        padding: const EdgeInsets.symmetric(horizontal: 24),
        animationDuration: Duration(milliseconds: 180),
      ).copyWith(
        shape: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.pressed)
              ? RoundedRectangleBorder(borderRadius: BorderRadius.circular(20))
              : StadiumBorder(),
        ),
      );

  static ButtonStyle roundedSquare({double size = 72}) =>
      FilledButton.styleFrom(
        minimumSize: Size(size, size),
        maximumSize: Size(size, size),
        padding: const EdgeInsets.all(8),
        animationDuration: Duration(milliseconds: 180),
      ).copyWith(
        shape: WidgetStateProperty.resolveWith(
          (states) => RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(
              states.contains(WidgetState.pressed) ? 12 : 24,
            ),
          ),
        ),
      );

  static ButtonStyle timerAction(
    ColorScheme scheme, {
    required bool running,
    bool reduceMotion = false,
  }) =>
      FilledButton.styleFrom(
        minimumSize: Size(116, 64),
        padding: const EdgeInsets.symmetric(horizontal: 28),
        animationDuration: reduceMotion
            ? Duration.zero
            : Duration(milliseconds: 280),
      ).copyWith(
        backgroundColor: WidgetStatePropertyAll(
          running ? scheme.secondaryContainer : scheme.primary,
        ),
        foregroundColor: WidgetStatePropertyAll(
          running ? scheme.onSecondaryContainer : scheme.onPrimary,
        ),
        elevation: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.pressed) ? 3 : 8,
        ),
        shadowColor: WidgetStatePropertyAll(
          scheme.shadow.withValues(alpha: 0.42),
        ),
        shape: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.pressed)) {
            return RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(18),
            );
          }
          return running
              ? StadiumBorder()
              : RoundedRectangleBorder(borderRadius: BorderRadius.circular(28));
        }),
      );
}
