using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Net.Http;
using Tomatotodo_Windows.Data;
using Tomatotodo_Windows.Services;
using Windows.Storage.Streams;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private FrameworkElement BuildAboutCredits()
    {
        var body = new StackPanel { Spacing = 20 };
        var host = new Border { Padding = new Thickness(20), Child = body,
            Background = DashboardBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = DashboardBrush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        var cancellation = new CancellationTokenSource();
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        host.Unloaded += (_, _) => { cancellation.Cancel(); client.Dispose(); };
        host.Loaded += async (_, _) => {

            var service = new AboutService(client, Path.Combine(StateStore.DirectoryPath, "Cache", "about-credits.json"));
            var cached = await service.CachedAsync();
            if (cancellation.IsCancellationRequested) return;
            if (cached is not null) Render(cached);
            else body.Children.Add(new ProgressRing { IsActive = true, Width = 28, Height = 28 });
            await Fetch();
            async Task Fetch() {
                try {
                    var data = await service.RefreshAsync(cancellation.Token);
                    if (!cancellation.IsCancellationRequested) { cached = data; Render(data); }
                } catch (Exception) {
                    if (cancellation.IsCancellationRequested) return;
                    if (cached is not null) Render(cached); else body.Children.Clear();
                    var status = new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational,
                        Message = cached is null ? global::Tomatotodo_Windows.Data.UiText.T("\u6682\u65F6\u65E0\u6CD5\u52A0\u8F7D\u540D\u5355\uFF0C\u8BF7\u7A0D\u540E\u91CD\u8BD5\u3002") : global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u663E\u793A\u5DF2\u7F13\u5B58\u7684\u540D\u5355\u3002") };
                    var retry = new Button { Content = global::Tomatotodo_Windows.Data.UiText.T("\u91CD\u8BD5") };
                    retry.Click += async (_, _) => { retry.IsEnabled = false; await Fetch(); };
                    status.ActionButton = retry; body.Children.Add(status);
                }
            }
        };
        return host;

        TextBlock Heading(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] };
        async Task Open(string raw) {
            var uri = AboutService.SafeLink(raw); if (uri is null) return;
            try { await Windows.System.Launcher.LaunchUriAsync(uri); }
            catch { /* Browser availability must not prevent displaying credits. */ }
        }
        Button Avatar(AboutPerson person, bool clickable)
        {
            var picture = new PersonPicture { DisplayName = person.Name, Initials = System.Globalization.StringInfo.GetNextTextElement(person.Name.Trim()), Width = 48, Height = 48 };
            var button = new Button { Width = 56, Height = 56, Padding = new Thickness(3), CornerRadius = new CornerRadius(28),
                Background = DashboardBrush("SubtleFillColorTransparentBrush"), BorderThickness = new Thickness(1),
                BorderBrush = DashboardBrush("CardStrokeColorDefaultBrush"), Content = picture };
            AutomationProperties.SetName(button, person.Name); ToolTipService.SetToolTip(button, person.Name);
            if (clickable && AboutService.SafeLink(person.Url) is not null) button.Click += async (_, _) => await Open(person.Url);
            if (person.Avatar.StartsWith("data:image/png;base64,", StringComparison.Ordinal)) {
                picture.Loaded += async (_, _) => {
                    try {
                        var bytes = Convert.FromBase64String(person.Avatar.Split(',')[1]);
                        using var stream = new InMemoryRandomAccessStream();
                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
                        stream.Seek(0); var image = new BitmapImage(); await image.SetSourceAsync(stream); picture.ProfilePicture = image;
                    } catch { /* Keep initial when an avatar cannot decode. */ }
                };
            }
            return button;
        }
        Grid Person(AboutPerson person, string? sponsorship)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(Avatar(person, true));
            var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = person.Name, FontSize = 16, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(person.Description)) text.Children.Add(new TextBlock { Text = person.Description, Foreground = DashboardBrush("TextFillColorSecondaryBrush"), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(text, 1); row.Children.Add(text);
            if (sponsorship is not null) {
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var sponsor = new Button { Content = new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u8D5E\u52A9 Tomatotodo"), TextWrapping = TextWrapping.Wrap }, MaxWidth = 150, IsEnabled = AboutService.SafeLink(sponsorship) is not null, VerticalAlignment = VerticalAlignment.Center };
                sponsor.Click += async (_, _) => await Open(sponsorship); Grid.SetColumn(sponsor, 2); row.Children.Add(sponsor);
            }
            return row;
        }
        void Render(AboutContent data)
        {
            body.Children.Clear(); body.Children.Add(Heading(global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u53D1\u8005")));
            for (var i = 0; i < data.Developers.Count; i++) body.Children.Add(Person(data.Developers[i], i == 0 ? data.SponsorUrl : null));
            if (data.Developers.Count == 0) body.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u540D\u5355\u5F85\u6DFB\u52A0") });
            body.Children.Add(Heading(global::Tomatotodo_Windows.Data.UiText.T("\u7279\u522B\u9E23\u8C22")));
            foreach (var person in data.Thanks) body.Children.Add(Person(person, null));
            if (data.Thanks.Count == 0) body.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u540D\u5355\u5F85\u6DFB\u52A0") });
            body.Children.Add(Heading(global::Tomatotodo_Windows.Data.UiText.T("\u8D5E\u52A9\u8005")));
            var supporters = new GridView { SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = false, IsTabStop = false, HorizontalAlignment = HorizontalAlignment.Stretch };
            ScrollViewer.SetVerticalScrollBarVisibility(supporters, ScrollBarVisibility.Disabled); ScrollViewer.SetVerticalScrollMode(supporters, ScrollMode.Disabled);
            foreach (var person in data.Sponsors) supporters.Items.Add(Avatar(person, false));
            body.Children.Add(supporters);
            if (data.Sponsors.Count == 0) body.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u611F\u8C22\u6BCF\u4E00\u4EFD\u652F\u6301") });
        }
    }
}
