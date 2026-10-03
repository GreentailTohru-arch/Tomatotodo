using System.Runtime.InteropServices;

namespace Tomatotodo_Windows.Services;

internal sealed partial class TrayService
{
    private readonly Dictionary<uint, (string Text, bool Enabled)> _menuItems = new();
    private nint _menuBrush, _menuFont;
    private bool _drawMenu, _darkMenu;
    private double _menuScale;
    private nint _roundedMenu;
    private SubclassProc? _menuFrameCallback;
    private void AddThemedSeparator(nint menu, uint id)
    {
        if (!_drawMenu) { AppendMenu(menu, 0x800, 0, ""); return; }
        AddThemedItem(menu, id, "", false);
    }
    private int Px(double value) => (int)Math.Round(value * _menuScale);
    private static uint Gray(byte value) => (uint)(value | value << 8 | value << 16);

    private void BeginMenuTheme(nint menu)
    {
        _drawMenu = !new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        if (!_drawMenu) return;
        _darkMenu = _isDark();
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, 2);
        _menuScale = GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96d : 1;
        _menuBrush = CreateSolidBrush(Gray(_darkMenu ? (byte)40 : (byte)249));
        _menuFont = CreateFont(-Px(14), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        var info = new MenuInfo { Size = (uint)Marshal.SizeOf<MenuInfo>(), Mask = 2, Background = _menuBrush };
        SetMenuInfo(menu, ref info);
    }
    private void AddThemedItem(nint menu, uint id, string text, bool enabled)
    {
        _menuItems[id] = (text, enabled);
        AppendMenu(menu, (enabled ? 0u : 2u) | (_drawMenu ? 0x100u : 0u), id, text.Replace("&", "&&"));
    }
    private void EndMenuTheme()
    {
        _menuItems.Clear(); _drawMenu = false; _roundedMenu = 0;
        if (_menuBrush != 0) DeleteObject(_menuBrush);
        if (_menuFont != 0) DeleteObject(_menuFont);
        _menuBrush = _menuFont = 0;
    }
    private bool HandleMenuDrawing(uint message, nint pointer)
    {
        if (!_drawMenu) return false;
        if (message == 0x2C) // WM_MEASUREITEM
        {
            var item = Marshal.PtrToStructure<MeasureItem>(pointer);
            if (item.Type != 1 || !_menuItems.TryGetValue(item.Id, out var entry)) return false;
            var dc = GetDC(_hwnd); var previous = SelectObject(dc, _menuFont);
            try
            {
                GetTextExtentPoint32(dc, entry.Text, entry.Text.Length, out var size);
                item.Width = (uint)Math.Clamp(size.X + Px(24), Px(180), Px(340));
                item.Height = (uint)Px(entry.Text.Length == 0 ? 9 : 32);
                Marshal.StructureToPtr(item, pointer, false);
            }
            finally { SelectObject(dc, previous); ReleaseDC(_hwnd, dc); }
            return true;
        }
        if (message == 0x2B) // WM_DRAWITEM
        {
            var item = Marshal.PtrToStructure<DrawItem>(pointer);
            if (item.Type != 1 || !_menuItems.TryGetValue(item.Id, out var entry)) return false;
            var selected = entry.Enabled && (item.State & 1) != 0;
            var background = CreateSolidBrush(Gray(_darkMenu ? (selected ? (byte)62 : (byte)40) : (selected ? (byte)232 : (byte)249)));
            var saved = SaveDC(item.Dc);
            try
            {
                FillRect(item.Dc, ref item.Rect, _menuBrush);
                if (entry.Text.Length == 0)
                {
                    var line = new Rect { Left = item.Rect.Left + Px(12), Right = item.Rect.Right - Px(12),
                        Top = (item.Rect.Top + item.Rect.Bottom) / 2 };
                    line.Bottom = line.Top + 1;
                    var stroke = CreateSolidBrush(Gray(_darkMenu ? (byte)62 : (byte)224));
                    try { FillRect(item.Dc, ref line, stroke); }
                    finally { DeleteObject(stroke); }
                    return true;
                }
                if (selected)
                {
                    var oldBrush = SelectObject(item.Dc, background);
                    var oldPen = SelectObject(item.Dc, GetStockObject(8)); // NULL_PEN
                    RoundRect(item.Dc, item.Rect.Left + Px(4), item.Rect.Top + Px(2),
                        item.Rect.Right - Px(4), item.Rect.Bottom - Px(2), Px(8), Px(8));
                    SelectObject(item.Dc, oldPen); SelectObject(item.Dc, oldBrush);
                }
                SetBkMode(item.Dc, 1);
                SetTextColor(item.Dc, Gray(_darkMenu ? (entry.Enabled ? (byte)248 : (byte)184) : (entry.Enabled ? (byte)28 : (byte)96)));
                SelectObject(item.Dc, _menuFont);
                item.Rect.Left += Px(12); item.Rect.Right -= Px(12);
                DrawText(item.Dc, entry.Text, entry.Text.Length, ref item.Rect, 0x20 | 0x4 | 0x800 | 0x8000);
            }
            finally { RestoreDC(item.Dc, saved); DeleteObject(background); }
            return true;
        }
        return false;
    }
    private void RoundMenuWindow(nint menuWindow)
    {
        // WM_ENTERIDLE supplies the actual popup HWND, not the HMENU.
        if (!_drawMenu || menuWindow == 0 || menuWindow == _roundedMenu) return;
        if (!GetWindowRect(menuWindow, out var bounds)) return;
        _menuFrameCallback ??= DrawMenuFrame;
        SetWindowSubclass(menuWindow, _menuFrameCallback, 2, 0);
        // Prefer compositor antialiasing and shadow to a binary GDI region on Windows 11.
        var corner = 2; // DWMWCP_ROUND
        var dark = _darkMenu ? 1 : 0;
        DwmSetWindowAttribute(menuWindow, 20, ref dark, sizeof(int));
        if (DwmSetWindowAttribute(menuWindow, 33, ref corner, sizeof(int)) == 0)
        {
            _roundedMenu = menuWindow;
            RedrawWindow(menuWindow, 0, 0, 0x401); // invalidate frame
            return;
        }
        var region = CreateRoundRectRgn(0, 0, bounds.Right - bounds.Left + 1,
            bounds.Bottom - bounds.Top + 1, Px(16), Px(16));
        if (region == 0) return;
        if (SetWindowRgn(menuWindow, region, true) == 0) DeleteObject(region);
        else _roundedMenu = menuWindow; // Windows owns the region after success.
    }
    private nint DrawMenuFrame(nint window, uint message, nuint wp, nint lp, nuint id, nuint data)
    {
        var result = DefSubclassProc(window, message, wp, lp);
        if (message == 0x82) RemoveWindowSubclass(window, _menuFrameCallback!, 2);
        if (message != 0x85 || !_drawMenu || !GetWindowRect(window, out var bounds)) return result;
        var dc = GetWindowDC(window);
        if (dc == 0) return result;
        var stroke = CreateSolidBrush(Gray(_darkMenu ? (byte)62 : (byte)216));
        try
        {
            var frame = new Rect { Right = bounds.Right - bounds.Left, Bottom = bounds.Bottom - bounds.Top };
            FrameRect(dc, ref frame, stroke);
            for (var i = 1; i <= 2; i++)
            {
                frame.Left++; frame.Top++; frame.Right--; frame.Bottom--;
                FrameRect(dc, ref frame, _menuBrush);
            }
        }
        finally { DeleteObject(stroke); ReleaseDC(window, dc); }
        return result;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);
    [DllImport("user32.dll")] private static extern nint GetWindowDC(nint window);
    [DllImport("user32.dll")] private static extern int FrameRect(nint dc, ref Rect rect, nint brush);
    [DllImport("user32.dll")] private static extern bool RedrawWindow(nint window, nint rect, nint region, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);
    [DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int index);
    [DllImport("gdi32.dll")] private static extern bool RoundRect(nint dc, int left, int top, int right, int bottom, int width, int height);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MeasureItem { public uint Type, ControlId, Id, Width, Height; public nuint Data; }
    [StructLayout(LayoutKind.Sequential)] private struct DrawItem { public uint Type, ControlId, Id, Action, State; public nint Window, Dc; public Rect Rect; public nuint Data; }
    [StructLayout(LayoutKind.Sequential)] private struct MenuInfo { public uint Size, Mask, Style, MaxHeight; public nint Background; public uint HelpId; public nuint Data; }
    [DllImport("user32.dll")] private static extern bool SetMenuInfo(nint menu, ref MenuInfo info);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateFont(int height, int width, int escape, int orientation, int weight, uint italic, uint underline, uint strike, uint charset, uint output, uint clip, uint quality, uint pitch, string face);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern int SaveDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool RestoreDC(nint dc, int saved);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool GetTextExtentPoint32(nint dc, string text, int length, out Point size);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("user32.dll")] private static extern int FillRect(nint dc, ref Rect rect, nint brush);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawText(nint dc, string text, int count, ref Rect rect, uint flags);
}
