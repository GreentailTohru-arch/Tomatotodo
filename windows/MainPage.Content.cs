using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tomatotodo_Windows.Data;
using Tomatotodo_Windows.Services;
using Windows.Devices.Geolocation;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private TextBlock? _weatherText, _weatherDetail, _quoteText;
    private TextBlock? _weatherTemperature;
    private Button? _weatherLocationButton;
    private TextBlock? _weatherSettingsStatus;
    private int _weatherRevision;
    private bool HasManualWeatherCity => _state.WeatherCityLatitude is >= -90 and <= 90 &&
        _state.WeatherCityLongitude is >= -180 and <= 180 && !string.IsNullOrWhiteSpace(_state.WeatherCityName);
    private WeatherSnapshot? _weather;
    private string _weatherStatus = global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5728\u5E38\u89C4 \u2192 \u4EEA\u8868\u76D8 \u2192 \u5929\u6C14\u8BBE\u7F6E\u4E2D\u9009\u62E9\u57CE\u5E02\u6216\u5141\u8BB8\u5B9A\u4F4D");
    private bool _weatherBusy;
    private DateTimeOffset _nextWeatherRefresh;
    private DateTimeOffset _nextQuoteRefresh;
    private bool _quoteBusy;
    private string _quote = OfflineQuotes.All[Random.Shared.Next(OfflineQuotes.All.Length)];
    private readonly Queue<string> _quoteHistory = new();

    private void UpdateDashboardContent()
    {
        UpdateMusicProgress();
        if ((_state.WeatherLocationConsent || HasManualWeatherCity) && DateTimeOffset.Now >= _nextWeatherRefresh && !_weatherBusy)
            _ = RefreshWeatherAsync();
        if (DateTimeOffset.Now >= _nextQuoteRefresh && !_quoteBusy) _ = RefreshQuoteAsync();
    }

    private UIElement BuildWeatherContent()
    {
        if (_weather is null && !string.IsNullOrEmpty(_state.CachedWeatherJson))
        {
            try
            {
                _weather = JsonSerializer.Deserialize<WeatherSnapshot>(_state.CachedWeatherJson);
                if (_weather is not null) _weatherStatus = (HasManualWeatherCity ? _state.WeatherCityName : global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u4F4D\u7F6E")) + global::Tomatotodo_Windows.Data.UiText.T(" \u00B7 \u7F13\u5B58\u5929\u6C14");
            }
            catch (JsonException) { }
        }
        var body = new Grid { ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        _weatherText = new TextBlock { FontSize = 18, TextWrapping = TextWrapping.Wrap };
        _weatherDetail = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Foreground = DashboardBrush("TextFillColorSecondaryBrush") };
        details.Children.Add(_weatherText); details.Children.Add(_weatherDetail); body.Children.Add(details);
        _weatherTemperature = new TextBlock { FontSize = 36, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_weatherTemperature, 1); body.Children.Add(_weatherTemperature);
        UpdateWeatherVisuals();
        return new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled };
    }

    private async Task ConfirmWeatherLocationAsync()
    {
        var owner = _state;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = global::Tomatotodo_Windows.Data.UiText.T("\u5141\u8BB8\u4F7F\u7528\u4F4D\u7F6E\u663E\u793A\u5929\u6C14\uFF1F"),
            Content = global::Tomatotodo_Windows.Data.UiText.T("\u5C06\u901A\u8FC7 Windows \u83B7\u53D6\u5F53\u524D\u4F4D\u7F6E\uFF0C\u5E76\u5C06\u7EA6 1 \u516C\u91CC\u7CBE\u5EA6\u7684\u7ECF\u7EAC\u5EA6\u53D1\u9001\u7ED9 Open-Meteo \u67E5\u8BE2\u5929\u6C14\u3002\u4E0D\u4F1A\u4FDD\u5B58\u7CBE\u786E\u4F4D\u7F6E\u3002\u5929\u6C14\u7EA6\u6BCF 10 \u5206\u949F\u66F4\u65B0\u4E00\u6B21\uFF0C\u4F60\u53EF\u4EE5\u968F\u65F6\u505C\u6B62\u5B9A\u4F4D\u3002"),
            PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u5141\u8BB8\u5E76\u83B7\u53D6\u5929\u6C14"), SecondaryButtonText = _state.WeatherLocationConsent ? global::Tomatotodo_Windows.Data.UiText.T("\u505C\u6B62\u5B9A\u4F4D") : "", CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), DefaultButton = ContentDialogButton.Close };
        var result = await ShowThemedDialogAsync(dialog);
        if (!ReferenceEquals(owner, _state)) return;
        if (result == ContentDialogResult.Secondary)
        {
            _weatherRevision++;
            _state.WeatherLocationConsent = false; _state.CachedWeatherJson = null; _weather = null;
            _weatherStatus = global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u505C\u6B62\u5B9A\u4F4D"); Save(); UpdateWeatherVisuals(); return;
        }
        if (result != ContentDialogResult.Primary) return;
        var access = await Geolocator.RequestAccessAsync();
        if (!ReferenceEquals(owner, _state)) return;
        _state.WeatherLocationConsent = access == GeolocationAccessStatus.Allowed;
        if (_state.WeatherLocationConsent)
        {
            _weatherRevision++; _state.WeatherCityName = null;
            _state.WeatherCityLatitude = _state.WeatherCityLongitude = null;
            _weather = null; _state.CachedWeatherJson = null; _nextWeatherRefresh = default;
        }
        Save();
        if (!_state.WeatherLocationConsent)
        { _weatherStatus = global::Tomatotodo_Windows.Data.UiText.T("\u672A\u83B7\u5F97 Windows \u5B9A\u4F4D\u6743\u9650\uFF0C\u53EF\u7A0D\u540E\u91CD\u8BD5"); UpdateWeatherVisuals(); return; }
        await RefreshWeatherAsync();
    }

    private async Task RefreshWeatherAsync()
    {
        if (_weatherBusy || (!_state.WeatherLocationConsent && !HasManualWeatherCity)) return;
        _weatherBusy = true;
        var owner = _state;
        var revision = _weatherRevision;
        _nextWeatherRefresh = DateTimeOffset.Now.AddMinutes(10);
        _weatherStatus = HasManualWeatherCity ? global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5728\u66F4\u65B0\u6240\u9009\u57CE\u5E02\u5929\u6C14\u2026") : global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5728\u66F4\u65B0\u5F53\u524D\u4F4D\u7F6E\u5929\u6C14\u2026"); UpdateWeatherVisuals();
        try
        {
            double latitude, longitude;
            if (HasManualWeatherCity)
            { latitude = _state.WeatherCityLatitude!.Value; longitude = _state.WeatherCityLongitude!.Value; }
            else
            {
                var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
                var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(15));
                latitude = position.Coordinate.Point.Position.Latitude; longitude = position.Coordinate.Point.Position.Longitude;
            }
            if (!ReferenceEquals(owner, _state) || revision != _weatherRevision) return;
            var weather = await DashboardContentService.GetWeatherAsync(Math.Round(latitude, 2), Math.Round(longitude, 2));
            if (!ReferenceEquals(owner, _state) || revision != _weatherRevision) return;
            _weather = weather; _weatherStatus = HasManualWeatherCity ? _state.WeatherCityName! : global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u4F4D\u7F6E");
            _state.CachedWeatherJson = JsonSerializer.Serialize(weather); Save();
        }
        catch
        {
            if (ReferenceEquals(owner, _state) && revision == _weatherRevision) _weatherStatus = _weather is null ? global::Tomatotodo_Windows.Data.UiText.T("\u5929\u6C14\u6682\u4E0D\u53EF\u7528\uFF0C\u8BF7\u68C0\u67E5\u7F51\u7EDC\u6216\u5B9A\u4F4D\u6743\u9650") : global::Tomatotodo_Windows.Data.UiText.T("\u6682\u672A\u66F4\u65B0 \u00B7 \u663E\u793A\u4E0A\u6B21\u5929\u6C14");
        }
        finally { _weatherBusy = false; UpdateWeatherVisuals(); }
    }

    private void UpdateWeatherVisuals()
    {
        if (_weatherSettingsStatus is not null) _weatherSettingsStatus.Text = (HasManualWeatherCity ? global::Tomatotodo_Windows.Data.UiText.T("\u624B\u52A8\u57CE\u5E02\uFF1A") + _state.WeatherCityName : _state.WeatherLocationConsent ? global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u52A8\u5B9A\u4F4D") : global::Tomatotodo_Windows.Data.UiText.T("\u672A\u9009\u62E9\u57CE\u5E02")) + " · " + _weatherStatus;
        if (_weatherLocationButton is not null) _weatherLocationButton.IsEnabled = !_weatherBusy;
        if (_weatherText is null || _weatherDetail is null) return;
        _weatherText.Text = _weather is null ? global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u5730\u5929\u6C14") : DashboardContentService.WeatherDescription(_weather.Code);
        _weatherDetail.Text = _weather is null ? _weatherStatus : HasManualWeatherCity ? _state.WeatherCityName : global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u4F4D\u7F6E");
        if (_weatherTemperature is not null)
        {
            _weatherTemperature.Text = _weather is null ? "—°" : $"{_weather.Temperature:0}°";
            ToolTipService.SetToolTip(_weatherTemperature, _weather is null ? _weatherStatus : global::Tomatotodo_Windows.Data.UiText.F("{0} \u00B7 \u4F53\u611F {1}\u00B0C \u00B7 {2}", _weatherStatus, $"{_weather.FeelsLike:0}", $"{_weather.UpdatedAt:MM/dd HH:mm}"));
        }
    }

    private void AddWeatherSettings(StackPanel page)
    {
        AddSection(page, IconGlyph.Location, global::Tomatotodo_Windows.Data.UiText.T("\u5929\u6C14\u8BBE\u7F6E"), global::Tomatotodo_Windows.Data.UiText.T("\u4EEA\u8868\u76D8\u5929\u6C14\u7684\u4F4D\u7F6E\u4E0E\u6570\u636E\u6765\u6E90"));
        var controls = new StackPanel { Spacing = 8 };
        _weatherSettingsStatus = new TextBlock { TextWrapping = TextWrapping.Wrap };
        controls.Children.Add(_weatherSettingsStatus);
        var actions = new StackPanel { Spacing = 8, Orientation = _compactSettingsLayout ? Orientation.Vertical : Orientation.Horizontal };
        _weatherLocationButton = Action(global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u52A8\u5B9A\u4F4D"), async (_, _) => await ConfirmWeatherLocationAsync());
        actions.Children.Add(_weatherLocationButton);
        actions.Children.Add(Action(global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u57CE\u5E02"), async (_, _) => await ChooseWeatherCityAsync()));
        actions.Children.Add(new HyperlinkButton { Content = "Open-Meteo / GeoNames", NavigateUri = new Uri("https://open-meteo.com/") });
        controls.Children.Add(actions);
        page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5929\u6C14\u4F4D\u7F6E"), global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u52A8\u6A21\u5F0F\u9700\u5141\u8BB8 Windows \u5B9A\u4F4D\uFF1B\u624B\u52A8\u9009\u62E9\u57CE\u5E02\u65E0\u9700\u8BFB\u53D6\u8BBE\u5907\u4F4D\u7F6E\u3002\u6BCF 10 \u5206\u949F\u66F4\u65B0\u3002"), controls, IconGlyph.Location));
        UpdateWeatherVisuals();
    }

    private async Task ChooseWeatherCityAsync()
    {
        var owner = _state;
        var input = new TextBox { Header = global::Tomatotodo_Windows.Data.UiText.T("\u57CE\u5E02\u540D\u79F0"), PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u4F8B\u5982\uFF1A\u5317\u4EAC / Tokyo"), MaxLength = 100 };
        var results = new ComboBox { Header = global::Tomatotodo_Windows.Data.UiText.T("\u5339\u914D\u57CE\u5E02"), DisplayMemberPath = "Label", HorizontalAlignment = HorizontalAlignment.Stretch };
        var status = new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u641C\u7D22\u65F6\u4EC5\u5C06\u8F93\u5165\u7684\u57CE\u5E02\u540D\u79F0\u53D1\u9001\u7ED9 Open-Meteo\u3002"), TextWrapping = TextWrapping.Wrap };
        var search = new Button { Content = global::Tomatotodo_Windows.Data.UiText.T("\u641C\u7D22\u57CE\u5E02") };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(input); content.Children.Add(search); content.Children.Add(results); content.Children.Add(status);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = global::Tomatotodo_Windows.Data.UiText.T("\u624B\u52A8\u9009\u62E9\u5929\u6C14\u57CE\u5E02"), Content = content,
            PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u4F7F\u7528\u6B64\u57CE\u5E02"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), DefaultButton = ContentDialogButton.Close, IsPrimaryButtonEnabled = false };
        var closed = false;
        input.TextChanged += (_, _) => { results.ItemsSource = null; dialog.IsPrimaryButtonEnabled = false; };
        results.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = results.SelectedItem is WeatherCity;
        search.Click += async (_, _) =>
        {
            var query = input.Text.Trim();
            if (query.Length < 2) { status.Text = global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u8F93\u5165\u81F3\u5C11\u4E24\u4E2A\u5B57\u7B26\u3002"); return; }
            search.IsEnabled = false; dialog.IsPrimaryButtonEnabled = false; results.ItemsSource = null;
            try
            {
                var cities = await DashboardContentService.SearchCitiesAsync(query);
                if (closed || input.Text.Trim() != query) return;
                results.ItemsSource = cities;
                status.Text = cities.Length == 0 ? global::Tomatotodo_Windows.Data.UiText.T("\u6CA1\u6709\u5339\u914D\u57CE\u5E02\uFF0C\u8BF7\u5C1D\u8BD5\u5B8C\u6574\u57CE\u5E02\u540D\u3001\u62FC\u97F3\u6216\u82F1\u6587\u540D\u3002") : global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u9009\u62E9\u5BF9\u5E94\u5730\u533A\u7684\u57CE\u5E02\u3002");
            }
            catch { if (!closed) status.Text = global::Tomatotodo_Windows.Data.UiText.T("\u57CE\u5E02\u641C\u7D22\u5931\u8D25\uFF0C\u8BF7\u68C0\u67E5\u7F51\u7EDC\u540E\u91CD\u8BD5\u3002"); }
            finally { search.IsEnabled = true; }
        };
        var result = await ShowThemedDialogAsync(dialog); closed = true;
        if (result != ContentDialogResult.Primary || results.SelectedItem is not WeatherCity city || !ReferenceEquals(owner, _state)) return;
        _weatherRevision++; _state.WeatherLocationConsent = false;
        _state.WeatherCityName = city.Label; _state.WeatherCityLatitude = Math.Round(city.Latitude, 2); _state.WeatherCityLongitude = Math.Round(city.Longitude, 2);
        _state.CachedWeatherJson = null; _weather = null; _nextWeatherRefresh = default;
        _weatherStatus = city.Label; Save(); UpdateWeatherVisuals(); await RefreshWeatherAsync();
    }

    private async Task RefreshQuoteAsync()
    {
        _quoteBusy = true;
        _nextQuoteRefresh = DateTimeOffset.Now.AddMinutes(7);
        var owner = _state;
        // Rotate immediately from the offline library, so a slow/offline API never stalls the widget.
        var pool = OfflineQuotes.All.Concat(_state.CachedFocusQuotes ?? []).Where(text => !_quoteHistory.Contains(text) && text != _quote).ToArray();
        _quote = pool[Random.Shared.Next(pool.Length)];
        _quoteHistory.Enqueue(_quote);
        while (_quoteHistory.Count > 50) _quoteHistory.Dequeue();
        if (_quoteText is not null) _quoteText.Text = _quote;
        try
        {
            var online = await DashboardContentService.GetQuoteAsync();
            if (online is null || !ReferenceEquals(owner, _state)) return;
            var text = $"{online.Text}\n— {online.Source}";
            _state.CachedFocusQuotes ??= [];
            if (!_state.CachedFocusQuotes.Contains(text)) _state.CachedFocusQuotes.Add(text);
            if (_state.CachedFocusQuotes.Count > 200) _state.CachedFocusQuotes.RemoveAt(0);
            Save(); // Online entries join the next rotation, avoiding a second change in one period.
        }
        catch { /* The built-in library remains available offline. */ }
        finally { _quoteBusy = false; }
    }
}
