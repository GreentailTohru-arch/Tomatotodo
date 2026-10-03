using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Tomatotodo_Windows;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private Microsoft.Windows.AppLifecycle.AppInstance? _primaryInstance;
    public Services.NotificationService Notifications { get; } = new();
    public Window? MainWindowInstance => _window;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        Data.UiText.Language = Services.LanguagePreference.Resolved;
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Services.LanguagePreference.Resolved;
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            try
            {
                var directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tomatotodo", "Native");
                Directory.CreateDirectory(directory);
                File.WriteAllText(System.IO.Path.Combine(directory, "startup-error.txt"), args.Exception?.ToString() ?? args.Message);
            }
            catch { }
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (_window is MainWindow existing) { existing.ShowMainWindow(); return; }
        var activation = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        _primaryInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("Tomatotodo.Desktop.SingleInstance");
        if (!_primaryInstance.IsCurrent)
        {
            await _primaryInstance.RedirectActivationToAsync(activation);
            Exit();
            return;
        }
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _primaryInstance.Activated += (_, _) => dispatcher.TryEnqueue(() =>
        {
            if (_window is MainWindow main) main.ShowMainWindow();
        });
#if DEBUG
        if (args.Arguments.Contains("--notification-smoke-test", StringComparison.Ordinal))
            Environment.SetEnvironmentVariable("TOMATOTODO_NOTIFICATION_SMOKE_TEST", "1");
#endif
        _window = new MainWindow();
        Notifications.Initialize(_window);
        _window.Closed += (_, _) => { Notifications.Dispose(); _primaryInstance.UnregisterKey(); };
        _window.Activate();
#if DEBUG
        if (Environment.GetEnvironmentVariable("TOMATOTODO_NOTIFICATION_SMOKE_TEST") == "1")
            ((MainWindow)_window).PreviewNotifications();
#endif
    }
}
