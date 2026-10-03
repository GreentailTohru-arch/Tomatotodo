using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Tomatotodo_Windows.Data;
using Windows.Foundation;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private bool _dashboardEditing;
    private DashboardLayout? _draftLayout;
    private Grid? _dashboardGrid;
    private IReadOnlyList<WidgetPlacement> _placements = [];
    private TextBlock? _timerStatus;
    private TextBlock? _liveClock;
    private ProgressBar? _timerProgress;
    private Line? _hourHand;
    private Line? _minuteHand;
    private Line? _secondHand;
    private DispatcherTimer? _holdTimer;
    private string? _pressedWidget;
    private string? _draggingWidget;
    private Border? _dragSurface;
    private string? _selectedDashboardWidget;
    private Brush? _dragOriginalBackground;
    private Point _pressPosition;
    private const double TileGap = 16;
    private double _dashboardRowHeight = 148;
    private int _dashboardColumns = 6;
    private ToggleSwitch? _miniModeToggle;
    private Grid? _modeTrack;
    private Border? _modeIndicator;
    private TranslateTransform? _modeSlide;
    private Button? _modeFocusButton, _modeBreakButton;
    private Storyboard? _modeStoryboard;

    private Windows.UI.Color Mint => AccentDisplayColor;
    private Windows.UI.Color TextPrimary => IsDarkAppearance
        ? ColorHelper.FromArgb(255, 248, 248, 248) : ColorHelper.FromArgb(255, 28, 28, 28);
    private Windows.UI.Color TextMuted => IsDarkAppearance
        ? ColorHelper.FromArgb(255, 190, 190, 190) : ColorHelper.FromArgb(255, 92, 92, 92);

    private void RenderDashboard()
    {
        var layout = _dashboardEditing ? _draftLayout ??= _state.DashboardLayout.Clone() : _state.DashboardLayout;
        // SizeChanged can fire while the new dashboard is still being built.
        // Reflow only after every widget has been attached to the replacement grid.
        _dashboardGrid = null;
        var grid = new Grid { ColumnSpacing = TileGap, RowSpacing = TileGap };
        var receiver = new Grid();
        var shadow = new ThemeShadow();
        shadow.Receivers.Add(receiver);
        foreach (var id in layout.Order.Where(id => layout.Visible.GetValueOrDefault(id)))
        {
            var tile = BuildWidget(id);
            tile.Tag = id;
            tile.Shadow = shadow;
            tile.Translation = new System.Numerics.Vector3(0, 0, 32);
            if (_dashboardEditing) AttachReorder(tile, id);
            grid.Children.Add(tile);
        }
        // Match Configuration's floating action: no full-width footer strip.
        // Trailing scroll space lets every final-row card clear the action buttons.
        var surface = new Grid { Margin = new Thickness(8, 8, 8, 80),
            HorizontalAlignment = HorizontalAlignment.Center };
        surface.Children.Add(receiver);
        surface.Children.Add(grid);
        PageBody.Children.Add(surface);
        _dashboardGrid = grid;
        if (grid.Children.Count == 0)
            PageBody.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u8FD8\u6CA1\u6709\u7EC4\u4EF6\uFF0C\u70B9\u51FB\u53F3\u4E0A\u89D2\u6DFB\u52A0\u7EC4\u4EF6\u3002"), FontSize = 16,
                Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap });
        ReflowDashboard();
        UpdateTimerDisplay();
        UpdateLiveWidgetClocks();
        grid.Loaded += (_, _) => ReflowDashboard();
    }

    private double DashboardCanvasWidth() => Math.Max(1, Math.Min(1600,
        PageScroll.ActualWidth > 0 ? PageScroll.ActualWidth - 16 : 1040));

    private void ReflowDashboard()
    {
        if (_dashboardGrid is not { } grid || _section != "dashboard") return;
        var width = DashboardCanvasWidth();
        var columns = DashboardWidgets.ColumnsForWidth(width);
        var layout = _dashboardEditing ? _draftLayout! : _state.DashboardLayout;
        _placements = DashboardWidgets.Place(layout, columns);
        var rowCount = _placements.Count == 0 ? 0 : _placements.Max(item => item.Row + item.Height);
        // Fit the default five-row dashboard above its floating actions whenever
        // the viewport allows it. Genuinely short windows still scroll the tiles.
        var heightBudget = PageScroll.ActualHeight > 0 ? (PageScroll.ActualHeight - 88 - 4 * TileGap) / 5 : 148;
        _dashboardRowHeight = Math.Clamp(Math.Min((width - (columns - 1) * TileGap) / columns, heightBudget), 120, 184);
        grid.Width = width;
        DashboardFooterTime.Visibility = width < 380 ? Visibility.Collapsed : Visibility.Visible;
        DashboardStartButton.MinWidth = 100;
        DashboardStartButton.Width = Math.Clamp(width - 56, 100, 200);
        if (_dashboardColumns != columns || grid.ColumnDefinitions.Count == 0)
        {
            _dashboardColumns = columns;
            grid.ColumnDefinitions.Clear();
            for (var column = 0; column < columns; column++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StopLongPress();
        }
        while (grid.RowDefinitions.Count > rowCount) grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count - 1);
        while (grid.RowDefinitions.Count < rowCount) grid.RowDefinitions.Add(new RowDefinition());
        var forecastRow = _placements.FirstOrDefault(item => item.Id == "courseForecast" && item.Height == 1)?.Row;
        for (var index = 0; index < grid.RowDefinitions.Count; index++)
        {
            var row = grid.RowDefinitions[index];
            row.MinHeight = _dashboardRowHeight;
            row.Height = index == forecastRow ? GridLength.Auto : new GridLength(_dashboardRowHeight);
        }
        foreach (var placement in _placements)
        {
            // Settings can change while a layout pass is in flight. A stale
            // placement must not bring down the whole app during SizeChanged.
            var tile = grid.Children.OfType<Border>().FirstOrDefault(item => Equals(item.Tag, placement.Id));
            if (tile is null) continue;
            Grid.SetColumn(tile, placement.Column); Grid.SetRow(tile, placement.Row);
            Grid.SetColumnSpan(tile, placement.Width); Grid.SetRowSpan(tile, placement.Height);
        }
        DashboardDrawerPanel.Width = Math.Min(420, Math.Max(1, PresetDrawerWorkspace.ActualWidth));
    }

    private Brush DashboardBrush(string key)
    {
        // Palette elements created by InitializeComponent can still reference
        // the system fallback resolved before page overrides were registered.
        // Accent roles must use the same owned mutable brushes as dialogs.
        var theme = IsDarkAppearance ? "Default" : "Light";
        if (Resources.ThemeDictionaries.TryGetValue(theme, out var value) &&
            value is ResourceDictionary dictionary &&
            _accentBrushes.Keys.Any(slot => slot.Key == key))
            return (Brush)dictionary[key];
        return DashboardThemePalette.Children.OfType<Border>()
            .First(resource => Equals(resource.Tag, key)).Background;
    }

    private static FontIcon DashboardIcon(string glyph, double size = 16) => new()
    {
        Glyph = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = size,
        VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
    };

    private static ScrollViewer DashboardMarquee(UIElement content)
    {
        var scroll = new ScrollViewer
        {
            Content = content, HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        EnablePresetMetadataAutoScroll(scroll);
        return scroll;
    }

    private Border BuildWidget(string id)
    {
        var title = id == "tasks" ? ActivePreset.Name : DashboardWidgets.Get(id).Label;
        var frame = new Grid { RowSpacing = id == "media" ? 0 : 12 };
        frame.RowDefinitions.Add(new RowDefinition { Height = id == "media" ? new GridLength(0) : new GridLength(24) });
        frame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        if (id != "media")
        {
            var titleRow = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 0, _dashboardEditing ? 32 : 0, 0) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = DashboardIcon(WidgetGlyph(id));
            icon.Foreground = DashboardBrush("TextFillColorSecondaryBrush");
            titleRow.Children.Add(icon);
            var caption = DashboardMarquee(new TextBlock { Text = title, FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = DashboardBrush("TextFillColorPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(caption, 1); titleRow.Children.Add(caption);
            if (id == "timer")
            {
                _timerStatus = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = DashboardBrush("TextFillColorSecondaryBrush") };
                Grid.SetColumn(_timerStatus, 2); titleRow.Children.Add(_timerStatus);
            }
            frame.Children.Add(titleRow);
        }

        var body = (FrameworkElement)WidgetBody(id);
        Grid.SetRow(body, id == "media" ? 0 : 1);
        if (id == "media") Grid.SetRowSpan(body, 2);
        frame.Children.Add(body);
        if (_dashboardEditing)
        {
            body.IsHitTestVisible = false;
            var close = new Button { Content = new SymbolIcon(Symbol.Cancel), Width = 28, Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Style = (Style)Application.Current.Resources["ShellIconButtonStyle"], Margin = new Thickness(0, -2, -2, 0) };
            AutomationProperties.SetName(close, global::Tomatotodo_Windows.Data.UiText.F("\u79FB\u9664{0}\u7EC4\u4EF6", DashboardWidgets.Get(id).Label));
            ToolTipService.SetToolTip(close, global::Tomatotodo_Windows.Data.UiText.T("\u79FB\u9664\u7EC4\u4EF6"));
            close.Click += (_, _) =>
            {
                StopLongPress();
                _draftLayout!.Visible[id] = false;
                Render();
            };
            Grid.SetRowSpan(close, 2);
            frame.Children.Add(close);
        }
        var tile = new Border
        {
            Background = DashboardBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = _dashboardEditing && _selectedDashboardWidget == id
                ? new SolidColorBrush(Mint) : DashboardBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = frame
        };
        tile.PointerEntered += (_, _) => { if (!_dashboardEditing) tile.BorderBrush = DashboardBrush("ControlStrongStrokeColorDefaultBrush"); };
        tile.PointerExited += (_, _) => { if (!_dashboardEditing) tile.BorderBrush = DashboardBrush("CardStrokeColorDefaultBrush"); };
        return tile;
    }

    private static string WidgetGlyph(string id) => id switch
    {
        "timer" => "\uE916", "mode" => "\uE787", "tasks" => "\uE8FD", "calendar" => "\uE787",
        "date" => "\uE823", "weather" => "\uE706", "quote" => "\uE8F2", "courseForecast" => "\uE8F1",
        "todayCourses" => "\uE8F1", "miniwindow" => "\uE8A7", "immersive" => "\uE740",
        "active" => "\uE768", "analogclock" => "\uE917", "countup" => "\uE74A", "countdown" => "\uE787",
        _ => "\uE8D6"
    };

    private UIElement WidgetBody(string id) => id switch
    {
        "timer" => TimerBody(), "mode" => ModeBody(), "tasks" => TasksBody(),
        "date" => DateBody(), "calendar" => CalendarBody(), "weather" => WeatherBody(),
        "quote" => QuoteBody(), "courseForecast" => CourseForecastBody(),
        "active" => ActiveBody(), "todayCourses" => TodayCoursesBody(),
        "miniwindow" or "immersive" or "countup" => SwitchBody(id),
        "analogclock" => AnalogClockBody(), "media" => MediaBody(),
        "countdown" => CountdownBody(),
        _ => PlaceholderBody(global::Tomatotodo_Windows.Data.UiText.T("\u7EC4\u4EF6\u5F00\u53D1\u4E2D"))
    };

    private UIElement TimerBody()
    {
        var grid = new Grid { RowSpacing = 12 };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _timerText = new TextBlock { FontSize = 96, FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontWeight = Microsoft.UI.Text.FontWeights.Light,
            Foreground = DashboardBrush("TextFillColorPrimaryBrush") };
        grid.Children.Add(new Viewbox { Child = _timerText, Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center });
        _timerProgress = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1,
            Foreground = DashboardBrush("AccentFillColorDefaultBrush"),
            VerticalAlignment = VerticalAlignment.Center };
        NormalizeProgressBarLayers(_timerProgress);
        AutomationProperties.SetName(_timerProgress, global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u8BA1\u65F6\u8FDB\u5EA6"));
        Grid.SetRow(_timerProgress, 1);
        grid.Children.Add(_timerProgress);
        var taskName = ActiveTask?.Title ?? global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u9009\u62E9\u4EFB\u52A1");
        var footer = DashboardMarquee(new TextBlock { Text = taskName, FontSize = 14,
            Foreground = DashboardBrush("TextFillColorSecondaryBrush") });
        Grid.SetRow(footer, 2);
        grid.Children.Add(footer);
        return grid;
    }

    private UIElement ModeBody()
    {
        var track = new Grid { ColumnSpacing = 0, VerticalAlignment = VerticalAlignment.Stretch,
            Background = DashboardBrush("SubtleFillColorSecondaryBrush") };
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _modeTrack = track;
        _modeSlide = new TranslateTransform();
        _modeIndicator = new Border { Background = DashboardBrush("AccentFillColorDefaultBrush"),
            CornerRadius = new CornerRadius(4), Margin = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = _modeSlide };
        Grid.SetColumnSpan(_modeIndicator, 2); track.Children.Add(_modeIndicator);
        var focus = new Button { Content = ModeChoiceContent(global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8"), _state.FocusMinutes),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch, MinHeight = 48, Padding = new Thickness(4),
            IsEnabled = !_state.PositiveCountup, CornerRadius = new CornerRadius(4),
            Style = (Style)Application.Current.Resources["ShellIconButtonStyle"] };
        _modeFocusButton = focus;
        AutomationProperties.SetName(focus, global::Tomatotodo_Windows.Data.UiText.F("\u4E13\u6CE8 {0} \u5206\u949F", _state.FocusMinutes));
        focus.Click += (_, _) => SetPhase(false);
        track.Children.Add(focus);
        var shortBreak = new Button { Content = ModeChoiceContent(global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11"), _state.BreakMinutes),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch, MinHeight = 48, Padding = new Thickness(4),
            CornerRadius = new CornerRadius(4), IsEnabled = _state.EnableShortBreak && !_state.PositiveCountup,
            Style = (Style)Application.Current.Resources["ShellIconButtonStyle"] };
        _modeBreakButton = shortBreak;
        AutomationProperties.SetName(shortBreak, global::Tomatotodo_Windows.Data.UiText.F("\u77ED\u4F11 {0} \u5206\u949F", _state.BreakMinutes));
        shortBreak.Click += (_, _) => SetPhase(true);
        Grid.SetColumn(shortBreak, 1); track.Children.Add(shortBreak);
        track.SizeChanged += (_, _) => UpdateModeIndicator(false);
        UpdateModeIndicator(false);
        return track;
    }

    private void UpdateModeIndicator(bool animate)
    {
        if (_modeTrack is null || _modeIndicator is null || _modeSlide is null) return;
        var half = _modeTrack.ActualWidth / 2;
        if (half <= 0) return;
        _modeIndicator.Width = half - 4;
        var destination = _breakPhase ? half : 0;
        _modeStoryboard?.Stop();
        if (animate && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            var animation = new DoubleAnimation { From = _modeSlide.X, To = destination,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var storyboard = new Storyboard();
            Storyboard.SetTarget(animation, _modeSlide);
            Storyboard.SetTargetProperty(animation, "X");
            storyboard.Children.Add(animation);
            storyboard.Completed += (_, _) => { storyboard.Stop(); _modeSlide.X = destination; };
            _modeStoryboard = storyboard; storyboard.Begin();
        }
        else _modeSlide.X = destination;
        foreach (var (button, selected) in new[] { (_modeFocusButton, !_breakPhase), (_modeBreakButton, _breakPhase) })
        {
            if (button?.Content is not StackPanel content) continue;
            foreach (var label in content.Children.OfType<TextBlock>())
                label.Foreground = DashboardBrush(selected ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush");
        }
    }

    private static StackPanel ModeChoiceContent(string name, int minutes) => new()
    {
        Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children =
        {
            new TextBlock { Text = name, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.F("{0} \u5206\u949F", minutes), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center }
        }
    };

    private void SetPhase(bool shortBreak)
    {
        if (_breakPhase == shortBreak || (shortBreak && (!_state.EnableShortBreak || _state.PositiveCountup))) return;
        _running = false;
        if (!_breakPhase) CommitFocus();
        _breakPhase = shortBreak;
        _remainingSeconds = (shortBreak ? _state.BreakMinutes : _state.FocusMinutes) * 60;
        UpdateModeIndicator(true);
        UpdateTimerDisplay();
    }

    private UIElement SwitchBody(string id)
    {
        var panel = new Grid();
        var toggle = new ToggleSwitch { OffContent = "", OnContent = "", MinWidth = 0,
            IsOn = id == "immersive" ? _state.ImmersiveMode : id == "miniwindow" ? _state.MiniWindowMode : _state.PositiveCountup,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        if (id == "miniwindow") _miniModeToggle = toggle;
        AutomationProperties.SetName(toggle, DashboardWidgets.Get(id).Label);
        toggle.Toggled += (_, _) =>
        {
            if (id == "immersive") { _state.ImmersiveMode = toggle.IsOn; ApplyImmersiveMode(); Save(); }
            else if (id == "miniwindow") { _state.MiniWindowMode = toggle.IsOn; SetMiniWindow(toggle.IsOn); Save(); }
            else SetPositiveCountup(toggle.IsOn);
        };
        panel.Children.Add(toggle);
        return panel;
    }

    private UIElement TasksBody()
    {
        var grid = new Grid { RowSpacing = 12 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var progress = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, ActivePreset.Tasks.Count),
            Value = ActivePreset.Tasks.Count(task => task.IsComplete), Height = 4 };
        AutomationProperties.SetName(progress, global::Tomatotodo_Windows.Data.UiText.T("\u6E05\u5355\u5B8C\u6210\u8FDB\u5EA6"));
        grid.Children.Add(progress);
        var rows = new StackPanel { Spacing = 4 };
        foreach (var task in ActivePreset.Tasks)
        {
            var row = new Grid { ColumnSpacing = 4, Padding = new Thickness(8, 4, 8, 4), MinHeight = 44 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var done = new CheckBox { IsChecked = task.IsComplete, Width = 32, MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(done, global::Tomatotodo_Windows.Data.UiText.F("\u5B8C\u6210 {0}", task.Title));
            done.Checked += (_, _) => { CompleteTask(ActivePreset, task); Save(); Render(); };
            done.Unchecked += (_, _) => { task.IsComplete = false; Save(); Render(); };
            row.Children.Add(done);
            var taskContent = new StackPanel { Spacing = 2 };
            taskContent.Children.Add(new TextBlock
                {
                    Text = task.Title, FontSize = 14, TextWrapping = TextWrapping.Wrap,
                    Foreground = DashboardBrush(task.IsComplete ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush"),
                    TextDecorations = task.IsComplete ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None
                });
            if (task.EstimatedPomodoros is > 0)
                taskContent.Children.Add(Secondary(global::Tomatotodo_Windows.Data.UiText.F("{0} / {1} \u4E2A\u756A\u8304", task.CompletedPomodoros, task.EstimatedPomodoros)));
            var title = new Button { Content = taskContent,
                Style = (Style)Application.Current.Resources["ShellIconButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(4), MinHeight = 32 };
            AutomationProperties.SetName(title, global::Tomatotodo_Windows.Data.UiText.F("\u9009\u62E9\u4EFB\u52A1 {0}", task.Title));
            if (!string.IsNullOrWhiteSpace(task.Subtitle)) ToolTipService.SetToolTip(title, task.Subtitle);
            title.Click += (_, _) => SelectTask(task);
            Grid.SetColumn(title, 1); row.Children.Add(title);
            rows.Children.Add(new Border { Child = row, CornerRadius = new CornerRadius(4),
                Background = DashboardBrush("SubtleFillColorSecondaryBrush"),
                BorderBrush = task.Id == _state.ActiveTaskId ? new SolidColorBrush(Mint) : DashboardBrush("CardStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(1) });
        }
        if (ActivePreset.Tasks.Count == 0) rows.Children.Add(Secondary(global::Tomatotodo_Windows.Data.UiText.T("\u8FD8\u6CA1\u6709\u4EFB\u52A1\uFF0C\u53EF\u5728\u914D\u7F6E\u4E2D\u6DFB\u52A0\u3002")));
        var scroll = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); grid.Children.Add(scroll);
        var footer = new Grid { ColumnSpacing = 8 };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(Secondary(global::Tomatotodo_Windows.Data.UiText.F("\u5DF2\u5B8C\u6210 {0}/{1}", ActivePreset.Tasks.Count(task => task.IsComplete), ActivePreset.Tasks.Count)));
        var manage = new HyperlinkButton { Content = global::Tomatotodo_Windows.Data.UiText.T("\u7BA1\u7406\u6E05\u5355"), FontSize = 12, Padding = new Thickness(4, 0, 4, 0), MinHeight = 24 };
        manage.Click += (_, _) => Navigation.SelectedItem = Navigation.MenuItems[1];
        Grid.SetColumn(manage, 1); footer.Children.Add(manage);
        Grid.SetRow(footer, 2); grid.Children.Add(footer);
        return grid;
    }

    private UIElement DateBody()
    {
        var grid = new Grid { ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _liveClock = new TextBlock { FontFamily = new FontFamily("Segoe UI Variable Display"), FontSize = 40,
            Foreground = DashboardBrush("TextFillColorPrimaryBrush"), FontWeight = Microsoft.UI.Text.FontWeights.Light };
        grid.Children.Add(new Viewbox { Child = _liveClock, Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left });
        var date = new TextBlock { Text = UiText.Date(DateTime.Now, "MM / dd\nddd", "d\nddd"), FontSize = 12,
            TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Foreground = DashboardBrush("TextFillColorSecondaryBrush") };
        Grid.SetColumn(date, 1); grid.Children.Add(date);
        return grid;
    }

    private UIElement WeatherBody()
    {
        return BuildWeatherContent();
    }

    private UIElement QuoteBody() => new ScrollViewer
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled,
        VerticalContentAlignment = VerticalAlignment.Center,
        Content = _quoteText = new TextBlock { Text = _quote,
            FontSize = 16, TextWrapping = TextWrapping.Wrap,
            Foreground = DashboardBrush("TextFillColorPrimaryBrush") }
    };

    private UIElement ActiveBody()
    {
        var body = new Grid { RowSpacing = 4 };
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Children.Add(DashboardMarquee(new TextBlock { Text = ActiveTask?.Title ?? global::Tomatotodo_Windows.Data.UiText.T("\u5C1A\u672A\u9009\u62E9\u4EFB\u52A1"),
            FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = DashboardBrush("TextFillColorPrimaryBrush") }));
        var detail = ActiveTask?.EstimatedPomodoros is > 0
            ? global::Tomatotodo_Windows.Data.UiText.F("\u9884\u8BA1 {0} \u4E2A\u756A\u8304 \u00B7 \u5DF2\u5B8C\u6210 {1}", ActiveTask.EstimatedPomodoros, ActiveTask.CompletedPomodoros)
            : ActiveTask?.Subtitle ?? global::Tomatotodo_Windows.Data.UiText.T("\u4ECE\u4EFB\u52A1\u6E05\u5355\u9009\u62E9\u4E00\u9879\u5F00\u59CB");
        var footer = DashboardMarquee(Secondary(detail));
        Grid.SetRow(footer, 1); body.Children.Add(footer);
        return body;
    }

    private UIElement CourseForecastBody()
    {
        var upcoming = _state.CourseSchedule.Weeks.SelectMany(week => week.Events.Select(course =>
            new { week.Date, Course = course }))
            .Select(item => new { item.Course, When = ParseCourseMoment(item.Date, item.Course) })
            .Where(item => item.When >= DateTime.Now)
            .OrderBy(item => item.When).FirstOrDefault();
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u63A5\u4E0B\u6765\u8BFE\u7A0B"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = DashboardBrush("AccentFillColorDefaultBrush") });
        var content = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock { Text = upcoming?.Course.Name ?? global::Tomatotodo_Windows.Data.UiText.T("\u6682\u65E0\u63A5\u4E0B\u6765\u7684\u8BFE\u7A0B"), FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(title);
        var metadata = new TextBlock { Text = upcoming is null ? "" : $"{upcoming.Course.StartTime}–{upcoming.Course.EndTime}\n{upcoming.Course.Room}",
            TextAlignment = TextAlignment.Right, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = DashboardBrush("TextFillColorSecondaryBrush"), TextWrapping = TextWrapping.Wrap, MaxWidth = 140 };
        Grid.SetColumn(metadata, 1); content.Children.Add(metadata);
        content.SizeChanged += (_, _) =>
        {
            var narrow = content.ActualWidth < 250;
            if (content.RowDefinitions.Count == 0) { content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); }
            Grid.SetColumn(metadata, narrow ? 0 : 1); Grid.SetRow(metadata, narrow ? 1 : 0);
            metadata.TextAlignment = narrow ? TextAlignment.Left : TextAlignment.Right;
        };
        panel.Children.Add(content);
        if (upcoming is not null) ToolTipService.SetToolTip(content, $"{upcoming.When:M月d日 ddd} · {upcoming.Course.Teacher}");
        var surface = new Border { Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1), BorderBrush = DashboardBrush("CardStrokeColorDefaultBrush"),
            Child = panel };
        if (upcoming is null) return surface;
        surface.BorderBrush = CourseBrush(CourseTone(upcoming.Course.Tone));
        return DashboardCourseButton(surface, upcoming.Course, DateOnly.FromDateTime(upcoming.When));
    }

    private static DateTime ParseCourseMoment(string weekDate, CourseEvent course)
    {
        var day = DateOnly.ParseExact(weekDate, "yyyy-MM-dd").AddDays(course.Day);
        return TimeOnly.TryParseExact(course.StartTime, "HH:mm", out var time)
            ? day.ToDateTime(time) : DateTime.MinValue;
    }

    private UIElement TodayCoursesBody()
    {
        var week = CourseScheduleCodec.WeekForDate(_state.CourseSchedule, DateOnly.FromDateTime(DateTime.Today));
        var day = ((int)DateTime.Today.DayOfWeek + 6) % 7;
        var panel = new StackPanel { Spacing = 8 };
        var courses = week is null ? Enumerable.Empty<CourseEvent>() :
            week.Events.Where(course => course.Day == day).OrderBy(course => course.Start);
        foreach (var course in courses)
        {
            var content = new StackPanel { Spacing = 4 };
            content.Children.Add(DashboardMarquee(Secondary($"{course.StartTime}–{course.EndTime}")));
            content.Children.Add(new TextBlock { Text = course.Name, FontSize = 14,
                TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            content.Children.Add(DashboardMarquee(Secondary($"{course.Room} · {course.Teacher}")));
            var surface = new Border { Child = content, Padding = new Thickness(8), CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1), BorderBrush = CourseBrush(CourseTone(course.Tone)),
                Background = DashboardBrush("SubtleFillColorSecondaryBrush") };
            panel.Children.Add(DashboardCourseButton(surface, course, DateOnly.FromDateTime(DateTime.Today)));
        }
        if (panel.Children.Count == 0) panel.Children.Add(Secondary(global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u5929\u6CA1\u6709\u8BFE\u7A0B")));
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled };
    }

    private UIElement MediaBody() => BuildMediaBody();

    private Button DashboardCourseButton(UIElement content, CourseEvent course, DateOnly date)
    {
        var button = new Button
        {
            Content = content, Padding = new Thickness(0), MinWidth = 0,
            BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(button, global::Tomatotodo_Windows.Data.UiText.F("\u67E5\u770B\u8BFE\u7A0B\u8BE6\u60C5\uFF1A{0}\uFF0C{1}", course.Name, $"{date:M/d}"));
        button.Click += async (_, _) => await ShowCourseDetail(course, date);
        return button;
    }

    private UIElement CountdownBody()
    {
        if (_state.CountdownDate is not { } date) return PlaceholderBody(global::Tomatotodo_Windows.Data.UiText.T("\u5C1A\u672A\u8BBE\u7F6E\u5012\u6570\u65E5"));
        var days = (date.Date - DateTimeOffset.Now.Date).Days;
        return new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
            Children =
        {
            DashboardMarquee(Secondary(_state.CountdownName)),
            CountdownValue(days)
        }};
    }

    private static FrameworkElement CountdownValue(int days)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
        panel.Children.Add(new TextBlock { Text = Math.Abs(days).ToString(), FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = days >= 0 ? global::Tomatotodo_Windows.Data.UiText.T("\u5929\u540E") : global::Tomatotodo_Windows.Data.UiText.T("\u5929\u524D"), FontSize = 14, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5) });
        Grid.SetRow(panel, 1); return panel;
    }

    private UIElement PlaceholderBody(string text) => new TextBlock
    {
        Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13,
        Foreground = DashboardBrush("TextFillColorSecondaryBrush"), VerticalAlignment = VerticalAlignment.Center
    };

    private TextBlock Secondary(string text) => new()
    { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap,
        Foreground = DashboardBrush("TextFillColorSecondaryBrush"), VerticalAlignment = VerticalAlignment.Center };

    private UIElement CalendarBody()
    {
        var now = DateTime.Today;
        var first = new DateTime(now.Year, now.Month, 1);
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var body = new Grid { RowSpacing = 8 };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(Secondary(global::Tomatotodo_Windows.Data.UiText.Date(now, "yyyy\u5E74 M\u6708", "Y")));
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (var i = 0; i < 7; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 7; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for (var column = 0; column < 7; column++)
        {
            var heading = Secondary(global::Tomatotodo_Windows.Data.UiText.Weekday(column));
            heading.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(heading, column); grid.Children.Add(heading);
        }
        for (var position = 0; position < 42; position++)
        {
            var date = first.AddDays(position - offset);
            var today = date == now;
            var text = new TextBlock { Text = date.Day.ToString(), FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = DashboardBrush(today ? "TextOnAccentFillColorPrimaryBrush" :
                    date.Month == now.Month ? "TextFillColorPrimaryBrush" : "TextFillColorDisabledBrush") };
            var circle = new Border { CornerRadius = new CornerRadius(4),
                Background = DashboardBrush(today ? "AccentFillColorDefaultBrush" : "SubtleFillColorTransparentBrush"),
                Child = text };
            Grid.SetColumn(circle, position % 7); Grid.SetRow(circle, position / 7 + 1);
            grid.Children.Add(circle);
        }
        Grid.SetRow(grid, 1); body.Children.Add(grid);
        return body;
    }

    private UIElement AnalogClockBody()
    {
        var canvas = new Canvas { Width = 126, Height = 126, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center };
        canvas.Children.Add(new Ellipse { Width = 124, Height = 124,
            Stroke = DashboardBrush("CardStrokeColorDefaultBrush"), StrokeThickness = 2 });
        if (_state.ShowClockMarkers)
            for (var index = 0; index < 12; index++)
            {
                var angle = index * Math.PI / 6 - Math.PI / 2;
                var marker = new Ellipse { Width = 3, Height = 3, Fill = DashboardBrush("TextFillColorSecondaryBrush") };
                Canvas.SetLeft(marker, 60.5 + Math.Cos(angle) * 53); Canvas.SetTop(marker, 60.5 + Math.Sin(angle) * 53); canvas.Children.Add(marker);
            }
        _hourHand = NewHand(4, 35); _minuteHand = NewHand(3, 49); _secondHand = NewHand(1, 53);
        canvas.Children.Add(_hourHand); canvas.Children.Add(_minuteHand); canvas.Children.Add(_secondHand);
        _secondHand.Visibility = _state.ShowClockSeconds ? Visibility.Visible : Visibility.Collapsed;
        var center = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Mint) };
        Canvas.SetLeft(center, 58.5); Canvas.SetTop(center, 58.5); canvas.Children.Add(center);
        _hourHand.Stroke = DashboardBrush("TextFillColorPrimaryBrush");
        _minuteHand.Stroke = DashboardBrush("TextFillColorPrimaryBrush");
        return new Viewbox { Child = canvas, Stretch = Stretch.Uniform, Margin = new Thickness(-4, 0, -4, 0) };
    }

    private Line NewHand(double thickness, double length) => new()
    {
        X1 = 62, Y1 = 62, X2 = 62, Y2 = 62 - length,
        Stroke = new SolidColorBrush(Mint), StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
    };

    private void UpdateLiveWidgetClocks()
    {
        if (_liveClock is not null) _liveClock.Text = DateTime.Now.ToString("HH:mm");
        if (_timerProgress is not null)
        {
            var duration = (_breakPhase ? _state.BreakMinutes : _state.FocusMinutes) * 60;
            _timerProgress.Value = !_state.PositiveCountup && duration > 0 ? Math.Clamp((double)(duration - _remainingSeconds) / duration, 0, 1) : 0;
        }
        var now = DateTime.Now;
        PositionHand(_hourHand, ((now.Hour % 12) + now.Minute / 60.0) / 12, 35);
        PositionHand(_minuteHand, (now.Minute + now.Second / 60.0) / 60, 49);
        PositionHand(_secondHand, now.Second / 60.0, 53);
    }

    private static void PositionHand(Line? hand, double revolution, double length)
    {
        if (hand is null) return;
        var angle = revolution * Math.PI * 2 - Math.PI / 2;
        hand.X2 = 62 + Math.Cos(angle) * length;
        hand.Y2 = 62 + Math.Sin(angle) * length;
    }
}
