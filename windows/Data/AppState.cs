using System.Text.Json;

namespace Tomatotodo_Windows.Data;

public sealed class TodoTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public int? EstimatedPomodoros { get; set; }
    public int CompletedPomodoros { get; set; }
    public bool IsComplete { get; set; }
}

public sealed class TaskPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = global::Tomatotodo_Windows.Data.UiText.T("\u6211\u7684\u4EFB\u52A1");
    public List<TodoTask> Tasks { get; set; } = [];
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? RemindAt { get; set; }
    public string Repeat { get; set; } = "none";
    public int RepeatInterval { get; set; } = 1;
    public string RepeatUnit { get; set; } = "week";
    public List<DayOfWeek> RepeatDays { get; set; } = [];
    public DateTimeOffset? LastRemindedAt { get; set; }
}

public sealed class ArchivedTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PresetId { get; set; }
    public TodoTask Task { get; set; } = new();
    public DateTimeOffset ArchivedAt { get; set; } = DateTimeOffset.Now;
    public string Status { get; set; } = "completed";
}

public sealed class FocusLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TaskId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public int Seconds { get; set; }
    public bool CompletedPomodoro { get; set; }
    public string TaskTitle { get; set; } = "";
}

public sealed class AppState
{
    public System.Text.Json.Nodes.JsonObject? UnifiedData { get; set; }
    public int Version { get; set; } = 1;
    public List<TaskPreset> Presets { get; set; } =
    [
        new TaskPreset { Name = global::Tomatotodo_Windows.Data.UiText.T("\uD83C\uDF1E \u6211\u7684\u4E00\u5929") },
        new TaskPreset { Name = global::Tomatotodo_Windows.Data.UiText.T("\u2B50 \u91CD\u8981") },
        new TaskPreset { Name = global::Tomatotodo_Windows.Data.UiText.T("\uD83D\uDDD3\uFE0F \u8BA1\u5212\u5185") }
    ];
    public Guid? ActivePresetId { get; set; }
    public Guid? ActiveTaskId { get; set; }
    public List<FocusLog> FocusLogs { get; set; } = [];
    public List<ArchivedTask> ArchivedTasks { get; set; } = [];
    public int FocusMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public bool EnableShortBreak { get; set; } = true;
    public bool AutomaticTimerCycle { get; set; }
    public bool FocusEndSystemNotification { get; set; } = true;
    public bool FocusEndSound { get; set; } = true;
    public bool BreakEndSystemNotification { get; set; } = true;
    public bool BreakEndSound { get; set; } = true;
    public bool PositiveCountup { get; set; }
    public bool ImmersiveMode { get; set; }
    public bool MiniWindowMode { get; set; }
    public bool HideTaskbarInBackground { get; set; }
    public string CloseBehavior { get; set; } = "ask";
    public bool TrayShowNextCourse { get; set; }
    public bool TrayMonochromeIcon { get; set; }
    public bool ShowClockMarkers { get; set; } = true;
    public bool ShowClockSeconds { get; set; } = true;
    public string CountdownName { get; set; } = global::Tomatotodo_Windows.Data.UiText.T("\u5047\u671F\u7ED3\u675F");
    public DateTimeOffset? CountdownDate { get; set; }
    public bool CourseReminderEnabled { get; set; }
    public int CourseReminderLeadMinutes { get; set; } = 10;
    public bool CourseReminderSystemToast { get; set; } = true;
    public bool CourseReminderAppMessage { get; set; } = true;
    public bool CourseReminderSound { get; set; } = true;
    public Dictionary<string, DateTimeOffset> DeliveredCourseReminders { get; set; } = [];
    public string MusicMode { get; set; } = "system";
    public bool WeatherLocationConsent { get; set; }
    public string? WeatherCityName { get; set; }
    public double? WeatherCityLatitude { get; set; }
    public double? WeatherCityLongitude { get; set; }
    public string? CachedWeatherJson { get; set; }
    public List<string> CachedFocusQuotes { get; set; } = [];
    public string? LocalMusicFolderToken { get; set; }
    public string Theme { get; set; } = "auto";
    public string AccentColor { get; set; } = "#30643B";
    public string AccentMode { get; set; } = "auto";
    public List<string> RecentAccentColors { get; set; } = ["#00B294", "#8E7B22", "#E62117", "#0078D7", "#A348B6"];
    public bool PureBlack { get; set; }
    public bool ShellAcrylic { get; set; }
    public int UiScale { get; set; } = 100;
    public string QuickNote { get; set; } = "";
    public bool CourseScheduleEnabled { get; set; }
    public CourseScheduleData CourseSchedule { get; set; } = CourseScheduleCodec.EmptyTerm();
    public DashboardLayout DashboardLayout { get; set; } = DashboardWidgets.Default();
}

