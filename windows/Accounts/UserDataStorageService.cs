using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tomatotodo_Windows.Data;

namespace Tomatotodo_Windows.Accounts;

/// <summary>Each Users/{guid} directory contains exactly the four documented files.</summary>
public sealed class UserDataStorageService : IUserDataStorageService
{
    private readonly string _root;
    private readonly object _gate = new();
    private static readonly JsonSerializerOptions Json = new()
    { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public UserDataStorageService(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(Path.Combine(_root, "Users"));
        // Recover a directory swap interrupted after moving the old data aside.
        var recovery = Path.Combine(_root, "Recovery");
        if (Directory.Exists(recovery))
            foreach (var entry in Directory.EnumerateDirectories(recovery))
                if (Guid.TryParse(Path.GetFileName(entry), out var id) && !Directory.Exists(UserPath(id)))
                    Directory.Move(entry, UserPath(id));
    }

    private string UserPath(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u6807\u8BC6\u65E0\u6548\u3002"));
        return Path.Combine(_root, "Users", id.ToString("N"));
    }

    public string GetAvatarPath(Guid id) => Path.Combine(UserPath(id), "avatar.png");

    public IReadOnlyList<UserProfile> ListProfiles()
    {
        lock (_gate)
        {
            var profiles = new List<UserProfile>();
            foreach (var dir in Directory.EnumerateDirectories(Path.Combine(_root, "Users")))
            {
                if (!Guid.TryParse(Path.GetFileName(dir), out var id)) continue;
                try { profiles.Add(ReadProfile(id)); }
                catch (JsonException) { /* A damaged account must not hide healthy accounts. */ }
                catch (IOException) { }
            }
            return profiles.OrderBy(p => p.CreatedAt).ToList();
        }
    }

    private UserProfile ReadProfile(Guid id)
    {
        var profile = JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(Path.Combine(UserPath(id), "UserProfile.json")), Json)
            ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u8D44\u6599\u4E3A\u7A7A\u3002"));
        if (profile.Id != id) throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u8D44\u6599\u6807\u8BC6\u4E0D\u5339\u914D\u3002"));
        return profile;
    }

    public UserDataSnapshot Read(Guid id)
    {
        lock (_gate)
        {
            var dir = UserPath(id);
            var profile = ReadProfile(id);
            var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(Path.Combine(dir, "AppSettings.json")), Json)
                ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u8BBE\u7F6E\u4E3A\u7A7A\u3002"));
            var timetable = JsonSerializer.Deserialize<CourseScheduleData>(File.ReadAllText(Path.Combine(dir, "Timetable.json")), Json)
                ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868\u4E3A\u7A7A\u3002"));
            state.CourseSchedule = timetable;
            return new(profile, state, timetable, File.ReadAllBytes(GetAvatarPath(id)));
        }
    }

    public void Create(UserDataSnapshot snapshot)
    {
        lock (_gate)
        {
            if (Directory.Exists(UserPath(snapshot.Profile.Id))) throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u5DF2\u7ECF\u5B58\u5728\u3002"));
            WriteSnapshot(snapshot);
        }
    }

    public void SaveProfile(UserProfile profile)
    {
        lock (_gate)
        {
            var old = Read(profile.Id);
            // Identity and activation are authentication metadata, not editable profile fields.
            WriteSnapshot(old with { Profile = old.Profile with
                { Nickname = profile.Nickname, Biography = profile.Biography, UpdatedAt = DateTimeOffset.UtcNow } });
        }
    }

    public void SaveAvatar(Guid id, byte[] png)
    {
        lock (_gate) WriteSnapshot(Read(id) with { Avatar = png });
    }

    public AppState LoadAppState(Guid id) => Read(id).Settings;

    public void SaveAppState(Guid id, AppState state)
    {
        lock (_gate) WriteSnapshot(Read(id) with { Settings = state, Timetable = state.CourseSchedule });
    }

    public void ReplaceData(Guid targetId, UserDataSnapshot source)
    {
        lock (_gate)
        {
            var target = Read(targetId);
            WriteSnapshot(source with { Profile = target.Profile with
            {
                Nickname = source.Profile.Nickname, Biography = source.Profile.Biography,
                UpdatedAt = DateTimeOffset.UtcNow
            } });
        }
    }

    private void WriteSnapshot(UserDataSnapshot snapshot)
    {
        var id = snapshot.Profile.Id;
        var destination = UserPath(id);
        var stage = Path.Combine(_root, "Staging", Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(_root, "Recovery", id.ToString("N"));
        Directory.CreateDirectory(stage);
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        try
        {
            var settings = JsonSerializer.SerializeToNode(snapshot.Settings, Json)!.AsObject();
            settings.Remove(nameof(AppState.CourseSchedule));
            File.WriteAllText(Path.Combine(stage, "UserProfile.json"), JsonSerializer.Serialize(snapshot.Profile, Json));
            File.WriteAllText(Path.Combine(stage, "AppSettings.json"), settings.ToJsonString(Json));
            File.WriteAllText(Path.Combine(stage, "Timetable.json"), JsonSerializer.Serialize(snapshot.Timetable, Json));
            File.WriteAllBytes(Path.Combine(stage, "avatar.png"), snapshot.Avatar);
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            if (Directory.Exists(destination)) Directory.Move(destination, backup);
            try { Directory.Move(stage, destination); }
            catch
            {
                if (!Directory.Exists(destination) && Directory.Exists(backup)) Directory.Move(backup, destination);
                throw;
            }
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }
}
