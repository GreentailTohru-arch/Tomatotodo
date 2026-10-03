using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Tomatotodo_Windows.Data;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private static string[] WeekdayNames => [global::Tomatotodo_Windows.Data.UiText.T("\u5468\u4E00"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u4E8C"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u4E09"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u56DB"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u4E94"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u516D"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u65E5")];
    private string _courseSearch = "";
    private bool _bindingCourseToolbar;
    private double _courseRenderedViewportWidth;

    private void RenderCourses()
    {
        _courseRenderedViewportWidth = PageScroll.ActualWidth;
        var schedule = _state.CourseSchedule;
        _courseWeekIndex = Math.Clamp(_courseWeekIndex, 0, schedule.Weeks.Count - 1);
        var week = schedule.Weeks[_courseWeekIndex];
        var monday = DateOnly.ParseExact(week.Date, "yyyy-MM-dd");
        ConfigureCourseToolbar(schedule);
        var allWeekCourses = week.Events.OrderBy(course => course.Day).ThenBy(course => course.Start).ToList();
        var query = _courseSearch.Trim();
        var visibleCourses = string.IsNullOrEmpty(query) ? allWeekCourses : allWeekCourses
            .Where(course => CourseMatches(course, query)).ToList();
        var layer = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        var shadowReceiver = new Grid { IsHitTestVisible = false };
        layer.Children.Add(shadowReceiver);
        var cardShadow = new ThemeShadow();
        cardShadow.Receivers.Add(shadowReceiver);
        var workspace = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        workspace.Children.Add(BuildCourseSummary(schedule, week, monday, allWeekCourses, cardShadow));
        workspace.Children.Add(BuildCourseWeek(monday, visibleCourses, !string.IsNullOrEmpty(query), cardShadow));
        workspace.Children.Add(new Border { Height = 20 });
        layer.Children.Add(workspace);
        PageBody.Children.Add(layer);
    }

    private void ConfigureCourseToolbar(CourseScheduleData schedule)
    {
        _bindingCourseToolbar = true;
        CourseWeekComboBox.Items.Clear();
        for (var index = 0; index < schedule.Weeks.Count; index++)
        {
            var monday = DateOnly.ParseExact(schedule.Weeks[index].Date, "yyyy-MM-dd");
            CourseWeekComboBox.Items.Add(global::Tomatotodo_Windows.Data.UiText.F("\u7B2C {0} \u5468 \u00B7 {1}", index + 1, $"{monday:M/d}"));
        }
        CourseWeekComboBox.SelectedIndex = _courseWeekIndex;
        CourseSearchBox.Text = _courseSearch;
        CourseSearchBox.ItemsSource = CourseSuggestions(schedule, _courseSearch);
        _bindingCourseToolbar = false;
    }

    private UIElement BuildCourseSummary(CourseScheduleData schedule, CourseWeek week, DateOnly monday,
        IReadOnlyList<CourseEvent> courses, ThemeShadow cardShadow)
    {
        var safeWidth = Math.Max(240, (PageScroll.ActualWidth > 0 ? PageScroll.ActualWidth : 960) - 40);
        // Two equal halves are invariant; narrow windows pan the summary instead
        // of stacking the next-course card below the three metrics.
        var summaryWidth = Math.Max(safeWidth, 1024);
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 16, Width = summaryWidth,
            HorizontalAlignment = HorizontalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var teachingDays = courses.Select(course => course.Day).Distinct().Count();
        var totalPeriods = courses.Sum(course => course.End - course.Start + 1);
        var metrics = new Grid { ColumnSpacing = 12 };
        for (var index = 0; index < 3; index++)
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 160 });
        var statCards = new[]
        {
            CourseMetric(IconGlyph.Education, global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u6570\u91CF"), global::Tomatotodo_Windows.Data.UiText.F("{0} \u95E8", courses.Count), cardShadow),
            CourseMetric(IconGlyph.Clock, global::Tomatotodo_Windows.Data.UiText.T("\u5360\u7528\u8282\u6B21"), global::Tomatotodo_Windows.Data.UiText.F("{0} \u8282", totalPeriods), cardShadow),
            CourseMetric(IconGlyph.CalendarDay, global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u8BFE\u65E5"), global::Tomatotodo_Windows.Data.UiText.F("{0} \u5929", teachingDays), cardShadow)
        };
        for (var index = 0; index < statCards.Length; index++)
        {
            Grid.SetColumn(statCards[index], index);
            metrics.Children.Add(statCards[index]);
        }
        grid.Children.Add(metrics);
        var next = NextCourse(schedule);
        var nextCard = new Grid { MinHeight = 128 };
        nextCard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        nextCard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        nextCard.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u63A5\u4E0B\u6765\u8BFE\u7A0B"), FontSize = 12,
            Foreground = CourseBrush(CourseMuted), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        if (next is null)
        {
            var empty = new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u672C\u5468\u6CA1\u6709\u5F85\u4E0A\u7684\u8BFE\u7A0B"), FontSize = 17, Foreground = CourseBrush(CourseText), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(empty, 1); nextCard.Children.Add(empty);
        }
        else
        {
            var nextCourse = next.Value;
            var button = new Button { Padding = new Thickness(0), MinWidth = 0,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
            button.Content = NextCourseTile(nextCourse.Course, nextCourse.Date);
            button.Click += async (_, _) => await ShowCourseDetail(nextCourse.Course, nextCourse.Date);
            Grid.SetRow(button, 1); nextCard.Children.Add(button);
        }
        var summary = CourseCard(nextCard, cardShadow);
        summary.MinHeight = 160;
        Grid.SetColumn(summary, 1);
        grid.Children.Add(summary);
        if (safeWidth >= summaryWidth) return grid;
        grid.HorizontalAlignment = HorizontalAlignment.Left;
        return new ScrollViewer
        {
            Width = safeWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = grid
        };
    }

    private Border CourseMetric(string glyph, string label, string value, ThemeShadow cardShadow)
    {
        var card = new Grid { MinHeight = 128 };
        card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        card.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        card.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = CourseBrush(CourseMuted), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 13, VerticalAlignment = VerticalAlignment.Bottom };
        line.Children.Add(FluentIcon(glyph, 24, CourseBrush(CourseMint)));
        line.Children.Add(new TextBlock { Text = value, FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = CourseBrush(CourseText), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetRow(line, 1); card.Children.Add(line);
        var surface = CourseCard(card, cardShadow);
        surface.MinHeight = 160;
        return surface;
    }

    private UIElement BuildCourseWeek(DateOnly monday, IReadOnlyList<CourseEvent> courses, bool searching,
        ThemeShadow cardShadow)
    {
        var viewportWidth = PageScroll.ActualWidth > 0 ? PageScroll.ActualWidth : 960;
        // Every weekday keeps the same usable width. Seven columns fill the complete course
        // workspace at normal desktop sizes; only genuinely narrow windows scroll horizontally.
        const double minimumDayWidth = 128;
        const double dayGap = 0;
        var minimumTimetableWidth = minimumDayWidth * 7 + dayGap * 6;
        // The outer page already supplies its own 24 epx safety margin. Keep a further 20 epx
        // inset at both ends so Monday and Sunday are fully visible and never sit under a clip.
        var cardWidth = Math.Max(1, viewportWidth - 40);
        var timetableWidth = Math.Max(minimumTimetableWidth, cardWidth - 2);
        var scroll = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        var grid = new Grid
        {
            ColumnSpacing = dayGap,
            Width = timetableWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < 7; index++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = minimumDayWidth });
        // Use one stable three-course capacity for every day. Quiet days no longer collapse,
        // while days with more than three courses remain reachable through their inner scroller.
        // Header, date and three complete 96 epx course tiles must all fit without exposing
        // a vertical scrollbar. Keep a little lower breathing room for WinUI focus visuals.
        const double weekdayCardHeight = 424;
        for (var day = 0; day < 7; day++)
        {
            var date = monday.AddDays(day);
            var dayCourses = courses.Where(course => course.Day == day).OrderBy(course => course.Start).ToList();
            var column = new Grid();
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var head = new Grid { ColumnSpacing = 8 };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = WeekdayNames[day], FontSize = 16, Foreground = CourseBrush(CourseText), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var count = new TextBlock { Text = dayCourses.Count == 0 ? global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u8BFE\u7A0B") : global::Tomatotodo_Windows.Data.UiText.F("{0} \u95E8", dayCourses.Count), FontSize = 10, Foreground = CourseBrush(CourseMuted), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(count, 1); head.Children.Add(count); column.Children.Add(head);
            var dateText = new TextBlock { Text = date.ToString("M/d") + (date == DateOnly.FromDateTime(DateTime.Today) ? global::Tomatotodo_Windows.Data.UiText.T(" \u00B7 \u4ECA\u5929") : ""), FontSize = 11, Foreground = CourseBrush(CourseMuted), Margin = new Thickness(0, 3, 0, 10) };
            Grid.SetRow(dateText, 1); column.Children.Add(dateText);
            if (dayCourses.Count == 0)
            {
                var empty = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                empty.Children.Add(new FontIcon { Glyph = IconGlyph.CalendarDay, FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"], FontSize = 32, Foreground = CourseBrush(CourseMint), HorizontalAlignment = HorizontalAlignment.Center });
                empty.Children.Add(new TextBlock { Text = searching ? global::Tomatotodo_Windows.Data.UiText.T("\u672A\u627E\u5230\u5339\u914D\u8BFE\u7A0B") : global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u5929\u6CA1\u6709\u5B89\u6392\u8BFE\u7A0B"), FontSize = 11, Foreground = CourseBrush(CourseMuted), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
                Grid.SetRow(empty, 2); column.Children.Add(empty);
            }
            else
            {
                var list = new StackPanel { Spacing = 8 };
                foreach (var course in dayCourses)
                {
                    var button = new Button { Padding = new Thickness(0), MinWidth = 0,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
                    button.Content = CourseTile(course, date);
                    button.Click += async (_, _) => await ShowCourseDetail(course, date);
                    list.Children.Add(button);
                }
                var listScroll = new ScrollViewer { Content = list,
                    VerticalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                    HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                Grid.SetRow(listScroll, 2); column.Children.Add(listScroll);
            }
            // Weekdays are cells in one continuous timetable, not separate cards.
            var cell = new Border
            {
                Child = column,
                Padding = new Thickness(12),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(day == 0 ? 0 : 1, 0, 0, 0),
                Height = weekdayCardHeight
            };
            if (date == DateOnly.FromDateTime(DateTime.Today))
                head.Children.OfType<TextBlock>().First().Foreground = CourseBrush(CourseMint);
            // A shared-height header and divider make all date columns line up.
            column.RowDefinitions[0].Height = new GridLength(28);
            column.RowDefinitions[1].Height = new GridLength(32);
            var divider = new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(-12, 0, -12, 8), IsHitTestVisible = false
            };
            Grid.SetRow(divider, 1); column.Children.Add(divider);
            Grid.SetColumn(cell, day); grid.Children.Add(cell);
        }
        // Constrain the outer card to the same boundaries as the summary.
        // Only its contents scroll; oversize columns must not move the outer frame.
        scroll.Content = grid;
        var timetableCard = CourseCard(scroll, cardShadow);
        timetableCard.Width = cardWidth;
        timetableCard.HorizontalAlignment = HorizontalAlignment.Center;
        timetableCard.Padding = new Thickness(0);
        return timetableCard;
    }

    private Border CourseTile(CourseEvent course, DateOnly date)
    {
        var tone = CourseTone(course.Tone);
        var body = new StackPanel { Spacing = 8, Padding = new Thickness(12) };
        body.Children.Add(AutoScrollingCourseLine(IconLabel(IconGlyph.Clock,
            global::Tomatotodo_Windows.Data.UiText.F("{0}-{1}\u8282  \u00B7  {2}", course.Start, course.End, CourseTime(course)), 11, CourseBrush(tone))));
        body.Children.Add(AutoScrollingCourseLine(new TextBlock
        {
            Text = course.Name, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = CourseBrush(CourseText), TextWrapping = TextWrapping.NoWrap
        }));
        var details = new Grid { ColumnSpacing = 12 };
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        details.Children.Add(IconLabel(IconGlyph.Location,
            string.IsNullOrWhiteSpace(course.Room) ? global::Tomatotodo_Windows.Data.UiText.T("\u6559\u5BA4\u5F85\u5B9A") : course.Room, 11, CourseBrush(CourseMuted)));
        var teacher = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(course.Teacher) ? global::Tomatotodo_Windows.Data.UiText.T("\u6559\u5E08\u5F85\u5B9A") : course.Teacher,
            FontSize = 11, Foreground = CourseBrush(CourseMuted), TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(teacher, 1);
        details.Children.Add(teacher);
        body.Children.Add(AutoScrollingCourseLine(details));
        return new Border { Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = CourseBrush(tone), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Height = 96, Child = body };
    }

    private static ScrollViewer AutoScrollingCourseLine(UIElement content)
    {
        var scroll = new ScrollViewer
        {
            Content = content,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Microsoft.UI.Dispatching.DispatcherQueueTimer? timer = null;
        var pauseTicks = 34;
        scroll.Loaded += (_, _) =>
        {
            timer ??= scroll.DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(30);
            timer.IsRepeating = true;
            timer.Tick += (_, _) =>
            {
                if (scroll.ScrollableWidth <= 0.5)
                {
                    if (scroll.HorizontalOffset > 0) scroll.ChangeView(0, null, null, true);
                    pauseTicks = 34;
                    return;
                }
                if (pauseTicks-- > 0) return;
                if (scroll.HorizontalOffset >= scroll.ScrollableWidth - 0.75)
                {
                    scroll.ChangeView(0, null, null, false);
                    pauseTicks = 48;
                    return;
                }
                scroll.ChangeView(Math.Min(scroll.ScrollableWidth, scroll.HorizontalOffset + 0.65),
                    null, null, true);
            };
            timer.Start();
        };
        scroll.Unloaded += (_, _) =>
        {
            timer?.Stop();
            timer = null;
        };
        return scroll;
    }

    private Border NextCourseTile(CourseEvent course, DateOnly date)
    {
        var tone = CourseTone(course.Tone);
        var body = new StackPanel { Spacing = 7, Padding = new Thickness(12, 9, 12, 9) };
        body.Children.Add(new TextBlock
        {
            Text = course.Name, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = CourseBrush(CourseText), TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None
        });
        var information = new Grid { ColumnSpacing = 24, RowSpacing = 5 };
        information.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        information.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        information.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        information.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var dateLine = IconLabel(IconGlyph.CalendarDay, $"{date:M/d}  ·  {WeekdayNames[course.Day]}", 11, CourseBrush(tone));
        information.Children.Add(dateLine);
        var timeLine = IconLabel(IconGlyph.Clock, global::Tomatotodo_Windows.Data.UiText.F("{0}  \u00B7  {1}-{2}\u8282  \u00B7  {3}", CoursePart(course), course.Start, course.End, CourseTime(course)), 11, CourseBrush(tone));
        Grid.SetColumn(timeLine, 1);
        information.Children.Add(timeLine);
        var roomLine = IconLabel(IconGlyph.Location, string.IsNullOrWhiteSpace(course.Room) ? global::Tomatotodo_Windows.Data.UiText.T("\u6559\u5BA4\u5F85\u5B9A") : course.Room, 11, CourseBrush(CourseMuted));
        Grid.SetRow(roomLine, 1);
        information.Children.Add(roomLine);
        var teacherLine = new TextBlock
        {
            Text = global::Tomatotodo_Windows.Data.UiText.F("\u6559\u5E08  \u00B7  {0}", (string.IsNullOrWhiteSpace(course.Teacher) ? "待定" : course.Teacher)),
            FontSize = 11, Foreground = CourseBrush(CourseMuted), TextWrapping = TextWrapping.NoWrap
        };
        Grid.SetRow(teacherLine, 1);
        Grid.SetColumn(teacherLine, 1);
        information.Children.Add(teacherLine);
        body.Children.Add(information);
        return new Border
        {
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = CourseBrush(tone), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = body
        };
    }

    private async Task ShowCourseDetail(CourseEvent course, DateOnly date)
    {
        var details = new StackPanel { Width = 360, Spacing = 12 };
        details.Children.Add(new TextBlock { Text = $"{WeekdayNames[course.Day]} · {CoursePart(course)}", Foreground = CourseBrush(CourseMint), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        details.Children.Add(new TextBlock { Text = course.Name, FontSize = 25, TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        foreach (var (label, value) in new[]
        {
            (global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u8BFE\u65F6\u95F4"), global::Tomatotodo_Windows.Data.UiText.F("{0}-{1}\u8282 \u00B7 {2}", course.Start, course.End, CourseTime(course))), (global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u8BFE\u65E5\u671F"), $"{date:yyyy年M月d日} · {WeekdayNames[course.Day]}"),
            (global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u8BFE\u6559\u5BA4"), string.IsNullOrWhiteSpace(course.Room) ? global::Tomatotodo_Windows.Data.UiText.T("\u5F85\u5B9A") : course.Room), (global::Tomatotodo_Windows.Data.UiText.T("\u4EFB\u8BFE\u6559\u5E08"), string.IsNullOrWhiteSpace(course.Teacher) ? global::Tomatotodo_Windows.Data.UiText.T("\u5F85\u5B9A") : course.Teacher)
        })
        {
            var row = new Grid { Padding = new Thickness(0, 9, 0, 9), ColumnSpacing = 12,
                BorderBrush = CourseBrush(CourseBorder), BorderThickness = new Thickness(0, 0, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = CourseBrush(CourseMuted) });
            var valueText = new TextBlock { Text = value, FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(valueText, 1); row.Children.Add(valueText); details.Children.Add(row);
        }
        await ShowThemedDialogAsync(new ContentDialog { Content = details, CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u5173\u95ED"), XamlRoot = XamlRoot });
    }

    private void CourseSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_bindingCourseToolbar || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        sender.ItemsSource = CourseSuggestions(_state.CourseSchedule, sender.Text);
    }

    private void CourseSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        _courseSearch = args.SelectedItem?.ToString() ?? sender.Text;
        Render();
    }

    private void CourseSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _courseSearch = args.ChosenSuggestion?.ToString() ?? args.QueryText;
        Render();
    }

    private void CourseWeekComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_bindingCourseToolbar || CourseWeekComboBox.SelectedIndex < 0) return;
        _courseWeekIndex = CourseWeekComboBox.SelectedIndex;
        Render();
    }

    private IEnumerable<string> CourseSuggestions(CourseScheduleData schedule, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        return schedule.Weeks.SelectMany(week => week.Events).Where(course => CourseMatches(course, query)).Select(course => course.Name).Distinct().Take(6).ToList();
    }

    private static bool CourseMatches(CourseEvent course, string query) => course.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        course.Room.Contains(query, StringComparison.CurrentCultureIgnoreCase) || course.Teacher.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    private (CourseEvent Course, DateOnly Date)? NextCourse(CourseScheduleData schedule)
    {
        var now = DateTime.Now;
        return schedule.Weeks.SelectMany(week => week.Events.Select(course => new { Course = course, Date = DateOnly.ParseExact(week.Date, "yyyy-MM-dd").AddDays(course.Day) }))
            .Select(item => new { item.Course, item.Date, Starts = CourseDateTime(item.Date, item.Course) })
            .Where(item => item.Starts >= now).OrderBy(item => item.Starts).Select(item => (item.Course, item.Date)).Cast<(CourseEvent Course, DateOnly Date)?>().FirstOrDefault();
    }

    private static DateTime CourseDateTime(DateOnly date, CourseEvent course) => TimeOnly.TryParse(course.StartTime, out var time) ? date.ToDateTime(time) : date.ToDateTime(TimeOnly.MinValue);
    private static string CourseTime(CourseEvent course) => string.IsNullOrWhiteSpace(course.StartTime) ? global::Tomatotodo_Windows.Data.UiText.T("\u65F6\u95F4\u5F85\u5B9A") : $"{course.StartTime}–{course.EndTime}";
    private static string CoursePart(CourseEvent course) => !string.IsNullOrWhiteSpace(course.PeriodPart) ? course.PeriodPart : course.StartTime is { Length: > 0 } && int.TryParse(course.StartTime[..2], out var hour) ? hour < 12 ? global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u5348") : hour < 18 ? global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u5348") : global::Tomatotodo_Windows.Data.UiText.T("\u665A\u4E0A") : global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B");

    private Windows.UI.Color CourseSurface => IsDarkAppearance ? ColorHelper.FromArgb(255, 45, 45, 45) : Colors.White;
    private Windows.UI.Color CourseBorder => IsDarkAppearance ? ColorHelper.FromArgb(255, 65, 65, 65) : ColorHelper.FromArgb(255, 222, 222, 222);
    private Windows.UI.Color CourseMint => AccentDisplayColor;
    private Windows.UI.Color CourseText => TextPrimary;
    private Windows.UI.Color CourseMuted => TextMuted;
    private static SolidColorBrush CourseBrush(Windows.UI.Color color) => new(color);
    private Windows.UI.Color CourseTone(string tone)
    {
        var color = tone switch
        {
            "violet" => ColorHelper.FromArgb(255, 150, 126, 255), "blue" => ColorHelper.FromArgb(255, 107, 178, 255),
            "coral" => ColorHelper.FromArgb(255, 255, 143, 120), "amber" => ColorHelper.FromArgb(255, 239, 193, 93),
            "green" => ColorHelper.FromArgb(255, 139, 218, 133), "rose" => ColorHelper.FromArgb(255, 239, 130, 163), _ => ColorHelper.FromArgb(255, 104, 221, 204)
        };
        var target = IsDarkAppearance ? Colors.White : Colors.Black;
        for (var step = 0; step < 12 && ContrastRatio(color, CourseSurface) < 4.5; step++)
            color = BlendColor(color, target, .12);
        return color;
    }
    private Border CourseCard(UIElement child, ThemeShadow cardShadow, bool selected = false) => new()
    {
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = selected ? CourseBrush(AccentDisplayColor)
            : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Stretch,
        Shadow = cardShadow, Translation = new System.Numerics.Vector3(0, 0, 32), Child = child
    };

    private void RenderDashboardCourses()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var week = CourseScheduleCodec.WeekForDate(_state.CourseSchedule, today);
        var day = ((int)today.DayOfWeek + 6) % 7;
        var courses = week?.Events.Where(course => course.Day == day)
            .OrderBy(course => course.StartTime).ToList() ?? [];
        var card = Card(global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u65E5\u8BFE\u7A0B"));
        if (courses.Count == 0) card.Children.Add(Muted(global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u5929\u6CA1\u6709\u8BFE\u7A0B\u3002")));
        foreach (var course in courses)
            card.Children.Add(new TextBlock { Text = $"{(string.IsNullOrEmpty(course.StartTime) ? "时间待定" : course.StartTime)}  {course.Name}" +
                (string.IsNullOrWhiteSpace(course.Room) ? "" : $" · {course.Room}"),
                TextWrapping = TextWrapping.Wrap });
        PageBody.Children.Add(card);
    }

    private async Task ImportCourseSchedule()
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".json");
            InitializePicker(picker);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            var imported = CourseScheduleCodec.Parse(await FileIO.ReadTextAsync(file));
            var count = imported.Weeks.Sum(week => week.Events.Count);
            var confirmation = new ContentDialog
            {
                Title = global::Tomatotodo_Windows.Data.UiText.T("\u8986\u76D6\u5F53\u524D\u8BFE\u7A0B\u8868\uFF1F"),
                Content = global::Tomatotodo_Windows.Data.UiText.F("\u5C06\u5BFC\u5165\u201C{0}\u201D\uFF0C\u5171 {1} \u5468\u3001{2} \u4E2A\u8BFE\u7A0B\u65F6\u6BB5\u3002\u5F53\u524D\u8BFE\u7A0B\u8868\u4F1A\u88AB\u66FF\u6362\uFF0C\u5176\u4ED6\u4EFB\u52A1\u548C\u4E13\u6CE8\u8BB0\u5F55\u4E0D\u53D7\u5F71\u54CD\u3002", imported.Term.Name, imported.Weeks.Count, count),
                PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u8986\u76D6\u5E76\u5BFC\u5165"),
                CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"),
                XamlRoot = XamlRoot
            };
            StyleDestructiveDialog(confirmation);
            if (await ShowThemedDialogAsync(confirmation) != ContentDialogResult.Primary) return;
            _state.CourseSchedule = imported;
            var current = CourseScheduleCodec.WeekForDate(imported, DateOnly.FromDateTime(DateTime.Today));
            _courseWeekIndex = Math.Max(0, imported.Weeks.FindIndex(week => week.Date == current?.Date));
            Save();
            Render();
        }
        catch (Exception exception)
        {
            await ShowCourseError(global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u5BFC\u5165\u8BFE\u7A0B\u8868"), exception.Message);
        }
    }

    private async Task ExportCourseSchedule()
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = global::Tomatotodo_Windows.Data.UiText.T("Tomatotodo-\u8BFE\u7A0B\u8868")
            };
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            InitializePicker(picker);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await FileIO.WriteTextAsync(file, CourseScheduleCodec.Export(_state.CourseSchedule));
        }
        catch (Exception exception)
        {
            await ShowCourseError(global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u5BFC\u51FA\u8BFE\u7A0B\u8868"), exception.Message);
        }
    }

    private static void InitializePicker(object picker)
    {
        var window = ((App)Application.Current).MainWindowInstance
            ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u4E3B\u7A97\u53E3\u5C1A\u672A\u5EFA\u7ACB\u3002"));
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
    }

    private async Task ShowCourseError(string title, string message)
    {
        await ShowThemedDialogAsync(new ContentDialog { Title = title, Content = message, CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u786E\u5B9A"),
            XamlRoot = XamlRoot });
    }
}
