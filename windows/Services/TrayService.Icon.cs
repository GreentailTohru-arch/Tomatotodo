using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Win32;

namespace Tomatotodo_Windows.Services;

internal sealed partial class TrayService
{
    private readonly Func<bool> _monochrome;
    private readonly DispatcherTimer _iconThemeTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private string? _iconStyle;

    public void RefreshIcon()
    {
        if (_disposed) return;
        bool light = true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = key?.GetValue("SystemUsesLightTheme") is not int value || value != 0;
        }
        catch { }
        var style = _monochrome() ? light ? "black" : "white" : "color";
        if (style == _iconStyle) return;
        var icon = style == "color"
            ? LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), 1, 0, 0, 0x10 | 0x40)
            : CreateMonochromeIcon(style == "white");
        if (icon == 0) return;
        var previous = _data.Icon;
        _data.Icon = icon;
        if (_visible && !ShellNotifyIcon(1, ref _data))
        {
            _data.Icon = previous;
            DestroyIcon(icon);
            return;
        }
        _iconStyle = style;
        if (previous != 0) DestroyIcon(previous);
    }

    // Code-native silhouette derived from the supplied logo: rounded shell,
    // tomato cutout, sprout and check. Transparent negative space stays legible
    // on any taskbar wallpaper; supersampling keeps the small icon smooth.
    private static nint CreateMonochromeIcon(bool white)
    {
        const int size = 32;
        var pixels = new byte[size * size * 4];
        var mask = new byte[size * size / 8];
        static double Segment(double x, double y, double ax, double ay, double bx, double by)
        {
            var t = Math.Clamp(((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / ((bx - ax) * (bx - ax) + (by - ay) * (by - ay)), 0, 1);
            return Math.Sqrt(Math.Pow(x - ax - t * (bx - ax), 2) + Math.Pow(y - ay - t * (by - ay), 2));
        }
        static bool Shape(double x, double y)
        {
            var dx = Math.Max(Math.Abs(x - 16) - 8, 0);
            var dy = Math.Max(Math.Abs(y - 16) - 8, 0);
            if (dx * dx + dy * dy > 49) return false;
            var tomato = Math.Pow((x - 16) / 9, 2) + Math.Pow((y - 17) / 7, 2) < 1;
            var sprout = Segment(x, y, 16, 6, 16, 8) < .9 ||
                Segment(x, y, 14, 7.5, 16, 9) < 1 || Segment(x, y, 16, 9, 18, 7.5) < 1;
            var check = Segment(x, y, 12.5, 17, 15, 19.5) < 1.4 || Segment(x, y, 15, 19.5, 20, 14.5) < 1.4;
            return (!tomato && !sprout) || check;
        }
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var count = 0;
            for (var sy = 0; sy < 4; sy++)
            for (var sx = 0; sx < 4; sx++)
                if (Shape(x + (sx + .5) / 4, y + (sy + .5) / 4)) count++;
            var alpha = (byte)(count * 255 / 16);
            var offset = (y * size + x) * 4;
            pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = white ? alpha : (byte)0;
            pixels[offset + 3] = alpha;
            if (alpha == 0) mask[y * 4 + x / 8] |= (byte)(0x80 >> (x % 8));
        }
        return CreateIcon(0, size, size, 1, 32, mask, pixels);
    }

    [DllImport("user32.dll")] private static extern nint CreateIcon(nint instance, int width, int height, byte planes, byte bits, byte[] andBits, byte[] xorBits);
}
