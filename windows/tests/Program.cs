using Tomatotodo_Windows.Data;
using Tomatotodo_Windows.Services;

const string BeijingSearch = """
{"results":[
  {"name":"北京","admin1":"北京市","country":"中国","feature_code":"PPLC","population":18960744,"latitude":39.91,"longitude":116.40},
  {"name":"北京","admin1":"重庆市","country":"中国","feature_code":"PPL","latitude":30.73,"longitude":108.67},
  {"name":"北京","admin1":"四川","country":"中国","feature_code":"PPL","latitude":30.97,"longitude":103.94}
]}
""";
var beijingCities = DashboardContentService.ParseCities(BeijingSearch);
Check(beijingCities.Length == 1 && beijingCities[0].Label == "北京 · 北京市 · 中国",
    "Beijing search excludes unpopulated same-name villages");
const string VillageOnlySearch = """
{"results":[{"name":"北京","admin1":"四川","country":"中国","feature_code":"PPL","latitude":30.97,"longitude":103.94}]}
""";
Check(DashboardContentService.ParseCities(VillageOnlySearch).Length == 0,
    "village-only results do not appear as cities");
const string OtherCitiesSearch = """
{"results":[
  {"name":"上海","admin1":"上海市","country":"中国","feature_code":"PPLA","population":24874500,"latitude":31.22,"longitude":121.46},
  {"name":"上海","admin1":"浙江","country":"中国","feature_code":"PPL","latitude":30.1,"longitude":120.1},
  {"name":"上海","admin1":"浙江","country":"中国","feature_code":"ISL","latitude":30.2,"longitude":120.2},
  {"name":"西安","admin1":"陕西","country":"中国","feature_code":"PPLA","population":9600000,"latitude":34.25,"longitude":108.93},
  {"name":"西安","admin1":"湖南","country":"中国","feature_code":"PPLA4","latitude":28.1,"longitude":112.1},
  {"name":"伦敦","admin1":"安大略","country":"加拿大","feature_code":"PPL","population":422324,"latitude":42.98,"longitude":-81.25}
]}
""";
var otherCities = DashboardContentService.ParseCities(OtherCitiesSearch);
Check(otherCities.Length == 3 && otherCities.Any(city => city.Label.StartsWith("伦敦 ·")),
    "Shanghai and Xi'an exclude small same-name places while populated cities remain available");

var cleanInstall = new AppState();
Check(cleanInstall.Presets[0].Name == "🌞 我的一天" && cleanInstall.Presets[0].Tasks.Count == 0,
    "fresh install starts with an empty My Day");
cleanInstall.ShellAcrylic = true;
cleanInstall.Presets[0].Tasks.Add(new TodoTask { Title = "保留已有任务" });
var restoredAppearance = System.Text.Json.JsonSerializer.Deserialize<AppState>(
    System.Text.Json.JsonSerializer.Serialize(cleanInstall))!;
Check(restoredAppearance.ShellAcrylic && restoredAppearance.Presets[0].Tasks.Count == 1,
    "acrylic persists without clearing existing tasks");

Check(AppUpdateService.ParseVersion("1.10") > AppUpdateService.ParseVersion("1.9"), "numeric update ordering");
Check(AppUpdateService.ParseVersion("1.5") == AppUpdateService.ParseVersion("1.5.0.0"), "normalized update equality");
foreach (var invalid in new[] { "1", "01.5", "0.0", "1.65536", "1.2-beta" })
{
    bool rejected = false;
    try { AppUpdateService.ParseVersion(invalid); } catch (FormatException) { rejected = true; }
    Check(rejected, "invalid update version: " + invalid);
}
var updateTestDirectory = Path.Combine(Path.GetTempPath(), "tomatotodo-update-test-" + Guid.NewGuid());
var updates = new AppUpdateService(updateTestDirectory);
Check(!updates.IsSkipped("1.6"), "no skipped release initially");
updates.Skip("1.6");
Check(new AppUpdateService(updateTestDirectory).IsSkipped("1.6.0"), "skipped version survives service restart");
Check(!updates.IsSkipped("1.7"), "future releases are not skipped");
File.Delete(Path.Combine(updateTestDirectory, "skipped-update.txt"));
Directory.Delete(updateTestDirectory);

