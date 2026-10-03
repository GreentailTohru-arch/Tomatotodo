using Tomatotodo_Windows.Data;

namespace Tomatotodo_Windows.Services;

/// <summary>Device preference, outside account/cloud user data.</summary>
public static class LanguagePreference
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tomatotodo", "Native", "ui-language.txt");
    public static string Selected { get; private set; } = Load();
    public static string Resolved => UiLanguage.Resolve(Selected, Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault());
    private static string Load()
    {
        try { var code = File.ReadAllText(FilePath).Trim(); return UiLanguage.Codes.Contains(code) ? code : code == "en" ? "en-US" : "system"; }
        catch (IOException) { return "system"; }
        catch (UnauthorizedAccessException) { return "system"; }
    }
    public static void Set(string code)
    {
        if (!UiLanguage.Codes.Contains(code)) throw new ArgumentException("Unsupported language", nameof(code));
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, code);
        File.Move(temporary, FilePath, true);
        Selected = code;
        Data.UiText.Language = Resolved;
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Resolved;
    }
}
