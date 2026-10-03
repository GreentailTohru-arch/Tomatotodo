using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tomatotodo_Windows.Services;

public sealed class AboutPerson
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("avatar")] public string Avatar { get; set; } = "";
}
public sealed class AboutContent
{
    [JsonPropertyName("developers")] public List<AboutPerson> Developers { get; set; } = [];
    [JsonPropertyName("thanks")] public List<AboutPerson> Thanks { get; set; } = [];
    [JsonPropertyName("sponsors")] public List<AboutPerson> Sponsors { get; set; } = [];
    [JsonPropertyName("sponsor_url")] public string SponsorUrl { get; set; } = "";
}

/// Public product metadata, never part of account backups or cloud user data.
public sealed class AboutService(HttpClient client, string cachePath)
{
    public static Uri? SafeLink(string? raw) => Uri.TryCreate(raw, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo) && !raw!.Any(char.IsWhiteSpace) ? uri : null;

    public static AboutContent Decode(string json)
    {
        var data = JsonSerializer.Deserialize<AboutContent>(json) ?? throw new JsonException(global::Tomatotodo_Windows.Data.UiText.T("\u540D\u5355\u4E3A\u7A7A"));
        if (data.Developers is null || data.Thanks is null || data.Sponsors is null
            || data.Developers.Count > 100 || data.Thanks.Count > 100 || data.Sponsors.Count > 200)
            throw new JsonException(global::Tomatotodo_Windows.Data.UiText.T("\u540D\u5355\u683C\u5F0F\u65E0\u6548"));
        foreach (var person in data.Developers.Concat(data.Thanks).Concat(data.Sponsors))
        {
            if (person is null || string.IsNullOrWhiteSpace(person.Name) || person.Name.Length > 80)
                throw new JsonException(global::Tomatotodo_Windows.Data.UiText.T("\u59D3\u540D\u683C\u5F0F\u65E0\u6548"));
            person.Description ??= ""; person.Avatar ??= ""; person.Url ??= "";
            if (SafeLink(person.Url) is null) person.Url = "";
        }
        // Sponsor portraits are labels rather than profile links.
        foreach (var sponsor in data.Sponsors) sponsor.Url = "";
        if (SafeLink(data.SponsorUrl) is null) data.SponsorUrl = "";
        return data;
    }
    public async Task<AboutContent?> CachedAsync()
    {
        try { return Decode(await File.ReadAllTextAsync(cachePath)); }
        catch { return null; }
    }
    public async Task<AboutContent> RefreshAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("https://47.245.57.193:8443/api/about", cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (json.Length > 5 * 1024 * 1024) throw new JsonException(global::Tomatotodo_Windows.Data.UiText.T("\u540D\u5355\u8FC7\u5927"));
        var data = Decode(json);
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var temporary = cachePath + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(data), cancellationToken);
            File.Move(temporary, cachePath, true);
        } catch (IOException) { /* Cache failure does not hide fresh content. */ }
        catch (UnauthorizedAccessException) { }
        return data;
    }
}
