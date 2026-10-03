using System.Security.Cryptography;
using Tomatotodo_Windows.Data;

namespace Tomatotodo_Windows.Accounts;

public sealed class LocalAccountService : IAccountService
{
    private readonly IUserDataStorageService _storage;
    private readonly ICredentialStore _credentials;
    private readonly IImageProcessingService _images;
    private readonly SemaphoreSlim _registrationGate = new(1, 1);
    private readonly Dictionary<string, (int Count, DateTimeOffset Until)> _failures = new();
    public AccountKind Kind { get; }

    public LocalAccountService(IUserDataStorageService storage, ICredentialStore credentials,
        IImageProcessingService images, AccountKind kind = AccountKind.Local)
    { _storage = storage; _credentials = credentials; _images = images; Kind = kind; }

    public async Task<UserProfile> RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken = default)
    {
        var email = AccountRules.NormalizeEmail(request.Email);
        AccountRules.ValidatePassword(request.Password);
        var nickname = AccountRules.ValidateNickname(request.Nickname);
        if (Kind == AccountKind.Cloud) AccountRules.ValidateActivationCode(request.ActivationCode);
        await _registrationGate.WaitAsync(cancellationToken);
        try
        {
            using var registrationLease = new DeviceAccountLease(@"Global\Tomatotodo.AccountRegistration.v1");
            registrationLease.Acquire();
            if (Kind == AccountKind.Cloud && _storage.ListProfiles().Any(p => p.Kind == AccountKind.Cloud &&
                string.Equals(p.ActivationCode, request.ActivationCode, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u6B64\u6FC0\u6D3B\u7801\u5DF2\u6CE8\u518C\u8FC7\u8D26\u6237\uFF0C\u4E0D\u80FD\u91CD\u590D\u4F7F\u7528\u3002"));
            if (_storage.ListProfiles().Any(p => p.Kind == Kind && p.Email == email))
                throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u6B64\u90AE\u7BB1\u5DF2\u6709\u8D26\u6237\uFF0C\u8BF7\u76F4\u63A5\u767B\u5F55\u3002"));
            var profile = new UserProfile { Email = email, Kind = Kind, Nickname = nickname,
                ActivationCode = Kind == AccountKind.Cloud ? request.ActivationCode : null,
                IsCloudPlaceholder = Kind == AccountKind.Cloud,
                CloudIdentity = Kind == AccountKind.Cloud ? "mock-" + Guid.NewGuid().ToString("N") : null };
            var hash = await Task.Run(() => PasswordSecurity.Hash(request.Password), cancellationToken);
            var avatar = await _images.CreateDefaultAvatarAsync(cancellationToken);
            await _credentials.SetAsync(profile.Id, hash);
            try
            {
                _storage.Create(new UserDataSnapshot(profile, new AppState(), CourseScheduleCodec.EmptyTerm(), avatar));
            }
            catch { await _credentials.RemoveAsync(profile.Id); throw; }
            return profile;
        }
        finally { _registrationGate.Release(); }
    }

    public async Task<UserProfile> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        email = AccountRules.NormalizeEmail(email);
        if (_failures.TryGetValue(email, out var attempt) && attempt.Until > DateTimeOffset.UtcNow)
            throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u5C1D\u8BD5\u6B21\u6570\u8F83\u591A\uFF0C\u8BF7\u7A0D\u540E\u518D\u8BD5\u3002"));
        var profile = _storage.ListProfiles().FirstOrDefault(p => p.Kind == Kind && p.Email == email);
        var credential = profile is null ? null : await _credentials.GetAsync(profile.Id);
        var valid = credential is not null && await Task.Run(() => PasswordSecurity.Verify(password, credential), cancellationToken);
        if (!valid || profile is null)
        {
            var count = attempt.Count + 1;
            _failures[email] = (count, count >= 5 ? DateTimeOffset.UtcNow.AddSeconds(30) : DateTimeOffset.MinValue);
            throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u90AE\u7BB1\u6216\u5BC6\u7801\u4E0D\u6B63\u786E\u3002"));
        }
        _failures.Remove(email);
        return profile;
    }

    public Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profile = _storage.Read(userId).Profile;
        if (profile.Kind != Kind) throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u7C7B\u578B\u4E0D\u5339\u914D\u3002"));
        return Task.FromResult(profile);
    }

    public async Task<UserProfile> UpdateProfileAsync(Guid userId, string nickname, string biography, CancellationToken cancellationToken = default)
    {
        nickname = AccountRules.ValidateNickname(nickname);
        if (biography.Length > 300) throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u4E2A\u4EBA\u7B80\u4ECB\u6700\u591A 300 \u4E2A\u5B57\u7B26\u3002"));
        var profile = await GetProfileAsync(userId, cancellationToken);
        profile = profile with { Nickname = nickname, Biography = biography.Trim(), UpdatedAt = DateTimeOffset.UtcNow };
        _storage.SaveProfile(profile);
        return profile;
    }
}

/// <summary>Entirely local mock. No network, activation validation, or email verification is claimed.</summary>
public sealed class MockCloudAccountService(IAccountService localPlaceholder) : IAccountService
{
    public AccountKind Kind => AccountKind.Cloud;
    public async Task<UserProfile> RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken = default)
    { await Task.Delay(500, cancellationToken); return await localPlaceholder.RegisterAsync(request, cancellationToken); }
    public async Task<UserProfile> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    { await Task.Delay(350, cancellationToken); return await localPlaceholder.LoginAsync(email, password, cancellationToken); }
    public Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default) => localPlaceholder.GetProfileAsync(userId, cancellationToken);
    public Task<UserProfile> UpdateProfileAsync(Guid userId, string nickname, string biography, CancellationToken cancellationToken = default) =>
        localPlaceholder.UpdateProfileAsync(userId, nickname, biography, cancellationToken);
}

