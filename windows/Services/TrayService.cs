using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Tomatotodo_Windows.Services;

internal sealed record TrayEntry(string Text, Action? Execute = null);

internal sealed partial class TrayService : IDisposable
{
    private readonly nint _hwnd;
    private readonly SubclassProc _callback;
    private readonly Action _show, _exit;
    private readonly Func<IReadOnlyList<TrayEntry>> _entries;
    private readonly Func<bool> _isDark;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private NotifyData _data;
    private bool _visible;
    private bool _disposed;
    private const uint CallbackMessage = 0x8001;
    public TrayService(Window window, Action show, Action exit, Func<IReadOnlyList<TrayEntry>> entries, Func<bool> isDark, Func<bool> monochrome)
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _show = show; _exit = exit; _entries = entries; _isDark = isDark; _callback = HandleMessage;
        _data = new NotifyData { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = _hwnd, Id = 1,
            Flags = 7, Callback = CallbackMessage, Tip = "Tomatotodo", Info = "", Title = "",
            Icon = LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), 1, 0, 0, 0x10 | 0x40) };
        if (!SetWindowSubclass(_hwnd, _callback, 1, 0)) throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u521D\u59CB\u5316\u6258\u76D8\u7A97\u53E3\u3002"));
        _monochrome = monochrome;
        _iconThemeTimer.Tick += (_, _) => RefreshIcon();
        _iconThemeTimer.Start();
        RefreshIcon();
    }
    public bool Show()
    {
        if (_visible) return true;
        _visible = ShellNotifyIcon(0, ref _data);
        return _visible;
    }
    public void Hide() { if (_visible) ShellNotifyIcon(2, ref _data); _visible = false; }
    private nint HandleMessage(nint window, uint message, nuint wparam, nint lparam, nuint id, nuint reference)
    {
        if (message == 0x121 && wparam == 2) RoundMenuWindow(lparam); // WM_ENTERIDLE / MSGF_MENU
        if (HandleMenuDrawing(message, lparam)) return 1;
        if (message == _taskbarCreated && _visible)
        {
            _visible = false;
            if (!Show()) _show(); // Never strand a hidden window after Explorer restarts.
        }
        if (message == CallbackMessage)
        {
            var action = (uint)lparam & 0xffff;
            if (action == 0x202 || action == 0x203) _show();
            if (action == 0x205 || action == 0x7b)
            {
                var menu = CreatePopupMenu();
                try
                {
                    BeginMenuTheme(menu);
                    AddThemedItem(menu, 1, global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u4E3B\u754C\u9762"), true);
                    var entries = _entries();
                    AddThemedSeparator(menu, 3);
                    for (var index = 0; index < entries.Count; index++)
                        AddThemedItem(menu, (uint)(index + 10), entries[index].Text, entries[index].Execute is not null);
                    AddThemedSeparator(menu, 4);
                    AddThemedItem(menu, 2, global::Tomatotodo_Windows.Data.UiText.T("\u9000\u51FA\u8F6F\u4EF6"), true);
                    GetCursorPos(out var point); SetForegroundWindow(_hwnd);
                    var selected = TrackPopupMenu(menu, 0x100 | 0x2, point.X, point.Y, 0, _hwnd, 0);
                    if (selected == 1) _show();
                    if (selected == 2) _exit();
                    if (selected >= 10 && selected - 10 < entries.Count)
                        entries[(int)selected - 10].Execute?.Invoke();
                    PostMessage(_hwnd, 0, 0, 0);
                }
                finally { DestroyMenu(menu); EndMenuTheme(); }
            }
            return 0;
        }
        return DefSubclassProc(window, message, wparam, lparam);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Hide(); RemoveWindowSubclass(_hwnd, _callback, 1);
        _iconThemeTimer.Stop();
        if (_data.Icon != 0) DestroyIcon(_data.Icon);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    private delegate nint SubclassProc(nint hwnd, uint msg, nuint wp, nint lp, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)] private static extern bool ShellNotifyIcon(uint msg, ref NotifyData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint msg, nuint wp, nint lp);
}
