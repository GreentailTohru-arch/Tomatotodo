using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tomatotodo_Windows.Data;

/// <summary>Offline interface resources. User-owned text never passes through this API.</summary>
public static class UiText
{
    public static string Date(DateTime value, string sourceFormat, string format)
    {
        if (Language.StartsWith("zh", StringComparison.Ordinal)) return value.ToString(sourceFormat);
        var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.GetCultureInfo(Language).Clone();
        // Timetable/archive dates are Gregorian even when the locale defaults to another calendar.
        culture.DateTimeFormat.Calendar = new System.Globalization.GregorianCalendar();
        return value.ToString(format, culture);
    }
    public static string Weekday(int mondayIndex) =>
        System.Globalization.CultureInfo.GetCultureInfo(Language).DateTimeFormat.AbbreviatedDayNames[(mondayIndex + 1) % 7];
    public static string Language { get; set; } = "zh-CN";
    private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Catalog = new(() =>
    {
        using var stream = typeof(UiText).Assembly.GetManifestResourceStream("Tomatotodo.Localization.Catalog")!;
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
    });
    public static string T(string key)
    {
        if (Language == "zh-CN") return key;
        if (Catalog.Value.TryGetValue(Language, out var translations) && translations.TryGetValue(key, out var text)) return text;
        return Catalog.Value.TryGetValue("en-US", out var fallback) && fallback.TryGetValue(key, out text) ? text : key;
    }
    public static string F(string key, params object?[] arguments) => Regex.Replace(T(key), @"\{(\d+)\}", match =>
        int.TryParse(match.Groups[1].Value, out var index) && index < arguments.Length ? arguments[index]?.ToString() ?? "" : match.Value);
}