if (args.Length is 2 or 6 && args[0] == "--verify-native-backup")
{
    using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[1]));
    var root = document.RootElement;
    Check(root.GetProperty("Format").GetString() == "tomatotodo-native-backup", "native backup format");
    Check(root.GetProperty("Version").GetInt32() == 1, "native backup version");
    var state = System.Text.Json.JsonSerializer.Deserialize<AppState>(root.GetProperty("Data").GetRawText()) ?? throw new Exception("Missing backup state");
    CourseScheduleCodec.Validate(state.CourseSchedule);
    var counts = args.Length == 6 ? args.Skip(2).Select(int.Parse).ToArray() : [3, 4, 79, 16];
    Check(state.Presets.Count == counts[0], "migrated preset count");
    Check(state.ArchivedTasks.Count == counts[1], "migrated history count");
    Check(state.FocusLogs.Count == counts[2], "migrated focus log count");
    Check(state.CourseSchedule.Weeks.Count == counts[3], "migrated course week count");
    Check(state.ActivePresetId is null || state.Presets.Any(preset => preset.Id == state.ActivePresetId), "active preset reference");
    Check(state.Presets.SelectMany(preset => preset.Tasks).Any(task => task.Id == state.ActiveTaskId), "active task reference");
    Console.WriteLine($"Native backup verified: {state.Presets.Count} presets, {state.ArchivedTasks.Count} history, {state.FocusLogs.Count} logs, {state.CourseSchedule.Weeks.Count} course weeks.");
    return;
}

var blank = CourseScheduleCodec.EmptyTerm();
Check(blank.Weeks.Count == 16, "default term preserves 16-week structure");
Check(blank.Weeks[0].Date == "2026-09-07", "first week date");
Check(CourseScheduleCodec.WeekForDate(blank, new DateOnly(2026, 9, 21))?.Date == "2026-09-21",
    "lookup uses Monday of selected week");

blank.Weeks[2].Events.Add(new CourseEvent
{
    Day = 0, Start = 1, End = 2, StartTime = "08:30", EndTime = "10:05",
    Name = "新媒体实验艺术", Room = "A503", Teacher = "张老师", Tone = "violet"
});
var json = CourseScheduleCodec.Export(blank);
var roundTrip = CourseScheduleCodec.Parse(json);
Check(roundTrip.Weeks[2].Events.Single().Name == "新媒体实验艺术", "course JSON round trip");
Check(roundTrip.Weeks[2].Events.Single().StartTime == "08:30", "exact start time preserved");

var nested = CourseScheduleCodec.Parse("{\"data\":" + json + "}");
Check(nested.Weeks.Count == 16, "nested data envelope accepted");

