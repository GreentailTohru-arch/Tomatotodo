using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private string? _generalGroup;
    private bool _updatingSettingsNavigation;
    private bool IsAppearanceSettings => _section == "general" && _generalGroup == "appearance";
    private string NavigationRoute => _section == "general" && _generalGroup is not null ? $"general:{_generalGroup}" : _section;
    private string GeneralPageTitle => SettingsGroups.FirstOrDefault(group => group.Id == _generalGroup)?.Title ?? global::Tomatotodo_Windows.Data.UiText.T("\u5E38\u89C4");

    private sealed record SettingsGroup(string Id, string Title, string Description, string Icon);
    private static SettingsGroup[] SettingsGroups =>
    [
        new("common", global::Tomatotodo_Windows.Data.UiText.T("\u901A\u7528\u8BBE\u7F6E"), global::Tomatotodo_Windows.Data.UiText.T("\u8BA1\u65F6\u8282\u594F\u3001\u6B63\u5411\u8BA1\u65F6\u3001\u5C0F\u7A97\u4E0E\u6C89\u6D78\u6A21\u5F0F"), "\uE713"),
        new("dashboard", global::Tomatotodo_Windows.Data.UiText.T("\u4EEA\u8868\u76D8"), global::Tomatotodo_Windows.Data.UiText.T("\u5706\u8868\u3001\u5012\u6570\u65E5\u3001\u8BFE\u7A0B\u63D0\u9192\u3001\u5929\u6C14\u4E0E\u97F3\u4E50"), "\uF246"),
        new("course", global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868"), global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u5165\u53E3\u3001\u8BFE\u7A0B\u8868\u5BFC\u5165\u4E0E\u5BFC\u51FA"), IconGlyph.Calendar),
        new("appearance", global::Tomatotodo_Windows.Data.UiText.T("\u4E2A\u6027\u5316"), global::Tomatotodo_Windows.Data.UiText.T("\u5916\u89C2\u6A21\u5F0F\u3001\u4E3B\u9898\u8272\u3001\u4E9A\u514B\u529B\u3001\u5B57\u4F53\u7F29\u653E"), IconGlyph.Color),
        new("account", global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u4E0E\u7528\u6237\u6570\u636E"), global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u4E0E\u4E2A\u4EBA\u8D44\u6599\u3001\u6570\u636E\u5907\u4EFD\u4E0E\u6062\u590D"), "\uE77B"),
        new("about", global::Tomatotodo_Windows.Data.UiText.T("\u5173\u4E8E\u4E0E\u66F4\u65B0"), global::Tomatotodo_Windows.Data.UiText.T("\u8F6F\u4EF6\u7248\u672C\u3001\u68C0\u67E5\u66F4\u65B0\u3001\u5F53\u524D\u516C\u544A"), "\uE946")
    ];

    private void SettingsParentButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateSettingsGroup(null);
    }

    private void NavigateSettingsGroup(string? group)
    {
        _settingsSearchTarget = null;
        NavigateWorkspace("general", group);
    }

    private void NavigateWorkspace(string section, string? group = null, bool remember = true)
    {
        if (section == "appearance") { section = "general"; group = "appearance"; }
        var changed = _section != section || _generalGroup != group;
        if (changed && remember) _navigationHistory.Push(NavigationRoute);
        if (_section == "dashboard" && section != "dashboard")
        {
            StopLongPress(); _dashboardEditing = false; _draftLayout = null;
            DashboardDrawerLayer.Visibility = Visibility.Collapsed;
        }
        _section = section;
        _generalGroup = section == "general" ? group : null;
        _updatingSettingsNavigation = true;
        try { Navigation.SelectedItem = FindNavigationItem(section); }
        finally { _updatingSettingsNavigation = false; }
        PresetDrawerLayer.Visibility = Visibility.Collapsed;
        if (changed) PageScroll.ChangeView(null, 0, null, true);
        Render();
    }

    private static string? SettingsGroupForSearch(ShellSearchEntry entry)
    {
        if (entry.Section == "appearance") return "appearance";
        if (entry.Group is not null) return entry.Group;
        if (entry.Highlight is null) return null;
        foreach (var (group, keys) in new (string, string[])[] {
            ("course", ["课程表", "课表", "课程表数据"]),
            ("common", ["沉浸模式", "小窗模式", "专注与短休", "启用短休", "循环计时", "正向计时", "专注结束提醒", "短休结束提醒"]),
            ("account", ["账户与个人资料", "全部本机数据", "清除原生数据"]),
            ("about", ["Tomatotodo", "当前公告"])
        })
            if (keys.Any(key => entry.Highlight == key || entry.Highlight == Data.UiText.T(key))) return group;
        return "dashboard";
    }
}
