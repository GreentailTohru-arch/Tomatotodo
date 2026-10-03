using Tomatotodo_Windows.Data;
foreach (var (selection, system, expected) in new[] {
    ("system", "zz-ZZ", "en-US"), ("system", "zh-HK", "zh-TW"),
    ("system", "zh-Hant", "zh-TW"), ("system", "zh-SG", "zh-CN"),
    ("system", "ja-JP", "ja"), ("zh-CN", "en-US", "zh-CN"),
})
    if (UiLanguage.Resolve(selection, system) != expected) throw new Exception($"Locale resolution failed: {selection}/{system}");
Console.WriteLine("Language resolution checks passed.");

foreach (var code in UiLanguage.Codes.Skip(1))
    if (UiLanguage.Resolve(code,null) != code) throw new Exception("Variant resolution failed: " + code);

var source = UiLanguage.Codes.Skip(1).ToArray();
foreach (var code in source) {
    UiText.Language = code;
    if (string.IsNullOrWhiteSpace(UiText.T("语言"))) throw new Exception("Missing language label: " + code);
    _ = UiText.Date(new DateTime(2026,10,3), "yyyy 年 M 月 d 日", "d");
    var formatted = UiText.F("专注 {0} 分钟 · 短休 {1} 分钟", 23, 7);
    if (!formatted.Contains("23") || !formatted.Contains("7") || formatted.Contains("{0}")) throw new Exception("Placeholder failure: " + code);
}
Console.WriteLine("All 41 language catalogs and formatted values passed.");

UiText.Language = "zh-CN";
_ = DashboardWidgets.All.Select(x => x.Label).ToArray();
foreach (var code in source) {
    UiText.Language = code;
    foreach (var widget in DashboardWidgets.All)
        if (widget.Label != UiText.T(widget.LabelKey) || widget.Detail != UiText.T(widget.DetailKey))
            throw new Exception("Stale widget translation: " + code + "/" + widget.Id);
    for (var day = 0; day < 7; day++)
        if (string.IsNullOrWhiteSpace(UiText.Weekday(day))) throw new Exception("Missing weekday: " + code);
}
Console.WriteLine("Live dashboard metadata and weekday labels passed for all 41 locales.");