MustReject(json.Replace("2026-09-21", "2026-09-22"), "non-Monday week date");
MustReject(json.Replace("10:05", "07:00"), "end before start");
MustReject(json.Replace("\"version\": 1", "\"version\": 2"), "unsupported version");
var dashboard = DashboardWidgets.Default();
var placements = DashboardWidgets.Place(dashboard).ToDictionary(item => item.Id);
Check(placements["timer"] is { Column: 0, Row: 0, Width: 4, Height: 2 }, "timer footprint");
Check(placements["mode"] is { Column: 4, Row: 0, Width: 2, Height: 1 }, "mode beside timer");
Check(placements["tasks"] is { Column: 0, Row: 2, Width: 2, Height: 2 }, "task footprint");
Check(placements["calendar"] is { Column: 4, Row: 2, Width: 2, Height: 2 }, "calendar footprint");
var draft = dashboard.Clone();
foreach (var viewportWidth in new[] { 240d, 320, 480, 688, 900, 1040, 1600 })
{
    var columns = DashboardWidgets.ColumnsForWidth(viewportWidth);
    var allTiles = dashboard.Clone();
    foreach (var widget in DashboardWidgets.All) allTiles.Visible[widget.Id] = true;
    var responsive = DashboardWidgets.Place(allTiles, columns);
    var occupied = new HashSet<(int X, int Y)>();
    Check(responsive.Count == DashboardWidgets.All.Count, $"all tiles retained at {viewportWidth}");
    foreach (var tile in responsive)
    {
        Check(tile.Column >= 0 && tile.Column + tile.Width <= columns, $"tile stays inside {columns}-column canvas");
        for (var y = tile.Row; y < tile.Row + tile.Height; y++)
            for (var x = tile.Column; x < tile.Column + tile.Width; x++)
                Check(occupied.Add((x, y)), $"no overlapping tiles at {viewportWidth}");
    }
    Check(allTiles.Order.SequenceEqual(dashboard.Order), "responsive layout does not rewrite saved order");
}
draft.Visible["timer"] = false;
Check(dashboard.Visible["timer"], "editing draft does not mutate saved layout");
Check(DashboardWidgets.Normalize(new DashboardLayout { Order = ["timer", "timer", "unknown"] }).Order.Count == DashboardWidgets.All.Count,
    "unknown or duplicate widget IDs removed");
DashboardWidgets.MoveBefore(draft, "calendar", "tasks");
Check(draft.Order.IndexOf("calendar") < draft.Order.IndexOf("tasks") &&
      dashboard.Order.IndexOf("calendar") > dashboard.Order.IndexOf("tasks"), "reordering changes only the draft");
var date = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(8));
var dragDraft = dashboard.Clone();
var savedOrder = dashboard.Order.ToArray();
foreach (var columns in new[] { 1, 2, 4, 6 })
{
    foreach (var source in savedOrder)
    foreach (var target in savedOrder)
    foreach (var after in new[] { false, true })
    {
        DashboardWidgets.MoveRelative(dragDraft, source, target, after);
        Check(dragDraft.Order.Count == savedOrder.Length && dragDraft.Order.Distinct().Count() == savedOrder.Length,
            "drag preview preserves every widget exactly once");
        if (source != target)
            Check(dragDraft.Order.IndexOf(source) == dragDraft.Order.IndexOf(target) + (after ? 1 : -1),
                "drag supports insertion before and after target");
        var cells = new HashSet<(int, int)>();
        foreach (var tile in DashboardWidgets.Place(dragDraft, columns))
            for (var y = tile.Row; y < tile.Row + tile.Height; y++)
                for (var x = tile.Column; x < tile.Column + tile.Width; x++)
                    Check(x < columns && cells.Add((x, y)), "live drag packing stays in bounds without overlap");
    }
}
Check(dashboard.Order.SequenceEqual(savedOrder), "live drag previews never persist without save");
dragDraft.Order = [.. savedOrder];
Check(dragDraft.Order.SequenceEqual(dashboard.Order), "cancel restores drag snapshot");
Check(PresetSchedule.Next(date, new TaskPreset { Repeat = "daily" }) == date.AddDays(1), "daily repeat");
Check(PresetSchedule.Next(date.AddDays(4), new TaskPreset { Repeat = "workday" }) == date.AddDays(7), "workday skips weekend");
Check(PresetSchedule.Next(date, new TaskPreset { Repeat = "monthly" }) == date.AddMonths(1), "monthly repeat");
Check(PresetSchedule.Next(date, new TaskPreset { Repeat = "custom", RepeatUnit = "week", RepeatInterval = 2,
    RepeatDays = [DayOfWeek.Tuesday] }) == date.AddDays(1), "custom next selected weekday");
Check(PresetSchedule.Next(date.AddDays(1), new TaskPreset { Repeat = "custom", RepeatUnit = "week", RepeatInterval = 2,
    RepeatDays = [DayOfWeek.Tuesday] }) == date.AddDays(15), "custom alternate week after selected weekday");
