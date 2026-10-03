namespace Tomatotodo_Windows.Data;

public static class PresetSchedule
{
    public static DateTimeOffset? Next(DateTimeOffset date, TaskPreset preset)
    {
        var interval = Math.Clamp(preset.RepeatInterval, 1, 365);
        return preset.Repeat switch
        {
            "daily" => date.AddDays(1),
            "workday" => NextWorkday(date),
            "weekly" => date.AddDays(7),
            "monthly" => date.AddMonths(1),
            "yearly" => date.AddYears(1),
            "custom" when preset.RepeatUnit == "day" => date.AddDays(interval),
            "custom" when preset.RepeatUnit == "week" => NextCustomWeek(date, interval, preset.RepeatDays),
            "custom" when preset.RepeatUnit == "month" => date.AddMonths(interval),
            "custom" when preset.RepeatUnit == "year" => date.AddYears(interval),
            _ => null
        };
    }

    private static DateTimeOffset NextWorkday(DateTimeOffset date)
    {
        do date = date.AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        return date;
    }

    private static DateTimeOffset NextCustomWeek(DateTimeOffset date, int interval, List<DayOfWeek> days)
    {
        if (days.Count == 0) return date.AddDays(7 * interval);
        var monday = date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7);
        for (var offset = 1; offset <= 7 * interval + 7; offset++)
        {
            var candidate = date.AddDays(offset);
            var week = (int)((candidate.Date - monday).TotalDays / 7);
            if (week % interval == 0 && days.Contains(candidate.DayOfWeek)) return candidate;
        }
        return date.AddDays(7 * interval);
    }
}
