using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Tomatotodo_Windows.Data;
using Tomatotodo_Windows.Services;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private readonly AppUpdateService _updates = new(StateStore.DirectoryPath);
    private bool _updateStartupChecked;
    private bool _checkingUpdates;

    private static Version InstalledVersion
    {
        get
        {
            try
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return new Version(version.Major, version.Minor, version.Build, version.Revision);
            }
            catch { return typeof(MainPage).Assembly.GetName().Version ?? new Version(1, 5, 0, 0); }
        }
    }

    private void InitializeUpdates()
    {
        Loaded += async (_, _) =>
        {
            if (_updateStartupChecked) return;
            _updateStartupChecked = true;
            await Task.Delay(1500);
            if (IsLoaded) await CheckForUpdatesAsync(false);
            if (IsLoaded) await ShowVersionAnnouncementsAsync();
        };
    }

    private UIElement UpdateSettingsCard()
    {
        var button = Action(global::Tomatotodo_Windows.Data.UiText.T("\u68C0\u67E5\u66F4\u65B0"), async (sender, _) => await CheckForUpdatesAsync(true, sender as Button));
        button.IsEnabled = !_checkingUpdates;
        var card = GeneralCard("Tomatotodo", global::Tomatotodo_Windows.Data.UiText.F("\u7248\u672C {0}", InstalledVersion), button, IconGlyph.Refresh);
        card.HeaderIcon = new BitmapIcon
        {
            UriSource = new Uri("ms-appx:///Assets/Square150x150Logo.scale-200.png"),
            ShowAsMonochrome = false,
            Width = 48, Height = 48
        };
        return card;
    }

    private async Task CheckForUpdatesAsync(bool manual, Button? button = null)
    {
        if (_checkingUpdates) return;
        _checkingUpdates = true;
        if (button != null) { button.IsEnabled = false; button.Content = global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5728\u68C0\u67E5\u2026"); }
        try
        {
            var release = await _updates.GetLatestAsync();
            if (release == null || AppUpdateService.ParseVersion(release.Version) <= InstalledVersion)
            {
                if (manual) ShowAppMessage(global::Tomatotodo_Windows.Data.UiText.T("\u68C0\u67E5\u66F4\u65B0"), global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u662F\u6700\u65B0\u7248\u3002"), InfoBarSeverity.Success);
                return;
            }
            if (!manual && _updates.IsSkipped(release.Version)) return;
            if (!IsLoaded) return;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, RequestedTheme = ActualTheme,
                Title = global::Tomatotodo_Windows.Data.UiText.T("\u53D1\u73B0\u65B0\u7248\u672C"),
                Content = global::Tomatotodo_Windows.Data.UiText.F("\u5F53\u524D\u7248\u672C\uFF1A{0}\n\u6700\u65B0\u7248\u672C\uFF1A{1}\n\n\u70B9\u51FB\u66F4\u65B0\u5C06\u5728\u6D4F\u89C8\u5668\u6253\u5F00\u4E0B\u8F7D\u94FE\u63A5\u3002", InstalledVersion, release.Version),
                PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u66F4\u65B0"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), SecondaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u8DF3\u8FC7\u6B64\u7248\u672C"),
                DefaultButton = ContentDialogButton.Primary
            };
            var result = await ShowThemedDialogAsync(dialog);
            if (result == ContentDialogResult.Primary)
            {
                if (!await Windows.System.Launcher.LaunchUriAsync(AppUpdateService.DownloadUri(release.DownloadUrl)))
                    ShowAppMessage(global::Tomatotodo_Windows.Data.UiText.T("\u66F4\u65B0"), global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u6253\u5F00\u6D4F\u89C8\u5668\uFF0C\u8BF7\u7A0D\u540E\u91CD\u8BD5\u3002"), InfoBarSeverity.Error);
            }
            else if (result == ContentDialogResult.Secondary) _updates.Skip(release.Version);
        }
        catch (Exception)
        {
            // 启动检查失败静默处理，网络故障不阻断客户端使用。
            if (manual) ShowAppMessage(global::Tomatotodo_Windows.Data.UiText.T("\u68C0\u67E5\u66F4\u65B0"), global::Tomatotodo_Windows.Data.UiText.T("\u6682\u65F6\u65E0\u6CD5\u68C0\u67E5\u66F4\u65B0\uFF0C\u8BF7\u68C0\u67E5\u7F51\u7EDC\u540E\u91CD\u8BD5\u3002"), InfoBarSeverity.Warning);
        }
        finally
        {
            _checkingUpdates = false;
            if (button != null) { button.IsEnabled = true; button.Content = global::Tomatotodo_Windows.Data.UiText.T("\u68C0\u67E5\u66F4\u65B0"); }
        }
    }
}
