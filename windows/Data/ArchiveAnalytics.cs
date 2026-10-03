namespace Tomatotodo_Windows.Data;

/// <summary>
/// Pure aggregation used by the native archive page. It accepts only persisted
/// focus logs so the UI never invents demonstration statistics.
/// </summary>
public static class ArchiveAnalytics
{
    public sealed record DaySummary(DateOnly Date, int Seconds, int Pomodoros, int Sessions);
    public sealed record SeriesPoint(DateTime Start, string Label, int Seconds);
    public sealed record HeatmapCell(DateOnly Date, int Seconds, bool IsInYear);

    public static DateOnly LocalDate(FocusLog log) => DateOnly.FromDateTime(log.StartedAt.LocalDateTime.Date);

    public static DaySummary SummaryForDay(IEnumerable<FocusLog> logs, DateOnly day)
    {
        var selected = logs.Where(log => LocalDate(log) == day).ToList();
        return new DaySummary(day, selected.Sum(log => Math.Max(0, log.Seconds)),
            selected.Count(log => log.CompletedPomodoro), selected.Count);
    }

    public static IReadOnlyDictionary<DateOnly, DaySummary> Days(IEnumerable<FocusLog> logs)
    {
        return logs.GroupBy(LocalDate).ToDictionary(group => group.Key,
            group => new DaySummary(group.Key, group.Sum(log => Math.Max(0, log.Seconds)),
                group.Count(log => log.CompletedPomodoro), group.Count()));
    }

    public static IReadOnlyList<DateOnly> MonthCells(DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var mondayOffset = ((int)first.DayOfWeek + 6) % 7;
        var start = DateOnly.FromDateTime(first.AddDays(-mondayOffset));
        return Enumerable.Range(0, 42).Select(start.AddDays).ToList();
    }

    public static DateTime WeekStart(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-offset);
    }

    public static IReadOnlyList<SeriesPoint> Series(IEnumerable<FocusLog> logs, string period, DateTime cursor)
    {
        var all = logs.Where(log => log.Seconds > 0).ToList();
        return period switch
        {
            "日度" => Build(all, cursor.Date, 24, TimeSpan.FromHours(1), point => global::Tomatotodo_Windows.Data.UiText.Date(point, "HH\u65F6", "HH")),
            "周度" => Build(all, WeekStart(cursor), 7, TimeSpan.FromDays(1), point => point.ToString("M/d")),
            "年度" => Months(all, new DateTime(cursor.Year, 1, 1)),
            _ => Build(all, new DateTime(cursor.Year, cursor.Month, 1),
                DateTime.DaysInMonth(cursor.Year, cursor.Month), TimeSpan.FromDays(1), point => point.Day.ToString())
        };
    }

    public static IReadOnlyList<HeatmapCell> Heatmap(IEnumerable<FocusLog> logs, int year)
    {
        var totals = Days(logs);
        var first = new DateTime(year, 1, 1);
        var start = DateOnly.FromDateTime(WeekStart(first));
        return Enumerable.Range(0, 53 * 7).Select(index =>
        {
            var date = start.AddDays(index);
            return new HeatmapCell(date, totals.TryGetValue(date, out var day) ? day.Seconds : 0, date.Year == year);
        }).ToList();
    }

    public static int HeatmapLevel(int seconds)
    {
        var minutes = seconds / 60d;
        return minutes <= 0 ? 0 : minutes <= 50 ? 1 : minutes <= 100 ? 2 : minutes <= 150 ? 3 : minutes <= 200 ? 4 : 5;
    }

    private static IReadOnlyList<SeriesPoint> Build(IEnumerable<FocusLog> logs, DateTime start, int count,
        TimeSpan step, Func<DateTime, string> label)
    {
        var result = new List<SeriesPoint>(count);
        for (var index = 0; index < count; index++)
        {
            var point = start.AddTicks(step.Ticks * index);
            var end = point.Add(step);
            var seconds = logs.Where(log => log.StartedAt.LocalDateTime >= point && log.StartedAt.LocalDateTime < end)
                .Sum(log => log.Seconds);
            result.Add(new SeriesPoint(point, label(point), seconds));
        }
        return result;
    }

    private static IReadOnlyList<SeriesPoint> Months(IEnumerable<FocusLog> logs, DateTime start)
    {
        var result = new List<SeriesPoint>(12);
        for (var month = 0; month < 12; month++)
        {
            var point = start.AddMonths(month);
            var end = point.AddMonths(1);
            result.Add(new SeriesPoint(point, global::Tomatotodo_Windows.Data.UiText.Date(point, "M\u6708", "MMM"), logs
                .Where(log => log.StartedAt.LocalDateTime >= point && log.StartedAt.LocalDateTime < end)
                .Sum(log => log.Seconds)));
        }
        return result;
    }
}
