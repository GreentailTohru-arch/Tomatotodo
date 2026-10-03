using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tomatotodo_Windows.Data;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;

namespace Tomatotodo_Windows.Accounts;

/// <summary>真实云端账户适配器。服务器始终是云端数据的权威来源；本机四文件目录仅作离线缓存。</summary>
public sealed class CloudAccountService : IAccountService
{
    private static readonly JsonSerializerOptions Json = new()
    { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    private readonly IUserDataStorageService _storage;
    private readonly ICredentialStore _credentials;
    private readonly IImageProcessingService _images;
    private readonly HttpClient _http;
    private readonly string _sessionPath;
    private readonly string _root;
    private readonly Guid _deviceId;
    private CloudToken? _token;
    private string? _deferredConflict;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    public Func<IReadOnlyList<UnifiedUserData.Conflict>, Task<bool?>>? ResolveConflicts { get; set; }
    public event Action<Guid>? DataApplied;
    public string SyncStatus { get; private set; } = global::Tomatotodo_Windows.Data.UiText.T("\u7B49\u5F85\u540C\u6B65");
    public AccountKind Kind => AccountKind.Cloud;

    private sealed record CloudToken(Guid UserId, string AccessToken, DateTimeOffset ExpiresAt, long Version);
    private sealed record ServerUser(Guid Id, string Email);
    private sealed record LoginResult(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("user")] ServerUser User);
    private sealed record SyncResult(
        [property: JsonPropertyName("user_profile")] JsonObject UserProfile,
        [property: JsonPropertyName("app_settings")] JsonObject AppSettings,
        [property: JsonPropertyName("timetable")] JsonObject Timetable,
        [property: JsonPropertyName("version")] long Version,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt = default);

    public CloudAccountService(IUserDataStorageService storage, ICredentialStore credentials,
        IImageProcessingService images, string root, HttpClient? http = null)
    {
        _storage = storage; _credentials = credentials; _images = images;
        Directory.CreateDirectory(root);
        _root = root;
        _sessionPath = Path.Combine(root, "CloudSession.dat");
        var devicePath = Path.Combine(root, "DeviceId.txt");
        if (!File.Exists(devicePath)) File.WriteAllText(devicePath, Guid.NewGuid().ToString("D"));
        if (!Guid.TryParse(File.ReadAllText(devicePath), out _deviceId))
            throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u8BBE\u5907\u6807\u8BC6\u635F\u574F\uFF0C\u8BF7\u68C0\u67E5\u8D26\u6237\u6570\u636E\u76EE\u5F55\u3002 "));
        // 不跳过 TLS 校验。当前服务器使用可信的 Let's Encrypt IP 证书。
        _http = http ?? new HttpClient(new HttpClientHandler { UseProxy = false });
        _http.BaseAddress ??= new Uri("https://47.245.57.193:8443/");
        if (_http.BaseAddress.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u4E91\u7AEF\u8D26\u6237\u5FC5\u987B\u4F7F\u7528 HTTPS\u3002 "));
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<UserProfile> RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken = default)
    {
        var email = AccountRules.NormalizeEmail(request.Email);
        AccountRules.ValidatePassword(request.Password);
        var nickname = AccountRules.ValidateNickname(request.Nickname);
        AccountRules.ValidateActivationCode(request.ActivationCode);
        using var response = await _http.PostAsJsonAsync("api/auth/register", new
        { email, password = request.Password, nickname, activation_code = request.ActivationCode }, Json, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await LoginAsync(email, request.Password, cancellationToken);
    }

    public async Task<UserProfile> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var login = await AuthenticateCoreAsync(email, password, cancellationToken);
        try
        {
            var sync = await PullCoreAsync(cancellationToken);
            if (_storage.ListProfiles().Any(p => p.Id == login.User.Id))
                _token = _token! with { Version = ReadSyncedVersion(login.User.Id) };
            else
            {
                await CacheAsync(login.User.Id, sync, cancellationToken);
                SaveSyncedVersion(login.User.Id, sync.Version);
                _token = _token! with { Version = sync.Version };
            }
            // 本地自动登录凭据只用于解锁本机缓存；网络令牌单独由 DPAPI 保护。
            await _credentials.SetAsync(login.User.Id, PasswordSecurity.Hash(password));
            return _storage.Read(login.User.Id).Profile;
        }
        catch { _token = null; throw; }
    }

    public async Task AuthenticateForMigrationAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        await AuthenticateCoreAsync(email, password, cancellationToken);
        var sync = await PullCoreAsync(cancellationToken);
        _token = _token! with { Version = sync.Version };
    }

