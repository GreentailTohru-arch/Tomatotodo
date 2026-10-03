using System.Text.Json.Nodes;
using Tomatotodo_Windows.Accounts;
using Tomatotodo_Windows.Data;

void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
if (args.Length == 3 && args[0] == "roundtrip")
{
    var input = JsonNode.Parse(File.ReadAllText(args[1]))!.AsObject();
    var state = UnifiedUserData.Apply(input);
    var output = UnifiedUserData.Capture(state);
    Check(UnifiedUserData.Equal(input["shared"], output["shared"]), "Desktop roundtrip changed shared data");
    Check(UnifiedUserData.Equal(input["platforms"]?["mobile"], output["platforms"]?["mobile"]), "Desktop discarded mobile settings");
    File.WriteAllText(args[2], output.ToJsonString());
    Console.WriteLine("Cross-platform desktop roundtrip passed"); return;
}
var task = new TodoTask { Title = "跨平台任务", Subtitle = "子标题", EstimatedPomodoros = 3, CompletedPomodoros = 1 };
var state0 = new AppState { Presets = [new TaskPreset { Name = "同步清单", Tasks = [task], DueAt = DateTimeOffset.Parse("2026-10-10T08:00:00+08:00"), RepeatDays = [DayOfWeek.Monday, DayOfWeek.Sunday] }], ActiveTaskId = task.Id };
state0.ActivePresetId = state0.Presets[0].Id;
state0.FocusLogs.Add(new FocusLog { TaskId = task.Id, TaskTitle = task.Title, StartedAt = DateTimeOffset.Parse("2026-09-30T10:00:00+08:00"), Seconds = 1500, CompletedPomodoro = true });
var baseline = UnifiedUserData.Capture(state0, profile: new UserProfile { Nickname = "同步测试" });
var restored = UnifiedUserData.Apply(baseline);
Check(restored.Presets[0].Tasks[0].CompletedPomodoros == 1, "tomato double count");
Check(UnifiedUserData.Equal(baseline, UnifiedUserData.Capture(restored)), "unchanged recapture created a false edit");
var local = UnifiedUserData.Clone(baseline); var remote = UnifiedUserData.Clone(baseline);
local["shared"]!["timer"]!["FocusMinutes"] = 30;
remote["shared"]!["countdown"]!["CountdownName"] = "考试";
var m = UnifiedUserData.Merge(baseline, local, remote);
Check(m.Conflicts.Count == 0 && m.Document["shared"]!["timer"]!["FocusMinutes"]!.GetValue<int>() == 30, "independent edits");
remote["shared"]!["timer"]!["FocusMinutes"] = 45;
Check(UnifiedUserData.Merge(baseline, local, remote).Conflicts.Count == 1, "missing conflict");
Check(UnifiedUserData.Merge(baseline, local, remote, true).Document["shared"]!["timer"]!["FocusMinutes"]!.GetValue<int>() == 30, "local resolution");
var added = Guid.NewGuid().ToString();
remote["shared"]!["archive"]!["records"]![added] = JsonNode.Parse("{\"Id\":\"" + added + "\",\"StartedAt\":\"2026-09-30T03:00:00Z\",\"Seconds\":60,\"CompletedPomodoro\":false}");
var deleted = state0.FocusLogs[0].Id.ToString();
local["shared"]!["archive"]!["deleted"]!.AsArray().Add(deleted);
local["shared"]!["archive"]!["records"]!.AsObject().Remove(deleted);
var merged = UnifiedUserData.Merge(baseline, local, remote, true).Document;
Check(merged["shared"]!["archive"]!["records"]!.AsObject().Count == 1, "log union/tombstones");
if (args.Length == 2 && args[0] == "fixture") File.WriteAllText(args[1], baseline.ToJsonString());
Console.WriteLine("Unified protocol tests passed (roundtrip, independent edits, conflict, resolution, log dedupe/deletion)");
