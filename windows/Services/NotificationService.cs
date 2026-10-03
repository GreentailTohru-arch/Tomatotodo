using System.Security;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

namespace Tomatotodo_Windows.Services;

public sealed class NotificationService : IDisposable
{
    private Window? _window;
    private bool _registered;

    public void Initialize(Window window)
    {
        _window = window;
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnInvoked;
            AppNotificationManager.Default.Register();
            _registered = true;
            Trace("Registered");
        }
        catch (Exception error) { Trace($"Notification registration: {error}"); }
    }

    private void OnInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args) =>
        _window?.DispatcherQueue.TryEnqueue(() => { _window.AppWindow.Show(); _window.Activate(); });

    public void Show(string title, string message, bool silent = false)
    {
        if (!_registered) return;
        try
        {
            var xml = $"<toast><visual><binding template='ToastGeneric'><text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(message)}</text></binding></visual>"
                + (silent ? "<audio silent='true'/>" : "") + "</toast>";
            var notification = new AppNotification(xml) { Expiration = DateTimeOffset.Now.AddHours(1) };
            AppNotificationManager.Default.Show(notification);
            Trace($"Delivered id={notification.Id}; setting={AppNotificationManager.Default.Setting}");
        }
        catch (Exception error) { Trace($"Notification delivery: {error}"); }
    }

    internal static void Trace(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
#if DEBUG
        if (Environment.GetEnvironmentVariable("TOMATOTODO_NOTIFICATION_SMOKE_TEST") == "1")
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "Tomatotodo-notification-smoke.log"), $"{DateTimeOffset.Now:O} {message}\n");
#endif
    }

    public void Dispose()
    {
        if (!_registered) return;
        AppNotificationManager.Default.NotificationInvoked -= OnInvoked;
        AppNotificationManager.Default.Unregister();
        _registered = false;
        _window = null;
    }
}
