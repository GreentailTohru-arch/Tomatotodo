using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Tomatotodo Setup")]
[assembly: AssemblyDescription("Tomatotodo 1.2.0 番茄专注与任务管理安装程序")]
[assembly: AssemblyCompany("Tomatotodo")]
[assembly: AssemblyProduct("Tomatotodo")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace TomatotodoSetup
{
    internal static class Installer
    {
        private const string ResourceName = "TomatotodoPayload.zip";

        [STAThread]
        private static int Main()
        {
            DpiAwareness.EnablePerMonitorV2();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (MessageBox.Show(
                "安装 Tomatotodo 1.2.0\n\n安装范围：当前用户\n快捷方式：桌面、开始菜单\n升级安装会保留本地任务、设置与专注记录。\n\n是否继续？",
                "安装 Tomatotodo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information) != DialogResult.Yes) return 0;

            string installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Tomatotodo");
            string tempRoot = Path.Combine(Path.GetTempPath(), "Tomatotodo-Install-" + Guid.NewGuid().ToString("N"));
            try
            {
                StopRunningApp();
                Directory.CreateDirectory(tempRoot);
                string payloadPath = Path.Combine(tempRoot, "payload.zip");
                ExtractPayloadResource(payloadPath);
                ZipFile.ExtractToDirectory(payloadPath, tempRoot);
                string sourceRoot = Path.Combine(tempRoot, "Tomatotodo");
                if (!File.Exists(Path.Combine(sourceRoot, "Tomatotodo.exe"))) throw new InvalidDataException("安装包内容不完整。");
                Directory.CreateDirectory(installRoot);
                CopyDirectory(sourceRoot, installRoot);
                CreateShortcuts(installRoot);
                RegisterUninstaller(installRoot);
                MessageBox.Show("Tomatotodo 1.2.0 安装完成。\n\n按 F12 可进入或退出全屏。", "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Process.Start(new ProcessStartInfo { FileName = Path.Combine(installRoot, "Tomatotodo.exe"), WorkingDirectory = installRoot, UseShellExecute = true });
                return 0;
            }
            catch (Exception exception)
            {
                MessageBox.Show("未能完成安装。\n\n" + exception.Message, "Tomatotodo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void ExtractPayloadResource(string destination)
        {
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            {
                if (input == null) throw new InvalidDataException("找不到安装资源。");
                using (FileStream output = File.Create(destination)) input.CopyTo(output);
            }
        }

        private static void StopRunningApp()
        {
            foreach (Process process in Process.GetProcessesByName("Tomatotodo"))
            {
                try { process.CloseMainWindow(); if (!process.WaitForExit(1500)) process.Kill(); } catch { } finally { process.Dispose(); }
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        private static void CreateShortcuts(string installRoot)
        {
            string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Tomatotodo");
            Directory.CreateDirectory(startMenu);
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new InvalidOperationException("无法创建 Windows 快捷方式。");
            dynamic shell = Activator.CreateInstance(shellType);
            string executable = Path.Combine(installRoot, "Tomatotodo.exe");
            CreateShortcut(shell, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Tomatotodo.lnk"), executable, "", installRoot, executable + ",0");
            CreateShortcut(shell, Path.Combine(startMenu, "Tomatotodo.lnk"), executable, "", installRoot, executable + ",0");
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            CreateShortcut(shell, Path.Combine(startMenu, "卸载 Tomatotodo.lnk"), powershell, "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(installRoot, "Uninstall-Tomatotodo.ps1") + "\"", installRoot, executable + ",0");
        }

        private static void CreateShortcut(dynamic shell, string path, string target, string arguments, string workingDirectory, string icon)
        {
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = target;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = icon;
            shortcut.Description = "Tomatotodo 番茄专注与任务清单";
            shortcut.Save();
        }

        private static void RegisterUninstaller(string installRoot)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Tomatotodo"))
            {
                if (key == null) throw new InvalidOperationException("无法写入卸载信息。");
                string executable = Path.Combine(installRoot, "Tomatotodo.exe");
                key.SetValue("DisplayName", "Tomatotodo", RegistryValueKind.String);
                key.SetValue("DisplayVersion", "1.2.0", RegistryValueKind.String);
                key.SetValue("Publisher", "Tomatotodo", RegistryValueKind.String);
                key.SetValue("DisplayIcon", executable, RegistryValueKind.String);
                key.SetValue("InstallLocation", installRoot, RegistryValueKind.String);
                key.SetValue("UninstallString", "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(installRoot, "Uninstall-Tomatotodo.ps1") + "\"", RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
    }

    internal static class DpiAwareness
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [System.Runtime.InteropServices.DllImport("shcore.dll")] private static extern int SetProcessDpiAwareness(int value);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        public static void EnablePerMonitorV2()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
            try { SetProcessDpiAwareness(2); } catch { }
            try { SetProcessDPIAware(); } catch { }
        }
    }
}
