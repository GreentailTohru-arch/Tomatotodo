using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Tomatotodo_Windows.Data;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private DateTime _archiveMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateOnly _archiveSelectedDate = DateOnly.FromDateTime(DateTime.Today);
    private DateTime _archiveStatsCursor = DateTime.Today;
    private string _archiveStatsPeriod = "月度";
    private int _archiveHeatmapYear = DateTime.Today.Year;
    private double _archiveRenderedViewportWidth;
    private ThemeShadow? _archiveCardShadow;
    private Grid? _archiveWorkspace;
    private Border? _archiveCalendarCard;
    private Border? _archiveDayLogCard;
    private Border? _archiveStatisticsCard;
    private TextBlock? _archiveStatisticsPeriodLabel;
    private Grid? _archiveStatisticsChartHost;
    private Canvas? _archiveStatisticsPlotLayer;
    private Storyboard? _archiveStatisticsSwitchAnimation;
    private readonly List<Button> _archiveStatisticsMarkers = new();
    private Border? _archiveHeatmapCard;
    private TextBlock? _archiveCalendarMonthLabel;
    private TextBlock? _archiveCalendarMonthCount;
    private readonly List<Button> _archiveCalendarCellButtons = new();
    private bool _archiveCompactLayout;
    private int _archiveRowOffset;
    private readonly Dictionary<DateOnly, Button> _archiveCalendarDayButtons = new();

    private Windows.UI.Color ArchiveSurface => IsDarkAppearance ? ColorHelper.FromArgb(255, 45, 45, 45) : Colors.White;
    private Windows.UI.Color ArchiveSurfaceStrong => IsDarkAppearance ? ColorHelper.FromArgb(255, 51, 51, 51) : ColorHelper.FromArgb(255, 248, 248, 248);
    private Windows.UI.Color ArchiveBorder => IsDarkAppearance ? ColorHelper.FromArgb(255, 65, 65, 65) : ColorHelper.FromArgb(255, 222, 222, 222);
    private Windows.UI.Color ArchiveMint => AccentDisplayColor;
    private Windows.UI.Color ArchiveText => IsDarkAppearance ? ColorHelper.FromArgb(255, 248, 248, 248) : ColorHelper.FromArgb(255, 28, 28, 28);
    private Windows.UI.Color ArchiveMuted => IsDarkAppearance ? ColorHelper.FromArgb(255, 184, 184, 184) : ColorHelper.FromArgb(255, 96, 96, 96);

    private void RenderArchive()
    {
        _archiveStatisticsSwitchAnimation?.Stop();
        _archiveStatisticsSwitchAnimation = null;
        _archiveRenderedViewportWidth = PageScroll.ActualWidth;
        var now = DateTime.Now;
        _archiveMonth = new DateTime(_archiveMonth.Year, _archiveMonth.Month, 1);
        var logs = ArchiveLogsWithLiveSegment();
        var today = ArchiveAnalytics.SummaryForDay(logs, DateOnly.FromDateTime(now));
        var viewportWidth = PageScroll.ActualWidth > 0 ? PageScroll.ActualWidth : 960;
        var compact = viewportWidth < 1040;
        _archiveCompactLayout = compact;
        _archiveRowOffset = _accountSession.Current is null ? 0 : 1;
        var safeWidth = Math.Max(280, viewportWidth - 40);
        var layer = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        var shadowReceiver = new Grid { IsHitTestVisible = false };
        layer.Children.Add(shadowReceiver);
        _archiveCardShadow = new ThemeShadow();
        _archiveCardShadow.Receivers.Add(shadowReceiver);
        var workspace = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 16,
            Width = safeWidth,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _archiveWorkspace = workspace;
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (!compact) workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < (compact ? 5 : 3) + _archiveRowOffset; row++)
            workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (_accountSession.Current is { } profile)
        {
            var profileCard = BuildArchiveProfile(profile);
            Grid.SetColumnSpan(profileCard, compact ? 1 : 2);
            workspace.Children.Add(profileCard);
        }

        var metricGrid = BuildArchiveMetrics(today, 4);
        const double minimumMetricsWidth = 4 * 180 + 3 * 12;
        metricGrid.Width = Math.Max(safeWidth, minimumMetricsWidth);
        metricGrid.HorizontalAlignment = HorizontalAlignment.Left;
        var metrics = new ScrollViewer
        {
            Content = metricGrid,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetColumnSpan(metrics, compact ? 1 : 2);
        Grid.SetRow(metrics, _archiveRowOffset);
        workspace.Children.Add(metrics);

        _archiveCalendarCard = BuildArchiveCalendar(logs);
        Grid.SetRow(_archiveCalendarCard, 1 + _archiveRowOffset);
        workspace.Children.Add(_archiveCalendarCard);

        _archiveDayLogCard = BuildArchiveDayLog(logs);
        if (compact) Grid.SetRow(_archiveDayLogCard, 2 + _archiveRowOffset);
        else { Grid.SetColumn(_archiveDayLogCard, 1); Grid.SetRow(_archiveDayLogCard, 1 + _archiveRowOffset); }
        workspace.Children.Add(_archiveDayLogCard);

        _archiveStatisticsCard = BuildArchiveStatistics(logs);
        if (compact) Grid.SetRow(_archiveStatisticsCard, 3 + _archiveRowOffset);
        else Grid.SetRow(_archiveStatisticsCard, 2 + _archiveRowOffset);
        workspace.Children.Add(_archiveStatisticsCard);

        _archiveHeatmapCard = BuildArchiveHeatmap(logs);
        if (compact) Grid.SetRow(_archiveHeatmapCard, 4 + _archiveRowOffset);
        else { Grid.SetColumn(_archiveHeatmapCard, 1); Grid.SetRow(_archiveHeatmapCard, 2 + _archiveRowOffset); }
        workspace.Children.Add(_archiveHeatmapCard);
        layer.Children.Add(workspace);
        PageBody.Children.Add(layer);
    }

    private void ReplaceArchiveCard(ref Border? current, Border replacement, int row, int column = 0)
    {
        if (_archiveWorkspace is null)
        {
            Render();
            return;
        }

        if (current is not null) _archiveWorkspace.Children.Remove(current);
        Grid.SetRow(replacement, row + _archiveRowOffset);
        Grid.SetColumn(replacement, column);
        _archiveWorkspace.Children.Add(replacement);
        current = replacement;
    }

    private void RefreshArchiveCalendar()
    {
        var logs = ArchiveLogsWithLiveSegment();
        if (_archiveCalendarCard is not null && _archiveCalendarMonthLabel is not null &&
            _archiveCalendarMonthCount is not null && _archiveCalendarCellButtons.Count == 42)
        {
            UpdateArchiveCalendarContent(logs);
            return;
        }
        ReplaceArchiveCard(ref _archiveCalendarCard, BuildArchiveCalendar(logs), 1);
    }

    private void RefreshArchiveDayLog()
    {
        var logs = ArchiveLogsWithLiveSegment();
        ReplaceArchiveCard(ref _archiveDayLogCard, BuildArchiveDayLog(logs), _archiveCompactLayout ? 2 : 1, _archiveCompactLayout ? 0 : 1);
    }

    private void RefreshArchiveStatistics(bool animate = false)
    {
        var logs = ArchiveLogsWithLiveSegment();
        if (_archiveStatisticsCard is not null && _archiveStatisticsPeriodLabel is not null &&
            _archiveStatisticsChartHost is not null)
        {
            _archiveStatisticsSwitchAnimation?.Stop();
            _archiveStatisticsSwitchAnimation = null;
            _archiveStatisticsPeriodLabel.Text = ArchivePeriodLabel();
            _archiveStatisticsChartHost.Children.Clear();
            _archiveStatisticsChartHost.Children.Add(BuildFocusChart(
                ArchiveAnalytics.Series(logs, _archiveStatsPeriod, _archiveStatsCursor), _archiveStatsPeriod));
            if (animate) AnimateArchiveStatisticsSwitch();
            else ResetArchiveStatisticsAnimationState();
            return;
        }
        ReplaceArchiveCard(ref _archiveStatisticsCard, BuildArchiveStatistics(logs), _archiveCompactLayout ? 3 : 2);
    }

    private void RefreshArchiveHeatmap()
    {
        var logs = ArchiveLogsWithLiveSegment();
        ReplaceArchiveCard(ref _archiveHeatmapCard, BuildArchiveHeatmap(logs), _archiveCompactLayout ? 4 : 2, _archiveCompactLayout ? 0 : 1);
    }

    private List<FocusLog> ArchiveLogsWithLiveSegment()
    {
        var logs = _state.FocusLogs.Where(log => log.Seconds > 0).ToList();
        if (!_breakPhase && _uncommittedSeconds > 0)
        {
            logs.Add(new FocusLog
            {
                TaskId = _state.ActiveTaskId,
                StartedAt = _segmentStart ?? DateTimeOffset.Now.AddSeconds(-_uncommittedSeconds),
                Seconds = _uncommittedSeconds
            });
        }
        return logs;
    }

    private Border BuildArchiveProfile(Tomatotodo_Windows.Accounts.UserProfile profile)
    {
        var content = new Grid { ColumnSpacing = 16, MinHeight = 88 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var picture = new PersonPicture { Width = 80, Height = 80, DisplayName = profile.Nickname,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(picture);
        var details = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock { Text = profile.Nickname, FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush(ArchiveText),
            TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(profile.Biography)
                ? global::Tomatotodo_Windows.Data.UiText.T("\u5C1A\u672A\u586B\u5199\u4E2A\u4EBA\u7B80\u4ECB") : profile.Biography,
            FontSize = 13, Foreground = Brush(ArchiveMuted), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(details, 1); content.Children.Add(details);
        _ = LoadArchiveProfilePictureAsync(picture, profile.Id);
        return ArchiveCard(content);
    }

    private async Task LoadArchiveProfilePictureAsync(PersonPicture picture, Guid userId)
    {
        try
        {
            var bytes = _accountStorage.Read(userId).Avatar;
            using var memory = new MemoryStream(bytes);
            using var stream = memory.AsRandomAccessStream();
            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            if (_accountSession.Current?.Id == userId) picture.ProfilePicture = image;
        }
        catch { /* PersonPicture retains its initials when the cached avatar is unavailable. */ }
    }

    private Grid BuildArchiveMetrics(ArchiveAnalytics.DaySummary today, int columns)
    {
        var metrics = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var index = 0; index < columns; index++)
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < (int)Math.Ceiling(4d / columns); index++)
            metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var totalTasks = ActivePreset.Tasks.Count;
        var completeTasks = ActivePreset.Tasks.Count(task => task.IsComplete);
        var cards = new[]
        {
            ArchiveMetric(IconGlyph.Stopwatch, global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u65E5\u4E13\u6CE8"), CompactDuration(today.Seconds)),
            ArchiveMetric(IconGlyph.Completed, global::Tomatotodo_Windows.Data.UiText.T("\u5B8C\u6210\u756A\u8304"), today.Pomodoros == 0 ? global::Tomatotodo_Windows.Data.UiText.T("\u5B8C\u6210\u4E00\u8F6E\u540E\uFF0C\u756A\u8304\u4F1A\u957F\u5728\u8FD9\u91CC") : global::Tomatotodo_Windows.Data.UiText.F("{0} \u4E2A\u756A\u8304", today.Pomodoros)),
            ArchiveMetric(IconGlyph.CheckList, global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u65E5\u4EFB\u52A1"), $"{completeTasks} / {totalTasks}"),
            ArchiveMetric(IconGlyph.Chart, global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u6B21\u6570"), global::Tomatotodo_Windows.Data.UiText.F("{0} \u6B21", today.Sessions))
        };
        for (var index = 0; index < cards.Length; index++)
        {
            Grid.SetColumn(cards[index], index % columns);
            Grid.SetRow(cards[index], index / columns);
            metrics.Children.Add(cards[index]);
        }
        return metrics;
    }

    private Border ArchiveMetric(string glyph, string title, string value)
    {
        var body = new Grid { MinHeight = 112 };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        label.Children.Add(ArchiveGlyph(glyph, 24));
        label.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush(ArchiveMuted), VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
        body.Children.Add(label);
        var compactValue = value.Length > 12;
        var valueText = new TextBlock { Text = value, FontSize = compactValue ? 13 : 31,
            FontFamily = new FontFamily(compactValue ? "Segoe UI" : "Georgia"),
            TextWrapping = TextWrapping.Wrap, Foreground = Brush(ArchiveText), VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetRow(valueText, 1); body.Children.Add(valueText);
        return ArchiveCard(body);
    }

    private Border BuildArchiveCalendar(IReadOnlyList<FocusLog> logs)
    {
        var content = new Grid { RowSpacing = 12 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(ArchiveHeading(IconGlyph.Calendar, global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u65E5\u5386"), BuildMonthControls()));
        var monthly = logs.Where(log => log.StartedAt.LocalDateTime.Year == _archiveMonth.Year &&
            log.StartedAt.LocalDateTime.Month == _archiveMonth.Month).ToList();
        var count = monthly.Count(log => log.CompletedPomodoro);
        var monthSummaryBody = new Grid();
        monthSummaryBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        monthSummaryBody.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        monthSummaryBody.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\uD83C\uDF45  \u672C\u6708\u756A\u8304"), Foreground = Brush(ArchiveMuted), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        _archiveCalendarMonthCount = new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.F("{0} \u4E2A", count), Foreground = Brush(ArchiveMint), FontFamily = new FontFamily("Georgia"), FontSize = 22 };
        Grid.SetColumn(_archiveCalendarMonthCount, 1); monthSummaryBody.Children.Add(_archiveCalendarMonthCount);
        var monthSummary = new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = monthSummaryBody
        };
        Grid.SetRow(monthSummary, 1); content.Children.Add(monthSummary);
        _archiveCalendarDayButtons.Clear();
        _archiveCalendarCellButtons.Clear();
        var grid = new Grid { ColumnSpacing = 6, RowSpacing = 6, MinHeight = 328 };
        for (var index = 0; index < 7; index++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < 7; index++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for (var column = 0; column < 7; column++)
        {
            var heading = new TextBlock { Text = UiText.Weekday(column), FontSize = 11, Foreground = Brush(ArchiveMuted), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(heading, column); grid.Children.Add(heading);
        }
        for (var index = 0; index < 42; index++)
        {
            var dayButton = new Button { Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new CornerRadius(6) };
            dayButton.Content = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 1,
                Children =
                {
                    new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13 },
                    new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 9 }
                }
            };
            dayButton.Click += ArchiveCalendarDay_Click;
            _archiveCalendarCellButtons.Add(dayButton);
            Grid.SetColumn(dayButton, index % 7); Grid.SetRow(dayButton, index / 7 + 1); grid.Children.Add(dayButton);
        }
        UpdateArchiveCalendarContent(logs);
        Grid.SetRow(grid, 2); content.Children.Add(grid);
        return ArchiveCard(content, 500);
    }

    private void UpdateArchiveCalendarContent(IReadOnlyList<FocusLog> logs)
    {
        if (_archiveCalendarMonthLabel is not null) _archiveCalendarMonthLabel.Text = _archiveMonth.ToString("yyyy / MM");
        if (_archiveCalendarMonthCount is not null)
        {
            _archiveCalendarMonthCount.Text = global::Tomatotodo_Windows.Data.UiText.F("{0} \u4E2A", logs.Count(log => log.CompletedPomodoro &&
                log.StartedAt.LocalDateTime.Year == _archiveMonth.Year &&
                log.StartedAt.LocalDateTime.Month == _archiveMonth.Month));
        }

        var cells = ArchiveAnalytics.MonthCells(_archiveMonth);
        var days = ArchiveAnalytics.Days(logs);
        _archiveCalendarDayButtons.Clear();
        for (var index = 0; index < _archiveCalendarCellButtons.Count && index < cells.Count; index++)
        {
            var button = _archiveCalendarCellButtons[index];
            var date = cells[index];
            days.TryGetValue(date, out var summary);
            button.Tag = date;
            ApplyArchiveCalendarDayVisual(button, date, summary);
            ToolTipService.SetToolTip(button, global::Tomatotodo_Windows.Data.UiText.F("{0} \u00B7 {1} \u00B7 {2} \u4E2A\u756A\u8304", $"{date:yyyy-MM-dd}", CompactDuration(summary?.Seconds ?? 0), summary?.Pomodoros ?? 0));
            _archiveCalendarDayButtons[date] = button;
        }
    }

    private void ArchiveCalendarDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button dayButton || dayButton.Tag is not DateOnly date || date == _archiveSelectedDate) return;
        if (date.Year != _archiveMonth.Year || date.Month != _archiveMonth.Month) return;
        var previous = _archiveSelectedDate;
        _archiveSelectedDate = date;
        var days = ArchiveAnalytics.Days(ArchiveLogsWithLiveSegment());
        if (_archiveCalendarDayButtons.TryGetValue(previous, out var previousButton))
        {
            days.TryGetValue(previous, out var previousSummary);
            ApplyArchiveCalendarDayVisual(previousButton, previous, previousSummary);
        }
        days.TryGetValue(date, out var selectedSummary);
        ApplyArchiveCalendarDayVisual(dayButton, date, selectedSummary);
        RefreshArchiveDayLog();
    }

    private void ApplyArchiveCalendarDayVisual(Button button, DateOnly date, ArchiveAnalytics.DaySummary? summary)
    {
        var inMonth = date.Month == _archiveMonth.Month && date.Year == _archiveMonth.Year;
        var selected = inMonth && date == _archiveSelectedDate;
        var isToday = date == DateOnly.FromDateTime(DateTime.Today);
        var todayFill = inMonth && isToday && !selected;
        var onAccent = ContrastRatio(Colors.Black, ArchiveMint) >= ContrastRatio(Colors.White, ArchiveMint)
            ? Colors.Black : Colors.White;
        var outOfMonthBackground = IsDarkAppearance
            ? ColorHelper.FromArgb(255, 39, 39, 39)
            : ColorHelper.FromArgb(255, 243, 243, 243);
        var outOfMonthText = IsDarkAppearance
            ? ColorHelper.FromArgb(255, 116, 116, 116)
            : ColorHelper.FromArgb(255, 163, 163, 163);
        button.IsTabStop = inMonth;
        button.Background = Brush(selected ? ArchiveSurface : todayFill ? ArchiveMint : !inMonth
            ? outOfMonthBackground : summary?.Seconds > 0 ? ArchiveSurfaceStrong : ArchiveSurface);
        button.BorderBrush = Brush(selected ? ArchiveMint : !inMonth ? outOfMonthBackground : ArchiveBorder);
        button.BorderThickness = new Thickness(selected ? 2 : 1);
        if (button.Content is StackPanel content && content.Children.Count == 2 &&
            content.Children[0] is TextBlock dayText && content.Children[1] is TextBlock durationText)
        {
            var textColor = todayFill ? onAccent : inMonth ? ArchiveText : outOfMonthText;
            dayText.Text = date.Day.ToString();
            dayText.Foreground = Brush(textColor);
            durationText.Text = summary?.Seconds > 0 ? $"{Math.Max(1, summary.Seconds / 60)}" : "";
            durationText.Foreground = Brush(todayFill ? onAccent : inMonth ? ArchiveMuted : outOfMonthText);
        }
    }

    private FrameworkElement BuildMonthControls()
    {
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(ArchiveRoundButton(Symbol.Previous, global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u4E2A\u6708"), () =>
        {
            _archiveMonth = _archiveMonth.AddMonths(-1);
            RefreshArchiveCalendar();
        }));
        _archiveCalendarMonthLabel = new TextBlock
        {
            Text = _archiveMonth.ToString("yyyy / MM"), Foreground = Brush(ArchiveText), FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center,
            MinWidth = 76, Margin = new Thickness(5, 0, 5, 0)
        };
        controls.Children.Add(_archiveCalendarMonthLabel);
        controls.Children.Add(ArchiveRoundButton(Symbol.Next, global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u4E2A\u6708"), () =>
        {
            _archiveMonth = _archiveMonth.AddMonths(1);
            RefreshArchiveCalendar();
        }));
        return controls;
    }

    private Border BuildArchiveDayLog(IReadOnlyList<FocusLog> logs)
    {
        var selected = logs.Where(log => ArchiveAnalytics.LocalDate(log) == _archiveSelectedDate)
            .OrderBy(log => log.StartedAt).ToList();
        var content = new Grid { RowSpacing = 12 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var total = selected.Sum(log => log.Seconds);
        content.Children.Add(ArchiveHeading(IconGlyph.Document, global::Tomatotodo_Windows.Data.UiText.F("{0} \u65E5\u5FD7", $"{_archiveSelectedDate:yyyy-MM-dd}"),
            new TextBlock { Text = CompactDuration(total), FontFamily = new FontFamily("Georgia"), FontSize = 25, Foreground = Brush(ArchiveMint) }));
        if (selected.Count == 0)
        {
            var empty = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 9 };
            empty.Children.Add(ArchiveGlyph(IconGlyph.History, 40, true));
            empty.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u8FD9\u4E00\u5929\u8FD8\u6CA1\u6709\u4E13\u6CE8\u8BB0\u5F55\u3002"), Foreground = Brush(ArchiveMuted), FontSize = 13 });
            Grid.SetRow(empty, 1); content.Children.Add(empty);
        }
        else
        {
            var rows = new StackPanel { Spacing = 8 };
            foreach (var log in selected)
            {
                var name = TaskName(log.TaskId);
                var rowBody = new Grid();
                rowBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowBody.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowBody.Children.Add(new TextBlock { Text = $"{log.StartedAt.LocalDateTime:HH:mm}  ·  {name}" + (log.CompletedPomodoro ? "  🍅" : ""),
                    TextWrapping = TextWrapping.Wrap, Foreground = Brush(ArchiveText), FontSize = 13 });
                var length = new TextBlock { Text = CompactDuration(log.Seconds), Foreground = Brush(ArchiveMint), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
                Grid.SetColumn(length, 1); rowBody.Children.Add(length);
                rows.Children.Add(new Border
                {
                    Padding = new Thickness(12, 10, 12, 10),
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Child = rowBody
                });
            }
            var scroll = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1); content.Children.Add(scroll);
        }
        return ArchiveCard(content, 500);
    }

    private Border BuildArchiveStatistics(IReadOnlyList<FocusLog> logs)
    {
        var content = new Grid { RowSpacing = 10 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var paging = BuildArchivePagingControls(ArchivePeriodLabel(), global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u4E00\u4E2A\u7EDF\u8BA1\u5468\u671F"), global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u4E00\u4E2A\u7EDF\u8BA1\u5468\u671F"),
            () => { ShiftStats(-1); RefreshArchiveStatistics(animate: true); },
            () => { ShiftStats(1); RefreshArchiveStatistics(animate: true); },
            labelCreated: label => _archiveStatisticsPeriodLabel = label);
        content.Children.Add(ArchiveHeading(IconGlyph.Chart, global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u7EDF\u8BA1"), paging));
        var selector = new SelectorBar
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };
        SelectorBarItem? selectedItem = null;
        foreach (var period in new[] { "日度", "周度", "月度", "年度" })
        {
            var item = new SelectorBarItem { Text = Data.UiText.T(period), Tag = period };
            selector.Items.Add(item);
            if (period == _archiveStatsPeriod) selectedItem = item;
        }
        selector.SelectedItem = selectedItem;
        selector.SelectionChanged += (_, _) =>
        {
            var period = selector.SelectedItem?.Tag as string;
            if (string.IsNullOrWhiteSpace(period) || period == _archiveStatsPeriod) return;
            _archiveStatsPeriod = period;
            RefreshArchiveStatistics(animate: true);
        };
        Grid.SetRow(selector, 1); content.Children.Add(selector);
        var series = ArchiveAnalytics.Series(logs, _archiveStatsPeriod, _archiveStatsCursor);
        _archiveStatisticsChartHost = new Grid();
        _archiveStatisticsChartHost.Children.Add(BuildFocusChart(series, _archiveStatsPeriod));
        Grid.SetRow(_archiveStatisticsChartHost, 2); content.Children.Add(_archiveStatisticsChartHost);
        return ArchiveCard(content, 386);
    }

    private void ResetArchiveStatisticsAnimationState()
    {
        if (_archiveStatisticsChartHost is not null)
        {
            _archiveStatisticsChartHost.Opacity = 1;
            if (_archiveStatisticsChartHost.RenderTransform is TranslateTransform offset) offset.Y = 0;
        }
        if (_archiveStatisticsPlotLayer?.RenderTransform is ScaleTransform growth) growth.ScaleY = 1;
        foreach (var marker in _archiveStatisticsMarkers) marker.Opacity = 1;
    }

    private void AnimateArchiveStatisticsSwitch()
    {
        if (_archiveStatisticsChartHost is null) return;
        if (!_systemUiSettings.AnimationsEnabled)
        {
            ResetArchiveStatisticsAnimationState();
            return;
        }

        var chart = _archiveStatisticsChartHost;
        chart.Opacity = 1;
        var storyboard = new Storyboard();
        if (_archiveStatisticsPlotLayer?.RenderTransform is ScaleTransform growth)
        {
            var rise = new DoubleAnimation
            {
                From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(480)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(rise, growth);
            Storyboard.SetTargetProperty(rise, "ScaleY");
            storyboard.Children.Add(rise);
        }
        for (var index = 0; index < _archiveStatisticsMarkers.Count; index++)
            AddArchiveStatisticsFade(storyboard, _archiveStatisticsMarkers[index], 0, 1, 120, 100 + index * 8);
        _archiveStatisticsSwitchAnimation = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_archiveStatisticsSwitchAnimation, storyboard)) return;
            _archiveStatisticsSwitchAnimation = null;
            ResetArchiveStatisticsAnimationState();
        };
        storyboard.Begin();
    }

    private static void AddArchiveStatisticsFade(Storyboard storyboard, UIElement target,
        double from, double to, int durationMs, int delayMs)
    {
        var fade = new DoubleAnimation
        {
            From = from, To = to, Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fade, target);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);
    }

    private FrameworkElement BuildFocusChart(IReadOnlyList<ArchiveAnalytics.SeriesPoint> series, string period)
    {
        _archiveStatisticsMarkers.Clear();
        const double width = 500;
        const double height = 210;
        const double left = 36;
        const double top = 18;
        const double bottom = 31;
        var canvas = new Canvas { Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Center };
        const double valueDivisor = 60d;
        var axisUnit = global::Tomatotodo_Windows.Data.UiText.T("\u5206\u949F");
        var dataMaximum = series.Count == 0 ? 0 : series.Max(point => point.Seconds) / valueDivisor;
        const int tickIntervals = 4;
        var baselineStep = period switch { "日度" => 10d, "周度" => 30d, "月度" => 60d, "年度" => 120d, _ => 60d };
        var targetMaximum = Math.Max(baselineStep * tickIntervals, dataMaximum * 1.08);
        var tickStep = Math.Max(baselineStep, NiceAxisStep(targetMaximum / tickIntervals));
        var maximum = tickStep * tickIntervals;
        for (var line = 0; line <= tickIntervals; line++)
        {
            var y = top + (height - top - bottom) * line / tickIntervals;
            canvas.Children.Add(new Line { X1 = left, X2 = width - 8, Y1 = y, Y2 = y, Stroke = Brush(ArchiveBorder), StrokeThickness = 1 });
            var value = tickStep * (tickIntervals - line);
            var label = new TextBlock { Text = FormatAxisTick(value), FontSize = 9, Foreground = Brush(ArchiveMuted) };
            Canvas.SetTop(label, y - 7); Canvas.SetLeft(label, 0); canvas.Children.Add(label);
        }
        var plotWidth = width - left - 10;
        var plotHeight = height - top - bottom;
        var plotLayer = new Canvas { Width = width, Height = height,
            RenderTransformOrigin = new Windows.Foundation.Point(.5, 1),
            RenderTransform = new ScaleTransform { ScaleY = 1 } };
        _archiveStatisticsPlotLayer = plotLayer;
        canvas.Children.Add(plotLayer);
        var points = new PointCollection();
        for (var index = 0; index < series.Count; index++)
        {
            var x = left + (series.Count < 2 ? plotWidth / 2 : plotWidth * index / (series.Count - 1));
            var y = top + plotHeight * (1 - Math.Min(maximum, series[index].Seconds / valueDivisor) / maximum);
            points.Add(new Windows.Foundation.Point(x, y));
        }
        if (points.Count > 0)
        {
            var area = new PointCollection { new(left, top + plotHeight) };
            foreach (var point in points) area.Add(point);
            area.Add(new Windows.Foundation.Point(points[^1].X, top + plotHeight));
            plotLayer.Children.Add(new Polygon { Points = area,
                Fill = Brush(ColorHelper.FromArgb(IsDarkAppearance ? (byte)46 : (byte)30,
                    ArchiveMint.R, ArchiveMint.G, ArchiveMint.B)) });
            plotLayer.Children.Add(new Polyline { Points = points, Stroke = Brush(ArchiveMint),
                StrokeThickness = 2.5, StrokeLineJoin = PenLineJoin.Round });
        }
        for (var index = 0; index < series.Count; index++)
        {
            var point = points[index];
            var hit = new Button { Width = 28, Height = 28, Padding = new Thickness(0), Background = Brush(Colors.Transparent), BorderThickness = new Thickness(0) };
            var dot = new Ellipse { Width = 9, Height = 9, Fill = Brush(ArchiveSurface), Stroke = Brush(ArchiveMint), StrokeThickness = 2 };
            hit.Content = dot;
            AttachFastArchiveToolTip(hit, ArchiveDurationToolTip($"{series[index].Start:yyyy-MM-dd HH:mm}", series[index].Seconds));
            _archiveStatisticsMarkers.Add(hit);
            Canvas.SetLeft(hit, point.X - 14); Canvas.SetTop(hit, point.Y - 14); plotLayer.Children.Add(hit);
            if (series.Count <= 12 || index % Math.Max(1, series.Count / 6) == 0 || index == series.Count - 1)
            {
                var label = new TextBlock { Text = series[index].Label, FontSize = 9, Foreground = Brush(ArchiveMuted) };
                Canvas.SetLeft(label, Math.Max(left, point.X - 10)); Canvas.SetTop(label, height - 20); canvas.Children.Add(label);
            }
        }
        var yAxis = new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.F("\u4E13\u6CE8\u65F6\u95F4 / {0}", axisUnit), FontSize = 10, Foreground = Brush(ArchiveMuted) };
        Canvas.SetLeft(yAxis, left); Canvas.SetTop(yAxis, 0); canvas.Children.Add(yAxis);
        return new Viewbox { Child = canvas, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    }

    private Border BuildArchiveHeatmap(IReadOnlyList<FocusLog> logs)
    {
        var content = new Grid { RowSpacing = 10 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var yearSeconds = logs.Where(log => log.StartedAt.LocalDateTime.Year == _archiveHeatmapYear).Sum(log => log.Seconds);
        var paging = BuildArchivePagingControls(global::Tomatotodo_Windows.Data.UiText.F("{0} \u5E74", _archiveHeatmapYear), global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u4E00\u5E74"), global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u4E00\u5E74"),
            () => { _archiveHeatmapYear--; RefreshArchiveHeatmap(); },
            () => { _archiveHeatmapYear++; RefreshArchiveHeatmap(); });
        content.Children.Add(ArchiveHeading(IconGlyph.Grid, global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u70ED\u529B\u56FE"), paging));
        const double cellSize = 13;
        const double cellGap = 3;
        const double heatmapWidth = 53 * cellSize + 52 * cellGap;
        var months = new Grid { Width = heatmapWidth };
        for (var index = 0; index < 12; index++)
        {
            months.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = new TextBlock { Text = (index + 1) + global::Tomatotodo_Windows.Data.UiText.T("\u6708"), FontSize = 9, Foreground = Brush(ArchiveMuted), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(text, index); months.Children.Add(text);
        }
        var matrix = new Grid { ColumnSpacing = cellGap, RowSpacing = cellGap, HorizontalAlignment = HorizontalAlignment.Left };
        for (var column = 0; column < 53; column++) matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellSize) });
        for (var row = 0; row < 7; row++) matrix.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });
        var cells = ArchiveAnalytics.Heatmap(logs, _archiveHeatmapYear);
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            var level = cell.IsInYear ? ArchiveAnalytics.HeatmapLevel(cell.Seconds) : 0;
            var square = new Button { Width = cellSize, Height = cellSize, Padding = new Thickness(0), CornerRadius = new CornerRadius(3),
                Background = Brush(HeatmapColor(level, cell.IsInYear)), BorderBrush = Brush(level == 0 ? ArchiveBorder : Colors.Transparent),
                BorderThickness = new Thickness(level == 0 ? 1 : 0), Opacity = cell.IsInYear ? 1 : .34 };
            var tooltip = ArchiveDurationToolTip(cell.Date.ToString("yyyy-MM-dd"), cell.Seconds);
            AttachFastArchiveToolTip(square, tooltip);
            var restingBorder = square.BorderBrush;
            var restingThickness = square.BorderThickness;
            square.PointerEntered += (_, _) =>
            {
                square.BorderBrush = Brush(ArchiveMint);
                square.BorderThickness = new Thickness(1);
            };
            square.PointerExited += (_, _) =>
            {
                square.BorderBrush = restingBorder;
                square.BorderThickness = restingThickness;
            };
            Grid.SetColumn(square, index / 7); Grid.SetRow(square, index % 7); matrix.Children.Add(square);
        }
        var spotlightBrush = new RadialGradientBrush
        {
            Center = new Windows.Foundation.Point(.5, .5),
            RadiusX = .5,
            RadiusY = .5,
            GradientStops =
            {
                new GradientStop { Color = ColorHelper.FromArgb(48, ArchiveMint.R, ArchiveMint.G, ArchiveMint.B), Offset = 0 },
                new GradientStop { Color = ColorHelper.FromArgb(18, ArchiveMint.R, ArchiveMint.G, ArchiveMint.B), Offset = .48 },
                new GradientStop { Color = ColorHelper.FromArgb(0, ArchiveMint.R, ArchiveMint.G, ArchiveMint.B), Offset = 1 }
            }
        };
        const double spotlightSize = 190;
        var heatArea = new Grid
        {
            Width = heatmapWidth,
            Height = 125,
            Background = Brush(Colors.Transparent),
            Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, heatmapWidth, 125) }
        };
        var spotlightLayer = new Canvas { Width = heatmapWidth, Height = 125, IsHitTestVisible = false };
        var spotlight = new Border { Width = spotlightSize, Height = spotlightSize, Background = spotlightBrush, Opacity = 0, IsHitTestVisible = false };
        spotlightLayer.Children.Add(spotlight);
        heatArea.Children.Add(spotlightLayer);
        matrix.VerticalAlignment = VerticalAlignment.Center;
        heatArea.Children.Add(matrix);
        heatArea.PointerEntered += (_, _) => spotlight.Opacity = 1;
        heatArea.PointerExited += (_, _) => spotlight.Opacity = 0;
        heatArea.PointerMoved += (_, args) =>
        {
            var point = args.GetCurrentPoint(heatArea).Position;
            Canvas.SetLeft(spotlight, point.X - spotlightSize / 2);
            Canvas.SetTop(spotlight, point.Y - spotlightSize / 2);
        };
        var heatmapStack = new StackPanel { Width = heatmapWidth, Spacing = 8 };
        heatmapStack.Children.Add(months);
        heatmapStack.Children.Add(heatArea);
        var heatBody = new StackPanel { Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
        heatBody.Children.Add(new ScrollViewer { Content = heatmapStack, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.F("\u5168\u5E74\u7D2F\u8BA1 {0}", CompactDuration(yearSeconds)), FontSize = 10,
            Foreground = Brush(ArchiveMuted), VerticalAlignment = VerticalAlignment.Center });
        var legend = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 5 };
        legend.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u683C\u4EE3\u8868\u4E00\u5929"), FontSize = 10, Foreground = Brush(ArchiveMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) });
        for (var level = 0; level <= 5; level++) legend.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(2), Background = Brush(HeatmapColor(level, true)) });
        legend.Children.Add(new TextBlock { Text = "250+", FontSize = 9, Foreground = Brush(ArchiveMuted), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(legend, 1); footer.Children.Add(legend);
        heatBody.Children.Add(footer);
        Grid.SetRow(heatBody, 1); content.Children.Add(heatBody);
        return ArchiveCard(content, 386);
    }

    private Windows.UI.Color HeatmapColor(int level, bool inYear)
    {
        if (!inYear) return MixColor(ArchiveSurface, ArchiveBorder, IsDarkAppearance ? 0.28 : 0.18);
        if (level <= 0) return IsDarkAppearance
            ? ColorHelper.FromArgb(255, 61, 61, 61)
            : ColorHelper.FromArgb(255, 232, 232, 232);

        // Keep every seed color on the same five-step tonal curve. The endpoint is
        // nudged toward the theme foreground so saturated or very dark seeds remain legible.
        var endpoint = MixColor(ArchiveMint, IsDarkAppearance ? Colors.White : Colors.Black,
            IsDarkAppearance ? 0.10 : 0.06);
        var amount = level switch { 1 => 0.28, 2 => 0.45, 3 => 0.62, 4 => 0.80, _ => 1.0 };
        return MixColor(IsDarkAppearance ? ArchiveSurfaceStrong : Colors.White, endpoint, amount);
    }

    private static Windows.UI.Color MixColor(Windows.UI.Color from, Windows.UI.Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        static byte Channel(byte start, byte end, double value) =>
            (byte)Math.Round(start + ((end - start) * value));
        return ColorHelper.FromArgb(255, Channel(from.R, to.R, amount), Channel(from.G, to.G, amount),
            Channel(from.B, to.B, amount));
    }

    private FrameworkElement ArchiveHeading(string glyph, string title, FrameworkElement? action = null,
        string? subtitle = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var names = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        names.Children.Add(ArchiveGlyph(glyph, 20));
        var text = new StackPanel { Spacing = 1 };
        var titleText = new TextBlock { Text = title, Foreground = Brush(ArchiveText), FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        text.Children.Add(titleText);
        if (!string.IsNullOrEmpty(subtitle)) text.Children.Add(new TextBlock { Text = subtitle, Foreground = Brush(ArchiveMuted), FontSize = 10 });
        names.Children.Add(text); grid.Children.Add(names);
        if (action is not null) { Grid.SetColumn(action, 1); action.VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(action); }
        return grid;
    }

    private Button ArchiveRoundButton(Symbol symbol, string hint, Action action) 
    {
        var button = new Button { Width = 34, Height = 34, Padding = new Thickness(0), CornerRadius = new CornerRadius(17),
            Background = Brush(ArchiveSurfaceStrong), BorderBrush = Brush(ArchiveBorder), BorderThickness = new Thickness(1), Content = FluentIcon(symbol == Symbol.Previous ? "\uE76B" : "\uE76C", 16) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, hint);
        ToolTipService.SetToolTip(button, hint); button.Click += (_, _) => action(); return button;
    }

    private FrameworkElement BuildArchivePagingControls(string label, string previousHint, string nextHint,
        Action previous, Action next, Action<TextBlock>? labelCreated = null)
    {
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(ArchiveRoundButton(Symbol.Previous, previousHint, previous));
        var labelText = new TextBlock
        {
            Text = label,
            Foreground = Brush(ArchiveText),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MinWidth = 76,
            Margin = new Thickness(5, 0, 5, 0)
        };
        controls.Children.Add(labelText);
        labelCreated?.Invoke(labelText);
        controls.Children.Add(ArchiveRoundButton(Symbol.Next, nextHint, next));
        return controls;
    }

    private Border ArchiveCard(UIElement child, double minHeight = 0) => new()
    {
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16), MinHeight = minHeight, Child = child,
        Shadow = _archiveCardShadow, Translation = new System.Numerics.Vector3(0, 0, 32)
    };

    private ToolTip ArchiveDurationToolTip(string context, int seconds) => new()
    {
        Placement = PlacementMode.Top,
        Content = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = context, FontSize = 11, Foreground = Brush(ArchiveMuted) },
                new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.F("\u4E13\u6CE8\u65F6\u957F\uFF1A{0}", ExactDuration(seconds)), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }
            }
        }
    };

    private static void AttachFastArchiveToolTip(FrameworkElement target, ToolTip tooltip)
    {
        ToolTipService.SetToolTip(target, tooltip);
        target.PointerEntered += (_, _) => tooltip.IsOpen = true;
        target.PointerExited += (_, _) => tooltip.IsOpen = false;
        target.PointerCanceled += (_, _) => tooltip.IsOpen = false;
    }

    private FontIcon ArchiveGlyph(string glyph, double size, bool centered = false) => new()
    {
        Glyph = glyph, FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"], FontSize = size, Foreground = Brush(ArchiveMint),
        HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static SolidColorBrush Brush(Windows.UI.Color color) => new(color);

    private static string CompactDuration(int seconds)
    {
        if (seconds < 60) return global::Tomatotodo_Windows.Data.UiText.F("{0} \u79D2", Math.Max(0, seconds));
        return seconds < 3600 ? global::Tomatotodo_Windows.Data.UiText.F("{0} \u5206\u949F", seconds / 60) : global::Tomatotodo_Windows.Data.UiText.F("{0} \u5C0F\u65F6 {1} \u5206\u949F", seconds / 3600, (seconds % 3600) / 60);
    }

    private static string ExactDuration(int seconds)
    {
        seconds = Math.Max(0, seconds);
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var remainingSeconds = seconds % 60;
        if (hours > 0) return global::Tomatotodo_Windows.Data.UiText.F("{0} \u5C0F\u65F6 {1} \u5206\u949F {2} \u79D2", hours, minutes, remainingSeconds);
        if (minutes > 0) return global::Tomatotodo_Windows.Data.UiText.F("{0} \u5206\u949F {1} \u79D2", minutes, remainingSeconds);
        return global::Tomatotodo_Windows.Data.UiText.F("{0} \u79D2", remainingSeconds);
    }

    private string TaskName(Guid? taskId) => _state.Presets.SelectMany(preset => preset.Tasks)
        .FirstOrDefault(task => task.Id == taskId)?.Title ?? global::Tomatotodo_Windows.Data.UiText.T("\u672A\u5173\u8054\u4EFB\u52A1");

    private string ArchivePeriodLabel() => _archiveStatsPeriod switch
    {
        "日度" => global::Tomatotodo_Windows.Data.UiText.Date(_archiveStatsCursor, "yyyy \u5E74 M \u6708 d \u65E5", "d"),
        "周度" => $"{ArchiveAnalytics.WeekStart(_archiveStatsCursor):M/d} — {ArchiveAnalytics.WeekStart(_archiveStatsCursor).AddDays(6):M/d}",
        "年度" => global::Tomatotodo_Windows.Data.UiText.Date(_archiveStatsCursor, "yyyy \u5E74", "yyyy"), _ => global::Tomatotodo_Windows.Data.UiText.Date(_archiveStatsCursor, "yyyy \u5E74 M \u6708", "Y")
    };

    private void ShiftStats(int direction)
    {
        _archiveStatsCursor = _archiveStatsPeriod switch
        {
            "日度" => _archiveStatsCursor.AddDays(direction), "周度" => _archiveStatsCursor.AddDays(direction * 7),
            "年度" => _archiveStatsCursor.AddYears(direction), _ => _archiveStatsCursor.AddMonths(direction)
        };
    }

    private static double NiceAxisStep(double rawStep)
    {
        if (rawStep <= 0) return 1;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var normalized = rawStep / magnitude;
        var factor = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return factor * magnitude;
    }

    private static string FormatAxisTick(double value)
    {
        var rounded = Math.Round(value);
        return Math.Abs(value - rounded) < .001 ? rounded.ToString("0") : value.ToString("0.##");
    }

    private static string ArchiveCareMessage(DateTime now) => now.Hour switch
    {
        < 5 => global::Tomatotodo_Windows.Data.UiText.T("\u591C\u6DF1\u4E86\uFF0C\u5148\u7167\u987E\u597D\u81EA\u5DF1\uFF1B\u672A\u5B8C\u6210\u7684\u4E8B\uFF0C\u4E5F\u53EF\u4EE5\u7559\u7ED9\u5929\u4EAE\u4EE5\u540E\u3002"),
        < 8 => global::Tomatotodo_Windows.Data.UiText.T("\u65E9\u6668\u5F88\u5B89\u9759\uFF0C\u6162\u6162\u5F00\u59CB\uFF0C\u8BA9\u7B2C\u4E00\u6BB5\u4E13\u6CE8\u4E3A\u4ECA\u5929\u5B9A\u4E0B\u8282\u594F\u3002"),
        < 12 => global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u5348\u601D\u8DEF\u6B63\u6E05\u6670\uFF0C\u7A33\u7A33\u63A8\u8FDB\u773C\u524D\u6700\u91CD\u8981\u7684\u4E00\u4EF6\u4E8B\u3002"),
        < 14 => global::Tomatotodo_Windows.Data.UiText.T("\u4E2D\u5348\u4E86\uFF0C\u8BB0\u5F97\u597D\u597D\u5403\u996D\u548C\u7A0D\u4F5C\u4F11\u606F\uFF0C\u8BA9\u4E13\u6CE8\u4E5F\u6709\u4F59\u5730\u3002"),
        < 18 => global::Tomatotodo_Windows.Data.UiText.T("\u5348\u540E\u5BB9\u6613\u75B2\u60EB\uFF0C\u653E\u6162\u4E00\u70B9\u4E5F\u6CA1\u5173\u7CFB\uFF0C\u4F60\u4ECD\u5728\u8BA4\u771F\u5411\u524D\u3002"),
        < 20 => global::Tomatotodo_Windows.Data.UiText.T("\u508D\u665A\u4E86\uFF0C\u6536\u597D\u4ECA\u5929\u7684\u8FDB\u5C55\uFF0C\u4E5F\u7ED9\u81EA\u5DF1\u7559\u4E00\u70B9\u5598\u606F\u3002"),
        _ => global::Tomatotodo_Windows.Data.UiText.T("\u665A\u4E0A\u9002\u5408\u6C89\u4E0B\u5FC3\uFF0C\u4F46\u522B\u5FD8\u4E86\u4E3A\u7761\u7720\u548C\u660E\u5929\u4FDD\u7559\u80FD\u91CF\u3002")
    };

    private async void ArchiveExport_Click(object sender, RoutedEventArgs e)
    {
        Border? postcard = null;
        try
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = global::Tomatotodo_Windows.Data.UiText.F("Tomatotodo-\u4E13\u6CE8\u6863\u6848-{0}", $"{DateTime.Today:yyyyMMdd}") };
            picker.FileTypeChoices.Add(global::Tomatotodo_Windows.Data.UiText.T("PNG \u56FE\u7247"), new List<string> { ".png" });
            InitializePicker(picker);
            var destination = await picker.PickSaveFileAsync();
            if (destination is null) return;
            postcard = await BuildArchivePostcardAsync();
            Canvas.SetLeft(postcard, -2400); Canvas.SetTop(postcard, -1800);
            RenderCaptureHost.Children.Add(postcard);
            await Task.Delay(80);
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(postcard, 1200, 750);
            var pixels = await bitmap.GetPixelsAsync();
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            var pixelReader = DataReader.FromBuffer(pixels);
            var pixelBytes = new byte[pixels.Length];
            pixelReader.ReadBytes(pixelBytes);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight,
                96, 96, pixelBytes);
            await encoder.FlushAsync();
            stream.Seek(0);
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[(int)stream.Size];
            reader.ReadBytes(bytes);
            await FileIO.WriteBytesAsync(destination, bytes);
        }
        catch (Exception exception)
        {
            await ShowThemedDialogAsync(new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u5BFC\u51FA\u4E13\u6CE8\u660E\u4FE1\u7247"), Content = exception.Message,
                CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u786E\u5B9A"), XamlRoot = XamlRoot });
        }
        finally
        {
            if (postcard is not null) RenderCaptureHost.Children.Remove(postcard);
        }
    }

    private async Task<Border> BuildArchivePostcardAsync()
    {
        var date = DateTime.Today;
        var allLogs = ArchiveLogsWithLiveSegment();
        var today = ArchiveAnalytics.SummaryForDay(allLogs, DateOnly.FromDateTime(date));
        var profile = _accountSession.Current;
        var dark = IsDarkAppearance;
        var text = ArchiveText;
        var muted = ArchiveMuted;
        var accent = ArchiveMint;
        var surface = dark ? ColorHelper.FromArgb(255, 36, 38, 41) : Colors.White;
        var stroke = dark ? ColorHelper.FromArgb(255, 66, 68, 72) : ColorHelper.FromArgb(255, 224, 226, 229);
        var logs = allLogs.Where(log => ArchiveAnalytics.LocalDate(log) == DateOnly.FromDateTime(date))
            .Select(log => (Time: log.StartedAt.LocalDateTime.ToString("HH:mm"), Name: TaskName(log.TaskId), Duration: CompactDuration(log.Seconds))).ToList();
        TextBlock Label(string value, double size, bool secondary = false, bool strong = false) => new()
        {
            Text = value, FontSize = size, Foreground = Brush(secondary ? muted : text),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable"),
            FontWeight = strong ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var root = new Grid { Width = 1200, Height = 750, Padding = new Thickness(48), RowSpacing = 24,
            RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light,
            Background = Brush(dark ? ColorHelper.FromArgb(255, 24, 26, 29) : ColorHelper.FromArgb(255, 248, 249, 250)) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(96) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        var header = new Grid { ColumnSpacing = 16 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var avatar = new PersonPicture { Width = 52, Height = 52, DisplayName = profile?.Nickname ?? global::Tomatotodo_Windows.Data.UiText.T("\u8BBF\u5BA2") };
        if (profile is not null)
        {
            try { avatar.ProfilePicture = await DecodePostcardImageAsync(_accountStorage.Read(profile.Id).Avatar); }
            catch { /* Missing account artwork keeps the native initials fallback. */ }
        }
        header.Children.Add(avatar);
        var identity = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(Label(profile?.Nickname ?? global::Tomatotodo_Windows.Data.UiText.T("\u8BBF\u5BA2"), 22, strong: true));
        identity.Children.Add(Label(global::Tomatotodo_Windows.Data.UiText.T("\u6211\u7684\u4E13\u6CE8\u624B\u8BB0  /  FOCUS JOURNAL"), 12, secondary: true));
        Grid.SetColumn(identity, 1); header.Children.Add(identity);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        var logoBytes = await File.ReadAllBytesAsync(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon-source.png"));
        brand.Children.Add(new Image { Width = 36, Height = 36, Stretch = Stretch.Uniform,
            Source = await DecodePostcardImageAsync(logoBytes) });
        brand.Children.Add(Label("Tomatotodo", 20, strong: true));
        Grid.SetColumn(brand, 2); header.Children.Add(brand);
        root.Children.Add(header);
        var body = new Grid { ColumnSpacing = 36 };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(440) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var focus = new Grid { Padding = new Thickness(32), RowSpacing = 12 };
        focus.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        focus.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        focus.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        focus.Children.Add(Label(global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u65E5\u4E13\u6CE8"), 18, strong: true));
        var hero = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        var duration = Label(CompactDuration(today.Seconds), 72, strong: true);
        duration.Foreground = Brush(accent);
        hero.Children.Add(new Viewbox { Child = duration, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            MaxHeight = 96, HorizontalAlignment = HorizontalAlignment.Left });
        hero.Children.Add(new Border { Width = 40, Height = 3, CornerRadius = new CornerRadius(1.5),
            Background = Brush(accent), HorizontalAlignment = HorizontalAlignment.Left });
        hero.Children.Add(Label(global::Tomatotodo_Windows.Data.UiText.T("\u628A\u65F6\u95F4\uFF0C\u7559\u7ED9\u771F\u6B63\u91CD\u8981\u7684\u4E8B\u3002"), 16, secondary: true));
        Grid.SetRow(hero, 1); focus.Children.Add(hero);
        var care = Label(ArchiveCareMessage(DateTime.Now), 14, secondary: true);
        care.TextWrapping = TextWrapping.Wrap; care.MaxLines = 2;
        Grid.SetRow(care, 2); focus.Children.Add(care);
        body.Children.Add(new Border { Background = Brush(BlendColor(surface, ParseColor(_state.AccentColor), dark ? .10 : .045)),
            CornerRadius = new CornerRadius(12), Child = focus });
        var list = new StackPanel { Spacing = 4 };
        var recordHeading = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        recordHeading.Children.Add(Label(global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u65E5\u8BB0\u5F55"), 20, strong: true));
        var count = Label(global::Tomatotodo_Windows.Data.UiText.F("{0} \u6761", $"{logs.Count:00}"), 13, secondary: true); count.HorizontalAlignment = HorizontalAlignment.Right;
        recordHeading.Children.Add(count); list.Children.Add(recordHeading);
        if (logs.Count == 0)
        {
            var empty = new StackPanel { Spacing = 12, Margin = new Thickness(0, 64, 0, 0) };
            empty.Children.Add(Label(global::Tomatotodo_Windows.Data.UiText.T("\u7559\u4E00\u70B9\u65F6\u95F4\uFF0C\u7ED9\u81EA\u5DF1\u3002"), 24, strong: true));
            empty.Children.Add(Label(global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u4E00\u6B21\u4E13\u6CE8\uFF0C\u4F1A\u6210\u4E3A\u8FD9\u91CC\u7684\u7B2C\u4E00\u7B14\u3002"), 15, secondary: true));
            list.Children.Add(empty);
        }
        foreach (var log in logs.Take(12))
        {
            var row = new Grid { ColumnSpacing = 16, Height = 24 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(Label(log.Time, 14, secondary: true));
            var name = Label(log.Name, 14); Grid.SetColumn(name, 1); row.Children.Add(name);
            var time = Label(log.Duration, 14, secondary: true); Grid.SetColumn(time, 2); row.Children.Add(time);
            list.Children.Add(row);
        }
        if (logs.Count > 12) list.Children.Add(Label(global::Tomatotodo_Windows.Data.UiText.F("\u53E6\u6709 {0} \u6761\u4E13\u6CE8\u8BB0\u5F55", logs.Count - 12), 13, secondary: true));
        Grid.SetColumn(list, 1); body.Children.Add(list);
        Grid.SetRow(body, 1); root.Children.Add(body);
        var metrics = new Grid { ColumnSpacing = 24, BorderBrush = Brush(stroke), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 16, 0, 0) };
        for (var column = 0; column < 3; column++) metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void Metric(int column, string title, string value, string? detail = null)
        {
            var stack = new StackPanel { Spacing = 3 };
            stack.Children.Add(Label(title, 13, secondary: true));
            stack.Children.Add(Label(value, 28, strong: true));
            if (detail is not null) stack.Children.Add(Label(detail, 12, secondary: true));
            Grid.SetColumn(stack, column); metrics.Children.Add(stack);
        }
        Metric(0, global::Tomatotodo_Windows.Data.UiText.T("\u5B8C\u6210\u756A\u8304"), global::Tomatotodo_Windows.Data.UiText.F("{0} \u4E2A", today.Pomodoros));
        Metric(1, global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u65E5\u4EFB\u52A1"), $"{ActivePreset.Tasks.Count(t => t.IsComplete)} / {ActivePreset.Tasks.Count}", ActivePreset.Name);
        Metric(2, global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u6B21\u6570"), global::Tomatotodo_Windows.Data.UiText.F("{0} \u6B21", today.Sessions));
        Grid.SetRow(metrics, 2); root.Children.Add(metrics);
        var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(Label(global::Tomatotodo_Windows.Data.UiText.Date(date, "yyyy \u5E74 MM \u6708 dd \u65E5", "d"), 14, secondary: true));
        var watermark = Label(global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u4E00\u523B\u4E13\u6CE8\uFF0C\u90FD\u7B97\u6570\u3002"), 14, secondary: true);
        Grid.SetColumn(watermark, 1); footer.Children.Add(watermark); Grid.SetRow(footer, 3); root.Children.Add(footer);
        return new Border { Width = 1200, Height = 750, Child = root };
    }

    private static async Task<BitmapImage> DecodePostcardImageAsync(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes);
        using var stream = memory.AsRandomAccessStream();
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }
}