public sealed class MockAccountMigrationService(IUserDataStorageService storage) : IAccountMigrationService
{
    public async Task MigrateAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        if (sourceId == targetId) throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u4E0D\u80FD\u8FC1\u79FB\u5230\u8D26\u6237\u81EA\u8EAB\u3002"));
        var source = storage.Read(sourceId);
        var target = storage.Read(targetId);
        if (source.Profile.Kind == target.Profile.Kind) throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u9009\u62E9\u53E6\u4E00\u79CD\u7C7B\u578B\u7684\u76EE\u6807\u8D26\u6237\u3002"));
        await Task.Delay(650, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        storage.ReplaceData(targetId, source);
    }
}

public sealed class CloudAccountMigrationService(IUserDataStorageService storage, CloudAccountService cloud)
    : IAccountMigrationService
{
    public async Task MigrateAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        if (sourceId == targetId) throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u4E0D\u80FD\u8FC1\u79FB\u5230\u8D26\u6237\u81EA\u8EAB\u3002 "));
        var source = storage.Read(sourceId);
        var target = storage.Read(targetId);
        if (source.Profile.Kind == target.Profile.Kind)
            throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u9009\u62E9\u53E6\u4E00\u79CD\u7C7B\u578B\u7684\u76EE\u6807\u8D26\u6237\u3002 "));
        if (target.Profile.Kind == AccountKind.Cloud)
        {
            // 先得到云端明确成功响应，之后才覆盖该账户的本机缓存。
            await cloud.PushSnapshotAsync(targetId, source, cancellationToken);
        }
        else
        {
            await cloud.PullAsync(sourceId, cancellationToken);
            storage.ReplaceData(targetId, storage.Read(sourceId));
        }
    }
}

public sealed class AccountSession(ICredentialStore credentials, IUserDataStorageService storage, IAccountWorkspace workspace,
    DeviceAccountLease? deviceLease = null, Func<Guid, Task<bool>>? validateCloudSession = null,
    Func<Task>? cloudLogout = null) : IDisposable
{
    private readonly DeviceAccountLease _deviceLease = deviceLease ?? new();
    public UserProfile? Current { get; private set; }
    public event Action? Changed;

    public async Task SignInAsync(UserProfile profile, bool remember, bool autoLogin, string? password = null)
    {
        if (Current is not null && Current.Id != profile.Id)
            throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5148\u9000\u51FA\u5F53\u524D\u8D26\u6237\uFF0C\u518D\u767B\u5F55\u53E6\u4E00\u4E2A\u8D26\u6237\u3002"));
        _deviceLease.Acquire();
        try
        {
        // Remembered passwords remain exclusively in the current-user DPAPI vault.
        await credentials.SaveLoginAsync(remember ? new(profile.Email, profile.Kind, password) : null);
        await RevokeAutoLoginAsync();
        if (autoLogin)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var credential = await credentials.GetAsync(profile.Id) ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u672C\u5730\u51ED\u636E\u4E0D\u53EF\u7528\u3002"));
            await credentials.SetAsync(profile.Id, credential with { SessionHash = PasswordSecurity.TokenHash(token) });
            await credentials.SaveSessionAsync(new(profile.Id, profile.Kind, token, DateTimeOffset.UtcNow.AddDays(30)));
        }
        workspace.SwitchAccount(profile.Id);
        Current = profile;
        Changed?.Invoke();
        }
        catch { if (Current is null) _deviceLease.Dispose(); throw; }
    }

    public async Task RestoreAsync()
    {
        var session = await credentials.ReadSessionAsync();
        if (session is null) return;
        var credential = await credentials.GetAsync(session.UserId);
        if (session.ExpiresAt <= DateTimeOffset.UtcNow || credential?.SessionHash is null ||
            !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(credential.SessionHash),
                System.Text.Encoding.UTF8.GetBytes(PasswordSecurity.TokenHash(session.Token))))
        { await RevokeAutoLoginAsync(); return; }
        var profile = storage.Read(session.UserId).Profile;
        if (profile.Kind != session.Kind) { await RevokeAutoLoginAsync(); return; }
        if (profile.Kind == AccountKind.Cloud &&
            (validateCloudSession is null || !await validateCloudSession(profile.Id)))
        { await RevokeAutoLoginAsync(); return; }
        _deviceLease.Acquire();
        try { workspace.SwitchAccount(profile.Id); Current = profile; Changed?.Invoke(); }
        catch { if (Current is null) _deviceLease.Dispose(); throw; }
    }

    private async Task RevokeAutoLoginAsync()
    {
        var remembered = await credentials.ReadSessionAsync();
        if (remembered is not null && await credentials.GetAsync(remembered.UserId) is { } previous)
            await credentials.SetAsync(remembered.UserId, previous with { SessionHash = null });
        await credentials.SaveSessionAsync(null);
    }

    public async Task SignOutAsync()
    {
        if (Current?.Kind == AccountKind.Cloud && cloudLogout is not null) await cloudLogout();
        await RevokeAutoLoginAsync();
        workspace.SwitchAccount(null);
        Current = null; _deviceLease.Dispose(); Changed?.Invoke();
    }

    public void Dispose() => _deviceLease.Dispose();

    public void RefreshProfile()
    {
        if (Current is not null) Current = storage.Read(Current.Id).Profile;
        Changed?.Invoke();
    }
}
