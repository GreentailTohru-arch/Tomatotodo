using System.Text.Json;
using System.Text.Json.Nodes;
using Tomatotodo_Windows.Data;

namespace Tomatotodo_Windows.Accounts;

// Protocol v2 is also implemented by the Flutter adapter. Platform settings never
// overwrite the other platform; sync uses three-way comparison, never clock order.
public static class UnifiedUserData
{
    public const string Format = "tomatotodo-user-data";
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] CommonFields = ["Presets", "ActivePresetId", "ActiveTaskId", "ArchivedTasks", "FocusLogs", "FocusMinutes", "BreakMinutes", "EnableShortBreak", "PositiveCountup", "CountdownName", "CountdownDate", "CourseSchedule", "UnifiedData"];
    public static JsonObject Empty() => new() { ["format"] = Format, ["version"] = 2, ["shared"] = new JsonObject(), ["platforms"] = new JsonObject(), ["changes"] = new JsonObject() };
    public static JsonObject Clone(JsonObject value) => (JsonObject)value.DeepClone();
    public static bool Equal(JsonNode? a, JsonNode? b) => JsonNode.DeepEquals(a, b);
    public static bool IsDocument(JsonObject value) => value["format"]?.GetValue<string>() == Format && value["version"]?.GetValue<int>() == 2;
    public static void Validate(JsonObject doc)
    {
        if (!IsDocument(doc) || doc["shared"] is not JsonObject || doc["platforms"] is not JsonObject || doc["changes"] is not JsonObject)
            throw new InvalidDataException("不是 Tomatotodo 跨平台用户数据 v2。");
        var common = doc["shared"]!.AsObject();
        if (common["tasks"] is not JsonObject tasks || tasks["Presets"] is not JsonArray presets || presets.Count == 0 || common["archive"]?["records"] is not JsonObject || common["archive"]?["deleted"] is not JsonArray)
            throw new InvalidDataException("用户数据缺少清单或档案。");
        var seen = new HashSet<Guid>();
        foreach (var item in presets)
        {
            if (item is not JsonObject p || !Guid.TryParse(p["Id"]?.ToString(), out var id) || !seen.Add(id) || p["Name"] is null || p["Tasks"] is not JsonArray list)
                throw new InvalidDataException("清单数据无效或标识重复。");
            foreach (var task in list)
                if (!Guid.TryParse(task?["Id"]?.ToString(), out _) || string.IsNullOrWhiteSpace(task?["Title"]?.ToString())) throw new InvalidDataException("任务数据无效。");
        }
        foreach (var (id, record) in common["archive"]!["records"]!.AsObject())
            if (!Guid.TryParse(id, out _) || record is not JsonObject || !DateTimeOffset.TryParse(record["StartedAt"]?.ToString(), out _) || record["Seconds"]?.GetValue<int>() is not > 0)
                throw new InvalidDataException("专注日志无效。");
        CourseScheduleCodec.Parse(common["timetable"]!.ToJsonString());
        var timer = common["timer"] ?? throw new InvalidDataException("计时设置缺失。");
        if (timer["FocusMinutes"]?.GetValue<int>() is not (>= 1 and <= 180) || timer["BreakMinutes"]?.GetValue<int>() is not (>= 1 and <= 60)) throw new InvalidDataException("计时长度无效。");
    }

    public static JsonObject Capture(AppState state, JsonObject? previous = null, UserProfile? profile = null, byte[]? avatar = null)
    {
        var doc = Clone(previous ?? state.UnifiedData ?? Empty());
        var shared = doc["shared"]!.AsObject();
        var raw = JsonSerializer.SerializeToNode(state, Json)!.AsObject();
        raw.Remove("UnifiedData");
        JsonObject Pick(params string[] keys) { var o = new JsonObject(); foreach (var key in keys) o[key] = raw[key]?.DeepClone(); return o; }
        var tasks = Pick("Presets", "ActivePresetId", "ActiveTaskId", "ArchivedTasks");
        foreach (var p in tasks["Presets"]!.AsArray())
        {
            p!.AsObject().Remove("LastRemindedAt");
            foreach (var key in new[] { "DueAt", "RemindAt" }) if (DateTimeOffset.TryParse(p[key]?.ToString(), out var at)) p[key] = at.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
            foreach (var t in p["Tasks"]!.AsArray()) {
            var id = Guid.Parse(t!["Id"]!.ToString());
            t["CompletedPomodoros"] = Math.Max(0, t["CompletedPomodoros"]!.GetValue<int>() - state.FocusLogs.Count(l => l.TaskId == id && l.CompletedPomodoro));
            if (t["EstimatedPomodoros"]?.GetValue<int>() == 0) t["EstimatedPomodoros"] = null;
            }
        }
        foreach (var h in tasks["ArchivedTasks"]!.AsArray()) if (DateTimeOffset.TryParse(h!["ArchivedAt"]?.ToString(), out var at)) h["ArchivedAt"] = at.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
        tasks["ActivePresetId"] ??= state.Presets.First().Id.ToString();
        Put(doc, "tasks", tasks);
        Put(doc, "timer", Pick("FocusMinutes", "BreakMinutes", "EnableShortBreak", "PositiveCountup"));
        var countdown = Pick("CountdownName", "CountdownDate");
        if (state.CountdownDate is { } date) countdown["CountdownDate"] = date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
        Put(doc, "countdown", countdown);
        Put(doc, "timetable", JsonNode.Parse(CourseScheduleCodec.Export(state.CourseSchedule))!);
        if (profile is not null) Put(doc, "profile", new JsonObject { ["Nickname"] = profile.Nickname, ["Biography"] = profile.Biography, ["AvatarBase64"] = avatar is null ? null : Convert.ToBase64String(avatar) });
        var records = new JsonObject();
        foreach (var log in state.FocusLogs)
        {
            var entry = JsonSerializer.SerializeToNode(log, Json)!.AsObject();
            entry["StartedAt"] = log.StartedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
            if (string.IsNullOrEmpty(log.TaskTitle)) entry["TaskTitle"] = state.Presets.SelectMany(p => p.Tasks).FirstOrDefault(t => t.Id == log.TaskId)?.Title ?? "未指定任务";
            records[log.Id.ToString()] = entry;
        }
        var deleted = (shared["archive"]?["deleted"]?.AsArray() ?? []).Select(x => x!.ToString()).ToHashSet();
        if (shared["archive"]?["records"] is JsonObject old) foreach (var id in old.Select(p => p.Key)) if (!records.ContainsKey(id)) deleted.Add(id);
        foreach (var id in deleted) records.Remove(id);
        Put(doc, "archive", new JsonObject { ["records"] = records, ["deleted"] = new JsonArray(deleted.Order().Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) });
        foreach (var key in CommonFields.Concat(new[] { "LocalMusicFolderToken", "WeatherLocationConsent", "DeliveredCourseReminders", "CachedWeatherJson", "CachedFocusQuotes" })) raw.Remove(key);
        Put(doc, "windows", raw, platform: true);
        return doc;
    }

    private static void Put(JsonObject doc, string key, JsonNode value, bool platform = false)
    {
        var group = doc[platform ? "platforms" : "shared"]!.AsObject();
        if (Equal(group[key], value)) return;
        group[key] = value;
        doc["changes"]![key] = new JsonObject { ["platform"] = "windows", ["at"] = DateTimeOffset.UtcNow.ToString("O") };
    }

    public static AppState Apply(JsonObject doc, AppState? local = null)
    {
        Validate(doc);
        var raw = JsonSerializer.SerializeToNode(local ?? new AppState(), Json)!.AsObject();
        if (doc["platforms"]?["windows"] is JsonObject desktop) foreach (var pair in desktop) raw[pair.Key] = pair.Value?.DeepClone();
        var shared = doc["shared"]!;
        foreach (var key in new[] { "tasks", "timer", "countdown" }) foreach (var pair in shared[key]!.AsObject()) raw[pair.Key] = pair.Value?.DeepClone();
        raw["FocusLogs"] = new JsonArray(shared["archive"]!["records"]!.AsObject().Select(p => p.Value!.DeepClone()).ToArray());
        raw["UnifiedData"] = null;
        var state = raw.Deserialize<AppState>(Json) ?? throw new InvalidDataException("无效数据。");
        state.CourseSchedule = CourseScheduleCodec.Parse(shared["timetable"]!.ToJsonString());
        foreach (var task in state.Presets.SelectMany(p => p.Tasks)) task.CompletedPomodoros += state.FocusLogs.Count(l => l.TaskId == task.Id && l.CompletedPomodoro);
        state.UnifiedData = Clone(doc);
        return state;
    }

    public sealed record Conflict(string Section, string LocalPlatform, string LocalTime, string RemotePlatform, string RemoteTime);
    public sealed record MergeResult(JsonObject Document, List<Conflict> Conflicts);
    public static MergeResult Merge(JsonObject baseline, JsonObject local, JsonObject remote, bool? useLocal = null)
    {
        var result = Clone(remote); var conflicts = new List<Conflict>();
        foreach (var group in new[] { "shared", "platforms" })
        {
            var keys = local[group]!.AsObject().Select(p => p.Key).Union(remote[group]!.AsObject().Select(p => p.Key));
            foreach (var key in keys)
            {
                var b = baseline[group]?[key]; var l = local[group]?[key]; var r = remote[group]?[key];
                if (key == "archive")
                {
                    var deleted = new HashSet<string>(); var records = new JsonObject();
                    foreach (var archive in new[] { r, l })
                    {
                        if (archive?["deleted"] is JsonArray d) foreach (var id in d) deleted.Add(id!.ToString());
                        if (archive?["records"] is JsonObject rows) foreach (var row in rows)
                            if (records[row.Key] is null || (row.Value?["Seconds"]?.GetValue<int>() ?? 0) > (records[row.Key]?["Seconds"]?.GetValue<int>() ?? 0)) records[row.Key] = row.Value?.DeepClone();
                    }
                    foreach (var id in deleted) records.Remove(id);
                    result[group]![key] = new JsonObject { ["records"] = records, ["deleted"] = new JsonArray(deleted.Order().Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) };
                    continue;
                }
                if (Equal(l, b) || Equal(l, r)) continue;
                var conflict = !Equal(r, b);
                if (conflict) conflicts.Add(new(key, local["changes"]?[key]?["platform"]?.ToString() ?? "windows", local["changes"]?[key]?["at"]?.ToString() ?? "未知", remote["changes"]?[key]?["platform"]?.ToString() ?? "未知", remote["changes"]?[key]?["at"]?.ToString() ?? "未知"));
                if (!conflict || useLocal == true)
                {
                    result[group]![key] = l?.DeepClone();
                    result["changes"]![key] = local["changes"]?[key]?.DeepClone();
                }
            }
        }
        return new(result, conflicts);
    }
}
