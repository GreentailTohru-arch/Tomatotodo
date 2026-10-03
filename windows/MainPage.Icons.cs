using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Tomatotodo_Windows;

// Segoe Fluent Icons: https://learn.microsoft.com/windows/apps/design/iconography/segoe-fluent-icons-font
internal static class IconGlyph
{
    public const string Power = "\uE7E8", ChromeClose = "\uE8BB";
    public const string Navigation = "\uE700", Brightness = "\uE706", Location = "\uE707", Note = "\uE70B";
    public const string FullScreen = "\uE740", Up = "\uE74A", Delete = "\uE74D", Refresh = "\uE72C";
    public const string Calendar = "\uE787", CalendarDay = "\uE8BF", Color = "\uE790", Contrast = "\uE7A1";
    public const string Education = "\uE7BE", Document = "\uE8A5", AcrylicLayers = "\uE89A", OpenExternal = "\uE8A7", Folder = "\uE8B7";
    public const string Characters = "\uE8C1", Sort = "\uE8CB", FontSize = "\uE8E9", Repeat = "\uE8EE";
    public const string Calculator = "\uE8EF", Stopwatch = "\uE916", Clock = "\uE917", History = "\uE81C";
    public const string Completed = "\uE930", MiniWindow = "\uE944", CheckList = "\uE9D5", Chart = "\uE9D2";
    public const string Media = "\uEA69", Bell = "\uEA8F", Coffee = "\uEC32", Grid = "\uF0E2", Drag = "\uE784";
}

public sealed partial class MainPage
{
    private static FontIcon FluentIcon(string glyph, double size = 20, Brush? foreground = null)
    {
        var icon = new FontIcon
        {
            Glyph = glyph, FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            FontSize = size, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center
        };
        if (foreground is not null) icon.Foreground = foreground;
        AutomationProperties.SetAccessibilityView(icon, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        return icon;
    }

    private static StackPanel IconLabel(string glyph, string label, double textSize = 14, Brush? foreground = null, bool wrap = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(FluentIcon(glyph, 16, foreground));
        var text = new TextBlock { Text = label, FontSize = textSize, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        if (foreground is not null) text.Foreground = foreground;
        row.Children.Add(text);
        return row;
    }
}
