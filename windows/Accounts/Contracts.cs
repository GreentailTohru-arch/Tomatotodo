using Tomatotodo_Windows.Data;

namespace Tomatotodo_Windows.Accounts;

public interface IAccountService
{
    AccountKind Kind { get; }
    Task<UserProfile> RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken = default);
    Task<UserProfile> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserProfile> UpdateProfileAsync(Guid userId, string nickname, string biography, CancellationToken cancellationToken = default);
}

public interface IUserDataStorageService
{
    IReadOnlyList<UserProfile> ListProfiles();
    UserDataSnapshot Read(Guid userId);
    void Create(UserDataSnapshot snapshot);
    void SaveProfile(UserProfile profile);
    void SaveAvatar(Guid userId, byte[] png);
    AppState LoadAppState(Guid userId);
    void SaveAppState(Guid userId, AppState state);
    void ReplaceData(Guid targetId, UserDataSnapshot source);
    string GetAvatarPath(Guid userId);
}

public interface IImageProcessingService
{
    Task<byte[]> ProcessAvatarAsync(AvatarSelection selection, CancellationToken cancellationToken = default);
    Task<byte[]> CreateDefaultAvatarAsync(CancellationToken cancellationToken = default);
}

public interface ICredentialStore
{
    Task<CredentialRecord?> GetAsync(Guid id);
    Task SetAsync(Guid id, CredentialRecord credential);
    Task RemoveAsync(Guid id);
    Task<RememberedSession?> ReadSessionAsync();
    Task SaveSessionAsync(RememberedSession? session);
    Task<RememberedLogin?> ReadLoginAsync();
    Task SaveLoginAsync(RememberedLogin? login);
}

public interface IAccountInteractionService
{
    Task<AvatarSelection?> PickAvatarAsync();
    Task<bool> ConfirmMigrationAsync(UserProfile source, UserProfile target);
    Task<bool> ConfirmCloudPullAsync(UserProfile account) => Task.FromResult(false);
}

public interface IAccountMigrationService
{
    Task MigrateAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default);
}

public interface IAccountWorkspace
{
    // The view adapter commits focus and saves the source before changing the storage route.
    void SwitchAccount(Guid? userId);
    void FlushCurrentAccount();
    void ReloadCurrentAccount(Guid userId) => throw new NotSupportedException(global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u5DE5\u4F5C\u533A\u4E0D\u652F\u6301\u539F\u4F4D\u91CD\u8F7D\u3002 "));
}
