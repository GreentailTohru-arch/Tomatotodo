using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tomatotodo_Windows.Accounts;

public sealed class AccountViewModel : ObservableObject, IDisposable
{
    private readonly Func<AccountKind, IAccountService> _accounts;
    private readonly IUserDataStorageService _storage;
    private readonly IImageProcessingService _images;
    private readonly IAccountMigrationService _migration;
    private readonly ICredentialStore _credentials;
    private readonly IAccountInteractionService _interaction;
    private readonly IAccountWorkspace _workspace;
    private readonly AccountSession _session;
    private readonly CloudAccountService? _cloud;
    private bool _busy, _registration, _remember, _autoLogin, _editingProfile;
    private int _mode;
    private string _email = "", _password = "", _confirmPassword = "", _nickname = "", _biography = "", _activationCode = "", _status = "", _targetPassword = "";
    private UserProfile? _selectedTarget;
    private byte[]? _avatar;

    public AccountViewModel(Func<AccountKind, IAccountService> accounts, IUserDataStorageService storage,
        IImageProcessingService images, IAccountMigrationService migration, ICredentialStore credentials,
        IAccountInteractionService interaction, IAccountWorkspace workspace, AccountSession session)
    {
        _accounts = accounts; _storage = storage; _images = images; _migration = migration;
        _credentials = credentials; _interaction = interaction; _workspace = workspace; _session = session;
        _cloud = accounts(AccountKind.Cloud) as CloudAccountService;
        SubmitCommand = new AsyncRelayCommand(() => RunAsync(SubmitAsync));
        ToggleRegistrationCommand = new RelayCommand(() => { IsRegistration = !IsRegistration; Password = ConfirmPassword = ""; Status = ""; });
        SaveProfileCommand = new AsyncRelayCommand(() => RunAsync(SaveProfileAsync));
        EditProfileCommand = new RelayCommand(() => IsEditingProfile = true);
        CancelEditCommand = new RelayCommand(() => { Refresh(); IsEditingProfile = false; });
        ChangeAvatarCommand = new AsyncRelayCommand(() => RunAsync(ChangeAvatarAsync));
        LogoutCommand = new AsyncRelayCommand(() => RunAsync(async () => { await _session.SignOutAsync(); await RestoreRememberedLoginAsync(); Status = global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u9000\u51FA\u767B\u5F55\uFF0C\u5DF2\u8FD4\u56DE\u672A\u767B\u5F55\u65F6\u7684\u6570\u636E\u3002"); }));
        MigrateCommand = new AsyncRelayCommand(() => RunAsync(MigrateAsync));
        PullCloudCommand = new AsyncRelayCommand(() => RunAsync(PullCloudAsync));
        PushCloudCommand = new AsyncRelayCommand(() => RunAsync(PushCloudAsync));
        _session.Changed += Refresh;
        Refresh();
    }

    public IAsyncRelayCommand SubmitCommand { get; }
    public IRelayCommand ToggleRegistrationCommand { get; }
    public IAsyncRelayCommand SaveProfileCommand { get; }
    public IRelayCommand EditProfileCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public bool IsEditingProfile { get => _editingProfile; private set { if (SetProperty(ref _editingProfile, value)) OnPropertyChanged(nameof(IsViewingProfile)); } }
    public bool IsViewingProfile => !IsEditingProfile;
    public string ProfileBiography => string.IsNullOrWhiteSpace(_session.Current?.Biography) ? global::Tomatotodo_Windows.Data.UiText.T("\u5C1A\u672A\u586B\u5199\u4E2A\u4EBA\u7B80\u4ECB") : _session.Current.Biography;
    public bool PasswordMismatch => IsRegistration && ConfirmPassword.Length > 0 && Password != ConfirmPassword;
    public IAsyncRelayCommand ChangeAvatarCommand { get; }
    public IAsyncRelayCommand LogoutCommand { get; }
    public IAsyncRelayCommand MigrateCommand { get; }
    public IAsyncRelayCommand PullCloudCommand { get; }
    public IAsyncRelayCommand PushCloudCommand { get; }
    public ObservableCollection<UserProfile> MigrationTargets { get; } = [];
    public IReadOnlyList<string> EmailSuffixes { get; } = ["@gmail.com", "@outlook.com", "@qq.com", "@163.com"];
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) OnPropertyChanged(nameof(IsReady)); } }
    public bool IsReady => !IsBusy;
    public bool IsSignedIn => _session.Current is not null;
    public bool IsSignedOut => !IsSignedIn;
    public bool IsCloudSignedIn => _session.Current?.Kind == AccountKind.Cloud;
    public bool IsRegistration { get => _registration; set { if (SetProperty(ref _registration, value)) NotifyForm(); } }
    public bool IsCloud => SelectedMode == 1;
    public bool ShowActivation => IsRegistration && IsCloud;
    public string SubmitLabel => IsRegistration ? (IsCloud ? global::Tomatotodo_Windows.Data.UiText.T("\u521B\u5EFA\u4E91\u7AEF\u8D26\u6237") : global::Tomatotodo_Windows.Data.UiText.T("\u521B\u5EFA\u672C\u5730\u8D26\u6237")) : global::Tomatotodo_Windows.Data.UiText.T("\u767B\u5F55");
    public string ToggleLabel => IsRegistration ? global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u6709\u8D26\u6237\uFF1F\u8FD4\u56DE\u767B\u5F55") : global::Tomatotodo_Windows.Data.UiText.T("\u6CA1\u6709\u8D26\u6237\uFF1F\u7ACB\u5373\u6CE8\u518C");
    public int SelectedMode { get => _mode; set { if (SetProperty(ref _mode, value)) { NotifyForm(); Password = ConfirmPassword = ""; Status = ""; } } }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string? SelectedSuffix { get => null; set { if (value is not null) Email = Email.Split('@')[0].Trim() + value; OnPropertyChanged(); } }
    public string Password { get => _password; set { if (SetProperty(ref _password, value)) { OnPropertyChanged(nameof(PasswordStrength)); OnPropertyChanged(nameof(PasswordHint)); OnPropertyChanged(nameof(PasswordMismatch)); } } }
    public string ConfirmPassword { get => _confirmPassword; set { if (SetProperty(ref _confirmPassword, value)) OnPropertyChanged(nameof(PasswordMismatch)); } }
    public int PasswordStrength => AccountRules.PasswordScore(Password) * 20;
    public string PasswordHint => Password.Length == 0 ? global::Tomatotodo_Windows.Data.UiText.T("8\u2013128 \u4F4D\uFF0C\u5305\u542B\u5927\u5C0F\u5199\u5B57\u6BCD\u3001\u6570\u5B57\u548C\u7279\u6B8A\u5B57\u7B26") :
        PasswordStrength == 100 && Password.Length <= 128 ? global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u6EE1\u8DB3\u5BC6\u7801\u8981\u6C42") : global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u8865\u9F50\uFF1A\u81F3\u5C11 8 \u4F4D\u3001\u5927\u5C0F\u5199\u5B57\u6BCD\u3001\u6570\u5B57\u3001\u7279\u6B8A\u5B57\u7B26");
    public string Nickname { get => _nickname; set => SetProperty(ref _nickname, value); }
    public string Biography { get => _biography; set => SetProperty(ref _biography, value); }
    public string ActivationCode { get => _activationCode; set => SetProperty(ref _activationCode, AccountRules.FormatActivationCode(value)); }
    public bool RememberMe { get => _remember; set { if (SetProperty(ref _remember, value) && !value) AutoLogin = false; } }
    public bool AutoLogin { get => _autoLogin; set { if (SetProperty(ref _autoLogin, value) && value) RememberMe = true; } }
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public string IdentityLabel => _session.Current is { } profile ? $"{profile.Email} · {(profile.Kind == AccountKind.Local ? "本地账户" : "云端账户")}" : "";
    public string MigrationDescription => _session.Current?.Kind == AccountKind.Cloud ? global::Tomatotodo_Windows.Data.UiText.T("\u5C06\u6B64\u4E91\u7AEF\u8D26\u6237\u7684\u6570\u636E\u8986\u76D6\u5230\u672C\u5730\u8D26\u6237\u3002") : global::Tomatotodo_Windows.Data.UiText.T("\u5C06\u6B64\u672C\u5730\u8D26\u6237\u7684\u6570\u636E\u8986\u76D6\u5230\u4E91\u7AEF\u8D26\u6237\u3002");
    public bool HasMigrationTargets => MigrationTargets.Count > 0;
    public byte[]? Avatar { get => _avatar; private set => SetProperty(ref _avatar, value); }
    public UserProfile? SelectedTarget { get => _selectedTarget; set { if (SetProperty(ref _selectedTarget, value)) TargetPassword = ""; } }
    public string TargetPassword { get => _targetPassword; set => SetProperty(ref _targetPassword, value); }

    public async Task InitializeAsync()
    {
        await RunAsync(RestoreRememberedLoginAsync);
    }

    private async Task RestoreRememberedLoginAsync()
    {
        if (!IsSignedIn && await _credentials.ReadLoginAsync() is { } saved)
        { SelectedMode = saved.Kind == AccountKind.Cloud ? 1 : 0; Email = saved.Email; RememberMe = true; Password = saved.Password ?? ""; }
    }

    private void NotifyForm()
    {
        OnPropertyChanged(nameof(IsCloud)); OnPropertyChanged(nameof(ShowActivation));
        OnPropertyChanged(nameof(PasswordMismatch));
        OnPropertyChanged(nameof(SubmitLabel)); OnPropertyChanged(nameof(ToggleLabel));
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Status = "";
        try { await action(); }
        catch (Exception ex) { Status = ex switch
        {
            ArgumentException or InvalidOperationException => ex.Message,
            HttpRequestException or TaskCanceledException => global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u8FDE\u63A5\u4E91\u7AEF\u670D\u52A1\u5668\uFF0C\u8BF7\u68C0\u67E5\u7F51\u7EDC\u540E\u91CD\u8BD5\u3002"),
            _ => global::Tomatotodo_Windows.Data.UiText.T("\u64CD\u4F5C\u672A\u5B8C\u6210\uFF0C\u8BF7\u68C0\u67E5\u6587\u4EF6\u6743\u9650\u6216\u7A0D\u540E\u91CD\u8BD5\u3002")
        }; }
        finally { IsBusy = false; }
    }

    private async Task SubmitAsync()
    {
        var service = _accounts(IsCloud ? AccountKind.Cloud : AccountKind.Local);
        UserProfile profile;
        if (IsRegistration)
        {
            if (Password != ConfirmPassword) throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u4E24\u6B21\u8F93\u5165\u7684\u5BC6\u7801\u4E0D\u4E00\u81F4\u3002"));
            profile = await service.RegisterAsync(new(Email, Password, Nickname, ActivationCode));
        }
        else profile = await service.LoginAsync(Email, Password);
        await _session.SignInAsync(profile, RememberMe, AutoLogin, Password);
        if (profile.Kind == AccountKind.Cloud && _cloud is not null)
            await _cloud.SaveRememberedTokenAsync(AutoLogin);
        Password = ConfirmPassword = "";
        Status = profile.Kind == AccountKind.Cloud ? global::Tomatotodo_Windows.Data.UiText.T("\u4E91\u7AEF\u767B\u5F55\u6210\u529F\uFF0C\u4FEE\u6539\u5C06\u81EA\u52A8\u540C\u6B65\uFF1B\u5982\u6709\u51B2\u7A81\u4F1A\u663E\u793A\u4E24\u7AEF\u4FEE\u6539\u65F6\u95F4\u5E76\u8BF7\u4F60\u9009\u62E9\u3002") : global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u767B\u5F55\u672C\u5730\u8D26\u6237\u3002");
    }

    private async Task SaveProfileAsync()
    {
        var current = _session.Current ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5148\u767B\u5F55\u3002"));
        await _accounts(current.Kind).UpdateProfileAsync(current.Id, Nickname, Biography);
        _session.RefreshProfile(); IsEditingProfile = false; Status = global::Tomatotodo_Windows.Data.UiText.T("\u4E2A\u4EBA\u8D44\u6599\u5DF2\u4FDD\u5B58\u3002");
    }

    private async Task ChangeAvatarAsync()
    {
        var current = _session.Current ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5148\u767B\u5F55\u3002"));
        var selected = await _interaction.PickAvatarAsync();
        if (selected is null) return;
        var bytes = await _images.ProcessAvatarAsync(selected);
        _storage.SaveAvatar(current.Id, bytes);
        Avatar = bytes; _session.RefreshProfile(); Status = current.Kind == AccountKind.Cloud
            ? global::Tomatotodo_Windows.Data.UiText.T("\u5934\u50CF\u5DF2\u4FDD\u5B58\uFF0C\u5C06\u968F\u7528\u6237\u6570\u636E\u81EA\u52A8\u540C\u6B65\u3002")
            : global::Tomatotodo_Windows.Data.UiText.T("\u5934\u50CF\u5DF2\u5C45\u4E2D\u88C1\u526A\u5E76\u4FDD\u5B58\u4E3A 256 \u00D7 256 PNG\u3002");
    }

    private async Task MigrateAsync()
    {
        var current = _session.Current ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5148\u767B\u5F55\u3002"));
        var target = SelectedTarget ?? throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u9009\u62E9\u8FC1\u79FB\u76EE\u6807\u8D26\u6237\u3002"));
        // Authenticate ownership before showing the destructive confirmation.
        if (target.Kind == AccountKind.Cloud && _cloud is not null)
            await _cloud.AuthenticateForMigrationAsync(target.Email, TargetPassword);
        else
            await _accounts(target.Kind).LoginAsync(target.Email, TargetPassword);
        TargetPassword = "";
        try
        {
            if (!await _interaction.ConfirmMigrationAsync(current, target)) { Status = global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u53D6\u6D88\u8FC1\u79FB\u3002"); return; }
            _workspace.FlushCurrentAccount();
            await _migration.MigrateAsync(current.Id, target.Id);
            Refresh(); Status = global::Tomatotodo_Windows.Data.UiText.T("\u8FC1\u79FB\u5B8C\u6210\u3002\u76EE\u6807\u7684\u8BBE\u7F6E\u3001\u8BFE\u7A0B\u8868\u548C\u8D44\u6599\u5DF2\u8986\u76D6\uFF1B\u5934\u50CF\u4F1A\u4E00\u5E76\u540C\u6B65\uFF0C\u767B\u5F55\u51ED\u636E\u4E0D\u53D8\u3002");
        }
        finally
        {
            // 当前是本地账户时，临时登录云端仅用于验证迁移目标，结束后立刻撤销。
            if (target.Kind == AccountKind.Cloud && _cloud is not null) await _cloud.LogoutAsync();
        }
    }

    private async Task PullCloudAsync()
    {
        var current = _session.Current ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5148\u767B\u5F55\u3002 "));
        if (current.Kind != AccountKind.Cloud || _cloud is null) return;
        if (!await _interaction.ConfirmCloudPullAsync(current)) { Status = global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u53D6\u6D88\u62C9\u53D6\u3002"); return; }
        _workspace.FlushCurrentAccount();
        await _cloud.PullAsync(current.Id);
        _workspace.ReloadCurrentAccount(current.Id);
        _session.RefreshProfile();
        Status = global::Tomatotodo_Windows.Data.UiText.T("\u4E91\u7AEF\u6570\u636E\u5DF2\u62C9\u53D6\u5E76\u5E94\u7528\u3002");
    }

    private async Task PushCloudAsync()
    {
        var current = _session.Current ?? throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5148\u767B\u5F55\u3002 "));
        if (current.Kind != AccountKind.Cloud || _cloud is null) return;
        _workspace.FlushCurrentAccount();
        await _cloud.PushAsync(current.Id);
        Status = _cloud.SyncStatus;
    }

    private void Refresh()
    {
        if (_session.Current is { } profile)
        {
            Nickname = profile.Nickname; Biography = profile.Biography;
            Avatar = _storage.Read(profile.Id).Avatar;
        }
        else { Nickname = Biography = Password = ConfirmPassword = TargetPassword = ""; Avatar = null; IsEditingProfile = false; }
        OnPropertyChanged(nameof(ProfileBiography));
        MigrationTargets.Clear();
        if (_session.Current is { } current)
            foreach (var target in _storage.ListProfiles().Where(p => p.Kind != current.Kind)) MigrationTargets.Add(target);
        SelectedTarget = null;
        foreach (var name in new[] { nameof(IsSignedIn), nameof(IsSignedOut), nameof(IsCloudSignedIn), nameof(IdentityLabel), nameof(MigrationDescription), nameof(HasMigrationTargets) }) OnPropertyChanged(name);
    }

    public void Dispose() { _session.Changed -= Refresh; Password = ConfirmPassword = TargetPassword = ""; }
}
