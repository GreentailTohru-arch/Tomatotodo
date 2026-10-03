using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Tomatotodo_Windows.Views;

internal static class FloatingWindowChrome
{
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    public static double Scale(Window window) => Math.Max(96, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window))) / 96d;
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint SendMessage(nint window, uint message, nint wparam, nint lparam);
    [DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref uint value, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    public static void RemoveFrame(Window window)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        // The capsule region is the sole outline. DWM's rectangular non-client
        // frame otherwise survives WinUI's hidden title bar and clips as white arcs.
        var style = GetWindowLongPtr(handle, -16).ToInt64();
        SetWindowLongPtr(handle, -16, (nint)(style & ~0x00C40000L)); // CAPTION | THICKFRAME
        uint disabled = 1, noBorder = 0xFFFFFFFE, square = 1;
        DwmSetWindowAttribute(handle, 2, ref disabled, sizeof(uint)); // NCRENDERING_POLICY
        DwmSetWindowAttribute(handle, 34, ref noBorder, sizeof(uint)); // BORDER_COLOR (Win11)
        DwmSetWindowAttribute(handle, 33, ref square, sizeof(uint)); // no second corner mask
        SetWindowPos(handle, 0, 0, 0, 0, 0, 0x0037); // FRAMECHANGED, no move/size/z-order/activation
    }

    public static void SetOwner(Window child, Window owner) =>
        SetWindowLongPtr(WinRT.Interop.WindowNative.GetWindowHandle(child), -8, WinRT.Interop.WindowNative.GetWindowHandle(owner));

    public static void Drag(Window window)
    {
        ReleaseCapture();
        SendMessage(WinRT.Interop.WindowNative.GetWindowHandle(window), 0xA1, 2, 0);
    }

    public static void SnapToEdge(Window window)
    {
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(window.AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest).WorkArea;
        var position = window.AppWindow.Position;
        var size = window.AppWindow.Size;
        var threshold = (int)Math.Round(20 * Scale(window));
        var x = Data.WindowEdgeSnap.Coordinate(position.X, size.Width, area.X, area.Width, threshold);
        var y = Data.WindowEdgeSnap.Coordinate(position.Y, size.Height, area.Y, area.Height, threshold);
        if (x != position.X || y != position.Y)
            window.AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    public static void Round(Window window)
    {
        RemoveFrame(window);
        var size = window.AppWindow.Size;
        var region = CreateRoundRectRgn(0, 0, size.Width + 1, size.Height + 1, size.Height, size.Height);
        // On success Windows owns the region; otherwise release it ourselves.
        if (SetWindowRgn(WinRT.Interop.WindowNative.GetWindowHandle(window), region, true) == 0) DeleteObject(region);
    }
}
