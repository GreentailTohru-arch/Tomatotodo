using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Tomatotodo_Windows.Accounts;
using Tomatotodo_Windows.Data;
using Tomatotodo_Windows.Views;

namespace Tomatotodo_Windows;

public sealed partial class MainPage : IAccountWorkspace
{
    private IUserDataStorageService _accountStorage = null!;
    private ICredentialStore _accountCredentials = null!;
    private IImageProcessingService _accountImages = null!;
    private IAccountService _localAccounts = null!;
    private CloudAccountService _cloudAccounts = null!;
    private AccountSession _accountSession = null!;
    private AccountWindow? _accountWindow;
    private bool _accountsInitialized;
    private readonly DispatcherTimer _cloudSyncTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _cloudChangeTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _syncingCloud;

    private void InitializeAccountSystem()
    {
        var root = Path.Combine(StateStore.DirectoryPath, "Accounts");
        _accountStorage = new UserDataStorageService(root);
        _accountCredentials = new WindowsCredentialStore(root);
        _accountImages = new WindowsImageProcessingService();
        _localAccounts = new LocalAccountService(_accountStorage, _accountCredentials, _accountImages);
        _cloudAccounts = new CloudAccountService(_accountStorage, _accountCredentials, _accountImages, root);
        _cloudAccounts.ResolveConflicts = ResolveCloudConflictsAsync;
        _cloudAccounts.DataApplied += ApplyCloudWorkspace;
        _cloudSyncTimer.Tick += async (_, _) => await AutoSyncCloudAsync();
        _cloudChangeTimer.Tick += async (_, _) => { _cloudChangeTimer.Stop(); await AutoSyncCloudAsync(); };
        _cloudSyncTimer.Start();
        _accountSession = new AccountSession(_accountCredentials, _accountStorage, this,
            validateCloudSession: id => _cloudAccounts.RestoreAsync(id),
            cloudLogout: async () => {
                ((IAccountWorkspace)this).FlushCurrentAccount();
                if (_accountSession.Current is { Kind: AccountKind.Cloud } account) {
                    try { await _cloudAccounts.SynchronizeAsync(account.Id); } catch { }
                }
                await _cloudAccounts.LogoutAsync();
            });
        _accountSession.Changed += async () =>
        {
            await UpdateAccountEntryAsync();
            if (_section == "archive") Render();
            if (_accountSession.Current?.Kind == AccountKind.Cloud) { _cloudChangeTimer.Stop(); _cloudChangeTimer.Start(); }
        };
        ThemeAppearanceChanged += (_, _) => _accountWindow?.ApplyTheme(IsDarkAppearance ? ElementTheme.Dark : ElementTheme.Light);
        Loaded += async (_, _) =>
        {
            if (_accountsInitialized) return;
            _accountsInitialized = true;
            try { await _accountSession.RestoreAsync(); }
            catch { ToolTipService.SetToolTip(AccountEntryButton, global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u52A8\u767B\u5F55\u4E0D\u53EF\u7528\uFF0C\u8BF7\u91CD\u65B0\u767B\u5F55\u8D26\u6237\u3002")); }
        };
    }

    private async Task AutoSyncCloudAsync()
    {
        if (_syncingCloud || _accountSession.Current is not { Kind: AccountKind.Cloud } current) return;
        _syncingCloud = true;
        try { await _cloudAccounts.SynchronizeAsync(current.Id); }
        catch { /* Durable local snapshots are retried on the next timer tick. */ }
        finally { _syncingCloud = false; ToolTipService.SetToolTip(AccountEntryButton, _cloudAccounts.SyncStatus); }
    }

    private async Task<bool?> ResolveCloudConflictsAsync(IReadOnlyList<UnifiedUserData.Conflict> conflicts)
    {
        static string Platform(string p) => p == "mobile" ? global::Tomatotodo_Windows.Data.UiText.T("\u79FB\u52A8\u7248") : p == "windows" ? global::Tomatotodo_Windows.Data.UiText.T("\u684C\u9762\u7248") : global::Tomatotodo_Windows.Data.UiText.T("\u53E6\u4E00\u8BBE\u5907");
        static string Time(string t) => DateTimeOffset.TryParse(t, out var at) ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : global::Tomatotodo_Windows.Data.UiText.T("\u672A\u77E5\u65F6\u95F4");
        static string Label(string key) => key switch { "tasks" => global::Tomatotodo_Windows.Data.UiText.T("\u914D\u7F6E\u6E05\u5355"), "timer" => global::Tomatotodo_Windows.Data.UiText.T("\u8BA1\u65F6\u8BBE\u7F6E"), "timetable" => global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868"), "countdown" => global::Tomatotodo_Windows.Data.UiText.T("\u5012\u6570\u65E5"), "profile" => global::Tomatotodo_Windows.Data.UiText.T("\u4E2A\u4EBA\u8D44\u6599"), "windows" => global::Tomatotodo_Windows.Data.UiText.T("\u684C\u9762\u4E13\u5C5E\u8BBE\u7F6E"), "mobile" => global::Tomatotodo_Windows.Data.UiText.T("\u624B\u673A\u4E13\u5C5E\u8BBE\u7F6E"), _ => key };
        var lines = string.Join("\n\n", conflicts.Select(c => global::Tomatotodo_Windows.Data.UiText.F("{0}\n\u672C\u673A \u00B7 {1}\uFF1A{2}\n\u4E91\u7AEF \u00B7 {3}\uFF1A{4}", Label(c.Section), Platform(c.LocalPlatform), Time(c.LocalTime), Platform(c.RemotePlatform), Time(c.RemoteTime))));
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u540C\u6B65\u6570\u636E"), Content = new ScrollViewer { MaxHeight = 400, Content = new TextBlock { Text = lines + global::Tomatotodo_Windows.Data.UiText.T("\n\n\u4EC5\u5BF9\u51B2\u7A81\u90E8\u5206\u4F7F\u7528\u6240\u9009\u7248\u672C\u3002\u4E13\u6CE8\u65E5\u5FD7\u81EA\u52A8\u5408\u5E76\uFF1B\u9009\u62E9\u524D\u7684\u4E24\u4EFD\u6570\u636E\u4FDD\u7559\u4E3A\u5907\u4EFD\u3002"), TextWrapping = TextWrapping.Wrap } }, PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u4FDD\u7559\u672C\u673A\u7248\u672C"), SecondaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u4F7F\u7528\u4E91\u7AEF\u7248\u672C"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u7A0D\u540E\u51B3\u5B9A"), DefaultButton = ContentDialogButton.Close };
        var result = await ShowThemedDialogAsync(dialog);
        return result == ContentDialogResult.Primary ? true : result == ContentDialogResult.Secondary ? false : null;
    }

