import 'package:tomatotodo/core/localization/ui_text.dart';

import 'dart:math' as math;
import 'dart:async';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import '../../core/account/account_drawer.dart';
import '../../core/account/local_profile.dart';

import 'dashboard_controller.dart';
import 'dashboard_quotes.dart';
import 'course_dashboard_data.dart';
import 'course_colors.dart';
import '../general/general_state.dart';
import 'task_dialog.dart';

class DashboardPage extends StatefulWidget {
  const DashboardPage({
    super.key,
    required this.controller,
    required this.general,
    required this.quotes,
    required this.onConfigure,
    this.profile,
    this.onAccountPressed,
    this.onEditingChanged,
  });
  final DashboardController controller;
  final GeneralState general;
  final DashboardQuotes quotes;
  final VoidCallback onConfigure;
  final LocalProfile? profile;
  final VoidCallback? onAccountPressed;
  final ValueChanged<bool>? onEditingChanged;

  @override
  State<DashboardPage> createState() => _DashboardPageState();
}

class _DashboardPageState extends State<DashboardPage> {
  bool _editing = false;
  DashboardTile? _resizingTile;
  TileSize? _resizePreview;
  TileSize? _resizeStart;
  double _resizeScrollStart = 0;
  Offset _resizeDelta = Offset.zero;
  double _gridStride = 1;
  int _gridColumns = 2;
  bool get _tablet => MediaQuery.sizeOf(context).width >= 600;

  TileSize _sizeFor(DashboardTile tile) =>
      _resizingTile == tile && _resizePreview != null
      ? _resizePreview!
      : widget.controller.tileSize(
          tile,
          tablet: _tablet,
          columns: _gridColumns,
        );

  List<TileSize> _sizesFor(DashboardTile tile) =>
      dashboardTileSizes(tile, tablet: _tablet, columns: _gridColumns);

  void _resizeBy(Offset delta) {
    _resizeDelta = delta;
    if (_resizingTile == null || _resizeStart == null) return;
    final x = _resizeStart!.columns + delta.dx / _gridStride;
    final y =
        _resizeStart!.rows +
        (delta.dy + _pageScroll.offset - _resizeScrollStart) / _gridStride;
    double distance(TileSize size) =>
        math.pow(size.columns - x, 2).toDouble() +
        math.pow(size.rows - y, 2).toDouble();
    final candidates = _sizesFor(_resizingTile!)
      ..sort((a, b) => distance(a).compareTo(distance(b)));
    final next = candidates.first;
    // A small hysteresis zone prevents jitter at a cell boundary.
    if (next != _resizePreview &&
        distance(next) + .18 < distance(_resizePreview!)) {
      HapticFeedback.selectionClick();
      setState(() => _resizePreview = next);
    }
  }

  void _finishResize({bool cancel = false}) {
    _stopEdgeScroll();
    if (!cancel && _resizingTile != null && _resizePreview != null) {
      widget.controller.setTileSize(_resizingTile!, _resizePreview!);
    }
    setState(() {
      _resizingTile = null;
      _resizePreview = null;
      _resizeStart = null;
    });
  }

