using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Tomatotodo_Windows.Data;

namespace Tomatotodo_Windows;

public sealed partial class MainPage : Page
{
    internal static void StyleDestructiveDialog(ContentDialog dialog)
    {
        // Keep the WinUI accent-button states, but scope critical red to this dialog only.
        var destructiveStyle = new Style(typeof(Button))
        {
            BasedOn = (Style)Application.Current.Resources["TomatotodoAccentButtonStyle"]
        };
        destructiveStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28))));
        destructiveStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Colors.White)));
        dialog.PrimaryButtonStyle = destructiveStyle;
        dialog.Resources["AccentButtonBackground"] = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
        dialog.Resources["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(ColorHelper.FromArgb(255, 172, 37, 24));
        dialog.Resources["AccentButtonBackgroundPressed"] = new SolidColorBrush(ColorHelper.FromArgb(255, 146, 31, 20));
        foreach (var key in new[] { "AccentButtonForeground", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed" })
            dialog.Resources[key] = new SolidColorBrush(Colors.White);
        // A default Close button receives the accent template in WinUI, which
        // would inherit the red resources above. Keep Cancel neutral.
        dialog.DefaultButton = ContentDialogButton.None;
    }

    private async Task<ContentDialogResult> ShowThemedDialogAsync(ContentDialog dialog)
    {
        dialog.XamlRoot ??= XamlRoot;
        dialog.RequestedTheme = IsDarkAppearance ? ElementTheme.Dark : ElementTheme.Light;
        ApplyAccentResources();
        // 弹窗/日期选择器使用独立 Popup 树，显式提供当前主题的本地资源。
        // 不覆盖删除确认框已经指定的危险操作样式和红色画刷。
        if (dialog.PrimaryButtonStyle is null && !new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
        {
            ScopeAccentResources(dialog);
            dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["TomatotodoAccentButtonStyle"];
        }
        if (dialog.Content is FrameworkElement content) ScopeAccentResources(content);
        // Native picker flyouts are separate Popup roots, not descendants of
        // ContentDialog.Content. Scope their resources for this dialog's lifetime.
        var popupRoots = new HashSet<FrameworkElement>();
        var popupThemeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        popupThemeTimer.Tick += (_, _) =>
        {
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(dialog.XamlRoot))
                if (popup.Child is FrameworkElement root && !ContainsContentDialog(root))
                    ScopePopupAccentResources(root, popupRoots);
        };
        popupThemeTimer.Start();
        try { return await dialog.ShowAsync(); }
        finally { popupThemeTimer.Stop(); popupRoots.Clear(); }
    }

    private static bool ContainsContentDialog(DependencyObject element)
    {
        if (element is ContentDialog) return true;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            if (ContainsContentDialog(VisualTreeHelper.GetChild(element, index))) return true;
        return false;
    }

    private void ScopePopupAccentResources(FrameworkElement root, HashSet<FrameworkElement> themedElements)
    {
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast) return;
        if (themedElements.Add(root))
        {
        root.RequestedTheme = IsDarkAppearance ? ElementTheme.Dark : ElementTheme.Light;
        ScopeAccentResources(root);
        var fill = (Brush)root.Resources["AccentFillColorDefaultBrush"];
        // These native template parts can retain a brush resolved before the
        // popup joined its tree. Assign the owned app brush to the actual part.
        if (root is Microsoft.UI.Xaml.Shapes.Rectangle { Name: "Pill" } pill &&
            HasPopupAncestor(root, "ComboBoxItem"))
            pill.Fill = fill;
        if (root is Grid { Name: "HighlightRect" } highlight &&
            (HasPopupAncestor(root, "TimePickerFlyoutPresenter") || HasPopupAncestor(root, "DatePickerFlyoutPresenter")))
            highlight.Background = fill;
        if (root is Microsoft.UI.Xaml.Controls.Primitives.MonochromaticOverlayPresenter overlay)
        {
            overlay.Background = fill;
            overlay.ReplacementColor = ((SolidColorBrush)Application.Current.Resources["TomatotodoAccentButtonForegroundBrush"]).Color;
        }
        // Style setters may already have resolved before the Popup opened.
        // Apply CalendarView's native brush properties as well as template roles.
        if (root is CalendarView calendar)
        {
            var accent = new SolidColorBrush(IsDarkAppearance ? AccentDisplayColor : ParseColor(_state.AccentColor));
            calendar.TodayBackground = accent;
            calendar.SelectedBorderBrush = accent;
            calendar.SelectedHoverBorderBrush = accent;
            calendar.SelectedPressedBorderBrush = accent;
            calendar.TodayHoverBackground = (Brush)root.Resources["CalendarViewTodayHoverBackground"];
            calendar.TodayPressedBackground = (Brush)root.Resources["CalendarViewTodayPressedBackground"];
            calendar.TodayForeground = (Brush)root.Resources["CalendarViewTodayForeground"];
        }
        }
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            if (VisualTreeHelper.GetChild(root, index) is FrameworkElement child)
                ScopePopupAccentResources(child, themedElements);
    }

    private static bool HasPopupAncestor(DependencyObject element, string typeName)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent.GetType().Name == typeName) return true;
        return false;
    }

    private void ScopeAccentResources(FrameworkElement element)
    {
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast) return;
        var theme = IsDarkAppearance ? "Default" : "Light";
        var dictionary = (ResourceDictionary)Resources.ThemeDictionaries[theme];
        // ResourceDictionary is a projected WinRT object: wrapper reference
        // identity is not a reliable test of dictionary ownership.
        foreach (var key in _accentBrushes.Keys.Select(slot => slot.Key).Distinct())
            element.Resources[key] = dictionary[key];
        // 时间/日期滚轮的 ForegroundColor 资源是 Color，不能误传 Brush。
        var foreground = ((SolidColorBrush)Application.Current.Resources["TomatotodoAccentButtonForegroundBrush"]).Color;
        element.Resources["DatePickerFlyoutPresenterHighlightForegroundColor"] = foreground;
        element.Resources["TimePickerFlyoutPresenterHighlightForegroundColor"] = foreground;
        if (element is Panel panel)
            foreach (var child in panel.Children.OfType<FrameworkElement>()) ScopeAccentResources(child);
        else if (element is Border { Child: FrameworkElement child }) ScopeAccentResources(child);
    }

    // Shared visual baseline: the native 工具 page's SettingsCard rectangle column.
    private const double SettingsWorkspaceMaxWidth = 1020;
    public FrameworkElement ShellTitleBar => ShellCommandBar;
    public bool IsDarkForShell => IsDarkAppearance;
    public bool UseShellAcrylic => _state.ShellAcrylic;
    private readonly SolidColorBrush _shellSurfaceBrush = new();
    public Windows.UI.Color ShellBackgroundColor => IsDarkAppearance
        ? _state.PureBlack ? Colors.Black : ColorHelper.FromArgb(255, 32, 32, 32)
        : ColorHelper.FromArgb(255, 243, 243, 243);
    public event Action<bool, Windows.UI.Color>? ThemeAppearanceChanged;
    private AppState _state = StateStore.Load();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private string _section = "dashboard";
    private string? _lastRenderedSection;
    private bool _running;
    private bool _breakPhase;
    private int _remainingSeconds;
    private DateTimeOffset _lastTick;
    private DateTimeOffset? _segmentStart;
    private int _uncommittedSeconds;
    private TextBlock? _timerText;
    private int _courseWeekIndex;
    private DateTime _lastPresetScheduleCheck = DateTime.MinValue;
    private readonly Stack<string> _navigationHistory = new();
    private bool _navigatingBack;
    private bool _navigatingFromShellSearch;
    private bool _shellSearchExpanded;
    private bool _compactSettingsLayout;
    private bool _ultraCompactSettingsLayout;
    // Windows Settings exposes the accent board by default and keeps it visible while a color is selected.
    private bool _accentPaletteExpanded = true;
    private bool _navigationAutoCollapsed;
    private bool _navigationWasOpenBeforeAutoCollapse;
    private string? _settingsSearchTarget;
    private FrameworkElement? _settingsSearchTargetElement;
    private readonly Windows.UI.ViewManagement.UISettings _systemUiSettings = new();

    private TaskPreset ActivePreset => _state.Presets.FirstOrDefault(p => p.Id == _state.ActivePresetId) ?? _state.Presets[0];
    private TodoTask? ActiveTask => ActivePreset.Tasks.FirstOrDefault(t => t.Id == _state.ActiveTaskId);

    public MainPage()
    {
        // Resolve auto accent before any template or palette captures its brush.
        if (SyncSystemAccentColor()) Save();
        // Register mutable application brush slots before control templates resolve
        // their ThemeResource references (including the navigation indicator).
        ApplyAccentResources();
        InitializeComponent();
        InitializeNavigationMotion();
        InitializeAccountSystem();
        InitializeUpdates();
        _remainingSeconds = _state.FocusMinutes * 60;
        _courseWeekIndex = Math.Max(0, _state.CourseSchedule.Weeks.FindIndex(
            week => week.Date == CourseScheduleCodec.WeekForDate(_state.CourseSchedule, DateOnly.FromDateTime(DateTime.Today))?.Date));
        _systemUiSettings.ColorValuesChanged += SystemUiSettings_ColorValuesChanged;
        SyncCourseNavigation();
        ApplyTheme();
        if (_state.ImmersiveMode) ApplyImmersiveMode();
        Loaded += (_, _) => { if (_state.ImmersiveMode) ApplyImmersiveMode(); };
        if (_state.MiniWindowMode) SetMiniWindow(true);
        _clock.Tick += Clock_Tick;
        InitializeNotifications();
        _clock.Start();
        _ = RestoreMusicFolderAsync();
        PageScroll.SizeChanged += (_, _) =>
        {
            if (_section == "dashboard" && _dashboardGrid is not null)
                ReflowDashboard();
            else if (_section == "archive" && Math.Abs(PageScroll.ActualWidth - _archiveRenderedViewportWidth) > 2)
                Render();
            else if (_section == "presets" && Math.Abs(PageScroll.ActualWidth - _presetRenderedViewportWidth) > 2)
                Render();
            else if (_section == "courses" && Math.Abs(PageScroll.ActualWidth - _courseRenderedViewportWidth) > 2)
                Render();
            if (_section is "appearance" or "general" or "tools")
                UpdateSettingsWorkspaceWidth();
            var compact = PageScroll.ActualWidth < 660;
            var ultraCompact = PageScroll.ActualWidth < 430;
            if (compact != _compactSettingsLayout || ultraCompact != _ultraCompactSettingsLayout)
            {
                _compactSettingsLayout = compact;
                _ultraCompactSettingsLayout = ultraCompact;
                if (_section is "appearance" or "general" or "tools") Render();
            }
        };
        Navigation.SelectedItem = Navigation.MenuItems[0];
        Render();
        ActualThemeChanged += (_, _) => Render();
    }

    private void Navigation_Loaded(object sender, RoutedEventArgs args)
    {
        // NavigationView gives its footer a separate scroller. A taller profile
        // row can expose its thumb beside the settings entry; hide only chrome.
        void Configure(DependencyObject parent)
        {
            if (parent is ScrollViewer { Name: "FooterItemsScrollViewer" } footer)
            {
                footer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                footer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                return;
            }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                Configure(VisualTreeHelper.GetChild(parent, i));
        }
        Configure(Navigation);
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string section)
        {
            if (_updatingSettingsNavigation || section == "account" || section == _section) return;
            if (!_navigatingFromShellSearch) _settingsSearchTarget = null;
            NavigateWorkspace(section, remember: !_navigatingBack);
            _navigatingBack = false;
            _navigatingFromShellSearch = false;
        }
    }

    private void Clock_Tick(object? sender, object e)
    {
        UpdateDashboardContent();
        CheckCourseReminders();
        UpdateLiveWidgetClocks();
        if (_section == "dashboard" && DateTimeOffset.Now - _lastMediaRefresh > TimeSpan.FromSeconds(2))
            _ = RefreshMediaAsync();
        if (_lastPresetScheduleCheck.Minute != DateTime.Now.Minute || _lastPresetScheduleCheck.Date != DateTime.Today)
        {
            _lastPresetScheduleCheck = DateTime.Now;
            CheckPresetSchedules();
        }
        if (!_running) return;
        var now = DateTimeOffset.Now;
        var elapsed = (int)(now - _lastTick).TotalSeconds;
        if (elapsed <= 0) return;
        _lastTick = now;
        if (_state.PositiveCountup)
        {
            _uncommittedSeconds += elapsed;
            _remainingSeconds += elapsed;
            UpdateTimerDisplay();
            return;
        }
        var counted = Math.Min(elapsed, _remainingSeconds);
        if (!_breakPhase) _uncommittedSeconds += counted;
        _remainingSeconds -= counted;
        UpdateTimerDisplay();
        if (_remainingSeconds == 0) FinishPhase();
    }

    private Guid? _cycleTaskId;
    private bool _cycleHasTask;
    private void FinishPhase()
    {
        _running = false;
        if (!_breakPhase)
        {
            var completedTaskName = ActiveTask?.Title ?? global::Tomatotodo_Windows.Data.UiText.T("\u672C\u6B21\u4E13\u6CE8");
            _cycleTaskId = ActiveTask?.Id;
            _cycleHasTask = _cycleTaskId is not null;
            CommitFocus(true);
            if (ActiveTask is { } task)
            {
                task.CompletedPomodoros++;
                if (task.EstimatedPomodoros is > 0 && task.CompletedPomodoros >= task.EstimatedPomodoros)
                    CompleteTask(ActivePreset, task);
            }
            _breakPhase = _state.EnableShortBreak;
            var continuingFocus = TimerCycle.ShouldContinue(_state.AutomaticTimerCycle,
                _state.PositiveCountup, false, _cycleHasTask,
                _cycleTaskId is { } completedId ? ActivePreset.Tasks.FirstOrDefault(t => t.Id == completedId) : null,
                _state.ActiveTaskId);
            NotifyPhaseEnd(global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u7ED3\u675F \u00B7 \u83B7\u5F97 1 \u4E2A\u756A\u8304 \uD83C\uDF45"), global::Tomatotodo_Windows.Data.UiText.F("{0}\u5DF2\u5B8C\u6210\u4E00\u4E2A\u4E13\u6CE8\u65F6\u6BB5\u3002", completedTaskName) +
                (_breakPhase ? global::Tomatotodo_Windows.Data.UiText.F("\u53EF\u4EE5\u4F11\u606F {0} \u5206\u949F\u3002", _state.BreakMinutes) : continuingFocus
                    ? global::Tomatotodo_Windows.Data.UiText.T("\u7EE7\u7EED\u4E0B\u4E00\u6B21\u4E13\u6CE8\u3002") : global::Tomatotodo_Windows.Data.UiText.T("\u672C\u6B21\u4E13\u6CE8\u5DF2\u7ED3\u675F\u3002")), shortBreak: false);
        }
        else
        {
            _breakPhase = false;
            NotifyPhaseEnd(global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u7ED3\u675F"), _state.AutomaticTimerCycle
                ? global::Tomatotodo_Windows.Data.UiText.T("\u4F11\u606F\u7ED3\u675F\uFF1B\u4EFB\u52A1\u672A\u5B8C\u6210\u65F6\u7EE7\u7EED\u4E13\u6CE8\uFF0C\u756A\u8304\u8FBE\u6807\u540E\u505C\u6B62\u5FAA\u73AF\u3002")
                : global::Tomatotodo_Windows.Data.UiText.T("\u4F11\u606F\u7ED3\u675F\uFF0C\u51C6\u5907\u597D\u540E\u5F00\u59CB\u4E0B\u4E00\u6B21\u4E13\u6CE8\u3002"), shortBreak: true);
        }
        _remainingSeconds = (_breakPhase ? _state.BreakMinutes : _state.FocusMinutes) * 60;
        var cycleTask = _cycleTaskId is { } cycleId
            ? ActivePreset.Tasks.FirstOrDefault(t => t.Id == cycleId) : null;
        if (TimerCycle.ShouldContinue(_state.AutomaticTimerCycle, _state.PositiveCountup,
            _breakPhase, _cycleHasTask, cycleTask, _state.ActiveTaskId))
            StartPause(notifyStart: false);
        Save();
        Render();
    }

    private void CommitFocus(bool completedPomodoro = false)
    {
        if (_uncommittedSeconds <= 0) return;
        _state.FocusLogs.Add(new FocusLog
        {
            TaskId = _state.ActiveTaskId,
            StartedAt = _segmentStart ?? DateTimeOffset.Now.AddSeconds(-_uncommittedSeconds),
            Seconds = _uncommittedSeconds,
            CompletedPomodoro = completedPomodoro
        });
        _uncommittedSeconds = 0;
        _segmentStart = _running ? DateTimeOffset.Now : null;
        Save();
    }

    private void Save()
    {
        if (_accountSession?.Current is { Kind: Accounts.AccountKind.Cloud } account)
        {
            _cloudAccounts.TrackLocal(account.Id, _state);
            _cloudChangeTimer.Stop(); _cloudChangeTimer.Start();
        }
        StateStore.Save(_state);
    }

    public void FlushOnClose()
    {
        _clock.Stop();
        _messageTimer.Stop();
        _reminderPlayer?.Dispose();
        _reminderPlayer = null;
        _phaseEndPlayer?.Dispose();
        _phaseEndPlayer = null;
        _running = false;
        if (!_breakPhase) CommitFocus();
        Save();
        _accountWindow?.CloseWithOwner();
        _miniWindow?.Close();
        _accountSession?.Dispose();
    }

    private void StartPause() => StartPause(notifyStart: true);

    private void StartPause(bool notifyStart)
    {
        _running = !_running;
        if (_running)
        {
            _lastTick = DateTimeOffset.Now;
            if (!_breakPhase) _segmentStart = _lastTick;
            if (notifyStart) NotifyFocus(_breakPhase ? global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u5F00\u59CB") : global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u5F00\u59CB"),
                _breakPhase ? global::Tomatotodo_Windows.Data.UiText.F("\u4F11\u606F {0} \u5206\u949F\uFF0C\u653E\u677E\u4E00\u4E0B\u3002", _remainingSeconds / 60) :
                $"{ActiveTask?.Title ?? "保持专注"} · " + (_state.PositiveCountup ? global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5728\u8BB0\u5F55\u4E13\u6CE8\u65F6\u957F\u3002") : global::Tomatotodo_Windows.Data.UiText.F("\u5269\u4F59 {0}:{1}\u3002", $"{_remainingSeconds / 60:00}", $"{_remainingSeconds % 60:00}")));
        }
        else if (!_breakPhase) CommitFocus();
        UpdateTimerDisplay();
    }

    private void ResetTimer()
    {
        _running = false;
        if (!_breakPhase) CommitFocus();
        _breakPhase = false;
        _cycleTaskId = null; _cycleHasTask = false;
        _remainingSeconds = _state.PositiveCountup ? 0 : _state.FocusMinutes * 60;
        Render();
    }

    private void SelectTask(TodoTask task)
    {
        if (task.IsComplete) return;
        if (_state.ActiveTaskId == task.Id) return;
        if (_running && !_breakPhase) CommitFocus();
        _state.ActiveTaskId = task.Id;
        Save();
        Render();
    }

    private void Render()
    {
        Data.UiText.Language = Services.LanguagePreference.Resolved;
        ApplyXamlLanguage();
        AccountEntryName.Text = _accountSession.Current?.Nickname ?? Data.UiText.T("未登录");
        Language = Services.LanguagePreference.Resolved;
        FlowDirection = UiLanguage.IsRightToLeft(Language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ShellSearchBox.FlowDirection = FlowDirection;
        // Only entering the dashboard should play its entrance transition. Editing a tile,
        // selecting a task or switching phases must not flash every other tile again.
        PageBody.ChildrenTransitions = _section != "dashboard" || _lastRenderedSection != NavigationRoute
            ? new Microsoft.UI.Xaml.Media.Animation.TransitionCollection { new Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition { FromVerticalOffset = 18 } }
            : null;
        _lastRenderedSection = NavigationRoute;
        _timerText = null;
        _timerStatus = null;
        _timerProgress = null;
        _miniModeToggle = null;
        _liveClock = null;
        _hourHand = _minuteHand = _secondHand = null;
        _settingsSearchTargetElement = null;
        ApplyAccentResources();
        var isSettingsWorkspace = _section is "appearance" or "general" or "tools";
        // Keep the appearance page in the visual tree while navigating. Unloading and reloading
        // its expanded SettingsExpander replays the toolkit's open animation even though the
        // user never touched the chevron.
        if (_appearancePage is not null && PageBody.Children.Contains(_appearancePage))
        {
            for (var index = PageBody.Children.Count - 1; index >= 0; index--)
                if (!ReferenceEquals(PageBody.Children[index], _appearancePage))
                    PageBody.Children.RemoveAt(index);
            _appearancePage.Visibility = IsAppearanceSettings ? Visibility.Visible : Visibility.Collapsed;
        }
        else PageBody.Children.Clear();
        PageContentHost.MaxWidth = _section is "presets" or "archive" or "courses" or "dashboard"
            ? double.PositiveInfinity
            : isSettingsWorkspace ? SettingsWorkspaceMaxWidth : 1180;
        PageContentHost.HorizontalAlignment = isSettingsWorkspace ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        PageBody.MaxWidth = double.PositiveInfinity;
        PageBody.HorizontalAlignment = HorizontalAlignment.Stretch;
        WorkspaceHeader.HorizontalAlignment = HorizontalAlignment.Stretch;
        WorkspaceHeader.MaxWidth = double.PositiveInfinity;
        UpdateSettingsWorkspaceWidth();
        Navigation.RequestedTheme = RequestedTheme;
        var neutralBackground = ShellBackgroundColor;
        // 保持同一个画刷实例，已经加载的 NavigationView 模板也能即时切换。
        _shellSurfaceBrush.Color = _state.ShellAcrylic ? Colors.Transparent : neutralBackground;
        var shellFill = _shellSurfaceBrush;
        Navigation.Background = shellFill;
        Background = shellFill;
        ShellCommandBar.Background = shellFill;
        Navigation.Resources["NavigationViewDefaultPaneBackground"] = shellFill;
        Navigation.Resources["NavigationViewExpandedPaneBackground"] = shellFill;
        var useAcrylic = _state.ShellAcrylic && !new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        // The window supplies desktop blur. A light veil keeps wallpaper colors
        // from overpowering day-mode content while retaining the material.
        PresetDrawerWorkspace.Background = new SolidColorBrush(useAcrylic
            ? (IsDarkAppearance ? Colors.Transparent : ColorHelper.FromArgb(184, 249, 249, 249)) : IsDarkAppearance
            ? (_state.PureBlack ? Colors.Black : ColorHelper.FromArgb(255, 39, 39, 39))
            : ColorHelper.FromArgb(255, 249, 249, 249));
        UpdateResetButtonMaterial(useAcrylic);
        UpdateMiniWindowTheme();
        ThemeAppearanceChanged?.Invoke(IsDarkAppearance, neutralBackground);
        DashboardToolbar.Visibility = _section == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        DashboardFooter.Visibility = _section == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        PageScroll.VerticalScrollBarVisibility = _section == "dashboard"
            ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        PresetToolbar.Visibility = _section == "presets" ? Visibility.Visible : Visibility.Collapsed;
        ArchiveToolbar.Visibility = _section == "archive" ? Visibility.Visible : Visibility.Collapsed;
        CourseToolbar.Visibility = _section == "courses" ? Visibility.Visible : Visibility.Collapsed;
        PresetAddButton.Visibility = _section == "presets" ? Visibility.Visible : Visibility.Collapsed;
        DashboardEditButton.Visibility = _section == "dashboard" && !_dashboardEditing ? Visibility.Visible : Visibility.Collapsed;
        DashboardAddButton.Visibility = _section == "dashboard" && _dashboardEditing ? Visibility.Visible : Visibility.Collapsed;
        DashboardSaveButton.Visibility = _section == "dashboard" && _dashboardEditing ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = _section switch
        {
            "dashboard" => global::Tomatotodo_Windows.Data.UiText.T("\u4EEA\u8868\u76D8"), "presets" => global::Tomatotodo_Windows.Data.UiText.T("\u914D\u7F6E"), "archive" => ArchiveCareMessage(DateTime.Now),
            "courses" => global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868"), "appearance" => global::Tomatotodo_Windows.Data.UiText.T("\u4E2A\u6027\u5316"), "general" => GeneralPageTitle,
            _ => global::Tomatotodo_Windows.Data.UiText.T("\u5DE5\u5177")
        };
        SettingsParentHeader.Visibility = _section == "general" && _generalGroup is not null
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateShellHeader();
        switch (_section)
        {
            case "dashboard": RenderDashboard(); break;
            case "presets": RenderPresets(); break;
            case "archive": RenderArchive(); break;
            case "courses": RenderCourses(); break;
            case "appearance": RenderAppearance(); break;
            case "general": RenderGeneral(); break;
            case "tools": RenderTools(); break;
        }
        ScrollToSearchTarget();
    }

    private void UpdateSettingsWorkspaceWidth()
    {
        if (_section is not ("appearance" or "general" or "tools") || PageScroll.ActualWidth <= 0)
        {
            PageContentHost.Width = double.NaN;
            return;
        }

        // MaxWidth alone lets a centered Grid shrink to its page's desired width. Give every settings
        // workspace the exact same rendered rectangle: the tool page's 1020 epx baseline, or the viewport when narrower.
        PageContentHost.Width = Math.Min(SettingsWorkspaceMaxWidth, PageScroll.ActualWidth);
    }

    private void AddTask(TextBox input)
    {
        var title = input.Text.Trim();
        if (title.Length == 0) return;
        var task = new TodoTask { Title = title };
        ActivePreset.Tasks.Add(task);
        _state.ActivePresetId = ActivePreset.Id;
        _state.ActiveTaskId ??= task.Id;
        Save(); Render();
    }

    private UIElement TaskRow(TodoTask task)
    {
        var row = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var complete = new CheckBox { IsChecked = task.IsComplete, VerticalAlignment = VerticalAlignment.Center };
        complete.Checked += (_, _) => { CompleteTask(ActivePreset, task); Save(); Render(); };
        complete.Unchecked += (_, _) => { task.IsComplete = false; Save(); Render(); };
        row.Children.Add(complete);
        var title = new Button { Content = new TextBlock
            {
                Text = task.Title +
                    (task.EstimatedPomodoros is > 0 ? $"   {task.CompletedPomodoros}/{task.EstimatedPomodoros}" : ""),
                TextWrapping = TextWrapping.Wrap,
                TextDecorations = task.IsComplete ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left };
        title.Click += (_, _) => SelectTask(task);
        Grid.SetColumn(title, 1); row.Children.Add(title);
        var active = new TextBlock { Text = _state.ActiveTaskId == task.Id ? "●" : "",
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(active, 2); row.Children.Add(active);
        return row;
    }

    private async Task DeletePreset(TaskPreset preset)
    {
        var dialog = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u5220\u9664\u4EFB\u52A1\u6E05\u5355\uFF1F"),
            Content = global::Tomatotodo_Windows.Data.UiText.F("\u201C{0}\u201D\u53CA\u5176\u4E2D\u4EFB\u52A1\u4F1A\u88AB\u5220\u9664\u3002\u4E13\u6CE8\u8BB0\u5F55\u4ECD\u4FDD\u7559\u3002", preset.Name),
            PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u5220\u9664"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
        StyleDestructiveDialog(dialog);
        if (await ShowThemedDialogAsync(dialog) != ContentDialogResult.Primary) return;
        if (_state.ActivePresetId == preset.Id && _running && !_breakPhase) CommitFocus();
        _state.Presets.Remove(preset);
        _state.ActivePresetId = _state.Presets[0].Id;
        _state.ActiveTaskId = ActivePreset.Tasks.FirstOrDefault()?.Id;
        Save(); Render();
    }

    private void ApplyTheme()
    {
        RequestedTheme = _state.Theme switch
        { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        ApplyUiScale();
    }

    private void ApplyUiScale()
    {
        if (ScaledShell is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var scale = Math.Clamp(_state.UiScale, 85, 125) / 100d;
        // Explicit logical bounds make Viewbox scaling participate in responsive layout.
        // Do not compound FontSize on top of this, or text would scale twice.
        ScaledShell.Width = ActualWidth / scale;
        ScaledShell.Height = ActualHeight / scale;
    }

    private bool IsDarkAppearance => _state.Theme switch
    {
        "dark" => true,
        "light" => false,
        // 根层显式同步材质后，不再从它继承的 ActualTheme 判断“自动”。
        _ => Luminance(_systemUiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background)) < .5
    };

    private Windows.UI.Color AccentDisplayColor
    {
        get
        {
            var color = ParseColor(_state.AccentColor);
            var surface = IsDarkAppearance ? ColorHelper.FromArgb(255, 32, 32, 32)
                : ColorHelper.FromArgb(255, 243, 243, 243);
            var target = IsDarkAppearance ? Colors.White : Colors.Black;
            for (var step = 0; step < 15 && ContrastRatio(color, surface) < 4.5; step++)
                color = BlendColor(color, target, .12);
            return color;
        }
    }

    private static double Luminance(Windows.UI.Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }

    private static double ContrastRatio(Windows.UI.Color first, Windows.UI.Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static Windows.UI.Color BlendColor(Windows.UI.Color color, Windows.UI.Color target, double fraction) =>
        ColorHelper.FromArgb(255,
            (byte)Math.Round(color.R + (target.R - color.R) * fraction),
            (byte)Math.Round(color.G + (target.G - color.G) * fraction),
            (byte)Math.Round(color.B + (target.B - color.B) * fraction));

    private void ApplyAccentResources()
    {
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast) return;
        // Filled controls use the selected accent itself. The stronger contrast
        // adjustment in AccentDisplayColor is for text on a neutral surface only.
        var accent = IsDarkAppearance ? AccentDisplayColor : ParseColor(_state.AccentColor);
        var onAccent = ContrastRatio(Colors.Black, accent) >= ContrastRatio(Colors.White, accent)
            ? Colors.Black : Colors.White;
        if (Application.Current.Resources["TomatotodoAccentButtonBrush"] is SolidColorBrush buttonFill)
            buttonFill.Color = accent;
        if (Application.Current.Resources["TomatotodoAccentButtonForegroundBrush"] is SolidColorBrush buttonText)
            buttonText.Color = onAccent;
        // WinUI AccentFill secondary/tertiary states use 90% / 80% opacity.
        // On a light surface this gently brightens the fill instead of blackening it.
        var hover = ColorHelper.FromArgb(230, accent.R, accent.G, accent.B);
        var pressed = ColorHelper.FromArgb(204, accent.R, accent.G, accent.B);

        // These are the accent roles used by WinUI's built-in control templates. Keep
        // ordinary button/card surfaces neutral; only their interactive accent states
        // inherit the selected color, as in the Gallery Color sample.
        foreach (var key in new[]
        {
            "AccentFillColorDefaultBrush",
            "ComboBoxItemPillFillBrush",
            "AccentAAFillColorDefaultBrush", "DatePickerFlyoutPresenterHighlightFill", "TimePickerFlyoutPresenterHighlightFill",
            "CalendarViewSelectedBorderBrush", "CalendarViewTodayBackground",
            "ToggleButtonBackgroundChecked", "AppBarToggleButtonBackgroundChecked", "SplitButtonBackgroundChecked",
            "ListViewItemSelectionIndicatorBrush", "ListViewItemSelectionIndicatorPointerOverBrush", "ListViewItemSelectionIndicatorPressedBrush",
            "TextControlBorderBrushFocused", "ComboBoxBorderBrushFocused", "PivotHeaderItemSelectedPipeFill",
            "NavigationViewSelectionIndicatorForeground", "ProgressBarForeground",
            "ProgressRingForegroundThemeBrush",
            "SliderThumbBackground", "SliderTrackValueFill",
            "ToggleSwitchFillOn", "ToggleSwitchStrokeOn",
            "RadioButtonOuterEllipseCheckedStroke", "RadioButtonOuterEllipseCheckedFill",
            "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeIndeterminate",
            "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillIndeterminate",
            "AccentButtonBackground"
        }) SetAccentBrush(key, accent);
        foreach (var key in new[]
        {
            "AccentFillColorSecondaryBrush",
            "CalendarViewFocusBorderBrush", "CalendarViewSelectedHoverBorderBrush", "CalendarViewTodayHoverBackground",
            "ToggleButtonBackgroundCheckedPointerOver", "AppBarToggleButtonBackgroundCheckedPointerOver", "SplitButtonBackgroundCheckedPointerOver",
            "SliderThumbBackgroundPointerOver", "SliderTrackValueFillPointerOver",
            "ToggleSwitchFillOnPointerOver", "ToggleSwitchStrokeOnPointerOver",
            "RadioButtonOuterEllipseCheckedStrokePointerOver", "RadioButtonOuterEllipseCheckedFillPointerOver",
            "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver",
            "CheckBoxCheckBackgroundFillCheckedPointerOver", "CheckBoxCheckBackgroundFillIndeterminatePointerOver",
            "AccentButtonBackgroundPointerOver"
        }) SetAccentBrush(key, hover);
        foreach (var key in new[]
        {
            "AccentFillColorTertiaryBrush",
            "CalendarViewSelectedPressedBorderBrush", "CalendarViewTodayPressedBackground", "CalendarViewTodayBlackoutBackground",
            "ToggleButtonBackgroundCheckedPressed", "AppBarToggleButtonBackgroundCheckedPressed", "SplitButtonBackgroundCheckedPressed",
            "SliderThumbBackgroundPressed", "SliderTrackValueFillPressed",
            "ToggleSwitchFillOnPressed", "ToggleSwitchStrokeOnPressed",
            "RadioButtonOuterEllipseCheckedStrokePressed", "RadioButtonOuterEllipseCheckedFillPressed",
            "CheckBoxCheckBackgroundStrokeCheckedPressed", "CheckBoxCheckBackgroundStrokeIndeterminatePressed",
            "CheckBoxCheckBackgroundFillCheckedPressed", "CheckBoxCheckBackgroundFillIndeterminatePressed",
            "AccentButtonBackgroundPressed"
        }) SetAccentBrush(key, pressed);
        var accentText = AccentDisplayColor;
        foreach (var key in new[] { "CalendarViewSelectedForeground", "CalendarViewSelectedHoverForeground", "CalendarViewSelectedPressedForeground" })
            SetAccentBrush(key, accentText);
        foreach (var key in new[] { "AccentTextFillColorPrimaryBrush", "HyperlinkButtonForeground", "HyperlinkForeground" })
            SetAccentBrush(key, accentText);
        foreach (var key in new[] { "AccentTextFillColorSecondaryBrush", "HyperlinkButtonForegroundPointerOver", "HyperlinkForegroundPointerOver" })
            SetAccentBrush(key, IsDarkAppearance ? accentText : BlendColor(accentText, Colors.Black, .12));
        foreach (var key in new[] { "AccentTextFillColorTertiaryBrush", "HyperlinkButtonForegroundPressed", "HyperlinkForegroundPressed" })
            SetAccentBrush(key, accentText);
        foreach (var key in new[]
        {
            "TextOnAccentFillColorPrimaryBrush", "TextOnAccentFillColorSecondaryBrush",
            "CalendarViewTodayForeground", "CalendarViewTodayBlackoutForeground", "CalendarViewTodaySelectedInnerBorderBrush",
            "AccentButtonForeground", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed",
            "RadioButtonCheckGlyphFill", "RadioButtonCheckGlyphFillPointerOver", "RadioButtonCheckGlyphFillPressed",
            "CheckBoxCheckGlyphForegroundChecked", "CheckBoxCheckGlyphForegroundCheckedPointerOver",
            "CheckBoxCheckGlyphForegroundCheckedPressed", "CheckBoxCheckGlyphForegroundIndeterminate",
            "CheckBoxCheckGlyphForegroundIndeterminatePointerOver", "CheckBoxCheckGlyphForegroundIndeterminatePressed"
        }) SetAccentBrush(key, onAccent);
    }

    // 只追踪本应用创建的画刷，不能用 ResourceDictionary.TryGetValue 判断本地键。
    // WinUI 的查找可能返回系统/合并字典里的回退资源；修改它并不等于注册 Light 覆盖。
    private readonly Dictionary<(ResourceDictionary Dictionary, string Key), SolidColorBrush> _accentBrushes = new();

    private void SetAccentBrush(string key, Windows.UI.Color color)
    {
        // A theme-scoped override lets high-contrast mode keep WinUI's own system
        // highlight and text colors instead of inheriting the user's app accent.
        foreach (var owner in new[] { Resources, Application.Current.Resources })
        {
            foreach (var slotTheme in new[] { "Default", "Light" })
            {
                if (!owner.ThemeDictionaries.TryGetValue(slotTheme, out var themeValue) ||
                    themeValue is not ResourceDictionary dictionary) continue;
                var slot = (dictionary, key);
                if (!_accentBrushes.TryGetValue(slot, out var brush))
                {
                    brush = new SolidColorBrush(color);
                    _accentBrushes.Add(slot, brush);
                    // 无条件建立真正的本地资源，避免 Light 继续引用系统绿色画刷。
                    dictionary[key] = brush;
                }
                // Keep inactive slots current too: controls and Popup roots may
                // retain a brush obtained before a light/dark transition.
                else brush.Color = color;
            }
        }
    }

    private void UpdateResetButtonMaterial(bool acrylic)
    {
        Brush Fill(bool dark, byte opacity, byte shade) => acrylic
            ? new AcrylicBrush { TintColor = ColorHelper.FromArgb(255, shade, shade, shade),
                TintOpacity = dark ? .65 : .85, TintLuminosityOpacity = dark ? .65 : .9,
                FallbackColor = ColorHelper.FromArgb(255, shade, shade, shade) }
            : new SolidColorBrush(ColorHelper.FromArgb(opacity, shade, shade, shade));
        foreach (var theme in new[] { "Default", "Light" })
        {
            var dark = theme == "Default";
            var resources = (ResourceDictionary)DashboardResetButton.Resources.ThemeDictionaries[theme];
            resources["ButtonBackgroundPointerOver"] = Fill(dark, 255, dark ? (byte)69 : (byte)233);
            resources["ButtonBackgroundPressed"] = Fill(dark, 255, dark ? (byte)41 : (byte)221);
        }
        DashboardResetButton.Background = Fill(IsDarkAppearance, 255, IsDarkAppearance ? (byte)48 : (byte)249);
    }

    private void SyncCourseNavigation()
    {
        var present = Navigation.MenuItems.Contains(CourseNavigationItem);
        if (_state.CourseScheduleEnabled)
        {
            CourseNavigationItem.Visibility = Visibility.Visible;
            if (!present) Navigation.MenuItems.Insert(3, CourseNavigationItem);
        }
        else if (!_state.CourseScheduleEnabled && present)
        {
            if (_section == "courses") Navigation.SelectedItem = Navigation.MenuItems[0];
            Navigation.MenuItems.Remove(CourseNavigationItem);
        }
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyUiScale();
        var logicalWidth = e.NewSize.Width / (Math.Clamp(_state.UiScale, 85, 125) / 100d);
        var narrow = logicalWidth < 860;
        var hideNavigation = logicalWidth < 760;
        if (hideNavigation && !_navigationAutoCollapsed)
        {
            _navigationWasOpenBeforeAutoCollapse = Navigation.IsPaneOpen;
            Navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
            Navigation.IsPaneOpen = false;
            _navigationAutoCollapsed = true;
        }
        else if (!hideNavigation && _navigationAutoCollapsed)
        {
            Navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            Navigation.IsPaneOpen = _navigationWasOpenBeforeAutoCollapse;
            _navigationAutoCollapsed = false;
        }
        Navigation.OpenPaneLength = 226;
        ShellAppIcon.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        ShellIconColumn.Width = new GridLength(narrow ? 0 : 30);
        UpdateCaptionInset();
        ShellSearchHost.Visibility = narrow && !_shellSearchExpanded ? Visibility.Collapsed : Visibility.Visible;
        ShellSearchButton.Visibility = narrow && !_shellSearchExpanded ? Visibility.Visible : Visibility.Collapsed;
        UpdateShellHeader();
    }

    internal void UpdateCaptionInset()
    {
        if (ShellCaptionColumn is null) return;
        var titleBar = (Application.Current as App)?.MainWindowInstance?.AppWindow.TitleBar;
        var scale = (XamlRoot?.RasterizationScale ?? 1) * (Math.Clamp(_state.UiScale, 85, 125) / 100d);
        ShellCaptionColumn.Width = new GridLength(Math.Max(140, (titleBar?.RightInset ?? 0) / scale));
    }

    private void UpdateShellHeader()
    {
        if (ShellAppTitle is null) return;
        ShellAppTitle.Text = ActualWidth < 860 ? PageTitle.Text : "Tomatotodo";
        var canGoBack = _navigationHistory.Count > 0;
        ShellBackButton.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;
        ShellBackColumn.Width = canGoBack ? new GridLength(40) : new GridLength(0);
    }

    private void ShellPaneButton_Click(object sender, RoutedEventArgs e) => Navigation.IsPaneOpen = !Navigation.IsPaneOpen;

    private void ShellBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_navigationHistory.Count == 0) return;
        var route = _navigationHistory.Pop().Split(':', 2);
        _settingsSearchTarget = null;
        NavigateWorkspace(route[0], route.Length == 2 ? route[1] : null, remember: false);
    }

    private NavigationViewItem? FindNavigationItem(string section) => Navigation.MenuItems.OfType<NavigationViewItem>()
        .Concat(Navigation.FooterMenuItems.OfType<NavigationViewItem>())
        .FirstOrDefault(item => string.Equals(item.Tag as string, section, StringComparison.Ordinal));

    private void ShellSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var query = sender.Text.Trim();
        sender.ItemsSource = string.IsNullOrWhiteSpace(query) ? null : ShellSearchIndex()
            .Where(item => item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || item.Location.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Take(12).ToList();
    }

    private void ShellSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is ShellSearchEntry entry) NavigateFromShellSearch(entry);
    }

    private void ShellSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var entry = args.ChosenSuggestion as ShellSearchEntry ?? ShellSearchIndex().FirstOrDefault(item =>
            item.Title.Equals(args.QueryText, StringComparison.CurrentCultureIgnoreCase) || item.Location.Equals(args.QueryText, StringComparison.CurrentCultureIgnoreCase));
        entry ??= ShellSearchIndex().FirstOrDefault(item => item.Title.Contains(args.QueryText, StringComparison.CurrentCultureIgnoreCase));
        if (entry is not null) NavigateFromShellSearch(entry);
    }

    private void ShellSearchButton_Click(object sender, RoutedEventArgs e)
    {
        _shellSearchExpanded = true;
        ShellSearchHost.Visibility = Visibility.Visible;
        ShellSearchButton.Visibility = Visibility.Collapsed;
        ShellSearchBox.Focus(FocusState.Programmatic);
    }

    private void ShellSearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (ActualWidth >= 860 || !string.IsNullOrWhiteSpace(ShellSearchBox.Text)) return;
        _shellSearchExpanded = false;
        ShellSearchHost.Visibility = Visibility.Collapsed;
        ShellSearchButton.Visibility = Visibility.Visible;
    }

    private IEnumerable<(string Label, string Section)> ShellNavigationItems()
    {
        yield return (global::Tomatotodo_Windows.Data.UiText.T("\u4EEA\u8868\u76D8"), "dashboard");
        yield return (global::Tomatotodo_Windows.Data.UiText.T("\u914D\u7F6E"), "presets");
        yield return (global::Tomatotodo_Windows.Data.UiText.T("\u6863\u6848"), "archive");
        if (_state.CourseScheduleEnabled) yield return (global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868"), "courses");
        yield return (global::Tomatotodo_Windows.Data.UiText.T("\u5DE5\u5177"), "tools");
        yield return (global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4"), "general");
    }

    private IEnumerable<ShellSearchEntry> ShellSearchIndex()
    {
        foreach (var item in ShellNavigationItems()) yield return new ShellSearchEntry(item.Label, global::Tomatotodo_Windows.Data.UiText.T("\u5BFC\u822A"), item.Section, null);
        foreach (var group in SettingsGroups) yield return new ShellSearchEntry(group.Title, global::Tomatotodo_Windows.Data.UiText.F("\u5E38\u89C4 \u203A {0}", group.Description), "general", null, group.Id);
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4E2A\u6027\u5316"), "appearance", global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u6A21\u5F0F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u4E3B\u9898\u8272"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4E2A\u6027\u5316"), "appearance", global::Tomatotodo_Windows.Data.UiText.T("\u4E3B\u9898\u8272"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u4E9A\u514B\u529B\u78E8\u7802\u73BB\u7483"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4E2A\u6027\u5316"), "appearance", global::Tomatotodo_Windows.Data.UiText.T("\u4E9A\u514B\u529B\u78E8\u7802\u73BB\u7483"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u7EAF\u9ED1\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4E2A\u6027\u5316"), "appearance", global::Tomatotodo_Windows.Data.UiText.T("\u7EAF\u9ED1\u6A21\u5F0F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u5B57\u4F53\u7F29\u653E"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4E2A\u6027\u5316"), "appearance", global::Tomatotodo_Windows.Data.UiText.T("\u5B57\u4F53\u7F29\u653E"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868\u5BFC\u822A\u5165\u53E3"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u8BFE\u7A0B\u8868"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u6C89\u6D78\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u6C89\u6D78\u6A21\u5F0F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u5C0F\u7A97\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u5C0F\u7A97\u6A21\u5F0F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u65F6\u957F"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u4E0E\u77ED\u4F11"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u65F6\u957F"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u4E0E\u77ED\u4F11"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u542F\u7528\u77ED\u4F11"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u542F\u7528\u77ED\u4F11"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u5FAA\u73AF\u8BA1\u65F6\uFF08\u624B\u52A8\uFF0F\u81EA\u52A8\uFF09"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u5FAA\u73AF\u8BA1\u65F6"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u7ED3\u675F\u63D0\u9192\uFF08\u7CFB\u7EDF\u901A\u77E5\uFF0F\u97F3\u6548\uFF09"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u7ED3\u675F\u63D0\u9192"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u7ED3\u675F\u63D0\u9192\uFF08\u7CFB\u7EDF\u901A\u77E5\uFF0F\u97F3\u6548\uFF09"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u7ED3\u675F\u63D0\u9192"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5411\u8BA1\u65F6"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u901A\u7528\u8BBE\u7F6E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5411\u8BA1\u65F6"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u523B\u5EA6"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u523B\u5EA6"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u79D2\u9488"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u79D2\u9488"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u5012\u6570\u65E5\u540D\u79F0\u4E0E\u65E5\u671F"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u76EE\u6807\u540D\u79F0\u4E0E\u65E5\u671F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u542F\u8BFE\u7A0B\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u542F\u8BFE\u7A0B\u63D0\u9192"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u524D\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u9192\u65B9\u5F0F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u7CFB\u7EDF\u901A\u77E5"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u9192\u65B9\u5F0F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u8F6F\u4EF6\u6D88\u606F"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u9192\u65B9\u5F0F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u97F3\u6548"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u9192\u65B9\u5F0F"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u5929\u6C14\u4F4D\u7F6E\u4E0E\u57CE\u5E02"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u5929\u6C14\u4F4D\u7F6E"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u5A92\u4F53\u6765\u6E90"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u5A92\u4F53\u6765\u6E90"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u672C\u5730\u97F3\u4E50\u6587\u4EF6\u5939"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u4EEA\u8868\u76D8"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u672C\u5730\u97F3\u4E50"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868\u5BFC\u5165\u4E0E\u5BFC\u51FA"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u8BFE\u7A0B\u8868"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868\u6570\u636E"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u4E0E\u4E2A\u4EBA\u8D44\u6599"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u8D26\u6237\u4E0E\u7528\u6237\u6570\u636E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u4E0E\u4E2A\u4EBA\u8D44\u6599"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u7528\u6237\u6570\u636E\u5BFC\u5165\u4E0E\u5BFC\u51FA"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u8D26\u6237\u4E0E\u7528\u6237\u6570\u636E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u5168\u90E8\u672C\u673A\u6570\u636E"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u6062\u590D\u51FA\u5382\u6570\u636E"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u8D26\u6237\u4E0E\u7528\u6237\u6570\u636E"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u6E05\u9664\u539F\u751F\u6570\u636E"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u68C0\u67E5\u66F4\u65B0"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u5173\u4E8E\u4E0E\u66F4\u65B0"), "general", "Tomatotodo");
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u516C\u544A"), global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4 \u203A \u5173\u4E8E\u4E0E\u66F4\u65B0"), "general", global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u516C\u544A"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u7CFB\u7EDF\u4FBF\u7B3A"), global::Tomatotodo_Windows.Data.UiText.T("\u5DE5\u5177 \u203A \u5FEB\u6377\u4FBF\u7B3A"), "tools", global::Tomatotodo_Windows.Data.UiText.T("\u7CFB\u7EDF\u4FBF\u7B3A"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u7FFB\u8BD1"), global::Tomatotodo_Windows.Data.UiText.T("\u5DE5\u5177 \u203A \u5176\u4ED6\u5DE5\u5177"), "tools", global::Tomatotodo_Windows.Data.UiText.T("\u7FFB\u8BD1"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u8BA1\u7B97\u5668"), global::Tomatotodo_Windows.Data.UiText.T("\u5DE5\u5177 \u203A \u5176\u4ED6\u5DE5\u5177"), "tools", global::Tomatotodo_Windows.Data.UiText.T("\u8BA1\u7B97\u5668"));
        yield return new ShellSearchEntry(global::Tomatotodo_Windows.Data.UiText.T("\u79D2\u8868"), global::Tomatotodo_Windows.Data.UiText.T("\u5DE5\u5177 \u203A \u5176\u4ED6\u5DE5\u5177"), "tools", global::Tomatotodo_Windows.Data.UiText.T("\u79D2\u8868"));
    }

    private void NavigateFromShellSearch(ShellSearchEntry destination)
    {
        ShellSearchBox.Text = "";
        _settingsSearchTarget = destination.Highlight;
        _navigatingFromShellSearch = true;
        _shellSearchExpanded = false;
        if (ActualWidth < 860)
        {
            ShellSearchHost.Visibility = Visibility.Collapsed;
            ShellSearchButton.Visibility = Visibility.Visible;
        }
        NavigateWorkspace(destination.Section, destination.Section is "general" or "appearance" ? SettingsGroupForSearch(destination) : null);
        _navigatingFromShellSearch = false;
    }

    private void ScrollToSearchTarget()
    {
        if (_settingsSearchTargetElement is null) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_settingsSearchTargetElement is null) return;
            var point = _settingsSearchTargetElement.TransformToVisual(PageBody).TransformPoint(new Windows.Foundation.Point(0, 0));
            PageScroll.ChangeView(null, Math.Max(0, point.Y - 12), null, true);
        });
    }

    private void TrackSettingsSearch(string header, string description, FrameworkElement card)
    {
        if (string.IsNullOrWhiteSpace(_settingsSearchTarget) || _settingsSearchTargetElement is not null) return;
        if (!header.Contains(_settingsSearchTarget, StringComparison.CurrentCultureIgnoreCase) && !description.Contains(_settingsSearchTarget, StringComparison.CurrentCultureIgnoreCase)) return;
        if (card is Border border)
            border.Background = new SolidColorBrush(ColorHelper.FromArgb(32, 91, 173, 255));
        else if (card is Control control)
            control.Background = new SolidColorBrush(ColorHelper.FromArgb(32, 91, 173, 255));
        _settingsSearchTargetElement = card;
    }

    private sealed record ShellSearchEntry(string Title, string Location, string Section, string? Highlight, string? Group = null)
    {
        public override string ToString() => $"{Title} · {Location}";
    }

    private static UIElement NumberSetting(string label, int value, int min, int max, Action<int> update)
    {
        var box = new NumberBox { Header = label, Value = value, Minimum = min, Maximum = max,
            Width = 180, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        box.ValueChanged += (_, e) => { if (!double.IsNaN(e.NewValue)) update((int)e.NewValue); };
        return box;
    }

    private void RenderFuture(string title, string message)
    {
        var card = Card(title); card.Children.Add(Muted(message)); PageBody.Children.Add(card);
    }

    private void UpdateTimerDisplay()
    {
        if (_timerText is not null) _timerText.Text = $"{_remainingSeconds / 60:00}:{_remainingSeconds % 60:00}";
        DashboardStartIcon.Symbol = _running ? Symbol.Pause : Symbol.Play;
        DashboardStartLabel.Text = _running ? global::Tomatotodo_Windows.Data.UiText.T("\u6682\u505C") : _state.PositiveCountup ? global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u59CB\u8BA1\u65F6") : _breakPhase ? global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u59CB\u77ED\u4F11") : global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u59CB\u4E13\u6CE8");
        DashboardFooterTime.Text = $"{_remainingSeconds / 60:00}:{_remainingSeconds % 60:00}";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DashboardStartButton,
            $"{DashboardStartLabel.Text}，{DashboardFooterTime.Text}");
        if (_timerStatus is not null) _timerStatus.Text = (_state.PositiveCountup ? global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5411\u8BA1\u65F6 \u00B7 ") : _breakPhase ? global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11 \u00B7 ") : "") + (_running ? global::Tomatotodo_Windows.Data.UiText.T("\u8FDB\u884C\u4E2D") : global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u6682\u505C"));
        UpdateMiniWindow();
        if (_section == "dashboard") UpdateLiveWidgetClocks();
    }

    private static string FormatDuration(int seconds) => global::Tomatotodo_Windows.Data.UiText.F("{0} \u5C0F\u65F6 {1} \u5206\u949F", seconds / 3600, (seconds % 3600) / 60);

    private static CardPanel Card(string title)
    {
        var panel = new CardPanel();
        panel.Children.Add(new TextBlock { Text = title, FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        return panel;
    }

    private static TextBlock Muted(string text) => new()
    { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Margin = new Thickness(0, 4, 0, 4) };

    private static Button Action(string text, RoutedEventHandler click, bool primary = false)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 4, 0, 0) };
        if (primary) button.Style = (Style)Application.Current.Resources["TomatotodoAccentButtonStyle"];
        button.Click += click;
        return button;
    }

    private sealed class CardPanel : ContentControl
    {
        private readonly StackPanel _content = new() { Spacing = 10 };
        public UIElementCollection Children => _content.Children;

        public CardPanel()
        {
            Content = new Border
            {
                Background = new SolidColorBrush(ColorHelper.FromArgb(20, 128, 128, 128)),
                BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(42, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(22),
                Child = _content
            };
        }
    }

}