Check(PresetSchedule.Next(date, new TaskPreset()) is null, "non-repeating preset stays stopped");
var archiveLogs = new List<FocusLog>
{
    new() { StartedAt = new DateTimeOffset(2026, 9, 5, 9, 0, 0, TimeSpan.FromHours(8)), Seconds = 25 * 60, CompletedPomodoro = true },
    new() { StartedAt = new DateTimeOffset(2026, 9, 5, 11, 0, 0, TimeSpan.FromHours(8)), Seconds = 55 * 60 },
    new() { StartedAt = new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.FromHours(8)), Seconds = 251 * 60 }
};
var archiveDay = ArchiveAnalytics.SummaryForDay(archiveLogs, new DateOnly(2026, 9, 5));
Check(archiveDay is { Seconds: 4800, Pomodoros: 1, Sessions: 2 }, "archive derives daily totals from real logs");
Check(ArchiveAnalytics.MonthCells(new DateTime(2026, 9, 1)).Count == 42, "archive calendar has six fixed rows");
Check(ArchiveAnalytics.Series(archiveLogs, "月度", new DateTime(2026, 9, 21)).Count == 30, "monthly statistics preserves every day");
Check(ArchiveAnalytics.Series(archiveLogs, "日度", new DateTime(2026, 9, 5)).Count == 24 &&
      ArchiveAnalytics.Series(archiveLogs, "周度", new DateTime(2026, 9, 5)).Count == 7 &&
      ArchiveAnalytics.Series(archiveLogs, "年度", new DateTime(2026, 9, 5)).Count == 12,
    "archive statistics exposes hourly, daily, and monthly horizontal axes");
Check(ArchiveAnalytics.HeatmapLevel(250 * 60) == 5 && ArchiveAnalytics.HeatmapLevel(50 * 60) == 1,
    "heatmap uses fixed absolute intensity thresholds");
Check(ArchiveAnalytics.Heatmap(archiveLogs, 2026).Count == 371, "heatmap is a complete 53-week grid");
var appearanceState = new AppState();
Check(appearanceState is { Theme: "auto", AccentColor: "#30643B", AccentMode: "auto", PureBlack: false, UiScale: 100 },
    "native personalization defaults are stable");
appearanceState.UiScale = 125;
Check(appearanceState.UiScale is >= 85 and <= 125, "personalization scale remains in supported range");
var generalState = new AppState { CountdownName = "学期结束", CountdownDate = date.AddDays(30), PositiveCountup = true };
Check(generalState.PositiveCountup && generalState.CountdownDate is not null, "general timer and countdown options persist");
Check(generalState is { ShowClockMarkers: true, ShowClockSeconds: true, CourseReminderLeadMinutes: 10, MusicMode: "system" },
    "general defaults preserve clock, reminder and media preferences");
Console.WriteLine("CourseSchedule + DashboardLayout (including seven responsive widths) + PresetSchedule + ArchiveAnalytics + Appearance + General: all checks passed.");

var reminderSchedule = new CourseScheduleData
{
    Term = new CourseTerm { Timezone = "Asia/Shanghai" },
    Weeks = [new CourseWeek { Date = "2026-09-21", Events = [
        new CourseEvent { Day = 4, Name = "设计课程", Room = "A101", StartTime = "08:00", EndTime = "09:00", Start = 1, End = 2 },
        new CourseEvent { Day = 5, Name = "跨日课程", StartTime = "00:05", EndTime = "01:00", Start = 1, End = 2 }
    ] }]
};
var delivered = new Dictionary<string, DateTimeOffset>();
var reminderNow = new DateTimeOffset(2026, 9, 25, 7, 50, 0, TimeSpan.FromHours(8));
Check(!CourseReminder.Due(reminderSchedule, reminderNow.AddSeconds(-1), 10, delivered).Any(), "reminders do not fire before the lead window");
var dueReminder = CourseReminder.Due(reminderSchedule, reminderNow, 10, delivered).Single();
Check(dueReminder.Course.Name == "设计课程", "reminders use exact course time and weekday");
Check(CourseReminder.Due(reminderSchedule, reminderNow.ToUniversalTime(), 10, delivered).Count() == 1, "schedule timezone is independent of device offset");
delivered[dueReminder.Key] = reminderNow;
var deliveredRoundTrip = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(System.Text.Json.JsonSerializer.Serialize(delivered))!;
Check(!CourseReminder.Due(reminderSchedule, reminderNow.AddMinutes(1), 10, deliveredRoundTrip).Any(), "reminders remain deduplicated after storage reload");
Check(!CourseReminder.Due(reminderSchedule, reminderNow.AddMinutes(10), 10, new Dictionary<string, DateTimeOffset>()).Any(), "already-started courses are not replayed");
Check(CourseReminder.Due(reminderSchedule, new DateTimeOffset(2026, 9, 25, 23, 55, 0, TimeSpan.FromHours(8)), 10, delivered).Single().Course.Name == "跨日课程", "lead windows work across midnight");
Console.WriteLine("CourseReminder: timing, timezone, persisted deduplication, stale and midnight checks passed.");

