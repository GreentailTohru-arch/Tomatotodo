using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Tomatotodo_Windows;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private Services.TrayService? _tray;
    private bool _exitRequested, _closePromptOpen;
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
        if (RootFrame.Content is MainPage page)
        {
            SetTitleBar(page.ShellTitleBar);
            AppWindow.Changed += (_, _) => page.UpdateCaptionInset();
            page.Loaded += (_, _) => page.UpdateCaptionInset();
            ConfigureCaptionButtons(page.IsDarkForShell, page.ShellBackgroundColor);
            page.ThemeAppearanceChanged += ConfigureCaptionButtons;
            EnsureTray(page);
            page.TrayIconStyleChanged += () => _tray?.RefreshIcon();
        }
        AppWindow.Closing += OnClosing;
        Closed += (_, _) => { _tray?.Dispose(); (RootFrame.Content as MainPage)?.FlushOnClose(); };
    }

    internal void ShowMainWindow()
    {
        AppWindow.IsShownInSwitchers = true;
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter) presenter.Restore();
        Activate();
    }

    private bool EnsureTray(MainPage page)
    {
        try
        {
            _tray ??= new Services.TrayService(this,
                () => DispatcherQueue.TryEnqueue(ShowMainWindow),
                () => DispatcherQueue.TryEnqueue(ExitApplication), page.BuildTrayEntries, () => page.IsDarkForShell, () => page.UseMonochromeTrayIcon);
            return _tray.Show();
        }
        catch { return false; }
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exitRequested) return;
        args.Cancel = true;
        if (_closePromptOpen || RootFrame.Content is not MainPage page) return;
        _closePromptOpen = true;
        try
        {
            var choice = page.CloseBehavior switch
            {
                "tray" => Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary,
                "exit" => Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary,
                _ => await page.AskCloseAsync()
            };
            if (choice == Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary)
            { page.RememberCloseBehavior("exit"); ExitApplication(); return; }
            if (choice != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary) return;
            page.RememberCloseBehavior("tray");
            if (EnsureTray(page)) { AppWindow.Hide(); return; }
            page.LeaveImmersiveForBackground();
            if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.Minimize();
        }
        catch (InvalidOperationException) { /* An existing modal dialog must be dismissed first. */ }
        finally { _closePromptOpen = false; }
    }

#if DEBUG
    internal void PreviewNotifications() => (RootFrame.Content as MainPage)?.PreviewNotifications();
#endif

    private void ConfigureCaptionButtons(bool dark, Windows.UI.Color background)
    {
        // SystemBackdrop 从 Window.Content 的主题解析材质，而不是子 Page。
        // 只设置 Page 会让手动日间模式仍使用 Windows 的深色亚克力。
        if (Content is FrameworkElement root)
            root.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        var acrylic = RootFrame.Content is MainPage { UseShellAcrylic: true };
        if (acrylic && SystemBackdrop is not Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop)
            SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        else if (!acrylic && SystemBackdrop is not Microsoft.UI.Xaml.Media.MicaBackdrop)
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        if (acrylic) background = Colors.Transparent;
        if (!AppWindowTitleBar.IsCustomizationSupported()) return;
        var titleBar = AppWindow.TitleBar;
        var foreground = dark ? Colors.White : ColorHelper.FromArgb(255, 28, 28, 28);
        var inactiveForeground = dark ? ColorHelper.FromArgb(255, 175, 175, 175)
            : ColorHelper.FromArgb(255, 110, 110, 110);
        var hover = dark ? ColorHelper.FromArgb(255, 58, 58, 58)
            : ColorHelper.FromArgb(255, 225, 225, 225);
        var pressed = dark ? ColorHelper.FromArgb(255, 72, 72, 72)
            : ColorHelper.FromArgb(255, 210, 210, 210);
        titleBar.BackgroundColor = background;
        titleBar.InactiveBackgroundColor = background;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonPressedBackgroundColor = pressed;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
    }
}
