import 'package:flutter/material.dart';

import 'equal_height_row.dart';

/// Reflows independently sized cards using the actual pane width.
class AdaptiveCardList extends StatelessWidget {
  const AdaptiveCardList({
    super.key,
    required this.children,
    this.padding = const EdgeInsets.all(24),
    this.controller,
    this.header,
    this.equalRowHeights = false,
  });
  final List<Widget> children;
  final EdgeInsets padding;
  final ScrollController? controller;
  final Widget? header;
  final bool equalRowHeights;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, box) {
      final columns = box.maxWidth >= 1200
          ? 3
          : box.maxWidth >= 720
          ? 2
          : 1;
      final width =
          (box.maxWidth - padding.horizontal - (columns - 1) * 16) / columns;
      final cards = children.where((child) => child is! SizedBox).toList();
      return SingleChildScrollView(
        controller: controller,
        padding: padding,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            ?header,
            if (equalRowHeights && columns > 1)
              for (var start = 0; start < cards.length; start += columns)
                Padding(
                  padding: EdgeInsets.only(
                    bottom: start + columns < cards.length ? 16 : 0,
                  ),
                  child: EqualHeightRow(
                    columns: columns,
                    children: cards.skip(start).take(columns).toList(),
                  ),
                )
            else
              Wrap(
                spacing: 16,
                runSpacing: columns == 1 ? 8 : 16,
                children: [
                  for (final child in children)
                    if (child is! SizedBox)
                      SizedBox(width: width, child: child),
                ],
              ),
          ],
        ),
      );
    },
  );
}
