using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Tomatotodo_Windows.Services;

public sealed record WeatherSnapshot(double Temperature, int Code, double FeelsLike, DateTimeOffset UpdatedAt);
public sealed record FocusQuote(string Text, string Source);
public sealed record WeatherCity(string Label, double Latitude, double Longitude);

public static class DashboardContentService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static async Task<WeatherCity[]> SearchCitiesAsync(string name)
    {
        var response = await Client.GetStringAsync("https://geocoding-api.open-meteo.com/v1/search?count=50&language=zh&name=" + Uri.EscapeDataString(name.Trim()));
        return ParseCities(response);
    }

    public static WeatherCity[] ParseCities(string response)
    {
        using var json = JsonDocument.Parse(response);
        if (!json.RootElement.TryGetProperty("results", out var results)) return [];
        var places = results.EnumerateArray().Select(city => new
        {
            Feature = city.TryGetProperty("feature_code", out var feature) ? feature.GetString() ?? "" : "",
            Population = city.TryGetProperty("population", out var population) && population.ValueKind == JsonValueKind.Number ? population.GetInt64() : 0,
            City = new WeatherCity(
                string.Join(" · ", new[] { "name", "admin1", "country" }.Select(key => city.TryGetProperty(key, out var value) ? value.GetString() : null).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()),
                city.GetProperty("latitude").GetDouble(), city.GetProperty("longitude").GetDouble())
        }).ToArray();
        return places.Where(place => place.Feature is "PPLC" or "PPLA" or "PPLA2" or "PPLA3"
            || (place.Feature is "PPL" or "PPLA4" or "PPLA5" && place.Population >= 10_000))
            .Select(place => place.City).ToArray();
    }

    public static async Task<WeatherSnapshot> GetWeatherAsync(double latitude, double longitude)
    {
        var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude:F2}&longitude={longitude:F2}&current=temperature_2m,apparent_temperature,weather_code&timezone=auto");
        using var json = JsonDocument.Parse(await Client.GetStringAsync(url));
        var current = json.RootElement.GetProperty("current");
        return new(current.GetProperty("temperature_2m").GetDouble(), current.GetProperty("weather_code").GetInt32(),
            current.GetProperty("apparent_temperature").GetDouble(), DateTimeOffset.Now);
    }

    public static async Task<FocusQuote?> GetQuoteAsync()
    {
        using var json = JsonDocument.Parse(await Client.GetStringAsync("https://v1.hitokoto.cn/?c=d&c=k&c=e&min_length=8&max_length=65"));
        var root = json.RootElement;
        var text = root.GetProperty("hitokoto").GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 120) return null;
        // The public feed is broad: retain only study, growth and perseverance-related entries.
        if (!new[] { global::Tomatotodo_Windows.Data.UiText.T("\u52AA\u529B"), global::Tomatotodo_Windows.Data.UiText.T("\u575A\u6301"), global::Tomatotodo_Windows.Data.UiText.T("\u5B66\u4E60"), global::Tomatotodo_Windows.Data.UiText.T("\u68A6\u60F3"), global::Tomatotodo_Windows.Data.UiText.T("\u884C\u52A8"), global::Tomatotodo_Windows.Data.UiText.T("\u65F6\u95F4"), global::Tomatotodo_Windows.Data.UiText.T("\u6210\u957F"), global::Tomatotodo_Windows.Data.UiText.T("\u52C7\u6C14"), global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8"), global::Tomatotodo_Windows.Data.UiText.T("\u524D\u8FDB"), global::Tomatotodo_Windows.Data.UiText.T("\u594B\u6597"), global::Tomatotodo_Windows.Data.UiText.T("\u5E0C\u671B"), global::Tomatotodo_Windows.Data.UiText.T("\u77E5\u8BC6") }.Any(text.Contains)) return null;
        var source = root.TryGetProperty("from", out var from) ? from.GetString() : null;
        return new(text, string.IsNullOrWhiteSpace(source) ? global::Tomatotodo_Windows.Data.UiText.T("\u4E00\u8A00") : global::Tomatotodo_Windows.Data.UiText.F("{0} \u00B7 \u4E00\u8A00", source));
    }

    public static string WeatherDescription(int code) => code switch
    {
        0 => global::Tomatotodo_Windows.Data.UiText.T("\u6674"), 1 => global::Tomatotodo_Windows.Data.UiText.T("\u5927\u90E8\u6674\u6717"), 2 => global::Tomatotodo_Windows.Data.UiText.T("\u591A\u4E91"), 3 => global::Tomatotodo_Windows.Data.UiText.T("\u9634"), 45 or 48 => global::Tomatotodo_Windows.Data.UiText.T("\u96FE"),
        51 or 53 or 55 => global::Tomatotodo_Windows.Data.UiText.T("\u6BDB\u6BDB\u96E8"), 56 or 57 or 66 or 67 => global::Tomatotodo_Windows.Data.UiText.T("\u51BB\u96E8"),
        61 => global::Tomatotodo_Windows.Data.UiText.T("\u5C0F\u96E8"), 63 => global::Tomatotodo_Windows.Data.UiText.T("\u4E2D\u96E8"), 65 => global::Tomatotodo_Windows.Data.UiText.T("\u5927\u96E8"), 71 or 73 or 75 or 77 => global::Tomatotodo_Windows.Data.UiText.T("\u96EA"),
        80 or 81 or 82 => global::Tomatotodo_Windows.Data.UiText.T("\u9635\u96E8"), 85 or 86 => global::Tomatotodo_Windows.Data.UiText.T("\u9635\u96EA"), 95 or 96 or 99 => global::Tomatotodo_Windows.Data.UiText.T("\u96F7\u96E8"), _ => global::Tomatotodo_Windows.Data.UiText.T("\u5929\u6C14\u66F4\u65B0\u4E2D")
    };
}
