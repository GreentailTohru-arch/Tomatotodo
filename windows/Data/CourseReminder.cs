using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Tomatotodo_Windows.Data;

public sealed record DueCourseReminder(string Key, CourseEvent Course, DateTime StartsAt);

public static class CourseReminder
{
    public static IEnumerable<DueCourseReminder> Due(CourseScheduleData schedule, DateTimeOffset now,
        int leadMinutes, IReadOnlyDictionary<string, DateTimeOffset> delivered)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.Term.Timezone); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException) { zone = TimeZoneInfo.Local; }
        var localNow = TimeZoneInfo.ConvertTime(now, zone).DateTime;
        foreach (var week in schedule.Weeks)
        {
            if (!DateOnly.TryParseExact(week.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var monday)) continue;
            foreach (var course in week.Events)
            {
                if (!TimeOnly.TryParseExact(course.StartTime, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var time)) continue;
                var starts = monday.AddDays(course.Day).ToDateTime(time);
                if (localNow < starts.AddMinutes(-Math.Clamp(leadMinutes, 1, 120)) || localNow >= starts) continue;
                var identity = $"{starts:O}\n{course.Name}\n{course.Room}\n{course.Teacher}\n{course.End}";
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                if (!delivered.ContainsKey(key)) yield return new(key, course, starts);
            }
        }
    }
}
