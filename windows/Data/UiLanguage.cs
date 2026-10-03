namespace Tomatotodo_Windows.Data;
public static class UiLanguage
{
    public static readonly string[] Codes = ["system", "ar", "bn", "bg", "ca", "zh-CN", "zh-TW", "hr", "cs", "da", "nl", "en-US", "en-GB", "fil", "fi", "fr", "de", "el", "he", "hi", "hu", "id", "it", "ja", "ko", "ms", "nb", "pl", "pt-BR", "pt-PT", "ro", "ru", "sr-Cyrl", "sr-Latn", "sk", "es-ES", "es-419", "sv", "th", "tr", "uk", "vi"];
    public static readonly Dictionary<string,string> Names = new() { ["ar"] = "العربية", ["bn"] = "বাংলা", ["bg"] = "Български", ["ca"] = "Català", ["zh-CN"] = "简体中文", ["zh-TW"] = "繁體中文", ["hr"] = "Hrvatski", ["cs"] = "Čeština", ["da"] = "Dansk", ["nl"] = "Nederlands", ["en-US"] = "English (US)", ["en-GB"] = "English (UK)", ["fil"] = "Filipino", ["fi"] = "Suomi", ["fr"] = "Français", ["de"] = "Deutsch", ["el"] = "Ελληνικά", ["he"] = "עברית", ["hi"] = "हिन्दी", ["hu"] = "Magyar", ["id"] = "Bahasa Indonesia", ["it"] = "Italiano", ["ja"] = "日本語", ["ko"] = "한국어", ["ms"] = "Bahasa Melayu", ["nb"] = "Norsk bokmål", ["pl"] = "Polski", ["pt-BR"] = "Português (Brasil)", ["pt-PT"] = "Português (Portugal)", ["ro"] = "Română", ["ru"] = "Русский", ["sr-Cyrl"] = "Српски (ћирилица)", ["sr-Latn"] = "Srpski (latinica)", ["sk"] = "Slovenčina", ["es-ES"] = "Español (España)", ["es-419"] = "Español (Latinoamérica)", ["sv"] = "Svenska", ["th"] = "ไทย", ["tr"] = "Türkçe", ["uk"] = "Українська", ["vi"] = "Tiếng Việt" };
    public static string Resolve(string preference, string? primarySystemLanguage)
    {
        var code = (preference == "system" ? primarySystemLanguage ?? "en-US" : preference).Replace('_','-');
        var parts = code.ToLowerInvariant().Split('-'); var language = parts[0];
        language = language switch { "iw" => "he", "in" => "id", "tl" => "fil", "no" => "nb", _ => language };
        if (language == "zh") return parts.Contains("hant") || parts.Any(p => p is "tw" or "hk" or "mo") ? "zh-TW" : "zh-CN";
        if (language == "en") return parts.Contains("gb") ? "en-GB" : "en-US";
        if (language == "pt") return parts.Skip(1).Contains("pt") ? "pt-PT" : "pt-BR";
        if (language == "es") return parts.Length == 1 || parts.Skip(1).Contains("es") ? "es-ES" : "es-419";
        if (language == "sr") return parts.Contains("latn") ? "sr-Latn" : "sr-Cyrl";
        return Codes.Contains(language) ? language : "en-US";
    }
    public static bool IsRightToLeft(string code) => code is "ar" or "he";
    public static string Label(string code, string resolved) => code == "system" ? UiText.T("跟随系统") : Names[Resolve(code,null)];
    public static string Title(string resolved) => UiText.T("语言");
}