public static class StateStore
{
    // The account workspace supplies these routes after authenticated login.
    // Leaving them null retains the original guest file without moving or overwriting it.
    public static Func<AppState>? AccountLoader { get; set; }
    public static Action<AppState>? AccountSaver { get; set; }
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tomatotodo", "Native");

    public static string FilePath => Path.Combine(DirectoryPath, "state.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppState Load()
    {
        if (AccountLoader is not null) return AccountLoader();
        try
        {
            if (!File.Exists(FilePath)) return new AppState();
            var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath));
            if (state is null || state.Version != 1) return new AppState();
            state.Presets ??= [];
            state.FocusLogs ??= [];
            state.ArchivedTasks ??= [];
            state.CourseSchedule ??= CourseScheduleCodec.EmptyTerm();
            state.DashboardLayout = DashboardWidgets.Normalize(state.DashboardLayout);
            if (state.Presets.Count == 0) state.Presets.Add(new TaskPreset());
            if (string.IsNullOrWhiteSpace(state.AccentColor) || state.AccentColor.Length != 7) state.AccentColor = "#30643B";
            if (state.AccentMode is not ("auto" or "manual")) state.AccentMode = "auto";
            state.RecentAccentColors ??= [];
            state.RecentAccentColors = state.RecentAccentColors.Where(color => color is { Length: 7 } && color.StartsWith('#'))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
            if (state.RecentAccentColors.Count == 0)
                state.RecentAccentColors = ["#00B294", "#8E7B22", "#E62117", "#0078D7", "#A348B6"];
            if (!state.RecentAccentColors.Contains(state.AccentColor, StringComparer.OrdinalIgnoreCase))
            {
                state.RecentAccentColors.Insert(0, state.AccentColor.ToUpperInvariant());
                state.RecentAccentColors = state.RecentAccentColors.Take(5).ToList();
            }
            state.UiScale = Math.Clamp(state.UiScale, 85, 125);
            var removedLegacyQuickNote = !string.IsNullOrWhiteSpace(state.QuickNote);
            state.QuickNote = "";
            state.CourseReminderLeadMinutes = Math.Clamp(state.CourseReminderLeadMinutes, 1, 120);
            if (string.IsNullOrWhiteSpace(state.CountdownName)) state.CountdownName = global::Tomatotodo_Windows.Data.UiText.T("\u5047\u671F\u7ED3\u675F");
            // The former Spotify placeholder is now the Windows current-media source.
            if (state.MusicMode == "spotify" || state.MusicMode is not ("system" or "local")) state.MusicMode = "system";
            foreach (var preset in state.Presets)
            {
                preset.Tasks ??= [];
                preset.RepeatDays ??= [];
                if (preset.RepeatInterval < 1) preset.RepeatInterval = 1;
            }
            if (removedLegacyQuickNote) Save(state);
            return state;
        }
        catch (Exception)
        {
            // Move unreadable data aside so a later save cannot destroy the only copy.
            try
            {
                if (File.Exists(FilePath))
                    File.Move(FilePath, FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
            }
            catch { }
            return new AppState();
        }
    }

    public static void Save(AppState state)
    {
        if (AccountSaver is not null) { AccountSaver(state); return; }
        Directory.CreateDirectory(DirectoryPath);
        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporaryPath, FilePath, true);
    }
}
