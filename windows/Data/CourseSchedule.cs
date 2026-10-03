using System.Globalization;
using System.Text.Json;

namespace Tomatotodo_Windows.Data;

public sealed class CourseTerm
{
    public string Name { get; set; } = global::Tomatotodo_Windows.Data.UiText.T("\u6211\u7684\u8BFE\u7A0B\u8868");
    public string Timezone { get; set; } = "Asia/Shanghai";
}

public sealed class CourseEvent
{
    public int Day { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string Name { get; set; } = "";
    public string Room { get; set; } = "";
    public string Teacher { get; set; } = "";
    public string Tone { get; set; } = "teal";
    public string PeriodPart { get; set; } = "";
}

public sealed class CourseWeek
{
    public string Date { get; set; } = "";
    public List<CourseEvent> Events { get; set; } = [];
}

public sealed class CourseScheduleData
{
    public string Format { get; set; } = CourseScheduleCodec.Format;
    public int Version { get; set; } = 1;
    public CourseTerm Term { get; set; } = new();
    public List<CourseWeek> Weeks { get; set; } = [];
}

public static class CourseScheduleCodec
{
    public const string Format = "tomatotodo-course-schedule";
    private static readonly HashSet<string> Tones =
        ["violet", "blue", "teal", "coral", "amber", "green", "rose"];
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static CourseScheduleData EmptyTerm()
    {
        var firstMonday = new DateOnly(2026, 9, 7);
        return new CourseScheduleData
        {
            Term = new CourseTerm { Name = global::Tomatotodo_Windows.Data.UiText.T("\u5927\u4E09\u4E0A\u5B66\u671F") },
            Weeks = Enumerable.Range(0, 16)
                .Select(index => new CourseWeek { Date = firstMonday.AddDays(index * 7).ToString("yyyy-MM-dd") })
                .ToList()
        };
    }

    public static CourseScheduleData Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868 JSON \u9876\u5C42\u5FC5\u987B\u662F\u5BF9\u8C61\u3002"));
        if (!root.TryGetProperty("weeks", out _) && root.TryGetProperty("data", out var nested))
            root = nested;
        var value = root.Deserialize<CourseScheduleData>(Options)
            ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868 JSON \u5185\u5BB9\u4E3A\u7A7A\u3002"));
        Validate(value);
        value.Weeks = value.Weeks.OrderBy(week => week.Date, StringComparer.Ordinal).ToList();
        foreach (var week in value.Weeks)
            week.Events = week.Events.OrderBy(course => course.Day).ThenBy(course => course.Start)
                .ThenBy(course => course.Name, StringComparer.Ordinal).ToList();
        return value;
    }

    public static string Export(CourseScheduleData schedule)
    {
        Validate(schedule);
        return JsonSerializer.Serialize(schedule, Options);
    }

    public static void Validate(CourseScheduleData value)
    {
        if (value.Format != Format) throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8FD9\u4E0D\u662F Tomatotodo \u8BFE\u7A0B\u8868 JSON\u3002"));
        if (value.Version != 1) throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.F("\u4E0D\u652F\u6301\u8BFE\u7A0B\u8868\u7248\u672C {0}\u3002", value.Version));
        if (value.Term is null || string.IsNullOrWhiteSpace(value.Term.Name) || value.Term.Name.Length > 80)
            throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u5B66\u671F\u540D\u79F0\u65E0\u6548\u3002"));
        if (value.Weeks is null || value.Weeks.Count is < 1 or > 64)
            throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868\u5FC5\u987B\u5305\u542B 1 \u5230 64 \u5468\u3002"));

        var dates = new HashSet<DateOnly>();
        foreach (var week in value.Weeks)
        {
            if (!DateOnly.TryParseExact(week.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var monday) || monday.DayOfWeek != DayOfWeek.Monday)
                throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u5468\u65E5\u671F\u5FC5\u987B\u662F YYYY-MM-DD \u683C\u5F0F\u7684\u5468\u4E00\u3002"));
            if (!dates.Add(monday)) throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.F("\u8BFE\u7A0B\u8868\u4E2D\u91CD\u590D\u4E86 {0}\u3002", week.Date));
            if (week.Events is null || week.Events.Count > 80)
                throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.F("{0} \u7684\u8BFE\u7A0B\u6570\u91CF\u65E0\u6548\u3002", week.Date));
            foreach (var course in week.Events)
            {
                if (course is null) throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.F("{0} \u5B58\u5728\u7A7A\u8BFE\u7A0B\u6761\u76EE\u3002", week.Date));
                if (course.Day is < 0 or > 6 || course.Start < 1 || course.End < course.Start || course.End > 99)
                    throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.F("{0} \u5B58\u5728\u65E0\u6548\u7684\u661F\u671F\u6216\u8282\u6B21\u3002", week.Date));
                if (string.IsNullOrWhiteSpace(course.Name) || course.Name.Length > 120)
                    throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.F("{0} \u5B58\u5728\u65E0\u6548\u8BFE\u7A0B\u540D\u79F0\u3002", week.Date));
                var hasStart = TimeOnly.TryParseExact(course.StartTime, "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var start);
                var hasEnd = TimeOnly.TryParseExact(course.EndTime, "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var end);
                if (!((hasStart && hasEnd && end > start) ||
                      (string.IsNullOrEmpty(course.StartTime) && string.IsNullOrEmpty(course.EndTime))))
                    throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.F("{0} \u7684\u5F00\u59CB\u6216\u7ED3\u675F\u65F6\u95F4\u65E0\u6548\uFF0C\u8BF7\u4F7F\u7528 HH:mm\u3002", course.Name));
                if (course.Room is null || course.Room.Length > 80 ||
                    course.Teacher is null || course.Teacher.Length > 80 ||
                    course.PeriodPart is null || course.PeriodPart.Length > 24 ||
                    course.Tone is null || !Tones.Contains(course.Tone))
                    throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.F("{0} \u7684\u6559\u5BA4\u3001\u6559\u5E08\u6216\u8272\u5F69\u5B57\u6BB5\u65E0\u6548\u3002", course.Name));
            }
        }
    }

    public static CourseWeek? WeekForDate(CourseScheduleData schedule, DateOnly date)
    {
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        return schedule.Weeks.FirstOrDefault(week => week.Date == monday.ToString("yyyy-MM-dd"));
    }
}
