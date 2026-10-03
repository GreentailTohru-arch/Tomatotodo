import 'dart:math' as math;

import 'package:flutter/rendering.dart';
import 'package:flutter/widgets.dart';

/// Measures both cards at their column width, then stretches their surfaces to
/// the taller card. Works with scroll views without intrinsic-size queries.
class EqualHeightRow extends MultiChildRenderObjectWidget {
  const EqualHeightRow({super.key, required super.children, this.columns = 2})
    : assert(columns > 0);
  final int columns;

  @override
  RenderObject createRenderObject(BuildContext context) =>
      _EqualHeightRow(columns);

  @override
  void updateRenderObject(
    BuildContext context,
    covariant RenderBox renderObject,
  ) {
    (renderObject as _EqualHeightRow).columns = columns;
  }
}

class _CardParentData extends ContainerBoxParentData<RenderBox> {}

class _EqualHeightRow extends RenderBox
    with
        ContainerRenderObjectMixin<
          RenderBox,
          ContainerBoxParentData<RenderBox>
        >,
        RenderBoxContainerDefaultsMixin<
          RenderBox,
          ContainerBoxParentData<RenderBox>
        > {
  _EqualHeightRow(this._columns);
  int _columns;
  set columns(int value) {
    if (_columns == value) return;
    _columns = value;
    markNeedsLayout();
  }

  @override
  void setupParentData(RenderBox child) {
    if (child.parentData is! ContainerBoxParentData<RenderBox>) {
      child.parentData = _CardParentData();
    }
  }

  @override
  void performLayout() {
    final width = (constraints.maxWidth - 16 * (_columns - 1)) / _columns;
    var height = 0.0;
    var child = firstChild;
    while (child != null) {
      child.layout(BoxConstraints.tightFor(width: width), parentUsesSize: true);
      height = math.max(height, child.size.height);
      child = childAfter(child);
    }
    var x = 0.0;
    child = firstChild;
    while (child != null) {
      child.layout(
        BoxConstraints.tightFor(width: width, height: height),
        parentUsesSize: true,
      );
      (child.parentData! as ContainerBoxParentData<RenderBox>).offset = Offset(
        x,
        0,
      );
      x += width + 16;
      child = childAfter(child);
    }
    size = constraints.constrain(Size(constraints.maxWidth, height));
  }

  @override
  void paint(PaintingContext context, Offset offset) =>
      defaultPaint(context, offset);
  @override
  bool hitTestChildren(BoxHitTestResult result, {required Offset position}) =>
      defaultHitTestChildren(result, position: position);
}
