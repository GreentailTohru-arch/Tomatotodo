using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Automation;
using Tomatotodo_Windows.Data;
using Windows.Foundation;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private readonly Dictionary<Border, Storyboard> _tileMotion = [];
    private DispatcherTimer? _dragTick;
    private Border? _dropSlot;
    private Point _dragPointer;
    private Point _dragGrab;
    private Point _lastPreviewPoint;
    private string? _hoverSlot;
    private DateTime _hoverSince;
    private List<string>? _dragOriginalOrder;
    private uint? _dragPointerId;

    private void DashboardEditButton_Click(object sender, RoutedEventArgs e)
    {
        _draftLayout = _state.DashboardLayout.Clone();
        _dashboardEditing = true;
        Render();
    }

    private void DashboardSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_draftLayout is null) return;
        StopLongPress();
        _state.DashboardLayout = DashboardWidgets.Normalize(_draftLayout);
        Save();
        _draftLayout = null;
        _dashboardEditing = false;
        DashboardDrawerLayer.Visibility = Visibility.Collapsed;
        Render();
    }

    private void DashboardAddButton_Click(object sender, RoutedEventArgs e)
    {
        StopLongPress();
        RenderDrawerItems();
        DashboardDrawerPanel.Width = Math.Min(420, Math.Max(1, PresetDrawerWorkspace.ActualWidth));
        DashboardDrawerLayer.Visibility = Visibility.Visible;
        if (_systemUiSettings.AnimationsEnabled)
        {
            var slide = new DoubleAnimation { From = DashboardDrawerPanel.Width, To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(slide, DashboardDrawerTranslate);
            Storyboard.SetTargetProperty(slide, "X");
            var animation = new Storyboard(); animation.Children.Add(slide); animation.Begin();
        }
        else DashboardDrawerTranslate.X = 0;
    }

    private void DashboardDrawerCloseButton_Click(object sender, RoutedEventArgs e) =>
        DashboardDrawerLayer.Visibility = Visibility.Collapsed;

    private void DashboardStartButton_Click(object sender, RoutedEventArgs e) => StartPause();
    private void DashboardResetButton_Click(object sender, RoutedEventArgs e) => ResetTimer();

    private void RenderDrawerItems()
    {
        DashboardDrawerItems.Children.Clear();
        if (_draftLayout is null) return;
        var hidden = _draftLayout.Order.Where(id => !_draftLayout.Visible.GetValueOrDefault(id)).ToList();
        if (hidden.Count == 0)
        {
            DashboardDrawerItems.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u5168\u90E8\u7EC4\u4EF6\u90FD\u5728\u4EEA\u8868\u76D8\u4E2D"),
                Foreground = DashboardBrush("TextFillColorSecondaryBrush"), FontSize = 16,
                Margin = new Thickness(8, 20, 0, 0) });
            return;
        }
        foreach (var id in hidden)
        {
            var widget = DashboardWidgets.Get(id);
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(DashboardIcon(WidgetGlyph(id), 20));
            var details = new StackPanel { Spacing = 4 };
            details.Children.Add(new TextBlock { Text = widget.Label, FontSize = 14,
                TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            details.Children.Add(new TextBlock { Text = widget.Detail, FontSize = 12,
                TextWrapping = TextWrapping.Wrap, Foreground = DashboardBrush("TextFillColorSecondaryBrush") });
            details.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.F("{0} \u00D7 {1} \u78C1\u8D34", widget.Width, widget.Height), FontSize = 12,
                Foreground = DashboardBrush("TextFillColorSecondaryBrush") });
            Grid.SetColumn(details, 1); row.Children.Add(details);
            var add = new Button { Content = new SymbolIcon(Symbol.Add), Width = 32, Height = 32, Padding = new Thickness(0),
                Style = (Style)Application.Current.Resources["TomatotodoAccentButtonStyle"], VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(add, global::Tomatotodo_Windows.Data.UiText.F("\u6DFB\u52A0{0}\u7EC4\u4EF6", widget.Label));
            add.Click += (_, _) =>
            {
                _draftLayout!.Visible[id] = true;
                Render();
                RenderDrawerItems();
            };
            Grid.SetColumn(add, 2); row.Children.Add(add);
            DashboardDrawerItems.Children.Add(new Border
            {
                Child = row, Padding = new Thickness(16), CornerRadius = new CornerRadius(8),
                BorderBrush = DashboardBrush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1),
                Background = DashboardBrush("CardBackgroundFillColorDefaultBrush")
            });
        }
    }

    private void AttachReorder(Border tile, string id)
    {
        tile.RenderTransform = new CompositeTransform();
        tile.RenderTransformOrigin = new Point(.5, .5);
        tile.ManipulationMode = ManipulationModes.None;
        tile.PointerPressed += (_, e) =>
        {
            if (!_dashboardEditing || _dashboardGrid is null ||
                !e.GetCurrentPoint(tile).Properties.IsLeftButtonPressed) return;
            // Close commands remain ordinary buttons, not drag handles.
            for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null && node != tile;
                 node = VisualTreeHelper.GetParent(node))
                if (node is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase) return;
            StopLongPress();
            _selectedDashboardWidget = id;
            foreach (var item in _dashboardGrid.Children.OfType<Border>().Where(item => item.Tag is string))
                item.BorderBrush = Equals(item.Tag, id)
                    ? DashboardBrush("AccentFillColorDefaultBrush") : DashboardBrush("CardStrokeColorDefaultBrush");
            _pressedWidget = id;
            _pressPosition = e.GetCurrentPoint(PageScroll).Position;
            _dragPointer = _pressPosition;
            _dragGrab = e.GetCurrentPoint(tile).Position;
            _dragSurface = tile;
            _dragPointerId = e.Pointer.PointerId;
            tile.CapturePointer(e.Pointer);
            var hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            hold.Tick += (_, _) =>
            {
                hold.Stop();
                if (_pressedWidget != id) return;
                BeginTileDrag(tile, id);
            };
            _holdTimer = hold;
            hold.Start();
            e.Handled = true;
        };
        tile.PointerMoved += (_, e) =>
        {
            if (_pressedWidget != id || e.Pointer.PointerId != _dragPointerId) return;
            _dragPointer = e.GetCurrentPoint(PageScroll).Position;
            // A deliberate mouse drag can start immediately; touch retains the hold gesture.
            if (_draggingWidget is null && e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse &&
                Math.Abs(_dragPointer.X - _pressPosition.X) + Math.Abs(_dragPointer.Y - _pressPosition.Y) > 6)
                BeginTileDrag(tile, id);
            if (_draggingWidget is not null) UpdateTileDrag();
            e.Handled = true;
        };
        tile.PointerReleased += (_, e) =>
        {
            if (_pressedWidget != id || e.Pointer.PointerId != _dragPointerId) return;
            _dragPointer = e.GetCurrentPoint(PageScroll).Position;
            if (_draggingWidget is not null)
            {
                UpdateTileDrag();
                _hoverSince = DateTime.MinValue;
                UpdateTileDrag();
            }
            FinishReorder();
            e.Handled = true;
        };
        tile.PointerCanceled += (_, _) => { if (_dragSurface == tile) StopLongPress(); };
        tile.PointerCaptureLost += (_, _) => { if (_dragSurface == tile) StopLongPress(); };
        tile.Unloaded += (_, _) => { if (_dragSurface == tile) StopLongPress(); StopTileMotion(tile); };
    }

    private void BeginTileDrag(Border tile, string id)
    {
        if (_draggingWidget is not null || _dashboardGrid is null || _draftLayout is null) return;
        _holdTimer?.Stop();
        _draggingWidget = id;
        _dragOriginalOrder = [.. _draftLayout.Order];
        _lastPreviewPoint = new Point(double.MinValue, double.MinValue);
        StopTileMotion(tile);
        Canvas.SetZIndex(tile, 100);
        _dragOriginalBackground = tile.Background;
        tile.Background = new SolidColorBrush(IsDarkAppearance
            ? ColorHelper.FromArgb(255, 48, 48, 48) : Colors.White);
        tile.Opacity = 1;
        var transform = (CompositeTransform)tile.RenderTransform;
        transform.ScaleX = transform.ScaleY = _systemUiSettings.AnimationsEnabled ? 1.025 : 1;
        _dropSlot = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2),
            BorderBrush = DashboardBrush("CardStrokeColorDefaultBrush"),
            Background = DashboardBrush("SubtleFillColorSecondaryBrush"), Opacity = .5, IsHitTestVisible = false };
        Canvas.SetZIndex(_dropSlot, -1);
        _dashboardGrid.Children.Add(_dropSlot);
        PositionDropSlot();
        KeyDown += DashboardDragKeyDown;
        _dragTick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _dragTick.Tick += (_, _) =>
        {
            var edge = Math.Min(64, PageScroll.ActualHeight / 4);
            var speed = _dragPointer.Y < edge ? -12 * Math.Clamp((edge - _dragPointer.Y) / edge, 0, 1)
                : _dragPointer.Y > PageScroll.ActualHeight - edge
                    ? 12 * Math.Clamp((_dragPointer.Y - PageScroll.ActualHeight + edge) / edge, 0, 1) : 0;
            if (speed != 0 && _dragPointer.X >= 0 && _dragPointer.X <= PageScroll.ActualWidth)
                PageScroll.ChangeView(null, Math.Clamp(PageScroll.VerticalOffset + speed, 0, PageScroll.ScrollableHeight), null, true);
            UpdateTileDrag();
        };
        _dragTick.Start();
        UpdateTileDrag();
    }

    private void DashboardDragKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape) return;
        StopLongPress();
        e.Handled = true;
    }

    private Point TileOrigin(Border tile) => new(
        Grid.GetColumn(tile) * ((_dashboardGrid!.Width + TileGap) / _dashboardColumns),
        Grid.GetRow(tile) * (_dashboardRowHeight + TileGap));

    private void PositionDropSlot()
    {
        if (_dropSlot is null || _dragSurface is null) return;
        Grid.SetColumn(_dropSlot, Grid.GetColumn(_dragSurface)); Grid.SetRow(_dropSlot, Grid.GetRow(_dragSurface));
        Grid.SetColumnSpan(_dropSlot, Grid.GetColumnSpan(_dragSurface)); Grid.SetRowSpan(_dropSlot, Grid.GetRowSpan(_dragSurface));
    }

    private void UpdateTileDrag()
    {
        if (_dragSurface is not { } tile || _draggingWidget is not { } source || _dashboardGrid is null || _draftLayout is null) return;
        var point = PageScroll.TransformToVisual(_dashboardGrid).TransformPoint(_dragPointer);
        var origin = TileOrigin(tile);
        var transform = (CompositeTransform)tile.RenderTransform;
        transform.TranslateX = point.X - _dragGrab.X - origin.X;
        transform.TranslateY = point.Y - _dragGrab.Y - origin.Y;
        if (_dragPointer.X < 0 || _dragPointer.X > PageScroll.ActualWidth || _dragPointer.Y < 0 || _dragPointer.Y > PageScroll.ActualHeight) return;
        // Ignore a small stationary pointer region after reflow, preventing target oscillation.
        if (Math.Abs(point.X - _lastPreviewPoint.X) + Math.Abs(point.Y - _lastPreviewPoint.Y) < 24) return;
        var pitch = (_dashboardGrid.Width + TileGap) / _dashboardColumns;
        var target = _placements.FirstOrDefault(p => p.Id != source && point.X >= p.Column * pitch &&
            point.X <= (p.Column + p.Width) * pitch - TileGap && point.Y >= p.Row * (_dashboardRowHeight + TileGap) &&
            point.Y <= (p.Row + p.Height) * (_dashboardRowHeight + TileGap) - TileGap);
        if (target is null) { _hoverSlot = null; return; }
        var after = point.X > (target.Column + target.Width / 2d) * pitch - TileGap / 2;
        var slot = target.Id + (after ? ":after" : ":before");
        if (_hoverSlot != slot) { _hoverSlot = slot; _hoverSince = DateTime.UtcNow; return; }
        if ((DateTime.UtcNow - _hoverSince).TotalMilliseconds < 120) return;
        var oldOrder = _draftLayout.Order.ToArray();
        DashboardWidgets.MoveRelative(_draftLayout, source, target.Id, after);
        if (oldOrder.SequenceEqual(_draftLayout.Order)) return;
        AnimateTileReflow();
        _lastPreviewPoint = point;
        _hoverSlot = null;
        origin = TileOrigin(tile);
        transform.TranslateX = point.X - _dragGrab.X - origin.X;
        transform.TranslateY = point.Y - _dragGrab.Y - origin.Y;
        PositionDropSlot();
    }

    private void AnimateTileReflow()
    {
        if (_dashboardGrid is null) return;
        var positions = _dashboardGrid.Children.OfType<Border>().Where(tile => tile.Tag is string && tile != _dragSurface)
            .ToDictionary(tile => tile, tile => tile.TransformToVisual(_dashboardGrid).TransformPoint(new Point()));
        foreach (var tile in positions.Keys) StopTileMotion(tile);
        ReflowDashboard();
        _dashboardGrid.UpdateLayout();
        foreach (var (tile, old) in positions)
        {
            var origin = TileOrigin(tile);
            var transform = (CompositeTransform)tile.RenderTransform;
            transform.TranslateX = old.X - origin.X; transform.TranslateY = old.Y - origin.Y;
            SettleTile(tile);
        }
    }

    private void StopTileMotion(Border tile)
    {
        if (_tileMotion.Remove(tile, out var motion)) motion.Stop();
    }

    private void SettleTile(Border tile)
    {
        var transform = (CompositeTransform)tile.RenderTransform;
        if (!_systemUiSettings.AnimationsEnabled)
        {
            transform.TranslateX = transform.TranslateY = 0; transform.ScaleX = transform.ScaleY = 1;
            Canvas.SetZIndex(tile, 0); return;
        }
        var motion = new Storyboard();
        foreach (var (property, from, to) in new[] { ("TranslateX", transform.TranslateX, 0d),
            ("TranslateY", transform.TranslateY, 0d), ("ScaleX", transform.ScaleX, 1d), ("ScaleY", transform.ScaleY, 1d) })
        {
            var animation = new DoubleAnimation { From = from, To = to, Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(animation, transform); Storyboard.SetTargetProperty(animation, property); motion.Children.Add(animation);
        }
        _tileMotion[tile] = motion;
        motion.Completed += (_, _) =>
        {
            if (!_tileMotion.TryGetValue(tile, out var current) || current != motion) return;
            _tileMotion.Remove(tile); motion.Stop();
            transform.TranslateX = transform.TranslateY = 0; transform.ScaleX = transform.ScaleY = 1;
            Canvas.SetZIndex(tile, 0);
        };
        motion.Begin();
    }

    private void FinishReorder()
    {
        var valid = _dragPointer.X >= 0 && _dragPointer.X <= PageScroll.ActualWidth &&
            _dragPointer.Y >= 0 && _dragPointer.Y <= PageScroll.ActualHeight;
        EndTileDrag(commit: valid);
    }

    private void StopLongPress() => EndTileDrag(commit: false);

    private void EndTileDrag(bool commit)
    {
        var tile = _dragSurface;
        var wasDragging = _draggingWidget is not null;
        _holdTimer?.Stop();
        _holdTimer = null;
        _dragTick?.Stop(); _dragTick = null;
        KeyDown -= DashboardDragKeyDown;
        var originalOrder = _dragOriginalOrder;
        _dragOriginalOrder = null;
        if (!commit && originalOrder is not null && _draftLayout is not null)
        {
            var old = tile?.TransformToVisual(_dashboardGrid).TransformPoint(new Point());
            _draftLayout.Order = originalOrder;
            AnimateTileReflow();
            if (tile is not null && old is { } position)
            {
                var origin = TileOrigin(tile);
                var transform = (CompositeTransform)tile.RenderTransform;
                transform.TranslateX = position.X - origin.X; transform.TranslateY = position.Y - origin.Y;
            }
        }
        if (_dropSlot is not null) _dashboardGrid?.Children.Remove(_dropSlot);
        _dropSlot = null; _hoverSlot = null; _dragPointerId = null;
        _pressedWidget = null;
        _draggingWidget = null;
        _dragSurface = null;
        if (tile is not null)
        {
            tile.ReleasePointerCaptures();
            tile.Opacity = 1;
            if (_dragOriginalBackground is not null) tile.Background = _dragOriginalBackground;
            if (wasDragging) SettleTile(tile);
        }
        _dragOriginalBackground = null;
    }
}