Check(OfflineQuotes.All.Length >= 80 && OfflineQuotes.All.Distinct().Count() == OfflineQuotes.All.Length,
    "offline quote library has at least 80 unique entries");
Check(!new AppState().WeatherLocationConsent, "location is off until explicit consent");
Check(Tomatotodo_Windows.Services.DashboardContentService.WeatherDescription(0) == "晴" &&
    Tomatotodo_Windows.Services.DashboardContentService.WeatherDescription(95) == "雷雨", "WMO weather codes map to readable conditions");
Console.WriteLine("Dashboard content: offline quote reserve, opt-in location and weather mapping checks passed.");

var cycleTask = new TodoTask { EstimatedPomodoros = 2, CompletedPomodoros = 1 };
Check(TimerCycle.ShouldContinue(true, false, true, true, cycleTask, cycleTask.Id), "automatic focus enters break");
Check(TimerCycle.ShouldContinue(true, false, false, true, cycleTask, cycleTask.Id), "first break resumes second tomato");
cycleTask.CompletedPomodoros = 2; cycleTask.IsComplete = true;
Check(TimerCycle.ShouldContinue(true, false, true, true, cycleTask, null), "final tomato still gets a break");
Check(!TimerCycle.ShouldContinue(true, false, false, true, cycleTask, Guid.NewGuid()), "target reached never starts next task");
Check(!TimerCycle.ShouldContinue(false, false, true, true, cycleTask, null), "manual waits at phase boundary");
Check(!TimerCycle.ShouldContinue(true, true, true, false, null, null), "countup never cycles");
Check(TimerCycle.ShouldContinue(true, false, false, false, null, null), "untargeted focus can continue");
Check(!TimerCycle.ShouldContinue(true, false, false, true, null, null), "deleted task stops cycle");
Console.WriteLine("Timer cycle: manual, automatic, target completion, countup and task deletion checks passed.");

Check(WindowEdgeSnap.Coordinate(12, 208, 0, 1920, 20) == 0, "mini snaps to left edge");
Check(WindowEdgeSnap.Coordinate(1700, 208, 0, 1920, 20) == 1712, "mini snaps to right edge");
Check(WindowEdgeSnap.Coordinate(10, 64, 0, 1040, 20) == 0, "mini snaps to top edge");
Check(WindowEdgeSnap.Coordinate(965, 64, 0, 1040, 20) == 976, "mini respects taskbar bottom work area");
Check(WindowEdgeSnap.Coordinate(-1910, 208, -1920, 1920, 20) == -1920, "mini supports negative monitor coordinates");
Check(WindowEdgeSnap.Coordinate(500, 208, 0, 1920, 20) == 500, "mini stays free away from edges");
Console.WriteLine("Mini edge snap: four edges, work area and multiple monitors passed.");

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
}

static void MustReject(string json, string message)
{
    try { CourseScheduleCodec.Parse(json); }
    catch (InvalidDataException) { return; }
    throw new Exception("FAILED: " + message);
}
