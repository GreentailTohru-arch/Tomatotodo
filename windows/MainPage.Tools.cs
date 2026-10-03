using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private void RenderTools()
    {
        var page = new StackPanel { Spacing = 8, Margin = new Thickness(0, 2, 0, 54) };

        page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u7CFB\u7EDF\u4FBF\u7B3A"), global::Tomatotodo_Windows.Data.UiText.T("\u5728\u72EC\u7ACB\u7684 Windows Sticky Notes \u7A97\u53E3\u4E2D\u521B\u5EFA\u3001\u67E5\u770B\u548C\u7BA1\u7406\u4FBF\u7B3A\u3002"), ToolLaunchButton(global::Tomatotodo_Windows.Data.UiText.T("\u6253\u5F00\u7CFB\u7EDF\u4FBF\u7B3A"), "shell:AppsFolder\\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe!App", "Sticky Notes"), IconGlyph.Note));
        page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u7FFB\u8BD1"), global::Tomatotodo_Windows.Data.UiText.T("\u5728\u9ED8\u8BA4\u6D4F\u89C8\u5668\u4E2D\u6253\u5F00 Bing \u7FFB\u8BD1\uFF0C\u9002\u5408\u4E34\u65F6\u67E5\u8BE2\u3002"), ToolLaunchButton(global::Tomatotodo_Windows.Data.UiText.T("\u6253\u5F00\u7FFB\u8BD1"), "https://www.bing.com/translator", global::Tomatotodo_Windows.Data.UiText.T("Bing \u7FFB\u8BD1")), IconGlyph.Characters));
        page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u8BA1\u7B97\u5668"), global::Tomatotodo_Windows.Data.UiText.T("\u76F4\u63A5\u542F\u52A8 Windows \u7CFB\u7EDF\u8BA1\u7B97\u5668\u3002"), ToolLaunchButton(global::Tomatotodo_Windows.Data.UiText.T("\u6253\u5F00\u8BA1\u7B97\u5668"), "calc.exe", global::Tomatotodo_Windows.Data.UiText.T("\u8BA1\u7B97\u5668")), IconGlyph.Calculator));
        page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u79D2\u8868"), global::Tomatotodo_Windows.Data.UiText.T("\u542F\u52A8 Windows \u65F6\u949F\uFF1B\u53EF\u5728\u5176\u4E2D\u4F7F\u7528\u7CFB\u7EDF\u79D2\u8868\u548C\u5206\u6BB5\u8BB0\u5F55\u3002"), ToolLaunchButton(global::Tomatotodo_Windows.Data.UiText.T("\u6253\u5F00 Windows \u65F6\u949F"), "shell:AppsFolder\\Microsoft.WindowsAlarms_8wekyb3d8bbwe!App", global::Tomatotodo_Windows.Data.UiText.T("Windows \u65F6\u949F")), IconGlyph.Stopwatch));

        PageBody.Children.Add(page);
    }

    private Button ToolLaunchButton(string label, string target, string toolName)
    {
        var button = new Button
        {
            Content = IconLabel(IconGlyph.OpenExternal, label),
            MinWidth = _compactSettingsLayout ? 0 : 150,
            HorizontalAlignment = _compactSettingsLayout ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            Padding = new Thickness(12, 4, 12, 4),
            MinHeight = 32,
            Style = (Style)Application.Current.Resources["TomatotodoAccentButtonStyle"]
        };
        button.Click += async (_, _) => await LaunchSystemTool(target, toolName);
        return button;
    }

    private async Task LaunchSystemTool(string target, string toolName)
    {
        try
        {
            var systemApp = target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase);
            Process.Start(systemApp
                ? new ProcessStartInfo { FileName = "explorer.exe", Arguments = target, UseShellExecute = true }
                : new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            await ShowCourseError(global::Tomatotodo_Windows.Data.UiText.F("\u65E0\u6CD5\u6253\u5F00{0}", toolName), global::Tomatotodo_Windows.Data.UiText.F("Windows \u672A\u80FD\u542F\u52A8\u8BE5\u5DE5\u5177\u3002\u8BF7\u4ECE\u5F00\u59CB\u83DC\u5355\u6253\u5F00\u5B83\u3002\n\n{0}", exception.Message));
        }
    }
}
