using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Tomatotodo_Windows.Services;

public sealed record AppReleaseInfo(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("download_url")] string DownloadUrl);

public sealed class AppUpdateService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _skipPath;
    public AppUpdateService(string directory) => _skipPath = Path.Combine(directory, "skipped-update.txt");

    public static Version ParseVersion(string value)
    {
        if (!Regex.IsMatch(value, @"\A(?:0|[1-9][0-9]{0,4})(?:\.(?:0|[1-9][0-9]{0,4})){1,3}\z"))
            throw new FormatException(global::Tomatotodo_Windows.Data.UiText.T("\u7248\u672C\u53F7\u683C\u5F0F\u65E0\u6548\u3002"));
        var parts = value.Split('.').Select(int.Parse).ToArray();
        if (parts.Any(part => part > 65535) || parts.All(part => part == 0)) throw new FormatException(global::Tomatotodo_Windows.Data.UiText.T("\u7248\u672C\u53F7\u8D85\u51FA\u8303\u56F4\u3002"));
        return new Version(parts[0], parts[1], parts.Length > 2 ? parts[2] : 0, parts.Length > 3 ? parts[3] : 0);
    }

    public static Uri DownloadUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || value.Any(char.IsWhiteSpace))
            throw new FormatException(global::Tomatotodo_Windows.Data.UiText.T("\u66F4\u65B0\u4E0B\u8F7D\u94FE\u63A5\u65E0\u6548\u3002"));
        return uri;
    }

    public async Task<AppReleaseInfo?> GetLatestAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://47.245.57.193:8443/api/releases/latest");
        request.Headers.CacheControl = new() { NoCache = true };
        using var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<AppReleaseInfo>();
        if (release != null) { ParseVersion(release.Version); DownloadUri(release.DownloadUrl); }
        return release;
    }

    public bool IsSkipped(string version)
    {
        try { return File.Exists(_skipPath) && ParseVersion(File.ReadAllText(_skipPath).Trim()) == ParseVersion(version); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException) { return false; }
    }

    public void Skip(string version)
    {
        var normalized = ParseVersion(version).ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(_skipPath)!);
        var temporary = _skipPath + ".tmp";
        File.WriteAllText(temporary, normalized);
        File.Move(temporary, _skipPath, true);
    }
}