    private void ApplyCloudWorkspace(Guid id)
    {
        if (_accountSession.Current?.Id != id) return;
        var loaded = _accountStorage.LoadAppState(id);
        var timerChanged = loaded.FocusMinutes != _state.FocusMinutes || loaded.BreakMinutes != _state.BreakMinutes || loaded.PositiveCountup != _state.PositiveCountup || loaded.ActiveTaskId != _state.ActiveTaskId;
        if (timerChanged)
        {
            if (!_breakPhase) CommitFocus();
            foreach (var log in _state.FocusLogs) if (!loaded.FocusLogs.Any(l => l.Id == log.Id)) loaded.FocusLogs.Add(log);
            _running = false; _breakPhase = false; _uncommittedSeconds = 0; _segmentStart = null;
            _remainingSeconds = loaded.PositiveCountup ? 0 : loaded.FocusMinutes * 60;
        }
        _state = loaded;
        SyncCourseNavigation(); ApplyTheme(); Render();
        _accountSession.RefreshProfile();
        if (timerChanged) Save();
    }

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem { Tag: "general" } && _section == "general" && _generalGroup is not null)
            NavigateSettingsGroup(null);
        // Let NavigationView finish restoring focus before activating another HWND.
        if (ReferenceEquals(args.InvokedItemContainer, AccountEntryButton))
            DispatcherQueue.TryEnqueue(() => AccountEntry_Click(sender, new RoutedEventArgs()));
    }

    private void AccountEntry_Click(object sender, RoutedEventArgs args)
    {
        if (_accountWindow is not null) { _accountWindow.Activate(); return; }
        var window = new AccountWindow(interaction => new AccountViewModel(
            kind => kind == AccountKind.Local ? _localAccounts : _cloudAccounts,
            _accountStorage, _accountImages, new CloudAccountMigrationService(_accountStorage, _cloudAccounts),
            _accountCredentials, interaction, this, _accountSession),
            IsDarkAppearance ? ElementTheme.Dark : ElementTheme.Light);
        _accountWindow = window;
        window.Closed += (_, _) => { if (ReferenceEquals(_accountWindow, window)) _accountWindow = null; };
        window.Activate();
    }

    private async Task UpdateAccountEntryAsync()
    {
        var profile = _accountSession.Current;
        // The reference portrait is about 1.7 times the standard settings glyph.
        var displaySize = profile is null ? 16d : 28d;
        AccountEntryButton.Resources["NavigationViewItemOnLeftIconBoxHeight"] = displaySize;
        AccountEntryButton.MinHeight = profile is null ? 36 : 44;
        void ResizeIconBox(Microsoft.UI.Xaml.DependencyObject parent)
        {
            if (parent is Viewbox { Name: "IconBox" } box) box.Height = displaySize;
            for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                ResizeIconBox(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i));
        }
        ResizeIconBox(AccountEntryButton);
        AccountEntryName.Text = profile?.Nickname ?? global::Tomatotodo_Windows.Data.UiText.T("\u672A\u767B\u5F55");
        var fallbackIcon = new FontIcon { Glyph = "\uE77B", FontSize = 20,
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"] };
        fallbackIcon.Loaded += (_, _) => ResizeIconBox(AccountEntryButton);
        AccountEntryButton.Icon = fallbackIcon;
        if (profile is null) return;
        try
        {
            using var memory = new MemoryStream(_accountStorage.Read(profile.Id).Avatar);
            using var stream = memory.AsRandomAccessStream();
            var image = new BitmapImage(); await image.SetSourceAsync(stream);
            if (_accountSession.Current?.Id == profile.Id)
            {
                const float avatarSize = 28;
                var avatar = new ImageIcon { Source = image, Width = avatarSize, Height = avatarSize };
                avatar.Loaded += (_, _) =>
                {
                    ResizeIconBox(AccountEntryButton);
                    var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(avatar);
                    var circle = visual.Compositor.CreateEllipseGeometry();
                    circle.Center = new System.Numerics.Vector2(avatarSize / 2);
                    circle.Radius = new System.Numerics.Vector2(avatarSize / 2);
                    visual.Clip = visual.Compositor.CreateGeometricClip(circle);
                };
                AccountEntryButton.Icon = avatar;
            }
        }
        catch { /* Keep the native account glyph if the avatar cannot be decoded. */ }
    }

    void IAccountWorkspace.FlushCurrentAccount()
    {
        if (!_breakPhase) CommitFocus();
        Save();
    }

    void IAccountWorkspace.SwitchAccount(Guid? userId)
        => SwitchAccountCore(userId, persistCurrent: true);

    void IAccountWorkspace.ReloadCurrentAccount(Guid userId)
        => SwitchAccountCore(userId, persistCurrent: false);

    private void SwitchAccountCore(Guid? userId, bool persistCurrent)
    {
        // Load the destination first: a corrupt account must not discard the current workspace.
        AppState? loaded = userId is { } id ? _accountStorage.LoadAppState(id) : null;
        _running = false;
        if (persistCurrent)
        {
            if (!_breakPhase) CommitFocus();
            Save();
        }
        StateStore.AccountLoader = userId is { } loadId ? () => _accountStorage.LoadAppState(loadId) : null;
        StateStore.AccountSaver = userId is { } saveId ? value => _accountStorage.SaveAppState(saveId, value) : null;
        _state = loaded ?? StateStore.Load();
        _weather = null;
        _weatherRevision++;
        _weatherStatus = global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5728\u5E38\u89C4 \u2192 \u4EEA\u8868\u76D8 \u2192 \u5929\u6C14\u8BBE\u7F6E\u4E2D\u9009\u62E9\u57CE\u5E02\u6216\u5141\u8BB8\u5B9A\u4F4D");
        _nextWeatherRefresh = default;
        _nextQuoteRefresh = default;
        _quoteHistory.Clear();
        _quote = OfflineQuotes.All[Random.Shared.Next(OfflineQuotes.All.Length)];
        _messages.Clear();
        _messageTimer.Stop();
        _messageStoryboard?.Stop();
        _messageClosing = false;
        _messageSwiping = false;
        AppMessageTransform.X = AppMessageTransform.Y = 0;
        AppMessage.IsOpen = false;
        AppMessage.Visibility = Visibility.Collapsed;
        _reminderPlayer?.Pause();
        _phaseEndPlayer?.Pause();
        _nextCourseReminderCheck = default;
        _breakPhase = false; _uncommittedSeconds = 0; _segmentStart = null;
        _remainingSeconds = _state.PositiveCountup ? 0 : _state.FocusMinutes * 60;
        StopLongPress(); _dashboardEditing = false; _draftLayout = null;
        PresetDrawerLayer.Visibility = DashboardDrawerLayer.Visibility = Visibility.Collapsed;
        var mini = _miniWindow; _miniWindow = null; mini?.Close();
        _localPlayer?.Pause(); _localPlayer?.Dispose(); _localPlayer = null;
        _musicFiles.Clear(); _musicIndex = -1; _musicArtwork = null;
        _musicTitle = global::Tomatotodo_Windows.Data.UiText.T("\u5C1A\u672A\u9009\u62E9\u97F3\u4E50\u6587\u4EF6\u5939"); _musicDetail = global::Tomatotodo_Windows.Data.UiText.T("\u5728\u5E38\u89C4 \u2192 \u4EEA\u8868\u76D8 \u2192 \u97F3\u4E50\u8BBE\u7F6E\u4E2D\u9009\u62E9\u6587\u4EF6\u5939");
        _courseWeekIndex = 0;
        SyncCourseNavigation(); ApplyTheme(); ApplyImmersiveMode();
        if (_state.MiniWindowMode) SetMiniWindow(true);
        Render();
        _ = RestoreMusicFolderAsync();
    }
}