  Future<void> _chooseSize(DashboardTile tile) async {
    final result = await showModalBottomSheet<TileSize>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Padding(
          padding: EdgeInsets.fromLTRB(24, 0, 24, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                UiText.f("调整{0}尺寸", [tile.label]),
                style: Theme.of(context).textTheme.titleLarge,
              ),
              SizedBox(height: 16),
              Wrap(
                spacing: 12,
                runSpacing: 8,
                children: [
                  for (final size in _sizesFor(tile))
                    ChoiceChip(
                      label: Text('${size.columns} × ${size.rows}'),
                      selected: _sizeFor(tile) == size,
                      onSelected: (_) => Navigator.pop(context, size),
                    ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
    if (result != null && mounted) widget.controller.setTileSize(tile, result);
  }

  final ScrollController _pageScroll = ScrollController();
  Timer? _edgeScrollTimer;
  Offset? _dragPointer;
  final GlobalKey _gridAreaKey = GlobalKey();
  final Map<DashboardTile, GlobalKey> _tileKeys = {};
  DashboardTile? _hoverTile;
  bool _hoverAfter = false;
  DashboardTile? _draggingTile;
  List<DashboardTile>? _previewTiles;
  Offset? _lastPreviewPosition;

  @override
  void dispose() {
    _edgeScrollTimer?.cancel();
    _pageScroll.dispose();
    super.dispose();
  }

  void _updateEdgeScroll(Offset position) {
    _dragPointer = position;
    _edgeScrollTimer ??= Timer.periodic(Duration(milliseconds: 16), (_) {
      if (!_pageScroll.hasClients ||
          (_draggingTile == null && _resizingTile == null)) {
        return;
      }
      if (_dragPointer == null || !mounted) return;
      final y = _dragPointer!.dy;
      final top = MediaQuery.paddingOf(context).top + 92;
      final bottom =
          MediaQuery.sizeOf(context).height -
          MediaQuery.paddingOf(context).bottom -
          148;
      final strength = y < top
          ? -((top - y) / 76).clamp(0.0, 1.0)
          : y > bottom
          ? ((y - bottom) / 76).clamp(0.0, 1.0)
          : 0.0;
      if (strength == 0) return;
      final position = _pageScroll.position;
      final next = (position.pixels + strength * 14).clamp(
        position.minScrollExtent,
        position.maxScrollExtent,
      );
      if (next != position.pixels) {
        position.jumpTo(next);
        if (_draggingTile != null) {
          _previewNearest(_draggingTile!, _dragPointer!);
        }
        if (_resizingTile != null) _resizeBy(_resizeDelta);
      }
    });
  }

  void _stopEdgeScroll() {
    _edgeScrollTimer?.cancel();
    _edgeScrollTimer = null;
    _dragPointer = null;
  }

  bool _dropAfter(DashboardTile tile, Offset globalPosition) {
    final box =
        _tileKeys[tile]?.currentContext?.findRenderObject() as RenderBox?;
    if (box == null) return false;
    final local = box.globalToLocal(globalPosition);
    if (local.dy < 0 || local.dx < 0) return false;
    if (local.dy > box.size.height || local.dx > box.size.width) return true;
    return box.size.width >= box.size.height
        ? local.dx > box.size.width / 2
        : local.dy > box.size.height / 2;
  }

  void _previewNearest(DashboardTile moved, Offset position) {
    DashboardTile? nearest;
    var distance = double.infinity;
    for (final tile in _previewTiles ?? widget.controller.visibleTiles) {
      if (tile == moved) continue;
      final box =
          _tileKeys[tile]?.currentContext?.findRenderObject() as RenderBox?;
      if (box == null || !box.hasSize) continue;
      final rect = box.localToGlobal(Offset.zero) & box.size;
      final dx = position.dx < rect.left
          ? rect.left - position.dx
          : position.dx > rect.right
          ? position.dx - rect.right
          : 0.0;
      final dy = position.dy < rect.top
          ? rect.top - position.dy
          : position.dy > rect.bottom
          ? position.dy - rect.bottom
          : 0.0;
      final current = dx * dx + dy * dy;
      if (current < distance) {
        distance = current;
        nearest = tile;
      }
    }
    if (nearest != null) _previewMove(moved, nearest, position);
  }

  bool _insideGrid(Offset globalPosition) {
    final box = _gridAreaKey.currentContext?.findRenderObject() as RenderBox?;
    if (box == null) return false;
    final local = box.globalToLocal(globalPosition);
    return (Offset.zero & box.size).contains(local);
  }

  void _finishDrag(DraggableDetails details) {
    _stopEdgeScroll();
    final preview = _previewTiles;
    if (preview != null &&
        (details.wasAccepted || _insideGrid(details.offset))) {
      widget.controller.setTileOrder(preview);
    }
    setState(() {
      _draggingTile = null;
      _previewTiles = null;
      _hoverTile = null;
      _lastPreviewPosition = null;
    });
  }

  void _enterEditMode() {
    if (_editing) return;
    HapticFeedback.mediumImpact();
    setState(() => _editing = true);
    widget.onEditingChanged?.call(true);
  }

  void _leaveEditMode() {
    if (_resizingTile != null) _finishResize();
    _stopEdgeScroll();
    setState(() {
      _editing = false;
      _draggingTile = null;
      _previewTiles = null;
      _hoverTile = null;
    });
    widget.onEditingChanged?.call(false);
  }

  void _previewMove(
    DashboardTile moved,
    DashboardTile target,
    Offset position,
  ) {
    if (_draggingTile != moved) return;
    final last = _lastPreviewPosition;
    if (last != null &&
        (last - position).distance < 12 &&
        _hoverTile != target) {
      return;
    }
    final after = _dropAfter(target, position);
    final next = reorderedDashboardTiles(
      _previewTiles ?? widget.controller.visibleTiles,
      moved,
      target,
      after: after,
    );
    if (_hoverTile != target ||
        _hoverAfter != after ||
        !listEquals(next, _previewTiles)) {
      setState(() {
        _hoverTile = target;
        _hoverAfter = after;
        _previewTiles = next;
        _lastPreviewPosition = position;
      });
    }
  }

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => AnimatedBuilder(
      animation: Listenable.merge([
        widget.controller,
        widget.general,
        widget.quotes,
      ]),
      builder: (context, _) => CustomScrollView(
        key: PageStorageKey('dashboard-scroll'),
        controller: _pageScroll,
        slivers: [
          SliverAppBar(
            pinned: true,
            title: Text(_editing ? UiText.t("调整仪表盘") : UiText.t("仪表盘")),
            actions: [
              if (_editing)
                IconButton.filledTonal(
                  tooltip: UiText.t("完成编辑"),
                  onPressed: _leaveEditMode,
                  icon: Icon(Icons.check),
                ),
              if (_editing)
                DragTarget<DashboardTile>(
                  onWillAcceptWithDetails: (_) => _draggingTile != null,
                  onAcceptWithDetails: (details) {
                    _previewTiles = null;
                    widget.controller.setTileVisible(details.data, false);
                    HapticFeedback.mediumImpact();
                  },
                  builder: (context, candidates, _) => AnimatedSize(
                    duration: MediaQuery.disableAnimationsOf(context)
                        ? Duration.zero
                        : Duration(milliseconds: 220),
                    curve: Curves.easeOutCubic,
                    alignment: AlignmentDirectional.centerEnd,
                    child: _draggingTile == null
                        ? IconButton.filledTonal(
                            tooltip: UiText.t("添加模块"),
                            onPressed: () => _showLibrary(context),
                            icon: Icon(Icons.add),
                          )
                        : Container(
                            key: ValueKey('remove-tile-target'),
                            padding: EdgeInsets.symmetric(
                              horizontal: 16,
                              vertical: 12,
                            ),
                            decoration: ShapeDecoration(
                              shape: StadiumBorder(),
                              color: candidates.isNotEmpty
                                  ? Theme.of(context).colorScheme.error
                                  : Theme.of(context)
                                        .colorScheme
                                        .errorContainer,
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Icon(
                                  Icons.delete_outline,
                                  color: candidates.isNotEmpty
                                      ? Theme.of(context).colorScheme.onError
                                      : Theme.of(context)
                                            .colorScheme
                                            .onErrorContainer,
                                ),
                                SizedBox(width: 6),
                                Text(
                                  UiText.t("移除"),
                                  style: Theme.of(context).textTheme.labelLarge
                                      ?.copyWith(
                                        color: candidates.isNotEmpty
                                            ? Theme.of(context)
                                                  .colorScheme
                                                  .onError
                                            : Theme.of(context)
                                                  .colorScheme
                                                  .onErrorContainer,
                                      ),
                                ),
                              ],
                            ),
                          ),
                  ),
                ),
              if (widget.profile != null && widget.onAccountPressed != null)
                AccountAvatarButton(
                  profile: widget.profile!,
                  onPressed: widget.onAccountPressed!,
                ),
              SizedBox(width: 8),
            ],
          ),
          SliverToBoxAdapter(
            child: SafeArea(
              top: false,
              child: Center(
                child: ConstrainedBox(
                  constraints: BoxConstraints(maxWidth: 1440),
                  child: GestureDetector(
                    key: _gridAreaKey,
                    behavior: HitTestBehavior.translucent,
                    onLongPress: _enterEditMode,
                    child: Padding(
                      padding: EdgeInsets.fromLTRB(16, 8, 16, 112),
                      child: LayoutBuilder(
                        builder: (context, constraints) {
                          const gap = 12.0;
                          final width = constraints.maxWidth;
                          final columnCount = width >= 1100
                              ? 6
                              : width >= 680
                              ? 4
                              : width >= 500
                              ? 3
                              : 2;
                          final stride = (width + gap) / columnCount;
                          _gridStride = stride;
                          _gridColumns = columnCount;
                          final tiles =
                              _previewTiles ?? widget.controller.visibleTiles;
                          final offsets = List<double>.filled(columnCount, 0);
                          final positions = <DashboardTile, Rect>{};
                          for (final tile in tiles) {
                            final size = _sizeFor(tile);
                            final columns = size.columns;
                            var column = 0;
                            var top = double.infinity;
                            for (
                              var candidate = 0;
                              candidate <= columnCount - columns;
                              candidate++
                            ) {
                              final candidateTop = offsets
                                  .sublist(candidate, candidate + columns)
                                  .reduce(math.max);
                              if (candidateTop < top) {
                                column = candidate;
                                top = candidateTop;
                              }
                            }
                            final height = stride * size.rows - gap;
                            positions[tile] = Rect.fromLTWH(
                              column * stride,
                              top,
                              stride * columns - gap,
                              height,
                            );
                            for (var i = column; i < column + columns; i++) {
                              offsets[i] = top + height + gap;
                            }
                          }
                          final height = offsets.reduce(math.max) - gap;
                          final duration =
                              MediaQuery.disableAnimationsOf(context)
                              ? Duration.zero
                              : Duration(milliseconds: 280);
                          return SizedBox(
                            height: math.max(0, height),
                            child: Stack(
                              children: [
                                for (final tile in tiles)
                                  AnimatedPositioned(
                                    key: ValueKey('tile-${tile.name}'),
                                    duration:
                                        _draggingTile == tile ||
                                            _resizingTile == tile
                                        ? Duration.zero
                                        : duration,
                                    curve: Curves.easeInOutCubicEmphasized,
                                    left: positions[tile]!.left,
                                    top: positions[tile]!.top,
                                    width: positions[tile]!.width,
                                    height: positions[tile]!.height,
                                    child: _reorderableTile(context, tile),
                                  ),
                              ],
                            ),
                          );
                        },
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    ),
  );

  Widget _buildTile(BuildContext context, DashboardTile tile) => switch (tile) {
    DashboardTile.analogTimer => _AnalogTimerTile(
      controller: widget.controller,
      showTicks: widget.general.timerDialTicks,
      general: widget.general,
    ),
    DashboardTile.digitalTimer => _DigitalTimerTile(
      controller: widget.controller,
      general: widget.general,
      expanded: _sizeFor(tile).rows == 2,
    ),
    DashboardTile.tasks => _TaskTile(
      controller: widget.controller,
      detailed: _sizeFor(tile).columns > 1 || _sizeFor(tile).rows > 1,
      onConfigure: widget.onConfigure,
    ),
    DashboardTile.mode => _ModeTile(controller: widget.controller),
    DashboardTile.countUp => _CountUpTile(controller: widget.controller),
    DashboardTile.countdown => _CountdownTile(controller: widget.controller),
    DashboardTile.quote => _QuoteTile(quotes: widget.quotes),
    DashboardTile.nextCourse => _NextCourseTile(
      general: widget.general,
      compact: _sizeFor(tile).columns == 1,
    ),
    DashboardTile.todayCourses => _TodayCoursesTile(
      general: widget.general,
      detailed: _sizeFor(tile).columns > 1 || _sizeFor(tile).rows > 1,
    ),
    DashboardTile.calendar => _CalendarTile(
      size: _sizeFor(DashboardTile.calendar),
    ),
  };

  Widget _reorderableTile(BuildContext context, DashboardTile tile) {
    final scheme = Theme.of(context).colorScheme;
    final tileKey = _tileKeys.putIfAbsent(tile, () => GlobalKey());
    final feedback = Material(
      elevation: 8,
      color: scheme.primaryContainer,
      borderRadius: BorderRadius.circular(28),
      child: SizedBox(
        width: tile.columns == 1 ? 160 : 250,
        height: 92,
        child: Center(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.drag_indicator),
              SizedBox(width: 8),
              Text(tile.label, style: Theme.of(context).textTheme.titleMedium),
            ],
          ),
        ),
      ),
    );
    final draggingPlaceholder = Card.outlined(
      margin: EdgeInsets.zero,
      child: Center(
        child: Icon(Icons.drag_indicator, color: scheme.onSurfaceVariant),
      ),
    );
    return DragTarget<DashboardTile>(
      onWillAcceptWithDetails: (details) => details.data != tile,
      onMove: (details) => _previewMove(details.data, tile, details.offset),
      onLeave: (_) {
        if (_hoverTile == tile) setState(() => _hoverTile = null);
      },
      onAcceptWithDetails: (details) {
        HapticFeedback.selectionClick();
        _previewTiles ??= reorderedDashboardTiles(
          widget.controller.visibleTiles,
          details.data,
          tile,
          after: _dropAfter(tile, details.offset),
        );
      },
      builder: (context, candidates, _) {
        final hovering = candidates.isNotEmpty;
        final shape =
            Theme.of(context).cardTheme.shape ??
            RoundedRectangleBorder(
              borderRadius: BorderRadius.all(Radius.circular(12)),
            );
        final card = Container(
          key: tileKey,
          foregroundDecoration: ShapeDecoration(
            shape: shape is OutlinedBorder
                ? shape.copyWith(
                    side: BorderSide(
                      color: hovering
                          ? scheme.primary
                          : (_editing
                                ? scheme.outlineVariant
                                : Colors.transparent),
                      width: hovering ? 2 : 1,
                    ),
                  )
                : shape,
          ),
          child: ClipPath(
            clipper: ShapeBorderClipper(shape: shape),
            child: TweenAnimationBuilder<double>(
              tween: Tween(end: hovering ? 2.5 : 0),
              duration: MediaQuery.disableAnimationsOf(context)
                  ? Duration.zero
                  : Duration(milliseconds: 160),
              builder: (context, blur, child) => ImageFiltered(
                imageFilter: ui.ImageFilter.blur(sigmaX: blur, sigmaY: blur),
                enabled: blur > 0,
                child: child,
              ),
              child: IgnorePointer(
                ignoring: _editing,
                child: ExcludeFocus(
                  excluding: _editing,
                  child: _buildTile(context, tile),
                ),
              ),
            ),
          ),
        );
        if (_editing) {
          final draggable = LongPressDraggable<DashboardTile>(
            key: ValueKey(tile),
            data: tile,
            maxSimultaneousDrags: _resizingTile == null ? 1 : 0,
            delay: Duration(milliseconds: 350),
            dragAnchorStrategy: pointerDragAnchorStrategy,
            onDragStarted: () => setState(() {
              _draggingTile = tile;
              _previewTiles = List.of(widget.controller.visibleTiles);
              _lastPreviewPosition = null;
            }),
            onDragUpdate: (details) {
              _updateEdgeScroll(details.globalPosition);
              if (_insideGrid(details.globalPosition)) {
                _previewNearest(tile, details.globalPosition);
              }
            },
            onDragEnd: _finishDrag,
            feedback: feedback,
            childWhenDragging: draggingPlaceholder,
            child: card,
          );
          if (_sizesFor(tile).length < 2) return draggable;
          final size = _sizeFor(tile);
          return Stack(
            fit: StackFit.expand,
            children: [
              draggable,
              if (_resizingTile == tile)
                Positioned.fill(
                  child: IgnorePointer(
                    child: DecoratedBox(
                      decoration: BoxDecoration(
                        border: Border.all(color: scheme.primary, width: 2),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Align(
                        alignment: Alignment.topCenter,
                        child: Padding(
                          padding: EdgeInsets.only(top: 4),
                          child: Chip(
                            label: Text('${size.columns} × ${size.rows}'),
                            backgroundColor: scheme.primaryContainer,
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              Positioned(
                key: ValueKey('resize-handle-${tile.name}'),
                right: 0,
                bottom: 0,
                child: Semantics(
                  button: true,
                  label: UiText.f("调整{0}尺寸", [tile.label]),
                  value: '${size.columns} × ${size.rows}',
                  child: Tooltip(
                    message: UiText.t("长按拖动调整大小 · 点按选择尺寸"),
                    child: GestureDetector(
                      key: ValueKey('resize-${tile.name}'),
                      behavior: HitTestBehavior.opaque,
                      onTap: () => _chooseSize(tile),
                      onLongPressStart: (_) {
                        HapticFeedback.mediumImpact();
                        setState(() {
                          _resizingTile = tile;
                          _resizeDelta = Offset.zero;
                          _resizeStart = size;
                          _resizePreview = size;
                          _resizeScrollStart = _pageScroll.offset;
                        });
                      },
                      onLongPressMoveUpdate: (details) {
                        _updateEdgeScroll(details.globalPosition);
                        _resizeBy(details.offsetFromOrigin);
                      },
                      onLongPressEnd: (_) => _finishResize(),
                      onLongPressCancel: () {
                        if (_resizingTile == tile) _finishResize(cancel: true);
                      },
                      child: SizedBox(
                        width: 48,
                        height: 48,
                        child: Align(
                          alignment: Alignment.bottomRight,
                          child: Container(
                            margin: EdgeInsets.all(6),
                            padding: EdgeInsets.all(4),
                            decoration: BoxDecoration(
                              color: scheme.primaryContainer,
                              borderRadius: BorderRadius.circular(8),
                            ),
                            child: Icon(
                              Icons.open_in_full_rounded,
                              size: 18,
                              color: scheme.onPrimaryContainer,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ],
          );
        }
        return GestureDetector(
          key: ValueKey(tile),
          behavior: HitTestBehavior.opaque,
          onLongPress: () {},
          child: card,
        );
      },
    );
  }

  void _showLibrary(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (sheetContext) => SafeArea(
        child: FractionallySizedBox(
          heightFactor: 0.75,
          child: AnimatedBuilder(
            animation: widget.controller,
            builder: (context, _) => Padding(
              padding: EdgeInsets.fromLTRB(20, 8, 20, 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    UiText.t("仪表盘模块"),
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  SizedBox(height: 8),
                  Text(UiText.t("选择显示在工作台上的模块")),
                  SizedBox(height: 12),
                  Expanded(
                    child: ListView(
                      children: [
                        for (final tile in DashboardTile.values)
                          SwitchListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(tile.label),
                            subtitle: Text(
                              '${_sizeFor(tile).columns}×${_sizeFor(tile).rows}',
                            ),
                            value: widget.controller.visibleTiles.contains(
                              tile,
                            ),
                            onChanged: (visible) =>
                                widget.controller.setTileVisible(tile, visible),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

String formatTimer(int seconds) =>
    '${(seconds ~/ 60).toString().padLeft(2, '0')}:${(seconds % 60).toString().padLeft(2, '0')}';

Widget tileCard(Widget child, {bool filled = false}) => filled
    ? Card.filled(
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        child: child,
      )
    : Card.outlined(
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        child: child,
      );

class _Heading extends StatelessWidget {
  const _Heading(this.icon, this.title, {this.action});
  final IconData icon;
  final String title;
  final Widget? action;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => Row(
      children: [
        Icon(icon, size: 18, color: Theme.of(context).colorScheme.primary),
        SizedBox(width: 8),
        Expanded(
          child: Tooltip(
            message: title,
            child: Text(
              title,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.titleSmall,
            ),
          ),
        ),
        ?action,
      ],
    ),
  );
}

class _AnalogTimerTile extends StatefulWidget {
  const _AnalogTimerTile({
    required this.controller,
    required this.showTicks,
    required this.general,
  });
  final DashboardController controller;
  final bool showTicks;
  final GeneralState general;

  @override
  State<_AnalogTimerTile> createState() => _AnalogTimerTileState();
}

class _AnalogTimerTileState extends State<_AnalogTimerTile>
    with SingleTickerProviderStateMixin {
  late final AnimationController _wave = AnimationController(
    vsync: this,
    duration: Duration(milliseconds: 1800),
  );

  @override
  void initState() {
    super.initState();
  }

  @override
  void didUpdateWidget(covariant _AnalogTimerTile oldWidget) {
    super.didUpdateWidget(oldWidget);
    _syncMotion();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _syncMotion();
  }

  void _syncMotion() {
    final reducedMotion =
        mounted && MediaQuery.maybeOf(context)?.disableAnimations == true;
    if (widget.controller.isRunning && !reducedMotion) {
      if (!_wave.isAnimating) _wave.repeat();
    } else {
      _wave.stop();
    }
  }

  @override
  void dispose() {
    _wave.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    final phase = widget.controller.countUp
        ? UiText.t("正向计时")
        : widget.controller.phase == TimerPhase.focus
        ? UiText.t("专注")
        : UiText.t("短休");
    final status = widget.controller.isRunning
        ? UiText.t("进行中")
        : widget.controller.elapsedSeconds > 0
        ? UiText.t("已暂停")
        : UiText.t("准备就绪");
    return tileCard(
      Padding(
        padding: EdgeInsets.all(14),
        child: Column(
          children: [
            Row(
              children: [
                Expanded(
                  child: _Heading(Icons.timer_outlined, UiText.t("大计时器")),
                ),
                SizedBox(width: 8),
                Flexible(
                  child: _TimerTonalPill(
                    label: phase,
                    icon: widget.controller.countUp
                        ? Icons.timer_outlined
                        : widget.controller.phase == TimerPhase.focus
                        ? Icons.center_focus_strong_outlined
                        : Icons.coffee_outlined,
                  ),
                ),
                SizedBox(width: 6),
                Flexible(
                  child: _TimerStatusPill(
                    label: status,
                    icon: widget.controller.isRunning
                        ? Icons.play_arrow_rounded
                        : widget.controller.elapsedSeconds > 0
                        ? Icons.pause_rounded
                        : Icons.check_rounded,
                  ),
                ),
              ],
            ),
            Expanded(
              child: LayoutBuilder(
                builder: (context, constraints) {
                  final size = math.min(
                    constraints.maxWidth,
                    constraints.maxHeight,
                  );
                  return Center(
                    child: SizedBox.square(
                      dimension: size,
                      child: Stack(
                        alignment: Alignment.center,
                        children: [
                          CustomPaint(
                            size: Size.square(size),
                            painter: _DialPainter(
                              progress: () => widget.controller.progressAt(
                                smooth: widget.general.progressSmooth,
                              ),
                              active: scheme.primary,
                              track: scheme.primary.withValues(alpha: 0.18),
                              ticks: scheme.onSurfaceVariant,
                              showTicks: widget.showTicks,
                              wavy: widget.general.progressWavy,
                              thickness: widget.general.progressThick ? 8 : 4,
                              wave: _wave,
                              reducedMotion: MediaQuery.of(context)
                                  .disableAnimations,
                            ),
                          ),
                          SizedBox(
                            width: math.max(0, size - 48),
                            child: FittedBox(
                              fit: BoxFit.scaleDown,
                              child: Text(
                                formatTimer(widget.controller.displaySeconds),
                                maxLines: 1,
                                style: Theme.of(context).textTheme.displaySmall
                                    ?.copyWith(
                                      color: scheme.primary,
                                      fontSize: math.min(84, size * .31),
                                      letterSpacing: -3,
                                      fontWeight: FontWeight.w700,
                                      fontFeatures: [
                                        FontFeature.tabularFigures(),
                                      ],
                                    ),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ),
            Align(
              alignment: AlignmentDirectional.centerStart,
              child: Text(
                _timerTaskLabel(widget.controller),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.titleMedium
                    ?.copyWith(color: scheme.onSurfaceVariant),
              ),
            ),
          ],
        ),
      ),
      filled: true,
    );
  }
}

String _timerTaskLabel(DashboardController controller) {
  final task = controller.activeTask;
  if (task == null) return UiText.t("未选择任务");
  return task.subtitle.trim().isEmpty
      ? task.title
      : '${task.title} · ${task.subtitle.trim()}';
}

class _TimerTonalPill extends StatelessWidget {
  const _TimerTonalPill({required this.label, required this.icon});

  final String label;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: EdgeInsets.symmetric(horizontal: 11, vertical: 7),
      decoration: ShapeDecoration(
        color: scheme.secondaryContainer,
        shape: StadiumBorder(),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 15, color: scheme.onSecondaryContainer),
          SizedBox(width: 5),
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelMedium
                  ?.copyWith(color: scheme.onSecondaryContainer),
            ),
          ),
        ],
      ),
    );
  }
}

class _TimerStatusPill extends StatefulWidget {
  const _TimerStatusPill({required this.label, required this.icon});
  final String label;
  final IconData icon;

  @override
  State<_TimerStatusPill> createState() => _TimerStatusPillState();
}

class _TimerStatusPillState extends State<_TimerStatusPill>
    with SingleTickerProviderStateMixin {
  bool _expanded = true;
  late final AnimationController _hold =
      AnimationController(vsync: this, duration: Duration(milliseconds: 1800))
        ..addStatusListener((status) {
          if (status == AnimationStatus.completed && mounted) {
            setState(() => _expanded = false);
          }
        });

  @override
  void initState() {
    super.initState();
    _hold.forward();
  }

  @override
  void didUpdateWidget(covariant _TimerStatusPill oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.label != widget.label) {
      _expanded = true;
      _hold.forward(from: 0);
    }
  }

  void _collapse() {
    _hold.stop();
    if (_expanded) setState(() => _expanded = false);
  }

  @override
  void dispose() {
    _hold.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    return TapRegion(
      onTapOutside: (_) => _collapse(),
      child: Tooltip(
        message: widget.label,
        child: Semantics(
          button: true,
          label: widget.label,
          expanded: _expanded,
          child: Material(
            color: scheme.secondaryContainer,
            shape: StadiumBorder(),
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: () {
                _hold.stop();
                setState(() => _expanded = !_expanded);
              },
              child: Padding(
                padding: EdgeInsets.symmetric(horizontal: 11, vertical: 7),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(
                      widget.icon,
                      size: 15,
                      color: scheme.onSecondaryContainer,
                    ),
                    Flexible(
                      child: TweenAnimationBuilder<double>(
                        tween: Tween(end: _expanded ? 1 : 0),
                        duration: MediaQuery.disableAnimationsOf(context)
                            ? Duration.zero
                            : Duration(milliseconds: 280),
                        curve: Curves.easeInOutCubic,
                        builder: (context, value, child) => ClipRect(
                          child: Align(
                            alignment: AlignmentDirectional.centerStart,
                            widthFactor: value,
                            child: Opacity(opacity: value, child: child),
                          ),
                        ),
                        child: ExcludeSemantics(
                          child: Padding(
                            padding: EdgeInsetsDirectional.only(start: 5),
                            child: Text(
                              widget.label,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.labelMedium
                                  ?.copyWith(
                                    color: scheme.onSecondaryContainer,
                                  ),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _DialPainter extends CustomPainter {
  _DialPainter({
    required this.progress,
    required this.active,
    required this.track,
    required this.ticks,
    required this.showTicks,
    required this.wavy,
    required this.thickness,
    required this.wave,
    required this.reducedMotion,
  }) : super(repaint: wave);
  final double Function() progress;
  final Color active;
  final Color track;
  final Color ticks;
  final bool wavy;
  final double thickness;
  final bool showTicks;
  final Animation<double> wave;
  final bool reducedMotion;
  @override
  void paint(Canvas canvas, Size size) {
    final progress = this.progress();
    final center = size.center(Offset.zero);
    final radius = math.min(size.width, size.height) / 2 - 20;
    final trackPaint = Paint()
      ..color = track
      ..style = PaintingStyle.stroke
      ..strokeWidth = thickness
      ..strokeCap = StrokeCap.round;
    final sweep = math.pi * 2 * progress.clamp(0.0, 1.0);
    final gap = (thickness + 2) / radius;
    if (progress <= 0) {
      canvas.drawCircle(center, radius, trackPaint);
    } else if (sweep + gap * 2 < math.pi * 2) {
      canvas.drawArc(
        Rect.fromCircle(center: center, radius: radius),
        -math.pi / 2 + sweep + gap,
        math.pi * 2 - sweep - gap * 2,
        false,
        trackPaint,
      );
    }
    if (showTicks) {
      final tickPaint = Paint()
        ..color = ticks.withValues(alpha: 0.45)
        ..strokeWidth = 1.3;
      for (var i = 0; i < 60; i++) {
        final angle = -math.pi / 2 + i * math.pi / 30;
        final outer = radius - 17;
        final inner = outer - (i % 5 == 0 ? 10 : 5);
        canvas.drawLine(
          center + Offset(math.cos(angle) * inner, math.sin(angle) * inner),
          center + Offset(math.cos(angle) * outer, math.sin(angle) * outer),
          tickPaint,
        );
      }
    }
    if (progress <= 0) return;
    final arcPaint = Paint()
      ..color = active
      ..style = PaintingStyle.stroke
      ..strokeWidth = thickness
      ..strokeCap = StrokeCap.round;
    if (!wavy) {
      canvas.drawArc(
        Rect.fromCircle(center: center, radius: radius),
        -math.pi / 2,
        sweep,
        false,
        arcPaint,
      );
      return;
    }
    // Material's alternating peak/trough cubic construction (0.48 handle
    // ratio), measured as a whole periodic path before revealing progress.
    // Large timer artwork uses shallow, closely spaced waves like the video.
    final scale = ((radius - 20) / 80).clamp(0.0, 1.0);
    final wavelength = 15 + 25 * scale;
    final amplitude = 1.6 + 1.4 * scale;
    final cycles = math.max(3, (2 * math.pi * radius / wavelength).floor());
    final halfAngle = math.pi / cycles;
    final handle = (2 * math.pi * radius / cycles) * .5 * .48;
    final fullPath = Path()..moveTo(radius, 0);
    for (var i = 1; i <= cycles * 4; i++) {
      final a0 = (i - 1) * halfAngle;
      final a1 = i * halfAngle;
      final r0 = (i - 1).isEven ? radius : radius - 2 * amplitude;
      final r1 = i.isEven ? radius : radius - 2 * amplitude;
      final p0 = Offset(math.cos(a0) * r0, math.sin(a0) * r0);
      final p1 = Offset(math.cos(a1) * r1, math.sin(a1) * r1);
      final c0 = p0 + Offset(-math.sin(a0), math.cos(a0)) * handle;
      final c1 = p1 - Offset(-math.sin(a1), math.cos(a1)) * handle;
      fullPath.cubicTo(c0.dx, c0.dy, c1.dx, c1.dy, p1.dx, p1.dy);
    }
    final metric = fullPath.computeMetrics().first;
    final phaseFraction = reducedMotion ? 0.0 : wave.value / cycles;
    final circumference = metric.length / 2;
    final path = metric.extractPath(
      phaseFraction * circumference,
      (phaseFraction + progress.clamp(0.0, 1.0)) * circumference,
    );
    canvas.save();
    canvas.translate(center.dx, center.dy);
    canvas.rotate(-math.pi / 2 - phaseFraction * math.pi * 2);
    canvas.drawPath(path, arcPaint);
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _DialPainter old) =>
      old.progress != progress ||
      old.active != active ||
      old.track != track ||
      old.ticks != ticks ||
      old.showTicks != showTicks ||
      old.wavy != wavy ||
      old.thickness != thickness ||
      old.reducedMotion != reducedMotion;
}

class _DigitalTimerTile extends StatefulWidget {
  const _DigitalTimerTile({
    required this.controller,
    required this.general,
    this.expanded = false,
  });
  final bool expanded;
  final DashboardController controller;
  final GeneralState general;

  @override
  State<_DigitalTimerTile> createState() => _DigitalTimerTileState();
}

class _DigitalTimerTileState extends State<_DigitalTimerTile>
    with SingleTickerProviderStateMixin {
  late final AnimationController _wave = AnimationController(
    vsync: this,
    duration: Duration(milliseconds: 1800),
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _syncMotion();
  }

  @override
  void didUpdateWidget(covariant _DigitalTimerTile oldWidget) {
    super.didUpdateWidget(oldWidget);
    _syncMotion();
  }

  void _syncMotion() {
    if (widget.controller.isRunning &&
        MediaQuery.maybeOf(context)?.disableAnimations != true) {
      if (!_wave.isAnimating) _wave.repeat();
    } else {
      _wave.stop();
    }
  }

  @override
  void dispose() {
    _wave.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    final controller = widget.controller;
    final phase = controller.countUp
        ? UiText.t("正向计时")
        : controller.phase == TimerPhase.focus
        ? UiText.t("专注")
        : UiText.t("短休");
    final status = controller.isRunning
        ? UiText.t("进行中")
        : controller.elapsedSeconds > 0
        ? UiText.t("已暂停")
        : UiText.t("准备就绪");
    return tileCard(
      Padding(
        padding: EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: _Heading(Icons.schedule_outlined, UiText.t("数字计时器")),
                ),
                SizedBox(width: 6),
                Flexible(
                  child: _TimerTonalPill(
                    label: phase,
                    icon: Icons.timelapse_rounded,
                  ),
                ),
                SizedBox(width: 6),
                Flexible(
                  child: _TimerStatusPill(
                    label: status,
                    icon: controller.isRunning
                        ? Icons.play_arrow_rounded
                        : controller.elapsedSeconds > 0
                        ? Icons.pause_rounded
                        : Icons.check_rounded,
                  ),
                ),
              ],
            ),
            Expanded(
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Text(
                    formatTimer(controller.displaySeconds),
                    style: Theme.of(context).textTheme.displayMedium?.copyWith(
                      color: scheme.primary,
                      fontSize: widget.expanded ? 112 : 48,
                      height: 1.05,
                      fontWeight: FontWeight.w700,
                      fontFeatures: [FontFeature.tabularFigures()],
                    ),
                  ),
                ),
              ),
            ),
            Text(
              UiText.f("当前任务：{0}", [_timerTaskLabel(controller)]),
              maxLines: widget.expanded ? 3 : 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.bodyLarge
                  ?.copyWith(color: scheme.onSurfaceVariant),
            ),
            SizedBox(height: 9),
            SizedBox(
              height: 16,
              child: CustomPaint(
                painter: _WaveBarPainter(
                  wavy: widget.general.progressWavy,
                  thickness: widget.general.progressThick ? 8 : 4,
                  progress: () => controller.progressAt(
                    smooth: widget.general.progressSmooth,
                  ),
                  active: scheme.primary,
                  track: scheme.primary.withValues(alpha: .16),
                  wave: _wave,
                  reducedMotion: MediaQuery.disableAnimationsOf(context),
                ),
              ),
            ),
          ],
        ),
      ),
      filled: true,
    );
  }
}

class _WaveBarPainter extends CustomPainter {
  _WaveBarPainter({
    required this.wavy,
    required this.thickness,
    required this.progress,
    required this.active,
    required this.track,
    required this.wave,
    required this.reducedMotion,
  }) : super(repaint: wave);

  final double Function() progress;
  final Color active, track;
  final bool wavy;
  final double thickness;
  final Animation<double> wave;
  final bool reducedMotion;

  @override
  void paint(Canvas canvas, Size size) {
    final progress = this.progress();
    final y = size.height / 2;
    final inset = thickness / 2;
    final available = math.max(0.0, size.width - thickness);
    final width = available * progress.clamp(0.0, 1.0);
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = thickness
      ..strokeCap = StrokeCap.round;
    final trackStart = width == 0 ? inset : inset + width + thickness + 4;
    if (trackStart < size.width - inset) {
      canvas.drawLine(
        Offset(trackStart, y),
        Offset(size.width - inset, y),
        paint..color = track,
      );
    }
    if (progress < 1) {
      canvas.drawCircle(
        Offset(size.width - inset, y),
        2,
        Paint()..color = active,
      );
    }
    if (width <= 0) return;
    final phase = reducedMotion ? 0.0 : wave.value * math.pi * 2;
    final path = Path();
    final steps = math.max(2, (width / 2).ceil());
    for (var i = 0; i <= steps; i++) {
      final distance = width * i / steps;
      final waveY =
          y + math.sin(distance / 40 * math.pi * 2 - phase) * (wavy ? 3 : 0);
      if (i == 0) {
        path.moveTo(inset, waveY);
      } else {
        path.lineTo(inset + distance, waveY);
      }
    }
    canvas.drawPath(path, paint..color = active);
  }

  @override
  bool shouldRepaint(covariant _WaveBarPainter old) =>
      old.progress != progress ||
      old.active != active ||
      old.track != track ||
      old.reducedMotion != reducedMotion ||
      old.wavy != wavy ||
      old.thickness != thickness;
}

class _TaskTile extends StatelessWidget {
  const _TaskTile({
    required this.controller,
    required this.onConfigure,
    this.detailed = false,
  });
  final bool detailed;
  final DashboardController controller;
  final VoidCallback onConfigure;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => tileCard(
      Padding(
        padding: EdgeInsets.fromLTRB(10, 6, 10, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _Heading(
              Icons.checklist_outlined,
              controller.presets
                  .firstWhere(
                    (preset) => preset.id == controller.activePresetId,
                  )
                  .name,
              action: IconButton(
                tooltip: UiText.t("添加任务"),
                icon: Icon(Icons.add, size: 20),
                padding: EdgeInsets.zero,
                constraints: BoxConstraints(minWidth: 32, minHeight: 32),
                visualDensity: VisualDensity.compact,
                style: IconButton.styleFrom(
                  tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
                onPressed: () => showTaskDialog(context, controller),
              ),
            ),
            Expanded(
              child: controller.activeTasks.isEmpty
                  ? Align(
                      alignment: Alignment.topLeft,
                      child: Padding(
                        padding: EdgeInsets.only(top: 8),
                        child: Text(
                          UiText.t("暂无任务"),
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                      ),
                    )
                  : ListView.builder(
                      primary: false,
                      padding: EdgeInsets.only(top: 6),
                      physics: BouncingScrollPhysics(
                        parent: AlwaysScrollableScrollPhysics(),
                      ),
                      itemCount: controller.activeTasks.length,
                      itemBuilder: (context, index) {
                        final task = controller.activeTasks[index];
                        final selected = task.id == controller.activeTaskId;
                        return Padding(
                          padding: EdgeInsets.only(bottom: 4),
                          child: Material(
                            color: selected
                                ? Theme.of(context)
                                      .colorScheme
                                      .secondaryContainer
                                : Colors.transparent,
                            borderRadius: BorderRadius.circular(12),
                            child: InkWell(
                              borderRadius: BorderRadius.circular(12),
                              onTap: () => controller.selectTask(task.id),
                              child: Row(
                                children: [
                                  Checkbox(
                                    value: task.done,
                                    onChanged: (_) =>
                                        controller.toggleTaskDone(task.id),
                                    visualDensity: VisualDensity.compact,
                                  ),
                                  Expanded(
                                    child: Padding(
                                      padding: EdgeInsets.fromLTRB(0, 5, 5, 5),
                                      child: Row(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.center,
                                        children: [
                                          Expanded(
                                            child: Text(
                                              detailed &&
                                                      task.subtitle.isNotEmpty
                                                  ? '${task.title}\n${task.subtitle}'
                                                  : task.title,
                                              softWrap: true,
                                              style: Theme.of(context)
                                                  .textTheme
                                                  .bodyMedium
                                                  ?.copyWith(
                                                    decoration: task.done
                                                        ? TextDecoration
                                                              .lineThrough
                                                        : null,
                                                  ),
                                            ),
                                          ),
                                          SizedBox(width: 4),
                                          Text(
                                            task.estimate > 0
                                                ? '🍅 ${controller.tomatoesForTask(task)}/${task.estimate}'
                                                : '🍅 ${controller.tomatoesForTask(task)}',
                                            style: Theme.of(context)
                                                .textTheme
                                                .labelSmall
                                                ?.copyWith(
                                                  color: Theme.of(context)
                                                      .colorScheme
                                                      .onSurfaceVariant,
                                                ),
                                          ),
                                        ],
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        );
                      },
                    ),
            ),
            Row(
              children: [
                Text(
                  '${controller.completedTasks}/${controller.activeTasks.length}',
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: TextButton(
                    onPressed: onConfigure,
                    style: TextButton.styleFrom(
                      padding: EdgeInsets.symmetric(horizontal: 4),
                      minimumSize: Size(64, 32),
                      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      textStyle: Theme.of(context).textTheme.labelSmall,
                    ),
                    child: Text(UiText.t("管理清单"), textAlign: TextAlign.center),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    ),
  );
}

class _ModeTile extends StatelessWidget {
  const _ModeTile({required this.controller});
  final DashboardController controller;
  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    final minutes = controller.phase == TimerPhase.focus
        ? controller.focusMinutes
        : controller.shortMinutes;
    return tileCard(
      _PatternSurface(
        countdown: false,
        resting: controller.phase == TimerPhase.shortBreak,
        child: Padding(
          padding: EdgeInsets.all(10),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _Heading(Icons.timelapse_outlined, UiText.t("计划时段")),
              Expanded(
                child: Center(
                  child: Text.rich(
                    TextSpan(
                      children: [
                        TextSpan(
                          text: '$minutes',
                          style: Theme.of(context).textTheme.displaySmall
                              ?.copyWith(
                                color: scheme.primary,
                                fontWeight: FontWeight.w600,
                                fontSize: 44,
                              ),
                        ),
                        TextSpan(
                          text: UiText.t(" 分钟"),
                          style: Theme.of(context).textTheme.labelMedium
                              ?.copyWith(color: scheme.onSurfaceVariant),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              SegmentedButton<TimerPhase>(
                showSelectedIcon: false,
                style: ButtonStyle(
                  visualDensity: VisualDensity.compact,
                  minimumSize: WidgetStatePropertyAll(Size(0, 40)),
                  padding: WidgetStatePropertyAll(
                    EdgeInsets.symmetric(horizontal: 4),
                  ),
                  textStyle: WidgetStatePropertyAll(
                    Theme.of(context).textTheme.labelMedium,
                  ),
                ),
                segments: [
                  ButtonSegment(
                    value: TimerPhase.focus,
                    label: Tooltip(
                      message: UiText.t("专注"),
                      child: Text(
                        UiText.t("专注"),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ),
                  ButtonSegment(
                    value: TimerPhase.shortBreak,
                    label: Tooltip(
                      message: UiText.t("短休"),
                      child: Text(
                        UiText.t("短休"),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ),
                ],
                selected: {controller.phase},
                onSelectionChanged:
                    controller.countUp || !controller.shortBreakEnabled
                    ? null
                    : (selection) => controller.choosePhase(selection.first),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CountUpTile extends StatelessWidget {
  const _CountUpTile({required this.controller});
  final DashboardController controller;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => tileCard(
      Padding(
        padding: EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _Heading(Icons.trending_up, UiText.t("正向计时")),
            Spacer(),
            Align(
              alignment: Alignment.bottomRight,
              child: Switch(
                value: controller.countUp,
                onChanged: controller.setCountUp,
              ),
            ),
          ],
        ),
      ),
    ),
  );
}

class _CalendarTile extends StatefulWidget {
  const _CalendarTile({required this.size});
  final TileSize size;
  @override
  State<_CalendarTile> createState() => _CalendarTileState();
}

class _CalendarTileState extends State<_CalendarTile> {
  late final Timer _clock = Timer.periodic(Duration(minutes: 1), (_) {
    if (mounted) setState(() {});
  });

  @override
  void initState() {
    super.initState();
    _clock;
  }

  @override
  void dispose() {
    _clock.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final now = DateTime.now();
    final scheme = Theme.of(context).colorScheme;
    if (widget.size.columns > 1) {
      return tileCard(
        Padding(
          padding: EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _Heading(
                Icons.calendar_month_outlined,
                UiText.f("{0} 年 {1} 月", [now.year, now.month]),
              ),
              SizedBox(height: 8),
              Expanded(child: _MonthGrid(now: now)),
            ],
          ),
        ),
        filled: true,
      );
    }
    return tileCard(
      InkWell(
        onTap: () => showDatePicker(
          context: context,
          initialDate: now,
          firstDate: DateTime(2000),
          lastDate: DateTime(2100, 12, 31),
          helpText: UiText.t("查看日历"),
          confirmText: UiText.t("完成"),
        ),
        child: Padding(
          padding: EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _Heading(Icons.calendar_month_outlined, UiText.t("日历")),
              SizedBox(height: 6),
              Text(
                UiText.f("{0} 年 {1} 月", [now.year, now.month]),
                style: Theme.of(context).textTheme.labelMedium
                    ?.copyWith(color: scheme.onSurfaceVariant),
              ),
              Expanded(
                child: Center(
                  child: FittedBox(
                    child: Text(
                      '${now.day}',
                      style: Theme.of(context).textTheme.displayLarge?.copyWith(
                        fontSize: 64,
                        fontWeight: FontWeight.w600,
                        color: scheme.primary,
                      ),
                    ),
                  ),
                ),
              ),
              Text(
                UiText.f("今天 · 星期{0}", [
                  [
                    UiText.t("一"),
                    UiText.t("二"),
                    UiText.t("三"),
                    UiText.t("四"),
                    UiText.t("五"),
                    UiText.t("六"),
                    UiText.t("日"),
                  ][now.weekday - 1],
                ]),
                style: Theme.of(context).textTheme.labelLarge
                    ?.copyWith(color: scheme.primary),
              ),
            ],
          ),
        ),
      ),
      filled: true,
    );
  }
}

class _MonthGrid extends StatelessWidget {
  const _MonthGrid({required this.now});
  final DateTime now;
  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    final first = DateTime(now.year, now.month, 1);
    final start = first.subtract(Duration(days: first.weekday - 1));
    return LayoutBuilder(
      builder: (context, box) {
        final compact = box.maxHeight < 210;
        return Column(
          children: [
            Row(
              children: [
                for (final day in [
                  UiText.t("一"),
                  UiText.t("二"),
                  UiText.t("三"),
                  UiText.t("四"),
                  UiText.t("五"),
                  UiText.t("六"),
                  UiText.t("日"),
                ])
                  Expanded(
                    child: Text(
                      day,
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.labelSmall,
                    ),
                  ),
              ],
            ),
            SizedBox(height: 4),
            Expanded(
              child: Column(
                children: [
                  for (var week = 0; week < 6; week++)
                    Expanded(
                      child: Row(
                        children: [
                          for (var day = 0; day < 7; day++)
                            Expanded(
                              child: Builder(
                                builder: (context) {
                                  final date = start.add(
                                    Duration(days: week * 7 + day),
                                  );
                                  final today = DateUtils.isSameDay(date, now);
                                  return Center(
                                    child: Container(
                                      constraints: BoxConstraints(
                                        maxWidth: 48,
                                        maxHeight: 48,
                                      ),
                                      alignment: Alignment.center,
                                      decoration: today
                                          ? BoxDecoration(
                                              color: scheme.primary,
                                              shape: BoxShape.circle,
                                            )
                                          : null,
                                      child: Text(
                                        '${date.day}',
                                        style:
                                            (compact
                                                    ? Theme.of(context)
                                                          .textTheme
                                                          .labelSmall
                                                    : Theme.of(context)
                                                          .textTheme
                                                          .bodyLarge)
                                                ?.copyWith(
                                                  color: today
                                                      ? scheme.onPrimary
                                                      : date.month == now.month
                                                      ? scheme.onSurface
                                                      : scheme.outline,
                                                  fontWeight: today
                                                      ? FontWeight.w700
                                                      : null,
                                                ),
                                      ),
                                    ),
                                  );
                                },
                              ),
                            ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}

class _CountdownTile extends StatelessWidget {
  const _CountdownTile({required this.controller});
  final DashboardController controller;
  Future<void> _edit(BuildContext context) async {
    final date = await showDatePicker(
      context: context,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
      initialDate:
          controller.countdownDate ?? DateTime.now().add(Duration(days: 30)),
    );
    if (date == null || !context.mounted) return;
    final textController = TextEditingController(
      text: controller.countdownName,
    );
    final name = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(UiText.t("倒数日名称")),
        content: TextField(
          controller: textController,
          autofocus: true,
          maxLength: 32,
          decoration: InputDecoration(
            border: OutlineInputBorder(),
            labelText: UiText.t("目标名称"),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: Text(UiText.t("取消")),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, textController.text.trim()),
            child: Text(UiText.t("保存")),
          ),
        ],
      ),
    );
    textController.dispose();
    if (name == null || name.isEmpty) return;
    controller.setCountdown(name, date);
  }

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final days = controller.countdownDays;
    final scheme = Theme.of(context).colorScheme;
    return tileCard(
      _PatternSurface(
        countdown: true,
        child: Padding(
          padding: EdgeInsets.fromLTRB(12, 6, 12, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _Heading(
                Icons.event_outlined,
                UiText.t("倒数日"),
                action: IconButton(
                  tooltip: UiText.t("设置倒数日"),
                  onPressed: () => _edit(context),
                  icon: Icon(Icons.edit_outlined, size: 18),
                  style: IconButton.styleFrom(
                    minimumSize: Size(32, 32),
                    padding: EdgeInsets.zero,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                ),
              ),
              Expanded(
                child: SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SizedBox(height: 6),
                      Text(
                        days == null
                            ? UiText.t("期待下一站")
                            : controller.countdownName,
                        style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          fontSize: 20,
                          fontWeight: FontWeight.w600,
                          color: scheme.onSurface,
                        ),
                      ),
                      SizedBox(height: 4),
                      if (days == null)
                        TextButton(
                          onPressed: () => _edit(context),
                          child: Text(UiText.t("设置日期")),
                        )
                      else
                        Text.rich(
                          TextSpan(
                            children: [
                              TextSpan(
                                text: days >= 0
                                    ? UiText.t("还有 ")
                                    : UiText.t("已过 "),
                                style: Theme.of(context).textTheme.labelMedium
                                    ?.copyWith(color: scheme.onSurfaceVariant),
                              ),
                              TextSpan(
                                text: '${days.abs()}',
                                style: Theme.of(context).textTheme.headlineLarge
                                    ?.copyWith(
                                      color: scheme.primary,
                                      fontWeight: FontWeight.w600,
                                    ),
                              ),
                              TextSpan(
                                text: UiText.t(" 天"),
                                style: Theme.of(context).textTheme.labelMedium
                                    ?.copyWith(color: scheme.onSurfaceVariant),
                              ),
                            ],
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _PatternSurface extends StatelessWidget {
  const _PatternSurface({
    required this.countdown,
    required this.child,
    this.resting = false,
  });
  final bool countdown;
  final bool resting;
  final Widget child;
  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => CustomPaint(
      painter: _WidgetPatternPainter(
        Theme.of(context).colorScheme,
        countdown,
        resting,
      ),
      child: child,
    ),
  );
}

class _WidgetPatternPainter extends CustomPainter {
  _WidgetPatternPainter(this.scheme, this.countdown, this.resting);
  final ColorScheme scheme;
  final bool countdown;
  final bool resting;
  @override
  void paint(Canvas canvas, Size size) {
    canvas.save();
    canvas.clipRect(Offset.zero & size);
    final paint = Paint()
      ..color = scheme.primary.withValues(
        alpha: scheme.brightness == Brightness.dark ? .12 : .07,
      );
    final center = Offset(size.width * .95, size.height * .42);
    if (countdown) {
      for (var i = 0; i < 8; i++) {
        canvas.save();
        canvas.translate(center.dx, center.dy);
        canvas.rotate(i * math.pi / 4);
        canvas.drawOval(
          Rect.fromCenter(
            center: Offset(size.width * .2, 0),
            width: size.width * .55,
            height: size.width * .22,
          ),
          paint,
        );
        canvas.restore();
      }
      canvas.drawCircle(
        center,
        size.width * .12,
        Paint()..color = scheme.tertiary.withValues(alpha: .1),
      );
    } else {
      // Quiet tonal illustrations leave the heading and controls unobstructed.
      canvas.translate(size.width * .48, size.height * .23);
      final scale = math.min(size.width / 170, size.height / 170);
      canvas.scale(scale);
      final ink = Paint()..color = scheme.primary.withValues(alpha: .16);
      final tone = Paint()..color = scheme.tertiary.withValues(alpha: .12);
      final line = Paint()
        ..color = scheme.primary.withValues(alpha: .21)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 4
        ..strokeCap = StrokeCap.round
        ..strokeJoin = StrokeJoin.round;
      if (resting) {
        // A branching plant with broad, gently curved leaves and a rounded pot.
        canvas.drawPath(
          Path()
            ..moveTo(38, 75)
            ..quadraticBezierTo(34, 42, 42, 10),
          line,
        );
        canvas.drawPath(
          Path()
            ..moveTo(38, 52)
            ..quadraticBezierTo(16, 54, 10, 29)
            ..quadraticBezierTo(36, 27, 38, 52)
            ..close(),
          ink,
        );
        canvas.drawPath(
          Path()
            ..moveTo(39, 35)
            ..quadraticBezierTo(65, 36, 72, 12)
            ..quadraticBezierTo(47, 8, 39, 35)
            ..close(),
          tone,
        );
        canvas.drawPath(
          Path()
            ..moveTo(41, 18)
            ..quadraticBezierTo(22, 17, 23, -4)
            ..quadraticBezierTo(44, -4, 41, 18)
            ..close(),
          ink,
        );
        canvas.drawPath(
          Path()
            ..moveTo(17, 65)
            ..lineTo(62, 65)
            ..lineTo(57, 91)
            ..quadraticBezierTo(55, 97, 49, 97)
            ..lineTo(29, 97)
            ..quadraticBezierTo(23, 97, 22, 91)
            ..close(),
          ink,
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromLTWH(14, 62, 51, 8),
            Radius.circular(4),
          ),
          tone,
        );
      } else {
        // Articulated reading lamp, soft pool of light and a small closed book.
        canvas.drawPath(
          Path()
            ..moveTo(30, 37)
            ..lineTo(4, 87)
            ..lineTo(69, 87)
            ..close(),
          tone,
        );
        canvas.drawPath(
          Path()
            ..moveTo(61, 88)
            ..lineTo(75, 46)
            ..lineTo(48, 20),
          line,
        );
        canvas.drawCircle(Offset(75, 46), 5, ink);
        canvas.drawPath(
          Path()
            ..moveTo(36, 9)
            ..quadraticBezierTo(46, 7, 51, 16)
            ..lineTo(61, 35)
            ..quadraticBezierTo(40, 48, 19, 36)
            ..lineTo(29, 17)
            ..quadraticBezierTo(31, 11, 36, 9)
            ..close(),
          ink,
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromLTWH(44, 86, 39, 7),
            Radius.circular(4),
          ),
          ink,
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromLTWH(3, 90, 32, 8),
            Radius.circular(3),
          ),
          tone,
        );
      }
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _WidgetPatternPainter oldDelegate) =>
      oldDelegate.scheme != scheme ||
      oldDelegate.countdown != countdown ||
      oldDelegate.resting != resting;
}

class _QuoteTile extends StatelessWidget {
  const _QuoteTile({required this.quotes});
  final DashboardQuotes quotes;

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final scheme = Theme.of(context).colorScheme;
    return tileCard(
      Padding(
        padding: EdgeInsets.fromLTRB(12, 4, 12, 10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _Heading(
              Icons.format_quote_outlined,
              UiText.t("名言警句"),
              action: IconButton(
                tooltip: UiText.t("换一句"),
                icon: Icon(Icons.refresh, size: 20),
                onPressed: () => quotes.refresh(force: true),
              ),
            ),
            SizedBox(height: 2),
            Expanded(
              child: LayoutBuilder(
                builder: (context, constraints) => SingleChildScrollView(
                  primary: false,
                  child: ConstrainedBox(
                    constraints: BoxConstraints(
                      minHeight: constraints.maxHeight,
                    ),
                    child: Center(
                      child: Text(
                        '“${quotes.current.text}”',
                        textAlign: TextAlign.center,
                        softWrap: true,
                        style: Theme.of(context).textTheme.titleMedium
                            ?.copyWith(height: 1.3),
                      ),
                    ),
                  ),
                ),
              ),
            ),
            SizedBox(height: 4),
            Text(
              quotes.current.source,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelSmall
                  ?.copyWith(color: scheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }
}

class _CourseNow extends StatefulWidget {
  const _CourseNow({required this.builder});
  final Widget Function(BuildContext context, DateTime now) builder;

  @override
  State<_CourseNow> createState() => _CourseNowState();
}

class _CourseNowState extends State<_CourseNow> {
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    _ticker = Timer.periodic(Duration(minutes: 1), (_) => setState(() {}));
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) =>
      UiText.watch(context, () => widget.builder(context, DateTime.now()));
}

class _NextCourseTile extends StatelessWidget {
  const _NextCourseTile({required this.general, this.compact = false});
  final bool compact;
  final GeneralState general;

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => _CourseNow(
      builder: (context, now) {
        final next = CourseDashboardData.next(general.schedule, now);
        return tileCard(
          Padding(
            padding: EdgeInsets.all(compact ? 10 : 14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _Heading(Icons.school_outlined, UiText.t("接下来课程")),
                Expanded(
                  child: Center(
                    child: SingleChildScrollView(
                      child: next == null
                          ? Text(
                              UiText.t("暂时没有接下来的课程"),
                              style: Theme.of(context).textTheme.bodyMedium,
                            )
                          : Container(
                              width: double.infinity,
                              padding: EdgeInsets.all(compact ? 8 : 12),
                              decoration: BoxDecoration(
                                color: courseColors(
                                  context,
                                  next.event.name,
                                ).primaryContainer,
                                borderRadius: BorderRadius.circular(16),
                              ),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Text(
                                    next.event.name,
                                    style: Theme.of(context)
                                        .textTheme
                                        .titleMedium
                                        ?.copyWith(
                                          color: courseColors(
                                            context,
                                            next.event.name,
                                          ).onPrimaryContainer,
                                          fontWeight: FontWeight.w600,
                                        ),
                                  ),
                                  SizedBox(height: 4),
                                  Text(
                                    UiText.f("{0}{1} {2}–{3}", [
                                      next.isOngoing(now)
                                          ? UiText.t("正在上课 · ")
                                          : '',
                                      _courseDay(next.start, now),
                                      next.event.startTime,
                                      next.event.endTime,
                                    ]),
                                    style: Theme.of(context).textTheme.bodySmall
                                        ?.copyWith(
                                          color: courseColors(
                                            context,
                                            next.event.name,
                                          ).onPrimaryContainer,
                                        ),
                                  ),
                                  if (next.event.room.isNotEmpty)
                                    Text(
                                      next.event.room,
                                      style: Theme.of(context)
                                          .textTheme
                                          .labelSmall
                                          ?.copyWith(
                                            color: courseColors(
                                              context,
                                              next.event.name,
                                            ).onPrimaryContainer,
                                          ),
                                    ),
                                ],
                              ),
                            ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    ),
  );
}

String _courseDay(DateTime date, DateTime now) {
  final day = DateTime(date.year, date.month, date.day);
  final today = DateTime(now.year, now.month, now.day);
  final difference = day.difference(today).inDays;
  if (difference == 0) return UiText.t("今天");
  if (difference == 1) return UiText.t("明天");
  return '${date.month}/${date.day}';
}

class _TodayCoursesTile extends StatelessWidget {
  const _TodayCoursesTile({required this.general, this.detailed = false});
  final bool detailed;
  final GeneralState general;

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => _CourseNow(
      builder: (context, now) {
        final courses = CourseDashboardData.today(general.schedule, now);
        final scheme = Theme.of(context).colorScheme;
        return tileCard(
          Padding(
            padding: EdgeInsets.all(14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _Heading(Icons.today_outlined, UiText.t("今日课程")),
                SizedBox(height: 4),
                Text(
                  UiText.f("{0} 月 {1} 日 · {2} 门", [
                    now.month,
                    now.day,
                    courses.length,
                  ]),
                  style: Theme.of(context).textTheme.labelSmall
                      ?.copyWith(color: scheme.onSurfaceVariant),
                ),
                SizedBox(height: 8),
                Expanded(
                  child: courses.isEmpty
                      ? Center(
                          child: Text(
                            UiText.t("今天没有课程"),
                            textAlign: TextAlign.center,
                            style: Theme.of(context).textTheme.bodySmall,
                          ),
                        )
                      : ListView.separated(
                          padding: EdgeInsets.zero,
                          physics: BouncingScrollPhysics(),
                          itemCount: courses.length,
                          separatorBuilder: (_, _) => SizedBox(height: 6),
                          itemBuilder: (context, index) {
                            final course = courses[index];
                            final active = course.isOngoing(now);
                            final colors = courseColors(
                              context,
                              course.event.name,
                            );
                            return Container(
                              padding: EdgeInsets.all(8),
                              decoration: BoxDecoration(
                                color: colors.primaryContainer,
                                borderRadius: BorderRadius.circular(12),
                                border: active
                                    ? Border.all(color: colors.primary)
                                    : null,
                              ),
                              child: DefaultTextStyle.merge(
                                style: TextStyle(
                                  color: colors.onPrimaryContainer,
                                ),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      course.event.name,
                                      style: Theme.of(context)
                                          .textTheme
                                          .labelLarge
                                          ?.copyWith(
                                            color: colors.onPrimaryContainer,
                                          ),
                                    ),
                                    SizedBox(height: 2),
                                    Text(
                                      UiText.f("{0}–{1}{2}", [
                                        course.event.startTime,
                                        course.event.endTime,
                                        active ? UiText.t(" · 上课中") : '',
                                      ]),
                                      style: Theme.of(context)
                                          .textTheme
                                          .labelSmall
                                          ?.copyWith(
                                            color: colors.onPrimaryContainer,
                                          ),
                                    ),
                                    if (course.event.room.isNotEmpty ||
                                        (detailed &&
                                            course.event.teacher.isNotEmpty))
                                      Text(
                                        [
                                              course.event.room,
                                              if (detailed)
                                                course.event.teacher,
                                            ]
                                            .where((text) => text.isNotEmpty)
                                            .join(' · '),
                                        style: Theme.of(context)
                                            .textTheme
                                            .labelSmall
                                            ?.copyWith(
                                              color: colors.onPrimaryContainer,
                                            ),
                                      ),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
                ),
              ],
            ),
          ),
        );
      },
    ),
  );
}
