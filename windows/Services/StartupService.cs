using Microsoft.Win32;

namespace Tomatotodo_Windows.Services;

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("Tomatotodo") is string; }
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (!enabled) { key.DeleteValue("Tomatotodo", false); return; }
        string command;
        try
        {
            var appId = Windows.ApplicationModel.Package.Current.Id.FamilyName + "!App";
            command = $"\"{Environment.GetFolderPath(Environment.SpecialFolder.Windows)}\\explorer.exe\" shell:AppsFolder\\{appId}";
        }
        catch { command = $"\"{Environment.ProcessPath}\""; }
        key.SetValue("Tomatotodo", command);
    }
}