    private async Task<LoginResult> AuthenticateCoreAsync(string email, string password, CancellationToken ct)
    {
        email = AccountRules.NormalizeEmail(email);
        using var response = await _http.PostAsJsonAsync("api/auth/login", new
        { email, password, device_id = _deviceId }, Json, ct);
        await EnsureSuccessAsync(response, ct);
        var login = await response.Content.ReadFromJsonAsync<LoginResult>(Json, ct)
            ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u670D\u52A1\u5668\u767B\u5F55\u54CD\u5E94\u4E3A\u7A7A\u3002 "));
        _token = new(login.User.Id, login.AccessToken,
            DateTimeOffset.UtcNow.AddSeconds(login.ExpiresIn - 30), 0);
        return login;
    }

    public Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_storage.Read(userId).Profile);
    }

    public async Task<UserProfile> UpdateProfileAsync(Guid userId, string nickname, string biography,
        CancellationToken cancellationToken = default)
    {
        nickname = AccountRules.ValidateNickname(nickname);
        if (biography.Length > 300) throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u4E2A\u4EBA\u7B80\u4ECB\u6700\u591A 300 \u4E2A\u5B57\u7B26\u3002 "));
        var profile = _storage.Read(userId).Profile with { Nickname = nickname, Biography = biography.Trim() };
        _storage.SaveProfile(profile);
        await SynchronizeAsync(userId, cancellationToken: cancellationToken);
        return _storage.Read(userId).Profile;
    }

    public async Task PullAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SynchronizeAsync(userId, cancellationToken: cancellationToken, manual: true);
        if (!SyncStatus.StartsWith("已同步")) throw new InvalidOperationException(SyncStatus);
    }

    public async Task PushAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SynchronizeAsync(userId, cancellationToken: cancellationToken, manual: true);
        if (!SyncStatus.StartsWith("已同步")) throw new InvalidOperationException(SyncStatus);
    }

    public async Task PushSnapshotAsync(Guid userId, UserDataSnapshot local, CancellationToken cancellationToken = default)
    {
        // Called only after the migration UI's explicit overwrite confirmation.
        await SynchronizeAsync(userId, true, local, cancellationToken);
    }

    private string BaselinePath(Guid id) => Path.Combine(_root, $"CloudSync-{id:N}.base.json");
    private string LocalPath(Guid id) => Path.Combine(_root, $"CloudSync-{id:N}.local.json");
    private static JsonObject? ReadDocument(string path) => File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() : null;
    private static void WriteDocument(string path, JsonObject doc)
    {
        File.WriteAllText(path + ".tmp", doc.ToJsonString()); File.Move(path + ".tmp", path, true);
    }
    public void AcceptImportedDocument(Guid id, AppState state)
    {
        if (state.UnifiedData is null) return;
        var previous = ReadDocument(LocalPath(id));
        if (previous?["platforms"] is JsonObject platforms) foreach (var pair in platforms)
            if (!state.UnifiedData["platforms"]!.AsObject().ContainsKey(pair.Key)) state.UnifiedData["platforms"]![pair.Key] = pair.Value?.DeepClone();
        if (previous?["shared"]?["archive"] is JsonObject old && state.UnifiedData["shared"]?["archive"] is JsonObject archive) {
            var records = archive["records"]!.AsObject();
            var deleted = (old["deleted"]!.AsArray()).Concat(archive["deleted"]!.AsArray()).Select(v => v!.ToString()).ToHashSet();
            foreach (var key in old["records"]!.AsObject().Select(p => p.Key)) if (!records.ContainsKey(key)) deleted.Add(key);
            foreach (var key in records.Select(p => p.Key).ToArray()) if (deleted.Contains(key)) {
                var row = records[key]!.DeepClone(); records.Remove(key);
                var restoredId = Guid.NewGuid(); row["Id"] = restoredId.ToString(); records[restoredId.ToString()] = row;
                var log = state.FocusLogs.FirstOrDefault(l => l.Id.ToString() == key); if (log is not null) log.Id = restoredId;
            }
            archive["deleted"] = new JsonArray(deleted.Order().Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
        }
        foreach (var pair in state.UnifiedData["shared"]!.AsObject())
            if (!UnifiedUserData.Equal(pair.Value, previous?["shared"]?[pair.Key])) state.UnifiedData["changes"]![pair.Key] = new JsonObject { ["platform"] = "windows", ["at"] = DateTimeOffset.UtcNow.ToString("O") };
        WriteDocument(LocalPath(id), state.UnifiedData);
        if (state.UnifiedData["shared"]?["profile"] is JsonObject profile) {
            var current = _storage.Read(id).Profile;
            _storage.SaveProfile(current with { Nickname = profile["Nickname"]?.ToString() ?? current.Nickname, Biography = profile["Biography"]?.ToString() ?? "" });
            if (profile["AvatarBase64"] is JsonValue avatar) _storage.SaveAvatar(id, Convert.FromBase64String(avatar.ToString()));
        }
    }
    public void TrackLocal(Guid id, AppState state)
    {
        var snapshot = _storage.Read(id);
        state.UnifiedData = UnifiedUserData.Capture(state, ReadDocument(LocalPath(id)) ?? state.UnifiedData, snapshot.Profile, snapshot.Avatar);
        WriteDocument(LocalPath(id), state.UnifiedData);
    }
    private JsonObject LocalDocument(Guid id, UserDataSnapshot? snapshot = null)
    {
        var value = snapshot ?? _storage.Read(id);
        var doc = UnifiedUserData.Capture(value.Settings, ReadDocument(LocalPath(id)) ?? value.Settings.UnifiedData, value.Profile, value.Avatar);
        if (snapshot is null) WriteDocument(LocalPath(id), doc);
        return doc;
    }
    private JsonObject RemoteDocument(SyncResult sync)
    {
        if (sync.AppSettings["format"]?.ToString() == UnifiedUserData.Format) { UnifiedUserData.Validate(sync.AppSettings); return UnifiedUserData.Clone(sync.AppSettings); }
        var legacy = sync.AppSettings.Deserialize<AppState>(Json) ?? new AppState();
        if (!sync.AppSettings.ContainsKey("Presets")) {
            var hex = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"tomatotodo:cloud-default:{_token!.UserId:D}"))).ToLowerInvariant();
            legacy.Presets = [new TaskPreset { Id = Guid.ParseExact(hex, "N"), Name = global::Tomatotodo_Windows.Data.UiText.T("\u6211\u7684\u4E00\u5929") }];
        }
        legacy.CourseSchedule = sync.Timetable.Count == 0 ? CourseScheduleCodec.EmptyTerm() : sync.Timetable.Deserialize<CourseScheduleData>(UnifiedUserData.Json)!;
        var profile = sync.UserProfile.Deserialize<UserProfile>(Json);
        var doc = UnifiedUserData.Capture(legacy, profile: profile);
        foreach (var change in doc["changes"]!.AsObject()) change.Value!["at"] = sync.UpdatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
        return doc;
    }
    public async Task SynchronizeAsync(Guid userId, bool? preferLocal = null, UserDataSnapshot? migration = null, CancellationToken cancellationToken = default, bool manual = false)
    {
        await _syncGate.WaitAsync(cancellationToken);
        try
        {
            await RefreshTokenAsync(cancellationToken);
            RequireActiveToken(userId);
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var current = LocalDocument(userId);
                var local = migration is null ? current : LocalDocument(userId, migration);
                var baseline = ReadDocument(BaselinePath(userId)) ?? UnifiedUserData.Empty();
                var remoteSnapshot = await PullCoreAsync(cancellationToken);
                var remote = RemoteDocument(remoteSnapshot);
                if (migration is not null) {
                    baseline = remote;
                    foreach (var pair in remote["platforms"]!.AsObject())
                        if (pair.Key != "windows") local["platforms"]![pair.Key] = pair.Value?.DeepClone();
                }
                var merged = UnifiedUserData.Merge(baseline, local, remote);
                if (merged.Conflicts.Count != 0)
                {
                    var signature = local["changes"]!.ToJsonString() + remote["changes"]!.ToJsonString();
                    if (!manual && preferLocal is null && signature == _deferredConflict) return;
                    bool? choice = preferLocal;
                    if (choice is null && ResolveConflicts is not null) choice = await ResolveConflicts(merged.Conflicts);
                    if (choice is null) { _deferredConflict = signature; SyncStatus = global::Tomatotodo_Windows.Data.UiText.T("\u5B58\u5728\u540C\u6B65\u51B2\u7A81\uFF0C\u7B49\u5F85\u9009\u62E9\uFF1B\u4E24\u7AEF\u6570\u636E\u5747\u5DF2\u4FDD\u7559"); return; }
                    WriteDocument(Path.Combine(_root, $"CloudSync-{userId:N}.conflict.json"), new JsonObject { ["local"] = local.DeepClone(), ["remote"] = remote.DeepClone() });
                    merged = UnifiedUserData.Merge(baseline, local, remote, choice);
                }
                if (!UnifiedUserData.Equal(current, LocalDocument(userId))) continue;
                var sync = remoteSnapshot;
                if (!UnifiedUserData.Equal(remote, merged.Document) || !UnifiedUserData.IsDocument(remoteSnapshot.AppSettings))
                {
                    var p = merged.Document["shared"]?["profile"];
                    try { sync = await PushCoreAsync(new { base_version = remoteSnapshot.Version, mode = "replace", app_settings = merged.Document,
                        timetable = merged.Document["shared"]!["timetable"], user_profile = new { Nickname = p?["Nickname"]?.ToString() ?? _storage.Read(userId).Profile.Nickname, Biography = p?["Biography"]?.ToString() ?? "" } }, cancellationToken); }
                    catch (SyncConflictException) { continue; }
                }
                if (!UnifiedUserData.Equal(current, LocalDocument(userId))) {
                    var latest = LocalDocument(userId);
                    var rebased = UnifiedUserData.Merge(current, latest, merged.Document, true).Document;
                    await CacheAsync(userId, sync with { AppSettings = rebased }, cancellationToken);
                    WriteDocument(LocalPath(userId), rebased);
                    WriteDocument(BaselinePath(userId), merged.Document);
                    DataApplied?.Invoke(userId);
                    continue;
                }
                var document = merged.Document;
                // Use the reconciled document even when no push was necessary.
                await CacheAsync(userId, sync with { AppSettings = document }, cancellationToken);
                WriteDocument(BaselinePath(userId), document);
                WriteDocument(LocalPath(userId), document);
                _token = _token! with { Version = sync.Version };
                SaveSyncedVersion(userId, sync.Version);
                await PersistIfPresentAsync();
                SyncStatus = global::Tomatotodo_Windows.Data.UiText.F("\u5DF2\u540C\u6B65 \u00B7 {0}", $"{DateTime.Now:HH:mm}");
                if (!UnifiedUserData.Equal(current, document)) DataApplied?.Invoke(userId);
                return;
            }
            SyncStatus = global::Tomatotodo_Windows.Data.UiText.T("\u6570\u636E\u4ECD\u5728\u4FEE\u6539\uFF0C\u7A0D\u540E\u91CD\u8BD5");
        }
        catch { SyncStatus = global::Tomatotodo_Windows.Data.UiText.T("\u6682\u672A\u540C\u6B65\uFF1B\u672C\u673A\u4FEE\u6539\u5DF2\u4FDD\u7559\uFF0C\u5C06\u81EA\u52A8\u91CD\u8BD5"); throw; }
        finally { _syncGate.Release(); }
    }
    private sealed class SyncConflictException : Exception { }

    public async Task SaveRememberedTokenAsync(bool remember, CancellationToken cancellationToken = default)
    {
        if (!remember || _token is null) { if (File.Exists(_sessionPath)) File.Delete(_sessionPath); return; }
        var clear = CryptographicBuffer.CreateFromByteArray(JsonSerializer.SerializeToUtf8Bytes(_token, Json));
        var protectedData = await new DataProtectionProvider("LOCAL=user").ProtectAsync(clear);
        CryptographicBuffer.CopyToByteArray(protectedData, out var bytes);
        await File.WriteAllBytesAsync(_sessionPath, bytes, cancellationToken);
    }

    public async Task<bool> RestoreAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_sessionPath)) return false;
        try
        {
            var bytes = await File.ReadAllBytesAsync(_sessionPath, cancellationToken);
            var clear = await new DataProtectionProvider().UnprotectAsync(CryptographicBuffer.CreateFromByteArray(bytes));
            CryptographicBuffer.CopyToByteArray(clear, out var json);
            _token = JsonSerializer.Deserialize<CloudToken>(json, Json);
            if (_token?.UserId != userId || _token.ExpiresAt <= DateTimeOffset.UtcNow) return false;
            var sync = await PullCoreAsync(cancellationToken);
            if (_storage.ListProfiles().Any(p => p.Id == userId))
                _token = _token with { Version = ReadSyncedVersion(userId) };
            else
            {
                await CacheAsync(userId, sync, cancellationToken);
                SaveSyncedVersion(userId, sync.Version);
                _token = _token with { Version = sync.Version };
            }
            await PersistIfPresentAsync();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return _token?.UserId == userId && _storage.ListProfiles().Any(p => p.Id == userId); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException)
        { _token = null; return false; }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_token is { } token)
        {
            try
            {
                using var request = Authorized(HttpMethod.Post, "api/auth/logout", token.AccessToken);
                using var response = await _http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException) { /* 本地退出不能因网络故障而被阻断。 */ }
            catch (TaskCanceledException) { }
        }
        _token = null;
        if (File.Exists(_sessionPath)) File.Delete(_sessionPath);
    }

    private CloudToken RequireActiveToken(Guid userId)
    {
        if (_token is not { } token || token.UserId != userId || token.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u4E91\u7AEF\u4F1A\u8BDD\u5DF2\u8FC7\u671F\uFF0C\u8BF7\u91CD\u65B0\u767B\u5F55\u3002 "));
        return token;
    }

    private CloudToken RequireToken(Guid userId)
    {
        var token = RequireActiveToken(userId);
        if (token.Version < 0)
            throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u6B64\u8D26\u6237\u7684\u672C\u673A\u6570\u636E\u6CA1\u6709\u540C\u6B65\u57FA\u7EBF\u3002\u8BF7\u5148\u5907\u4EFD\u9700\u8981\u4FDD\u7559\u7684\u6570\u636E\uFF0C\u518D\u4ECE\u4E91\u7AEF\u62C9\u53D6\u3002 "));
        return token;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        if (_token is not { } token || token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5) || token.ExpiresAt <= DateTimeOffset.UtcNow) return;
        using var request = Authorized(HttpMethod.Post, "api/auth/refresh", token.AccessToken);
        request.Content = JsonContent.Create(new { });
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return;
        await EnsureSuccessAsync(response, ct);
        var renewed = await response.Content.ReadFromJsonAsync<LoginResult>(Json, ct) ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u4F1A\u8BDD\u5237\u65B0\u54CD\u5E94\u4E3A\u7A7A\u3002"));
        if (renewed.User.Id != token.UserId) throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u4F1A\u8BDD\u8D26\u6237\u4E0D\u5339\u914D\u3002"));
        _token = token with { AccessToken = renewed.AccessToken, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(renewed.ExpiresIn - 30) };
        await PersistIfPresentAsync();
    }

    private async Task<SyncResult> PullCoreAsync(CancellationToken ct)
    {
        var token = _token ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5148\u767B\u5F55\u4E91\u7AEF\u8D26\u6237\u3002 "));
        using var request = Authorized(HttpMethod.Get, "api/sync/pull", token.AccessToken);
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<SyncResult>(Json, ct)
            ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u670D\u52A1\u5668\u540C\u6B65\u54CD\u5E94\u4E3A\u7A7A\u3002 "));
    }

    private async Task<SyncResult> PushCoreAsync<T>(T payload, CancellationToken ct)
    {
        var token = _token ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5148\u767B\u5F55\u4E91\u7AEF\u8D26\u6237\u3002 "));
        using var request = Authorized(HttpMethod.Post, "api/sync/push", token.AccessToken);
        request.Content = JsonContent.Create(payload, options: Json);
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<SyncResult>(Json, ct)
            ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u670D\u52A1\u5668\u540C\u6B65\u54CD\u5E94\u4E3A\u7A7A\u3002 "));
    }

    private async Task CacheAsync(Guid userId, SyncResult sync, CancellationToken ct)
    {
        var profile = sync.UserProfile.Deserialize<UserProfile>(Json)
            ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u4E91\u7AEF\u4E2A\u4EBA\u8D44\u6599\u65E0\u6548\u3002 "));
        if (profile.Id != userId || profile.Kind != AccountKind.Cloud)
            throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u4E91\u7AEF\u8D26\u6237\u6807\u8BC6\u4E0D\u5339\u914D\u3002 "));
        var existing = _storage.ListProfiles().FirstOrDefault(p => p.Id == userId);
        var avatar = existing is null ? await _images.CreateDefaultAvatarAsync(ct) : _storage.Read(userId).Avatar;
        var document = RemoteDocument(sync);
        var settings = UnifiedUserData.Apply(document, existing is null ? null : _storage.Read(userId).Settings);
        var timetable = sync.Timetable.Count == 0 ? CourseScheduleCodec.EmptyTerm() :
            sync.Timetable.Deserialize<CourseScheduleData>(Json) ?? CourseScheduleCodec.EmptyTerm();
        timetable = settings.CourseSchedule;
        if (document["shared"]?["profile"] is JsonObject personal)
        {
            profile = profile with { Nickname = personal["Nickname"]?.ToString() ?? profile.Nickname, Biography = personal["Biography"]?.ToString() ?? profile.Biography };
            if (personal["AvatarBase64"] is JsonValue encoded) avatar = Convert.FromBase64String(encoded.ToString());
        }
        var snapshot = new UserDataSnapshot(profile, settings, timetable, avatar);
        if (existing is null) _storage.Create(snapshot);
        else _storage.ReplaceData(userId, snapshot);
        if (existing is null) { WriteDocument(BaselinePath(userId), document); WriteDocument(LocalPath(userId), document); }
    }

    private async Task PersistIfPresentAsync() { if (File.Exists(_sessionPath)) await SaveRememberedTokenAsync(true); }

    private string VersionPath(Guid userId) => Path.Combine(_root, $"CloudSync-{userId:N}.version");

    private long ReadSyncedVersion(Guid userId) =>
        File.Exists(VersionPath(userId)) && long.TryParse(File.ReadAllText(VersionPath(userId)), out var version)
            ? version : -1;

    private void SaveSyncedVersion(Guid userId, long version)
    {
        var path = VersionPath(userId);
        var temp = path + ".tmp";
        File.WriteAllText(temp, version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.Move(temp, path, true);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string detail;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
            detail = body?["detail"]?.ToString() ?? global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u6C42\u5931\u8D25");
        }
        catch (JsonException) { detail = global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u6C42\u5931\u8D25"); }
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new SyncConflictException();
        throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.F("\u4E91\u7AEF\u670D\u52A1\u8FD4\u56DE {0}\uFF1A{1}", (int)response.StatusCode, detail));
    }
}
