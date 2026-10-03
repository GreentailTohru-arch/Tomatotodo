import 'dart:math' as math;

import 'package:flutter/material.dart';

/// Keep a 16dp edge margin without adding it twice to system navigation space.
class AppFabLocation extends FloatingActionButtonLocation {
  const AppFabLocation();

  @override
  Offset getOffset(ScaffoldPrelayoutGeometry geometry) {
    final offset = FloatingActionButtonLocation.endFloat.getOffset(geometry);
    final inset = geometry.minViewPadding.bottom;
    // Snackbars and sheets keep the standard avoidance behavior.
    final adjustment =
        geometry.snackBarSize.height > 0 || geometry.bottomSheetSize.height > 0
        ? 0.0
        : math.min(16.0, inset);
    return offset.translate(0, adjustment);
  }
}
