using System.Net.Http;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Tomatotodo_Windows.Data;
using Tomatotodo_Windows.Services;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private bool _showingAnnouncements;
    private async Task ShowVersionAnnouncementsAsync(bool manual = false)
    {
        if (_showingAnnouncements) return;
        _showingAnnouncements = true;
        int shown = 0;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            var json = await client.GetStringAsync($"https://47.245.57.193:8443/api/announcements?platform=windows&version={InstalledVersion}");
            using var document = JsonDocument.Parse(json);
            var path = Path.Combine(StateStore.DirectoryPath, "read-announcements.json");
            HashSet<string> read;
            try { read = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path)) ?? []; }
            catch { read = []; }
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var id = row.GetProperty("id").GetString()!;
                var urgent = row.GetProperty("severity").GetString() == "urgent";
                if (!row.GetProperty("active").GetBoolean() || row.GetProperty("platform").GetString() != "windows"
                    || AppUpdateService.ParseVersion(row.GetProperty("version").GetString()!) != InstalledVersion
                    || (!manual && !urgent && read.Contains(id))) continue;
                if (!IsLoaded) return;
                var color = new SolidColorBrush(IsDarkAppearance ? (urgent ? ColorHelper.FromArgb(255, 255, 180, 171) : ColorHelper.FromArgb(255, 124, 219, 155)) : urgent ? ColorHelper.FromArgb(255, 196, 43, 28) : ColorHelper.FromArgb(255, 22, 131, 74));
                var heading = new Grid { ColumnDefinitions = { new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
                heading.Children.Add(new TextBlock { Text = urgent ? global::Tomatotodo_Windows.Data.UiText.T("\u7D27\u6025\u516C\u544A") : global::Tomatotodo_Windows.Data.UiText.T("\u7248\u672C\u516C\u544A"), Foreground = color, VerticalAlignment = VerticalAlignment.Center });
                var content = new StackPanel { Spacing = 12, MaxWidth = 460 };
                content.Children.Add(new TextBlock { Text = row.GetProperty("title").GetString(), FontSize = 22, TextWrapping = TextWrapping.Wrap });
                content.Children.Add(new TextBlock { Text = row.GetProperty("subtitle").GetString(), TextWrapping = TextWrapping.Wrap });
                var dialog = new ContentDialog { Title = heading, Content = new ScrollViewer { Content = content, MaxHeight = 400 }, PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u6211\u77E5\u9053\u4E86"), DefaultButton = ContentDialogButton.Primary };
                bool acknowledged = false;
                dialog.PrimaryButtonClick += (_, _) => acknowledged = true;
                shown++;
                await ShowThemedDialogAsync(dialog);
                if (acknowledged && !urgent)
                {
                    read.Add(id); Directory.CreateDirectory(StateStore.DirectoryPath);
                    File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(read));
                    File.Move(path + ".tmp", path, true);
                }
            }
            if (manual && shown == 0) ShowAppMessage(global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u516C\u544A"), global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u7248\u672C\u6682\u65E0\u516C\u544A\u3002"), InfoBarSeverity.Informational);
        }
        catch { if (manual) ShowAppMessage(global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u516C\u544A"), global::Tomatotodo_Windows.Data.UiText.T("\u516C\u544A\u52A0\u8F7D\u5931\u8D25\uFF0C\u8BF7\u68C0\u67E5\u7F51\u7EDC\u540E\u91CD\u8BD5\u3002"), InfoBarSeverity.Warning); }
        finally { _showingAnnouncements = false; }
    }
}
