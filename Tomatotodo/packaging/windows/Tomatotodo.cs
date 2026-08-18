using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Tomatotodo")]
[assembly: AssemblyDescription("Tomatotodo focus timer and task manager")]
[assembly: AssemblyCompany("Tomatotodo")]
[assembly: AssemblyProduct("Tomatotodo")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace TomatotodoDesktop
{
    internal static class Program
    {
        private const string AppUrl = "http://localhost:4173/";
        private const string MutexName = "Local\\Tomatotodo-Desktop-1.0";

        [STAThread]
        private static int Main(string[] args)
        {
            DpiAwareness.EnablePerMonitorV2();

            if (args.Length > 0 && string.Equals(args[0], "--verify", StringComparison.OrdinalIgnoreCase))
                return VerifyInstallation();

            bool ownsMutex;
            using (var mutex = new Mutex(true, MutexName, out ownsMutex))
            {
                if (!ownsMutex)
                {
                    NativeMethods.ActivateExistingWindow();
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www");
                using (var server = new StaticFileServer(webRoot, 4173))
                {
                    bool ownsServer = server.TryStart();
                    if (!ownsServer && !StaticFileServer.IsTomatotodoRunning(AppUrl))
                    {
                        MessageBox.Show("本地端口 4173 已被其他程序占用。", "Tomatotodo 无法启动", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 2;
                    }

                    using (var window = new MainWindow(AppUrl))
                    {
                        Application.AddMessageFilter(new FullscreenKeyFilter(window));
                        Application.Run(window);
                    }
                }
            }
            return 0;
        }

        private static int VerifyInstallation()
        {
            try
            {
                string root = AppDomain.CurrentDomain.BaseDirectory;
                string[] required = {
                    Path.Combine(root, "www", "index.html"),
                    Path.Combine(root, "www", "manifest.webmanifest"),
                    Path.Combine(root, "Microsoft.Web.WebView2.Core.dll"),
                    Path.Combine(root, "Microsoft.Web.WebView2.WinForms.dll"),
                    Path.Combine(root, "WebView2Loader.dll")
                };
                foreach (string path in required) if (!File.Exists(path)) return 10;
                return string.IsNullOrWhiteSpace(CoreWebView2Environment.GetAvailableBrowserVersionString()) ? 11 : 0;
            }
            catch { return 12; }
        }
    }

    internal sealed class FullscreenKeyFilter : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const int VkF12 = 0x7B;
        private readonly MainWindow window;
        private bool keyDown;

        public FullscreenKeyFilter(MainWindow window) { this.window = window; }

        public bool PreFilterMessage(ref Message message)
        {
            if ((message.Msg == WmKeyDown || message.Msg == WmSysKeyDown) && message.WParam.ToInt32() == VkF12)
            {
                if (!keyDown)
                {
                    keyDown = true;
                    window.BeginInvoke(new Action(window.ToggleFullscreen));
                }
                return true;
            }
            if (message.Msg == 0x0101 && message.WParam.ToInt32() == VkF12) keyDown = false;
            return false;
        }
    }

    internal sealed class MainWindow : Form
    {
        private readonly WebView2 webView;
        private readonly string appUrl;
        private readonly MiniTimerWindow miniTimer;
        private bool fullscreen;
        private FormBorderStyle savedBorder;
        private FormWindowState savedState;
        private Rectangle savedBounds;
        private bool savedTopMost;
        private IntPtr keyboardHook;
        private NativeMethods.LowLevelKeyboardProc keyboardProc;
        private bool f12Down;

        public MainWindow(string appUrl)
        {
            this.appUrl = appUrl;
            Text = "Tomatotodo";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1024, 640);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(11, 13, 11);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            miniTimer = new MiniTimerWindow(Icon);
            miniTimer.UserHidden += delegate {
                try { webView.CoreWebView2.PostWebMessageAsString("mini-window-hidden"); } catch { }
            };
            miniTimer.ToggleRequested += delegate {
                try { webView.CoreWebView2.PostWebMessageAsString("mini-window-toggle"); } catch { }
            };

            webView = new WebView2 {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.FromArgb(11, 13, 11)
            };
            Controls.Add(webView);
            Shown += InitializeWebView;
            HandleCreated += InstallKeyboardHook;
            HandleCreated += ApplySystemTitleBarTheme;
            FormClosed += RemoveKeyboardHook;
            FormClosed += delegate { miniTimer.Close(); };
        }

        private void ApplySystemTitleBarTheme(object sender, EventArgs e)
        {
            ApplyAppTheme(NativeWindowTheme.SystemUsesDarkApps());
        }

        private void ApplyAppTheme(bool useDarkMode)
        {
            Color background = useDarkMode ? Color.FromArgb(11, 16, 12) : Color.FromArgb(248, 246, 238);
            BackColor = background;
            webView.DefaultBackgroundColor = background;
            NativeWindowTheme.Apply(Handle, useDarkMode);
        }

        private void InstallKeyboardHook(object sender, EventArgs e)
        {
            if (keyboardHook != IntPtr.Zero) return;
            keyboardProc = HandleLowLevelKey;
            keyboardHook = NativeMethods.SetWindowsHookEx(13, keyboardProc, IntPtr.Zero, 0);
        }

        private void RemoveKeyboardHook(object sender, FormClosedEventArgs e)
        {
            if (keyboardHook == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(keyboardHook);
            keyboardHook = IntPtr.Zero;
        }

        private IntPtr HandleLowLevelKey(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && NativeMethods.IsAppForeground(Handle))
            {
                int virtualKey = Marshal.ReadInt32(data);
                int keyMessage = message.ToInt32();
                if (virtualKey == 0x7B)
                {
                    if ((keyMessage == 0x0100 || keyMessage == 0x0104) && !f12Down)
                    {
                        f12Down = true;
                        BeginInvoke(new Action(ToggleFullscreen));
                    }
                    if (keyMessage == 0x0101 || keyMessage == 0x0105) f12Down = false;
                    return new IntPtr(1);
                }
            }
            return NativeMethods.CallNextHookEx(keyboardHook, code, message, data);
        }

        private async void InitializeWebView(object sender, EventArgs e)
        {
            try
            {
                string userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tomatotodo", "WebView2");
                Directory.CreateDirectory(userData);
                var environment = await CoreWebView2Environment.CreateAsync(null, userData);
                await webView.EnsureCoreWebView2Async(environment);
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                webView.ZoomFactor = 1.0;
                webView.CoreWebView2.NewWindowRequested += HandleNewWindow;
                webView.CoreWebView2.WebMessageReceived += HandleWebMessage;
                webView.CoreWebView2.PermissionRequested += HandlePermissionRequested;
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    "window.addEventListener('keydown',function(event){"
                    + "if(event.key==='F12'){event.preventDefault();event.stopImmediatePropagation();"
                    + "window.chrome.webview.postMessage('toggle-fullscreen');}},true);");
                webView.CoreWebView2.Navigate(appUrl);
            }
            catch (Exception exception)
            {
                MessageBox.Show("Tomatotodo 无法载入 WebView2。\n\n" + exception.Message, "Tomatotodo 无法启动", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void HandlePermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            if (e.PermissionKind != CoreWebView2PermissionKind.FileReadWrite ||
                !e.Uri.StartsWith(appUrl, StringComparison.OrdinalIgnoreCase)) return;
            e.State = CoreWebView2PermissionState.Allow;
            e.SavesInProfile = true;
            e.Handled = true;
        }

        public void ToggleFullscreen()
        {
            if (!fullscreen)
            {
                savedBorder = FormBorderStyle;
                savedState = WindowState;
                savedBounds = Bounds;
                savedTopMost = TopMost;
                SuspendLayout();
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                TopMost = true;
                Bounds = Screen.FromControl(this).Bounds;
                ResumeLayout(true);
                fullscreen = true;
                return;
            }

            SuspendLayout();
            TopMost = savedTopMost;
            FormBorderStyle = savedBorder;
            Bounds = savedBounds;
            WindowState = savedState;
            ResumeLayout(true);
            fullscreen = false;
        }

        private void HandleWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                if (message == "toggle-fullscreen") BeginInvoke(new Action(ToggleFullscreen));
                if (message == "app-theme|dark") BeginInvoke(new Action(() => ApplyAppTheme(true)));
                if (message == "app-theme|light") BeginInvoke(new Action(() => ApplyAppTheme(false)));
                if (message == "mini-window|hide") BeginInvoke(new Action(miniTimer.Hide));
                if (message.StartsWith("mini-window|show|", StringComparison.Ordinal) || message.StartsWith("mini-window|update|", StringComparison.Ordinal))
                {
                    string[] parts = message.Split(new[] { '|' }, 6);
                    if (parts.Length >= 5)
                    {
                        string phase = parts[2];
                        string status = parts[3];
                        string time = parts[4];
                        double progress = 0;
                        if (parts.Length >= 6) double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out progress);
                        BeginInvoke(new Action(() => {
                            miniTimer.UpdateTimer(phase, status, time, progress);
                            if (parts[1] == "show" && !miniTimer.Visible) miniTimer.Show();
                        }));
                    }
                }
            }
            catch { }
        }

        private void HandleNewWindow(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            try { Process.Start(e.Uri); } catch { }
        }
    }

    internal sealed class MiniTimerWindow : Form
    {
        private readonly Label phaseLabel;
        private readonly MiniProgressButton statusLabel;
        private readonly Label timeLabel;
        public event EventHandler UserHidden;
        public event EventHandler ToggleRequested;

        public MiniTimerWindow(Icon icon)
        {
            Text = "Tomatotodo 小窗";
            Icon = icon;
            Width = 320;
            Height = 174;
            MinimumSize = new Size(260, 148);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(15, 31, 20);
            Padding = new Padding(22, 16, 22, 15);

            phaseLabel = new Label {
                AutoSize = true,
                ForeColor = Color.FromArgb(157, 196, 151),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold)
            };
            statusLabel = new MiniProgressButton {
                Width = 70,
                Height = 24,
                ForeColor = Color.FromArgb(170, 184, 173),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                Cursor = Cursors.Hand
            };
            statusLabel.Click += delegate {
                if (ToggleRequested != null) ToggleRequested(this, EventArgs.Empty);
            };
            timeLabel = new Label {
                AutoSize = true,
                ForeColor = Color.FromArgb(248, 246, 238),
                Font = new Font("Georgia", 34F, FontStyle.Regular)
            };

            var heading = new TableLayoutPanel { Dock = DockStyle.Top, Height = 28, ColumnCount = 2 };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            heading.Controls.Add(phaseLabel, 0, 0);
            heading.Controls.Add(statusLabel, 1, 0);
            statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            var timePanel = new Panel { Dock = DockStyle.Fill };
            timePanel.Controls.Add(timeLabel);
            timeLabel.Location = new Point(0, 16);
            Controls.Add(timePanel);
            Controls.Add(heading);

            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (e.CloseReason == CloseReason.UserClosing) {
                    e.Cancel = true;
                    Hide();
                    if (UserHidden != null) UserHidden(this, EventArgs.Empty);
                }
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 22, area.Bottom - Height - 22);
        }

        public void UpdateTimer(string phase, string status, string time, double progress)
        {
            phaseLabel.Text = phase;
            statusLabel.Text = status;
            statusLabel.Progress = Math.Max(0, Math.Min(1, progress / 100.0));
            timeLabel.Text = time;
        }
    }

    internal sealed class MiniProgressButton : Control
    {
        private double progress;
        private bool hovered;

        public double Progress
        {
            get { return progress; }
            set { progress = value; Invalidate(); }
        }

        public MiniProgressButton()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseEnter += delegate { hovered = true; Invalidate(); };
            MouseLeave += delegate { hovered = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF bounds = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
            float radius = Height / 2F;
            using (GraphicsPath track = RoundedPath(bounds, radius))
            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(18, 245, 246, 239)))
            using (Pen border = new Pen(Color.FromArgb(hovered ? 70 : 38, 124, 169, 130)))
            {
                e.Graphics.FillPath(trackBrush, track);
                if (Progress > 0)
                {
                    e.Graphics.SetClip(track);
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(hovered ? 48 : 34, 124, 169, 130)))
                        e.Graphics.FillRectangle(fill, 0, 0, (float)(Width * Progress), Height);
                    e.Graphics.ResetClip();
                }
                e.Graphics.DrawPath(border, track);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, hovered ? Color.FromArgb(232, 239, 229) : ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath RoundedPath(RectangleF rectangle, float radius)
        {
            float diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal static class DpiAwareness
    {
        private static readonly IntPtr PerMonitorV2 = new IntPtr(-4);

        public static void EnablePerMonitorV2()
        {
            try { if (SetProcessDpiAwarenessContext(PerMonitorV2)) return; } catch { }
            try { SetProcessDpiAwareness(2); } catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("shcore.dll")] private static extern int SetProcessDpiAwareness(int awareness);
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    }

    internal static class NativeWindowTheme
    {
        private const int ImmersiveDarkModeBefore20H1 = 19;
        private const int ImmersiveDarkMode = 20;
        private const int CaptionColor = 35;
        private const int TextColor = 36;

        public static void Apply(IntPtr window)
        {
            Apply(window, SystemUsesDarkApps());
        }

        public static void Apply(IntPtr window, bool useDarkMode)
        {
            int dark = useDarkMode ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(window, ImmersiveDarkMode, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(window, ImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
                int caption = ColorTranslator.ToWin32(useDarkMode ? Color.FromArgb(24, 31, 26) : Color.FromArgb(248, 246, 238));
                int text = ColorTranslator.ToWin32(useDarkMode ? Color.FromArgb(232, 238, 232) : Color.FromArgb(35, 40, 36));
                DwmSetWindowAttribute(window, CaptionColor, ref caption, sizeof(int));
                DwmSetWindowAttribute(window, TextColor, ref text, sizeof(int));
            }
            catch { }
        }

        public static bool SystemUsesDarkApps()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch { return true; }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
    }

    internal sealed class StaticFileServer : IDisposable
    {
        private readonly string root;
        private readonly int port;
        private TcpListener listener;
        private volatile bool running;

        public StaticFileServer(string root, int port)
        {
            this.root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            this.port = port;
        }

        public bool TryStart()
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                running = true;
                var thread = new Thread(ListenLoop) { IsBackground = true, Name = "Tomatotodo static server" };
                thread.Start();
                return true;
            }
            catch (SocketException) { return false; }
        }

        public static bool IsTomatotodoRunning(string appUrl)
        {
            try
            {
                using (var client = new WebClient())
                    return client.DownloadString(new Uri(new Uri(appUrl), "manifest.webmanifest")).IndexOf("Tomatotodo", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private void ListenLoop()
        {
            while (running)
            {
                try { ThreadPool.QueueUserWorkItem(HandleClient, listener.AcceptTcpClient()); }
                catch (SocketException) { if (!running) return; }
                catch (ObjectDisposedException) { return; }
            }
        }

        private void HandleClient(object state)
        {
            using (var client = (TcpClient)state)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                NetworkStream stream = client.GetStream();
                try
                {
                    string line;
                    using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                    {
                        line = reader.ReadLine();
                        string header;
                        do { header = reader.ReadLine(); } while (!string.IsNullOrEmpty(header));
                    }
                    if (string.IsNullOrWhiteSpace(line)) return;
                    string[] request = line.Split(' ');
                    if (request.Length < 2 || (request[0] != "GET" && request[0] != "HEAD")) return;
                    string path = ResolvePath(request[1]);
                    if (path == null) return;
                    if (!File.Exists(path)) path = Path.Combine(root, "index.html");
                    byte[] body = File.ReadAllBytes(path);
                    string name = Path.GetFileName(path);
                    bool noCache = name == "index.html" || name == "sw.js" || name == "manifest.webmanifest";
                    string headers = "HTTP/1.1 200 OK\r\nContent-Type: " + ContentType(path) + "\r\nContent-Length: " + body.Length
                        + "\r\nCache-Control: " + (noCache ? "no-cache" : "public, max-age=31536000, immutable") + "\r\nConnection: close\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    if (request[0] == "GET") stream.Write(body, 0, body.Length);
                }
                catch { }
            }
        }

        private string ResolvePath(string target)
        {
            string decoded = Uri.UnescapeDataString(target.Split('?')[0]).TrimStart('/');
            if (string.IsNullOrEmpty(decoded) || decoded.EndsWith("/")) decoded += "index.html";
            string candidate = Path.GetFullPath(Path.Combine(root, decoded.Replace('/', Path.DirectorySeparatorChar)));
            return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? candidate : null;
        }

        private static string ContentType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".css": return "text/css; charset=utf-8";
                case ".html": return "text/html; charset=utf-8";
                case ".ico": return "image/x-icon";
                case ".js": return "text/javascript; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".png": return "image/png";
                case ".svg": return "image/svg+xml";
                case ".webmanifest": return "application/manifest+json; charset=utf-8";
                case ".webp": return "image/webp";
                default: return "application/octet-stream";
            }
        }

        public void Dispose() { running = false; if (listener != null) listener.Stop(); }
    }

    internal static class NativeMethods
    {
        public delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int hook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

        public static bool IsAppForeground(IntPtr appWindow)
        {
            IntPtr foreground = GetForegroundWindow();
            return foreground == appWindow || GetAncestor(foreground, 2) == appWindow;
        }

        public static void ActivateExistingWindow()
        {
            IntPtr window = FindWindow(null, "Tomatotodo");
            if (window == IntPtr.Zero) return;
            ShowWindow(window, 9);
            SetForegroundWindow(window);
        }
    }
}
